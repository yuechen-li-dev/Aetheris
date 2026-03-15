using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Lowering;

public sealed record FirmamentPrimitiveLoweringPlan(
    IReadOnlyList<FirmamentLoweredPrimitive> Primitives,
    IReadOnlyList<FirmamentLoweringSkippedOp> SkippedOps);

public sealed record FirmamentLoweredPrimitive(
    string FeatureId,
    FirmamentLoweredPrimitiveKind Kind,
    FirmamentLoweredPrimitiveParameters Parameters);

public enum FirmamentLoweredPrimitiveKind
{
    Box,
    Cylinder,
    Sphere
}

public abstract record FirmamentLoweredPrimitiveParameters;

public sealed record FirmamentLoweredBoxParameters(double SizeX, double SizeY, double SizeZ)
    : FirmamentLoweredPrimitiveParameters;

public sealed record FirmamentLoweredCylinderParameters(double Radius, double Height)
    : FirmamentLoweredPrimitiveParameters;

public sealed record FirmamentLoweredSphereParameters(double Radius)
    : FirmamentLoweredPrimitiveParameters;

public sealed record FirmamentLoweringSkippedOp(
    int OpIndex,
    string OpName,
    FirmamentKnownOpKind KnownKind,
    FirmamentOpFamily Family,
    string Reason);
