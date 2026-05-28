using Aetheris.Kernel.Core.Brep.EdgeFinishing;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record TrianglePrismChamferCandidateSummary(int CornerCandidateCount, int CornerAdmissibleCount, IReadOnlyList<string> RejectionDiagnostics);
public sealed record TrianglePrismBodyLedger(int VertexCount, int EdgeCount, int FaceCount, int LoopCount, int CoedgeCount, string Extents, IReadOnlyDictionary<string, int> FaceFamilyCounts, IReadOnlyDictionary<string, int> CurveFamilyCounts, IReadOnlyList<TrianglePrismFaceLedgerRow> Faces, IReadOnlyList<TrianglePrismEdgeLedgerRow> Edges, IReadOnlyList<TrianglePrismVertexLedgerRow> Vertices, IReadOnlyList<TrianglePrismLoopLedgerRow> Loops);
public sealed record TrianglePrismFaceLedgerRow(int LocalIndex, int FaceId, string SurfaceKind, int LoopCount, int CoedgeCount, string ApproxNormal, string Area, string Role, IReadOnlyList<int> BoundaryEdgeIds);
public sealed record TrianglePrismEdgeLedgerRow(int LocalIndex, int EdgeId, string CurveKind, string Start, string End, int AdjacentFaceCount, IReadOnlyList<int> AdjacentFaceIds, IReadOnlyList<string> AdjacentFaceKinds, string Length, string Direction, bool OnCapSideBoundary, bool ChamferCornerAdjacent);
public sealed record TrianglePrismVertexLedgerRow(int LocalIndex, int VertexId, string Coordinate, int IncidentEdgeCount, int IncidentFaceCount, IReadOnlyList<int> IncidentEdgeIds, IReadOnlyList<int> IncidentFaceIds, IReadOnlyList<string> IncidentFaceKinds, bool IsChamferCornerVertex);
public sealed record TrianglePrismLoopLedgerRow(int LocalIndex, int LoopId, int FaceId, int CoedgeCount, IReadOnlyList<string> EdgeOrder);
public sealed record TrianglePrismAdjacencyDeltaRow(string CaseName, bool LegacyProduced, bool CandidateProduced, TrianglePrismBodyLedger LegacyLedger, TrianglePrismBodyLedger CandidateLedger, TrianglePrismChamferCandidateSummary LegacyChamfer, TrianglePrismChamferCandidateSummary CandidateChamfer, string? FirstDeltaCategory, string? FirstDeltaPayload, IReadOnlyList<string> Diagnostics, string Recommendation);

public static class TrianglePrismAdjacencyDeltaAuditLab
{
    public static IReadOnlyList<TrianglePrismAdjacencyDeltaRow> RunAll() =>
    [
        Run("triangle-basic", 12, 8, 10, 0.75),
        Run("triangle-non-orth-chamfer", 9.5, 7.25, 6.5, 0.55),
        Run("triangle-alt", 7.5, 6.25, 4, 0.4)
    ];

