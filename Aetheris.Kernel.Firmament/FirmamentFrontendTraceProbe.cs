using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.ParsedModel;
using Aetheris.Kernel.Firmament.Parsing;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentFrontendTraceProbeResult(
    string ParserName,
    bool ParseSucceeded,
    string FrontendStageReached,
    string FrontendSummary,
    IReadOnlyList<string> Diagnostics,
    FirmamentPrimitiveAirTraceSummary? FeatureAir = null,
    FirmamentConstructiveAirTraceSummary? ConstructiveAir = null);

public sealed record FirmamentPrimitiveAirTraceSummary(
    bool ParserBacked,
    string SourceOpKind,
    string FeatureAirNodeKind,
    FirmamentTraceDimensions? SourceDimensions,
    string ConstructionIntent,
    string StageReached,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Guarantees);

public sealed record FirmamentConstructiveAirTraceSummary(
    string NodeKind,
    string CanonicalForm,
    string StageReached,
    IReadOnlyList<string> Diagnostics);

public sealed record FirmamentTraceDimensions(double Width, double Depth, double Height);

public static class FirmamentFrontendTraceProbe
{
    public static FirmamentFrontendTraceProbeResult ParseOnly(string sourceText)
    {
        ArgumentNullException.ThrowIfNull(sourceText);

        var parseResult = FirmamentTopLevelParser.Parse(sourceText);
        if (parseResult.IsSuccess)
        {
            var opCount = parseResult.Value.Ops.Entries.Count;
            var boxOp = parseResult.Value.Ops.Entries.FirstOrDefault(op => op.KnownKind == FirmamentKnownOpKind.Box);
            if (boxOp is not null)
            {
                var dimensions = TryExtractBoxDimensions(boxOp) ?? TryExtractBoxDimensionsFromSource(sourceText);
                var dimensionDiagnostics = dimensions is null
                    ? new[] { "air-x9-box-dimensions-missing" }
                    : new[] { "air-x9-source-dimensions-extracted" };
                var featureAirDiagnostics = new[]
                {
                    "air-x9-parser-backed-fixture-loaded",
                    "air-x9-firmament-parser-invoked",
                    "air-x9-firmament-parse-succeeded",
                    "air-x9-firmament-box-op-recognized",
                    "air-x9-feature-air-summary-created",
                    "air-x9-feature-air-box-created",
                    "air-x9-actual-stage-feature-air",
                    "air-x9-box-constructive-air-lowering-not-wired",
                    "air-x9-constructive-air-deferred",
                    "air-x9-no-production-grammar-change",
                    "air-x9-no-production-route-replacement"
                }.Concat(dimensionDiagnostics).Order(StringComparer.Ordinal).ToArray();

                var featureAir = new FirmamentPrimitiveAirTraceSummary(
                    ParserBacked: true,
                    SourceOpKind: boxOp.OpName,
                    FeatureAirNodeKind: "CreateBox",
                    SourceDimensions: dimensions,
                    ConstructionIntent: "box / rectangular prism",
                    StageReached: "feature-air",
                    Diagnostics: featureAirDiagnostics,
                    Guarantees:
                    [
                        "parser-backed Firmament source form",
                        "Feature AIR summary only",
                        "Constructive AIR deferred",
                        "no production grammar expansion",
                        "no production route replacement",
                        "no geometry emitted"
                    ]);

                return new(
                    "FirmamentTopLevelParser",
                    true,
                    "feature-air",
                    $"Parsed Firmament document with {opCount} op(s). Recognized box op and created Feature AIR CreateBox summary; Constructive AIR is deferred.",
                    featureAirDiagnostics,
                    featureAir);
            }

            return new(
                "FirmamentTopLevelParser",
                true,
                "parsed",
                $"Parsed Firmament document with {opCount} op(s). No AIR-X9 supported primitive op was recognized.",
                [
                    "air-x9-firmament-parser-invoked",
                    "air-x9-firmament-parse-succeeded",
                    "air-x9-firmament-box-op-not-recognized",
                    "air-x9-no-production-grammar-change"
                ]);
        }

        return new(
            "FirmamentTopLevelParser",
            false,
            "parsed",
            "Firmament parser rejected the fixture source body.",
            [
                "air-x9-firmament-parser-invoked",
                "air-x9-firmament-parse-failed",
                .. parseResult.Diagnostics.Select(d => d.Code.ToString()).Order(StringComparer.Ordinal),
                "air-x9-no-production-grammar-change"
            ]);
    }

    private static FirmamentTraceDimensions? TryExtractBoxDimensions(FirmamentParsedOpEntry boxOp)
    {
        return boxOp.RawFields.TryGetValue("size[3]", out var rawSize) || boxOp.RawFields.TryGetValue("size[3]:", out rawSize)
            ? TryParseDimensions(rawSize)
            : null;
    }

    private static FirmamentTraceDimensions? TryExtractBoxDimensionsFromSource(string sourceText)
    {
        var match = Regex.Match(sourceText, @"(?m)^\s*size\[3\]:\s*\r?\n\s*(?<w>[+-]?(?:\d+(?:\.\d*)?|\.\d+))\s*\r?\n\s*(?<d>[+-]?(?:\d+(?:\.\d*)?|\.\d+))\s*\r?\n\s*(?<h>[+-]?(?:\d+(?:\.\d*)?|\.\d+))\s*$", RegexOptions.CultureInvariant);
        return match.Success
            ? TryParseDimensions($"{match.Groups["w"].Value}\n{match.Groups["d"].Value}\n{match.Groups["h"].Value}")
            : null;
    }

    private static FirmamentTraceDimensions? TryParseDimensions(string rawSize)
    {
        var values = rawSize
            .Split(['\r', '\n', ',', '[', ']'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        if (values.Length != 3)
        {
            return null;
        }

        return double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
            && double.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var depth)
            && double.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var height)
            && width > 0
            && depth > 0
            && height > 0
            ? new FirmamentTraceDimensions(width, depth, height)
            : null;
    }
}
