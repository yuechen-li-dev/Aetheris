using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record SectionChainAuthoringResult(
    bool IsSuccess,
    SectionChain? Chain,
    SectionChainMaterializationResult? Materialization,
    IReadOnlyList<string> Diagnostics,
    LoftAuthoredBinding? LoftBinding = null,
    IReadOnlyList<SectionChainProfileDerivation>? ProfileDerivations = null,
    IReadOnlyList<SectionChainBoundaryEditInspection>? BoundaryEdits = null,
    IReadOnlyList<FirmamentV2CanonicalPatternDecl>? Patterns = null);

public sealed record SectionChainBoundaryEditInspection(string Section, string Profile,
    IReadOnlyList<ProfileBoundaryEditEvidence> Edits);

public sealed record SectionChainProfileDerivation(string Section, string Profile, string ConceptCurve,
    string SourceSegment, string Transform, string Plane);

public sealed record LoftAuthoredBinding(string Name, string RearProfile, string RearFrame,
    string FrontProfile, string FrontFrame, IReadOnlyList<double> Reference, double TwistDegrees,
    FirmamentV2SourceSpan ConstructSpan, IReadOnlyList<FirmamentV2AuthoredField> Fields);

/// <summary>
/// Domain binder for Firmament SectionChain declarations. Profiles remain ordinary
/// named Profiles; this layer only associates them with ordered frames,
/// seams, correspondence, transition law, and termination intent.
/// </summary>
public static class SectionChainAuthoringParser
{
    public static SectionChainAuthoringResult CompileFile(string path, bool materialize = true)
    {
        var errors = new List<Assembly.AssemblyDiagnostic>();
        var source = Assembly.AssemblyM0Parser.LoadResource(path, null, [], errors);
        return source is null || errors.Count != 0 ? new(false, null, null, errors.Select(d => d.Code + ":" + d.Message).ToArray())
            : Compile(source, materialize);
    }
    public static bool IsSectionChainSource(string source)
    {
        var declarations = FirmamentV2Parser.MechanicalDeclarations(FirmamentSyntaxParser.Parse(source).Document.Statements).ToArray();
        var sculpting = declarations.Any(d => d.Type.Name.Text is "BodyState" or "SculptState");
        // Surfacing owns its Station-shaped construction chains. Do not steal
        // their containing document; ordinary Section/Loft productions still
        // claim mixed documents so unsupported material cannot disappear.
        return declarations.Any(d => d.Type.Name.Text == "Loft" || d.Type.Name.Text == "SectionChain"
            && (!sculpting || d.Body?.Statements.OfType<DeclarationSyntax>().Any(s => s.Type.Name.Text == "Station") != true));
    }

