namespace Aetheris.Surfacing;

public sealed record SculptingTimingEvidence(double BasePreparationMilliseconds, double OperationConstructionMilliseconds, double LocalityAndPreservationMilliseconds, double BrepValidationMilliseconds, double StepExportMilliseconds);
public sealed record SculptingCompileResult(bool IsSuccess, string ModelName, BodyState? OutputState, IReadOnlyDictionary<string, BodyState> States, IReadOnlyList<SculptDiagnostic> Diagnostics, SculptingTimingEvidence Timings)
{
    public MeshSculptState? OutputMesh { get; init; }
    public IReadOnlyDictionary<string, MeshSculptState> MeshStates { get; init; } = new Dictionary<string, MeshSculptState>();
}

