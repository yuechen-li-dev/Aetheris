using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242SphericalShellRoundtripTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(7.0364833422123, 5.6114082167286, 0)]
    [InlineData(-9.14, 3.19, 2.1)]
    public void ComplementaryHemispheres_RetainOutwardOrientationAcrossRepeatedSerialization(double x, double y, double z)
    {
        var center = new Point3D(x, y, z);
        var sphere = new SphereSurface(center, Direction3D.Create(new Vector3D(-.6234898018587, .781831482468, 0)),
            2.19931404045, Direction3D.Create(new Vector3D(.781831482468, .6234898018587, 0)));
        var body = CreateSplitSphere(sphere);
        for (var round = 0; round < 3; round++)
        {
            var export = Step242Exporter.ExportBody(body);
            Assert.True(export.IsSuccess, string.Join(";", export.Diagnostics.Select(d => d.Message)));
            var import = Step242Importer.ImportBody(export.Value);
            Assert.True(import.IsSuccess);
            body = import.Value;
            Assert.Equal(ImportQualificationStatus.Qualified, body.ImportQualification!.Status);
            Assert.All(body.Bindings.FaceBindings, f => Assert.True(f.Orientation.IsAlignedWithSurface));
            Assert.Equal(2, body.Topology.Faces.Count());
            Assert.True(BrepPcurveValidator.Validate(body, 1e-3, requireEveryCoedge: true).IsValid);
            var mesh = BrepDisplayTessellator.Tessellate(body).Value;
            Assert.Equal(2, mesh.FacePatches.Count);
            var signedVolume = 0d;
            foreach (var patch in mesh.FacePatches)
            {
                for (var i = 0; i < patch.TriangleIndices.Count; i += 3)
                {
                    var a = patch.Positions[patch.TriangleIndices[i]] - center;
                    var b = patch.Positions[patch.TriangleIndices[i + 1]] - center;
                    var c = patch.Positions[patch.TriangleIndices[i + 2]] - center;
                    var cross = (b - a).Cross(c - a);
                    Assert.True(cross.Dot(a + b + c) > 0, "Every triangle must face outward, on both hemispheres.");
                    signedVolume += a.Dot(b.Cross(c)) / 6;
                }
            }
            var exactVolume = 4 * double.Pi * double.Pow(sphere.Radius, 3) / 3;
            Assert.InRange(signedVolume, exactVolume * .94, exactVolume * 1.01);
        }
    }

    private static BrepBody CreateSplitSphere(SphereSurface sphere)
    {
        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel();
        var a = builder.AddVertex(); var b = builder.AddVertex();
        var edge1 = builder.AddEdge(a, b); var edge2 = builder.AddEdge(a, b);
        var circle = new Circle3Curve(sphere.Center, Direction3D.Create(-sphere.Axis.ToVector()), sphere.Radius, sphere.XAxis);
        var opposite = new Circle3Curve(sphere.Center, sphere.Axis, sphere.Radius, Direction3D.Create(-sphere.XAxis.ToVector()));
        // Opposite semicircles with a shared endpoint direction and a wrap-crossing
        // native interval reproduce the vendor topology without vendor source data.
        var interval = new ParameterInterval(4.7123889803847, 7.8539816339745);
        geometry.AddCurve(new(1), CurveGeometry.FromCircle(circle));
        geometry.AddCurve(new(2), CurveGeometry.FromCircle(opposite));
        bindings.AddEdgeBinding(new(edge1, new(1), interval));
        bindings.AddEdgeBinding(new(edge2, new(2), interval));
        var surface = new SurfaceGeometryId(1); geometry.AddSurface(surface, SurfaceGeometry.FromSphere(sphere));
        var faces = new List<FaceId>();
        foreach (var reverse in new[] { false, true })
        {
            var loop = builder.AllocateLoopId();
            var c1 = builder.AllocateCoedgeId(); var c2 = builder.AllocateCoedgeId();
            builder.AddCoedge(new(c1, reverse ? edge2 : edge1, loop, c2, c2, true));
            builder.AddCoedge(new(c2, reverse ? edge1 : edge2, loop, c1, c1, false));
            builder.AddLoop(new Loop(loop, [c1, c2]));
            var face = builder.AddFace([loop]); faces.Add(face);
            bindings.AddFaceBinding(new(face, surface));
            bindings.AddFaceBoundaryRoleBinding(new(face, loop, FaceBoundaryRole.Outer));
        }
        builder.AddBody([builder.AddShell(faces)]);
        var body = new BrepBody(builder.Model, geometry, bindings,
            new Dictionary<VertexId, Point3D> { [a] = circle.Evaluate(interval.Start), [b] = circle.Evaluate(interval.End) });
        Assert.True(BrepPcurveRecovery.Populate(body.Topology, geometry, bindings, 1e-3).IsSuccess);
        return body;
    }
}