    public static TrianglePrismAdjacencyDeltaRow Run(string caseName, double width, double depth, double height, double chamferDistance)
    {
        var diagnostics = new List<string> { "v2-x8-2-triangle-adjacency-delta-audit-started", "v2-x8-2-no-3d-boolean-used" };
        var legacy = BrepPrimitives.CreateTriangularPrism(width, depth, height);
        var legacyBody = legacy.IsSuccess ? legacy.Value : null;
        if (legacyBody is not null) diagnostics.Add("v2-x8-2-legacy-triangle-created");

        var req = new LineArcProfileExtrudeRequest([TriangleLoop(width, depth)], height);
        var candidate = LineArcProfileExtrudeEmitter.TryEmit(req);
        var candidateBody = candidate.Status == LineArcProfileExtrudeStatus.Succeeded ? candidate.Body : null;
        if (candidateBody is not null) diagnostics.Add("v2-x8-2-linearc-triangle-created");

        if (legacyBody is null || candidateBody is null)
            return new(caseName, legacyBody is not null, candidateBody is not null, EmptyLedger(), EmptyLedger(), EmptyChamfer(), EmptyChamfer(), "body-count-mismatch", "legacy-or-candidate-body-not-produced", diagnostics, "triangle-adjacency-delta-legacy-route-required");

        var legacyLedger = CaptureLedger(legacyBody);
        diagnostics.Add("v2-x8-2-legacy-ledger-captured");
        var candidateLedger = CaptureLedger(candidateBody);
        diagnostics.Add("v2-x8-2-candidate-ledger-captured");

        var legacyChamfer = SummarizeCornerCandidates(legacyBody, chamferDistance);
        var candidateChamfer = SummarizeCornerCandidates(candidateBody, chamferDistance);
        diagnostics.Add($"v2-x8-2-chamfer-delta:legacy-adm={legacyChamfer.CornerAdmissibleCount};candidate-adm={candidateChamfer.CornerAdmissibleCount}");

        var (cat,payload) = FirstDelta(legacyLedger, candidateLedger, legacyChamfer, candidateChamfer);
        if (cat is null) diagnostics.Add("v2-x8-2-no-delta-detected");
        else diagnostics.Add($"v2-x8-2-first-delta:{cat}:{payload}");

        var rec = Recommend(cat, payload, legacyChamfer, candidateChamfer);
        diagnostics.Add($"v2-x8-2-recommendation:{rec}");
        return new(caseName, true, true, legacyLedger, candidateLedger, legacyChamfer, candidateChamfer, cat, payload, diagnostics.Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray(), rec);
    }

    private static (string?,string?) FirstDelta(TrianglePrismBodyLedger l, TrianglePrismBodyLedger c, TrianglePrismChamferCandidateSummary lc, TrianglePrismChamferCandidateSummary cc)
    {
        if (l.VertexCount != c.VertexCount) return ("vertex-count-mismatch",$"legacy={l.VertexCount};candidate={c.VertexCount}");
        if (l.EdgeCount != c.EdgeCount) return ("edge-count-mismatch",$"legacy={l.EdgeCount};candidate={c.EdgeCount}");
        if (l.FaceCount != c.FaceCount) return ("face-count-mismatch",$"legacy={l.FaceCount};candidate={c.FaceCount}");
        if (l.LoopCount != c.LoopCount) return ("loop-count-mismatch",$"legacy={l.LoopCount};candidate={c.LoopCount}");
        if (l.CoedgeCount != c.CoedgeCount) return ("coedge-count-mismatch",$"legacy={l.CoedgeCount};candidate={c.CoedgeCount}");
        if (!l.Extents.Equals(c.Extents, StringComparison.Ordinal)) return ("coordinate/extents-mismatch",$"legacy={l.Extents};candidate={c.Extents}");
        var lf = string.Join(",", l.FaceFamilyCounts.OrderBy(k=>k.Key).Select(k=>$"{k.Key}:{k.Value}"));
        var cf = string.Join(",", c.FaceFamilyCounts.OrderBy(k=>k.Key).Select(k=>$"{k.Key}:{k.Value}"));
        if (!lf.Equals(cf, StringComparison.Ordinal)) return ("face-family-count-mismatch",$"legacy={lf};candidate={cf}");
        for (var i=0;i<Math.Min(l.Faces.Count,c.Faces.Count);i++)
            if (!l.Faces[i].BoundaryEdgeIds.SequenceEqual(c.Faces[i].BoundaryEdgeIds))
                return ("side-face-order-mismatch",$"faceIndex={i};legacy=[{string.Join(',',l.Faces[i].BoundaryEdgeIds)}];candidate=[{string.Join(',',c.Faces[i].BoundaryEdgeIds)}]");
        for (var i=0;i<Math.Min(l.Edges.Count,c.Edges.Count);i++)
            if (!l.Edges[i].AdjacentFaceIds.SequenceEqual(c.Edges[i].AdjacentFaceIds))
                return ("edge-adjacency-mismatch",$"edgeIndex={i};legacy=[{string.Join(',',l.Edges[i].AdjacentFaceIds)}];candidate=[{string.Join(',',c.Edges[i].AdjacentFaceIds)}]");
        for (var i=0;i<Math.Min(l.Vertices.Count,c.Vertices.Count);i++)
            if (!l.Vertices[i].IncidentFaceIds.SequenceEqual(c.Vertices[i].IncidentFaceIds))
                return ("vertex-incidence-mismatch",$"vertexIndex={i};legacy=[{string.Join(',',l.Vertices[i].IncidentFaceIds)}];candidate=[{string.Join(',',c.Vertices[i].IncidentFaceIds)}]");
        if (lc.CornerCandidateCount != cc.CornerCandidateCount) return ("chamfer-candidate-count-mismatch",$"legacy={lc.CornerCandidateCount};candidate={cc.CornerCandidateCount}");
        if (lc.CornerAdmissibleCount != cc.CornerAdmissibleCount) return ("chamfer-admissibility-mismatch",$"legacy={lc.CornerAdmissibleCount};candidate={cc.CornerAdmissibleCount}");
        return (null,null);
    }

