using System.Text;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Xunit.Abstractions;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242WorstLoopOrientationAuditTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void Step242_WorstLoops_OrientationAndAssembly_ForensicAudit()
    {
        var targets = new[]
        {
            new AuditTarget("ctc_04", "testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp", LoopId: 259, WorstCoedgeId: 977),
            new AuditTarget("ftc_07", "testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp", LoopId: 11, WorstCoedgeId: 35),
            new AuditTarget("ftc_10", "testdata/step242/nist/FTC/nist_ftc_10_asme1_ap242-e2.stp", LoopId: 3, WorstCoedgeId: 20)
        };

        foreach (var target in targets)
        {
            var fullPath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), target.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var stepText = File.ReadAllText(fullPath, Encoding.UTF8);

            var records = new List<Step242Importer.CoedgeAssemblyForensicRecord>();
            using var _ = Step242Importer.CaptureCoedgeAssemblyForensics(records);
            var import = Step242Importer.ImportBody(stepText);

            var loopRecords = records.Where(r => r.LoopId == target.LoopId).ToList();
            Assert.NotEmpty(loopRecords);

            var worst = loopRecords.Single(r => r.CoedgeId == target.WorstCoedgeId);
            var current = Evaluate(loopRecords, CounterfactualMode.Current, reverseLoopOrder: false);
            var flipOriented = Evaluate(loopRecords, CounterfactualMode.FlipOrientedEdge, reverseLoopOrder: false);
            var ignoreSameSense = Evaluate(loopRecords, CounterfactualMode.IgnoreSameSenseAtAssembly, reverseLoopOrder: false);
            var ignoreFaceBound = Evaluate(loopRecords, CounterfactualMode.IgnoreFaceBoundInAssembly, reverseLoopOrder: false);
            var reverseOrder = Evaluate(loopRecords, CounterfactualMode.Current, reverseLoopOrder: true);

            _output.WriteLine($"=== {target.Id} loop {target.LoopId} (importSuccess={import.IsSuccess}) ===");
            if (!import.IsSuccess)
            {
                _output.WriteLine($"First import diagnostic: {import.Diagnostics.First().Source} :: {import.Diagnostics.First().Message}");
            }
            _output.WriteLine("Assembled coedge sequence:");
            _output.WriteLine(string.Join(" -> ", loopRecords.Select(r => $"c{r.CoedgeId}(e{r.EdgeId})")));
            _output.WriteLine($"Worst coedge c{worst.CoedgeId}: edgeCurveSameSense={worst.EdgeCurveSameSense}, orientedEdgeOrientation={worst.OrientedEdgeOrientation}, faceBoundOrientation={worst.FaceBoundOrientation}, isReversed={worst.IsReversed}");
            _output.WriteLine($"  raw vertices: v{worst.RawEdgeStartVertexEntityId} -> v{worst.RawEdgeEndVertexEntityId}");
            _output.WriteLine($"  effective vertices: v{worst.EffectiveStartVertexEntityId} -> v{worst.EffectiveEndVertexEntityId}");
            _output.WriteLine($"  evaluated start=({worst.EvaluatedStartPoint.X:F6}, {worst.EvaluatedStartPoint.Y:F6}, {worst.EvaluatedStartPoint.Z:F6})");
            _output.WriteLine($"  evaluated end=({worst.EvaluatedEndPoint.X:F6}, {worst.EvaluatedEndPoint.Y:F6}, {worst.EvaluatedEndPoint.Z:F6})");
            _output.WriteLine($"  gaps: prev={worst.GapToPrevious3d:F12}, next={worst.GapToNext3d:F12}");
            _output.WriteLine($"Current composition: mismatches={current.VertexMismatches}, worstJoinGap={current.WorstJoinGap3d:F12}");
            _output.WriteLine($"Counterfactual flip ORIENTED_EDGE: mismatches={flipOriented.VertexMismatches}, worstJoinGap={flipOriented.WorstJoinGap3d:F12}");
            _output.WriteLine($"Counterfactual ignore same_sense at assembly: mismatches={ignoreSameSense.VertexMismatches}, worstJoinGap={ignoreSameSense.WorstJoinGap3d:F12}");
            _output.WriteLine($"Counterfactual ignore FACE_BOUND.orientation in assembly: mismatches={ignoreFaceBound.VertexMismatches}, worstJoinGap={ignoreFaceBound.WorstJoinGap3d:F12}");
            _output.WriteLine($"Counterfactual reverse assembled loop order: mismatches={reverseOrder.VertexMismatches}, worstJoinGap={reverseOrder.WorstJoinGap3d:F12}");
            _output.WriteLine(string.Empty);
        }
    }

    private static EvaluationResult Evaluate(IReadOnlyList<Step242Importer.CoedgeAssemblyForensicRecord> records, CounterfactualMode mode, bool reverseLoopOrder)
    {
        var ordered = reverseLoopOrder ? records.Reverse().ToList() : records.ToList();
        var endpoints = ordered.Select(r => EndpointFor(r, mode, reverseLoopOrder)).ToList();

        var mismatches = 0;
        var worstGap = 0d;
        for (var i = 0; i < endpoints.Count; i++)
        {
            var current = endpoints[i];
            var next = endpoints[(i + 1) % endpoints.Count];
            if (current.EndVertex != next.StartVertex)
            {
                mismatches++;
            }

            var gap = (current.EndPoint - next.StartPoint).Length;
            if (gap > worstGap)
            {
                worstGap = gap;
            }
        }

        return new EvaluationResult(mismatches, worstGap);
    }

    private static EndpointState EndpointFor(Step242Importer.CoedgeAssemblyForensicRecord record, CounterfactualMode mode, bool reverseLoopOrder)
    {
        var composeWithFaceBound = mode != CounterfactualMode.IgnoreFaceBoundInAssembly;
        var oriented = mode == CounterfactualMode.FlipOrientedEdge ? !record.OrientedEdgeOrientation : record.OrientedEdgeOrientation;
        var sameSense = mode == CounterfactualMode.IgnoreSameSenseAtAssembly ? false : record.EdgeCurveSameSense;

        var isReversed = oriented != sameSense;
        if (composeWithFaceBound && !record.FaceBoundOrientation)
        {
            isReversed = !isReversed;
        }

        var startVertex = isReversed ? record.RawEdgeEndVertexEntityId : record.RawEdgeStartVertexEntityId;
        var endVertex = isReversed ? record.RawEdgeStartVertexEntityId : record.RawEdgeEndVertexEntityId;
        var startPoint = isReversed == record.IsReversed ? record.EvaluatedStartPoint : record.EvaluatedEndPoint;
        var endPoint = isReversed == record.IsReversed ? record.EvaluatedEndPoint : record.EvaluatedStartPoint;

        if (reverseLoopOrder)
        {
            return new EndpointState(endVertex, startVertex, endPoint, startPoint);
        }

        return new EndpointState(startVertex, endVertex, startPoint, endPoint);
    }

    private sealed record AuditTarget(string Id, string RelativePath, int LoopId, int WorstCoedgeId);
    private sealed record EvaluationResult(int VertexMismatches, double WorstJoinGap3d);
    private sealed record EndpointState(int StartVertex, int EndVertex, Point3D StartPoint, Point3D EndPoint);

    private enum CounterfactualMode
    {
        Current,
        FlipOrientedEdge,
        IgnoreSameSenseAtAssembly,
        IgnoreFaceBoundInAssembly
    }
}
