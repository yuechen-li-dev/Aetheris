namespace Aetheris.Kernel.Firmament.ParsedModel;

public enum FirmamentKnownOpKind
{
    Box,
    Cylinder,
    Sphere,
    Add,
    Subtract,
    Intersect,
    ExpectExists,
    ExpectSelectable,
    ExpectManifold
}

internal static class FirmamentKnownOpKinds
{
    private static readonly IReadOnlyDictionary<string, FirmamentKnownOpKind> Registry =
        new Dictionary<string, FirmamentKnownOpKind>(StringComparer.Ordinal)
        {
            ["box"] = FirmamentKnownOpKind.Box,
            ["cylinder"] = FirmamentKnownOpKind.Cylinder,
            ["sphere"] = FirmamentKnownOpKind.Sphere,
            ["add"] = FirmamentKnownOpKind.Add,
            ["subtract"] = FirmamentKnownOpKind.Subtract,
            ["intersect"] = FirmamentKnownOpKind.Intersect,
            ["expect_exists"] = FirmamentKnownOpKind.ExpectExists,
            ["expect_selectable"] = FirmamentKnownOpKind.ExpectSelectable,
            ["expect_manifold"] = FirmamentKnownOpKind.ExpectManifold
        };

    public static bool TryParse(string opName, out FirmamentKnownOpKind kind) =>
        Registry.TryGetValue(opName, out kind);
}
