using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

internal sealed record CoaxialPrimitiveSpan(string Name, string Kind, double BottomRadius, double TopRadius,
    double From, double To, FirmamentV2SourceSpan SourceSpan);
internal sealed record CoaxialRimChamfer(string Name, string On, double Distance, FirmamentV2SourceSpan SourceSpan);
internal sealed record CoaxialPrimitiveComposition(string Name, IReadOnlyList<CoaxialPrimitiveSpan> Spans,
    IReadOnlyList<CoaxialRimChamfer> Chamfers, RevolvedProfileTopologyPlan Topology);

public static partial class FirmamentV2Parser
{
    // A production of the ordinary Compose owner, selected by its child AST nodes.
    // No source rewrite, Boolean union or separate whole-document grammar.
    private static bool IsCoaxialComposition(DeclarationSyntax? compose) => compose?.Type.Name.Text == "Compose"
        && compose.Body?.Statements.OfType<DeclarationSyntax>().Any(d => d.Type.Name.Text is "Cylinder" or "Frustum") == true;

    private static FirmamentV2ParseResult BindCoaxialComposition(DeclarationSyntax model, DeclarationSyntax compose,
        string source, List<string> diagnostics, IReadOnlyList<FirmamentV2BoundLet> values)
    {
        var located = new List<FirmamentV2ValidationDiagnostic>();
        void Error(string code, string message, FirmamentV2SourceSpan span)
        {
            diagnostics.Add(code);
            located.Add(new(code, "error", message) { SourceSpan = span, Stage = "Capability" });
        }
        const string prefix = "firmament-compose-coaxial-";
        var units = model.Body!.Statements.OfType<FieldSyntax>().Where(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Units")).ToArray();
        if (model.Type.Name.Text == "Model" && units.Length == 0 || units.Any(f => FirmamentSyntaxValues.Reference(f.Value) != "mm"))
            diagnostics.Add(CanonicalUnitsInvalid);
        if (values.Any(v => v.Tolerance is not null))
            Error(prefix + "tolerance-consumer-unsupported", "Coaxial primitive Compose does not yet project typed tolerances; nominal substitution cannot discard them.", compose.Span);
        var members = compose.Body?.Statements.OfType<DeclarationSyntax>().ToArray() ?? [];
        var fields = new FirmamentSyntaxFields(compose, diagnostics);
        fields.Complete(members.ToHashSet<StatementSyntax>());
        if (compose.Name is null || compose.Type.Arguments.Count > 0 || compose.Modifiers.Count > 0) diagnostics.Add(CanonicalPrimitiveMalformed);
        var spans = new List<CoaxialPrimitiveSpan>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in members)
        {
            if (member.Type.Name.Text is not ("Cylinder" or "Frustum"))
            { Error(prefix + "operation-unsupported", $"Coaxial Compose requires Cylinder/Frustum spans; '{member.Type.Name.Text}' is not consumed.", member.Span); continue; }
            var f = new FirmamentSyntaxFields(member, diagnostics);
            var on = f.Reference("On");
            var primitive = BindMechanicalPrimitive(f, diagnostics);
            f.Complete();
            if (member.Name is null || !names.Add(member.Name.Text) || member.Type.Arguments.Count > 0 || member.Modifiers.Count > 0)
            { diagnostics.Add(DuplicateName); continue; }
            var (r0, r1, height) = primitive switch
            {
                FirmamentV2CylinderRecord c => (c.Radius, c.Radius, c.Height),
                FirmamentV2FrustumRecord c => (c.BottomRadius, c.TopRadius, c.Height),
                _ => (double.NaN, double.NaN, double.NaN)
            };
            if (!double.IsFinite(r0 + r1 + height) || !(r0 > 0 && r1 > 0 && height > 0))
            { Error(prefix + "dimensions-invalid", "Use finite positive radii and height. A Frustum also requires unequal end radii; use Cylinder for equal radii.", member.Span); continue; }
            var previous = spans.LastOrDefault();
            if (previous is null ? on is not null : on != previous.Name + ".Top")
                Error(prefix + "support-invalid", previous is null ? "The first span starts at the local origin; omit on."
                    : $"Attach this span with on: {previous.Name}.Top. Arbitrary placement and out-of-order attachments are unsupported.", member.Span);
            if (previous is not null && Math.Abs(previous.TopRadius - r0) > 1e-9)
                Error(prefix + "junction-radius-mismatch", "Adjacent spans must share the same rim radius. Radial shoulders, overlaps and gaps are not admitted by this stack.", member.Span);
            var from = previous?.To ?? 0;
            spans.Add(new(member.Name.Text, member.Type.Name.Text, r0, r1, from, from + height, member.Span));
        }
        var declarations = MechanicalDeclarations(model.Body.Statements).ToArray();
        var modifies = declarations.Where(d => d.Type.Name.Text == "Modify").ToArray();
        var remainder = declarations.Where(d => d != compose && !modifies.Contains(d) && d.Type.Name.Text != "Static"
            && !(d.Type.Name.Text == "Struct" && !d.Modifiers.Any(m => m.Name.Text == "Concept"))).Cast<StatementSyntax>().ToArray();
        BindMaterialDomainScope(new("inline", remainder, [], model.Span), "Compose", diagnostics);
        foreach (var unsupported in declarations.Where(d => d.Type.Name.Text is "Pmi" or "Selection" or "Thread" or "Assert"
            or "Boss" or "Pocket" or "AddOffset" or "RemoveOffset" or "Recognize" or "Replace" or "Remove" or "Pattern"
            || d.Modifiers.Any(m => m.Name.Text == "Assert")))
            Error(prefix + "consumer-unsupported", $"Coaxial Compose does not yet consume '{unsupported.Type.Name.Text}'; remove it or use a qualified owner.", unsupported.Span);
        var chamfers = new List<CoaxialRimChamfer>();
        var ends = new HashSet<string>(StringComparer.Ordinal);
        foreach (var modify in modifies)
        {
            var finishes = modify.Body?.Statements.OfType<DeclarationSyntax>().ToArray() ?? [];
            new FirmamentSyntaxFields(modify, diagnostics).Complete(finishes.ToHashSet<StatementSyntax>());
            if (modify.Name?.Text != compose.Name?.Text || modify.Type.Arguments.Count > 0 || modify.Modifiers.Count > 0)
                diagnostics.Add(ModifyTargetUnresolved);
            foreach (var finish in finishes)
            {
                if (finish.Type.Name.Text != "EdgeFinish")
                { Error(prefix + "finish-unsupported", $"'{finish.Type.Name.Text}' is not an admitted coaxial rim finish.", finish.Span); continue; }
                var f = new FirmamentSyntaxFields(finish, diagnostics);
                var kind = MechanicalFinishKind(f, diagnostics);
                var on = f.Reference("On");
                var target = f.Reference("Target");
                var distance = f.Number("Distance", "mm");
                f.Complete();
                var span = spans.FirstOrDefault(s => on == s.Name + ".Bottom" && s == spans.FirstOrDefault()
                    || on == s.Name + ".Top" && s == spans.LastOrDefault());
                if (kind != "Chamfer" || target != "Boundary" || span?.Kind != "Cylinder" || finish.Name is null || !names.Add(finish.Name.Text)
                    || finish.Modifiers.Count > 0 || !(distance > 0 && distance < span.BottomRadius && distance < span.To - span.From)
                    || !ends.Add(on!))
                { Error(prefix + "chamfer-unsupported", "Use EdgeFinish<Chamfer> on an exposed terminal Cylinder rim: on: shaft.Bottom (or shaft.Top); target: Boundary; distance: a positive length smaller than radius and height. One finish per terminal is supported.", finish.Span); continue; }
                chamfers.Add(new(finish.Name.Text, on!, distance, finish.Span));
            }
        }
        if (spans.Count == 0 || diagnostics.Any(IsFatalDiagnosticCode)) return Failure();
        var points = new List<ProfilePoint2D> { new(spans[0].BottomRadius, spans[0].From) };
        points.AddRange(spans.Select(s => new ProfilePoint2D(s.TopRadius, s.To)));
        foreach (var chamfer in chamfers)
        {
            if (chamfer.On.EndsWith(".Bottom", StringComparison.Ordinal))
            {
                var first = points[0]; points[0] = new(first.X - chamfer.Distance, first.Y);
                points.Insert(1, new(first.X, first.Y + chamfer.Distance));
            }
            else
            {
                var last = points[^1]; points[^1] = new(last.X - chamfer.Distance, last.Y);
                points.Insert(points.Count - 1, new(last.X, last.Y - chamfer.Distance));
            }
        }
        var axis = new Vector3D(0, 0, 1);
        var topology = RevolvedProfileStackEmitter.Plan(points, new(new(0, 0, 0), Direction3D.Create(axis), Direction3D.Create(new Vector3D(1, 0, 0))), new(new(0, 0, 0), axis));
        if (!topology.IsSuccess)
        { Error(prefix + "stack-invalid", string.Join("; ", topology.Diagnostics.Select(d => d.Message)), compose.Span); return Failure(); }
        var composition = new CoaxialPrimitiveComposition(compose.Name!.Text, spans, chamfers, topology.Value);
        var document = new FirmamentV2Document(model.Name!.Text, "mm", [new(compose.Name.Text, "Compose", new FirmamentV2AdvancedMaterialRecord("Compose"))],
            Composes: [new(compose.Name.Text, spans.Select(s => s.Name).Concat(chamfers.Select(c => c.Name)).ToArray(), compose.Span)]);
        return FirmamentV2ParseResult.Success(document, diagnostics.Append("firmament-v2-parse-succeeded").ToArray())
            with { BoundCoaxialComposition = composition, BoundSource = source };

        FirmamentV2ParseResult Failure() => CanonicalFailure(diagnostics, CanonicalDocumentMalformed) with { SyntaxDiagnostics = located };
    }
}
