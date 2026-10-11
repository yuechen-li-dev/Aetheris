using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentLanguageCompletion(
    string Document, string Revision, string Context, int ReplaceStart, int ReplaceLength,
    IReadOnlyList<FirmamentAuthoringField> Fields,
    IReadOnlyList<string> MissingRequiredFields,
    IReadOnlyList<FirmamentLanguageEntry>? Entries = null,
    IReadOnlyList<string>? Values = null);
public sealed record FirmamentLanguageEntry(string ConstructId, string Name, string Context, string Source);

/// <summary>Bounded authoring help from construct owners; no BRep or export work.</summary>
public static class FirmamentLanguageService
{
    public static FirmamentLanguageCompletion Complete(string source, string document, string revision, int offset)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (offset < 0 || offset > source.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        source = FirmamentSourceSpelling.Normalize(source);
        var prefixStart = offset;
        while (prefixStart > 0 && (char.IsLetterOrDigit(source[prefixStart - 1]) || source[prefixStart - 1] == '_')) prefixStart--;
        var prefix = source[prefixStart..offset];
        var lineStart = prefixStart == 0 ? 0 : source.LastIndexOf('\n', prefixStart - 1) + 1;
        var beforePrefix = source[lineStart..prefixStart];
        IReadOnlyList<FirmamentAuthoringField> fields;
        string context;
        var interfaceFamily = ActiveFrameInterface(source, offset);
        var composition = ActiveCompositionConstruct(source, offset);
        if (composition is not null)
        {
            beforePrefix = source[Math.Max(lineStart,composition.Value.Open+1)..prefixStart];
            fields = FirmamentSchemaAuthoringFields.For(composition.Value.Name);
            context = composition.Value.Name;
        }
        else if (interfaceFamily is not null)
        {
            fields = FirmamentSchemaAuthoringFields.For($"Interface<{interfaceFamily}>");
            context = $"Interface<{interfaceFamily}>";
        }
        else if (IsInsideThread(source, offset))
        {
            fields = FirmamentSchemaAuthoringFields.For("Thread");
            context = "Thread";
        }
        else if (IsInsidePerforation(source, offset))
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
        else if (IsInsideConstruct(source, offset, "Box")) { fields = FirmamentSchemaAuthoringFields.For("Box"); context = "Box"; }
        else if (IsInsideConstruct(source, offset, "Hole")) { fields = FirmamentSchemaAuthoringFields.For("Hole"); context = "Hole"; }
        else
        {
            if (beforePrefix.Contains(':')) return new(document, revision, "Value", prefixStart, prefix.Length, [], []);
            var inModify = IsInsideConstruct(source, offset, "Modify");
            var entries = FirmamentSemanticSchemas.All
                .Where(item => item.Entry is not null &&
                    (item.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                     || beforePrefix.EndsWith("Interface<", StringComparison.Ordinal)
                     && item.Name.StartsWith("Interface<" + prefix, StringComparison.OrdinalIgnoreCase)) &&
                    (!inModify || item.Context?.StartsWith("Modify", StringComparison.Ordinal) == true))
                .Select(item => new FirmamentLanguageEntry(item.Id.Value, item.Name, item.Context ?? string.Empty, FirmamentSourceSpelling.Prefer(item.Entry!))).ToArray();
            return new(document, revision, entries.Length > 0 ? "ConstructEntry" : "Unsupported",
                prefixStart, prefix.Length, [], [], entries);
        }

        var activeField = Regex.Match(beforePrefix, @"(?<field>[A-Za-z_]\w*)\s*:\s*$", RegexOptions.CultureInvariant);
        if (activeField.Success)
        {
            var field = fields.FirstOrDefault(item => item.Name == activeField.Groups["field"].Value);
            var values = field?.Choices?.Where(choice => choice.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
            return new(document, revision, "Value", prefixStart, prefix.Length, [], [], Values: values);
        }
        if (beforePrefix.Contains(':')) return new(document, revision, "Value", prefixStart, prefix.Length, [], []);

        var open = composition?.Open ?? source.LastIndexOf('{', Math.Max(0, offset - 1));
        var body = open >= 0 ? source[(open + 1)..offset] : string.Empty;
        var present = fields.Where(field => Regex.IsMatch(body, $@"\b{Regex.Escape(field.Name)}\s*:", RegexOptions.CultureInvariant))
            .Select(field => field.Name).ToHashSet(StringComparer.Ordinal);
        var candidates = fields.Where(field => !present.Contains(field.Name) && field.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
        var missing = fields.Where(field => field.Required && !present.Contains(field.Name)).Select(field => field.Name).ToList();
        if (context == "Interface<Fixed>" && present.Contains("Datum"))
        {
            candidates = candidates.Where(f => f.Name is not ("A" or "B")).ToArray();
            missing.RemoveAll(name => name is "A" or "B");
            if (!present.Contains("Members")) missing.Add("Members");
        }
        if (context == "Helix" && !present.Contains("Pitch") && !present.Contains("Height")) missing.Add("Pitch or Height");
        if (context is "Scene" or "Garment")
        {
            var entries = FirmamentSemanticSchemas.All.Where(item => item.Context == context && item.Entry is not null
                    && item.Name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))
                .Select(item => new FirmamentLanguageEntry(item.Id.Value,item.Name,item.Context!,FirmamentSourceSpelling.Prefer(item.Entry!))).ToArray();
            return new(document,revision,context,prefixStart,prefix.Length,candidates,missing,entries);
        }
        return new(document, revision, context, prefixStart, prefix.Length, candidates, missing);
    }

    private static bool IsInsidePerforation(string source, int offset)
    {
        var prefix = source[..offset];
        var header = Regex.Matches(prefix, @"\bPerforation\s+[A-Za-z_][A-Za-z0-9_]*\s*\{", RegexOptions.CultureInvariant).Cast<Match>().LastOrDefault();
        return header is not null && !prefix[(header.Index + header.Length)..].Contains('}');
    }

    private static string? ActiveFrameInterface(string source, int offset)
    {
        var prefix = source[..offset];
        var header = Regex.Matches(prefix, @"\bInterface\s*<\s*(Fixed|Revolute|Prismatic)\s*>\s+[A-Za-z_]\w*\s*\{", RegexOptions.CultureInvariant)
            .Cast<Match>().LastOrDefault();
        return header is not null && !prefix[(header.Index + header.Length)..].Contains('}')
            ? header.Groups[1].Value : null;
    }

    private static bool IsInsideThread(string source, int offset)
    {
        var prefix = source[..offset];
        var header = Regex.Matches(prefix, @"\bThread\s+[A-Za-z_][A-Za-z0-9_]*\s*\{", RegexOptions.CultureInvariant).Cast<Match>().LastOrDefault();
        return header is not null && !prefix[(header.Index + header.Length)..].Contains('}');
    }

    private static bool IsInsideConstruct(string source, int offset, string construct)
    {
        var prefix = source[..offset];
        var header = Regex.Matches(prefix, $@"\b{Regex.Escape(construct)}(?:\s*<[^>]+>)?\s+[A-Za-z_]\w*\s*\{{", RegexOptions.CultureInvariant)
            .Cast<Match>().LastOrDefault();
        return header is not null && !prefix[(header.Index + header.Length)..].Contains('}');
    }

    private static (string Name, int Open)? ActiveCompositionConstruct(string source, int offset)
    {
        // Ignore comments/strings and balance nested blocks in incomplete drafts.
        var prefix = Regex.Replace(source[..offset], @"//[^\r\n]*|/\*[\s\S]*?\*/|""(?:\\.|[^""\\])*""", m => new string(' ', m.Length));
        var names = "Scene|Room|Door|Window|Camera|WireRoute|Follow|Section|Points|Linear|Series|Appearance|Material|Placement|FrameTransform|Garment|Panel|Fabric|Drape|Interface";
        var matches = Regex.Matches(prefix, $@"\b(?<kind>{names})(?:\s*<(?<family>[^>]+)>)?(?:\s+[A-Za-z_]\w*)?\s*\{{");
        foreach (Match header in matches.Cast<Match>().Reverse())
        {
            var depth = 1;
            foreach (var c in prefix[(header.Index + header.Length)..])
            {
                if (c == '{') depth++;
                else if (c == '}') depth--;
                if (depth == 0) break;
            }
            if (depth > 0)
            {
                string name = header.Groups["kind"].Value;
                if (name == "Interface") name += "<" + header.Groups["family"].Value.Trim() + ">";
                return (name, header.Index + header.Length - 1);
            }
        }
        return null;
    }
}
