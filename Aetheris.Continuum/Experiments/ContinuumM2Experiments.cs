using System.Diagnostics;
using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Analytic;
using Aetheris.Continuum.Sampling;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Experiments;

public sealed record ContinuumM2Timings(
    double ClassificationMilliseconds,
    double BrepBoundaryLookupMilliseconds,
    double LocalFrameMilliseconds,
    double MapConstructionMilliseconds,
    double RuntimeCertificateMilliseconds,
    double ExactQueryCacheMilliseconds,
    double GeometrySamplingMilliseconds,
    double IntegrationMilliseconds,
    double OracleValidationMilliseconds,
    double RuntimeExcludingOracleMilliseconds,
    double ExperimentalIncludingOracleMilliseconds);

public sealed record ContinuumM2StrategyResult(
    string Strategy,
    int TotalCells,
    int CutCells,
    IReadOnlyDictionary<string, int> MapResolutionDistribution,
    double EstimatedVolume,
    double RelativeVolumeError,
    double? EstimatedBoundaryArea,
    double? RelativeBoundaryAreaError,
    double? MaximumPositionError,
    double? RmsPositionError,
    double? MeanPositionError,
    double? MaximumNormalErrorDegrees,
    double? RmsNormalErrorDegrees,
    long RuntimeCertificateQueries,
    long OracleValidationQueries,
    long MapRawRequests,
    long MapUniqueQueries,
    long MapReusedQueries,
    double MapCacheHitRate,
    long GeometryRawRequests,
    long GeometryUniqueQueries,
    long GeometryReusedQueries,
    double GeometryReusePercentage,
    int RefinedCells,
    int CertificateOracleAgreements,
    int FalseAccepts,
    int FalseRefines,
    double MinimumActiveFraction,
    int ActiveBelowOnePercent,
    int ActiveBelowFivePercent,
    int ActiveBelowTenPercent,
    Vector3D BoundaryCentroid,
    ContinuumM2Timings Timings);

public sealed record ContinuumM2CellDiagnostic(
    CellIndex CellIndex,
    BoundaryReference BoundaryReference,
    string SupportKind,
    BoundaryLocalFrame LocalFrame,
    BoundaryMapDomain Domain,
    int ResolutionU,
    int ResolutionV,
    EngineeringBoundaryMapCertificate Certificate,
    double OracleMaximumPositionError,
    double OracleRmsPositionError,
    double OracleMaximumNormalErrorDegrees,
    double Occupancy,
    double ActiveFraction,
    double DomainAspectRatio,
    double CurvatureAnisotropy,
    double OffsetRange,
    double NormalVariationDegrees,
    int MapSamples);

public sealed record ContinuumM2OrientationResult(
    string Orientation,
    double RotationXDegrees,
    double RotationYDegrees,
    double RotationZDegrees,
    ContinuumM2StrategyResult Result,
    IReadOnlyList<ContinuumM2CellDiagnostic> Diagnostics);

public sealed record ContinuumM2CurvatureResult(double Radius, ContinuumM2StrategyResult Result);

public sealed record ContinuumM2Benchmark(
    string Schema,
    IReadOnlyList<ContinuumM2StrategyResult> GeometricRefinementLadder,
    IReadOnlyList<ContinuumM2OrientationResult> OrientationMatrix,
    IReadOnlyList<ContinuumM2CurvatureResult> CurvatureMatrix,
    ContinuumM2StrategyResult FineGridReference,
    bool FixedLatticeBeatsFineGridVolumeError,
    bool CirBrepConsistencyPassed,
    string ExactBrepFace,
    string SupportKind);

public static class ContinuumM2Experiments
{
    private static readonly BoundingBox3D LatticeBounds = new(new Point3D(-1.4d, -1.4d, -1.4d), new Point3D(1.4d, 1.4d, 1.4d));
    private static readonly Vector3D Translation = new(0.047d, -0.031d, 0.023d);
    private static readonly BoundaryOffsetMapErrorPolicy Policy = new(0.00005d, 0.15d, 24);

