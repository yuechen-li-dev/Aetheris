using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Execution;

public sealed record FirmamentPrimitiveExecutionResult(
    IReadOnlyList<FirmamentExecutedPrimitive> ExecutedPrimitives);

public sealed record FirmamentExecutedPrimitive(
    int OpIndex,
    string FeatureId,
    FirmamentLoweredPrimitiveKind Kind,
    BrepBody Body);
