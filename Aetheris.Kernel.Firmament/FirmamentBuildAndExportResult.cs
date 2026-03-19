namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentBuildAndExportResult(
    string SourcePath,
    string OutputPath,
    FirmamentStepExportResult Export);
