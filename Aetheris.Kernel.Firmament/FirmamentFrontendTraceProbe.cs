using Aetheris.Kernel.Firmament.Parsing;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentFrontendTraceProbeResult(
    string ParserName,
    bool ParseSucceeded,
    string FrontendStageReached,
    string FrontendSummary,
    IReadOnlyList<string> Diagnostics);

public static class FirmamentFrontendTraceProbe
{
    public static FirmamentFrontendTraceProbeResult ParseOnly(string sourceText)
    {
        ArgumentNullException.ThrowIfNull(sourceText);

        var parseResult = FirmamentTopLevelParser.Parse(sourceText);
        if (parseResult.IsSuccess)
        {
            var opCount = parseResult.Value.Ops.Entries.Count;
            return new(
                "FirmamentTopLevelParser",
                true,
                "parsed",
                $"Parsed Firmament document with {opCount} op(s). AIR lowering is not wired for parser-backed trace fixtures in AIR-X8.",
                [
                    "air-x8-firmament-parser-invoked",
                    "air-x8-firmament-parse-succeeded",
                    "air-x8-frontend-stage-recorded",
                    "air-x8-air-lowering-not-wired-for-parser-backed-fixture",
                    "air-x8-parser-backed-lowering-boundary-reached",
                    "air-x8-parser-backed-fixture-does-not-change-production-grammar"
                ]);
        }

        return new(
            "FirmamentTopLevelParser",
            false,
            "parsed",
            "Firmament parser rejected the fixture source body.",
            [
                "air-x8-firmament-parser-invoked",
                "air-x8-firmament-parse-failed",
                "air-x8-frontend-stage-recorded",
                .. parseResult.Diagnostics.Select(d => d.Code.ToString()).Order(StringComparer.Ordinal),
                "air-x8-parser-backed-fixture-does-not-change-production-grammar"
            ]);
    }
}
