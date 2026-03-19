namespace Aetheris.Kernel.Firmament.ParsedModel;

public enum FirmamentKnownOpKind
{
    Box,
    Cylinder,
    Cone,
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
            ["cone"] = FirmamentKnownOpKind.Cone,
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

    public static FirmamentOpFamily ClassifyFamily(FirmamentKnownOpKind kind) =>
        kind switch
        {
            FirmamentKnownOpKind.Box => FirmamentOpFamily.Primitive,
            FirmamentKnownOpKind.Cylinder => FirmamentOpFamily.Primitive,
            FirmamentKnownOpKind.Cone => FirmamentOpFamily.Primitive,
            FirmamentKnownOpKind.Sphere => FirmamentOpFamily.Primitive,
            FirmamentKnownOpKind.Add => FirmamentOpFamily.Boolean,
            FirmamentKnownOpKind.Subtract => FirmamentOpFamily.Boolean,
            FirmamentKnownOpKind.Intersect => FirmamentOpFamily.Boolean,
            FirmamentKnownOpKind.ExpectExists => FirmamentOpFamily.Validation,
            FirmamentKnownOpKind.ExpectSelectable => FirmamentOpFamily.Validation,
            FirmamentKnownOpKind.ExpectManifold => FirmamentOpFamily.Validation,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Firmament known op kind.")
        };
}
