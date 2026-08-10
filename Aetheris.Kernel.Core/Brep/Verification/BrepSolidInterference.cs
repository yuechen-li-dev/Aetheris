using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Verification;

public enum BrepSolidInterferenceStatus
{
    DisjointOrTouching,
    Interfering,
    Unsupported,
}

public sealed record BrepSolidInterferenceResult(
    BrepSolidInterferenceStatus Status,
    string Evidence,
    double PenetrationWitnessMm = 0,
    double WitnessTetrahedronVolumeMm3 = 0,
    int IntersectionVertexCount = 0);

/// <summary>
/// Sound bounded interference proof for closed convex planar BReps. Broad-phase
/// bounds only reject pairs; a positive result requires a full-dimensional
/// intersection of the exact oriented planar half-spaces. Face, edge, and point
/// contact therefore remain admissible.
/// </summary>
public static class BrepSolidInterference
{
    private readonly record struct HalfSpace(Vector3D Normal, double Offset);
    private sealed record ConvexPlanarSolid(IReadOnlyList<HalfSpace> HalfSpaces, IReadOnlyList<Point3D> Vertices, Bounds Bounds);
    private readonly record struct Bounds(double MinX, double MaxX, double MinY, double MaxY, double MinZ, double MaxZ);

    public static BrepSolidInterferenceResult Analyze(BrepBody left, BrepBody right, ToleranceContext? tolerance = null)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var resolvedTolerance = tolerance ?? ToleranceContext.Default;

        if (!TryBuildConvexPlanarSolid(left, resolvedTolerance, out var leftSolid, out var leftReason))
            return new(BrepSolidInterferenceStatus.Unsupported, $"left operand is outside the convex-planar proof subset: {leftReason}");
        if (!TryBuildConvexPlanarSolid(right, resolvedTolerance, out var rightSolid, out var rightReason))
            return new(BrepSolidInterferenceStatus.Unsupported, $"right operand is outside the convex-planar proof subset: {rightReason}");

        if (!HasPositiveBroadPhaseOverlap(leftSolid.Bounds, rightSolid.Bounds, resolvedTolerance.Linear))
            return new(BrepSolidInterferenceStatus.DisjointOrTouching, "exact vertex bounds are disjoint or touching-only");

        var halfSpaces = leftSolid.HalfSpaces.Concat(rightSolid.HalfSpaces).ToArray();
        var candidates = IntersectionVertices(halfSpaces, resolvedTolerance);
        if (candidates.Count < 4)
            return new(BrepSolidInterferenceStatus.DisjointOrTouching, "combined planar half-spaces have no full-dimensional intersection", IntersectionVertexCount: candidates.Count);

        var centroid = new Point3D(candidates.Average(point => point.X), candidates.Average(point => point.Y), candidates.Average(point => point.Z));
        var penetrationWitness = halfSpaces.Min(halfSpace => halfSpace.Offset - Dot(halfSpace.Normal, centroid));
        var tetrahedronVolume = WitnessTetrahedronVolume(candidates);
        var volumeTolerance = resolvedTolerance.Linear * resolvedTolerance.Linear * resolvedTolerance.Linear;
        if (penetrationWitness <= resolvedTolerance.Linear || tetrahedronVolume <= volumeTolerance)
            return new(BrepSolidInterferenceStatus.DisjointOrTouching, "combined planar half-spaces intersect only at or below the model tolerance", IntersectionVertexCount: candidates.Count);

