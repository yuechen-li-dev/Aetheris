using System.Security.Cryptography;
using System.Text;

namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

/// <summary>Lexical identities for compile-time data. Identifier spans are lowered
/// only after AST lookup, at the existing source boundary to domain adapters.</summary>
internal sealed class FirmamentDataBinding
{
    internal sealed record Symbol(string Name, string Identity, string Namespace, string Scope, DeclarationSyntax Declaration);
    private sealed class Scope(Scope? parent, string identity)
    {
        internal string Identity => identity;
        internal readonly Dictionary<(string Namespace, string Name), Symbol> Symbols = new();
        internal readonly HashSet<string> Values = new(StringComparer.Ordinal);
        internal readonly HashSet<string> Types = new(StringComparer.Ordinal);
        internal Symbol? Resolve(string space, string name) => Symbols.GetValueOrDefault((space, name))
            ?? (space == "value" && Values.Contains(name) || space == "type" && Types.Contains(name) ? null : parent?.Resolve(space, name));
        internal bool HasValueShadow(string name) => Values.Contains(name) || parent?.HasValueShadow(name) == true;
        internal bool HasTypeShadow(string name) => Types.Contains(name) || parent?.HasTypeShadow(name) == true;
    }
    private sealed record Edit(FirmamentV2SourceSpan Span, string Text);
    private readonly List<Edit> edits = [];
    private readonly Dictionary<string, Symbol> identities = new(StringComparer.Ordinal);
    private readonly List<(FirmamentV2SourceSpan Span, Scope Scope)> scopeSpans = [];
    internal string OriginalSource { get; }
    internal string Source { get; }
    internal IReadOnlyList<Symbol> Symbols { get; }
    internal IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics { get; }
    internal bool IsChanged => edits.Count != 0;