    public static ContinuumM2Benchmark Run()
    {
        var baselineRegion = Region("baseline", 1d, 0d, 0d, 0d);
        AssertCirBrepConsistency(baselineRegion);
        var lattice = new LatticeSpec(LatticeBounds, 16, 16, 16);
        var topology = Classify(baselineRegion, lattice);
        var ladder = new List<ContinuumM2StrategyResult>
        {
            RunPointSampling("MSAA 4x4x4", baselineRegion, lattice, topology, 4),
            RunMaps("OffsetMap 4x4", baselineRegion, lattice, topology, (_, _) => (4, 4), selective: false, runOracle: true).Result,
            RunMaps("OffsetMap 8x8", baselineRegion, lattice, topology, (_, _) => (8, 8), selective: false, runOracle: true).Result,
            RunMaps("OffsetMap 16x16", baselineRegion, lattice, topology, (_, _) => (16, 16), selective: false, runOracle: true).Result,
            RunMaps("OffsetMap 24x24", baselineRegion, lattice, topology, (_, _) => (24, 24), selective: false, runOracle: true).Result,
            RunMaps("curvature-aware anisotropic map", baselineRegion, lattice, topology,
                (support, bounds) => support.ChooseResolution(bounds, Policy), selective: false, runOracle: true).Result,
            RunMaps("anisotropic map + selective MSAA", baselineRegion, lattice, topology,
                (support, bounds) => support.ChooseResolution(bounds, Policy), selective: true, runOracle: true).Result,
        };

        var orientations = new[]
        {
            ("baseline", 0d, 0d, 0d),
            ("rotate-x-31", 31d, 0d, 0d),
            ("compound-23-37-11", 23d, 37d, 11d),
        }.Select(item =>
        {
            var region = Region(item.Item1, 1d, item.Item2, item.Item3, item.Item4);
            AssertCirBrepConsistency(region);
            var classified = Classify(region, lattice);
            var run = RunMaps("curvature-aware anisotropic map", region, lattice, classified,
                (support, bounds) => support.ChooseResolution(bounds, Policy), selective: false, runOracle: true);
            return new ContinuumM2OrientationResult(item.Item1, item.Item2, item.Item3, item.Item4, run.Result, run.Diagnostics);
        }).ToArray();

        var curvature = new[] { 0.8d, 1d, 1.2d }.Select(radius =>
        {
            var region = Region($"radius-{radius:R}", radius, 23d, 37d, 11d);
            var classified = Classify(region, lattice);
            var run = RunMaps("curvature-aware anisotropic map", region, lattice, classified,
                (support, bounds) => support.ChooseResolution(bounds, Policy), selective: false, runOracle: true);
            return new ContinuumM2CurvatureResult(radius, run.Result);
        }).ToArray();

        var fineLattice = new LatticeSpec(LatticeBounds, 32, 32, 32);
        var fineTopology = Classify(baselineRegion, fineLattice);
        var fine = RunPointSampling("32x32x32 lattice + MSAA 4x4x4", baselineRegion, fineLattice, fineTopology, 4);
        var bestFixed = ladder.Where(row => row.EstimatedBoundaryArea.HasValue).MinBy(row => row.RelativeVolumeError)!;
        return new ContinuumM2Benchmark("aetheris-continuum-m2-v1", ladder, orientations, curvature, fine,
            bestFixed.RelativeVolumeError < fine.RelativeVolumeError, true,
            baselineRegion.BoundaryReference.ExactBrepFaceId!, baselineRegion.ExactQuery.SupportKind);
    }

    private static BrepSphereContinuumRegion Region(string id, double radius, double rx, double ry, double rz)
    {
        var rotation = Transform3D.CreateRotationX(ToRadians(rx)) * Transform3D.CreateRotationY(ToRadians(ry)) * Transform3D.CreateRotationZ(ToRadians(rz));
        return new BrepSphereContinuumRegion(new RegionId($"m2-{id}"), radius, rotation * Transform3D.CreateTranslation(Translation));
    }

