using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class PrismaticSectionTransitionEmitterLabTests
{
    private static string Stable(PrismaticSectionTransitionRow row) => string.Join("|",
        row.CaseName,
        row.Status,
        row.Succeeded,
        row.Topology.BodyProduced,
        row.Topology.SectionCount,
        row.Topology.VertexCount,
        row.Topology.EdgeCount,
        row.Topology.BottomProfileEdgeCount,
        row.Topology.TopProfileEdgeCount,
        row.Topology.TransitionEdgeCount,
        row.Topology.CapFaceCount,
        row.Topology.TransitionFaceCount,
        row.Topology.StableIntervalFaceCount,
        row.Topology.ChangedIntervalFaceCount,
        row.Topology.FaceCount,
        row.Topology.PlanarFaceCount,
        row.Topology.CylindricalFaceCount,
        row.Topology.LoopCount,
        row.Topology.CoedgeCount,
        row.Topology.Bounds,
        row.Step.Exported,
        string.Join(",", row.Step.PresentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", row.Step.MissingRequiredMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", row.Step.AbsentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", row.Step.UnexpectedPresentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", row.Diagnostics.OrderBy(x => x, StringComparer.Ordinal)),
        row.Recommendation);

    [Fact]
    public void PrismaticSectionTransitionEmitterLab_IsDeterministic()
    {
        var first = PrismaticSectionTransitionEmitterLab.RunAll();
        var second = PrismaticSectionTransitionEmitterLab.RunAll();

        Assert.Equal(first.Select(Stable), second.Select(Stable));
    }

    [Fact]
    public void TwoSectionRectangleToInsetRectangle_EmitsClosedPlanarBrepAndStepSmoke()
    {
        var row = PrismaticSectionTransitionEmitterLab.RunAll().Single(x => x.CaseName == "rectangle-to-inset-rectangle");

        Assert.True(row.Succeeded);
        Assert.Equal(LabProfileStatus.Succeeded, row.Status);
        Assert.True(row.Topology.BodyProduced);
        Assert.Equal(2, row.Topology.SectionCount);
        Assert.Equal(8, row.Topology.VertexCount);
        Assert.Equal(12, row.Topology.EdgeCount);
        Assert.Equal(4, row.Topology.BottomProfileEdgeCount);
        Assert.Equal(4, row.Topology.TopProfileEdgeCount);
        Assert.Equal(4, row.Topology.TransitionEdgeCount);
        Assert.Equal(2, row.Topology.CapFaceCount);
        Assert.Equal(4, row.Topology.TransitionFaceCount);
        Assert.Equal(0, row.Topology.StableIntervalFaceCount);
        Assert.Equal(4, row.Topology.ChangedIntervalFaceCount);
        Assert.Equal(6, row.Topology.FaceCount);
        Assert.Equal(6, row.Topology.PlanarFaceCount);
        Assert.Equal(0, row.Topology.CylindricalFaceCount);
        Assert.Equal(6, row.Topology.LoopCount);
        Assert.Equal(24, row.Topology.CoedgeCount);
        Assert.Equal("[-5,-4,0]..[5,4,1]", row.Topology.Bounds);
        Assert.True(row.Step.Exported);
        Assert.Empty(row.Step.MissingRequiredMarkers);
        Assert.Empty(row.Step.UnexpectedPresentMarkers);
        Assert.Contains("ISO-10303-21", row.Step.PresentMarkers);
        Assert.Contains("MANIFOLD_SOLID_BREP", row.Step.PresentMarkers);
        Assert.Contains("ADVANCED_FACE", row.Step.PresentMarkers);
        Assert.Contains("PLANE", row.Step.PresentMarkers);
        Assert.Contains("CYLINDRICAL_SURFACE", row.Step.AbsentMarkers);
        Assert.Contains("BREP_WITH_VOIDS", row.Step.AbsentMarkers);
        Assert.Contains("edge-prismatic-x1-step-smoke-succeeded", row.Diagnostics);
        Assert.Contains("edge-prismatic-x1-no-air-edge-sweep-used", row.Diagnostics);
        Assert.Contains("edge-prismatic-x1-no-brep-bounded-chamfer-used", row.Diagnostics);
        Assert.Contains("edge-prismatic-x1-no-topology-graft-used", row.Diagnostics);
        Assert.Contains("edge-prismatic-x1-no-3d-boolean-used", row.Diagnostics);
    }

    [Fact]
    public void ThreeSectionStablePlusTransition_EmitsSplitIntervalFacesWithDocumentedTopology()
    {
        var row = PrismaticSectionTransitionEmitterLab.RunAll().Single(x => x.CaseName == "three-section-stable-plus-transition");

        Assert.True(row.Succeeded);
        Assert.Equal(LabProfileStatus.Succeeded, row.Status);
        Assert.True(row.Topology.BodyProduced);
        Assert.Equal(3, row.Topology.SectionCount);
        Assert.Equal(12, row.Topology.VertexCount);
        Assert.Equal(20, row.Topology.EdgeCount);
        Assert.Equal(4, row.Topology.BottomProfileEdgeCount);
        Assert.Equal(4, row.Topology.TopProfileEdgeCount);
        Assert.Equal(8, row.Topology.TransitionEdgeCount);
        Assert.Equal(2, row.Topology.CapFaceCount);
        Assert.Equal(8, row.Topology.TransitionFaceCount);
        Assert.Equal(4, row.Topology.StableIntervalFaceCount);
        Assert.Equal(4, row.Topology.ChangedIntervalFaceCount);
        Assert.Equal(10, row.Topology.FaceCount);
        Assert.Equal(10, row.Topology.PlanarFaceCount);
        Assert.Equal(0, row.Topology.CylindricalFaceCount);
        Assert.Equal(10, row.Topology.LoopCount);
        Assert.Equal(40, row.Topology.CoedgeCount);
        Assert.Equal("[-5,-4,0]..[5,4,6]", row.Topology.Bounds);
        Assert.True(row.Step.Exported);
        Assert.Empty(row.Step.MissingRequiredMarkers);
        Assert.Empty(row.Step.UnexpectedPresentMarkers);
        Assert.Contains("edge-prismatic-x1-transition-faces-created", row.Diagnostics);
        Assert.Contains("edge-prismatic-x1-step-smoke-succeeded", row.Diagnostics);
    }

    [Theory]
    [InlineData("invalid-non-increasing-z", LabProfileStatus.Failed, "edge-prismatic-x1-non-increasing-sections-rejected", "prismatic-section-transition-invalid-rejected")]
    [InlineData("invalid-mismatched-vertex-count", LabProfileStatus.Failed, "edge-prismatic-x1-mismatched-vertex-count-rejected", "prismatic-section-transition-invalid-rejected")]
    [InlineData("invalid-missing-correspondence", LabProfileStatus.Failed, "edge-prismatic-x1-missing-correspondence-rejected", "prismatic-section-transition-invalid-rejected")]
    [InlineData("invalid-self-intersecting-profile", LabProfileStatus.Failed, "edge-prismatic-x1-invalid-profile-rejected", "prismatic-section-transition-invalid-rejected")]
    [InlineData("deferred-holes", LabProfileStatus.Deferred, "edge-prismatic-x1-holes-deferred", "prismatic-section-transition-deferred")]
    [InlineData("deferred-line-arc", LabProfileStatus.Deferred, "edge-prismatic-x1-line-arc-deferred", "prismatic-section-transition-deferred")]
    [InlineData("deferred-multiple-loops", LabProfileStatus.Deferred, "edge-prismatic-x1-multiple-loops-deferred", "prismatic-section-transition-deferred")]
    public void InvalidAndDeferredCases_AreClassifiedDeterministically(string caseName, LabProfileStatus expectedStatus, string diagnostic, string recommendation)
    {
        var row = PrismaticSectionTransitionEmitterLab.RunAll().Single(x => x.CaseName == caseName);

        Assert.False(row.Succeeded);
        Assert.False(row.Topology.BodyProduced);
        Assert.Equal(expectedStatus, row.Status);
        Assert.Contains(diagnostic, row.Diagnostics);
        Assert.Equal(recommendation, row.Recommendation);
    }

    [Fact]
    public void RecommendationVocabulary_IsFiniteAndDeterministic()
    {
        var rows = PrismaticSectionTransitionEmitterLab.RunAll();

        foreach (var row in rows)
        {
            Assert.Contains(row.Recommendation, PrismaticSectionTransitionEmitterLab.AllowedRecommendations);
        }

        Assert.Equal(rows.Select(r => r.Recommendation), PrismaticSectionTransitionEmitterLab.RunAll().Select(r => r.Recommendation));
    }
}
