using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

/// <summary>
/// Plane-local description of a trimmed face.  It deliberately carries the
/// authoritative boundary samples rather than inventing a face-local boundary.
/// Flat-face refinement is a topology decision, never an approximation decision.
/// </summary>
public sealed record PlanarDomain(
    SurfaceMeshTrimLoop OuterLoop,
    IReadOnlyList<SurfaceMeshTrimLoop> InnerLoops,
    IReadOnlyDictionary<int, (double U, double V)> BoundaryVertices,
    IReadOnlyList<PlanarBoundaryChain> BoundaryChains,
    IReadOnlyList<int> ConcaveVertices,
    IReadOnlyList<PlanarFeatureLoop> FeatureLoops,
    IReadOnlyList<(double U, double V)> DominantDirections);

public enum PlanarFeatureLoopKind
{
    CircularHole,
    Slot,
    RoundedSlot,
    GeneralConvexInnerLoop,
    GeneralConcaveInnerLoop,
    UnknownSimpleInnerLoop
}

public sealed record PlanarFeatureLoop(
    SurfaceMeshTrimLoop Loop,
    PlanarFeatureLoopKind Kind,
    (double U, double V) Center,
    (double U, double V)? Axis,
    double CharacteristicSize,
    string ClassificationEvidence);

public enum PlanarBoundaryChainKind
{
    Straight,
    CircularArc,
    SplineSampled,
    ConvexCorner,
    ConcaveCorner,
    SmoothContinuation,
    HoleLoop,
    SlotLoop,
    OuterStructuralBoundary
}

public sealed record PlanarBoundaryChain(
    int LoopId,
    int SourceEdgeId,
    int StartVertexId,
    int EndVertexId,
    PlanarBoundaryChainKind Kind,
    CurveGeometryKind SourceCurveKind,
    int SampleCount);

/// <summary>Bounded deterministic planar partitioner used by SurfaceMeshIR.</summary>
internal static class PlanarDomainPlanner
{
    private const double Epsilon = 1e-9d;
    private const int MaximumPolygonVertices = 12;

    public static PlanarDomain Create(
        IReadOnlyList<SurfaceMeshTrimLoop> loops)
    {
        var outer = loops.OrderByDescending(loop => double.Abs(loop.SignedArea)).First();
        var inners = loops.Where(loop => loop.LoopId != outer.LoopId).ToArray();
        var vertices = loops.SelectMany(loop => loop.VertexIds.Select((id, index) => (id, uv: loop.LocalCoordinates[index])))
            .GroupBy(item => item.id).ToDictionary(group => group.Key, group => group.First().uv);
        var chains = new List<PlanarBoundaryChain>();
        var concave = new List<int>();
        foreach (var loop in loops)
        {
            var isInner = loop.LoopId != outer.LoopId;
            for (var index = 0; index < loop.VertexIds.Count; index++)
            {
                var previous = loop.LocalCoordinates[(index - 1 + loop.VertexIds.Count) % loop.VertexIds.Count];
                var current = loop.LocalCoordinates[index];
                var next = loop.LocalCoordinates[(index + 1) % loop.VertexIds.Count];
                var cross = Cross(previous, current, next);
                var loopSign = loop.SignedArea >= 0d ? 1d : -1d;
                if (cross * loopSign < -Epsilon) concave.Add(loop.VertexIds[index]);
            }
            var spans = loop.BoundarySpans ?? Array.Empty<SurfaceMeshBoundarySpan>();
            var slotLike = isInner && spans.Count(span => span.SourceCurveKind == CurveGeometryKind.Line3) >= 2
                && spans.Count(span => span.SourceCurveKind == CurveGeometryKind.Circle3) >= 2;
            foreach (var span in spans)
            {
                var kind = span.SourceCurveKind switch
                {
                    CurveGeometryKind.Line3 when !isInner => PlanarBoundaryChainKind.Straight,
                    CurveGeometryKind.Line3 when slotLike => PlanarBoundaryChainKind.SlotLoop,
                    CurveGeometryKind.Circle3 when isInner => PlanarBoundaryChainKind.HoleLoop,
                    CurveGeometryKind.BSpline3 => PlanarBoundaryChainKind.SplineSampled,
                    _ when isInner => PlanarBoundaryChainKind.HoleLoop,
                    _ => PlanarBoundaryChainKind.OuterStructuralBoundary
                };
                chains.Add(new PlanarBoundaryChain(loop.LoopId.Value, span.SourceEdgeId.Value, span.StartVertexId, span.EndVertexId, kind,
                    span.SourceCurveKind, span.SampleCount));
            }
        }
        var features = inners.Select(loop => ClassifyFeature(loop, vertices)).ToArray();
        var directions = ExtractDominantDirections(chains, vertices, features);
        return new PlanarDomain(outer, inners, vertices, chains, concave.Distinct().Order().ToArray(), features, directions);
    }

