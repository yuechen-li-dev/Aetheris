using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record AirBoxExtrudeCase(string Name, double Width, double Depth, double Height);

public sealed record AirBoxExtrudeTopologySummary(
    bool BodyProduced,
    int VertexCount,
    int EdgeCount,
    int FaceCount,
    int PlanarFaceCount,
    int LoopCount,
    int CoedgeCount);

public sealed record AirBoxExtrudeStepSummary(
    bool Exported,
    IReadOnlyList<string> PresentMarkers,
    IReadOnlyList<string> MissingMarkers,
    bool ContainsBrepWithVoids,
    IReadOnlyList<string> Diagnostics);

public sealed record AirBoxExtrudeLabResult(
    AirBoxExtrudeCase Case,
    bool IsValid,
    AirBoxExtrudeTopologySummary Baseline,
    AirBoxExtrudeTopologySummary Extrude,
    AirBoxExtrudeStepSummary BaselineStep,
    AirBoxExtrudeStepSummary ExtrudeStep,
    IReadOnlyList<string> Diagnostics,
    string FinalRecommendation,
    bool TopologyParity,
    bool StepSmokeParity);

public static class AirBoxExtrudeLab
{
    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE"];

    public static AirBoxExtrudeLabResult Run(AirBoxExtrudeCase @case)
    {
        var diagnostics = new List<string> { "air-x4-box-extrude-lab-started" };

        if (!IsPositiveFinite(@case.Width) || !IsPositiveFinite(@case.Depth) || !IsPositiveFinite(@case.Height))
        {
            diagnostics.Add("air-x4-invalid-dimensions");
            return new AirBoxExtrudeLabResult(
                @case,
                false,
                EmptyTopology(),
                EmptyTopology(),
                EmptyStep(["invalid dimensions"]),
                EmptyStep(["invalid dimensions"]),
                diagnostics,
                "box-air-extrude-needs-emitter-parity-work",
                false,
                false);
        }

        var baselineResult = BrepPrimitives.CreateBox(@case.Width, @case.Depth, @case.Height);
        if (!baselineResult.IsSuccess || baselineResult.Value is null)
        {
            diagnostics.Add("air-x4-baseline-box-failed");
            diagnostics.AddRange(baselineResult.Diagnostics.Select(d => d.Message));
            return new AirBoxExtrudeLabResult(@case, true, EmptyTopology(), EmptyTopology(), EmptyStep(["baseline failed"]), EmptyStep(["baseline failed"]), diagnostics, "box-air-extrude-needs-emitter-parity-work", false, false);
        }

        diagnostics.Add("air-x4-baseline-box-created");
        var baselineBody = baselineResult.Value;
        var baselineTopology = SummarizeTopology(baselineBody);
        var baselineStep = SummarizeStep(baselineBody);

        var profileResult = PolylineProfile2D.Create([
            new ProfilePoint2D(-@case.Width * 0.5d, -@case.Depth * 0.5d),
            new ProfilePoint2D(@case.Width * 0.5d, -@case.Depth * 0.5d),
            new ProfilePoint2D(@case.Width * 0.5d, @case.Depth * 0.5d),
            new ProfilePoint2D(-@case.Width * 0.5d, @case.Depth * 0.5d)
        ]);

        if (!profileResult.IsSuccess)
        {
            diagnostics.Add("air-x4-extrude-box-failed:profile-create");
            diagnostics.AddRange(profileResult.Diagnostics.Select(d => d.Message));
            return new AirBoxExtrudeLabResult(@case, true, baselineTopology, EmptyTopology(), baselineStep, EmptyStep(profileResult.Diagnostics.Select(d => d.Message).ToArray()), diagnostics, "box-air-extrude-needs-emitter-parity-work", false, false);
        }

        var frame = new ExtrudeFrame3D(new Point3D(0d, 0d, -@case.Height * 0.5d), Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var extrudeResult = BrepExtrude.Create(profileResult.Value, frame, @case.Height);
        if (!extrudeResult.IsSuccess || extrudeResult.Value is null)
        {
            diagnostics.Add("air-x4-extrude-box-failed:extrude-create");
            diagnostics.AddRange(extrudeResult.Diagnostics.Select(d => d.Message));
            return new AirBoxExtrudeLabResult(@case, true, baselineTopology, EmptyTopology(), baselineStep, EmptyStep(extrudeResult.Diagnostics.Select(d => d.Message).ToArray()), diagnostics, "box-air-extrude-needs-emitter-parity-work", false, false);
        }

        diagnostics.Add("air-x4-extrude-box-created");
        var extrudeBody = extrudeResult.Value;
        var extrudeTopology = SummarizeTopology(extrudeBody);
        var extrudeStep = SummarizeStep(extrudeBody);

        var topologyParity = baselineTopology == extrudeTopology;
        diagnostics.Add(topologyParity ? "air-x4-topology-parity-succeeded" : $"air-x4-topology-parity-mismatch: baseline={baselineTopology} extrude={extrudeTopology}");

        var stepParity = baselineStep.Exported
            && extrudeStep.Exported
            && baselineStep.MissingMarkers.Count == 0
            && extrudeStep.MissingMarkers.Count == 0
            && !baselineStep.ContainsBrepWithVoids
            && !extrudeStep.ContainsBrepWithVoids;
        diagnostics.Add(stepParity ? "air-x4-step-smoke-succeeded" : "air-x4-step-smoke-failed");

        var recommendation = topologyParity && stepParity
            ? "box-air-extrude-ready-for-production-migration"
            : "box-air-extrude-needs-emitter-parity-work";

        return new AirBoxExtrudeLabResult(@case, true, baselineTopology, extrudeTopology, baselineStep, extrudeStep, diagnostics, recommendation, topologyParity, stepParity);
    }

    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0d;

    private static AirBoxExtrudeTopologySummary EmptyTopology() => new(false, 0, 0, 0, 0, 0, 0);

    private static AirBoxExtrudeStepSummary EmptyStep(IReadOnlyList<string> diagnostics) => new(false, [], RequiredStepMarkers, false, diagnostics);

    private static AirBoxExtrudeTopologySummary SummarizeTopology(BrepBody body)
        => new(
            true,
            body.Topology.Vertices.Count(),
            body.Topology.Edges.Count(),
            body.Topology.Faces.Count(),
            body.Topology.Faces.Count(face => body.GetFaceSurface(face.Id).Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Plane),
            body.Topology.Loops.Count(),
            body.Topology.Coedges.Count());

    private static AirBoxExtrudeStepSummary SummarizeStep(BrepBody body)
    {
        var exported = Step242Exporter.ExportBody(body);
        if (!exported.IsSuccess || exported.Value is null)
        {
            return new AirBoxExtrudeStepSummary(false, [], RequiredStepMarkers, false, exported.Diagnostics.Select(d => d.Message).ToArray());
        }

        var text = exported.Value;
        var present = RequiredStepMarkers.Where(marker => text.Contains(marker, StringComparison.Ordinal)).ToArray();
        var missing = RequiredStepMarkers.Except(present).ToArray();
        return new AirBoxExtrudeStepSummary(true, present, missing, text.Contains("BREP_WITH_VOIDS", StringComparison.Ordinal), []);
    }
}
