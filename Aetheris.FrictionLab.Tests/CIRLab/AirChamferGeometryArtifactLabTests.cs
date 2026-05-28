using Aetheris.Firmament.FrictionLab.CIRLab;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class AirChamferGeometryArtifactLabTests
{
    [Fact]
    public void RunAll_IsDeterministicAcrossRepeatedRuns()
    {
        var a = AirChamferGeometryArtifactLab.RunAll();
        var b = AirChamferGeometryArtifactLab.RunAll();
        Assert.Equal(a.Select(r => $"{r.CaseName}|{r.Decision}|{r.Recommendation}|{r.ArtifactProduced}|{string.Join(",", r.Diagnostics)}"), b.Select(r => $"{r.CaseName}|{r.Decision}|{r.Recommendation}|{r.ArtifactProduced}|{string.Join(",", r.Diagnostics)}"));
    }

    [Fact]
    public void Diagnostics_IncludeJudgmentEngineUsage()
    {
        var row = AirChamferGeometryArtifactLab.Evaluate(AirChamferGeometryArtifactLab.Cases().Single(x => x.CaseName == "canonical-orthogonal-convex-planar-single-edge"));
        Assert.Contains("edge-x5-judgment-engine-used", row.Diagnostics);
    }

    [Fact]
    public void CanonicalConvexPlanar_CreatesExpectedArtifact()
    {
        var result = AirChamferGeometryArtifactLab.Evaluate(AirChamferGeometryArtifactLab.Cases().Single(x => x.CaseName == "canonical-orthogonal-convex-planar-single-edge"));
        var artifact = Assert.IsType<AirChamferGeometryArtifact>(result.Artifact);

        Assert.Equal(3, artifact.FaceCount);
        Assert.Equal(3, artifact.PlanarFaceCount);
        Assert.Equal(1, artifact.ChamferFaceCount);
        Assert.Equal(2, artifact.AffectedAdjacentFaceCount);
        Assert.Equal(2, artifact.OffsetCurveCount);
        Assert.Equal(2, artifact.TransitionEdgeCount);
        Assert.Equal(0, artifact.CornerPatchCount);
        Assert.True(artifact.CornerPatchesDeferred);
        Assert.True(artifact.OriginalEdgeMarkedForReplacement);

        Assert.True(artifact.TrimmedFacePatchA.Area > 0d);
        Assert.True(artifact.TrimmedFacePatchB.Area > 0d);
        Assert.True(artifact.ChamferFace.Area > 0d);
        Assert.True(float.IsFinite(artifact.TrimmedFacePatchA.Normal.X));
        Assert.True(float.IsFinite(artifact.TrimmedFacePatchB.Normal.X));
        Assert.True(float.IsFinite(artifact.ChamferFace.Normal.X));

        var offsetA = artifact.OffsetCurves.Single(x => x.Name == "offset-curve-a");
        var offsetB = artifact.OffsetCurves.Single(x => x.Name == "offset-curve-b");
        var transitionStart = artifact.TransitionEdges.Single(x => x.Name == "transition-edge-start");
        var transitionEnd = artifact.TransitionEdges.Single(x => x.Name == "transition-edge-end");

        Assert.Equal(offsetA.Start, transitionStart.Start);
        Assert.Equal(offsetB.Start, transitionStart.End);
        Assert.Equal(offsetA.End, transitionEnd.Start);
        Assert.Equal(offsetB.End, transitionEnd.End);

        Assert.Contains("edge-x5-no-3d-boolean-used", result.Diagnostics);
        Assert.Contains("edge-x5-step-smoke-deferred:open-local-artifact-export-unsupported", result.Diagnostics);
    }

    [Theory]
    [InlineData("convex-planar-unsafe-envelope")]
    [InlineData("invalid-distance")]
    [InlineData("invalid-edge")]
    [InlineData("invalid-face-adjacency")]
    [InlineData("ambiguous-classification")]
    [InlineData("edge-chain")]
    [InlineData("corner-chain")]
    [InlineData("triangle-legacy-dependent")]
    public void InvalidDeferredOrLegacyCases_StopBeforeArtifact(string caseName)
    {
        var result = AirChamferGeometryArtifactLab.Evaluate(AirChamferGeometryArtifactLab.Cases().Single(x => x.CaseName == caseName));
        Assert.Null(result.Artifact);
    }
}
