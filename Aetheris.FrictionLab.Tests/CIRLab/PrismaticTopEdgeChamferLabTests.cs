using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class PrismaticTopEdgeChamferLabTests
{
    private static string Stable(PrismaticTopEdgeChamferRow row) => string.Join("|",
        row.CaseName,
        row.Status,
        row.Succeeded,
        row.PrismaticEmitterInvoked,
        row.Topology.BodyProduced,
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
        row.Step.Exported,
        string.Join(",", row.Step.PresentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", row.Step.MissingRequiredMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", row.Step.AbsentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", row.Step.UnexpectedPresentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", row.Diagnostics.OrderBy(x => x, StringComparer.Ordinal)),
        row.Recommendation);

    [Fact]
    public void PrismaticTopEdgeChamferLab_IsDeterministic()
    {
        var first = PrismaticTopEdgeChamferLab.RunAll();
        var second = PrismaticTopEdgeChamferLab.RunAll();

        Assert.Equal(first.Select(Stable), second.Select(Stable));
    }

    [Fact]
    public void CanonicalTopPosXChamfer_RoutesThroughPrismaticEmitterAndMatchesProfileX2Topology()
    {
        var row = PrismaticTopEdgeChamferLab.RunAll().Single(x => x.CaseName == "canonical-top-pos-x-edge");

        Assert.True(row.Succeeded);
        Assert.Equal(LabProfileStatus.Succeeded, row.Status);
        Assert.True(row.PrismaticEmitterInvoked);
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
        Assert.Contains("edge-prismatic-x2-section-stack-created", row.Diagnostics);
        Assert.Contains("edge-prismatic-x2-correspondence-created", row.Diagnostics);
        Assert.Contains("edge-prismatic-x2-prismatic-emitter-invoked", row.Diagnostics);
        Assert.Contains("edge-prismatic-x2-chamfer-transition-face-classified", row.Diagnostics);
        Assert.Contains("edge-prismatic-x2-body-created", row.Diagnostics);
    }

    [Fact]
    public void CanonicalTopPosXChamfer_StepSmokePassesWithoutCylindersOrVoids()
    {
        var row = PrismaticTopEdgeChamferLab.RunAll().Single(x => x.CaseName == "canonical-top-pos-x-edge");

        Assert.True(row.Step.Exported);
        Assert.Empty(row.Step.MissingRequiredMarkers);
        Assert.Empty(row.Step.UnexpectedPresentMarkers);
        Assert.Contains("ISO-10303-21", row.Step.PresentMarkers);
        Assert.Contains("MANIFOLD_SOLID_BREP", row.Step.PresentMarkers);
        Assert.Contains("ADVANCED_FACE", row.Step.PresentMarkers);
        Assert.Contains("PLANE", row.Step.PresentMarkers);
        Assert.Contains("CYLINDRICAL_SURFACE", row.Step.AbsentMarkers);
        Assert.Contains("BREP_WITH_VOIDS", row.Step.AbsentMarkers);
        Assert.Contains("edge-prismatic-x2-step-smoke-succeeded", row.Diagnostics);
    }

    [Fact]
    public void CanonicalTopPosXChamfer_CandidatePathExcludesTrimGraftEdgeSweepChamferAndBoolean()
    {
        var row = PrismaticTopEdgeChamferLab.RunAll().Single(x => x.CaseName == "canonical-top-pos-x-edge");

        Assert.Contains("edge-prismatic-x2-no-air-edge-sweep-used", row.Diagnostics);
        Assert.Contains("edge-prismatic-x2-no-brep-bounded-chamfer-used", row.Diagnostics);
        Assert.Contains("edge-prismatic-x2-no-topology-graft-used", row.Diagnostics);
        Assert.Contains("edge-prismatic-x2-no-3d-boolean-used", row.Diagnostics);
    }

    [Theory]
    [InlineData("invalid-zero-width", "edge-prismatic-x2-invalid-dimensions-rejected")]
    [InlineData("invalid-negative-depth", "edge-prismatic-x2-invalid-dimensions-rejected")]
    [InlineData("invalid-zero-height", "edge-prismatic-x2-invalid-dimensions-rejected")]
    [InlineData("invalid-non-finite-height", "edge-prismatic-x2-invalid-dimensions-rejected")]
    [InlineData("invalid-zero-chamfer-distance", "edge-prismatic-x2-invalid-chamfer-distance-rejected")]
    [InlineData("invalid-negative-chamfer-distance", "edge-prismatic-x2-invalid-chamfer-distance-rejected")]
    [InlineData("invalid-non-finite-chamfer-distance", "edge-prismatic-x2-invalid-chamfer-distance-rejected")]
    [InlineData("invalid-too-large-chamfer-distance", "edge-prismatic-x2-invalid-chamfer-distance-rejected")]
    public void InvalidCasesRejectBeforeEmission(string caseName, string expectedDiagnostic)
    {
        var row = PrismaticTopEdgeChamferLab.RunAll().Single(x => x.CaseName == caseName);

        Assert.False(row.Succeeded);
        Assert.False(row.Topology.BodyProduced);
        Assert.False(row.PrismaticEmitterInvoked);
        Assert.Contains(expectedDiagnostic, row.Diagnostics);
        Assert.DoesNotContain("edge-prismatic-x2-prismatic-emitter-invoked", row.Diagnostics);
        Assert.Equal("prismatic-top-edge-chamfer-invalid-rejected", row.Recommendation);
    }

    [Fact]
    public void RecommendationsAreFromFiniteVocabulary()
    {
        var rows = PrismaticTopEdgeChamferLab.RunAll();

        foreach (var row in rows)
        {
            Assert.Contains(row.Recommendation, PrismaticTopEdgeChamferLab.AllowedRecommendations);
        }

        Assert.Equal(rows.Select(r => r.Recommendation), PrismaticTopEdgeChamferLab.RunAll().Select(r => r.Recommendation));
    }
}
