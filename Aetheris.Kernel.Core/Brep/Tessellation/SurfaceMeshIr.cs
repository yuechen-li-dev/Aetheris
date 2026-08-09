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

public enum SurfaceMeshSupportKind { Plane, Cylinder, Cone, Sphere, Torus }
public enum SurfaceMeshCellKind { Quad, Triangle, BoundaryPolygon, Singular }
public sealed record SurfaceMeshSupport(
    SurfaceMeshSupportKind Kind,
    PlaneSurface? Plane = null,
    CylinderSurface? Cylinder = null,
    ConeSurface? Cone = null,
    SphereSurface? Sphere = null,
    TorusSurface? Torus = null);

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
    bool HasPeriodicVSeam = false,
    int MaxRefinementLevel = 0,
    IReadOnlyList<SurfaceMeshTrimLoop>? TrimLoopData = null,
    string? ChartId = null);

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

/// <summary>
/// A deliberately bounded correspondence for a four-sided analytic patch.  It is
/// not an arbitrary trim remesher: the two transverse boundaries retain their
/// authoritative B-rep samples and each pair of opposing boundaries has the same
/// deterministic segment count.  Interior points are evaluated on the support.
/// </summary>
public sealed record TrimBandPlan(
    EdgeId StructuredGuideEdge,
    EdgeId ExactTrimEdge,
    EdgeId StartSideEdge,
    EdgeId EndSideEdge,
    int AngularSegments,
    int GeneratorSegments,
    string CorrespondenceRule = "matched edge parameter order; interpolate in analytic support coordinates");

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

/// <summary>Read-only coverage accounting for an imported B-rep before a structured build.</summary>
public sealed record SurfaceMeshIrCoverageAudit(
    int FaceCount,
    int AnalyticSupportFaceCount,
    int UnsupportedSupportFaceCount,
    IReadOnlyDictionary<string, int> EdgeCurveFamilies,
    IReadOnlyList<SurfaceMeshIrCoverageBlocker> BoundaryBlockers);

public sealed record SurfaceMeshIrCoverageBlocker(int EdgeId, string CurveFamily, IReadOnlyList<int> FaceIds);

public static class SurfaceMeshIrDebug
{
    public static string ToJson(SurfaceMeshDocument document)
        => JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// Planner/lowerer for the analytic surface subset with explicit support topology.
/// General trim remeshing remains a visible legacy fallback; supported patches retain
/// quads until the final deterministic triangle lowering step.
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

