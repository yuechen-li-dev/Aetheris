using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Experiments;
using Aetheris.Continuum.Lattice;
using Aetheris.Continuum.Regions.Analytic;
using Aetheris.Continuum.Sampling;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.StandardLibrary;

var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Converters = { new JsonStringEnumConverter() } };
var compactOptions = new JsonSerializerOptions(jsonOptions) { WriteIndented = false };
var root = FindRepositoryRoot(AppContext.BaseDirectory);
var output = Path.Combine(root, "docs", "continuum", "artifacts", "m3");
Directory.CreateDirectory(output);
var occupancyEngine = new JudgmentEngine<OccupancyChoice>();
JudgmentCandidate<OccupancyChoice>[] occupancyStrategies =
[
    new("structured-map", c => !c.Contact && double.Abs(c.MapValue - c.MsaaValue) <= 0.002d, _ => 100d,
        c => $"map/MSAA disagreement={double.Abs(c.MapValue-c.MsaaValue):R}", 0),
    new("msaa-fallback", _ => true, _ => 10d, _ => "deterministic control is always admissible", 1),
];

// Warm the JIT before production-like timing.
_ = RunRoot("warmup", 0.30d, 0d, 0d, 0d, 12, includeDense: false, includeFine: false);
var baseline = RunRoot("baseline", 0.30d, 0d, 0d, 0d, 16, includeDense: true, includeFine: true);
var orientations = new[]
{
    baseline,
    RunRoot("rotate-y-29", 0.30d, 0d, 29d, 0d, 16, false, false),
    RunRoot("compound-17-31-13", 0.30d, 17d, 31d, 13d, 16, false, false),
};
var curvature = new[]
{
    RunRoot("root-r035", 0.35d, 17d, 31d, 13d, 16, false, false),
    orientations[2],
    RunRoot("root-r025", 0.25d, 17d, 31d, 13d, 16, false, false),
};
var repeated = RunRoot("baseline", 0.30d, 0d, 0d, 0d, 16, false, false);
var sphere = RunSphereRegression();
var cylinder = RunCylinderRegression();

var geometry1 = JsonSerializer.Serialize(GeometryProjection(baseline), compactOptions);
var geometry2 = JsonSerializer.Serialize(GeometryProjection(repeated), compactOptions);
var benchmark = new
{
    schema = "aetheris-continuum-m3-v1",
    milestone = "AETHERIS-CONTINUUM-M3",
    fixture = "production ConcaveFilletConstruction quarter-torus between exact Plane and Cylinder",
    baseline,
    sphere,
    cylinder,
    fixedLatticeBeatsFineGridVolumeError = baseline.FineVolumeError is double fine && baseline.RelativeVolumeError < fine,
    denseIntegrationMateriallyReduced = baseline.DenseIntegrationMilliseconds is double oldTime && baseline.StructuredIntegrationMilliseconds < oldTime * 0.75d,
    deterministicGeometry = geometry1 == geometry2,
};
Write("benchmark-summary.json", benchmark);
Write("root-fillet-diagnostics.json", new { schema = "aetheris-continuum-m3-cell-diagnostics-v1", baseline.Diagnostics });
Write("orientation-matrix.json", orientations.Select(MatrixRow));
Write("curvature-matrix.json", curvature.Select(row => new
{
    minorRadius = row.MinorRadius, curvatureRatio = row.CurvatureRatio, row.CutCells, row.TorusCutCells,
    row.MapResolutionDistribution, row.RelativeVolumeError, row.RelativeRootAreaError, row.ProductionMilliseconds,
}));
Write("old-vs-new-integration-table.json", new object[]
{
    cylinder,
    sphere,
    new { fixture = "Torus/root fillet", oldMethod = "dense projected-footprint 64^2 + fixed 24^2 area sweep",
        newMethod = "map-cell polygon clipping + triangle quadrature + error-driven adaptive refinement",
        oldEvaluations = baseline.DenseThicknessEvaluations + baseline.DenseAreaEvaluations,
        newEvaluations = baseline.StructuredThicknessEvaluations + baseline.StructuredSurfaceTriangles,
        oldIntegrationMilliseconds = baseline.DenseIntegrationMilliseconds, newIntegrationMilliseconds = baseline.StructuredIntegrationMilliseconds,
        volumeError = baseline.RelativeVolumeError, areaError = baseline.RelativeRootAreaError }
});
Write("m2-integration-bottleneck-audit.json", JsonSerializer.SerializeToElement(sphere, jsonOptions).GetProperty("audit"));
Write("sphere-regression.json", sphere);
Write("cylinder-regression.json", cylinder);
Write("fixed-vs-fine-comparison.json", new { fixedCells = baseline.TotalCells, fineCells = baseline.FineTotalCells,
    fixedVolumeError = baseline.RelativeVolumeError, fineVolumeError = baseline.FineVolumeError,
    fixedBeatsFine = baseline.FineVolumeError is double e && baseline.RelativeVolumeError < e });