    private static PlanarFeatureLoop ClassifyFeature(SurfaceMeshTrimLoop loop, IReadOnlyDictionary<int, (double U, double V)> vertices)
    {
        var spans = loop.BoundarySpans ?? [];
        var lines = spans.Where(span => span.SourceCurveKind == CurveGeometryKind.Line3).ToArray();
        var circles = spans.Where(span => span.SourceCurveKind == CurveGeometryKind.Circle3).ToArray();
        var center = PolygonCentroid(loop.LocalCoordinates);
        var bounds = Bounds(loop.LocalCoordinates);
        var size = double.Min(bounds.MaxU - bounds.MinU, bounds.MaxV - bounds.MinV);
        if (spans.Count > 0 && circles.Length == spans.Count)
            return new(loop, PlanarFeatureLoopKind.CircularHole, center, null, size, $"all {circles.Length} source spans are circular");
        var axis = PrincipalAxis(loop.LocalCoordinates);
        if (lines.Length == 2 && circles.Length == 2 && spans.Count == 4)
            return new(loop, PlanarFeatureLoopKind.Slot, center, axis, size, "two straight source spans joined by two circular end spans");
        if (lines.Length >= 2 && circles.Length >= 2 && (bounds.MaxU - bounds.MinU) / double.Max(bounds.MaxV - bounds.MinV, Epsilon) is > 1.35d or < 0.7407407407407407d)
            return new(loop, PlanarFeatureLoopKind.RoundedSlot, center, axis, size, $"mixed rounded boundary: lines={lines.Length}, circles={circles.Length}");
        if (IsConvex(loop.LocalCoordinates))
            return new(loop, PlanarFeatureLoopKind.GeneralConvexInnerLoop, center, axis, size, "simple convex projected loop");
        if (IsSimple(loop.LocalCoordinates))
            return new(loop, PlanarFeatureLoopKind.GeneralConcaveInnerLoop, center, axis, size, "simple concave projected loop");
        return new(loop, PlanarFeatureLoopKind.UnknownSimpleInnerLoop, center, axis, size, "bounded classification unavailable");
    }

    private static IReadOnlyList<(double U, double V)> ExtractDominantDirections(
        IReadOnlyList<PlanarBoundaryChain> chains,
        IReadOnlyDictionary<int, (double U, double V)> vertices,
        IReadOnlyList<PlanarFeatureLoop> features)
    {
        var candidates = chains.Where(chain => chain.SourceCurveKind == CurveGeometryKind.Line3)
            .Select(chain => Direction(vertices[chain.StartVertexId], vertices[chain.EndVertexId]))
            .Concat(features.Where(feature => feature.Axis is not null).Select(feature => feature.Axis!.Value))
            .Where(direction => (direction.U * direction.U) + (direction.V * direction.V) > Epsilon).ToArray();
        var result = new List<(double U, double V)>();
        foreach (var candidate in candidates.OrderByDescending(direction => double.Abs(direction.U)).ThenByDescending(direction => double.Abs(direction.V)))
        {
            var normalized = NormalizeUndirected(candidate);
            if (result.All(existing => double.Abs((existing.U * normalized.U) + (existing.V * normalized.V)) < 0.9961946980917455d)) result.Add(normalized);
            if (result.Count == 4) break;
        }
        return result;
    }

