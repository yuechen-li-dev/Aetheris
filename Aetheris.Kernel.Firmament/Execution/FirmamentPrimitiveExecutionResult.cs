using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Execution;

public sealed record FirmamentPrimitiveExecutionResult(
    IReadOnlyList<FirmamentExecutedPrimitive> ExecutedPrimitives,
    IReadOnlyList<FirmamentExecutedBoolean> ExecutedBooleans,
    NativeGeometryState NativeGeometryState);

public sealed record FirmamentExecutedPrimitive(
    int OpIndex,
    string FeatureId,
    FirmamentLoweredPrimitiveKind Kind,
    BrepBody Body,
    BrepExtrudeConstructionTopology? BoxConstructionTopology = null);

public sealed record FirmamentExecutedBoolean(
    int OpIndex,
    string FeatureId,
    FirmamentLoweredBooleanKind Kind,
    BrepBody Body,
    SafeBooleanComposition? SemanticSafeComposition = null);
