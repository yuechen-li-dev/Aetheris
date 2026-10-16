using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>One structural edit, addressed by outline node ids (<see cref="FirmamentLanguageOutlineService"/>).
/// <c>Kind</c> is <c>setField</c>, <c>removeField</c>, <c>insertDeclaration</c>, <c>removeDeclaration</c>,
/// <c>renameDeclaration</c> or <c>moveDeclaration</c>.</summary>
public sealed record FirmamentLanguageEditOperation(string Kind)
{
    /// <summary>The declaration edited, removed, renamed or moved.</summary>
    public string? Node { get; init; }
    /// <summary>insertDeclaration: the declaration whose body receives the new one; null for the document.</summary>
    public string? Parent { get; init; }
    /// <summary>insertDeclaration and moveDeclaration: the sibling to follow. Absent means the end, unless <see cref="First"/>.</summary>
    public string? After { get; init; }
    /// <summary>insertDeclaration and moveDeclaration: place before the first sibling declaration.</summary>
    public bool First { get; init; }
    public string? Field { get; init; }
    public string? Value { get; init; }
    /// <summary>renameDeclaration: the new name.</summary>
    public string? Name { get; init; }
    public FirmamentDeclarationSpec? Declaration { get; init; }
}

/// <summary>A declaration to write: <c>Modifiers Kind&lt;Variant&gt; Name { Field: Value ... }</c>.</summary>
public sealed record FirmamentDeclarationSpec(string Kind, string? Variant = null, IReadOnlyList<string>? Modifiers = null,
    string? Name = null, IReadOnlyList<FirmamentFieldSpec>? Fields = null);

public sealed record FirmamentFieldSpec(string Name, string Value);

/// <summary>A single replacement of <c>[Start, Start + Length)</c> in the original source.</summary>
public sealed record FirmamentTextEdit(int Start, int Length, string Replacement);

/// <summary>The edited source, the one replacement that turns the original into it, the outline ids
/// of the declarations each operation produced, in order, and what the edit could not keep.</summary>
public sealed record FirmamentLanguageEdit(string Document, string Revision, string Text, bool Changed,
    FirmamentTextEdit Edit, IReadOnlyList<string> Nodes, IReadOnlyList<string> Warnings);

