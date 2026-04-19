using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public sealed record SupportedPrismaticSubtractTool(
    AxisAlignedBoxExtents Bounds,
    IReadOnlyList<(double X, double Y)> Footprint);

internal static class BrepBooleanPrismaticToolRecognition
{
    public static bool TryRecognize(BrepBody body, ToleranceContext tolerance, out SupportedPrismaticSubtractTool tool, out string reason)
    {
        tool = default!;
        if (!BrepBooleanPrismaticProfileRecognition.TryRecognize(body, tolerance, out var profile, out var profileReason))
        {
            reason = profileReason.Replace("prismatic profile", "prismatic tool", StringComparison.Ordinal);
            return false;
        }

        tool = new SupportedPrismaticSubtractTool(profile.Bounds, profile.Footprint);
        reason = string.Empty;
        return true;
    }
}
