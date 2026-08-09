using System.Diagnostics;
using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Analytic;
using Aetheris.Continuum.Sampling;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Experiments;

public sealed record ContinuumM1Timings(
    double ClassificationMilliseconds,
    double BoundaryMapConstructionMilliseconds,
    double GeometrySamplingMilliseconds,
    double AggregationMilliseconds,
    double TotalMilliseconds);

public sealed record ContinuumM1StrategyResult(
    string Strategy,
    int TotalCells,
    int CutCells,
    long BoundaryMapSamples,
    long IndependentValidationSamples,
    long GeometrySamples,
    long RawRequestedSamples,
    long UniqueExactGeometryQueries,
    long ReusedSamples,
    double ReuseRatio,
    double BoundaryMapCacheHitRate,
    double GeometryCacheHitRate,
    int SelectivelyRefinedCells,
    double EstimatedVolume,
    double ExactVolume,
    double RelativeVolumeError,
    double EstimatedBoundaryArea,
    double ExactBoundaryArea,
    double AbsoluteBoundaryAreaError,
    double RelativeBoundaryAreaError,
    double? MaximumPositionError,
    double? RmsPositionError,
    double? MeanPositionError,
    double? MaximumNormalAngleDegrees,
    double? RmsNormalAngleDegrees,
    Vector3D BoundaryCentroid,
    Vector3D AggregateNormal,
    ContinuumM1Timings Timings);

public sealed record ContinuumM1CellDiagnostic(
    CellIndex CellIndex,
    string Strategy,
    double OccupancyEstimate,
    double ReferenceOccupancy,
    double OccupancyError,
    double? OffsetMapMaximumPositionError,
    double? MaximumNormalAngleDegrees,
    int GeometrySampleCount,
    int BoundaryMapSampleCount,
    long ReusedSampleCount,
    double ActiveVolumeFraction);

public sealed record ContinuumM1FixtureResult(
    string Fixture,
    LatticeSpec Lattice,
    int InsideCells,
    int OutsideCells,
    int CutCells,
    IReadOnlyList<ContinuumM1StrategyResult> Strategies,
    IReadOnlyList<ContinuumM1CellDiagnostic> PerCellDiagnostics);

public sealed record ContinuumM1Benchmark(
    string Schema,
    ContinuumM1FixtureResult CylindricalHole,
    ContinuumM1FixtureResult ObliquePlane,
    IReadOnlyList<ContinuumExperimentResult> M0CylindricalHoleBaselines);

public static class ContinuumM1Experiments
{
    private sealed record ClassifiedTopology(
        IReadOnlyList<(CellIndex Index, BoundingBox3D Bounds, CellClassification Classification)> Cells,
        int Inside,
        int Outside,
        IReadOnlyList<(CellIndex Index, BoundingBox3D Bounds)> Cut,
        double Milliseconds);

    private sealed record MapRun(
        ContinuumM1StrategyResult Result,
        IReadOnlyList<ContinuumM1CellDiagnostic> Diagnostics);

    public static ContinuumM1Benchmark Run()
    {
        return new ContinuumM1Benchmark(
            "aetheris-continuum-m1-v1",
            RunCylinder(),
            RunPlane(),
            ContinuumM0Experiments.CylindricalHoleConvergence());
    }

    public static ContinuumM1FixtureResult RunCylinder()
    {
        var bounds = new BoundingBox3D(new Point3D(-2d, -2d, -0.5d), new Point3D(2d, 2d, 0.5d));
        var region = new BlockWithCylindricalHoleRegion(new RegionId("m1-cylindrical-hole"), bounds, 1d);
        var lattice = new LatticeSpec(bounds, 16, 16, 4);
        var topology = Classify(region, lattice);
        var exactOccupancy = new Func<BoundingBox3D, double>(cell =>
            AnalyticContinuumReferences.CylinderMaterialOccupancy(cell, region.HoleCenter, region.HoleRadius));
        var rows = new List<ContinuumM1StrategyResult>();
        var diagnostics = new List<ContinuumM1CellDiagnostic>();

        var regular2 = RunRegular("regular-2x2x2", region, lattice, topology, 2, region.ExactVolume, region.ExactCylindricalBoundaryArea, exactOccupancy);
        rows.Add(regular2.Result); diagnostics.AddRange(regular2.Diagnostics);
        var regular4 = RunRegular("m0-baseline-4x4x4", region, lattice, topology, 4, region.ExactVolume, region.ExactCylindricalBoundaryArea, exactOccupancy);
        rows.Add(regular4.Result); diagnostics.AddRange(regular4.Diagnostics);
        rows.Add(regular4.Result with { Strategy = "regular-4x4x4" });
        diagnostics.AddRange(regular4.Diagnostics.Select(item => item with { Strategy = "regular-4x4x4" }));

        var map2 = RunMap("offset-map-2x2", region, lattice, topology, 2, region.ExactVolume, region.ExactCylindricalBoundaryArea, exactOccupancy, selective: false);
        rows.Add(map2.Result); diagnostics.AddRange(map2.Diagnostics);
        var map4 = RunMap("offset-map-4x4", region, lattice, topology, 4, region.ExactVolume, region.ExactCylindricalBoundaryArea, exactOccupancy, selective: false);
        rows.Add(map4.Result); diagnostics.AddRange(map4.Diagnostics);
        var selective = RunMap("offset-map-selective-msaa", region, lattice, topology, 4, region.ExactVolume, region.ExactCylindricalBoundaryArea, exactOccupancy, selective: true);
        rows.Add(selective.Result); diagnostics.AddRange(selective.Diagnostics);
        return Fixture("cylindrical-hole", lattice, topology, rows, diagnostics);
    }

