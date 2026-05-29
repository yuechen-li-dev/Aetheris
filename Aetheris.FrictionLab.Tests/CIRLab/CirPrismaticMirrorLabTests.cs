using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class CirPrismaticMirrorLabTests
{
    private static string Stable(CirPrismaticMirrorResult result) => result.StableProjection();

    [Theory]
    [InlineData("rectangle-inset", CirPrismaticMirrorStrategy.HalfSpaceConvexPolyhedron)]
    [InlineData("rectangle-inset", CirPrismaticMirrorStrategy.SectionStackImplicit)]
    public void CirPrismaticX1_RectangleInsetMirrorFeasibility_ClassifiesPointsAndCreatesStableMap(
        string caseName,
        CirPrismaticMirrorStrategy strategy)
    {
        var result = CirPrismaticMirrorLab.RunRequiredCases().Single(x => x.CaseName == caseName && x.Strategy == strategy);

        Assert.True(result.Succeeded);
        Assert.Equal("mirror-admitted-exact", result.MirrorStatus);
        Assert.Contains("point-containment", result.Capabilities);
        Assert.Contains("map-occupancy", result.Capabilities);
        Assert.Contains("face-identity-lost", result.KnownLosses);
        Assert.Contains("topology-parity-unavailable", result.KnownLosses);
        Assert.All(result.PointClassifications, p => Assert.True(p.Matched, $"{p.Name} expected {p.ExpectedInside} but was {p.ActualInside} ({p.SignedDistance})"));
        Assert.Contains(result.PointClassifications, p => p.Name == "center-mid-height" && p.ActualInside);
        Assert.Contains(result.PointClassifications, p => p.Name == "outside-far-pos-x" && !p.ActualInside);
        Assert.Contains(result.PointClassifications, p => p.Name == "outside-far-pos-y" && !p.ActualInside);
        Assert.Contains(result.PointClassifications, p => p.Name == "lower-full-rectangle-only" && p.ActualInside);
        Assert.Contains(result.PointClassifications, p => p.Name == "upper-inset-excluded" && !p.ActualInside);
        Assert.NotNull(result.MapSummary);
        Assert.Equal(16, result.MapSummary!.Rows);
        Assert.Equal(16, result.MapSummary.Cols);
        Assert.Equal(256, result.MapSummary.TotalSamples);
        Assert.Equal("[-5,-4,0]..[5,4,1]", result.MapSummary.Bounds);
        Assert.Equal(256, result.MapSummary.HitSamples);
        Assert.Equal(0, result.MapSummary.EmptySamples);
        Assert.Equal(0.250, Math.Round(result.MapSummary.ThicknessMin!.Value, 3));
        Assert.Equal(1.000, Math.Round(result.MapSummary.ThicknessMax!.Value, 3));
        Assert.Equal(strategy == CirPrismaticMirrorStrategy.HalfSpaceConvexPolyhedron
            ? "cir-prismatic-mirror-use-convex-polyhedron-first"
            : "cir-prismatic-mirror-needs-section-stack-evaluator", result.Recommendation);
        Assert.Contains("cir-prismatic-x1-mirror-admitted-exact:rectangle-inset", result.Diagnostics);
        Assert.Contains("cir-prismatic-x1-point-classification-succeeded:rectangle-inset", result.Diagnostics);
        Assert.Contains("cir-prismatic-x1-map-summary-created:rectangle-inset", result.Diagnostics);
        Assert.Contains("cir-prismatic-x1-no-production-analyzer-behavior-changed", result.Diagnostics);
        Assert.Contains("cir-prismatic-x1-no-cir-to-brep-extraction", result.Diagnostics);
    }

    [Theory]
    [InlineData(CirPrismaticMirrorStrategy.HalfSpaceConvexPolyhedron)]
    [InlineData(CirPrismaticMirrorStrategy.SectionStackImplicit)]
    public void CirPrismaticX1_TopEdgeChamferMirrorFeasibility_ClassifiesPointsAndCreatesStableMap(CirPrismaticMirrorStrategy strategy)
    {
        var result = CirPrismaticMirrorLab.RunRequiredCases().Single(x => x.CaseName == "top-edge-chamfer" && x.Strategy == strategy);

        Assert.True(result.Succeeded);
        Assert.Equal("mirror-admitted-exact", result.MirrorStatus);
        Assert.All(result.PointClassifications, p => Assert.True(p.Matched, $"{p.Name} expected {p.ExpectedInside} but was {p.ActualInside} ({p.SignedDistance})"));
        Assert.Contains(result.PointClassifications, p => p.Name == "lower-body-below-transition" && p.ActualInside);
        Assert.Contains(result.PointClassifications, p => p.Name == "above-inset-excluded-pos-x" && !p.ActualInside);
        Assert.Contains(result.PointClassifications, p => p.Name == "below-chamfer-plane" && p.ActualInside);
        Assert.Contains(result.PointClassifications, p => p.Name == "beyond-chamfer-plane" && !p.ActualInside);
        Assert.Contains(result.PointClassifications, p => p.Name == "center-inside" && p.ActualInside);
        Assert.NotNull(result.MapSummary);
        Assert.Equal(256, result.MapSummary!.TotalSamples);
        Assert.Equal("[-5,-4,0]..[5,4,6]", result.MapSummary.Bounds);
        Assert.Equal(256, result.MapSummary.HitSamples);
        Assert.Equal(0, result.MapSummary.EmptySamples);
        Assert.Equal(5.312, Math.Round(result.MapSummary.ThicknessMin!.Value, 3));
        Assert.Equal(6.000, Math.Round(result.MapSummary.ThicknessMax!.Value, 3));
        Assert.Contains("cir-prismatic-x1-mirror-admitted-exact:top-edge-chamfer", result.Diagnostics);
        Assert.Contains("cir-prismatic-x1-map-summary-created:top-edge-chamfer", result.Diagnostics);
    }

    [Theory]
    [InlineData(CirPrismaticMirrorRequestKind.FaceIdentity)]
    [InlineData(CirPrismaticMirrorRequestKind.TopologyParity)]
    public void CirPrismaticX1_LossyRequestsRejectWithMetadataVocabulary(CirPrismaticMirrorRequestKind requestKind)
    {
        var result = CirPrismaticMirrorLab.Evaluate(
            "rectangle-inset",
            PrismaticSectionTransitionEmitterLab.RectangleToInsetRectangle(),
            CirPrismaticMirrorStrategy.HalfSpaceConvexPolyhedron,
            requestKind);

        Assert.False(result.Succeeded);
        Assert.Equal("mirror-rejected-lossy-for-request", result.MirrorStatus);
        Assert.Equal("none", result.Capabilities);
        Assert.Contains("face-identity-lost", result.KnownLosses);
        Assert.Contains("topology-parity-unavailable", result.KnownLosses);
        Assert.Contains("cir-prismatic-x1-mirror-rejected-lossy-for-request", result.Diagnostics);
        Assert.Contains("cir-prismatic-x1-loss-face-identity", result.Diagnostics);
        Assert.Contains("cir-prismatic-x1-loss-topology-parity", result.Diagnostics);
        Assert.Equal("cir-prismatic-mirror-invalid-rejected", result.Recommendation);
    }

    [Fact]
    public void CirPrismaticX1_RepeatedMirrorEvaluationsAreDeterministic()
    {
        var first = CirPrismaticMirrorLab.RunRequiredCases().Select(Stable).ToArray();
        var second = CirPrismaticMirrorLab.RunRequiredCases().Select(Stable).ToArray();

        Assert.Equal(first, second);
    }
}
