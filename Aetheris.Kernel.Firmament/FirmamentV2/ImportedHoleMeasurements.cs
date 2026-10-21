using System.Globalization;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Source-derived engineering facts, scoped to one proved recognized
/// hole. Binding happens at the imported edit owner, without a text macro pass.</summary>
internal sealed class ImportedHoleMeasurements(FirmamentV2SolidBinding solid, FirmamentV2RecognizedRegion region,
    List<string> diagnostics) : FirmamentSyntaxRewriter
{
    ThroughHoleCandidate? measured;
    bool inspected;
    internal override ExpressionSyntax Expression(ExpressionSyntax expression)
    {
        var prefix = solid.Name + "." + region.RegionName + ".";
        var path = FirmamentSyntaxValues.Reference(expression);
        if (path?.StartsWith(prefix, StringComparison.Ordinal) != true) return base.Expression(expression);
        if (!inspected)
        {
            inspected = true;
            if (solid.InlineStep is { SourceHashPin: not null, RuntimeBody: { } body } imported)
                measured = BrepThroughHoleRevision.Discover(body, imported.ImportToleranceMillimetres ?? .001).Candidates
                    .SingleOrDefault(c => c.CanPlan && c.StepFaceEntities.Select(id => "#" + id).Order().SequenceEqual(region.FaceRefs.Order()));
        }
        if (measured is null)
        { diagnostics.Add("firmament-imported-hole-measurement-unresolved: Source measurements require a pinned, proved isolated round through-hole."); return expression; }
        var field = path[prefix.Length..];
        var center = measured.Center!.Value;
        Point3D point = field.StartsWith("exit", StringComparison.Ordinal) ? Shift(center, measured.AxisIndex!.Value, -measured.Depth!.Value) : center;
        double? value = field switch
        {
            "diameter" => measured.Diameter,
            "depth" => measured.Depth,
            "center.X" or "entry.X" or "exit.X" => point.X,
            "center.Y" or "entry.Y" or "exit.Y" => point.Y,
            "center.Z" or "entry.Z" or "exit.Z" => point.Z,
            _ => null
        };
        if (value is not null) return Length(value.Value, expression.Span);
        if (field is "center" or "entry" or "exit")
            return new CallSyntax(new ReferenceSyntax(new("Point3", expression.Span), expression.Span),
                new[] { point.X, point.Y, point.Z }.Select(v => new ArgumentSyntax(null, Length(v, expression.Span), null, expression.Span)).ToArray(), expression.Span);
        diagnostics.Add("firmament-imported-hole-measurement-unknown: Unknown recovered measurement '" + field + "'. Use diameter, depth, center, entry or exit.");
        return expression;
    }
    static ExpressionSyntax Length(double value, FirmamentV2SourceSpan span) => new QuantitySyntax(
        new LiteralSyntax(LiteralKind.Number, value.ToString("R", CultureInfo.InvariantCulture), span), new("mm", span), span);
    static Point3D Shift(Point3D p, int axis, double distance) => axis switch { 0 => new(p.X + distance, p.Y, p.Z), 1 => new(p.X, p.Y + distance, p.Z), _ => new(p.X, p.Y, p.Z + distance) };
}
