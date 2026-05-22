using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Execution;

public sealed record FirmamentValidationExecutionResult(
    IReadOnlyList<FirmamentExecutedValidation> Validations);

public sealed record FirmamentExecutedValidation(
    int OpIndex,
    FirmamentKnownOpKind Kind,
    string? Target,
    bool IsExecuted,
    bool IsSuccess,
    string? Reason);
