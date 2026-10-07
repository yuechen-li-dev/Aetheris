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
    public void ExplicitProductionBudget_RecoversOnCopyAndPreservesTopologyThroughRoundtrip()
    {
        var box = Step242Importer.ImportBody(Step242Exporter.ExportBody(BrepPrimitives.CreateBox(10, 10, 10).Value).Value).Value;
        var id = box.Bindings.FaceBindings.First().SurfaceGeometryId;
        var plane = box.Geometry.GetSurface(id).Plane!.Value;
        var vertices = box.Topology.Vertices.Select(v => { box.TryGetVertexPoint(v.Id, out var p); return p; }).ToArray();
        var us = vertices.Select(p => (p - plane.Origin).Dot(plane.UAxis.ToVector())).ToArray();
        var vs = vertices.Select(p => (p - plane.Origin).Dot(plane.VAxis.ToVector())).ToArray();
        var rational = new BSplineSurfaceWithKnots(1, 1,
            new IReadOnlyList<Point3D>[] {
                [plane.Evaluate(us.Min(), vs.Min()), plane.Evaluate(us.Min(), vs.Max())],
                [plane.Evaluate(us.Max(), vs.Min()), plane.Evaluate(us.Max(), vs.Max())]
            }, "UNSPECIFIED", false, false, false, [2, 2], [2, 2], [0d, 1d], [0d, 1d], "UNSPECIFIED",
            new IReadOnlyList<double>[] { [1d, 1.2d], [1d, 1.2d] });
        var geometry = new BrepGeometryStore();
        foreach (var c in box.Geometry.Curves) geometry.AddCurve(c.Key, c.Value);
        foreach (var s in box.Geometry.Surfaces) geometry.AddSurface(s.Key,
            s.Key == id ? SurfaceGeometry.FromBSplineSurfaceWithKnots(rational) : s.Value);
        var points = box.Topology.Vertices.ToDictionary(v => v.Id, v => { box.TryGetVertexPoint(v.Id, out var p); return p; });
        var bindings = new BrepBindingModel();
        foreach (var e in box.Bindings.EdgeBindings) bindings.AddEdgeBinding(e);
        foreach (var f in box.Bindings.FaceBindings) bindings.AddFaceBinding(f);
        var source = new BrepBody(box.Topology, geometry, bindings, points, faceOrientationReport: box.FaceOrientationReport);
        var prepared = Step242ProductionNormalization.Prepare(source, .1);
        Assert.True(prepared.IsSuccess, string.Join(" | ", prepared.Diagnostics.Select(d => d.Message)));
        Assert.Same(source.Topology, prepared.Value.Topology);
        Assert.Empty(source.Bindings.PcurveBindings);
        Assert.True(source.Geometry.GetSurface(id).BSplineSurfaceWithKnots!.IsRational);
        Assert.False(prepared.Value.Geometry.GetSurface(id).BSplineSurfaceWithKnots!.IsRational);
        Assert.True(BrepPcurveValidator.Validate(prepared.Value, 1e-3, requireEveryCoedge: true).IsValid);
        for (var u = 0; u <= 23; u++) for (var v = 0; v <= 31; v++)
            Assert.InRange((rational.Evaluate(u / 23d, v / 31d) -
                prepared.Value.Geometry.GetSurface(id).BSplineSurfaceWithKnots!.Evaluate(u / 23d, v / 31d)).Length, 0, .1);
        var exported = Step242Exporter.ExportBody(source, new Step242ExportOptions {
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            ImportedRecoveryToleranceMillimetres = .1
        });
        Assert.True(exported.IsSuccess, string.Join(" | ", exported.Diagnostics.Select(d => d.Message)));
        Assert.Contains(exported.Diagnostics, d => d.Source == "Step242.ProductionNormalization");
        Assert.DoesNotContain("RATIONAL_B_SPLINE", exported.Value, StringComparison.Ordinal);
        var reimport = Step242Importer.ImportBody(exported.Value);
        Assert.True(reimport.IsSuccess);
        Assert.Equal(source.Topology.Faces.Count(), reimport.Value.Topology.Faces.Count());
        Assert.Equal(source.Topology.Edges.Count(), reimport.Value.Topology.Edges.Count());
    }

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
