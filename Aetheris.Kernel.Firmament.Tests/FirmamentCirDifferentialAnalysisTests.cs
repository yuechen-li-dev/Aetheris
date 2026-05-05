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
    private const int SharedVolumeResolution = 72;
    private static readonly JsonSerializerOptions ReportJsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public void CIRvsBRep_PrimitiveMatrix()
    {
        var cases = new[]
        {
            new CirBrepDifferentialCase("box_basic", "testdata/firmament/examples/box_basic.firmament", true, 0.02d, 0.001d,
                [
                    new DifferentialProbePoint("inside_core", new Point3D(0d, 0d, 1d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(100d, 100d, 100d), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift", AllowVolumeUnavailable: true),
            new CirBrepDifferentialCase("cylinder_basic", "testdata/firmament/examples/cylinder_basic.firmament", true, 0.03d, 0.001d,
                [
                    new DifferentialProbePoint("inside_core", new Point3D(0d, 0d, 1d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_radial", new Point3D(8d, 0d, 0d), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift"),
            new CirBrepDifferentialCase("sphere_basic", "testdata/firmament/examples/sphere_basic.firmament", true, 0.04d, 0.001d,
                [
                    new DifferentialProbePoint("inside_core", new Point3D(0d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(13d, 0d, 0d), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift")
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
                    new DifferentialProbePoint("inside_material", new Point3D(10d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_void", new Point3D(0d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(40d, 0d, 0d), ProbeExpectation.Certain)
                ]),
            new CirBrepDifferentialCase("boolean_subtract_basic", "testdata/firmament/examples/boolean_subtract_basic.firmament", true, 0.10d, 0.02d,
                [
                    new DifferentialProbePoint("inside_material", new Point3D(-2.5d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_void", new Point3D(0d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(20d, 0d, 0d), ProbeExpectation.Certain)
                ]),
            new CirBrepDifferentialCase("boolean_add_basic", "testdata/firmament/examples/boolean_add_basic.firmament", true, 0.10d, 0.02d,
                [
                    new DifferentialProbePoint("inside_original", new Point3D(-2d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_added", new Point3D(2.5d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(20d, 0d, 0d), ProbeExpectation.Certain)
                ]),
            new CirBrepDifferentialCase("boolean_intersect_basic", "testdata/firmament/examples/boolean_intersect_basic.firmament", true, 0.12d, 0.02d,
                [
                    new DifferentialProbePoint("inside_overlap", new Point3D(0d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_left", new Point3D(-2.8d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(10d, 0d, 0d), ProbeExpectation.Certain)
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
                    new DifferentialProbePoint("inside_anchor", new Point3D(0d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_offset_post", new Point3D(0d, 0d, 10d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(50d, 0d, 0d), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift", AllowVolumeUnavailable: true),
            new CirBrepDifferentialCase("w2_cylinder_root_blind_bore_semantic", "testdata/firmament/examples/w2_cylinder_root_blind_bore_semantic.firmament", true, 0.20d, 0.02d,
                [
                    new DifferentialProbePoint("inside_material", new Point3D(20d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_void", new Point3D(0d, 0d, 5d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(60d, 0d, 0d), ProbeExpectation.Certain)
                ])
        };

        var report = RunMatrix(cases);
        Assert.True(report.Success, "Placement matrix expected success report.");
    }

    [Fact]
    public void CIRvsBRep_DifferentialReportArtifact_IsGeneratedAndReadable()
    {
        var cases = BuildAllCases();
        var report = RunMatrix(cases, enforceAssertions: false);
        var outputPath = WriteDifferentialReport(report);

        Assert.True(File.Exists(outputPath), $"Expected report at '{outputPath}'.");

        var json = File.ReadAllText(outputPath);
        var parsed = JsonSerializer.Deserialize<CirBrepDifferentialReport>(json, ReportJsonOptions);
        Assert.NotNull(parsed);
        Assert.Equal("cir-vs-brep", parsed.MatrixName);
        Assert.Equal(SharedVolumeResolution, parsed.Resolution);
        Assert.Equal(cases.Count, parsed.FixtureCount);
        Assert.NotNull(parsed.GeneratedAtUtc);
        Assert.NotEmpty(parsed.Fixtures);
        Assert.Contains(parsed.Fixtures, f => f.Cir is not null && f.Brep is not null && f.Comparisons is not null);
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

    private static IReadOnlyList<CirBrepDifferentialCase> BuildAllCases()
        =>
        [
            new CirBrepDifferentialCase("box_basic", "testdata/firmament/examples/box_basic.firmament", true, 0.02d, 0.001d,
                [
                    new DifferentialProbePoint("inside_core", new Point3D(0d, 0d, 1d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(100d, 100d, 100d), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift", AllowVolumeUnavailable: true),
            new CirBrepDifferentialCase("cylinder_basic", "testdata/firmament/examples/cylinder_basic.firmament", true, 0.03d, 0.001d,
                [
                    new DifferentialProbePoint("inside_core", new Point3D(0d, 0d, 1d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_radial", new Point3D(8d, 0d, 0d), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift"),
            new CirBrepDifferentialCase("sphere_basic", "testdata/firmament/examples/sphere_basic.firmament", true, 0.04d, 0.001d,
                [
                    new DifferentialProbePoint("inside_core", new Point3D(0d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(13d, 0d, 0d), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift"),
            new CirBrepDifferentialCase("boolean_box_cylinder_hole", "testdata/firmament/examples/boolean_box_cylinder_hole.firmament", true, 0.08d, 0.02d,
                [
                    new DifferentialProbePoint("inside_material", new Point3D(10d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_void", new Point3D(0d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(40d, 0d, 0d), ProbeExpectation.Certain)
                ]),
            new CirBrepDifferentialCase("boolean_subtract_basic", "testdata/firmament/examples/boolean_subtract_basic.firmament", true, 0.10d, 0.02d,
                [
                    new DifferentialProbePoint("inside_material", new Point3D(-2.5d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_void", new Point3D(0d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(20d, 0d, 0d), ProbeExpectation.Certain)
                ]),
            new CirBrepDifferentialCase("boolean_add_basic", "testdata/firmament/examples/boolean_add_basic.firmament", true, 0.10d, 0.02d,
                [
                    new DifferentialProbePoint("inside_original", new Point3D(-2d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_added", new Point3D(2.5d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(20d, 0d, 0d), ProbeExpectation.Certain)
                ]),
            new CirBrepDifferentialCase("boolean_intersect_basic", "testdata/firmament/examples/boolean_intersect_basic.firmament", true, 0.12d, 0.02d,
                [
                    new DifferentialProbePoint("inside_overlap", new Point3D(0d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_left", new Point3D(-2.8d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(10d, 0d, 0d), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift"),
            new CirBrepDifferentialCase("placed_primitive", "testdata/firmament/examples/placed_primitive.firmament", true, 0.08d, 0.02d,
                [
                    new DifferentialProbePoint("inside_anchor", new Point3D(0d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_offset_post", new Point3D(0d, 0d, 10d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(50d, 0d, 0d), ProbeExpectation.Certain)
                ], ExpectedBoundsMismatchClass: "placement drift", AllowVolumeUnavailable: true),
            new CirBrepDifferentialCase("w2_cylinder_root_blind_bore_semantic", "testdata/firmament/examples/w2_cylinder_root_blind_bore_semantic.firmament", true, 0.20d, 0.02d,
                [
                    new DifferentialProbePoint("inside_material", new Point3D(20d, 0d, 0d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("inside_void", new Point3D(0d, 0d, 5d), ProbeExpectation.Certain),
                    new DifferentialProbePoint("outside_far", new Point3D(60d, 0d, 0d), ProbeExpectation.Certain)
                ]),
            new CirBrepDifferentialCase("rounded_corner_box_basic", "testdata/firmament/examples/rounded_corner_box_basic.firmament", false, 0d, 0d, [])
        ];

    private static CirBrepDifferentialReport RunMatrix(IReadOnlyList<CirBrepDifferentialCase> cases, bool enforceAssertions = true)
    {
        var entries = new List<CirBrepFixtureReport>(cases.Count);
        foreach (var @case in cases)
        {
            var compile = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(@case.FixturePath));
            var lower = FirmamentCirLowerer.Lower(compile.Compilation.Value.PrimitiveLoweringPlan!);

            var notes = lower.Diagnostics.Select(d => d.Message).ToList();
            var status = lower.IsSuccess ? "passed" : (@case.ExpectedSupport ? "failed" : "unsupported");
            var entry = new CirBrepFixtureReport(@case.Name, @case.FixturePath, @case.ExpectedSupport ? "supported" : "unsupported", status, null,
                new CirFixtureSection(lower.IsSuccess, notes, null, new VolumeMetric(null, SharedVolumeResolution), []),
                new BrepFixtureSection(false, null, new VolumeMetric(null, SharedVolumeResolution, null, null), []),
                new ComparisonSection(new ComparisonMetric(0d, true), new VolumeComparisonMetric(null, null, true), new ProbeComparisonMetric(true, [])),
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
                Assert.True(brepBounds.HasValue, $"{@case.Name}: BRep bounds unavailable.");
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

            var boundsMismatch = FindBoundsMismatch(@case, cirBounds, brepBounds!.Value);
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

            var cirVolume = CirAnalyzer.EstimateVolume(lower.Value.Root, SharedVolumeResolution);
            var brepVolume = EstimateBrepVolume(rootBody, SharedVolumeResolution, out var unknownCount, out var sampleCount);
            var unknownRatio = sampleCount == 0 ? 1d : unknownCount / (double)sampleCount;
            entry = entry with
            {
                Cir = entry.Cir with { Volume = new VolumeMetric(cirVolume, SharedVolumeResolution) },
                Brep = entry.Brep with { Volume = new VolumeMetric(brepVolume, SharedVolumeResolution, unknownCount, unknownRatio) }
            };
            entries[^1] = entry;

            if (!brepVolume.HasValue || brepVolume.Value <= 1e-9d)
            {
                if (!@case.AllowVolumeUnavailable)
                {
                    if (enforceAssertions)
                    {
                        Assert.Fail($"{@case.Name}: BRep approximate volume unavailable. class=analyzer uncertainty. unknownRatio={(sampleCount == 0 ? 1d : unknownCount / (double)sampleCount):F6}");
                    }
                }
            }
            else
            {
                var relativeDelta = Math.Abs(cirVolume - brepVolume.Value) / brepVolume.Value;
                if (enforceAssertions)
                {
                    Assert.True(relativeDelta <= @case.VolumeTolerance,
                $"{@case.Name}: volume mismatch. class=analyzer uncertainty or semantic drift. cir={cirVolume:F6}, brep={brepVolume.Value:F6}, relDelta={relativeDelta:F6}, tolerance={@case.VolumeTolerance:F6}, resolution={SharedVolumeResolution}, brepUnknownCount={unknownCount}, sampleCount={sampleCount}");
                }
            }

            foreach (var probe in @case.Probes)
            {
                var cirClass = CirAnalyzer.ClassifyPoint(lower.Value.Root, probe.Point).Classification;
                var brepResult = BrepSpatialQueries.ClassifyPoint(rootBody, probe.Point);
                if (!brepResult.IsSuccess || brepResult.Value == PointContainment.Unknown)
                {
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
                if (enforceAssertions)
                {
                    Assert.True(brepInside == cirInside,
                    $"{@case.Name}: probe mismatch for '{probe.Label}'. class=primitive convention drift or boolean semantic drift or placement drift. cir={cirClass}, brep={brepResult.Value}, point={probe.Point}");
                }
            }
        }

        return BuildSummary(entries);
    }

    private static string WriteDifferentialReport(CirBrepDifferentialReport report)
    {
        var outputDir = Path.Combine(AppContext.BaseDirectory, "artifacts", "cir", "differential-matrix");
        Directory.CreateDirectory(outputDir);
        var outputPath = Path.Combine(outputDir, "latest.json");
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, ReportJsonOptions));
        return outputPath;
    }

    private static CirBrepDifferentialReport BuildSummary(IReadOnlyList<CirBrepFixtureReport> fixtures)
    {
        var failedCount = fixtures.Count(f => f.Status == "failed");
        var unsupportedCount = fixtures.Count(f => f.Status == "unsupported");
        var passedCount = fixtures.Count - failedCount - unsupportedCount;
        return new CirBrepDifferentialReport(
            Success: failedCount == 0,
            GeneratedAtUtc: DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            MatrixName: "cir-vs-brep",
            Resolution: SharedVolumeResolution,
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

        for (var ix = 0; ix < resolution; ix++)
        for (var iy = 0; iy < resolution; iy++)
        for (var iz = 0; iz < resolution; iz++)
        {
            var p = new Point3D(value.Min.X + (ix + 0.5d) * dx, value.Min.Y + (iy + 0.5d) * dy, value.Min.Z + (iz + 0.5d) * dz);
            var result = BrepSpatialQueries.ClassifyPoint(body, p);
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
        bool AllowVolumeUnavailable = false);

    private sealed record DifferentialProbePoint(string Label, Point3D Point, ProbeExpectation Expectation);


    private static BoundsMetric ToBounds(CirBounds bounds) => new(new PointMetric(bounds.Min.X, bounds.Min.Y, bounds.Min.Z), new PointMetric(bounds.Max.X, bounds.Max.Y, bounds.Max.Z));

    private static BoundsMetric ToBounds(BoundingBox3D bounds) => new(new PointMetric(bounds.Min.X, bounds.Min.Y, bounds.Min.Z), new PointMetric(bounds.Max.X, bounds.Max.Y, bounds.Max.Z));
    private enum ProbeExpectation
    {
        Certain,
        AllowUnknown
    }

    private sealed record CirBrepDifferentialReport(bool Success, string GeneratedAtUtc, string MatrixName, int Resolution, int FixtureCount, int PassedCount, int FailedCount, int UnsupportedCount, IReadOnlyList<CirBrepFixtureReport> Fixtures);
    private sealed record CirBrepFixtureReport(string Name, string Path, string ExpectedSupport, string Status, string? MismatchClass, CirFixtureSection Cir, BrepFixtureSection Brep, ComparisonSection Comparisons, IReadOnlyList<string> Notes);
    private sealed record CirFixtureSection(bool LoweringSucceeded, IReadOnlyList<string> Diagnostics, BoundsMetric? Bounds, VolumeMetric Volume, IReadOnlyList<ProbeClassificationMetric> ProbeClassifications);
    private sealed record BrepFixtureSection(bool BuildSucceeded, BoundsMetric? Bounds, VolumeMetric Volume, IReadOnlyList<ProbeClassificationMetric> ProbeClassifications);
    private sealed record ComparisonSection(ComparisonMetric Bounds, VolumeComparisonMetric Volume, ProbeComparisonMetric Probes);
    private sealed record BoundsMetric(PointMetric Min, PointMetric Max);
    private sealed record PointMetric(double X, double Y, double Z);
    private sealed record VolumeMetric(double? Value, int Resolution, int? UnknownCount = null, double? UnknownRatio = null);
    private sealed record ProbeClassificationMetric(string Label, string Point, string? Cir, string? Brep, bool Passed);
    private sealed record ComparisonMetric(double MaxAbsDelta, bool Passed);
    private sealed record VolumeComparisonMetric(double? AbsDelta, double? RelativeDelta, bool Passed);
    private sealed record ProbeComparisonMetric(bool Passed, IReadOnlyList<string> Mismatches);
}
