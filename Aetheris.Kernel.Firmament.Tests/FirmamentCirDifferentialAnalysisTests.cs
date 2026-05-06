using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Lowering;
using System.Globalization;
using System.Text.Json;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentCirDifferentialAnalysisTests
{
    private const int CirVolumeResolution = 72;
    private const int BrepVolumeResolution = 20;
    private const int ReportArtifactResolution = 32;
    private static readonly JsonSerializerOptions ReportJsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly Lazy<(CirBrepDifferentialReport Report, string OutputPath, IReadOnlyList<CirBrepDifferentialCase> Cases)> CachedReportArtifact =
        new(GenerateReportArtifact, true);

    [Fact]
    public void CIRvsBRep_PrimitiveMatrix()
    {
        var cases = new[]
        {
            new CirBrepDifferentialCase("box_basic", "testdata/firmament/examples/box_basic.firmament", true, 0.02d, 0.001d,
                [
                    new DifferentialProbePoint("inside_core", new BoundsFractionalProbeLocation(0.5d, 0.5d, 0.5d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(100d, 100d, 100d)), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift", AllowVolumeUnavailable: true, AllowBoundsUnavailable: true),
            new CirBrepDifferentialCase("cylinder_basic", "testdata/firmament/examples/cylinder_basic.firmament", true, 0.03d, 0.001d,
                [
                    new DifferentialProbePoint("inside_core", new BoundsFractionalProbeLocation(0.5d, 0.5d, 0.5d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_radial", new AbsoluteProbeLocation(new Point3D(8d, 0d, 0d)), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift", AllowVolumeUnavailable: true, AllowBoundsUnavailable: true),
            new CirBrepDifferentialCase("sphere_basic", "testdata/firmament/examples/sphere_basic.firmament", true, 0.04d, 0.001d,
                [
                    new DifferentialProbePoint("inside_core", new AbsoluteProbeLocation(new Point3D(0d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(13d, 0d, 0d)), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift", AllowVolumeUnavailable: true, AllowBoundsUnavailable: true)
        };

        var report = RunMatrix(cases);
        Assert.True(report.Success, "Primitive matrix expected success report.");
    }

    [Fact]
    public void CIRvsBRep_BooleanMatrix()
    {
        var cases = new[]
        {
            new CirBrepDifferentialCase("boolean_box_cylinder_hole", "testdata/firmament/examples/boolean_box_cylinder_hole.firmament", true, 0.08d, 0.02d,
                [
                    new DifferentialProbePoint("inside_material", new BoundsCentreOffsetProbeLocation(new Vector3D(0d, 4d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_void", new AbsoluteProbeLocation(new Point3D(0d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(40d, 40d, 40d)), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift"),
            new CirBrepDifferentialCase("boolean_subtract_basic", "testdata/firmament/examples/boolean_subtract_basic.firmament", true, 0.10d, 0.02d,
                [
                    new DifferentialProbePoint("inside_material", new BoundsCentreOffsetProbeLocation(new Vector3D(-2.5d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_void", new AbsoluteProbeLocation(new Point3D(0d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(20d, 0d, 0d)), ProbeExpectation.Certain)
                ], AllowVolumeUnavailable: true),
            new CirBrepDifferentialCase("boolean_add_basic", "testdata/firmament/examples/boolean_add_basic.firmament", true, 0.10d, 0.02d,
                [
                    new DifferentialProbePoint("inside_original", new AbsoluteProbeLocation(new Point3D(-2d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_added", new AbsoluteProbeLocation(new Point3D(2.5d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(20d, 0d, 0d)), ProbeExpectation.Certain)
                ]),
            new CirBrepDifferentialCase("boolean_intersect_basic", "testdata/firmament/examples/boolean_intersect_basic.firmament", true, 0.12d, 0.02d,
                [
                    new DifferentialProbePoint("inside_overlap", new AbsoluteProbeLocation(new Point3D(0d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_left", new AbsoluteProbeLocation(new Point3D(-2.8d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(10d, 0d, 0d)), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift")
        };

        var report = RunMatrix(cases);
        Assert.True(report.Success, "Boolean matrix expected success report.");
    }

    [Fact]
    public void CIRvsBRep_PlacementMatrix()
    {
        var cases = new[]
        {
            new CirBrepDifferentialCase("placed_primitive", "testdata/firmament/examples/placed_primitive.firmament", true, 0.08d, 0.02d,
                [
                    new DifferentialProbePoint("inside_anchor", new BoundsFractionalProbeLocation(0.5d, 0.5d, 0.5d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_offset_post", new BoundsCentreOffsetProbeLocation(new Vector3D(0d, 0d, 10d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(50d, 0d, 0d)), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift", AllowVolumeUnavailable: true, AllowBoundsUnavailable: true),
            new CirBrepDifferentialCase("w2_cylinder_root_blind_bore_semantic", "testdata/firmament/examples/w2_cylinder_root_blind_bore_semantic.firmament", true, 0.20d, 0.02d,
                [
                    new DifferentialProbePoint("inside_material", new AbsoluteProbeLocation(new Point3D(20d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_void", new AbsoluteProbeLocation(new Point3D(0d, 0d, 5d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(60d, 0d, 0d)), ProbeExpectation.Certain)
                ], AllowVolumeUnavailable: true, AllowBoundsUnavailable: true)
        };

        var report = RunMatrix(cases);
        Assert.True(report.Success, "Placement matrix expected success report.");
    }

    [Fact]
    public void CIRvsBRep_DifferentialReportArtifact_IsGeneratedAndReadable()
    {
        var artifact = CachedReportArtifact.Value;
        Assert.True(File.Exists(artifact.OutputPath), $"Expected report at '{artifact.OutputPath}'.");

        var json = File.ReadAllText(artifact.OutputPath);
        var parsed = JsonSerializer.Deserialize<CirBrepDifferentialReport>(json, ReportJsonOptions);
        Assert.NotNull(parsed);
        Assert.Equal("cir-vs-brep", parsed.MatrixName);
        Assert.Equal(ReportArtifactResolution, parsed.CirVolumeResolution);
        Assert.Equal(ReportArtifactResolution, parsed.BrepVolumeResolution);
        Assert.Equal(artifact.Cases.Count, parsed.FixtureCount);
        Assert.NotNull(parsed.GeneratedAtUtc);
        Assert.NotEmpty(parsed.Fixtures);
        Assert.Contains(parsed.Fixtures, f => f.ExpectedSupport == "supported");
        Assert.Contains(parsed.Fixtures, f => f.ExpectedSupport == "unsupported");
    }

    [Fact]
    public void CIRvsBRep_DifferentialReport_RecordsFailedFixtureWithoutFailingArtifactTest()
    {
        var report = CachedReportArtifact.Value.Report;
        Assert.True(report.FailedCount >= 1);
        Assert.Contains(report.Fixtures, f => f.Status == "failed");
    }

    [Fact]
    public void CIRvsBRep_DifferentialReport_PopulatesComparisonFields()
    {
        var report = CachedReportArtifact.Value.Report;
        var supported = report.Fixtures.First(f => f.ExpectedSupport == "supported" && f.Cir.Bounds is not null && f.Brep.Bounds is not null);

        Assert.NotNull(supported.Comparisons.Bounds);
        Assert.True(supported.Comparisons.Bounds.MaxAbsDelta >= 0d);
        Assert.True(supported.Comparisons.Bounds.Tolerance >= 0d);
        Assert.NotNull(supported.Comparisons.Volume);
        Assert.True(supported.Comparisons.Volume.Tolerance >= 0d);
        Assert.NotNull(supported.Comparisons.Probes);
        Assert.True(supported.Comparisons.Probes.ProbeCount >= 1);
    }

    [Fact]
    public void CIRvsBRep_UnsupportedFixture_FailsClearly()
    {
        var @case = new CirBrepDifferentialCase("rounded_corner_box_basic", "testdata/firmament/examples/rounded_corner_box_basic.firmament", false, 0d, 0d, []);
        var compile = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(@case.FixturePath));
        var lower = FirmamentCirLowerer.Lower(compile.Compilation.Value.PrimitiveLoweringPlan!);

        Assert.False(lower.IsSuccess);
        Assert.Contains(lower.Diagnostics, d => d.Message.Contains("Unsupported primitive", StringComparison.OrdinalIgnoreCase));
    }

    private static (CirBrepDifferentialReport Report, string OutputPath, IReadOnlyList<CirBrepDifferentialCase> Cases) GenerateReportArtifact()
    {
        var cases = BuildAllCases();
        var report = RunMatrix(cases, enforceAssertions: false, cirVolumeResolution: ReportArtifactResolution, brepVolumeResolution: ReportArtifactResolution);
        var outputPath = WriteDifferentialReport(report);
        return (report, outputPath, cases);
    }

    private static IReadOnlyList<CirBrepDifferentialCase> BuildAllCases()
        =>
        [
            new CirBrepDifferentialCase("box_basic", "testdata/firmament/examples/box_basic.firmament", true, 0.02d, 0.001d,
                [
                    new DifferentialProbePoint("inside_core", new BoundsFractionalProbeLocation(0.5d, 0.5d, 0.5d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(100d, 100d, 100d)), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift", AllowVolumeUnavailable: true),
            new CirBrepDifferentialCase("cylinder_basic", "testdata/firmament/examples/cylinder_basic.firmament", true, 0.03d, 0.001d,
                [
                    new DifferentialProbePoint("inside_core", new BoundsFractionalProbeLocation(0.5d, 0.5d, 0.5d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_radial", new AbsoluteProbeLocation(new Point3D(8d, 0d, 0d)), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift", AllowVolumeUnavailable: true),
            new CirBrepDifferentialCase("sphere_basic", "testdata/firmament/examples/sphere_basic.firmament", true, 0.04d, 0.001d,
                [
                    new DifferentialProbePoint("inside_core", new AbsoluteProbeLocation(new Point3D(0d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(13d, 0d, 0d)), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift", AllowVolumeUnavailable: true, AllowBoundsUnavailable: true),
            new CirBrepDifferentialCase("boolean_box_cylinder_hole", "testdata/firmament/examples/boolean_box_cylinder_hole.firmament", true, 0.08d, 0.02d,
                [
                    new DifferentialProbePoint("inside_material", new BoundsCentreOffsetProbeLocation(new Vector3D(0d, 4d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_void", new AbsoluteProbeLocation(new Point3D(0d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(40d, 40d, 40d)), ProbeExpectation.Certain)
                ]),
            new CirBrepDifferentialCase("boolean_subtract_basic", "testdata/firmament/examples/boolean_subtract_basic.firmament", true, 0.10d, 0.02d,
                [
                    new DifferentialProbePoint("inside_material", new BoundsCentreOffsetProbeLocation(new Vector3D(-2.5d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_void", new BoundsFractionalProbeLocation(0.5d, 0.5d, 0.5d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(20d, 0d, 0d)), ProbeExpectation.Certain)
                ], AllowVolumeUnavailable: true),
            new CirBrepDifferentialCase("boolean_add_basic", "testdata/firmament/examples/boolean_add_basic.firmament", true, 0.10d, 0.02d,
                [
                    new DifferentialProbePoint("inside_original", new AbsoluteProbeLocation(new Point3D(-2d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_added", new AbsoluteProbeLocation(new Point3D(2.5d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(20d, 0d, 0d)), ProbeExpectation.Certain)
                ]),
            new CirBrepDifferentialCase("boolean_intersect_basic", "testdata/firmament/examples/boolean_intersect_basic.firmament", true, 0.12d, 0.02d,
                [
                    new DifferentialProbePoint("inside_overlap", new AbsoluteProbeLocation(new Point3D(0d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_left", new AbsoluteProbeLocation(new Point3D(-2.8d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(10d, 0d, 0d)), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift"),
            new CirBrepDifferentialCase("placed_primitive", "testdata/firmament/examples/placed_primitive.firmament", true, 0.08d, 0.02d,
                [
                    new DifferentialProbePoint("inside_anchor", new BoundsFractionalProbeLocation(0.5d, 0.5d, 0.5d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_offset_post", new BoundsCentreOffsetProbeLocation(new Vector3D(0d, 0d, 10d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(50d, 0d, 0d)), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift", AllowVolumeUnavailable: true),
            new CirBrepDifferentialCase("w2_cylinder_root_blind_bore_semantic", "testdata/firmament/examples/w2_cylinder_root_blind_bore_semantic.firmament", true, 0.20d, 0.02d,
                [
                    new DifferentialProbePoint("inside_material", new AbsoluteProbeLocation(new Point3D(20d, 0d, 0d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_void", new AbsoluteProbeLocation(new Point3D(0d, 0d, 5d)), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new AbsoluteProbeLocation(new Point3D(60d, 0d, 0d)), ProbeExpectation.Certain)
                ]),
            new CirBrepDifferentialCase("rounded_corner_box_basic", "testdata/firmament/examples/rounded_corner_box_basic.firmament", false, 0d, 0d, [])
        ];

    private static CirBrepDifferentialReport RunMatrix(IReadOnlyList<CirBrepDifferentialCase> cases, bool enforceAssertions = true, int cirVolumeResolution = CirVolumeResolution, int brepVolumeResolution = BrepVolumeResolution)
    {
        var entries = new List<CirBrepFixtureReport>(cases.Count);
        foreach (var @case in cases)
        {
            var compile = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(@case.FixturePath));
            var lower = FirmamentCirLowerer.Lower(compile.Compilation.Value.PrimitiveLoweringPlan!);

            var notes = lower.Diagnostics.Select(d => d.Message).ToList();
            var status = lower.IsSuccess ? "passed" : (@case.ExpectedSupport ? "failed" : "unsupported");
            var entry = new CirBrepFixtureReport(@case.Name, @case.FixturePath, @case.ExpectedSupport ? "supported" : "unsupported", status, null,
                new CirFixtureSection(lower.IsSuccess, notes, null, new VolumeMetric(null, cirVolumeResolution), []),
                new BrepFixtureSection(false, null, new VolumeMetric(null, brepVolumeResolution, null, null), []),
                new ComparisonSection(new ComparisonMetric(0d, true, @case.BoundsTolerance), new VolumeComparisonMetric(null, null, true, @case.VolumeTolerance), new ProbeComparisonMetric(true, 0, 0, [])),
                notes);
            entries.Add(entry);

            if (enforceAssertions)
            {
                Assert.True(lower.IsSuccess == @case.ExpectedSupport,
                $"{ @case.Name }: lowering status mismatch. expectedSupport={@case.ExpectedSupport}, actualSuccess={lower.IsSuccess}. diagnostics={string.Join(" | ", lower.Diagnostics.Select(d => d.Message))}");
            }
            if (!lower.IsSuccess)
            {
                continue;
            }

            var rootBody = ResolveComparisonBody(compile.Compilation.Value.PrimitiveExecutionResult!);
            var cirBounds = lower.Value.Root.Bounds;
            var brepBounds = ComputeBoundsFromVertices(rootBody);
            if (enforceAssertions)
            {
                Assert.True(brepBounds.HasValue || @case.AllowBoundsUnavailable, $"{@case.Name}: BRep bounds unavailable.");
            }
            if (!brepBounds.HasValue)
            {
                continue;
            }

            entry = entry with
            {
                Cir = entry.Cir with { Bounds = ToBounds(cirBounds) },
                Brep = entry.Brep with { BuildSucceeded = true, Bounds = ToBounds(brepBounds!.Value) }
            };
            entries[^1] = entry;

            var boundsMaxAbsDelta = ComputeBoundsMaxAbsDelta(cirBounds, brepBounds!.Value);
            var boundsMismatch = FindBoundsMismatch(@case, cirBounds, brepBounds!.Value);
            var boundsPassed = boundsMismatch is null || @case.ExpectedBoundsMismatchClass is not null;
            if (boundsMismatch is not null)
            {
                if (@case.ExpectedBoundsMismatchClass is null)
                {
                    if (enforceAssertions)
                    {
                        Assert.Fail(boundsMismatch);
                    }
                }

                if (enforceAssertions)
                {
                    Assert.Contains($"class={@case.ExpectedBoundsMismatchClass}", boundsMismatch, StringComparison.Ordinal);
                }
            }
            entry = entry with
            {
                Comparisons = entry.Comparisons with { Bounds = new ComparisonMetric(boundsMaxAbsDelta, boundsPassed, @case.BoundsTolerance) },
                MismatchClass = boundsMismatch is not null ? (@case.ExpectedBoundsMismatchClass ?? "placement drift") : entry.MismatchClass
            };
            entries[^1] = entry;

            var cirVolume = CirAnalyzer.EstimateVolume(lower.Value.Root, cirVolumeResolution);
            var brepVolume = EstimateBrepVolume(rootBody, brepVolumeResolution, out var unknownCount, out var sampleCount);
            var unknownRatio = sampleCount == 0 ? 1d : unknownCount / (double)sampleCount;
            entry = entry with
            {
                Cir = entry.Cir with { Volume = new VolumeMetric(cirVolume, cirVolumeResolution) },
                Brep = entry.Brep with { Volume = new VolumeMetric(brepVolume, brepVolumeResolution, unknownCount, unknownRatio) }
            };
            entries[^1] = entry;

            double? absDelta = null;
            double? relDelta = null;
            var volumePassed = true;
            if (!brepVolume.HasValue || brepVolume.Value <= 1e-9d)
            {
                if (!@case.AllowVolumeUnavailable)
                {
                    volumePassed = false;
                    if (enforceAssertions)
                    {
                        Assert.Fail($"{@case.Name}: BRep approximate volume unavailable. class=analyzer uncertainty. unknownRatio={(sampleCount == 0 ? 1d : unknownCount / (double)sampleCount):F6}");
                    }
                }
            }
            else
            {
                absDelta = Math.Abs(cirVolume - brepVolume.Value);
                // CIR is the intent reference for this differential matrix; BRep volume is analyzer-approximate.
                relDelta = absDelta.Value / Math.Max(Math.Abs(cirVolume), 1e-9d);
                volumePassed = relDelta <= @case.VolumeTolerance;
                if (enforceAssertions)
                {
                    Assert.True(volumePassed,
                $"{@case.Name}: volume mismatch. class=analyzer uncertainty or semantic drift. cir={cirVolume:F6}, brep={brepVolume.Value:F6}, relDelta={relDelta:F6}, tolerance={@case.VolumeTolerance:F6}, cirResolution={cirVolumeResolution}, brepResolution={brepVolumeResolution}, brepUnknownCount={unknownCount}, sampleCount={sampleCount}");
                }
            }
            entry = entry with
            {
                Comparisons = entry.Comparisons with { Volume = new VolumeComparisonMetric(absDelta, relDelta, volumePassed, @case.VolumeTolerance) },
                MismatchClass = !volumePassed ? (entry.MismatchClass ?? "analyzer uncertainty or semantic drift") : entry.MismatchClass
            };
            entries[^1] = entry;

            var probeMismatches = new List<ProbeMismatchMetric>();
            var probeCount = 0;
            foreach (var probe in @case.Probes)
            {
                probeCount++;
                var resolvedPoint = ResolveProbePoint(probe.Location, cirBounds);
                var cirClass = CirAnalyzer.ClassifyPoint(lower.Value.Root, resolvedPoint).Classification;
                var brepResult = BrepSpatialQueries.ClassifyPoint(rootBody, resolvedPoint);
                if (!brepResult.IsSuccess || brepResult.Value == PointContainment.Unknown)
                {
                    probeMismatches.Add(new ProbeMismatchMetric(probe.Label, resolvedPoint.ToString(), DescribeProbeLocation(probe.Location), cirClass.ToString(), brepResult.IsSuccess ? brepResult.Value.ToString() : "Failure", true, "unsupported BRep analyzer certainty"));
                    if (probe.Expectation == ProbeExpectation.Certain)
                    {
                        var unknownText = brepResult.IsSuccess ? "Unknown" : "Failure";
                        if (enforceAssertions)
                        {
                            Assert.Fail($"{@case.Name}: probe '{probe.Label}' returned BRep {unknownText}; class=unsupported BRep analyzer certainty.");
                        }
                    }

                    continue;
                }

                var brepInside = brepResult.Value == PointContainment.Inside;
                var cirInside = cirClass == CirPointClassification.Inside;
                if (brepInside != cirInside)
                {
                    probeMismatches.Add(new ProbeMismatchMetric(probe.Label, resolvedPoint.ToString(), DescribeProbeLocation(probe.Location), cirClass.ToString(), brepResult.Value.ToString(), false, "primitive convention drift or boolean semantic drift or placement drift"));
                }
                if (enforceAssertions)
                {
                    Assert.True(brepInside == cirInside,
                    $"{@case.Name}: probe mismatch for '{probe.Label}'. class=primitive convention drift or boolean semantic drift or placement drift. cir={cirClass}, brep={brepResult.Value}, point={resolvedPoint}, location={DescribeProbeLocation(probe.Location)}");
                }
            }
            entry = entry with
            {
                Comparisons = entry.Comparisons with { Probes = new ProbeComparisonMetric(probeMismatches.Count == 0, probeCount, probeMismatches.Count, probeMismatches) },
                MismatchClass = probeMismatches.Count > 0 ? (entry.MismatchClass ?? probeMismatches[0].Reason) : entry.MismatchClass,
                Status = (@case.ExpectedSupport && (entry.Comparisons.Bounds.Passed && entry.Comparisons.Volume.Passed && probeMismatches.Count == 0)) ? "passed" : entry.Status
            };
            if (@case.ExpectedSupport && (!entry.Comparisons.Bounds.Passed || !entry.Comparisons.Volume.Passed || probeMismatches.Count > 0))
            {
                entry = entry with { Status = "failed" };
            }
            entries[^1] = entry;
        }

        return BuildSummary(entries, cirVolumeResolution, brepVolumeResolution);
    }

    private static string WriteDifferentialReport(CirBrepDifferentialReport report)
    {
        var outputDir = Path.Combine(AppContext.BaseDirectory, "artifacts", "cir", "differential-matrix");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "latest.json");
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, ReportJsonOptions));
        return outputPath;
    }

    private static CirBrepDifferentialReport BuildSummary(IReadOnlyList<CirBrepFixtureReport> fixtures, int cirVolumeResolution, int brepVolumeResolution)
    {
        var failedCount = fixtures.Count(f => f.Status == "failed");
        var unsupportedCount = fixtures.Count(f => f.Status == "unsupported");
        var passedCount = fixtures.Count - failedCount - unsupportedCount;
        return new CirBrepDifferentialReport(
            Success: failedCount == 0,
            GeneratedAtUtc: DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            MatrixName: "cir-vs-brep",
            CirVolumeResolution: cirVolumeResolution,
            BrepVolumeResolution: brepVolumeResolution,
            FixtureCount: fixtures.Count,
            PassedCount: passedCount,
            FailedCount: failedCount,
            UnsupportedCount: unsupportedCount,
            Fixtures: fixtures);
    }

    private static BrepBody ResolveComparisonBody(FirmamentPrimitiveExecutionResult execution)
        => execution.ExecutedBooleans.Count > 0 ? execution.ExecutedBooleans.MaxBy(b => b.OpIndex)!.Body : execution.ExecutedPrimitives.MaxBy(p => p.OpIndex)!.Body;

    private static string? FindBoundsMismatch(CirBrepDifferentialCase @case, CirBounds cirBounds, BoundingBox3D brepBounds)
    {
        var cirExtent = cirBounds.Max - cirBounds.Min;
        var brepExtent = brepBounds.Max - brepBounds.Min;

        var checks = new (string metric, double cir, double brep, string mismatchClass)[]
        {
            ("bounds.min.x", cirBounds.Min.X, brepBounds.Min.X, "placement drift"),
            ("bounds.min.y", cirBounds.Min.Y, brepBounds.Min.Y, "placement drift"),
            ("bounds.min.z", cirBounds.Min.Z, brepBounds.Min.Z, "placement drift"),
            ("bounds.max.x", cirBounds.Max.X, brepBounds.Max.X, "placement drift"),
            ("bounds.max.y", cirBounds.Max.Y, brepBounds.Max.Y, "placement drift"),
            ("bounds.max.z", cirBounds.Max.Z, brepBounds.Max.Z, "placement drift"),
            ("bounds.extent.x", cirExtent.X, brepExtent.X, "primitive convention drift"),
            ("bounds.extent.y", cirExtent.Y, brepExtent.Y, "primitive convention drift"),
            ("bounds.extent.z", cirExtent.Z, brepExtent.Z, "primitive convention drift")
        };

        foreach (var check in checks)
        {
            var delta = Math.Abs(check.cir - check.brep);
            if (delta > @case.BoundsTolerance)
            {
                return $"{@case.Name}: {check.metric} mismatch. class={check.mismatchClass}. cir={check.cir:F6}, brep={check.brep:F6}, delta={delta:F6}, tolerance={@case.BoundsTolerance:F6}";
            }
        }

        return null;
    }

    private static double ComputeBoundsMaxAbsDelta(CirBounds cirBounds, BoundingBox3D brepBounds)
    {
        var cirExtent = cirBounds.Max - cirBounds.Min;
        var brepExtent = brepBounds.Max - brepBounds.Min;
        return new[]
        {
            Math.Abs(cirBounds.Min.X - brepBounds.Min.X),
            Math.Abs(cirBounds.Min.Y - brepBounds.Min.Y),
            Math.Abs(cirBounds.Min.Z - brepBounds.Min.Z),
            Math.Abs(cirBounds.Max.X - brepBounds.Max.X),
            Math.Abs(cirBounds.Max.Y - brepBounds.Max.Y),
            Math.Abs(cirBounds.Max.Z - brepBounds.Max.Z),
            Math.Abs(cirExtent.X - brepExtent.X),
            Math.Abs(cirExtent.Y - brepExtent.Y),
            Math.Abs(cirExtent.Z - brepExtent.Z)
        }.Max();
    }

    private static double? EstimateBrepVolume(BrepBody body, int resolution, out int unknownCount, out int sampleCount)
    {
        unknownCount = 0;
        sampleCount = 0;
        var bounds = ComputeBoundsFromVertices(body);
        if (!bounds.HasValue)
        {
            return null;
        }

        var value = bounds.Value;
        var dx = (value.Max.X - value.Min.X) / resolution;
        var dy = (value.Max.Y - value.Min.Y) / resolution;
        var dz = (value.Max.Z - value.Min.Z) / resolution;
        var cell = dx * dy * dz;
        var inside = 0;

        var queryContext = BrepSpatialQueries.CreatePointContainmentQueryContext(body);
        for (var ix = 0; ix < resolution; ix++)
        for (var iy = 0; iy < resolution; iy++)
        for (var iz = 0; iz < resolution; iz++)
        {
            var p = new Point3D(value.Min.X + (ix + 0.5d) * dx, value.Min.Y + (iy + 0.5d) * dy, value.Min.Z + (iz + 0.5d) * dz);
            var result = BrepSpatialQueries.ClassifyPoint(body, p, tolerance: null, queryContext: queryContext);
            sampleCount++;
            if (!result.IsSuccess || result.Value == PointContainment.Unknown)
            {
                unknownCount++;
                continue;
            }

            if (result.Value == PointContainment.Inside)
            {
                inside++;
            }
        }

        return inside * cell;
    }

    private static BoundingBox3D? ComputeBoundsFromVertices(BrepBody body)
    {
        var points = body.Topology.Vertices
            .Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : (Point3D?)null)
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .ToArray();
        if (points.Length == 0)
        {
            return null;
        }

        return new BoundingBox3D(
            new Point3D(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)),
            new Point3D(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z)));
    }

    private sealed record CirBrepDifferentialCase(
        string Name,
        string FixturePath,
        bool ExpectedSupport,
        double VolumeTolerance,
        double BoundsTolerance,
        IReadOnlyList<DifferentialProbePoint> Probes,
        string? ExpectedBoundsMismatchClass = null,
        bool AllowVolumeUnavailable = false,
        bool AllowBoundsUnavailable = false);

    private sealed record DifferentialProbePoint(string Label, ProbeLocation Location, ProbeExpectation Expectation);

    private static Point3D ResolveProbePoint(ProbeLocation location, CirBounds bounds)
        => location switch
        {
            AbsoluteProbeLocation absolute => absolute.Point,
            BoundsFractionalProbeLocation fractional => new Point3D(
                Lerp(bounds.Min.X, bounds.Max.X, fractional.Fx),
                Lerp(bounds.Min.Y, bounds.Max.Y, fractional.Fy),
                Lerp(bounds.Min.Z, bounds.Max.Z, fractional.Fz)),
            BoundsCentreOffsetProbeLocation offset => new Point3D(
                (bounds.Min.X + bounds.Max.X) * 0.5d + offset.Offset.X,
                (bounds.Min.Y + bounds.Max.Y) * 0.5d + offset.Offset.Y,
                (bounds.Min.Z + bounds.Max.Z) * 0.5d + offset.Offset.Z),
            _ => throw new InvalidOperationException($"Unsupported probe location type '{location.GetType().Name}'.")
        };

    private static string DescribeProbeLocation(ProbeLocation location) => location switch
    {
        AbsoluteProbeLocation absolute => $"absolute:{absolute.Point}",
        BoundsFractionalProbeLocation fractional => $"bounds-fractional:({fractional.Fx:F3},{fractional.Fy:F3},{fractional.Fz:F3})",
        BoundsCentreOffsetProbeLocation offset => $"bounds-center-offset:{offset.Offset}",
        _ => location.GetType().Name
    };

    private static double Lerp(double min, double max, double fraction) => min + ((max - min) * fraction);

    private abstract record ProbeLocation;
    private sealed record AbsoluteProbeLocation(Point3D Point) : ProbeLocation;
    private sealed record BoundsFractionalProbeLocation(double Fx, double Fy, double Fz) : ProbeLocation;
    private sealed record BoundsCentreOffsetProbeLocation(Vector3D Offset) : ProbeLocation;


    private static BoundsMetric ToBounds(CirBounds bounds) => new(new PointMetric(bounds.Min.X, bounds.Min.Y, bounds.Min.Z), new PointMetric(bounds.Max.X, bounds.Max.Y, bounds.Max.Z));

    private static BoundsMetric ToBounds(BoundingBox3D bounds) => new(new PointMetric(bounds.Min.X, bounds.Min.Y, bounds.Min.Z), new PointMetric(bounds.Max.X, bounds.Max.Y, bounds.Max.Z));
    private enum ProbeExpectation
    {
        Certain,
        AllowUnknown
    }

    private sealed record CirBrepDifferentialReport(bool Success, string GeneratedAtUtc, string MatrixName, int CirVolumeResolution, int BrepVolumeResolution, int FixtureCount, int PassedCount, int FailedCount, int UnsupportedCount, IReadOnlyList<CirBrepFixtureReport> Fixtures);
    private sealed record CirBrepFixtureReport(string Name, string Path, string ExpectedSupport, string Status, string? MismatchClass, CirFixtureSection Cir, BrepFixtureSection Brep, ComparisonSection Comparisons, IReadOnlyList<string> Notes);
    private sealed record CirFixtureSection(bool LoweringSucceeded, IReadOnlyList<string> Diagnostics, BoundsMetric? Bounds, VolumeMetric Volume, IReadOnlyList<ProbeClassificationMetric> ProbeClassifications);
    private sealed record BrepFixtureSection(bool BuildSucceeded, BoundsMetric? Bounds, VolumeMetric Volume, IReadOnlyList<ProbeClassificationMetric> ProbeClassifications);
    private sealed record ComparisonSection(ComparisonMetric Bounds, VolumeComparisonMetric Volume, ProbeComparisonMetric Probes);
    private sealed record BoundsMetric(PointMetric Min, PointMetric Max);
    private sealed record PointMetric(double X, double Y, double Z);
    private sealed record VolumeMetric(double? Value, int Resolution, int? UnknownCount = null, double? UnknownRatio = null);
    private sealed record ProbeClassificationMetric(string Label, string Point, string? Cir, string? Brep, bool Passed);
    private sealed record ComparisonMetric(double MaxAbsDelta, bool Passed, double Tolerance);
    private sealed record VolumeComparisonMetric(double? AbsDelta, double? RelativeDelta, bool Passed, double Tolerance);
    private sealed record ProbeComparisonMetric(bool Passed, int ProbeCount, int MismatchCount, IReadOnlyList<ProbeMismatchMetric> Mismatches);
    private sealed record ProbeMismatchMetric(string Label, string Point, string LocationKind, string? Cir, string? Brep, bool BrepUnknown, string Reason);
}