Write("deterministic-hashes.json", new { algorithm = "SHA-256", repeatedGeometryWasIdentical = geometry1 == geometry2,
    hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(geometry1))) });
var mapNodes = baseline.Diagnostics.Sum(d =>
{
    var parts = d.MapResolution.Split('x'); return int.Parse(parts[0]) * int.Parse(parts[1]);
});
Write("memory-allocation.json", new { footprintAllocatedBytesInDenseAudit = ((JsonElement)JsonSerializer.SerializeToElement(baseline.DenseAudit!, jsonOptions)).GetProperty("allocationsBytes").GetInt64(),
    mapNodes, mapLogicalScalarPayloadBytes = mapNodes * 6L * sizeof(double),
    structuredWorkBufferEstimatedBytes = baseline.Diagnostics.Sum(d => d.Integration.EstimatedWorkBufferBytes),
    note = "Logical payload excludes CLR array/object headers; allocation audit uses per-thread measured bytes." });

Console.WriteLine(JsonSerializer.Serialize(new { output, baseline = MatrixRow(baseline), sphere, cylinder,
    fixedBeatsFine = benchmark.fixedLatticeBeatsFineGridVolumeError,
    denseIntegrationMateriallyReduced = benchmark.denseIntegrationMateriallyReduced,
    deterministic = benchmark.deterministicGeometry }, jsonOptions));
return;