    /// <summary>
    /// Reports generic support coverage without invoking a fallback or modifying
    /// topology. It is intentionally conservative: an analytic support face is
    /// counted separately from whether its bounded trim is currently plannable.
    /// </summary>
    public static SurfaceMeshIrCoverageAudit Audit(BrepBody body)
    {
        var families = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var blockers = new List<SurfaceMeshIrCoverageBlocker>();
        var analytic = 0;
        var unsupportedSupports = 0;
        foreach (var face in body.Topology.Faces)
        {
            if (body.TryGetFaceSurfaceGeometry(face.Id, out var surface) && surface?.Kind is SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Cone or SurfaceGeometryKind.Sphere or SurfaceGeometryKind.Torus) analytic++;
            else unsupportedSupports++;
        }
        foreach (var edge in body.Topology.Edges.OrderBy(edge => edge.Id.Value))
        {
            var family = body.TryGetEdgeCurveGeometry(edge.Id, out var curve) && curve is not null ? curve.Kind.ToString() : "Unbound";
            families[family] = families.GetValueOrDefault(family) + 1;
            if (family is "Line3" or "Circle3" or "Hyperbola3") continue;
            var uses = body.Topology.Faces
                .Where(face => body.GetLoopIds(face.Id).SelectMany(body.GetCoedgeIds).Any(coedgeId => body.Topology.GetCoedge(coedgeId).EdgeId == edge.Id))
                .Select(face => face.Id.Value).OrderBy(id => id).ToArray();
            blockers.Add(new SurfaceMeshIrCoverageBlocker(edge.Id.Value, family, uses));
        }
        return new SurfaceMeshIrCoverageAudit(body.Topology.Faces.Count(), analytic, unsupportedSupports, families, blockers);
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
        // Multi-chart supports such as a sphere legitimately contribute several
        // patches for one B-rep face; all charts share the same smooth support.
        var supportByFace = document.Patches
            .GroupBy(patch => patch.FaceId)
            .ToDictionary(group => group.Key, group => group.First().Support.Kind);
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
        => TryBuild(body, policy, out document, out _);

    /// <summary>Builds the structured document and exposes the first generic unsupported contract.</summary>
    public static bool TryBuild(BrepBody body, SurfaceMeshPolicy policy, out SurfaceMeshDocument document, out string? failure)
    {
        document = default!; failure = null;
        var vertices = new List<SurfaceMeshVertex>();
        var plans = new List<SharedEdgeSamplePlan>();
        var endpointVertices = new Dictionary<VertexId, SurfaceMeshVertex>();
        var nextVertexId = 0;
        // A bounded conic/torus band can only use shared edge IDs when every
        // opposing boundary has a common cell lattice.  Establish those counts
        // before sampling anything; this is what makes the Hyperbola authoritative
        // rather than a face-local approximation.
        var structuredSegments = BuildStructuredSegmentConstraints(body, policy);
        foreach (var edge in body.Topology.Edges.OrderBy(e => e.Id.Value))
        {
            if (!TryPlanEdge(body, edge.Id, policy, endpointVertices, ref nextVertexId, structuredSegments.TryGetValue(edge.Id, out var forcedSegments) ? forcedSegments : null, out var plan))
            {
                failure = body.TryGetEdgeCurveGeometry(edge.Id, out var curve) && curve is not null
                    ? $"SurfaceMeshIR does not support edge {edge.Id.Value} with curve family {curve.Kind}."
                    : $"SurfaceMeshIR could not resolve exact geometry for edge {edge.Id.Value}.";
                return false;
            }
            vertices.AddRange(plan.Samples);
            plans.Add(plan);
        }

        vertices = vertices.GroupBy(v => v.Id).Select(g => g.First()).OrderBy(v => v.Id).ToList();
        var byEdge = plans.ToDictionary(p => p.EdgeId);
        var patches = new List<SurfacePatch>();
        foreach (var face in body.Topology.Faces.OrderBy(f => f.Id.Value))
        {
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null ||
                surface.Kind is not (SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Cone or SurfaceGeometryKind.Sphere or SurfaceGeometryKind.Torus) ||
                !body.Bindings.TryGetFaceBinding(face.Id, out var faceBinding))
            {
                failure = $"SurfaceMeshIR does not support face {face.Id.Value}: the face has no bound Plane/Cylinder/Cone/Sphere/Torus support.";
                return false;
            }
            switch (surface.Kind)
            {
                case SurfaceGeometryKind.Plane:
                    var planePatch = TryBuildPlanePatch(body, face.Id, faceBinding.SameSense, surface.Plane!.Value, byEdge, vertices, ref nextVertexId);
                    if (planePatch is null) { failure = $"SurfaceMeshIR does not support trim topology on planar face {face.Id.Value}."; return false; }
                    patches.Add(planePatch);
                    break;
                case SurfaceGeometryKind.Cylinder:
                    var cylinderPatch = TryBuildCylinderPatch(body, face.Id, faceBinding.SameSense, surface.Cylinder!.Value, byEdge, vertices, ref nextVertexId);
                    if (cylinderPatch is null) { failure = $"SurfaceMeshIR does not support trim topology on cylindrical face {face.Id.Value}."; return false; }
                    patches.Add(cylinderPatch);
                    break;
                case SurfaceGeometryKind.Cone:
                    var conePatch = TryBuildConePatch(body, face.Id, faceBinding.SameSense, surface.Cone!.Value, byEdge, vertices, ref nextVertexId);
                    if (conePatch is null) { failure = $"SurfaceMeshIR does not support trim topology on conical face {face.Id.Value}."; return false; }
                    patches.Add(conePatch);
                    break;
                case SurfaceGeometryKind.Sphere:
                    if (!TryBuildSphereCharts(body, face.Id, faceBinding.SameSense, surface.Sphere!.Value, vertices, ref nextVertexId, out var spherePatches)) { failure = $"SurfaceMeshIR does not support chart/trim topology on spherical face {face.Id.Value}."; return false; }
                    patches.AddRange(spherePatches);
                    break;
                case SurfaceGeometryKind.Torus:
                    var torusPatch = TryBuildTorusPatch(body, face.Id, faceBinding.SameSense, surface.Torus!.Value, byEdge, policy, vertices, ref nextVertexId);
                    if (torusPatch is null) { failure = $"SurfaceMeshIR does not support trim topology on toroidal face {face.Id.Value}."; return false; }
                    patches.Add(torusPatch);
                    break;
            }
        }

        vertices = vertices.GroupBy(v => v.Id).Select(g => g.First()).OrderBy(v => v.Id).ToList();
        // Some construction recipes retain source-only topological edges after a
        // replacement trim has been emitted.  They have no face use and are not a
        // mesh boundary contract, so keep them out of the SurfaceMeshIR document.
        var sharedPlans = plans.Where(plan => plan.Uses.Count > 0).ToArray();
        var preMetrics = ComputeMetrics(vertices, patches, sharedPlans, triangleCount: 0);
        var builtDocument = new SurfaceMeshDocument(vertices, patches, sharedPlans, preMetrics);
        var triangleCount = builtDocument.Patches.Sum(p => CountLoweredTriangles(builtDocument, p));
        document = builtDocument with { Metrics = ComputeMetrics(vertices, patches, sharedPlans, triangleCount) };
        return true;
    }

    private static bool TryPlanEdge(BrepBody body, EdgeId edgeId, SurfaceMeshPolicy policy, Dictionary<VertexId, SurfaceMeshVertex> endpointVertices, ref int nextVertexId, int? forcedSegments, out SharedEdgeSamplePlan plan)
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
                var lineSegments = forcedSegments ?? (body.Geometry.Curves.Any(c => c.Value.Kind == CurveGeometryKind.Circle3) ? 9 : 1);
                sampled = Enumerable.Range(0, lineSegments + 1)
                    .Select(i => (interval.Start + ((interval.End - interval.Start) * i / lineSegments), line.Evaluate(interval.Start + ((interval.End - interval.Start) * i / lineSegments))))
                    .ToList();
                closed = false; chordError = 0d; break;
            case CurveGeometryKind.Circle3 when curve.Circle3 is { } circle:
                var span = interval.End - interval.Start;
                var segments = int.Max(ResolveCircleSegments(circle.Radius, span, policy), forcedSegments ?? 1);
                sampled = Enumerable.Range(0, segments + 1).Select(i =>
                {
                    var parameter = interval.Start + (span * i / segments);
                    return (parameter, circle.Evaluate(parameter));
                }).ToList();
                closed = double.Abs(double.Abs(span) - (2d * double.Pi)) <= Epsilon;
                chordError = circle.Radius * (1d - double.Cos(double.Abs(span) / (2d * segments)));
                break;
            case CurveGeometryKind.Hyperbola3 when curve.Hyperbola3 is { } hyperbola:
                sampled = SampleHyperbola(hyperbola, interval, policy);
                if (forcedSegments is { } count && count > 0)
                    sampled = Enumerable.Range(0, count + 1)
                        .Select(i => (interval.Start + ((interval.End - interval.Start) * i / count), hyperbola.Evaluate(interval.Start + ((interval.End - interval.Start) * i / count))))
                        .ToList();
                closed = false;
                chordError = ComputePolylineChordDeviation(hyperbola.Evaluate, sampled);
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