    internal FirmamentDataBinding(string source)
    {
        OriginalSource = source;
        Source = source;
        var syntax = FirmamentSyntaxParser.Parse(source);
        if (!syntax.IsSuccess) { Symbols = []; Diagnostics = []; return; } // syntax owner presents recovery failures
        var errors = new List<FirmamentV2ValidationDiagnostic>();
        var symbols = new List<Symbol>();
        var scopes = new Dictionary<StatementSyntax, Scope>(ReferenceEqualityComparer.Instance);
        var definitions = new Dictionary<DeclarationSyntax, Symbol>(ReferenceEqualityComparer.Instance);
        var headers = new Dictionary<DeclarationSyntax, Scope>(ReferenceEqualityComparer.Instance);
        var root = new Scope(null, "document");
        scopeSpans.Add((new(0, source.Length), root));
        var occupied = syntax.Document.Tokens.Where(t => t.Kind == SyntaxTokenKind.Identifier).Select(t => t.Text).ToHashSet(StringComparer.Ordinal);
        Collect(syntax.Document.Statements, root);
        // Preserve all established unique spellings, including root declarations.
        foreach (var group in symbols.GroupBy(s => (s.Namespace, s.Name)))
        {
            var ordered = group.OrderBy(s => s.Scope == "document" ? 0 : 1).ThenBy(s => s.Declaration.Span.Start).ToArray();
            for (var i = 0; i < ordered.Length; i++)
            {
                var symbol = ordered[i];
                var identity = symbol.Name;
                if (i > 0)
                {
                    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(symbol.Scope + "/" + symbol.Namespace + "/" + symbol.Name)))[..12];
                    identity = "__firmamentData_" + hash + "_" + symbol.Name;
                    while (!occupied.Add(identity)) identity += "_";
                }
                var bound = symbol with { Identity = identity };
                scopes[symbol.Declaration].Symbols[(symbol.Namespace, symbol.Name)] = bound;
                definitions[symbol.Declaration] = bound;
                identities[identity] = bound;
            }
        }
        Rewrite(syntax.Document.Statements);
        Symbols = definitions.Values.OrderBy(s => s.Declaration.Span.Start).ToArray();
        Diagnostics = errors;
        edits.Sort((a, b) => a.Span.Start.CompareTo(b.Span.Start));
        for (var i = edits.Count - 1; i >= 0; i--)
            Source = Source.Remove(edits[i].Span.Start, edits[i].Span.Length).Insert(edits[i].Span.Start, edits[i].Text);

        void Collect(IReadOnlyList<StatementSyntax> statements, Scope scope)
        {
            foreach (var statement in statements)
            {
                scopes[statement] = scope;
                if (statement is not DeclarationSyntax { Name: { } name } d) continue;
                if (d.Type.Name.Text == "Record" && d.Modifiers.Any(m => m.Name.Text == "Inventory"))
                {
                    Error("firmament-module-context-required", $"Inventory Record '{name.Text}' requires an Assembly or Scene project loader. Reference this module from a consuming project; it is not an ordinary data Record.", d.Span, name.Text, "Module binding");
                    continue;
                }
                var space = d.Type.Name.Text is "Record" or "Enum" ? "type"
                    : d.Type.Name.Text == "Static" || d.Type.Name.Text == "Table" && d.Modifiers.Any(m => m.Name.Text == "Static") ? "value" : null;
                if (space is null) { scope.Values.Add(name.Text); continue; }
                var table = d.Type.Name.Text == "Table";
                if (d.HasParameterList || d.Type.Span.Length != d.Type.Name.Span.Length || d.Occurrence
                    || d.ResultType is not null || d.Clauses.Any(c => !table || c.Kind != ClauseKind.Key)
                    || d.Clauses.Count(c => c.Kind == ClauseKind.Key) > 1
                    || d.Modifiers.Any(m => !table || m.Name.Text != "Static"))
                {
                    Error("firmament-binding-invalid-data-header:" + name.Text, $"{d.Type.Name.Text} '{name.Text}' cannot carry ignored parameters, placement, generic or result clauses.", d.Span, name.Text);
                    continue;
                }
                var symbol = new Symbol(name.Text, name.Text, space, scope.Identity, d);
                if (!scope.Symbols.TryAdd((space, name.Text), symbol))
                    Error("firmament-binding-duplicate-name:" + name.Text, $"Compile-time {space} '{name.Text}' is declared more than once in scope '{scope.Identity}'.", d.Span, name.Text);
                else { symbols.Add(symbol); definitions[d] = symbol; }
            }
            var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var statement in statements)
            {
                if (statement is StatementBlockSyntax block) { Collect(block.Body.Statements, scope); continue; }
                if (statement is MappingSyntax m)
                {
                    var child = new Scope(scope, scope.Identity + "/mapping " + m.Key.Text); child.Values.Add(m.Key.Text);
                    Collect([m.Value], child); continue;
                }
                if (statement is not DeclarationSyntax d || d.Body is null) continue;
                var label = d.Type.Name.Text + " " + d.Name?.Text;
                var ordinal = ordinals.GetValueOrDefault(label); ordinals[label] = ordinal + 1;
                var header = new Scope(scope, scope.Identity + "/parameters " + label + "#" + ordinal);
                foreach (var p in d.Parameters.Concat(d.Modifiers.Where(m => m.Name.Text == "Template").SelectMany(m => m.Arguments)))
                    if (p.Name is not null)
                    {
                        if (p.Value is TypeParameterSyntax) header.Types.Add(p.Name.Text);
                        else header.Values.Add(p.Name.Text);
                    }
                headers[d] = header;
                var body = new Scope(header, scope.Identity + "/" + label + "#" + ordinal);
                scopeSpans.Add((d.Body.ContentSpan, body));
                Collect(d.Body.Statements, body);
            }
        }
        void Rewrite(IEnumerable<StatementSyntax> statements)
        {
            foreach (var statement in statements)
            {
                var scope = scopes[statement];
                if (statement is StatementBlockSyntax block) { Rewrite(block.Body.Statements); continue; }
                if (statement is MappingSyntax m) { Rewrite([m.Value]); continue; }
                if (statement is FieldSyntax f) { Expression(f.Value, scope, false); continue; }
                if (statement is ExpressionStatementSyntax e) { Expression(e.Value, scope, false); continue; }
                if (statement is ReturnSyntax r) { Expression(r.Value, scope, false); continue; }
                if (statement is not DeclarationSyntax d) continue;
                if (definitions.TryGetValue(d, out var symbol)) Rename(d.Name!, symbol.Identity);
                var parameters = d.Parameters.Concat(d.Modifiers.Where(m => m.Name.Text == "Template").SelectMany(m => m.Arguments)).ToArray();
                foreach (var p in parameters)
                {
                    Expression(p.Value, headers.GetValueOrDefault(d) ?? scope, true);
                    if (p.Default is not null) Expression(p.Default, headers.GetValueOrDefault(d) ?? scope, false);
                }
                if (d.Annotation is not null) Expression(d.Annotation, scope, true);
                if (d.ResultType is not null) Expression(d.ResultType, headers.GetValueOrDefault(d) ?? scope, true);
                if (d.Initializer is not null) Expression(d.Initializer, scope, false);
                foreach (var clause in d.Clauses.Where(c => c.Kind != ClauseKind.Key)) Expression(clause.Value, scope, false);
                if (d.Body is not null)
                {
                    if (d.Type.Name.Text == "Record")
                        foreach (var field in d.Body.Statements.OfType<FieldSyntax>()) Expression(field.Value, scope, true);
                    else Rewrite(d.Body.Statements);
                }
            }
        }
        void Expression(ExpressionSyntax expression, Scope scope, bool type)
        {
            switch (expression)
            {
                case ReferenceSyntax r:
                    var space = type ? "type" : "value";
                    var symbol = scope.Resolve(space, r.Name.Text);
                    if (symbol is not null) Rename(r.Name, symbol.Identity);
                    else if (!(space == "value" ? scope.HasValueShadow(r.Name.Text) : scope.HasTypeShadow(r.Name.Text)) && symbols.Any(s => s.Namespace == space && s.Name == r.Name.Text))
                        Error("firmament-binding-name-not-visible:" + r.Name.Text, $"Compile-time {space} '{r.Name.Text}' is not visible from scope '{scope.Identity}'.", r.Span, r.Name.Text);
                    return;
                case ObjectSyntax o:
                    if (o.Type is not null) Expression(o.Type, scope, true);
                    foreach (var f in o.Body.Statements.OfType<FieldSyntax>()) Expression(f.Value, scope, false);
                    return;
                case SpecializationSyntax s:
                    foreach (var a in s.Arguments) Expression(a.Value, scope, type);
                    return;
                case CallSyntax c:
                    foreach (var a in c.Arguments) Expression(a.Value, scope, false);
                    if (c.Target is not ReferenceSyntax) Expression(c.Target, scope, false);
                    return;
                case MemberSyntax m: Expression(m.Target, scope, type); return;
                case WithSyntax w:
                    Expression(w.Target, scope, false);
                    foreach (var f in w.Body.Statements.OfType<FieldSyntax>()) Expression(f.Value, scope, false);
                    return;
                case SequenceSyntax s: foreach (var a in s.Items) Expression(a.Value, scope, type); return;
                case TypeParameterSyntax: return;
                case UnarySyntax u: Expression(u.Operand, scope, type); return;
                case BinarySyntax b: Expression(b.Left, scope, type); Expression(b.Right, scope, type); return;
                case QuantitySyntax q: Expression(q.Value, scope, false); return;
                case IndexSyntax i: Expression(i.Target, scope, type); if (i.Index is not null) Expression(i.Index, scope, false); return;
                case TypedValueSyntax t: Expression(t.Type, scope, true); Expression(t.Value, scope, false); return;
                case ToleranceSyntax t:
                    Expression(t.Nominal, scope, false); Expression(t.Plus, scope, false); if (t.Minus is not null) Expression(t.Minus, scope, false); return;
                case MatchSyntax m: Expression(m.Scrutinee, scope, false); foreach (var a in m.Arms) Expression(a.Value, scope, false); return;
                case AliasSyntax a: Expression(a.Value, scope, type); return;
                case ProvenanceSyntax p: Expression(p.Value, scope, type); return;
                case FrameSyntax f:
                    Expression(f.Origin, scope, false); foreach (var a in f.Axes) Expression(a.Value, scope, false); return;
                case MemberSetSyntax m:
                    Expression(m.Target, scope, false); foreach (var a in m.Members) Expression(a.Value, scope, false); return;
                case TraceSyntax t:
                    Expression(t.Source, scope, false); if (t.From is not null) Expression(t.From, scope, false); if (t.To is not null) Expression(t.To, scope, false); return;
            }
        }
        void Rename(IdentifierSyntax name, string text)
        {
            if (name.Text != text && !edits.Any(e => e.Span == name.Span)) edits.Add(new(name.Span, text));
        }
        void Error(string code, string message, FirmamentV2SourceSpan span, string name, string stage = "Data binding") => errors.Add(new(code, "fatal", message, Target: name)
            { SourceSpan = span, IsOriginalSource = true, Stage = stage });
    }

    internal string DisplayName(string identity) => identities.TryGetValue(identity, out var symbol) ? symbol.Name : identity;
    internal string ResolveIdentity(string space, string name, int position) => scopeSpans
        .Where(s => position >= s.Span.Start && position < s.Span.Start + s.Span.Length).OrderBy(s => s.Span.Length)
        .FirstOrDefault().Scope?.Resolve(space, name)?.Identity ?? name;
    internal FirmamentV2SourceSpan OriginalSpan(FirmamentV2SourceSpan span)
    {
        int Position(int position)
        {
            var delta = 0;
            foreach (var edit in edits)
            {
                var start = edit.Span.Start + delta;
                if (position < start) break;
                if (position <= start + edit.Text.Length) return edit.Span.Start + Math.Min(position - start, edit.Span.Length);
                delta += edit.Text.Length - edit.Span.Length;
            }
            return position - delta;
        }
        var start = Position(span.Start); return new(start, Math.Max(0, Position(span.Start + span.Length) - start));
    }
}
