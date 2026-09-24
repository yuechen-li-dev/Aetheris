using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentLanguageCompletion(
    string Document, string Revision, string Context, int ReplaceStart, int ReplaceLength,
    IReadOnlyList<FirmamentAuthoringField> Fields,
    IReadOnlyList<string> MissingRequiredFields,
    IReadOnlyList<FirmamentLanguageEntry>? Entries = null);
public sealed record FirmamentLanguageEntry(string ConstructId, string Name, string Context, string Source);

/// <summary>Bounded authoring help from construct owners; no BRep or export work.</summary>
public static class FirmamentLanguageService
{
    public static FirmamentLanguageCompletion Complete(string source, string document, string revision, int offset)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (offset < 0 || offset > source.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        var prefixStart = offset;
        while (prefixStart > 0 && (char.IsLetterOrDigit(source[prefixStart - 1]) || source[prefixStart - 1] == '_')) prefixStart--;
        var prefix = source[prefixStart..offset];
        var lineStart = prefixStart == 0 ? 0 : source.LastIndexOf('\n', prefixStart - 1) + 1;
        var beforePrefix = source[lineStart..prefixStart];
        if (beforePrefix.Contains(':')) return new(document, revision, "Value", prefixStart, prefix.Length, [], []);

        IReadOnlyList<FirmamentAuthoringField> fields;
        string context;
        if (IsInsidePerforation(source, offset))
        {
            fields = FirmamentSchemaAuthoringFields.For("Perforation");
            context = "Perforation";
        }
        else if (WireFormAuthoring.IsInsideHelix(source, offset))
        {
            fields = FirmamentSchemaAuthoringFields.For("Helix");
            context = "Helix";
        }
        else if (LoftAuthoringParser.TryGetAuthoringFields(source, offset, out fields)) context = "Loft";
        else
        {
            var entries = FirmamentSemanticSchemas.All
                .Where(item => item.Name is "Helix" or "Loft" or "Perforation" && item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && item.Entry is not null)
                .Select(item => new FirmamentLanguageEntry(item.Id.Value, item.Name, item.Context ?? string.Empty, item.Entry!)).ToArray();
            return new(document, revision, entries.Length > 0 ? "ConstructEntry" : "Unsupported",
                prefixStart, prefix.Length, [], [], entries);
        }

        var open = source.LastIndexOf('{', Math.Max(0, offset - 1));
        var body = open >= 0 ? source[(open + 1)..offset] : string.Empty;
        var present = fields.Where(field => Regex.IsMatch(body, $@"\b{Regex.Escape(field.Name)}\s*:", RegexOptions.CultureInvariant))
            .Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        var candidates = fields.Where(field => !present.Contains(field.Name) && field.Name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        var missing = fields.Where(field => field.Required && !present.Contains(field.Name)).Select(field => field.Name).ToList();
        if (context == "Helix" && !present.Contains("Pitch") && !present.Contains("Height")) missing.Add("Pitch or Height");
        return new(document, revision, context, prefixStart, prefix.Length, candidates, missing);
    }

    private static bool IsInsidePerforation(string source, int offset)
    {
        var prefix = source[..offset];
        var header = Regex.Matches(prefix, @"\bPerforation\s+[A-Za-z_][A-Za-z0-9_]*\s*\{", RegexOptions.CultureInvariant).Cast<Match>().LastOrDefault();
        return header is not null && !prefix[(header.Index + header.Length)..].Contains('}');
    }
}
