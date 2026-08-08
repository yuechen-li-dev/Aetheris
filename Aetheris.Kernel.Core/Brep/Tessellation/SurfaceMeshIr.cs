using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

/// <summary>
/// Derived, surface-aware mesh document.  This is deliberately not a topology or
/// mass-property authority: the B-rep remains the exact source of both.
/// </summary>
public sealed record SurfaceMeshDocument(
    IReadOnlyList<SurfaceMeshVertex> Vertices,
    IReadOnlyList<SurfacePatch> Patches,
    IReadOnlyList<SharedEdgeSamplePlan> SharedBoundaries,
    SurfaceMeshMetrics Metrics);

public sealed record SurfaceMeshVertex(int Id, Point3D Position, double? U = null, double? V = null);

/// <summary>One trim loop expressed both as shared B-rep samples and in the support plane's local coordinates.</summary>
public sealed record SurfaceMeshTrimLoop(
    LoopId LoopId,
    IReadOnlyList<int> VertexIds,
    IReadOnlyList<(double U, double V)> LocalCoordinates,
    bool IsInner,
    double SignedArea);

public sealed record FaceBoundaryUse(FaceId FaceId, LoopId LoopId, CoedgeId CoedgeId, bool IsReversed);

/// <summary>One exact curve sampled once in edge parameter order, then reused by every face use.</summary>
public sealed record SharedEdgeSamplePlan(
    EdgeId EdgeId,
    CurveGeometryKind CurveKind,
    ParameterInterval Interval,
    IReadOnlyList<SurfaceMeshVertex> Samples,
    IReadOnlyList<FaceBoundaryUse> Uses,
    bool IsClosed,
    double MaxChordalDeviation);

public enum SurfaceMeshSupportKind { Plane, Cylinder }
public enum SurfaceMeshCellKind { Quad, Triangle, BoundaryPolygon, Singular }
public sealed record SurfaceMeshSupport(SurfaceMeshSupportKind Kind, PlaneSurface? Plane = null, CylinderSurface? Cylinder = null);

public abstract record SurfaceMeshCell(SurfaceMeshCellKind Kind, IReadOnlyList<int> VertexIds, int RefinementLevel = 0, int? ParentCellId = null);
public sealed record QuadCell(IReadOnlyList<int> VertexIds, int RefinementLevel = 0, int? ParentCellId = null)
    : SurfaceMeshCell(SurfaceMeshCellKind.Quad, VertexIds, RefinementLevel, ParentCellId);
public sealed record TriangleCell(IReadOnlyList<int> VertexIds, int RefinementLevel = 0, int? ParentCellId = null)
    : SurfaceMeshCell(SurfaceMeshCellKind.Triangle, VertexIds, RefinementLevel, ParentCellId);
public sealed record BoundaryPolygonCell(IReadOnlyList<int> VertexIds, int RefinementLevel = 0, int? ParentCellId = null)
    : SurfaceMeshCell(SurfaceMeshCellKind.BoundaryPolygon, VertexIds, RefinementLevel, ParentCellId);
public sealed record SingularCell(IReadOnlyList<int> VertexIds, int RefinementLevel = 0, int? ParentCellId = null)
    : SurfaceMeshCell(SurfaceMeshCellKind.Singular, VertexIds, RefinementLevel, ParentCellId);

public sealed record SurfacePatch(
    FaceId FaceId,
    SurfaceMeshSupport Support,
    IReadOnlyList<LoopId> TrimLoops,
    IReadOnlyList<SurfaceMeshCell> Cells,
    bool SameSense,
    string? SemanticOwner = null,
    bool HasPeriodicUSeam = false,
    int MaxRefinementLevel = 0,
    IReadOnlyList<SurfaceMeshTrimLoop>? TrimLoopData = null);

public enum SurfaceMeshDownstreamIntent { Presentation, Manufacturing, Fea }

/// <summary>Small policy seam for task-aware meshing; no language surface is exposed yet.</summary>
public sealed record SurfaceMeshPolicy(
    double TargetChordalError,
    double TargetNormalErrorRadians,
    int MaxRefinementDepth,
    int MaxBoundarySamples,
    SurfaceMeshDownstreamIntent DownstreamIntent = SurfaceMeshDownstreamIntent.Presentation)
{
    public static SurfaceMeshPolicy FromDisplayOptions(DisplayTessellationOptions options) => new(
        options.ChordTolerance, options.AngularToleranceRadians, 8, options.MaximumSegments, SurfaceMeshDownstreamIntent.Presentation);
}

