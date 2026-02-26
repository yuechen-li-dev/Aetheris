using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Numerics;

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
}
