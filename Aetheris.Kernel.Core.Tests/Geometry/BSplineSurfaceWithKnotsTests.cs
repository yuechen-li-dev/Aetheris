using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Geometry;

public sealed class BSplineSurfaceWithKnotsTests
{
    [Fact]
    public void Evaluate_AtDomainCorners_ReturnsCornerControlPoints()
    {
        var surface = CreateBilinearSurface();

        Assert.Equal(new Point3D(0d, 0d, 0d), surface.Evaluate(surface.DomainStartU, surface.DomainStartV));
        Assert.Equal(new Point3D(0d, 1d, 0d), surface.Evaluate(surface.DomainStartU, surface.DomainEndV));
        Assert.Equal(new Point3D(1d, 0d, 0d), surface.Evaluate(surface.DomainEndU, surface.DomainStartV));
        Assert.Equal(new Point3D(1d, 1d, 1d), surface.Evaluate(surface.DomainEndU, surface.DomainEndV));
    }

    [Fact]
    public void Evaluate_IsDeterministic_ForRepeatedCalls()
    {
        var surface = CreateBilinearSurface();
        var u = (surface.DomainStartU + surface.DomainEndU) * 0.5d;
        var v = (surface.DomainStartV + surface.DomainEndV) * 0.25d;

        var first = surface.Evaluate(u, v);
        var second = surface.Evaluate(u, v);

        Assert.Equal(first, second);
    }

    private static BSplineSurfaceWithKnots CreateBilinearSurface()
    {
        var controlPoints = new[]
        {
            new[] { new Point3D(0d, 0d, 0d), new Point3D(0d, 1d, 0d) },
            new[] { new Point3D(1d, 0d, 0d), new Point3D(1d, 1d, 1d) }
        };

        return new BSplineSurfaceWithKnots(
            degreeU: 1,
            degreeV: 1,
            controlPoints: controlPoints,
            surfaceForm: "UNSPECIFIED",
            uClosed: false,
            vClosed: false,
            selfIntersect: false,
            knotMultiplicitiesU: new[] { 2, 2 },
            knotMultiplicitiesV: new[] { 2, 2 },
            knotValuesU: new[] { 0d, 1d },
            knotValuesV: new[] { 0d, 1d },
            knotSpec: "UNSPECIFIED");
    }
}
