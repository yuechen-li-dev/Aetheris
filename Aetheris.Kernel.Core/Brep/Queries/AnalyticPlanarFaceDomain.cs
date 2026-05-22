using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Core.Brep.Tessellation;

namespace Aetheris.Kernel.Core.Brep.Queries;

internal sealed class AnalyticPlanarFaceDomain
{
    private readonly PlaneSurface _plane;
    private readonly (Point3D Min, Point3D Max)? _axisBounds;
    private readonly IReadOnlyList<(double U, double V)> _outerPolygon;
    private readonly IReadOnlyList<IReadOnlyList<(double U, double V)>> _innerPolygons;
    private readonly (Point3D Center, double Radius)? _singleCircularBoundary;
    private readonly IReadOnlyList<(Point3D Center, double Radius)> _circularExclusionBoundaries;

    private AnalyticPlanarFaceDomain(
        PlaneSurface plane,
        (Point3D Min, Point3D Max)? axisBounds,
        IReadOnlyList<(double U, double V)> outerPolygon,
        IReadOnlyList<IReadOnlyList<(double U, double V)>> innerPolygons,
        (Point3D Center, double Radius)? singleCircularBoundary,
        IReadOnlyList<(Point3D Center, double Radius)> circularExclusionBoundaries)
    {
        _plane = plane;
        _axisBounds = axisBounds;
        _outerPolygon = outerPolygon;
        _innerPolygons = innerPolygons;
        _singleCircularBoundary = singleCircularBoundary;
        _circularExclusionBoundaries = circularExclusionBoundaries;
    }

    public static bool TryCreate(BrepBody body, FaceId faceId, PlaneSurface plane, out AnalyticPlanarFaceDomain domain)
    {
        domain = null!;
        if (!body.Topology.TryGetFace(faceId, out var face) || face is null)
        {
            return false;
        }

        if (TryBuildPolygonLoops(body, face, plane, out var outerPolygon, out var innerPolygons))
        {
            domain = new AnalyticPlanarFaceDomain(
                plane,
                axisBounds: null,
                outerPolygon,
                innerPolygons,
                singleCircularBoundary: null,
                circularExclusionBoundaries: []);
            return true;
        }

        var circularBoundaries = TryResolveCircularBoundaries(body, face);
        if (face.LoopIds.Count == 1 && circularBoundaries.Count == 1)
        {
            domain = new AnalyticPlanarFaceDomain(
                plane,
                axisBounds: null,
                outerPolygon: [],
                innerPolygons: [],
                singleCircularBoundary: circularBoundaries[0],
                circularExclusionBoundaries: []);
            return true;
        }

        if (IsAxisAlignedPlane(plane.Normal.ToVector()))
        {
            TryResolveAxisAlignedBoxBounds(body, out var min, out var max);
            domain = new AnalyticPlanarFaceDomain(
                plane,
                (min, max),
                outerPolygon: [],
                innerPolygons: [],
                singleCircularBoundary: null,
                circularExclusionBoundaries: circularBoundaries);
            return true;
        }

        return false;
    }

    internal static bool TryGetOuterBoundaryWorld(BrepBody body, FaceId faceId, PlaneSurface plane, out IReadOnlyList<Point3D> outerBoundary)
    {
        outerBoundary = [];
        if (!body.Topology.TryGetFace(faceId, out var face) || face is null || face.LoopIds.Count == 0)
        {
            return false;
        }

        var loops = new List<(IReadOnlyList<Point3D> Vertices, IReadOnlyList<(double U, double V)> Projected, double Area)>(face.LoopIds.Count);
        foreach (var loopId in face.LoopIds)
        {
            var loop = body.Topology.GetLoop(loopId);
            if (!TryBuildPolygonLoopDetailed(body, loop, plane, out var vertices, out var projected))
            {
                return false;
            }

            loops.Add((vertices, projected, double.Abs(SignedArea(projected))));
        }

        var ordered = loops.OrderByDescending(entry => entry.Area).ToArray();
        if (ordered.Length == 0 || ordered[0].Area <= 1e-9d)
        {
            return false;
        }

        var candidateOuter = ordered[0].Projected;
        for (var i = 1; i < ordered.Length; i++)
        {
            if (!ordered[i].Projected.All(vertex => ContainsPoint(candidateOuter, vertex, ToleranceContext.Default)))
            {
                return false;
            }
        }

        outerBoundary = ordered[0].Vertices;
        return true;
    }

