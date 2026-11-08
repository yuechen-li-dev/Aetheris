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
}

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
    MoldTexture? Texture);

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
    MoldEvidenceStrength Strength);

public sealed record MoldPartingLineEvidence(
    IReadOnlyList<int> EdgeIds,
    double MinimumHeight,
    double MaximumHeight,
    bool OnDeclaredPlane,
    IReadOnlyList<int> CosmeticFacesTouched);

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
    double ShotMass);

public sealed record MoldReport(
    MoldIntent Intent,
    IReadOnlyList<MoldFaceEvidence> Faces,
    MoldPartingLineEvidence PartingLine,
    MoldSummary Summary,
    string Method);

public sealed record MoldCompileResult(
    bool IsSuccess,
    MoldIntent? Intent,
    MoldReport? Report,
    string? PartStep,
    IReadOnlyList<MoldDiagnostic> Diagnostics);
