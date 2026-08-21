using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;

namespace Aetheris.PlasticShell;

public enum PlasticEvidenceStrength { ExactAnalytic, CertifiedBounded, SampledConservative, GeometricProxy }
public enum PlasticDiagnosticSeverity { Information, Warning, Error }
public enum MoldPullSide { CoreSide, CavitySide, PartingBoundary, Undercut }
public enum StandoffSupportIntent { Pcb, Fastener, Spacing }
public enum GateKind { Point, Edge }

public static class PlasticDiagnosticCodes
{
    public const string SourceInvalid = "plastic-shell-source-invalid";
    public const string WallOffsetCollapse = "plastic-shell-wall-offset-collapse";
    public const string WallThicknessViolation = "plastic-shell-wall-thickness-violation";
    public const string DraftConflict = "plastic-shell-draft-conflicts-preserved-surface";
    public const string DraftInsufficient = "plastic-shell-draft-insufficient";
    public const string Undercut = "plastic-shell-undercut";
    public const string InvalidParting = "plastic-shell-invalid-parting";
    public const string InvalidGate = "plastic-shell-gate-invalid-region";
    public const string GateInaccessible = "plastic-shell-gate-inaccessible";
    public const string EjectorNotCoreAccessible = "plastic-shell-ejector-not-core-accessible";
    public const string EjectorCollidesFeature = "plastic-shell-ejector-collides-feature";
    public const string EjectorCosmeticRegion = "plastic-shell-ejector-cosmetic-region";
    public const string StandoffThickSection = "plastic-shell-standoff-thick-section-proxy";
    public const string AutoRibNoEligibleNetwork = "plastic-shell-autorib-no-eligible-network";
    public const string RibThicknessViolation = "plastic-shell-rib-thickness-violation";
    public const string RibToolingConflict = "plastic-shell-rib-tooling-conflict";
    public const string ConstantSectionFeatureZeroDraft = "plastic-shell-constant-section-feature-zero-draft";
    public const string MaterializationFailed = "plastic-shell-materialization-failed";
    public const string MaterialAccumulation = "plastic-shell-material-accumulation";
    public const string MaterializedFeatureOutsideAuthorizedRegion = "plastic-shell-materialized-feature-outside-authorized-region";
    public const string EjectorRibCollision = "plastic-shell-ejector-rib-collision";
}

public sealed record PlasticDiagnostic(string Code, PlasticDiagnosticSeverity Severity, string Message, string? Entity = null);
public sealed record PlasticWallPolicy(double NominalThickness, double MinimumThickness, double MaximumThickness, double ThicknessTolerance);
public sealed record PlasticPartingPlane(string StableId, Point3D Origin, Direction3D Normal);
public sealed record PlasticGate(string GateId, Point3D Location, string TargetRegion, GateKind Kind, double? Size);
public sealed record PlasticStandoff(string StandoffId, Point3D Position, double Height, double OuterDiameter, double? HoleDiameter, StandoffSupportIntent Intent);
public sealed record PlasticEjectorPin(string EjectorId, Point3D Position, double Diameter, string TargetRegion);
public sealed record PlasticRibPolicy(double ThicknessRatio, double MinimumHeight, double MaximumHeight, double MinimumSpacing, double DraftAngleDegrees, double BaseBlendRadius);
public sealed record PlasticAutoRibRequest(string RequestId, IReadOnlyList<string> Supports, string GateId, IReadOnlyList<string> KeepOuts, PlasticRibPolicy Policy);

public sealed record PlasticExteriorAuthority(
    string StableId,
    string Kind,
    double BottomRadius,
    double TopRadius,
    double Height,
    bool Protected);

public sealed record PlasticShellIr(
    string PlasticShellId,
    PlasticExteriorAuthority Exterior,
    string Material,
    PlasticWallPolicy WallPolicy,
    Direction3D ToolingDirection,
    PlasticPartingPlane PartingPlane,
    double MinimumDraftAngleDegrees,
    IReadOnlyList<PlasticGate> Gates,
    IReadOnlyList<PlasticStandoff> Standoffs,
    IReadOnlyList<PlasticEjectorPin> Ejectors,
    PlasticAutoRibRequest? AutoRib,
    IReadOnlyList<string> PreservedEntities);

