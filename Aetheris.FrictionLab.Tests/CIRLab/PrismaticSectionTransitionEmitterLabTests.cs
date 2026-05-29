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

    [Theory]
    [InlineData("scaled-pentagon", 5, 10, 15, 7, 30, "[-4.755,-5,0]..[4.755,4.045,2]")]
    [InlineData("scaled-hexagon", 6, 12, 18, 8, 36, "[-5.196,-6,0]..[5.196,6,2]")]
    [InlineData("asymmetric-translated-pentagon", 5, 10, 15, 7, 30, "[-4,-3.35,0]..[5.75,3.5,2]")]
    public void GenericTwoSectionPolygonCases_ValidateTopologyFormulaAndStepSmoke(
        string caseName,
        int vertexCount,
        int expectedVertices,
        int expectedEdges,
        int expectedFaces,
        int expectedCoedges,
        string expectedBounds)
    {
        var row = PrismaticSectionTransitionEmitterLab.RunAll().Single(x => x.CaseName == caseName);

        Assert.True(row.Succeeded);
        Assert.Equal(LabProfileStatus.Succeeded, row.Status);
        Assert.True(row.Topology.BodyProduced);
        Assert.Equal(2, row.Topology.SectionCount);
        Assert.Equal(expectedVertices, row.Topology.VertexCount);
        Assert.Equal(expectedEdges, row.Topology.EdgeCount);
        Assert.Equal(vertexCount, row.Topology.BottomProfileEdgeCount);
        Assert.Equal(vertexCount, row.Topology.TopProfileEdgeCount);
        Assert.Equal(vertexCount, row.Topology.TransitionEdgeCount);
        Assert.Equal(2, row.Topology.CapFaceCount);
        Assert.Equal(vertexCount, row.Topology.TransitionFaceCount);
        Assert.Equal(0, row.Topology.StableIntervalFaceCount);
        Assert.Equal(vertexCount, row.Topology.ChangedIntervalFaceCount);
        Assert.Equal(expectedFaces, row.Topology.FaceCount);
        Assert.Equal(expectedFaces, row.Topology.PlanarFaceCount);
        Assert.Equal(0, row.Topology.CylindricalFaceCount);
        Assert.Equal(expectedFaces, row.Topology.LoopCount);
        Assert.Equal(expectedCoedges, row.Topology.CoedgeCount);
        Assert.Equal(expectedBounds, row.Topology.Bounds);
        Assert.True(row.Step.Exported);
        Assert.Empty(row.Step.MissingRequiredMarkers);
        Assert.Empty(row.Step.UnexpectedPresentMarkers);
        Assert.Contains("ISO-10303-21", row.Step.PresentMarkers);
        Assert.Contains("MANIFOLD_SOLID_BREP", row.Step.PresentMarkers);
        Assert.Contains("ADVANCED_FACE", row.Step.PresentMarkers);
        Assert.Contains("PLANE", row.Step.PresentMarkers);
        Assert.Contains("CYLINDRICAL_SURFACE", row.Step.AbsentMarkers);
        Assert.Contains("BREP_WITH_VOIDS", row.Step.AbsentMarkers);
        Assert.Contains("edge-prismatic-x3-topology-formula-validated", row.Diagnostics);
        Assert.Equal("prismatic-section-transition-generic-ready-for-production-evaluation", row.Recommendation);
    }

    [Fact]
    public void GenericLabDiagnostics_ConfirmNoLegacyRoutesOrBooleanRoutes()
    {
        var rows = PrismaticSectionTransitionEmitterLab.RunAll().Where(x => x.Succeeded).ToArray();

        Assert.NotEmpty(rows);
        foreach (var row in rows)
        {
            Assert.Contains("edge-prismatic-x3-prismatic-emitter-invoked", row.Diagnostics);
            Assert.Contains("edge-prismatic-x3-section-stack-created", row.Diagnostics);
            Assert.Contains("edge-prismatic-x3-correspondence-created", row.Diagnostics);
            Assert.Contains("edge-prismatic-x3-body-created", row.Diagnostics);
            Assert.Contains("edge-prismatic-x3-step-smoke-succeeded", row.Diagnostics);
            Assert.Contains("edge-prismatic-x3-no-air-edge-sweep-used", row.Diagnostics);
            Assert.Contains("edge-prismatic-x3-no-brep-bounded-chamfer-used", row.Diagnostics);
            Assert.Contains("edge-prismatic-x3-no-topology-graft-used", row.Diagnostics);
            Assert.Contains("edge-prismatic-x3-no-3d-boolean-used", row.Diagnostics);
        }
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

    [Theory]
    [InlineData("invalid-non-increasing-z", "edge-prismatic-x3-non-increasing-sections-rejected")]
    [InlineData("invalid-mismatched-vertex-count", "edge-prismatic-x3-mismatched-vertex-count-rejected")]
    [InlineData("invalid-missing-correspondence", "edge-prismatic-x3-missing-correspondence-rejected")]
    [InlineData("invalid-self-intersecting-profile", "edge-prismatic-x3-invalid-profile-rejected")]
    [InlineData("deferred-holes", "edge-prismatic-x3-holes-deferred")]
    [InlineData("deferred-line-arc", "edge-prismatic-x3-line-arc-deferred")]
    [InlineData("deferred-multiple-loops", "edge-prismatic-x3-multiple-loops-deferred")]
    public void GenericInvalidAndDeferredCases_RejectOrDeferBeforeEmission(string caseName, string diagnostic)
    {
        var row = PrismaticSectionTransitionEmitterLab.RunAll().Single(x => x.CaseName == caseName);

        Assert.False(row.Succeeded);
        Assert.DoesNotContain("edge-prismatic-x3-prismatic-emitter-invoked", row.Diagnostics);
        Assert.Contains(diagnostic, row.Diagnostics);
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