    private static (double U, double V) PrincipalAxis(IReadOnlyList<(double U, double V)> points)
    {
        var center = PolygonCentroid(points);
        var xx = points.Sum(point => (point.U - center.U) * (point.U - center.U));
        var yy = points.Sum(point => (point.V - center.V) * (point.V - center.V));
        var xy = points.Sum(point => (point.U - center.U) * (point.V - center.V));
        var angle = 0.5d * double.Atan2(2d * xy, xx - yy);
        return NormalizeUndirected((double.Cos(angle), double.Sin(angle)));
    }

    private static (double U, double V) PolygonCentroid(IReadOnlyList<(double U, double V)> points)
    {
        var area6 = 0d; var u = 0d; var v = 0d;
        for (var index = 0; index < points.Count; index++)
        {
            var next = points[(index + 1) % points.Count];
            var cross = (points[index].U * next.V) - (next.U * points[index].V);
            area6 += 3d * cross; u += (points[index].U + next.U) * cross; v += (points[index].V + next.V) * cross;
        }
        return double.Abs(area6) <= Epsilon ? (points.Average(point => point.U), points.Average(point => point.V)) : (u / area6, v / area6);
    }
    private static (double MinU, double MinV, double MaxU, double MaxV) Bounds(IReadOnlyList<(double U, double V)> points) => (points.Min(point => point.U), points.Min(point => point.V), points.Max(point => point.U), points.Max(point => point.V));
    private static (double U, double V) Direction((double U, double V) a, (double U, double V) b) => NormalizeUndirected((b.U - a.U, b.V - a.V));
    private static (double U, double V) NormalizeUndirected((double U, double V) direction)
    {
        var length = double.Sqrt((direction.U * direction.U) + (direction.V * direction.V));
        if (length <= Epsilon) return (0d, 0d);
        var value = (U: direction.U / length, V: direction.V / length);
        return value.U < -Epsilon || (double.Abs(value.U) <= Epsilon && value.V < 0d) ? (-value.U, -value.V) : value;
    }

    public static bool TryDecompose(
        PlanarDomain domain,
        PlaneSurface plane,
        IReadOnlyDictionary<int, SurfaceMeshVertex> vertices,
        bool sameSense,
        out IReadOnlyList<SurfaceMeshCell> cells,
        out string plannerPath)
    {
        cells = Array.Empty<SurfaceMeshCell>();
        plannerPath = "PlanarDomain.Invalid";
        if (!IsSimple(domain.OuterLoop.LocalCoordinates) || domain.InnerLoops.Any(loop => !IsSimple(loop.LocalCoordinates))) return false;
        if (domain.InnerLoops.Count == 0 && IsConvex(domain.OuterLoop.LocalCoordinates))
        {
            cells = [new BoundaryPolygonCell(Orient(domain.OuterLoop.VertexIds, sameSense))];
            plannerPath = "PlanarDomain.ConvexPolygon";
            return true;
        }

        var outer = domain.OuterLoop.VertexIds.Select(id => vertices[id].Position).ToArray();
        var holes = domain.InnerLoops.Select(loop => (IReadOnlyList<Point3D>)loop.VertexIds.Select(id => vertices[id].Position).ToArray()).ToArray();
        if (!PlanarPolygonTriangulator.TryTriangulateWithHoles(outer, holes, plane.Normal.ToVector(), out var points, out var indices, out _)) return false;
        var idByPoint = vertices.Values.GroupBy(vertex => vertex.Position).ToDictionary(group => group.Key, group => group.First().Id);
        if (!points.All(idByPoint.ContainsKey)) return false;
        var triangles = new List<IReadOnlyList<int>>(indices.Count / 3);
        for (var index = 0; index < indices.Count; index += 3)
            triangles.Add(EnsureCounterClockwise([idByPoint[points[indices[index]]], idByPoint[points[indices[index + 1]]], idByPoint[points[indices[index + 2]]]], domain.BoundaryVertices));

        cells = triangles.Select(ids => (SurfaceMeshCell)new TriangleCell(Orient(ids, sameSense))).ToArray();
        plannerPath = domain.InnerLoops.Count == 0
            ? "PlanarDomain.Concave.ConvexPartition"
            : "PlanarDomain.FeatureLoops.ConvexPartition";
        return true;
    }

