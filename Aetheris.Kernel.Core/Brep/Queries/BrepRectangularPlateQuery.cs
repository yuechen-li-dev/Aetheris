using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Queries;

public sealed record RectangularPlateHole(FaceId Wall, double X, double Y, double Diameter);
public sealed record RectangularPlateDefinition(Point3D Origin, double Width, double Height, double Thickness,
    IReadOnlyList<RectangularPlateHole> Holes, IReadOnlyList<FaceId> CoveredFaces)
{
    public IReadOnlyList<EdgeId> StockEdges { get; init; } = [];
}

/// <summary>Exact, deliberately closed recognition of an axis-aligned rectangular plate with
/// separated circular Z-through holes. Every face/loop/edge must be accounted for. No sampled
/// bounding box, feature history or display mesh is used to claim a complete definition.</summary>
public static class BrepRectangularPlateQuery
{
    private const double Epsilon = 1e-7;
    public static bool TryDefine(BrepBody body, out RectangularPlateDefinition? definition, out string reason)
    {
        definition = null;
        reason = "Complete dimension planning currently requires six rectangular stock planes and disjoint circular Z-through holes.";
        var planes = new List<(Face Face, PlaneSurface Plane, int Axis)>();
        var walls = new List<(Face Face, CylinderSurface Cylinder)>();
        if (body.Topology.Bodies.Count() != 1 || body.Topology.Shells.Count() != 1) return false;
        if (body.FaceOrientationReport is { } orientation && orientation.Shells.Any(s => !s.IsClosedManifold || s.Qualification != FaceOrientationQualification.DerivedQualified))
        { reason = "Complete dimensions require a qualified closed shell with resolved material-side orientation."; return false; }
        foreach (var face in body.Topology.Faces)
        {
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface)) return false;
            if (surface?.Plane is { } plane)
            {
                var n = plane.Normal.ToVector();
                var axis = Axis(n);
                if (axis < 0)
                { reason = $"Face {face.Id.Value} has a non-axis-aligned plane; complete rectangular-stock recognition requires axis-aligned stock planes."; return false; }
                planes.Add((face, plane, axis));
            }
            else if (surface?.Cylinder is { } cylinder && Axis(cylinder.Axis.ToVector()) == 2)
            {
                if (!body.Bindings.TryGetFaceBinding(face.Id, out var binding) || binding.Orientation.IsAlignedWithSurface)
                { reason = $"Face {face.Id.Value} has cylindrical support without inward hole-wall orientation; stock cylinders and bosses are not rectangular-stock through holes."; return false; }
                walls.Add((face, cylinder));
            }
            else
            { reason = $"Face {face.Id.Value} has unsupported stock/hole support {surface?.Kind}; complete recognition admits stock planes and circular Z-through-hole walls."; return false; }
        }
        if (planes.Count != 6 || Enumerable.Range(0, 3).Any(a => planes.Count(p => p.Axis == a) != 2))
        { reason = $"Complete rectangular-stock recognition requires six stock planes (two per axis); found {planes.Count}."; return false; }
        double Coordinate(Point3D p, int axis) => axis == 0 ? p.X : axis == 1 ? p.Y : p.Z;
        var min = Enumerable.Range(0, 3).Select(a => planes.Where(p => p.Axis == a).Min(p => Coordinate(p.Plane.Origin, a))).ToArray();
        var max = Enumerable.Range(0, 3).Select(a => planes.Where(p => p.Axis == a).Max(p => Coordinate(p.Plane.Origin, a))).ToArray();
        if (Enumerable.Range(0, 3).Any(a => max[a] - min[a] <= Epsilon)) return false;
        var holes = new List<RectangularPlateHole>();
        var ringEdges = new HashSet<EdgeId>();
        foreach (var (face, cylinder) in walls)
        {
            if (face.LoopIds.Count != 1) return false;
            var uses = body.Topology.GetLoop(face.LoopIds[0]).CoedgeIds.Select(body.Topology.GetCoedge).ToArray();
            var edges = uses.Select(c => c.EdgeId).Distinct().ToArray();
            if (uses.Length != 4 || edges.Length != 3) return false;
            var circles = new List<(EdgeId Id, Circle3Curve Circle)>();
            EdgeId? seam = null;
            foreach (var edge in edges)
            {
                if (!body.TryGetEdgeCurveGeometry(edge, out var curve)) return false;
                if (curve?.Circle3 is { } circle)
                {
                    if (!body.Bindings.TryGetEdgeBinding(edge, out var binding) || binding.TrimInterval is not { } interval
                        || System.Math.Abs(System.Math.Abs(interval.End - interval.Start) - 2 * System.Math.PI) > Epsilon) return false;
                    circles.Add((edge, circle));
                }
                else if (curve?.Line3 is { } line && Axis(line.Direction.ToVector()) == 2) seam = edge;
                else return false;
            }
            if (circles.Count != 2 || seam is null || uses.Count(c => c.EdgeId == seam) != 2) return false;
            if (!BoundLineMatchesVertices(body, seam.Value, out var seamA, out var seamB)
                || !Near(seamA.X, seamB.X) || !Near(seamA.Y, seamB.Y)
                || !Near(System.Math.Min(seamA.Z, seamB.Z), min[2]) || !Near(System.Math.Max(seamA.Z, seamB.Z), max[2])
                || !Near(System.Math.Sqrt(System.Math.Pow(seamA.X - cylinder.Origin.X, 2) + System.Math.Pow(seamA.Y - cylinder.Origin.Y, 2)), cylinder.Radius)) return false;
            var ordered = circles.OrderBy(c => c.Circle.Center.Z).ToArray();
            if (!Near(ordered[0].Circle.Center.Z, min[2]) || !Near(ordered[1].Circle.Center.Z, max[2])) return false;
            foreach (var (id, circle) in circles)
            {
                if (!Near(circle.Radius, cylinder.Radius) || !Near(circle.Center.X, cylinder.Origin.X)
                    || !Near(circle.Center.Y, cylinder.Origin.Y) || Axis(circle.Normal.ToVector()) != 2) return false;
                if (!ringEdges.Add(id)) return false;
                var edge = body.Topology.GetEdge(id);
                foreach (var vertex in new[] { edge.StartVertexId, edge.EndVertexId })
                    if (!body.TryGetVertexPoint(vertex, out var point) || !Near(point.Z, circle.Center.Z)
                        || !Near(System.Math.Sqrt(System.Math.Pow(point.X - circle.Center.X, 2) + System.Math.Pow(point.Y - circle.Center.Y, 2)), circle.Radius)) return false;
            }
            var x = cylinder.Origin.X; var y = cylinder.Origin.Y; var r = cylinder.Radius;
            if (x - r <= min[0] + Epsilon || x + r >= max[0] - Epsilon || y - r <= min[1] + Epsilon || y + r >= max[1] - Epsilon) return false;
            if (holes.Any(h => System.Math.Sqrt(System.Math.Pow(h.X - x, 2) + System.Math.Pow(h.Y - y, 2)) <= h.Diameter / 2 + r + Epsilon)) return false;
            holes.Add(new(face.Id, x, y, 2 * r));
        }
        var stockEdges = new HashSet<EdgeId>();
        var planeRingUses = new Dictionary<EdgeId, int>();
        foreach (var (face, plane, axis) in planes)
        {
            var rectangles = 0;
            foreach (var loopId in face.LoopIds)
            {
                var edges = body.Topology.GetLoop(loopId).CoedgeIds.Select(body.Topology.GetCoedge).Select(c => c.EdgeId).ToArray();
                if (edges.Length == 1 && ringEdges.Contains(edges[0]) && axis == 2)
                {
                    body.TryGetEdgeCurveGeometry(edges[0], out var ring);
                    if (!Near(ring!.Circle3!.Value.Center.Z, plane.Origin.Z)) return false;
                    planeRingUses[edges[0]] = planeRingUses.GetValueOrDefault(edges[0]) + 1;
                    continue;
                }
                if (edges.Length != 4 || edges.Distinct().Count() != 4) return false;
                var corners = new HashSet<(int, int, int)>();
                foreach (var id in edges)
                {
                    if (!body.TryGetEdgeCurveGeometry(id, out var curve) || curve?.Kind != CurveGeometryKind.Line3) return false;
                    if (!BoundLineMatchesVertices(body, id, out var a, out var b)) return false;
                    foreach (var point in new[] { a, b })
                    {
                        var indices = new int[3];
                        for (var i = 0; i < 3; i++)
                        {
                            var value = Coordinate(point, i);
                            indices[i] = Near(value, min[i]) ? 0 : Near(value, max[i]) ? 1 : -1;
                            if (indices[i] < 0 || i == axis && !Near(value, Coordinate(plane.Origin, axis))) return false;
                        }
                        corners.Add((indices[0], indices[1], indices[2]));
                    }
                    if (Enumerable.Range(0, 3).Count(i => !Near(Coordinate(a, i), Coordinate(b, i))) != 1) return false;
                    stockEdges.Add(id);
                }
                if (corners.Count != 4) return false;
                rectangles++;
            }
            if (rectangles != 1) return false;
        }
        if (stockEdges.Count != 12 || ringEdges.Any(e => planeRingUses.GetValueOrDefault(e) != 1)
            || body.Topology.Edges.Count() != 12 + 3 * holes.Count) return false;
        definition = new(new(min[0], min[1], min[2]), max[0] - min[0], max[1] - min[1], max[2] - min[2],
            holes.OrderBy(h => h.Y).ThenBy(h => h.X).ToArray(), body.Topology.Faces.Select(f => f.Id).ToArray()) { StockEdges = stockEdges.OrderBy(e => e.Value).ToArray() };
        reason = "Every face and boundary is covered by exact stock planes and full circular through-hole walls.";
        return true;
    }

    private static bool Near(double a, double b) => System.Math.Abs(a - b) <= Epsilon;
    private static bool BoundLineMatchesVertices(BrepBody body, EdgeId id, out Point3D a, out Point3D b)
    {
        a = default; b = default;
        var edge = body.Topology.GetEdge(id);
        if (!body.TryGetVertexPoint(edge.StartVertexId, out a) || !body.TryGetVertexPoint(edge.EndVertexId, out b)
            || !body.TryGetEdgeCurveGeometry(id, out var curve) || curve?.Line3 is not { } line
            || !body.Bindings.TryGetEdgeBinding(id, out var binding) || binding.TrimInterval is not { } interval) return false;
        var start = line.Evaluate(interval.Start); var end = line.Evaluate(interval.End);
        return (a - start).Length <= Epsilon && (b - end).Length <= Epsilon || (a - end).Length <= Epsilon && (b - start).Length <= Epsilon;
    }
    private static int Axis(Vector3D v)
    {
        const double angularEpsilon = 1e-12;
        var values = new[] { System.Math.Abs(v.X), System.Math.Abs(v.Y), System.Math.Abs(v.Z) };
        for (var i = 0; i < 3; i++)
            if (System.Math.Abs(values[i] - 1) <= angularEpsilon && Enumerable.Range(0, 3).Where(a => a != i).All(a => values[a] <= angularEpsilon)) return i;
        return -1;
    }
}
