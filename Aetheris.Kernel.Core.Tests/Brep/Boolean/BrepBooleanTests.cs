using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Boolean;

public sealed class BrepBooleanTests
{
    [Fact]
    public void BoxRecognition_CreateBox_IsRecognizedAndExtentsExtracted()
    {
        var box = BrepPrimitives.CreateBox(4d, 6d, 8d).Value;

        var recognized = BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(box, ToleranceContext.Default, out var extents, out var reason);

        Assert.True(recognized, reason);
        Assert.Equal(-2d, extents.MinX);
        Assert.Equal(2d, extents.MaxX);
        Assert.Equal(-3d, extents.MinY);
        Assert.Equal(3d, extents.MaxY);
        Assert.Equal(-4d, extents.MinZ);
        Assert.Equal(4d, extents.MaxZ);
    }

    [Fact]
    public void BoxRecognition_NonBoxBody_IsRejected()
    {
        var sphere = BrepPrimitives.CreateSphere(2d).Value;

        var recognized = BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(sphere, ToleranceContext.Default, out _, out var reason);

        Assert.False(recognized);
        Assert.NotEmpty(reason);
    }

    [Fact]
    public void Intersect_OverlappingBoxes_ReturnsPositiveVolumeBox()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(0d, 4d, 0d, 4d, 0d, 4d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(2d, 6d, 1d, 3d, -1d, 2d)).Value;

        var result = BrepBoolean.Intersect(left, right);

