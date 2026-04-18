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
                    new Point3D(-7d, -2d, 0d),
                    new Point3D(-7d, -2d, 12d),
                    Direction3D.Create(new Vector3D(0d, 0d, 1d)),
                    Direction3D.Create(new Vector3D(1d, 0d, 0d)),
                    4d,
                    4d,
                    SupportedBooleanHoleSpanKind.Through,
                    0d,
                    12d)
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
    public void ValidateNextSubtract_ContainedSphere_PassesForSingleContainedCavity()
    {
        var composition = new SafeBooleanComposition(
            new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d),
            []);
        var sphere = new AnalyticSurface(
            AnalyticSurfaceKind.Sphere,
            Sphere: new RecognizedSphere(new Point3D(0d, 0d, 6d), 3d));

        var result = BrepBooleanSafeCompositionGraphValidator.TryValidateNextSubtract(
            composition,
            sphere,
            ToleranceContext.Default,
            out _,
            out var diagnostic);

        Assert.True(result);
        Assert.Null(diagnostic);
    }

    [Fact]
    public void ValidateNextSubtract_IndependentHoleContinuationOnRecognizedRoot_RejectsUnsupportedAxisFamily()
    {
        var composition = new SafeBooleanComposition(
            new AxisAlignedBoxExtents(-30d, 30d, -15d, 15d, -4d, 44d),
            [
                new SupportedBooleanHole(
                    "hole_a",
                    new AnalyticSurface(
                        AnalyticSurfaceKind.Cylinder,
                        Cylinder: new RecognizedCylinder(
                            new Point3D(-15d, 0d, 0d),
                            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
                            0d,
                            20d,
                            3.5d)),
                    -15d,
                    0d,
                    new Point3D(-15d, 0d, -4d),
                    new Point3D(-15d, 0d, 44d),
                    Direction3D.Create(new Vector3D(0d, 0d, 1d)),
                    Direction3D.Create(new Vector3D(1d, 0d, 0d)),
                    3.5d,
                    3.5d,
                    SupportedBooleanHoleSpanKind.Through,
                    -4d,
                    44d)
            ],
            SafeBooleanRootDescriptor.FromBox(new AxisAlignedBoxExtents(-30d, 30d, -15d, 15d, -4d, 44d)),
            [new AxisAlignedBoxExtents(-30d, 30d, -15d, 15d, -4d, 4d), new AxisAlignedBoxExtents(22d, 30d, -15d, 15d, 4d, 44d)]);
        var next = new AnalyticSurface(
            AnalyticSurfaceKind.Cylinder,
            Cylinder: new RecognizedCylinder(
                new Point3D(26d, 0d, 18d),
                Direction3D.Create(new Vector3D(0.2d, 0d, 1d)),
                0d,
                20d,
                3.5d));

        var result = BrepBooleanSafeCompositionGraphValidator.TryValidateNextSubtract(
            composition,
            next,
            ToleranceContext.Default,
            out _,
            out var diagnostic);

        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.True(
            diagnostic!.Code is BooleanDiagnosticCode.NotFullySpanning or BooleanDiagnosticCode.TangentContact,
            diagnostic.Message);
        Assert.Equal("BrepBoolean.AnalyticHole.NotFullySpanning", diagnostic.Source);
        Assert.Contains("does not match the supported subtract span family", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateNextSubtract_SphereAfterExistingHole_FailsWithUnsupportedAnalyticSurfaceDiagnostic()
    {
        var composition = new SafeBooleanComposition(
            new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d),
            [
                new SupportedBooleanHole(
                    "hole_a",
                    new AnalyticSurface(
                        AnalyticSurfaceKind.Cylinder,
                        Cylinder: new RecognizedCylinder(
                            new Point3D(0d, 0d, -4d),
                            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
                            0d,
                            20d,
                            3d)),
                    0d,
                    0d,
                    new Point3D(0d, 0d, 0d),
                    new Point3D(0d, 0d, 12d),
                    Direction3D.Create(new Vector3D(0d, 0d, 1d)),
                    Direction3D.Create(new Vector3D(1d, 0d, 0d)),
                    3d,
                    3d,
                    SupportedBooleanHoleSpanKind.Through,
                    0d,
                    12d),
            ]);
        var sphere = new AnalyticSurface(
            AnalyticSurfaceKind.Sphere,
            Sphere: new RecognizedSphere(new Point3D(10d, 0d, 6d), 2d));

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
    }

    [Fact]
    public void ValidateNextSubtract_InvalidCoaxialContinuation_RemainsRejected()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-35d, 35d, -20d, 20d, -8d, 8d)).Value;
        var blindPocket = TransformBody(BrepPrimitives.CreateCylinder(7d, 6d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 5d)));
        var first = BrepBoolean.Subtract(baseBox, blindPocket);
        Assert.True(first.IsSuccess);

        var nonConformingThroughBody = BrepPrimitives.CreateCylinder(8.0d, 24d).Value;
        var recognized = BrepBooleanAnalyticSurfaceRecognition.TryRecognizeCylinder(
            nonConformingThroughBody,
            ToleranceContext.Default,
            out var next,
            out _);
        Assert.True(recognized);

        var result = BrepBooleanSafeCompositionGraphValidator.TryValidateNextSubtract(
            first.Value.SafeBooleanComposition!,
            next,
            ToleranceContext.Default,
            out _,
            out var diagnostic,
            "hole_b");

        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.True(
            diagnostic!.Code is BooleanDiagnosticCode.NotFullySpanning or BooleanDiagnosticCode.TangentContact or BooleanDiagnosticCode.UnsupportedBlindHoleComposition,
            diagnostic.Message);
    }

    [Fact]
    public void ValidateNextSubtract_BlindCoaxialStack_StillPasses()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-35d, 35d, -20d, 20d, -8d, 8d)).Value;
        var firstBlind = TransformBody(BrepPrimitives.CreateCylinder(7d, 6d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 5d)));
        var first = BrepBoolean.Subtract(baseBox, firstBlind);
        Assert.True(first.IsSuccess);

        var through = BrepPrimitives.CreateCylinder(3.5d, 24d).Value;
        var recognized = BrepBooleanAnalyticSurfaceRecognition.TryRecognizeCylinder(through, ToleranceContext.Default, out var toolSurface, out _);
        Assert.True(recognized);

        var result = BrepBooleanSafeCompositionGraphValidator.TryValidateNextSubtract(
            first.Value.SafeBooleanComposition!,
            toolSurface,
            ToleranceContext.Default,
            out var updated,
            out var diagnostic,
            "hole_through");

        Assert.True(result, diagnostic?.Message);
        Assert.Null(diagnostic);
        Assert.Equal(2, updated.Holes.Count);
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
