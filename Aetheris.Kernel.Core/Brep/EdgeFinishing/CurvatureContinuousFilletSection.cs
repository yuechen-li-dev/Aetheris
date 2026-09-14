using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.EdgeFinishing;

/// <summary>The qualified fixed quintic quarter-section, shared by local and loop fillets.</summary>
public static class CurvatureContinuousFilletSection
{
    public static BSpline3Curve CreateQuarter(Point3D start, Point3D end, Point3D center)
    {
        var fromCenter = start - center;
        var toCenter = end - center;
        if (fromCenter.Length <= 0 || toCenter.Length <= 0 ||
            !double.IsFinite(fromCenter.Length + toCenter.Length) ||
            System.Math.Abs(fromCenter.Dot(toCenter)) > 1e-12 * fromCenter.Length * toCenter.Length)
            throw new ArgumentException("Fillet quarter requires finite nonzero orthogonal support directions.");
        return new BSpline3Curve(5,
            [start, start + toCenter * 0.25, start + toCenter * 0.5,
             end + fromCenter * 0.5, end + fromCenter * 0.25, end],
            [6, 6], [0, 1], "UNSPECIFIED", false, false, "UNSPECIFIED");
    }
}
