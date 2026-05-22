using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Materializer;

public enum ThroughHoleHostKind
{
    RectangularBox,
    Unsupported
}

public enum ThroughHoleToolKind
{
    Cylindrical,
    Unsupported
}

public enum ThroughHoleProfileKind
{
    Circular,
    Unsupported
}

public enum ThroughHoleAxisKind
{
    Z,
    Unsupported
}

public enum ThroughHoleSurfaceRole
{
    EntryFace,
    ExitFace,
    HostRetainedPlanarFaces,
    CylindricalWall
}

public enum ThroughHoleTrimRole
{
    CircularRimTrim,
    Deferred
}

public sealed record ThroughHoleSurfaceParticipation(ThroughHoleSurfaceRole Role, string Description);

public sealed record ThroughHoleExpectedPatch(ThroughHoleSurfaceRole Role, string Description);

public sealed record ThroughHoleExpectedTrim(ThroughHoleTrimRole Role, string Description);

public sealed record ThroughHoleRecoveryPlan(
    ThroughHoleHostKind HostKind,
    ThroughHoleToolKind ToolKind,
    ThroughHoleProfileKind ProfileKind,
    ThroughHoleAxisKind Axis,
    double ThroughLength,
    double ToolRadius,
    double HostSizeX,
    double HostSizeY,
    double HostSizeZ,
    Vector3D HostTranslation,
    Vector3D ToolTranslation,
    IReadOnlyList<ThroughHoleSurfaceParticipation> EntryExitSurfaces,
    IReadOnlyList<ThroughHoleExpectedPatch> ExpectedPatches,
    IReadOnlyList<ThroughHoleExpectedTrim> ExpectedTrims,
    FrepMaterializerCapability Capability,
    IReadOnlyList<string> Diagnostics);
