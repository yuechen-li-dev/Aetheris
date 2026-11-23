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
}

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
    double Density);

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
    double? InsertMargin = null);

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
    double? SilhouetteHeight = null);

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

/// <summary>The core and cavity inserts cut from a block around the part: block = cavity + core + part.</summary>
public sealed record MoldInsertEvidence(double Margin, Point3D BlockMinimum, Point3D BlockMaximum, double BlockVolume, double CavityVolume, double CoreVolume,
    double PartVolume, int CavityFaces, int CoreFaces, int ShutOffs);

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
    MoldInsertEvidence? Inserts = null);

public sealed record MoldCompileResult(
    bool IsSuccess,
    MoldIntent? Intent,
    MoldReport? Report,
    string? PartStep,
    IReadOnlyList<MoldDiagnostic> Diagnostics,
    string? CavityStep = null,
    string? CoreStep = null);
