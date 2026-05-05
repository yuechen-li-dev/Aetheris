using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class FirmamentPlacementAnchorSemantics
{
    public static bool TryResolveAuthoredFaceAnchorFromBounds(string port, Point3D min, Point3D max, out Point3D anchor)
    {
        var centerX = (min.X + max.X) * 0.5d;
        var centerY = (min.Y + max.Y) * 0.5d;

        switch (port)
        {
            case "top_face":
                anchor = new Point3D(centerX, centerY, max.Z);
                return true;
            case "bottom_face":
                anchor = new Point3D(centerX, centerY, min.Z);
                return true;
            default:
                anchor = Point3D.Origin;
                return false;
        }
    }
}
