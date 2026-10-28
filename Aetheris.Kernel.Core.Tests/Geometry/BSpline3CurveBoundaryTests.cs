using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Geometry;

public sealed class BSpline3CurveBoundaryTests
{
    [Fact]
    public void NonClampedEndUsesTheBasisLimitForPositionAndDerivatives()
    {
        var curve = new BSpline3Curve(2, [new(0, 0, 0), new(1, 0, 0), new(3, 0, 0), new(6, 0, 0)],
            [1, 1, 1, 1, 1, 1, 1], [-2, -1, 0, 1, 2, 3, 4], "UNSPECIFIED", false, false, "UNSPECIFIED");
        // Uniform quadratic basis at u=2: half of each final control point.
        Assert.Equal(new Point3D(4.5, 0, 0), curve.Evaluate(curve.DomainEnd));
        Assert.Equal(new Vector3D(3, 0, 0), curve.EvaluateTangent(curve.DomainEnd));
        Assert.Equal(new Vector3D(1, 0, 0), curve.EvaluateSecondDerivative(curve.DomainEnd));
        Assert.Equal(curve.EvaluateRational([1, 1, 1, 1], curve.DomainEnd), curve.Evaluate(curve.DomainEnd));
        Assert.InRange((curve.Evaluate(curve.DomainEnd - 1e-8) - curve.Evaluate(curve.DomainEnd)).Length, 0, 1e-7);
        var cubic = new BSpline3Curve(3, [new(0, 0, 0), new(1, 0, 0), new(4, 0, 0), new(9, 0, 0), new(16, 0, 0)],
            Enumerable.Repeat(1, 9).ToArray(), [-3, -2, -1, 0, 1, 2, 3, 4, 5], "UNSPECIFIED", false, false, "UNSPECIFIED");
        Assert.Equal(new Vector3D(6, 0, 0), cubic.EvaluateTangent(cubic.DomainEnd));
    }
}