public sealed record ThicknessSample(string Region, Point3D Location, double Thickness, PlasticEvidenceStrength Strength);
public sealed record PlasticThicknessEvidence(double RequestedNominal, double Minimum, double Maximum, double Mean, Point3D MinimumLocation, Point3D MaximumLocation, IReadOnlyList<ThicknessSample> Samples, IReadOnlyList<string> Violations, string Method, PlasticEvidenceStrength Strength);
public sealed record PlasticDraftRegionEvidence(string Region, MoldPullSide PullSide, double DraftAngleDegrees, double RequiredDegrees, bool Satisfied, PlasticEvidenceStrength Strength, string Basis);
public sealed record PlasticPullabilityEvidence(IReadOnlyList<string> CoreAccessible, IReadOnlyList<string> CavityAccessible, IReadOnlyList<string> PartingBoundary, IReadOnlyList<string> Undercuts, string Method, PlasticEvidenceStrength Strength);
public sealed record PlasticGateEvidence(string GateId, Point3D Location, string SurfaceAssociation, double MaximumProxyDistance, double MeanRepresentativeProxyDistance, string Method);
public sealed record PlasticEjectorEvidence(string EjectorId, bool CoreAccessible, bool CollisionFree, bool OutsideProtectedCosmeticRegion, string Basis);
public sealed record RibEdge(string From, string To, double Length, string Kind);
public sealed record RibCandidateMetrics(double SupportProxy, double FlowCompatibility, double SinkProxy, double RibLength, double Complexity, double Utility);
public sealed record RibCandidateEvidence(string CandidateId, bool Eligible, RibCandidateMetrics Metrics, IReadOnlyList<RibEdge> Edges, IReadOnlyList<string> RejectionReasons);
public sealed record RibMaterializationGateEvidence(string CandidateId, string Status, bool? Eligible, IReadOnlyList<string> RejectionReasons);
public sealed record AutoRibJudgmentEvidence(string RequestId, IReadOnlyList<RibCandidateEvidence> Candidates, string? SelectedCandidate, IReadOnlyList<string> Rejections, string DeterministicBasis,
    string? OriginalSelectedCandidate = null, IReadOnlyList<RibMaterializationGateEvidence>? MaterializationGates = null);
public sealed record MoldedFeatureEvidence(string FeatureId, string Kind, IReadOnlyList<int> FaceIds, double Height,
    double BaseThickness, double TopThickness, double NominalWallRatio, SpatialInfluenceEnvelope AuthorizedRegion,
    double MinimumDraftAngleDegrees, PlasticEvidenceStrength Strength);
public sealed record MoldedJunctionEvidence(string JunctionId, IReadOnlyList<string> Members, double AccumulationRatio, bool WithinLimit, string Transition);
public sealed record MoldedMaterializationEvidence(
    IReadOnlyList<MoldedFeatureEvidence> Features,
    IReadOnlyList<MoldedJunctionEvidence> Junctions,
    double ExteriorMaximumDeviation,
    string ExteriorFingerprintBefore,
    string ExteriorFingerprintAfter,
    int ConnectedBodies,
    int ClosedShells,
    int BoundaryFaces,
    bool HasInternalDuplicateFaces,
    string ConstructionMethod,
    PlasticEvidenceStrength Strength);

public sealed record PlasticShellEvidence(
    PlasticThicknessEvidence WallThickness,
    IReadOnlyList<PlasticDraftRegionEvidence> Draft,
    PlasticPullabilityEvidence Pullability,
    IReadOnlyList<PlasticGateEvidence> Gates,
    IReadOnlyList<PlasticEjectorEvidence> Ejectors,
    AutoRibJudgmentEvidence? RibNetwork,
    MoldedMaterializationEvidence? Materialization,
    IReadOnlyDictionary<string, MoldPullSide> SurfaceClassification,
    string PartingSummary,
    string TopologySummary);

public sealed record PlasticShellBodyState(
    BodyStateId StateId,
    string BodyStableId,
    BrepBody Body,
    PlasticShellIr Intent,
    GeometricDelta Delta,
    PlasticShellEvidence Evidence);

public sealed record PlasticShellCompileResult(
    bool IsSuccess,
    string ModelName,
    PlasticShellIr? Intent,
    PlasticShellBodyState? State,
    IReadOnlyList<PlasticDiagnostic> Diagnostics);
