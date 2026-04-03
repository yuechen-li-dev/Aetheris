using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Brep.Queries;

public sealed class AnalyticDisplayQueryTests
{
    [Fact]
    public void PlanarFace_BoxFace_HitMissAndNormal()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var faceId = box.Topology.Faces.OrderBy(f => f.Id.Value).First().Id;

        var hitRay = new Ray3D(new Point3D(0d, 0d, -3d), Direction3D.Create(new Vector3D(0d, 0d, 1d)));
        var missRay = new Ray3D(new Point3D(3d, 3d, -3d), Direction3D.Create(new Vector3D(0d, 0d, 1d)));

        var hit = AnalyticDisplayQuery.TryIntersectFace(box, faceId, hitRay, out var faceHit);
        var miss = AnalyticDisplayQuery.TryIntersectFace(box, faceId, missRay, out _);

        Assert.True(hit);
        Assert.False(miss);
        Assert.Equal(2d, faceHit.Distance, 12);
        Assert.Equal(new Point3D(0d, 0d, -1d), faceHit.Position);
        Assert.Equal(-1d, faceHit.Normal.Z, 12);
    }

    [Fact]
    public void Sphere_HitMissAndNormal()
    {
        var sphere = BrepPrimitives.CreateSphere(2d).Value;
        var faceId = sphere.Topology.Faces.Single().Id;

        var hitRay = new Ray3D(new Point3D(-5d, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var missRay = new Ray3D(new Point3D(-5d, 0d, 0d), Direction3D.Create(new Vector3D(0d, 1d, 0d)));

        Assert.True(AnalyticDisplayQuery.TryIntersectFace(sphere, faceId, hitRay, out var hit));
        Assert.False(AnalyticDisplayQuery.TryIntersectFace(sphere, faceId, missRay, out _));

        Assert.Equal(3d, hit.Distance, 12);
        Assert.Equal(new Point3D(-2d, 0d, 0d), hit.Position);
        Assert.Equal(-1d, hit.Normal.X, 12);
    }

    [Fact]
    public void Cylinder_Side_HitMissAndNormal()
    {
        var cylinder = BrepPrimitives.CreateCylinder(2d, 6d).Value;
        var sideFaceId = FindFaceByKind(cylinder, Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cylinder);

        var hitRay = new Ray3D(new Point3D(3d, 0d, 0d), Direction3D.Create(new Vector3D(-1d, 0d, 0d)));
        var missRay = new Ray3D(new Point3D(3d, 0d, 4d), Direction3D.Create(new Vector3D(-1d, 0d, 0d)));

        Assert.True(AnalyticDisplayQuery.TryIntersectFace(cylinder, sideFaceId, hitRay, out var hit));
        Assert.False(AnalyticDisplayQuery.TryIntersectFace(cylinder, sideFaceId, missRay, out _));

        Assert.Equal(1d, hit.Distance, 12);
        Assert.Equal(new Point3D(2d, 0d, 0d), hit.Position);
        Assert.Equal(1d, hit.Normal.X, 12);
    }

    [Fact]
    public void Cone_Side_HitMissAndNormal()
    {
        var cone = BrepRevolve.Create(
            [new ProfilePoint2D(1d, 0d), new ProfilePoint2D(3d, 4d)],
            new ExtrudeFrame3D(Point3D.Origin, Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d))),
            new RevolveAxis3D(Point3D.Origin, new Vector3D(0d, 0d, 1d))).Value;

        var sideFaceId = FindFaceByKind(cone, Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cone);

        var hitRay = new Ray3D(new Point3D(5d, 0d, 2d), Direction3D.Create(new Vector3D(-1d, 0d, 0d)));
        var missRay = new Ray3D(new Point3D(5d, 0d, -1d), Direction3D.Create(new Vector3D(-1d, 0d, 0d)));

        Assert.True(AnalyticDisplayQuery.TryIntersectFace(cone, sideFaceId, hitRay, out var hit));
        Assert.False(AnalyticDisplayQuery.TryIntersectFace(cone, sideFaceId, missRay, out _));

        Assert.True(hit.Normal.X > 0d);
        Assert.True(hit.Normal.Z < 0d);
    }

    [Fact]
    public void Torus_HitAndMiss()
    {
        var torus = BrepPrimitives.CreateTorus(4d, 1d).Value;
        var torusFace = torus.Topology.Faces.Single().Id;

        var hitRay = new Ray3D(new Point3D(-8d, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var missRay = new Ray3D(new Point3D(0d, 0d, 0d), Direction3D.Create(new Vector3D(0d, 1d, 0d)));

        Assert.True(AnalyticDisplayQuery.TryIntersectFace(torus, torusFace, hitRay, out var hit));
        Assert.False(AnalyticDisplayQuery.TryIntersectFace(torus, torusFace, missRay, out _));

        Assert.True(hit.Distance > 0d);
        Assert.Equal(Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Torus, hit.SurfaceKind);
    }

    [Fact]
    public void BodyLevelNearestHit_BoxAndDeterminism()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var ray = new Ray3D(new Point3D(-5d, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        Assert.True(AnalyticDisplayQuery.TryIntersectBody(box, ray, out var first));
        Assert.True(AnalyticDisplayQuery.TryIntersectBody(box, ray, out var second));

        Assert.Equal(first.Distance, second.Distance, 12);
        Assert.Equal(first.Position, second.Position);
        Assert.Equal(first.FaceId, second.FaceId);
        Assert.Equal(4d, first.Distance, 12);
        Assert.Equal(new Point3D(-1d, 0d, 0d), first.Position);
    }

    [Fact]
    public void BodyLevelNearestHit_Sphere_PreservesIdentity()
    {
        var sphere = BrepPrimitives.CreateSphere(2d).Value;
        var ray = new Ray3D(new Point3D(-5d, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        Assert.True(AnalyticDisplayQuery.TryIntersectBody(sphere, ray, out var hit));

        Assert.Equal(3d, hit.Distance, 12);
        Assert.True(hit.BodyId.IsValid);
        Assert.True(hit.FaceId.IsValid);
    }

    [Fact]
    public void AnalyticPath_SupportsTorusWhereSpatialPrimitiveRaycastDoesNot()
    {
        var torus = BrepPrimitives.CreateTorus(4d, 1d).Value;
        var ray = new Ray3D(new Point3D(-8d, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        var spatial = BrepSpatialQueries.Raycast(torus, ray);
        var analytic = AnalyticDisplayQuery.TryIntersectBody(torus, ray, out var hit);

        Assert.False(spatial.IsSuccess);
        Assert.True(analytic);
        Assert.Equal(Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Torus, hit.SurfaceKind);
    }

    private static Aetheris.Kernel.Core.Topology.FaceId FindFaceByKind(BrepBody body, Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind kind)
    {
        foreach (var face in body.Topology.Faces.OrderBy(f => f.Id.Value))
        {
            var binding = body.Bindings.GetFaceBinding(face.Id);
            if (body.Geometry.GetSurface(binding.SurfaceGeometryId).Kind == kind)
            {
                return face.Id;
            }
        }

        throw new InvalidOperationException($"Face kind {kind} not found.");
    }
}
