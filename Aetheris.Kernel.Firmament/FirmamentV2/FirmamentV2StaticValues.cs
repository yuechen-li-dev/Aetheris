using System.Globalization;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public static partial class FirmamentV2Parser
{
    internal static FirmamentV2PrimitiveType? CompileTimeArithmeticType(FirmamentV2PrimitiveType left, string op, FirmamentV2PrimitiveType right) =>
        ExpressionBinder.ResultType(left, op, right);
    private sealed record StaticValueBinding(DeclarationSyntax Model,
        IReadOnlyList<FirmamentV2LetDeclaration> Declarations, IReadOnlyList<FirmamentV2BoundLet> Values,
        IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics);

    // Static scalars share the existing typed dependency binder and tolerance IR.
    // Only syntax is projected here; there is no text substitution or second evaluator.
    private static StaticValueBinding BindStaticValues(DocumentSyntax syntax, DeclarationSyntax model, string source,
        List<string> diagnostics, IReadOnlyList<FirmamentV2BoundLet>? inputs = null, FirmamentCompileTimeValues? compileTime = null)
    {
        var declarations = new List<FirmamentV2LetDeclaration>();
        var located = new List<FirmamentV2ValidationDiagnostic>();
        var scope = syntax.Statements.Where(s => !ReferenceEquals(s, model)).Concat(model.Body!.Statements).ToArray();
        var names = scope.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text != "Static")
            .Select(d => d.Name?.Text).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var statics = scope.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Static").ToArray();
        void Error(string code, string message, FirmamentV2SourceSpan span, string? name = null)
        {
            diagnostics.Add(code);
            located.Add(new(code, "error", message, Target: name) { SourceFile = syntax.SourceFile, SourceSpan = span });
        }
        foreach (var nested in MechanicalDeclarations(model.Body!.Statements).Where(d => d.Type.Name.Text == "Static" && !statics.Contains(d)))
            Error("firmament-v2-static-scalar-scope-unsupported", "Declare scalar Static values directly in the Model or its module; nested construction scopes do not introduce scalar environments.", nested.Span, nested.Name?.Text);
        FirmamentV2PrimitiveType? InferType(ExpressionSyntax? e, HashSet<string> visiting)
        {
            if (e is null) return null;
            var type = FirmamentChoiceBinding.Infer(e, reference =>
            {
                var name = FirmamentSyntaxValues.Reference(reference);
                if (name is null || visiting.Count >= 128 || visiting.Contains(name)) return null;
                var nested = new HashSet<string>(visiting, StringComparer.Ordinal) { name };
                return InferReference(name, nested)?.ToString();
            }, match => match.Arms.Count == 0 ? null : InferType(match.Arms[0].Value, visiting)?.ToString());
            return type is not null && TryParseLetType(type, out var primitive) ? primitive : null;
        }
        FirmamentV2PrimitiveType? InferReference(string name, HashSet<string> visiting)
        {
            var matches = statics.Where(d => d.Name?.Text == name).ToArray();
            if (matches.Length != 1) return inputs?.FirstOrDefault(v => v.Name == name)?.Type;
            var d = matches[0];
            return d.Annotation is null ? InferType(d.Initializer, visiting)
                : FirmamentSyntaxValues.Reference(d.Annotation) is { } annotation && TryParseLetType(annotation, out var t) ? t : null;
        }
        foreach (var d in statics)
        {
            var typeName = FirmamentSyntaxValues.Reference(d.Annotation);
            var declared = typeName is not null && TryParseLetType(typeName, out var explicitType) ? explicitType
                : d.Annotation is null ? inputs?.FirstOrDefault(v => v.Name == d.Name?.Text)?.Type ?? InferType(d.Initializer, new(StringComparer.Ordinal)) : null;
            if (d.Name is null || declared is not { } type
                || d.Initializer is null || d.Body is not null || d.Modifiers.Count > 0 || d.Type.Arguments.Count > 0
                || d.Parameters.Count > 0 || d.Clauses.Count > 0 || d.ResultType is not null || d.Occurrence)
            {
                Error("firmament-v2-static-scalar-declaration-invalid",
                    "Expected Static name: Length, Angle, Int, Float, Bool or String = compile-time expression, or an initializer with an inferable scalar type. Record and Set values use their documented declaration forms.", d.Span, d.Name?.Text);
                continue;
            }
            if (!names.Add(d.Name.Text)) { Error(DuplicateName, $"Duplicate compile-time name '{d.Name.Text}'.", d.Span, d.Name.Text); continue; }
            var valueSyntax = d.Initializer;
            if (inputs?.FirstOrDefault(v => v.Name == d.Name.Text) is { } prebound)
                valueSyntax = Literal(prebound, valueSyntax.Span);
            FirmamentV2Tolerance? tolerance = null;
            if (valueSyntax is ToleranceSyntax t)
            {
                valueSyntax = t.Nominal;
                tolerance = BindTolerance(t, type, (code, message, span) => Error(code, message, span, d.Name.Text));
            }
            if (ValueExpression(valueSyntax, type) is { } expression)
                declarations.Add(new(d.Name.Text, type, expression, d.Span, tolerance));
            else Error(ExpressionUnsupported, "Expected a typed literal, scalar reference or dimensional arithmetic; tol belongs at the end of a Static initializer.", valueSyntax.Span, d.Name.Text);
        }

        var before = diagnostics.Count;
        var imported = (inputs ?? []).Where(v => declarations.All(d => d.Name != v.Name)).ToArray();
        // A value already checked by this compilation's specialization environment is
        // an input, not a second evaluation (and must not emit duplicate drop warnings).
        var checkedDeclarations = declarations.Select(d => inputs?.FirstOrDefault(v => v.Name == d.Name && v.Type == d.DeclaredType) is { } value
            ? d with { ValueExpression = new FirmamentV2LiteralExpression(value.Value), Tolerance = value.Tolerance } : d);
        var values = BindLets(checkedDeclarations.Concat(imported.Select(v => new FirmamentV2LetDeclaration(v.Name, v.Type,
            new FirmamentV2LiteralExpression(v.Value), v.SourceSpan, v.Tolerance))).ToArray(), [], [], diagnostics, located)
            .Select(v => inputs?.FirstOrDefault(i => i.Name == v.Name) is { } input
                ? v with { Origins = input.Origins, Expression = input.Expression, Dependencies = input.Dependencies }
                : v with { Origins = v.Dependencies?.SelectMany(n => inputs?.FirstOrDefault(i => i.Name == n)?.Origins ?? []).Distinct().ToArray() ?? [] }).ToArray();
        foreach (var value in values.Where(v => v.Value.NumericValue is { } n && !double.IsFinite(n)))
            Error("firmament-v2-static-scalar-nonfinite", $"Compile-time value '{value.Name}' is not finite.", value.SourceSpan, value.Name);
        foreach (var value in values.Where(v => v.Type == FirmamentV2PrimitiveType.Int && v.Value.NumericValue is { } n && (n < int.MinValue || n > int.MaxValue)))
            Error("firmament-v2-static-scalar-out-of-range", $"Compile-time Int '{value.Name}' is outside the supported integer range.", value.SourceSpan, value.Name);
        var unresolved = diagnostics.Skip(before).Contains(ExpressionUnknownSymbol)
            ? statics.Where(d => d.Initializer is not null).SelectMany(d => FirmamentSyntaxTraversal.Expressions(d.Initializer!))
                .OfType<ReferenceSyntax>().Where(r => declarations.All(d => d.Name != r.Name.Text)).ToArray() : [];
        foreach (var code in diagnostics.Skip(before).Distinct().Where(code => !located.Any(d => d.Code == code)))
        {
            // Dependency errors may involve several declarations. Use the enclosing
            // scope rather than inventing an exact failing token in the graph binder.
            located.Add(new(code, code == ToleranceDroppedThroughArithmetic ? "warning" : "error", code switch
            {
                ToleranceDroppedThroughArithmetic => "Arithmetic computes a nominal value and does not propagate tolerances. Add an explicit tol allowance to the result when it is a requirement.",
                ExpressionUnknownSymbol when unresolved.Length > 0 => "Unknown compile-time scalar reference: " + string.Join(", ", unresolved.Select(r => r.Name.Text).Distinct(StringComparer.Ordinal)) + ".",
                ExpressionUnknownSymbol => "A referenced Static definition could not be bound. Resolve its preceding diagnostic.",
                ExpressionCycle => "Compile-time scalar definitions contain a dependency cycle.",
                ExpressionDivisionByZero => "Compile-time scalar arithmetic divides by zero.",
                _ => "Compile-time scalar binding failed: " + code
            }, Target: model.Name?.Text) { SourceFile = syntax.SourceFile,
                SourceSpan = code == ExpressionUnknownSymbol && unresolved.Length == 1 ? unresolved[0].Span : model.Span,
                LocationNote = code == ExpressionUnknownSymbol && unresolved.Length == 1 ? null : "Dependency binding reports the enclosing compile-time scope." });
        }
        var map = values.ToDictionary(v => v.Name, StringComparer.Ordinal);
        ExpressionSyntax Nominal(ExpressionSyntax e) => e switch
        {
            MatchSyntax m when compileTime is not null => Select(m),
            MemberSyntax m when FirmamentSyntaxValues.Reference(m) is { } name && map.TryGetValue(name, out var value) => Literal(value, e.Span),
            ReferenceSyntax r when map.TryGetValue(r.Name.Text, out var value) => Literal(value, e.Span),
            BinarySyntax b => b with { Left = Nominal(b.Left), Right = Nominal(b.Right) },
            UnarySyntax u => u with { Operand = Nominal(u.Operand) },
            QuantitySyntax q => q with { Value = Nominal(q.Value) },
            SequenceSyntax s => s with { Items = s.Items.Select(a => a with { Value = Nominal(a.Value) }).ToArray() },
            CallSyntax c => c with { Arguments = c.Arguments.Select(a => a with { Value = Nominal(a.Value) }).ToArray() },
            ToleranceSyntax t => RejectInline(t),
            _ => e
        };
        ExpressionSyntax Select(MatchSyntax match)
        {
            var before = compileTime!.Diagnostics.Count;
            var selected = compileTime.ResolveChoices(match, diagnostics);
            located.AddRange(compileTime.Diagnostics.Skip(before));
            return selected is MatchSyntax ? selected : Nominal(selected);
        }
        ExpressionSyntax RejectInline(ToleranceSyntax t)
        {
            Error(ToleranceUnsupported, "Declare a named Static value with tol, use its nominal value in geometry, then project it with Pmi dimension: name.", t.ToleranceSpan);
            return t;
        }
        StatementSyntax Project(StatementSyntax s) => s switch
        {
            // These names belong to their semantic owners, not scalar lookup.
            DeclarationSyntax { Type.Name.Text: "Static" or "Pmi" or "Concept" or "Feature" or "Template" } => s,
            DeclarationSyntax d when d.Modifiers.Count == 0 => d with
            {
                // Polyhedron's sole generic is a compile-time numeric face count;
                // type/variant names in other owners must never become scalar lookups.
                Type = d.Type.Name.Text == "Polyhedron" ? d.Type with
                    { Arguments = d.Type.Arguments.Select(a => a with { Value = Nominal(a.Value) }).ToArray() } : d.Type,
                Body = d.Body is null ? null : d.Body with { Statements = d.Body.Statements.Select(Project).ToArray() }
            },
            FieldSyntax f => f with { Value = Nominal(f.Value) },
            _ => s
        };
        var projected = (DeclarationSyntax)Project(model);
        return new(projected, declarations, values, located);

        FirmamentV2ValueExpression? ValueExpression(ExpressionSyntax e, FirmamentV2PrimitiveType expected)
        {
            var text = source.Substring(e.Span.Start, e.Span.Length);
            if (e is MemberSyntax m && FirmamentSyntaxValues.Reference(m) is { } member) return new FirmamentV2IdentifierReferenceExpression(member, text);
            if (e is ReferenceSyntax r) return new FirmamentV2IdentifierReferenceExpression(r.Name.Text, text);
            if (e is SequenceSyntax { Kind: SequenceKind.Tuple, Items.Count: 1 } group) return ValueExpression(group.Items[0].Value, expected);
            if (e is BinarySyntax b)
            {
                var op = b.Operator switch { BinaryOperator.Add => "+", BinaryOperator.Subtract => "-", BinaryOperator.Multiply => "*", BinaryOperator.Divide => "/",
                    BinaryOperator.Equal => "==", BinaryOperator.NotEqual => "!=", BinaryOperator.Less => "<", BinaryOperator.LessEqual => "<=", BinaryOperator.Greater => ">", BinaryOperator.GreaterEqual => ">=",
                    BinaryOperator.And => "&&", BinaryOperator.Or => "||", _ => null };
                return op is not null && ValueExpression(b.Left, expected) is { } l && ValueExpression(b.Right, expected) is { } rgt
                    ? new FirmamentV2BinaryExpression(l, op, rgt, text) : null;
            }
            if (e is UnarySyntax { Operator: UnaryOperator.Not } not && ValueExpression(not.Operand, FirmamentV2PrimitiveType.Bool) is { } operand)
                return new FirmamentV2UnaryExpression("!", operand, text);
            var unit = e is QuantitySyntax q ? q.Unit.Text : e is UnarySyntax { Operand: QuantitySyntax uq } ? uq.Unit.Text : "";
            var inferred = unit switch { "mm" => FirmamentV2PrimitiveType.Length, "deg" => FirmamentV2PrimitiveType.Angle, "s" or "ms" => FirmamentV2PrimitiveType.Time,
                "" => e is LiteralSyntax { Kind: LiteralKind.Boolean } ? FirmamentV2PrimitiveType.Bool
                    : e is LiteralSyntax { Kind: LiteralKind.String } ? FirmamentV2PrimitiveType.String
                    : text.Contains('.') || text.Contains('e', StringComparison.OrdinalIgnoreCase) ? FirmamentV2PrimitiveType.Float : FirmamentV2PrimitiveType.Int, _ => (FirmamentV2PrimitiveType?)null };
            if (inferred is null) return null;
            if (inferred is FirmamentV2PrimitiveType.String or FirmamentV2PrimitiveType.Bool)
            {
                if (e is not LiteralSyntax literal || literal.Kind != (inferred == FirmamentV2PrimitiveType.String ? LiteralKind.String : LiteralKind.Boolean)) return null;
                try
                {
                    object value = inferred == FirmamentV2PrimitiveType.String
                        ? System.Text.Json.JsonSerializer.Deserialize(literal.Raw, FirmamentJsonContext.Default.String)!
                        : bool.Parse(literal.Raw);
                    return new FirmamentV2LiteralExpression(new(inferred.Value, value, Raw: literal.Raw));
                }
                catch (System.Text.Json.JsonException) { return null; }
            }
            if (!FirmamentSyntaxValues.TryMeasure(e, inferred == FirmamentV2PrimitiveType.Time ? "s" : unit, out var n) || inferred == FirmamentV2PrimitiveType.Int && (n < int.MinValue || n > int.MaxValue || n != Math.Truncate(n))) return null;
            // Float declarations admit integer-written constants, as legacy let did.
            if (inferred == FirmamentV2PrimitiveType.Int && expected == FirmamentV2PrimitiveType.Float) inferred = expected;
            return new FirmamentV2LiteralExpression(new(inferred.Value, inferred == FirmamentV2PrimitiveType.Int ? (object)(int)n : n, n, inferred == FirmamentV2PrimitiveType.Time ? "s" : unit.Length == 0 ? null : unit, text));
        }
    }

    internal static FirmamentV2Tolerance? BindTolerance(ToleranceSyntax syntax, FirmamentV2PrimitiveType type,
        Action<string, string, FirmamentV2SourceSpan> error)
    {
        void Fail(string code, string message) => error(code, message, syntax.ToleranceSpan);
        if (type is not (FirmamentV2PrimitiveType.Length or FirmamentV2PrimitiveType.Angle))
        { Fail(ToleranceInvalidType, "tol requires a Length or Angle value."); return null; }
        var unit = type == FirmamentV2PrimitiveType.Length ? "mm" : "deg";
        var asymmetric = syntax.Plus is UnarySyntax { Operator: UnaryOperator.Positive };
        if (asymmetric && syntax.Minus is null)
        { Fail(ToleranceMissingMinus, "A +upper tolerance requires -lower; use tol 0.1mm for a symmetric allowance."); return null; }
        if (!FirmamentSyntaxValues.TryMeasure(syntax.Plus, unit, out var plus)
            || syntax.Minus is not null && !FirmamentSyntaxValues.TryMeasure(syntax.Minus, unit, out _))
        { Fail(ToleranceUnitMismatch, $"Tolerance must have the value's dimensional unit ({unit})."); return null; }
        var minus = syntax.Minus is null ? plus : FirmamentSyntaxValues.TryMeasure(syntax.Minus, unit, out var m) ? m : double.NaN;
        if (!double.IsFinite(plus) || !double.IsFinite(minus) || plus < 0 || minus < 0)
        { Fail(ToleranceNegativeBilateral, "Tolerance allowances must be finite nonnegative magnitudes; write tol +upper -lower."); return null; }
        return new(asymmetric ? FirmamentV2ToleranceKind.Asymmetric : FirmamentV2ToleranceKind.Bilateral,
            plus, minus, unit, type, syntax.ToleranceSpan);
    }

    // Template/Feature scalar arguments use precisely the same dimensional graph binder as Static.
    // The caller owns scope and supplies AST declarations and already checked input values.
    internal static (IReadOnlyList<FirmamentV2BoundLet> Values, IReadOnlyList<FirmamentV2ValidationDiagnostic> Diagnostics)
        BindCompileTimeScalars(IReadOnlyList<DeclarationSyntax> declarations, string source, string sourceFile,
            List<string> diagnostics, IReadOnlyList<FirmamentV2BoundLet>? inputs = null)
    {
        var span = new FirmamentV2SourceSpan(0, source.Length);
        var model = new DeclarationSyntax(new(new("Model", span), [], span), new("CompileTime", span), [], [],
            null, null, [], null, new(declarations, span) { Delimited = false }, false, span);
        var binding = BindStaticValues(new(sourceFile, [model], [], span), model, source, diagnostics, inputs);
        return (binding.Values, binding.Diagnostics);
    }

    private static ExpressionSyntax Literal(FirmamentV2BoundLet value, FirmamentV2SourceSpan span)
    {
        ExpressionSyntax literal = new LiteralSyntax(value.Type switch
        {
            FirmamentV2PrimitiveType.Bool => LiteralKind.Boolean,
            FirmamentV2PrimitiveType.String => LiteralKind.String,
            _ => LiteralKind.Number
        }, value.Type switch
        {
            FirmamentV2PrimitiveType.Bool => (bool)value.Value.Value ? "true" : "false",
            FirmamentV2PrimitiveType.String => System.Text.Json.JsonSerializer.Serialize((string)value.Value.Value, FirmamentJsonContext.Default.String),
            _ => value.Value.NumericValue!.Value.ToString("R", CultureInfo.InvariantCulture)
        }, span);
        return value.Value.Unit is { } unit ? new QuantitySyntax(literal, new(unit, span), span) : literal;
    }

    private static IReadOnlyList<FirmamentV2ValidationDiagnostic> ValidateStaticPmi(FirmamentV2PmiBlock? block,
        FirmamentV2BoundPmiBlock? bound, IReadOnlyList<FirmamentV2ModifyBlock> modifies,
        IReadOnlyList<FirmamentV2BoundLet> values, List<string> diagnostics)
    {
        var result = new List<FirmamentV2ValidationDiagnostic>();
        foreach (var record in block?.Records ?? [])
        {
            if (!record.Fields.TryGetValue("dimension", out var dimension)) continue;
            var value = values.FirstOrDefault(v => v.Name == dimension.Source);
            if (value?.Tolerance is null) continue; // Existing PMI type/admission errors remain authoritative.
            var requirement = value.Origins.FirstOrDefault();
            var displayName = requirement?.Binding ?? value.Name;
            void Error(string code, string message)
            {
                diagnostics.Add(code);
                result.Add(new(code, "error", message, "dimension", record.Name) { SourceSpan = requirement?.SourceSpan ?? record.SourceSpan,
                    IsOriginalSource = requirement is not null, Stage = "Binding",
                    LocationNote = requirement is null ? null : "Location identifies the original projected requirement." });
            }
            if (record.Fields.Keys.Any(k => k is "value" or "diameter" or "tolerance"))
                Error("firmament-v2-pmi-typed-dimension-override", $"PMI '{record.Name}' projects '{displayName}'; do not override its nominal value or tolerance. Declare a separate typed requirement instead.");
            var resolved = bound?.Dimensions.FirstOrDefault(d => d.Name == record.Name);
            var holeName = resolved?.HoleWallTarget?.HoleName ?? resolved?.Targets.FirstOrDefault();
            var hole = modifies.SelectMany(m => m.SemanticHoles).FirstOrDefault(h => h.Name == holeName);
            if (hole is null || value.Value.NumericValue is not { } nominal) continue;
            var delta = hole.ShaftDiameter - nominal;
            if (delta > value.Tolerance.Plus + 1e-9 || -delta > value.Tolerance.Minus + 1e-9)
                Error("firmament-v2-pmi-typed-dimension-unsatisfied", $"Hole '{hole.Name}' has diameter {hole.ShaftDiameter:R}mm, outside '{displayName}' ({nominal:R}mm +{value.Tolerance.Plus:R}mm -{value.Tolerance.Minus:R}mm).");
        }
        return result;
    }
}
