using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242LoopRoleNormalizationRegressionTests
{
    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp", "Importer.LoopRole.InnerBoundaryIntersectionWithOutsideVerticesAfterNormalization", "Inner loop could not be normalized")]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_05_asme1_ap242-e1.stp", "Entity:1234", "FACE_BOUND loop type 'VERTEX_LOOP' is unsupported")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp", "Importer.LoopRole.InnerBoundaryIntersectionWithOutsideVerticesAfterNormalization", "Inner loop could not be normalized")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp", "Importer.LoopRole.InnerBoundaryIntersectionWithOutsideVerticesAfterNormalization", "Inner loop could not be normalized")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp", "Importer.LoopRole.CylinderNonNormalizableDegenerateProjection", "Cylinder loop normalization failed")]
    [InlineData("testdata/step242/nist/STC/nist_stc_06_asme1_ap242-e3.stp", null, null)]
    [InlineData("testdata/step242/nist/STC/nist_stc_09_asme1_ap242-e3.stp", null, null)]
    public void Step242_NistLoopRoleTargets_ProgressDeterministically_WithExplicitNormalizedBoundaries(
        string relativePath,
        string? expectedSource,
        string? expectedMessagePrefix)
    {
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-loop-role-regression",
            Notes: "regression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        if (expectedSource is null)
        {
            Assert.DoesNotContain("Loop projection is degenerate.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
            Assert.DoesNotContain("Inner loop is not contained by outer loop.", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
            Assert.DoesNotContain("Importer.LoopRole.DegenerateLoop", first.FirstDiagnostic.Source, StringComparison.Ordinal);
            Assert.DoesNotContain("Importer.LoopRole.InnerNotContained", first.FirstDiagnostic.Source, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(expectedSource, first.FirstDiagnostic.Source);
            Assert.StartsWith(expectedMessagePrefix!, first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        }

        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
    }

    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp", true)]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp", true)]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp", false)]
    public void Step242_PlanarLoopRole_BSplineSampling_IsAdaptiveAndDeterministic(string relativePath, bool expectBsplineSampling)
    {
        var first = CaptureBsplineSampling(relativePath);
        var second = CaptureBsplineSampling(relativePath);

        Assert.Equal(first, second);

        if (!expectBsplineSampling)
        {
            Assert.Empty(first);
            return;
        }

        Assert.NotEmpty(first);

        Assert.All(first, s =>
        {
            Assert.True(s.AdaptivePointCount >= s.LegacyPointCount);
            Assert.True(s.AdaptivePointCount >= 16);
            var expected = System.Math.Max(16, (int)System.Math.Ceiling(System.Math.Abs(s.TrimSpan) * 8d) + 1);
            Assert.Equal(expected, s.AdaptivePointCount);
        });

        Assert.Contains(first, s => s.AdaptivePointCount > s.LegacyPointCount);
    }

    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp")]
    public void Step242_PlanarLoopRole_CoedgeGapDiagnostics_AreDeterministic(string relativePath)
    {
        var first = CaptureCoedgeGapDiagnostics(relativePath);
        var second = CaptureCoedgeGapDiagnostics(relativePath);

        Assert.Equal(first, second);
        Assert.All(first, d =>
        {
            Assert.True(d.GapLength > d.Threshold);
            Assert.True(d.Threshold > 0d);
        });
    }

    private static IReadOnlyList<Step242Importer.LoopRoleBsplineSamplingDiagnostic> CaptureBsplineSampling(string relativePath)
    {
        var absolutePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(absolutePath);
        var diagnostics = new List<Step242Importer.LoopRoleBsplineSamplingDiagnostic>();

        using var captureScope = Step242Importer.CaptureLoopRoleBsplineSamplingDiagnostics(diagnostics);
        Step242Importer.ImportBody(text);
        return diagnostics;
    }

    private static IReadOnlyList<Step242Importer.LoopRoleCoedgeGapDiagnostic> CaptureCoedgeGapDiagnostics(string relativePath)
    {
        var absolutePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(absolutePath);
        var diagnostics = new List<Step242Importer.LoopRoleCoedgeGapDiagnostic>();

        using var captureScope = Step242Importer.CaptureLoopRoleCoedgeGapDiagnostics(diagnostics);
        Step242Importer.ImportBody(text);
        return diagnostics;
    }
}