    public static IReadOnlyList<SurfaceMeshCell> MergeConformingCells(
        IReadOnlyList<SurfaceMeshCell> source,
        IReadOnlyDictionary<int, (double U, double V)> uv,
        bool sameSense)
    {
        // Boundary conformity is established before merging.  This ordering is
        // essential: a shared curved-edge sample may be collinear in the plane
        // but it remains an authoritative mesh edge.
        var normalized = source.Select(cell => EnsureCounterClockwise(cell.VertexIds, uv)).ToArray();
        var merged = MergeConvexCells(normalized, uv);
        return merged.Select(ids => CreateCell(Orient(ids, sameSense))).ToArray();
    }

    private static IReadOnlyList<IReadOnlyList<int>> MergeConvexCells(
        IReadOnlyList<IReadOnlyList<int>> seed,
        IReadOnlyDictionary<int, (double U, double V)> uv)
    {
        var cells = seed.Select(ids => (IReadOnlyList<int>)ids.ToArray()).ToList();
        // Rebuild a local edge adjacency once per pass.  Pairwise comparison of
        // every triangle on a large planar face would turn a bounded greedy
        // topology pass into quadratic (and visibly non-interactive) work.
        for (var pass = 0; pass < MaximumPolygonVertices; pass++)
        {
            var owners = new Dictionary<(int A, int B), List<int>>();
            for (var index = 0; index < cells.Count; index++)
            {
                foreach (var edge in Edges(cells[index]))
                {
                    var key = edge.Start < edge.End ? (edge.Start, edge.End) : (edge.End, edge.Start);
                    if (!owners.TryGetValue(key, out var list)) owners[key] = list = [];
                    list.Add(index);
                }
            }
            var candidates = new List<MergeCandidate>();
            foreach (var pair in owners.Values.Where(pair => pair.Count == 2))
            {
                var a = pair[0]; var b = pair[1];
                if (TryMerge(cells[a], cells[b], uv, out var merged)) candidates.Add(new MergeCandidate(a, b, merged, Score(merged, uv)));
            }
            var selected = new List<MergeCandidate>();
            var claimed = new HashSet<int>();
            foreach (var candidate in candidates.OrderBy(candidate => candidate))
            {
                if (claimed.Add(candidate.A) && claimed.Add(candidate.B)) selected.Add(candidate);
            }
            if (selected.Count == 0) return cells;
            var replacements = selected.ToDictionary(candidate => candidate.A, candidate => candidate.VertexIds);
            var removed = selected.Select(candidate => candidate.B).ToHashSet();
            cells = cells.Select((cell, index) => replacements.TryGetValue(index, out var replacement) ? replacement : cell)
                .Where((_, index) => !removed.Contains(index)).ToList();
        }
        return cells;
    }

    private static bool TryMerge(IReadOnlyList<int> a, IReadOnlyList<int> b, IReadOnlyDictionary<int, (double U, double V)> uv, out IReadOnlyList<int> merged)
    {
        merged = Array.Empty<int>();
        var directed = new List<(int Start, int End)>();
        AddEdges(a, directed); AddEdges(b, directed);
        var boundary = directed.Where(edge => !directed.Any(other => other.Start == edge.End && other.End == edge.Start)).ToArray();
        if (boundary.Length != a.Count + b.Count - 2 || boundary.Length > MaximumPolygonVertices) return false;
        var next = boundary.GroupBy(edge => edge.Start).ToDictionary(group => group.Key, group => group.ToArray());
        if (next.Any(pair => pair.Value.Length != 1)) return false;
        var ids = new List<int> { boundary[0].Start };
        while (ids.Count <= boundary.Length)
        {
            var edge = next[ids[^1]][0];
            ids.Add(edge.End);
            if (edge.End == ids[0]) break;
        }
        if (ids.Count != boundary.Length + 1 || ids[^1] != ids[0]) return false;
        ids.RemoveAt(ids.Count - 1);
        if (ids.Distinct().Count() != ids.Count || !IsSimple(ids.Select(id => uv[id]).ToArray()) || !IsConvex(ids.Select(id => uv[id]).ToArray())) return false;
        merged = EnsureCounterClockwise(ids, uv);
        return true;
    }

