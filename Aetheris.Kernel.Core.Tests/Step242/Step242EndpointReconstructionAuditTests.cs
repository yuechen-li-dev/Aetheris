using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242EndpointReconstructionAuditTests
{
    private static readonly string[] TargetFiles =
    [
        "testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp",
        "testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp",
        "testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp"
    ];

    [Theory]
    [MemberData(nameof(TargetFileData))]
    public void Step242_TargetPlanarResiduals_EndpointReconstructionDiagnostics_AreDeterministic(string relativePath)
    {
        var first = CaptureEndpointDiagnostics(relativePath);
        var second = CaptureEndpointDiagnostics(relativePath);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Step242_TargetPlanarResiduals_EndpointReconstructionAudit_EmitsRuntimeSummary()
    {
        foreach (var relativePath in TargetFiles)
        {
            var diagnostics = CaptureEndpointDiagnostics(relativePath);
            Assert.NotEmpty(diagnostics);

            var maxStartError = diagnostics.Max(d => d.StartReconstructionError);
            var maxEndError = diagnostics.Max(d => d.EndReconstructionError);
            var worstEvaluatedJoin = diagnostics.MaxBy(d => d.EvaluatedJoinGapFromPrevious)!;
            var firstDiverged = diagnostics
                .OrderBy(d => d.LoopId)
                .ThenBy(d => d.CoedgeId)
                .FirstOrDefault(d => d.EvaluatedJoinGapFromPrevious > d.RawJoinGapFromPrevious + 1e-9d
                    || d.StartReconstructionError > 1e-9d
                    || d.EndReconstructionError > 1e-9d);

            Console.WriteLine($"[endpoint-audit] file={Path.GetFileName(relativePath)} maxStartErr={maxStartError:G17} maxEndErr={maxEndError:G17} worstEvalJoin={worstEvaluatedJoin.EvaluatedJoinGapFromPrevious:G17} worstLoop={worstEvaluatedJoin.LoopId} worstCoedge={worstEvaluatedJoin.CoedgeId} prevCoedge={worstEvaluatedJoin.PreviousCoedgeId}");

            if (firstDiverged is not null)
            {
                Console.WriteLine($"[endpoint-audit] first-diverged file={Path.GetFileName(relativePath)} loop={firstDiverged.LoopId} coedge={firstDiverged.CoedgeId} edge={firstDiverged.EdgeId} orientedEdgeEntity={firstDiverged.OrientedEdgeEntityId} edgeCurveEntity={firstDiverged.EdgeCurveEntityId} curve={firstDiverged.CurveKind} startErr={firstDiverged.StartReconstructionError:G17} endErr={firstDiverged.EndReconstructionError:G17} rawJoin={firstDiverged.RawJoinGapFromPrevious:G17} evalJoin={firstDiverged.EvaluatedJoinGapFromPrevious:G17} senses=({firstDiverged.EdgeCurveSameSense},{firstDiverged.OrientedEdgeOrientation},{firstDiverged.FaceBoundOrientation})");
            }

            var hasLargeEvaluatedJoinGap = diagnostics.Any(d => d.EvaluatedJoinGapFromPrevious > 1d);
            Assert.True(hasLargeEvaluatedJoinGap, $"Expected at least one large evaluated join gap for {relativePath}.");
        }
    }

    public static IEnumerable<object[]> TargetFileData()
        => TargetFiles.Select(path => new object[] { path });

    private static IReadOnlyList<Step242Importer.LoopEndpointReconstructionDiagnostic> CaptureEndpointDiagnostics(string relativePath)
    {
        var absolutePath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(absolutePath);
        var diagnostics = new List<Step242Importer.LoopEndpointReconstructionDiagnostic>();

        using var captureScope = Step242Importer.CaptureLoopEndpointReconstructionDiagnostics(diagnostics);
        Step242Importer.ImportBody(text);
        return diagnostics;
    }
}