    private static MapRun RunMaps(string name, BrepSphereContinuumRegion region, LatticeSpec lattice, Topology topology,
        Func<BrepSphereBoundarySupport, BoundingBox3D, (int U, int V)> resolution, bool selective, bool runOracle)
    {
        var total = Stopwatch.StartNew();
        var lookupTime = TimeSpan.Zero;
        var buildTime = TimeSpan.Zero;
        var oracleTime = TimeSpan.Zero;
        var cache = new BoundaryEvaluationCache();
        var buildCosts = new BoundaryMapBuildCosts();
        var maps = new Dictionary<CellIndex, SampledBoundaryOffsetMap>();
        foreach (var cell in topology.Cut)
        {
            var timer = Stopwatch.StartNew();
            var support = (BrepSphereBoundarySupport)region.BoundarySupports(cell.Bounds).Single();
            timer.Stop(); lookupTime += timer.Elapsed;
            var (nu, nv) = resolution(support, cell.Bounds);
            timer.Restart();
            var runtime = support.Build(cell.Index, cell.Bounds, nu, nv, Policy, cache, runOracle: false, buildCosts);
            timer.Stop(); buildTime += timer.Elapsed;
            if (runOracle)
            {
                timer.Restart();
                runtime = support.Validate(runtime, Policy);
                timer.Stop();
                oracleTime += timer.Elapsed;
            }
            maps[cell.Index] = runtime;
        }

        var samplingTimer = Stopwatch.StartNew();
        var geometryCache = new GeometryQueryCache();
        long geometryRaw = 0, geometryUnique = 0, geometryReused = 0;
        var refined = new HashSet<CellIndex>();
        if (selective)
        {
            foreach (var cell in topology.Cut)
            {
                var basePass = HierarchicalGeometrySampler.SampleNestedBase2(region, cell.Bounds, geometryCache);
                geometryRaw += basePass.RawRequestedSamples; geometryUnique += basePass.UniqueExactQueries; geometryReused += basePass.ReusedSamples;
                var mapEstimate = BoundaryOffsetMap3DIntegrator.Integrate(maps[cell.Index], cell.Bounds, 6, 2);
                var certificate = maps[cell.Index].Approximation.RuntimeCertificate!;
                if (certificate.Decision != BoundaryMapCertificateDecision.Acceptable
                    || double.Abs(basePass.Plan.CoverageEstimate - mapEstimate.OccupancyFraction) >= 0.08d)
                {
                    refined.Add(cell.Index);
                    var pass = HierarchicalGeometrySampler.RefineToRegular4(region, cell.Bounds, geometryCache);
                    geometryRaw += pass.RawRequestedSamples; geometryUnique += pass.UniqueExactQueries; geometryReused += pass.ReusedSamples;
                }
            }
        }
        samplingTimer.Stop();

        var integrationTimer = Stopwatch.StartNew();
        var estimates = topology.Cut.ToDictionary(cell => cell.Index, cell =>
            BoundaryOffsetMap3DIntegrator.Integrate(maps[cell.Index], cell.Bounds, refined.Contains(cell.Index) ? 96 : 64, 6));
        var cellVolume = CellVolume(lattice);
        var volume = topology.InsideCount * cellVolume + estimates.Values.Sum(value => value.OccupancyFraction * cellVolume);
        var area = estimates.Values.Sum(value => value.BoundaryArea);
        var moment = estimates.Values.Aggregate(Vector3D.Zero, (sum, value) => sum + value.BoundaryFirstMoment);
        var centroid = area > 0d ? moment / area : Vector3D.Zero;
        integrationTimer.Stop(); total.Stop();

        var allMaps = maps.Values.ToArray();
        var oracleCount = allMaps.Sum(map => map.Approximation.IndependentValidationPointCount);
        var accepted = allMaps.Count(map => map.Approximation.IsAccepted);
        var agreements = allMaps.Count(map => (map.Approximation.RuntimeCertificate!.Decision == BoundaryMapCertificateDecision.Acceptable) == map.Approximation.IsAccepted);
        var falseAccepts = allMaps.Count(map => map.Approximation.RuntimeCertificate!.Decision == BoundaryMapCertificateDecision.Acceptable && !map.Approximation.IsAccepted);
        var falseRefines = allMaps.Count(map => map.Approximation.RuntimeCertificate!.Decision != BoundaryMapCertificateDecision.Acceptable && map.Approximation.IsAccepted);
        var active = estimates.Values.Select(value => value.OccupancyFraction).Order().ToArray();
        var diagnostics = topology.Cut.Select(cell => Diagnostic(cell.Index, region, maps[cell.Index], estimates[cell.Index])).ToArray();
        var runtimeMs = topology.ClassificationMilliseconds + lookupTime.TotalMilliseconds + buildTime.TotalMilliseconds
            + samplingTimer.Elapsed.TotalMilliseconds + integrationTimer.Elapsed.TotalMilliseconds;
        var result = new ContinuumM2StrategyResult(name, lattice.TotalCellCount, topology.Cut.Count,
            allMaps.GroupBy(map => $"{map.Approximation.ResolutionU}x{map.Approximation.ResolutionV}").OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()),
            volume, double.Abs(volume - region.ExactVolume) / region.ExactVolume,
            area, double.Abs(area - region.ExactArea) / region.ExactArea,
            allMaps.Max(map => map.Approximation.MaximumPositionError), WeightedRms(allMaps, false), WeightedMean(allMaps),
            allMaps.Max(map => map.Approximation.MaximumNormalAngleDegrees), WeightedRms(allMaps, true),
            allMaps.Sum(map => map.Approximation.RuntimeCertificate!.ExactQueryCount), oracleCount,
            cache.Requests, cache.Misses, cache.Hits, cache.HitRate,
            geometryRaw, geometryUnique, geometryReused, geometryRaw == 0 ? 0d : 100d * geometryReused / geometryRaw,
            refined.Count, agreements, falseAccepts, falseRefines, active.Min(), active.Count(x => x < 0.01d), active.Count(x => x < 0.05d), active.Count(x => x < 0.10d), centroid,
            new(topology.ClassificationMilliseconds, lookupTime.TotalMilliseconds, buildCosts.LocalFrameMilliseconds,
                buildCosts.MapConstructionMilliseconds, buildCosts.RuntimeCertificateMilliseconds, buildCosts.ExactQueryCacheMilliseconds,
                samplingTimer.Elapsed.TotalMilliseconds, integrationTimer.Elapsed.TotalMilliseconds, oracleTime.TotalMilliseconds,
                runtimeMs, runtimeMs + oracleTime.TotalMilliseconds));
        _ = accepted;
        return new MapRun(result, diagnostics);
    }

    private static ContinuumM2StrategyResult RunPointSampling(string name, BrepSphereContinuumRegion region, LatticeSpec lattice, Topology topology, int rate)
    {
        var timer = Stopwatch.StartNew();
        var cache = new GeometryQueryCache();
        var coverages = new Dictionary<CellIndex, double>();
        foreach (var cell in topology.Cut) coverages[cell.Index] = HierarchicalGeometrySampler.SampleRegular(region, cell.Bounds, rate, cache).Plan.CoverageEstimate;
        var volume = topology.InsideCount * CellVolume(lattice) + topology.Cut.Sum(cell => coverages[cell.Index] * CellVolume(lattice));
        timer.Stop();
        var active = coverages.Values.Order().ToArray();
        return new(name, lattice.TotalCellCount, topology.Cut.Count, new Dictionary<string, int>(), volume,
            double.Abs(volume - region.ExactVolume) / region.ExactVolume, null, null, null, null, null, null, null,
            0, 0, 0, 0, 0, 0d, cache.Requests, cache.Misses, cache.Hits, cache.Requests == 0 ? 0d : 100d * cache.Hits / cache.Requests,
            0, 0, 0, 0, active.Min(), active.Count(x => x < .01d), active.Count(x => x < .05d), active.Count(x => x < .10d), Vector3D.Zero,
            new(topology.ClassificationMilliseconds, 0, 0, 0, 0, 0, timer.Elapsed.TotalMilliseconds, 0, 0,
                topology.ClassificationMilliseconds + timer.Elapsed.TotalMilliseconds, topology.ClassificationMilliseconds + timer.Elapsed.TotalMilliseconds));
    }

    private static Topology Classify(IContinuumRegion region, LatticeSpec lattice)
    {
        var timer = Stopwatch.StartNew();
        var cells = lattice.Indices().Select(index => (Index: index, Bounds: lattice.CellBounds(index), Classification: ContinuumGridClassifier.ClassifyCell(region, lattice.CellBounds(index)))).ToArray();
        timer.Stop();
        return new(cells.Count(cell => cell.Classification == CellClassification.Inside),
            cells.Where(cell => cell.Classification == CellClassification.Cut).Select(cell => (cell.Index, cell.Bounds)).ToArray(), timer.Elapsed.TotalMilliseconds);
    }

    private static ContinuumM2CellDiagnostic Diagnostic(CellIndex index, BrepSphereContinuumRegion region, SampledBoundaryOffsetMap map, BoundaryMapCellEstimate estimate)
    {
        var offsets = map.Samples.Select(sample => sample.Offset).ToArray();
        var normals = map.Samples.Select(sample => sample.Normal!.Value).ToArray();
        var normalVariation = normals.Select(normal => double.Acos(double.Clamp(normal.Dot(map.LocalFrame.Normal), -1d, 1d)) * 180d / double.Pi).Max();
        var u = map.Domain.MaximumU - map.Domain.MinimumU;
        var v = map.Domain.MaximumV - map.Domain.MinimumV;
        return new(index, region.BoundaryReference, region.ExactQuery.SupportKind, map.LocalFrame, map.Domain,
            map.Approximation.ResolutionU, map.Approximation.ResolutionV, map.Approximation.RuntimeCertificate!,
            map.Approximation.MaximumPositionError, map.Approximation.RmsPositionError, map.Approximation.MaximumNormalAngleDegrees,
            estimate.OccupancyFraction, estimate.OccupancyFraction, double.Max(u, v) / double.Min(u, v), 1d,
            offsets.Max() - offsets.Min(), normalVariation, map.Samples.Count);
    }

    private static void AssertCirBrepConsistency(BrepSphereContinuumRegion region)
    {
        var directions = new[] { new Vector3D(1,0,0), new Vector3D(-1,0,0), new Vector3D(0,1,0), new Vector3D(0,0,1), new Vector3D(1,2,3) };
        foreach (var raw in directions)
        {
            raw.TryNormalize(out var direction);
            var boundary = region.ExactQuery.Center + (direction * region.ExactQuery.Radius);
            var materialNormal = -region.ExactQuery.SupportNormalAt(boundary);
            if (region.Classify(boundary + (materialNormal * 1e-6d)) == ContinuumPointClassification.Outside
                || region.Classify(boundary - (materialNormal * 1e-6d)) != ContinuumPointClassification.Outside)
                throw new InvalidOperationException($"CIR/BRep material-side disagreement on face {region.FaceId.Value}.");
        }
    }

    private static double WeightedRms(IReadOnlyList<SampledBoundaryOffsetMap> maps, bool normal)
    {
        var count = maps.Sum(map => map.Approximation.IndependentValidationPointCount);
        return double.Sqrt(maps.Sum(map => double.Pow(normal ? map.Approximation.RmsNormalAngleDegrees : map.Approximation.RmsPositionError, 2d)
            * map.Approximation.IndependentValidationPointCount) / count);
    }
    private static double WeightedMean(IReadOnlyList<SampledBoundaryOffsetMap> maps)
    {
        var count = maps.Sum(map => map.Approximation.IndependentValidationPointCount);
        return maps.Sum(map => map.Approximation.MeanPositionError * map.Approximation.IndependentValidationPointCount) / count;
    }
    private static double CellVolume(LatticeSpec lattice) => lattice.CellSize.X * lattice.CellSize.Y * lattice.CellSize.Z;
    private static double ToRadians(double degrees) => degrees * double.Pi / 180d;
    private sealed record Topology(int InsideCount, IReadOnlyList<(CellIndex Index, BoundingBox3D Bounds)> Cut, double ClassificationMilliseconds);
    private sealed record MapRun(ContinuumM2StrategyResult Result, IReadOnlyList<ContinuumM2CellDiagnostic> Diagnostics);
}
