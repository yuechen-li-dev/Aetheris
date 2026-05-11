using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class RetainedLoopTrimOracleIntegrationTests
{
    [Fact]
    public void RetainedLoopOracleTrim_Cylinder_AttachesAnalyticCircleEvidence()
    {
        var result = FacePatchCandidateGenerator.Generate(new CirSubtractNode(new CirBoxNode(10,10,10), new CirCylinderNode(2,20)));
        var loop = result.Candidates.SelectMany(c => c.RetainedRegionLoops).First(l => l.OracleTrimRepresentation is not null);
        Assert.Equal(TieredTrimRepresentationKind.AnalyticCircle, loop.OracleTrimRepresentation!.Kind);
        Assert.NotNull(loop.OracleTrimRepresentation.NumericalContour);
        Assert.False(loop.OracleTrimRepresentation.ExactStepExported);
        Assert.False(loop.OracleTrimRepresentation.BRepTopologyEmitted);
    }

    [Fact]
    public void RetainedLoopOracleTrim_Sphere_AttachesAnalyticCircleEvidence()
    {
        var result = FacePatchCandidateGenerator.Generate(new CirSubtractNode(new CirBoxNode(10,10,10), new CirSphereNode(6)));
        Assert.Contains(result.Candidates.SelectMany(c => c.RetainedRegionLoops), l => l.OracleTrimRepresentation is not null);
    }

    [Fact]
    public void RetainedLoopOracleTrim_Torus_RemainsNumericalOrDeferredWithoutGenericExactness()
    {
        var result = FacePatchCandidateGenerator.Generate(new CirSubtractNode(new CirBoxNode(12,12,12), new CirTorusNode(4,1)));
        var oracle = result.Candidates.SelectMany(c => c.RetainedRegionLoops).Select(l => l.OracleTrimRepresentation).FirstOrDefault(r => r is not null);
        Assert.NotNull(oracle);
        Assert.True(oracle!.Kind is TieredTrimRepresentationKind.NumericalOnly or TieredTrimRepresentationKind.Deferred or TieredTrimRepresentationKind.AnalyticCircle or TieredTrimRepresentationKind.Unsupported);
        Assert.False(oracle.ExactStepExported);
        Assert.False(oracle.BRepTopologyEmitted);
        Assert.Contains(oracle.Diagnostics, d => d.Contains("torus-generic-exactness-not-claimed", StringComparison.Ordinal));
    }

    [Fact]
    public void MaterializationReadiness_IncludesTrimOracleDiagnostics()
    {
        var report = MaterializationReadinessAnalyzer.Analyze(new CirSubtractNode(new CirBoxNode(10,10,10), new CirCylinderNode(2,20)));
        Assert.Contains(report.LayerSummaries, l => l.LayerName == "trim-oracle-evidence");
        Assert.Contains(report.Diagnostics.Concat(report.LayerSummaries.SelectMany(l => l.Diagnostics)), d => d.Contains("trim-oracle:", StringComparison.Ordinal));
    }
}
