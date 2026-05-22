using Aetheris.Firmament.FrictionLab.CIRLab;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class GenericCirBrepExecutorLabTests
{
    [Fact]
    public void GenericExecutor_ThroughHole_SucceedsAndExports()
    {
        var r = GenericCirBrepExecutorLab.RunScenarioMatrix().Scenarios.Single(s => s.Scenario == "A-ThroughHole");
        Assert.True(r.Status == GenericCirBrepLabStatus.Succeeded, string.Join(" | ", r.Diagnostics));
        Assert.True(r.StepExportSucceeded, string.Join(" | ", r.StepMarkers));
    }

    [Fact]
    public void GenericExecutor_BlindHole_SucceedsAndExports()
    {
        var r = GenericCirBrepExecutorLab.RunScenarioMatrix().Scenarios.Single(s => s.Scenario == "B-BlindHole");
        Assert.True(r.Status == GenericCirBrepLabStatus.Succeeded, $"Expected blind success, got {r.Status}/{r.FailureCode}");
    }

    [Fact]
    public void GenericExecutor_Counterbore_SucceedsAndExportsOrReportsBlocker()
    {
        var r = GenericCirBrepExecutorLab.RunScenarioMatrix().Scenarios.Single(s => s.Scenario == "C-Counterbore");
        Assert.True(r.Status == GenericCirBrepLabStatus.Succeeded || !string.IsNullOrWhiteSpace(r.FailureCode));
    }

    [Fact]
    public void GenericExecutor_Countersink_SucceedsAndExportsOrReportsBlocker()
    {
        var r = GenericCirBrepExecutorLab.RunScenarioMatrix().Scenarios.Single(s => s.Scenario == "D-Countersink");
        Assert.True(r.Status == GenericCirBrepLabStatus.Succeeded || !string.IsNullOrWhiteSpace(r.FailureCode));
    }

    [Fact]
    public void GenericExecutor_SteppedHole_CharacterizesWhetherGenericRouteHelps()
    {
        var r = GenericCirBrepExecutorLab.RunScenarioMatrix().Scenarios.Single(s => s.Scenario == "E-SteppedHole");
        Assert.True(r.Status == GenericCirBrepLabStatus.Succeeded || r.FailureCode.Contains("boolean"));
    }

    [Fact]
    public void GenericExecutor_UnsupportedTransform_Rejects()
    {
        var r = GenericCirBrepExecutorLab.RunScenarioMatrix().Scenarios.Single(s => s.Scenario == "F-UnsupportedTransform");
        Assert.Equal(GenericCirBrepLabStatus.Unsupported, r.Status);
        Assert.Equal("transform-non-translation-unsupported", r.FailureCode);
    }

    [Fact]
    public void GenericExecutor_ReportContainsScenarioMatrix()
    {
        var report = GenericCirBrepExecutorLab.RunScenarioMatrix();
        Assert.Contains(report.Scenarios, s => s.Scenario == "A-ThroughHole");
        Assert.Contains(report.Scenarios, s => s.Scenario == "E-SteppedHole");
        Assert.Contains(report.Scenarios, s => s.Scenario == "F-BoxMinusTorus");
    }

    [Fact]
    public void GenericExecutor_DeterministicResults()
    {
        var a = GenericCirBrepExecutorLab.RunScenarioMatrix().Scenarios.ToDictionary(x => x.Scenario);
        var b = GenericCirBrepExecutorLab.RunScenarioMatrix().Scenarios.ToDictionary(x => x.Scenario);
        foreach (var key in a.Keys)
        {
            Assert.Equal(a[key].Status, b[key].Status);
            Assert.Equal(a[key].FailureCode, b[key].FailureCode);
            Assert.Equal(string.Join(";", a[key].BooleanSequence), string.Join(";", b[key].BooleanSequence));
        }
    }
}