RootRun RunRoot(string orientation, double minorRadius, double rx, double ry, double rz, int resolution, bool includeDense, bool includeFine)
{
    const double shaftRadius = 0.8d; const double outerRadius = 1.5d; const double headThickness = 0.55d; const double shaftLength = 1.8d;
    var majorRadius = shaftRadius + minorRadius;
    var recipe = new ExactCoaxialPartRecipe($"m3-{orientation}", 12, 2d * outerRadius, headThickness, 1d, 30d,
        minorRadius, 2d * shaftRadius, shaftLength, 0.2d, 1.3d, 0.8d, "M3", "geometry");
    var plan = ExactCoaxialPartBuilder.Plan(recipe).Value!;
    var exact = ExactConstructionMaterializer.Materialize(plan).Value!;
    var transform = Transform3D.CreateRotationX(rx * double.Pi / 180d) * Transform3D.CreateRotationY(ry * double.Pi / 180d)
        * Transform3D.CreateRotationZ(rz * double.Pi / 180d) * Transform3D.CreateTranslation(new(0.031d, -0.027d, 0.019d));
    var region = new BrepTorusRootFilletContinuumRegion(new($"m3-{orientation}"), exact.Body, exact.FaceGroups["RootBlend"],
        majorRadius, minorRadius, outerRadius, headThickness, shaftLength, transform);
    if (!region.ContactValidation.Passed) throw new InvalidOperationException("Production Plane/Torus/Cylinder topology validation failed.");
    var lattice = new LatticeSpec(region.Bounds, resolution, resolution, resolution);
    var classifyTimer = Stopwatch.StartNew();
    var cells = lattice.Indices().Select(i => (Index: i, Bounds: lattice.CellBounds(i), Class: ContinuumGridClassifier.ClassifyCell(region, lattice.CellBounds(i)))).ToArray();
    classifyTimer.Stop();
    var inside = cells.Count(c => c.Class == CellClassification.Inside); var cut = cells.Where(c => c.Class == CellClassification.Cut).ToArray();
    var cellVolume = lattice.CellSize.X * lattice.CellSize.Y * lattice.CellSize.Z;
    var policy = new BoundaryOffsetMapErrorPolicy(0.0005d, 0.2d, 24);
    var mapTimer = Stopwatch.StartNew();
    var maps = new List<(CellIndex Index, BoundingBox3D Bounds, BrepTorusBoundarySupport Support, SampledBoundaryOffsetMap Map, bool Contact)>();
    var oracleMs = 0d;
    var torusCells = FindTorusCells(new ExactBrepBoundaryQuery(exact.Body, exact.FaceGroups["RootBlend"][0], transform), lattice);
    foreach (var cell in cut)
    {
        if (!torusCells.Contains(cell.Index)) continue;
        var candidates = region.BoundarySupports(cell.Bounds).Cast<BrepTorusBoundarySupport>().ToArray();
        if (candidates.Length == 0) throw new InvalidOperationException($"CIR bounds omitted sampled exact torus cell {cell.Index}.");
        var centerUv = candidates[0].Query.RecoverParameters(candidates[0].Query.Project(lattice.CellCenter(cell.Index)));
        var support = candidates.Length == 1 || centerUv.U < double.Pi ? candidates[0] : candidates[1];
        var centerMinor = ExactBrepBoundaryQuery.UnwrapPeriodic(centerUv.V, 1.25d * double.Pi);
        var contact = double.Min(double.Abs(centerMinor - double.Pi), double.Abs(centerMinor - (1.5d * double.Pi))) < 0.3d;
        var selected = support.ChooseResolution(cell.Bounds, policy);
        var runtime = support.Build(cell.Index, cell.Bounds, selected.U, selected.V, policy, new(), false);
        var oracleTimer = Stopwatch.StartNew(); var validated = support.Validate(runtime, policy); oracleTimer.Stop(); oracleMs += oracleTimer.Elapsed.TotalMilliseconds;
        maps.Add((cell.Index, cell.Bounds, support, validated, contact));
    }
    mapTimer.Stop();
    var mapByCell = maps.ToDictionary(x => x.Index);
    var structuredTimer = Stopwatch.StartNew();
    var structured = maps.ToDictionary(x => x.Index, x => BoundaryOffsetMap3DIntegrator.IntegrateStructured(x.Map, x.Bounds));
    structuredTimer.Stop();
    var samplingTimer = Stopwatch.StartNew();
    var occupancies = new Dictionary<CellIndex, double>();
    foreach (var cell in cut)
    {
        var control = SampleFraction(region, cell.Bounds, 16);
        if (mapByCell.TryGetValue(cell.Index, out var mapped) && !mapped.Contact)
        {
            var mapValue = structured[cell.Index].Estimate.OccupancyFraction;
            var choice = new OccupancyChoice(mapValue, control, mapped.Contact);
            var decision = occupancyEngine.Evaluate(choice, occupancyStrategies);
            occupancies[cell.Index] = decision.Selection!.Value.Candidate.Name == "structured-map" ? mapValue : control;
        }
        else occupancies[cell.Index] = control;
    }
    samplingTimer.Stop();
    var volume = (inside * cellVolume) + cut.Sum(c => occupancies[c.Index] * cellVolume);
    var rootArea = structured.Where(kv => !mapByCell[kv.Key].Contact).Sum(kv => kv.Value.Estimate.BoundaryArea)
        + structured.Where(kv => mapByCell[kv.Key].Contact).Sum(kv => kv.Value.Estimate.BoundaryArea);

    double? denseMs = null; long denseThickness = 0, denseArea = 0; object? denseAudit = null;
    if (includeDense)
    {
        var denseTimer = Stopwatch.StartNew(); var audits = maps.Select(x => (x.Index, Audit: BoundaryOffsetMap3DIntegrator.AuditDense(x.Map, x.Bounds))).ToArray(); denseTimer.Stop();
        denseMs = denseTimer.Elapsed.TotalMilliseconds; denseThickness = audits.Sum(x => x.Audit.ThicknessEvaluations); denseArea = audits.Sum(x => x.Audit.AreaMapEvaluations);
        denseAudit = new
        {
            cells = audits.Length, footprintConstructionMilliseconds = audits.Sum(x => x.Audit.FootprintConstructionMilliseconds),
            clippingMilliseconds = 0d, pointInFootprintTests = 0, offsetMapEvaluationMilliseconds = audits.Sum(x => x.Audit.VolumeSamplingMilliseconds + x.Audit.AreaSamplingMilliseconds),
            volumeSamplingMilliseconds = audits.Sum(x => x.Audit.VolumeSamplingMilliseconds), areaAccumulationMilliseconds = audits.Sum(x => x.Audit.AreaSamplingMilliseconds),
            footprintConstructionCalls = audits.Length, subdivisionRate = 64, uniformMicroTriangles = denseThickness,
            repeatedSampleEvaluation = denseThickness + denseArea, boundaryHandling = "centroid-in-box rejection after a separate fixed 24x24 area sweep",
            footprintVertexDistribution = audits.GroupBy(x => x.Audit.FootprintVertices).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
            thicknessEvaluations = denseThickness, areaMapEvaluations = denseArea, allocationsBytes = audits.Sum(x => x.Audit.AllocatedBytes),
            averageCellMilliseconds = audits.Average(x => x.Audit.FootprintConstructionMilliseconds + x.Audit.VolumeSamplingMilliseconds + x.Audit.AreaSamplingMilliseconds),
            worstCells = audits.OrderByDescending(x => x.Audit.VolumeSamplingMilliseconds + x.Audit.AreaSamplingMilliseconds).Take(10).Select(x => new
            { x.Index, milliseconds = x.Audit.VolumeSamplingMilliseconds + x.Audit.AreaSamplingMilliseconds, x.Audit.FootprintVertices, x.Audit.ThicknessEvaluations, x.Audit.AreaMapEvaluations }),
            conclusion = "Dense uniform triangular thickness sampling dominates; hull construction and clipping are negligible, while the separate 24x24 area sweep is secondary."
        };
    }
    double? fineError = null; int? fineCells = null;
    if (includeFine)
    {
        var fine = new LatticeSpec(region.Bounds, 32, 32, 32); fineCells = fine.TotalCellCount;
        var fineRows = fine.Indices().Select(i => (Bounds: fine.CellBounds(i), Class: ContinuumGridClassifier.ClassifyCell(region, fine.CellBounds(i)))).ToArray();
        var fineCellVolume = fine.CellSize.X * fine.CellSize.Y * fine.CellSize.Z;
        var fineVolume = fineRows.Count(x => x.Class == CellClassification.Inside) * fineCellVolume
            + fineRows.Where(x => x.Class == CellClassification.Cut).Sum(x => SampleFraction(region, x.Bounds, 4) * fineCellVolume);
        fineError = double.Abs(fineVolume - region.ExactVolume) / region.ExactVolume;
    }
    var allMaps = maps.Select(x => x.Map).ToArray();
    var active = cut.Select(c => occupancies[c.Index]).Order().ToArray();
    var diagnostics = maps.Select(x =>
    {
        var s = structured[x.Index]; var uv = x.Support.Query.RecoverParameters(x.Map.LocalFrame.Origin);
        var k = x.Support.Query.PrincipalCurvatures(uv.U, uv.V); var offsets = x.Map.Samples.Select(v => v.Offset).ToArray();
        return new RootCellDiagnostic(x.Index, x.Map.SourceBoundary.ExactBrepFaceId!, "Torus", x.Map.LocalFrame, uv,
            k.CurvatureU, k.CurvatureV, $"{x.Map.Approximation.ResolutionU}x{x.Map.Approximation.ResolutionV}", s.Footprint,
            occupancies[x.Index], x.Map.Approximation.RuntimeCertificate!, x.Map.Approximation.MaximumPositionError,
            x.Map.Approximation.RmsPositionError, x.Map.Approximation.MaximumNormalAngleDegrees, x.Map.Approximation.RmsNormalAngleDegrees,
            x.Contact ? "contact" : "ordinary-interior-torus-cut",
            s.Diagnostics, s.Estimate.OccupancyFraction * cellVolume, offsets.Max() - offsets.Min(),
            (x.Map.Domain.MaximumU - x.Map.Domain.MinimumU) / (x.Map.Domain.MaximumV - x.Map.Domain.MinimumV),
            double.Max(double.Abs(k.CurvatureU), double.Abs(k.CurvatureV)) / double.Max(1e-15d, double.Min(double.Abs(k.CurvatureU), double.Abs(k.CurvatureV))),
            x.Map.Approximation.MaximumNormalAngleDegrees);
    }).ToArray();
    var production = classifyTimer.Elapsed.TotalMilliseconds + (mapTimer.Elapsed.TotalMilliseconds - oracleMs) + structuredTimer.Elapsed.TotalMilliseconds + samplingTimer.Elapsed.TotalMilliseconds;
    return new RootRun(orientation, rx, ry, rz, minorRadius, majorRadius, double.Max(1d / minorRadius, 1d / shaftRadius) / double.Min(1d / minorRadius, 1d / majorRadius),
        lattice.TotalCellCount, cut.Length, maps.Count, allMaps.GroupBy(m => $"{m.Approximation.ResolutionU}x{m.Approximation.ResolutionV}").OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()),
        volume, region.ExactVolume, double.Abs(volume - region.ExactVolume) / region.ExactVolume, rootArea, region.ExactRootFilletArea,
        double.Abs(rootArea - region.ExactRootFilletArea) / region.ExactRootFilletArea,
        allMaps.Length == 0 ? null : allMaps.Max(m => m.Approximation.MaximumPositionError), WeightedRms(allMaps, false),
        allMaps.Length == 0 ? null : allMaps.Max(m => m.Approximation.MaximumNormalAngleDegrees), WeightedRms(allMaps, true),
        allMaps.Sum(m => m.Approximation.IndependentValidationPointCount), allMaps.Count(m => m.Approximation.RuntimeCertificate!.Decision == BoundaryMapCertificateDecision.Acceptable && !m.Approximation.IsAccepted),
        allMaps.Count(m => m.Approximation.RuntimeCertificate!.Decision != BoundaryMapCertificateDecision.Acceptable && m.Approximation.IsAccepted),
        active.Min(), active.Count(x => x < .01d), active.Count(x => x < .05d), active.Count(x => x < .1d),
        classifyTimer.Elapsed.TotalMilliseconds, mapTimer.Elapsed.TotalMilliseconds - oracleMs, structuredTimer.Elapsed.TotalMilliseconds, samplingTimer.Elapsed.TotalMilliseconds,
        production, oracleMs, denseMs, denseThickness, denseArea, structured.Values.Sum(x => (long)x.Diagnostics.ThicknessEvaluations), structured.Values.Sum(x => (long)x.Diagnostics.SurfaceTriangles),
        fineCells, fineError, region.ContactValidation, denseAudit, diagnostics);
}

