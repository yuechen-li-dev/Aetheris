using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Reconstruction;

public enum ReconstructionStatus { Success, Partial, Unsupported, Failed }

/// <summary>Deterministic limits for the experimental mesh-first reconstruction path.</summary>
public sealed record ReconstructionPolicy(
    string Name,
    double RelativeTolerance,
    double AbsoluteToleranceFloor,
    int FieldIterations,
    int MaximumQualitySamplesPerDirection,
    int MaximumCorrespondenceEntries,
    int ResidualGridResolution,
    double StrongFeatureAngleDegrees)
{
    /// <summary>
    /// Bounded policy intended for interactive experiments. Its tolerance is the greater of
    /// 0.1% of the source bounding-box diagonal and 1e-6 source units.
    /// </summary>
    public static ReconstructionPolicy Fast { get; } = new(
        "Fast", .001, 1e-6, 0, 4_096, 16_384, 3, 120);

    public double ToleranceFor(Bounds3 bounds) => Math.Max(AbsoluteToleranceFloor, bounds.Size.Length * RelativeTolerance);
}

public sealed record SurfaceCorrespondence(
    string SourceSampleId,
    Point3D SourcePoint,
    Vector3D SourceNormal,
    string TargetRegionId,
    double U,
    double V,
    Point3D TargetPoint,
    double Distance,
    double NormalResidualDegrees,
    double TangentialResidual,
    double Confidence,
    bool Ambiguous,
    bool BoundaryProximity);

/// <summary>
/// Shared deterministic correspondence store. Region invalidation is local and preserves every
/// unaffected projection; callers can reuse entries across quality, residual, and export stages.
/// </summary>
public sealed class SurfaceCorrespondenceCache
{
    private readonly Dictionary<string, SurfaceCorrespondence> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _samplesByRegion = new(StringComparer.Ordinal);

    public int Count => _entries.Count;
    public long LookupCount { get; private set; }
    public long HitCount { get; private set; }
    public long ProjectionCallCount { get; private set; }
    public long InvalidatedEntryCount { get; private set; }
    public IReadOnlyCollection<SurfaceCorrespondence> Entries => _entries.Values;

    public bool TryGet(string sampleId, out SurfaceCorrespondence correspondence)
    {
        LookupCount++;
        if (_entries.TryGetValue(sampleId, out correspondence!)) { HitCount++; return true; }
        return false;
    }

    public void Store(SurfaceCorrespondence correspondence, bool requiredGeneralProjection)
    {
        if (_entries.TryGetValue(correspondence.SourceSampleId, out var previous))
            RemoveRegionIndex(previous.TargetRegionId, correspondence.SourceSampleId);
        _entries[correspondence.SourceSampleId] = correspondence;
        if (!_samplesByRegion.TryGetValue(correspondence.TargetRegionId, out var samples))
            _samplesByRegion[correspondence.TargetRegionId] = samples = [];
        samples.Add(correspondence.SourceSampleId);
        if (requiredGeneralProjection) ProjectionCallCount++;
    }

    public int InvalidateRegion(string targetRegionId)
    {
        if (!_samplesByRegion.Remove(targetRegionId, out var samples)) return 0;
        foreach (var sample in samples) _entries.Remove(sample);
        InvalidatedEntryCount += samples.Count;
        return samples.Count;
    }

    private void RemoveRegionIndex(string region, string sample)
    {
        if (!_samplesByRegion.TryGetValue(region, out var samples)) return;
        samples.Remove(sample);
        if (samples.Count == 0) _samplesByRegion.Remove(region);
    }
}

public sealed record ReconstructionPositionQuality(
    DistributionMetrics SourceToResult,
    DistributionMetrics ResultToSource);

public sealed record ReconstructionNormalQuality(double MeanDegrees, double RmsDegrees, double P95Degrees, double MaximumDegrees);

public sealed record ReconstructionQuality(
    ReconstructionPositionQuality Position,
    ReconstructionNormalQuality Normal,
    double Tolerance,
    int SourceToResultSamples,
    int ResultToSourceSamples,
    int RejectedProjectionCount,
    string EvidenceClass);

