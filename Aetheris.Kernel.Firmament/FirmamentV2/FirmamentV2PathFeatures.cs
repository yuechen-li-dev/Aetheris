using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentV2PathFeature(string Host, string Support, string GuideReference,
    PlanarPathFeature Feature, FirmamentV2SourceSpan SourceSpan, double Station = 0);

public static partial class FirmamentV2Parser
{
    private static FirmamentV2PathFeature? BindPathFeature(FirmamentSyntaxFields fields,
        string host, string source, List<string> diagnostics, FirmamentCompileTimeValues? values,
        IReadOnlyList<FirmamentV2ClosedBoundary2Decl> boundaries, DocumentSyntax authoredSyntax)
    {
        var declaration = fields.Declaration;
        var add = declaration.Type.Name.Text == "Rib";
        var variant = declaration.Type.Arguments.Count == 1 ? FirmamentSyntaxValues.Reference(declaration.Type.Arguments[0].Value) : null;
        var supportExpression = fields.Take("On");
        var support = supportExpression is CallSyntax { Target: ReferenceSyntax { Name.Text: "face" }, Arguments.Count: 1 } call
            ? FirmamentSyntaxValues.Reference(call.Arguments[0].Value) : FirmamentSyntaxValues.Reference(supportExpression);
        if (support == host + ".Top" || support == "Top") support = "+Z";
        if (support == host + ".OuterWall") support = "OuterWall";
        if (support == host + ".InnerWall") support = "InnerWall";
        var guideReference = fields.Reference("Path");
        var semicircle = variant == "Semicircular";
        var width = semicircle ? fields.Number("Radius", "mm") * 2 : fields.Number(add ? "Thickness" : "Width", "mm");
        var extent = semicircle ? width / 2 : fields.Number(add ? "Height" : "Depth", "mm");
        var draft = add ? fields.Number("Draft", "deg", 0) : 0;
        var endsExpression = fields.Take("Ends");
        var ends = endsExpression is null ? "Flat" : FirmamentSyntaxValues.Reference(endsExpression);
        var station = support is "OuterWall" or "InnerWall" ? fields.Number("Station", "mm") : 0;
        if (declaration.Name is null || variant is not ("Rectangular" or "Semicircular") || add && semicircle
            || support is not ("+Z" or "OuterWall" or "InnerWall") || guideReference is null || ends is not ("Flat" or "Round"))
        {
            diagnostics.Add("firmament-path-feature-contract: Use Rib<Rectangular> or Groove<Rectangular|Semicircular>, an explicit supported on face, and a named path; ends is Flat or Round.");
            return null;
        }
        var construction = ProfileAuthoringParser.InspectConstruction(source, values ?? new(source));
        diagnostics.AddRange(construction.Diagnostics);
        var authoredDeclarations = FirmamentSyntaxTraversal.Declarations(authoredSyntax).ToArray();
        // The legacy guide owner also stores point dependencies by leaf name.
        // A qualified Line2 must not accidentally consume another layout's Start/End.
        var duplicatePoint = authoredDeclarations.Where(d => d.Type.Name.Text == "Point2" && d.Name is not null)
            .GroupBy(d => d.Name!.Text, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicatePoint is not null)
            diagnostics.Add("firmament-path-feature-guide-ambiguous: Named Point2 '" + duplicatePoint.Key
                + "' is declared in multiple scopes. The current guide owner requires unique construction point names.");
        var guideParts = guideReference.Split('.');
        var explicitOwner = authoredDeclarations.Where(d => d.Type.Name.Text == "Struct" && d.Name?.Text == guideParts[0]
            && d.Modifiers.Any(m => m.Name.Text == "Concept")).ToArray();
        var rootName = explicitOwner.Length == 1 && guideParts.Length > 1 ? guideParts[1] : guideParts[0];
        var guideDeclarations = authoredDeclarations.Where(d => d.Name?.Text == rootName
            && d.Type.Name.Text is "Line2" or "Arc2" or "Circle2" or "Rect2" or "Path").ToArray();
        if (guideDeclarations.Length > 1)
            diagnostics.Add("firmament-path-feature-guide-ambiguous: Reused guide names are not yet scope-qualified by the legacy guide owner. Use unique guide names.");
        foreach (var owner in authoredDeclarations.Where(d => d.Type.Name.Text == "Struct" && d.Modifiers.Any(m => m.Name.Text == "Concept")
            && guideDeclarations.Any(guide => guide.Span.Start > d.Span.Start && guide.Span.Start < d.Span.Start + d.Span.Length)))
            if (owner.Clauses.Any(c => c.Kind != ClauseKind.On || FirmamentSyntaxValues.Reference(c.Value) != "XY"))
                diagnostics.Add("firmament-path-feature-frame: This feature consumes XY support-local construction coordinates; other construction frames are not admitted.");
        var guideKey = guideReference;
        // The existing guide owner publishes leaf names for legacy Concept Structs.
        // Resolve the qualifier against that actual declaration, rejecting ambiguity.
        if (explicitOwner.Length == 1 && guideParts.Length > 1)
        {
            var members = explicitOwner[0].Body?.Statements.OfType<DeclarationSyntax>().Where(d => d.Name?.Text == rootName).ToArray() ?? [];
            if (members.Length != 1)
            {
                diagnostics.Add("firmament-path-feature-guide: '" + guideReference + "' does not identify one member of its Concept Struct.");
                return null;
            }
            if (!construction.Guides.ContainsKey(guideKey)) guideKey = string.Join('.', guideParts.Skip(1));
        }
        LineArcProfileCurve2D? curve = construction.Guides.GetValueOrDefault(guideKey);
        if (curve is null)
        {
            // ClosedBoundary2 owns the dimensions erased by its earlier expansion.
            // Consume its typed result, never infer generated declaration spellings.
            var shapes = boundaries.Where(b => b.Name == guideKey && b.ShapeType == "Circle2").ToArray();
            if (shapes.Length == 1) curve = new LineArcFullCircle2D((shapes[0].CenterX, shapes[0].CenterY), shapes[0].Dimensions["Radius"]);
        }
        if (curve is null)
        {
            var path = ProfileAuthoringParser.ResolveConceptPath(source, guideReference, out var reported);
            if (path?.Segments.Count == 1) curve = path.Segments[0].Geometry;
            else diagnostics.Add("firmament-path-feature-guide: Path '" + guideReference + "' must resolve to one line, arc or circle. Multi-segment junctions are not yet admitted. " + string.Join("; ", reported));
        }
        PlanarFeatureGuide? guide = curve switch
        {
            LineArcLineSegment2D line => new(PlanarGuideKind.Line, new(line.Start.X, line.Start.Y), new(line.End.X, line.End.Y), default),
            LineArcCircularArc2D arc => new(PlanarGuideKind.Arc, default, default, new(arc.Center.X, arc.Center.Y), arc.Radius, arc.StartAngleRadians, arc.SweepAngleRadians),
            LineArcFullCircle2D circle => new(PlanarGuideKind.Circle, default, default, new(circle.Center.X, circle.Center.Y), circle.Radius),
            _ => null
        };
        if (guide is null) { diagnostics.Add("firmament-path-feature-guide: Only analytic line, arc and circle guides are admitted."); return null; }
        if (guide.Kind == PlanarGuideKind.Circle && endsExpression is not null)
            diagnostics.Add("firmament-path-feature-ends: Closed guides have no end condition; omit ends.");
        if (!double.IsFinite(width + extent + draft + station) || width <= 0 || extent <= 0 || draft < 0)
            diagnostics.Add("firmament-path-feature-dimensions: Section dimensions must be finite and positive; draft must be nonnegative.");
        return new(host, support!, guideReference, new(declaration.Name.Text, add,
            semicircle ? PathSectionKind.Semicircular : PathSectionKind.Rectangular, guide, width, extent, draft, ends == "Round"), declaration.Span, station);
    }
}