        Assert.True(result.IsSuccess);
        Assert.True(BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(result.Value, ToleranceContext.Default, out var extents, out _));
        Assert.Equal(new AxisAlignedBoxExtents(2d, 4d, 1d, 3d, 0d, 2d), extents);
        Assert.True(BrepBindingValidator.Validate(result.Value, true).IsSuccess);
    }

    [Fact]
    public void Intersect_DisjointBoxes_ReturnsDeterministicNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(0d, 1d, 0d, 1d, 0d, 1d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(2d, 3d, 2d, 3d, 2d, 3d)).Value;

        var result = BrepBoolean.Intersect(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Intersect: empty intersection result is not representable in M13.", diagnostic.Message);
    }

    [Fact]
    public void Intersect_TouchingFaceOnly_ReturnsDeterministicNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(0d, 1d, 0d, 1d, 0d, 1d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(1d, 2d, 0d, 1d, 0d, 1d)).Value;

        var result = BrepBoolean.Intersect(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Intersect: touching-only intersection is non-solid and empty results are not representable in M13.", diagnostic.Message);
    }

    [Fact]
    public void Union_ContainingCase_ReturnsContainingBox()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-3d, 3d, -3d, 3d, -3d, 3d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-1d, 1d, -1d, 1d, -1d, 1d)).Value;

        var result = BrepBoolean.Union(left, right);

        Assert.True(result.IsSuccess);
        Assert.True(BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(result.Value, ToleranceContext.Default, out var extents, out _));
        Assert.Equal(new AxisAlignedBoxExtents(-3d, 3d, -3d, 3d, -3d, 3d), extents);
    }

    [Fact]
    public void Union_ExactSingleBoxCase_ReturnsCombinedBox()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(0d, 2d, 0d, 2d, 0d, 2d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(2d, 5d, 0d, 2d, 0d, 2d)).Value;

        var result = BrepBoolean.Union(left, right);

        Assert.True(result.IsSuccess);
        Assert.True(BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(result.Value, ToleranceContext.Default, out var extents, out _));
        Assert.Equal(new AxisAlignedBoxExtents(0d, 5d, 0d, 2d, 0d, 2d), extents);
    }

    [Fact]
    public void Union_LShapedOverlap_DoesNotReturnBoundingBox()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(0d, 2d, 0d, 4d, 0d, 2d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(2d, 4d, 0d, 2d, 0d, 2d)).Value;

        var result = BrepBoolean.Union(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Contains("not a single box", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Union_DisjointBoxes_ReturnsNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(0d, 1d, 0d, 1d, 0d, 1d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(3d, 4d, 3d, 4d, 3d, 4d)).Value;

        var result = BrepBoolean.Union(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Union: disjoint box union is multi-body and not supported in M13.", diagnostic.Message);
    }

    [Fact]
    public void Subtract_NoOverlap_ReturnsLeftBox()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-2d, 2d, -2d, 2d, -2d, 2d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(3d, 4d, 3d, 4d, 3d, 4d)).Value;

        var result = BrepBoolean.Subtract(left, right);

        Assert.True(result.IsSuccess);
        Assert.True(BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(result.Value, ToleranceContext.Default, out var extents, out _));
        Assert.Equal(new AxisAlignedBoxExtents(-2d, 2d, -2d, 2d, -2d, 2d), extents);
    }

    [Fact]
    public void Subtract_FullContainment_ReturnsEmptyNotRepresentableDiagnostic()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-1d, 1d, -1d, 1d, -1d, 1d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-3d, 3d, -3d, 3d, -3d, 3d)).Value;

        var result = BrepBoolean.Subtract(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Subtract: subtraction fully removes the left box and empty results are not representable in M13.", diagnostic.Message);
    }

    [Fact]
    public void Subtract_NonSingleBoxResult_ReturnsNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(0d, 4d, 0d, 4d, 0d, 4d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(1d, 3d, 1d, 3d, 0d, 4d)).Value;

        var result = BrepBoolean.Subtract(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Subtract: box subtraction result is not representable as a single box in M13.", diagnostic.Message);
    }

    [Fact]
    public void Subtract_SingleBoxClipCase_ReturnsSingleBox()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(0d, 6d, 0d, 2d, 0d, 2d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(3d, 6d, 0d, 2d, 0d, 2d)).Value;

        var result = BrepBoolean.Subtract(left, right);

        Assert.True(result.IsSuccess);
        Assert.True(BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(result.Value, ToleranceContext.Default, out var extents, out _));
        Assert.Equal(new AxisAlignedBoxExtents(0d, 3d, 0d, 2d, 0d, 2d), extents);
        Assert.True(BrepBindingValidator.Validate(result.Value, true).IsSuccess);
    }


    [Fact]
    public void Subtract_BoxCylinderThroughHole_Returns_ManifoldBodyWithCylindricalHole()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var right = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(3d, -2d, 6d)));

        var result = BrepBoolean.Subtract(left, right);

        Assert.True(result.IsSuccess);
        var body = result.Value;
        Assert.Single(body.Topology.Bodies);
        Assert.Single(body.Topology.Shells);
        Assert.Equal(7, body.Topology.Faces.Count());
        Assert.Equal(15, body.Topology.Edges.Count());
        Assert.Equal(12, body.Topology.Vertices.Count());
        Assert.True(BrepBindingValidator.Validate(body, true).IsSuccess);

        var cylindricalFace = Assert.Single(body.Topology.Faces, face =>
            body.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Kind == SurfaceGeometryKind.Cylinder);
        Assert.Single(body.GetLoopIds(cylindricalFace.Id));

        var topFace = Assert.Single(body.Topology.Faces, face =>
            body.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Kind == SurfaceGeometryKind.Plane
            && surface.Plane!.Value.Normal.Z > 0.5d);
        Assert.Equal(2, body.GetLoopIds(topFace.Id).Count);

        var bottomFace = Assert.Single(body.Topology.Faces, face =>
            body.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Kind == SurfaceGeometryKind.Plane
            && surface.Plane!.Value.Normal.Z < -0.5d);
        Assert.Equal(2, body.GetLoopIds(bottomFace.Id).Count);
    }

    [Fact]
    public void Subtract_BoxCylinderThroughHole_Exports_And_RoundTrips_Deterministically()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var right = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(3d, -2d, 6d)));

        var first = BrepBoolean.Subtract(left, right);
        var second = BrepBoolean.Subtract(left, right);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var firstExport = Step242Exporter.ExportBody(first.Value);
        var secondExport = Step242Exporter.ExportBody(second.Value);
        Assert.True(firstExport.IsSuccess);
        Assert.True(secondExport.IsSuccess);
        Assert.Equal(firstExport.Value, secondExport.Value);
        Assert.Contains("MANIFOLD_SOLID_BREP", firstExport.Value, StringComparison.Ordinal);
        Assert.Contains("CYLINDRICAL_SURFACE", firstExport.Value, StringComparison.Ordinal);
        Assert.Contains("FACE_BOUND('',", firstExport.Value, StringComparison.Ordinal);
        Assert.Contains("FACE_OUTER_BOUND('',", firstExport.Value, StringComparison.Ordinal);

        var import = Step242Importer.ImportBody(firstExport.Value);
        Assert.True(import.IsSuccess);
        Assert.True(BrepBindingValidator.Validate(import.Value, true).IsSuccess);
        Assert.Contains(import.Value.Topology.Faces, face =>
            import.Value.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Kind == SurfaceGeometryKind.Cylinder);
    }

    [Fact]
    public void Subtract_BoxCylinderRotatedAxis_ReturnsDeterministicNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var rotated = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateRotationX(System.Math.PI / 2d) * Transform3D.CreateTranslation(new Vector3D(0d, 0d, 6d)));

        var result = BrepBoolean.Subtract(left, rotated);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Contains("strict Z-aligned through-hole subset", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("cylinder axis is not aligned with the box Z axis", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_BoxCylinderPartialHeight_ReturnsDeterministicNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var partial = TransformBody(BrepPrimitives.CreateCylinder(4d, 6d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 6d)));

        var result = BrepBoolean.Subtract(left, partial);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Contains("strict Z-aligned through-hole subset", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("does not fully span the box Z range", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_BoxCylinderOutsideFootprint_ReturnsDeterministicNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var outside = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(30d, 0d, 6d)));

        var result = BrepBoolean.Subtract(left, outside);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Contains("strict Z-aligned through-hole subset", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("strictly inside the box XY footprint", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Add_BoxCylinder_StillReturnsDeterministicNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var right = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 6d)));

        var result = BrepBoolean.Union(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Union: M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", diagnostic.Message);
    }

    [Fact]
    public void Intersect_BoxCylinder_StillReturnsDeterministicNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var right = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 6d)));

        var result = BrepBoolean.Intersect(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Intersect: M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", diagnostic.Message);
    }


    [Fact]
    public void Subtract_BoxSphereFullyContained_StillReturnsDeterministicNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, -6d, 6d)).Value;
        var right = BrepPrimitives.CreateSphere(4d).Value;

        var result = BrepBoolean.Subtract(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Subtract: M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", diagnostic.Message);
    }

    [Fact]
    public void Union_BoxSphereOverlap_StillReturnsDeterministicNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, -6d, 6d)).Value;
        var right = BrepPrimitives.CreateSphere(4d).Value;

        var result = BrepBoolean.Union(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Union: M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", diagnostic.Message);
    }

    [Fact]
    public void Intersect_BoxSphereOverlap_StillReturnsDeterministicNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, -6d, 6d)).Value;
        var right = BrepPrimitives.CreateSphere(4d).Value;

        var result = BrepBoolean.Intersect(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Intersect: M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", diagnostic.Message);
    }

    [Fact]
    public void Subtract_BoxPointedCone_StillReturnsDeterministicNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var right = CreateCone(bottomRadius: 6d, topRadius: 0d, height: 20d);

        var result = BrepBoolean.Subtract(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Subtract: M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", diagnostic.Message);
    }

    [Fact]
    public void Subtract_BoxFrustumCone_StillReturnsDeterministicNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var right = CreateCone(bottomRadius: 6d, topRadius: 2d, height: 20d);

        var result = BrepBoolean.Subtract(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Subtract: M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", diagnostic.Message);
    }

    [Fact]
    public void Union_BoxConeOverlap_StillReturnsDeterministicNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var right = CreateCone(bottomRadius: 6d, topRadius: 0d, height: 20d);

        var result = BrepBoolean.Union(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Union: M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", diagnostic.Message);
    }

    [Fact]
    public void Intersect_BoxConeOverlap_StillReturnsDeterministicNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var right = CreateCone(bottomRadius: 6d, topRadius: 0d, height: 20d);

        var result = BrepBoolean.Intersect(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Intersect: M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", diagnostic.Message);
    }

    [Fact]
    public void Execute_NonBoxInput_ReturnsDeterministicNotImplementedWithoutThrowing()
    {
        var left = BrepPrimitives.CreateCylinder(1d, 2d).Value;
        var right = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;

        var exception = Record.Exception(() => BrepBoolean.Union(left, right));
        Assert.Null(exception);

        var result = BrepBoolean.Union(left, right);
        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Union: M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", diagnostic.Message);
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
                _ => surfaceEntry.Value
            });
        }

        var vertexPoints = new Dictionary<VertexId, Point3D>();
        foreach (var vertex in body.Topology.Vertices)
        {
            if (body.TryGetVertexPoint(vertex.Id, out var point))
            {
                vertexPoints[vertex.Id] = transform.Apply(point);
            }
        }

        return new BrepBody(body.Topology, geometry, body.Bindings, vertexPoints);
    }

}
