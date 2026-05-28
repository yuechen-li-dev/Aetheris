using Aetheris.Kernel.Core.Brep.EdgeFinishing;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record TrianglePrismAdjacencyGraphSummary(int VertexCount, int EdgeCount, int FaceCount, int PlanarFaceCount, IReadOnlyList<string> EdgeRows, IReadOnlyList<string> VertexRows);
public sealed record TrianglePrismChamferCandidateSummary(int CornerCandidateCount, int CornerAdmissibleCount, IReadOnlyList<string> RejectionDiagnostics);
public sealed record TrianglePrismChamferAdjacencyParityRow(string CaseName, bool LegacyProduced, bool CandidateProduced, TrianglePrismAdjacencyGraphSummary LegacyAdjacency, TrianglePrismAdjacencyGraphSummary CandidateAdjacency, TrianglePrismChamferCandidateSummary LegacyChamfer, TrianglePrismChamferCandidateSummary CandidateChamfer, bool FeatureRecognitionParity, string? FirstDivergence, IReadOnlyList<string> Diagnostics, string Recommendation);

public static class TrianglePrismChamferAdjacencyParityLab
{
    public static IReadOnlyList<TrianglePrismChamferAdjacencyParityRow> RunAll() =>
    [
        Run("triangle-basic", 12, 8, 10, 0.75),
        Run("triangle-non-orth-chamfer", 9.5, 7.25, 6.5, 0.55),
        Run("triangle-alt", 7.5, 6.25, 4, 0.4)
    ];

    public static TrianglePrismChamferAdjacencyParityRow Run(string caseName, double width, double depth, double height, double chamferDistance)
    {
        var diagnostics = new List<string> { "v2-x8-1-triangle-chamfer-adjacency-lab-started", "v2-x8-1-no-3d-boolean-used" };

        var legacyResult = BrepPrimitives.CreateTriangularPrism(width, depth, height);
        var legacyBody = legacyResult.IsSuccess ? legacyResult.Value : null;
        if (legacyBody is not null) diagnostics.Add("v2-x8-1-legacy-triangle-created");

        var req = new LineArcProfileExtrudeRequest([TriangleLoop(width, depth)], height);
        var candidateResult = LineArcProfileExtrudeEmitter.TryEmit(req);
        var candidateBody = candidateResult.Status == LineArcProfileExtrudeStatus.Succeeded ? candidateResult.Body : null;
        if (candidateBody is not null) diagnostics.Add("v2-x8-1-linearc-triangle-created");

        if (legacyBody is null || candidateBody is null)
        {
            diagnostics.Add("v2-x8-1-blocker-classified:candidate-construction-bug");
            return new(caseName, legacyBody is not null, candidateBody is not null, EmptyAdjacency(), EmptyAdjacency(), EmptyChamfer(), EmptyChamfer(), false, "body-production-failed", diagnostics, "triangle-feature-recognition-keep-legacy-route");
        }

        var legacyAdj = SummarizeAdjacency(legacyBody);
        var candidateAdj = SummarizeAdjacency(candidateBody);
        diagnostics.Add("v2-x8-1-adjacency-summary-captured");

        var legacyChamfer = SummarizeCornerCandidates(legacyBody, chamferDistance);
        var candidateChamfer = SummarizeCornerCandidates(candidateBody, chamferDistance);
        diagnostics.Add("v2-x8-1-chamfer-candidates-captured");

        var firstDivergence = FirstDivergence(legacyAdj, candidateAdj, legacyChamfer, candidateChamfer);
        var parity = firstDivergence is null;
        if (parity) diagnostics.Add("v2-x8-1-feature-recognition-parity-succeeded");
        else
        {
            diagnostics.Add($"v2-x8-1-feature-recognition-parity-mismatch:{firstDivergence}");
            diagnostics.Add($"v2-x8-1-first-divergence:{firstDivergence}");
        }

        var recommendation = parity
            ? "triangle-feature-recognition-parity-ready"
            : firstDivergence!.StartsWith("adjacency", StringComparison.Ordinal)
                ? "triangle-feature-recognition-needs-adjacency-parity"
                : "triangle-feature-recognition-needs-corner-resolution-contract";

        if (!parity)
            diagnostics.Add($"v2-x8-1-blocker-classified:{recommendation}");

        return new(caseName, true, true, legacyAdj, candidateAdj, legacyChamfer, candidateChamfer, parity, firstDivergence, diagnostics.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray(), recommendation);
    }

