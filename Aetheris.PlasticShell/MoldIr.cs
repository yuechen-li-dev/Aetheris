using Aetheris.Kernel.Core.Math;

namespace Aetheris.PlasticShell;

/// <summary>How hard a manufacturing finding is. Early designs get the whole report without failing the build; the
/// tooling stage is the release gate.</summary>
public enum MoldStage { Concept, Prototype, Tooling }

/// <summary>Which half of the tool forms a face, by its outward normal against the pull direction.</summary>
public enum MoldFaceSide
{
    /// <summary>Faces the pull direction: formed by the half that opens along +pull.</summary>
    Positive,
    /// <summary>Faces against the pull direction: formed by the half that opens along −pull.</summary>
    Negative,
    /// <summary>Parallel to the pull everywhere (zero draft): either half, at its parting end.</summary>
    Vertical,
    /// <summary>Faces both ways: the parting line has to split it.</summary>
    Straddling,
}

public enum MoldEvidenceStrength { ExactAnalytic, SampledConservative }

public static class MoldDiagnosticCodes
{
    public const string SourceInvalid = "mold-source-invalid";
    public const string PartUnresolved = "mold-part-unresolved";
    public const string PartBuildFailed = "mold-part-build-failed";
    public const string MaterialUnknown = "mold-material-unknown";
    public const string PolicyInvalid = "mold-policy-invalid";
    public const string SelectorUnresolved = "mold-face-selector-unresolved";
    public const string PartingInvalid = "mold-parting-invalid";
    public const string DraftInsufficient = "mold-draft-insufficient";
    public const string Undercut = "mold-undercut";
    public const string StraddlingFace = "mold-face-straddles-pull";
    public const string PartingLineOffPlane = "mold-parting-line-off-plane";
    public const string PartingLineOnCosmetic = "mold-parting-line-on-cosmetic-face";
    public const string UndercutOnCosmetic = "mold-cosmetic-face-not-pullable";
    public const string ActionInvalid = "mold-action-invalid";
    public const string ActionReleasesNothing = "mold-action-releases-nothing";
    public const string InsertsUnavailable = "mold-inserts-unavailable";
    public const string WallTooThin = "mold-wall-too-thin";
    public const string WallTooThick = "mold-wall-too-thick";
    public const string SharpInsideCorner = "mold-sharp-inside-corner";
    public const string RequirementFailed = "mold-requirement-failed";
    public const string RequirementInvalid = "mold-requirement-invalid";
    public const string GateInvalid = "mold-gate-invalid";
    public const string GateMisplaced = "mold-gate-misplaced";
    public const string GateOnCosmetic = "mold-gate-on-cosmetic-face";
    public const string EjectorInvalid = "mold-ejector-invalid";
    public const string EjectorMisplaced = "mold-ejector-misplaced";
    public const string EjectorOnCosmetic = "mold-ejector-on-cosmetic-face";
    public const string FlowTooLong = "mold-flow-too-long";
    public const string FlowUnfilled = "mold-flow-unfilled";
    public const string KnitLineOnCosmetic = "mold-knit-line-on-cosmetic-face";
}

/// <summary>How the melt enters the part.</summary>
public enum MoldGateKind
{
    /// <summary>At the parting line, from a runner in the parting plane.</summary>
    Edge,
    /// <summary>Tunnelled through the −pull (core) half below the parting line; sheared off on ejection.</summary>
    Submarine,
    /// <summary>A pinpoint gate on the +pull (cavity) half, fed from a runner plate (a three-plate tool).</summary>
    Pin,
    /// <summary>The sprue straight onto the +pull half.</summary>
    Direct,
}

/// <summary><c>Gate name { kind; at }</c>: where the melt enters.</summary>
public sealed record MoldGateIntent(string Name, MoldGateKind Kind, Point3D At);

/// <summary><c>Ejector name { at; diameter }</c>: a round ejector pin pushing the part off the −pull half.</summary>
public sealed record MoldEjectorIntent(string Name, Point3D At, double Diameter);

/// <summary>A side action (slide, lifter, side core) declared in the Mold: it forms the undercuts it can withdraw from
/// along <see cref="Pull"/>; <see cref="Near"/> picks among undercuts that more than one action could release.</summary>
public sealed record MoldActionIntent(string Name, Direction3D Pull, Point3D? Near);