    private static string Recommend(string? cat, string? payload, TrianglePrismChamferCandidateSummary legacyChamfer, TrianglePrismChamferCandidateSummary candidateChamfer)
    {
        if (cat is null) return "triangle-adjacency-delta-no-action-parity-ready";
        if (cat is "cap-loop-order-mismatch") return "triangle-adjacency-delta-fix-cap-loop-convention";
        if (cat is "side-face-order-mismatch") return "triangle-adjacency-delta-fix-side-face-convention";
        if (cat is "edge-adjacency-mismatch") return "triangle-adjacency-delta-fix-edge-orientation";
        if (cat is "vertex-incidence-mismatch") return "triangle-adjacency-delta-fix-vertex-incidence";
        if (cat.Contains("count", StringComparison.Ordinal) || cat.Contains("extents", StringComparison.Ordinal)) return "triangle-adjacency-delta-fix-emitter-ordering";
        if (legacyChamfer.CornerAdmissibleCount > candidateChamfer.CornerAdmissibleCount || payload?.Contains("admiss", StringComparison.Ordinal) == true) return "triangle-adjacency-delta-update-chamfer-contract";
        return "triangle-adjacency-delta-legacy-route-required";
    }

    private static TrianglePrismBodyLedger CaptureLedger(BrepBody body)
    {
        var faceIndexById = body.Topology.Faces.OrderBy(f=>f.Id.Value).Select((f,i)=>(f.Id,i)).ToDictionary(x=>x.Id, x=>x.i);
        var loopFace = LoopFaceMap(body);
        var faceRows = body.Topology.Faces.OrderBy(f=>f.Id.Value).Select((face,i)=>
        {
            var coedges = face.LoopIds.SelectMany(id => body.Topology.Coedges.Where(c=>c.LoopId==id)).ToArray();
            var surface = body.GetFaceSurface(face.Id);
            var edgeIds = coedges.Select(c=>c.EdgeId.Value).ToArray();
            var role = edgeIds.Length == 3 ? "triangular-cap" : edgeIds.Length == 4 ? "rectangular-side" : "cap-face";
            return new TrianglePrismFaceLedgerRow(i, face.Id.Value, surface.Kind.ToString(), face.LoopIds.Count, coedges.Length, "n/a", "n/a", role, edgeIds);
        }).ToArray();

        var edgeRows = body.Topology.Edges.OrderBy(e=>e.Id.Value).Select((e,i)=>
        {
            var adj = body.Topology.Coedges.Where(c=>c.EdgeId==e.Id).Select(c=>loopFace[c.LoopId]).Distinct().OrderBy(x=>x.Value).ToArray();
            body.TryGetVertexPoint(e.StartVertexId, out var s); body.TryGetVertexPoint(e.EndVertexId, out var t);
            var kind = body.TryGetEdgeCurve(e.Id, out var curve) && curve is not null ? curve.Kind.ToString() : "none";
            var dir = $"{(t.X-s.X):F3},{(t.Y-s.Y):F3},{(t.Z-s.Z):F3}";
            var len = Math.Sqrt((t.X-s.X)*(t.X-s.X)+(t.Y-s.Y)*(t.Y-s.Y)+(t.Z-s.Z)*(t.Z-s.Z));
            var adjKinds = adj.Select(fid => body.GetFaceSurface(fid).Kind.ToString()).ToArray();
            var capSide = adjKinds.Contains("Plane", StringComparer.Ordinal) && adjKinds.Length==2;
            var cornerAdj = (s.X >= t.X && s.Y >= t.Y && s.Z >= t.Z) || (t.X >= s.X && t.Y >= s.Y && t.Z >= s.Z);
            return new TrianglePrismEdgeLedgerRow(i,e.Id.Value,kind,Point(s),Point(t),adj.Length,adj.Select(x=>x.Value).ToArray(),adjKinds,$"{len:F4}",dir,capSide,cornerAdj);
        }).ToArray();

        var vertexRows = body.Topology.Vertices.OrderBy(v=>v.Id.Value).Select((v,i)=>
        {
            body.TryGetVertexPoint(v.Id, out var p);
            var ies = body.Topology.Edges.Where(e=>e.StartVertexId==v.Id || e.EndVertexId==v.Id).OrderBy(e=>e.Id.Value).ToArray();
            var ifaces = body.Topology.Coedges.Where(c=>ies.Any(e=>e.Id==c.EdgeId)).Select(c=>loopFace[c.LoopId]).Distinct().OrderBy(f=>f.Value).ToArray();
            return new TrianglePrismVertexLedgerRow(i,v.Id.Value,Point(p),ies.Length,ifaces.Length,ies.Select(e=>e.Id.Value).ToArray(),ifaces.Select(f=>f.Value).ToArray(),ifaces.Select(f=>body.GetFaceSurface(f).Kind.ToString()).ToArray(), false);
        }).ToArray();

        var loopRows = body.Topology.Loops.OrderBy(l=>l.Id.Value).Select((l,i)=>
        {
            var coedges = body.Topology.Coedges.Where(c=>c.LoopId==l.Id).ToArray();
            return new TrianglePrismLoopLedgerRow(i,l.Id.Value,loopFace[l.Id].Value,coedges.Length,coedges.Select(c=>$"{c.EdgeId.Value}:{(c.IsReversed?"rev":"fwd")}").ToArray());
        }).ToArray();

        var faceCounts = body.Topology.Faces.GroupBy(f=>body.GetFaceSurface(f.Id).Kind.ToString()).ToDictionary(g=>g.Key,g=>g.Count());
        var curveCounts = body.Topology.Edges.GroupBy(e=> body.TryGetEdgeCurve(e.Id, out var curve) && curve is not null ? curve.Kind.ToString() : "none").ToDictionary(g=>g.Key,g=>g.Count());
        return new(body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count(), body.Topology.Loops.Count(), body.Topology.Coedges.Count(), Extents(body), faceCounts, curveCounts, faceRows, edgeRows, vertexRows, loopRows);
    }

