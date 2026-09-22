using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

public enum FaceOrientationQualification
{
    DerivedQualified,
    DerivedLocallyConsistentGlobalUnknown,
    SourceOnly,
    Ambiguous,
    Invalid,
}

public enum SourceFaceOrientationComparison
{
    Agrees,
    Disagrees,
    SourceMissing,
    NotComparable,
}

/// <summary>STEP-authored orientation retained as evidence, never as an instruction.</summary>
public sealed record SourceFaceOrientationEvidence(
    bool? StepSameSense,
    int? SourceEntityId,
    int? SourceSurfaceId);

/// <summary>The resolved face patch visible after the import boundary.</summary>
public sealed record OrientedFacePatch(
    FaceId FaceId,
    ShellId ShellId,
    ResolvedFaceOrientation Orientation,
    FaceOrientationQualification Qualification,
    SourceFaceOrientationEvidence SourceEvidence,
    SourceFaceOrientationComparison SourceComparison,
    string EvidenceSummary);

public sealed record ShellOrientationResolution(
    ShellId ShellId,
    FaceOrientationQualification Qualification,
    bool IsClosedManifold,
    bool IsInnerVoid,
    double? SignedVolumeBeforeGlobalProjection,
    double? SignedVolumeAfterGlobalProjection,
    bool GlobalFlipApplied,
    string EvidenceSummary);

public sealed record FaceOrientationReport(
    IReadOnlyList<OrientedFacePatch> Faces,
    IReadOnlyList<ShellOrientationResolution> Shells)
{
    public int DerivedFaceCount => Faces.Count(face => face.Qualification is
        FaceOrientationQualification.DerivedQualified or
        FaceOrientationQualification.DerivedLocallyConsistentGlobalUnknown);
    public int SourceAgreementCount => Faces.Count(face => face.SourceComparison == SourceFaceOrientationComparison.Agrees);
    public int SourceMismatchCount => Faces.Count(face => face.SourceComparison == SourceFaceOrientationComparison.Disagrees);
    public int AmbiguousComponentCount => Shells.Count(shell => shell.Qualification == FaceOrientationQualification.Ambiguous);
    public int GlobalFlipCount => Shells.Count(shell => shell.GlobalFlipApplied);
}
