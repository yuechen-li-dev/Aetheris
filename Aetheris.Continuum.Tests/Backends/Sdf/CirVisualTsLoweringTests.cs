using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Tests.Backends.Sdf;

public sealed class CirVisualTsLoweringTests
{
    [Fact]
    public void EqualDefinitionsShareProgramIdentityAndDifferentDimensionsDoNotAlias()
    {
        var first = CirVisualTsLowerer.Lower(new SdfCylinderNode(2, 6));
        var equal = CirVisualTsLowerer.Lower(new SdfCylinderNode(2, 6));
        var different = CirVisualTsLowerer.Lower(new SdfCylinderNode(3, 6));
        Assert.True(first.Success);
        Assert.Equal(first.Program!.StructuralHash, equal.Program!.StructuralHash);
        Assert.NotEqual(first.Program.StructuralHash, different.Program!.StructuralHash);
        Assert.Equal(new SdfCylinderNode(2, 6).Bounds, first.Program.Bounds);
    }

    [Fact]
    public void AffineDistanceFieldsAndInvalidDimensionsFailWithInspectableFallback()
    {
        var affine = new SdfTransformNode(new SdfSphereNode(2), Transform3D.CreateScale(0.1));
        var result = CirVisualTsLowerer.Lower(affine);
        Assert.False(result.Success);
        Assert.Contains("transform-not-rigid", result.FallbackReason);
        Assert.False(CirVisualTsLowerer.Lower(new SdfCylinderNode(double.NaN, 2)).Success);
        Assert.False(CirVisualTsLowerer.Lower(new SdfTorusNode(1, 2)).Success);
    }

    [Fact]
    public void RigidCsgSpecializesStraightLineTapeAndRetainsAuthoritativeBounds()
    {
        SdfNode node = new SdfSubtractNode(new SdfCylinderNode(2, 6),
            new SdfTransformNode(new SdfSphereNode(1), Transform3D.CreateTranslation(new Vector3D(1, 0, 0))));
        var result = CirVisualTsLowerer.Lower(node);
        Assert.True(result.Success);
        Assert.Equal(node.Bounds, result.Program!.Bounds);
        Assert.Contains("Max(v", result.Program.FieldSource);
        Assert.DoesNotContain("switch", result.Program.FieldSource);
        Assert.DoesNotContain("for (", result.Program.FieldSource);
    }
}