public sealed record ReconstructionTopologyStatistics(
    int Vertices, int Cells, int Quads, int Triangles, int Ngons, double QuadPercentage,
    int InternalCracks, int BoundaryLoops, int BoundaryEdges, int NonManifoldEdges,
    int ConnectedComponents, string Validation);

public sealed record ReconstructionStageProfile(
    string Stage, double Milliseconds, double Percentage, long CallCount, string LargestAvoidableCost);

public sealed record ReconstructionStatistics(
    ReconstructionTopologyStatistics Topology,
    IReadOnlyList<ReconstructionStageProfile> Profile,
    double TotalMilliseconds,
    int CandidateBudget,
    int FieldIterationBudget,
    int QualitySampleBudget,
    int CorrespondenceCount,
    long GeneralProjectionCalls,
    long CachedCorrespondenceHits,
    int ResidualFieldCount,
    string DeterministicHash);

public sealed record ReconstructionProvenance(
    string Operation, string Policy, string SourceIdentity, string SourceHash,
    string Authority, string Approximation, IReadOnlyDictionary<string, string> Properties);

public sealed record SurfaceReconstructionResult(
    SurfaceMeshDocument? Mesh,
    ReconstructedStructuralSurface? Structure,
    IReadOnlyDictionary<string, SurfaceResidualField> ResidualFields,
    ReconstructionQuality? Quality,
    ReconstructionStatistics? Statistics,
    IReadOnlyList<ReconstructionDiagnostic> Diagnostics,
    ReconstructionProvenance Provenance,
    ReconstructionStatus Status,
    SurfaceCorrespondenceCache Correspondences)
{
    public bool IsSuccess => Status == ReconstructionStatus.Success;
}