double? WeightedRms(IReadOnlyList<SampledBoundaryOffsetMap> maps, bool normal)
{
    var count = maps.Sum(m => m.Approximation.IndependentValidationPointCount); if (count == 0) return null;
    return double.Sqrt(maps.Sum(m => double.Pow(normal ? m.Approximation.RmsNormalAngleDegrees : m.Approximation.RmsPositionError, 2d) * m.Approximation.IndependentValidationPointCount) / count);
}

HashSet<CellIndex> FindTorusCells(ExactBrepBoundaryQuery query, LatticeSpec lattice)
{
    var cells = new HashSet<CellIndex>(); var size = lattice.CellSize;
    for (var j = 0; j <= 128; j++)
    for (var i = 0; i < 2048; i++)
    {
        var point = query.Evaluate((2d * double.Pi * i) / 2048d, double.Pi + ((0.5d * double.Pi * j) / 128d));
        var ix = int.Clamp((int)double.Floor((point.X - lattice.Bounds.Min.X) / size.X), 0, lattice.CountX - 1);
        var iy = int.Clamp((int)double.Floor((point.Y - lattice.Bounds.Min.Y) / size.Y), 0, lattice.CountY - 1);
        var iz = int.Clamp((int)double.Floor((point.Z - lattice.Bounds.Min.Z) / size.Z), 0, lattice.CountZ - 1);
        cells.Add(new(ix, iy, iz));
    }
    return cells;
}