    private static IReadOnlyDictionary<EdgeId, int> BuildStructuredSegmentConstraints(BrepBody body, SurfaceMeshPolicy policy)
    {
        var result = new Dictionary<EdgeId, int>();
        foreach (var face in body.Topology.Faces.OrderBy(face => face.Id.Value))
        {
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null ||
                surface.Kind is not (SurfaceGeometryKind.Cone or SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Torus)) continue;
            var loops = body.GetLoopIds(face.Id);
            if (loops.Count != 1) continue;
            var coedges = body.GetCoedgeIds(loops[0]).Select(body.Topology.GetCoedge).ToArray();
            if (coedges.Length != 4) continue;
            var candidates = new List<(EdgeId Edge, int Segments)>();
            foreach (var coedge in coedges)
            {
                if (!body.TryGetEdgeCurveGeometry(coedge.EdgeId, out var curve) || curve is null ||
                    !body.Bindings.TryGetEdgeBinding(coedge.EdgeId, out var binding)) { candidates.Clear(); break; }
                var interval = binding.TrimInterval ?? new ParameterInterval(0d, 1d);
                var count = curve.Kind switch
                {
                    CurveGeometryKind.Circle3 when curve.Circle3 is { } circle => ResolveCircleSegments(circle.Radius, interval.End - interval.Start, policy),
                    CurveGeometryKind.Hyperbola3 when curve.Hyperbola3 is { } hyperbola => SampleHyperbola(hyperbola, interval, policy).Count - 1,
                    CurveGeometryKind.Line3 => body.Geometry.Curves.Any(item => item.Value.Kind == CurveGeometryKind.Circle3) ? 9 : 1,
                    _ => 0
                };
                if (count <= 0) { candidates.Clear(); break; }
                candidates.Add((coedge.EdgeId, count));
            }
            if (candidates.Count != 4) continue;
            // Opposing boundaries share a lattice direction.  Do not force the
            // transverse direction to the same density: a root torus needs many
            // samples around its major span but only a few across its minor arc.
            var transverse = int.Max(candidates[0].Segments, candidates[2].Segments);
            var longitudinal = int.Max(candidates[1].Segments, candidates[3].Segments);
            foreach (var candidate in new[] { candidates[0], candidates[2] })
                result[candidate.Edge] = int.Max(result.GetValueOrDefault(candidate.Edge), transverse);
            foreach (var candidate in new[] { candidates[1], candidates[3] })
                result[candidate.Edge] = int.Max(result.GetValueOrDefault(candidate.Edge), longitudinal);
        }
        return result;
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

    private static List<(double Parameter, Point3D Point)> SampleHyperbola(Hyperbola3Curve hyperbola, ParameterInterval interval, SurfaceMeshPolicy policy)
    {
        var samples = new List<(double Parameter, Point3D Point)> { (interval.Start, hyperbola.Evaluate(interval.Start)) };
        AppendHyperbolaSegment(hyperbola, interval.Start, interval.End, samples[0].Point, hyperbola.Evaluate(interval.End), policy, samples, 0);
        return samples;
    }

    private static void AppendHyperbolaSegment(
        Hyperbola3Curve hyperbola,
        double start,
        double end,
        Point3D startPoint,
        Point3D endPoint,
        SurfaceMeshPolicy policy,
        List<(double Parameter, Point3D Point)> samples,
        int depth)
    {
        var middle = (start + end) * 0.5d;
        var middlePoint = hyperbola.Evaluate(middle);
        var chordMidpoint = new Point3D((startPoint.X + endPoint.X) * 0.5d, (startPoint.Y + endPoint.Y) * 0.5d, (startPoint.Z + endPoint.Z) * 0.5d);
        var deviation = (middlePoint - chordMidpoint).Length;
        if (depth >= policy.MaxRefinementDepth || samples.Count + 1 >= policy.MaxBoundarySamples || deviation <= policy.TargetChordalError)
        {
            samples.Add((end, endPoint));
            return;
        }

        AppendHyperbolaSegment(hyperbola, start, middle, startPoint, middlePoint, policy, samples, depth + 1);
        AppendHyperbolaSegment(hyperbola, middle, end, middlePoint, endPoint, policy, samples, depth + 1);
    }

