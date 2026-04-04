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
    public void Union_LShapedOverlap_Rebuilds_BoundedOrthogonalUnion()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(0d, 2d, 0d, 4d, 0d, 2d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(2d, 4d, 0d, 2d, 0d, 2d)).Value;

        var result = BrepBoolean.Union(left, right);

        Assert.True(result.IsSuccess);
        Assert.False(BrepBooleanBoxRecognition.TryRecognizeAxisAlignedBox(result.Value, ToleranceContext.Default, out _, out _));
        var validation = BrepBindingValidator.Validate(result.Value, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);
    }

    [Fact]
    public void Union_RibWallLikeAdd_Rebuilds_BoundedOrthogonalUnion()
    {
        var basePlate = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(0d, 6d, 0d, 4d, 0d, 1d)).Value;
        var ribWall = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(2d, 4d, 0d, 4d, 1d, 4d)).Value;

        var result = BrepBoolean.Union(basePlate, ribWall);

        Assert.True(result.IsSuccess);
        var validation = BrepBindingValidator.Validate(result.Value, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);
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
    public void Union_ConnectedButOutsideBoundedFamily_ReturnsNotImplemented()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(0d, 3d, 0d, 2d, 0d, 2d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(1d, 4d, 1d, 3d, 1d, 3d)).Value;

        var result = BrepBoolean.Union(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Contains("bounded F1 additive family", diagnostic.Message, StringComparison.Ordinal);
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
    public void Subtract_CylinderRootCenterBore_Exports_And_RoundTrips_Deterministically()
    {
        var root = BrepPrimitives.CreateCylinder(40d, 12d).Value;
        var bore = BrepPrimitives.CreateCylinder(12d, 24d).Value;

        var first = BrepBoolean.Subtract(root, bore);
        var second = BrepBoolean.Subtract(root, bore);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(BrepBindingValidator.Validate(first.Value, true).IsSuccess);

        var firstExport = Step242Exporter.ExportBody(first.Value);
        var secondExport = Step242Exporter.ExportBody(second.Value);
        Assert.True(firstExport.IsSuccess);
        Assert.True(secondExport.IsSuccess);
        Assert.Equal(firstExport.Value, secondExport.Value);
        Assert.Contains("BREP_WITH_VOIDS", firstExport.Value, StringComparison.Ordinal);
        Assert.Equal(6, first.Value.Topology.Faces.Count());
    }

    [Fact]
    public void Subtract_CylinderRootCenterBoreAndOffsetHole_Succeeds()
    {
        var root = BrepPrimitives.CreateCylinder(40d, 12d).Value;
        var centerBore = BrepPrimitives.CreateCylinder(12d, 24d).Value;
        var offsetHole = TransformBody(BrepPrimitives.CreateCylinder(4d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(25d, 0d, 0d)));

        var withCenterBore = BrepBoolean.Subtract(root, centerBore);
        Assert.True(withCenterBore.IsSuccess);

        var result = BrepBoolean.Subtract(withCenterBore.Value, offsetHole);

        Assert.True(result.IsSuccess);
        Assert.True(BrepBindingValidator.Validate(result.Value, true).IsSuccess);
        Assert.Equal(9, result.Value.Topology.Faces.Count());
        Assert.NotNull(result.Value.ShellRepresentation);
        Assert.Equal(2, result.Value.ShellRepresentation!.InnerShellIds.Count);
        var export = Step242Exporter.ExportBody(result.Value);
        Assert.True(export.IsSuccess);
        Assert.Contains("BREP_WITH_VOIDS", export.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_CylinderRootCenterBoreAndBoltCircle_SucceedsDeterministically()
    {
        var root = BrepPrimitives.CreateCylinder(40d, 12d).Value;
        var centerBore = BrepPrimitives.CreateCylinder(12d, 24d).Value;
        var hole0 = TransformBody(BrepPrimitives.CreateCylinder(4d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(25d, 0d, 0d)));
        var hole90 = TransformBody(BrepPrimitives.CreateCylinder(4d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 25d, 0d)));
        var hole180 = TransformBody(BrepPrimitives.CreateCylinder(4d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(-25d, 0d, 0d)));
        var hole270 = TransformBody(BrepPrimitives.CreateCylinder(4d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(0d, -25d, 0d)));

        var first = BrepBoolean.Subtract(root, centerBore).Value;
        first = BrepBoolean.Subtract(first, hole0).Value;
        first = BrepBoolean.Subtract(first, hole90).Value;
        first = BrepBoolean.Subtract(first, hole180).Value;
        var firstResult = BrepBoolean.Subtract(first, hole270);

        var second = BrepBoolean.Subtract(root, centerBore).Value;
        second = BrepBoolean.Subtract(second, hole0).Value;
        second = BrepBoolean.Subtract(second, hole90).Value;
        second = BrepBoolean.Subtract(second, hole180).Value;
        var secondResult = BrepBoolean.Subtract(second, hole270);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.True(BrepBindingValidator.Validate(firstResult.Value, true).IsSuccess);
        Assert.Equal(5, firstResult.Value.ShellRepresentation!.InnerShellIds.Count);
        var firstExport = Step242Exporter.ExportBody(firstResult.Value);
        var secondExport = Step242Exporter.ExportBody(secondResult.Value);
        Assert.True(firstExport.IsSuccess);
        Assert.True(secondExport.IsSuccess);
        Assert.Equal(firstExport.Value, secondExport.Value);
    }

    [Fact]
    public void Subtract_CylinderRootOffsetHoleThenCenterBore_Succeeds()
    {
        var root = BrepPrimitives.CreateCylinder(40d, 12d).Value;
        var offsetHole = TransformBody(BrepPrimitives.CreateCylinder(4d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(25d, 0d, 0d)));
        var centerBore = BrepPrimitives.CreateCylinder(12d, 24d).Value;

        var withOffset = BrepBoolean.Subtract(root, offsetHole);
        Assert.True(withOffset.IsSuccess);
        var result = BrepBoolean.Subtract(withOffset.Value, centerBore);

        Assert.True(result.IsSuccess);
        Assert.True(BrepBindingValidator.Validate(result.Value, true).IsSuccess);
        Assert.Equal(2, result.Value.ShellRepresentation!.InnerShellIds.Count);
        var export = Step242Exporter.ExportBody(result.Value);
        Assert.True(export.IsSuccess);
        Assert.Contains("BREP_WITH_VOIDS", export.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_CylinderRootCenterBoreThenWallTouchingOffsetHole_IsRejectedLoudly()
    {
        var root = BrepPrimitives.CreateCylinder(40d, 12d).Value;
        var centerBore = BrepPrimitives.CreateCylinder(12d, 24d).Value;
        var touchingOuterWallHole = TransformBody(BrepPrimitives.CreateCylinder(4d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(36d, 0d, 0d)));

        var withCenterBore = BrepBoolean.Subtract(root, centerBore);
        Assert.True(withCenterBore.IsSuccess);
        var result = BrepBoolean.Subtract(withCenterBore.Value, touchingOuterWallHole);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("strictly inside the outer cylindrical wall", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_CylinderRootCenterBoreThenOverlappingOffsetHole_IsRejectedLoudly()
    {
        var root = BrepPrimitives.CreateCylinder(40d, 12d).Value;
        var centerBore = BrepPrimitives.CreateCylinder(12d, 24d).Value;
        var overlappingCenterBoreHole = TransformBody(BrepPrimitives.CreateCylinder(4d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(15d, 0d, 0d)));

        var withCenterBore = BrepBoolean.Subtract(root, centerBore);
        Assert.True(withCenterBore.IsSuccess);
        var result = BrepBoolean.Subtract(withCenterBore.Value, overlappingCenterBoreHole);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("overlaps previously accepted hole", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_CylinderRootBoltHoleTangentToBoltHole_IsRejectedLoudly()
    {
        var root = BrepPrimitives.CreateCylinder(40d, 12d).Value;
        var centerBore = BrepPrimitives.CreateCylinder(12d, 24d).Value;
        var holeA = TransformBody(BrepPrimitives.CreateCylinder(4d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(25d, 0d, 0d)));
        var tangentHoleB = TransformBody(BrepPrimitives.CreateCylinder(4d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(33d, 0d, 0d)));

        var withCenterBore = BrepBoolean.Subtract(root, centerBore);
        Assert.True(withCenterBore.IsSuccess);
        var withHoleA = BrepBoolean.Subtract(withCenterBore.Value, holeA);
        Assert.True(withHoleA.IsSuccess);
        var result = BrepBoolean.Subtract(withHoleA.Value, tangentHoleB);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("tangent to previously accepted hole", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_BoxCylinderRotatedAxis_SucceedsWithinBoundedSubset()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var rotated = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateRotationX(System.Math.PI / 6d) * Transform3D.CreateTranslation(new Vector3D(0d, -3d, 6d)));

        var result = BrepBoolean.Subtract(left, rotated);

        Assert.True(result.IsSuccess);
        Assert.True(BrepBindingValidator.Validate(result.Value, true).IsSuccess);
        var export = Step242Exporter.ExportBody(result.Value);
        Assert.True(export.IsSuccess);
        Assert.Contains("ELLIPSE", export.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_BoxCylinderBlindTopEntry_ReturnsClosedBottomHole()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var blind = TransformBody(BrepPrimitives.CreateCylinder(4d, 8d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 8d)));

        var result = BrepBoolean.Subtract(left, blind);

        Assert.True(result.IsSuccess);
        Assert.True(BrepBindingValidator.Validate(result.Value, true).IsSuccess);
        Assert.Equal(8, result.Value.Topology.Faces.Count());

        var topFace = Assert.Single(result.Value.Topology.Faces, face =>
            result.Value.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Kind == SurfaceGeometryKind.Plane
            && surface.Plane!.Value.Normal.Z > 0.5d);
        Assert.Equal(2, result.Value.GetLoopIds(topFace.Id).Count);

        var bottomFace = Assert.Single(result.Value.Topology.Faces, face =>
            result.Value.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Kind == SurfaceGeometryKind.Plane
            && surface.Plane!.Value.Normal.Z < -0.5d
            && System.Math.Abs(surface.Plane!.Value.Origin.Z) < 1e-6d);
        Assert.Single(result.Value.GetLoopIds(bottomFace.Id));
    }

    [Fact]
    public void Subtract_BoxCylinderOutsideFootprint_ReturnsRadiusExceedsBoundaryDiagnostic()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var outside = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(17d, 0d, 6d)));

        var result = BrepBoolean.Subtract(left, outside);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("BrepBoolean.AnalyticHole.RadiusExceedsBoundary", diagnostic.Source);
        Assert.Contains("extending outside the box side-wall footprint", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_BoxCylinderTangentToBoundary_ReturnsTangentContactDiagnostic()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var tangent = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(16d, 0d, 6d)));

        var result = BrepBoolean.Subtract(left, tangent);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("BrepBoolean.AnalyticHole.TangentContact", diagnostic.Source);
        Assert.Contains("tangent to a box side wall", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_BoxFrustumConeThroughHole_Returns_ManifoldBodyWithConicalHole()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var right = CreateCone(bottomRadius: 4d, topRadius: 8d, height: 20d);

        var result = BrepBoolean.Subtract(left, right);

        Assert.True(result.IsSuccess);
        var body = result.Value;
        Assert.Single(body.Topology.Bodies);
        Assert.Single(body.Topology.Shells);
        Assert.Equal(7, body.Topology.Faces.Count());
        Assert.Equal(15, body.Topology.Edges.Count());
        Assert.Equal(12, body.Topology.Vertices.Count());
        Assert.True(BrepBindingValidator.Validate(body, true).IsSuccess);

        var conicalFace = Assert.Single(body.Topology.Faces, face =>
            body.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Kind == SurfaceGeometryKind.Cone);
        Assert.Single(body.GetLoopIds(conicalFace.Id));

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
    public void Subtract_BoxPointedConeThroughHole_Exports_And_RoundTrips_Deterministically()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var right = CreateCone(bottomRadius: 6d, topRadius: 0d, height: 20d);

        var first = BrepBoolean.Subtract(left, right);
        var second = BrepBoolean.Subtract(left, right);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(7, first.Value.Topology.Faces.Count());
        Assert.Contains(first.Value.Topology.Faces, face =>
            first.Value.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Kind == SurfaceGeometryKind.Cone);

        var firstExport = Step242Exporter.ExportBody(first.Value);
        var secondExport = Step242Exporter.ExportBody(second.Value);
        Assert.True(firstExport.IsSuccess);
        Assert.True(secondExport.IsSuccess);
        Assert.Equal(firstExport.Value, secondExport.Value);
        Assert.Contains("MANIFOLD_SOLID_BREP", firstExport.Value, StringComparison.Ordinal);
        Assert.Contains("CONICAL_SURFACE", firstExport.Value, StringComparison.Ordinal);
        Assert.Contains("FACE_BOUND('',", firstExport.Value, StringComparison.Ordinal);
        Assert.Contains("FACE_OUTER_BOUND('',", firstExport.Value, StringComparison.Ordinal);

        var import = Step242Importer.ImportBody(firstExport.Value);
        Assert.True(import.IsSuccess);
        Assert.True(BrepBindingValidator.Validate(import.Value, true).IsSuccess);
        Assert.Contains(import.Value.Topology.Faces, face =>
            import.Value.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Kind == SurfaceGeometryKind.Cone);
    }

    [Fact]
    public void Subtract_BoxConeRotatedAxis_ReturnsDeferredArbitraryAxisDiagnostic()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var rotated = TransformBody(CreateCone(bottomRadius: 2d, topRadius: 3d, height: 20d), Transform3D.CreateRotationX(System.Math.PI / 12d) * Transform3D.CreateTranslation(new Vector3D(0d, -1d, 2d)));

        var result = BrepBoolean.Subtract(left, rotated);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
    }

    [Fact]
    public void Subtract_BoxCylinderMidSpanPartialHeight_RebuildsAsContainedCylinderCavity()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var partial = TransformBody(BrepPrimitives.CreateCylinder(4d, 6d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 6d)));

        var result = BrepBoolean.Subtract(left, partial);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ShellRepresentation);
        Assert.Single(result.Value.ShellRepresentation!.InnerShellIds);
        Assert.True(BrepBindingValidator.Validate(result.Value, true).IsSuccess);
    }

    [Fact]
    public void Subtract_BoxCylinderAxisNearlyParallelToTopPlane_ReturnsAxisNotAlignedDiagnostic()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var rotated = TransformBody(BrepPrimitives.CreateCylinder(3d, 20d).Value, Transform3D.CreateRotationX(System.Math.PI / 2d) * Transform3D.CreateTranslation(new Vector3D(0d, 0d, 6d)));
        var result = BrepBoolean.Subtract(left, rotated);
        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("BrepBoolean.AnalyticHole.AxisNotAligned", diagnostic.Source);
    }

    [Fact]
    public void Subtract_BoxConeBlindTopEntry_ReturnsClosedBottomHole()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var blind = TransformBody(CreateCone(bottomRadius: 4d, topRadius: 2d, height: 8d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, 4d)));

        var result = BrepBoolean.Subtract(left, blind);

        Assert.True(result.IsSuccess);
        Assert.True(BrepBindingValidator.Validate(result.Value, true).IsSuccess);
        Assert.Equal(8, result.Value.Topology.Faces.Count());
        var topFace = Assert.Single(result.Value.Topology.Faces, face =>
            result.Value.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Kind == SurfaceGeometryKind.Plane
            && surface.Plane!.Value.Normal.Z > 0.5d
            && System.Math.Abs(surface.Plane!.Value.Origin.Z - 12d) < 1e-6d);
        Assert.Equal(2, result.Value.GetLoopIds(topFace.Id).Count);

        var bottomFace = Assert.Single(result.Value.Topology.Faces, face =>
            result.Value.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Kind == SurfaceGeometryKind.Plane
            && surface.Plane!.Value.Normal.Z < -0.5d
            && System.Math.Abs(surface.Plane!.Value.Origin.Z) < 1e-6d);
        Assert.Single(result.Value.GetLoopIds(bottomFace.Id));
    }

    [Fact]
    public void Subtract_BoxCylinderBlindRotatedAxis_ReturnsDeferredArbitraryAxisBlindDiagnostic()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var blind = TransformBody(BrepPrimitives.CreateCylinder(2.5d, 8d).Value, Transform3D.CreateRotationX(System.Math.PI / 12d) * Transform3D.CreateTranslation(new Vector3D(-1d, -1d, 8d)));
        var result = BrepBoolean.Subtract(left, blind);
        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("BrepBoolean.AnalyticHole.NotFullySpanning", diagnostic.Source);
        Assert.Contains("does not match the supported subtract span family", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_BoxConeBlindRotatedAxis_ReturnsDeferredArbitraryAxisDiagnostic()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var blind = TransformBody(CreateCone(bottomRadius: 2.5d, topRadius: 1.5d, height: 8d), Transform3D.CreateRotationX(System.Math.PI / 12d) * Transform3D.CreateTranslation(new Vector3D(1d, -1d, 4d)));
        var result = BrepBoolean.Subtract(left, blind);
        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
    }

    [Fact]
    public void Subtract_BoxConeRotatedAxis_RejectionIsDeterministic()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var rotated = TransformBody(CreateCone(bottomRadius: 2d, topRadius: 3d, height: 20d), Transform3D.CreateRotationX(System.Math.PI / 12d) * Transform3D.CreateTranslation(new Vector3D(0d, -1d, 2d)));
        var first = BrepBoolean.Subtract(left, rotated);
        var second = BrepBoolean.Subtract(left, rotated);
        Assert.False(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal(Assert.Single(first.Diagnostics).Message, Assert.Single(second.Diagnostics).Message);
    }

    [Fact]
    public void Subtract_BoxConePartialHeight_ReturnsNotFullySpanningDiagnostic()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var partial = TransformBody(CreateCone(bottomRadius: 4d, topRadius: 2d, height: 8d), Transform3D.CreateTranslation(new Vector3D(0d, 0d, 1d)));

        var result = BrepBoolean.Subtract(left, partial);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("BrepBoolean.AnalyticHole.NotFullySpanning", diagnostic.Source);
        Assert.Equal("Boolean Subtract does not match the supported subtract span family; only through-holes or one-sided blind holes are allowed in this milestone.", diagnostic.Message);
    }

    [Fact]
    public void Subtract_BoxConeOutsideFootprint_ReturnsRadiusExceedsBoundaryDiagnostic()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var outside = TransformBody(CreateCone(bottomRadius: 4d, topRadius: 8d, height: 20d), Transform3D.CreateTranslation(new Vector3D(13.7d, 0d, 0d)));

        var result = BrepBoolean.Subtract(left, outside);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("BrepBoolean.AnalyticHole.RadiusExceedsBoundary", diagnostic.Source);
        Assert.Equal("Boolean Subtract has top boundary circle extending outside the box side-wall footprint. Reduce the boundary radius or move the cone center farther inside the box XY boundary.", diagnostic.Message);
    }

    [Fact]
    public void Subtract_BoxConeTangentToBoundary_ReturnsTangentContactDiagnostic()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var tangent = TransformBody(CreateCone(bottomRadius: 4d, topRadius: 8d, height: 20d), Transform3D.CreateTranslation(new Vector3D(13.6d, 0d, 0d)));

        var result = BrepBoolean.Subtract(left, tangent);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("BrepBoolean.AnalyticHole.TangentContact", diagnostic.Source);
        Assert.Equal("Boolean Subtract has top boundary circle tangent to a box side wall; tangent analytic-hole cases are rejected to avoid zero-thickness geometry. Move the cone inward or reduce the boundary radius at that plane.", diagnostic.Message);
    }

    [Fact]
    public void ValidateThroughHole_ConeBoundarySectionDegenerates_ReturnsDegenerateBoundarySectionDiagnostic()
    {
        var box = new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d);
        var cone = new RecognizedCone(
            new Point3D(0d, 0d, 0d),
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            0d,
            20d,
            System.Math.Atan(0.5d),
            0d,
            10d);

        var result = BrepBooleanCylinderRecognition.ValidateThroughHole(
            box,
            cone,
            ToleranceContext.Default,
            out var diagnostic);

        Assert.False(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(BooleanDiagnosticCode.DegenerateBoundarySection, diagnostic!.Code);
        Assert.Equal("BrepBoolean.AnalyticHole.DegenerateBoundarySection", diagnostic.Source);
        Assert.Equal("Boolean Subtract requires non-degenerate circular sections where the cone meets the two box boundary planes. Increase the boundary radii at those planes by moving the apex farther away or changing the cone taper.", diagnostic.Message);
    }

    [Fact]
    public void Subtract_ComposedTwoCylinderThroughHoles_RemainsTruthful_And_Deterministic()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var firstHole = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(-7d, 0d, 6d)));
        var secondHole = TransformBody(BrepPrimitives.CreateCylinder(3d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(8d, 2d, 6d)));

        var first = BrepBoolean.Subtract(baseBox, firstHole);
        Assert.True(first.IsSuccess);

        var second = BrepBoolean.Subtract(first.Value, secondHole);
        var repeated = BrepBoolean.Subtract(first.Value, secondHole);

        Assert.True(second.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Equal(8, second.Value.Topology.Faces.Count());
        Assert.Equal(18, second.Value.Topology.Edges.Count());
        Assert.Equal(16, second.Value.Topology.Vertices.Count());
        Assert.Equal(second.Value.SafeBooleanComposition?.Holes.Count, repeated.Value.SafeBooleanComposition?.Holes.Count);
        Assert.Equal(2, second.Value.SafeBooleanComposition?.Holes.Count);
        Assert.True(BrepBindingValidator.Validate(second.Value, true).IsSuccess);

        var export = Step242Exporter.ExportBody(second.Value);
        var repeatedExport = Step242Exporter.ExportBody(repeated.Value);
        Assert.True(export.IsSuccess);
        Assert.True(repeatedExport.IsSuccess);
        Assert.Equal(export.Value, repeatedExport.Value);
        Assert.Contains("CYLINDRICAL_SURFACE", export.Value, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", export.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_ComposedCylinderThenConeThroughHole_RemainsTruthful_And_Deterministic()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var cylinderHole = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(-7d, -2d, 6d)));
        var coneHole = TransformBody(CreateCone(bottomRadius: 3d, topRadius: 5d, height: 20d), Transform3D.CreateTranslation(new Vector3D(8d, 0d, 0d)));

        var first = BrepBoolean.Subtract(baseBox, cylinderHole);
        Assert.True(first.IsSuccess);

        var second = BrepBoolean.Subtract(first.Value, coneHole);
        var repeated = BrepBoolean.Subtract(first.Value, coneHole);

        Assert.True(second.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Equal(8, second.Value.Topology.Faces.Count());
        Assert.Equal(18, second.Value.Topology.Edges.Count());
        Assert.Equal(16, second.Value.Topology.Vertices.Count());
        Assert.Equal(2, second.Value.SafeBooleanComposition?.Holes.Count);
        Assert.True(BrepBindingValidator.Validate(second.Value, true).IsSuccess);
        Assert.Contains(second.Value.Topology.Faces, face =>
            second.Value.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Kind == SurfaceGeometryKind.Cylinder);
        Assert.Contains(second.Value.Topology.Faces, face =>
            second.Value.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Kind == SurfaceGeometryKind.Cone);

        var export = Step242Exporter.ExportBody(second.Value);
        var repeatedExport = Step242Exporter.ExportBody(repeated.Value);
        Assert.True(export.IsSuccess);
        Assert.True(repeatedExport.IsSuccess);
        Assert.Equal(export.Value, repeatedExport.Value);
        Assert.Contains("CYLINDRICAL_SURFACE", export.Value, StringComparison.Ordinal);
        Assert.Contains("CONICAL_SURFACE", export.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_ComposedThroughThenBlindCylinder_ReturnsUnsupportedBlindHoleCompositionDiagnostic()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var firstHole = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(-7d, 0d, 6d)));
        var blind = TransformBody(BrepPrimitives.CreateCylinder(3d, 8d).Value, Transform3D.CreateTranslation(new Vector3D(8d, 1d, 8d)));

        var first = BrepBoolean.Subtract(baseBox, firstHole);
        Assert.True(first.IsSuccess);

        var result = BrepBoolean.Subtract(first.Value, blind);
        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("BrepBoolean.AnalyticHole.UnsupportedBlindHoleComposition", diagnostic.Source);
    }

    [Fact]
    public void Subtract_ComposedBlindPocketThenCoaxialThrough_Succeeds_AsBoundedSteppedHole()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-35d, 35d, -20d, 20d, -8d, 8d)).Value;
        var pocket = TransformBody(BrepPrimitives.CreateCylinder(7d, 6d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 5d)));
        var through = BrepPrimitives.CreateCylinder(3.5d, 24d).Value;

        var first = BrepBoolean.Subtract(baseBox, pocket);
        Assert.True(first.IsSuccess);
        var second = BrepBoolean.Subtract(first.Value, through);
        var repeated = BrepBoolean.Subtract(first.Value, through);

        Assert.True(second.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.True(BrepBindingValidator.Validate(second.Value, true).IsSuccess);
        Assert.Equal(9, second.Value.Topology.Faces.Count());
        Assert.Equal(2, second.Value.SafeBooleanComposition?.Holes.Count);

        var export = Step242Exporter.ExportBody(second.Value);
        var repeatedExport = Step242Exporter.ExportBody(repeated.Value);
        Assert.True(export.IsSuccess);
        Assert.True(repeatedExport.IsSuccess);
        Assert.Equal(export.Value, repeatedExport.Value);
        Assert.Contains("CYLINDRICAL_SURFACE", export.Value, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", export.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_ComposedBlindPocketThenOffsetThrough_ReturnsCoaxialSteppedDiagnostic()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-35d, 35d, -20d, 20d, -8d, 8d)).Value;
        var pocket = TransformBody(BrepPrimitives.CreateCylinder(7d, 6d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 5d)));
        var offsetThrough = TransformBody(BrepPrimitives.CreateCylinder(3.5d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(1.5d, 0d, 0d)));

        var first = BrepBoolean.Subtract(baseBox, pocket);
        Assert.True(first.IsSuccess);
        var second = BrepBoolean.Subtract(first.Value, offsetThrough);

        Assert.False(second.IsSuccess);
        var diagnostic = Assert.Single(second.Diagnostics);
        Assert.Equal("BrepBoolean.AnalyticHole.AxisNotAligned", diagnostic.Source);
        Assert.Contains("requires coaxial cylinders", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_BoxContainedCylinderCavity_Succeeds_WithInnerShell_And_DeterministicExport()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-25d, 25d, -20d, 20d, -10d, 10d)).Value;
        var containedPocket = BrepPrimitives.CreateCylinder(5d, 6d).Value;

        var first = BrepBoolean.Subtract(baseBox, containedPocket);
        var second = BrepBoolean.Subtract(baseBox, containedPocket);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotNull(first.Value.ShellRepresentation);
        Assert.Single(first.Value.ShellRepresentation!.InnerShellIds);
        Assert.True(BrepBindingValidator.Validate(first.Value, true).IsSuccess);

        var export = Step242Exporter.ExportBody(first.Value);
        var repeatedExport = Step242Exporter.ExportBody(second.Value);
        Assert.True(export.IsSuccess);
        Assert.True(repeatedExport.IsSuccess);
        Assert.Equal(export.Value, repeatedExport.Value);
        Assert.Contains("BREP_WITH_VOIDS", export.Value, StringComparison.Ordinal);
        Assert.Contains("CYLINDRICAL_SURFACE", export.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_ComposedContainedThenThroughCoaxial_Succeeds_AsContainedEntryCounterboreStack()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-25d, 25d, -20d, 20d, -10d, 10d)).Value;
        var containedPocket = BrepPrimitives.CreateCylinder(6d, 6d).Value;
        var through = BrepPrimitives.CreateCylinder(3d, 30d).Value;

        var first = BrepBoolean.Subtract(baseBox, containedPocket);
        Assert.True(first.IsSuccess);

        var result = BrepBoolean.Subtract(first.Value, through);
        Assert.True(result.IsSuccess);
        Assert.Equal(9, result.Value.Topology.Faces.Count());
        Assert.True(BrepBindingValidator.Validate(result.Value, true).IsSuccess);
    }

    [Fact]
    public void Subtract_ComposedBlindPocketThenEqualRadiusThrough_ReturnsDegenerateShoulderDiagnostic()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-35d, 35d, -20d, 20d, -8d, 8d)).Value;
        var pocket = TransformBody(BrepPrimitives.CreateCylinder(3.5d, 6d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 5d)));
        var through = BrepPrimitives.CreateCylinder(3.5d, 24d).Value;

        var first = BrepBoolean.Subtract(baseBox, pocket);
        Assert.True(first.IsSuccess);

        var second = BrepBoolean.Subtract(first.Value, through);
        Assert.False(second.IsSuccess);
        var diagnostic = Assert.Single(second.Diagnostics);
        Assert.Equal("BrepBoolean.AnalyticHole.TangentContact", diagnostic.Source);
        Assert.Contains("strictly larger entry-segment radius", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_ComposedBlindPocketThenLargerThroughSegment_ReturnsInvalidCounterboreRadiusOrderingDiagnostic()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-35d, 35d, -20d, 20d, -8d, 8d)).Value;
        var pocket = TransformBody(BrepPrimitives.CreateCylinder(3.5d, 6d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 5d)));
        var through = BrepPrimitives.CreateCylinder(5d, 24d).Value;

        var first = BrepBoolean.Subtract(baseBox, pocket);
        Assert.True(first.IsSuccess);

        var second = BrepBoolean.Subtract(first.Value, through);
        Assert.False(second.IsSuccess);
        var diagnostic = Assert.Single(second.Diagnostics);
        Assert.Equal("BrepBoolean.AnalyticHole.TangentContact", diagnostic.Source);
        Assert.Contains("invalid counterbore radius ordering", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_ComposedOverlappingHoles_FailsWithDeterministicInterferenceDiagnostic()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var firstHole = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 6d)));
        var secondHole = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(6d, 0d, 6d)));

        var first = BrepBoolean.Subtract(baseBox, firstHole);
        Assert.True(first.IsSuccess);

        var second = BrepBoolean.Subtract(first.Value, secondHole);

        Assert.False(second.IsSuccess);
        var diagnostic = Assert.Single(second.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("BrepBoolean.AnalyticHole.HoleInterference", diagnostic.Source);
        Assert.Equal("Boolean Subtract overlaps previously accepted hole <unknown>; overlapping safe-hole composition is not supported. Separate the hole centers or reduce one of the boundary radii.", diagnostic.Message);
    }

    [Fact]
    public void Subtract_ComposedTangentHoles_FailsWithDeterministicTangentDiagnostic()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var firstHole = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(-4d, 0d, 6d)));
        var secondHole = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(4d, 0d, 6d)));

        var first = BrepBoolean.Subtract(baseBox, firstHole);
        Assert.True(first.IsSuccess);

        var second = BrepBoolean.Subtract(first.Value, secondHole);

        Assert.False(second.IsSuccess);
        var diagnostic = Assert.Single(second.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("BrepBoolean.AnalyticHole.TangentContact", diagnostic.Source);
        Assert.Equal("Boolean Subtract would be tangent to previously accepted hole <unknown>; tangent safe-hole composition is rejected to avoid zero-thickness geometry.", diagnostic.Message);
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
    public void Subtract_BoxSphereFullyContained_RebuildsToInnerShellCavityAndExportsDeterministically()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, -6d, 6d)).Value;
        var right = BrepPrimitives.CreateSphere(4d).Value;

        var result = BrepBoolean.Subtract(left, right);
        var repeated = BrepBoolean.Subtract(left, right);

        Assert.True(result.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.NotNull(result.Value.ShellRepresentation);
        Assert.Single(result.Value.ShellRepresentation!.InnerShellIds);
        Assert.True(BrepBindingValidator.Validate(result.Value, requireAllEdgeAndFaceBindings: true).IsSuccess);

        var export = Step242Exporter.ExportBody(result.Value);
        var repeatedExport = Step242Exporter.ExportBody(repeated.Value);
        Assert.True(export.IsSuccess);
        Assert.True(repeatedExport.IsSuccess);
        Assert.Equal(export.Value, repeatedExport.Value);
        Assert.Contains("SPHERICAL_SURFACE", export.Value, StringComparison.Ordinal);
        Assert.Contains("BREP_WITH_VOIDS", export.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("MANIFOLD_SOLID_BREP", export.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Subtract_BoxSphereTangentBoundary_ReturnsTangentContactDiagnostic()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, -6d, 6d)).Value;
        var right = TransformBody(BrepPrimitives.CreateSphere(6d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 0d)));

        var result = BrepBoolean.Subtract(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("BrepBoolean.AnalyticHole.TangentContact", diagnostic.Source);
        Assert.Equal("Boolean Subtract is tangent to a box boundary plane; tangent spherical cavities are rejected to avoid zero-thickness boundary contact.", diagnostic.Message);
    }

    [Fact]
    public void Subtract_BoxSpherePartiallyOutside_ReturnsRadiusExceedsBoundaryDiagnostic()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, -6d, 6d)).Value;
        var right = TransformBody(BrepPrimitives.CreateSphere(4d).Value, Transform3D.CreateTranslation(new Vector3D(17d, 0d, 0d)));

        var result = BrepBoolean.Subtract(left, right);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("BrepBoolean.AnalyticHole.RadiusExceedsBoundary", diagnostic.Source);
        Assert.Equal("Boolean Subtract extends outside the box boundary; spherical cavity tools must remain strictly contained inside the box.", diagnostic.Message);
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
    public void ClassifyBooleanCase_BoxBox_IsPlanarOnly()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-2d, 2d, -2d, 2d, -2d, 2d)).Value;
        var right = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-1d, 1d, -1d, 1d, -1d, 1d)).Value;

        var classification = BrepBoolean.ClassifyBooleanCase(left, right, BooleanOperation.Union);

        Assert.Equal(BooleanExecutionClass.PlanarOnly, classification.ExecutionClass);
        Assert.NotNull(classification.LeftBox);
        Assert.NotNull(classification.RightBox);
        Assert.Null(classification.RightAnalyticSurface);
    }

    [Fact]
    public void ClassifyBooleanCase_BoxCylinderSubtract_IsPlanarWithAnalyticHole()
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var right = TransformBody(BrepPrimitives.CreateCylinder(4d, 20d).Value, Transform3D.CreateTranslation(new Vector3D(3d, -2d, 6d)));

        var classification = BrepBoolean.ClassifyBooleanCase(left, right, BooleanOperation.Subtract);

        Assert.Equal(BooleanExecutionClass.PlanarWithAnalyticHole, classification.ExecutionClass);
        Assert.Equal(AnalyticSurfaceKind.Cylinder, classification.RightAnalyticSurface?.Kind);
    }

    [Theory]
    [InlineData(AnalyticSurfaceKind.Cone)]
    [InlineData(AnalyticSurfaceKind.Sphere)]
    [InlineData(AnalyticSurfaceKind.Torus)]
    public void ClassifyBooleanCase_BoxAnalyticSubtract_RoutesToAnalyticHoleClass(AnalyticSurfaceKind expectedKind)
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-20d, 20d, -15d, 15d, 0d, 12d)).Value;
        var right = expectedKind switch
        {
            AnalyticSurfaceKind.Cone => CreateCone(bottomRadius: 6d, topRadius: 0d, height: 20d),
            AnalyticSurfaceKind.Sphere => TransformBody(BrepPrimitives.CreateSphere(4d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 6d))),
            AnalyticSurfaceKind.Torus => TransformBody(BrepPrimitives.CreateTorus(6d, 2d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 6d))),
            _ => throw new ArgumentOutOfRangeException(nameof(expectedKind)),
        };

        var classification = BrepBoolean.ClassifyBooleanCase(left, right, BooleanOperation.Subtract);

        Assert.Equal(BooleanExecutionClass.PlanarWithAnalyticHole, classification.ExecutionClass);
        Assert.Equal(expectedKind, classification.RightAnalyticSurface?.Kind);
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

    public static TheoryData<string, BooleanOperation, Transform3D> BoxTorusRepresentativeCases =>
        new()
        {
            { "subtract centered torus", BooleanOperation.Subtract, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 0d)) },
            { "subtract torus fully inside box", BooleanOperation.Subtract, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 10d)) },
            { "subtract offset torus", BooleanOperation.Subtract, Transform3D.CreateTranslation(new Vector3D(4d, 0d, 10d)) },
            { "subtract torus intersecting faces", BooleanOperation.Subtract, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 18d)) },
            { "subtract torus outside footprint", BooleanOperation.Subtract, Transform3D.CreateTranslation(new Vector3D(18d, 0d, 10d)) },
            { "add torus fully inside box", BooleanOperation.Union, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 10d)) },
            { "add offset torus", BooleanOperation.Union, Transform3D.CreateTranslation(new Vector3D(4d, 0d, 10d)) },
            { "add torus intersecting faces", BooleanOperation.Union, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 18d)) },
            { "add torus outside footprint", BooleanOperation.Union, Transform3D.CreateTranslation(new Vector3D(18d, 0d, 10d)) },
            { "intersect torus fully inside box", BooleanOperation.Intersect, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 10d)) },
            { "intersect offset torus", BooleanOperation.Intersect, Transform3D.CreateTranslation(new Vector3D(4d, 0d, 10d)) },
            { "intersect torus intersecting faces", BooleanOperation.Intersect, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 18d)) },
            { "intersect torus outside footprint", BooleanOperation.Intersect, Transform3D.CreateTranslation(new Vector3D(18d, 0d, 10d)) },
        };

    [Theory]
    [MemberData(nameof(BoxTorusRepresentativeCases))]
    public void BoxTorusRepresentativeCases_RemainUnsupported(string _, BooleanOperation operation, Transform3D transform)
    {
        var left = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-10d, 10d, -10d, 10d, 0d, 20d)).Value;
        var right = TransformBody(BrepPrimitives.CreateTorus(6d, 2d).Value, transform);

        var result = BrepBoolean.Execute(new BooleanRequest(left, right, operation));

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        if (operation == BooleanOperation.Subtract)
        {
            Assert.Equal("BrepBoolean.UnsupportedAnalyticSurfaceKind", diagnostic.Source);
            Assert.Equal($"Boolean {operation} does not support analytic tool surface kind 'Torus' in the safe boolean family. Use a cylinder or cone through-hole instead.", diagnostic.Message);
        }
        else
        {
            Assert.Equal($"Boolean {operation}: M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", diagnostic.Message);
        }
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

        var vertexPoints = new Dictionary<VertexId, Point3D>();
        foreach (var vertex in body.Topology.Vertices)
        {
            if (body.TryGetVertexPoint(vertex.Id, out var point))
            {
                vertexPoints[vertex.Id] = transform.Apply(point);
            }
        }

        return new BrepBody(body.Topology, geometry, body.Bindings, vertexPoints, body.SafeBooleanComposition);
    }

}