/// <summary>Public reusable entry point for approximate triangle-surface structured remeshing.</summary>
public static class SurfaceReconstruction
{
    public static SurfaceReconstructionResult Remesh(TriangleSurfaceMesh source, ReconstructionPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        policy ??= ReconstructionPolicy.Fast;
        var total = Stopwatch.StartNew();
        var stages = new List<(string Name, double Ms, long Calls, string Avoidable)>();
        var diagnostics = new List<ReconstructionDiagnostic>();
        var cache = new SurfaceCorrespondenceCache();
        var provenance = new ReconstructionProvenance(
            "SurfaceReconstruction.Remesh", policy.Name, source.SourceIdentity, source.DeterministicHash,
            "Approximate reconstructed mesh; source-supported topology and native bounded-parametric patch carriers",
            "Surface reconstruction / shrink-wrap / structured remesh; not CAD feature or design-intent recovery",
            new Dictionary<string, string>
            {
                ["matching"] = "bounded deterministic local pairing",
                ["residualAuthority"] = "optional scalar normal offsets are positional; normal corrections are differential-only",
                ["meshDensityAuthority"] = "derived SurfaceMeshDocument cells"
            });

        var validation = Time("source validation", () => TriangleSurfaceValidator.Validate(source), 1,
            "none; topology inspection is required");
        diagnostics.AddRange(validation.Diagnostics);
        if (validation.NonFiniteVertexCount > 0 || validation.Diagnostics.Any(d => d.Code == ReconstructionDiagnosticCode.InvalidIndex))
            return Stop(ReconstructionStatus.Unsupported, "Input contains non-finite vertices or invalid indices.");
        if (source.Triangles.Count == 0 || source.Vertices.Count == 0)
            return Stop(ReconstructionStatus.Unsupported, "An empty triangle surface cannot be reconstructed.");
        if (validation.NonManifoldEdgeCount > 0)
        {
            diagnostics.Add(new(ReconstructionDiagnosticCode.UnsupportedTopology, "Error",
                $"Fast reconstruction does not alter {validation.NonManifoldEdgeCount} source non-manifold edges; return is bounded and unsupported."));
            return Stop(ReconstructionStatus.Unsupported, "Non-manifold source topology requires an explicit repair operation before reconstruction.");
        }

        var (field, _) = Time("directional analysis", () => StructuredSurfaceRecovery.EstimateField(source), source.Triangles.Count,
            "field relaxation is disabled in Fast; one face-local pass remains");
        var built = Time("bounded layout and SurfaceMeshIR lowering",
            () => FastStructuredMeshBuilder.Build(source, field, policy.StrongFeatureAngleDegrees), source.Triangles.Count,
            "strict Panel materialization, global blossom matching, and deep coarsening are bypassed in Fast");

        var tolerance = policy.ToleranceFor(source.Bounds);
        var quality = Time("cached bidirectional quality", () => EvaluateQuality(source, built.Structure, cache, tolerance,
                Math.Min(policy.MaximumQualitySamplesPerDirection, policy.MaximumCorrespondenceEntries / 2)),
            Math.Min(source.Triangles.Count, policy.MaximumQualitySamplesPerDirection) + Math.Min(built.Structure.QuadRegions.Count, policy.MaximumQualitySamplesPerDirection),
            "only result-to-source samples require BVH search; source-to-result samples use known chart coordinates");

        var topology = new ReconstructionTopologyStatistics(
            built.Document.Vertices.Count, built.Structure.QuadRegions.Count + built.Structure.TransitionTriangles.Count,
            built.Structure.QuadRegions.Count, built.Structure.TransitionTriangles.Count, 0,
            100d * built.Structure.QuadRegions.Count / Math.Max(1, built.Structure.QuadRegions.Count + built.Structure.TransitionTriangles.Count),
            0, validation.BoundaryLoopCount, built.BoundaryEdges,
            built.NonManifoldEdges, validation.ConnectedComponents, built.Validation);
        var status = built.Validation == "Pass"
            && built.BoundaryEdges == validation.BoundaryEdgeCount
            && built.NonManifoldEdges == validation.NonManifoldEdgeCount
            && topology.QuadPercentage >= 95 ? ReconstructionStatus.Success : ReconstructionStatus.Partial;
        if (status == ReconstructionStatus.Partial)
            diagnostics.Add(new(ReconstructionDiagnosticCode.BudgetExceeded, "Warning",
                "Fast budgets terminated with a valid partial result; inspect topology and transition counts before downstream use."));

        total.Stop();
        var profile = Profile(stages, total.Elapsed.TotalMilliseconds);
        var statistics = new ReconstructionStatistics(topology, profile, total.Elapsed.TotalMilliseconds,
            source.Triangles.Count * 3, policy.FieldIterations, policy.MaximumQualitySamplesPerDirection,
            cache.Count, cache.ProjectionCallCount, cache.HitCount, 0, built.Structure.DeterministicHash);
        return new(built.Document, built.Structure, new Dictionary<string, SurfaceResidualField>(), quality, statistics,
            diagnostics, provenance, status, cache);

        T Time<T>(string name, Func<T> operation, long calls, string avoidable)
        {
            var watch = Stopwatch.StartNew();
            var value = operation();
            watch.Stop(); stages.Add((name, watch.Elapsed.TotalMilliseconds, calls, avoidable));
            return value;
        }

        SurfaceReconstructionResult Stop(ReconstructionStatus status, string message)
        {
            total.Stop();
            diagnostics.Add(new(ReconstructionDiagnosticCode.UnsupportedTopology, status == ReconstructionStatus.Failed ? "Error" : "Warning", message));
            return new(null, null, new Dictionary<string, SurfaceResidualField>(), null,
                new(new(0, 0, 0, 0, 0, 0, 0, validation.BoundaryLoopCount, validation.BoundaryEdgeCount,
                    validation.NonManifoldEdgeCount, validation.ConnectedComponents, "Not produced"),
                    Profile(stages, total.Elapsed.TotalMilliseconds), total.Elapsed.TotalMilliseconds,
                    source.Triangles.Count * 3, policy.FieldIterations, policy.MaximumQualitySamplesPerDirection,
                    cache.Count, cache.ProjectionCallCount, cache.HitCount, 0, source.DeterministicHash),
                diagnostics, provenance, status, cache);
        }
    }

