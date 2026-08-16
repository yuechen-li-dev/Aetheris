using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Surfacing;

namespace Aetheris.SheetMetal;

public enum SheetMetalRecognitionStatus { Complete, Partial, Ambiguous, Unsupported }
public enum RecognizedBendStatus { Candidate, Recognized, Rejected, Ambiguous }
public enum RecoveredFlatReferenceKind { GeometricMidSurface, ManufacturingNeutralSurface }
public enum SheetRegionKind { Planar, CylindricalBend, Developable, Unsupported }
public enum SheetBendDirection { Up, Down, Unknown }
public enum SheetFeatureKind { CircularHole, ProfileHole, Slot, Cutout, Unsupported }
public enum SheetEvidenceKind { Authored, Exact, ToleranceBounded, DeterministicHeuristic, Derived, Unsupported }
public enum FlatPatternStatus { Valid, Overlapping, Partial, Unsupported }
public enum SheetMetalDiagnosticSeverity { Information, Warning, Error }
public enum SheetMetalIntentConfidence { StructuralFact, StrongCandidate, WeakCandidate, Ambiguous, Unknown }
public enum SheetMetalProvenanceCategory { Authored, Recovered, Reconstructed }
public enum SheetCornerKind { ClosedCorner, OpenCorner, ReliefCorner, OverlapCorner, MiteredCorner, Unknown }
public enum SheetReliefKind { Rectangular, Round, Unknown }
public enum SheetCornerPolicy { Open, Mitered, RectangularRelief, RoundRelief, Relief = RectangularRelief }
public enum SheetReliefPolicy { None, Auto, Rectangular, Round }
public enum SheetFlangeLengthMode { TangentToEdge }
public enum SheetPathCapability { FlangeAttachable, BendAttachable, FeatureAttachable }
public enum SheetBendEnd { Start, End }
public enum SheetBendTerminationTreatment { Natural, Trimmed, Rounded, Auto }

public static class SheetMetalDiagnosticCodes
{
    public const string NonConstantThickness = "sheetmetal-non-constant-thickness";
    public const string UnpairedFaces = "sheetmetal-unpaired-sheet-faces";
    public const string NonDevelopable = "sheetmetal-non-developable-region";
    public const string AmbiguousBendAxis = "sheetmetal-ambiguous-bend-axis";
    public const string UnsupportedBendTopology = "sheetmetal-unsupported-bend-topology";
    public const string InconsistentBendRadius = "sheetmetal-inconsistent-bend-radius";
    public const string DisconnectedGraph = "sheetmetal-disconnected-sheet-graph";
    public const string FlatOverlap = "sheetmetal-flat-overlap";
    public const string FeatureMappingFailure = "sheetmetal-feature-mapping-failure";
    public const string InvalidRadius = "sheetmetal-invalid-radius";
    public const string DuplicateCut = "sheetmetal-flat-duplicate-cut";
    public const string BendLineOutsideMaterial = "sheetmetal-flat-bend-line-outside-material";
    public const string ZeroWidthSliver = "sheetmetal-flat-zero-width-sliver";
    public const string DuplicateFlange = "sheetmetal-duplicate-flange";
    public const string ImpossibleTopology = "sheetmetal-impossible-topology";
    public const string InvalidRelief = "sheetmetal-invalid-relief";
    public const string CutCrossesBend = "sheetmetal-cut-crosses-bend";
    public const string FormedBodyInvalid = "sheetmetal-formed-body-invalid";
    public const string ExactBlankContour = "sheetmetal-exact-blank-contour";
    public const string FlangeBelowMinimum = "sheetmetal-flange-below-minimum";
    public const string RecognitionAssertionInvalid = "sheetmetal-recognition-assertion-invalid";
    public const string RecognitionPlanIncomplete = "sheetmetal-recognition-plan-incomplete";
    public const string RegionGraphCycle = "sheetmetal-region-graph-cycle";
    public const string UnfoldCrack = "sheetmetal-unfold-crack";
    public const string UnfoldDuplicateBoundary = "sheetmetal-unfold-duplicate-boundary";
    public const string SourceContourUnsupported = "sheetmetal-source-contour-unsupported";
    public static IReadOnlyList<string> All { get; } =
        [NonConstantThickness, UnpairedFaces, NonDevelopable, AmbiguousBendAxis, UnsupportedBendTopology,
         InconsistentBendRadius, DisconnectedGraph, FlatOverlap, FeatureMappingFailure, InvalidRadius,
         DuplicateCut, BendLineOutsideMaterial, ZeroWidthSliver, DuplicateFlange, ImpossibleTopology,
         InvalidRelief, CutCrossesBend, FormedBodyInvalid, ExactBlankContour, FlangeBelowMinimum,
         RecognitionAssertionInvalid, RecognitionPlanIncomplete, RegionGraphCycle, UnfoldCrack,
         UnfoldDuplicateBoundary, SourceContourUnsupported];
}

public sealed record SheetMetalDiagnostic(
    string Code,
    SheetMetalDiagnosticSeverity Severity,
    string Message,
    IReadOnlyList<int>? SourceFaceIds = null);

