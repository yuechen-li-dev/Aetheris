using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>One structural edit, addressed by outline node ids (<see cref="FirmamentLanguageOutlineService"/>).
/// <c>Kind</c> is <c>setField</c>, <c>setValue</c> (Static initializer), <c>removeField</c>, <c>insertDeclaration</c>, <c>removeDeclaration</c>,
/// <c>renameDeclaration</c>, <c>moveDeclaration</c>, <c>insertFeature</c> <c>extractFeature</c> or <c>nameFeature</c>.
/// <c>removeDeclaration</c> and <c>moveDeclaration</c> also take a call node (a placed Feature).</summary>
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
    /// <summary>nameFeature: exact authored statement span of an anonymous call, when Node is absent.</summary>
    public FirmamentOutlineSpan? Span { get; init; }
    public FirmamentDeclarationSpec? Declaration { get; init; }
    /// <summary>insertDeclaration and insertFeature: one declaration as source text, used instead of
    /// <see cref="Declaration"/>. With <see cref="Name"/> its declared name is replaced.</summary>
    public string? Source { get; init; }
    /// <summary>extractFeature: the fields that become parameters; the rest stay fixed in the Feature.</summary>
    public IReadOnlyList<string>? Parameters { get; init; }
    public FirmamentGridEdit? Grid { get; init; }
    public FirmamentPlacementEdit? Placement { get; init; }
}

