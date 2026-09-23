using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentLanguageCompletion(
    string Document, string Revision, string Context, int ReplaceStart, int ReplaceLength,
    IReadOnlyList<FirmamentAuthoringField> Fields,
    IReadOnlyList<string> MissingRequiredFields);

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
        if (WireFormAuthoring.IsInsideHelix(source, offset))
        {
            fields = FirmamentSchemaAuthoringFields.For("Helix");
            context = "Helix";
        }
        else if (LoftAuthoringParser.TryGetAuthoringFields(source, offset, out fields)) context = "Loft";
        else return new(document, revision, "Unsupported", prefixStart, prefix.Length, [], []);

        var open = source.LastIndexOf('{', Math.Max(0, offset - 1));
        var body = open >= 0 ? source[(open + 1)..offset] : string.Empty;
        var present = fields.Where(field => Regex.IsMatch(body, $@"\b{Regex.Escape(field.Name)}\s*:", RegexOptions.CultureInvariant))
            .Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        var candidates = fields.Where(field => !present.Contains(field.Name) && field.Name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        var missing = fields.Where(field => field.Required && !present.Contains(field.Name)).Select(field => field.Name).ToList();
        if (context == "Helix" && !present.Contains("Pitch") && !present.Contains("Height")) missing.Add("Pitch or Height");
        return new(document, revision, context, prefixStart, prefix.Length, candidates, missing);
    }
}
