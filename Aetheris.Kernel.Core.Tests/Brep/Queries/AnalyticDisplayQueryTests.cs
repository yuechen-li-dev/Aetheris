using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
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
    public void PlanarFace_RotatedBoxSide_RejectsOutOfDomainPlaneHit()
    {
        var rotated = TransformBody(BrepPrimitives.CreateBox(2d, 2d, 2d).Value, Transform3D.CreateRotationZ(double.Pi / 4d));
        var sideFaceId = rotated.Topology.Faces
            .Select(face => face.Id)
            .First(faceId =>
            {
                var surface = rotated.GetFaceSurface(faceId);
                return surface.Kind == SurfaceGeometryKind.Plane
                    && surface.Plane is { } plane
                    && double.Abs(plane.Normal.Z) < 0.1d;
            });

        var plane = rotated.GetFaceSurface(sideFaceId).Plane!.Value;
        var outsideOnPlane = plane.Origin + (plane.VAxis.ToVector() * 5d);
        var ray = new Ray3D(outsideOnPlane - (plane.Normal.ToVector() * 3d), Direction3D.Create(plane.Normal.ToVector()));

        Assert.False(AnalyticDisplayQuery.TryIntersectFace(rotated, sideFaceId, ray, out _));
    }

    [Fact]
    public void PlanarFace_CircularCap_StillUsesAnalyticDomain()
    {
        var cylinder = BrepPrimitives.CreateCylinder(2d, 6d).Value;
        var capFaceId = cylinder.Topology.Faces
            .Select(face => face.Id)
            .First(faceId => cylinder.GetFaceSurface(faceId).Kind == SurfaceGeometryKind.Plane);

        var hitRay = new Ray3D(new Point3D(0d, 0d, 10d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));
        var missRay = new Ray3D(new Point3D(3d, 0d, 10d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));

        Assert.True(AnalyticDisplayQuery.TryIntersectFace(cylinder, capFaceId, hitRay, out _));
        Assert.False(AnalyticDisplayQuery.TryIntersectFace(cylinder, capFaceId, missRay, out _));
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

    [Fact]
    public void BodyLevelNearestHit_BoxCylinderThroughHole_QueriesExteriorAndHoleSurface()
    {
        var body = CreateBoxCylinderThroughHole();

        var exteriorRay = new Ray3D(new Point3D(-30d, 0d, 6d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        Assert.True(AnalyticDisplayQuery.TryIntersectBody(body, exteriorRay, out var exteriorHit));
        Assert.Equal(SurfaceGeometryKind.Plane, exteriorHit.SurfaceKind);

        var openingRay = new Ray3D(new Point3D(3d, -2d, 20d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));
        Assert.False(AnalyticDisplayQuery.TryIntersectBody(body, openingRay, out _));

        var holeInteriorRay = new Ray3D(new Point3D(3d, -2d, 6d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        Assert.True(AnalyticDisplayQuery.TryIntersectBody(body, holeInteriorRay, out var holeHit));
        Assert.Equal(SurfaceGeometryKind.Cylinder, holeHit.SurfaceKind);
        Assert.True(holeHit.Normal.X > 0d);

        Assert.True(AnalyticDisplayQuery.TryIntersectBody(body, holeInteriorRay, out var holeHitRepeat));
        Assert.Equal(holeHit.Distance, holeHitRepeat.Distance, 12);
        Assert.Equal(holeHit.FaceId, holeHitRepeat.FaceId);

        var spatial = BrepSpatialQueries.Raycast(body, openingRay);
        Assert.False(spatial.IsSuccess);
    }

    [Fact]
    public void BodyLevelNearestHit_BoxConeThroughHole_QueriesExteriorOpeningAndConicalSide()
    {
        var body = CreateBoxConeThroughHole();

        var exteriorRay = new Ray3D(new Point3D(-30d, 0d, 6d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        Assert.True(AnalyticDisplayQuery.TryIntersectBody(body, exteriorRay, out var exteriorHit));
        Assert.Equal(SurfaceGeometryKind.Plane, exteriorHit.SurfaceKind);

        var openingRay = new Ray3D(new Point3D(0d, 0d, 20d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));
        Assert.False(AnalyticDisplayQuery.TryIntersectBody(body, openingRay, out _));

        var sideRay = new Ray3D(new Point3D(0d, 0d, 6d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        Assert.True(AnalyticDisplayQuery.TryIntersectBody(body, sideRay, out var sideHit));
        Assert.Equal(SurfaceGeometryKind.Cone, sideHit.SurfaceKind);

        Assert.True(AnalyticDisplayQuery.TryIntersectBody(body, sideRay, out var sideHitRepeat));
        Assert.Equal(sideHit.Distance, sideHitRepeat.Distance, 12);
        Assert.Equal(sideHit.FaceId, sideHitRepeat.FaceId);
    }

    [Fact]
    public void BodyLevelNearestHit_BoxSphereCavity_DoesNotExposeCavityToExteriorRays()
    {
        var body = CreateBoxSphereCavity();

        var exteriorRay = new Ray3D(new Point3D(-30d, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        Assert.True(AnalyticDisplayQuery.TryIntersectBody(body, exteriorRay, out var exteriorHit));
        Assert.Equal(SurfaceGeometryKind.Plane, exteriorHit.SurfaceKind);

        var innerRay = new Ray3D(new Point3D(0d, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        Assert.True(AnalyticDisplayQuery.TryIntersectBody(body, innerRay, out var innerHit));
        Assert.Equal(SurfaceGeometryKind.Sphere, innerHit.SurfaceKind);
    }

    [Fact]
    public void BodyLevelNearestHit_BoxBooleanRegression_StillResolvesNearestHit()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-2d, 2d, -2d, 2d, -2d, 2d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(1d, 3d, -2d, 2d, -2d, 2d)).Value;
        var result = BrepBoolean.Intersect(left, right);
        Assert.True(result.IsSuccess);

        var ray = new Ray3D(new Point3D(-5d, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        Assert.True(AnalyticDisplayQuery.TryIntersectBody(result.Value, ray, out var hit));
        Assert.Equal(SurfaceGeometryKind.Plane, hit.SurfaceKind);
        Assert.Equal(1d, hit.Position.X, 12);
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

    private static BrepBody CreateBoxCylinderThroughHole()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var right = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(3d, -2d, 6d)));
        var result = BrepBoolean.Subtract(left, right);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static BrepBody CreateBoxConeThroughHole()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-12d, 12d, -10d, 10d, 0d, 10d)).Value;
        var right = CreateCone(bottomRadius: 6d, topRadius: 0d, height: 20d);
        var result = BrepBoolean.Subtract(left, right);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static BrepBody CreateBoxSphereCavity()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-10d, 10d, -10d, 10d, -10d, 10d)).Value;
        var right = TransformBody(BrepPrimitives.CreateSphere(6d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 0d)));
        var result = BrepBoolean.Subtract(left, right);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static BrepBody CreateCone(double bottomRadius, double topRadius, double height)
    {
        var frame = new ExtrudeFrame3D(
            origin: Point3D.Origin,
            normal: Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            uAxis: Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var axis = new RevolveAxis3D(Point3D.Origin, new Vector3D(0d, 0d, 1d));

        var result = BrepRevolve.Create(
            [
                new ProfilePoint2D(bottomRadius, 0d),
                new ProfilePoint2D(topRadius, height)
            ],
            frame,
            axis);

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static BrepBody TransformBody(BrepBody body, Transform3D transform)
    {
        var geometry = new BrepGeometryStore();
        foreach (var curveEntry in body.Geometry.Curves)
        {
            geometry.AddCurve(curveEntry.Key, curveEntry.Value.Kind switch
            {
                CurveGeometryKind.Line3 => CurveGeometry.FromLine(new Line3Curve(
                    transform.Apply(curveEntry.Value.Line3!.Value.Origin),
                    transform.Apply(curveEntry.Value.Line3.Value.Direction))),
                CurveGeometryKind.Circle3 => CurveGeometry.FromCircle(new Circle3Curve(
                    transform.Apply(curveEntry.Value.Circle3!.Value.Center),
                    transform.Apply(curveEntry.Value.Circle3.Value.Normal),
                    curveEntry.Value.Circle3.Value.Radius,
                    transform.Apply(curveEntry.Value.Circle3.Value.XAxis))),
                _ => curveEntry.Value
            });
        }

        foreach (var surfaceEntry in body.Geometry.Surfaces)
        {
            geometry.AddSurface(surfaceEntry.Key, surfaceEntry.Value.Kind switch
            {
                SurfaceGeometryKind.Plane => SurfaceGeometry.FromPlane(new PlaneSurface(
                    transform.Apply(surfaceEntry.Value.Plane!.Value.Origin),
                    transform.Apply(surfaceEntry.Value.Plane.Value.Normal),
                    transform.Apply(surfaceEntry.Value.Plane.Value.UAxis))),
                SurfaceGeometryKind.Cylinder => SurfaceGeometry.FromCylinder(new CylinderSurface(
                    transform.Apply(surfaceEntry.Value.Cylinder!.Value.Origin),
                    transform.Apply(surfaceEntry.Value.Cylinder.Value.Axis),
                    surfaceEntry.Value.Cylinder.Value.Radius,
                    transform.Apply(surfaceEntry.Value.Cylinder.Value.XAxis))),
                SurfaceGeometryKind.Cone => SurfaceGeometry.FromCone(new ConeSurface(
                    transform.Apply(surfaceEntry.Value.Cone!.Value.PlacementOrigin),
                    transform.Apply(surfaceEntry.Value.Cone.Value.Axis),
                    surfaceEntry.Value.Cone.Value.PlacementRadius,
                    surfaceEntry.Value.Cone.Value.SemiAngleRadians,
                    transform.Apply(surfaceEntry.Value.Cone.Value.ReferenceAxis))),
                SurfaceGeometryKind.Torus => SurfaceGeometry.FromTorus(new TorusSurface(
                    transform.Apply(surfaceEntry.Value.Torus!.Value.Center),
                    transform.Apply(surfaceEntry.Value.Torus.Value.Axis),
                    surfaceEntry.Value.Torus.Value.MajorRadius,
                    surfaceEntry.Value.Torus.Value.MinorRadius,
                    transform.Apply(surfaceEntry.Value.Torus.Value.XAxis))),
                SurfaceGeometryKind.Sphere => SurfaceGeometry.FromSphere(new SphereSurface(
                    transform.Apply(surfaceEntry.Value.Sphere!.Value.Center),
                    transform.Apply(surfaceEntry.Value.Sphere.Value.Axis),
                    surfaceEntry.Value.Sphere.Value.Radius,
                    transform.Apply(surfaceEntry.Value.Sphere.Value.XAxis))),
                _ => surfaceEntry.Value
            });
        }

        var vertexPoints = new Dictionary<Aetheris.Kernel.Core.Topology.VertexId, Point3D>();
        foreach (var vertex in body.Topology.Vertices)
        {
            if (body.TryGetVertexPoint(vertex.Id, out var point))
            {
                vertexPoints[vertex.Id] = transform.Apply(point);
            }
        }

        return new BrepBody(body.Topology, geometry, body.Bindings, vertexPoints, body.SafeBooleanComposition, body.ShellRepresentation);
    }
}