public sealed record SurfaceMeshMetrics(
    int PatchCount, int SharedBoundaryCount, int CellCount, int QuadCount, int ExceptionalCellCount,
    int TriangleCount, int MaxRefinementLevel, double MaxChordalDeviation, int BoundaryCrackCount,
    double MinEdgeLength, double MaxEdgeLength, string DeterministicHash,
    int BoundaryPolygonCount = 0,
    int NonManifoldEdgeCount = 0,
    int FinalTriangleCount = 0,
    double WorstAspectRatio = 0d,
    double MaxNormalDeviation = 0d,
    long ApproximateBufferBytes = 0);

public static class SurfaceMeshIrDebug
{
    public static string ToJson(SurfaceMeshDocument document)
        => JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// M1 planner/lowerer for closed Plane/Cylinder solids with line/circle trims.
/// It intentionally returns false for more general trimming so the named legacy
/// display tessellator remains a visible migration fallback.
/// </summary>
public static class SurfaceMeshIrTessellator
{
    private const double Epsilon = 1e-9d;

    public static bool TryTessellate(BrepBody body, DisplayTessellationOptions options, out DisplayTessellationResult result)
    {
        result = default!;
        var policy = SurfaceMeshPolicy.FromDisplayOptions(options);
        if (!TryBuild(body, policy, out var document)) return false;
        if (!SurfaceMeshIrValidator.TryValidate(document, out _)) return false;
        if (!TryLowerToTriangleMesh(document, out _, out _)) return false;
        var patches = document.Patches.OrderBy(p => p.FaceId.Value).Select(p => LowerPatch(document, p)).ToArray();
        var edges = document.SharedBoundaries.OrderBy(p => p.EdgeId.Value)
            .Select(p => new DisplayEdgePolyline(p.EdgeId, p.Samples.Select(s => s.Position).ToArray(), p.IsClosed)).ToArray();
        result = new DisplayTessellationResult(patches, edges, MeshPipeline: DisplayMeshPipeline.SurfaceMeshIr, SurfaceMeshMetrics: document.Metrics);
        return true;
    }

    /// <summary>Final deterministic lowering seam used by export.  Consumers only see triangles.</summary>
    public static bool TryLowerToTriangleMesh(SurfaceMeshDocument document, out TriangleMesh mesh, out TriangleMeshTopologyReport report)
    {
        var positions = document.Vertices.OrderBy(v => v.Id).Select(v => v.Position).ToList();
        var normals = document.Vertices.OrderBy(v => v.Id).Select(_ => new Vector3D(0d, 0d, 0d)).ToList();
        var indexById = document.Vertices.OrderBy(v => v.Id).Select((vertex, index) => (vertex.Id, index)).ToDictionary(item => item.Id, item => item.index);
        var indices = new List<int>();
        foreach (var patch in document.Patches.OrderBy(p => p.FaceId.Value))
        {
            var vertexById = document.Vertices.ToDictionary(v => v.Id);
            var exactNormal = ResolvePatchNormal(document, patch, vertexById);
            foreach (var cell in patch.Cells)
            {
                var ids = cell.VertexIds;
                if (cell.Kind == SurfaceMeshCellKind.Quad)
                {
                    var a = indexById[ids[0]]; var b = indexById[ids[1]]; var c = indexById[ids[2]]; var d = indexById[ids[3]];
                    var ac = (positions[a] - positions[c]).Length; var bd = (positions[b] - positions[d]).Length;
                    if (ac <= bd) indices.AddRange([a, b, c, a, c, d]); else indices.AddRange([a, b, d, b, c, d]);
                    normals[a] = exactNormal(positions[a]); normals[b] = exactNormal(positions[b]); normals[c] = exactNormal(positions[c]); normals[d] = exactNormal(positions[d]);
                }
                else if (cell.Kind == SurfaceMeshCellKind.Triangle)
                {
                    var a = indexById[ids[0]]; var b = indexById[ids[1]]; var c = indexById[ids[2]];
                    indices.AddRange([a, b, c]); normals[a] = exactNormal(positions[a]); normals[b] = exactNormal(positions[b]); normals[c] = exactNormal(positions[c]);
                }
                else
                {
                    var center = new Point3D(ids.Average(id => vertexById[id].Position.X), ids.Average(id => vertexById[id].Position.Y), ids.Average(id => vertexById[id].Position.Z));
                    var centerIndex = positions.Count; positions.Add(center); normals.Add(exactNormal(center));
                    for (var i = 0; i < ids.Count; i++)
                    {
                        var a = indexById[ids[i]]; var b = indexById[ids[(i + 1) % ids.Count]];
                        indices.AddRange([centerIndex, a, b]); normals[a] = exactNormal(positions[a]); normals[b] = exactNormal(positions[b]);
                    }
                }
            }
        }
        var hard = BuildHardEdges(document, indexById);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", positions.Select(p => $"{p.X:R},{p.Y:R},{p.Z:R}") ) + ";" + string.Join(',', indices)))).ToLowerInvariant();
        mesh = new TriangleMesh(positions, normals, indices, hard, hash);
        // A B-rep's shell orientation is authoritative.  The global sign is a
        // final deterministic correction, never a repair/weld operation.
        if (!TriangleMeshValidator.TryValidateClosed(mesh, out report, out _)
            && report.SignedVolume < 0d)
        {
            for (var i = 0; i < indices.Count; i += 3) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
            mesh = mesh with { TriangleIndices = indices };
        }
        return TriangleMeshValidator.TryValidateClosed(mesh, out report, out _);
    }