public sealed record SheetEvidence(
    SheetEvidenceKind Kind,
    string Predicate,
    string Basis,
    double? Measured = null,
    double? Tolerance = null,
    IReadOnlyList<int>? SourceFaceIds = null);

public sealed record SheetSourceBinding(
    string SourceKind,
    string SourceAuthority,
    IReadOnlyList<int> FaceIds,
    IReadOnlyList<int> EdgeIds,
    string? SourcePath = null);

public sealed record SheetPlaneReference(
    Point3D Origin,
    Vector3D Normal,
    Vector3D UAxis,
    Vector3D VAxis,
    bool MaterialPositiveSide);

public sealed record SheetCylinderReference(
    Point3D AxisOrigin,
    Vector3D AxisDirection,
    double GeometricMidRadius,
    double InsideRadius,
    double AngularSpanRadians,
    double AxisLength,
    bool MaterialOutside);

public sealed record SheetRegionIr(
    string StableId,
    SheetRegionKind Kind,
    DevelopabilityEvidence Developability,
    SheetPlaneReference? Plane,
    SheetCylinderReference? Cylinder,
    IReadOnlyList<Point3D> Boundary3D,
    double ApproximateArea,
    SheetSourceBinding Source,
    IReadOnlyList<SheetEvidence> Evidence,
    PlanarContour2? ExactContour = null);

public sealed record SheetNeutralAxisPolicy(string Kind, double KFactor)
{
    public static SheetNeutralAxisPolicy KFactorPolicy(double kFactor) => new("KFactor", kFactor);
}

public sealed record SheetBendIr(
    string StableId,
    Point3D AxisOrigin,
    Vector3D AxisDirection,
    double BendAngleRadians,
    double InsideRadius,
    double Thickness,
    SheetBendDirection Direction,
    string AdjacentRegionA,
    string AdjacentRegionB,
    SheetNeutralAxisPolicy NeutralAxisPolicy,
    SheetSourceBinding Source,
    IReadOnlyList<SheetEvidence> Evidence,
    SheetBendTerminationIr? StartTermination = null,
    SheetBendTerminationIr? EndTermination = null);

/// <summary>
/// Stable semantic identity for the end of a finite bend/root.  The treatment is
/// Sheet Metal intent; LoweredProfileDelta records the exact, reusable 2D contour
/// program selected for the adjacent planar profile.
/// </summary>
public sealed record SheetBendTerminationIr(
    string StableId,
    string BendId,
    SheetBendEnd End,
    Point3D RootPoint,
    string AdjacentRegionId,
    string NeighborBoundary,
    SheetBendTerminationTreatment AuthoredTreatment,
    SheetBendTerminationTreatment ResolvedTreatment,
    double Setback,
    double Depth,
    double? Radius,
    bool IsPolicyDerived,
    SemanticProfileDeltaIr? LoweredProfileDelta,
    SheetSourceBinding Source,
    IReadOnlyList<SheetEvidence> Evidence);

public sealed record SheetFeatureIr(
    string StableId,
    SheetFeatureKind Kind,
    string OwningRegionId,
    Point3D Center,
    double? Diameter,
    IReadOnlyList<Point3D> Boundary3D,
    SheetSourceBinding Source,
    IReadOnlyList<SheetEvidence> Evidence,
    PlanarContour2? ExactContour = null);

public sealed record SheetMetalCornerIr(
    string StableId,
    string RegionA,
    string RegionB,
    string ParentRegion,
    string VertexName,
    SheetCornerPolicy Policy,
    string? ReliefId,
    SheetSourceBinding Source,
    IReadOnlyList<SheetEvidence> Evidence);

public sealed record SheetMetalReliefIr(
    string StableId,
    SheetReliefKind Kind,
    string OwningRegionId,
    string CornerId,
    double Width,
    double Depth,
    double? Radius,
    bool IsPolicyDerived,
    SheetSourceBinding Source,
    IReadOnlyList<SheetEvidence> Evidence);

public sealed record SheetMetalCorrespondence(
    string SemanticId,
    string Kind,
    string FormedId,
    string FlatId);

/// <summary>
/// An oriented, bounded path owned by a planar sheet region.  Unlike a region
/// boundary this is feature intent: it may be shorter than, or inset from, the
/// physical carrier edge from which it was authored.
/// </summary>
public sealed record SheetAttachmentPathIr(
    string StableId,
    string OwningRegionId,
    string CarrierPath,
    Point3D Start,
    Point3D End,
    Vector3D Tangent,
    Vector3D InPlaneNormal,
    Vector3D RegionNormal,
    double Inset,
    double SpanOffset,
    IReadOnlyList<SheetPathCapability> Capabilities,
    SheetSourceBinding Source,
    IReadOnlyList<SheetEvidence> Evidence);

public sealed record SheetMetalFlattenPolicy(double KFactor)
{
    public static SheetMetalFlattenPolicy Default { get; } = new(0.5d);

    public double NeutralRadius(double insideRadius, double thickness)
    {
        Validate(insideRadius, thickness);
        return insideRadius + KFactor * thickness;
    }

