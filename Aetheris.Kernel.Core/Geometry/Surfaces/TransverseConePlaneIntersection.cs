using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Geometry.Surfaces;

/// <summary>
/// Exact, deliberately narrow intersection for a signed-permutation transverse cone
/// and a world-Z section plane.  It is not a general surface/surface intersection
/// service: this is the analytic route used by section-stack partition planning.
/// </summary>
public static class TransverseConePlaneIntersection
{
    private const double DirectionTolerance = 1e-10d;
    private const double DegeneracyTolerance = 1e-10d;

    public static KernelResult<Hyperbola3Curve> IntersectWorldZ(ConeSurface cone, double worldZ)
    {
        if (!double.IsFinite(worldZ))
        {
            return Failure("World-Z plane coordinate must be finite.", "Geometry.TransverseConePlane.NonFinitePlane");
        }

        var axis = cone.Axis.ToVector();
        var isSignedX = double.Abs(double.Abs(axis.X) - 1d) <= DirectionTolerance
            && double.Abs(axis.Y) <= DirectionTolerance && double.Abs(axis.Z) <= DirectionTolerance;
        var isSignedY = double.Abs(double.Abs(axis.Y) - 1d) <= DirectionTolerance
            && double.Abs(axis.X) <= DirectionTolerance && double.Abs(axis.Z) <= DirectionTolerance;
        if (!isSignedX && !isSignedY)
        {
            return Failure("Only signed-permutation transverse cone axes (+/-X, +/-Y) are admitted.", "Geometry.TransverseConePlane.UnsupportedAxis");
        }

        var zOffset = worldZ - cone.Apex.Z;
        if (double.Abs(zOffset) <= DegeneracyTolerance)
        {
            return Failure("A world-Z plane through the cone apex is a degenerate pair of lines, not a bounded hyperbola trim.", "Geometry.TransverseConePlane.ApexDegeneracy");
        }

        var tangent = double.Tan(cone.SemiAngleRadians);
        if (!double.IsFinite(tangent) || tangent <= DegeneracyTolerance)
        {
            return Failure("Cone semi-angle does not produce a finite transverse hyperbola.", "Geometry.TransverseConePlane.InvalidSemiAngle");
        }

        var planeNormal = Direction3D.Create(new Vector3D(0d, 0d, 1d));
        var center = new Point3D(cone.Apex.X, cone.Apex.Y, worldZ);
        var semiAxisA = double.Abs(zOffset) / tangent;
        var semiAxisB = double.Abs(zOffset);
        try
        {
            // The forward cone sheet v >= 0 is the positive axial branch.  AxisV is
            // derived right-handed from +Z x the drilling axis, so +/-X and +/-Y keep
            // their construction-plane orientation without camera-relative aliases.
            return KernelResult<Hyperbola3Curve>.Success(new Hyperbola3Curve(
                center,
                planeNormal,
                cone.Axis,
                semiAxisA,
                semiAxisB,
                HyperbolaBranch.PositiveAxisU));
        }
        catch (ArgumentException exception)
        {
            return Failure($"Unable to construct transverse cone hyperbola: {exception.Message}", "Geometry.TransverseConePlane.FrameInvalid");
        }
    }

    private static KernelResult<Hyperbola3Curve> Failure(string message, string source)
        => KernelResult<Hyperbola3Curve>.Failure([
            new KernelDiagnostic(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message, source)]);
}
