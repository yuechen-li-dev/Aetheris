namespace Aetheris.Surfacing;

public sealed record SculptingTimingEvidence(double BasePreparationMilliseconds, double OperationConstructionMilliseconds, double LocalityAndPreservationMilliseconds, double BrepValidationMilliseconds, double StepExportMilliseconds);
public sealed record SculptingCompileResult(bool IsSuccess, string ModelName, BodyState? OutputState, IReadOnlyDictionary<string, BodyState> States, IReadOnlyList<SculptDiagnostic> Diagnostics, SculptingTimingEvidence Timings);