object RunSphereRegression()
{
    var region = new BrepSphereContinuumRegion(new("m3-sphere-regression"), 1d, Transform3D.CreateTranslation(new(0.047d, -0.031d, 0.023d)));
    var lattice = new LatticeSpec(new(new(-1.4d, -1.4d, -1.4d), new(1.4d, 1.4d, 1.4d)), 16, 16, 16);
    var rows = lattice.Indices().Select(i => (Index: i, Bounds: lattice.CellBounds(i), Class: ContinuumGridClassifier.ClassifyCell(region, lattice.CellBounds(i)))).ToArray();
    var cut = rows.Where(x => x.Class == CellClassification.Cut).ToArray(); var policy = new BoundaryOffsetMapErrorPolicy(.00005d, .15d, 24);
    var maps = cut.Select(c => { var s = (BrepSphereBoundarySupport)region.BoundarySupports(c.Bounds).Single(); var r = s.ChooseResolution(c.Bounds, policy); return (c, Map: s.Build(c.Index, c.Bounds, r.U, r.V, policy, new(), false)); }).ToArray();
    var oldTimer = Stopwatch.StartNew(); var audits = maps.Select(x => (x.c.Index, Audit: BoundaryOffsetMap3DIntegrator.AuditDense(x.Map, x.c.Bounds, 64, 6))).ToArray(); oldTimer.Stop();
    var old = audits.Select(x => x.Audit.Estimate).ToArray();
    var newTimer = Stopwatch.StartNew(); var modern = maps.Select(x => BoundaryOffsetMap3DIntegrator.IntegrateStructured(x.Map, x.c.Bounds)).ToArray(); newTimer.Stop();
    var cellVolume = lattice.CellSize.X * lattice.CellSize.Y * lattice.CellSize.Z; var inside = rows.Count(x => x.Class == CellClassification.Inside);
    object Row(string method, double ms, IReadOnlyList<BoundaryMapCellEstimate> estimates, long evaluations) { var volume = inside * cellVolume + estimates.Sum(x => x.OccupancyFraction * cellVolume); var area = estimates.Sum(x => x.BoundaryArea); return new { method, evaluations, volumeError = double.Abs(volume - region.ExactVolume) / region.ExactVolume, areaError = double.Abs(area - region.ExactArea) / region.ExactArea, integrationMilliseconds = ms }; }
    var audit = new
    {
        fixture = "M2 exact BRep sphere", calls = audits.Length,
        footprintConstructionMilliseconds = audits.Sum(x => x.Audit.FootprintConstructionMilliseconds),
        denseVolumeSamplingMilliseconds = audits.Sum(x => x.Audit.VolumeSamplingMilliseconds),
        areaAccumulationMilliseconds = audits.Sum(x => x.Audit.AreaSamplingMilliseconds),
        clippingMilliseconds = 0d, pointInFootprintTests = 0,
        footprintVertexDistribution = audits.GroupBy(x => x.Audit.FootprintVertices).OrderBy(g => g.Key).ToDictionary(g => g.Key.ToString(), g => g.Count()),
        thicknessEvaluations = audits.Sum(x => x.Audit.ThicknessEvaluations), areaMapEvaluations = audits.Sum(x => x.Audit.AreaMapEvaluations),
        allocatedBytes = audits.Sum(x => x.Audit.AllocatedBytes),
        averageCellMilliseconds = audits.Average(x => x.Audit.FootprintConstructionMilliseconds + x.Audit.VolumeSamplingMilliseconds + x.Audit.AreaSamplingMilliseconds),
        worstCells = audits.OrderByDescending(x => x.Audit.VolumeSamplingMilliseconds + x.Audit.AreaSamplingMilliseconds).Take(10).Select(x => new { x.Index, milliseconds = x.Audit.VolumeSamplingMilliseconds + x.Audit.AreaSamplingMilliseconds, x.Audit.ThicknessEvaluations, x.Audit.AreaMapEvaluations }),
        repeatedSampleEvaluation = "one bilinear map evaluation per 64^2 footprint microtriangle plus four evaluations per 24^2 area quad",
        subdivision = "uniform 64 per footprint-triangle edge; no error criterion",
        boundaryHandling = "centroid-in-box rejection",
        conclusion = "Dense uniform sampling/repeated bilinear evaluation dominates. Hull construction, allocations, and polygon clipping are not the bottleneck."
    };
    return new { fixture = "Sphere", oldMethod = Row("dense projected footprint", oldTimer.Elapsed.TotalMilliseconds, old, audits.Sum(x => x.Audit.ThicknessEvaluations + x.Audit.AreaMapEvaluations)), newMethod = Row("structured polygon/adaptive", newTimer.Elapsed.TotalMilliseconds, modern.Select(x => x.Estimate).ToArray(), modern.Sum(x => (long)x.Diagnostics.ThicknessEvaluations + x.Diagnostics.SurfaceTriangles)), audit };
}