    public static ContinuumM1FixtureResult RunPlane()
    {
        var bounds = new BoundingBox3D(new Point3D(-1d, -1d, -1d), new Point3D(1d, 1d, 1d));
        var region = new ObliqueHalfSpaceRegion(new RegionId("m1-oblique-plane"), bounds, new Vector3D(1d, 1d, 0d), 0d);
        var lattice = new LatticeSpec(bounds, 8, 8, 8);
        var topology = Classify(region, lattice);
        var exactOccupancy = new Func<BoundingBox3D, double>(_ => 0.5d);
        var rows = new List<ContinuumM1StrategyResult>();
        var diagnostics = new List<ContinuumM1CellDiagnostic>();
        var regular2 = RunRegular("regular-2x2x2", region, lattice, topology, 2, 4d, region.ExactPlaneBoundaryArea, exactOccupancy);
        rows.Add(regular2.Result); diagnostics.AddRange(regular2.Diagnostics);
        var regular4 = RunRegular("regular-4x4x4", region, lattice, topology, 4, 4d, region.ExactPlaneBoundaryArea, exactOccupancy);
        rows.Add(regular4.Result); diagnostics.AddRange(regular4.Diagnostics);
        var map2 = RunMap("offset-map-2x2", region, lattice, topology, 2, 4d, region.ExactPlaneBoundaryArea, exactOccupancy, selective: false);
        rows.Add(map2.Result); diagnostics.AddRange(map2.Diagnostics);
        var selective = RunMap("offset-map-selective-msaa", region, lattice, topology, 4, 4d, region.ExactPlaneBoundaryArea, exactOccupancy, selective: true);
        rows.Add(selective.Result); diagnostics.AddRange(selective.Diagnostics);
        return Fixture("oblique-plane-control", lattice, topology, rows, diagnostics);
    }

    private static MapRun RunRegular(
        string name,
        IContinuumRegion region,
        LatticeSpec lattice,
        ClassifiedTopology topology,
        int rate,
        double exactVolume,
        double exactArea,
        Func<BoundingBox3D, double> referenceOccupancy)
    {
        var total = Stopwatch.StartNew();
        var sampling = Stopwatch.StartNew();
        var cache = new GeometryQueryCache();
        var plans = new Dictionary<CellIndex, GeometrySamplePlan>();
        long raw = 0, unique = 0, reused = 0;
        foreach (var cell in topology.Cut)
        {
            var pass = HierarchicalGeometrySampler.SampleRegular(region, cell.Bounds, rate, cache);
            plans.Add(cell.Index, pass.Plan);
            raw += pass.RawRequestedSamples;
            unique += pass.UniqueExactQueries;
            reused += pass.ReusedSamples;
        }
        sampling.Stop();

        var aggregation = Stopwatch.StartNew();
        var cellVolume = CellVolume(lattice);
        var volume = topology.Inside * cellVolume + topology.Cut.Sum(cell => plans[cell.Index].CoverageEstimate * cellVolume);
        var area = EstimateExtrudedArea(lattice, topology, plans, rate);
        var diagnostics = topology.Cut.Select(cell => Diagnostic(
            cell.Index, name, plans[cell.Index].CoverageEstimate, referenceOccupancy(cell.Bounds), null, null,
            plans[cell.Index].GeometrySampleCount, 0, 0)).ToArray();
        aggregation.Stop(); total.Stop();
        return new MapRun(
            Result(name, lattice, topology, 0, 0, raw, raw, unique, reused, 0d, cache.HitRate, 0, volume, exactVolume, area, exactArea,
                null, Vector3D.Zero, Vector3D.Zero,
                new ContinuumM1Timings(topology.Milliseconds, 0d, sampling.Elapsed.TotalMilliseconds, aggregation.Elapsed.TotalMilliseconds, total.Elapsed.TotalMilliseconds + topology.Milliseconds)),
            diagnostics);
    }

