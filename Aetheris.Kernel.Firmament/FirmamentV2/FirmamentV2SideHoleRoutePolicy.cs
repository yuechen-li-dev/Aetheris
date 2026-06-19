namespace Aetheris.Kernel.Firmament.FirmamentV2;

public static class FirmamentV2SideHoleRoutePolicy
{
    public const string RouteUnsupportedDiagnostic = "firmament-v2-side-hole-route-unsupported";
    public const string SameFaceUnsupportedDiagnostic = "firmament-v2-side-hole-same-face-unsupported";
    public const string CenterExceedsClearanceDiagnostic = "firmament-v2-side-hole-center-exceeds-clearance";
    public const string RadiusExceedsClearanceDiagnostic = "firmament-v2-side-hole-radius-exceeds-clearance";

    private static readonly IReadOnlyDictionary<string, RouteDefinition> Routes = new Dictionary<string, RouteDefinition>(StringComparer.Ordinal)
    {
        ["+X->-X"] = new("X", "+X", "-X", "face(+X):u=+Y,v=+Z", "+Y", "+Z", BoxDimension.Y, BoxDimension.Z),
        ["-X->+X"] = new("X", "-X", "+X", "face(-X):u=+Y,v=+Z", "+Y", "+Z", BoxDimension.Y, BoxDimension.Z),
        ["+Y->-Y"] = new("Y", "+Y", "-Y", "face(+Y):u=+X,v=+Z", "+X", "+Z", BoxDimension.X, BoxDimension.Z),
        ["-Y->+Y"] = new("Y", "-Y", "+Y", "face(-Y):u=+X,v=+Z", "+X", "+Z", BoxDimension.X, BoxDimension.Z),
        ["+Z->-Z"] = new("Z", "+Z", "-Z", "face(+Z):u=+X,v=+Y", "+X", "+Y", BoxDimension.X, BoxDimension.Y),
        ["-Z->+Z"] = new("Z", "-Z", "+Z", "face(-Z):u=+X,v=+Y", "+X", "+Y", BoxDimension.X, BoxDimension.Y),
    };

    public static IReadOnlyList<string> SupportedDirections => Routes.Keys.Order(StringComparer.Ordinal).ToArray();

    public static FirmamentV2SideHoleRoutePolicyResult Resolve(string attachFace, string throughFace, IReadOnlyList<double> boxSize, double radius, double centerU = 0, double centerV = 0)
    {
        ArgumentNullException.ThrowIfNull(attachFace);
        ArgumentNullException.ThrowIfNull(throughFace);
        ArgumentNullException.ThrowIfNull(boxSize);
        if (boxSize.Count != 3) throw new ArgumentException("Box size must contain X, Y, and Z dimensions.", nameof(boxSize));

        var direction = Direction(attachFace, throughFace);
        if (!Routes.TryGetValue(direction, out var route))
        {
            var diagnostic = string.Equals(attachFace, throughFace, StringComparison.Ordinal)
                ? SameFaceUnsupportedDiagnostic
                : RouteUnsupportedDiagnostic;
            return FirmamentV2SideHoleRoutePolicyResult.Rejected(diagnostic, direction);
        }

        var uHalfExtent = HalfExtent(boxSize, route.UHalfExtentDimension);
        var vHalfExtent = HalfExtent(boxSize, route.VHalfExtentDimension);
        var evidence = new FirmamentV2SideHoleRoutePolicyEvidence(
            route.Axis,
            direction,
            route.AttachFace,
            route.ThroughFace,
            route.CenterFrame,
            route.UAxis,
            route.VAxis,
            uHalfExtent,
            vHalfExtent);

        if (radius >= Math.Min(uHalfExtent, vHalfExtent))
            return FirmamentV2SideHoleRoutePolicyResult.Rejected(RadiusExceedsClearanceDiagnostic, direction, evidence);

        if (!HasStrictClearance(centerU, centerV, radius, uHalfExtent, vHalfExtent))
            return FirmamentV2SideHoleRoutePolicyResult.Rejected(CenterExceedsClearanceDiagnostic, direction, evidence);

        return FirmamentV2SideHoleRoutePolicyResult.Supported(evidence);
    }

    public static bool HasStrictClearance(double centerU, double centerV, double radius, double uHalfExtent, double vHalfExtent) =>
        Math.Abs(centerU) + radius < uHalfExtent && Math.Abs(centerV) + radius < vHalfExtent;

    public static FirmamentV2SideHoleRoutePolicyEvidence? EvidenceFor(string attachFace, string throughFace, IReadOnlyList<double> boxSize)
    {
        var result = Resolve(attachFace, throughFace, boxSize, 0);
        return result.Route;
    }

    private static string Direction(string attachFace, string throughFace) => $"{attachFace}->{throughFace}";

    private static double HalfExtent(IReadOnlyList<double> boxSize, BoxDimension dimension) => boxSize[(int)dimension] / 2.0;

    private enum BoxDimension { X = 0, Y = 1, Z = 2 }

    private sealed record RouteDefinition(string Axis, string AttachFace, string ThroughFace, string CenterFrame, string UAxis, string VAxis, BoxDimension UHalfExtentDimension, BoxDimension VHalfExtentDimension);
}

public sealed record FirmamentV2SideHoleRoutePolicyEvidence(string Axis, string Direction, string AttachFace, string ThroughFace, string CenterFrame, string UAxis, string VAxis, double UHalfExtent, double VHalfExtent);

public sealed record FirmamentV2SideHoleRoutePolicyResult(bool IsSupported, string? Diagnostic, string Direction, FirmamentV2SideHoleRoutePolicyEvidence? Route)
{
    public static FirmamentV2SideHoleRoutePolicyResult Supported(FirmamentV2SideHoleRoutePolicyEvidence route) => new(true, null, route.Direction, route);
    public static FirmamentV2SideHoleRoutePolicyResult Rejected(string diagnostic, string direction, FirmamentV2SideHoleRoutePolicyEvidence? route = null) => new(false, diagnostic, direction, route);
}
