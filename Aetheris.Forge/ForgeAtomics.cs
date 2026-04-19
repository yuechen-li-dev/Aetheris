using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Forge;

public static class ForgeAtomics
{
    public static KernelResult<ForgeRoundedRectangleProfile> RoundedRectangle(double width, double depth, double cornerRadius, int cornerSegments = 8) =>
        ForgeRoundedRectangleProfile.Create(width, depth, cornerRadius, cornerSegments);

    public static KernelResult<BrepBody> ExtrudeCentered(ForgeRoundedRectangleProfile profile, double height)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!double.IsFinite(height) || height <= 0d)
        {
            return KernelResult<BrepBody>.Failure(
            [
                new Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic(
                    Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.InvalidArgument,
                    Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
                    "height must be finite and greater than zero.")
            ]);
        }

        var polylineProfile = profile.ToPolylineProfile();
        if (!polylineProfile.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(polylineProfile.Diagnostics);
        }

        return BrepExtrude.Create(
            polylineProfile.Value,
            new ExtrudeFrame3D(
                new Point3D(0d, 0d, -height * 0.5d),
                Direction3D.Create(new Vector3D(0d, 0d, 1d)),
                Direction3D.Create(new Vector3D(1d, 0d, 0d))),
            height);
    }
}
