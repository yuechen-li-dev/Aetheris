using System.Text.Json;
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
        var syntax = Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FirmamentSyntaxParser.Parse(source);
        var roots = syntax.Document.Statements.OfType<Aetheris.Kernel.Firmament.FirmamentV2.Syntax.DeclarationSyntax>()
            .Where(d => d.Type.Name.Text == "Assembly" && !d.Modifiers.Any(m => m.Name.Text == "Template")).ToArray();
        if (roots.Length != 1 || roots[0].Body is null) return new(null, []);
        var changes = new List<FirmamentV2SourceSpan>();
        var notes = new List<AssemblyPmiNote>();
        AssemblyReleaseRecord? release = null;
        var fields = roots[0].Body!.Statements.OfType<Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FieldSyntax>()
            .Where(f => f.Name.Text is "Provenance" or "provenance").ToArray();
        if (fields.Length > 1) Error("provenance-selection-invalid", "Select one release Record.", roots[0].Span);
        foreach (var selection in fields)
        {
            var errors = new List<string>();
            var value = Aetheris.Kernel.Firmament.Modules.FirmamentMetadata.Bind(original, selection.Value, errors);
            foreach (var error in errors) Error(error.StartsWith("firmament-metadata-record-unresolved:", StringComparison.Ordinal)
                ? "provenance-record-unresolved" : "provenance-record-invalid", error, selection.Span);
            if (value is null) continue;
            var projected = value.Fields.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            foreach (var (name, type) in new[] { ("Author", "String"), ("Date", "Date"), ("Version", "Version"), ("Description", "String") })
            {
                var preferred = char.ToLowerInvariant(name[0]) + name[1..];
                if (value.Fields.ContainsKey(name) && value.Fields.ContainsKey(preferred))
                { Error("provenance-field-invalid", "Duplicate metadata aliases: " + name + "/" + preferred, selection.Span); continue; }
                var key = value.Fields.ContainsKey(preferred) || value.Fields.ContainsKey(preferred + ".name") ? preferred : name;
                if (name == "Author" && value.FieldTypes.GetValueOrDefault(key) != "String" && value.Fields.ContainsKey(key + ".name")) key += ".name";
                if (value.FieldTypes.GetValueOrDefault(key) != type || string.IsNullOrWhiteSpace(value.Fields.GetValueOrDefault(key)))
                    Error("provenance-field-invalid", value.Name + "." + preferred + " must provide " + type + ".", selection.Span);
                else projected[name] = value.Fields[key];
            }
            if (value.Fields.ContainsKey("Organization") && value.Fields.ContainsKey("organization"))
                Error("provenance-field-invalid", "Duplicate metadata aliases: Organization/organization", selection.Span);
            var organizationKey = value.Fields.ContainsKey("organization") ? "organization" : "Organization";
            if (value.Fields.TryGetValue(organizationKey, out var organization))
            {
                if (value.FieldTypes.GetValueOrDefault(organizationKey) != "String") Error("provenance-field-invalid", "Release organization must provide String.", selection.Span);
                else projected["Organization"] = organization;
            }
            release = new(value.Name, value.Type, projected, new(identity, selection.Span.Start, selection.Span.Length));
            // Metadata is bound by the normal Record owner. Remove the selected
            // declaration/value by AST spans from the geometry-only projection.
            foreach (var d in syntax.Document.Statements.OfType<Aetheris.Kernel.Firmament.FirmamentV2.Syntax.DeclarationSyntax>()
                .Where(d => d.Name?.Text == value.Name && d.Type.Name.Text == "Static" || d.Name?.Text == value.Type && d.Type.Name.Text == "Record")) changes.Add(d.Span);
            changes.Add(selection.Span);
        }
        foreach (var pmi in roots[0].Body!.Statements.OfType<Aetheris.Kernel.Firmament.FirmamentV2.Syntax.DeclarationSyntax>().Where(d => d.Type.Name.Text == "Pmi"))
        {
            var scopeErrors = new List<string>();
            new Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FirmamentSyntaxFields(pmi, scopeErrors).Complete(
                (pmi.Body?.Statements.OfType<Aetheris.Kernel.Firmament.FirmamentV2.Syntax.DeclarationSyntax>() ?? []).ToHashSet<Aetheris.Kernel.Firmament.FirmamentV2.Syntax.StatementSyntax>());
            foreach (var error in scopeErrors) Error("pmi-note-invalid", error, pmi.Span);
            foreach (var statement in pmi.Body?.Statements ?? [])
            {
                if (statement is not Aetheris.Kernel.Firmament.FirmamentV2.Syntax.DeclarationSyntax { Type.Name.Text: "Note", Name: { } name, Body: { } body } note)
                { Error("pmi-kind-unsupported", "Assembly PMI currently admits Note declarations.", statement.Span); continue; }
                var errors = new List<string>();
                var owned = new Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FirmamentSyntaxFields(note, errors);
                var target = owned.Reference("Target");
                var text = owned.Take("Text") is Aetheris.Kernel.Firmament.FirmamentV2.Syntax.LiteralSyntax { Kind: Aetheris.Kernel.Firmament.FirmamentV2.Syntax.LiteralKind.String } literal
                    ? JsonSerializer.Deserialize<string>(literal.Raw) : null;
                owned.Complete();
                if (errors.Count > 0 || target is null || string.IsNullOrWhiteSpace(text) || text.Any(char.IsControl))
                    Error("pmi-note-invalid", string.Join(" | ", errors), note.Span);
                else if (notes.Any(n => n.Name == name.Text)) Error("pmi-note-duplicate", name.Text, note.Span);
                else notes.Add(new(name.Text, target, text, new(identity, note.Span.Start, note.Span.Length)));
            }
            changes.Add(pmi.Span);
        }
        var chars = source.ToCharArray();
        foreach (var span in changes) for (var i = span.Start; i < span.Start + span.Length; i++) if (chars[i] is not ('\r' or '\n')) chars[i] = ' ';
        source = new(chars);
        return new(release, notes);
        void Error(string code, string message, FirmamentV2SourceSpan span) => diagnostics.Add(new("assembly-" + code, message) { SourceSpan = new(identity, span.Start, span.Length) });
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

}
