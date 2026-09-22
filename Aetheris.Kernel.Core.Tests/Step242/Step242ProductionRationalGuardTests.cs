using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242ProductionRationalGuardTests
{
    [Fact]
    public void TrustedProductionRouteRejectsRationalSurfaceBeforeSerialization()
    {
        var box = BrepPrimitives.CreateBox(10, 10, 10).Value;
        var firstSurfaceId = box.Bindings.FaceBindings.First().SurfaceGeometryId;
        var geometry = new BrepGeometryStore();
        foreach (var curve in box.Geometry.Curves) geometry.AddCurve(curve.Key, curve.Value);
        foreach (var surface in box.Geometry.Surfaces)
            geometry.AddSurface(surface.Key, surface.Key == firstSurfaceId
                ? SurfaceGeometry.FromBSplineSurfaceWithKnots(new BSplineSurfaceWithKnots(
                    1, 1,
                    new IReadOnlyList<Point3D>[]
                    {
                        [new(0, 0, 0), new(0, 1, 0)],
                        [new(1, 0, 0), new(1, 1, 0)]
                    },
                    "UNSPECIFIED", false, false, false,
                    [2, 2], [2, 2], [0d, 1d], [0d, 1d], "UNSPECIFIED",
                    new IReadOnlyList<double>[] { [1d, 2d], [1d, 1d] }))
                : surface.Value);
        var body = new BrepBody(box.Topology, geometry, box.Bindings);

        var production = Step242Exporter.ExportBody(body, new Step242ExportOptions
        {
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute
        });
        Assert.False(production.IsSuccess);
        Assert.Contains(production.Diagnostics, d => d.Message.Contains("RationalGeometryNotCanonical", StringComparison.Ordinal));

        var ordinary = Step242Exporter.ExportBody(box, new Step242ExportOptions
        {
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute
        });
        Assert.True(ordinary.IsSuccess, string.Join(" | ", ordinary.Diagnostics.Select(d => d.Message)));
        Assert.DoesNotContain("RATIONAL_B_SPLINE", ordinary.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void TrustedProductionRouteRejectsRationalPcurveBeforeSerialization()
    {
        var box = BrepPrimitives.CreateBox(10, 10, 10).Value;
        var face = box.Topology.Faces.First();
        var coedge = box.Topology.GetLoop(face.LoopIds[0]).CoedgeIds[0];
        var surfaceId = box.Bindings.GetFaceBinding(face.Id).SurfaceGeometryId;
        var curve = new BSpline3Curve(1, [new Point3D(0, 0, 0), new Point3D(1, 0, 0)],
            [2, 2], [0d, 1d], "UNSPECIFIED", false, false, "UNSPECIFIED");
        box.Bindings.AddPcurveBinding(new CoedgePcurveBinding(coedge, face.Id, surfaceId,
            PcurveGeometry.RationalPolynomial(new ParameterInterval(0, 1), curve, [1d, 2d])));

        var production = Step242Exporter.ExportBody(box, new Step242ExportOptions
        {
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute
        });
        Assert.False(production.IsSuccess);
        Assert.Contains(production.Diagnostics, d => d.Message.Contains("RationalGeometryNotCanonical", StringComparison.Ordinal));
    }
}
