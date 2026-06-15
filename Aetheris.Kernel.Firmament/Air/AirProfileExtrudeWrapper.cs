using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Air;

internal static class AirProfileExtrudeWrapper
{
    public static AirLoweringSummary LowerCanonicalRectangleExtrude()
    {
        var diagnostics = new List<AirDiagnostic>
        {
            D("air-x1-profile-extrude-wrapper-created"),
            D("air-x1-profile-extrude-existing-emitter-invoked"),
        };

        var result = LineArcProfileExtrudeEmitter.TryEmit(new LineArcProfileExtrudeRequest(
            [new LineArcProfileLoop2D([
                new LineArcLineSegment2D((-5, -4), (5, -4)),
                new LineArcLineSegment2D((5, -4), (5, 4)),
                new LineArcLineSegment2D((5, 4), (-5, 4)),
                new LineArcLineSegment2D((-5, 4), (-5, -4)),
            ], IsHole: false)],
            Height: 6));

        diagnostics.Add(D("air-x1-profile-extrude-summary-created"));
        diagnostics.Add(D("air-x1-no-production-route-replacement"));
        diagnostics.AddRange(result.Diagnostics.Select(D));
        var succeeded = result.Status == LineArcProfileExtrudeStatus.Succeeded && result.Body is not null;

        return new AirLoweringSummary(
            AirNodeKind.ProfileExtrude,
            AirRouteKind.ProfileExtrudeEmitter,
            succeeded,
            succeeded ? "profile-extrude-air-wrapper-ready-for-envelope-validation" : "profile-extrude-air-wrapper-needs-emitter-investigation",
            new AirProvenance("AIR-X1", "Constructive AIR wrapper", "canonical-rectangle-profile-extrude", "air-x1-profile-extrude-canonical", nameof(LineArcProfileExtrudeEmitter), AirSelectionClass.None, AirRuleKind.None, "generated/constructive", true, ["Uses existing profile extrusion emitter; AIR wrapper is not a production route replacement."]),
            result.Body is null ? new AirTopologySummary(0, 0, 0, 0, 0, 0, 0) : Summarize(result.Body),
            AirStepSmokeSummary.NotChecked,
            diagnostics.GroupBy(x => x.Code).Select(g => g.First()).OrderBy(x => x.Code, StringComparer.Ordinal).ToArray(),
            ["no production route replacement"]);
    }

    private static AirTopologySummary Summarize(Aetheris.Kernel.Core.Brep.BrepBody body) => new(
        body.Topology.Vertices.Count(),
        body.Topology.Edges.Count(),
        body.Topology.Faces.Count(),
        body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane),
        body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder),
        body.Topology.Loops.Count(),
        body.Topology.Coedges.Count(),
        CapFaceCount: 2,
        SideFaceCount: body.Topology.Faces.Count() - 2,
        Bounds: "[-5,-4,-3]..[5,4,3]");

    private static AirDiagnostic D(string code) => new(code, AirDiagnosticSeverity.Info, code);
}
