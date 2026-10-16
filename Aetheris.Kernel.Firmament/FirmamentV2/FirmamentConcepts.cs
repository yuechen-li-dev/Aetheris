using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using System.Globalization;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// Source-level Concept contracts and requirements. All recognition uses the shared AST;
/// numeric operations retain the existing dimensional syntax binder. This owns neither
/// material geometry nor Template expansion.
/// </summary>
internal sealed class FirmamentConcepts
{
    private readonly DocumentSyntax syntax;
    private readonly string source;
    private readonly FirmamentCompileTimeValues? scalars;
    private readonly Dictionary<string, DeclarationSyntax> declarations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConceptIrDefinition> contracts = new(StringComparer.Ordinal);
    private readonly List<FirmamentV2ValidationDiagnostic> diagnostics = [];
    private readonly List<string> bindingDiagnostics = [];
    internal IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics => diagnostics;
    internal IReadOnlyList<string> BindingDiagnostics => bindingDiagnostics;

    internal FirmamentConcepts(string source, FirmamentCompileTimeValues? scalars = null)
    {
        syntax = FirmamentSyntaxParser.Parse(source).Document;
        this.source = source;
        this.scalars = scalars;
        foreach (var d in FirmamentSyntaxTraversal.Declarations(syntax).Where(d => d.Type.Name.Text == "Concept"))
        {
            if (d.Name is null) { Add("declaration-invalid", "A Concept needs a name.", d.Span); continue; }
            if (!declarations.TryAdd(d.Name.Text, d)) Add("duplicate-declaration", $"Concept '{d.Name.Text}' is declared twice.", d.Span);
        }
        foreach (var d in declarations.Values)
        {
            try
            {
                if (d.Modifiers.Count > 0 || d.Type.Arguments.Count > 0 || d.ResultType is not null || d.Clauses.Count > 0 || d.Occurrence
                    || (d.Initializer is null) == (d.Body is null))
                    throw Error("declaration-invalid", "Use Concept Name { member: Type }, Concept Name(parameters) => predicate, or a labeled requirement bundle.", d.Span);
                if (IsPredicate(d))
                {
                    if (d.Annotation is not null) throw Error("declaration-invalid", "A predicate Concept returns Bool; structural inheritance belongs to a structural Concept.", d.Annotation.Span);
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var p in d.Parameters)
                        if (p.Name is null || !names.Add(p.Name.Text) || p.Default is not null
                            || FirmamentSyntaxValues.Reference(p.Value) is not { } type || !FirmamentV2Parser.TryParseLetType(type, out _))
                            throw Error("parameter-invalid", "Concept parameters need unique names and scalar types (Length, Angle, Int, Float, Bool or String), without defaults.", p.Span);
                    if (d.Body is not null)
                    {
                        if (d.Body.Statements.Count == 0) throw Error("bundle-empty", "A requirement bundle needs at least one labeled Boolean requirement.", d.Body.Span);
                        names.Clear();
                        foreach (var s in d.Body.Statements)
                            if (s is not FieldSyntax f || !names.Add(f.Name.Text))
                                throw Error("bundle-member-invalid", "A requirement bundle contains unique labeled Boolean expressions: name: predicate.", s.Span);
                    }
                }
                else _ = Contract(d.Name!.Text, []);
            }
            catch (BindingError e) { Add(e); }
        }
        // Definitions must type-check even if no Require applies them. Placeholder
        // values check types, never impose the truth of an unapplied requirement.
        if (scalars is not null && diagnostics.Count == 0)
            foreach (var d in declarations.Values.Where(IsPredicate))
            {
                try
                {
                    var arguments = d.Parameters.Select(p => new ArgumentSyntax(null,
                        Sample(FirmamentSyntaxValues.Reference(p.Value)!, p.Span), null, p.Span)).ToArray();
                    _ = Evaluate(new CallSyntax(new ReferenceSyntax(d.Name!, d.Name!.Span), arguments, d.Span),
                        new(StringComparer.Ordinal), [], 0, typeOnly: true);
                }
                catch (BindingError e) { Add(e); }
            }
    }

    private bool IsPredicate(DeclarationSyntax d)
    {
        return Visit(d, new(StringComparer.Ordinal));
        bool Visit(DeclarationSyntax current, HashSet<string> visiting)
        {
            if (current.Initializer is not null || current.Parameters.Count > 0) return true;
            if (visiting.Count >= 128 || !visiting.Add(current.Name!.Text)) return false;
            return current.Body?.Statements.OfType<FieldSyntax>().Any(f => !IsType(f.Value)
                || f.Value is ReferenceSyntax r && declarations.TryGetValue(r.Name.Text, out var target)
                    && Visit(target, new(visiting, StringComparer.Ordinal))) == true;
        }
    }

    private static bool IsType(ExpressionSyntax e) => e is ReferenceSyntax or IndexSyntax { Target: ReferenceSyntax, Index: null };

    internal IReadOnlyList<ConceptIrDefinition> Contracts => contracts.Values.ToArray();
    internal bool HasContract(string name) => declarations.TryGetValue(name, out var d) && !IsPredicate(d);

    internal void ValidateClaims()
    {
        Visit(syntax.Statements);
        void Visit(IReadOnlyList<StatementSyntax> statements)
        {
            foreach (var d in statements.OfType<DeclarationSyntax>())
            {
                if (d.Type.Name.Text is "Template" or "Feature" || d.Modifiers.Any(m => m.Name.Text == "Template")) continue;
                if (d.Type.Name.Text == "Struct" && d.Modifiers.Any(m => m.Name.Text == "Concept") && d.Annotation is not null)
                {
                    var name = FirmamentSyntaxValues.Reference(d.Annotation);
                    if (name is null || !contracts.TryGetValue(name, out var contract))
                        Add("contract-unresolved", "A Concept Struct conformance annotation must name a declared structural Concept; compose contracts in a named Concept first.", d.Annotation.Span);
                    else if (!ConceptIrResolver.ProvidesContract(source, d, contract, out var reasons))
                    {
                        bindingDiagnostics.AddRange(reasons);
                        diagnostics.Add(new("firmament-concept-structural-conformance-failed", "error",
                            $"Concept Struct '{d.Name?.Text}' does not provide '{name}'. Required semantic members: {string.Join(", ", contract.Members.Values.Select(m => m.Name + ": " + m.Type))}. Binding reasons: {string.Join("; ", reasons)}.", Target: d.Name?.Text)
                            { SourceSpan = d.Span, Stage = "Concept binding" });
                    }
                }
                else if (d.Type.Name.Text is "Model" or "Struct" && d.Body is not null) Visit(d.Body.Statements);
            }
        }
    }

    internal static IEnumerable<(DeclarationSyntax Declaration, bool AdmittedScope)> Requirements(DocumentSyntax document)
    {
        return Visit(document.Statements, true);
        static IEnumerable<(DeclarationSyntax, bool)> Visit(IReadOnlyList<StatementSyntax> statements, bool admitted)
        {
            foreach (var d in statements.OfType<DeclarationSyntax>())
            {
                if (d.Type.Name.Text is "Concept" or "Template" or "Feature" || d.Modifiers.Any(m => m.Name.Text == "Template")) continue;
                if (d.Type.Name.Text == "Require" && d.Body is null) yield return (d, admitted);
                else if (d.Body is not null)
                    foreach (var child in Visit(d.Body.Statements, d.Type.Name.Text is "Model" or "Struct")) yield return child;
            }
        }
    }

    private ConceptIrDefinition Contract(string name, List<string> stack)
    {
        if (contracts.TryGetValue(name, out var known)) return known;
        if (!declarations.TryGetValue(name, out var d) || IsPredicate(d))
            throw Error("contract-unresolved", $"'{name}' is not a structural Concept.", declarations.GetValueOrDefault(name)?.Span ?? syntax.Span);
        if (stack.Contains(name, StringComparer.Ordinal) || stack.Count >= 128)
            throw Error("contract-cycle", $"Cyclic or excessively deep Concept composition: {string.Join(" -> ", stack.Append(name))}.", d.Span);
        var members = new Dictionary<string, ConceptIrMemberRequirement>(StringComparer.Ordinal);
        foreach (var parent in Parents(d.Annotation))
            foreach (var field in Contract(parent, [.. stack, name]).Members.Values) Merge(field);
        foreach (var statement in d.Body?.Statements ?? [])
        {
            if (statement is not FieldSyntax f || !IsType(f.Value))
                throw Error("contract-member-invalid", "A structural Concept contains typed members (name: Type); Boolean checks belong in a parameterized requirement bundle.", statement.Span);
            var element = f.Value is IndexSyntax index ? index.Target : f.Value;
            Merge(new(f.Name.Text, new(FirmamentSyntaxValues.Reference(element)!, f.Value is IndexSyntax), f.Span), own: true);
        }
        return contracts[name] = new(name, members, d.Span);

        void Merge(ConceptIrMemberRequirement member, bool own = false)
        {
            if (members.TryGetValue(member.Name, out var previous))
            {
                if (previous.Type != member.Type || own)
                    throw Error("contract-member-conflict", $"Concept '{name}' has duplicate or conflicting requirement '{member.Name}' ({previous.Type} and {member.Type}).", member.SourceSpan);
                return; // A diamond can require the same member with the same type.
            }
            members.Add(member.Name, member);
        }
        IEnumerable<string> Parents(ExpressionSyntax? e) => e switch
        {
            null => [],
            ReferenceSyntax r => [r.Name.Text],
            BinarySyntax { Operator: BinaryOperator.And } b => Parents(b.Left).Concat(Parents(b.Right)),
            _ => throw Error("contract-composition-invalid", "Compose structural Concepts with : First && Second; every name must be a structural Concept.", e.Span)
        };
    }

    // A supplied type must provide the required structural signature, including inherited
    // members. The existing semantic resolver still checks concrete values and Expose.
    internal bool Satisfies(string type, string concept)
    {
        if (!contracts.TryGetValue(concept, out var required)) return false;
        if (type == "Box")
        {
            var available = new Dictionary<string, string>(StringComparer.Ordinal) { ["Bounds"] = "Box3", ["TopPlane"] = "Plane", ["CenterAxis"] = "Axis" };
            return required.Members.Values.All(m => !m.Type.IsCollection && available.GetValueOrDefault(m.Name) == m.Type.Name);
        }
        var supplied = FirmamentSyntaxTraversal.Declarations(syntax).Where(d => d.Type.Name.Text == "Struct"
            && d.Modifiers.Any(m => m.Name.Text == "Concept") && d.Name?.Text == type).ToArray();
        if (supplied.Length != 1 || supplied[0].Annotation is not ReferenceSyntax claim || !contracts.TryGetValue(claim.Name.Text, out var actual)) return false;
        if (!required.Members.Values.All(m => actual.Members.TryGetValue(m.Name, out var provided) && provided.Type == m.Type)) return false;
        var valid = ConceptIrResolver.ProvidesContract(source, supplied[0], actual, out var reasons);
        bindingDiagnostics.AddRange(reasons);
        return valid;
    }

    internal bool Check(ExpressionSyntax expression, string context, FirmamentV2SourceSpan? location = null)
    {
        try
        {
            var value = Evaluate(expression, new(StringComparer.Ordinal), [], 0);
            if (value.Type != "Bool") throw Error("requirement-non-bool", $"Requirement '{context}' must be Bool, not {value.Type}.", expression.Span);
            if (!(bool)value.Data) throw Error("requirement-failed", $"Requirement '{context}' is false{value.FailedMember}; no geometry was admitted.", expression.Span);
            return true;
        }
        catch (BindingError e)
        {
            Add(e);
            if (location is not null) diagnostics[^1] = diagnostics[^1] with { SourceSpan = location,
                LocationNote = "Location identifies the authored Require declaration; the check evaluated its specialized values." };
            return false;
        }
    }

    private sealed record Value(string Type, object Data, ExpressionSyntax Syntax, string? FailedMember = null);

    private Value Evaluate(ExpressionSyntax e, Dictionary<string, Value> scope, List<string> stack, int depth, bool typeOnly = false)
    {
        if (depth > 128) throw Error("expression-depth", "Concept expressions are limited to 128 nesting levels.", e.Span);
        Value Eval(ExpressionSyntax child) => Evaluate(child, scope, stack, depth + 1, typeOnly);
        if (e is SequenceSyntax { Kind: SequenceKind.Tuple, Items.Count: 1 } group) return Eval(group.Items[0].Value);
        if (e is LiteralSyntax { Kind: LiteralKind.Boolean } boolean) return new("Bool", bool.Parse(boolean.Raw), e);
        if (e is LiteralSyntax { Kind: LiteralKind.String } text) return new("String", System.Text.Json.JsonSerializer.Deserialize<string>(text.Raw)!, e);
        if (e is ReferenceSyntax or MemberSyntax)
        {
            var name = FirmamentSyntaxValues.Reference(e)!;
            if (scope.TryGetValue(name, out var local)) return local;
            if (scalars?.TryValue(name, out var checkedValue) == true)
            {
                var nominal = FirmamentSyntaxParser.ParseExpression(scalars.NominalExpression(name)).Expression;
                if (nominal is not null) return Eval(nominal) with { Type = checkedValue.Type.ToString() };
            }
            if (declarations.TryGetValue(name, out var predicate)) return Invoke(predicate, [], e.Span);
            throw Error("name-unresolved", $"Cannot resolve '{name}' in a Concept requirement.", e.Span);
        }
        if (e is CallSyntax call && FirmamentSyntaxValues.Reference(call.Target) is { } target)
        {
            if (!declarations.TryGetValue(target, out var predicate)) throw Error("name-unresolved", $"Unknown Concept predicate '{target}'.", call.Target.Span);
            return Invoke(predicate, call.Arguments, call.Span);
        }
        if (e is UnarySyntax { Operator: UnaryOperator.Not } not)
        {
            var operand = Eval(not.Operand);
            if (operand.Type != "Bool") throw TypeError(e);
            return Boolean(!(bool)operand.Data, e);
        }
        if (e is BinarySyntax b && b.Operator is BinaryOperator.And or BinaryOperator.Or)
        {
            var left = Eval(b.Left); var right = Eval(b.Right); // Check both operands, including unresolved/ill-typed branches.
            if (left.Type != "Bool" || right.Type != "Bool") throw TypeError(e);
            return Boolean(b.Operator == BinaryOperator.And ? (bool)left.Data && (bool)right.Data : (bool)left.Data || (bool)right.Data, e);
        }
        if (e is BinarySyntax compare && compare.Operator is BinaryOperator.Equal or BinaryOperator.NotEqual or BinaryOperator.Less or BinaryOperator.LessEqual or BinaryOperator.Greater or BinaryOperator.GreaterEqual)
        {
            var left = Eval(compare.Left); var right = Eval(compare.Right);
            if (left.Type != right.Type && !(IsNumber(left) && IsNumber(right))) throw TypeError(e);
            var numeric = left.Data is double && right.Data is double;
            if (!numeric && compare.Operator is not (BinaryOperator.Equal or BinaryOperator.NotEqual)) throw TypeError(e);
            var order = numeric ? ((double)left.Data).CompareTo((double)right.Data) : Equals(left.Data, right.Data) ? 0 : 1;
            return Boolean(compare.Operator switch { BinaryOperator.Equal => order == 0, BinaryOperator.NotEqual => order != 0,
                BinaryOperator.Less => order < 0, BinaryOperator.LessEqual => order <= 0, BinaryOperator.Greater => order > 0, _ => order >= 0 }, e);
        }
        // Rebind operands to their checked nominal syntax, then delegate all arithmetic
        // and dimensional rules to the existing shared geometry/scalar syntax owner.
        string? arithmeticType = null;
        ExpressionSyntax Arithmetic(BinarySyntax binary)
        {
            var left = Eval(binary.Left); var right = Eval(binary.Right);
            _ = FirmamentV2Parser.TryParseLetType(left.Type, out var lt);
            _ = FirmamentV2Parser.TryParseLetType(right.Type, out var rt);
            var op = binary.Operator switch { BinaryOperator.Add => "+", BinaryOperator.Subtract => "-", BinaryOperator.Multiply => "*", _ => "/" };
            arithmeticType = FirmamentV2Parser.CompileTimeArithmeticType(lt, op, rt)?.ToString();
            if (arithmeticType is null) throw TypeError(e);
            return binary with { Left = left.Syntax, Right = right.Syntax };
        }
        ExpressionSyntax Unary(UnarySyntax unary)
        {
            var operand = Eval(unary.Operand); arithmeticType = operand.Type;
            return unary with { Operand = operand.Syntax };
        }
        var normalized = e switch
        {
            BinarySyntax binary when binary.Operator is BinaryOperator.Add or BinaryOperator.Subtract or BinaryOperator.Multiply or BinaryOperator.Divide => Arithmetic(binary),
            UnarySyntax unary when unary.Operator is UnaryOperator.Positive or UnaryOperator.Negative => Unary(unary),
            QuantitySyntax quantity => quantity with { Value = Eval(quantity.Value).Syntax },
            LiteralSyntax { Kind: LiteralKind.Number } => e,
            _ => throw Error("expression-unsupported", "Expected a Concept call, scalar comparison, Boolean expression or dimensional arithmetic.", e.Span)
        };
        foreach (var unit in new[] { "mm", "deg", "" })
            if (FirmamentSyntaxValues.TryMeasure(normalized, unit, out var number))
            {
                var type = arithmeticType ?? (unit == "mm" ? "Length" : unit == "deg" ? "Angle"
                    : e is LiteralSyntax n && !n.Raw.Contains('.') && !n.Raw.Contains('e', StringComparison.OrdinalIgnoreCase) ? "Int" : "Float");
                if (typeOnly && e is BinarySyntax) number = 1;
                if (type == "Int" && (number < int.MinValue || number > int.MaxValue)) throw TypeError(e);
                var literal = new LiteralSyntax(LiteralKind.Number, number.ToString("R", CultureInfo.InvariantCulture), e.Span);
                return new(type, number, unit.Length == 0 ? literal : new QuantitySyntax(literal, new(unit, e.Span), e.Span));
            }
        throw TypeError(e);

        Value Invoke(DeclarationSyntax d, IReadOnlyList<ArgumentSyntax> arguments, FirmamentV2SourceSpan span)
        {
            var name = d.Name!.Text;
            if (!IsPredicate(d)) throw Error("predicate-expected", $"'{name}' is a structural Concept; use it as a conformance constraint, not a Boolean predicate.", span);
            if (stack.Contains(name, StringComparer.Ordinal) || stack.Count >= 128)
                throw Error("predicate-cycle", $"Recursive Concept requirement: {string.Join(" -> ", stack.Append(name))}.", span);
            if (arguments.Count != d.Parameters.Count) throw Error("argument-invalid", $"Concept '{name}' expects {d.Parameters.Count} argument(s), received {arguments.Count}.", span);
            var inputs = new Dictionary<string, Value>(StringComparer.Ordinal);
            for (var i = 0; i < arguments.Count; i++)
            {
                var a = arguments[i];
                var p = a.Name is null ? d.Parameters[i] : d.Parameters.FirstOrDefault(p => p.Name?.Text == a.Name.Text);
                if (p?.Name is null || inputs.ContainsKey(p.Name.Text) || a.Default is not null)
                    throw Error("argument-invalid", $"Unknown or duplicate argument in Concept '{name}'.", a.Span);
                var value = Eval(a.Value); var type = FirmamentSyntaxValues.Reference(p.Value)!;
                if (FirmamentV2Parser.TryParseLetType(type, out var primitive)) type = primitive.ToString();
                if (value.Type != type && !(type == "Float" && value.Type == "Int"))
                    throw Error("argument-type-mismatch", $"Concept '{name}.{p.Name.Text}' expects {type}, received {value.Type}.", a.Span);
                inputs.Add(p.Name.Text, value with { Type = type });
            }
            var next = new List<string>(stack) { name };
            if (d.Initializer is not null)
            {
                var result = Evaluate(d.Initializer, inputs, next, depth + 1, typeOnly);
                if (result.Type != "Bool") throw Error("requirement-non-bool", $"Concept '{name}' must return Bool, not {result.Type}.", d.Initializer.Span);
                return result;
            }
            Value? failed = null;
            foreach (var field in d.Body!.Statements.Cast<FieldSyntax>())
            {
                var result = Evaluate(field.Value, inputs, next, depth + 1, typeOnly);
                if (result.Type != "Bool") throw Error("requirement-non-bool", $"Concept '{name}.{field.Name.Text}' must be Bool, not {result.Type}.", field.Value.Span);
                if (!(bool)result.Data && failed is null) failed = result with { FailedMember = $" at {name}.{field.Name.Text}{result.FailedMember}" };
            }
            return failed ?? Boolean(true, span: span);
        }
    }

    private static bool IsNumber(Value value) => value.Type is "Int" or "Float";
    private static ExpressionSyntax Sample(string type, FirmamentV2SourceSpan span)
    {
        _ = FirmamentV2Parser.TryParseLetType(type, out var primitive);
        return primitive switch
        {
            FirmamentV2PrimitiveType.Bool => new LiteralSyntax(LiteralKind.Boolean, "true", span),
            FirmamentV2PrimitiveType.String => new LiteralSyntax(LiteralKind.String, "\"sample\"", span),
            FirmamentV2PrimitiveType.Length => new QuantitySyntax(new LiteralSyntax(LiteralKind.Number, "1", span), new("mm", span), span),
            FirmamentV2PrimitiveType.Time => new QuantitySyntax(new LiteralSyntax(LiteralKind.Number, "1", span), new("s", span), span),
            FirmamentV2PrimitiveType.Angle => new QuantitySyntax(new LiteralSyntax(LiteralKind.Number, "1", span), new("deg", span), span),
            _ => new LiteralSyntax(LiteralKind.Number, primitive == FirmamentV2PrimitiveType.Float ? "1.5" : "1", span)
        };
    }
    private static Value Boolean(bool value, ExpressionSyntax? syntax = null, FirmamentV2SourceSpan? span = null) =>
        new("Bool", value, syntax ?? new LiteralSyntax(LiteralKind.Boolean, value ? "true" : "false", span!));
    private static BindingError TypeError(ExpressionSyntax e) => Error("expression-type-mismatch", "Concept operands have incompatible types or units, or the arithmetic is undefined. Lengths compare with lengths, angles with angles, and logical operators require Bool.", e.Span);
    private sealed class BindingError(string code, string message, FirmamentV2SourceSpan span) : Exception(message)
    { internal string Code => "firmament-concept-" + code; internal FirmamentV2SourceSpan Span => span; }
    private static BindingError Error(string code, string message, FirmamentV2SourceSpan span) => new(code, message, span);
    private void Add(BindingError error) => diagnostics.Add(new(error.Code, "error", error.Message)
        { SourceFile = syntax.SourceFile, SourceSpan = error.Span, Stage = "Concept binding" });
    private void Add(string code, string message, FirmamentV2SourceSpan span) => Add(Error(code, message, span));
}
