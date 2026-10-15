using System.Collections.Immutable;
using System.Globalization;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Scoped, finite AST specialization of pure terminal-return Features.
/// Source serialization is the boundary to domain adapters not yet migrated to AST inputs.</summary>
internal static class FirmamentV2FeatureExpansion
{
    internal const string Prefix = "firmament-feature-";
    internal const string DuplicateName = Prefix + "duplicate-name";
    internal const string Unknown = Prefix + "unknown";
    internal const string DuplicateParameter = Prefix + "duplicate-parameter";
    internal const string InvalidParameter = Prefix + "invalid-parameter";
    internal const string ArgumentCount = Prefix + "argument-count";
    internal const string UnknownArgument = Prefix + "unknown-argument";
    internal const string DuplicateArgument = Prefix + "duplicate-argument";
    internal const string ArgumentType = Prefix + "argument-type";
    internal const string MissingReturn = Prefix + "missing-return";
    internal const string MultipleReturns = Prefix + "multiple-returns";
    internal const string ReturnType = Prefix + "return-type";
    internal const string ControlFlowUnsupported = Prefix + "control-flow-unsupported";
    internal const string Recursion = Prefix + "recursion";
    internal const string LocalType = Prefix + "local-type";
    internal const string LocalDuplicate = Prefix + "local-duplicate";
    internal const string UnsupportedBody = Prefix + "body-unsupported";
    internal const int MaximumDepth = 64;
    internal const int MaximumInvocations = 4096;
    internal const int MaximumOutputCharacters = 4 * 1024 * 1024;

    private sealed record Definition(DeclarationSyntax Syntax, FirmamentBindingScope Scope, string ResultType,
        ReturnSyntax Return, IReadOnlyList<DeclarationSyntax> Locals, IReadOnlyList<DeclarationSyntax> Construction);
    internal sealed record Result(string Source, IReadOnlyList<FirmamentV2FeatureDefinition> Definitions,
        IReadOnlyList<FirmamentV2FeatureInvocation> Invocations, FirmamentV2FeatureExpansionMetrics Metrics)
    {
        internal DocumentSyntax? Document { get; init; }
        internal IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics { get; init; } = [];
    }

