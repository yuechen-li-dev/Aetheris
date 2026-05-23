using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Materializer;

public enum HoleHostKind { RectangularBox, Unsupported }
public enum HoleAxisKind { Z, Unsupported }
public enum HoleKind { Through, Blind, Counterbore, Countersink, ChamferedEntry, Stepped, Unsupported }
public enum HoleDepthKind { Through, Blind, ThroughWithEntryRelief, BlindWithEntryRelief, Unsupported }
public enum HoleProfileSegmentKind { Cylindrical, Conical, Chamfer, ThreadDeferred, Unsupported }
public enum HoleTierAnchorSide { Top, Bottom, Through, Unknown }
public enum HoleEntryFeatureKind { Plain, Counterbore, Countersink, Chamfer, Stepped, Unsupported }
public enum HoleExitFeatureKind { Plain, ClosedBottom, Unsupported }
public enum HoleSurfacePatchRole { EntryFace, ExitFace, HostRetainedPlanarFaces, CylindricalWall, BlindBottomCap, CounterboreFloorAnnulus, CounterboreWall, CountersinkWall, ChamferedEntryWall, SteppedTransitionFloorAnnulus }
public enum HoleTrimCurveRole { CircularRimTrim, Deferred }

public sealed record HoleProfileSegment(
    HoleProfileSegmentKind SegmentKind,
    double RadiusStart,
    double RadiusEnd,
    double DepthStart,
    double DepthEnd,
    HoleTierAnchorSide AnchorSide = HoleTierAnchorSide.Unknown,
    double DepthFromAnchor = 0d,
    double ZMin = double.NaN,
    double ZMax = double.NaN,
    bool IsThrough = false,
    IReadOnlyList<string>? PlacementDiagnostics = null);
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

public static class HoleProfileSegmentPlacementValidator
{
    public static bool TryValidate(HoleRecoveryPlan plan, out IReadOnlyList<string> diagnostics)
    {
        var issues = new List<string>();
        var tol = Aetheris.Kernel.Core.Numerics.ToleranceContext.Default.Linear;
        var hostMinZ = plan.HostTranslation.Z - (plan.HostSizeZ * 0.5d);
        var hostMaxZ = plan.HostTranslation.Z + (plan.HostSizeZ * 0.5d);

        for (var i = 0; i < plan.ProfileStack.Count; i++)
        {
            var s = plan.ProfileStack[i];
            var role = $"segment[{i}]/{s.SegmentKind}";
            if (double.IsNaN(s.ZMin) || double.IsNaN(s.ZMax) || s.ZMax - s.ZMin <= tol)
            {
                issues.Add($"placement-invalid:{role}:z-span");
            }

            if (s.IsThrough)
            {
                if (s.AnchorSide != HoleTierAnchorSide.Through) issues.Add($"placement-invalid:{role}:through-anchor");
                if (s.ZMin > hostMinZ + tol || s.ZMax < hostMaxZ - tol) issues.Add($"placement-invalid:{role}:through-z-coverage");
            }
            else
            {
                if (s.AnchorSide != HoleTierAnchorSide.Top && s.AnchorSide != HoleTierAnchorSide.Bottom) issues.Add($"placement-invalid:{role}:blind-anchor");
                if (s.DepthFromAnchor <= tol) issues.Add($"placement-invalid:{role}:depth-from-anchor");
            }

            if (s.PlacementDiagnostics is null || s.PlacementDiagnostics.Count == 0)
            {
                issues.Add($"placement-invalid:{role}:diagnostics");
            }
        }

        diagnostics = issues;
        return issues.Count == 0;
    }
}
