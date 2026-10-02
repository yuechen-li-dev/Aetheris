using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

public sealed record AssemblyReleaseRecord(string Name, string Type,
    IReadOnlyDictionary<string, string> Fields, SemanticSourceSpan SourceSpan);
public sealed record AssemblyPmiNote(string Name, string Target, string Text, SemanticSourceSpan SourceSpan);
public sealed record AssemblyAnnotations(AssemblyReleaseRecord? Release, IReadOnlyList<AssemblyPmiNote> Notes);

[FirmamentConstruct("AssemblyProvenance", "Provenance", Context = "Assembly", Entry = "Provenance: Release;", Description = "Select a typed Static release Record for AP242 header metadata and inspectable semantic annotations.")]
public static class AssemblyProvenanceDeclaration { }

[FirmamentConstruct("AssemblyPmi", "Pmi", Context = "Assembly", Entry = "Pmi { Note DesignIntent { Target: Product; Text: \"Design intent\"; } }", Description = "Root-owned semantic notes associated with declared product occurrences; no face or GD&T inference.")]
public static class AssemblyPmiDeclaration { }

[FirmamentConstruct("AssemblyPmiNote", "Note", Context = "Assembly Pmi", Entry = "Note DesignIntent { Target: Product; Text: \"Design intent\"; }", Description = "An engineer-authored product annotation with an explicit occurrence target.")]
[FirmamentField("Target", "Target", FirmamentSchemaValueKind.ConstructReference, Required = true)]
[FirmamentField("Text", "Text", FirmamentSchemaValueKind.String, Required = true)]
public static class AssemblyPmiNoteDeclaration { }

/// <summary>Root-owned product annotations. Typed data stays with the ordinary Record binder;
/// this seam only selects that data and binds annotation owners, without manufacturing geometry.</summary>
internal static class AssemblyPmiAuthoring
{
    internal static AssemblyAnnotations Parse(ref string source, string identity, List<AssemblyDiagnostic> diagnostics)
    {
        var original = source;
        var mask = Mask(source);
        var changes = new List<(int Start, int Length)>();
        var notes = new List<AssemblyPmiNote>();
        AssemblyReleaseRecord? release = null;
        // Template Assembly definitions have a '>' immediately before Assembly.
        var roots = Regex.Matches(mask, @"\bAssembly\s+[A-Za-z_]\w*\s*\{").Cast<Match>()
            .Where(m => Depth(mask, m.Index) == 0 && mask[..m.Index].TrimEnd().LastOrDefault() != '>').ToArray();
        if (roots.Length != 1) return new(null, []); // The owning assembly parser diagnoses root structure.
        var root = roots[0]; var rootEnd = End(mask, root.Index + root.Length - 1);
        if (rootEnd < 0) return new(null, []);
        var bodyStart = root.Index + root.Length;
        var body = mask[bodyStart..rootEnd];
        // Note text is opaque to the Record binder, too. It must never discover
        // a fictional Record or Static declaration inside an annotation string.
        var catalogChars = original.ToCharArray();
        foreach (Match header in Regex.Matches(mask, @"\bPmi\s*\{"))
        {
            var end = End(mask, header.Index + header.Length - 1);
            if (end >= 0) Array.Fill(catalogChars, ' ', header.Index, end - header.Index + 1);
        }
        var catalog = new string(catalogChars);
        var selections = Regex.Matches(body, @"\bProvenance\s*:\s*(?<name>[A-Za-z_]\w*)\s*;");
        if (Regex.Matches(body, @"\bProvenance\s*:").Count != selections.Count || selections.Count > 1)
            Error("provenance-selection-invalid", "Expected one Provenance: StaticRecord; in the root Assembly.");
        foreach (Match selection in selections)
        {
            if (Depth(body, selection.Index) != 0) { Error("provenance-scope-invalid", "Provenance belongs to the root Assembly."); continue; }
            var name = selection.Groups["name"].Value;
            var recordDiagnostics = new List<string>();
            var value = FirmamentV2TemplateExpansion.InspectStaticRecords(catalog, recordDiagnostics).SingleOrDefault(r => r.Name == name);
            foreach (var diagnostic in recordDiagnostics) Error("provenance-record-invalid", diagnostic);
            if (value is null) Error("provenance-record-unresolved", name);
            else
            {
                var schema = FirmamentV2TemplateExpansion.InspectRecords(catalog, recordDiagnostics).Single(r => r.Name == value.TypeName);
                foreach (var required in new[] { ("Author", "String"), ("Date", "Date"), ("Version", "Version"), ("Description", "String") })
                    if (schema.Fields.GetValueOrDefault(required.Item1) != required.Item2)
                        Error("provenance-field-invalid", $"{name}.{required.Item1} must have type {required.Item2}.");
                if (schema.Fields.ContainsKey("Organization") && schema.Fields["Organization"] != "String")
                    Error("provenance-field-invalid", $"{name}.Organization must have type String.");
                if (schema.Fields.Values.Any(t => t is not ("String" or "Date" or "Version" or "Length" or "Angle" or "Int" or "Float" or "Bool")))
                    Error("provenance-field-invalid", "Release records admit flat scalar engineering data.");
                var fields = value.Fields.ToDictionary(p => p.Key, p => Unquote(p.Value), StringComparer.Ordinal);
                if (fields.Values.Any(string.IsNullOrWhiteSpace)) Error("provenance-field-invalid", "Release fields must be non-empty.");
                release = new(name, value.TypeName, fields, new(identity, bodyStart + selection.Index, selection.Length));
                // Erase only the selected data from the material-definition source so metadata edits
                // cannot invalidate the reusable geometry cache. The typed value is retained above.
                foreach (Match declaration in Regex.Matches(mask, $@"\b(?:Record\s+{Regex.Escape(value.TypeName)}|Static\s+{Regex.Escape(name)}\s*:\s*{Regex.Escape(value.TypeName)})\s*\{{"))
                {
                    var end = End(mask, declaration.Index + declaration.Length - 1);
                    if (end >= 0) changes.Add((declaration.Index, end - declaration.Index + 1));
                }
            }
            changes.Add((bodyStart + selection.Index, selection.Length));
        }
        foreach (Match header in Regex.Matches(body, @"\bPmi\s*\{"))
        {
            var start = bodyStart + header.Index;
            var end = End(mask, start + header.Length - 1);
            if (end < 0) { Error("pmi-malformed", "Unclosed Pmi block."); continue; }
            if (Depth(body, header.Index) != 0) Error("pmi-scope-invalid", "Assembly Pmi belongs to the root Assembly.");
            var cursor = start + header.Length;
            while (cursor < end)
            {
                while (cursor < end && char.IsWhiteSpace(mask[cursor])) cursor++;
                if (cursor == end) break;
                var note = Regex.Match(mask[cursor..end], @"^Note\s+(?<name>[A-Za-z_]\w*)\s*\{");
                if (!note.Success) { Error("pmi-kind-unsupported", "Assembly Pmi currently admits only Note Name { Target: occurrence; Text: string; }."); break; }
                var close = End(mask, cursor + note.Length - 1);
                if (close < 0 || close >= end) { Error("pmi-malformed", note.Value); break; }
                var fields = original[(cursor + note.Length)..close];
                var parsed = Regex.Match(fields, "^\\s*Target\\s*:\\s*(?<target>[A-Za-z_]\\w*(?:\\.[A-Za-z_]\\w*)*)\\s*;\\s*Text\\s*:\\s*(?<text>\"(?:\\\\.|[^\"\\\\])*\")\\s*;\\s*$", RegexOptions.Singleline);
                if (!parsed.Success) Error("pmi-note-invalid", note.Groups["name"].Value);
                else
                {
                    try
                    {
                        using var literal = JsonDocument.Parse(parsed.Groups["text"].Value);
                        var text = literal.RootElement.GetString()!;
                        var name = note.Groups["name"].Value;
                        if (string.IsNullOrWhiteSpace(text) || text.Any(char.IsControl)) Error("pmi-note-invalid", name);
                        else if (notes.Any(n => n.Name == name)) Error("pmi-note-duplicate", name);
                        else notes.Add(new(name, parsed.Groups["target"].Value, text, new(identity, cursor, close - cursor + 1)));
                    }
                    catch (JsonException) { Error("pmi-note-invalid", note.Groups["name"].Value); }
                }
                cursor = close + 1;
            }
            changes.Add((start, end - start + 1));
        }
        var chars = source.ToCharArray();
        foreach (var change in changes) Array.Fill(chars, ' ', change.Start, change.Length);
        source = new(chars);
        return new(release, notes);

        void Error(string code, string message) => diagnostics.Add(new("assembly-" + code, message));
    }

