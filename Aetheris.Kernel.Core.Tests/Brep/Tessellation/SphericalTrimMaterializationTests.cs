using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class SphericalTrimMaterializationTests
{
    private static readonly SphereSurface Sphere = new(Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)), 2, Direction3D.Create(new Vector3D(1, 0, 0)));

    [Theory]
    [InlineData(0.0, 2)]
    [InlineData(0.5, 1)]
    public void EquatorialHemisphereAndLatitudeCap_MaterializeThroughBoundCoedges(double latitude, int arcs)
    {
        var body = CapBody(latitude, arcs);
        if (arcs == 1)
        {
            var recovered = BrepPcurveRecovery.Populate(body.Topology, body.Geometry, body.Bindings, 1e-3);
            Assert.True(recovered.IsSuccess, string.Join(";", recovered.Diagnostics.Select(d => d.Message)));
        }
        var mesh = BrepDisplayTessellator.Tessellate(body);
        Assert.True(mesh.IsSuccess, string.Join(";", mesh.Diagnostics.Select(d => d.Message)));
        var patch = Assert.Single(mesh.Value.FacePatches);
        Assert.NotEmpty(patch.TriangleIndices);
        Assert.All(patch.Positions, p => Assert.InRange(p.Z, 2 * double.Sin(latitude) - 1e-8, 2 + 1e-8));
        AssertExactSupportAndFinite(patch);
    }

    [Fact]
    public void SphericalBand_PreservesInnerLatitudeHole()
    {
        var patch = Materialize([Circle(.1), Circle(.6).Reverse().ToArray()]);
        AssertExactSupportAndFinite(patch);
        foreach (var (a, b, c) in Triangles(patch))
        {
            var middle = ((a - Point3D.Origin) + (b - Point3D.Origin) + (c - Point3D.Origin)) * (1d / 3d);
            var latitude = double.Asin(middle.Z / middle.Length);
            Assert.InRange(latitude, .1 - .01, .6 + .01);
        }
    }

    [Fact]
    public void LongitudeSeamCrossingTrim_StaysInItsSmallNativeDomain()
    {
        var patch = Materialize([Rectangle(5.8, 6.5, .1, .6)]);
        AssertExactSupportAndFinite(patch);
        Assert.All(patch.Positions, p => {
            var u = double.Atan2(p.Y, p.X); if (u < 0) u += 2 * double.Pi;
            if (u < 1) u += 2 * double.Pi;
            Assert.InRange(u, 5.8 - .01, 6.5 + .01);
        });
    }

    [Fact]
    public void PoleTouchingTrim_CollapsesRepeatedPoleSamplesWithoutInventingArea()
    {
        var patch = Materialize([Rectangle(0, .7, .8, double.Pi / 2)]);
        AssertExactSupportAndFinite(patch);
        Assert.All(patch.Positions, p => Assert.True(p.Z >= 2 * double.Sin(.8) - .01));
        Assert.Contains(patch.Positions, p => (p - new Point3D(0, 0, 2)).Length < 1e-8);
    }

    [Fact]
    public void TruePointBoundary_DoesNotBecomeASphericalCap()
    {
        var pole = Sphere.Evaluate(0, double.Pi / 2);
        var result = SphericalTrimTessellator.Tessellate(new FaceId(1), Sphere, [new[] { pole, pole, pole, pole }],
            0, true, DisplayTessellationOptions.Default, null);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("collapsed boundary"));
    }

    private static DisplayFaceMeshPatch Materialize(IReadOnlyList<IReadOnlyList<Point3D>> loops)
    {
        var result = SphericalTrimTessellator.Tessellate(new FaceId(1), Sphere, loops, 0, true, DisplayTessellationOptions.Default, null);
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        Assert.NotEmpty(result.Value.TriangleIndices);
        return result.Value;
    }

    private static Point3D[] Circle(double v) => Enumerable.Range(0, 65).Select(i => Sphere.Evaluate(2 * double.Pi * i / 64, v)).ToArray();

    private static Point3D[] Rectangle(double u0, double u1, double v0, double v1)
    {
        var points = new List<Point3D>();
        foreach (var (a, b) in new[] { ((u0, v0), (u1, v0)), ((u1, v0), (u1, v1)), ((u1, v1), (u0, v1)), ((u0, v1), (u0, v0)) })
            for (var i = 0; i < 24; i++) points.Add(Sphere.Evaluate(a.Item1 + (b.Item1 - a.Item1) * i / 24, a.Item2 + (b.Item2 - a.Item2) * i / 24));
        return points.ToArray();
    }

    private static BrepBody CapBody(double latitude, int arcCount)
    {
        var builder = new TopologyBuilder(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel();
        var points = new Dictionary<VertexId, Point3D>();
        var vertices = Enumerable.Range(0, arcCount).Select(i => { var id = builder.AddVertex(); points[id] = Sphere.Evaluate(2 * double.Pi * i / arcCount, latitude); return id; }).ToArray();
        var loop = builder.AllocateLoopId(); var uses = Enumerable.Range(0, arcCount).Select(_ => builder.AllocateCoedgeId()).ToArray();
        for (var i = 0; i < arcCount; i++)
        {
            var edge = builder.AddEdge(vertices[i], vertices[(i + 1) % arcCount]); var curve = new CurveGeometryId(i + 1);
            geometry.AddCurve(curve, CurveGeometry.FromCircle(new Circle3Curve(new Point3D(0, 0, 2 * double.Sin(latitude)),
                Direction3D.Create(new Vector3D(0, 0, 1)), 2 * double.Cos(latitude), Direction3D.Create(new Vector3D(1, 0, 0)))));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edge, curve, new ParameterInterval(2 * double.Pi * i / arcCount, 2 * double.Pi * (i + 1) / arcCount)));
            builder.AddCoedge(new Coedge(uses[i], edge, loop, uses[(i + 1) % arcCount], uses[(i + arcCount - 1) % arcCount], false));
        }
        builder.AddLoop(new Loop(loop, uses)); var face = builder.AddFace([loop]); builder.AddBody([builder.AddShell([face])]);
        var surface = new SurfaceGeometryId(1); geometry.AddSurface(surface, SurfaceGeometry.FromSphere(Sphere));
        bindings.AddFaceBinding(new FaceGeometryBinding(face, surface));
        bindings.AddFaceBoundaryRoleBinding(new FaceBoundaryRoleBinding(face, loop, FaceBoundaryRole.Outer));
        return new BrepBody(builder.Model, geometry, bindings, points);
    }

    private static void AssertExactSupportAndFinite(DisplayFaceMeshPatch patch)
    {
        Assert.All(patch.Positions, p => Assert.InRange((p - Point3D.Origin).Length, 2 - 1e-10, 2 + 1e-10));
        Assert.All(patch.Normals, n => Assert.True(double.IsFinite(n.Length) && n.Length > .99));
    }

    private static IEnumerable<(Point3D A, Point3D B, Point3D C)> Triangles(DisplayFaceMeshPatch patch)
    {
        for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
            yield return (patch.Positions[patch.TriangleIndices[i]], patch.Positions[patch.TriangleIndices[i + 1]], patch.Positions[patch.TriangleIndices[i + 2]]);
    }
}
