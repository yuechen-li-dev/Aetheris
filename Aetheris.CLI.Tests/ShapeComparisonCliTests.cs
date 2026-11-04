using System.Text.Json;
using Aetheris.CLI;

namespace Aetheris.CLI.Tests;

public sealed class ShapeComparisonCliTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly Lazy<string> PlateBossHole = new(() => Build("fixtures/Regression/ShapeCompare/plate-boss-hole.firmament"));
    private static readonly Lazy<string> PlateBoss = new(() => Build("fixtures/Regression/ShapeCompare/plate-boss.firmament"));
    private static readonly Lazy<string> Ctc01 = new(() => Build("fixtures/Regression/Decompile/ctc-01.firmament"));
    private static string Ctc01Original => Path.Combine(RepoRoot, "testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp");

    [Fact]
    public void MissingHoleIsNamedLocatedAndMeasured()
    {
        var result = StepShapeComparison.Compare(PlateBossHole.Value, PlateBoss.Value, resolution: 300);

        var missing = Assert.Single(result.Carriers.MissingFromCandidate);
        Assert.Equal("Cylinder", missing.Kind);
        Assert.Contains("r=4", missing.Description, StringComparison.Ordinal);
        Assert.Contains("(15, 5, 0)", missing.Description, StringComparison.Ordinal);
        Assert.Empty(result.Carriers.ExtraInCandidate);

        var hole = Math.PI * 4 * 4 * 6;
        Assert.InRange(result.Overlap.CandidateOnlyVolume, hole * 0.97, hole * 1.03);
        Assert.Equal(0d, result.Overlap.ReferenceOnlyVolume, 3);
        var region = Assert.Single(result.Overlap.Regions);
        Assert.Equal("candidate-only", region.Side);
        Assert.InRange(region.BoundsMin[0], 10.5, 11.5);
        Assert.InRange(region.BoundsMax[1], 8.5, 9.5);
        Assert.Equal(0d, region.BoundsMin[2], 6);
        Assert.Equal(6d, region.BoundsMax[2], 6);
    }

    [Fact]
    public void SelfComparisonMatchesEveryCarrierAndAllMaterial()
    {
        var result = StepShapeComparison.Compare(Ctc01Original, Ctc01Original, resolution: 200);

        Assert.Empty(result.Carriers.MissingFromCandidate);
        Assert.Empty(result.Carriers.ExtraInCandidate);
        Assert.Equal(result.Carriers.ReferenceCarrierCount, result.Carriers.MatchedCarrierCount);
        Assert.Equal(1d, result.Overlap.IntersectionOverUnion, 9);
        Assert.Empty(result.Overlap.Regions);
    }

    [Fact]
    public void Ctc01ReconstructionMatchesTheOriginalExceptForKnownLanguageGaps()
    {
        var result = StepShapeComparison.Compare(Ctc01Original, Ctc01.Value);

        // Everything the reconstruction builds is on the original's surfaces.
        Assert.Empty(result.Carriers.ExtraInCandidate);
        Assert.True(result.Overlap.IntersectionOverUnion >= 0.9985, $"IoU {result.Overlap.IntersectionOverUnion}");
        Assert.True(result.Carriers.MatchedCarrierCount >= 75, $"matched {result.Carriers.MatchedCarrierCount}");

        // What is missing is exactly the features Firmament cannot yet express (see the fixture's header).
        // Closing one of these gaps shrinks this list; a new kind of discrepancy fails here.
        foreach (var carrier in result.Carriers.MissingFromCandidate)
        {
            Assert.True(
                IsHexFootFillet(carrier) || IsLugUndersideFillet(carrier),
                $"Unexpected missing carrier: {carrier.Description}");
        }
    }

    [Fact]
    public void BoxMinusHoleVolumeIsNotClaimedForStockThatIsNotTheBoundingBox()
    {
        foreach (var step in new[] { PlateBossHole.Value, Ctc01.Value })
        {
            try
            {
                var volume = StepAnalyzer.AnalyzeVolume(step);
                Assert.NotEqual("analytic-box-minus-z-hole", volume.Method);
            }
            catch (InvalidOperationException)
            {
                // No exact route for this body is an honest answer; a box formula on a non-box is not.
            }
        }
    }

    [Fact]
    public void CompareShapeOptionReportsCarriersAndOverlapInJson()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = CliRunner.Run(["analyze", "compare", PlateBossHole.Value, PlateBoss.Value, "--shape", "--shape-resolution", "150", "--json"], stdout, stderr);

        Assert.True(exit == 0, stderr.ToString());
        using var document = JsonDocument.Parse(stdout.ToString());
        var shape = document.RootElement.GetProperty("shapeComparison");
        Assert.Equal(1, shape.GetProperty("carriers").GetProperty("missingFromCandidate").GetArrayLength());
        Assert.True(shape.GetProperty("overlap").GetProperty("candidateOnlyVolume").GetDouble() > 0);
    }

    private static bool IsHexFootFillet(StepShapeComparison.CarrierGroup c)
        => c.Kind == "Cylinder" && c.Description.Contains("r=5 ", StringComparison.Ordinal) && c.BoundsMax is { } hi && hi[2] <= 5.01;

    private static bool IsLugUndersideFillet(StepShapeComparison.CarrierGroup c)
        => c.Kind == "Cylinder" && c.Description.Contains("r=10 ", StringComparison.Ordinal)
           && c.BoundsMin is { } lo && c.BoundsMax is { } hi && lo[2] >= -60.01 && hi[2] <= -49.99;

    private static string Build(string fixture)
    {
        var directory = Path.Combine(Path.GetTempPath(), "aetheris-shape-compare", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var step = Path.Combine(directory, Path.GetFileNameWithoutExtension(fixture) + ".step");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = CliRunner.Run(["build", Path.Combine(RepoRoot, fixture), "--out", step], stdout, stderr);
        Assert.True(exit == 0, $"{fixture}: {stderr}");
        return step;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
