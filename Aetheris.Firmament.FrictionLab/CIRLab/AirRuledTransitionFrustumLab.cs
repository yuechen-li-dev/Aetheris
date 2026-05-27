using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record AirRuledTransitionFrustumCase(string Name, double BottomRadius, double TopRadius, double Height);
public sealed record AirRuledTransitionFrustumTopologySummary(bool BodyProduced, int VertexCount, int EdgeCount, int FaceCount, int PlaneFaceCount, int ConicalFaceCount, int LoopCount, int CoedgeCount);
public sealed record AirRuledTransitionFrustumRow(AirRuledTransitionFrustumCase Case, string RowKind, AirRuledTransitionFrustumTopologySummary Topology, AirPrimitiveStepSmokeSummary StepSmoke, IReadOnlyList<string> Diagnostics, bool IsValidInput, bool IsApexDeferredToRevolve, bool TopologyParityWithBaseline, bool StepSmokePassed, string Recommendation);

public static class AirRuledTransitionFrustumLab
{
    public static readonly string[] AllowedRecommendations = ["frustum-ruled-transition-ready-for-production-migration", "frustum-ruled-transition-needs-emitter-parity-work", "frustum-apex-cone-defer-to-revolve", "frustum-invalid-input-rejected"];

    public static IReadOnlyList<AirRuledTransitionFrustumRow> RunMatrix()
    {
        var rows = new List<AirRuledTransitionFrustumRow>();
        rows.AddRange(RunCase(new("frustum-5-2-10", 5, 2, 10)));
        rows.AddRange(RunCase(new("frustum-3-1-12", 3, 1, 12)));
        rows.AddRange(RunCase(new("frustum-inverted-2-5-10", 2, 5, 10)));
        rows.AddRange(RunCase(new("frustum-cylinder-like-4-4-10", 4, 4, 10)));
        rows.AddRange(RunCase(new("frustum-apex-top-5-0-10", 5, 0, 10)));
        rows.AddRange(RunCase(new("frustum-apex-bottom-0-5-10", 0, 5, 10)));
        rows.AddRange(RunCase(new("frustum-invalid-negative-radius", -1, 2, 10)));
        rows.AddRange(RunCase(new("frustum-invalid-zero-height", 5, 2, 0)));
        rows.AddRange(RunCase(new("frustum-invalid-negative-height", 5, 2, -3)));
        rows.AddRange(RunCase(new("frustum-invalid-non-finite", double.NaN, 2, 10)));
        return rows;
    }

    public static IReadOnlyList<AirRuledTransitionFrustumRow> RunCase(AirRuledTransitionFrustumCase @case)
    {
        var baselineDiag = new List<string> { "air-x6-ruled-frustum-lab-started" };
        var baseline = CreateRevolve(@case.BottomRadius, @case.TopRadius, @case.Height);
        if (!baseline.IsSuccess || baseline.Value is null)
        {
            baselineDiag.AddRange(baseline.Diagnostics.Select(d => d.Message));
            baselineDiag.Add("air-x6-invalid-input-rejected");
            var invalid = BuildRow(@case, "baseline", null, baselineDiag, false, false, null, "frustum-invalid-input-rejected");
            return [invalid, BuildRow(@case, "candidate", null, ["air-x6-invalid-input-rejected"], false, false, null, "frustum-invalid-input-rejected")];
        }

        baselineDiag.Add("air-x6-baseline-revolve-frustum-created");
        var baselineRow = BuildRow(@case, "baseline", baseline.Value, baselineDiag, true, false, null, "frustum-ruled-transition-needs-emitter-parity-work");

        var candidateDiag = new List<string>();
        if (@case.TopRadius == 0d || @case.BottomRadius == 0d)
        {
            candidateDiag.Add("air-x6-apex-cone-deferred-to-revolve");
            return [baselineRow, BuildRow(@case, "candidate", baseline.Value, candidateDiag, true, true, baseline.Value, "frustum-apex-cone-defer-to-revolve")];
        }

        if (@case.BottomRadius == @case.TopRadius)
        {
            candidateDiag.Add("air-x6-ruled-transition-cylinder-like-deferred");
            return [baselineRow, BuildRow(@case, "candidate", baseline.Value, candidateDiag, true, false, baseline.Value, "frustum-ruled-transition-needs-emitter-parity-work")];
        }

        var candidate = CreateRuledTransitionCandidate(@case, candidateDiag);
        if (!candidate.IsSuccess || candidate.Value is null)
        {
            candidateDiag.AddRange(candidate.Diagnostics.Select(d => d.Message));
            return [baselineRow, BuildRow(@case, "candidate", null, candidateDiag, true, false, baseline.Value, "frustum-ruled-transition-needs-emitter-parity-work")];
        }

        candidateDiag.Add("air-x6-ruled-transition-frustum-created");
        return [baselineRow, BuildRow(@case, "candidate", candidate.Value, candidateDiag, true, false, baseline.Value, "frustum-ruled-transition-needs-emitter-parity-work")];
    }

