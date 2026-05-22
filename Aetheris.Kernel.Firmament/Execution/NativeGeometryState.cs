using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Execution;

public enum NativeGeometryExecutionMode
{
    BRepActive,
    CirOnly,
    Failed
}

public enum NativeGeometryMaterializationAuthority
{
    BRepAuthoritative,
    CirIntentOnly,
    PendingRematerialization
}

public enum NativeGeometryTransitionReasonCategory
{
    MaterializationUnsupported,
    InvalidIntent,
    AnalyzerUncertainty,
    ExplicitFailure
}

public sealed record NativeGeometryTransitionEvent(
    NativeGeometryExecutionMode FromMode,
    NativeGeometryExecutionMode ToMode,
    string? FeatureId,
    int? OpIndex,
    NativeGeometryTransitionReasonCategory ReasonCategory,
    string Message);

public sealed record NativeGeometryReplayLog(
    IReadOnlyList<NativeGeometryReplayOperation> Operations);

public enum NativeGeometryPlacementKind
{
    None,
    Offset,
    OnFace,
    AroundAxis,
    Unsupported
}

public readonly record struct NativeGeometryResolvedPlacement(
    NativeGeometryPlacementKind Kind,
    string? AnchorFeatureId,
    string? AnchorPort,
    Vector3D Offset,
    Vector3D Translation,
    bool IsResolved,
    string? Diagnostic);

public sealed record NativeGeometryReplayOperation(
    int OpIndex,
    string FeatureId,
    string OperationKind,
    string? SourceFeatureId,
    string? ToolKind,
    string? ToolId,
    string? PlacementSummary,
    NativeGeometryResolvedPlacement ResolvedPlacement,
    string? MetadataReference);

public enum CirMirrorStatus
{
    NotAttempted,
    Available,
    Unsupported,
    Failed
}

public sealed record NativeGeometryCirMirrorDiagnostics(
    string Message,
    int? OpIndex,
    string? FeatureId);

public sealed record NativeGeometryCirMirrorSummary(
    Point3D Min,
    Point3D Max,
    double EstimatedVolume,
    bool VolumeApproximate,
    int? DenseResolution);

public sealed record NativeGeometryCirMirrorState(
    CirMirrorStatus Status,
    NativeGeometryCirMirrorSummary? Summary,
    IReadOnlyList<NativeGeometryCirMirrorDiagnostics> Diagnostics);

public sealed record NativeGeometryState(
    NativeGeometryExecutionMode ExecutionMode,
    NativeGeometryMaterializationAuthority MaterializationAuthority,
    BrepBody? MaterializedBody,
    string? CirIntentRootReference,
    NativeGeometryReplayLog ReplayLog,
    IReadOnlyList<NativeGeometryTransitionEvent> TransitionEvents,
    NativeGeometryCirMirrorState CirMirror);