    public double BendAllowance(double bendAngleRadians, double insideRadius, double thickness)
    {
        if (!double.IsFinite(bendAngleRadians) || bendAngleRadians <= 0d)
            throw new ArgumentOutOfRangeException(nameof(bendAngleRadians));
        return bendAngleRadians * NeutralRadius(insideRadius, thickness);
    }

    private void Validate(double radius, double thickness)
    {
        if (!double.IsFinite(KFactor) || KFactor < 0d || KFactor > 1d) throw new ArgumentOutOfRangeException(nameof(KFactor));
        if (!double.IsFinite(radius) || radius < 0d) throw new ArgumentOutOfRangeException(nameof(radius));
        if (!double.IsFinite(thickness) || thickness <= 0d) throw new ArgumentOutOfRangeException(nameof(thickness));
    }
}

public sealed record SheetThicknessPairEvidence(
    string Family,
    int FaceA,
    int FaceB,
    double Separation,
    double Residual,
    bool Admitted);

public sealed record SheetThicknessRecognition(
    bool IsPlausible,
    double? NominalThickness,
    double Tolerance,
    IReadOnlyList<SheetThicknessPairEvidence> SourcePairs,
    IReadOnlyList<int> OutlierFaceIds,
    IReadOnlyList<int> UnsupportedFaceIds,
    IReadOnlyList<SheetEvidence> Evidence);

public sealed record SheetMetalPartIr(
    string StableId,
    double Thickness,
    string? Material,
    string BaseRegionId,
    IReadOnlyList<SheetRegionIr> Regions,
    IReadOnlyList<SheetBendIr> Bends,
    IReadOnlyList<SheetFeatureIr> Features,
    SheetMetalFlattenPolicy FlatPatternPolicy,
    SheetMetalRecognitionStatus RecognitionStatus,
    string Provenance,
    IReadOnlyList<SheetEvidence> Evidence,
    IReadOnlyList<SheetMetalDiagnostic> Diagnostics,
    BrepBody? FormedBody = null,
    IReadOnlyList<SheetMetalCornerIr>? Corners = null,
    IReadOnlyList<SheetMetalReliefIr>? Reliefs = null,
    IReadOnlyList<SheetMetalCorrespondence>? Correspondence = null,
    SheetFlangeLengthMode FlangeLengthMode = SheetFlangeLengthMode.TangentToEdge,
    IReadOnlyList<SheetAttachmentPathIr>? AttachmentPaths = null);

public readonly record struct SheetPoint2(double X, double Y);

public sealed record FlatRegion2D(
    string StableId,
    string SourceRegionId,
    SheetRegionKind Kind,
    IReadOnlyList<SheetPoint2> Boundary,
    string MappingKind,
    PlanarContour2? ExactContour = null);

public sealed record FlatBendLine(
    string BendId,
    SheetPoint2 Start,
    SheetPoint2 End,
    SheetBendDirection Direction,
    double BendAngleRadians,
    double InsideRadius,
    double Thickness,
    double KFactor,
    double BendAllowance);

public sealed record FlatCutLoop(
    string FeatureId,
    SheetFeatureKind Kind,
    IReadOnlyList<SheetPoint2> Boundary,
    string SourceRegionId,
    PlanarContour2? ExactContour = null);

public sealed record FlatReliefLoop(
    string ReliefId,
    SheetReliefKind Kind,
    IReadOnlyList<SheetPoint2> Boundary,
    string SourceRegionId,
    PlanarContour2 ExactContour,
    double Width,
    double Depth);

public sealed record SourceToFlatMapping(
    string SourceRegionId,
    Point3D PlaneOrigin,
    Vector3D SourceU,
    Vector3D SourceV,
    SheetPoint2 FlatOrigin,
    SheetPoint2 FlatU,
    SheetPoint2 FlatV);

public sealed record FlatPatternBounds(double MinX, double MinY, double MaxX, double MaxY)
{
    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
}

public sealed record SheetMetalFlatPatternIr(
    string StableId,
    FlatPatternStatus Status,
    IReadOnlyList<FlatRegion2D> Regions2D,
    IReadOnlyList<FlatBendLine> BendLines,
    IReadOnlyList<FlatCutLoop> CutLoops,
    IReadOnlyList<SourceToFlatMapping> SourceToFlatMappings,
    IReadOnlyList<SheetPoint2> Boundary,
    FlatPatternBounds? Bounds,
    SheetMetalFlattenPolicy Policy,
    IReadOnlyList<SheetEvidence> Evidence,
    IReadOnlyList<SheetMetalDiagnostic> Diagnostics,
    string DeterministicHash,
    PlanarContour2? ExactBlankContour = null,
    IReadOnlyList<FlatReliefLoop>? ReliefLoops = null,
    BlankCompositionPlan? CompositionPlan = null);

public sealed record SheetMetalRecognitionResult(
    SheetMetalPartIr? Part,
    SheetThicknessRecognition Thickness,
    IReadOnlyList<SheetMetalDiagnostic> Diagnostics,
    TimeSpan ImportTime,
    TimeSpan RecognitionTime);
