using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public static partial class FirmamentV2Parser
{
    private static bool BindCompatibilityScope(DocumentSyntax syntax, DeclarationSyntax? model, List<string> diagnostics)
    {
        var core = true;
        if (model?.Body is null) return core;
        if (model.Clauses.Count > 0 || model.Type.Arguments.Count > 0 || model.Parameters.Count > 0 || model.Annotation is not null || model.Initializer is not null || model.ResultType is not null || model.Occurrence)
            diagnostics.Add(CanonicalDocumentMalformed);
        foreach (var statement in syntax.Statements.Where(s => !ReferenceEquals(s, model)).Concat(model.Body.Statements))
        {
            switch (statement)
            {
                case DirectiveSyntax { Kind: DirectiveKind.Schema }: break;
                case DirectiveSyntax directive: diagnostics.Add($"{CanonicalDeclarationUnknown}:Model:{directive.Kind}"); break;
                case FieldSyntax field when FirmamentSyntaxFields.SameField(field.Name.Text, "Units"): break;
                case DeclarationSyntax { Type.Name.Text: "solid" or "modify" or "Modify" or "MODIFY" }: break;
                // Explicit existing semantic owners. These are not qualified as
                // whole-document AST admission until their remaining grammar moves.
                case DeclarationSyntax { Type.Name.Text: "let" or "template" or "pmi" or "Pmi" or "recognize" or "replace" or "manufacturing" or "feature" or "region" or "fill" }:
                    core = false; break;
                case ReplacementSyntax:
                case DeclarationSyntax declaration when declaration.Modifiers.Any(m => m.Name.Text == "manufacturing"):
                    core = false; break; // same existing extension owner, now structurally represented
                case FieldSyntax field: diagnostics.Add($"{CanonicalFieldUnknown}:Model:{field.Name.Text}"); break;
                default: diagnostics.Add($"{CanonicalDeclarationUnknown}:Model:{(statement as DeclarationSyntax)?.Type.Name.Text ?? "expression"}"); break;
            }
        }
        return core;
    }

    private static IReadOnlyList<FirmamentV2ModifyBlock> BindCompatibilityModifies(SyntaxParseResult syntax,
        DeclarationSyntax? model, Dictionary<string, FirmamentV2SolidBinding> solids, List<string> diagnostics)
    {
        var result = new List<FirmamentV2ModifyBlock>();
        foreach (var modify in model?.Body?.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text is "modify" or "Modify" or "MODIFY") ?? [])
        {
            if (modify.Name is null || !solids.TryGetValue(modify.Name.Text, out var stock)) { diagnostics.Add(ModifyTargetUnresolved); continue; }
            if (stock.Box is null) { diagnostics.Add(ModifyTargetNotSolid); continue; }
            var holes = new List<FirmamentV2SemanticHoleDecl>();
            var regions = new List<FirmamentV2RegionDecl>();
            foreach (var error in syntax.Diagnostics.Where(error => error.SourceSpan is { } span &&
                span.Start >= modify.Span.Start && span.Start <= modify.Span.Start + modify.Span.Length)) diagnostics.Add(error.Code);
            if (modify.Body is null || modify.Annotation is not null || modify.Clauses.Count > 0 || modify.Parameters.Count > 0 || modify.Type.Arguments.Count > 0 || modify.Initializer is not null || modify.ResultType is not null || modify.Modifiers.Count > 0 || modify.Occurrence)
                diagnostics.Add(CanonicalModifyMalformed);
            foreach (var statement in modify.Body?.Statements ?? [])
            {
                if (statement is DeclarationSyntax { Type.Name.Text: "hole" or "Hole" } hole)
                {
                    var fields = new FirmamentSyntaxFields(hole, diagnostics, implicitUnit: "mm");
                    if (BindMechanicalHole(fields, diagnostics, stock) is { } bound) holes.Add(bound);
                    fields.Complete();
                }
                else if (statement is DeclarationSyntax { Type.Name.Text: "region" } region)
                {
                    if (BindCompatibilityRegion(region, stock, diagnostics) is { } bound) regions.Add(bound);
                }
                else diagnostics.Add($"{CanonicalDeclarationUnknown}:Modify:{(statement as DeclarationSyntax)?.Type.Name.Text ?? "expression"}");
            }
            if (holes.Count > 0 || regions.Count > 0) result.Add(new(stock.Name, regions, holes));
        }
        return result;
    }

    private static FirmamentV2RegionDecl? BindCompatibilityRegion(DeclarationSyntax region, FirmamentV2SolidBinding stock, List<string> diagnostics)
    {
        if (region.Name?.Text != "sideHole" || region.Clauses.Count != 1 || region.Clauses[0].Kind != ClauseKind.On
            || region.Type.Arguments.Count > 0 || region.Parameters.Count > 0 || region.Annotation is not null || region.Initializer is not null || region.ResultType is not null || region.Modifiers.Count > 0 || region.Occurrence)
        { diagnostics.Add(RegionUnsupported); return null; }
        var attachment = BindCompatibilityFace(region.Clauses[0].Value, stock, RegionAttachmentSelectorUnsupported, diagnostics);
        var statements = region.Body?.Statements ?? [];
        if (statements.Count != 1 || statements[0] is not DeclarationSyntax { Type.Name.Text: "cut" } cut)
        { diagnostics.Add(CutUnsupported); return null; }
        if (cut.Name?.Text is not ("Cylinder" or "cylinder")) { diagnostics.Add(CutToolUnsupported); return null; }
        var fields = new FirmamentSyntaxFields(cut, diagnostics, implicitUnit: "mm");
        var radiusExpression = fields.Take("Radius");
        var validRadius = FirmamentSyntaxValues.TryMeasure(radiusExpression, "mm", out var radius, "mm");
        if (radiusExpression is null) diagnostics.Add(CylinderRadiusMissing);
        else if (!validRadius || radius <= 0) diagnostics.Add(CylinderRadiusInvalid);
        var centerExpression = fields.Take("Center");
        FirmamentV2FaceLocalPoint2D? center = null;
        if (centerExpression is not null)
        {
            if (!FirmamentSyntaxValues.Vector(centerExpression, "mm", out var coordinates, "mm")) diagnostics.Add(CylinderCenterInvalid);
            else if (coordinates.Length != 2) diagnostics.Add(CylinderCenterArityInvalid);
            else center = new(coordinates[0], coordinates[1], string.Empty);
        }
        var through = BindCompatibilityFace(fields.Take("Through"), stock, ThroughSelectorUnsupported, diagnostics);
        fields.Complete();
        if (cut.Type.Arguments.Count > 0 || cut.Modifiers.Count > 0) diagnostics.Add(CutUnsupported);
        if (attachment is null || through is null || !validRadius || radius <= 0) return null;
        var route = FirmamentV2SideHoleRoutePolicy.Resolve(attachment.Axis, through.Axis, stock.Box!.Size, radius, center?.U ?? 0, center?.V ?? 0);
        if (!route.IsSupported)
        {
            diagnostics.Add(route.Diagnostic!);
            if (route.Diagnostic is SideHoleRouteUnsupported && (attachment.Kind == "Alias" || through.Kind == "Alias")) diagnostics.Add(SideHoleAliasResolvesToUnsupportedFace);
            diagnostics.Add(SideHoleOnlyPlusXMinusXSupported);
        }
        return diagnostics.Any(IsFatalDiagnosticCode) ? null : new(region.Name.Text, "FaceAttachedRegion", attachment, new("Cut", new("Cylinder", radius, center, through)));
    }

    private static FirmamentV2FaceTarget? BindCompatibilityFace(ExpressionSyntax? expression, FirmamentV2SolidBinding stock, string diagnostic, List<string> diagnostics)
    {
        var reference = FirmamentSyntaxValues.Reference(expression);
        if (expression is CallSyntax { Target: ReferenceSyntax { Name.Text: "face" }, Arguments.Count: 1 } call
            && call.Arguments[0].Name is null && call.Arguments[0].Default is null)
            reference = "face(" + FirmamentSyntaxValues.Reference(call.Arguments[0].Value) + ")";
        if (reference is null) { diagnostics.Add(diagnostic); return null; }
        // Canonical axes and compatibility face() values share the existing
        // semantic resolver; authored aliases retain their exact identities.
        if (reference is "+X" or "-X" or "+Y" or "-Y" or "+Z" or "-Z") return FirmamentV2FaceTarget.Direct(reference);
        return ResolveFaceTarget(reference, stock, diagnostic, diagnostics);
    }

    // Historical solid declarations are an AST spelling adapter, not another
    // primitive grammar. Units remain implicit only within this admitted syntax.
    private static Dictionary<string, FirmamentV2SolidBinding> BindCompatibilitySolids(
        SyntaxParseResult syntax, DeclarationSyntax? model, string source, string? directory, List<string> diagnostics)
    {
        var solids = new Dictionary<string, FirmamentV2SolidBinding>(StringComparer.Ordinal);
        if (model?.Body is null) return solids;
        foreach (var declaration in model.Body.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "solid"))
        {
            foreach (var error in syntax.Diagnostics.Where(error => error.SourceSpan is { } span &&
                span.Start >= declaration.Span.Start && span.Start <= declaration.Span.Start + declaration.Span.Length))
                diagnostics.Add(error.Code);
            if (declaration.Type.Arguments.Count > 0 || declaration.Modifiers.Count > 0) diagnostics.Add(CanonicalPrimitiveMalformed);
            var target = declaration.Annotation is WithSyntax with ? with.Target : declaration.Annotation;
            var type = FirmamentSyntaxValues.Reference(target);
            if (type is null || declaration.Name is null || declaration.Body is null)
            { diagnostics.Add(UnknownRecordType); continue; }
            var derived = declaration.Annotation is WithSyntax;
            var body = declaration.Body;
            var exposures = body.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "expose").ToArray();
            if (exposures.Length > 1) diagnostics.Add(ExposeBlockUnsupported);
            var primitiveSyntax = declaration with
            {
                Type = new(new(type, target!.Span), [], target.Span), Annotation = null,
                Body = body with { Statements = body.Statements.Except(exposures).ToArray() }
            };
            var fields = new FirmamentSyntaxFields(primitiveSyntax, diagnostics, implicitUnit: "mm");
            FirmamentV2SolidBinding? binding = null;
            if (derived)
            {
                if (!solids.TryGetValue(type, out var stock)) { diagnostics.Add(NameUnresolved); diagnostics.Add(WithForwardReference); }
                else if (stock.Box is null) diagnostics.Add(WithRequiresBoxRecord);
                else if (!fields.Vector("Size", "mm", out var size) || size.Length != 3) diagnostics.Add(WithFieldTypeMismatch);
                else if (size.Any(v => v <= 0)) diagnostics.Add(DegenerateDimension);
                else binding = new(declaration.Name.Text, "Box", new FirmamentV2BoxRecord(size, []), type,
                    new Dictionary<string, IReadOnlyList<double>>(StringComparer.Ordinal) { ["size"] = size });
                if (exposures.Length > 0) diagnostics.Add(ExposeBlockUnsupported);
                if (body.Statements.OfType<FieldSyntax>().Any(f => !FirmamentSyntaxFields.SameField(f.Name.Text, "Size"))) diagnostics.Add(WithFieldNotFound);
            }
            else if (type == "InlineStep")
            {
                if (exposures.Length > 0) diagnostics.Add(ExposeBlockUnsupported);
                _ = fields.Reference("Path");
                // The import owner retains canonical AP242 admission and topology
                // evidence; syntax and unknown-field admission are AST-owned.
                var span = body.ContentSpan;
                binding = ParseInlineStep(declaration.Name.Text, source.Substring(span.Start, span.Length), diagnostics, directory);
            }
            else if (type is "Box" or "Cylinder" or "Cone" or "Sphere" or "Torus")
            {
                var primitive = BindMechanicalPrimitive(fields, diagnostics);
                if (primitive is FirmamentV2BoxRecord box)
                    primitive = box with { Exposures = exposures.SelectMany(e => BindCompatibilityExposures(e, diagnostics)).ToArray() };
                else if (exposures.Length > 0) diagnostics.Add(ExposeBlockUnsupported);
                if (primitive is null)
                {
                    diagnostics.Add(CanonicalPrimitiveMalformed);

                }
                else binding = new(declaration.Name.Text, type, primitive, SourceSpan: declaration.Span, AuthoredFields: fields.AuthoredFields);
            }
            else diagnostics.Add(UnknownRecordType);
            if (body.Statements.OfType<ExpressionStatementSyntax>().Any(e => e.Value is AliasSyntax)) diagnostics.Add(FatArrowOutsideExpose);
            fields.Complete();
            if (binding is not null && !solids.TryAdd(binding.Name, binding)) diagnostics.Add(DuplicateName);
        }
        return solids;
    }

    private static IReadOnlyList<FirmamentV2Exposure> BindCompatibilityExposures(DeclarationSyntax expose, List<string> diagnostics)
    {
        var result = new List<FirmamentV2Exposure>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (expose.Name is not null || expose.Type.Arguments.Count > 0 || expose.Parameters.Count > 0 || expose.Clauses.Count > 0 || expose.Annotation is not null || expose.Initializer is not null || expose.ResultType is not null || expose.Modifiers.Count > 0 || expose.Occurrence)
            diagnostics.Add(ExposeBlockUnsupported);
        foreach (var statement in expose.Body?.Statements ?? [])
        {
            if (statement is not ExpressionStatementSyntax { Value: AliasSyntax alias }) { diagnostics.Add(SelectorUnsupported); continue; }
            if (!names.Add(alias.Name.Text)) { diagnostics.Add(ExposeAliasDuplicate); continue; }
            var value = alias.Value;
            string? sub = null;
            if (value is MemberSyntax member) { sub = member.Member.Text; value = member.Target; }
            if (value is not CallSyntax { Target: ReferenceSyntax { Name.Text: "face" }, Arguments.Count: 1 } call)
            { diagnostics.Add(SelectorUnsupported); continue; }
            if (call.Arguments[0].Name is not null || call.Arguments[0].Default is not null) { diagnostics.Add(SelectorUnsupported); continue; }
            var axis = FirmamentSyntaxValues.Reference(call.Arguments[0].Value);
            if (axis is not ("+X" or "-X" or "+Y" or "-Y" or "+Z" or "-Z")) { diagnostics.Add(SelectorAxisInvalid); continue; }
            if (sub is not null and not "outerLoop") { diagnostics.Add(SelectorSubselectorUnsupported); continue; }
            result.Add(new(alias.Name.Text, "face", $"face({axis})" + (sub is null ? "" : "." + sub), sub is null ? "FaceRef" : "LoopRef", axis, sub));
        }
        return result;
    }
}
