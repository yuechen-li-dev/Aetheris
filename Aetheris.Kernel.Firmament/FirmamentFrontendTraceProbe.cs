using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.ParsedModel;
using Aetheris.Kernel.Firmament.Parsing;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament;

public sealed record FirmamentFrontendTraceProbeResult(
    string ParserName,
    bool ParseSucceeded,
    string FrontendStageReached,
    string FrontendSummary,
    IReadOnlyList<string> Diagnostics,
    FirmamentPrimitiveAirTraceSummary? FeatureAir = null,
    FirmamentConstructiveAirTraceSummary? ConstructiveAir = null,
    FirmamentV2TraceSummary? FirmamentV2 = null);

public sealed record FirmamentV2TraceSummary(string SyntaxVersion, string ModelName, string Units, string SolidName, string RecordType, IReadOnlyList<double> Size, string StageReached, IReadOnlyList<FirmamentV2SolidTraceSummary> Solids);
public sealed record FirmamentV2SolidTraceSummary(string Name, string RecordType, IReadOnlyList<double> Size, string? DerivedFrom, IReadOnlyDictionary<string, IReadOnlyList<double>> Overrides);

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
    string SourceFeatureAirNodeKind,
    string ProfileKind,
    FirmamentTraceDimensions Dimensions,
    string ExtrusionAxis,
    string ConstructionIntent,
    string RouteKind,
    string StageReached,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<string> Guarantees);

public sealed record FirmamentTraceDimensions(double Width, double Depth, double Height);

public static class FirmamentFrontendTraceProbe
{

    public static FirmamentFrontendTraceProbeResult ParseV2Only(string sourceText)
    {
        ArgumentNullException.ThrowIfNull(sourceText);

        var parseResult = FirmamentV2Parser.Parse(sourceText);
        if (!parseResult.IsSuccess || parseResult.Document is null)
        {
            return new(
                "FirmamentV2Parser",
                false,
                "parsed",
                "Firmament V2 parser rejected the fixture source body.",
                parseResult.Diagnostics);
        }

        var document = parseResult.Document;
        var loweredSolid = document.Solids.LastOrDefault(s => s.IsDerived) ?? document.Solid;
        var dimensions = new FirmamentTraceDimensions(loweredSolid.Box.Size[0], loweredSolid.Box.Size[1], loweredSolid.Box.Size[2]);
        var featureDiagnostics = new[]
        {
            "firmament-v2-box-record-recognized",
            "firmament-v2-feature-air-create-box-created",
            "firmament-v2-inherited-model-units",
            "firmament-v2-no-v1-parser",
            "firmament-v2-parser-backed-fixture-loaded"
        }.Concat(parseResult.Diagnostics).Order(StringComparer.Ordinal).ToArray();

        var featureAir = new FirmamentPrimitiveAirTraceSummary(
            ParserBacked: true,
            SourceOpKind: loweredSolid.RecordType,
            FeatureAirNodeKind: "CreateBox",
            SourceDimensions: dimensions,
            ConstructionIntent: "Box",
            StageReached: "feature-air",
            Diagnostics: featureDiagnostics,
            Guarantees:
            [
                "Firmament V2 parser invoked",
                "typed record solid binding",
                "model units inherited by Box size",
                "Feature AIR CreateBox summary",
                "no V1 parser route",
                "no production route replacement",
                "no new geometry emitted"
            ]);

        return new(
            "FirmamentV2Parser",
            true,
            "feature-air",
            $"Firmament V2 parsed model '{document.ModelName}' with units '{document.Units}', lowered solid '{loweredSolid.Name}: {loweredSolid.RecordType}', and created Feature AIR CreateBox summary.",
            featureDiagnostics,
            featureAir,
            null,
            new FirmamentV2TraceSummary("FirmamentV2", document.ModelName, document.Units, loweredSolid.Name, loweredSolid.RecordType, loweredSolid.Box.Size, "feature-air", document.Solids.Select(s => new FirmamentV2SolidTraceSummary(s.Name, s.RecordType, s.Box.Size, s.DerivedFrom, s.Overrides ?? new Dictionary<string, IReadOnlyList<double>>())).ToArray()));
    }

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
                    ? new[] { "air-x10-box-dimensions-missing" }
                    : new[] { "air-x10-box-dimensions-extracted", "air-x9-source-dimensions-extracted" };
                var featureAirDiagnostics = new[]
                {
                    "air-x10-parser-backed-fixture-loaded",
                    "air-x10-firmament-parser-invoked",
                    "air-x10-firmament-parse-succeeded",
                    "air-x10-firmament-box-op-recognized",
                    "air-x10-feature-air-box-read",
                    "air-x10-no-production-grammar-change",
                    "air-x10-no-production-route-replacement",
                    "air-x9-feature-air-summary-created",
                    "air-x9-feature-air-box-created"
                }.Concat(dimensionDiagnostics).Order(StringComparer.Ordinal).ToArray();

