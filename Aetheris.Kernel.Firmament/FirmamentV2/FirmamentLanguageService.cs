using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentLanguageCompletion(
    string Document, string Revision, string Context, int ReplaceStart, int ReplaceLength,
    IReadOnlyList<FirmamentAuthoringField> Fields,
    IReadOnlyList<string> MissingRequiredFields,
    IReadOnlyList<FirmamentLanguageEntry>? Entries = null,
    IReadOnlyList<string>? Values = null,
    IReadOnlyList<FirmamentLanguageSymbol>? Symbols = null);
public sealed record FirmamentLanguageEntry(string ConstructId, string Name, string Context, string Source)
{
    public string? InsertText { get; init; }
    public string? Snippet { get; init; }
}
public sealed record FirmamentLanguageSymbol(string Name, string Kind, string Detail, int Start, int Length);

/// <summary>Authoring help from the shared syntax tree and construct owners; no BRep or export work.</summary>
public static class FirmamentLanguageService
{
    internal sealed record Owner(string Name, string? Variant, BlockSyntax Body, DeclarationSyntax? Declaration)
    {
        public string SchemaName => Name == "Interface" && Variant is not null ? $"Interface<{Variant}>" : Name;
        public string Scope => Name == "Struct" && Declaration?.Modifiers.Any(m => m.Name.Text == "Concept") == true ? "Concept Struct" : Name;
    }
    private sealed record EntryTemplate(FirmamentConstructSchema Schema, string Source, DocumentSyntax Syntax);
    private static readonly EntryTemplate[] Entries = FirmamentSemanticSchemas.All.Where(s => s.Entry is not null)
        .Select(s => { var text = FirmamentSourceSpelling.Prefer(s.Entry!); return new EntryTemplate(s, text, FirmamentSyntaxParser.Parse(text).Document); }).ToArray();