    public static SectionChainAuthoringResult Compile(string source, bool materialize = true)
    {
        using var buildPerfPhase = Aetheris.Kernel.Core.Diagnostics.BuildPerfTrace.Phase("sectionchain.authoring-compile");
        ArgumentNullException.ThrowIfNull(source);
        var frontend = FirmamentFrontendSchemas.Select(source);
        if (!frontend.IsSuccess) return new(false, null, null, frontend.Diagnostics);
        source = frontend.Source;
        source = FirmamentSourceSpelling.Normalize(source);
        if (LoftAuthoringParser.IsLoftSource(source))
            return LoftAuthoringParser.Compile(source, materialize);
        var diagnostics = new List<string>();
        var boundaries = ClosedBoundary2Authoring.Expand(source, diagnostics);
        if (boundaries is null || diagnostics.Count > 0) return new(false, null, null, diagnostics);
        source = boundaries.Source;
        var staticSource = CanonicalStaticAuthoring.Expand(source, diagnostics);
        if (staticSource is null || diagnostics.Count > 0) return new(false, null, null, diagnostics);
        source = ExpandSectionValues(staticSource.Source, diagnostics);
        var templates = FirmamentV2TemplateExpansion.Expand(source, diagnostics);
        if (templates is null || diagnostics.Count > 0) return new(false, null, null, diagnostics);
        source = templates.Source;
        var syntax = FirmamentSyntaxParser.Parse(source);
        FirmamentV2Parser.BindMaterialDomainScope(syntax.Document, "SectionChain", diagnostics);
        diagnostics.AddRange(syntax.Diagnostics.Select(d => d.Code + ": " + d.Message));
        if (!FirmamentSyntaxTraversal.Statements(syntax.Document).OfType<FieldSyntax>().Any(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Units") && FirmamentSyntaxValues.Reference(f.Value) == "mm"))
            diagnostics.Add("section-chain-units-invalid:millimetres-required");
        var declarations = FirmamentV2Parser.MechanicalDeclarations(syntax.Document.Statements).Where(d => d.Type.Name.Text == "SectionChain").ToArray();
        if (declarations.Length != 1) return Fail(diagnostics.Append($"section-chain-count-invalid:expected=1:actual={declarations.Length}"));
        var declaration = declarations[0];
        var constructionStatements = declaration.Body?.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text is not ("Section" or "Correspond")).Cast<StatementSyntax>().ToArray() ?? [];
        FirmamentV2Parser.BindMaterialDomainScope(syntax.Document with { Statements = constructionStatements }, "SectionChain", diagnostics);
        var fields = new FirmamentSyntaxFields(declaration with { Body = declaration.Body is null ? null : declaration.Body with
            { Statements = declaration.Body.Statements.Where(s => s is not DeclarationSyntax).ToArray() } }, diagnostics);
        var profiles = ProfileAuthoringParser.BindPathDerivedProfiles(source, diagnostics);
        var authoredTransition = fields.Reference("Transition");
        var continuityText = fields.Reference("Continuity") ?? (authoredTransition == "Ruled" ? "G0" : "G1");
        if (!Enum.TryParse<SectionChainContinuity>(continuityText, false, out var continuity)) diagnostics.Add($"section-chain-continuity-invalid:{continuityText}");
        var transitionText = authoredTransition ?? (continuity == SectionChainContinuity.G0 ? "Ruled" : "SmoothPolynomial");
        if (!Enum.TryParse<SectionTransitionPolicy>(transitionText, false, out var transition)) diagnostics.Add($"section-chain-transition-policy-invalid:{transitionText}");
        var startText = fields.Reference("Start") ?? "Open"; var endText = fields.Reference("End") ?? "Open";
        if (!Enum.TryParse<SectionTermination>(startText, false, out var start)) diagnostics.Add($"section-chain-termination-invalid:Start:{startText}");
        if (!Enum.TryParse<SectionTermination>(endText, false, out var end)) diagnostics.Add($"section-chain-termination-invalid:End:{endText}");

        var sections = new List<Section>();
        var derivations = new List<SectionChainProfileDerivation>();
        var boundaryEdits = new List<SectionChainBoundaryEditInspection>();
        foreach (var authored in declaration.Body?.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Section") ?? [])
        {
            if (authored.Name is null || authored.Type.Arguments.Count > 0 || authored.Modifiers.Count > 0)
            { diagnostics.Add("section-chain-section-header-invalid"); continue; }
            var sectionFields = new FirmamentSyntaxFields(authored, diagnostics);
            var frameName = sectionFields.Reference("Frame");
            var profileName = sectionFields.Reference("Profile");
            var seam = sectionFields.Reference("Seam");
            sectionFields.Complete();
            if (frameName is null) diagnostics.Add($"section-chain-frame-missing:{authored.Name!.Text}");
            ResolvedProfile2D? profile = null;
            if (profileName is not null && !profiles.TryGetValue(profileName, out profile))
            {
                profile = ProfileAuthoringParser.ResolveNamedProfile(source, profileName, out var profileDiagnostics);
                if (profile is null) diagnostics.AddRange(profileDiagnostics);
            }
            if (profile is null)
                diagnostics.Add($"section-chain-profile-unresolved:{authored.Name!.Text}:{profileName ?? "<missing>"}");
            var plane = frameName is null ? null : ProfileAuthoringParser.ResolveNamedConstructionPlane(source, frameName, diagnostics);
            if (frameName is not null && plane is null) diagnostics.Add($"section-chain-frame-unresolved:{authored.Name!.Text}:{frameName}");
            if (plane is null || profile is null) continue;
            var outer = profile.Loops.Single(loop => loop.IsOuter);
            if (profile.BoundaryEdits is { Count: > 0 }) boundaryEdits.Add(new(authored.Name!.Text, profile.Name, profile.BoundaryEdits));
            if (outer.Segments.FirstOrDefault()?.Provenance is { } provenance
                && provenance.ConceptStableId.StartsWith("concept-curve:", StringComparison.Ordinal))
                derivations.Add(new(authored.Name!.Text, profile.Name, provenance.ConceptStableId,
                    provenance.TracedFrom ?? "", provenance.Derivation, profile.PlaneFrame));
            if (profile.Loops.Count != 1) diagnostics.Add($"section-chain-profile-loop-count-invalid:{authored.Name!.Text}");
            var spans = outer.Segments.Select(segment => Convert(segment, diagnostics, authored.Name!.Text)).Where(item => item is not null).Cast<SectionProfileSpan>().ToArray();
            var seamId = seam ?? spans.FirstOrDefault()?.SpanId ?? string.Empty;
            sections.Add(new(authored.Name!.Text, new(plane.Origin, plane.AxisX, plane.AxisY, plane.AxisZ), new(profile.Name, spans, seamId)));
        }
        if (sections.Count < 2) diagnostics.Add("section-chain-section-count-invalid:minimum=2");

        var correspondence = new List<AdjacentSectionCorrespondence>();
        foreach (var authored in declaration.Body?.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Correspond") ?? [])
        {
            if (authored.Type.Arguments.Count > 0 || authored.Modifiers.Count > 0) diagnostics.Add("section-chain-correspondence-header-invalid");
            var mappingStatements = authored.Body?.Statements.OfType<ExpressionStatementSyntax>().ToArray() ?? [];
            var mappingFields = new FirmamentSyntaxFields(authored with { Body = authored.Body is null ? null : authored.Body with
                { Statements = authored.Body.Statements.Where(s => s is not ExpressionStatementSyntax).ToArray() } }, diagnostics);
            var from = mappingFields.Reference("From"); var to = mappingFields.Reference("To");
            mappingFields.Complete();
            if (from is null || to is null) { diagnostics.Add($"section-chain-correspondence-endpoint-missing:{authored.Name?.Text}"); continue; }
            var mappings = new List<SectionSpanCorrespondence>();
            foreach (var mapping in mappingStatements)
            {
                if (mapping.Value is BinarySyntax { Operator: BinaryOperator.Direction, Left: ReferenceSyntax left, Right: ReferenceSyntax right })
                    mappings.Add(new(left.Name.Text, right.Name.Text));
                else diagnostics.Add($"section-chain-correspondence-invalid:{authored.Name?.Text}");
            }
            foreach (var duplicate in mappings.GroupBy(item => item.TargetSpanId, StringComparer.Ordinal).Where(g => g.Count() > 1))
                diagnostics.Add($"section-chain-correspondence-duplicate:{from}:{to}:{duplicate.Key}");
            correspondence.Add(new(from, to, mappings));
        }
        var tolerance = fields.Number("ApproximationTolerance", "mm", 1e-5);
        if (!(tolerance > 0)) diagnostics.Add("section-chain-normalization-tolerance-invalid:positive-length-required");
        fields.Complete();
        if (declaration.Type.Arguments.Count > 0 || declaration.Modifiers.Count > 0 || declaration.Name is null)
            diagnostics.Add("section-chain-header-invalid");
        if (diagnostics.Count != 0) return Fail(diagnostics);
        var chain = new SectionChain(declaration.Name!.Text, sections, correspondence, transition, start, end, continuity, ProfileApproximationTolerance: tolerance);
        if (!materialize) return new(true, chain, null, [], ProfileDerivations: derivations, BoundaryEdits: boundaryEdits, Patterns: staticSource.Document?.Patterns);
        var result = SectionChainMaterializer.Materialize(chain);
        if (!result.IsSuccess)
            diagnostics.AddRange(result.Diagnostics.Select(item => $"{item.Code}:{item.Message}"));
        return new(result.IsSuccess, chain, result, diagnostics, ProfileDerivations: derivations, BoundaryEdits: boundaryEdits, Patterns: staticSource.Document?.Patterns);

        SectionChainAuthoringResult Fail(IEnumerable<string> items) => new(false, null, null, items.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static string ExpandSectionValues(string source, List<string> diagnostics)
    {
        // Retain the existing inline-value lowering and its identities. Syntax owns
        // exact declaration/field boundaries, so no field suffix or sibling is lost.
        var syntax = FirmamentSyntaxParser.Parse(source);
        diagnostics.AddRange(syntax.Diagnostics.Select(d => d.Code + ": " + d.Message));
        var changes = new List<(int Start, int Length, string Text)>();
        foreach (var section in FirmamentSyntaxTraversal.Declarations(syntax.Document).Where(d => d.Type.Name.Text == "Section"))
        {
            if (section.Name is null) { diagnostics.Add("section-chain-section-malformed"); continue; }
            var name = section.Name.Text;
            var prelude = "";
            foreach (var field in section.Body?.Statements.OfType<FieldSyntax>() ?? [])
            {
                if (FirmamentSyntaxFields.SameField(field.Name.Text, "Frame") && field.Value is ObjectSyntax { Type: ReferenceSyntax { Name.Text: "Plane" } } plane)
                {
                    var concept = "__SectionDatum_" + name; var construction = "__SectionFrame_" + name;
                    prelude += $"Concept Struct {concept} {{ Support: Plane {Slice(plane.Body.Span)} }}\nConstruction Plane {construction} {{ Trace: {concept}.Support }}\n";
                    changes.Add((field.Value.Span.Start, field.Value.Span.Length, construction));
                }
                else if (FirmamentSyntaxFields.SameField(field.Name.Text, "Profile") && field.Value is SpecializationSyntax profile)
                {
                    var profileName = "__SectionProfile_" + name;
                    prelude += $"Profile {profileName} = {Slice(profile.Span)}\n";
                    changes.Add((field.Value.Span.Start, field.Value.Span.Length, profileName));
                }
            }
            if (prelude.Length > 0) changes.Add((section.Span.Start, 0, prelude));
        }
        foreach (var change in changes.OrderByDescending(c => c.Start)) source = source.Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        return source;
        string Slice(FirmamentV2SourceSpan span) => source.Substring(span.Start, span.Length);
    }

    private static SectionProfileSpan? Convert(ResolvedProfileSegment2D segment, List<string> diagnostics, string section) => segment.Geometry switch
    {
        LineArcCubicBezier2D cubic => new(segment.Name, new SectionProfileCurve.PolynomialBSpline(3, [new(cubic.Start.X, cubic.Start.Y), new(cubic.Control1.X, cubic.Control1.Y), new(cubic.Control2.X, cubic.Control2.Y), new(cubic.End.X, cubic.End.Y)], [4, 4], [0, 1])),
        LineArcLineSegment2D line => new(segment.Name, new SectionProfileCurve.Line(new(line.Start.X, line.Start.Y), new(line.End.X, line.End.Y))),
        LineArcCircularArc2D arc => new(segment.Name, new SectionProfileCurve.Arc(new(arc.Center.X, arc.Center.Y), arc.Radius, arc.StartAngleRadians, arc.SweepAngleRadians)),
        _ => Unsupported(segment, diagnostics, section)
    };

    private static SectionProfileSpan? Unsupported(ResolvedProfileSegment2D segment, List<string> diagnostics, string section)
    {
        diagnostics.Add($"section-chain-profile-curve-unsupported:{section}:{segment.Name}:{segment.Geometry.GetType().Name}");
        return null;
    }

}
