using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Air;

internal static class AirProfileExtrudeWrapper
{
    public static AirLoweringSummary LowerCanonicalRectangleExtrude() => LowerRectangleExtrude(10, 8, 6);

    internal static AirLoweringSummary LowerRectangleExtrude(double width, double depth, double height)
    {
        var diagnostics = new List<AirDiagnostic>
        {
            D("air-x1-profile-extrude-wrapper-created"),
            D("air-x1-profile-extrude-existing-emitter-invoked"),
        };

        var result = LineArcProfileExtrudeEmitter.TryEmit(new LineArcProfileExtrudeRequest(
            [new LineArcProfileLoop2D([
                new LineArcLineSegment2D((-width / 2d, -depth / 2d), (width / 2d, -depth / 2d)),
                new LineArcLineSegment2D((width / 2d, -depth / 2d), (width / 2d, depth / 2d)),
                new LineArcLineSegment2D((width / 2d, depth / 2d), (-width / 2d, depth / 2d)),
                new LineArcLineSegment2D((-width / 2d, depth / 2d), (-width / 2d, -depth / 2d)),
            ], IsHole: false)],
            Height: height));

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
            result.Body is null ? new AirTopologySummary(0, 0, 0, 0, 0, 0, 0) : Summarize(result.Body, width, depth, height),
            AirStepSmokeSummary.NotChecked,
            diagnostics.GroupBy(x => x.Code).Select(g => g.First()).OrderBy(x => x.Code, StringComparer.Ordinal).ToArray(),
            ["no production route replacement"]);
    }

    private static AirTopologySummary Summarize(Aetheris.Kernel.Core.Brep.BrepBody body, double width, double depth, double height) => new(
        body.Topology.Vertices.Count(),
        body.Topology.Edges.Count(),
        body.Topology.Faces.Count(),
        body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane),
        body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder),
        body.Topology.Loops.Count(),
        body.Topology.Coedges.Count(),
        CapFaceCount: 2,
        SideFaceCount: body.Topology.Faces.Count() - 2,
        Bounds: FormattableString.Invariant($"[{-width / 2d:g},{-depth / 2d:g},{-height / 2d:g}]..[{width / 2d:g},{depth / 2d:g},{height / 2d:g}]"));

    private static AirDiagnostic D(string code) => new(code, AirDiagnosticSeverity.Info, code);
}