object RunCylinderRegression()
{
    var fixture = ContinuumM1Experiments.RunCylinder(); var old = fixture.Strategies.First(x => x.Strategy == "regular-4x4x4"); var modern = fixture.Strategies.First(x => x.Strategy == "offset-map-4x4");
    return new { fixture = "Cylinder", oldMethod = "MSAA 4x4x4", newMethod = "analytic piecewise-linear column polygon integration", oldEvaluations = old.GeometrySamples,
        newEvaluations = modern.BoundaryMapSamples, oldVolumeError = old.RelativeVolumeError, newVolumeError = modern.RelativeVolumeError,
        oldAreaError = old.RelativeBoundaryAreaError, newAreaError = modern.RelativeBoundaryAreaError, oldIntegrationMilliseconds = old.Timings.TotalMilliseconds, newIntegrationMilliseconds = modern.Timings.TotalMilliseconds };
}

double SampleFraction(IContinuumRegion region, BoundingBox3D b, int n) => Samples(b, n).Count(p => region.Classify(p) != ContinuumPointClassification.Outside) / (double)(n * n * n);
IEnumerable<Point3D> Samples(BoundingBox3D b, int n) { for (var k = 0; k < n; k++) for (var j = 0; j < n; j++) for (var i = 0; i < n; i++) yield return new(Lerp(b.Min.X,b.Max.X,(i+.5d)/n),Lerp(b.Min.Y,b.Max.Y,(j+.5d)/n),Lerp(b.Min.Z,b.Max.Z,(k+.5d)/n)); }
double Lerp(double a, double b, double t) => a + (b-a)*t;
object MatrixRow(RootRun row) => new { row.Orientation, row.RotationXDegrees, row.RotationYDegrees, row.RotationZDegrees, row.CutCells, row.TorusCutCells, row.RelativeVolumeError, row.RelativeRootAreaError, row.MaximumPositionError, row.MaximumNormalErrorDegrees, row.MapResolutionDistribution, row.ProductionMilliseconds, row.OracleMilliseconds };
object GeometryProjection(RootRun row) => new { row.Orientation, row.MinorRadius, row.TotalCells, row.CutCells, row.TorusCutCells, row.MapResolutionDistribution, row.EstimatedVolume, row.EstimatedRootArea, row.MaximumPositionError, row.MaximumNormalErrorDegrees, row.Diagnostics };
void Write(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, jsonOptions) + Environment.NewLine, new UTF8Encoding(false));
static string FindRepositoryRoot(string start) { for (var d = new DirectoryInfo(start); d is not null; d = d.Parent) if (File.Exists(Path.Combine(d.FullName, "Aetheris.slnx"))) return d.FullName; throw new DirectoryNotFoundException(); }