    public static FirmamentLanguageCompletion Complete(string source, string document, string revision, int offset)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (offset < 0 || offset > source.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        source = FirmamentSourceSpelling.Normalize(source);
        var start = offset;
        while (start > 0 && IdentifierPart(source[start - 1])) start--;
        var end = offset;
        while (end < source.Length && IdentifierPart(source[end])) end++;
        var prefix = source[start..offset];
        FirmamentLanguageCompletion Empty(string context) => new(document, revision, context, start, end - start, [], []);
        // The compiler lexer owns strings/comments, including unfinished ones while typing.
        if (FirmamentLanguageAnalysisService.Lex(source).Any(t => offset > t.Start &&
            (offset < t.Start + t.Length || offset == t.Start + t.Length &&
                (t.Shape == "comment" && t.Text.StartsWith("//", StringComparison.Ordinal) || t.Error is not null)) &&
            t.Shape is "comment" or "string")) return Empty("Literal");

        var syntax = FirmamentSyntaxParser.Parse(source).Document;
        var owner = OwnerAt(syntax, offset);
        var context = owner?.SchemaName ?? "Document";
        if (FirmamentSyntaxTraversal.Declarations(syntax).Any(d => d.Name is { } name &&
            start >= name.Span.Start && offset <= name.Span.Start + name.Span.Length &&
            (owner is null || d.Span.Start > owner.Body.Span.Start))) return Empty("DeclarationName");
        var fields = FieldsFor(owner, source, offset);
        var directFields = owner?.Body.Statements.OfType<FieldSyntax>().ToArray() ?? [];
        var activeField = directFields.LastOrDefault(f => f.Name.Span.Start + f.Name.Span.Length < start &&
            f.Value.Span.Start <= offset && offset <= f.Value.Span.Start + f.Value.Span.Length);
        // A missing expression can be omitted by parser recovery. Its two
        // structural tokens still identify the value slot without inventing an AST.
        var preceding = syntax.Tokens.Where(t => t.Kind is not (SyntaxTokenKind.Comment or SyntaxTokenKind.Whitespace or SyntaxTokenKind.End)
            && t.Span.Start < start && t.Span.Start >= (owner?.Body.ContentSpan.Start ?? 0)).TakeLast(2).ToArray();
        var activeName = activeField?.Name.Text ?? (preceding.Length == 2 && preceding[0].Kind == SyntaxTokenKind.Identifier && preceding[1].Text == ":" ? preceding[0].Text : null);
        if (activeName is not null)
        {
            var field = fields.FirstOrDefault(f => FirmamentSyntaxFields.SameField(activeName, f.Name));
            return new(document, revision, "Value", start, end - start, [], [], Values:
                field?.Choices?.Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray(),
                Symbols: VisibleSymbols(source, syntax, owner, offset, prefix, values: true));
        }
        // Fields after the caret still count, except the field currently being edited.
        var present = directFields.Where(f => !(f.Name.Span.Start <= offset && offset <= f.Name.Span.Start + f.Name.Span.Length))
            .Select(f => f.Name.Text).ToHashSet(StringComparer.Ordinal);
        var candidates = fields.Where(f => !present.Contains(f.Name) && f.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
        var missing = fields.Where(f => f.Required && !present.Contains(f.Name)).Select(f => f.Name).ToList();
        if (context == "Interface<Fixed>" && present.Contains("Datum"))
        {
            candidates = candidates.Where(f => f.Name is not ("A" or "B")).ToArray();
            missing.RemoveAll(name => name is "A" or "B");
            if (!present.Contains("Members")) missing.Add("Members");
        }
        if (context == "Helix" && !present.Contains("Pitch") && !present.Contains("Height")) missing.Add("Pitch or Height");
        var entries = Entries.Where(e => e.Schema.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && LegalScope(e, owner))
            .Select(e => EntryFor(e, owner)).ToArray();
        return new(document, revision, owner?.Name is "Model" or "Struct" && candidates.Length == 0 && entries.Length > 0 ? "ConstructEntry" : context, start, end - start, candidates, missing, entries,
            Symbols: VisibleSymbols(source, syntax, owner, offset, prefix, values: false));
    }

    private static bool IdentifierPart(char value) => char.IsLetterOrDigit(value) || value == '_';
    private static bool Contains(BlockSyntax body, int offset) => body.ContentSpan.Start <= offset &&
        offset <= body.ContentSpan.Start + body.ContentSpan.Length;

    internal static Owner? OwnerAt(DocumentSyntax syntax, int offset)
    {
        var declarations = FirmamentSyntaxTraversal.Declarations(syntax).Where(d => d.Body is not null && Contains(d.Body, offset))
            .Select(d => new Owner(d.Type.Name.Text, d.Type.Arguments.FirstOrDefault() is { } a ? FirmamentSyntaxValues.Reference(a.Value) : null, d.Body!, d));
        var objects = FirmamentSyntaxTraversal.Expressions(syntax).OfType<ObjectSyntax>().Where(o => o.Type is not null && Contains(o.Body, offset))
            .Select(o => new Owner(FirmamentSyntaxValues.Reference(o.Type is SpecializationSyntax s ? s.Target : o.Type) ?? "",
                o.Type is SpecializationSyntax generic && generic.Arguments.FirstOrDefault() is { } a ? FirmamentSyntaxValues.Reference(a.Value) : null, o.Body, null));
        return declarations.Concat(objects).OrderByDescending(o => o.Body.Span.Start).FirstOrDefault();
    }

    internal static FirmamentAuthoringField? FieldAt(string source, int offset, string name)
    {
        var syntax = FirmamentSyntaxParser.Parse(source).Document;
        return FieldsFor(OwnerAt(syntax, offset), source, offset).FirstOrDefault(f => FirmamentSyntaxFields.SameField(name, f.Name));
    }

    private static IReadOnlyList<FirmamentAuthoringField> FieldsFor(Owner? owner, string source, int offset)
    {
        if (owner is null) return [];
        if (owner.Name == "Loft" && LoftAuthoringParser.TryGetAuthoringFields(source, offset, out var loft)) return loft;
        if (owner.Name == "Points" && owner.Body.Statements.OfType<DeclarationSyntax>().Any(d =>
            d.Type.Name.Text == "Series" || d.Body?.Statements.OfType<DeclarationSyntax>().Any(c => c.Type.Name.Text == "Stations") == true)) return [];
        if (owner.Name == "Struct") return owner.Scope == "Concept Struct" ? [] : FirmamentSchemaAuthoringFields.For("Model").Select(f => f with { Required = false }).ToArray();
        return FirmamentSchemaAuthoringFields.For(owner.SchemaName, owner.Variant);
    }

    private static bool LegalScope(EntryTemplate entry, Owner? owner)
    {
        var context = entry.Schema.Context ?? "";
        var scope = owner?.Scope ?? "Document";
        var modelTemplate = entry.Syntax.Statements.OfType<DeclarationSyntax>().Any(d => d.Type.Name.Text == "Model");
        return scope switch
        {
            "Document" => context is "Document" or "Declaration" || modelTemplate,
            "Model" or "Struct" => context.StartsWith("Model", StringComparison.Ordinal) || modelTemplate && entry.Schema.Name != "Model",
            "Modify" => context.StartsWith("Modify", StringComparison.Ordinal),
            "Assembly" or "Subassembly" => context == "Assembly",
            "Concept Struct" => context == "Concept Struct" || context.Contains("layout", StringComparison.Ordinal),
            _ => Regex.IsMatch(context, $@"\b{Regex.Escape(scope)}\b", RegexOptions.CultureInvariant)
        };
    }

    private static FirmamentLanguageEntry EntryFor(EntryTemplate entry, Owner? owner)
    {
        var text = entry.Source;
        var declarations = FirmamentSyntaxTraversal.Declarations(entry.Syntax).ToArray();
        var declaration = declarations.FirstOrDefault(d => d.Type.Name.Text == entry.Schema.Name.Split('<')[0]);
        if (owner is not null && declaration is not null)
        {
            // An entry may be a complete document (Loft/Helix) or have a Modify
            // wrapper (Hole). Insert the declaration appropriate to the actual owner.
            if (owner.Name is "Model" or "Struct" && entry.Syntax.Statements.OfType<DeclarationSyntax>().FirstOrDefault(d => d.Type.Name.Text == "Model") is { } root)
                declaration = root.Body?.Statements.OfType<DeclarationSyntax>().FirstOrDefault(d =>
                    d.Span.Start <= declaration.Span.Start && d.Span.Start + d.Span.Length >= declaration.Span.Start + declaration.Span.Length) ?? declaration;
            text = text.Substring(declaration.Span.Start, declaration.Span.Length);
            var indentStart = entry.Source.LastIndexOf('\n', Math.Max(0, declaration.Span.Start - 1)) + 1;
            var indent = entry.Source[indentStart..declaration.Span.Start];
            if (indent.All(char.IsWhiteSpace) && indent.Length > 0)
                text = string.Join('\n', text.Split('\n').Select((line, i) => i > 0 && line.StartsWith(indent, StringComparison.Ordinal) ? line[indent.Length..] : line));
        }
        var parsed = FirmamentSyntaxParser.Parse(text).Document;
        var roots = parsed.Statements.OfType<DeclarationSyntax>().Where(d => d.Name is not null).ToArray();
        var name = (roots.FirstOrDefault(d => d.Type.Name.Text == entry.Schema.Name)
            ?? roots.FirstOrDefault(d => d.Type.Name.Text is "Model" or "Assembly" or "Scene" or "Garment")
            ?? roots.FirstOrDefault())?.Name;
        static string Escape(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("$", "\\$", StringComparison.Ordinal).Replace("}", "\\}", StringComparison.Ordinal);
        var snippet = name is null ? Escape(text) + "$0" : Escape(text[..name.Span.Start]) + "${1:" + Escape(name.Text) + "}" + Escape(text[(name.Span.Start + name.Span.Length)..]) + "$0";
        return new(entry.Schema.Id.Value, entry.Schema.Name, entry.Schema.Context ?? "", entry.Source) { InsertText = text, Snippet = snippet };
    }

    private static IReadOnlyList<FirmamentLanguageSymbol> VisibleSymbols(string source, DocumentSyntax syntax, Owner? owner, int offset, string prefix, bool values)
    {
        var declarations = FirmamentSyntaxTraversal.Declarations(syntax).ToArray();
        var result = new List<FirmamentLanguageSymbol>();
        foreach (var d in declarations.Where(d => d.Name is not null && d.Name.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            if (!values && d.Type.Name.Text is not ("Feature" or "Function")) continue;
            var scope = declarations.Where(p => p != d && p.Body is not null && p.Body.Span.Start < d.Span.Start &&
                p.Body.Span.Start + p.Body.Span.Length >= d.Span.Start + d.Span.Length).OrderByDescending(p => p.Body!.Span.Start).FirstOrDefault();
            if (scope?.Body is not null && !Contains(scope.Body, offset) || d == owner?.Declaration) continue;
            var kind = d.Type.Name.Text is "Feature" or "Function" ? "function" : d.Type.Name.Text is "Record" or "Enum" or "Options" ? "type" : "reference";
            result.Add(new(d.Name!.Text, kind, d.Type.Name.Text + (d.Annotation is null ? "" : ": " + source.Substring(d.Annotation.Span.Start, d.Annotation.Span.Length)), d.Name.Span.Start, d.Name.Span.Length));
        }
        if (values)
            foreach (var parameter in declarations.Where(d => d.Body is not null && Contains(d.Body, offset)).SelectMany(d => d.Parameters).Where(p => p.Name is not null && p.Name.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                result.Add(new(parameter.Name!.Text, "variable", "parameter", parameter.Name.Span.Start, parameter.Name.Span.Length));
        // A completion must not choose arbitrarily between same-named declarations.
        return result.GroupBy(s => s.Name, StringComparer.Ordinal).Where(g => g.Count() == 1).Select(g => g.Single()).ToArray();
    }
}