    private static TrianglePrismChamferCandidateSummary SummarizeCornerCandidates(BrepBody body, double distance)
    {
        var result = BrepBoundedChamfer.ChamferTrustedPolyhedralSingleCorner(body, BrepBoundedChamferCorner.XMaxYMaxZMax, distance);
        var rejected = result.Diagnostics.Select(d => d.Message).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return new(1, result.IsSuccess ? 1 : 0, rejected);
    }

    private static TrianglePrismAdjacencyGraphSummary SummarizeAdjacency(BrepBody body)
    {
        var edgeRows = body.Topology.Edges
            .OrderBy(e => e.Id.Value)
            .Select(e =>
            {
                var loopFace = LoopFaceMap(body);
                var adjacentFaces = body.Topology.Coedges.Where(c => c.EdgeId == e.Id).Select(c => loopFace[c.LoopId]).Distinct().OrderBy(f => f.Value).ToArray();
                var curveKind = body.TryGetEdgeCurve(e.Id, out var curve) && curve is not null ? curve.Kind.ToString() : "none";
                var sp = body.TryGetVertexPoint(e.StartVertexId, out var s) ? s : default;
                var ep = body.TryGetVertexPoint(e.EndVertexId, out var t) ? t : default;
                return $"e:{e.Id.Value}|af:{adjacentFaces.Length}|ck:{curveKind}|s:{sp.X:F3},{sp.Y:F3},{sp.Z:F3}|e:{ep.X:F3},{ep.Y:F3},{ep.Z:F3}";
            })
            .ToArray();

        var vertexRows = body.Topology.Vertices
            .OrderBy(v => v.Id.Value)
            .Select(v =>
            {
                var incidentEdges = body.Topology.Edges.Count(e => e.StartVertexId == v.Id || e.EndVertexId == v.Id);
                var incidentFaces = body.Topology.Coedges.Where(c =>
                {
                    if (!body.Topology.TryGetEdge(c.EdgeId, out var edge) || edge is null) return false;
                    return edge.StartVertexId == v.Id || edge.EndVertexId == v.Id;
                }).Select(c => LoopFaceMap(body)[c.LoopId]).Distinct().Count();
                return $"v:{v.Id.Value}|ie:{incidentEdges}|if:{incidentFaces}";
            }).ToArray();

        return new(body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count(), body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane), edgeRows, vertexRows);
    }

    private static Dictionary<LoopId, FaceId> LoopFaceMap(BrepBody body)
    {
        var map = new Dictionary<LoopId, FaceId>();
        foreach (var face in body.Topology.Faces)
            foreach (var loopId in face.LoopIds)
                map[loopId] = face.Id;
        return map;
    }

    private static string? FirstDivergence(TrianglePrismAdjacencyGraphSummary legacyAdj, TrianglePrismAdjacencyGraphSummary candidateAdj, TrianglePrismChamferCandidateSummary legacyChamfer, TrianglePrismChamferCandidateSummary candidateChamfer)
    {
        if (legacyAdj.FaceCount != candidateAdj.FaceCount || legacyAdj.EdgeCount != candidateAdj.EdgeCount || legacyAdj.VertexCount != candidateAdj.VertexCount)
            return "adjacency:topology-count-mismatch";
        if (!legacyAdj.EdgeRows.SequenceEqual(candidateAdj.EdgeRows, StringComparer.Ordinal)) return "adjacency:edge-row-mismatch";
        if (!legacyAdj.VertexRows.SequenceEqual(candidateAdj.VertexRows, StringComparer.Ordinal)) return "adjacency:vertex-row-mismatch";
        if (legacyChamfer.CornerAdmissibleCount != candidateChamfer.CornerAdmissibleCount) return "corner:admissible-count-mismatch";
        if (!legacyChamfer.RejectionDiagnostics.SequenceEqual(candidateChamfer.RejectionDiagnostics, StringComparer.Ordinal)) return "corner:rejection-diagnostic-mismatch";
        return null;
    }

    private static LineArcProfileLoop2D TriangleLoop(double width, double depth)
    {
        var hw = width / 2d;
        var hd = depth / 2d;
        return new([
            new LineArcLineSegment2D((-hw, -hd), (hw, -hd)),
            new LineArcLineSegment2D((hw, -hd), (0, hd)),
            new LineArcLineSegment2D((0, hd), (-hw, -hd))
        ], false);
    }

    private static TrianglePrismAdjacencyGraphSummary EmptyAdjacency() => new(0, 0, 0, 0, [], []);
    private static TrianglePrismChamferCandidateSummary EmptyChamfer() => new(0, 0, []);
}
