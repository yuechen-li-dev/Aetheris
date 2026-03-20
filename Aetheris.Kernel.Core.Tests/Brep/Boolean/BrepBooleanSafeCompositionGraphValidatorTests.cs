using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Tests.Brep.Boolean;

public sealed class BrepBooleanSafeCompositionGraphValidatorTests
{
    [Fact]
    public void ValidateNextSubtract_CylinderThenCone_PassesAndAppendsHole()
    {
        var composition = new SafeBooleanComposition(
            new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d),
            [
                new SupportedBooleanHole(
                    "hole_a",
                    new AnalyticSurface(
                        AnalyticSurfaceKind.Cylinder,
                        Cylinder: new RecognizedCylinder(
                            new Point3D(-7d, -2d, -4d),
                            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
                            0d,
                            20d,
                            4d)),
                    -7d,
                    -2d,
                    4d,
                    4d)
            ]);
        var next = new AnalyticSurface(
            AnalyticSurfaceKind.Cone,
            Cone: new RecognizedCone(
                new Point3D(8d, 0d, -4d),
                Direction3D.Create(new Vector3D(0d, 0d, 1d)),
                0d,
                20d,
                System.Math.Atan(0.25d),
                3d,
                5d));

        var result = BrepBooleanSafeCompositionGraphValidator.TryValidateNextSubtract(
            composition,
            next,
            ToleranceContext.Default,
            out var updated,
            out var diagnostic);

        Assert.True(result);
        Assert.Null(diagnostic);
        Assert.Equal(2, updated.Holes.Count);
        Assert.Equal(AnalyticSurfaceKind.Cone, updated.Holes[1].Surface.Kind);
    }

    [Fact]
    public void ValidateNextSubtract_OverlappingHole_FailsDeterministically()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var firstHole = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 6d)));
        var firstResult = BrepBoolean.Subtract(baseBox, firstHole);
        Assert.True(firstResult.IsSuccess);

        var overlap = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(6d, 0d, 6d)));
        var recognized = BrepBooleanAnalyticSurfaceRecognition.TryRecognizeCylinder(overlap, ToleranceContext.Default, out var surface, out _);
        Assert.True(recognized);

        var result = BrepBooleanSafeCompositionGraphValidator.TryValidateNextSubtract(
            firstResult.Value.SafeBooleanComposition!,
            surface,
            ToleranceContext.Default,
            out _,
            out var diagnostic);

        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(BooleanDiagnosticCode.HoleInterference, diagnostic!.Code);
        Assert.Equal("BrepBoolean.AnalyticHole.HoleInterference", diagnostic.Source);
        Assert.Contains("previously accepted hole", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateNextSubtract_Sphere_FailsWithUnsupportedSurfaceDiagnostic()
    {
        var composition = new SafeBooleanComposition(
            new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d),
            []);
        var sphere = new AnalyticSurface(
            AnalyticSurfaceKind.Sphere,
            Sphere: new RecognizedSphere(Point3D.Origin, 3d));

        var result = BrepBooleanSafeCompositionGraphValidator.TryValidateNextSubtract(
            composition,
            sphere,
            ToleranceContext.Default,
            out _,
            out var diagnostic);

        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(BooleanDiagnosticCode.UnsupportedAnalyticSurfaceKind, diagnostic!.Code);
        Assert.Equal("BrepBoolean.UnsupportedAnalyticSurfaceKind", diagnostic.Source);
        Assert.Equal("Boolean Subtract does not support analytic tool surface kind 'Sphere' in the safe boolean family. Use a cylinder or cone through-hole instead.", diagnostic.Message);
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

        var vertexPoints = body.Topology.Vertices.ToDictionary(
            vertex => vertex.Id,
            vertex => transform.Apply(body.TryGetVertexPoint(vertex.Id, out var point) ? point : Point3D.Origin));

        return new BrepBody(body.Topology, geometry, body.Bindings, vertexPoints, body.SafeBooleanComposition);
    }
}
