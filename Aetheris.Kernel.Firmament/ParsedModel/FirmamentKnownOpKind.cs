namespace Aetheris.Kernel.Firmament.ParsedModel;

public enum FirmamentKnownOpKind
{
    Box,
    Cylinder,
    Cone,
    Torus,
    Sphere,
    TriangularPrism,
    HexagonalPrism,
    StraightSlot,
    Add,
    Subtract,
    Intersect,
    Draft,
    ExpectExists,
    ExpectSelectable,
    ExpectManifold,
    PatternLinear,
    PatternCircular,
    PatternMirror
}

internal static class FirmamentKnownOpKinds
{
    private static readonly IReadOnlyDictionary<string, FirmamentKnownOpKind> Registry =
        new Dictionary<string, FirmamentKnownOpKind>(StringComparer.Ordinal)
        {
            ["box"] = FirmamentKnownOpKind.Box,
            ["cylinder"] = FirmamentKnownOpKind.Cylinder,
            ["cone"] = FirmamentKnownOpKind.Cone,
            ["torus"] = FirmamentKnownOpKind.Torus,
            ["sphere"] = FirmamentKnownOpKind.Sphere,
            ["triangular_prism"] = FirmamentKnownOpKind.TriangularPrism,
            ["hexagonal_prism"] = FirmamentKnownOpKind.HexagonalPrism,
            ["straight_slot"] = FirmamentKnownOpKind.StraightSlot,
            ["add"] = FirmamentKnownOpKind.Add,
            ["subtract"] = FirmamentKnownOpKind.Subtract,
            ["intersect"] = FirmamentKnownOpKind.Intersect,
            ["draft"] = FirmamentKnownOpKind.Draft,
            ["expect_exists"] = FirmamentKnownOpKind.ExpectExists,
            ["expect_selectable"] = FirmamentKnownOpKind.ExpectSelectable,
            ["expect_manifold"] = FirmamentKnownOpKind.ExpectManifold,
            ["pattern_linear"] = FirmamentKnownOpKind.PatternLinear,
            ["pattern_circular"] = FirmamentKnownOpKind.PatternCircular,
            ["pattern_mirror"] = FirmamentKnownOpKind.PatternMirror
        };

    public static bool TryParse(string opName, out FirmamentKnownOpKind kind) =>
        Registry.TryGetValue(opName, out kind);

    public static FirmamentOpFamily ClassifyFamily(FirmamentKnownOpKind kind) =>
        kind switch
        {
            FirmamentKnownOpKind.Box => FirmamentOpFamily.Primitive,
            FirmamentKnownOpKind.Cylinder => FirmamentOpFamily.Primitive,
            FirmamentKnownOpKind.Cone => FirmamentOpFamily.Primitive,
            FirmamentKnownOpKind.Torus => FirmamentOpFamily.Primitive,
            FirmamentKnownOpKind.Sphere => FirmamentOpFamily.Primitive,
            FirmamentKnownOpKind.TriangularPrism => FirmamentOpFamily.Primitive,
            FirmamentKnownOpKind.HexagonalPrism => FirmamentOpFamily.Primitive,
            FirmamentKnownOpKind.StraightSlot => FirmamentOpFamily.Primitive,
            FirmamentKnownOpKind.Add => FirmamentOpFamily.Boolean,
            FirmamentKnownOpKind.Subtract => FirmamentOpFamily.Boolean,
            FirmamentKnownOpKind.Intersect => FirmamentOpFamily.Boolean,
            FirmamentKnownOpKind.Draft => FirmamentOpFamily.Boolean,
            FirmamentKnownOpKind.ExpectExists => FirmamentOpFamily.Validation,
            FirmamentKnownOpKind.ExpectSelectable => FirmamentOpFamily.Validation,
            FirmamentKnownOpKind.ExpectManifold => FirmamentOpFamily.Validation,
            FirmamentKnownOpKind.PatternLinear => FirmamentOpFamily.Pattern,
            FirmamentKnownOpKind.PatternCircular => FirmamentOpFamily.Pattern,
            FirmamentKnownOpKind.PatternMirror => FirmamentOpFamily.Pattern,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Firmament known op kind.")
        };
}