    private static IReadOnlySet<(int A, int B)> BuildHardEdges(SurfaceMeshDocument document, IReadOnlyDictionary<int, int> indexById)
    {
        var supportByFace = document.Patches.ToDictionary(p => p.FaceId, p => p.Support.Kind);
        var hard = new HashSet<(int A, int B)>();
        foreach (var boundary in document.SharedBoundaries)
        {
            var kinds = boundary.Uses.Select(use => supportByFace[use.FaceId]).Distinct().ToArray();
            if (kinds.Length < 2 || kinds.All(kind => kind == kinds[0])) continue;
            for (var i = 0; i + 1 < boundary.Samples.Count; i++)
            {
                var a = indexById[boundary.Samples[i].Id]; var b = indexById[boundary.Samples[i + 1].Id];
                hard.Add(a < b ? (a, b) : (b, a));
            }
        }
        return hard;
    }

    public static bool TryBuild(BrepBody body, SurfaceMeshPolicy policy, out SurfaceMeshDocument document)
    {
        document = default!;
        var vertices = new List<SurfaceMeshVertex>();
        var plans = new List<SharedEdgeSamplePlan>();
        var endpointVertices = new Dictionary<VertexId, SurfaceMeshVertex>();
        var nextVertexId = 0;
        foreach (var edge in body.Topology.Edges.OrderBy(e => e.Id.Value))
        {
            if (!TryPlanEdge(body, edge.Id, policy, endpointVertices, ref nextVertexId, out var plan)) return false;
            vertices.AddRange(plan.Samples);
            plans.Add(plan);
        }

        vertices = vertices.GroupBy(v => v.Id).Select(g => g.First()).OrderBy(v => v.Id).ToList();
        var byEdge = plans.ToDictionary(p => p.EdgeId);
        var patches = new List<SurfacePatch>();
        foreach (var face in body.Topology.Faces.OrderBy(f => f.Id.Value))
        {
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null ||
                surface.Kind is not (SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder) ||
                !body.Bindings.TryGetFaceBinding(face.Id, out var faceBinding)) return false;
            var patch = surface.Kind == SurfaceGeometryKind.Plane
                ? TryBuildPlanePatch(body, face.Id, faceBinding.SameSense, surface.Plane!.Value, byEdge, vertices, ref nextVertexId)
                : TryBuildCylinderPatch(body, face.Id, faceBinding.SameSense, surface.Cylinder!.Value, byEdge);
            if (patch is null) return false;
            patches.Add(patch);
        }

        vertices = vertices.GroupBy(v => v.Id).Select(g => g.First()).OrderBy(v => v.Id).ToList();
        var preMetrics = ComputeMetrics(vertices, patches, plans, triangleCount: 0);
        var builtDocument = new SurfaceMeshDocument(vertices, patches, plans, preMetrics);
        var triangleCount = builtDocument.Patches.Sum(p => CountLoweredTriangles(builtDocument, p));
        document = builtDocument with { Metrics = ComputeMetrics(vertices, patches, plans, triangleCount) };
        return true;
    }