public sealed record MoldDiagnostic(string Code, PlasticDiagnosticSeverity Severity, string Message, string? Entity = null);

/// <summary>The scalar rules of a molding material: compile-time policy, independent of any part.</summary>
public sealed record MoldingPolicy(
    string Name,
    double MinimumDraftDegrees,
    double TextureDraftDegreesPerMillimetre,
    double MinimumWall,
    double MaximumWall,
    double MaximumFlowRatio,
    double Shrinkage,
    double Density,
    double MinimumInsideRadius = 0.5,
    double EjectorClearance = 0.5);

public sealed record MoldPartingPlane(Point3D Origin, Direction3D Normal, string Source);

public sealed record MoldTexture(IReadOnlyList<string> Selectors, double Depth);

public sealed record MoldIntent(
    string MoldName,
    string PartName,
    MoldingPolicy Policy,
    MoldStage Stage,
    Direction3D Pull,
    MoldPartingPlane? Parting,
    IReadOnlyList<string> Cosmetic,
    MoldTexture? Texture,
    IReadOnlyList<MoldActionIntent>? Actions = null,
    double? InsertMargin = null,
    IReadOnlyList<MoldRequirement>? Requirements = null,
    string? Source = null,
    IReadOnlyList<MoldGateIntent>? Gates = null,
    IReadOnlyList<MoldEjectorIntent>? Ejectors = null);

/// <summary><c>Require name => expression</c> in a Mold: a Concept applied to the review's measurements.</summary>
public sealed record MoldRequirement(string Name, string Expression);

public sealed record MoldFaceEvidence(
    int FaceId,
    string Surface,
    MoldFaceSide Side,
    double MinimumDraftDegrees,
    double RequiredDraftDegrees,
    bool DraftSatisfied,
    double UndercutFraction,
    int Samples,
    bool Cosmetic,
    Point3D Representative,
    double Area,
    MoldEvidenceStrength Strength,
    string? Action = null,
    double? SilhouetteHeight = null,
    double? MinimumWall = null,
    double? MaximumWall = null,
    MoldEvidenceStrength? WallStrength = null);

/// <summary>An inside edge between two faces that meet at an angle: no radius, a stress raiser and a flow corner.</summary>
public sealed record MoldCornerEvidence(int EdgeId, IReadOnlyList<int> FaceIds, double AngleDegrees, Point3D Midpoint);

/// <summary>The review's measurements, as the names a Mold <c>Require</c> can use.</summary>
public sealed record MoldMeasurements(double MinimumDraftDegrees, double MinimumWall, double MaximumWall, double NominalWall,
    int SharpInsideCorners, double MinimumInsideRadius, int Undercuts, int Actions,
    int Gates = 0, int Ejectors = 0, double EjectorArea = 0, double? FlowRatio = null, double? FlowLength = null, int KnitLines = 0, int KnitLinesOnCosmetic = 0);

/// <summary>A gate, snapped to the part: the face it lands on, and the problem with it, if any.</summary>
public sealed record MoldGateEvidence(string Name, MoldGateKind Kind, Point3D At, int? FaceId, MoldFaceSide? Side, double Offset,
    double? DistanceToPartingLine, bool Cosmetic, string? Problem);

/// <summary>An ejector pin: the faces its footprint (with the policy's clearance) seats on, and the problem with it, if any.</summary>
public sealed record MoldEjectorEvidence(string Name, Point3D At, double Diameter, int? FaceId, IReadOnlyList<int> SeatFaces, bool Cosmetic, string? Problem);

/// <summary>Where melt fronts meet: behind an obstacle the front splits around (<see cref="Gates"/> one gate), or
/// between gates. <see cref="AngleDegrees"/> is the widest angle between the fronts arriving there.</summary>
public sealed record MoldKnitLineEvidence(IReadOnlyList<string> Gates, Point3D Centroid, Point3D From, Point3D To, double Length,
    double AngleDegrees, IReadOnlyList<int> Faces, bool Cosmetic);

