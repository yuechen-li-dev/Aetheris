using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class PcurveBoundaryLiftBoundTests
{
    private static BSplineSurfaceWithKnots Patch(bool flat = false, bool shifted = false) => new(3, 2,
        Enumerable.Range(0, 4).Select(i => (IReadOnlyList<Point3D>)Enumerable.Range(0, 3)
            .Select(j => flat ? new Point3D(i / 3d, j / 2d, 0)
                : new Point3D(10 + 15 * i + .2 * j * j, -7 + 9 * j + .3 * i * i, 4 + .7 * i * j)).ToArray()).ToArray(),
        "UNSPECIFIED", false, false, false, [4, 4], [3, 3], shifted ? [-4d, 3d] : [0d, 1d], shifted ? [2d, 7d] : [0d, 1d], "UNSPECIFIED");

    private static BSpline3Curve Curve(int degree, IReadOnlyList<Point3D> controls, double a = 0, double b = 1) =>
        new(degree, controls, [degree + 1, degree + 1], [a, b], "UNSPECIFIED", false, false, "UNSPECIFIED");

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(5)] [InlineData(8)]
    public void BoundCoversSupportedDegreesAtLargeWorldAndParameterOffsets(int degree)
    {
        var random = new Random(147 + degree);
        var net = Enumerable.Range(0, degree + 1).Select(_ => (IReadOnlyList<Point3D>)Enumerable.Range(0, degree + 1)
            .Select(_ => new Point3D(1e6 + random.NextDouble() * 20, -2e6 + random.NextDouble() * 20, 3e6 + random.NextDouble() * 20)).ToArray()).ToArray();
        var patch = new BSplineSurfaceWithKnots(degree, degree, net, "UNSPECIFIED", false, false, false,
            [degree + 1, degree + 1], [degree + 1, degree + 1], [1000d, 1001d], [-7d, -3d], "UNSPECIFIED");
        var curve = Curve(degree, net[0], -7, -3);
        var interval = new ParameterInterval(-7, -3);
        var pcurve = PcurveGeometry.Line(interval, new(1000, -7), new(1000, -3));
        Assert.True(BrepPcurveLiftBounds.TryBoundaryBound(SurfaceGeometry.FromBSplineSurfaceWithKnots(patch),
            CurveGeometry.FromBSpline(curve), pcurve, interval, out var bound));
        for (var i = 0; i <= 1000; i++)
        {
            var t = interval.Start + (interval.End - interval.Start) * i / 1000d;
            var uv = pcurve.Evaluate(t);
            Assert.True((patch.Evaluate(uv.U, uv.V) - curve.Evaluate(t)).Length <= bound);
        }
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    [InlineData(3, false)] [InlineData(3, true)]
    public void BoundCoversTheWholeTrimIncludingReversalAndRecoveredUvError(int side, bool reverse)
    {
        var patch = Patch(shifted: true); var fixedU = side < 2;
        var degree = fixedU ? patch.DegreeV : patch.DegreeU;
        var controls = Enumerable.Range(0, degree + 1).Select(i => fixedU
            ? patch.ControlPoints[side == 0 ? 0 : patch.DegreeU][i]
            : patch.ControlPoints[i][side == 2 ? 0 : patch.DegreeV]).ToArray();
        if (reverse) Array.Reverse(controls);
        controls[1] += new Vector3D(1e-7, -2e-8, 3e-8);
        var start = fixedU ? patch.DomainStartV : patch.DomainStartU;
        var end = fixedU ? patch.DomainEndV : patch.DomainEndU;
        var curve = Curve(degree, controls, start, end);
        var low = start + .17 * (end - start); var high = start + .89 * (end - start);
        var interval = new ParameterInterval(low, high);
        SurfaceParameterPoint Ideal(double t) => fixedU
            ? new(side == 0 ? patch.DomainStartU : patch.DomainEndU, reverse ? end - (t - start) : t)
            : new(reverse ? end - (t - start) : t, side == 2 ? patch.DomainStartV : patch.DomainEndV);
        var a = Ideal(interval.Start); var b = Ideal(interval.End);
        var pcurve = PcurveGeometry.Line(interval, new(a.U + 2e-10, a.V - 3e-10), new(b.U - 2e-10, b.V + 3e-10));
        Assert.True(BrepPcurveLiftBounds.TryBoundaryBound(SurfaceGeometry.FromBSplineSurfaceWithKnots(patch),
            CurveGeometry.FromBSpline(curve), pcurve, interval, out var bound));
        Assert.InRange(bound, 1e-7, 1e-5);
        for (var i = 0; i <= 4096; i++)
        {
            var t = interval.Start + (interval.End - interval.Start) * i / 4096d;
            var uv = pcurve.Evaluate(t);
            Assert.True((patch.Evaluate(uv.U, uv.V) - curve.Evaluate(t)).Length <= bound);
        }
    }

    [Fact]
    public void MatchingEndpointsAndMidpointDoNotCertifyAnOffSurfaceCurve()
    {
        var patch = Patch(flat: true);
        var curve = Curve(3, [new(0, 0, 0), new(1d / 3, 0, .01), new(2d / 3, 0, -.01), new(1, 0, 0)]);
        var interval = new ParameterInterval(0, 1);
        var pcurve = PcurveGeometry.Line(interval, new(0, 0), new(1, 0));
        foreach (var t in new[] { 0d, .5, 1d }) Assert.InRange(double.Abs(curve.Evaluate(t).Z), 0d, 1e-17);
        Assert.True(double.Abs(curve.Evaluate(.25).Z) > 1e-3);
        Assert.True(BrepPcurveLiftBounds.TryBoundaryBound(SurfaceGeometry.FromBSplineSurfaceWithKnots(patch),
            CurveGeometry.FromBSpline(curve), pcurve, interval, out var bound));
        Assert.True(bound > .01);
        var body = TrimmedPatch(patch, corrupt: true);
        var recovery = BrepPcurveRecovery.Populate(body.Topology, body.Geometry, body.Bindings);
        Assert.False(recovery.IsSuccess);
        Assert.Contains(recovery.Diagnostics, d => d.Code == "surf-pcurve-invalid");
    }

    [Fact]
    public void InteriorTrimKeepsDenseQualificationAndOffDomainOrRationalInputsCannotUseTheBound()
    {
        var patch = Patch(flat: true);
        var body = TrimmedPatch(patch, interior: true);
        var recovery = BrepPcurveRecovery.Populate(body.Topology, body.Geometry, body.Bindings);
        Assert.True(recovery.IsSuccess, string.Join("; ", recovery.Diagnostics));
        Assert.Equal(4, recovery.Count);
        Assert.All(body.Bindings.PcurveBindings, p => Assert.Equal(8193, p.Qualification!.LiftSamples));
        var interval = new ParameterInterval(0, 1);
        var edge = CurveGeometry.FromBSpline(Curve(3, patch.ControlPoints.Select(row => row[0]).ToArray()));
        var line = PcurveGeometry.Line(interval, new(0, 0), new(1, 0));
        var weights = patch.ControlPoints.Select(row => row.Select(_ => 1d).ToArray()).ToArray();
        weights[0][0] = 1.1;
        var rational = patch.WithWeights(weights);
        Assert.False(BrepPcurveLiftBounds.TryBoundaryBound(SurfaceGeometry.FromBSplineSurfaceWithKnots(rational), edge, line, interval, out _));
        Assert.False(BrepPcurveLiftBounds.TryBoundaryBound(SurfaceGeometry.FromBSplineSurfaceWithKnots(patch), edge, line, new(-1, 1), out _));
        var segmented = new BSpline3Curve(3, [new(0, 0, 0), new(.25, 0, 0), new(.5, 0, 0), new(.75, 0, 0), new(1, 0, 0)],
            [4, 1, 4], [0d, .5, 1d], "UNSPECIFIED", false, false, "UNSPECIFIED");
        Assert.False(BrepPcurveLiftBounds.TryBoundaryBound(SurfaceGeometry.FromBSplineSurfaceWithKnots(patch),
            CurveGeometry.FromBSpline(segmented), line, interval, out _));
    }

    [Fact]
    public void BoundaryRecoveryReportsABoundWithoutChangingTheRecoveredUvOrGeometry()
    {
        var body = TrimmedPatch(Patch(flat: true));
        var vertices = body.Topology.Vertices.Select(v => { Assert.True(body.TryGetVertexPoint(v.Id, out var point)); return (v.Id, point); }).ToArray(); var curves = body.Geometry.Curves.ToArray();
        var recovery = BrepPcurveRecovery.Populate(body.Topology, body.Geometry, body.Bindings);
        Assert.True(recovery.IsSuccess, string.Join("; ", recovery.Diagnostics));
        Assert.Equal(4, recovery.Count);
        Assert.All(body.Bindings.PcurveBindings, binding => {
            Assert.Equal(0, binding.Qualification!.LiftSamples);
            Assert.Equal("BezierBoundaryControlHull", binding.Qualification.RecoveryMethod);
            Assert.Equal(PcurveBindingOrigin.RecoveredSpline, binding.Qualification.Origin);
            Assert.InRange(binding.Qualification.MaximumLiftDeviationMillimetres, 0d, 1e-5);
            Assert.Equal(PcurveGeometryKind.Line, binding.Pcurve.Kind);
        });
        foreach (var (id, expected) in vertices) { Assert.True(body.TryGetVertexPoint(id, out var actual)); Assert.Equal(expected, actual); }
        Assert.Equal(curves, body.Geometry.Curves.ToArray());
        Assert.True(BrepPcurveValidator.Validate(body, 1e-5, requireEveryCoedge: true).IsValid);
    }

    private static BrepBody TrimmedPatch(BSplineSurfaceWithKnots patch, bool interior = false, bool corrupt = false)
    {
        var builder = new TopologyBuilder(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel();
        var low = interior ? .2 : 0d; var high = interior ? .8 : 1d;
        var uv = new[] { (low, low), (high, low), (high, high), (low, high) };
        var points = new Dictionary<VertexId, Point3D>();
        var vertices = uv.Select(p => { var id = builder.AddVertex(); points[id] = patch.Evaluate(p.Item1, p.Item2); return id; }).ToArray();
        var loop = builder.AllocateLoopId(); var uses = uv.Select(_ => builder.AllocateCoedgeId()).ToArray();
        for (var i = 0; i < 4; i++)
        {
            var j = (i + 1) % 4; var edge = builder.AddEdge(vertices[i], vertices[j]); var curveId = new CurveGeometryId(i + 1);
            var a = points[vertices[i]]; var b = points[vertices[j]];
            var degree = i % 2 == 0 ? patch.DegreeU : patch.DegreeV;
            var controls = Enumerable.Range(0, degree + 1).Select(k => a + (b - a) * (k / (double)degree)).ToArray();
            if (corrupt && i == 0) { controls[1] += new Vector3D(0, 0, .01); controls[2] += new Vector3D(0, 0, -.01); }
            geometry.AddCurve(curveId, CurveGeometry.FromBSpline(Curve(degree, controls)));
            bindings.AddEdgeBinding(new(edge, curveId, new ParameterInterval(0, 1)));
            builder.AddCoedge(new(uses[i], edge, loop, uses[j], uses[(i + 3) % 4], false));
        }
        builder.AddLoop(new Loop(loop, uses)); var face = builder.AddFace([loop]); builder.AddBody([builder.AddShell([face])]);
        var surface = new SurfaceGeometryId(1); geometry.AddSurface(surface, SurfaceGeometry.FromBSplineSurfaceWithKnots(patch));
        bindings.AddFaceBinding(new(face, surface));
        return new(builder.Model, geometry, bindings, points);
    }
}
