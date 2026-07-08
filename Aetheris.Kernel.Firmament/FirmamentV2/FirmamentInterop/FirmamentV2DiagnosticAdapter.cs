using Aetheris.Forge.Abstractions.FirmamentInterop;

namespace Aetheris.Kernel.Firmament.FirmamentV2.FirmamentInterop;

public static class FirmamentV2DiagnosticAdapter
{
    public static FirmamentDiagnostic FromValidationDiagnostic(
        FirmamentV2ValidationDiagnostic diagnostic,
        FirmamentV2SourceSpan? sourceSpan = null,
        string? target = null,
        string? fieldName = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        return new FirmamentDiagnostic(
            diagnostic.Code,
            diagnostic.Severity switch
            {
                "fatal" => FirmamentDiagnosticSeverity.Fatal,
                "warning" => FirmamentDiagnosticSeverity.Warning,
                _ => FirmamentDiagnosticSeverity.Info
            },
            diagnostic.Message,
            FirmamentV2InteropValueAdapter.AdaptSourceSpan(sourceSpan),
            target,
            fieldName);
    }

    public static FirmamentDiagnostic FromParserDiagnosticCode(
        string diagnosticCode,
        FirmamentV2SourceSpan? sourceSpan = null,
        string? target = null,
        string? fieldName = null) =>
        new(
            diagnosticCode,
            FirmamentV2Parser.IsFatalDiagnosticCode(diagnosticCode) ? FirmamentDiagnosticSeverity.Fatal : FirmamentDiagnosticSeverity.Warning,
            MessageFor(diagnosticCode),
            FirmamentV2InteropValueAdapter.AdaptSourceSpan(sourceSpan),
            target,
            fieldName);

    private static string MessageFor(string diagnosticCode) => diagnosticCode switch
    {
        FirmamentV2Parser.PmiDimensionMissingTolerance => "PMI dimension resolves to a value without tolerance evidence.",
        FirmamentV2Parser.ConceptMissingRequiredField => "Forge concept application is missing a required descriptor field.",
        FirmamentV2Parser.PmiUnknownDatum => "PMI relation references an unknown datum.",
        FirmamentV2Parser.ToleranceDroppedThroughArithmetic => "A toleranced value was used in arithmetic and only the nominal value was preserved.",
        _ => diagnosticCode
    };
}