    private static bool TryPlanEdge(BrepBody body, EdgeId edgeId, SurfaceMeshPolicy policy, Dictionary<VertexId, SurfaceMeshVertex> endpointVertices, ref int nextVertexId, out SharedEdgeSamplePlan plan)
    {
        plan = default!;
        if (!body.Bindings.TryGetEdgeBinding(edgeId, out var binding) || !body.TryGetEdgeCurveGeometry(edgeId, out var curve) || curve is null) return false;
        var interval = binding.TrimInterval ?? new ParameterInterval(0d, 1d);
        List<(double Parameter, Point3D Point)> sampled;
        bool closed;
        double chordError;
        switch (curve.Kind)
        {
            case CurveGeometryKind.Line3 when curve.Line3 is { } line:
                // A bounded through-hole ring needs matching side-face boundary
                // vertices.  Split only line edges on bodies that actually carry
                // circular trims; ordinary flat boxes remain one coarse quad/face.
                var lineSegments = body.Geometry.Curves.Any(c => c.Value.Kind == CurveGeometryKind.Circle3) ? 9 : 1;
                sampled = Enumerable.Range(0, lineSegments + 1)
                    .Select(i => (interval.Start + ((interval.End - interval.Start) * i / lineSegments), line.Evaluate(interval.Start + ((interval.End - interval.Start) * i / lineSegments))))
                    .ToList();
                closed = false; chordError = 0d; break;
            case CurveGeometryKind.Circle3 when curve.Circle3 is { } circle:
                var span = interval.End - interval.Start;
                var segments = ResolveCircleSegments(circle.Radius, span, policy);
                sampled = Enumerable.Range(0, segments + 1).Select(i =>
                {
                    var parameter = interval.Start + (span * i / segments);
                    return (parameter, circle.Evaluate(parameter));
                }).ToList();
                closed = double.Abs(double.Abs(span) - (2d * double.Pi)) <= Epsilon;
                chordError = circle.Radius * (1d - double.Cos(double.Abs(span) / (2d * segments)));
                break;
            default: return false;
        }
        if (!body.TryGetEdgeVertices(edgeId, out var startVertex, out var endVertex)) return false;
        var samples = new SurfaceMeshVertex[sampled.Count];
        for (var i = 0; i < sampled.Count; i++)
        {
            var endpoint = i == 0 ? startVertex : i == sampled.Count - 1 ? endVertex : default;
            if (endpoint.IsValid && endpointVertices.TryGetValue(endpoint, out var existing))
            {
                samples[i] = existing;
            }
            else
            {
                samples[i] = new SurfaceMeshVertex(nextVertexId++, sampled[i].Point);
                if (endpoint.IsValid) endpointVertices.Add(endpoint, samples[i]);
            }
        }
        var uses = body.Topology.Faces.OrderBy(f => f.Id.Value).SelectMany(face => body.GetLoopIds(face.Id).SelectMany(loop => body.GetCoedgeIds(loop)
            .Select(coedgeId => body.Topology.GetCoedge(coedgeId)).Where(c => c.EdgeId == edgeId)
            .Select(c => new FaceBoundaryUse(face.Id, loop, c.Id, c.IsReversed)))).ToArray();
        plan = new SharedEdgeSamplePlan(edgeId, curve.Kind, interval, samples, uses, closed, chordError);
        return true;
    }

    private static int ResolveCircleSegments(double radius, double span, SurfaceMeshPolicy policy)
    {
        var chord = policy.TargetChordalError >= radius ? 1 : (int)double.Ceiling(double.Abs(span) / (2d * double.Acos(double.Clamp(1d - (policy.TargetChordalError / radius), -1d, 1d))));
        var normal = (int)double.Ceiling(double.Abs(span) / policy.TargetNormalErrorRadians);
        // Keep the established display-circle baseline stable while the policy grows a
        // dedicated edge-density knob; legacy display callers historically receive 36.
        var baseline = double.Abs(double.Abs(span) - (2d * double.Pi)) <= Epsilon ? 36 : 1;
        return int.Max(baseline, int.Max(1, int.Max(chord, normal)));
    }

