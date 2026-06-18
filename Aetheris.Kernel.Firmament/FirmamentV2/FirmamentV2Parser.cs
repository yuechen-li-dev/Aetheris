using System.Globalization;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public static class FirmamentV2Parser
{
    public const string MissingModel = "firmament-v2-missing-model";
    public const string MissingUnits = "firmament-v2-missing-units";
    public const string MissingSolid = "firmament-v2-missing-solid";
    public const string UnsupportedConstruct = "firmament-v2-unsupported-construct";
    public const string UnknownRecordType = "firmament-v2-unknown-record-type";
    public const string BoxMissingSize = "firmament-v2-box-missing-size";
    public const string BoxSizeArity = "firmament-v2-box-size-arity";
    public const string DegenerateDimension = "firmament-degenerate-dimension";

    private static readonly Regex ModelRegex = new(@"\bmodel\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex UnitsRegex = new(@"\bunits\s+(?<units>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.CultureInvariant);
    private static readonly Regex SolidRegex = new(@"\bsolid\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<type>[A-Za-z_][A-Za-z0-9_]*)\s*\{(?<body>.*?)\}", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex SizeRegex = new(@"\bsize\s*:\s*\[(?<values>[^\]]*)\]", RegexOptions.CultureInvariant | RegexOptions.Singleline);

    public static FirmamentV2ParseResult Parse(string sourceText)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        var diagnostics = new List<string> { "firmament-v2-parser-invoked" };
        var source = StripLineComments(sourceText);

        if (ContainsUnsupportedConstruct(source)) diagnostics.Add(UnsupportedConstruct);

        var modelMatch = ModelRegex.Match(source);
        if (!modelMatch.Success) diagnostics.Add(MissingModel);

        var unitsMatch = UnitsRegex.Match(source);
        if (!unitsMatch.Success) diagnostics.Add(MissingUnits);
        else if (!string.Equals(unitsMatch.Groups["units"].Value, "mm", StringComparison.Ordinal)) diagnostics.Add(UnsupportedConstruct);

        var solidMatches = SolidRegex.Matches(source);
        if (solidMatches.Count == 0) diagnostics.Add(MissingSolid);
        if (solidMatches.Count > 1) diagnostics.Add(UnsupportedConstruct);

        FirmamentV2Document? document = null;
        if (modelMatch.Success && unitsMatch.Success && solidMatches.Count > 0)
        {
            var solid = solidMatches[0];
            var recordType = solid.Groups["type"].Value;
            if (!string.Equals(recordType, "Box", StringComparison.Ordinal)) diagnostics.Add(UnknownRecordType);

            var sizeMatch = SizeRegex.Match(solid.Groups["body"].Value);
            IReadOnlyList<double>? values = null;
            if (!sizeMatch.Success) diagnostics.Add(BoxMissingSize);
            else
            {
                values = ParseSizeValues(sizeMatch.Groups["values"].Value, diagnostics);
            }

            if (string.Equals(recordType, "Box", StringComparison.Ordinal) && values is not null && !diagnostics.Any(IsFatalDiagnostic))
            {
                document = new FirmamentV2Document(
                    modelMatch.Groups["name"].Value,
                    unitsMatch.Groups["units"].Value,
                    new FirmamentV2SolidBinding(solid.Groups["name"].Value, recordType, new FirmamentV2BoxRecord(values)));
            }
        }

        diagnostics.Add(document is null ? "firmament-v2-parse-failed" : "firmament-v2-parse-succeeded");
        diagnostics.Sort(StringComparer.Ordinal);
        return document is null ? FirmamentV2ParseResult.Failure(diagnostics) : FirmamentV2ParseResult.Success(document, diagnostics);
    }

    private static bool IsFatalDiagnostic(string code) => code is MissingModel or MissingUnits or MissingSolid or UnsupportedConstruct or UnknownRecordType or BoxMissingSize or BoxSizeArity or DegenerateDimension;

    private static IReadOnlyList<double>? ParseSizeValues(string raw, List<string> diagnostics)
    {
        var parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            diagnostics.Add(BoxSizeArity);
            return null;
        }

        var values = new List<double>(3);
        foreach (var part in parts)
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                diagnostics.Add(UnsupportedConstruct);
                return null;
            }

            if (value <= 0) diagnostics.Add(DegenerateDimension);
            values.Add(value);
        }

        return diagnostics.Contains(DegenerateDimension) ? null : values;
    }

    private static bool ContainsUnsupportedConstruct(string source) =>
        Regex.IsMatch(source, @"\b(with|concept|PMI|where|template|cut|add|shell|fillet|chamfer|region|regions|profile|material|pattern)\b|=>|<\s*Process\s*>", RegexOptions.CultureInvariant);

    private static string StripLineComments(string sourceText) => string.Join('\n', sourceText.Split('\n').Select(line =>
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index >= 0 ? line[..index] : line;
    }));
}