    private static MapRun RunMap(
        string name,
        IContinuumRegion region,
        LatticeSpec lattice,
        ClassifiedTopology topology,
        int resolution,
        double exactVolume,
        double exactArea,
        Func<BoundingBox3D, double> referenceOccupancy,
        bool selective)
    {
        if (region is not IBoundaryOffsetMapCapability capability)
        {
            throw new InvalidOperationException("Fixture does not expose analytic BoundaryOffsetMap support.");
        }

        var total = Stopwatch.StartNew();
        var policy = new BoundaryOffsetMapErrorPolicy(MaximumPositionError: 0.0015d, MaximumNormalAngleDegrees: 0.25d, MaximumResolution: 8);
        var mapTimer = Stopwatch.StartNew();
        var boundaryCache = new BoundaryEvaluationCache();
        long validationQueries = 0;
        var maps = new Dictionary<CellIndex, IBoundaryOffsetMap>();
        foreach (var cell in topology.Cut)
        {
            var support = capability.BoundarySupports(cell.Bounds).Single();
            var map = support.CreateOffsetMap(cell.Index, cell.Bounds, resolution, policy, boundaryCache);
            maps.Add(cell.Index, map);
            validationQueries += map.Approximation.IndependentValidationPointCount;
        }
        mapTimer.Stop();

        var samplingTimer = Stopwatch.StartNew();
        var geometryCache = new GeometryQueryCache();
        var basePlans = new Dictionary<CellIndex, GeometrySamplingPass>();
        var refinedPlans = new Dictionary<CellIndex, GeometrySamplingPass>();
        var refined = new HashSet<CellIndex>();
        if (selective)
        {
            foreach (var cell in topology.Cut)
            {
                var basePass = HierarchicalGeometrySampler.SampleNestedBase2(region, cell.Bounds, geometryCache);
                basePlans.Add(cell.Index, basePass);
                var mapEstimate = BoundaryOffsetMapIntegrator.IntegrateColumn(maps[cell.Index], cell.Bounds);
                var mixed = basePass.Plan.CoverageEstimate is > 0d and < 1d;
                var disagreement = double.Abs(basePass.Plan.CoverageEstimate - mapEstimate.OccupancyFraction);
                if (!maps[cell.Index].Approximation.IsAccepted || (mixed && disagreement >= 0.02d))
                {
                    refined.Add(cell.Index);
                    refinedPlans.Add(cell.Index, HierarchicalGeometrySampler.RefineToRegular4(region, cell.Bounds, geometryCache));
                }
            }

            // Refinement decisions are now fixed. Only those cells receive a denser local boundary graph.
            samplingTimer.Stop();
            mapTimer.Start();
            foreach (var cell in topology.Cut.Where(cell => refined.Contains(cell.Index)))
            {
                var support = capability.BoundarySupports(cell.Bounds).Single();
                var map = support.CreateOffsetMap(cell.Index, cell.Bounds, 8, policy, boundaryCache);
                maps[cell.Index] = map;
                validationQueries += map.Approximation.IndependentValidationPointCount;
            }
            mapTimer.Stop();
        }
        if (samplingTimer.IsRunning)
        {
            samplingTimer.Stop();
        }

        var aggregation = Stopwatch.StartNew();
        var estimates = topology.Cut.ToDictionary(cell => cell.Index, cell => BoundaryOffsetMapIntegrator.IntegrateColumn(maps[cell.Index], cell.Bounds));
        var cellVolume = CellVolume(lattice);
        var volume = topology.Inside * cellVolume + topology.Cut.Sum(cell => estimates[cell.Index].OccupancyFraction * cellVolume);
        var area = estimates.Values.Sum(value => value.BoundaryArea);
        var firstMoment = estimates.Values.Aggregate(Vector3D.Zero, (sum, value) => sum + value.BoundaryFirstMoment);
        var normalIntegral = estimates.Values.Aggregate(Vector3D.Zero, (sum, value) => sum + value.AreaWeightedNormal);
        var centroid = area <= 0d ? Vector3D.Zero : firstMoment / area;
        var aggregateNormal = normalIntegral.TryNormalize(out var n) ? n : Vector3D.Zero;
        var allMaps = maps.Values.ToArray();
        var maximumPosition = allMaps.Max(map => map.Approximation.MaximumPositionError);
        var rmsPosition = WeightedRms(allMaps, map => map.Approximation.RmsPositionError);
        var meanPosition = WeightedMean(allMaps, map => map.Approximation.MeanPositionError);
        var maximumNormal = allMaps.Max(map => map.Approximation.MaximumNormalAngleDegrees);
        var rmsNormal = WeightedRms(allMaps, map => map.Approximation.RmsNormalAngleDegrees);
        var geometryRaw = selective ? basePlans.Values.Sum(pass => pass.RawRequestedSamples) + refinedPlans.Values.Sum(pass => pass.RawRequestedSamples) : 0;
        var geometryUnique = selective ? basePlans.Values.Sum(pass => pass.UniqueExactQueries) + refinedPlans.Values.Sum(pass => pass.UniqueExactQueries) : 0;
        var geometryReused = selective ? basePlans.Values.Sum(pass => pass.ReusedSamples) + refinedPlans.Values.Sum(pass => pass.ReusedSamples) : 0;
        var mapSamples = boundaryCache.Requests;
        var raw = mapSamples + validationQueries + geometryRaw;
        var unique = boundaryCache.Misses + validationQueries + geometryUnique;
        var reused = boundaryCache.Hits + geometryReused;
        var diagnostics = topology.Cut.Select(cell =>
        {
            var map = maps[cell.Index];
            var geometryCount = selective
                ? basePlans[cell.Index].Plan.GeometrySampleCount + (refinedPlans.TryGetValue(cell.Index, out var pass) ? pass.Plan.GeometrySampleCount : 0)
                : 0;
            var reuse = selective && refined.Contains(cell.Index) ? 8 : 0;
            return Diagnostic(cell.Index, name, estimates[cell.Index].OccupancyFraction, referenceOccupancy(cell.Bounds),
                map.Approximation.MaximumPositionError, map.Approximation.MaximumNormalAngleDegrees,
                geometryCount, map.Samples.Count, reuse);
        }).ToArray();
        aggregation.Stop(); total.Stop();
        var result = Result(name, lattice, topology, mapSamples, validationQueries, geometryRaw, raw, unique, reused, boundaryCache.HitRate, geometryCache.HitRate, refined.Count,
            volume, exactVolume, area, exactArea,
            (maximumPosition, rmsPosition, meanPosition, maximumNormal, rmsNormal), centroid, aggregateNormal,
            new ContinuumM1Timings(topology.Milliseconds, mapTimer.Elapsed.TotalMilliseconds, samplingTimer.Elapsed.TotalMilliseconds, aggregation.Elapsed.TotalMilliseconds, total.Elapsed.TotalMilliseconds + topology.Milliseconds));
        return new MapRun(result, diagnostics);
    }

