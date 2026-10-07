using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class RecoveredCylinderTrimMaterializationTests
{
    [Fact]
    public void SingleBoundedSector_WithEquivalentPcurveCharts_DoesNotFillTheCylinder()
    {
        var builder = new TopologyBuilder(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel();
        var support = new CylinderSurface(Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)),
            1, Direction3D.Create(new Vector3D(1, 0, 0)));
        var uv = new[] { (6.2, 0d), (6.4, 0d), (6.4, 2d), (6.2, 2d) };
        var points = new Dictionary<VertexId, Point3D>();
        var vertices = uv.Select(p => { var id = builder.AddVertex(); points[id] = support.Evaluate(p.Item1, p.Item2); return id; }).ToArray();
        var loop = builder.AllocateLoopId(); var uses = uv.Select(_ => builder.AllocateCoedgeId()).ToArray();
        for (var i = 0; i < 4; i++)
        {
            var j = (i + 1) % 4; var edge = builder.AddEdge(vertices[i], vertices[j]);
            var curveId = new CurveGeometryId(i + 1);
            var controls = Enumerable.Range(0, 17).Select(k => support.Evaluate(
                uv[i].Item1 + (uv[j].Item1 - uv[i].Item1) * k / 16d,
                uv[i].Item2 + (uv[j].Item2 - uv[i].Item2) * k / 16d)).ToArray();
            geometry.AddCurve(curveId, CurveGeometry.FromBSpline(new BSpline3Curve(1, controls,
                Enumerable.Range(0, 17).Select(k => k == 0 || k == 16 ? 2 : 1).ToArray(),
                Enumerable.Range(0, 17).Select(k => k / 16d).ToArray(), "UNSPECIFIED", false, false, "UNSPECIFIED")));
            bindings.AddEdgeBinding(new(edge, curveId, new ParameterInterval(0, 1)));
            builder.AddCoedge(new(uses[i], edge, loop, uses[j], uses[(i + 3) % 4], false));
        }
        builder.AddLoop(new Loop(loop, uses)); var face = builder.AddFace([loop]); builder.AddBody([builder.AddShell([face])]);
        var surface = new SurfaceGeometryId(1); geometry.AddSurface(surface, SurfaceGeometry.FromCylinder(support));
        bindings.AddFaceBinding(new(face, surface));
        for (var i = 0; i < 4; i++)
        {
            var j = (i + 1) % 4;
            var offset = i is 1 or 2 ? 2d * double.Pi : 0d;
            bindings.AddPcurveBinding(new(uses[i], face, surface, PcurveGeometry.Line(new(0, 1),
                new(uv[i].Item1 - offset, uv[i].Item2), new(uv[j].Item1 - offset, uv[j].Item2))));
        }
        var body = new BrepBody(builder.Model, geometry, bindings, points);
        var before = bindings.PcurveBindings.ToArray();
        var result = BrepDisplayTessellator.Tessellate(body);
        Assert.True(result.IsSuccess);
        var patch = Assert.Single(result.Value.FacePatches); Assert.NotEmpty(patch.TriangleIndices);
        Assert.All(patch.Positions, p => { Assert.True(p.X > .99); Assert.InRange(p.Y, -.084, .117); });
        Assert.Equal(before, bindings.PcurveBindings.ToArray());
    }

    [Theory]
    [InlineData(true, .05, true)]
    [InlineData(false, .05, false)]
    [InlineData(true, .02, false)]
    public void RecoveredBoundaryProjection_IsBoundedByMeasuredDeviationAndDisplayBudget(bool recovered, double chord, bool visible)
    {
        var body = CylinderWithHole(recovered);
        var mesh = BrepDisplayTessellator.TessellateBoundedPartial(body, DisplayTessellationOptions.Default with { ChordTolerance = chord });
        var patch = Assert.Single(mesh.FacePatches);
        Assert.Equal(visible, patch.TriangleIndices.Count > 0);
        Assert.Empty(body.Bindings.PcurveBindings);
        if (visible)
        {
            Assert.All(patch.Positions, p => Assert.InRange(double.Sqrt(p.X * p.X + p.Y * p.Y), 1 - 1e-10, 1 + 1e-10));
            Assert.All(body.Geometry.Curves, c => Assert.Equal(.04, c.Value.RecoveryProvenance!.MeasuredMaxDeviationMillimetres));
        }
        else Assert.Contains(mesh.FaceDiagnostics!, d => d.Message.Contains("could not project"));
    }

    private static BrepBody CylinderWithHole(bool recovered)
    {
        var builder = new TopologyBuilder(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel();
        var points = new Dictionary<VertexId, Point3D>(); var loops = new List<LoopId>(); var edgeNumber = 0;
        foreach (var uv in new[] { new[] { (0d, 0d), (1d, 0d), (1d, 2d), (0d, 2d) },
            new[] { (.2d, .5d), (.2d, 1.5d), (.4d, 1.5d), (.4d, .5d) } })
        {
            var vertices = uv.Select(p => { var id = builder.AddVertex(); points[id] = Point(p.Item1, p.Item2); return id; }).ToArray();
            var loop = builder.AllocateLoopId(); loops.Add(loop); var uses = uv.Select(_ => builder.AllocateCoedgeId()).ToArray();
            for (var i = 0; i < 4; i++)
            {
                var j = (i + 1) % 4; var edge = builder.AddEdge(vertices[i], vertices[j]);
                var controls = Enumerable.Range(0, 33).Select(k => Point(uv[i].Item1 + (uv[j].Item1 - uv[i].Item1) * k / 32d,
                    uv[i].Item2 + (uv[j].Item2 - uv[i].Item2) * k / 32d)).ToArray();
                var spline = new BSpline3Curve(1, controls, Enumerable.Range(0, 33).Select(k => k == 0 || k == 32 ? 2 : 1).ToArray(),
                    Enumerable.Range(0, 33).Select(k => k / 32d).ToArray(), "UNSPECIFIED", false, false, "UNSPECIFIED");
                var curve = new CurveGeometryId(++edgeNumber);
                geometry.AddCurve(curve, recovered ? CurveGeometry.FromRecoveredBSpline(spline,
                    new("synthetic rational edge", "bounded fixture", .1, .04, true, "free-form")) : CurveGeometry.FromBSpline(spline));
                bindings.AddEdgeBinding(new(edge, curve, new ParameterInterval(0, 1)));
                builder.AddCoedge(new(uses[i], edge, loop, uses[j], uses[(i + 3) % 4], false));
            }
            builder.AddLoop(new Loop(loop, uses));
        }
        var face = builder.AddFace(loops); builder.AddBody([builder.AddShell([face])]);
        var surface = new SurfaceGeometryId(1); geometry.AddSurface(surface, SurfaceGeometry.FromCylinder(new CylinderSurface(
            Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)), 1, Direction3D.Create(new Vector3D(1, 0, 0)))));
        bindings.AddFaceBinding(new(face, surface)); return new(builder.Model, geometry, bindings, points);
        static Point3D Point(double u, double v) => new(1.03 * double.Cos(u), 1.03 * double.Sin(u), v);
    }
}
