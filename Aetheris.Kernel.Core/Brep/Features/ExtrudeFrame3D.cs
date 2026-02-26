using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Features;

/// <summary>
/// Local frame used to map profile-space (u,v) points into world-space for extrusion.
/// Extrusion direction is +Normal.
/// </summary>
public readonly record struct ExtrudeFrame3D
{
    private readonly Direction3D _uAxis;
    private readonly Direction3D _vAxis;

    public ExtrudeFrame3D(Point3D origin, Direction3D normal, Direction3D uAxis)
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
            throw new ArgumentOutOfRangeException(nameof(normal), "Normal must define a valid frame basis.");
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

    public Point3D ToWorld(ProfilePoint2D point, double normalOffset = 0d)
        => Origin + (_uAxis.ToVector() * point.X) + (_vAxis.ToVector() * point.Y) + (Normal.ToVector() * normalOffset);
}