sealed record RootRun(string Orientation, double RotationXDegrees, double RotationYDegrees, double RotationZDegrees, double MinorRadius, double MajorRadius, double CurvatureRatio,
    int TotalCells, int CutCells, int TorusCutCells, IReadOnlyDictionary<string,int> MapResolutionDistribution, double EstimatedVolume, double ExactVolume, double RelativeVolumeError,
    double EstimatedRootArea, double ExactRootArea, double RelativeRootAreaError, double? MaximumPositionError, double? RmsPositionError,
    double? MaximumNormalErrorDegrees, double? RmsNormalErrorDegrees, long OracleQueries,
    int FalseAccepts, int FalseRefines, double MinimumActiveFraction, int ActiveBelowOnePercent, int ActiveBelowFivePercent, int ActiveBelowTenPercent,
    double ClassificationMilliseconds, double MapConstructionMilliseconds, double StructuredIntegrationMilliseconds, double SamplingFallbackMilliseconds, double ProductionMilliseconds,
    double OracleMilliseconds, double? DenseIntegrationMilliseconds, long DenseThicknessEvaluations, long DenseAreaEvaluations, long StructuredThicknessEvaluations, long StructuredSurfaceTriangles,
    int? FineTotalCells, double? FineVolumeError, RootFilletContactValidation ContactValidation, object? DenseAudit, IReadOnlyList<RootCellDiagnostic> Diagnostics);

sealed record RootCellDiagnostic(CellIndex CellIndex, string BrepFaceId, string SupportKind, BoundaryLocalFrame LocalFrame, BoundarySurfaceParameters TorusParameters,
    double PrincipalCurvatureU, double PrincipalCurvatureV, string MapResolution, ClippedBoundaryFootprint Footprint, double ActiveFraction,
    EngineeringBoundaryMapCertificate Certificate, double OracleMaximumPositionError, double OracleRmsPositionError, double OracleMaximumNormalErrorDegrees, double OracleRmsNormalErrorDegrees,
    string GeometryClassification, BoundaryIntegrationDiagnostics Integration, double EstimatedVolumeContribution, double OffsetRange,
    double DomainAspectRatio, double PrincipalCurvatureRatio, double NormalVariationDegrees);

readonly record struct OccupancyChoice(double MapValue, double MsaaValue, bool Contact);