    private static SurfacePatch? TryBuildPlanePatch(BrepBody body, FaceId faceId, bool sameSense, PlaneSurface plane, IReadOnlyDictionary<EdgeId, SharedEdgeSamplePlan> plans, List<SurfaceMeshVertex> vertices, ref int nextVertexId)
    {
        var loops = body.GetLoopIds(faceId);
        if (loops.Count == 0) return null;
        var loopData = new List<SurfaceMeshTrimLoop>(loops.Count);
        foreach (var loop in loops)
        {
            var coedges = body.GetCoedgeIds(loop).Select(body.Topology.GetCoedge).ToArray();
            if (coedges.Length == 0) return null;
            var ids = FlattenLoop(coedges, plans);
            if (ids.Count < 3) return null;
            var local = ids.Select(id => ToPlaneLocal(plane, vertices.First(v => v.Id == id).Position)).ToArray();
            loopData.Add(new SurfaceMeshTrimLoop(loop, ids, local, false, SignedArea(local)));
        }

        // M2 deliberately covers the common bounded topology: a rectangular
        // outer boundary plus circular inner trims.  It is not a general
        // polygon mesher; unsupported trims retain the explicit legacy route.
        var outer = loopData.OrderByDescending(loop => double.Abs(loop.SignedArea)).First();
        var inners = loopData.Where(loop => loop.LoopId != outer.LoopId).ToArray();
        var annotatedLoops = loopData.Select(loop => loop with { IsInner = loop.LoopId != outer.LoopId }).ToArray();
        if (inners.Length == 0)
        {
            var coedges = body.GetCoedgeIds(outer.LoopId).Select(body.Topology.GetCoedge).ToArray();
            var cell = coedges.Length == 4 && coedges.All(c => plans[c.EdgeId].CurveKind == CurveGeometryKind.Line3) && outer.VertexIds.Count == 4
                ? (SurfaceMeshCell)new QuadCell(Orient(outer.VertexIds, sameSense))
                : new BoundaryPolygonCell(Orient(outer.VertexIds, sameSense));
            return new SurfacePatch(faceId, new SurfaceMeshSupport(SurfaceMeshSupportKind.Plane, Plane: plane), loops, [cell], sameSense, TrimLoopData: annotatedLoops);
        }
        if (inners.Length != 1 || !IsRectangle(outer.LocalCoordinates) || inners[0].VertexIds.Count != outer.VertexIds.Count) return null;
        var inner = inners[0];
        var vertexById = vertices.ToDictionary(v => v.Id);
        var outerIds = EnsureCounterClockwise(outer.VertexIds, outer.LocalCoordinates);
        var innerIds = AlignRingToOuter(
            outerIds,
            EnsureCounterClockwise(inner.VertexIds, inner.LocalCoordinates),
            vertexById);
        var cells = new List<SurfaceMeshCell>(outerIds.Count * 2);
        var middle = new int[outerIds.Count];
        for (var i = 0; i < outerIds.Count; i++)
        {
            var outerPoint = vertexById[outerIds[i]].Position;
            var innerPoint = vertexById[innerIds[i]].Position;
            // One grading band prevents a giant cell from landing directly on
            // the curved trim while keeping the zero-curvature plane cheap.
            var point = new Point3D(
                outerPoint.X * 0.40d + innerPoint.X * 0.60d,
                outerPoint.Y * 0.40d + innerPoint.Y * 0.60d,
                outerPoint.Z * 0.40d + innerPoint.Z * 0.60d);
            middle[i] = nextVertexId++;
            var uv = ToPlaneLocal(plane, point);
            vertices.Add(new SurfaceMeshVertex(middle[i], point, uv.U, uv.V));
        }
        for (var i = 0; i < outerIds.Count; i++)
        {
            var next = (i + 1) % outerIds.Count;
            cells.Add(new QuadCell(Orient([outerIds[i], outerIds[next], middle[next], middle[i]], sameSense), 0));
            cells.Add(new QuadCell(Orient([middle[i], middle[next], innerIds[next], innerIds[i]], sameSense), 1));
        }
        return new SurfacePatch(faceId, new SurfaceMeshSupport(SurfaceMeshSupportKind.Plane, Plane: plane), loops, cells, sameSense, MaxRefinementLevel: 1, TrimLoopData: annotatedLoops);
    }

