using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class DisplayPreparationFallbackBuilderTests
{
    [Fact]
    public void Build_SingleFaceBsplineBody_UsesAcceptedScaffoldPatch()
    {
        var body = UvTrimMaskExtractorTests.ImportSingleLoopBsplineBody();

        var result = DisplayPreparationFallbackBuilder.Build(body);

        Assert.True(result.IsSuccess);
        var patch = Assert.Single(result.Value.FacePatches);
        Assert.Equal(DisplayFaceMeshSource.BsplineUvScaffold, patch.Source);
        Assert.Null(patch.ScaffoldRejectionReason);
    }

    [Fact]
    public void Build_StrictScaffoldThreshold_RejectedAndFallsBackToTessellator()
    {
        var body = UvTrimMaskExtractorTests.ImportSingleLoopBsplineBody();

        var result = DisplayPreparationFallbackBuilder.Build(
            body,
            tessellationOptions: null,
            scaffoldOptions: new DisplayPreparationBsplineScaffoldOptions(
                USegments: 12,
                VSegments: 12,
                Acceptance: new BsplineUvGridScaffoldAcceptanceThresholds(
                    MaxTriangleDensityRatioVsFallback: 0.01d)));

        Assert.True(result.IsSuccess);
        var patch = Assert.Single(result.Value.FacePatches);
        Assert.Equal(DisplayFaceMeshSource.Tessellator, patch.Source);
        Assert.Equal(BsplineUvGridScaffoldRejectionReason.TooDenseVsFallback.ToString(), patch.ScaffoldRejectionReason);
    }

    [Fact]
    public void Build_UnsupportedBody_StaysOnExistingFallbackWithoutScaffoldAttempt()
    {
        var body = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;

        var result = DisplayPreparationFallbackBuilder.Build(body);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value.FacePatches, patch =>
        {
            Assert.Equal(DisplayFaceMeshSource.Tessellator, patch.Source);
            Assert.Null(patch.ScaffoldRejectionReason);
        });
    }

    [Fact]
    public void Build_UnsupportedTrimExtraction_FallsBackDeterministically()
    {
        var body = UvTrimMaskExtractorTests.CreateBodyWithMissingVertexPoint(UvTrimMaskExtractorTests.ImportSingleLoopBsplineBody());

        var first = DisplayPreparationFallbackBuilder.Build(body);
        var second = DisplayPreparationFallbackBuilder.Build(body);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var firstPatch = Assert.Single(first.Value.FacePatches);
        var secondPatch = Assert.Single(second.Value.FacePatches);

        Assert.Equal(DisplayFaceMeshSource.Tessellator, firstPatch.Source);
        Assert.Equal("TrimMaskExtraction:MissingVertexPoint", firstPatch.ScaffoldRejectionReason);
        Assert.Equal(firstPatch.Source, secondPatch.Source);
        Assert.Equal(firstPatch.ScaffoldRejectionReason, secondPatch.ScaffoldRejectionReason);
        Assert.Equal(CreateSignature(first.Value), CreateSignature(second.Value));
    }

    [Fact]
    public void Build_AcceptedScaffoldCase_IsDeterministic()
    {
        var body = UvTrimMaskExtractorTests.ImportSingleLoopBsplineBody();

        var first = DisplayPreparationFallbackBuilder.Build(body);
        var second = DisplayPreparationFallbackBuilder.Build(body);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(CreateSignature(first.Value), CreateSignature(second.Value));
    }

    private static string CreateSignature(DisplayTessellationResult result)
        => string.Join(
            "|",
            result.FacePatches.Select(patch =>
                $"{patch.FaceId.Value}:{patch.Source}:{patch.ScaffoldRejectionReason}:{patch.Positions.Count}:{patch.TriangleIndices.Count}:{string.Join(',', patch.TriangleIndices.Take(24))}"));
}
