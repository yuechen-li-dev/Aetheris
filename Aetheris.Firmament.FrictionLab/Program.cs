using Aetheris.Firmament.FrictionLab.Harness;

var caseIds = ParseCaseIds(args);
var summaryFileName = ParseSummaryFileName(args);

var runner = new FrictionLabRunner();
var summary = runner.Run(caseIds, summaryFileName);

Console.WriteLine($"FrictionLab processed {summary.TotalCases} case(s): success={summary.SuccessCount}, partial={summary.PartialCount}, failure={summary.FailureCount}.");
Console.WriteLine($"Summary: {summary.SummaryPath}");

static IReadOnlyList<string>? ParseCaseIds(string[] args)
{
    const string casesPrefix = "--cases=";
    var casesArg = args.FirstOrDefault(arg => arg.StartsWith(casesPrefix, StringComparison.Ordinal));
    if (string.IsNullOrWhiteSpace(casesArg))
    {
        return null;
    }

    var raw = casesArg[casesPrefix.Length..];
    var caseIds = raw
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    return caseIds.Length == 0 ? null : caseIds;
}

static string ParseSummaryFileName(string[] args)
{
    const string summaryPrefix = "--summary=";
    var summaryArg = args.FirstOrDefault(arg => arg.StartsWith(summaryPrefix, StringComparison.Ordinal));
    if (string.IsNullOrWhiteSpace(summaryArg))
    {
        return "summary.json";
    }

    var fileName = summaryArg[summaryPrefix.Length..].Trim();
    return string.IsNullOrWhiteSpace(fileName) ? "summary.json" : fileName;
}
