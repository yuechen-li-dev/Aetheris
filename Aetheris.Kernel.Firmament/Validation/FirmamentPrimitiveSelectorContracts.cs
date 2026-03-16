using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentPrimitiveSelectorContracts
{
    private static readonly IReadOnlySet<string> BoxPorts = new HashSet<string>(StringComparer.Ordinal)
    {
        "top_face",
        "bottom_face",
        "side_faces",
        "edges",
        "vertices"
    };

    private static readonly IReadOnlySet<string> CylinderPorts = new HashSet<string>(StringComparer.Ordinal)
    {
        "top_face",
        "bottom_face",
        "side_face",
        "circular_edges",
        "edges",
        "vertices"
    };

    private static readonly IReadOnlySet<string> SpherePorts = new HashSet<string>(StringComparer.Ordinal)
    {
        "surface",
        "edges",
        "vertices"
    };

    public static bool TryGetAllowedPorts(FirmamentKnownOpKind featureKind, out IReadOnlySet<string> allowedPorts)
    {
        switch (featureKind)
        {
            case FirmamentKnownOpKind.Box:
                allowedPorts = BoxPorts;
                return true;
            case FirmamentKnownOpKind.Cylinder:
                allowedPorts = CylinderPorts;
                return true;
            case FirmamentKnownOpKind.Sphere:
                allowedPorts = SpherePorts;
                return true;
            default:
                allowedPorts = EmptyPorts;
                return false;
        }
    }

    private static readonly IReadOnlySet<string> EmptyPorts = new HashSet<string>(StringComparer.Ordinal);
}
