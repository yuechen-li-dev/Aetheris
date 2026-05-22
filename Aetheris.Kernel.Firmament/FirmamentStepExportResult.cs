namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentStepExportResult(
    string StepText,
    string ExportedFeatureId,
    int ExportedOpIndex,
    string ExportedBodyCategory,
    string? ExportedFeatureKind = null,
    string ExportBodyPolicy = FirmamentStepExporter.LastExecutedGeometricBodyPolicy,
    string ExportBodySelectionReason = FirmamentStepExporter.LastExecutedGeometricBodySelectionReason,
    IReadOnlyList<FirmamentPmiInspectionDatum>? DatumInspection = null,
    IReadOnlyList<FirmamentPmiInspectionDimension>? DimensionInspection = null);

public sealed record FirmamentPmiInspectionDatum(
    string Label,
    string DatumType,
    string Target);

public sealed record FirmamentPmiInspectionDimension(
    string Kind,
    string Target,
    string? Datum,
    double Value,
    string? SourceTag,
    string? CandidateName = null);