/// <summary>The fill from the gates over a voxel sampling of the part's interior: path length and the flow ratio
/// (path length over local wall, summed along the path) to the last point filled.</summary>
public sealed record MoldFlowEvidence(string Method, double VoxelSize, int Cells, double MaximumFlowRatio, double MaximumFlowLength,
    Point3D LastToFill, int? LastToFillFace, double UnfilledFraction, IReadOnlyList<MoldKnitLineEvidence> KnitLines);

public sealed record MoldRequirementEvidence(string Name, string Expression, bool Passed, string Message);

public sealed record MoldPartingLineEvidence(
    IReadOnlyList<int> EdgeIds,
    double MinimumHeight,
    double MaximumHeight,
    bool OnDeclaredPlane,
    IReadOnlyList<int> CosmeticFacesTouched,
    IReadOnlyList<MoldPartingLoop>? Loops = null);

/// <summary>How a closed loop of the parting line meets the tool.</summary>
public enum MoldPartingLoopKind
{
    /// <summary>On the parting plane: the two halves meet across the parting face around it.</summary>
    Parting,
    /// <summary>Flat and across the pull, off the parting plane, around an opening: the halves touch across the opening
    /// (a shut-off), so the parting plane need not reach it.</summary>
    ShutOff,
    /// <summary>Anything else: the parting surface would have to step or follow a contour to reach it.</summary>
    Stepped,
}

public sealed record MoldPartingLoop(IReadOnlyList<int> EdgeIds, double MinimumHeight, double MaximumHeight, MoldPartingLoopKind Kind);

/// <summary>Connected undercut faces, the action that forms them if one does, and the directions perpendicular to the
/// pull along which they would release.</summary>
public sealed record MoldUndercutGroup(IReadOnlyList<int> FaceIds, Point3D Representative, string? Action, IReadOnlyList<string> ReleasingDirections);

public sealed record MoldActionEvidence(string Name, Direction3D Pull, IReadOnlyList<int> FaceIds, double MinimumDraftDegrees, double RequiredDraftDegrees, bool DraftSatisfied);

/// <summary>The core and cavity inserts cut from a block around the part, and each side action's pin:
/// block = cavity + core + part + pins. <see cref="SplitFaces"/> are the part's faces cut at the parting plane first.</summary>
public sealed record MoldInsertEvidence(double Margin, Point3D BlockMinimum, Point3D BlockMaximum, double BlockVolume, double CavityVolume, double CoreVolume,
    double PartVolume, int CavityFaces, int CoreFaces, int ShutOffs, IReadOnlyList<MoldSlideEvidence>? Slides = null, IReadOnlyList<int>? SplitFaces = null);

/// <summary>A side action's pin: the action's faces closed by a cap across their mouth, withdrawn along <see cref="Pull"/>.</summary>
public sealed record MoldSlideEvidence(string Name, Direction3D Pull, double Volume, int Faces, IReadOnlyList<int> PartFaces);

public sealed record MoldSummary(
    int Faces,
    int PositiveFaces,
    int NegativeFaces,
    int VerticalFaces,
    int StraddlingFaces,
    int DraftFailures,
    int UndercutFaces,
    double ProjectedArea,
    double Volume,
    double ShotMass,
    int ActionFaces = 0);

public sealed record MoldReport(
    MoldIntent Intent,
    IReadOnlyList<MoldFaceEvidence> Faces,
    MoldPartingLineEvidence PartingLine,
    MoldSummary Summary,
    string Method,
    IReadOnlyList<MoldUndercutGroup>? Undercuts = null,
    IReadOnlyList<MoldActionEvidence>? Actions = null,
    MoldInsertEvidence? Inserts = null,
    IReadOnlyList<MoldCornerEvidence>? InsideCorners = null,
    MoldMeasurements? Measurements = null,
    IReadOnlyList<MoldRequirementEvidence>? Requirements = null,
    IReadOnlyList<MoldGateEvidence>? Gates = null,
    IReadOnlyList<MoldEjectorEvidence>? Ejectors = null,
    MoldFlowEvidence? Flow = null);

public sealed record MoldCompileResult(
    bool IsSuccess,
    MoldIntent? Intent,
    MoldReport? Report,
    string? PartStep,
    IReadOnlyList<MoldDiagnostic> Diagnostics,
    string? CavityStep = null,
    string? CoreStep = null,
    IReadOnlyDictionary<string, string>? SlideSteps = null);
