using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentOutlineSpan(int Start, int Length);

public sealed record FirmamentOutlineField(string Name, string Text, string Shape, FirmamentOutlineSpan Span, FirmamentOutlineSpan ValueSpan);

public sealed record FirmamentOutlineNode(string Id, string Kind, string? Variant, IReadOnlyList<string> Modifiers, string? Name,
    bool Occurrence, string? ParentId, int Order, FirmamentOutlineSpan Span, FirmamentOutlineSpan? NameSpan, string? SchemaId,
    IReadOnlyList<string> Parameters, IReadOnlyList<FirmamentOutlineField> Fields, IReadOnlyList<string> Children)
{
    /// <summary>A bare or named Feature application: <c>Kind</c> is the callee and <c>Fields</c> are its arguments,
    /// positional ones named <c>#1</c>, <c>#2</c>. Named applications retain <c>Name</c> and its stable path.</summary>
    public bool Call { get; init; }
    /// <summary>The value a declaration is initialized with (<c>Static Thickness: Length = 8mm</c>), named <c>=</c>.
    /// Absent for object initializers, whose fields are <c>Fields</c>.</summary>
    public FirmamentOutlineField? Value { get; init; }
    /// <summary>The declared type annotation (<c>Length</c>), as written.</summary>
    public string? Annotation { get; init; }
    /// <summary>Declared parameters with their types and defaults, as written (Feature, Template).</summary>
    public IReadOnlyList<FirmamentOutlineParameter> Signature { get; init; } = [];
}

public sealed record FirmamentOutlineParameter(string Name, string Type, string? Default);

/// <summary>One resolved name use. <c>Via</c> says where the use sits in the source declaration:
/// <c>field:Profile</c>, <c>clause:Using</c>, <c>clause:Over</c>, <c>target</c>, <c>body</c> (an
/// expression statement or Pattern mapping), <c>argument:Spec</c>, <c>annotation</c>, <c>result</c>.
/// <c>Kind</c> is <c>reference</c>, <c>call</c>, <c>specialization</c>, or, for a declaration whose own
/// name is the use, <c>modifies</c>, <c>asserts</c> or <c>recognizes</c>.</summary>
public sealed record FirmamentOutlineEdge(string From, string To, string Kind, string Via, string? Member,
    FirmamentOutlineSpan Span, bool Ambiguous)
{
    /// <summary>The document that declares <c>To</c> when it is not this one (project outlines only).</summary>
    public string? ToDocument { get; init; }
}

/// <summary>A name use that no declaration in scope answers: a builtin (<c>Point2</c>,
/// <c>TraceLoop</c>), a vocabulary word, or a declaration in another document.</summary>
public sealed record FirmamentOutlineExternal(string From, string Name, string? Member, string Kind, string Via, FirmamentOutlineSpan Span);

public sealed record FirmamentOutlineDiagnostic(string Severity, string Code, string Message, FirmamentOutlineSpan? Span);

public sealed record FirmamentLanguageOutline(string Version, string Document, string Revision,
    IReadOnlyList<FirmamentOutlineNode> Nodes, IReadOnlyList<FirmamentOutlineEdge> Edges,
    IReadOnlyList<FirmamentOutlineExternal> Externals, IReadOnlyList<FirmamentOutlineDiagnostic> Diagnostics);

/// <summary>
/// Declaration graph of one Firmament document for visual editors: every declaration with a
/// stable name-path id, its authored fields, and the resolved name uses between declarations.
/// This is source structure resolved by lexical scope, not compiler admission: an edge says
/// "this declaration names that one", and the build decides whether the use is admitted.
/// </summary>
/// <remarks>
/// Ids are name paths (<c>MotorMount/Body</c>). A name shared by siblings is qualified by kind
/// for every sibling that shares it (<c>MotorMount/Compose:Body</c>, <c>MotorMount/Modify:Body</c>),
/// with a <c>#n</c> ordinal when kind and name both repeat. A nameless declaration is <c>Kind#n</c>.
/// Ids therefore do not depend on source offsets, only on names, kinds and nesting.
/// </remarks>
public static class FirmamentLanguageOutlineService
{
    public const string Version = "firmament-outline/1";

