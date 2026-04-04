namespace Aetheris.Firmament.FrictionLab.Harness;

internal sealed record FrictionLabSummary(
    int TotalCases,
    int SuccessCount,
    int PartialCount,
    int FailureCount,
    IReadOnlyList<FrictionCaseResult> CaseResults,
    string SummaryPath)
{
    public static FrictionLabSummary FromResults(IReadOnlyList<FrictionCaseResult> caseResults, string summaryPath)
    {
        var successCount = caseResults.Count(result => string.Equals(result.BuildStatus, "success", StringComparison.Ordinal));
        var partialCount = caseResults.Count(result => string.Equals(result.BuildStatus, "partial", StringComparison.Ordinal));
        var failureCount = caseResults.Count(result => string.Equals(result.BuildStatus, "failure", StringComparison.Ordinal));

        return new FrictionLabSummary(
            caseResults.Count,
            successCount,
            partialCount,
            failureCount,
            caseResults,
            summaryPath);
    }
}

internal sealed record FrictionCaseResult(
    string CaseId,
    string BuildStatus,
    string? StepArtifactPath,
    IReadOnlyList<FrictionDiagnostic> Diagnostics);

internal sealed record FrictionDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Source);
