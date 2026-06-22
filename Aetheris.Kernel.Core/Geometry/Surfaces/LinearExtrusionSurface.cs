using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Surfaces;

public readonly record struct LinearExtrusionSurface
{
    public LinearExtrusionSurface(CurveGeometry directrix, Vector3D extrusionVector)
    {
        if (directrix is null)
        {
            throw new ArgumentNullException(nameof(directrix));
        }

        if (!double.IsFinite(extrusionVector.X)
            || !double.IsFinite(extrusionVector.Y)
            || !double.IsFinite(extrusionVector.Z)
            || extrusionVector.LengthSquared <= 1e-24d)
        {
            throw new ArgumentOutOfRangeException(nameof(extrusionVector), "Extrusion vector must be finite and non-zero.");
        }

        Directrix = directrix;
        ExtrusionVector = extrusionVector;
    }

    public CurveGeometry Directrix { get; }

    public Vector3D ExtrusionVector { get; }
}
