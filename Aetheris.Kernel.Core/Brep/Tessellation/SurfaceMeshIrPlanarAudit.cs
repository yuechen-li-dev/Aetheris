using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

/// <summary>Deterministic planar-only topology evidence for inspection and regression review.</summary>
public sealed record SurfaceMeshPlanarFaceAudit(
    int FaceId, double Area, int OuterLoopEdgeCount, int InnerLoopCount,
    IReadOnlyList<string> InnerLoopTypes, int ConcavityCount, int CellCount,
    int QuadCount, int TriangleCount, int NgonCount, double LongestInternalEdge,
    double ShortestInternalEdge, double TotalInternalEdgeLength, double WorstAspectRatio,
    int SkinnyCellCount, int TriangleFanCount, int FeatureBandCellCount, int BridgeCellCount,
    int CoarseRemainderCellCount, int ResidualTransitionCellCount, int FeatureBandCount,
    int BridgeCount, double MaximumTopologyLocality, bool UsedM6Fallback,
    string? FallbackReason, string PlannerPath);

public sealed record SurfaceMeshPlanarAudit(
    int FaceCount, int CellCount, int QuadCount, int TriangleCount, int NgonCount,
    double AverageCellsPerFace, int MaximumCellsOnFace, double TotalInternalEdgeLength,
    double LongestInternalDiagonal, int SkinnyCellCount, int TriangleFanCount,
    int FeatureBandCellCount, int BridgeCellCount, int CoarseRemainderCellCount,
    int ResidualTransitionCellCount, int FeatureBandCount, int BridgeCount,
    double MaximumTopologyLocality, int M6FallbackFaceCount,
    IReadOnlyList<SurfaceMeshPlanarFaceAudit> Faces);

public static class SurfaceMeshIrPlanarAudit
{
    public static SurfaceMeshPlanarAudit Analyze(SurfaceMeshDocument document)
    {
        var vertices = document.Vertices.ToDictionary(vertex => vertex.Id);
        var faces = document.Patches.Where(patch => patch.Support.Kind == SurfaceMeshSupportKind.Plane)
            .OrderBy(patch => patch.FaceId.Value).Select(patch => AnalyzePatch(patch, vertices)).ToArray();
        return new SurfaceMeshPlanarAudit(
            faces.Length, faces.Sum(face => face.CellCount), faces.Sum(face => face.QuadCount), faces.Sum(face => face.TriangleCount), faces.Sum(face => face.NgonCount),
            faces.Length == 0 ? 0d : faces.Average(face => face.CellCount), faces.Select(face => face.CellCount).DefaultIfEmpty().Max(),
            faces.Sum(face => face.TotalInternalEdgeLength),
            faces.Select(face => face.LongestInternalEdge).DefaultIfEmpty().Max(), faces.Sum(face => face.SkinnyCellCount), faces.Sum(face => face.TriangleFanCount),
            faces.Sum(face => face.FeatureBandCellCount), faces.Sum(face => face.BridgeCellCount),
            faces.Sum(face => face.CoarseRemainderCellCount), faces.Sum(face => face.ResidualTransitionCellCount),
            faces.Sum(face => face.FeatureBandCount), faces.Sum(face => face.BridgeCount),
            faces.Select(face => face.MaximumTopologyLocality).DefaultIfEmpty().Max(),
            faces.Count(face => face.UsedM6Fallback), faces);
    }