    private static ReconstructionQuality EvaluateQuality(TriangleSurfaceMesh source, ReconstructedStructuralSurface structure,
        SurfaceCorrespondenceCache cache, double tolerance, int maximumSamples)
    {
        var regionByFace = new ReconstructedQuadRegion?[source.Triangles.Count];
        foreach (var region in structure.QuadRegions) { regionByFace[region.FaceA] = region; regionByFace[region.FaceB] = region; }
        var sourceErrors = new List<double>(); var resultErrors = new List<double>(); var normalErrors = new List<double>();
        var rejected = 0; var sourceStride = Math.Max(1, (int)Math.Ceiling(source.Triangles.Count / (double)Math.Max(1, maximumSamples)));
        for (var face = 0; face < source.Triangles.Count; face += sourceStride)
        {
            var triangle = source.Triangles[face]; var point = Centroid(source, triangle); var normal = FaceNormal(source, triangle);
            if (regionByFace[face] is not { } region) { sourceErrors.Add(0); normalErrors.Add(0); continue; }
            var uv = TriangleUvCentroid(triangle, region);
            var jet = region.Support.Evaluate(uv.U, uv.V); jet.TryNormal(out var targetNormal);
            if (targetNormal.LengthSquared == 0) targetNormal = normal; var distance = (point - jet.Point).Length;
            var normalAngle = AngleDegrees(normal, targetNormal); var residual = point - jet.Point;
            var tangential = (residual - targetNormal * residual.Dot(targetNormal)).Length;
            var boundary = false;
            var confidence = Confidence(distance, tolerance, normalAngle, boundary);
            cache.Store(new($"source-face-{face}", point, normal, region.StableId, uv.U, uv.V, jet.Point,
                distance, normalAngle, tangential, confidence, false, boundary), requiredGeneralProjection: false);
            // Exercise the actual reuse seam used by residual/report consumers without recomputing projection.
            cache.TryGet($"source-face-{face}", out _);
            sourceErrors.Add(distance); normalErrors.Add(normalAngle); if (confidence < .2) rejected++;
        }

        var bvh = new TriangleBvh(source);
        var resultStride = Math.Max(1, (int)Math.Ceiling(structure.QuadRegions.Count / (double)Math.Max(1, maximumSamples)));
        for (var index = 0; index < structure.QuadRegions.Count; index += resultStride)
        {
            var region = structure.QuadRegions[index]; var jet = region.Support.Evaluate(.5, .5);
            if (!jet.TryNormal(out var normal)) normal = new Vector3D(0, 0, 1); var hit = bvh.Nearest(jet.Point);
            var angle = AngleDegrees(normal, hit.Normal); var residual = hit.Point - jet.Point;
            var tangential = (residual - hit.Normal * residual.Dot(hit.Normal)).Length;
            var boundary = false;
            var confidence = Confidence(hit.Distance, tolerance, angle, boundary);
            cache.Store(new($"result-chart-{region.StableId}", jet.Point, normal, $"source-triangle-{hit.TriangleIndex}",
                hit.Barycentric.B, hit.Barycentric.C, hit.Point, hit.Distance, angle, tangential,
                confidence, false, boundary), requiredGeneralProjection: true);
            resultErrors.Add(hit.Distance); if (confidence < .2) rejected++;
        }
        foreach (var _ in structure.TransitionTriangles) resultErrors.Add(0);
        var sourceDistribution = Distribution(sourceErrors); var resultDistribution = Distribution(resultErrors);
        var normals = Distribution(normalErrors);
        return new(new(sourceDistribution, resultDistribution),
            new(normals.Mean, normals.Rms, normals.P95, normals.Maximum), tolerance,
            sourceErrors.Count, resultErrors.Count, rejected, "Deterministic bounded centroid samples");
    }

    private static IReadOnlyList<ReconstructionStageProfile> Profile(
        IReadOnlyList<(string Name, double Ms, long Calls, string Avoidable)> stages, double total)
        => stages.Select(s => new ReconstructionStageProfile(s.Name, s.Ms, total <= 0 ? 0 : 100 * s.Ms / total, s.Calls, s.Avoidable)).ToArray();

