using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class RetainedLoopTrimOracleIntegrationTests
{
    [Fact]
    public void OracleTrimRouting_BoxCylinder_SelectsSpecificOpposite()
    {
        var result = FacePatchCandidateGenerator.Generate(new CirSubtractNode(new CirBoxNode(10,10,10), new CirCylinderNode(2,20)));
        var loop = result.Candidates.SelectMany(c => c.RetainedRegionLoops).First(l => l.OracleTrimRepresentation is not null && l.OracleTrimStrongEvidence);
        Assert.Equal("oracle-trim: specific-opposite-selected", loop.OracleTrimRoutingDiagnostic);
        Assert.Equal(TieredTrimRepresentationKind.AnalyticCircle, loop.OracleTrimRepresentation!.Kind);
        Assert.False(loop.OracleTrimRepresentation.ExactStepExported);
        Assert.False(loop.OracleTrimRepresentation.BRepTopologyEmitted);
    }

    [Fact]
    public void OracleTrimRouting_BoxSphere_SelectsSpecificOpposite()
    {
        var result = FacePatchCandidateGenerator.Generate(new CirSubtractNode(new CirBoxNode(10,10,10), new CirSphereNode(6)));
        Assert.Contains(result.Candidates.SelectMany(c => c.RetainedRegionLoops), l => l.OracleTrimRoutingDiagnostic == "oracle-trim: specific-opposite-selected");
    }

    [Fact]
    public void OracleTrimRouting_BoxTorus_NoGenericExactness()
    {
        var result = FacePatchCandidateGenerator.Generate(new CirSubtractNode(new CirBoxNode(12,12,12), new CirTorusNode(4,1)));
        var oracle = result.Candidates.SelectMany(c => c.RetainedRegionLoops).Select(l => l.OracleTrimRepresentation).FirstOrDefault(r => r is not null);
        Assert.NotNull(oracle);
        Assert.False(oracle!.ExactStepExported);
        Assert.False(oracle.BRepTopologyEmitted);
        Assert.Contains(oracle.Diagnostics, d => d.Contains("torus-generic-exactness-not-claimed", StringComparison.Ordinal));
    }

    [Fact]
    public void OracleTrimRouting_MultipleOpposites_Defers()
    {
        var root = new CirSubtractNode(new CirBoxNode(10,10,10), new CirUnionNode(new CirCylinderNode(2,20), new CirCylinderNode(2,20)));
        var result = FacePatchCandidateGenerator.Generate(root);
        Assert.Contains(result.Candidates.SelectMany(c => c.RetainedRegionLoops), l => l.OracleTrimRoutingDiagnostic == "oracle-trim: multiple-opposite-sources-deferred");
    }

    [Fact]
    public void OracleTrimRouting_MissingOpposite_Diagnosed()
    {
        var loop = new RetainedRegionLoopDescriptor(RetainedRegionLoopKind.InnerTrim, TrimCurveFamily.Circle, TrimCapabilityClassification.ExactSupported, SurfacePatchFamily.Planar, SurfacePatchFamily.Spherical,
            FacePatchOrientationRole.Forward, FacePatchRetentionRole.BaseBoundaryRetainedOutsideTool, RetainedRegionLoopStatus.ExactReady, "missing", "diag", null, null, false, "oracle-trim: missing-opposite-source");
        Assert.Equal("oracle-trim: missing-opposite-source", loop.OracleTrimRoutingDiagnostic);
    }

    [Fact]
    public void MaterializationReadiness_TrimOracleLayer_DistinguishesStrongVsBroad()
    {
        var report = MaterializationReadinessAnalyzer.Analyze(new CirSubtractNode(new CirBoxNode(10,10,10), new CirCylinderNode(2,20)));
        var layer = report.LayerSummaries.Single(l => l.LayerName == "trim-oracle-evidence");
        Assert.True(layer.Counts.ContainsKey("strong-trim-oracle"));
        Assert.True(layer.Diagnostics.Any(d => d.StartsWith("oracle-trim:", StringComparison.Ordinal)));
    }
}
