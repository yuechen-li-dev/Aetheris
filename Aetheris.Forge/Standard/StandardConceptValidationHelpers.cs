using Aetheris.Forge.Abstractions.FirmamentInterop;

namespace Aetheris.Forge.Standard;

internal static class StandardConceptValidationHelpers
{
    public static FirmamentDiagnostic Fatal(string code, string message, string? fieldName = null) =>
        new(code, FirmamentDiagnosticSeverity.Fatal, message, FieldName: fieldName);

    public static FirmamentDiagnostic Warning(string code, string message, string? fieldName = null) =>
        new(code, FirmamentDiagnosticSeverity.Warning, message, FieldName: fieldName);

    public static bool TryGetPositiveLength(
        ConceptValidationContext context,
        string fieldName,
        string code,
        string message,
        List<FirmamentDiagnostic> diagnostics,
        out double numericValue)
    {
        numericValue = 0;
        if (!context.TryGetScalar(fieldName, out var value) || value.Kind != FirmamentValueKind.Length || value.NumericValue is null)
        {
            return false;
        }

        numericValue = value.NumericValue.Value;
        if (numericValue > 0)
        {
            return true;
        }

        diagnostics.Add(Fatal(code, message, fieldName));
        return false;
    }

    public static bool TryGetNumeric(
        ConceptValidationContext context,
        string fieldName,
        FirmamentValueKind expectedKind,
        out double numericValue)
    {
        numericValue = 0;
        if (!context.TryGetScalar(fieldName, out var value) || value.Kind != expectedKind || value.NumericValue is null)
        {
            return false;
        }

        numericValue = value.NumericValue.Value;
        return true;
    }

    public static bool HasTarget(ConceptValidationContext context, string fieldName) =>
        context.TryGetTargetSource(fieldName, out _);

    public static bool HasNonEmptyString(ConceptValidationContext context, string fieldName)
    {
        if (!context.TryGetScalar(fieldName, out var value) || value.Kind != FirmamentValueKind.String)
        {
            return false;
        }

        return value.Nominal is string text && !string.IsNullOrWhiteSpace(text);
    }

    public static void AddToleranceRecommendation(
        ConceptValidationContext context,
        string fieldName,
        string code,
        string message,
        List<FirmamentDiagnostic> diagnostics)
    {
        if (context.TryGetScalar(fieldName, out var value) && value.Kind == FirmamentValueKind.Length && value.Tolerance is null)
        {
            diagnostics.Add(Warning(code, message, fieldName));
        }
    }
}