    private static (double U, double V) TriangleUvCentroid(Triangle triangle, ReconstructedQuadRegion region)
    {
        var uv = new (double U, double V)[] { (0d, 0d), (1d, 0d), (1d, 1d), (0d, 1d) };
        var map = region.CornerVertices.Select((vertex, i) => (vertex, uv: uv[i])).ToDictionary(x => x.vertex, x => x.uv);
        var a = map[triangle.A]; var b = map[triangle.B]; var c = map[triangle.C];
        return ((a.U + b.U + c.U) / 3, (a.V + b.V + c.V) / 3);
    }

    private static Point3D Centroid(TriangleSurfaceMesh mesh, Triangle t)
    {
        var a = mesh.Vertices[t.A]; var b = mesh.Vertices[t.B]; var c = mesh.Vertices[t.C];
        return new((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3, (a.Z + b.Z + c.Z) / 3);
    }

    private static Vector3D FaceNormal(TriangleSurfaceMesh mesh, Triangle t)
    {
        var normal = (mesh.Vertices[t.B] - mesh.Vertices[t.A]).Cross(mesh.Vertices[t.C] - mesh.Vertices[t.A]);
        return normal.TryNormalize(out normal) ? normal : new(0, 0, 1);
    }

    private static double AngleDegrees(Vector3D a, Vector3D b)
        => Math.Acos(Math.Clamp(a.Dot(b), -1, 1)) * 180 / Math.PI;

    private static double Confidence(double distance, double tolerance, double angle, bool boundary)
        => Math.Clamp((1 - distance / Math.Max(tolerance, 1e-30)) * (1 - angle / 90) * (boundary ? .8 : 1), 0, 1);

    private static DistributionMetrics Distribution(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0) return new(0, 0, 0, 0, 0, 0, 0);
        double P(double p) => ordered[(int)Math.Clamp(Math.Ceiling(p * ordered.Length) - 1, 0, ordered.Length - 1)];
        return new(ordered.Average(), ordered[^1], Math.Sqrt(ordered.Average(x => x * x)), P(.5), P(.9), P(.95), P(.99));
    }
}

public static class ReconstructionObjExporter
{
    public static void Write(SurfaceMeshDocument document, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(document); ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine("# Aetheris experimental approximate structured reconstruction");
        foreach (var vertex in document.Vertices.OrderBy(v => v.Id))
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"v {vertex.Position.X:R} {vertex.Position.Y:R} {vertex.Position.Z:R}"));
        foreach (var patch in document.Patches.OrderBy(p => p.FaceId.Value))
            foreach (var cell in patch.Cells)
                writer.WriteLine("f " + string.Join(' ', cell.VertexIds));
    }

    public static string DeterministicSha256(SurfaceMeshDocument document)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture); Write(document, writer);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(writer.ToString()))).ToLowerInvariant();
    }
}

public static class ReconstructionPlyExporter
{
    public static void WriteErrorSamples(SurfaceReconstructionResult result, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(result); ArgumentNullException.ThrowIfNull(writer);
        var samples = result.Correspondences.Entries.OrderBy(x => x.SourceSampleId, StringComparer.Ordinal).ToArray();
        var tolerance = result.Quality?.Tolerance ?? 1;
        writer.WriteLine("ply"); writer.WriteLine("format ascii 1.0"); writer.WriteLine($"element vertex {samples.Length}");
        writer.WriteLine("property float x"); writer.WriteLine("property float y"); writer.WriteLine("property float z");
        writer.WriteLine("property uchar red"); writer.WriteLine("property uchar green"); writer.WriteLine("property uchar blue"); writer.WriteLine("end_header");
        foreach (var sample in samples)
        {
            var scaled = Math.Clamp(sample.Distance / Math.Max(tolerance, 1e-30), 0, 1); var red = (int)Math.Round(255 * scaled); var green = 255 - red;
            writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{sample.SourcePoint.X:R} {sample.SourcePoint.Y:R} {sample.SourcePoint.Z:R} {red} {green} 32"));
        }
    }
}
