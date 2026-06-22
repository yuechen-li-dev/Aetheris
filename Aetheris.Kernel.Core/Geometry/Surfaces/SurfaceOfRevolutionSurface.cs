using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Surfaces;

public readonly record struct SurfaceOfRevolutionSurface
{
    public SurfaceOfRevolutionSurface(CurveGeometry directrix, Point3D axisOrigin, Direction3D axisDirection)
    {
        Directrix = directrix ?? throw new ArgumentNullException(nameof(directrix));
        AxisOrigin = axisOrigin;
        AxisDirection = axisDirection;
    }

    public CurveGeometry Directrix { get; }

    public Point3D AxisOrigin { get; }

    public Direction3D AxisDirection { get; }
}
