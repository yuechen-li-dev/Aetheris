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
    public const string NameUnresolved = "firmament-v2-name-unresolved";
    public const string DuplicateName = "firmament-v2-duplicate-name";
    public const string WithRequiresRecord = "firmament-v2-with-requires-record";
    public const string WithRequiresBoxRecord = "firmament-v2-with-requires-box-record";
    public const string WithFieldNotFound = "firmament-v2-with-field-not-found";
    public const string WithFieldTypeMismatch = "firmament-v2-with-field-type-mismatch";
    public const string WithForwardReference = "firmament-v2-with-forward-reference";
    public const string WithDerivedRecordInvalid = "firmament-v2-with-derived-record-invalid";

    private static readonly Regex ModelRegex = new(@"\bmodel\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex UnitsRegex = new(@"\bunits\s+(?<units>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.CultureInvariant);
    private static readonly Regex SolidRegex = new(@"\bsolid\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<target>[A-Za-z_][A-Za-z0-9_]*)(?<with>\s+with)?\s*\{(?<body>.*?)\}", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex LegacyEqualsSolidRegex = new(@"\bsolid\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<target>[A-Za-z_][A-Za-z0-9_]*)(?<with>\s+with)?\s*\{(?<body>.*?)\}", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex SizeRegex = new(@"\bsize\s*:\s*\[(?<values>[^\]]*)\]", RegexOptions.CultureInvariant | RegexOptions.Singleline);
    private static readonly Regex FieldRegex = new(@"(?<field>@[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_\.]*)\s*:", RegexOptions.CultureInvariant);

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

        var solidMatches = SolidRegex.Matches(source).Cast<Match>().ToArray();
        if (solidMatches.Length == 0 && LegacyEqualsSolidRegex.IsMatch(source)) diagnostics.Add(UnsupportedConstruct);
        if (solidMatches.Length == 0) diagnostics.Add(MissingSolid);

        var solids = new List<FirmamentV2SolidBinding>();
        var byName = new Dictionary<string, FirmamentV2SolidBinding>(StringComparer.Ordinal);
        if (modelMatch.Success && unitsMatch.Success)
        {
            foreach (var solid in solidMatches)
            {
                var name = solid.Groups["name"].Value;
                var target = solid.Groups["target"].Value;
                var body = solid.Groups["body"].Value;
                var isWith = solid.Groups["with"].Success;
                if (byName.ContainsKey(name)) { diagnostics.Add(DuplicateName); continue; }

                FirmamentV2SolidBinding? binding = isWith
                    ? ParseDerived(name, target, body, byName, diagnostics)
                    : ParseDirect(name, target, body, diagnostics);
                if (binding is not null)
                {
                    solids.Add(binding);
                    byName.Add(name, binding);
                }
            }
        }

        FirmamentV2Document? document = null;
        if (modelMatch.Success && unitsMatch.Success && solids.Count > 0 && !diagnostics.Any(IsFatalDiagnostic))
            document = new FirmamentV2Document(modelMatch.Groups["name"].Value, unitsMatch.Groups["units"].Value, solids);

        diagnostics.Add(document is null ? "firmament-v2-parse-failed" : "firmament-v2-parse-succeeded");
        diagnostics.Sort(StringComparer.Ordinal);
        return document is null ? FirmamentV2ParseResult.Failure(diagnostics) : FirmamentV2ParseResult.Success(document, diagnostics);
    }

    private static FirmamentV2SolidBinding? ParseDirect(string name, string recordType, string body, List<string> diagnostics)
    {
        if (!string.Equals(recordType, "Box", StringComparison.Ordinal)) { diagnostics.Add(UnknownRecordType); return null; }
        var values = ParseSizeField(body, diagnostics, BoxMissingSize);
        return values is null ? null : new(name, "Box", new(values));
    }

    private static FirmamentV2SolidBinding? ParseDerived(string name, string baseName, string body, Dictionary<string, FirmamentV2SolidBinding> byName, List<string> diagnostics)
    {
        if (!byName.TryGetValue(baseName, out var baseSolid)) { diagnostics.Add(NameUnresolved); if (Regex.IsMatch(body, @"\bsize\s*:", RegexOptions.CultureInvariant)) diagnostics.Add(WithForwardReference); return null; }
        if (!string.Equals(baseSolid.RecordType, "Box", StringComparison.Ordinal)) { diagnostics.Add(WithRequiresBoxRecord); return null; }
        var fields = FieldRegex.Matches(body).Select(m => m.Groups["field"].Value).ToArray();
        if (fields.Length == 0) { diagnostics.Add(WithFieldNotFound); return null; }
        if (fields.Any(f => f.StartsWith('@'))) { diagnostics.Add(WithRequiresRecord); return null; }
        if (fields.Any(f => !string.Equals(f, "size", StringComparison.Ordinal))) { diagnostics.Add(WithFieldNotFound); return null; }
        var values = ParseSizeField(body, diagnostics, WithFieldTypeMismatch);
        if (values is null) { diagnostics.Add(WithDerivedRecordInvalid); return null; }
        return new(name, "Box", new(values), baseName, new Dictionary<string, IReadOnlyList<double>>(StringComparer.Ordinal) { ["size"] = values });
    }

    private static bool IsFatalDiagnostic(string code) => code is MissingModel or MissingUnits or MissingSolid or UnsupportedConstruct or UnknownRecordType or BoxMissingSize or BoxSizeArity or DegenerateDimension or NameUnresolved or DuplicateName or WithRequiresRecord or WithRequiresBoxRecord or WithFieldNotFound or WithFieldTypeMismatch or WithForwardReference or WithDerivedRecordInvalid;

    private static IReadOnlyList<double>? ParseSizeField(string body, List<string> diagnostics, string missingDiagnostic)
    {
        var sizeMatch = SizeRegex.Match(body);
        if (!sizeMatch.Success) { diagnostics.Add(missingDiagnostic); return null; }
        return ParseSizeValues(sizeMatch.Groups["values"].Value, diagnostics);
    }

    private static IReadOnlyList<double>? ParseSizeValues(string raw, List<string> diagnostics)
    {
        var parts = raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) { diagnostics.Add(BoxSizeArity); return null; }
        var values = new List<double>(3);
        foreach (var part in parts)
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) { diagnostics.Add(WithFieldTypeMismatch); return null; }
            if (value <= 0) diagnostics.Add(DegenerateDimension);
            values.Add(value);
        }
        return diagnostics.Contains(DegenerateDimension) ? null : values;
    }

    private static bool ContainsUnsupportedConstruct(string source) =>
        Regex.IsMatch(source, @"\b(concept|PMI|where|template|cut|add|shell|fillet|chamfer|region|regions|profile|material|pattern)\b|=>|<\s*Process\s*>", RegexOptions.CultureInvariant);

    private static string StripLineComments(string sourceText) => string.Join('\n', sourceText.Split('\n').Select(line =>
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index >= 0 ? line[..index] : line;
    }));
}