public sealed class FirmamentLanguageEditException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// Structural source edits for visual editors. An edit is a splice placed by the syntax tree
/// (the body brace, the last field, a sibling's end, the indentation already in use), so comments,
/// layout and spelling outside the edited statement are kept byte for byte. Every operation is
/// checked by parsing its result: an edit that adds a syntax error is refused. Parsing is the only
/// gate here; whether the edited model builds is the compiler's decision.
/// </summary>
public static class FirmamentLanguageEditService
{
    private static readonly Regex Identifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    public static FirmamentLanguageEdit Apply(string source, string document, string revision,
        IReadOnlyList<FirmamentLanguageEditOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Count == 0) throw new FirmamentLanguageEditException("edit-empty", "No edit operations were given.");
        var text = source;
        var nodes = new List<string>();
        var warnings = new List<string>();
        foreach (var operation in operations)
        {
            var before = Parse(text);
            var (next, node) = operation.Kind switch
            {
                "setField" => SetField(text, before, operation),
                "removeField" => RemoveField(text, before, operation),
                "insertDeclaration" => InsertDeclaration(text, before, operation),
                "removeDeclaration" => RemoveDeclaration(text, before, operation, document, warnings),
                "renameDeclaration" => RenameDeclaration(text, before, operation, document, warnings),
                "moveDeclaration" => MoveDeclaration(text, before, operation),
                _ => throw new FirmamentLanguageEditException("edit-unknown-operation", $"Unknown edit operation '{operation.Kind}'.")
            };
            var after = Parse(next);
            var introduced = Errors(after).Except(Errors(before)).FirstOrDefault();
            if (introduced is not null)
                throw new FirmamentLanguageEditException("edit-unparseable", $"{operation.Kind} was refused because the result does not parse: {introduced}");
            text = next;
            if (node is not null) nodes.Add(node);
        }
        return new(document, revision, text, text != source, Diff(source, text), nodes, warnings);
    }

    // ---- Operations -------------------------------------------------------------------------

    private static (string, string?) SetField(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        var (id, declaration) = Find(parse, op.Node);
        var name = RequireIdentifier(op.Field, "field name");
        var value = RequireValue(op.Value, name);
        var matches = Fields(declaration).Where(f => f.Name.Text == name).ToArray();
        if (matches.Length > 1)
            throw new FirmamentLanguageEditException("edit-duplicate-field", $"{name} is written {matches.Length} times on {id}; edit it in source.");
        if (matches.Length == 1)
            return (Replace(text, matches[0].Value.Span.Start, matches[0].Value.Span.Length, value), id);
        return (InsertStatement(text, declaration, $"{name}: {value}", afterFields: true), id);
    }

    private static (string, string?) RemoveField(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        var (id, declaration) = Find(parse, op.Node);
        var name = RequireIdentifier(op.Field, "field name");
        var matches = Fields(declaration).Where(f => f.Name.Text == name).ToArray();
        if (matches.Length == 0) throw new FirmamentLanguageEditException("edit-field-not-found", $"{id} has no {name} field.");
        if (matches.Length > 1)
            throw new FirmamentLanguageEditException("edit-duplicate-field", $"{name} is written {matches.Length} times on {id}; edit it in source.");
        return (RemoveStatement(text, matches[0].Span), id);
    }

    private static (string, string?) InsertDeclaration(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        var spec = op.Declaration ?? throw new FirmamentLanguageEditException("edit-input-required", "insertDeclaration requires a declaration.");
        var declarations = FirmamentLanguageOutlineService.Declarations(parse);
        DeclarationSyntax? parent = null;
        if (op.Parent is not null) parent = Find(parse, op.Parent).Syntax;
        var siblings = Siblings(declarations, op.Parent);
        var indent = parent is null ? "" : ChildIndent(text, parent);
        var snippet = Write(spec, indent, Unit(text));
        CheckSnippet(snippet);
        var anchor = Anchor(declarations, siblings, op);
        int at;
        string inserted;
        if (parent is null)
        {
            if (anchor.After is { } after) { at = StatementEnd(text, after.Span); inserted = "\n" + snippet; }
            else if (anchor.Before is { } first) { at = LineStart(text, first.Span.Start); inserted = snippet + "\n"; }
            else { at = text.TrimEnd().Length; inserted = (at == 0 ? "" : "\n") + snippet; }
        }
        else
        {
            var (position, insertion) = Placement(text, parent, snippet, anchor.After, anchor.Before);
            at = position; inserted = insertion;
        }
        var next = Replace(text, at, 0, inserted);
        var start = at + inserted.IndexOf(snippet.TrimStart(), StringComparison.Ordinal);
        var created = FirmamentLanguageOutlineService.Declarations(Parse(next)).FirstOrDefault(d => d.Syntax.Span.Start == start);
        return (next, created.Id);
    }

    private static (string, string?) RemoveDeclaration(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op,
        string document, List<string> warnings)
    {
        var (id, declaration) = Find(parse, op.Node);
        var outline = FirmamentLanguageOutlineService.Outline(text, document, "edit");
        var inside = (string node) => node == id || node.StartsWith(id + "/", StringComparison.Ordinal);
        var broken = outline.Edges.Where(e => e.ToDocument is null && inside(e.To) && !inside(e.From))
            .Select(e => e.From).Distinct().ToArray();
        if (broken.Length > 0)
            warnings.Add($"{declaration.Name?.Text ?? id} is still used by {string.Join(", ", broken)}; those uses no longer resolve.");
        return (RemoveStatement(text, declaration.Span), null);
    }

    private static (string, string?) RenameDeclaration(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op,
        string document, List<string> warnings)
    {
        var (id, declaration) = Find(parse, op.Node);
        var name = RequireIdentifier(op.Name, "name");
        if (declaration.Name is not { } current)
            throw new FirmamentLanguageEditException("edit-unnamed", $"{id} has no name to change.");
        if (current.Text == name) return (text, id);
        var declarations = FirmamentLanguageOutlineService.Declarations(parse);
        var parentId = declarations.First(d => d.Id == id).ParentId;
        if (Siblings(declarations, parentId).Any(s => s.Syntax.Name?.Text == name && s.Syntax.Type.Name.Text == declaration.Type.Name.Text))
            throw new FirmamentLanguageEditException("edit-name-taken", $"Another {declaration.Type.Name.Text} here is already called {name}.");

        var edits = new List<(int Start, int Length)> { (current.Span.Start, current.Span.Length) };
        // `Modify X` names the body it changes: renaming it retargets it, and nothing refers to it.
        if (!FirmamentLanguageOutlineService.NameIsUse(declaration))
        {
            var outline = FirmamentLanguageOutlineService.Outline(text, document, "edit");
            var skipped = 0;
            // Direct uses, and member chains that pass through it (`Stock.Outer` resolves to Stock's Outer).
            foreach (var use in outline.Edges.Where(e => e.ToDocument is null && (e.To == id || e.To.StartsWith(id + "/", StringComparison.Ordinal))))
            {
                var direct = use.To == id;
                var at = direct ? NameIn(text, use.Span.Start, use.Span.Length, current.Text)
                    : ChainThrough(text, use, id) ? HeadIn(text, use.Span.Start, current.Text) : null;
                if (at is null) { if (direct) skipped++; continue; }
                if (!edits.Any(e => e.Start == at)) edits.Add((at.Value, current.Text.Length));
                if (use.Ambiguous) warnings.Add($"{use.From} names {current.Text} ambiguously; check that it still means this declaration.");
            }
            if (skipped > 0) warnings.Add($"{skipped} use(s) of {current.Text} could not be located and still use the old name.");
            warnings.Add("Uses in other project files are not renamed.");
        }
        var next = text;
        foreach (var (start, length) in edits.OrderByDescending(e => e.Start)) next = Replace(next, start, length, name);
        var renamed = FirmamentLanguageOutlineService.Declarations(Parse(next))
            .FirstOrDefault(d => d.Syntax.Span.Start == declaration.Span.Start && d.Syntax.Name?.Text == name);
        return (next, renamed.Id);
    }

    private static (string, string?) MoveDeclaration(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        var (id, declaration) = Find(parse, op.Node);
        var declarations = FirmamentLanguageOutlineService.Declarations(parse);
        var parentId = declarations.First(d => d.Id == id).ParentId;
        if (op.After == id) return (text, id);
        if (op.After is not null && declarations.FirstOrDefault(d => d.Id == op.After).ParentId != parentId)
            throw new FirmamentLanguageEditException("edit-move-scope", "A declaration moves among its siblings; use remove and insert to move it elsewhere.");
        var body = text[declaration.Span.Start..StatementEnd(text, declaration.Span)];
        // Resolve the anchor and parent before removing: ids can change once a sibling that shared
        // their name is gone (`Modify:Base` becomes `Base`), positions only shift.
        var anchorBefore = Anchor(declarations, Siblings(declarations, parentId).Where(s => s.Id != id).ToArray(), op);
        var parentBefore = parentId is null ? null : declarations.First(d => d.Id == parentId).Syntax;
        var removed = RemoveStatement(text, declaration.Span);
        var shift = text.Length - removed.Length;
        var declarations2 = FirmamentLanguageOutlineService.Declarations(Parse(removed));
        DeclarationSyntax? Relocate(DeclarationSyntax? before)
        {
            if (before is null) return null;
            var start = before.Span.Start > declaration.Span.Start ? before.Span.Start - shift : before.Span.Start;
            return declarations2.First(d => d.Syntax.Span.Start == start && d.Syntax.Type.Name.Text == before.Type.Name.Text).Syntax;
        }
        var anchor = new AnchorPoint(Relocate(anchorBefore.After), Relocate(anchorBefore.Before));
        DeclarationSyntax? parent = Relocate(parentBefore);
        int at; string inserted;
        if (parent is null)
        {
            var indent = "";
            if (anchor.After is { } after) { at = StatementEnd(removed, after.Span); inserted = "\n" + indent + body; }
            else if (anchor.Before is { } first) { at = LineStart(removed, first.Span.Start); inserted = body + "\n"; }
            else { at = removed.TrimEnd().Length; inserted = "\n" + body; }
        }
        else
        {
            var snippet = ChildIndent(removed, parent) + body;
            (at, inserted) = Placement(removed, parent, snippet, anchor.After, anchor.Before);
        }
        var next = Replace(removed, at, 0, inserted);
        var start = at + inserted.IndexOf(body, StringComparison.Ordinal);
        var moved = FirmamentLanguageOutlineService.Declarations(Parse(next)).FirstOrDefault(d => d.Syntax.Span.Start == start);
        return (next, moved.Id);
    }

    // ---- Placement --------------------------------------------------------------------------

    private readonly record struct AnchorPoint(DeclarationSyntax? After, DeclarationSyntax? Before);

    private static AnchorPoint Anchor(IReadOnlyList<(string Id, string? ParentId, DeclarationSyntax Syntax)> declarations,
        IReadOnlyList<(string Id, string? ParentId, DeclarationSyntax Syntax)> siblings, FirmamentLanguageEditOperation op)
    {
        if (op.After is not null)
        {
            var after = siblings.FirstOrDefault(s => s.Id == op.After);
            if (after.Syntax is null)
                throw new FirmamentLanguageEditException("edit-anchor-not-found", $"{op.After} is not a sibling at this place.");
            return new(after.Syntax, null);
        }
        if (op.First) return siblings.Count == 0 ? new(null, null) : new(null, siblings[0].Syntax);
        return siblings.Count == 0 ? new(null, null) : new(siblings[^1].Syntax, null);
    }

    /// <summary>Where a declaration goes inside a parent body, and the text to insert there.</summary>
    private static (int At, string Inserted) Placement(string text, DeclarationSyntax parent, string snippet,
        DeclarationSyntax? after, DeclarationSyntax? before)
    {
        if (after is not null) return Follow(text, parent, after.Span, snippet);
        if (before is not null)
        {
            var multiline = Multiline(text, parent);
            return multiline ? (LineStart(text, before.Span.Start), snippet + "\n") : (before.Span.Start, snippet.TrimStart() + " ");
        }
        if (BodyOf(parent) is null) throw new FirmamentLanguageEditException("edit-no-body",
            "This declaration has no body to hold declarations; add a field first or edit it in source.");
        var last = BodyOf(parent).Statements.LastOrDefault();
        return last is not null ? Follow(text, parent, last.Span, snippet) : IntoEmpty(text, BodyOf(parent), parent, snippet);
    }

    private static string InsertStatement(string text, DeclarationSyntax declaration, string statement, bool afterFields)
    {
        if (BodyOf(declaration) is null)
        {
            if (declaration.Initializer is not null)
                throw new FirmamentLanguageEditException("edit-no-body", "This declaration is a value, not a body; edit it in source.");
            var (at, inserted) = OpenBody(text, declaration, statement);
            return Replace(WithoutTerminator(text, declaration), at, 0, inserted);
        }
        var statements = BodyOf(declaration).Statements;
        var lastField = afterFields ? statements.LastOrDefault(s => s is FieldSyntax) : statements.LastOrDefault();
        var indent = ChildIndent(text, declaration);
        if (lastField is not null) { var (at, inserted) = Follow(text, declaration, lastField.Span, indent + statement); return Replace(text, at, 0, inserted); }
        if (statements.Count > 0)
        {
            // Fields lead the body: go before the first declaration.
            var first = statements[0];
            return Multiline(text, declaration)
                ? Replace(text, LineStart(text, first.Span.Start), 0, indent + statement + "\n")
                : Replace(text, first.Span.Start, 0, statement + " ");
        }
        var (position, insertion) = IntoEmpty(text, BodyOf(declaration), declaration, indent + statement);
        return Replace(text, position, 0, insertion);
    }

    private static (int, string) Follow(string text, DeclarationSyntax parent, FirmamentV2SourceSpan previous, string snippet)
    {
        if (Multiline(text, parent)) return (StatementEnd(text, previous), "\n" + snippet);
        var end = previous.Start + previous.Length;
        if (end < text.Length && text[end] == ';') end++;
        return (end, " " + snippet.TrimStart());
    }

    private static (int, string) IntoEmpty(string text, BlockSyntax body, DeclarationSyntax parent, string snippet)
    {
        if (!body.Delimited) return (body.Span.Start, snippet.TrimStart() + "\n");
        var open = body.Span.Start + 1;
        var content = text.Substring(open, Math.Max(0, body.ContentSpan.Length));
        if (content.Contains('\n')) return (open, "\n" + snippet);
        // `{}` or `{ }` on one line: keep it on one line.
        var padded = " " + snippet.TrimStart() + (content.Length > 0 ? "" : " ");
        return (open, padded);
    }

    private static (int, string) OpenBody(string text, DeclarationSyntax declaration, string statement)
    {
        var end = declaration.Span.Start + declaration.Span.Length;
        // `Box Plate;` becomes `Box Plate { Size: ... }`.
        if (end > 0 && text[end - 1] == ';') return (end - 1, " { " + statement + " }");
        return (end, " { " + statement + " }");
    }

    /// <summary>`Box A;`: the `;` ends a bodyless declaration and goes when a body is opened.</summary>
    private static string WithoutTerminator(string text, DeclarationSyntax declaration)
    {
        var end = declaration.Span.Start + declaration.Span.Length;
        return BodyOf(declaration) is null && end < text.Length && text[end] == ';' ? text.Remove(end, 1) : text;
    }

    /// <summary>A statement's extent for removal and moving: its span, a following `;`, and a
    /// `//` comment that trails it on its own line.</summary>
    private static int StatementEnd(string text, FirmamentV2SourceSpan span)
    {
        var end = span.Start + span.Length;
        if (end < text.Length && text[end] == ';') end++;
        var lineEnd = text.IndexOf('\n', end);
        if (lineEnd < 0) lineEnd = text.Length;
        var rest = text[end..lineEnd];
        if (rest.TrimStart().StartsWith("//", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(text[LineStart(text, span.Start)..span.Start]))
            return lineEnd - (rest.Length - rest.TrimEnd().Length);
        return end;
    }

    private static bool Multiline(string text, DeclarationSyntax declaration) =>
        BodyOf(declaration) is { } body && (!body.Delimited || text.Substring(body.Span.Start, body.Span.Length).Contains('\n'));

    private static string ChildIndent(string text, DeclarationSyntax declaration)
    {
        var first = BodyOf(declaration)?.Statements.FirstOrDefault();
        if (first is not null && Multiline(text, declaration) && LineStart(text, first.Span.Start) > declaration.Span.Start)
            return Indent(text, first.Span.Start);
        return Indent(text, declaration.Span.Start) + Unit(text);
    }

    private static string Indent(string text, int position)
    {
        var start = LineStart(text, position);
        var end = start;
        while (end < text.Length && text[end] is ' ' or '\t') end++;
        return text[start..end];
    }

    /// <summary>The indentation step the document already uses; two spaces when it has none.</summary>
    private static string Unit(string text)
    {
        var widths = text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0)
            .Select(l => l.Length - l.TrimStart(' ').Length).Where(w => w > 0).ToArray();
        if (text.Split('\n').Any(l => l.StartsWith('\t'))) return "\t";
        return new string(' ', widths.Length == 0 ? 2 : widths.Min());
    }

    private static int LineStart(string text, int position) => position <= 0 ? 0 : text.LastIndexOf('\n', position - 1) + 1;

    /// <summary>Remove a statement: its whole line when it stands alone on it, otherwise just the
    /// statement and the space that separated it.</summary>
    private static string RemoveStatement(string text, FirmamentV2SourceSpan span)
    {
        var start = span.Start;
        var end = StatementEnd(text, span);
        var lineStart = LineStart(text, start);
        var lineEnd = text.IndexOf('\n', end);
        if (lineEnd < 0) lineEnd = text.Length;
        if (string.IsNullOrWhiteSpace(text[lineStart..start]) && string.IsNullOrWhiteSpace(text[end..lineEnd]))
            return text.Remove(lineStart, Math.Min(text.Length, lineEnd + 1) - lineStart);
        while (end < text.Length && text[end] is ' ' or '\t') end++;
        if (end < text.Length && text[end] == '}' && start > 0 && text[start - 1] == ' ')
        {
            // `{ A: 1 B: 2 }` removing B: drop the space before it and keep the one before `}`.
            var back = start;
            while (back > 0 && text[back - 1] == ' ') back--;
            return text.Remove(back, start - back + (end - start)).Insert(back, " ");
        }
        return text.Remove(start, end - start);
    }

    // ---- Writing and checking ---------------------------------------------------------------

    private static string Write(FirmamentDeclarationSpec spec, string indent, string unit)
    {
        var kind = RequireIdentifier(spec.Kind, "kind");
        var head = new StringBuilder();
        foreach (var modifier in spec.Modifiers ?? []) head.Append(RequireIdentifier(modifier, "modifier")).Append(' ');
        head.Append(kind);
        if (!string.IsNullOrWhiteSpace(spec.Variant))
        {
            if (spec.Variant.IndexOfAny(['{', '}', ';', '\n', '\r']) >= 0)
                throw new FirmamentLanguageEditException("edit-invalid-variant", $"'{spec.Variant}' is not a variant.");
            head.Append('<').Append(spec.Variant.Trim()).Append('>');
        }
        if (spec.Name is not null) head.Append(' ').Append(RequireIdentifier(spec.Name, "name"));
        var fields = (spec.Fields ?? []).Select(f => $"{RequireIdentifier(f.Name, "field name")}: {RequireValue(f.Value, f.Name)}").ToArray();
        var line = indent + head + (fields.Length == 0 ? " {}" : " { " + string.Join(" ", fields) + " }");
        if (line.Length <= 100) return line;
        return indent + head + " {\n" + string.Join("\n", fields.Select(f => indent + unit + f)) + "\n" + indent + "}";
    }

    private static void CheckSnippet(string snippet)
    {
        var parse = Parse(snippet);
        if (!parse.IsSuccess || parse.Document.Statements.Count != 1 || parse.Document.Statements[0] is not DeclarationSyntax)
            throw new FirmamentLanguageEditException("edit-invalid-declaration",
                $"The declaration does not parse on its own: {Errors(parse).FirstOrDefault() ?? "expected one declaration"}");
    }

    private static string RequireValue(string? value, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) throw new FirmamentLanguageEditException("edit-invalid-value", $"{field} needs a value.");
        // The value must be exactly one field value: parse it in place and compare the extent.
        var probe = $"Probe Probe {{ {field}: {trimmed} }}";
        var parse = Parse(probe);
        var declaration = parse.Document.Statements.OfType<DeclarationSyntax>().FirstOrDefault();
        var fields = declaration?.Body?.Statements ?? [];
        var prefix = probe.IndexOf(": ", StringComparison.Ordinal) + 2;
        if (!parse.IsSuccess || parse.Document.Statements.Count != 1 || fields.Count != 1 || fields[0] is not FieldSyntax f
            || f.Value.Span.Start != prefix || f.Value.Span.Length != trimmed.Length)
            throw new FirmamentLanguageEditException("edit-invalid-value", $"'{trimmed}' is not one value for {field}.");
        return trimmed;
    }

    private static string RequireIdentifier(string? value, string what)
    {
        if (value is null || !Identifier.IsMatch(value))
            throw new FirmamentLanguageEditException("edit-invalid-name", $"'{value}' is not a valid {what}: use letters, digits and _, starting with a letter.");
        return value;
    }

    // ---- Lookup -----------------------------------------------------------------------------

    private static SyntaxParseResult Parse(string text) => FirmamentSyntaxParser.Parse(text, "edit");

    private static IEnumerable<string> Errors(SyntaxParseResult parse) =>
        parse.Diagnostics.Where(d => d.Severity is "fatal" or "error").Select(d => d.Message);

    private static (string Id, DeclarationSyntax Syntax) Find(SyntaxParseResult parse, string? id)
    {
        if (id is null) throw new FirmamentLanguageEditException("edit-input-required", "The operation requires a node id.");
        var match = FirmamentLanguageOutlineService.Declarations(parse).FirstOrDefault(d => d.Id == id);
        return match.Syntax is null
            ? throw new FirmamentLanguageEditException("edit-node-not-found", $"No declaration has the id {id}. The source may have changed.")
            : (match.Id, match.Syntax);
    }

    private static IReadOnlyList<(string Id, string? ParentId, DeclarationSyntax Syntax)> Siblings(
        IReadOnlyList<(string Id, string? ParentId, DeclarationSyntax Syntax)> declarations, string? parentId) =>
        declarations.Where(d => d.ParentId == parentId).ToArray();

    /// <summary>The block that holds a declaration's statements: its body, or the object it is
    /// initialized with (`Static S: Policy = Policy { ... }`), the same block the outline reads.</summary>
    private static BlockSyntax? BodyOf(DeclarationSyntax declaration) =>
        declaration.Body ?? (declaration.Initializer as ObjectSyntax)?.Body;

    private static IEnumerable<FieldSyntax> Fields(DeclarationSyntax declaration) =>
        BodyOf(declaration)?.Statements.OfType<FieldSyntax>() ?? [];

    /// <summary>Where <paramref name="name"/> starts inside a use: at the use's start, or as one
    /// <c>.name</c> member of a chain. Null when it is not there exactly once.</summary>
    private static int? NameIn(string text, int start, int length, string name)
    {
        bool Boundary(int at) => at >= text.Length || !(char.IsLetterOrDigit(text[at]) || text[at] == '_');
        if (start + name.Length <= text.Length && string.CompareOrdinal(text, start, name, 0, name.Length) == 0 && Boundary(start + name.Length))
            return start;
        var segment = text.Substring(start, Math.Min(length, text.Length - start));
        var hits = new List<int>();
        for (var i = segment.IndexOf("." + name, StringComparison.Ordinal); i >= 0; i = segment.IndexOf("." + name, i + 1, StringComparison.Ordinal))
            if (Boundary(start + i + 1 + name.Length)) hits.Add(start + i + 1);
        return hits.Count == 1 ? hits[0] : null;
    }

    private static readonly Regex DottedChain = new(@"^[A-Za-z_][A-Za-z0-9_]*(\s*\.\s*[A-Za-z_][A-Za-z0-9_]*)*", RegexOptions.CultureInvariant);

    /// <summary>Whether a use that resolved to a descendant of <paramref name="id"/> got there
    /// through <paramref name="id"/> itself: its chain has one head for the declaration, one part
    /// per step down to the target, and the unresolved member tail. A same-named nested
    /// declaration (an occurrence named like its assembly) makes the count differ.</summary>
    private static bool ChainThrough(string text, FirmamentOutlineEdge use, string id)
    {
        var steps = use.To[(id.Length + 1)..].Split('/').Length;
        var tail = use.Member?.Split('.').Length ?? 0;
        var span = text.Substring(use.Span.Start, Math.Min(use.Span.Length, text.Length - use.Span.Start));
        var chain = DottedChain.Match(span);
        return chain.Success && chain.Value.Split('.').Length == 1 + steps + tail;
    }

    private static int? HeadIn(string text, int start, string name)
    {
        var end = start + name.Length;
        return end <= text.Length && string.CompareOrdinal(text, start, name, 0, name.Length) == 0
            && (end == text.Length || !(char.IsLetterOrDigit(text[end]) || text[end] == '_')) ? start : null;
    }

    private static string Replace(string text, int start, int length, string replacement) =>
        string.Concat(text.AsSpan(0, start), replacement, text.AsSpan(start + length));

    private static FirmamentTextEdit Diff(string before, string after)
    {
        var prefix = 0;
        var max = Math.Min(before.Length, after.Length);
        while (prefix < max && before[prefix] == after[prefix]) prefix++;
        var suffix = 0;
        while (suffix < max - prefix && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix]) suffix++;
        return new(prefix, before.Length - prefix - suffix, after.Substring(prefix, after.Length - prefix - suffix));
    }
}