    private static SurfaceMeshPlanarFaceAudit AnalyzePatch(SurfacePatch patch, IReadOnlyDictionary<int, SurfaceMeshVertex> vertices)
    {
        var loops = patch.TrimLoopData ?? [];
        var outer = loops.OrderByDescending(loop => double.Abs(loop.SignedArea)).FirstOrDefault();
        var inners = loops.Where(loop => loop.LoopId != outer?.LoopId).ToArray();
        var internalLengths = InternalEdges(patch, vertices).Select(edge => edge.Length).ToArray();
        var triangleFanCount = patch.Cells.Where(cell => cell.Kind == SurfaceMeshCellKind.Triangle)
            .SelectMany(cell => cell.VertexIds).GroupBy(id => id).Count(group => group.Count() >= 5);
        var aspect = patch.Cells.Select(cell => Aspect(cell, vertices)).DefaultIfEmpty().Max();
        var plan = patch.PlanarFeaturePlan;
        var provenance = patch.Cells.GroupBy(cell => cell.Provenance).ToDictionary(group => group.Key, group => group.Count());
        int ProvenanceCount(SurfaceMeshCellProvenance value) => provenance.GetValueOrDefault(value);
        return new SurfaceMeshPlanarFaceAudit(
            patch.FaceId.Value,
            loops.Sum(loop => loop.LoopId == outer?.LoopId ? double.Abs(loop.SignedArea) : -double.Abs(loop.SignedArea)),
            outer?.VertexIds.Count ?? 0, inners.Length,
            inners.Select(loop => DescribeLoop(loop, plan)).Order(StringComparer.Ordinal).ToArray(),
            loops.Sum(CountConcave), patch.Cells.Count,
            patch.Cells.Count(cell => cell.Kind == SurfaceMeshCellKind.Quad), patch.Cells.Count(cell => cell.Kind == SurfaceMeshCellKind.Triangle), patch.Cells.Count(cell => cell.Kind == SurfaceMeshCellKind.BoundaryPolygon),
            internalLengths.DefaultIfEmpty().Max(), internalLengths.DefaultIfEmpty().Min(), internalLengths.Sum(), aspect,
            CountSkinnyCells(patch, vertices), triangleFanCount,
            ProvenanceCount(SurfaceMeshCellProvenance.FeatureBand), ProvenanceCount(SurfaceMeshCellProvenance.Bridge),
            ProvenanceCount(SurfaceMeshCellProvenance.CoarseRemainder), ProvenanceCount(SurfaceMeshCellProvenance.ResidualTransition),
            plan?.Bands.Count ?? 0, plan?.Bridges.Count ?? 0, plan?.MaximumTopologyLocality ?? 0d,
            plan?.UsedM6Fallback ?? false, plan?.FallbackReason,
            patch.PlanarPlannerPath ?? "LegacyOrCurved");
    }

    private static IEnumerable<(double Length, int A, int B)> InternalEdges(SurfacePatch patch, IReadOnlyDictionary<int, SurfaceMeshVertex> vertices)
        => patch.Cells.SelectMany(cell => Enumerable.Range(0, cell.VertexIds.Count).Select(index => (A: cell.VertexIds[index], B: cell.VertexIds[(index + 1) % cell.VertexIds.Count])))
            .Select(edge => (A: int.Min(edge.A, edge.B), B: int.Max(edge.A, edge.B))).GroupBy(edge => edge).Where(group => group.Count() == 2)
            .Select(group => ((vertices[group.Key.A].Position - vertices[group.Key.B].Position).Length, group.Key.A, group.Key.B));
    private static int CountSkinnyCells(SurfacePatch patch, IReadOnlyDictionary<int, SurfaceMeshVertex> vertices) => patch.Cells.Count(cell => Aspect(cell, vertices) > 12d);
    private static double Aspect(SurfaceMeshCell cell, IReadOnlyDictionary<int, SurfaceMeshVertex> vertices)
    {
        var lengths = Enumerable.Range(0, cell.VertexIds.Count).Select(index => (vertices[cell.VertexIds[index]].Position - vertices[cell.VertexIds[(index + 1) % cell.VertexIds.Count]].Position).Length).Where(length => length > 1e-12d).ToArray();
        return lengths.Length == 0 ? 0d : lengths.Max() / lengths.Min();
    }
    private static int CountConcave(SurfaceMeshTrimLoop loop)
    {
        var sign = loop.SignedArea >= 0d ? 1d : -1d;
        return Enumerable.Range(0, loop.LocalCoordinates.Count).Count(index =>
        {
            var a = loop.LocalCoordinates[(index - 1 + loop.LocalCoordinates.Count) % loop.LocalCoordinates.Count]; var b = loop.LocalCoordinates[index]; var c = loop.LocalCoordinates[(index + 1) % loop.LocalCoordinates.Count];
            return (((b.U - a.U) * (c.V - b.V)) - ((b.V - a.V) * (c.U - b.U))) * sign < -1e-9d;
        });
    }
    private static string DescribeLoop(SurfaceMeshTrimLoop loop, PlanarFeatureDecompositionPlan? plan)
    {
        var kinds = loop.BoundarySpans?.Select(span => span.SourceCurveKind).Distinct().Order().ToArray() ?? [];
        var feature = plan?.Bands.FirstOrDefault(band => band.LoopId == loop.LoopId.Value)?.FeatureKind.ToString();
        var source = kinds.Length == 0 ? "Unknown" : string.Join('+', kinds);
        return feature is null ? source : $"{feature} ({source})";
    }
}