    private static SurfacePatch? TryBuildCylinderPatch(BrepBody body, FaceId faceId, bool sameSense, CylinderSurface cylinder, IReadOnlyDictionary<EdgeId, SharedEdgeSamplePlan> plans)
    {
        var loops = body.GetLoopIds(faceId);
        if (loops.Count != 1) return null;
        var coedges = body.GetCoedgeIds(loops[0]).Select(body.Topology.GetCoedge).ToArray();
        var circles = coedges.Where(c => plans[c.EdgeId].CurveKind == CurveGeometryKind.Circle3).Select(c => plans[c.EdgeId]).ToArray();
        if (circles.Length != 2 || circles[0].Samples.Count != circles[1].Samples.Count || !circles.All(p => p.IsClosed)) return null;
        var rings = circles.Select(p => p.Samples.Take(p.Samples.Count - 1).ToArray()).ToArray();
        if (rings[0].Length < 3) return null;
        var v0 = (rings[0][0].Position - cylinder.Origin).Dot(cylinder.Axis.ToVector());
        var v1 = (rings[1][0].Position - cylinder.Origin).Dot(cylinder.Axis.ToVector());
        var bottom = v0 <= v1 ? rings[0] : rings[1];
        var top = v0 <= v1 ? rings[1] : rings[0];
        var cells = new List<SurfaceMeshCell>(bottom.Length);
        for (var i = 0; i < bottom.Length; i++)
        {
            var next = (i + 1) % bottom.Length;
            cells.Add(new QuadCell(Orient([bottom[i].Id, bottom[next].Id, top[next].Id, top[i].Id], sameSense)));
        }
        return new SurfacePatch(faceId, new SurfaceMeshSupport(SurfaceMeshSupportKind.Cylinder, Cylinder: cylinder), loops, cells, sameSense, HasPeriodicUSeam: true);
    }

    private static IReadOnlyList<int> FlattenLoop(IReadOnlyList<Coedge> coedges, IReadOnlyDictionary<EdgeId, SharedEdgeSamplePlan> plans)
    {
        var output = new List<int>();
        foreach (var coedge in coedges)
        {
            var ids = plans[coedge.EdgeId].Samples.Select(s => s.Id).ToArray();
            if (coedge.IsReversed) Array.Reverse(ids);
            if (plans[coedge.EdgeId].IsClosed) ids = ids[..^1];
            if (output.Count > 0 && ids.Length > 0 && output[^1] == ids[0]) ids = ids[1..];
            output.AddRange(ids);
        }
        if (output.Count > 1 && output[0] == output[^1]) output.RemoveAt(output.Count - 1);
        return output;
    }

    private static (double U, double V) ToPlaneLocal(PlaneSurface plane, Point3D point)
    {
        var offset = point - plane.Origin;
        return (offset.Dot(plane.UAxis.ToVector()), offset.Dot(plane.VAxis.ToVector()));
    }

    private static double SignedArea(IReadOnlyList<(double U, double V)> points)
        => Enumerable.Range(0, points.Count).Sum(i =>
        {
            var next = points[(i + 1) % points.Count];
            return (points[i].U * next.V) - (next.U * points[i].V);
        }) * 0.5d;

    private static IReadOnlyList<int> EnsureCounterClockwise(IReadOnlyList<int> ids, IReadOnlyList<(double U, double V)> local)
        => SignedArea(local) >= 0d ? ids : ids.Reverse().ToArray();

    private static IReadOnlyList<int> AlignRingToOuter(
        IReadOnlyList<int> outerIds,
        IReadOnlyList<int> innerIds,
        IReadOnlyDictionary<int, SurfaceMeshVertex> vertexById)
    {
        // Loop orientation alone does not establish the cyclic phase.  Pairing
        // a rectangular loop that begins at a corner with a circle that begins
        // on its +X axis can otherwise create a twisted annular strip whose
        // quads overlap the hole.  Select the deterministic cyclic shift with
        // the shortest total spokes while preserving both loop orderings.
        var bestShift = 0;
        var bestCost = double.PositiveInfinity;
        for (var shift = 0; shift < innerIds.Count; shift++)
        {
            var cost = 0d;
            for (var i = 0; i < outerIds.Count; i++)
            {
                var delta = vertexById[outerIds[i]].Position - vertexById[innerIds[(i + shift) % innerIds.Count]].Position;
                cost += delta.LengthSquared;
            }
            if (cost < bestCost - Epsilon)
            {
                bestCost = cost;
                bestShift = shift;
            }
        }
        return Enumerable.Range(0, innerIds.Count).Select(i => innerIds[(i + bestShift) % innerIds.Count]).ToArray();
    }

