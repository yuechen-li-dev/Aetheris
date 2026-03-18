namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentStepExportResult(
    string StepText,
    string ExportedFeatureId,
    int ExportedOpIndex,
    string ExportBodyPolicy = FirmamentStepExporter.LastExecutedGeometricBodyPolicy);