                var featureAir = new FirmamentPrimitiveAirTraceSummary(
                    ParserBacked: true,
                    SourceOpKind: boxOp.OpName,
                    FeatureAirNodeKind: "CreateBox",
                    SourceDimensions: dimensions,
                    ConstructionIntent: "Box",
                    StageReached: "feature-air",
                    Diagnostics: featureAirDiagnostics,
                    Guarantees:
                    [
                        "parser-backed Firmament source form",
                        "Feature AIR CreateBox summary",
                        "Constructive AIR rectangle profile extrusion summary",
                        "no production grammar expansion",
                        "no production route replacement",
                        "no geometry emitted"
                    ]);

                var constructiveDiagnostics = dimensions is null
                    ? new[] { "air-x10-box-canonicalization-failed" }
                    : new[]
                    {
                        "air-x10-box-canonicalized-to-profile-extrude",
                        "air-x10-constructive-air-summary-created",
                        "air-x10-actual-stage-constructive-air",
                        "air-x10-profile-extrude-wrapper-not-invoked",
                        "air-x10-brepplan-deferred",
                        "air-x10-emission-deferred",
                        "air-x10-cir-mirror-deferred",
                        "air-x10-no-geometry-emission-required"
                    }.Order(StringComparer.Ordinal).ToArray();

                var constructiveAir = dimensions is null
                    ? null
                    : new FirmamentConstructiveAirTraceSummary(
                        NodeKind: "AirProfileExtrude",
                        CanonicalForm: "rectangle-profile-extrude",
                        SourceFeatureAirNodeKind: "CreateBox",
                        ProfileKind: "Rectangle",
                        Dimensions: dimensions,
                        ExtrusionAxis: "Z",
                        ConstructionIntent: "Box",
                        RouteKind: "ProfileExtrude",
                        StageReached: "constructive-air",
                        Diagnostics: constructiveDiagnostics,
                        Guarantees:
                        [
                            "trace summary only",
                            "rectangle profile uses size[0] width and size[1] depth",
                            "extrusion uses size[2] height",
                            "profile extrusion wrapper not invoked",
                            "BRepPlan deferred",
                            "emission deferred",
                            "CIR mirror deferred"
                        ]);

                var allDiagnostics = featureAirDiagnostics.Concat(constructiveDiagnostics).Order(StringComparer.Ordinal).ToArray();
                return new(
                    "FirmamentTopLevelParser",
                    true,
                    dimensions is null ? "feature-air" : "constructive-air",
                    dimensions is null
                        ? $"Parsed Firmament document with {opCount} op(s). Recognized box op and created Feature AIR CreateBox summary; Constructive AIR canonicalization failed because dimensions were missing or invalid."
                        : $"Parsed Firmament document with {opCount} op(s). Recognized box op, created Feature AIR CreateBox summary, and canonicalized it to Constructive AIR AirProfileExtrude rectangle profile extrusion.",
                    allDiagnostics,
                    featureAir,
                    constructiveAir);
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
