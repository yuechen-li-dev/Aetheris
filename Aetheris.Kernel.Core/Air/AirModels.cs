using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Kernel.Core.Air;

internal enum AirNodeKind { ProfileExtrude, PrismaticSectionTransition, TopFaceLoopChamfer, Unsupported }
internal enum AirRouteKind { ProfileExtrudeEmitter, PrismaticSectionTransitionEmitter, TopFaceLoopChamferPrismatic, Unsupported }
internal enum AirAuthority { ConstructionIntent, ExplicitTopology, EvaluationMirror, Serialization }
internal enum AirSelectionClass { None, SingleEdge, FaceBoundaryLoop, WholeBodyCanonical, ArbitraryGraph }
internal enum AirRuleKind { None, UniformChamfer, ConstantRadiusFillet, ShellThickness, Unsupported }
internal enum AirMirrorStatus { NotRequested, ReferencedOnly, Available, Unsupported, Deferred }
internal enum AirDiagnosticSeverity { Info, Warning, Error }

internal sealed record AirProvenance(
    string Milestone,
    string SourceKind,
    string FeatureName,
    string FeatureId,
    string RouteName,
    AirSelectionClass SelectionClass,
    AirRuleKind RuleKind,
    string ConstructionHistoryKind,
    bool IsProductionRoute,
    IReadOnlyList<string> Notes);

internal sealed record AirDiagnostic(string Code, AirDiagnosticSeverity Severity, string Message, IReadOnlyDictionary<string, string>? Details = null);

internal sealed record AirTopologySummary(
    int VertexCount,
    int EdgeCount,
    int FaceCount,
    int PlanarFaceCount,
    int CylindricalFaceCount,
    int LoopCount,
    int CoedgeCount,
    int? CapFaceCount = null,
    int? SideFaceCount = null,
    int? TransitionFaceCount = null,
    int? ChamferFaceCount = null,
    string? Bounds = null,
    int? SectionCount = null);

internal sealed record AirStepSmokeSummary(
    bool WasChecked,
    bool Succeeded,
    bool RequiredMarkersPresent,
    bool ForbiddenMarkersAbsent,
    IReadOnlyList<AirDiagnostic> Diagnostics)
{
    public static AirStepSmokeSummary NotChecked { get; } = new(false, false, false, false, []);
}

internal sealed record AirLoweringSummary(
    AirNodeKind NodeKind,
    AirRouteKind RouteKind,
    bool Succeeded,
    string Recommendation,
    AirProvenance Provenance,
    AirTopologySummary TopologySummary,
    AirStepSmokeSummary StepSmokeSummary,
    IReadOnlyList<AirDiagnostic> Diagnostics,
    IReadOnlyList<string> Guarantees);

internal sealed record AirBody(
    string NodeId,
    AirNodeKind NodeKind,
    AirProvenance Provenance,
    BrepBody? Body,
    AirTopologySummary TopologySummary,
    IReadOnlyList<AirDiagnostic> Diagnostics,
    IReadOnlyList<string> Guarantees);
