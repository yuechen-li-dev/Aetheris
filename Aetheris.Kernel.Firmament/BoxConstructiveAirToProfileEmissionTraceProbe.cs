using Aetheris.Kernel.Firmament.Air;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentProfileEmissionTopologySummary(
    int Vertices,
    int Edges,
    int Faces,
    int PlanarFaces,
    int CylindricalFaces,
    int Loops,
    int Coedges,
    int? CapFaces,
    int? SideFaces,
    string? Bounds);

public sealed record FirmamentProfileEmissionStepSmokeSummary(
    bool WasChecked,
    bool Succeeded,
    bool RequiredMarkersPresent,
    bool ForbiddenMarkersAbsent,
    IReadOnlyList<string> Diagnostics);

public sealed record ParserBackedBoxProfileExtrudeTraceResult(
    bool WrapperInvoked,
    string EmitterName,
    bool Succeeded,
    double Width,
    double Depth,
    double Height,
    string StageReached,
    FirmamentProfileEmissionTopologySummary? TopologySummary,
    FirmamentProfileEmissionStepSmokeSummary StepSmoke,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Guarantees);

public static class BoxConstructiveAirToProfileEmissionTraceProbe
{
    public static ParserBackedBoxProfileExtrudeTraceResult Invoke(FirmamentConstructiveAirTraceSummary constructiveAir)
    {
        ArgumentNullException.ThrowIfNull(constructiveAir);

        if (!string.Equals(constructiveAir.NodeKind, "AirProfileExtrude", StringComparison.Ordinal)
            || !string.Equals(constructiveAir.CanonicalForm, "rectangle-profile-extrude", StringComparison.Ordinal))
        {
            return Deferred(constructiveAir, "air-x11-box-profile-canonicalization-missing");
        }

        var dimensions = constructiveAir.Dimensions;
        var summary = AirProfileExtrudeWrapper.LowerRectangleExtrude(dimensions.Width, dimensions.Depth, dimensions.Height);
        var topology = summary.TopologySummary;
        var succeeded = summary.Succeeded;
        var stage = succeeded && topology.FaceCount > 0 ? "emitted-brep" : "profile-emission";
        var diagnosticList = new List<string>
        {
            "air-x11-profile-extrude-wrapper-invoked",
            "air-x11-profile-extrude-wrapper-summary-created",
            "air-x11-line-arc-profile-extrude-emitter-invoked",
            "air-x11-profile-emission-summary-created",
            succeeded && topology.FaceCount > 0 ? "air-x11-emitted-brep-summary-created" : "air-x11-actual-stage-profile-emission",
            succeeded && topology.FaceCount > 0 ? "air-x11-actual-stage-emitted-brep" : "air-x11-profile-extrude-wrapper-failed",
            "air-x11-step-smoke-unavailable",
            "air-x11-brepplan-deferred",
            "air-x11-cir-mirror-deferred",
            "air-x11-no-production-grammar-change",
            "air-x11-no-production-route-replacement",
            "air-x11-no-new-geometry-implementation",
            "air-x11-no-profile-emitter-rewrite"
        };
        diagnosticList.AddRange(summary.Diagnostics.Select(d => d.Code));
        var diagnostics = diagnosticList.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        return new(
            true,
            summary.Provenance.RouteName,
            succeeded,
            dimensions.Width,
            dimensions.Depth,
            dimensions.Height,
            stage,
            new(topology.VertexCount, topology.EdgeCount, topology.FaceCount, topology.PlanarFaceCount, topology.CylindricalFaceCount, topology.LoopCount, topology.CoedgeCount, topology.CapFaceCount, topology.SideFaceCount, topology.Bounds),
            new(summary.StepSmokeSummary.WasChecked, summary.StepSmokeSummary.Succeeded, summary.StepSmokeSummary.RequiredMarkersPresent, summary.StepSmokeSummary.ForbiddenMarkersAbsent, summary.StepSmokeSummary.Diagnostics.Select(d => d.Code).Order(StringComparer.Ordinal).ToArray()),
            diagnostics,
            [.. summary.Guarantees, "parser-backed Constructive AIR dimensions propagated to profile emission", "BRepPlan deferred", "CIR mirror deferred", "STEP smoke unavailable"]);
    }

    private static ParserBackedBoxProfileExtrudeTraceResult Deferred(FirmamentConstructiveAirTraceSummary constructiveAir, string reason) => new(
        false,
        "none",
        false,
        constructiveAir.Dimensions.Width,
        constructiveAir.Dimensions.Depth,
        constructiveAir.Dimensions.Height,
        constructiveAir.StageReached,
        null,
        new(false, false, false, false, []),
        ["air-x11-profile-extrude-wrapper-not-invoked", "air-x11-profile-emission-deferred", reason],
        ["no production grammar expansion", "no production route replacement", "no new geometry"]);
}