    internal static Result? Expand(string source, List<string> diagnostics, FirmamentCompileTimeValues? scalars = null,
        string? originalSource = null)
    {
        var parsed = FirmamentSyntaxParser.Parse(source);
        var declarations = FirmamentSyntaxTraversal.Declarations(parsed.Document)
            .Where(d => d.Type.Name.Text == "Feature" && d.ResultType is not null).ToArray();
        if (declarations.Length == 0) return new(source, [], [], new(0, 0, 0, "BoundedSourceOrder")) { Document = parsed.Document };
        var located = new List<FirmamentV2ValidationDiagnostic>();
        var original = FirmamentSyntaxParser.Parse(originalSource ?? source).Document;
        var origins = new FirmamentSyntaxOrigins(original, parsed.Document);
        FirmamentV2SourceSpan? OriginalDefinition(DeclarationSyntax d)
        {
            if (originalSource is null || originalSource == source) return d.Span;
            return origins.Find(d.Span);
        }
        void Error(string code, string message, FirmamentV2SourceSpan span, DeclarationSyntax? definition = null)
        {
            diagnostics.Add(code);
            var invocation = origins.Find(span);
            var anchor = invocation ?? (definition is null ? null : OriginalDefinition(definition));
            var unchanged = originalSource is null || originalSource == source;
            located.Add(new(code, "fatal", message, Target: definition?.Name?.Text)
            {
                SourceFile = unchanged || anchor is not null ? "inline" : "inline#pre-feature-expansion",
                SourceSpan = unchanged ? span : anchor ?? span, IsOriginalSource = unchanged || anchor is not null, Stage = "Feature binding",
                LocationNote = anchor is not null && !unchanged ? invocation is not null ? "Location identifies the original Feature use." : "Location identifies the original Feature definition; its invocation is in expanded source."
                    : unchanged ? null : "Location is in the input to Feature expansion; original-file mapping is unavailable."
            });
        }
        Result? Failure()
        {
            scalars?.AddExpansionDiagnostics(located);
            return null;
        }
        bool Failed() => located.Count > 0 || diagnostics.Any(d => d.StartsWith(Prefix, StringComparison.Ordinal));
        if (!parsed.IsSuccess)
        {
            foreach (var d in parsed.Diagnostics) { diagnostics.Add(d.Code); located.Add(d); }
            return Failure();
        }
        var scopes = new Dictionary<DeclarationSyntax, FirmamentBindingScope>(ReferenceEqualityComparer.Instance);
        var bodies = new Dictionary<BlockSyntax, FirmamentBindingScope>(ReferenceEqualityComparer.Instance);
        var definitions = new Dictionary<DeclarationSyntax, Definition>(ReferenceEqualityComparer.Instance);
        var root = new FirmamentBindingScope(null, "document");
        void Collect(IReadOnlyList<StatementSyntax> statements, FirmamentBindingScope scope)
        {
            foreach (var mapping in statements.OfType<MappingSyntax>()) Collect([mapping.Value], scope);
            foreach (var d in statements.OfType<DeclarationSyntax>())
            {
                scopes[d] = scope;
                var kind = d.Type.Name.Text == "Feature" && d.ResultType is not null ? "Feature" : d.Type.Name.Text;
                if (d.Name is not null && !scope.Declare(kind, d) && kind == "Feature")
                    Error(DuplicateName + ":" + d.Name.Text, $"Duplicate Feature '{d.Name.Text}' in {scope.Identity}.", d.Span, d);
            }
            foreach (var d in statements.OfType<DeclarationSyntax>().Where(d => d.Body is not null))
            {
                var child = new FirmamentBindingScope(scope, scope.Identity + "/" + d.Type.Name.Text + " " + d.Name?.Text);
                bodies[d.Body!] = child; Collect(d.Body!.Statements, child);
            }
        }
        Collect(parsed.Document.Statements, root);
        foreach (var d in declarations)
        {
            var name = d.Name?.Text ?? "<unnamed>"; var resultType = TypeName(d.ResultType!);
            if (!AllowedResult(resultType)) Error(ReturnType + $":{name}:unsupported-{resultType}", $"Feature '{name}' cannot return {resultType}; expected a supported Hole variant, Boss, Pocket, EdgeFinish or Profile.", d.Span, d);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in d.Parameters)
                if (p.Name is null || p.Value is not ReferenceSyntax)
                    Error(InvalidParameter + ":" + name, $"Feature '{name}' expects named typed parameters.", p.Span, d);
                else if (!names.Add(p.Name.Text)) Error(DuplicateParameter + $":{name}:{p.Name.Text}", $"Duplicate parameter '{p.Name.Text}' in Feature '{name}'.", p.Span, d);
            if (d.Body is null || d.Name is null || d.Annotation is not null || d.Clauses.Count > 0 || d.Modifiers.Count > 0 || d.Initializer is not null
                || d.Type.Span.Length > d.Type.Name.Span.Length || d.Occurrence || d.Target is not null || !d.HasParameterList)
            { Error(UnsupportedBody + ":" + name, $"Feature '{name}' expects a non-generic name(parameters) definition, a body and terminal return; header placement and initialization clauses are not admitted.", d.Span, d); continue; }
            var returns = d.Body.Statements.OfType<ReturnSyntax>().ToArray();
            if (returns.Length != 1)
            { Error((returns.Length == 0 ? MissingReturn : MultipleReturns) + ":" + name, $"Feature '{name}' must have exactly one terminal return.", d.Span, d); continue; }
            if (!ReferenceEquals(returns[0], d.Body.Statements.Last()))
                Error(UnsupportedBody + ":" + name + ":return-must-be-terminal", $"Feature '{name}' has statements after its return.", returns[0].Span, d);
            var locals = new List<DeclarationSyntax>(); var construction = new List<DeclarationSyntax>();
            foreach (var statement in d.Body.Statements.Where(s => s is not ReturnSyntax))
            {
                if (statement is DeclarationSyntax { Type.Name.Text: "let", Name: not null, Annotation: ReferenceSyntax, Initializer: not null, Body: null,
                    Parameters.Count: 0, Modifiers.Count: 0, Clauses.Count: 0, ResultType: null, Occurrence: false, HasParameterList: false } local
                    && local.Type.Span.Length == local.Type.Name.Span.Length) locals.Add(local);
                else if (statement is DeclarationSyntax { Body: not null, Name: not null, HasParameterList: false } c && c.Type.Name.Text is "Point2" or "Line2" or "CubicBezier2" or "Circle2" or "RoundedRect2" or "Rect2" or "Profile") construction.Add(c);
                else { Error(UnsupportedBody + ":" + name, $"Unsupported statement in Feature '{name}'; use typed immutable let values and admitted private construction before return.", statement.Span, d); continue; }
                var localName = ((DeclarationSyntax)statement).Name!.Text;
                if (!names.Add(localName)) Error(LocalDuplicate + $":{name}:{localName}", $"Duplicate local name '{localName}' in Feature '{name}'.", statement.Span, d);
            }
            definitions[d] = new(d, scopes[d], resultType, returns[0], locals, construction);
        }
        foreach (var definition in definitions.Values)
        {
            var returned = definition.Return.Value;
            if (returned is ObjectSyntax { Name: not null })
                Error(UnsupportedBody + ":" + definition.Syntax.Name!.Text + ":named-return", "A return constructor is unnamed. Name its consuming invocation with Feature name = Recipe(arguments).", returned.Span, definition.Syntax);
            else if (returned is CallSyntax { Target: ReferenceSyntax call })
            {
                if (definition.Scope.Resolve("Feature", call.Name.Text) is not { } target || !definitions.TryGetValue(target, out var nested))
                    Error(Unknown + ":" + call.Name.Text, $"Feature '{call.Name.Text}' is not visible from definition '{definition.Syntax.Name!.Text}'.", returned.Span, definition.Syntax);
                else if (!SameType(definition.ResultType, nested.ResultType))
                    Error(ReturnType + $":{definition.Syntax.Name!.Text}:expected-{definition.ResultType}:actual-{nested.ResultType}", "Delegated Feature result does not match the declared return type.", returned.Span, definition.Syntax);
            }
            else if (returned is not ObjectSyntax { Type: not null, Name: null } obj || !SameType(definition.ResultType, TypeName(obj.Type)))
                Error(ReturnType + $":{definition.Syntax.Name!.Text}:expected-{definition.ResultType}", $"Feature '{definition.Syntax.Name!.Text}' must return {definition.ResultType} construction or a Feature with that result type.", returned.Span, definition.Syntax);
        }
        var checkedDefinitions = new HashSet<DeclarationSyntax>(ReferenceEqualityComparer.Instance);
        void CheckCycles(Definition definition, List<Definition> stack)
        {
            if (stack.Contains(definition))
            { Error(Recursion + ":" + string.Join(":", stack.SkipWhile(d => d != definition).Append(definition).Select(d => d.Syntax.Name!.Text)), "Feature calls contain a recursive dependency. Use a finite, acyclic recipe.", definition.Return.Span, definition.Syntax); return; }
            if (stack.Count >= MaximumDepth)
            { Error(Prefix + "depth-limit", $"Feature specialization exceeds depth {MaximumDepth}.", definition.Syntax.Span, definition.Syntax); return; }
            if (!checkedDefinitions.Add(definition.Syntax)) return;
            if (definition.Return.Value is CallSyntax { Target: ReferenceSyntax r }
                && definition.Scope.Resolve("Feature", r.Name.Text) is { } target && definitions.TryGetValue(target, out var nested)) CheckCycles(nested, [.. stack, definition]);
        }
        foreach (var definition in definitions.Values) CheckCycles(definition, []);
        if (Failed()) return Failure();
        var invocations = new List<FirmamentV2FeatureInvocation>(); var ordinal = 0;
        long emittedCharacters = 0;
        var emittedDeclarations = 0;
        var changes = new List<(FirmamentV2SourceSpan Span, string Text)>();
        var replacements = new Dictionary<StatementSyntax, IReadOnlyList<StatementSyntax>>(ReferenceEqualityComparer.Instance);

