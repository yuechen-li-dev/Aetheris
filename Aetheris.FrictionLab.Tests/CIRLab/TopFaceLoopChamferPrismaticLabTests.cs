using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class TopFaceLoopChamferPrismaticLabTests
{
    private static string Stable(FaceLoopChamferRow row) => string.Join("|",
        row.CaseName,
        row.Status,
        row.Succeeded,
        row.PrismaticEmitterInvoked,
        row.Topology.BodyProduced,
        row.Topology.SectionCount,
        row.Topology.VertexCount,
        row.Topology.EdgeCount,
        row.Topology.FaceCount,
        row.Topology.PlanarFaceCount,
        row.Topology.CylindricalFaceCount,
        row.Topology.CapFaceCount,
        row.Topology.LowerPrismSideFaceCount,
        row.Topology.TransitionFaceCount,
        row.Topology.ChamferTransitionFaceCount,
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
    public void TopFaceLoopChamferPrismaticLab_IsDeterministic()
    {
        var first = TopFaceLoopChamferPrismaticLab.RunAll();
        var second = TopFaceLoopChamferPrismaticLab.RunAll();

        Assert.Equal(first.Select(Stable), second.Select(Stable));
    }

    [Fact]
    public void CanonicalTopFaceOuterLoopChamfer_SucceedsAsClassBLoopRoute()
    {
        var row = TopFaceLoopChamferPrismaticLab.RunAll().Single(x => x.CaseName == "canonical-top-face-outer-loop");

        Assert.True(row.Succeeded);
        Assert.Equal(LabProfileStatus.Succeeded, row.Status);
        Assert.True(row.PrismaticEmitterInvoked);
        Assert.Contains("edge-loop-x1-class-b-loop-route", row.Diagnostics);
        Assert.Contains("edge-loop-x1-not-four-independent-single-edge-chamfers", row.Diagnostics);
        Assert.Contains("edge-loop-x1-loop-selection-created", row.Diagnostics);
        Assert.Contains("edge-loop-x1-owning-face-top-cap", row.Diagnostics);
        Assert.Contains("edge-loop-x1-loop-kind-outer", row.Diagnostics);
        Assert.Contains("edge-loop-x1-loop-closed", row.Diagnostics);
        Assert.Contains("edge-loop-x1-loop-edge-count:4", row.Diagnostics);
        Assert.Contains("edge-loop-x1-uniform-chamfer-rule-validated", row.Diagnostics);
        Assert.Contains("edge-loop-x1-prismatic-emitter-invoked", row.Diagnostics);
    }

    [Theory]
    [InlineData("canonical-top-face-outer-loop", "[-5,-4,0]..[5,4,6]")]
    [InlineData("larger-valid-top-face-outer-loop", "[-5,-4,0]..[5,4,6]")]
    [InlineData("non-square-valid-top-face-outer-loop", "[-6,-2.5,0]..[6,2.5,7]")]
    public void ValidLoopChamfers_MatchSplitPreservingTopology(string caseName, string bounds)
    {
        var row = TopFaceLoopChamferPrismaticLab.RunAll().Single(x => x.CaseName == caseName);

        Assert.True(row.Succeeded);
        Assert.True(row.Topology.BodyProduced);
        Assert.Equal(3, row.Topology.SectionCount);
        Assert.Equal(12, row.Topology.VertexCount);
        Assert.Equal(20, row.Topology.EdgeCount);
        Assert.Equal(10, row.Topology.FaceCount);
        Assert.Equal(10, row.Topology.PlanarFaceCount);
        Assert.Equal(0, row.Topology.CylindricalFaceCount);
        Assert.Equal(2, row.Topology.CapFaceCount);
        Assert.Equal(4, row.Topology.LowerPrismSideFaceCount);
        Assert.Equal(4, row.Topology.TransitionFaceCount);
        Assert.Equal(4, row.Topology.ChamferTransitionFaceCount);
        Assert.Equal(10, row.Topology.LoopCount);
        Assert.Equal(40, row.Topology.CoedgeCount);
        Assert.Equal(bounds, row.Topology.Bounds);
        Assert.Contains("edge-loop-x1-topology-validated", row.Diagnostics);
        Assert.Contains("edge-loop-x1-split-preserving-topology", row.Diagnostics);
    }

    [Fact]
    public void CanonicalTopFaceOuterLoopChamfer_StepSmokePasses()
    {
        var row = TopFaceLoopChamferPrismaticLab.RunAll().Single(x => x.CaseName == "canonical-top-face-outer-loop");

        Assert.True(row.Step.Exported);
        Assert.Empty(row.Step.MissingRequiredMarkers);
        Assert.Empty(row.Step.UnexpectedPresentMarkers);
        Assert.Contains("ISO-10303-21", row.Step.PresentMarkers);
        Assert.Contains("MANIFOLD_SOLID_BREP", row.Step.PresentMarkers);
        Assert.Contains("ADVANCED_FACE", row.Step.PresentMarkers);
        Assert.Contains("PLANE", row.Step.PresentMarkers);
        Assert.Contains("CYLINDRICAL_SURFACE", row.Step.AbsentMarkers);
        Assert.Contains("BREP_WITH_VOIDS", row.Step.AbsentMarkers);
        Assert.Contains("edge-loop-x1-step-smoke-succeeded", row.Diagnostics);
    }

    [Fact]
    public void CanonicalTopFaceOuterLoopChamfer_ExcludesLegacyAndMutationRoutes()
    {
        var row = TopFaceLoopChamferPrismaticLab.RunAll().Single(x => x.CaseName == "canonical-top-face-outer-loop");

        Assert.Contains("edge-loop-x1-no-air-edge-sweep-used", row.Diagnostics);
        Assert.Contains("edge-loop-x1-no-brep-bounded-chamfer-used", row.Diagnostics);
        Assert.Contains("edge-loop-x1-no-topology-graft-used", row.Diagnostics);
        Assert.Contains("edge-loop-x1-no-3d-boolean-used", row.Diagnostics);
        Assert.Contains("edge-loop-x1-no-coplanar-merge-used", row.Diagnostics);
        Assert.Contains("edge-loop-x1-no-production-route-replacement", row.Diagnostics);
    }

    [Theory]
    [InlineData("invalid-zero-width", "edge-loop-x1-invalid-dimensions-rejected", "face-loop-chamfer-invalid-rejected", LabProfileStatus.Failed)]
    [InlineData("invalid-negative-depth", "edge-loop-x1-invalid-dimensions-rejected", "face-loop-chamfer-invalid-rejected", LabProfileStatus.Failed)]
    [InlineData("invalid-zero-height", "edge-loop-x1-invalid-dimensions-rejected", "face-loop-chamfer-invalid-rejected", LabProfileStatus.Failed)]
    [InlineData("invalid-non-finite-width", "edge-loop-x1-invalid-dimensions-rejected", "face-loop-chamfer-invalid-rejected", LabProfileStatus.Failed)]
    [InlineData("invalid-zero-chamfer-distance", "edge-loop-x1-invalid-chamfer-distance-rejected", "face-loop-chamfer-invalid-rejected", LabProfileStatus.Failed)]
    [InlineData("invalid-negative-chamfer-distance", "edge-loop-x1-invalid-chamfer-distance-rejected", "face-loop-chamfer-invalid-rejected", LabProfileStatus.Failed)]
    [InlineData("invalid-non-finite-chamfer-distance", "edge-loop-x1-invalid-chamfer-distance-rejected", "face-loop-chamfer-invalid-rejected", LabProfileStatus.Failed)]
    [InlineData("invalid-too-large-chamfer-distance", "edge-loop-x1-chamfer-distance-too-large-rejected", "face-loop-chamfer-invalid-rejected", LabProfileStatus.Failed)]
    [InlineData("invalid-non-closed-loop", "edge-loop-x1-non-closed-loop-rejected", "face-loop-chamfer-invalid-rejected", LabProfileStatus.Failed)]
    [InlineData("deferred-inner-loop", "edge-loop-x1-non-outer-loop-deferred", "face-loop-chamfer-deferred", LabProfileStatus.Deferred)]
    [InlineData("deferred-open-chain", "edge-loop-x1-open-chain-deferred", "face-loop-chamfer-deferred", LabProfileStatus.Deferred)]
    [InlineData("rejected-arbitrary-graph", "edge-loop-x1-arbitrary-graph-rejected", "face-loop-chamfer-invalid-rejected", LabProfileStatus.Failed)]
    [InlineData("rejected-non-uniform-rule", "edge-loop-x1-non-uniform-rule-rejected", "face-loop-chamfer-invalid-rejected", LabProfileStatus.Failed)]
    [InlineData("deferred-non-planar-owning-face", "edge-loop-x1-non-planar-owning-face-deferred", "face-loop-chamfer-deferred", LabProfileStatus.Deferred)]
    public void InvalidRejectedAndDeferredCases_ClassifyBeforeEmission(string caseName, string diagnostic, string recommendation, LabProfileStatus status)
    {
        var row = TopFaceLoopChamferPrismaticLab.RunAll().Single(x => x.CaseName == caseName);

        Assert.False(row.Succeeded);
        Assert.False(row.Topology.BodyProduced);
        Assert.False(row.PrismaticEmitterInvoked);
        Assert.Equal(status, row.Status);
        Assert.Contains(diagnostic, row.Diagnostics);
        Assert.DoesNotContain("edge-loop-x1-prismatic-emitter-invoked", row.Diagnostics);
        Assert.Equal(recommendation, row.Recommendation);
    }

    [Fact]
    public void RecommendationVocabulary_IsFiniteAndDeterministic()
    {
        var rows = TopFaceLoopChamferPrismaticLab.RunAll();

        foreach (var row in rows)
        {
            Assert.Contains(row.Recommendation, TopFaceLoopChamferPrismaticLab.AllowedRecommendations);
        }

        Assert.Equal(rows.Select(r => r.Recommendation), TopFaceLoopChamferPrismaticLab.RunAll().Select(r => r.Recommendation));
    }
}
