using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public class ProfileStackChamferLabTests
{
    private static string Stable(ProfileStackChamferRow row) => string.Join("|",
        row.CaseName,
        row.Status,
        row.Succeeded,
        row.SucceededRoute,
        row.Topology.VertexCount,
        row.Topology.EdgeCount,
        row.Topology.FaceCount,
        row.Topology.PlanarFaceCount,
        row.Topology.CylindricalFaceCount,
        row.Topology.LowerPrismSideFaceCount,
        row.Topology.TransitionFaceCount,
        row.Topology.ChamferTransitionFaceCount,
        row.Topology.LoopCount,
        row.Topology.CoedgeCount,
        row.Topology.Bounds,
        string.Join(",", row.Step.PresentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", row.Step.MissingRequiredMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", row.Step.AbsentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", row.Step.UnexpectedPresentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", row.Diagnostics.OrderBy(x => x, StringComparer.Ordinal)),
        row.Recommendation);

    [Fact]
    public void ProfileStackChamferLab_IsDeterministic()
    {
        var first = ProfileStackChamferLab.RunAll();
        var second = ProfileStackChamferLab.RunAll();

        Assert.Equal(first.Select(Stable), second.Select(Stable));
    }

    [Fact]
    public void ProfileStackChamferLab_RouteAReportsExactProfileStackBlockers()
    {
        var row = ProfileStackChamferLab.RunAll().Single(x => x.CaseName == "canonical-top-pos-x-edge");

        Assert.Contains("edge-profile-x2-route-a-profile-stack-attempted", row.Diagnostics);
        Assert.Contains("edge-profile-x2-route-a-profile-stack-blocked:profile-stack-polygon-profile-blocker", row.Diagnostics);
        Assert.Contains("edge-profile-x2-profile-stack-polygon-profile-blocker", row.Diagnostics);
        Assert.Contains("edge-profile-x2-ruled-transition-emitter-missing-blocker", row.Diagnostics);
        Assert.DoesNotContain(row.Diagnostics, d => d.Contains("unknown", StringComparison.OrdinalIgnoreCase) || d.Contains("vague", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProfileStackChamferLab_RouteBProducesClosedPlanarConstructiveWitness()
    {
        var row = ProfileStackChamferLab.RunAll().Single(x => x.CaseName == "canonical-top-pos-x-edge");

        Assert.True(row.Succeeded);
        Assert.Equal(LabProfileStatus.Succeeded, row.Status);
        Assert.Equal(ProfileStackChamferRoute.SectionTransition, row.SucceededRoute);
        Assert.True(row.Topology.BodyProduced);
        Assert.Equal(12, row.Topology.VertexCount);
        Assert.Equal(20, row.Topology.EdgeCount);
        Assert.Equal(10, row.Topology.FaceCount);
        Assert.Equal(10, row.Topology.PlanarFaceCount);
        Assert.Equal(0, row.Topology.CylindricalFaceCount);
        Assert.Equal(4, row.Topology.LowerPrismSideFaceCount);
        Assert.Equal(4, row.Topology.TransitionFaceCount);
        Assert.Equal(1, row.Topology.ChamferTransitionFaceCount);
        Assert.Equal(10, row.Topology.LoopCount);
        Assert.Equal(40, row.Topology.CoedgeCount);
        Assert.Equal("[-5,-4,0]..[5,4,6]", row.Topology.Bounds);
        Assert.Contains("edge-profile-x2-route-b-section-transition-attempted", row.Diagnostics);
        Assert.Contains("edge-profile-x2-route-b-section-transition-succeeded", row.Diagnostics);
        Assert.Contains("edge-profile-x2-profile-correspondence-created", row.Diagnostics);
        Assert.Contains("edge-profile-x2-ruled-transition-faces-created", row.Diagnostics);
    }

    [Fact]
    public void ProfileStackChamferLab_StepSmokePassesWithoutCylindersOrVoids()
    {
        var row = ProfileStackChamferLab.RunAll().Single(x => x.CaseName == "canonical-top-pos-x-edge");

        Assert.True(row.Step.Exported);
        Assert.Empty(row.Step.MissingRequiredMarkers);
        Assert.Empty(row.Step.UnexpectedPresentMarkers);
        Assert.Contains("ISO-10303-21", row.Step.PresentMarkers);
        Assert.Contains("MANIFOLD_SOLID_BREP", row.Step.PresentMarkers);
        Assert.Contains("ADVANCED_FACE", row.Step.PresentMarkers);
        Assert.Contains("PLANE", row.Step.PresentMarkers);
        Assert.Contains("CYLINDRICAL_SURFACE", row.Step.AbsentMarkers);
        Assert.Contains("BREP_WITH_VOIDS", row.Step.AbsentMarkers);
        Assert.Contains("edge-profile-x2-step-smoke-succeeded", row.Diagnostics);
    }

    [Fact]
    public void ProfileStackChamferLab_CandidatePathExcludesTrimGraftEdgeSweepChamferAndBoolean()
    {
        var row = ProfileStackChamferLab.RunAll().Single(x => x.CaseName == "canonical-top-pos-x-edge");

        Assert.Contains("edge-profile-x2-no-air-edge-sweep-used", row.Diagnostics);
        Assert.Contains("edge-profile-x2-no-brep-bounded-chamfer-used", row.Diagnostics);
        Assert.Contains("edge-profile-x2-no-topology-graft-used", row.Diagnostics);
        Assert.Contains("edge-profile-x2-no-3d-boolean-used", row.Diagnostics);
    }

    [Theory]
    [InlineData("invalid-zero-width", "edge-profile-x2-invalid-dimensions-rejected")]
    [InlineData("invalid-negative-depth", "edge-profile-x2-invalid-dimensions-rejected")]
    [InlineData("invalid-zero-height", "edge-profile-x2-invalid-dimensions-rejected")]
    [InlineData("invalid-non-finite-height", "edge-profile-x2-invalid-dimensions-rejected")]
    [InlineData("invalid-zero-chamfer-distance", "edge-profile-x2-invalid-chamfer-distance-rejected")]
    [InlineData("invalid-too-large-chamfer-distance", "edge-profile-x2-invalid-chamfer-distance-rejected")]
    public void ProfileStackChamferLab_InvalidCasesRejectBeforeGeometry(string caseName, string expectedDiagnostic)
    {
        var row = ProfileStackChamferLab.RunAll().Single(x => x.CaseName == caseName);

        Assert.False(row.Succeeded);
        Assert.False(row.Topology.BodyProduced);
        Assert.Equal(ProfileStackChamferRoute.None, row.SucceededRoute);
        Assert.Contains(expectedDiagnostic, row.Diagnostics);
        Assert.DoesNotContain("edge-profile-x2-route-a-profile-stack-attempted", row.Diagnostics);
        Assert.DoesNotContain("edge-profile-x2-route-b-section-transition-attempted", row.Diagnostics);
        Assert.Equal("profile-stack-chamfer-invalid-rejected", row.Recommendation);
    }

    [Fact]
    public void ProfileStackChamferLab_RecommendationsAreFromFiniteVocabulary()
    {
        foreach (var row in ProfileStackChamferLab.RunAll())
        {
            Assert.Contains(row.Recommendation, ProfileStackChamferLab.AllowedRecommendations);
        }
    }
}
