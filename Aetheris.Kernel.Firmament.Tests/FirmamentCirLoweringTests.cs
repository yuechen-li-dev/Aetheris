using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentCirLoweringTests
{
    [Fact]
    public void FirmamentCirLowerer_BoxBasic_Succeeds()
    {
        var compile = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/box_basic.firmament"));
        var lower = FirmamentCirLowerer.Lower(compile.Compilation.Value.PrimitiveLoweringPlan!);
        Assert.True(lower.IsSuccess);

        var estimate = CirAnalyzer.EstimateVolume(lower.Value.Root, 40);
        Assert.InRange(estimate, 13950d, 14850d);
    }

    [Fact]
    public void FirmamentCirLowerer_CylinderBasic_Succeeds()
    {
        var compile = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/cylinder_basic.firmament"));
        var lower = FirmamentCirLowerer.Lower(compile.Compilation.Value.PrimitiveLoweringPlan!);
        Assert.True(lower.IsSuccess);

        Assert.Equal(CirPointClassification.Inside, CirAnalyzer.ClassifyPoint(lower.Value.Root, new Point3D(0d, 0d, 0d)).Classification);
        Assert.Equal(CirPointClassification.Outside, CirAnalyzer.ClassifyPoint(lower.Value.Root, new Point3D(100d, 0d, 0d)).Classification);
    }

    [Fact]
    public void FirmamentCirLowerer_OffsetPlacement_StillWorks()
    {
        var compile = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/placed_primitive.firmament"));
        var lower = FirmamentCirLowerer.Lower(compile.Compilation.Value.PrimitiveLoweringPlan!);
        Assert.True(lower.IsSuccess);

        var estimate = CirAnalyzer.EstimateVolume(lower.Value.Root, 48);
        Assert.InRange(estimate, 200d, 400d);
    }

    [Fact]
    public void FirmamentCirLowerer_OnFacePlacement_Succeeds_ForBoundedCase()
    {
        var compile = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/w2_cylinder_root_blind_bore_semantic.firmament"));
        var lower = FirmamentCirLowerer.Lower(compile.Compilation.Value.PrimitiveLoweringPlan!);
        Assert.True(lower.IsSuccess);
        Assert.True(lower.Value.Root.Bounds.Max.Z > 5d);
    }

    [Fact]
    public void FirmamentCirLowerer_UnsupportedPlacement_FailsClearly()
    {
        var compile = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/p1_flange_radial_hole_semantic.firmament"));
        var lower = FirmamentCirLowerer.Lower(compile.Compilation.Value.PrimitiveLoweringPlan!);
        Assert.False(lower.IsSuccess);
        Assert.Contains(lower.Diagnostics, d => d.Message.Contains("around-axis", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FirmamentCirLowerer_UnsupportedOp_FailsClearly()
    {
        var compile = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/rounded_corner_box_basic.firmament"));
        var lower = FirmamentCirLowerer.Lower(compile.Compilation.Value.PrimitiveLoweringPlan!);
        Assert.False(lower.IsSuccess);
        Assert.Contains(lower.Diagnostics, d => d.Message.Contains("Unsupported primitive", StringComparison.Ordinal));
    }
}