    private static bool IsRectangle(IReadOnlyList<(double U, double V)> points)
    {
        var minU = points.Min(p => p.U); var maxU = points.Max(p => p.U);
        var minV = points.Min(p => p.V); var maxV = points.Max(p => p.V);
        return points.All(p => double.Abs(p.U - minU) < 1e-7d || double.Abs(p.U - maxU) < 1e-7d || double.Abs(p.V - minV) < 1e-7d || double.Abs(p.V - maxV) < 1e-7d);
    }

    private static IReadOnlyList<int> Orient(IReadOnlyList<int> ids, bool sameSense) => sameSense ? ids : ids.Reverse().ToArray();

    private static DisplayFaceMeshPatch LowerPatch(SurfaceMeshDocument document, SurfacePatch patch)
    {
        var vertexById = document.Vertices.ToDictionary(v => v.Id);
        var positions = new List<Point3D>(); var normals = new List<Vector3D>(); var indices = new List<int>(); var local = new Dictionary<int, int>();
        var normal = ResolvePatchNormal(document, patch, vertexById);
        int Add(int id) { if (local.TryGetValue(id, out var existing)) return existing; var index = positions.Count; positions.Add(vertexById[id].Position); normals.Add(normal(vertexById[id].Position)); local[id] = index; return index; }
        foreach (var cell in patch.Cells)
        {
            var ids = cell.VertexIds;
            if (cell.Kind == SurfaceMeshCellKind.Quad)
            {
                var a = Add(ids[0]); var b = Add(ids[1]); var c = Add(ids[2]); var d = Add(ids[3]);
                var ac = (positions[a] - positions[c]).Length; var bd = (positions[b] - positions[d]).Length;
                if (ac <= bd) { indices.AddRange([a, b, c, a, c, d]); } else { indices.AddRange([a, b, d, b, c, d]); }
            }
            else if (cell.Kind == SurfaceMeshCellKind.Triangle) indices.AddRange([Add(ids[0]), Add(ids[1]), Add(ids[2])]);
            else if (ids.Count >= 3)
            {
                var center = new Point3D(ids.Average(id => vertexById[id].Position.X), ids.Average(id => vertexById[id].Position.Y), ids.Average(id => vertexById[id].Position.Z));
                var centerIndex = positions.Count; positions.Add(center); normals.Add(normal(center));
                for (var i = 0; i < ids.Count; i++) indices.AddRange([centerIndex, Add(ids[i]), Add(ids[(i + 1) % ids.Count])]);
            }
        }
        return new DisplayFaceMeshPatch(patch.FaceId, positions, normals, indices, DisplayFaceMeshSource.SurfaceMeshIr);
    }

    private static Func<Point3D, Vector3D> ResolvePatchNormal(SurfaceMeshDocument document, SurfacePatch patch, IReadOnlyDictionary<int, SurfaceMeshVertex> vertices)
    {
        return patch.Support.Kind switch
        {
            SurfaceMeshSupportKind.Plane when patch.Support.Plane is { } plane => _ => patch.SameSense ? plane.Normal.ToVector() : -plane.Normal.ToVector(),
            SurfaceMeshSupportKind.Cylinder when patch.Support.Cylinder is { } cylinder => p =>
            {
                var offset = p - cylinder.Origin;
                var angle = double.Atan2(offset.Dot(cylinder.YAxis.ToVector()), offset.Dot(cylinder.XAxis.ToVector()));
                var exact = cylinder.Normal(angle).ToVector();
                return patch.SameSense ? exact : -exact;
            },
            _ => throw new InvalidOperationException($"Patch {patch.FaceId.Value} has no exact support evaluator."),
        };
    }