    private static TrianglePrismChamferCandidateSummary SummarizeCornerCandidates(BrepBody body, double distance)
    {
        var result = BrepBoundedChamfer.ChamferTrustedPolyhedralSingleCorner(body, BrepBoundedChamferCorner.XMaxYMaxZMax, distance);
        var rejected = result.Diagnostics.Select(d => d.Message).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return new(1, result.IsSuccess ? 1 : 0, rejected);
    }

    private static Dictionary<LoopId, FaceId> LoopFaceMap(BrepBody body)
    {
        var map = new Dictionary<LoopId, FaceId>();
        foreach (var face in body.Topology.Faces)
            foreach (var loopId in face.LoopIds)
                map[loopId] = face.Id;
        return map;
    }

    private static string Extents(BrepBody body)
    {
        var pts = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : default).ToArray();
        var minX = pts.Min(p=>p.X); var maxX = pts.Max(p=>p.X); var minY=pts.Min(p=>p.Y); var maxY=pts.Max(p=>p.Y); var minZ=pts.Min(p=>p.Z); var maxZ=pts.Max(p=>p.Z);
        return $"[{minX:F3},{maxX:F3}]x[{minY:F3},{maxY:F3}]x[{minZ:F3},{maxZ:F3}]";
    }
    private static string Point(Point3D p) => $"{p.X:F3},{p.Y:F3},{p.Z:F3}";

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

    private static TrianglePrismBodyLedger EmptyLedger() => new(0,0,0,0,0,"n/a",new Dictionary<string,int>(),new Dictionary<string,int>(),[],[],[],[]);
    private static TrianglePrismChamferCandidateSummary EmptyChamfer() => new(0, 0, []);
}
