using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Curves;

/// <summary>
/// One explicit branch of an analytic hyperbola.  The support is parameterized
/// as C + branch*A*cosh(t)*U + B*sinh(t)*V.  A bounded B-rep use owns its trim
/// interval; this support deliberately remains unbounded.
/// </summary>
public readonly record struct Hyperbola3Curve
{
    public Hyperbola3Curve(Point3D center, Direction3D planeNormal, Direction3D axisU, double semiAxisA, double semiAxisB, HyperbolaBranch branch)
    {
        if (!double.IsFinite(center.X) || !double.IsFinite(center.Y) || !double.IsFinite(center.Z)) throw new ArgumentOutOfRangeException(nameof(center));
        if (!double.IsFinite(semiAxisA) || semiAxisA <= 0d) throw new ArgumentOutOfRangeException(nameof(semiAxisA));
        if (!double.IsFinite(semiAxisB) || semiAxisB <= 0d) throw new ArgumentOutOfRangeException(nameof(semiAxisB));
        var normal = planeNormal.ToVector(); var reference = axisU.ToVector(); var projected = reference - normal * reference.Dot(normal);
        if (!Direction3D.TryCreate(projected, out var u)) throw new ArgumentOutOfRangeException(nameof(axisU), "AxisU must lie in the hyperbola plane.");
        if (!Direction3D.TryCreate(normal.Cross(u.ToVector()), out var v)) throw new ArgumentOutOfRangeException(nameof(planeNormal), "Plane normal and AxisU must define a right-handed frame.");
        Center = center; PlaneNormal = planeNormal; AxisU = u; AxisV = v; SemiAxisA = semiAxisA; SemiAxisB = semiAxisB; Branch = branch;
    }

    public Point3D Center { get; }
    public Direction3D PlaneNormal { get; }
    public Direction3D AxisU { get; }
    public Direction3D AxisV { get; }
    public double SemiAxisA { get; }
    public double SemiAxisB { get; }
    public HyperbolaBranch Branch { get; }
    public double BranchSign => Branch == HyperbolaBranch.PositiveAxisU ? 1d : -1d;

    public Point3D Evaluate(double parameter)
    {
        if (!double.IsFinite(parameter) || double.Abs(parameter) > 700d) throw new ArgumentOutOfRangeException(nameof(parameter), "Hyperbola parameter must be finite and safely evaluable.");
        var p = Center + AxisU.ToVector() * (BranchSign * SemiAxisA * double.Cosh(parameter)) + AxisV.ToVector() * (SemiAxisB * double.Sinh(parameter));
        if (!double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Z)) throw new ArgumentOutOfRangeException(nameof(parameter), "Hyperbola evaluation is non-finite.");
        return p;
    }
    public Vector3D FirstDerivative(double parameter)
    {
        ValidateParameter(parameter);
        return AxisU.ToVector() * (BranchSign * SemiAxisA * double.Sinh(parameter)) + AxisV.ToVector() * (SemiAxisB * double.Cosh(parameter));
    }

    public Vector3D SecondDerivative(double parameter)
    {
        ValidateParameter(parameter);
        return AxisU.ToVector() * (BranchSign * SemiAxisA * double.Cosh(parameter)) + AxisV.ToVector() * (SemiAxisB * double.Sinh(parameter));
    }

    public Direction3D Tangent(double parameter) => Direction3D.Create(FirstDerivative(parameter));

    /// <summary>
    /// Returns the same geometric branch with reversed parameter direction.  B-rep edge
    /// orientation remains owned by <see cref="Brep.EdgeGeometryBinding"/>; this helper is
    /// useful only where a support itself must be reparameterized.
    /// </summary>
    public Hyperbola3Curve Reverse() => new(Center, Direction3D.Create(-PlaneNormal.ToVector()), AxisU, SemiAxisA, SemiAxisB, Branch);

    private static void ValidateParameter(double parameter)
    {
        if (!double.IsFinite(parameter) || double.Abs(parameter) > 700d)
        {
            throw new ArgumentOutOfRangeException(nameof(parameter), "Hyperbola parameter must be finite and safely evaluable.");
        }
    }
}

public enum HyperbolaBranch { PositiveAxisU, NegativeAxisU }
