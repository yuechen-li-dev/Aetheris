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
    [InlineData("testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp")]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_05_asme1_ap242-e1.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp")]
    public void Step242_PlanarLoopRole_CircularSampling_IsAdaptiveAndDeterministic(string relativePath)
    {
        var first = CaptureCircularSampling(relativePath);
        var second = CaptureCircularSampling(relativePath);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);

        Assert.All(first, s =>
        {
            Assert.True(s.AdaptivePointCount >= s.LegacyPointCount);
            var expected = System.Math.Max(2, (int)System.Math.Ceiling(System.Math.Abs(s.TrimSpan) / (System.Math.PI / 4d))) + 1;
            Assert.Equal(expected, s.AdaptivePointCount);
        });

        Assert.Contains(first, s => s.AdaptivePointCount > s.LegacyPointCount);
    }


    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp")]
    public void Step242_PlanarLoopRole_AuditDiagnostics_ReportProjectionAndContainmentEvidence(string relativePath)
    {
        var first = CapturePlanarAudit(relativePath);
        var second = CapturePlanarAudit(relativePath);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);

        Assert.All(first, diagnostic =>
        {
            Assert.NotEqual(0, diagnostic.FaceEntityId);
            Assert.NotEqual(0d, System.Math.Sqrt((diagnostic.PlaneNormalX * diagnostic.PlaneNormalX) + (diagnostic.PlaneNormalY * diagnostic.PlaneNormalY) + (diagnostic.PlaneNormalZ * diagnostic.PlaneNormalZ)));
            Assert.NotEqual(0d, System.Math.Sqrt((diagnostic.PlaneUAxisX * diagnostic.PlaneUAxisX) + (diagnostic.PlaneUAxisY * diagnostic.PlaneUAxisY) + (diagnostic.PlaneUAxisZ * diagnostic.PlaneUAxisZ)));
            Assert.NotEqual(0d, System.Math.Sqrt((diagnostic.PlaneVAxisX * diagnostic.PlaneVAxisX) + (diagnostic.PlaneVAxisY * diagnostic.PlaneVAxisY) + (diagnostic.PlaneVAxisZ * diagnostic.PlaneVAxisZ)));
            Assert.True(diagnostic.VertexCount >= 3);
            Assert.True(diagnostic.IntersectionCount >= 0);
        });
    }


    [Fact]
    public void Step242_PlanarLoopRole_AuditDiagnostics_TargetedProbe_ReportsRuntimeVerdict()
    {
        var targets = new[]
        {
            "testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp",
            "testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp",
            "testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp"
        };

        foreach (var relativePath in targets)
        {
            var entry = new Step242CorpusManifestEntry(
                Id: Path.GetFileNameWithoutExtension(relativePath),
                Path: relativePath,
                Group: "nist-loop-role-probe",
                Notes: "probe",
                ExpectedFirstDiagnostic: null,
                ExpectHashStableAfterCanonicalization: null,
                ExpectTopologyCounts: null,
                ExpectGeometryKinds: null);

            var report = Step242CorpusManifestRunner.RunOne(entry);
            var diagnostics = CapturePlanarAudit(relativePath);
            var analyticAccepted = diagnostics.Count(d => d.AnalyticCircularContainmentAccepted);
            var analyticVsPolygonMismatch = diagnostics.Count(d => d.AnalyticCircularContainmentAccepted && !d.PolygonContainmentAccepted);
            var circularPairs = diagnostics.Count(d => d.OuterIsCircular && d.InnerIsCircular);

            Console.WriteLine($"M78bProbe|file={relativePath}|firstLayer={report.FirstFailureLayer}|firstSource={report.FirstDiagnostic.Source}|firstMessage={report.FirstDiagnostic.MessagePrefix}|pairs={diagnostics.Count}|circularPairs={circularPairs}|analyticAccepted={analyticAccepted}|analyticPolygonMismatch={analyticVsPolygonMismatch}");

            Assert.NotEmpty(diagnostics);
        }
    }

    private static IReadOnlyList<Step242Importer.LoopRoleCircularSamplingDiagnostic> CaptureCircularSampling(string relativePath)
    {
        var absolutePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(absolutePath);
        var diagnostics = new List<Step242Importer.LoopRoleCircularSamplingDiagnostic>();

        using var captureScope = Step242Importer.CaptureLoopRoleCircularSamplingDiagnostics(diagnostics);
        Step242Importer.ImportBody(text);
        return diagnostics;
    }

    private static IReadOnlyList<Step242Importer.PlanarLoopRoleAuditDiagnostic> CapturePlanarAudit(string relativePath)
    {
        var absolutePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(absolutePath);
        var diagnostics = new List<Step242Importer.PlanarLoopRoleAuditDiagnostic>();

        using var captureScope = Step242Importer.CapturePlanarLoopRoleAuditDiagnostics(diagnostics);
        Step242Importer.ImportBody(text);
        return diagnostics;
    }

}