    // Fields whose values are vocabulary, never declaration names, even when a declaration
    // happens to share the word (Role: Stock next to Profile Stock).
    private static readonly HashSet<string> VocabularyFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Units", "Role", "End", "Kind", "Angle", "Handedness", "Orientation", "Hand", "Rule",
        "Correspondence", "Easing", "Layout", "Direction", "Variant", "Mode", "Policy", "Alignment"
    };

    private static readonly HashSet<string> ChoiceFields = FirmamentSemanticSchemas.All
        .SelectMany(c => c.Fields.Where(f => f.Kind == FirmamentSchemaValueKind.Choice).Select(f => f.Name))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private sealed class Entry
    {
        /// <summary>The declaration, or a synthetic stand-in for a call statement (see <see cref="Call"/>).</summary>
        public required DeclarationSyntax Syntax { get; init; }
        /// <summary>For a bare call written in a body: the call. Calls have no name and no children.</summary>
        public CallSyntax? Call { get; init; }
        public required StatementSyntax Statement { get; init; }
        public required string Id { get; init; }
        public Entry? Parent { get; init; }
        public required int Order { get; init; }
        public List<Entry> Children { get; } = [];
        public string Kind => Call?.Target is ReferenceSyntax recipe ? recipe.Name.Text : Syntax.Type.Name.Text;
        public string? Name => Syntax.Name?.Text;
    }

    public static FirmamentLanguageOutline Outline(string source, string document, string revision)
    {
        ArgumentNullException.ThrowIfNull(source);
        var parse = FirmamentSyntaxParser.Parse(source, document);
        var roots = new List<Entry>();
        Collect(parse.Document.Statements, null, roots);
        var all = Flatten(roots).ToList();
        var edges = new List<FirmamentOutlineEdge>();
        var externals = new List<FirmamentOutlineExternal>();
        foreach (var entry in all) Resolve(entry, roots, source, edges, externals);
        var nodes = all.Select(entry => Node(entry, source)).ToArray();
        var diagnostics = parse.Diagnostics.Select(d => new FirmamentOutlineDiagnostic(d.Severity, d.Code, d.Message,
            d.SourceSpan is { } s ? new FirmamentOutlineSpan(s.Start, s.Length) : null)).ToArray();
        return new(Version, document, revision, nodes, edges.Distinct().ToArray(), externals.Distinct().ToArray(), diagnostics);
    }

    /// <summary>Outline of one project document. Names its own scopes do not answer are resolved
    /// against the top-level declarations of the other project documents (its includes and
    /// their modules); a resolved use becomes an edge with <c>ToDocument</c> set.</summary>
    public static FirmamentLanguageOutline Outline(Assembly.FirmamentProjectSnapshot project, string document, string revision)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!project.TryResolve(document, out var source)) throw new ArgumentException("Document is absent from the snapshot.", nameof(document));
        var local = Outline(source, document, revision);
        if (local.Externals.Count == 0) return local;
        var self = Assembly.FirmamentProjectSnapshot.NormalizePath(document);
        var others = project.Documents.Where(d => !string.Equals(d.Key, self, StringComparison.Ordinal))
            .Select(d => Outline(d.Value, d.Key, revision)).ToArray();
        var topLevel = others.SelectMany(o => o.Nodes.Where(n => n.ParentId is null && n.Name is not null && !NameIsUseKind(n))
            .Select(n => (Document: o.Document, Node: n, Outline: o))).ToLookup(x => x.Node.Name!, StringComparer.Ordinal);
        var edges = local.Edges.ToList();
        var remaining = new List<FirmamentOutlineExternal>();
        foreach (var use in local.Externals)
        {
            var candidates = topLevel[use.Name].ToArray();
            if (candidates.Length == 0) { remaining.Add(use); continue; }
            var (doc, node, outline) = candidates[0];
            var target = node;
            var parts = use.Member?.Split('.') ?? [];
            var consumed = 0;
            var byId = outline.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
            while (consumed < parts.Length && target.Children.Select(id => byId[id]).FirstOrDefault(c => c.Name == parts[consumed]) is { } child)
            { target = child; consumed++; }
            var member = consumed < parts.Length ? string.Join(".", parts.Skip(consumed)) : null;
            edges.Add(new(use.From, target.Id, use.Kind, use.Via, member, use.Span, candidates.Length > 1) { ToDocument = doc });
        }
        return local with { Edges = edges, Externals = remaining };
    }

    /// <summary>The declarations of a parsed document under their outline ids, parents first.
    /// Structural edits use this to address the same nodes a visual editor shows.</summary>
    internal static IReadOnlyList<(string Id, string? ParentId, DeclarationSyntax Syntax)> Declarations(SyntaxParseResult parse)
    {
        var roots = new List<Entry>();
        Collect(parse.Document.Statements, null, roots);
        return Flatten(roots).Where(e => e.Statement is DeclarationSyntax).Select(e => (e.Id, e.Parent?.Id, e.Syntax)).ToArray();
    }

    /// <summary>Every outline node's statement, declarations and bare calls, under its outline id.</summary>
    internal static IReadOnlyList<(string Id, string? ParentId, StatementSyntax Statement, bool Call)> Statements(SyntaxParseResult parse)
    {
        var roots = new List<Entry>();
        Collect(parse.Document.Statements, null, roots);
        return Flatten(roots).Select(e => (e.Id, e.Parent?.Id, e.Statement, e.Call is not null)).ToArray();
    }

    /// <summary>The expression of a bare call or a named Feature application.</summary>
    internal static CallSyntax? ApplicationCall(StatementSyntax statement) =>
        statement switch
        {
            ExpressionStatementSyntax { Value: CallSyntax { Target: ReferenceSyntax } call } => call,
            DeclarationSyntax { Type.Name.Text: "Feature", ResultType: null, Initializer: CallSyntax { Target: ReferenceSyntax } call } => call,
            _ => null
        };

    internal static bool NameIsUse(DeclarationSyntax declaration) =>
        declaration.Type.Name.Text is "Modify" or "modify" or "Recognize" || declaration.Modifiers.Any(m => m.Name.Text == "Assert");

    private static bool NameIsUseKind(FirmamentOutlineNode node) =>
        node.Kind is "Modify" or "modify" or "Recognize" || node.Modifiers.Contains("Assert");

    private static IEnumerable<Entry> Flatten(IEnumerable<Entry> entries) =>
        entries.SelectMany(e => new[] { e }.Concat(Flatten(e.Children)));

    private static void Collect(IEnumerable<StatementSyntax> statements, Entry? parent, List<Entry> into)
    {
        // Inside a body, a bare call is a node too: a reused Feature placed like any other feature.
        var items = statements.SelectMany(s => DirectDeclarations(s).Select(d => ((StatementSyntax)d, d, ApplicationCall(d)))
            .Concat(parent is null ? [] : DirectCalls(s).Select(c => (c, CallStandIn(ApplicationCall(c)!), ApplicationCall(c))))).ToList();
        var segments = Segments(items.Select(i => i.Item2).ToList());
        for (var i = 0; i < items.Count; i++)
        {
            var id = parent is null ? segments[i] : parent.Id + "/" + segments[i];
            var entry = new Entry { Syntax = items[i].Item2, Call = items[i].Item3, Statement = items[i].Item1, Id = id, Parent = parent, Order = i };
            into.Add(entry);
            if (entry.Call is null) Collect(Body(items[i].Item2), entry, entry.Children);
        }
    }

    // Bare calls reachable the way DirectDeclarations reaches declarations: through nested blocks, not mappings.
    private static IEnumerable<StatementSyntax> DirectCalls(StatementSyntax statement) => statement switch
    {
        StatementBlockSyntax b => b.Body.Statements.SelectMany(DirectCalls),
        ExpressionStatementSyntax when ApplicationCall(statement) is not null => [statement],
        _ => []
    };

    // A nameless declaration whose type is the callee keeps id assignment uniform: `MountHole`, or
    // `MountHole#1` and `MountHole#2` when a body calls it twice.
    private static DeclarationSyntax CallStandIn(CallSyntax call) =>
        new(new TypeSyntax(((ReferenceSyntax)call.Target).Name, [], call.Target.Span), null, [], [], null, null, [], null, null, false, call.Span);

    // Declarations reachable from a statement without passing through another declaration:
    // nested blocks and Pattern mappings hold declarations that belong to the enclosing one.
    private static IEnumerable<DeclarationSyntax> DirectDeclarations(StatementSyntax statement) => statement switch
    {
        DeclarationSyntax d => [d],
        StatementBlockSyntax b => b.Body.Statements.SelectMany(DirectDeclarations),
        MappingSyntax m => DirectDeclarations(m.Value),
        ReplacementSyntax r => [r.Replacement],
        _ => []
    };

    private static IEnumerable<StatementSyntax> Body(DeclarationSyntax declaration) =>
        (declaration.Body?.Statements ?? []).Concat(declaration.Initializer is ObjectSyntax o ? o.Body.Statements : []);

    private static string[] Segments(IReadOnlyList<DeclarationSyntax> declarations)
    {
        var nameCounts = declarations.Where(d => d.Name is not null).GroupBy(d => d.Name!.Text, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var bases = declarations.Select(d => d.Name is null ? d.Type.Name.Text
            : nameCounts[d.Name.Text] == 1 ? d.Name.Text : d.Type.Name.Text + ":" + d.Name.Text).ToArray();
        var baseCounts = bases.GroupBy(b => b, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        // Every repeat of a basis carries an ordinal, the first included, so adding a second
        // occurrence later in the file never renames the first.
        return bases.Select(b => baseCounts[b] == 1 ? b : b + "#" + (seen[b] = seen.GetValueOrDefault(b) + 1)).ToArray();
    }

    private static FirmamentOutlineNode Node(Entry entry, string source)
    {
        if (entry.Call is { } call)
            return new(entry.Id, entry.Kind, null, [], entry.Name, false, entry.Parent?.Id, entry.Order, Span(entry.Statement.Span),
                entry.Syntax.Name is { } name ? Span(name.Span) : null, null, [],
                call.Arguments.Select((a, i) => new FirmamentOutlineField(a.Name?.Text ?? "#" + (i + 1), Text(source, a.Value.Span),
                    Shape(a.Value), Span(a.Span), Span(a.Value.Span))).ToArray(), []) { Call = true };
        var d = entry.Syntax;
        var variant = d.Type.Arguments.Count == 0 ? null
            : string.Join(", ", d.Type.Arguments.Select(a => Text(source, a.Span)));
        var schema = SchemaFor(entry);
        var fields = Body(d).OfType<FieldSyntax>().Select(f => new FirmamentOutlineField(f.Name.Text,
            Text(source, f.Value.Span), Shape(f.Value), Span(f.Span), Span(f.Value.Span))).ToArray();
        var parameters = d.Parameters.Select(p => p.Name?.Text ?? Text(source, p.Span)).ToArray();
        var value = d.Initializer is { } init and not ObjectSyntax
            ? new FirmamentOutlineField("=", Text(source, init.Span), Shape(init), Span(init.Span), Span(init.Span)) : null;
        return new(entry.Id, entry.Kind, variant, d.Modifiers.Select(m => m.Name.Text).ToArray(), entry.Name,
            d.Occurrence, entry.Parent?.Id, entry.Order, Span(d.Span), d.Name is null ? null : Span(d.Name.Span), schema,
            parameters, fields, entry.Children.Select(c => c.Id).ToArray())
        {
            Value = value,
            Annotation = d.Annotation is null ? null : Text(source, d.Annotation.Span),
            Signature = d.Parameters.Where(p => p.Name is not null).Select(p => new FirmamentOutlineParameter(p.Name!.Text,
                Text(source, p.Value.Span), p.Default is null ? null : Text(source, p.Default.Span))).ToArray(),
        };
    }

    private static string? SchemaFor(Entry entry)
    {
        var name = entry.Kind;
        var variantName = entry.Syntax.Type.Arguments.Count == 1 ? $"{name}<{entry.Syntax.Type.Arguments[0].Value switch { ReferenceSyntax r => r.Name.Text, _ => "" }}>" : null;
        var candidates = FirmamentSemanticSchemas.All.Where(c => c.Name == name || (variantName is not null && c.Name == variantName)).ToArray();
        if (variantName is not null && candidates.Count(c => c.Name == variantName) == 1) return candidates.Single(c => c.Name == variantName).Id.Value;
        return candidates.Length == 1 ? candidates[0].Id.Value : null;
    }

    private static string Shape(ExpressionSyntax expression) => expression switch
    {
        QuantitySyntax => "quantity",
        LiteralSyntax l => l.Kind.ToString().ToLowerInvariant(),
        ReferenceSyntax => "reference",
        MemberSyntax => "member",
        SequenceSyntax => "sequence",
        CallSyntax => "call",
        SpecializationSyntax => "specialization",
        ObjectSyntax => "object",
        BinarySyntax or UnarySyntax => "expression",
        _ => "other"
    };

    private static void Resolve(Entry entry, IReadOnlyList<Entry> roots, string source, List<FirmamentOutlineEdge> edges, List<FirmamentOutlineExternal> externals)
    {
        if (entry.Call is { } call)
        {
            var scope = new HashSet<string>(StringComparer.Ordinal);
            for (var a = entry.Parent; a is not null; a = a.Parent)
                foreach (var p in a.Syntax.Parameters) if (p.Name is not null) scope.Add(p.Name.Text);
            Walk(call, entry, roots, source, "body", scope, null, edges, externals);
            return;
        }
        var d = entry.Syntax;
        var locals = new HashSet<string>(d.Parameters.Select(p => p.Name?.Text).OfType<string>(), StringComparer.Ordinal);
        for (var a = entry.Parent; a is not null; a = a.Parent)
            foreach (var p in a.Syntax.Parameters) if (p.Name is not null) locals.Add(p.Name.Text);

        // `Modify X`, `Assert Volume X` and `Recognize X` name the body they act on: their own name is a use.
        if (NameIsUse(entry) && entry.Name is not null)
        {
            var target = Lookup(entry, roots, [entry.Name], exclude: entry, openUsing: true);
            if (target.Entry is not null)
                edges.Add(new(entry.Id, target.Entry.Id, UseKind(entry), "target", null,
                    Span(d.Name!.Span), target.Ambiguous));
        }

        void Uses(ExpressionSyntax? expression, string via, bool vocabulary = false, HashSet<string>? scoped = null, bool quiet = false)
        {
            if (expression is null || vocabulary) return;
            Walk(expression, entry, roots, source, via, locals, scoped, edges, quiet ? null : externals);
        }

        foreach (var c in d.Clauses) Uses(c.Value, "clause:" + c.Kind);
        if (d.Target is not null) Uses(d.Target, "target");
        if (d.Annotation is not null) Uses(d.Annotation, "annotation");
        if (d.ResultType is not null) Uses(d.ResultType, "result");
        // An unnamed type argument is usually a variant word (Hole<Shaft>, Interface<Fixed>): it
        // links when it names a declaration but is never reported as an unresolved use.
        foreach (var a in d.Type.Arguments.Concat(d.Modifiers.SelectMany(m => m.Arguments)))
            Uses(a.Value, "argument:" + (a.Name?.Text ?? "type"), quiet: a.Name is null);
        if (d.Initializer is ObjectSyntax typed) Uses(typed.Type, "initializer");
        else if (d.Initializer is not null) Uses(d.Initializer, "initializer");
        foreach (var s in Body(d)) Statement(s, null);

        void Statement(StatementSyntax s, HashSet<string>? scoped)
        {
            switch (s)
            {
                case FieldSyntax f:
                    var vocab = VocabularyFields.Contains(f.Name.Text) || ChoiceFields.Contains(f.Name.Text);
                    Uses(f.Value, "field:" + f.Name.Text, vocab && f.Value is ReferenceSyntax, scoped);
                    break;
                // A bare call in a body is its own node and resolves its own uses.
                case ExpressionStatementSyntax e when scoped is null && ApplicationCall(e) is not null: break;
                case ExpressionStatementSyntax e: Uses(e.Value, "body", scoped: scoped); break;
                case ReturnSyntax r: Uses(r.Value, "body", scoped: scoped); break;
                case MappingSyntax m:
                    // `key => value`: the key is a loop binding inside a Pattern and a label in a Set.
                    var inner = new HashSet<string>(scoped ?? [], StringComparer.Ordinal) { m.Key.Text };
                    if (m.Value is not DeclarationSyntax) Statement(m.Value, inner);
                    break;
                case StatementBlockSyntax b: foreach (var x in b.Body.Statements) Statement(x, scoped); break;
            }
        }
    }

    private static void Walk(ExpressionSyntax expression, Entry from, IReadOnlyList<Entry> roots, string source, string via,
        HashSet<string> locals, HashSet<string>? scoped, List<FirmamentOutlineEdge> edges, List<FirmamentOutlineExternal>? externals)
    {
        void Recurse(ExpressionSyntax? e) { if (e is not null) Walk(e, from, roots, source, via, locals, scoped, edges, externals); }
        void Nested(string field, ExpressionSyntax e) => Walk(e, from, roots, source, via + "." + field, locals, scoped, edges, externals);
        void Add(IReadOnlyList<string> chain, string kind, FirmamentV2SourceSpan span) =>
            FirmamentLanguageOutlineService.Add(from, roots, chain, kind, via, span, edges, externals);
        bool Local(string name) => locals.Contains(name) || (scoped?.Contains(name) ?? false);

        switch (expression)
        {
            case ReferenceSyntax r:
                if (!Local(r.Name.Text)) Add([r.Name.Text], "reference", r.Span);
                return;
            case MemberSyntax m when Chain(m) is { } chain:
                if (!Local(chain[0])) Add(chain, "reference", m.Span);
                return;
            case CallSyntax c:
                if (c.Target is ReferenceSyntax cr && !Local(cr.Name.Text)) Add([cr.Name.Text], "call", c.Span);
                else if (c.Target is not ReferenceSyntax) Recurse(c.Target);
                foreach (var a in c.Arguments) { Recurse(a.Value); Recurse(a.Default); }
                return;
            case SpecializationSyntax s:
                if (s.Target is ReferenceSyntax sr && !Local(sr.Name.Text)) Add([sr.Name.Text], "specialization", s.Span);
                else Recurse(s.Target);
                foreach (var a in s.Arguments) { Recurse(a.Value); Recurse(a.Default); }
                return;
            case ObjectSyntax o:
                Recurse(o.Type);
                foreach (var f in o.Body.Statements.OfType<FieldSyntax>())
                {
                    var vocab = VocabularyFields.Contains(f.Name.Text) || ChoiceFields.Contains(f.Name.Text);
                    if (!(vocab && f.Value is ReferenceSyntax)) Nested(f.Name.Text, f.Value);
                }
                foreach (var e in o.Body.Statements.OfType<ExpressionStatementSyntax>()) Recurse(e.Value);
                return;
            case WithSyntax w:
                Recurse(w.Target);
                foreach (var f in w.Body.Statements.OfType<FieldSyntax>()) Nested(f.Name.Text, f.Value);
                return;
        }
        foreach (var child in Children(expression)) Recurse(child);
    }

    private static IEnumerable<ExpressionSyntax> Children(ExpressionSyntax expression) => expression switch
    {
        AliasSyntax a => [a.Value],
        BinarySyntax b => [b.Left, b.Right],
        UnarySyntax u => [u.Operand],
        QuantitySyntax q => [q.Value],
        ToleranceSyntax t => new[] { t.Nominal, t.Plus, t.Minus }.OfType<ExpressionSyntax>(),
        SequenceSyntax s => s.Items.SelectMany(a => new[] { a.Value, a.Default }.OfType<ExpressionSyntax>()),
        MemberSyntax m => [m.Target],
        IndexSyntax i => i.Index is null ? [i.Target] : [i.Target, i.Index],
        TraceSyntax t => new[] { t.Source, t.From, t.To }.OfType<ExpressionSyntax>(),
        TypedValueSyntax t => [t.Type, t.Value],
        ProvenanceSyntax p => [p.Value],
        TypeParameterSyntax p => p.Constraint is null ? [] : [p.Constraint],
        FrameSyntax f => new[] { f.Origin }.Concat(f.Axes.Select(a => a.Value)),
        MemberSetSyntax m => new[] { m.Target }.Concat(m.Members.Select(a => a.Value)),
        MatchSyntax m => new[] { m.Scrutinee }.Concat(m.Arms.SelectMany(a => new[] { a.Pattern, a.Value })),
        _ => []
    };

    // Stock.Outer -> ["Stock", "Outer"]; null when the chain does not start at a plain name.
    private static List<string>? Chain(MemberSyntax member)
    {
        var parts = new List<string>();
        ExpressionSyntax current = member;
        while (current is MemberSyntax m) { parts.Insert(0, m.Member.Text); current = m.Target; }
        if (current is not ReferenceSyntax r) return null;
        parts.Insert(0, r.Name.Text);
        return parts;
    }

    private static void Add(Entry from, IReadOnlyList<Entry> roots, IReadOnlyList<string> chain, string kind, string via,
        FirmamentV2SourceSpan span, List<FirmamentOutlineEdge> edges, List<FirmamentOutlineExternal>? externals)
    {
        var (target, consumed, ambiguous) = Lookup(from, roots, chain, exclude: null, openUsing: true);
        if (target is null)
        {
            externals?.Add(new(from.Id, chain[0], chain.Count > 1 ? string.Join(".", chain.Skip(1)) : null, kind, via, Span(span)));
            return;
        }
        // A name that resolves to the declaration itself or to one of its ancestors is not a
        // dependency (Pattern's `Hole<Shaft> Item { Center: Item }`, a body naming its own host).
        for (var a = from; a is not null; a = a.Parent) if (ReferenceEquals(a, target)) return;
        var member = consumed < chain.Count ? string.Join(".", chain.Skip(consumed)) : null;
        edges.Add(new(from.Id, target.Id, kind, via, member, Span(span), ambiguous));
    }

    private static bool Encloses(Entry candidate, Entry from)
    {
        for (var a = from; a is not null; a = a.Parent) if (ReferenceEquals(a, candidate)) return true;
        return false;
    }

    private readonly record struct Resolution(Entry? Entry, int Consumed, bool Ambiguous);

    private static Resolution Lookup(Entry from, IReadOnlyList<Entry> roots, IReadOnlyList<string> chain, Entry? exclude, bool openUsing)
    {
        var name = chain[0];
        // Scopes from the innermost outward: each enclosing declaration's children, the
        // children of any declaration it opens with `Using`, then the document.
        for (var scope = from; ; scope = scope.Parent)
        {
            IEnumerable<Entry> candidates = scope is null ? roots : scope.Children;
            var visible = candidates.ToList();
            if (scope is not null && openUsing)
                foreach (var clause in scope.Syntax.Clauses.Where(c => c.Kind == ClauseKind.Using && c.Value is ReferenceSyntax))
                {
                    // Resolve the opened name from where the clause is written, without opening
                    // further Using clauses, so two profiles using each other cannot loop.
                    var opened = Lookup(scope, roots, [((ReferenceSyntax)clause.Value).Name.Text], scope, openUsing: false);
                    if (opened.Entry is not null) visible.AddRange(opened.Entry.Children);
                }
            // A declaration never resolves its own uses to itself or to an enclosing declaration.
            var matches = visible.Where(e => e.Name == name && !ReferenceEquals(e, exclude) && !Encloses(e, from)).ToList();
            if (matches.Count > 0)
            {
                if (matches.Count > 1)
                {
                    var definitions = matches.Where(m => !NameIsUse(m)).ToList();
                    if (definitions.Count > 0) matches = definitions;
                }
                var target = matches[0];
                var consumed = 1;
                while (consumed < chain.Count && target.Children.FirstOrDefault(c => c.Name == chain[consumed]) is { } child)
                { target = child; consumed++; }
                return new(target, consumed, matches.Count > 1);
            }
            if (scope is null) return new(null, 0, false);
        }
    }

    private static bool NameIsUse(Entry entry) =>
        entry.Kind is "Modify" or "modify" or "Recognize" || entry.Syntax.Modifiers.Any(m => m.Name.Text == "Assert");

    private static string UseKind(Entry entry) => entry.Kind switch
    {
        "Modify" or "modify" => "modifies",
        "Recognize" => "recognizes",
        _ => "asserts"
    };

    private static string Text(string source, FirmamentV2SourceSpan span) =>
        span.Start >= 0 && span.Start + span.Length <= source.Length ? source.Substring(span.Start, span.Length) : "";

    private static FirmamentOutlineSpan Span(FirmamentV2SourceSpan span) => new(span.Start, span.Length);
}
