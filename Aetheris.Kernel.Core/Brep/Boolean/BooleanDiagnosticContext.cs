namespace Aetheris.Kernel.Core.Brep.Boolean;

public readonly record struct BooleanDiagnosticContext(BooleanOperation Operation, string? FeatureId = null)
{
    public string FormatMessage(string detail)
    {
        var subject = string.IsNullOrWhiteSpace(FeatureId)
            ? $"Boolean {Operation}"
            : $"Boolean feature '{FeatureId}' ({Operation.ToString().ToLowerInvariant()})";
        return $"{subject} {detail}";
    }

    public BooleanDiagnostic Error(BooleanDiagnosticCode code, string detail, string source)
        => new(code, FormatMessage(detail), source);
}
