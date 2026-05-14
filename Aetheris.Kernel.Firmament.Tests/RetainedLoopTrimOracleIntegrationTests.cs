using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class RetainedLoopTrimOracleIntegrationTests
{
    [Fact]
    public void OracleTrimSelectedField_BoxCylinder_UsesSelectedCylinderField()
    {
        var result = FacePatchCandidateGenerator.Generate(new CirSubtractNode(new CirBoxNode(10,10,10), new CirCylinderNode(2,20)));
        var loop = result.Candidates.SelectMany(c => c.RetainedRegionLoops).First(l => l.OracleTrimRepresentation is not null && l.OracleTrimStrongEvidence);
        Assert.Equal("oracle-trim: selected-opposite-field-used", loop.OracleTrimRoutingDiagnostic);
        Assert.Equal(TieredTrimRepresentationKind.AnalyticCircle, loop.OracleTrimRepresentation!.Kind);
        Assert.False(loop.OracleTrimRepresentation.ExactStepExported);
        Assert.False(loop.OracleTrimRepresentation.BRepTopologyEmitted);
    }

    [Fact]
    public void OracleTrimSelectedField_CylinderMatchesBroadSimpleCase()
    {
        var result = FacePatchCandidateGenerator.Generate(new CirSubtractNode(new CirBoxNode(10,10,10), new CirCylinderNode(2,20)));
        var loop = result.Candidates.SelectMany(c => c.RetainedRegionLoops).First(l => l.OracleTrimStrongEvidence);
        Assert.NotNull(loop.OracleTrimRepresentation?.Circle);
        Assert.True(loop.OracleTrimRepresentation!.Circle!.RadiusUV > 0d);
    }

    [Fact]
    public void OracleTrimSelectedField_BoxSphere_DefersOrUsesSelectedSphereField()
    {
        var result = FacePatchCandidateGenerator.Generate(new CirSubtractNode(new CirBoxNode(10,10,10), new CirSphereNode(6)));
        Assert.Contains(result.Candidates.SelectMany(c => c.RetainedRegionLoops), l => l.OracleTrimRoutingDiagnostic.Contains("selected-opposite-field-deferred:selected-opposite-field: sphere-geometry-missing", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Candidates.SelectMany(c => c.RetainedRegionLoops), l => l.OracleTrimStrongEvidence && l.OppositeSurfaceFamily == SurfacePatchFamily.Spherical);
    }

    [Fact]
    public void OracleTrimSelectedField_BoxTorus_DefersOrUsesSelectedTorusFieldWithoutGenericExactness()
    {
        var result = FacePatchCandidateGenerator.Generate(new CirSubtractNode(new CirBoxNode(12,12,12), new CirTorusNode(4,1)));
        Assert.Contains(result.Candidates.SelectMany(c => c.RetainedRegionLoops), l => l.OracleTrimRoutingDiagnostic.Contains("selected-opposite-field-deferred:selected-opposite-field: torus-geometry-missing", StringComparison.Ordinal));
    }

    [Fact]
    public void OracleTrimSelectedField_MultipleOpposites_StillDefers()
    {
        var root = new CirSubtractNode(new CirBoxNode(10,10,10), new CirUnionNode(new CirCylinderNode(2,20), new CirCylinderNode(2,20)));
        var result = FacePatchCandidateGenerator.Generate(root);
        Assert.Contains(result.Candidates.SelectMany(c => c.RetainedRegionLoops), l => l.OracleTrimRoutingDiagnostic == "oracle-trim: multiple-opposite-sources-deferred");
    }

    [Fact]
    public void MaterializationReadiness_ReportsSelectedFieldVsDeferred()
    {
        var report = MaterializationReadinessAnalyzer.Analyze(new CirSubtractNode(new CirBoxNode(10,10,10), new CirCylinderNode(2,20)));
        var layer = report.LayerSummaries.Single(l => l.LayerName == "trim-oracle-evidence");
        Assert.True(layer.Counts.ContainsKey("strong-trim-oracle"));
        Assert.True(layer.Counts.ContainsKey("selected-field-deferred-trim-oracle"));
        Assert.True(layer.Counts.ContainsKey("broad-only-trim-oracle"));
    }
}
