using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Materializer;

public enum HoleHostKind { RectangularBox, Unsupported }
public enum HoleAxisKind { Z, Unsupported }
public enum HoleKind { Through, Counterbore, Unsupported }
public enum HoleDepthKind { Through, Blind, ThroughWithEntryRelief, Unsupported }
public enum HoleProfileSegmentKind { Cylindrical, Conical, Chamfer, ThreadDeferred, Unsupported }
public enum HoleEntryFeatureKind { Plain, Counterbore, Unsupported }
public enum HoleExitFeatureKind { Plain, Unsupported }
public enum HoleSurfacePatchRole { EntryFace, ExitFace, HostRetainedPlanarFaces, CylindricalWall, CounterboreFloorAnnulus, CounterboreWall }
public enum HoleTrimCurveRole { CircularRimTrim, Deferred }

public sealed record HoleProfileSegment(HoleProfileSegmentKind SegmentKind, double RadiusStart, double RadiusEnd, double DepthStart, double DepthEnd);
public sealed record HoleSurfacePatchExpectation(HoleSurfacePatchRole Role, string Description);
public sealed record HoleTrimCurveExpectation(HoleTrimCurveRole Role, string Description);

public sealed record HoleRecoveryPlan(
    HoleHostKind HostKind,
    HoleAxisKind Axis,
    HoleKind HoleKind,
    HoleDepthKind DepthKind,
    HoleEntryFeatureKind EntryFeature,
    HoleExitFeatureKind ExitFeature,
    double ThroughLength,
    double HostSizeX,
    double HostSizeY,
    double HostSizeZ,
    Vector3D HostTranslation,
    Vector3D ToolTranslation,
    IReadOnlyList<HoleProfileSegment> ProfileStack,
    IReadOnlyList<HoleSurfacePatchExpectation> ExpectedSurfacePatches,
    IReadOnlyList<HoleTrimCurveExpectation> ExpectedTrimCurves,
    FrepMaterializerCapability Capability,
    IReadOnlyList<string> Diagnostics);
