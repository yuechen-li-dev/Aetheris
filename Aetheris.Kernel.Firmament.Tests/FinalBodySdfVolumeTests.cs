using System.Security.Cryptography;
using System.Text;
using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Firmament.Analysis;
using Aetheris.Kernel.Firmament.Modules;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FinalBodySdfVolumeTests
{
    private static string Source(string name) => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(
        "fixtures/Regression/Assertions/" + name + ".firmament"));

    private static FirmamentStepExportResult Build(string name)
    {
        var result = FirmamentBuildAndExport.CompileSource(Source(name));
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return result.Value;
    }

    [Fact]
    public void FinalHoleGeometryOverridesAStaleStockMirrorAndCarriesBoundaryProvenance()
    {
        var plate = Build("through-hole-plate");
        var stockMirror = Build("box").Cir;
        Assert.NotNull(stockMirror);
        var product = plate with { Cir = stockMirror };
        var measurement = new FirmamentMeasurements(product);
        var volume = measurement.Volume(20, forceSdf: true);
        var expected = 80 * 50 * 8 - Math.PI * 4 * 4 * 8;
        Assert.True(volume.Authoritative);
        Assert.False(volume.BudgetExhausted);
        Assert.InRange(expected, volume.LowerBoundMm3!.Value, volume.UpperBoundMm3!.Value);
        Assert.True(volume.UpperBoundMm3 < 80 * 50 * 8);
        Assert.Equal("CompleteFinalBrepRectangularStockThroughHoles", volume.FieldQualification);
        Assert.Equal(measurement.Topology.Faces, volume.FieldCoveredFaceCount);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plate.StepText))).ToLowerInvariant(), volume.FieldSourceStepSha256);
        Assert.Equal(plate.StepText, product.StepText);

        Assert.True(BrepSdfVolumeLowering.TryLower(measurement.Body, out var field, out var reason), reason);
        var tape = SdfTapeLowerer.Lower(field!.Root);
        Assert.True(tape.Evaluate(new(0, 0, 4)) > 0); // Empty bore.
        Assert.True(tape.Evaluate(new(0, 0, 0)) > 0); // No spurious bottom cap disk.
        Assert.True(tape.Evaluate(new(0, 0, 8)) > 0); // No spurious top cap disk.
        Assert.True(tape.Evaluate(new(10, 0, 4)) < 0); // Retained material.
    }

    [Fact]
    public void EveryFinalHoleContributesAndTwoHolesCannotReuseTheOneHoleField()
    {
        var one = Build("through-hole-plate");
        var two = Build("two-through-holes");
        var first = new FirmamentMeasurements(one).Volume(20, true);
        var second = new FirmamentMeasurements(two with { Cir = one.Cir }).Volume(20, true);
        Assert.False(second.BudgetExhausted);
        Assert.InRange(80 * 50 * 8 - 2 * Math.PI * 16 * 8, second.LowerBoundMm3!.Value, second.UpperBoundMm3!.Value);
        Assert.True(second.UpperBoundMm3 < first.LowerBoundMm3);
        Assert.NotEqual(first.FieldSourceStepSha256, second.FieldSourceStepSha256);
        Assert.Equal(first.FieldCoveredFaceCount + 1, second.FieldCoveredFaceCount);
    }

    [Fact]
    public void UncoveredFilletsCannotBeMeasuredAsTheUneditedStock()
    {
        var source = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(
            "fixtures/Regression/CanonicalGeometry/profile-fillet-convex-two-segment-top.firmament"));
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var volume = new FirmamentMeasurements(built.Value).Volume(20, true);
        Assert.False(volume.Authoritative);
        Assert.Null(volume.ValueMm3);
        Assert.Contains("No qualified final-body field", volume.Diagnostic);
    }

    [Fact]
    public void ImportedStepPlateUsesItsFinalBoundaryWithoutAnyRetainedSourceField()
    {
        var native = Build("through-hole-plate");
        var pin = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(native.StepText))).ToLowerInvariant();
        var imported = FirmamentBuildAndExport.CompileSource(Source("imported-plate"), "",
            new Dictionary<string, string>
            {
                ["plate.step"] = native.StepText,
                [FirmamentDependencyLock.FileName] = new FirmamentDependencyLock("imported-plate.firmament",
                    [new("plate.step", "step", "sha256-bytes", pin)]).ToToml()
            });
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics.Select(d => d.Message)));
        Assert.Null(imported.Value.Cir);
        var before = imported.Value.StepText;
        var volume = new FirmamentMeasurements(imported.Value).Volume(20, true);
        Assert.True(volume.Authoritative);
        Assert.False(volume.BudgetExhausted);
        Assert.InRange(80 * 50 * 8 - Math.PI * 16 * 8, volume.LowerBoundMm3!.Value, volume.UpperBoundMm3!.Value);
        Assert.Equal("CompleteFinalBrepRectangularStockThroughHoles", volume.FieldQualification);
        Assert.Equal(before, imported.Value.StepText);
    }
}
