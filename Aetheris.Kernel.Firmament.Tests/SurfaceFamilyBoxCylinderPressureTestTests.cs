using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceFamilyBoxCylinderPressureTestTests
{
    private static readonly string[] RequiredStages = ["InputValidation","PlanarPatchEmission","CylindricalWallEmission","TokenPairingAnalysis","StitchCandidatePlanning","CombinedBodyRemap","SharedEdgeRewrite","DuplicateEdgeCleanup","VertexMergePlanning","LoopClosureValidation","ShellClosureValidation","BrepBodyValidation","StepExportSmoke"];

    [Fact]
    public void PressureTest_BoxCylinder_ReportsConcreteStageStatuses()
    {
        var r = RunCanonical();
        Assert.Equal(RequiredStages, r.Stages.Select(s => s.Stage));
        Assert.Contains(r.Stages.SelectMany(s => s.Diagnostics), d => d.Contains("canonical-input", StringComparison.Ordinal));
    }

    [Fact]
    public void PressureTest_BoxCylinder_ReportsTopologyCounts()
    {
        var r = RunCanonical();
        Assert.True(r.TopologyCounts.ContainsKey("FaceCount") || r.Blockers.Any(b => b.Code.Contains("combined-remap-failed", StringComparison.Ordinal)));
    }

    [Fact]
    public void PressureTest_BoxCylinder_ReportsEdgeUseCounts()
    {
        var a = RunCanonical();
        var b = RunCanonical();
        Assert.Contains("EdgesWithOneCoedge", a.EdgeUseCounts.Keys);
        Assert.Equal(a.EdgeUseCounts.OrderBy(x=>x.Key), b.EdgeUseCounts.OrderBy(x=>x.Key));
    }

    [Fact]
    public void PressureTest_BoxCylinder_BlockersAreActionable()
    {
        var r = RunCanonical();
        Assert.NotEmpty(r.Blockers);
        Assert.All(r.Blockers, b =>
        {
            Assert.False(string.IsNullOrWhiteSpace(b.Stage));
            Assert.False(string.IsNullOrWhiteSpace(b.Code));
            Assert.False(string.IsNullOrWhiteSpace(b.Message));
            Assert.False(string.IsNullOrWhiteSpace(b.RecommendedFix));
        });
    }

    [Fact]
    public void PressureTest_BoxCylinder_StepSmokeIsGated()
    {
        var r = RunCanonical();
        if (!r.ShellClosureValidated)
        {
            Assert.False(r.StepExportAttempted);
            Assert.Equal("step-smoke-skipped-shell-not-closed", r.StepExportDiagnostic);
        }
    }

    [Fact]
    public void PressureTest_DeterministicReport()
    {
        var a = RunCanonical();
        var b = RunCanonical();
        Assert.Equal(a.Stages.Select(s=>s.Stage), b.Stages.Select(s=>s.Stage));
        Assert.Equal(a.Blockers.Select(x=>x.Code), b.Blockers.Select(x=>x.Code));
        Assert.Equal(a.TopologyCounts.OrderBy(x=>x.Key), b.TopologyCounts.OrderBy(x=>x.Key));
    }

    [Fact]
    public void PressureTest_RejectsNonCanonicalInput()
    {
        var r = SurfaceFamilyBoxCylinderPressureTest.Run(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirBoxNode(2, 2, 2)));
        Assert.False(r.Success);
        Assert.Contains(r.Blockers, b => b.Code == "unsupported-input-noncanonical");
    }

    private static SurfaceFamilyBoxCylinderPressureTestResult RunCanonical()
        => SurfaceFamilyBoxCylinderPressureTest.Run(new CirSubtractNode(new CirBoxNode(10, 10, 10), new CirCylinderNode(2, 8)));
}
