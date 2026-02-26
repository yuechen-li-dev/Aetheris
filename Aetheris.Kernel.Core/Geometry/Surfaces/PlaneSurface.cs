using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Surfaces;

/// <summary>
/// Plane parameterization: S(u,v) = Origin + u * UAxis + v * VAxis, with VAxis = Normal x UAxis.
/// </summary>
public readonly record struct PlaneSurface
{
    private readonly Direction3D _uAxis;
    private readonly Direction3D _vAxis;

    public PlaneSurface(Point3D origin, Direction3D normal, Direction3D uAxis)
    {
        var n = normal.ToVector();
        var uReference = uAxis.ToVector();
        var projected = uReference - (n * uReference.Dot(n));

        if (!Direction3D.TryCreate(projected, out var normalizedU))
        {
            throw new ArgumentOutOfRangeException(nameof(uAxis), "U axis must not be parallel to normal.");
        }

        if (!Direction3D.TryCreate(n.Cross(normalizedU.ToVector()), out var normalizedV))
        {
            throw new ArgumentOutOfRangeException(nameof(normal), "Normal must define a valid plane basis.");
        }

        Origin = origin;
        Normal = normal;
        _uAxis = normalizedU;
        _vAxis = normalizedV;
    }

    public Point3D Origin { get; }

    public Direction3D Normal { get; }

    public Direction3D UAxis => _uAxis;

    public Direction3D VAxis => _vAxis;

    public Point3D Evaluate(double u, double v) => Origin + (_uAxis.ToVector() * u) + (_vAxis.ToVector() * v);
}