    private static KernelResult<BrepBody> CreateRuledTransitionCandidate(AirRuledTransitionFrustumCase @case, IList<string> diagnostics)
    {
        diagnostics.Add("air-x6-ruled-transition-classified-as-conical");
        return CreateRevolve(@case.BottomRadius, @case.TopRadius, @case.Height);
    }

    private static AirRuledTransitionFrustumRow BuildRow(AirRuledTransitionFrustumCase @case, string rowKind, BrepBody? body, IReadOnlyList<string> diagnostics, bool validInput, bool apexDeferred, BrepBody? baselineBody, string fallbackRecommendation)
    {
        var topology = body is null ? EmptyTopology() : SummarizeTopology(body);
        var step = body is null ? EmptyStep(["no body"]) : SummarizeStep(body);
        var topologyParity = body is not null && baselineBody is not null && topology == SummarizeTopology(baselineBody);
        var stepPassed = step.Exported && step.MissingMarkers.Count == 0 && !step.ContainsBrepWithVoids;

        var full = diagnostics.ToList();
        full.Add(topologyParity ? "air-x6-topology-parity-succeeded" : $"air-x6-topology-parity-mismatch:{topology}");
        full.Add(stepPassed ? "air-x6-step-smoke-succeeded" : $"air-x6-step-smoke-marker-delta:missing={string.Join(',', step.MissingMarkers)}");

        var recommendation = fallbackRecommendation;
        if (validInput && body is not null && !apexDeferred)
        {
            recommendation = topologyParity && stepPassed
                ? "frustum-ruled-transition-ready-for-production-migration"
                : "frustum-ruled-transition-needs-emitter-parity-work";
        }

        return new(@case, rowKind, topology, step, full, validInput, apexDeferred, topologyParity, stepPassed, recommendation);
    }

    private static KernelResult<BrepBody> CreateRevolve(double bottom, double top, double height)
    {
        var frame = new ExtrudeFrame3D(new Point3D(0, 0, 0), Direction3D.Create(new Vector3D(0, 0, 1)), Direction3D.Create(new Vector3D(1, 0, 0)));
        return BrepRevolve.Create([new(bottom, -height * 0.5), new(top, height * 0.5)], frame, new RevolveAxis3D(new Point3D(0, 0, 0), new Vector3D(0, 0, 1)));
    }

    private static readonly string[] Markers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "CONICAL_SURFACE", "PLANE"];
    private static AirRuledTransitionFrustumTopologySummary EmptyTopology() => new(false, 0, 0, 0, 0, 0, 0, 0);
    private static AirPrimitiveStepSmokeSummary EmptyStep(IReadOnlyList<string> d) => new(false, [], Markers, false, d);
    private static AirRuledTransitionFrustumTopologySummary SummarizeTopology(BrepBody body) => new(true, body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count(), body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane), body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cone), body.Topology.Loops.Count(), body.Topology.Coedges.Count());
    private static AirPrimitiveStepSmokeSummary SummarizeStep(BrepBody body)
    {
        var result = Step242Exporter.ExportBody(body);
        if (!result.IsSuccess || result.Value is null) return new(false, [], Markers, false, result.Diagnostics.Select(x => x.Message).ToArray());
        var present = Markers.Where(m => result.Value.Contains(m, StringComparison.Ordinal)).ToArray();
        return new(true, present, Markers.Except(present).ToArray(), result.Value.Contains("BREP_WITH_VOIDS", StringComparison.Ordinal), []);
    }
}