    private static int CountLoweredTriangles(SurfaceMeshDocument document, SurfacePatch patch) => patch.Cells.Sum(c => c.Kind switch { SurfaceMeshCellKind.Quad => 2, SurfaceMeshCellKind.Triangle => 1, _ => c.VertexIds.Count >= 3 ? c.VertexIds.Count : 0 });
    private static SurfaceMeshMetrics ComputeMetrics(IReadOnlyList<SurfaceMeshVertex> vertices, IReadOnlyList<SurfacePatch> patches, IReadOnlyList<SharedEdgeSamplePlan> plans, int triangleCount)
    {
        var lengths = plans.SelectMany(p => p.Samples.Zip(p.Samples.Skip(1), (a, b) => (a.Position - b.Position).Length)).ToArray();
        var cells = patches.SelectMany(p => p.Cells).ToArray();
        var payload = string.Join("|", vertices.OrderBy(v => v.Id).Select(v => $"{v.Id}:{v.Position.X:R},{v.Position.Y:R},{v.Position.Z:R}")) + string.Join("|", cells.Select(c => $"{c.Kind}:{string.Join(',', c.VertexIds)}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var boundaryPolygons = cells.Count(c => c.Kind == SurfaceMeshCellKind.BoundaryPolygon);
        var approximateBytes = (vertices.Count * (sizeof(int) + (3 * sizeof(double)))) + (cells.Sum(c => c.VertexIds.Count) * sizeof(int));
        return new SurfaceMeshMetrics(patches.Count, plans.Count, cells.Length, cells.Count(c => c.Kind == SurfaceMeshCellKind.Quad), cells.Count(c => c.Kind != SurfaceMeshCellKind.Quad), triangleCount, cells.Select(c => c.RefinementLevel).DefaultIfEmpty().Max(), plans.Select(p => p.MaxChordalDeviation).DefaultIfEmpty().Max(), 0, lengths.DefaultIfEmpty().Min(), lengths.DefaultIfEmpty().Max(), hash, boundaryPolygons, 0, triangleCount, 0d, 0d, approximateBytes);
    }
}

public static class SurfaceMeshIrValidator
{
    public static bool TryValidate(SurfaceMeshDocument document, out string? failure)
    {
        var vertices = document.Vertices.Select(v => v.Id).ToHashSet();
        if (vertices.Count != document.Vertices.Count) { failure = "SurfaceMeshIR vertex IDs are not unique."; return false; }
        foreach (var boundary in document.SharedBoundaries)
        {
            if (boundary.Samples.Count < 2 || boundary.Uses.Count == 0) { failure = $"Shared edge {boundary.EdgeId.Value} has no complete sample/use contract."; return false; }
            if (boundary.Samples.Any(sample => !vertices.Contains(sample.Id))) { failure = $"Shared edge {boundary.EdgeId.Value} references a missing vertex."; return false; }
            if (boundary.IsClosed && boundary.Samples[0].Id != boundary.Samples[^1].Id) { failure = $"Closed shared edge {boundary.EdgeId.Value} does not reuse its endpoint vertex."; return false; }
        }
        foreach (var cell in document.Patches.SelectMany(p => p.Cells))
        {
            if (cell.VertexIds.Count < 3 || cell.VertexIds.Any(id => !vertices.Contains(id))) { failure = "A cell references an invalid or insufficient vertex set."; return false; }
            if (cell.VertexIds.Distinct().Count() != cell.VertexIds.Count) { failure = "A cell repeats a vertex."; return false; }
            var points = cell.VertexIds.Select(id => document.Vertices.Single(v => v.Id == id).Position).ToArray();
            if (PolygonAreaMagnitude(points) <= 1e-14d) { failure = "A cell has zero area."; return false; }
        }
        if (document.Patches.Any(patch => patch.TrimLoopData is { Count: > 1 } && patch.Cells.Count == 0)) { failure = "A trimmed patch has no covering cells."; return false; }
        failure = null; return true;
    }

    private static double PolygonAreaMagnitude(IReadOnlyList<Point3D> points)
    {
        var normal = new Vector3D(0d, 0d, 0d);
        for (var i = 0; i < points.Count; i++) normal += new Vector3D(points[i].X, points[i].Y, points[i].Z).Cross(new Vector3D(points[(i + 1) % points.Count].X, points[(i + 1) % points.Count].Y, points[(i + 1) % points.Count].Z));
        return normal.Length * 0.5d;
    }
}