    private static ContinuumM1StrategyResult Result(
        string name,
        LatticeSpec lattice,
        ClassifiedTopology topology,
        long mapSamples,
        long validationSamples,
        long geometrySamples,
        long raw,
        long unique,
        long reused,
        double boundaryMapCacheHitRate,
        double geometryCacheHitRate,
        int refined,
        double volume,
        double exactVolume,
        double area,
        double exactArea,
        (double MaxPosition, double RmsPosition, double MeanPosition, double MaxNormal, double RmsNormal)? errors,
        Vector3D centroid,
        Vector3D normal,
        ContinuumM1Timings timings) => new(
            name, lattice.TotalCellCount, topology.Cut.Count, mapSamples, validationSamples, geometrySamples, raw, unique, reused,
            raw == 0 ? 0d : (double)reused / raw, boundaryMapCacheHitRate, geometryCacheHitRate, refined, volume, exactVolume, double.Abs(volume - exactVolume) / exactVolume,
            area, exactArea, double.Abs(area - exactArea), double.Abs(area - exactArea) / exactArea,
            errors?.MaxPosition, errors?.RmsPosition, errors?.MeanPosition, errors?.MaxNormal, errors?.RmsNormal,
            centroid, normal, timings);

    private static ContinuumM1CellDiagnostic Diagnostic(
        CellIndex index,
        string strategy,
        double occupancy,
        double reference,
        double? positionError,
        double? normalError,
        int geometrySamples,
        int mapSamples,
        long reuse) => new(
            index, strategy, occupancy, reference, occupancy - reference, positionError, normalError,
            geometrySamples, mapSamples, reuse, occupancy);

