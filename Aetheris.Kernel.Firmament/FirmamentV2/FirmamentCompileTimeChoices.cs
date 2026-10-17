using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

internal sealed partial class FirmamentCompileTimeValues
{
    internal sealed record ChoiceInput(string Type, ExpressionSyntax Value);
    private static string CanonicalValueType(string type) => FirmamentV2Parser.TryParseLetType(type, out var primitive) ? primitive.ToString() : type;
    private FirmamentChoiceBinding? choices;
    internal FirmamentChoiceBinding Choices => choices ??= new(source);
    internal void UseChoiceTypes(IEnumerable<FirmamentChoiceBinding.Choice> types) => choices = new(types);
    private readonly Dictionary<string, ChoiceInput> choiceInputs = new(StringComparer.Ordinal);
    private readonly List<string> activeChoices = [];

    internal IReadOnlyDictionary<string, ChoiceInput> ChoiceLocals(DocumentSyntax document,
        IReadOnlyDictionary<string, ChoiceInput> parameters)
    {
        var result = new Dictionary<string, ChoiceInput>(parameters, StringComparer.Ordinal);
        foreach (var d in document.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Static" && d.Name is not null && d.Initializer is not null))
            result[d.Name!.Text] = new(FirmamentSyntaxValues.Reference(d.Annotation)
                ?? (d.Annotation is not null ? FirmamentSyntaxWriter.Expression(d.Annotation) : null)
                ?? (d.Initializer is ObjectSyntax o ? FirmamentSyntaxValues.Reference(o.Type) : null) ?? "", d.Initializer!);
        return result;
    }

    internal IReadOnlyDictionary<string, ChoiceInput> ChoiceScope(DocumentSyntax document, MatchSyntax match,
        IReadOnlyDictionary<string, ChoiceInput> parameters)
    {
        var inputs = new Dictionary<string, ChoiceInput>(ChoiceLocals(document, parameters), StringComparer.Ordinal);
        foreach (var d in FirmamentSyntaxTraversal.Declarations(document).Where(d => d.Body is not null && Encloses(d.Body.ContentSpan, match.Span))
            .OrderByDescending(d => d.Body!.ContentSpan.Length))
        {
            foreach (var p in d.Parameters.Concat(d.Modifiers.Where(m => m.Name.Text == "Template").SelectMany(m => m.Arguments)))
                if (p.Name is not null && FirmamentSyntaxValues.Reference(p.Value) is { } type)
                    inputs[p.Name.Text] = new(type, p.Default ?? new ReferenceSyntax(p.Name, p.Name.Span));
            inputs = new(ChoiceLocals(new(document.SourceFile, d.Body!.Statements, [], d.Body.Span), inputs), StringComparer.Ordinal);
            if (d.Type.Name.Text == "Struct" && d.Modifiers.Any(m => m.Name.Text == "Concept"))
            {
                var outer = inputs.ToDictionary(p => p.Key, p => p.Value.Value, StringComparer.Ordinal);
                foreach (var field in d.Body.Statements.OfType<FieldSyntax>())
                {
                    var typed = field.Value as TypedValueSyntax;
                    inputs[field.Name.Text] = new(typed is null ? "" : FirmamentSyntaxValues.Reference(typed.Type) ?? "",
                        new FirmamentSyntaxSubstitution(outer).Expression(typed?.Value ?? field.Value));
                }
            }
        }
        return inputs;
    }

    internal static bool Encloses(FirmamentV2SourceSpan parent, FirmamentV2SourceSpan child) =>
        parent.Start <= child.Start && parent.Start + parent.Length >= child.Start + child.Length;

    private ChoiceInput? ChoiceInputFor(string name, IReadOnlyDictionary<string, ChoiceInput>? locals,
        List<string> codes, bool evaluate, int depth = 0)
    {
        if (locals?.TryGetValue(name, out var local) == true) return local;
        var rootEnd = name.IndexOf('.');
        if (evaluate && rootEnd > 0 && locals?.TryGetValue(name[..rootEnd], out var root) == true
            && root.Value is ReferenceSyntax receipt && recordValues.TryGetValue(receipt.Name.Text, out var record)
            && record.TypeName == root.Type && RecordFieldType(root.Type, name[(rootEnd + 1)..]) is { } fieldType)
        {
            var fields = FirmamentV2TemplateExpansion.FlattenRecordFields(record, recordTypes, recordValues, codes);
            if (fields.TryGetValue(name[(rootEnd + 1)..], out var memberValue))
                return CheckedChoiceInput(fieldType, FirmamentSyntaxParser.ParseExpression(memberValue).Expression!);
        }
        if (choiceInputs.TryGetValue(name, out var input)) return input;
        if (depth >= 128)
        { ChoiceError(new(FirmamentV2Parser.ExpressionCycle, "error", $"Record choice projection exceeds the finite dependency depth at '{name}'.") { Stage = "Choice binding" }, codes); return null; }
        var dot = name.LastIndexOf('.');
        if (dot < 1 || ChoiceInputFor(name[..dot], locals, codes, evaluate, depth + 1) is not { } owner
            || RecordFieldType(owner.Type, name[(dot + 1)..]) is not { } type) return null;
        if (!evaluate) return new(type, new ReferenceSyntax(new(name, owner.Value.Span), owner.Value.Span));
        var value = owner.Value is MatchSyntax selection ? ResolveChoices(selection, codes, locals, owner.Type) : owner.Value;
        if (value is ReferenceSyntax alias) return ChoiceInputFor(alias.Name.Text + name[dot..], locals, codes, true, depth + 1);
        var body = value is ObjectSyntax obj ? obj.Body : (value as WithSyntax)?.Body;
        if (body?.Statements.OfType<FieldSyntax>().FirstOrDefault(f => f.Name.Text == name[(dot + 1)..]) is { } field) return new(type, field.Value);
        if (value is WithSyntax derived && FirmamentSyntaxValues.Reference(derived.Target) is { } parent)
            return ChoiceInputFor(parent + name[dot..], locals, codes, true, depth + 1);
        return null;
    }

    private void InitializeChoices(IReadOnlyDictionary<string, TemplateRecordTypeIr> records, List<string> codes, string? bindingSource = null)
    {
        foreach (var diagnostic in Choices.Diagnostics) ChoiceError(diagnostic, codes);
        foreach (var d in Scope(FirmamentSyntaxParser.Parse(bindingSource ?? source).Document.Statements)
                     .Where(d => d.Type.Name.Text == "Static" && d.Name is not null))
        {
            var type = FirmamentSyntaxValues.Reference(d.Annotation) ?? (d.Initializer is ObjectSyntax o ? FirmamentSyntaxValues.Reference(o.Type) : null);
            if (type is not null && d.Initializer is not null) choiceInputs[d.Name!.Text] = new(type, d.Initializer);
            if (type is not null && records.TryGetValue(type, out var schema)
                && (d.Body ?? (d.Initializer as ObjectSyntax)?.Body) is { } body)
                foreach (var f in body.Statements.OfType<FieldSyntax>())
                    if (schema.Fields.TryGetValue(f.Name.Text, out var fieldType))
                        choiceInputs[d.Name!.Text + "." + f.Name.Text] = new(fieldType, f.Value);
        }
        foreach (var (name, input) in choiceInputs.Where(p => Choices.Types.ContainsKey(p.Value.Type)).ToArray())
            _ = ChoiceTag(new ReferenceSyntax(new(name, input.Value.Span), input.Value.Span), input.Type, null, codes, new(StringComparer.Ordinal));
    }

    internal void LocateChoiceErrors(int start, FirmamentV2SourceSpan? location, string note)
    {
        for (var i = start; i < diagnostics.Count; i++)
            if (diagnostics[i].Stage == "Choice binding") diagnostics[i] = diagnostics[i] with
            { SourceSpan = location, IsOriginalSource = location is not null, LocationNote = note };
    }

    private void ChoiceError(FirmamentV2ValidationDiagnostic error, List<string> codes, int sourceOffset = 0)
    {
        codes.Add(error.Code);
        var span = error.SourceSpan is { } located ? located with { Start = located.Start + sourceOffset } : null;
        diagnostics.Add(error with { SourceSpan = span is null ? null : dataBinding?.OriginalSpan(span) ?? span,
            IsOriginalSource = true });
    }

    internal bool IsChoice(string type) => Choices.Types.ContainsKey(type);

    internal ChoiceInput CheckedChoiceInput(string type, ExpressionSyntax value)
    {
        // A decoded argument is a value receipt, not another lookup in the
        // callee's namespace. Qualification prevents a case from capturing a
        // parameter with the same authored spelling.
        if (IsChoice(type) && value is ReferenceSyntax tag && Choices.Types[type].Cases.Contains(tag.Name.Text, StringComparer.Ordinal))
            value = new MemberSyntax(new ReferenceSyntax(new(type, value.Span), value.Span), tag.Name, value.Span);
        return new(type, value);
    }

    internal bool TryChoiceTransfer(string expression, string type, List<string> codes, out string tag)
    {
        var parsed = FirmamentSyntaxParser.ParseExpression(expression);
        tag = "";
        if (parsed.Expression is null) return false;
        var resolved = ResolveChoices(parsed.Expression, codes, expected: type);
        var value = ChoiceTag(resolved, type, null, codes, new(StringComparer.Ordinal));
        if (value is null) return false;
        tag = value;
        return true;
    }

    internal ExpressionSyntax ResolveChoices(ExpressionSyntax expression, List<string> codes,
        IReadOnlyDictionary<string, ChoiceInput>? locals = null, string? expected = null,
        IDictionary<string, string>? selected = null, bool select = true, int sourceOffset = 0, bool finitePatternSelector = false)
    {
        string? TypeOf(ExpressionSyntax e, string? context = null, HashSet<string>? stack = null)
        {
            stack ??= new(StringComparer.Ordinal);
            if (e is ToleranceSyntax tolerance)
            {
                var nominal = TypeOf(tolerance.Nominal, context, stack);
                if (nominal is null) return null;
                if (!FirmamentV2Parser.TryParseLetType(nominal, out var primitive))
                { Fail(FirmamentV2Parser.ToleranceInvalidType, "tol requires a Length or Angle value.", tolerance.ToleranceSpan); return null; }
                return FirmamentV2Parser.BindTolerance(tolerance, primitive, Fail) is null ? null : nominal;
            }
            string? Reference(ExpressionSyntax reference)
            {
                var name = FirmamentSyntaxValues.Reference(reference);
                if (name is not null)
                {
                    if (reference is MemberSyntax { Target: ReferenceSyntax qualifiedOwner } qualifiedCase
                        && Choices.Types.TryGetValue(qualifiedOwner.Name.Text, out var domain)
                        && domain.Cases.Contains(qualifiedCase.Member.Text, StringComparer.Ordinal)) return domain.Name;
                    if (ChoiceInputFor(name, locals, codes, false) is { } local)
                    {
                        if (local.Type.Length > 0) return CanonicalValueType(local.Type);
                        if (stack.Count >= 128 || !stack.Add(name)) { Fail(FirmamentV2Parser.ExpressionCycle, $"Compile-time dependency cycle at '{name}'.", reference.Span); return null; }
                        var inferred = TypeOf(local.Value, context, stack); stack.Remove(name); return inferred;
                    }
                    if (values.TryGetValue(name, out var value)) return value.Type.ToString();
                    if (definitions.TryGetValue(name, out var definition))
                    {
                        if (!stack.Add(name)) { Fail(FirmamentV2Parser.ExpressionCycle, $"Compile-time dependency cycle at '{name}'.", reference.Span); return null; }
                        var type = FirmamentSyntaxValues.Reference(definition.Annotation) ?? (definition.Initializer is null ? null : TypeOf(definition.Initializer, null, stack));
                        stack.Remove(name); return type;
                    }
                    var dot = name.LastIndexOf('.');
                    if (dot > 0 && Choices.Types.TryGetValue(name[..dot], out var qualified) && qualified.Cases.Contains(name[(dot + 1)..], StringComparer.Ordinal)) return qualified.Name;
                    if (context is not null && Choices.Types.TryGetValue(context, out var contextual) && contextual.Cases.Contains(name, StringComparer.Ordinal)) return context;
                    var candidates = Choices.Types.Values.Where(t => t.Cases.Contains(name, StringComparer.Ordinal)).ToArray();
                    if (candidates.Length == 1) return candidates[0].Name;
                }
                if (reference is CallSyntax call)
                {
                    if (call.Target is ReferenceSyntax constructor && constructor.Name.Text is "Point2" or "Point3" or "Vector2" or "Vector3")
                    {
                        var dimensions = constructor.Name.Text.EndsWith('3') ? 3 : 2;
                        var types = call.Arguments.Select(a => TypeOf(a.Value, null, stack)).ToArray();
                        if (types.Length == dimensions && call.Arguments.All(a => a.Name is null && a.Default is null)
                            && types.All(t => constructor.Name.Text.StartsWith("Point", StringComparison.Ordinal) ? t == "Length" : t is "Int" or "Float"))
                            return constructor.Name.Text;
                    }
                    if (call.Target is MemberSyntax member)
                    {
                        var parent = TypeOf(member.Target, null, stack);
                        var axis = call.Arguments.Count == 1 && call.Arguments[0].Name is null && call.Arguments[0].Default is null
                            && FirmamentSyntaxValues.Reference(call.Arguments[0].Value) is "+X" or "-X" or "+Y" or "-Y" or "+Z" or "-Z";
                        if (parent == "Box3" && member.Member.Text == "Face" && axis) return "Plane";
                        if (member.Member.Text == "Axis" && parent is "Point3" or "Box3" && axis) return "Axis";
                        if (member.Member.Text == "Inset" && parent == "Plane" && call.Arguments.Count == 1
                            && TypeOf(call.Arguments[0].Value, "Length", stack) == "Length") return "Region2";
                    }
                }
                if (reference is MemberSyntax field && TypeOf(field.Target, null, stack) is { } owner)
                {
                    if (RecordFieldType(owner, field.Member.Text) is { } fieldType) return fieldType;
                    if (owner == "Box3" && field.Member.Text == "Center") return "Point3";
                }
                if (reference is IndexSyntax index && TypeOf(index.Target, null, stack) is { } indexed
                    && indexed.EndsWith(']') && indexed.IndexOf('[') is var bracket && bracket > 0
                    && index.Index is not null && TypeOf(index.Index, "Int", stack) == "Int") return indexed[..bracket];
                Fail(FirmamentV2Parser.ExpressionUnknownSymbol, $"Cannot resolve compile-time value '{name ?? FirmamentSyntaxWriter.Expression(reference)}'.", reference.Span);
                return null;
            }
            if (e is TypedValueSyntax typed)
            {
                var type = FirmamentSyntaxValues.Reference(typed.Type);
                var actual = TypeOf(typed.Value, type, stack);
                if (type is not null && CanonicalValueType(type) == actual) return actual;
                Fail(FirmamentV2Parser.ExpressionTypeMismatch, $"Typed value requires {type}; found {actual}.", typed.Span); return null;
            }
            if (e is WithSyntax derived)
            {
                var type = TypeOf(derived.Target, null, stack);
                if (type is not null && recordTypes.TryGetValue(type, out var schema))
                { CheckRecord(derived.Body, schema, false, stack); return type; }
                Fail(FirmamentV2Parser.ExpressionTypeMismatch, "with requires a bound Record value.", derived.Target.Span); return null;
            }
            if (e is ObjectSyntax obj && FirmamentSyntaxValues.Reference(obj.Type) is { } objectType)
            {
                if (recordTypes.TryGetValue(objectType, out var schema)) CheckRecord(obj.Body, schema, true, stack);
                else if (ConceptIrResolver.SpatialFields.TryGetValue(objectType, out var spatialSchema))
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var statement in obj.Body.Statements)
                    {
                        if (statement is not FieldSyntax f)
                        { Fail(FirmamentV2Parser.ExpressionUnsupported, $"{objectType} requires named value fields.", statement.Span); continue; }
                        var name = spatialSchema.Keys.FirstOrDefault(k => FirmamentSyntaxFields.SameField(k, f.Name.Text));
                        if (name is null) { Fail("firmament-v2-canonical-field-unknown", $"Unknown field '{f.Name.Text}' in {objectType}.", f.Name.Span); continue; }
                        if (!seen.Add(name)) Fail("firmament-v2-field-duplicate", $"Duplicate field '{f.Name.Text}' in {objectType}.", f.Name.Span);
                        var actual = TypeOf(f.Value, null, stack);
                        if (actual is not null && !spatialSchema[name].Type.Split('|').Contains(actual, StringComparer.Ordinal))
                            Fail(ConceptIrResolver.MatchArmTypeMismatch, $"Field '{f.Name.Text}' in {objectType} requires {spatialSchema[name].Type}; found {actual}.", f.Value.Span);
                    }
                    foreach (var missing in spatialSchema.Where(p => p.Value.Required && !seen.Contains(p.Key)))
                        Fail(FirmamentV2Parser.ExpressionTypeMismatch, $"Missing required field '{missing.Key}' in {objectType}.", obj.Span);
                }
                else Fail(FirmamentV2Parser.ExpressionUnsupported, $"Constructor '{objectType}' is not an admitted compile-time value type.", obj.Span);
                return objectType == "Grid" ? "Point3[]" : objectType;
            }
            if (e is BinarySyntax { Operator: BinaryOperator.Equal or BinaryOperator.NotEqual } comparison)
            {
                var left = TypeOf(comparison.Left, null, stack);
                var right = TypeOf(comparison.Right, left, stack);
                if (left is not null && left == right)
                {
                    if (IsChoice(left) || FirmamentV2Parser.TryParseLetType(left, out _)) return "Bool";
                    Fail(FirmamentV2Parser.ExpressionUnsupported, $"Equality is not admitted for {left}; select an Enum case or compare scalar fields.", comparison.Span);
                    return null;
                }
                Fail(FirmamentV2Parser.ExpressionTypeMismatch, $"Equality requires the same type; found {left ?? "unresolved"} and {right ?? "unresolved"}.", comparison.Span);
                return null;
            }
            var inferred = FirmamentChoiceBinding.Infer(e, Reference, m => Choices.Check(m, TypeOf(m.Scrutinee, null, stack),
                arm => TypeOf(arm, context, new(stack, StringComparer.Ordinal)), d => ChoiceError(d, codes, sourceOffset),
                finitePatternSelector && m.Span == expression.Span ? CanonicalStaticAuthoring.FinitePatternDomain(m) : null));
            if (inferred is null && e is not (ReferenceSyntax or MemberSyntax or CallSyntax or MatchSyntax))
                Fail(FirmamentV2Parser.ExpressionTypeMismatch, "Cannot bind the type of this compile-time expression.", e.Span);
            return inferred;
        }
        void CheckRecord(BlockSyntax body, TemplateRecordTypeIr schema, bool requireAll, HashSet<string> stack)
        {
            var fields = body.Statements.OfType<FieldSyntax>().ToArray();
            foreach (var statement in body.Statements.Where(s => s is not FieldSyntax))
                Fail(FirmamentV2TemplateExpansion.Prefix + "record-field-declaration", $"Record {schema.Name} requires named value fields.", statement.Span);
            foreach (var duplicate in fields.GroupBy(f => f.Name.Text).Where(g => g.Count() > 1))
                Fail(FirmamentV2TemplateExpansion.Prefix + "record-duplicate-field", $"Duplicate field '{duplicate.Key}' in {schema.Name}.", duplicate.Last().Span);
            if (requireAll)
                foreach (var missing in schema.Fields.Keys.Where(k => fields.All(f => f.Name.Text != k)))
                    Fail(FirmamentV2TemplateExpansion.Prefix + "record-missing-field", $"Missing field '{missing}' in {schema.Name}.", body.Span);
            foreach (var f in fields)
                if (!schema.Fields.TryGetValue(f.Name.Text, out var fieldType))
                    Fail(FirmamentV2TemplateExpansion.Prefix + "record-extra-field", $"Unknown field '{f.Name.Text}' in {schema.Name}.", f.Span);
                else if (TypeOf(f.Value, fieldType, stack) is { } actual && actual != CanonicalValueType(fieldType))
                    Fail(ConceptIrResolver.MatchArmTypeMismatch, $"Field '{f.Name.Text}' requires {fieldType}; found {actual}.", f.Value.Span);
        }
        ExpressionSyntax Select(MatchSyntax match)
        {
            var type = TypeOf(match, expected);
            if (type is null || !select) return match;
            var domain = TypeOf(match.Scrutinee);
            var scrutinee = new ChoiceRewriter(Select).Expression(match.Scrutinee);
            var tag = domain is null ? null : ChoiceTag(scrutinee, domain, locals, codes, new(StringComparer.Ordinal));
            if (tag is null) return match;
            var arm = match.Arms.SingleOrDefault(a => FirmamentChoiceBinding.Pattern(a.Pattern, domain!) == tag);
            if (arm is null)
            {
                Fail(ConceptIrResolver.SelectedBranchEvaluationFailure, $"Pattern selector has no arm for '{tag}'. Admitted cases: {string.Join(", ", match.Arms.Select(a => FirmamentSyntaxWriter.Expression(a.Pattern)))}.", match.Scrutinee.Span);
                return match;
            }
            selected?[FirmamentSyntaxValues.Reference(match.Scrutinee) ?? FirmamentSyntaxWriter.Expression(match.Scrutinee)] = tag;
            return new ChoiceRewriter(Select).Expression(arm.Value);
        }
        void Fail(string code, string message, FirmamentV2SourceSpan span) => ChoiceError(new(code, "error", message)
            { SourceSpan = span, Stage = "Choice binding" }, codes, sourceOffset);
        var resolved = new ChoiceRewriter(Select).Expression(expression);
        return select ? BindEnumEqualities(resolved, locals, codes, new(StringComparer.Ordinal)) : resolved;
    }

    private string? ChoiceTag(ExpressionSyntax expression, string type, IReadOnlyDictionary<string, ChoiceInput>? locals,
        List<string> codes, HashSet<string> stack)
    {
        if (expression is MemberSyntax { Target: ReferenceSyntax owner } qualifiedCase && owner.Name.Text == type
            && Choices.Types.TryGetValue(type, out var domain) && domain.Cases.Contains(qualifiedCase.Member.Text, StringComparer.Ordinal)) return qualifiedCase.Member.Text;
        var name = FirmamentSyntaxValues.Reference(expression);
        var input = name is null ? null : ChoiceInputFor(name, locals, codes, true);
        if (input is not null)
        {
            if (input.Type.Length > 0 && CanonicalValueType(input.Type) != type) { Fail(FirmamentV2Parser.ExpressionTypeMismatch, $"Expected {type}; '{name}' has nominal type {input.Type}."); return null; }
            if (activeChoices.Count >= 128 || activeChoices.Contains(name!, StringComparer.Ordinal))
            { Fail(FirmamentV2Parser.ExpressionCycle, "Compile-time choice dependency cycle: " + string.Join(" -> ", activeChoices.Append(name!)) + "."); return null; }
            activeChoices.Add(name!);
            try
            {
                var before = diagnostics.Count;
                var value = ResolveChoices(input.Value, codes, locals, type);
                return diagnostics.Count > before ? null : ChoiceTag(value, type, locals, codes, stack);
            }
            finally { activeChoices.Remove(name!); }
        }
        if (type is "Bool" or "Int")
        {
            expression = BindEnumEqualities(expression, locals, codes, stack);
            var bindings = values.Values.ToDictionary(v => v.Name, v => v, StringComparer.Ordinal);
            var substitution = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
            Expand(expression, new(StringComparer.Ordinal));
            var normalized = new FirmamentSyntaxSubstitution(substitution).Expression(expression);
            var text = FirmamentSyntaxWriter.Expression(normalized);
            var parsed = FirmamentSyntaxParser.ParseExpression(text).Expression;
            if (parsed is null) return null;
            var bound = FirmamentV2Parser.BindCompileTimeScalars([Scalar("__choiceCondition", type, parsed, parsed.Span)], text, "inline", codes, bindings.Values.ToArray());
            foreach (var d in bound.Diagnostics) ChoiceError(d with { SourceSpan = expression.Span }, codes);
            var result = bound.Values.FirstOrDefault(v => v.Name == "__choiceCondition")?.Value;
            return result?.Value is bool b ? b ? "true" : "false" : result?.NumericValue?.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            void Expand(ExpressionSyntax e, HashSet<string> visiting)
            {
                foreach (var r in FirmamentSyntaxTraversal.Expressions(e).Where(r => r is ReferenceSyntax or MemberSyntax))
                {
                    var n = FirmamentSyntaxValues.Reference(r);
                    if (n is null || !visiting.Add(n) || visiting.Count > 128) continue;
                    var value = locals?.GetValueOrDefault(n)?.Value ?? choiceInputs.GetValueOrDefault(n)?.Value ?? definitions.GetValueOrDefault(n)?.Initializer;
                    if (value is null) continue;
                    Expand(value, visiting);
                    substitution[n] = new FirmamentSyntaxSubstitution(substitution).Expression(value);
                }
            }

        }
        var caseName = FirmamentChoiceBinding.Pattern(expression, type);
        if (caseName is not null && Choices.Types.TryGetValue(type, out var choice) && choice.Cases.Contains(caseName, StringComparer.Ordinal)) return caseName;
        Fail(ConceptIrResolver.UnknownEnumVariant, $"'{FirmamentSyntaxWriter.Expression(expression)}' is not a case of {type}.");
        return null;
        void Fail(string code, string message) => ChoiceError(new(code, "error", message) { SourceSpan = expression.Span, Stage = "Choice binding" }, codes);
    }

    private ExpressionSyntax BindEnumEqualities(ExpressionSyntax expression, IReadOnlyDictionary<string, ChoiceInput>? locals,
        List<string> codes, HashSet<string> stack)
    {
        return new EnumEqualityProjection(compare =>
            {
                var domain = KnownType(compare.Left) ?? KnownType(compare.Right);
                if (domain is null || !IsChoice(domain)) return null;
                var left = ChoiceTag(compare.Left, domain, locals, codes, stack);
                var right = ChoiceTag(compare.Right, domain, locals, codes, stack);
                if (left is null || right is null) return null;
                var result = compare.Operator == BinaryOperator.Equal ? left == right : left != right;
                return new LiteralSyntax(LiteralKind.Boolean, result ? "true" : "false", compare.Span);
        }).Expression(expression);

        string? KnownType(ExpressionSyntax e)
        {
            var n = FirmamentSyntaxValues.Reference(e);
            var t = n is null ? null : locals?.GetValueOrDefault(n)?.Type ?? choiceInputs.GetValueOrDefault(n)?.Type;
            if (t is not null) return CanonicalValueType(t);
            return e is MemberSyntax { Target: ReferenceSyntax owner } && IsChoice(owner.Name.Text) ? owner.Name.Text : null;
        }
    }

    private sealed class ChoiceRewriter(Func<MatchSyntax, ExpressionSyntax> select) : FirmamentSyntaxRewriter
    {
        internal override ExpressionSyntax Expression(ExpressionSyntax e) => e is MatchSyntax m ? select(m) : base.Expression(e);
    }

    private sealed class EnumEqualityProjection(Func<BinarySyntax, ExpressionSyntax?> bind) : FirmamentSyntaxRewriter
    {
        internal override ExpressionSyntax Expression(ExpressionSyntax expression) => expression is BinarySyntax { Operator: BinaryOperator.Equal or BinaryOperator.NotEqual } binary
            ? bind(binary) ?? base.Expression(expression) : base.Expression(expression);
    }
}