    private static void AddEdges(IReadOnlyList<int> ids, ICollection<(int Start, int End)> edges)
    {
        for (var index = 0; index < ids.Count; index++) edges.Add((ids[index], ids[(index + 1) % ids.Count]));
    }
    private static IEnumerable<(int Start, int End)> Edges(IReadOnlyList<int> ids)
    {
        for (var index = 0; index < ids.Count; index++) yield return (ids[index], ids[(index + 1) % ids.Count]);
    }

    private static SurfaceMeshCell CreateCell(IReadOnlyList<int> ids) => ids.Count switch
    {
        3 => new TriangleCell(ids),
        4 => new QuadCell(ids),
        _ => new BoundaryPolygonCell(ids)
    };

    // Lower score wins: remove the shortest internal edge first, prefer quads,
    // then favour compact cells.  It is a deterministic bounded quality cost.
    private static (int NotQuad, int VertexCount, double NegativeCompactness, string Key) Score(IReadOnlyList<int> ids, IReadOnlyDictionary<int, (double U, double V)> uv)
    {
        var lengths = Enumerable.Range(0, ids.Count).Select(i => Distance(uv[ids[i]], uv[ids[(i + 1) % ids.Count]])).ToArray();
        var compactness = lengths.Min() / lengths.Max();
        return (ids.Count == 4 ? 0 : 1, ids.Count, -compactness, string.Join(',', ids));
    }

    private static IReadOnlyList<int> EnsureCounterClockwise(IReadOnlyList<int> ids, IReadOnlyDictionary<int, (double U, double V)> uv)
        => SignedArea(ids.Select(id => uv[id]).ToArray()) >= 0d ? ids.ToArray() : ids.Reverse().ToArray();
    private static IReadOnlyList<int> Orient(IReadOnlyList<int> ids, bool sameSense) => sameSense ? ids : ids.Reverse().ToArray();
    private static double Cross((double U, double V) a, (double U, double V) b, (double U, double V) c) => ((b.U - a.U) * (c.V - b.V)) - ((b.V - a.V) * (c.U - b.U));
    private static double SignedArea(IReadOnlyList<(double U, double V)> points) => Enumerable.Range(0, points.Count).Sum(i => (points[i].U * points[(i + 1) % points.Count].V) - (points[(i + 1) % points.Count].U * points[i].V)) * 0.5d;
    private static double Distance((double U, double V) a, (double U, double V) b) => double.Sqrt(((a.U - b.U) * (a.U - b.U)) + ((a.V - b.V) * (a.V - b.V)));
    private static bool IsConvex(IReadOnlyList<(double U, double V)> points)
    {
        var sign = 0;
        for (var index = 0; index < points.Count; index++)
        {
            var cross = Cross(points[index], points[(index + 1) % points.Count], points[(index + 2) % points.Count]);
            if (double.Abs(cross) <= Epsilon) continue;
            var current = cross > 0d ? 1 : -1;
            if (sign != 0 && sign != current) return false;
            sign = current;
        }
        return sign != 0;
    }
    private static bool IsSimple(IReadOnlyList<(double U, double V)> points) => points.Count >= 3 && double.Abs(SignedArea(points)) > Epsilon;
    private sealed record MergeCandidate(int A, int B, IReadOnlyList<int> VertexIds, (int NotQuad, int VertexCount, double NegativeCompactness, string Key) Score) : IComparable<MergeCandidate>
    {
        public int CompareTo(MergeCandidate? other) => other is null ? -1 : Comparer<(int, int, double, string)>.Default.Compare(Score, other.Score);
    }
}
