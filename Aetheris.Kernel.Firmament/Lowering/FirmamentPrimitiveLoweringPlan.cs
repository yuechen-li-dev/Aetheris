using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Lowering;

public sealed record FirmamentPrimitiveLoweringPlan(
    IReadOnlyList<FirmamentLoweredPrimitive> Primitives,
    IReadOnlyList<FirmamentLoweredBoolean> Booleans,
    IReadOnlyList<FirmamentLoweringSkippedOp> SkippedOps);

public sealed record FirmamentLoweredPrimitive(
    int OpIndex,
    string FeatureId,
    FirmamentLoweredPrimitiveKind Kind,
    FirmamentLoweredPrimitiveParameters Parameters,
    FirmamentLoweredPlacement? Placement);

public sealed record FirmamentLoweredPlacement(
    FirmamentLoweredPlacementAnchor? On,
    IReadOnlyList<double> Offset,
    string? OnFace,
    string? CenteredOn,
    string? AroundAxis,
    double? RadialOffset,
    double? AngleDegrees,
    IReadOnlyList<string> UnknownFields);

public abstract record FirmamentLoweredPlacementAnchor;

public sealed record FirmamentLoweredPlacementOriginAnchor : FirmamentLoweredPlacementAnchor;

public sealed record FirmamentLoweredPlacementSelectorAnchor(string Selector) : FirmamentLoweredPlacementAnchor;

public sealed record FirmamentLoweredBoolean(
    int OpIndex,
    string FeatureId,
    FirmamentLoweredBooleanKind Kind,
    string PrimaryReferenceField,
    string PrimaryReferenceFeatureId,
    FirmamentLoweredToolOp Tool,
    FirmamentLoweredPlacement? Placement);

public enum FirmamentLoweredBooleanKind
{
    Add,
    Subtract,
    Intersect,
    Draft,
    Chamfer,
    Fillet
}

public enum FirmamentLoweredPrimitiveKind
{
    Box,
    Cylinder,
    Cone,
    Torus,
    Sphere,
    TriangularPrism,
    HexagonalPrism,
    StraightSlot,
    RoundedCornerBox
}

public abstract record FirmamentLoweredPrimitiveParameters;

public sealed record FirmamentLoweredBoxParameters(double SizeX, double SizeY, double SizeZ)
    : FirmamentLoweredPrimitiveParameters;

public sealed record FirmamentLoweredCylinderParameters(double Radius, double Height)
    : FirmamentLoweredPrimitiveParameters;

public sealed record FirmamentLoweredConeParameters(double BottomRadius, double TopRadius, double Height)
    : FirmamentLoweredPrimitiveParameters;

public sealed record FirmamentLoweredTorusParameters(double MajorRadius, double MinorRadius)
    : FirmamentLoweredPrimitiveParameters;

public sealed record FirmamentLoweredSphereParameters(double Radius)
    : FirmamentLoweredPrimitiveParameters;

public sealed record FirmamentLoweredTriangularPrismParameters(double BaseWidth, double BaseDepth, double Height)
    : FirmamentLoweredPrimitiveParameters;

public sealed record FirmamentLoweredHexagonalPrismParameters(double AcrossFlats, double Height)
    : FirmamentLoweredPrimitiveParameters;

public sealed record FirmamentLoweredStraightSlotParameters(double Length, double Width, double Height)
    : FirmamentLoweredPrimitiveParameters;

public sealed record FirmamentLoweredRoundedCornerBoxParameters(double Width, double Depth, double Height, double CornerRadius)
    : FirmamentLoweredPrimitiveParameters;

public sealed record FirmamentLoweredToolOp(
    string OpName,
    IReadOnlyDictionary<string, string> RawFields,
    string RawValue);

public sealed record FirmamentLoweringSkippedOp(
    int OpIndex,
    string OpName,
    FirmamentKnownOpKind KnownKind,
    FirmamentOpFamily Family,
    string Reason);