    private static ClassifiedTopology Classify(IContinuumRegion region, LatticeSpec lattice)
    {
        var timer = Stopwatch.StartNew();
        var cells = lattice.Indices().Select(index =>
        {
            var bounds = lattice.CellBounds(index);
            return (index, bounds, ContinuumGridClassifier.ClassifyCell(region, bounds));
        }).ToArray();
        timer.Stop();
        var cut = cells.Where(cell => cell.Item3 == CellClassification.Cut).Select(cell => (cell.index, cell.bounds)).ToArray();
        return new ClassifiedTopology(
            cells,
            cells.Count(cell => cell.Item3 == CellClassification.Inside),
            cells.Count(cell => cell.Item3 == CellClassification.Outside),
            cut,
            timer.Elapsed.TotalMilliseconds);
    }

    private static ContinuumM1FixtureResult Fixture(
        string name,
        LatticeSpec lattice,
        ClassifiedTopology topology,
        IReadOnlyList<ContinuumM1StrategyResult> rows,
        IReadOnlyList<ContinuumM1CellDiagnostic> diagnostics) =>
        new(name, lattice, topology.Inside, topology.Outside, topology.Cut.Count, rows, diagnostics);

    private static double EstimateExtrudedArea(
        LatticeSpec lattice,
        ClassifiedTopology topology,
        IReadOnlyDictionary<CellIndex, GeometrySamplePlan> plans,
        int rate)
    {
        var nx = lattice.CountX * rate;
        var ny = lattice.CountY * rate;
        var material = new bool[nx, ny];
        foreach (var cell in topology.Cells.Where(cell => cell.Index.K == 0))
        {
            for (var j = 0; j < rate; j++)
            for (var i = 0; i < rate; i++)
            {
                material[(cell.Index.I * rate) + i, (cell.Index.J * rate) + j] = cell.Classification switch
                {
                    CellClassification.Inside => true,
                    CellClassification.Outside => false,
                    _ => plans[cell.Index].Samples[(j * rate) + i].IsMaterial,
                };
            }
        }

        var dx = lattice.CellSize.X / rate;
        var dy = lattice.CellSize.Y / rate;
        var contourLength = 0d;
        for (var j = 0; j < ny - 1; j++)
        for (var i = 0; i < nx - 1; i++)
        {
            var bottomLeft = material[i, j];
            var bottomRight = material[i + 1, j];
            var topRight = material[i + 1, j + 1];
            var topLeft = material[i, j + 1];
            var crossings = new List<(double X, double Y)>(4);
            if (bottomLeft != bottomRight) crossings.Add((dx * 0.5d, 0d));
            if (bottomRight != topRight) crossings.Add((dx, dy * 0.5d));
            if (topRight != topLeft) crossings.Add((dx * 0.5d, dy));
            if (topLeft != bottomLeft) crossings.Add((0d, dy * 0.5d));
            if (crossings.Count == 2)
            {
                contourLength += Distance(crossings[0], crossings[1]);
            }
            else if (crossings.Count == 4)
            {
                // Deterministic asymptotic-decider fallback for the two binary saddle cases.
                contourLength += Distance(crossings[0], crossings[1]) + Distance(crossings[2], crossings[3]);
            }
        }

        var height = lattice.Bounds.Max.Z - lattice.Bounds.Min.Z;
        return contourLength * height;

        static double Distance((double X, double Y) a, (double X, double Y) b)
        {
            var x = a.X - b.X;
            var y = a.Y - b.Y;
            return double.Sqrt((x * x) + (y * y));
        }
    }

    private static double WeightedRms(IReadOnlyList<IBoundaryOffsetMap> maps, Func<IBoundaryOffsetMap, double> selector)
    {
        var count = maps.Sum(map => map.Approximation.IndependentValidationPointCount);
        return double.Sqrt(maps.Sum(map =>
        {
            var value = selector(map);
            return value * value * map.Approximation.IndependentValidationPointCount;
        }) / count);
    }

    private static double WeightedMean(IReadOnlyList<IBoundaryOffsetMap> maps, Func<IBoundaryOffsetMap, double> selector)
    {
        var count = maps.Sum(map => map.Approximation.IndependentValidationPointCount);
        return maps.Sum(map => selector(map) * map.Approximation.IndependentValidationPointCount) / count;
    }

    private static double CellVolume(LatticeSpec lattice) => lattice.CellSize.X * lattice.CellSize.Y * lattice.CellSize.Z;
}
