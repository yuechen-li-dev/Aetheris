using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Validation;

internal static class FirmamentSelectorContracts
{
    private static readonly IReadOnlyDictionary<string, FirmamentSelectorPortContract> BoxPorts =
        new Dictionary<string, FirmamentSelectorPortContract>(StringComparer.Ordinal)
        {
            ["top_face"] = new(FirmamentSelectorResultKind.Face, FirmamentSelectorCardinality.One),
            ["bottom_face"] = new(FirmamentSelectorResultKind.Face, FirmamentSelectorCardinality.One),
            ["side_faces"] = new(FirmamentSelectorResultKind.FaceSet, FirmamentSelectorCardinality.Many),
            ["edges"] = new(FirmamentSelectorResultKind.EdgeSet, FirmamentSelectorCardinality.Many),
            ["vertices"] = new(FirmamentSelectorResultKind.VertexSet, FirmamentSelectorCardinality.Many)
        };

    private static readonly IReadOnlyDictionary<string, FirmamentSelectorPortContract> CylinderPorts =
        new Dictionary<string, FirmamentSelectorPortContract>(StringComparer.Ordinal)
        {
            ["top_face"] = new(FirmamentSelectorResultKind.Face, FirmamentSelectorCardinality.One),
            ["bottom_face"] = new(FirmamentSelectorResultKind.Face, FirmamentSelectorCardinality.One),
            ["side_face"] = new(FirmamentSelectorResultKind.Face, FirmamentSelectorCardinality.One),
            ["circular_edges"] = new(FirmamentSelectorResultKind.EdgeSet, FirmamentSelectorCardinality.Many),
            ["edges"] = new(FirmamentSelectorResultKind.EdgeSet, FirmamentSelectorCardinality.Many),
            ["vertices"] = new(FirmamentSelectorResultKind.VertexSet, FirmamentSelectorCardinality.Many)
        };

    private static readonly IReadOnlyDictionary<string, FirmamentSelectorPortContract> SpherePorts =
        new Dictionary<string, FirmamentSelectorPortContract>(StringComparer.Ordinal)
        {
            ["surface"] = new(FirmamentSelectorResultKind.Face, FirmamentSelectorCardinality.One),
            ["edges"] = new(FirmamentSelectorResultKind.EdgeSet, FirmamentSelectorCardinality.Many),
            ["vertices"] = new(FirmamentSelectorResultKind.VertexSet, FirmamentSelectorCardinality.Many)
        };

    private static readonly IReadOnlyDictionary<string, FirmamentSelectorPortContract> BooleanPorts =
        new Dictionary<string, FirmamentSelectorPortContract>(StringComparer.Ordinal)
        {
            ["top_face"] = new(FirmamentSelectorResultKind.Face, FirmamentSelectorCardinality.One),
            ["bottom_face"] = new(FirmamentSelectorResultKind.Face, FirmamentSelectorCardinality.One),
            ["side_faces"] = new(FirmamentSelectorResultKind.FaceSet, FirmamentSelectorCardinality.Many),
            ["edges"] = new(FirmamentSelectorResultKind.EdgeSet, FirmamentSelectorCardinality.Many),
            ["vertices"] = new(FirmamentSelectorResultKind.VertexSet, FirmamentSelectorCardinality.Many)
        };

    public static bool TryGetAllowedPorts(FirmamentKnownOpKind featureKind, out IReadOnlySet<string> allowedPorts)
    {
        switch (featureKind)
        {
            case FirmamentKnownOpKind.Box:
                allowedPorts = BoxAllowedPorts;
                return true;
            case FirmamentKnownOpKind.Cylinder:
                allowedPorts = CylinderAllowedPorts;
                return true;
            case FirmamentKnownOpKind.Sphere:
                allowedPorts = SphereAllowedPorts;
                return true;
            case FirmamentKnownOpKind.Add:
            case FirmamentKnownOpKind.Subtract:
            case FirmamentKnownOpKind.Intersect:
                allowedPorts = BooleanAllowedPorts;
                return true;
            default:
                allowedPorts = EmptyPorts;
                return false;
        }
    }

    public static bool TryGetPortContract(FirmamentKnownOpKind featureKind, string portToken, out FirmamentSelectorPortContract contract)
    {
        if (TryGetContracts(featureKind, out var contracts) && contracts.TryGetValue(portToken, out var value))
        {
            contract = value;
            return true;
        }

        contract = null!;
        return false;
    }

    private static bool TryGetContracts(FirmamentKnownOpKind featureKind, out IReadOnlyDictionary<string, FirmamentSelectorPortContract> contracts)
    {
        switch (featureKind)
        {
            case FirmamentKnownOpKind.Box:
                contracts = BoxPorts;
                return true;
            case FirmamentKnownOpKind.Cylinder:
                contracts = CylinderPorts;
                return true;
            case FirmamentKnownOpKind.Sphere:
                contracts = SpherePorts;
                return true;
            case FirmamentKnownOpKind.Add:
            case FirmamentKnownOpKind.Subtract:
            case FirmamentKnownOpKind.Intersect:
                contracts = BooleanPorts;
                return true;
            default:
                contracts = EmptyContracts;
                return false;
        }
    }

    private static readonly IReadOnlyDictionary<string, FirmamentSelectorPortContract> EmptyContracts =
        new Dictionary<string, FirmamentSelectorPortContract>(StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> BoxAllowedPorts = new HashSet<string>(BoxPorts.Keys, StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> CylinderAllowedPorts = new HashSet<string>(CylinderPorts.Keys, StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> SphereAllowedPorts = new HashSet<string>(SpherePorts.Keys, StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> BooleanAllowedPorts = new HashSet<string>(BooleanPorts.Keys, StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> EmptyPorts = new HashSet<string>(StringComparer.Ordinal);
}
