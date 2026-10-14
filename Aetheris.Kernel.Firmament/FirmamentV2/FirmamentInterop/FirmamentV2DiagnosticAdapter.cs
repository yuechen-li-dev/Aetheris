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
            ParserSeverity(diagnosticCode),
            MessageFor(diagnosticCode),
            FirmamentV2InteropValueAdapter.AdaptSourceSpan(sourceSpan),
            target,
            fieldName);

    // Legacy producers still return strings. At this boundary unclassified diagnostics
    // are errors; adding an error must never require updating an admission allowlist.
    public static FirmamentDiagnosticSeverity ParserSeverity(string code) => code switch
    {
        FirmamentV2Parser.ToleranceDroppedThroughArithmetic => FirmamentDiagnosticSeverity.Warning,
        "firmament-v2-parser-invoked" or "firmament-v2-parse-succeeded"
            or "firmament-concept-ir-resolved" or "firmament-concept-struct-erased-before-feature-air"
            or "firmament-template-expanded-before-feature-air" => FirmamentDiagnosticSeverity.Info,
        _ when code.EndsWith("-parsed", StringComparison.Ordinal)
            || code.EndsWith("-adapted", StringComparison.Ordinal)
            || code.EndsWith("-symbols-bound", StringComparison.Ordinal) => FirmamentDiagnosticSeverity.Info,
        _ => FirmamentDiagnosticSeverity.Fatal
    };

    public static string MessageFor(string diagnosticCode) => diagnosticCode switch
    {
        FirmamentV2Parser.CanonicalPrimitiveMalformed => "Malformed primitive: check the required dimension fields and their length units.",
        FirmamentV2Parser.CanonicalDocumentMalformed => "Malformed Model document. Check its declarations and closing braces.",
        "firmament-v2-parse-failed" => "Firmament V2 parsing or binding failed; the program has not been admitted for compilation.",
        FirmamentV2Parser.PmiDimensionMissingTolerance => "PMI dimension resolves to a value without tolerance evidence.",
        FirmamentV2Parser.ConceptMissingRequiredField => "Forge concept application is missing a required descriptor field.",
        FirmamentV2Parser.PmiUnknownDatum => "PMI relation references an unknown datum.",
        FirmamentV2Parser.ToleranceDroppedThroughArithmetic => "A toleranced value was used in arithmetic and only the nominal value was preserved.",
        _ when diagnosticCode.StartsWith(FirmamentV2Parser.CanonicalFieldUnknown + ":", StringComparison.Ordinal)
            => $"Unknown field '{diagnosticCode.Split(':')[^1]}' in {diagnosticCode.Split(':')[1]}. Check this construct's documented fields.",
        _ when diagnosticCode.StartsWith(FirmamentV2Parser.CanonicalDeclarationUnknown + ":", StringComparison.Ordinal)
            => $"Unknown statement '{diagnosticCode.Split(':')[^1]}'. Use an admitted construct in this context.",
        _ => diagnosticCode
    };
}