        return new(
            BrepSolidInterferenceStatus.Interfering,
            "exact convex-planar half-space intersection has positive three-dimensional volume",
            penetrationWitness,
            tetrahedronVolume,
            candidates.Count);
    }

    private static bool TryBuildConvexPlanarSolid(BrepBody body, ToleranceContext tolerance, out ConvexPlanarSolid solid, out string reason)
    {
        solid = null!;
        var vertices = body.Topology.Vertices
            .Select(vertex => body.TryGetVertexPoint(vertex.Id, out var point) ? point : (Point3D?)null)
            .Where(point => point.HasValue)
            .Select(point => point!.Value)
            .ToList();
        if (vertices.Count != body.Topology.Vertices.Count())
        {
            foreach (var edge in body.Topology.Edges)
            {
                if (!body.Bindings.TryGetEdgeBinding(edge.Id, out var binding)
                    || binding.TrimInterval is not { } trim
                    || body.Geometry.GetCurve(binding.CurveGeometryId) is not { Kind: CurveGeometryKind.Line3, Line3: Line3Curve line })
                {
                    reason = "complete finite vertex evidence or line-edge reconstruction is required";
                    return false;
                }
                AddUnique(vertices, line.Evaluate(trim.Start), tolerance.Linear * 4d);
                AddUnique(vertices, line.Evaluate(trim.End), tolerance.Linear * 4d);
            }
        }
        if (vertices.Count < 4)
        {
            reason = "at least four finite vertices are required";
            return false;
        }

        var centroid = new Point3D(vertices.Average(point => point.X), vertices.Average(point => point.Y), vertices.Average(point => point.Z));
        var halfSpaces = new List<HalfSpace>();
        foreach (var binding in body.Bindings.FaceBindings.OrderBy(binding => binding.FaceId.Value))
        {
            var surface = body.Geometry.GetSurface(binding.SurfaceGeometryId);
            if (surface.Kind != SurfaceGeometryKind.Plane || surface.Plane is not PlaneSurface plane)
            {
                reason = $"face {binding.FaceId.Value} is not planar";
                return false;
            }

            var planeNormal = new Vector3D(plane.Normal.X, plane.Normal.Y, plane.Normal.Z);
            var normalLength = planeNormal.Length;
            if (!double.IsFinite(normalLength) || normalLength <= tolerance.Linear)
            {
                reason = $"face {binding.FaceId.Value} has a degenerate plane normal";
                return false;
            }
            var normal = planeNormal / normalLength;
            var offset = Dot(normal, plane.Origin);
            if (Dot(normal, centroid) > offset)
            {
                normal = -normal;
                offset = -offset;
            }
            halfSpaces.Add(new(normal, offset));
        }
        if (halfSpaces.Count < 4)
        {
            reason = "at least four planar faces are required";
            return false;
        }

        var admissibilityTolerance = tolerance.Linear * 4d;
        if (vertices.Any(vertex => halfSpaces.Any(halfSpace => Dot(halfSpace.Normal, vertex) > halfSpace.Offset + admissibilityTolerance)))
        {
            reason = "face half-spaces do not describe one convex solid";
            return false;
        }

        solid = new(
            halfSpaces,
            vertices,
            new(vertices.Min(point => point.X), vertices.Max(point => point.X), vertices.Min(point => point.Y), vertices.Max(point => point.Y), vertices.Min(point => point.Z), vertices.Max(point => point.Z)));
        reason = string.Empty;
        return true;
    }

    private static List<Point3D> IntersectionVertices(IReadOnlyList<HalfSpace> halfSpaces, ToleranceContext tolerance)
    {
        var result = new List<Point3D>();
        var admissibilityTolerance = tolerance.Linear * 4d;
        for (var first = 0; first < halfSpaces.Count - 2; first++)
        for (var second = first + 1; second < halfSpaces.Count - 1; second++)
        for (var third = second + 1; third < halfSpaces.Count; third++)
        {
            if (!TryIntersect(halfSpaces[first], halfSpaces[second], halfSpaces[third], tolerance, out var point)) continue;
            if (halfSpaces.Any(halfSpace => Dot(halfSpace.Normal, point) > halfSpace.Offset + admissibilityTolerance)) continue;
            if (result.Any(existing => Distance(existing, point) <= admissibilityTolerance)) continue;
            result.Add(point);
        }
        return result;
    }

    private static bool TryIntersect(HalfSpace first, HalfSpace second, HalfSpace third, ToleranceContext tolerance, out Point3D point)
    {
        var secondCrossThird = Cross(second.Normal, third.Normal);
        var determinant = first.Normal.Dot(secondCrossThird);
        if (System.Math.Abs(determinant) <= tolerance.Angular)
        {
            point = default;
            return false;
        }
        var numerator = secondCrossThird * first.Offset
            + Cross(third.Normal, first.Normal) * second.Offset
            + Cross(first.Normal, second.Normal) * third.Offset;
        var coordinates = numerator / determinant;
        point = new(coordinates.X, coordinates.Y, coordinates.Z);
        return double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);
    }

    private static double WitnessTetrahedronVolume(IReadOnlyList<Point3D> points)
    {
        var origin = points[0];
        var farthest = points.Skip(1).MaxBy(point => Distance(origin, point));
        if (farthest == default) return 0;
        var baseline = farthest - origin;
        var third = points.MaxBy(point => Cross(baseline, point - origin).Length);
        if (third == default) return 0;
        var normal = Cross(baseline, third - origin);
        return points.Max(point => System.Math.Abs(normal.Dot(point - origin))) / 6d;
    }

    private static bool HasPositiveBroadPhaseOverlap(Bounds left, Bounds right, double tolerance) =>
        System.Math.Min(left.MaxX, right.MaxX) - System.Math.Max(left.MinX, right.MinX) > tolerance
        && System.Math.Min(left.MaxY, right.MaxY) - System.Math.Max(left.MinY, right.MinY) > tolerance
        && System.Math.Min(left.MaxZ, right.MaxZ) - System.Math.Max(left.MinZ, right.MinZ) > tolerance;

    private static double Dot(Vector3D normal, Point3D point) => normal.X * point.X + normal.Y * point.Y + normal.Z * point.Z;
    private static Vector3D Cross(Vector3D first, Vector3D second) => new(first.Y * second.Z - first.Z * second.Y, first.Z * second.X - first.X * second.Z, first.X * second.Y - first.Y * second.X);
    private static double Distance(Point3D first, Point3D second) => (first - second).Length;
    private static void AddUnique(List<Point3D> points, Point3D candidate, double tolerance)
    {
        if (!points.Any(point => Distance(point, candidate) <= tolerance)) points.Add(candidate);
    }
}
