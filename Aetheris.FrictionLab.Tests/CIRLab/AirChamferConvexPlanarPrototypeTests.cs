using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class AirChamferConvexPlanarPrototypeTests
{
    [Theory]
    [InlineData("canonical-orthogonal-convex-planar-single-edge")]
    [InlineData("nonorthogonal-convex-planar-single-edge")]
    public void Evaluate_AcceptedConvexCases_ProducesPlanArtifactWitness(string caseName)
    {
        var c = AirChamferTopologyPlanLab.Cases().Single(x => x.CaseName == caseName);
        var result = AirChamferConvexPlanarPrototype.Evaluate(ToRequest(c));

        Assert.Equal(AirChamferPrototypeStatus.Accepted, result.Status);
        Assert.NotNull(result.JudgmentScore);
        Assert.NotNull(result.TopologyPlan);
        Assert.NotNull(result.GeometryArtifact);
        Assert.NotNull(result.ClosedWitness);
        Assert.Contains("edge-v1-judgment-engine-used", result.Diagnostics);
        Assert.Contains("edge-v1-topology-plan-created", result.Diagnostics);
        Assert.Contains("edge-v1-geometry-artifact-created", result.Diagnostics);
        Assert.Contains("edge-v1-closed-witness-created", result.Diagnostics);
        Assert.Contains("edge-v1-closed-witness-step-smoke-succeeded", result.Diagnostics);
        Assert.Contains("edge-v1-legacy-authority-preserved", result.Diagnostics);
        Assert.Contains("edge-v1-no-production-route-replacement", result.Diagnostics);
        Assert.DoesNotContain(result.Diagnostics, d => d.Contains("3d-boolean", StringComparison.OrdinalIgnoreCase) && !d.Equals("edge-v1-no-3d-boolean-used", StringComparison.Ordinal));

        var step = result.ClosedWitness!.StepSummary;
        Assert.True(step.Succeeded);
        Assert.True(step.HasIso);
        Assert.True(step.HasManifoldSolidBrep);
        Assert.True(step.HasAdvancedFace);
        Assert.True(step.HasPlane);
        Assert.False(step.HasCylindricalSurface);
        Assert.False(step.HasBrepWithVoids);
    }

    [Theory]
    [InlineData("convex-planar-unsafe-envelope", AirChamferPrototypeStatus.Rejected)]
    [InlineData("invalid-distance", AirChamferPrototypeStatus.Rejected)]
    [InlineData("invalid-edge", AirChamferPrototypeStatus.Rejected)]
    [InlineData("invalid-face-adjacency", AirChamferPrototypeStatus.Rejected)]
    [InlineData("ambiguous-classification", AirChamferPrototypeStatus.Rejected)]
    [InlineData("edge-chain", AirChamferPrototypeStatus.Deferred)]
    [InlineData("corner-chain", AirChamferPrototypeStatus.Deferred)]
    [InlineData("triangle-legacy-dependent", AirChamferPrototypeStatus.FallbackLegacy)]
    public void Evaluate_RejectedDeferredAndFallbackCases_NoArtifacts(string caseName, AirChamferPrototypeStatus expectedStatus)
    {
        var c = AirChamferTopologyPlanLab.Cases().Single(x => x.CaseName == caseName);
        var result = AirChamferConvexPlanarPrototype.Evaluate(ToRequest(c));

        Assert.Equal(expectedStatus, result.Status);
        Assert.Null(result.TopologyPlan);
        Assert.Null(result.GeometryArtifact);
        Assert.Null(result.ClosedWitness);
    }

    [Fact]
    public void Evaluate_IsDeterministicAcrossRepeatedRuns()
    {
        var c = AirChamferTopologyPlanLab.Cases().Single(x => x.CaseName == "canonical-orthogonal-convex-planar-single-edge");
        var a = AirChamferConvexPlanarPrototype.Evaluate(ToRequest(c));
        var b = AirChamferConvexPlanarPrototype.Evaluate(ToRequest(c));

        Assert.Equal(a.Status, b.Status);
        Assert.Equal(a.Decision, b.Decision);
        Assert.Equal(a.Diagnostics, b.Diagnostics);
        Assert.Equal(a.JudgmentScore, b.JudgmentScore);
    }

    private static AirChamferConvexPlanarPrototypeRequest ToRequest(AirChamferTopologyPlanCase c)
        => new(
            c.CaseName,
            c.Request.EdgeStart,
            c.Request.EdgeEnd,
            c.Request.FaceANormal,
            c.Request.FaceBNormal,
            c.Request.ChamferDistance,
            c.Request.FaceFamily,
            c.Request.IsEdgeChain,
            c.Request.IsCornerChain,
            c.Request.LegacyDependency,
            c.Request.RoutePreference,
            c.Request.ClassificationExpectation,
            c.Request.IsOrthogonalPlanarPair,
            c.Request.LocalFeatureEnvelope,
            IncludeGeometryArtifact: true,
            IncludeClosedWitness: true);
}