public sealed record FirmamentGridEdit(int Columns, int Rows, double PitchXmm, double PitchYmm);
public sealed record FirmamentPlacementEdit(double[] TranslationMm, string Axis, double AngleDegrees);

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
public static partial class FirmamentLanguageEditService
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
                "setValue" => SetValue(text, before, operation),
                "removeField" => RemoveField(text, before, operation),
                "insertDeclaration" => InsertDeclaration(text, before, operation),
                "removeDeclaration" => RemoveDeclaration(text, before, operation, document, warnings),
                "renameDeclaration" => RenameDeclaration(text, before, operation, document, warnings),
                "moveDeclaration" => MoveDeclaration(text, before, operation),
                "insertFeature" => InsertFeature(text, before, operation),
                "extractFeature" => ExtractFeature(text, before, operation),

                "nameFeature" => NameFeature(text, before, operation),
                "replaceDeclaration" => ReplaceDeclaration(text, before, operation),
                "patternFeature" => PatternFeature(text, before, operation),
                "extrudeProfile" => ExtrudeProfile(text, before, operation),
                "pocketProfile" => PocketProfile(text, before, operation),
                "setPlacement" => SetPlacement(text, before, operation),
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

    private static (string, string?) SetValue(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        var (id, declaration) = Find(parse, op.Node);
        if (declaration.Type.Name.Text != "Static" || declaration.Initializer is null || declaration.Body is not null)
            throw new FirmamentLanguageEditException("edit-value-readonly", "Only an authored Static initializer can be edited as a value.");
        var value = RequireValue(op.Value, "Value");
        return (Replace(text, declaration.Initializer.Span.Start, declaration.Initializer.Span.Length, value), id);
    }

    private static (string, string?) SetField(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        if (FindStatement(parse, op.Node) is { Call: true } placed) return (SetArgument(text, placed, op), placed.Id);
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
        if (FindStatement(parse, op.Node) is { Call: true } placed) return (RemoveArgument(text, placed, op), placed.Id);
        var (id, declaration) = Find(parse, op.Node);
        var name = RequireIdentifier(op.Field, "field name");
        var matches = Fields(declaration).Where(f => f.Name.Text == name).ToArray();
        if (matches.Length == 0) throw new FirmamentLanguageEditException("edit-field-not-found", $"{id} has no {name} field.");
        if (matches.Length > 1)
            throw new FirmamentLanguageEditException("edit-duplicate-field", $"{name} is written {matches.Length} times on {id}; edit it in source.");
        return (RemoveStatement(text, matches[0].Span), id);
    }

    // A placed call's fields are its named arguments: `MountHole(center: Point2(0mm, 0mm), diameter: 8mm)`.
    private static ArgumentSyntax[] Arguments((string Id, string? ParentId, StatementSyntax Statement, bool Call) placed, string name) =>
        FirmamentLanguageOutlineService.ApplicationCall(placed.Statement)!.Arguments.Where(a => a.Name?.Text == name).ToArray();

    private static string SetArgument(string text, (string Id, string? ParentId, StatementSyntax Statement, bool Call) placed, FirmamentLanguageEditOperation op)
    {
        var name = RequireIdentifier(op.Field, "argument name");
        var value = RequireValue(op.Value, name);
        var call = FirmamentLanguageOutlineService.ApplicationCall(placed.Statement)!;
        var matches = Arguments(placed, name);
        if (matches.Length > 1) throw new FirmamentLanguageEditException("edit-duplicate-field", $"{name} is passed {matches.Length} times; edit it in source.");
        if (matches.Length == 1) return Replace(text, matches[0].Value.Span.Start, matches[0].Value.Span.Length, value);
        var close = call.Span.Start + call.Span.Length - 1;
        return Replace(text, close, 0, (call.Arguments.Count > 0 ? ", " : "") + $"{name}: {value}");
    }

    private static string RemoveArgument(string text, (string Id, string? ParentId, StatementSyntax Statement, bool Call) placed, FirmamentLanguageEditOperation op)
    {
        var name = RequireIdentifier(op.Field, "argument name");
        var call = FirmamentLanguageOutlineService.ApplicationCall(placed.Statement)!;
        var index = call.Arguments.ToList().FindIndex(a => a.Name?.Text == name);
        if (index < 0) throw new FirmamentLanguageEditException("edit-field-not-found", $"{placed.Id} does not pass {name}.");
        var argument = call.Arguments[index];
        // Take the separator on one side: the comma after it, or for the last argument the one before.
        int start = argument.Span.Start, end = argument.Span.Start + argument.Span.Length;
        if (index < call.Arguments.Count - 1) end = call.Arguments[index + 1].Span.Start;
        else if (index > 0) start = call.Arguments[index - 1].Span.Start + call.Arguments[index - 1].Span.Length;
        return text.Remove(start, end - start);
    }

    private static (string, string?) InsertDeclaration(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        if (op.Declaration is null && op.Source is null)
            throw new FirmamentLanguageEditException("edit-input-required", "insertDeclaration requires a declaration or its source.");
        DeclarationSyntax? parent = null;
        if (op.Parent is not null) parent = Find(parse, op.Parent).Syntax;
        var siblings = StatementSiblings(parse, op.Parent);
        var indent = parent is null ? "" : ChildIndent(text, parent);
        var call = op.Declaration is null && IsCallSource(op.Source!);
        if (call && parent is null) throw new FirmamentLanguageEditException("edit-call-scope", "A call is placed inside a body, such as a Modify.");
        var callSource = call ? op.Source!.Trim() : null;
        if (call && op.Name is not null)
        {
            var recipe = (ReferenceSyntax)FirmamentLanguageOutlineService.ApplicationCall(Parse(callSource!).Document.Statements.Single())!.Target;
            var name = ApplicationName(recipe.Name.Text, op.Name, Siblings(FirmamentLanguageOutlineService.Declarations(parse), op.Parent).Select(d => d.Syntax));
            callSource = $"Feature {name} = {callSource}";
        }
        var snippet = op.Declaration is { } spec ? Write(spec, indent, Unit(text)) : call ? indent + callSource : FromSource(op.Source!, op.Name, indent);
        if (!call) CheckSnippet(snippet);
        var anchor = Anchor(siblings, op);
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
        var created = FirmamentLanguageOutlineService.Statements(Parse(next)).FirstOrDefault(d => d.Statement.Span.Start == start);
        return (next, created.Id);
    }

    /// <summary>One bare call of a named Feature or function, such as <c>MountHole(center: Point2(0mm, 0mm))</c>.</summary>
    private static bool IsCallSource(string source)
    {
        var text = source.Trim();
        if (!text.EndsWith(')')) return false;
        var parse = Parse("Probe Probe {\n" + text + "\n}");
        var body = parse.Document.Statements.OfType<DeclarationSyntax>().FirstOrDefault()?.Body?.Statements ?? [];
        if (parse.IsSuccess && body.Count == 1 && body[0] is DeclarationSyntax) return false;
        if (!parse.IsSuccess || body.Count != 1 || body[0] is not ExpressionStatementSyntax || FirmamentLanguageOutlineService.ApplicationCall(body[0]) is null)
            throw new FirmamentLanguageEditException("edit-invalid-declaration", $"'{text}' is not one call.");
        return true;
    }

    /// <summary>A feature goes into the <c>Modify</c> of its body: the last one already written for
    /// that body among its siblings, or a new one placed right after the body.</summary>
    private static (string, string?) InsertFeature(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        var (id, body) = Find(parse, op.Node);
        if (op.Source is null) throw new FirmamentLanguageEditException("edit-input-required", "insertFeature requires the feature source.");
        if (body.Name is not { } bodyName || FirmamentLanguageOutlineService.NameIsUse(body))
            throw new FirmamentLanguageEditException("edit-not-a-body", $"{id} is not a named body that a feature can modify.");
        var declarations = FirmamentLanguageOutlineService.Declarations(parse);
        var parentId = declarations.First(d => d.Id == id).ParentId;
        var modify = Siblings(declarations, parentId)
            .LastOrDefault(s => s.Syntax.Type.Name.Text is "Modify" or "modify" && s.Syntax.Name?.Text == bodyName.Text);
        var snippet = Parse(op.Source);
        var call = snippet.Document.Statements.Count == 1 ? snippet.Document.Statements[0] as ExpressionStatementSyntax : null;
        if (snippet.IsSuccess && call?.Value is CallSyntax { Target: ReferenceSyntax recipe })
        {
            var siblings = modify.Syntax is null ? [] : Siblings(declarations, modify.Id).Select(d => d.Syntax).ToArray();
            var name = ApplicationName(recipe.Name.Text, op.Name, siblings);
            op = op with { Source = $"Feature {name} = {op.Source.Trim().TrimEnd(';')}", Name = null };
        }
        if (modify.Syntax is not null)
            return InsertDeclaration(text, parse, new("insertDeclaration") { Parent = modify.Id, Source = op.Source, Name = op.Name });
        var (opened, modifyId) = InsertDeclaration(text, parse,
            new("insertDeclaration") { Parent = parentId, After = id, Source = $"Modify {bodyName.Text} {{\n}}" });
        if (modifyId is null) throw new FirmamentLanguageEditException("edit-unparseable", $"A Modify for {bodyName.Text} could not be opened.");
        return InsertDeclaration(opened, Parse(opened), new("insertDeclaration") { Parent = modifyId, Source = op.Source, Name = op.Name });
    }

    /// <summary>
    /// Turn a feature into a reusable <c>Feature</c>: the declaration becomes a call of a new
    /// document-level Feature placed before the declaration's root (usually its Model). Every chosen
    /// field becomes a typed parameter defaulting to its current value, and the call passes the
    /// current values, so the model builds the same geometry. Other fields stay fixed in the Feature.
    /// The Feature's body is written one field after another on one line; comments inside the
    /// original body are not carried over. The result's node is the call.
    /// </summary>
    private static (string, string?) ExtractFeature(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        var (id, declaration) = Find(parse, op.Node);
        var name = RequireIdentifier(op.Name, "Feature name");
        var declarations = FirmamentLanguageOutlineService.Declarations(parse);
        var entry = declarations.First(d => d.Id == id);
        if (entry.ParentId is null)
            throw new FirmamentLanguageEditException("edit-extract-scope", "Only a declaration inside a body, such as a feature in a Modify, becomes a placed Feature.");
        if (BodyOf(declaration) is not { } body || declaration.Body is null || body.Statements.Any(s => s is not FieldSyntax))
            throw new FirmamentLanguageEditException("edit-extract-shape", "Only a declaration written as fields becomes a Feature; this one has nested declarations or no body.");
        if (declarations.Any(d => d.ParentId is null && d.Syntax.Name?.Text == name))
            throw new FirmamentLanguageEditException("edit-name-taken", $"The document already declares {name}.");
        var fields = body.Statements.Cast<FieldSyntax>().ToArray();
        var parameters = new List<(string Field, string Name, string Type, string Value)>();
        foreach (var requested in op.Parameters ?? [])
        {
            var field = fields.FirstOrDefault(f => f.Name.Text == requested)
                ?? throw new FirmamentLanguageEditException("edit-field-not-found", $"{id} has no {requested} field.");
            var type = ParameterType(field.Value)
                ?? throw new FirmamentLanguageEditException("edit-extract-parameter", $"{requested} cannot be a parameter: lengths, angles, numbers and Point2 values can.");
            var parameter = char.ToLowerInvariant(requested[0]) + requested[1..];
            if (parameters.Any(p => p.Name == parameter)) continue;
            parameters.Add((requested, parameter, type, text.Substring(field.Value.Span.Start, field.Value.Span.Length)));
        }
        string ValueOf(FieldSyntax f) => parameters.FirstOrDefault(p => p.Field == f.Name.Text) is { Name: { } p } ? p : text.Substring(f.Value.Span.Start, f.Value.Span.Length);
        var kind = text.Substring(declaration.Type.Span.Start, declaration.Type.Span.Length);
        var unit = Unit(text);
        var feature = $"Feature {name}({string.Join(", ", parameters.Select(p => $"{p.Name}: {p.Type} = {p.Value}"))}) -> {kind} {{\n"
            + $"{unit}return {kind} {{ {string.Join(" ", fields.Select(f => $"{f.Name.Text}: {ValueOf(f)}"))} }}\n}}";
        CheckSnippet(feature);
        var application = ApplicationName(name, declaration.Name?.Text,
            Siblings(declarations, entry.ParentId).Where(d => d.Id != id).Select(d => d.Syntax));
        var call = $"Feature {application} = {name}({string.Join(", ", parameters.Select(p => $"{p.Name}: {p.Value}"))})";

        // The declaration becomes the call, in place; then the Feature goes before the root that holds it.
        var end = declaration.Span.Start + declaration.Span.Length;
        if (end < text.Length && text[end] == ';') end++;
        var placed = Replace(text, declaration.Span.Start, end - declaration.Span.Start, call);
        var rootId = id[..(id.IndexOf('/') is var slash && slash < 0 ? id.Length : slash)];
        var root = declarations.First(d => d.Id == rootId && d.ParentId is null).Syntax;
        var at = LineStart(placed, root.Span.Start);
        var next = Replace(placed, at, 0, feature + "\n\n");
        var callStart = declaration.Span.Start + feature.Length + 2;
        var created = FirmamentLanguageOutlineService.Statements(Parse(next)).FirstOrDefault(d => d.Call && d.Statement.Span.Start == callStart);
        return (next, created.Id);
    }

    private static string? ParameterType(ExpressionSyntax value) => value switch
    {
        UnarySyntax { Operator: UnaryOperator.Negative or UnaryOperator.Positive } u => ParameterType(u.Operand),
        QuantitySyntax q => q.Unit.Text switch { "mm" or "cm" or "m" or "um" or "in" => "Length", "deg" or "rad" => "Angle", _ => null },
        LiteralSyntax { Kind: LiteralKind.Number } n => n.Raw.Contains('.') ? "Float" : "Int",
        CallSyntax { Target: ReferenceSyntax { Name.Text: "Point2" } } => "Point2",
        _ => null
    };

    private static (string, string?) NameFeature(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        var requestedSpan = op.Node is not null ? FindStatement(parse, op.Node).Statement.Span : op.Span is { } requested
            ? new FirmamentV2SourceSpan(requested.Start, requested.Length) : null;
        if (requestedSpan is not { } span)
            throw new FirmamentLanguageEditException("edit-input-required", "nameFeature requires an anonymous call node or exact authored statement span.");
        var statement = FirmamentSyntaxTraversal.Statements(parse.Document).OfType<ExpressionStatementSyntax>()
            .SingleOrDefault(s => s.Span.Start == span.Start && (s.Span.Length == span.Length || s.Value.Span.Length == span.Length));
        if (statement?.Value is not CallSyntax { Target: ReferenceSyntax recipe })
            throw new FirmamentLanguageEditException("edit-not-feature-call", "The span no longer identifies one anonymous Feature call; refresh the source revision.");
        var declarations = FirmamentLanguageOutlineService.Declarations(parse);
        var parent = declarations.Where(d => d.Syntax.Span.Start <= span.Start && d.Syntax.Span.Start + d.Syntax.Span.Length >= span.Start + span.Length)
            .OrderBy(d => d.Syntax.Span.Length).FirstOrDefault();
        var name = ApplicationName(recipe.Name.Text, op.Name, Siblings(declarations, parent.Id).Select(d => d.Syntax));
        var next = Replace(text, span.Start, 0, $"Feature {name} = ");
        return (next, FirmamentLanguageOutlineService.Declarations(Parse(next)).Single(d => d.Syntax.Span.Start == span.Start).Id);
    }

    private static string ApplicationName(string recipe, string? requested, IEnumerable<DeclarationSyntax> siblings)
    {
        var names = siblings.Where(d => d.Name is not null).Select(d => d.Name!.Text).ToHashSet(StringComparer.Ordinal);
        names.Add(recipe); // A result must not shadow the callable it invokes.
        if (requested is not null)
        {
            RequireIdentifier(requested, "Feature application name");
            if (names.Contains(requested)) throw new FirmamentLanguageEditException("edit-name-taken", $"A declaration here is already called {requested}.");
            return requested;
        }
        var stem = char.ToLowerInvariant(recipe[0]) + recipe[1..];
        var candidate = stem;
        for (var suffix = 2; names.Contains(candidate); suffix++) candidate = stem + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return candidate;
    }

    private static (string, string?) RemoveDeclaration(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op,
        string document, List<string> warnings)
    {
        var placed = FindStatement(parse, op.Node);
        if (placed.Call && placed.Statement is not DeclarationSyntax) return (RemoveStatement(text, placed.Statement.Span), null);
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
        var (id, parentId, statement, _) = FindStatement(parse, op.Node);
        if (op.After == id) return (text, id);
        var all = FirmamentLanguageOutlineService.Statements(parse);
        if (op.After is not null && all.FirstOrDefault(d => d.Id == op.After).ParentId != parentId)
            throw new FirmamentLanguageEditException("edit-move-scope", "A declaration moves among its siblings; use remove and insert to move it elsewhere.");
        var body = text[statement.Span.Start..StatementEnd(text, statement.Span)];
        // Resolve the anchor and parent before removing: ids can change once a sibling that shared
        // their name is gone (`Modify:Base` becomes `Base`), positions only shift.
        var anchorBefore = Anchor(StatementSiblings(parse, parentId).Where(s => s.Id != id).ToArray(), op);
        var parentBefore = parentId is null ? null : all.First(d => d.Id == parentId).Statement;
        var removed = RemoveStatement(text, statement.Span);
        var shift = text.Length - removed.Length;
        var all2 = FirmamentLanguageOutlineService.Statements(Parse(removed));
        StatementSyntax? Relocate(StatementSyntax? before)
        {
            if (before is null) return null;
            var start = before.Span.Start > statement.Span.Start ? before.Span.Start - shift : before.Span.Start;
            return all2.First(d => d.Statement.Span.Start == start && d.Statement.GetType() == before.GetType()).Statement;
        }
        var anchor = new AnchorPoint(Relocate(anchorBefore.After), Relocate(anchorBefore.Before));
        var parent = Relocate(parentBefore) as DeclarationSyntax;
        int at; string inserted;
        if (parent is null)
        {
            if (anchor.After is { } after) { at = StatementEnd(removed, after.Span); inserted = "\n" + body; }
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
        var moved = FirmamentLanguageOutlineService.Statements(Parse(next)).FirstOrDefault(d => d.Statement.Span.Start == start);
        return (next, moved.Id);
    }

    // ---- Placement --------------------------------------------------------------------------

    private readonly record struct AnchorPoint(StatementSyntax? After, StatementSyntax? Before);

    private static AnchorPoint Anchor(IReadOnlyList<(string Id, StatementSyntax Statement)> siblings, FirmamentLanguageEditOperation op)
    {
        if (op.After is not null)
        {
            var after = siblings.FirstOrDefault(s => s.Id == op.After);
            if (after.Statement is null)
                throw new FirmamentLanguageEditException("edit-anchor-not-found", $"{op.After} is not a sibling at this place.");
            return new(after.Statement, null);
        }
        if (op.First) return siblings.Count == 0 ? new(null, null) : new(null, siblings[0].Statement);
        return siblings.Count == 0 ? new(null, null) : new(siblings[^1].Statement, null);
    }

    /// <summary>The outline nodes under a parent, declarations and placed calls, in source order.</summary>
    private static IReadOnlyList<(string Id, StatementSyntax Statement)> StatementSiblings(SyntaxParseResult parse, string? parentId) =>
        FirmamentLanguageOutlineService.Statements(parse).Where(s => s.ParentId == parentId).Select(s => (s.Id, s.Statement)).ToArray();

    private static (string Id, string? ParentId, StatementSyntax Statement, bool Call) FindStatement(SyntaxParseResult parse, string? id)
    {
        if (id is null) throw new FirmamentLanguageEditException("edit-input-required", "The operation requires a node id.");
        var match = FirmamentLanguageOutlineService.Statements(parse).FirstOrDefault(d => d.Id == id);
        return match.Statement is null
            ? throw new FirmamentLanguageEditException("edit-node-not-found", $"Nothing has the id {id}. The source may have changed.")
            : match;
    }

    /// <summary>Where a declaration goes inside a parent body, and the text to insert there.</summary>
    private static (int At, string Inserted) Placement(string text, DeclarationSyntax parent, string snippet,
        StatementSyntax? after, StatementSyntax? before)
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

    /// <summary>Declaration source placed at <paramref name="indent"/>, its own relative indentation
    /// kept, its declared name replaced when <paramref name="name"/> is given.</summary>
    private static string FromSource(string source, string? name, string indent)
    {
        var text = source.Replace("\r\n", "\n").Trim();
        var parse = Parse(text);
        if (!parse.IsSuccess || parse.Document.Statements.Count != 1 || parse.Document.Statements[0] is not DeclarationSyntax declaration)
            throw new FirmamentLanguageEditException("edit-invalid-declaration",
                $"The declaration does not parse on its own: {Errors(parse).FirstOrDefault() ?? "expected one declaration"}");
        if (name is not null)
        {
            RequireIdentifier(name, "name");
            if (declaration.Name is not { } declared)
                throw new FirmamentLanguageEditException("edit-unnamed", "The declaration has no name to replace.");
            text = Replace(text, declared.Span.Start, declared.Span.Length, name);
        }
        var lines = text.Split('\n');
        var common = lines.Skip(1).Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        // The closing brace sits at the first line's level: keep the rest relative to it.
        var closing = lines.Length > 1 ? lines[^1].Length - lines[^1].TrimStart().Length : 0;
        var strip = Math.Min(common, closing);
        return string.Join("\n", lines.Select((l, i) => i == 0 ? indent + l : l.Trim().Length == 0 ? "" : indent + l[Math.Min(strip, l.Length - l.TrimStart().Length)..]));
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