    public bool Contains(Point3D point, ToleranceContext tolerance)
    {
        if (_outerPolygon.Count > 0)
        {
            var uv = ProjectToPlane(point, _plane);
            if (!ContainsPoint(_outerPolygon, uv, tolerance))
            {
                return false;
            }

            foreach (var hole in _innerPolygons)
            {
                if (ContainsPoint(hole, uv, tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        if (_singleCircularBoundary.HasValue)
        {
            return IsPointInsideCircularBoundary(point, _plane, _singleCircularBoundary.Value.Center, _singleCircularBoundary.Value.Radius, tolerance);
        }

        if (_axisBounds.HasValue)
        {
            if (!IsPointInsideAxisBounds(point, _plane.Normal.ToVector(), _axisBounds.Value.Min, _axisBounds.Value.Max, tolerance))
            {
                return false;
            }
        }

        foreach (var boundary in _circularExclusionBoundaries)
        {
            if (IsPointInsideCircularBoundary(point, _plane, boundary.Center, boundary.Radius, tolerance))
            {
                return false;
            }
        }

        return true;
    }


    public bool IsNearBoundary(Point3D point, ToleranceContext tolerance, out double boundaryDistance, out bool nearVertex)
    {
        boundaryDistance = double.PositiveInfinity;
        nearVertex = false;
        var sqTol = tolerance.Linear * tolerance.Linear;

        if (_outerPolygon.Count > 0)
        {
            var uv = ProjectToPlane(point, _plane);
            foreach (var polygon in EnumerateAllPolygons())
            {
                for (var i = 0; i < polygon.Count; i++)
                {
                    var a = polygon[i];
                    var b = polygon[(i + 1) % polygon.Count];
                    var d2 = DistancePointToSegmentSquared(uv, a, b);
                    if (d2 < boundaryDistance * boundaryDistance)
                    {
                        boundaryDistance = double.Sqrt(d2);
                    }

                    if (d2 <= sqTol)
                    {
                        nearVertex = nearVertex || DistancePointToPointSquared(uv, a) <= sqTol || DistancePointToPointSquared(uv, b) <= sqTol;
                        return true;
                    }
                }
            }

            if (!double.IsFinite(boundaryDistance))
            {
                boundaryDistance = double.PositiveInfinity;
            }

            return false;
        }

        if (_singleCircularBoundary.HasValue)
        {
            var radial = point - _singleCircularBoundary.Value.Center;
            var height = radial.Dot(_plane.Normal.ToVector());
            var inPlaneRadial = radial - (_plane.Normal.ToVector() * height);
            var distance = double.Abs(double.Sqrt(inPlaneRadial.Dot(inPlaneRadial)) - _singleCircularBoundary.Value.Radius);
            boundaryDistance = distance;
            return distance <= tolerance.Linear;
        }

        return false;
    }

    private IEnumerable<IReadOnlyList<(double U, double V)>> EnumerateAllPolygons()
    {
        if (_outerPolygon.Count > 0)
        {
            yield return _outerPolygon;
        }

        foreach (var hole in _innerPolygons)
        {
            yield return hole;
        }
    }

    private static bool TryBuildPolygonLoops(
        BrepBody body,
        Face face,
        PlaneSurface plane,
        out IReadOnlyList<(double U, double V)> outerPolygon,
        out IReadOnlyList<IReadOnlyList<(double U, double V)>> innerPolygons)
    {
        outerPolygon = [];
        innerPolygons = [];
        if (face.LoopIds.Count == 0)
        {
            return false;
        }

        var loopPolygons = new List<IReadOnlyList<(double U, double V)>>(face.LoopIds.Count);
        foreach (var loopId in face.LoopIds)
        {
            var loop = body.Topology.GetLoop(loopId);
            if (!TryBuildPolygonLoop(body, loop, plane, out var polygon))
            {
                return false;
            }

            loopPolygons.Add(polygon);
        }

        var sorted = loopPolygons
            .Select(loop => (Loop: loop, Area: double.Abs(SignedArea(loop))))
            .OrderByDescending(entry => entry.Area)
            .ToArray();

        if (sorted.Length == 0 || sorted[0].Area <= 1e-9d)
        {
            return false;
        }

        var outer = sorted[0].Loop;
        var holes = new List<IReadOnlyList<(double U, double V)>>(sorted.Length - 1);
        for (var i = 1; i < sorted.Length; i++)
        {
            var candidate = sorted[i].Loop;
            if (!candidate.All(vertex => ContainsPoint(outer, vertex, ToleranceContext.Default)))
            {
                return false;
            }

            holes.Add(candidate);
        }

        outerPolygon = outer;
        innerPolygons = holes;
        return true;
    }

    private static bool TryBuildPolygonLoop(BrepBody body, Loop loop, PlaneSurface plane, out IReadOnlyList<(double U, double V)> polygon)
    {
        polygon = [];
        if (!TryBuildPolygonLoopDetailed(body, loop, plane, out _, out var projected))
        {
            return false;
        }

        polygon = projected;
        return true;
    }

    private static bool TryBuildPolygonLoopDetailed(
        BrepBody body,
        Loop loop,
        PlaneSurface plane,
        out IReadOnlyList<Point3D> vertices3d,
        out IReadOnlyList<(double U, double V)> projected)
    {
        vertices3d = [];
        projected = [];
        if (loop.CoedgeIds.Count < 3)
        {
            return false;
        }

        var lines = new List<(Point3D Origin, Vector3D Direction)>(loop.CoedgeIds.Count);
        foreach (var coedgeId in loop.CoedgeIds)
        {
            var coedge = body.Topology.GetCoedge(coedgeId);
            if (!body.TryGetEdgeCurveGeometry(coedge.EdgeId, out var curve)
                || curve is null
                || curve.Kind != CurveGeometryKind.Line3)
            {
                return false;
            }

            var line = curve.Line3!.Value;
            var direction = coedge.IsReversed ? line.Direction.ToVector() * -1d : line.Direction.ToVector();
            if (direction.Length <= 1e-12d)
            {
                return false;
            }

            lines.Add((line.Origin, direction));
        }

        var vertices = new List<Point3D>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var current = lines[i];
            var next = lines[(i + 1) % lines.Count];
            var current2d = ProjectToPlane(current.Origin, plane);
            var next2d = ProjectToPlane(next.Origin, plane);
            var currentDir2d = (
                current.Direction.Dot(plane.UAxis.ToVector()),
                current.Direction.Dot(plane.VAxis.ToVector()));
            var nextDir2d = (
                next.Direction.Dot(plane.UAxis.ToVector()),
                next.Direction.Dot(plane.VAxis.ToVector()));
            if (!TryIntersectLines2D(current2d, currentDir2d, next2d, nextDir2d, out var intersection2d))
            {
                return false;
            }

            var point = plane.Origin + (plane.UAxis.ToVector() * intersection2d.U) + (plane.VAxis.ToVector() * intersection2d.V);
            vertices.Add(point);
        }

        if (vertices.Count < 3)
        {
            return false;
        }

        var uvVertices = new List<(double U, double V)>(vertices.Count);
        foreach (var vertex in vertices)
        {
            uvVertices.Add(ProjectToPlane(vertex, plane));
        }

        if (double.Abs(SignedArea(uvVertices)) <= 1e-9d)
        {
            return false;
        }

        vertices3d = vertices;
        projected = uvVertices;
        return true;
    }

    private static bool TryIntersectLines2D(
        (double U, double V) originA,
        (double U, double V) directionA,
        (double U, double V) originB,
        (double U, double V) directionB,
        out (double U, double V) intersection)
    {
        intersection = default;
        var determinant = (directionA.U * directionB.V) - (directionA.V * directionB.U);
        if (double.Abs(determinant) <= 1e-12d)
        {
            return false;
        }

        var delta = (U: originB.U - originA.U, V: originB.V - originA.V);
        var t = ((delta.U * directionB.V) - (delta.V * directionB.U)) / determinant;
        intersection = (originA.U + (directionA.U * t), originA.V + (directionA.V * t));
        return true;
    }

    private static bool PointsAlmostEqual(Point3D a, Point3D b)
        => (a - b).Length <= 1e-7d;

    private static List<(Point3D Center, double Radius)> TryResolveCircularBoundaries(BrepBody body, Face face)
    {
        var boundaries = new List<(Point3D Center, double Radius)>();
        foreach (var loopId in face.LoopIds)
        {
            var loop = body.Topology.GetLoop(loopId);
            var hasOnlyCircles = loop.CoedgeIds.Count > 0;
            Point3D? center = null;
            double? radius = null;
            foreach (var coedgeId in loop.CoedgeIds)
            {
                var edgeId = body.Topology.GetCoedge(coedgeId).EdgeId;
                if (!body.Bindings.TryGetEdgeBinding(edgeId, out var edgeBinding)
                    || !body.Geometry.TryGetCurve(edgeBinding.CurveGeometryId, out var curve)
                    || curve is not { Kind: CurveGeometryKind.Circle3, Circle3: { } circle })
                {
                    hasOnlyCircles = false;
                    break;
                }

                center ??= circle.Center;
                radius ??= circle.Radius;
            }

            if (hasOnlyCircles && center.HasValue && radius.HasValue)
            {
                boundaries.Add((center.Value, radius.Value));
            }
        }

        return boundaries;
    }

    private static (double U, double V) ProjectToPlane(Point3D point, PlaneSurface plane)
    {
        var local = point - plane.Origin;
        return (local.Dot(plane.UAxis.ToVector()), local.Dot(plane.VAxis.ToVector()));
    }

    private static bool ContainsPoint(IReadOnlyList<(double U, double V)> polygon, (double U, double V) point, ToleranceContext tolerance)
    {
        var inside = false;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            if (DistancePointToSegmentSquared(point, a, b) <= tolerance.Linear * tolerance.Linear)
            {
                return true;
            }

            var intersects = ((a.V > point.V) != (b.V > point.V))
                && (point.U < ((b.U - a.U) * (point.V - a.V) / (b.V - a.V + 1e-20d)) + a.U);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static double DistancePointToSegmentSquared((double U, double V) point, (double U, double V) a, (double U, double V) b)
    {
        var abU = b.U - a.U;
        var abV = b.V - a.V;
        var lengthSquared = (abU * abU) + (abV * abV);
        if (lengthSquared <= 1e-20d)
        {
            var du = point.U - a.U;
            var dv = point.V - a.V;
            return (du * du) + (dv * dv);
        }

        var apU = point.U - a.U;
        var apV = point.V - a.V;
        var t = double.Clamp(((apU * abU) + (apV * abV)) / lengthSquared, 0d, 1d);
        var closestU = a.U + (abU * t);
        var closestV = a.V + (abV * t);
        var dU = point.U - closestU;
        var dV = point.V - closestV;
        return (dU * dU) + (dV * dV);
    }

    private static double DistancePointToPointSquared((double U, double V) a, (double U, double V) b)
    {
        var du = a.U - b.U;
        var dv = a.V - b.V;
        return (du * du) + (dv * dv);
    }

    private static double SignedArea(IReadOnlyList<(double U, double V)> polygon)
    {
        var area = 0d;
        for (var i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];
            area += (current.U * next.V) - (next.U * current.V);
        }

        return area * 0.5d;
    }

    private static bool IsAxisAlignedPlane(Vector3D normal)
        => double.Abs(normal.X) > 0.9d || double.Abs(normal.Y) > 0.9d || double.Abs(normal.Z) > 0.9d;

    private static bool IsPointInsideAxisBounds(Point3D point, Vector3D normal, Point3D min, Point3D max, ToleranceContext tolerance)
    {
        if (double.Abs(normal.X) > 0.9d)
        {
            return point.Y >= min.Y - tolerance.Linear && point.Y <= max.Y + tolerance.Linear
                && point.Z >= min.Z - tolerance.Linear && point.Z <= max.Z + tolerance.Linear;
        }

        if (double.Abs(normal.Y) > 0.9d)
        {
            return point.X >= min.X - tolerance.Linear && point.X <= max.X + tolerance.Linear
                && point.Z >= min.Z - tolerance.Linear && point.Z <= max.Z + tolerance.Linear;
        }

        return point.X >= min.X - tolerance.Linear && point.X <= max.X + tolerance.Linear
            && point.Y >= min.Y - tolerance.Linear && point.Y <= max.Y + tolerance.Linear;
    }

    private static bool IsPointInsideCircularBoundary(Point3D point, PlaneSurface plane, Point3D center, double radius, ToleranceContext tolerance)
    {
        var radial = point - center;
        var height = radial.Dot(plane.Normal.ToVector());
        var inPlaneRadial = radial - (plane.Normal.ToVector() * height);
        return inPlaneRadial.Dot(inPlaneRadial) <= (radius + tolerance.Linear) * (radius + tolerance.Linear);
    }

    private static bool TryResolveAxisAlignedBoxBounds(BrepBody body, out Point3D min, out Point3D max)
    {
        min = default;
        max = default;
        var hasXMin = false;
        var hasXMax = false;
        var hasYMin = false;
        var hasYMax = false;
        var hasZMin = false;
        var hasZMax = false;

        foreach (var faceBinding in body.Bindings.FaceBindings)
        {
            var surface = body.Geometry.GetSurface(faceBinding.SurfaceGeometryId);
            if (surface.Kind != SurfaceGeometryKind.Plane || surface.Plane is not PlaneSurface plane)
            {
                continue;
            }

            var n = plane.Normal;
            if (ToleranceMath.AlmostEqual(n.X, -1d, ToleranceContext.Default))
            {
                min = min with { X = plane.Origin.X };
                hasXMin = true;
            }
            else if (ToleranceMath.AlmostEqual(n.X, 1d, ToleranceContext.Default))
            {
                max = max with { X = plane.Origin.X };
                hasXMax = true;
            }
            else if (ToleranceMath.AlmostEqual(n.Y, -1d, ToleranceContext.Default))
            {
                min = min with { Y = plane.Origin.Y };
                hasYMin = true;
            }
            else if (ToleranceMath.AlmostEqual(n.Y, 1d, ToleranceContext.Default))
            {
                max = max with { Y = plane.Origin.Y };
                hasYMax = true;
            }
            else if (ToleranceMath.AlmostEqual(n.Z, -1d, ToleranceContext.Default))
            {
                min = min with { Z = plane.Origin.Z };
                hasZMin = true;
            }
            else if (ToleranceMath.AlmostEqual(n.Z, 1d, ToleranceContext.Default))
            {
                max = max with { Z = plane.Origin.Z };
                hasZMax = true;
            }
        }

        return hasXMin && hasXMax && hasYMin && hasYMax && hasZMin && hasZMax;
    }
}
