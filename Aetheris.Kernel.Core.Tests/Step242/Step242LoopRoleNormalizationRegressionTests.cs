using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242LoopRoleNormalizationRegressionTests
{
    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp", null, null)]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_05_asme1_ap242-e1.stp", null, null)]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp", null, null)]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp", null, null)]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp", null, null)]
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
            Assert.DoesNotContain("Importer.LoopRole.CylinderNonNormalizableDegenerateProjection", first.FirstDiagnostic.Source, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(expectedSource, first.FirstDiagnostic.Source);
            if (expectedMessagePrefix is not null)
            {
                Assert.StartsWith(expectedMessagePrefix, first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
            }
        }

        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);
    }

    [Theory]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_08_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/STC/nist_stc_06_asme1_ap242-e3.stp")]
    public void Step242_CylinderLoopRole_ProjectionDiagnostics_AreDeterministic_AndExplicit(string relativePath)
    {
        var first = CaptureCylinderProjectionDiagnostics(relativePath);
        var second = CaptureCylinderProjectionDiagnostics(relativePath);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
        Assert.Contains(first, d => !string.Equals(d.Degeneracy, "None", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp")]
    public void Step242_TorusLoopRole_ProjectionDiagnostics_AreDeterministic_AndExplicit(string relativePath)
    {
        var first = CaptureTorusProjectionDiagnostics(relativePath);
        var second = CaptureTorusProjectionDiagnostics(relativePath);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
        Assert.Contains(first, d => !string.Equals(d.InitialDegeneracy, "None", StringComparison.Ordinal));
        Assert.Contains(first, d => string.Equals(d.InitialDegeneracy, "RepeatedSeamProjectionCollapse", StringComparison.Ordinal));
        Assert.Contains(first, d => string.Equals(d.Degeneracy, "DegenerateMinorSpan", StringComparison.Ordinal));
        Assert.Contains(first, d => d.MajorSeamCrossings > 0 && d.MinorSeamCrossings == 0);
        Assert.Contains(first, d => d.RepeatedMajorSeamPointCount == 0 && d.RepeatedMinorSeamPointCount == 0);
        Assert.Contains(first, d => d.MajorSpan >= 5.4d && d.MinorSpan <= 1e-8d);
        Assert.Contains(first, d => d.FullMajorSpanNearConstantMinorCandidate);
    }


    [Fact]
    public void Step242_NistFtc11_TorusMajorRingDegeneracy_AdvancesPastMinorSpanBlocker_Deterministically()
    {
        const string relativePath = "testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp";
        var entry = new Step242CorpusManifestEntry(
            Id: Path.GetFileNameWithoutExtension(relativePath),
            Path: relativePath,
            Group: "nist-loop-role-regression",
            Notes: "torus major-ring progression",
            ExpectedFirstDiagnostic: null,
            ExpectHashStableAfterCanonicalization: null,
            ExpectTopologyCounts: null,
            ExpectGeometryKinds: null);

        var first = Step242CorpusManifestRunner.RunOne(entry);
        var second = Step242CorpusManifestRunner.RunOne(entry);

        Assert.Equal(first.FirstFailureLayer, second.FirstFailureLayer);
        Assert.Equal(first.FirstDiagnostic.Source, second.FirstDiagnostic.Source);
        Assert.Equal(first.FirstDiagnostic.MessagePrefix, second.FirstDiagnostic.MessagePrefix);

        Assert.NotEqual("Importer.LoopRole.TorusDegenerateMinorSpan", first.FirstDiagnostic.Source);
        Assert.DoesNotContain("TorusDegenerateMinorSpan", first.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_05_asme1_ap242-e1.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp")]
    public void Step242_PeriodicDegenerateNormalization_PreservesSharedClosedEdgeFaceUses_Deterministically(string relativePath)
    {
        var first = ImportFromCorpus(relativePath);
        var second = ImportFromCorpus(relativePath);

        var firstSignature = BuildSharedClosedEdgeUseSignature(first);
        var secondSignature = BuildSharedClosedEdgeUseSignature(second);

        Assert.NotEmpty(firstSignature);
        Assert.Equal(firstSignature, secondSignature);
    }

    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp")]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp")]
    public void Step242_PlanarLoopRole_CoedgeGapDiagnostics_AreDeterministic(string relativePath)
    {
        var first = CaptureCoedgeGaps(relativePath);
        var second = CaptureCoedgeGaps(relativePath);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
        Assert.All(first, d => Assert.True(d.Gap3d >= 0d));
    }

    [Theory]
    [InlineData("testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp", 259, 977)]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp", 11, 35)]
    [InlineData("testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp", 3, 20)]
    public void Step242_PlanarLoopRole_KnownWorstLoopJoinGaps_CollapseToNearZero(
        string relativePath,
        int loopId,
        int coedgeId)
    {
        var diagnostics = CaptureCoedgeGaps(relativePath);
        var matching = diagnostics
            .Where(d => d.LoopId == loopId && (d.NextCoedgeId == coedgeId || d.PreviousCoedgeId == coedgeId))
            .ToArray();

        Assert.NotEmpty(matching);
        var worstGap = matching.Max(d => d.Gap3d);
        Assert.InRange(worstGap, 0d, 1e-8);
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


    private static IReadOnlyList<Step242Importer.LoopRoleCoedgeGapDiagnostic> CaptureCoedgeGaps(string relativePath)
    {
        var absolutePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(absolutePath);
        var diagnostics = new List<Step242Importer.LoopRoleCoedgeGapDiagnostic>();

        using var captureScope = Step242Importer.CaptureLoopRoleCoedgeGapDiagnostics(diagnostics);
        Step242Importer.ImportBody(text);
        return diagnostics;
    }

    private static global::Aetheris.Kernel.Core.Brep.BrepBody ImportFromCorpus(string relativePath)
    {
        var absolutePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(absolutePath);
        var import = Step242Importer.ImportBody(text);
        Assert.True(import.IsSuccess);
        return import.Value;
    }

    private static IReadOnlyList<string> BuildSharedClosedEdgeUseSignature(global::Aetheris.Kernel.Core.Brep.BrepBody body)
    {
        var faceUsesByEdge = new Dictionary<int, HashSet<int>>();
        foreach (var face in body.Topology.Faces)
        {
            foreach (var loopId in face.LoopIds)
            {
                foreach (var coedgeId in body.GetCoedgeIds(loopId))
                {
                    var coedge = body.Topology.GetCoedge(coedgeId);
                    if (!faceUsesByEdge.TryGetValue(coedge.EdgeId.Value, out var faceUses))
                    {
                        faceUses = [];
                        faceUsesByEdge.Add(coedge.EdgeId.Value, faceUses);
                    }

                    faceUses.Add(face.Id.Value);
                }
            }
        }

        return faceUsesByEdge
            .Where(kvp => kvp.Value.Count == 2)
            .Select(kvp => (Edge: body.Topology.GetEdge(new global::Aetheris.Kernel.Core.Topology.EdgeId(kvp.Key)), Faces: kvp.Value.OrderBy(v => v).ToArray()))
            .Where(item => item.Edge.StartVertexId == item.Edge.EndVertexId)
            .Select(item => $"edge:{item.Edge.Id.Value}:faces:{item.Faces[0]}-{item.Faces[1]}")
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToArray();
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

    private static IReadOnlyList<Step242Importer.LoopRoleCylinderProjectionDiagnostic> CaptureCylinderProjectionDiagnostics(string relativePath)
    {
        var absolutePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(absolutePath);
        var diagnostics = new List<Step242Importer.LoopRoleCylinderProjectionDiagnostic>();

        using var captureScope = Step242Importer.CaptureLoopRoleCylinderProjectionDiagnostics(diagnostics);
        Step242Importer.ImportBody(text);
        return diagnostics;
    }

    private static IReadOnlyList<Step242Importer.LoopRoleTorusProjectionDiagnostic> CaptureTorusProjectionDiagnostics(string relativePath)
    {
        var absolutePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(absolutePath);
        var diagnostics = new List<Step242Importer.LoopRoleTorusProjectionDiagnostic>();

        using var captureScope = Step242Importer.CaptureLoopRoleTorusProjectionDiagnostics(diagnostics);
        Step242Importer.ImportBody(text);
        return diagnostics;
    }
}
