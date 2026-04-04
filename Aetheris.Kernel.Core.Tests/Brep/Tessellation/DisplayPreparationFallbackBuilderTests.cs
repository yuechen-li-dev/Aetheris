using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class DisplayPreparationFallbackBuilderTests
{
    private static readonly DisplayPreparationSweepCase[] BoundedRealCaseSweepCases =
    [
        new("single-loop-accepted", UvTrimMaskExtractorTests.ImportSingleLoopBsplineBody),
        new("two-loop-hole-accepted", UvTrimMaskExtractorTests.ImportBsplineBodyWithHole),
        new("missing-vertex-rejected", () => UvTrimMaskExtractorTests.CreateBodyWithMissingVertexPoint(UvTrimMaskExtractorTests.ImportSingleLoopBsplineBody())),
    ];

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
    public void Build_SingleFaceBsplineBodyWithHole_UsesAcceptedScaffoldPatch()
    {
        var body = UvTrimMaskExtractorTests.ImportBsplineBodyWithHole();

        var first = DisplayPreparationFallbackBuilder.Build(body);
        var second = DisplayPreparationFallbackBuilder.Build(body);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var firstPatch = Assert.Single(first.Value.FacePatches);
        var secondPatch = Assert.Single(second.Value.FacePatches);
        Assert.Equal(DisplayFaceMeshSource.BsplineUvScaffold, firstPatch.Source);
        Assert.Null(firstPatch.ScaffoldRejectionReason);
        Assert.Equal(CreateSignature(first.Value), CreateSignature(second.Value));
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

    [Fact]
    public void Build_BoundedRealCaseSweep_ReportsDeterministicAcceptedRejectedAndReasons()
    {
        var first = RunBoundedRealCaseSweep();
        var second = RunBoundedRealCaseSweep();

        Assert.Equal(CreateSweepSignature(first), CreateSweepSignature(second));
        Assert.Equal(3, first.TotalCases);
        Assert.Equal(2, first.AcceptedCases);
        Assert.Equal(1, first.RejectedCases);
        Assert.Equal(2, first.SourceHistogram[DisplayFaceMeshSource.BsplineUvScaffold]);
        Assert.Equal(1, first.SourceHistogram[DisplayFaceMeshSource.Tessellator]);
        Assert.Equal(1, first.RejectionReasonHistogram["TrimMaskExtraction:MissingVertexPoint"]);
        Assert.Contains(first.Cases, c => c.Name == "two-loop-hole-accepted" && c.Accepted && c.Source == DisplayFaceMeshSource.BsplineUvScaffold);
        Assert.Contains(first.Cases, c => c.Name == "missing-vertex-rejected" && !c.Accepted && c.RejectionReason == "TrimMaskExtraction:MissingVertexPoint");
    }

    private static BoundedRealCaseSweepSummary RunBoundedRealCaseSweep()
    {
        var caseResults = new List<BoundedRealCaseSweepCaseResult>(BoundedRealCaseSweepCases.Length);
        foreach (var sweepCase in BoundedRealCaseSweepCases)
        {
            var body = sweepCase.CreateBody();
            var result = DisplayPreparationFallbackBuilder.Build(body);
            Assert.True(result.IsSuccess);
            var patch = Assert.Single(result.Value.FacePatches);
            caseResults.Add(new BoundedRealCaseSweepCaseResult(
                sweepCase.Name,
                Attempted: patch.Source == DisplayFaceMeshSource.BsplineUvScaffold || patch.ScaffoldRejectionReason is not null,
                Accepted: patch.Source == DisplayFaceMeshSource.BsplineUvScaffold,
                patch.ScaffoldRejectionReason,
                patch.Source));
        }

        var sourceHistogram = caseResults
            .GroupBy(c => c.Source)
            .ToDictionary(group => group.Key, group => group.Count());
        var rejectionHistogram = caseResults
            .Where(c => !string.IsNullOrWhiteSpace(c.RejectionReason))
            .GroupBy(c => c.RejectionReason!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return new BoundedRealCaseSweepSummary(
            caseResults.Count,
            caseResults.Count(c => c.Accepted),
            caseResults.Count(c => !c.Accepted),
            sourceHistogram,
            rejectionHistogram,
            caseResults);
    }

    private static string CreateSweepSignature(BoundedRealCaseSweepSummary summary)
    {
        var sources = string.Join(",", summary.SourceHistogram.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
        var reasons = string.Join(",", summary.RejectionReasonHistogram.OrderBy(kvp => kvp.Key, StringComparer.Ordinal).Select(kvp => $"{kvp.Key}:{kvp.Value}"));
        var cases = string.Join("|", summary.Cases.Select(c => $"{c.Name}:{c.Attempted}:{c.Accepted}:{c.Source}:{c.RejectionReason}"));
        return $"{summary.TotalCases}:{summary.AcceptedCases}:{summary.RejectedCases}:{sources}:{reasons}:{cases}";
    }

    private static string CreateSignature(DisplayTessellationResult result)
        => string.Join(
            "|",
            result.FacePatches.Select(patch =>
                $"{patch.FaceId.Value}:{patch.Source}:{patch.ScaffoldRejectionReason}:{patch.Positions.Count}:{patch.TriangleIndices.Count}:{string.Join(',', patch.TriangleIndices.Take(24))}"));
}

internal sealed record DisplayPreparationSweepCase(
    string Name,
    Func<BrepBody> CreateBody);

internal sealed record BoundedRealCaseSweepCaseResult(
    string Name,
    bool Attempted,
    bool Accepted,
    string? RejectionReason,
    DisplayFaceMeshSource Source);

internal sealed record BoundedRealCaseSweepSummary(
    int TotalCases,
    int AcceptedCases,
    int RejectedCases,
    IReadOnlyDictionary<DisplayFaceMeshSource, int> SourceHistogram,
    IReadOnlyDictionary<string, int> RejectionReasonHistogram,
    IReadOnlyList<BoundedRealCaseSweepCaseResult> Cases);