    private static double ComputePolylineChordDeviation(Func<double, Point3D> evaluate, IReadOnlyList<(double Parameter, Point3D Point)> samples)
    {
        var worst = 0d;
        for (var index = 0; index + 1 < samples.Count; index++)
        {
            var middle = (samples[index].Parameter + samples[index + 1].Parameter) * 0.5d;
            var midpoint = evaluate(middle);
            var chordMidpoint = new Point3D(
                (samples[index].Point.X + samples[index + 1].Point.X) * 0.5d,
                (samples[index].Point.Y + samples[index + 1].Point.Y) * 0.5d,
                (samples[index].Point.Z + samples[index + 1].Point.Z) * 0.5d);
            worst = double.Max(worst, (midpoint - chordMidpoint).Length);
        }
        return worst;
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
        // The rectangular annulus is retained as the economical all-quad path.
        // Other bounded planar holes (notably the HexBolt underside's hex/circle
        // contact) use the existing deterministic planar triangulator while still
        // consuming the exact shared boundary vertices.
        if (inners.Length != 1 || !IsRectangle(outer.LocalCoordinates) || inners[0].VertexIds.Count != outer.VertexIds.Count)
        {
            if (inners.Length == 1)
            {
                var stitched = TryBuildConvexAnnularStitch(outer, inners[0], sameSense);
                if (stitched is not null)
                    return new SurfacePatch(faceId, new SurfaceMeshSupport(SurfaceMeshSupportKind.Plane, Plane: plane), loops, stitched, sameSense, TrimLoopData: annotatedLoops);
            }
            var vertexByPosition = vertices.GroupBy(vertex => vertex.Position).ToDictionary(group => group.Key, group => group.First().Id);
            var outerPoints = outer.VertexIds.Select(id => vertices.First(vertex => vertex.Id == id).Position).ToArray();
            var innerPoints = inners.Select(inner => (IReadOnlyList<Point3D>)inner.VertexIds.Select(id => vertices.First(vertex => vertex.Id == id).Position).ToArray()).ToArray();
            if (PlanarPolygonTriangulator.TryTriangulateWithHoles(outerPoints, innerPoints, plane.Normal.ToVector(), out var points, out var indices, out _)
                && points.All(point => vertexByPosition.ContainsKey(point)))
            {
                var triangulatedCells = new List<SurfaceMeshCell>(indices.Count / 3);
                for (var index = 0; index < indices.Count; index += 3)
                    triangulatedCells.Add(new TriangleCell(Orient([vertexByPosition[points[indices[index]]], vertexByPosition[points[indices[index + 1]]], vertexByPosition[points[indices[index + 2]]]], sameSense)));
                return new SurfacePatch(faceId, new SurfaceMeshSupport(SurfaceMeshSupportKind.Plane, Plane: plane), loops, triangulatedCells, sameSense, TrimLoopData: annotatedLoops);
            }
            return null;
        }
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

    private static SurfacePatch? TryBuildCylinderPatch(BrepBody body, FaceId faceId, bool sameSense, CylinderSurface cylinder, IReadOnlyDictionary<EdgeId, SharedEdgeSamplePlan> plans, List<SurfaceMeshVertex> vertices, ref int nextVertexId)
    {
        var bounded = TryBuildFourSidedCylinderPatch(body, faceId, sameSense, cylinder, plans, vertices, ref nextVertexId);
        if (bounded is not null) return bounded;
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

    private static SurfacePatch? TryBuildConePatch(BrepBody body, FaceId faceId, bool sameSense, ConeSurface cone, IReadOnlyDictionary<EdgeId, SharedEdgeSamplePlan> plans, List<SurfaceMeshVertex> vertices, ref int nextVertexId)
    {
        var bounded = TryBuildFourSidedConePatch(body, faceId, sameSense, cone, plans, vertices, ref nextVertexId);
        if (bounded is not null) return bounded;
        var loops = body.GetLoopIds(faceId);
        var circles = loops
            .SelectMany(loop => body.GetCoedgeIds(loop).Select(body.Topology.GetCoedge))
            .Select(coedge => plans[coedge.EdgeId])
            .Where(plan => plan.CurveKind == CurveGeometryKind.Circle3 && plan.IsClosed)
            .DistinctBy(plan => plan.EdgeId)
            .ToArray();
        if (circles.Length != 2 || circles[0].Samples.Count != circles[1].Samples.Count)
        {
            return null;
        }

        var rings = circles.Select(plan => plan.Samples.Take(plan.Samples.Count - 1).ToArray()).ToArray();
        if (rings[0].Length < 3)
        {
            return null;
        }

        var v0 = cone.AxialParameterFromPoint(rings[0][0].Position);
        var v1 = cone.AxialParameterFromPoint(rings[1][0].Position);
        if (!double.IsFinite(v0) || !double.IsFinite(v1) || double.Abs(v1 - v0) <= Epsilon)
        {
            return null;
        }

        var lower = v0 <= v1 ? rings[0] : rings[1];
        var upper = v0 <= v1 ? rings[1] : rings[0];
        var cells = new List<SurfaceMeshCell>(lower.Length);
        for (var i = 0; i < lower.Length; i++)
        {
            var next = (i + 1) % lower.Length;
            cells.Add(new QuadCell(Orient([lower[i].Id, lower[next].Id, upper[next].Id, upper[i].Id], sameSense)));
        }

        return new SurfacePatch(
            faceId,
            new SurfaceMeshSupport(SurfaceMeshSupportKind.Cone, Cone: cone),
            loops,
            cells,
            sameSense,
            HasPeriodicUSeam: true);
    }

    private static SurfacePatch? TryBuildFourSidedCylinderPatch(BrepBody body, FaceId faceId, bool sameSense, CylinderSurface cylinder, IReadOnlyDictionary<EdgeId, SharedEdgeSamplePlan> plans, List<SurfaceMeshVertex> vertices, ref int nextVertexId)
    {
        if (!TryGetFourSidedBoundary(body, faceId, plans, out var boundary)) return null;
        if (boundary.TopKind != CurveGeometryKind.Circle3 || boundary.BottomKind != CurveGeometryKind.Circle3 ||
            boundary.LeftKind != CurveGeometryKind.Line3 || boundary.RightKind != CurveGeometryKind.Line3) return null;
        return BuildFourSidedPatch(faceId, sameSense, new SurfaceMeshSupport(SurfaceMeshSupportKind.Cylinder, Cylinder: cylinder), body.GetLoopIds(faceId), boundary,
            point => TryProjectPointToCylinderUv(cylinder, point),
            (u, v) => cylinder.Evaluate(u, v), vertices, ref nextVertexId,
            hasPeriodicUSeam: false);
    }

    private static SurfacePatch? TryBuildFourSidedConePatch(BrepBody body, FaceId faceId, bool sameSense, ConeSurface cone, IReadOnlyDictionary<EdgeId, SharedEdgeSamplePlan> plans, List<SurfaceMeshVertex> vertices, ref int nextVertexId)
    {
        if (!TryGetFourSidedBoundary(body, faceId, plans, out var boundary)) return null;
        if (boundary.TopKind != CurveGeometryKind.Circle3 ||
            boundary.BottomKind is not (CurveGeometryKind.Circle3 or CurveGeometryKind.Hyperbola3) ||
            boundary.LeftKind != CurveGeometryKind.Line3 || boundary.RightKind != CurveGeometryKind.Line3) return null;
        return BuildFourSidedPatch(faceId, sameSense, new SurfaceMeshSupport(SurfaceMeshSupportKind.Cone, Cone: cone), body.GetLoopIds(faceId), boundary,
            point => TryProjectPointToConeUv(cone, point),
            (u, v) => cone.Evaluate(u, v), vertices, ref nextVertexId,
            hasPeriodicUSeam: false);
    }

    private sealed record FourSidedBoundary(
        SharedEdgeSamplePlan TopPlan, SharedEdgeSamplePlan RightPlan, SharedEdgeSamplePlan BottomPlan, SharedEdgeSamplePlan LeftPlan,
        IReadOnlyList<SurfaceMeshVertex> Top, IReadOnlyList<SurfaceMeshVertex> Right, IReadOnlyList<SurfaceMeshVertex> Bottom, IReadOnlyList<SurfaceMeshVertex> Left)
    {
        public CurveGeometryKind TopKind => TopPlan.CurveKind;
        public CurveGeometryKind BottomKind => BottomPlan.CurveKind;
        public CurveGeometryKind LeftKind => LeftPlan.CurveKind;
        public CurveGeometryKind RightKind => RightPlan.CurveKind;
    }

    private static bool TryGetFourSidedBoundary(BrepBody body, FaceId faceId, IReadOnlyDictionary<EdgeId, SharedEdgeSamplePlan> plans, out FourSidedBoundary boundary)
    {
        boundary = default!;
        var loops = body.GetLoopIds(faceId);
        if (loops.Count != 1) return false;
        var coedges = body.GetCoedgeIds(loops[0]).Select(body.Topology.GetCoedge).ToArray();
        if (coedges.Length != 4 || coedges.Any(coedge => plans[coedge.EdgeId].IsClosed)) return false;
        IReadOnlyList<SurfaceMeshVertex> Samples(Coedge coedge)
        {
            var samples = plans[coedge.EdgeId].Samples.ToArray();
            if (coedge.IsReversed) Array.Reverse(samples);
            return samples;
        }
        var top = Samples(coedges[0]); var right = Samples(coedges[1]);
        var bottom = Samples(coedges[2]).Reverse().ToArray(); var left = Samples(coedges[3]).Reverse().ToArray();
        if (top.Count < 2 || top.Count != bottom.Length || right.Count != left.Length ||
            top[0].Id != left[0].Id || top[^1].Id != right[0].Id || bottom[0].Id != left[^1].Id || bottom[^1].Id != right[^1].Id) return false;
        boundary = new(plans[coedges[0].EdgeId], plans[coedges[1].EdgeId], plans[coedges[2].EdgeId], plans[coedges[3].EdgeId], top, right, bottom, left);
        return true;
    }

    private static SurfacePatch? BuildFourSidedPatch(
        FaceId faceId, bool sameSense, SurfaceMeshSupport support, IReadOnlyList<LoopId> loops, FourSidedBoundary boundary,
        Func<Point3D, (double U, double V)?> project, Func<double, double, Point3D> evaluate, List<SurfaceMeshVertex> vertices, ref int nextVertexId, bool hasPeriodicUSeam)
    {
        var columns = boundary.Top.Count;
        var rows = boundary.Left.Count;
        var topUv = boundary.Top.Select(sample => project(sample.Position)).ToArray();
        var bottomUv = boundary.Bottom.Select(sample => project(sample.Position)).ToArray();
        if (topUv.Any(uv => uv is null) || bottomUv.Any(uv => uv is null)) return null;
        var grid = new SurfaceMeshVertex[rows, columns];
        for (var column = 0; column < columns; column++) { grid[0, column] = boundary.Top[column]; grid[rows - 1, column] = boundary.Bottom[column]; }
        for (var row = 0; row < rows; row++) { grid[row, 0] = boundary.Left[row]; grid[row, columns - 1] = boundary.Right[row]; }
        for (var row = 1; row < rows - 1; row++)
        {
            var t = row / (double)(rows - 1);
            for (var column = 1; column < columns - 1; column++)
            {
                var top = topUv[column]!.Value; var bottom = bottomUv[column]!.Value;
                var u = InterpolateAngle(top.U, bottom.U, t);
                var v = top.V + ((bottom.V - top.V) * t);
                var point = evaluate(u, v);
                grid[row, column] = new SurfaceMeshVertex(nextVertexId++, point, u, v);
                vertices.Add(grid[row, column]);
            }
        }
        var cells = new List<SurfaceMeshCell>((rows - 1) * (columns - 1));
        for (var row = 0; row < rows - 1; row++)
            for (var column = 0; column < columns - 1; column++)
                cells.Add(new QuadCell(Orient([grid[row, column].Id, grid[row, column + 1].Id, grid[row + 1, column + 1].Id, grid[row + 1, column].Id], sameSense)));
        return new SurfacePatch(faceId, support, loops, cells, sameSense, HasPeriodicUSeam: hasPeriodicUSeam);
    }

    private static bool TryBuildSphereCharts(
        BrepBody body,
        FaceId faceId,
        bool sameSense,
        SphereSurface sphere,
        List<SurfaceMeshVertex> vertices,
        ref int nextVertexId,
        out IReadOnlyList<SurfacePatch> patches)
    {
        // A six-chart cube sphere has no pole fan.  Integer cube-grid coordinates
        // are the seam identity: neighboring charts literally reuse vertex IDs.
        if (body.GetLoopIds(faceId).Count != 0)
        {
            patches = [];
            return false;
        }

        const int subdivisions = 6;
        var seamVertices = new Dictionary<(int X, int Y, int Z), int>();
        var byId = vertices.ToDictionary(vertex => vertex.Id);
        var chartDefinitions = new[]
        {
            new CubeChart("+X", (1, 0, 0), (0, 1, 0), (0, 0, 1)),
            new CubeChart("-X", (-1, 0, 0), (0, 0, 1), (0, 1, 0)),
            new CubeChart("+Y", (0, 1, 0), (0, 0, 1), (1, 0, 0)),
            new CubeChart("-Y", (0, -1, 0), (1, 0, 0), (0, 0, 1)),
            new CubeChart("+Z", (0, 0, 1), (1, 0, 0), (0, 1, 0)),
            new CubeChart("-Z", (0, 0, -1), (0, 1, 0), (1, 0, 0)),
        };
        var result = new List<SurfacePatch>(chartDefinitions.Length);
        foreach (var chart in chartDefinitions)
        {
            var grid = new int[subdivisions + 1, subdivisions + 1];
            for (var row = 0; row <= subdivisions; row++)
            {
                for (var column = 0; column <= subdivisions; column++)
                {
                    var numeratorX = (chart.Normal.X * subdivisions) + (chart.U.X * ((2 * column) - subdivisions)) + (chart.V.X * ((2 * row) - subdivisions));
                    var numeratorY = (chart.Normal.Y * subdivisions) + (chart.U.Y * ((2 * column) - subdivisions)) + (chart.V.Y * ((2 * row) - subdivisions));
                    var numeratorZ = (chart.Normal.Z * subdivisions) + (chart.U.Z * ((2 * column) - subdivisions)) + (chart.V.Z * ((2 * row) - subdivisions));
                    var key = (numeratorX, numeratorY, numeratorZ);
                    if (!seamVertices.TryGetValue(key, out var id))
                    {
                        var cube = new Vector3D(numeratorX, numeratorY, numeratorZ);
                        var unit = cube / cube.Length;
                        var position = sphere.Center
                            + (sphere.XAxis.ToVector() * (sphere.Radius * unit.X))
                            + (sphere.YAxis.ToVector() * (sphere.Radius * unit.Y))
                            + (sphere.Axis.ToVector() * (sphere.Radius * unit.Z));
                        id = nextVertexId++;
                        var vertex = new SurfaceMeshVertex(id, position, column / (double)subdivisions, row / (double)subdivisions);
                        vertices.Add(vertex);
                        byId.Add(id, vertex);
                        seamVertices.Add(key, id);
                    }
                    grid[row, column] = id;
                }
            }

            var cells = new List<SurfaceMeshCell>(subdivisions * subdivisions);
            for (var row = 0; row < subdivisions; row++)
            {
                for (var column = 0; column < subdivisions; column++)
                {
                    cells.Add(new QuadCell(Orient([grid[row, column], grid[row, column + 1], grid[row + 1, column + 1], grid[row + 1, column]], sameSense)));
                }
            }
            result.Add(new SurfacePatch(faceId, new SurfaceMeshSupport(SurfaceMeshSupportKind.Sphere, Sphere: sphere), [], cells, sameSense, ChartId: chart.Name));
        }

        patches = result;
        return true;
    }

    private static SurfacePatch? TryBuildTorusPatch(
        BrepBody body,
        FaceId faceId,
        bool sameSense,
        TorusSurface torus,
        IReadOnlyDictionary<EdgeId, SharedEdgeSamplePlan> plans,
        SurfaceMeshPolicy policy,
        List<SurfaceMeshVertex> vertices,
        ref int nextVertexId)
    {
        // Root/concave fillet: four directed circle uses delimit a genuine
        // bounded torus domain (major-angle across each split face, minor-angle
        // across the fillet).  Preserve that coedge ordering; reversing both
        // contact rings would visually turn the trim inside-out.
        if (TryGetFourSidedBoundary(body, faceId, plans, out var bounded)
            && bounded.TopKind == CurveGeometryKind.Circle3 && bounded.RightKind == CurveGeometryKind.Circle3
            && bounded.BottomKind == CurveGeometryKind.Circle3 && bounded.LeftKind == CurveGeometryKind.Circle3)
        {
            return BuildFourSidedPatch(faceId, sameSense, new SurfaceMeshSupport(SurfaceMeshSupportKind.Torus, Torus: torus), body.GetLoopIds(faceId), bounded,
                point => TryProjectPointToTorusUv(torus, point), (u, v) => torus.Evaluate(u, v), vertices, ref nextVertexId,
                hasPeriodicUSeam: false);
        }
        if (body.GetLoopIds(faceId).Count != 1)
        {
            return null;
        }

        var seamPlans = body.GetCoedgeIds(body.GetLoopIds(faceId)[0])
            .Select(body.Topology.GetCoedge)
            .Select(coedge => plans[coedge.EdgeId])
            .Where(plan => plan.CurveKind == CurveGeometryKind.Circle3 && plan.IsClosed)
            .DistinctBy(plan => plan.EdgeId)
            .ToArray();
        if (seamPlans.Length != 2)
        {
            return null;
        }

        var parameters = seamPlans
            .Select(plan => (Plan: plan, Samples: plan.Samples.Take(plan.Samples.Count - 1).Select(sample => (Sample: sample, Uv: TryProjectPointToTorusUv(torus, sample.Position))).ToArray()))
            .ToArray();
        if (parameters.Any(entry => entry.Samples.Any(sample => sample.Uv is null)))
        {
            return null;
        }

        var major = parameters.OrderByDescending(entry => CircularSpan(entry.Samples.Select(sample => sample.Uv!.Value.U))).First();
        var minor = parameters.Single(entry => entry.Plan.EdgeId != major.Plan.EdgeId);
        var uSegments = System.Math.Max(major.Samples.Length, ResolveCircleSegments(torus.MajorRadius + torus.MinorRadius, 2d * double.Pi, policy));
        var vSegments = System.Math.Max(minor.Samples.Length, ResolveCircleSegments(torus.MinorRadius, 2d * double.Pi, policy));
        var seamIds = new Dictionary<(int U, int V), SurfaceMeshVertex>();
        AddTorusSeamSamples(major.Samples, uSegments, vSegments, seamIds);
        AddTorusSeamSamples(minor.Samples, uSegments, vSegments, seamIds);

        var grid = new int[uSegments, vSegments];
        for (var uIndex = 0; uIndex < uSegments; uIndex++)
        {
            for (var vIndex = 0; vIndex < vSegments; vIndex++)
            {
                if (seamIds.TryGetValue((uIndex, vIndex), out var sampled))
                {
                    grid[uIndex, vIndex] = sampled.Id;
                    continue;
                }

                var u = (2d * double.Pi * uIndex) / uSegments;
                var v = (2d * double.Pi * vIndex) / vSegments;
                var vertex = new SurfaceMeshVertex(nextVertexId++, torus.Evaluate(u, v), u, v);
                vertices.Add(vertex);
                grid[uIndex, vIndex] = vertex.Id;
            }
        }

        var cells = new List<SurfaceMeshCell>(uSegments * vSegments);
        for (var uIndex = 0; uIndex < uSegments; uIndex++)
        {
            for (var vIndex = 0; vIndex < vSegments; vIndex++)
            {
                var nextU = (uIndex + 1) % uSegments;
                var nextV = (vIndex + 1) % vSegments;
                cells.Add(new QuadCell(Orient([grid[uIndex, vIndex], grid[nextU, vIndex], grid[nextU, nextV], grid[uIndex, nextV]], sameSense)));
            }
        }
        return new SurfacePatch(faceId, new SurfaceMeshSupport(SurfaceMeshSupportKind.Torus, Torus: torus), body.GetLoopIds(faceId), cells, sameSense, HasPeriodicUSeam: true, HasPeriodicVSeam: true);
    }

    private static void AddTorusSeamSamples(
        IReadOnlyList<(SurfaceMeshVertex Sample, (double U, double V)? Uv)> samples,
        int uSegments,
        int vSegments,
        IDictionary<(int U, int V), SurfaceMeshVertex> seamIds)
    {
        foreach (var (sample, uv) in samples)
        {
            if (uv is not { } value)
            {
                continue;
            }
            var u = ((int)System.Math.Round(value.U / (2d * double.Pi) * uSegments)) % uSegments;
            var v = ((int)System.Math.Round(value.V / (2d * double.Pi) * vSegments)) % vSegments;
            seamIds.TryAdd((U: u, V: v), sample);
        }
    }

    private static (double U, double V)? TryProjectPointToTorusUv(TorusSurface torus, Point3D point)
    {
        var axis = torus.Axis.ToVector();
        var offset = point - torus.Center;
        var axial = offset.Dot(axis);
        var planar = offset - (axis * axial);
        var planarLength = planar.Length;
        if (planarLength <= Epsilon)
        {
            return null;
        }
        var u = NormalizeAngle(double.Atan2(planar.Dot(torus.YAxis.ToVector()), planar.Dot(torus.XAxis.ToVector())));
        var v = NormalizeAngle(double.Atan2(axial, planarLength - torus.MajorRadius));
        return (u, v);
    }

    private static (double U, double V)? TryProjectPointToCylinderUv(CylinderSurface cylinder, Point3D point)
    {
        var offset = point - cylinder.Origin;
        var v = offset.Dot(cylinder.Axis.ToVector());
        var radial = offset - (cylinder.Axis.ToVector() * v);
        if (radial.Length <= Epsilon) return null;
        return (NormalizeAngle(double.Atan2(radial.Dot(cylinder.YAxis.ToVector()), radial.Dot(cylinder.XAxis.ToVector()))), v);
    }

    private static (double U, double V)? TryProjectPointToConeUv(ConeSurface cone, Point3D point)
    {
        var offset = point - cone.Apex;
        var v = offset.Dot(cone.Axis.ToVector());
        var radial = offset - (cone.Axis.ToVector() * v);
        if (radial.Length <= Epsilon) return null;
        var x = cone.ReferenceAxis.ToVector() - (cone.Axis.ToVector() * cone.ReferenceAxis.ToVector().Dot(cone.Axis.ToVector()));
        if (!x.TryNormalize(out x)) return null;
        var y = cone.Axis.ToVector().Cross(x);
        return (NormalizeAngle(double.Atan2(radial.Dot(y), radial.Dot(x))), v);
    }

    private static double CircularSpan(IEnumerable<double> angles)
    {
        var values = angles.OrderBy(value => value).ToArray();
        if (values.Length < 2)
        {
            return 0d;
        }
        return values.Zip(values.Skip(1), (a, b) => b - a).DefaultIfEmpty().Max();
    }

    private static double NormalizeAngle(double angle)
    {
        var normalized = angle % (2d * double.Pi);
        return normalized < 0d ? normalized + (2d * double.Pi) : normalized;
    }

    private static double InterpolateAngle(double start, double end, double t)
    {
        var delta = end - start;
        if (delta > double.Pi) delta -= 2d * double.Pi;
        else if (delta < -double.Pi) delta += 2d * double.Pi;
        return NormalizeAngle(start + (delta * t));
    }

    private readonly record struct CubeChart(string Name, (int X, int Y, int Z) Normal, (int X, int Y, int Z) U, (int X, int Y, int Z) V);

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

    private static IReadOnlyList<SurfaceMeshCell>? TryBuildConvexAnnularStitch(SurfaceMeshTrimLoop outer, SurfaceMeshTrimLoop inner, bool sameSense)
    {
        // Localized planar boundary adaptation for an unequal-count convex ring.
        // It advances each authoritative boundary by normalized arc progress and
        // emits a quad whenever both advance together; the unavoidable count
        // mismatch is restricted to boundary triangles.
        if (!IsConvex(outer.LocalCoordinates) || !IsConvex(inner.LocalCoordinates)) return null;
        var outerIds = EnsureCounterClockwise(outer.VertexIds, outer.LocalCoordinates).ToArray();
        var innerIds = EnsureCounterClockwise(inner.VertexIds, inner.LocalCoordinates).ToArray();
        if (outerIds.Length < 3 || innerIds.Length < 3) return null;
        var cells = new List<SurfaceMeshCell>(outerIds.Length + innerIds.Length);
        var outerIndex = 0; var innerIndex = 0;
        while (outerIndex < outerIds.Length || innerIndex < innerIds.Length)
        {
            var nextOuter = (outerIndex + 1d) / outerIds.Length;
            var nextInner = (innerIndex + 1d) / innerIds.Length;
            var outerCurrent = outerIds[outerIndex % outerIds.Length];
            var innerCurrent = innerIds[innerIndex % innerIds.Length];
            if (double.Abs(nextOuter - nextInner) <= Epsilon)
            {
                cells.Add(new QuadCell(Orient([outerCurrent, outerIds[(outerIndex + 1) % outerIds.Length], innerIds[(innerIndex + 1) % innerIds.Length], innerCurrent], sameSense)));
                outerIndex++; innerIndex++;
            }
            else if (nextOuter < nextInner)
            {
                cells.Add(new TriangleCell(Orient([outerCurrent, outerIds[(outerIndex + 1) % outerIds.Length], innerCurrent], sameSense)));
                outerIndex++;
            }
            else
            {
                cells.Add(new TriangleCell(Orient([outerCurrent, innerIds[(innerIndex + 1) % innerIds.Length], innerCurrent], sameSense)));
                innerIndex++;
            }
        }
        return cells;
    }

    private static bool IsConvex(IReadOnlyList<(double U, double V)> points)
    {
        if (points.Count < 3) return false;
        var sign = 0;
        for (var index = 0; index < points.Count; index++)
        {
            var a = points[index]; var b = points[(index + 1) % points.Count]; var c = points[(index + 2) % points.Count];
            var cross = ((b.U - a.U) * (c.V - b.V)) - ((b.V - a.V) * (c.U - b.U));
            if (double.Abs(cross) <= Epsilon) continue;
            var current = cross > 0d ? 1 : -1;
            if (sign != 0 && sign != current) return false;
            sign = current;
        }
        return sign != 0;
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
            SurfaceMeshSupportKind.Cone when patch.Support.Cone is { } cone => p =>
            {
                var offset = p - cone.Apex;
                var axis = cone.Axis.ToVector();
                var radial = offset - (axis * offset.Dot(axis));
                var reference = cone.ReferenceAxis.ToVector();
                var xAxis = reference - (axis * reference.Dot(axis));
                var yAxis = axis.Cross(xAxis);
                var angle = radial.Length <= Epsilon ? 0d : double.Atan2(radial.Dot(yAxis), radial.Dot(xAxis));
                var exact = cone.Normal(angle).ToVector();
                return patch.SameSense ? exact : -exact;
            },
            SurfaceMeshSupportKind.Sphere when patch.Support.Sphere is { } sphere => p =>
            {
                var exact = Direction3D.Create(p - sphere.Center).ToVector();
                return patch.SameSense ? exact : -exact;
            },
            SurfaceMeshSupportKind.Torus when patch.Support.Torus is { } torus => p =>
            {
                var uv = TryProjectPointToTorusUv(torus, p) ?? throw new InvalidOperationException("Torus mesh vertex is not on its exact support.");
                var exact = torus.Normal(uv.U, uv.V).ToVector();
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
            if (boundary.Samples.Count < 2 || boundary.Uses.Count == 0) { failure = $"Shared edge {boundary.EdgeId.Value} has no complete sample/use contract (samples={boundary.Samples.Count}, uses={boundary.Uses.Count})."; return false; }
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