        IReadOnlyList<StatementSyntax> ExpandCall(Definition definition, CallSyntax call, FirmamentBindingScope callScope,
            IReadOnlyList<Definition> stack, bool pattern, string? resultName = null)
        {
            if (stack.Contains(definition)) { Error(Recursion + ":" + string.Join(":", stack.Append(definition).Select(d => d.Syntax.Name!.Text)), "Recursive Feature invocation is not admitted.", call.Span, definition.Syntax); return []; }
            if (stack.Count >= MaximumDepth || ordinal >= MaximumInvocations)
            { Error(Prefix + "expansion-limit", $"Feature expansion is limited to depth {MaximumDepth} and {MaximumInvocations} invocations; no output was truncated.", call.Span, definition.Syntax); return []; }
            var invocationOrdinal = ordinal++;
            var environment = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
            var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
            var supplied = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal); var position = 0;
            foreach (var arg in call.Arguments)
            {
                var name = arg.Name?.Text ?? (position < definition.Syntax.Parameters.Count ? definition.Syntax.Parameters[position++].Name!.Text : null);
                if (name is null) { Error(ArgumentCount + ":" + definition.Syntax.Name!.Text, "Too many positional Feature arguments.", arg.Span, definition.Syntax); continue; }
                if (!definition.Syntax.Parameters.Any(p => p.Name?.Text == name)) { Error(UnknownArgument + $":{definition.Syntax.Name!.Text}:{name}", $"Unknown Feature argument '{name}'.", arg.Span, definition.Syntax); continue; }
                if (!supplied.TryAdd(name, arg.Value)) Error(DuplicateArgument + $":{definition.Syntax.Name!.Text}:{name}", $"Duplicate Feature argument '{name}'.", arg.Span, definition.Syntax);
            }
            bool BindValue(ExpressionSyntax expression, string type, string name, FirmamentV2SourceSpan span, out ExpressionSyntax bound)
            {
                bound = expression; var text = FirmamentSyntaxWriter.Expression(expression);
                if (scalars?.IsRecordType(type) == true)
                {
                    if (!scalars.TryRecordTransfer(text, type, $"Feature {definition.Syntax.Name!.Text}.{name}", call.Span, diagnostics, out var fields))
                    { Error(ArgumentType + $":{definition.Syntax.Name!.Text}:{name}:expected-{type}", $"Feature input '{name}' requires Record {type}.", span, definition.Syntax); return false; }
                    foreach (var field in fields)
                    {
                        environment[name + "." + field.Key] = ParseValue(field.Value);
                        bindings[name + "." + field.Key] = field.Value;
                    }
                    return true;
                }
                if (scalars?.IsScalar(type) == true && scalars.TryTransfer(text, type, $"Feature {definition.Syntax.Name!.Text}.{name}", call.Span, diagnostics, out var transferred))
                { bound = ParseValue(transferred); return true; }
                if (!Matches(expression, type))
                { Error(ArgumentType + $":{definition.Syntax.Name!.Text}:{name}:expected-{type}", $"Feature input '{name}' requires {type}; supplied {text}.", span, definition.Syntax); return false; }
                bound = Normalize(expression, type); return true;
            }
            var resolving = new HashSet<string>(StringComparer.Ordinal);
            void BindParameter(ArgumentSyntax p)
            {
                var name = p.Name!.Text;
                if (environment.ContainsKey(name)) return;
                if (resolving.Count >= MaximumDepth)
                { Error(Prefix + "default-depth-limit:" + name, $"Feature defaults exceed dependency depth {MaximumDepth}.", p.Span, definition.Syntax); return; }
                if (!resolving.Add(name)) { Error(Prefix + "default-cycle:" + name, $"Feature default for '{name}' contains a dependency cycle.", p.Span, definition.Syntax); return; }
                var explicitArgument = supplied.TryGetValue(name, out var value);
                if (!explicitArgument)
                {
                    if (p.Default is null) { Error(ArgumentCount + $":{definition.Syntax.Name!.Text}:missing-{name}", $"Missing required Feature argument '{name}'.", call.Span, definition.Syntax); resolving.Remove(name); return; }
                    foreach (var dependency in FirmamentSyntaxTraversal.Expressions(p.Default).OfType<ReferenceSyntax>())
                        if (definition.Syntax.Parameters.FirstOrDefault(x => x.Name!.Text == dependency.Name.Text) is { } other) BindParameter(other);
                    value = new FirmamentSyntaxSubstitution(environment).Expression(p.Default);
                }
                var type = TypeName(p.Value);
                if (type is "Point2" or "Point3") value = ResolvePoint(value!, explicitArgument ? callScope : definition.Scope);
                if (BindValue(value!, type, name, p.Span, out var bound)) { environment[name] = bound; bindings[name] = FirmamentSyntaxWriter.Expression(bound); }
                resolving.Remove(name);
            }
            foreach (var p in definition.Syntax.Parameters) BindParameter(p);
            foreach (var local in definition.Locals)
            {
                var expression = new FirmamentSyntaxSubstitution(environment).Expression(local.Initializer!); var type = TypeName(local.Annotation!);
                if (BindValue(expression, type, local.Name!.Text, local.Span, out var bound)) environment[local.Name.Text] = bound;
                else Error(LocalType + $":{definition.Syntax.Name!.Text}:{local.Name.Text}:expected-{type}", $"Feature local '{local.Name.Text}' requires {type}.", local.Span, definition.Syntax);
            }
            if (Failed()) return [];
            var prefix = resultName is null ? definition.Syntax.Name!.Text + "__" + invocationOrdinal.ToString(CultureInfo.InvariantCulture)
                : stack.Count == 0 ? resultName : resultName + "__Call" + invocationOrdinal.ToString(CultureInfo.InvariantCulture);
            var renames = definition.Construction.ToDictionary(d => d.Name!.Text, d => prefix + "__" + d.Name!.Text, StringComparer.Ordinal);
            var transform = new FirmamentSyntaxSubstitution(environment, renames);
            var output = definition.Construction.Select(transform.Statement).ToList(); var returned = transform.Expression(definition.Return.Value);
            if (returned is CallSyntax { Target: ReferenceSyntax nestedName } nestedCall
                && definition.Scope.Resolve("Feature", nestedName.Name.Text) is { } nestedSyntax && definitions.TryGetValue(nestedSyntax, out var nested))
            {
                if (!SameType(definition.ResultType, nested.ResultType)) Error(ReturnType + $":{definition.Syntax.Name!.Text}:expected-{definition.ResultType}:actual-{nested.ResultType}", "Delegated Feature result does not match the declared return type.", definition.Return.Span, definition.Syntax);
                else output.AddRange(ExpandCall(nested, nestedCall, definition.Scope, stack.Append(definition).ToArray(), pattern, resultName));
            }
            else if (returned is ObjectSyntax { Type: not null, Name: null } obj && SameType(definition.ResultType, TypeName(obj.Type)))
            {
                var generated = resultName ?? (pattern ? PatternItemName(obj, definition.Syntax.Name!.Text, invocationOrdinal) : definition.Syntax.Name!.Text + "__" + invocationOrdinal.ToString(CultureInfo.InvariantCulture));
                output.Add(new DeclarationSyntax(ToType(obj.Type), new(generated, call.Span), [], [], null, null, [], null, obj.Body, false, definition.Return.Span));
            }
            else Error(ReturnType + $":{definition.Syntax.Name!.Text}:expected-{definition.ResultType}", $"Feature '{definition.Syntax.Name!.Text}' must return {definition.ResultType} construction or a Feature with that result type.", definition.Return.Span, definition.Syntax);
            invocations.Add(new(definition.Syntax.Name!.Text, invocationOrdinal, definition.ResultType, bindings.ToImmutableDictionary(StringComparer.Ordinal), call.Span, definition.ResultType, "ExpandedBeforeFeatureAir", resultName)
            {
                BindingScope = definition.Scope.Identity,
                DefinitionOrigin = new(OriginalDefinition(definition.Syntax) is null ? "inline#pre-feature-expansion" : "inline", OriginalDefinition(definition.Syntax) ?? definition.Syntax.Span, definition.Syntax.Name.Text,
                    OriginalDefinition(definition.Syntax) is null ? "Definition is in expanded input; original-file mapping unavailable." : null),
                InvocationOrigin = new(origins.Find(call.Span) is null ? "inline#pre-feature-expansion" : "inline", origins.Find(call.Span) ?? call.Span, definition.Syntax.Name.Text,
                    origins.Find(call.Span) is null ? "Invocation offsets refer to the AST input of Feature expansion; original include/Template mapping is not yet available." : null),
                ExpansionChain = stack.Select(d => d.Syntax.Name!.Text).Append(definition.Syntax.Name.Text).ToArray()
            });
            // Account only this invocation's own nodes; delegated nodes were already
            // charged by their owner. Reject before retaining another large expansion.
            foreach (var node in output.Take(definition.Construction.Count).Concat(returned is ObjectSyntax ? output.TakeLast(1) : []))
            {
                emittedCharacters += FirmamentSyntaxWriter.Statement(node).Length;
                emittedDeclarations += FirmamentSyntaxTraversal.Declarations(node).Count();
                if (emittedCharacters > MaximumOutputCharacters || emittedDeclarations > 65536)
                { Error(Prefix + "output-limit", "Feature expansion exceeds 4 MiB or 65,536 declarations; no construction was truncated.", call.Span, definition.Syntax); return []; }
            }
            return output;
        }
        void Visit(IReadOnlyList<StatementSyntax> statements, FirmamentBindingScope scope, bool pattern)
        {
            foreach (var statement in statements)
            {
                if (statement is DeclarationSyntax d && definitions.ContainsKey(d)) { changes.Add((d.Span, "")); replacements[d] = []; continue; }
                var call = statement switch { ExpressionStatementSyntax { Value: CallSyntax c } => c,
                    DeclarationSyntax { Type.Name.Text: "Feature", Initializer: CallSyntax c, ResultType: null } => c, _ => null };
                if (call?.Target is ReferenceSyntax target)
                {
                    if (statement is DeclarationSyntax named && (named.Name is null || named.HasParameterList || named.Type.Span.Length > named.Type.Name.Span.Length
                        || named.Modifiers.Count > 0 || named.Clauses.Count > 0 || named.Annotation is not null || named.Body is not null || named.Occurrence))
                    { Error(UnsupportedBody + ":invocation-header", "A named invocation is Feature name = Recipe(arguments), without parameter, type, placement or body clauses.", named.Span); continue; }
                    if (scope.Resolve("Feature", target.Name.Text) is { } declaration && definitions.TryGetValue(declaration, out var definition))
                    {
                        var nodes = ExpandCall(definition, call, scope, [], pattern, (statement as DeclarationSyntax)?.Name?.Text);
                        changes.Add((statement.Span, FirmamentSyntaxWriter.Statements(nodes))); replacements[statement] = nodes;
                    }
                    else Error(Unknown + ":" + target.Name.Text, $"Feature '{target.Name.Text}' is not visible in {scope.Identity}.", call.Span);
                }
                // Static mappings contain values (Point2 constructors, for example),
                // not statements invoking a Feature. Their owner binds those values.
                else if (statement is DeclarationSyntax { Type.Name.Text: "Static" or "Table" or "Record" or "Enum" }) continue;
                else if (statement is DeclarationSyntax { Body: not null } owner) Visit(owner.Body.Statements, bodies[owner.Body], pattern || owner.Type.Name.Text == "Pattern");
                else if (statement is MappingSyntax mapping) Visit([mapping.Value], scope, true);
            }
        }
        Visit(parsed.Document.Statements, root, false);
        if (Failed()) return Failure();
        if (source.Length + changes.Sum(c => (long)c.Text.Length - c.Span.Length) > MaximumOutputCharacters)
        { Error(Prefix + "output-limit", $"Feature output exceeds {MaximumOutputCharacters} characters; no construction was truncated.", parsed.Document.Span); return Failure(); }
        var projected = new ExpansionProjection(replacements).Rewrite(parsed.Document);
        foreach (var change in changes.OrderByDescending(c => c.Span.Start)) source = source.Remove(change.Span.Start, change.Span.Length).Insert(change.Span.Start, change.Text);
        var summaries = definitions.Values.Select(d => new FirmamentV2FeatureDefinition(d.Syntax.Name!.Text,
            d.Syntax.Parameters.Select(p => new FirmamentV2FeatureParameter(p.Name!.Text, TypeName(p.Value), p.Default is null ? null : FirmamentSyntaxWriter.Expression(p.Default))).ToArray(),
            d.ResultType, d.Syntax.Span, d.Scope.Identity, "PureTerminalReturn")).ToArray();
        return new(source, summaries, invocations.OrderBy(i => i.Ordinal).ToArray(), new(summaries.Length, invocations.Count, emittedDeclarations, "BoundedSourceOrder")) { Document = projected, Diagnostics = located };
    }

    private sealed class ExpansionProjection(IReadOnlyDictionary<StatementSyntax, IReadOnlyList<StatementSyntax>> replacements) : FirmamentSyntaxRewriter
    {
        internal DocumentSyntax Rewrite(DocumentSyntax document) => document with { Statements = Project(document.Statements) };
        private StatementSyntax[] Project(IReadOnlyList<StatementSyntax> statements) => statements.SelectMany(s => replacements.TryGetValue(s, out var r) ? r : [Statement(s)]).ToArray();
        internal override BlockSyntax Block(BlockSyntax block) => block with { Statements = Project(block.Statements) };
        internal override StatementSyntax Statement(StatementSyntax statement) => statement is MappingSyntax m && replacements.TryGetValue(m.Value, out var nodes) && nodes.Count == 1
            ? m with { Value = nodes[0] } : base.Statement(statement);
    }
    private static TypeSyntax ToType(ExpressionSyntax e) => e switch
    {
        ReferenceSyntax r => new(r.Name, [], r.Span),
        SpecializationSyntax { Target: ReferenceSyntax r } s => new(r.Name, s.Arguments, s.Span),
        _ => throw new InvalidOperationException("A checked Feature return must have a constructor type.")
    };
    private static string TypeName(ExpressionSyntax e) => FirmamentSyntaxWriter.Expression(e).Replace(" ", "", StringComparison.Ordinal);
    private static bool SameType(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static bool AllowedResult(string type) => type is "Hole<Shaft>" or "Hole<Counterbore>" or "Hole<Countersink>" or "Boss" or "Pocket" or "EdgeFinish" or "Profile";
    private static ExpressionSyntax ParseValue(string source) => FirmamentSyntaxParser.ParseExpression(source).Expression ?? throw new InvalidOperationException("A checked compile-time value did not serialize as a Firmament expression.");
    private static ExpressionSyntax ResolvePoint(ExpressionSyntax expression, FirmamentBindingScope scope)
    {
        DeclarationSyntax? point = expression switch
        {
            ReferenceSyntax r => scope.Resolve("Point2", r.Name.Text) ?? scope.Resolve("Point3", r.Name.Text),
            MemberSyntax { Target: ReferenceSyntax owner } member when scope.Resolve("Struct", owner.Name.Text)?.Body is { } body
                => body.Statements.OfType<DeclarationSyntax>().FirstOrDefault(d => d.Name?.Text == member.Member.Text && d.Type.Name.Text is "Point2" or "Point3"),
            _ => null
        };
        if (point?.Body?.Statements.OfType<FieldSyntax>().FirstOrDefault(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Position"))?.Value is not SequenceSyntax position) return expression;
        return new CallSyntax(new ReferenceSyntax(point.Type.Name, point.Type.Span), position.Items, expression.Span);
    }
    private static bool Matches(ExpressionSyntax e, string type)
    {
        if (type is "FaceId" or "EdgeId" or "Body") return false;
        if (type is "Length" or "Angle" or "Int" or "Float") return FirmamentNumericEvaluation.TryEvaluate(e, out var n)
            && n.Unit == (type == "Length" ? "mm" : type == "Angle" ? "deg" : "") && (type != "Int" || n.Number == Math.Truncate(n.Number));
        if (type == "Bool") return e is LiteralSyntax { Kind: LiteralKind.Boolean };
        if (type == "String") return e is LiteralSyntax { Kind: LiteralKind.String };
        if (type is "Point2" or "Point3") return FirmamentSyntaxValues.Vector(e, "mm", out var components) && components.Length == (type == "Point2" ? 2 : 3) || e is ReferenceSyntax or MemberSyntax or IndexSyntax;
        return e is ReferenceSyntax or MemberSyntax or IndexSyntax;
    }
    private static ExpressionSyntax Normalize(ExpressionSyntax e, string type)
    {
        if (type is "Length" or "Angle" or "Int" or "Float" && FirmamentNumericEvaluation.TryEvaluate(e, out var n)) return ParseValue(n.Number.ToString("R", CultureInfo.InvariantCulture) + n.Unit);
        if (type is "Point2" or "Point3" && FirmamentSyntaxValues.Vector(e, "mm", out var components))
        {
            var args = components.Select(n => new ArgumentSyntax(null, ParseValue(n.ToString("R", CultureInfo.InvariantCulture) + "mm"), null, e.Span)).ToArray();
            return e is CallSyntax c ? c with { Arguments = args } : new SequenceSyntax(SequenceKind.List, args, e.Span);
        }
        return e;
    }
    private static string PatternItemName(ObjectSyntax obj, string feature, int ordinal) => obj.Body.Statements.OfType<FieldSyntax>()
        .FirstOrDefault(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Center"))?.Value is ReferenceSyntax r ? r.Name.Text : feature + "__" + ordinal.ToString(CultureInfo.InvariantCulture);

}
