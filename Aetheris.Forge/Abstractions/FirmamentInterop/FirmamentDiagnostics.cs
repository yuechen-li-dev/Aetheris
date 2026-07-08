namespace Aetheris.Forge.Abstractions.FirmamentInterop;

public enum FirmamentDiagnosticSeverity
{
    Info,
    Warning,
    Fatal
}

public sealed record FirmamentDiagnostic(
    string Code,
    FirmamentDiagnosticSeverity Severity,
    string Message,
    FirmamentSourceSpan? SourceSpan = null,
    string? Target = null,
    string? FieldName = null);
