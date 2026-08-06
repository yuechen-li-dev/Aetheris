using Aetheris.Kernel.Core.Geometry.Curves;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Material-side classification of a vertex in an authored Profile loop.</summary>
public enum ProfileJunctionKind
{
    Collinear,
    ConvexProfileJunction,
    ReflexProfileJunction,
    Degenerate
}

public sealed record ProfileJunctionClassification(
    string ProfileId,
    string LoopId,
    string PredecessorSegmentId,
    string SuccessorSegmentId,
    string VertexId,
    double SignedTurnRadians,
    double MaterialInteriorAngleRadians,
    ProfileJunctionKind Classification);

/// <summary>
/// Computes junction polarity from directed resolved-Profile semantics.  The material is
/// on the left of an outer CCW loop and on the right of an inner CCW loop; winding reverses
/// both relationships.  No world-axis or post-BRep face orientation is consulted.
/// </summary>
public static class ProfileJunctionClassifier
{
    private const double Tolerance = 1e-8;

    public static IReadOnlyList<ProfileJunctionClassification> Classify(ResolvedProfile2D profile) =>
        profile.Loops.SelectMany(loop => Classify(profile, loop)).ToArray();

    public static IReadOnlyList<ProfileJunctionClassification> Classify(ResolvedProfile2D profile, ResolvedProfileLoop2D loop)
    {
        if (loop.Segments.Count < 2)
            return [];

        var winding = SignedArea(loop);
        if (Math.Abs(winding) <= Tolerance)
            return loop.Segments.Select((successor, index) => new ProfileJunctionClassification(
                profile.Name, loop.Name, loop.Segments[(index + loop.Segments.Count - 1) % loop.Segments.Count].Name,
                successor.Name, $"{loop.Name}.{successor.Name}.Start", 0d, double.NaN, ProfileJunctionKind.Degenerate)).ToArray();

        var windingSign = Math.Sign(winding);
        var materialSideSign = loop.IsOuter ? windingSign : -windingSign;
        var classifications = new List<ProfileJunctionClassification>(loop.Segments.Count);
        for (var successorIndex = 0; successorIndex < loop.Segments.Count; successorIndex++)
        {
            var predecessor = loop.Segments[(successorIndex + loop.Segments.Count - 1) % loop.Segments.Count];
            var successor = loop.Segments[successorIndex];
            if (!TryEndTangent(predecessor.Geometry, out var incoming) || !TryStartTangent(successor.Geometry, out var outgoing))
            {
                classifications.Add(new(profile.Name, loop.Name, predecessor.Name, successor.Name, $"{loop.Name}.{successor.Name}.Start", 0d, double.NaN, ProfileJunctionKind.Degenerate));
                continue;
            }

            var cross = incoming.X * outgoing.Y - incoming.Y * outgoing.X;
            var dot = Math.Clamp(incoming.X * outgoing.X + incoming.Y * outgoing.Y, -1d, 1d);
            var turn = Math.Atan2(cross, dot);
            var materialInterior = Math.PI - materialSideSign * turn;
            if (materialInterior <= 0d) materialInterior += 2d * Math.PI;
            if (materialInterior > 2d * Math.PI) materialInterior -= 2d * Math.PI;
            var classification = Math.Abs(turn) <= Tolerance
                ? ProfileJunctionKind.Collinear
                : Math.Abs(Math.Abs(turn) - Math.PI) <= Tolerance
                    ? ProfileJunctionKind.Degenerate
                    : turn * materialSideSign > 0d
                        ? ProfileJunctionKind.ConvexProfileJunction
                        : ProfileJunctionKind.ReflexProfileJunction;
            classifications.Add(new(profile.Name, loop.Name, predecessor.Name, successor.Name, $"{loop.Name}.{successor.Name}.Start", turn, materialInterior, classification));
        }
        return classifications;
    }

    private static double SignedArea(ResolvedProfileLoop2D loop)
    {
        var points = loop.Segments.Select(segment => Start(segment.Geometry)).ToArray();
        return points.Any(point => point is null)
            ? 0d
            : points.Select((point, index) => point!.Value.X * points[(index + 1) % points.Length]!.Value.Y - points[(index + 1) % points.Length]!.Value.X * point.Value.Y).Sum() / 2d;
    }

    private static bool TryStartTangent(LineArcProfileCurve2D geometry, out (double X, double Y) tangent) =>
        TryTangent(geometry, false, out tangent);

    private static bool TryEndTangent(LineArcProfileCurve2D geometry, out (double X, double Y) tangent) =>
        TryTangent(geometry, true, out tangent);

    private static bool TryTangent(LineArcProfileCurve2D geometry, bool atEnd, out (double X, double Y) tangent)
    {
        switch (geometry)
        {
            case LineArcLineSegment2D line:
                return Normalize(line.End.X - line.Start.X, line.End.Y - line.Start.Y, out tangent);
            case LineArcCircularArc2D arc:
                var angle = arc.StartAngleRadians + (atEnd ? arc.SweepAngleRadians : 0d);
                var sign = Math.Sign(arc.SweepAngleRadians);
                if (sign == 0d) { tangent = default; return false; }
                return Normalize(-Math.Sin(angle) * sign, Math.Cos(angle) * sign, out tangent);
            default:
                tangent = default;
                return false;
        }
    }

    private static (double X, double Y)? Start(LineArcProfileCurve2D geometry) => geometry switch
    {
        LineArcLineSegment2D line => line.Start,
        LineArcCircularArc2D arc => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)),
        _ => null
    };

    private static bool Normalize(double x, double y, out (double X, double Y) tangent)
    {
        var length = Math.Sqrt(x * x + y * y);
        if (!double.IsFinite(length) || length <= Tolerance) { tangent = default; return false; }
        tangent = (x / length, y / length);
        return true;
    }
}