    internal static void Validate(AssemblyAnnotations? annotations, IEnumerable<AssemblyInstanceIr> instances, List<AssemblyDiagnostic> diagnostics)
    {
        var paths = instances.Select(i => i.Path.ToString()).ToHashSet(StringComparer.Ordinal);
        foreach (var note in annotations?.Notes ?? [])
            if (!paths.Contains(note.Target)) diagnostics.Add(new("assembly-pmi-target-unresolved", $"Note '{note.Name}' targets unknown occurrence '{note.Target}'."));
    }

    internal static Step242HeaderMetadata? Header(AssemblyReleaseRecord? release, string assemblyName)
        => release is null ? null : new(assemblyName + ".step", release.Fields["Description"] + " v" + release.Fields["Version"],
            release.Fields["Date"] + "T00:00:00", release.Fields["Author"], release.Fields.GetValueOrDefault("Organization", "Aetheris"),
            "Aetheris.Kernel.Firmament", "Aetheris/Firmament", "");

    private static string Unquote(string value) => value.StartsWith('"') ? value[1..^1] : value;
    private static string Mask(string source) => Regex.Replace(source, "\"(?:\\\\.|[^\"\\\\])*\"|//[^\\r\\n]*", m => new string(' ', m.Length));
    private static int Depth(string text, int end)
    { var depth = 0; for (var i = 0; i < end; i++) { if (text[i] == '{') depth++; else if (text[i] == '}') depth--; } return depth; }
    private static int End(string text, int opening)
    { var depth = 0; for (var i = opening; i < text.Length; i++) { if (text[i] == '{') depth++; else if (text[i] == '}' && --depth == 0) return i; } return -1; }
}
