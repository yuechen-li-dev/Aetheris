using Aetheris.Kernel.Core.Brep;

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

public sealed record NativeGeometryReplayOperation(
    int OpIndex,
    string FeatureId,
    string OperationKind,
    string? SourceFeatureId,
    string? ToolKind,
    string? ToolId,
    string? PlacementSummary,
    string? MetadataReference);

public sealed record NativeGeometryState(
    NativeGeometryExecutionMode ExecutionMode,
    NativeGeometryMaterializationAuthority MaterializationAuthority,
    BrepBody? MaterializedBody,
    string? CirIntentRootReference,
    NativeGeometryReplayLog ReplayLog,
    IReadOnlyList<NativeGeometryTransitionEvent> TransitionEvents);
