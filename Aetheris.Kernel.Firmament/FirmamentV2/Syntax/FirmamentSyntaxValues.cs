using System.Globalization;

namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

/// <summary>Literal and bounded dimensional binding over syntax. This preserves the
/// existing profile evaluator's one-unit, dimension-0/1 arithmetic contract.</summary>
internal static class FirmamentSyntaxValues
{
    public static string? Reference(ExpressionSyntax? value) => value switch
    {
        ReferenceSyntax r => r.Name.Text,
        MemberSyntax m when Reference(m.Target) is { } target => target + "." + m.Member.Text,
        UnarySyntax { Operator: UnaryOperator.Positive } u when Reference(u.Operand) is { } axis => "+" + axis,
        UnarySyntax { Operator: UnaryOperator.Negative } u when Reference(u.Operand) is { } axis => "-" + axis,
        LiteralSyntax { Kind: LiteralKind.String } s => System.Text.Json.JsonSerializer.Deserialize<string>(s.Raw),
        _ => null
    };

    public static bool TryMeasure(ExpressionSyntax? expression, string unit, out double value)
    {
        value = double.NaN;
        if (!Evaluate(expression, unit, out var result, out var dimension) || dimension != (unit.Length == 0 ? 0 : 1)) return false;
        value = result; return double.IsFinite(value);
    }

    private static bool Evaluate(ExpressionSyntax? expression, string unit, out double value, out int dimension)
    {
        value = double.NaN; dimension = 0;
        switch (expression)
        {
            case LiteralSyntax { Kind: LiteralKind.Number } literal:
                return double.TryParse(literal.Raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
            case QuantitySyntax quantity when quantity.Unit.Text == unit:
                if (!Evaluate(quantity.Value, unit, out value, out dimension) || dimension != 0) return false;
                dimension = 1; return true;
            case UnarySyntax { Operator: UnaryOperator.Positive or UnaryOperator.Negative } unary:
                if (!Evaluate(unary.Operand, unit, out value, out dimension)) return false;
                if (unary.Operator == UnaryOperator.Negative) value = -value;
                return true;
            case SequenceSyntax { Kind: SequenceKind.Tuple, Items.Count: 1 } group:
                return Evaluate(group.Items[0].Value, unit, out value, out dimension);
            case BinarySyntax binary:
                if (!Evaluate(binary.Left, unit, out var left, out var ld) || !Evaluate(binary.Right, unit, out var right, out var rd)) return false;
                switch (binary.Operator)
                {
                    case BinaryOperator.Add or BinaryOperator.Subtract when ld == rd:
                        dimension = ld; value = binary.Operator == BinaryOperator.Add ? left + right : left - right; break;
                    case BinaryOperator.Multiply:
                        dimension = ld + rd; value = left * right; break;
                    case BinaryOperator.Divide when Math.Abs(right) > double.Epsilon:
                        dimension = ld - rd; value = left / right; break;
                    default: return false;
                }
                return dimension is >= 0 and <= 1 && double.IsFinite(value);
            default: return false;
        }
    }

    public static bool Vector(ExpressionSyntax? expression, string unit, out double[] values)
    {
        values = [];
        var sharedUnit = expression as QuantitySyntax;
        var inner = sharedUnit?.Value ?? expression;
        IReadOnlyList<ArgumentSyntax>? items = inner switch
        {
            SequenceSyntax { Kind: SequenceKind.List } sequence => sequence.Items,
            CallSyntax { Target: ReferenceSyntax { Name.Text: "Point2" or "Point3" or "Vector2" or "Vector3" } } call => call.Arguments,
            _ => null
        };
        if (items is null) return false;
        var result = new double[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i].Value;
            if (sharedUnit is not null) item = new QuantitySyntax(item, sharedUnit.Unit, item.Span);
            if (!TryMeasure(item, unit, out result[i])) return false;
        }
        values = result; return true;
    }
}

/// <summary>Each semantic owner consumes its actual fields, then closes its scope.
/// No second field schema or spelling allowlist is maintained here.</summary>
internal sealed class FirmamentSyntaxFields(DeclarationSyntax declaration, List<string> diagnostics)
{
    private readonly HashSet<FieldSyntax> consumed = [];
    public DeclarationSyntax Declaration => declaration;
    public ExpressionSyntax? Take(string name)
    {
        var fields = declaration.Body?.Statements.OfType<FieldSyntax>().Where(f => SameField(f.Name.Text, name)).ToArray() ?? [];
        foreach (var field in fields) consumed.Add(field);
        if (fields.Length > 1) diagnostics.Add($"firmament-v2-field-duplicate:{declaration.Type.Name.Text}:{name}");
        return fields.Length == 1 ? fields[0].Value : null;
    }
    public bool Measure(string name, string unit, out double value) => FirmamentSyntaxValues.TryMeasure(Take(name), unit, out value);
    public double Number(string name, string unit, double fallback = double.NaN)
    {
        var expression = Take(name);
        if (expression is null) return fallback;
        if (FirmamentSyntaxValues.TryMeasure(expression, unit, out var value)) return value;
        Invalid(name); return double.NaN;
    }
    public IReadOnlyList<FieldSyntax> TakeAll(string name)
    {
        var fields = declaration.Body?.Statements.OfType<FieldSyntax>().Where(f => SameField(f.Name.Text, name)).ToArray() ?? [];
        foreach (var field in fields) consumed.Add(field);
        return fields;
    }
    public void Invalid(string name) => diagnostics.Add($"firmament-v2-field-value-invalid:{declaration.Type.Name.Text}:{name}");
    // Some owners intentionally try a reference and then an inline vector. A
    // conversion probe is not an error; the owner decides which forms it admits.
    public string? Word(string name) => FirmamentSyntaxValues.Reference(Take(name));
    public bool Vector(string name, string unit, out double[] values) => FirmamentSyntaxValues.Vector(Take(name), unit, out values);
    public void Complete()
    {
        if (declaration.Parameters.Count > 0 || declaration.Annotation is not null || declaration.Initializer is not null)
            diagnostics.Add($"{FirmamentV2Parser.CanonicalDeclarationUnknown}:{declaration.Type.Name.Text}:header");
        if (declaration.Clauses.Count > 0)
            diagnostics.Add($"firmament-v2-placement-unsupported:{declaration.Type.Name.Text}:{declaration.Name?.Text}: This field-based construct does not admit header placement clauses; use its documented fields.");
        foreach (var field in declaration.Body?.Statements.OfType<FieldSyntax>() ?? [])
            if (!consumed.Contains(field)) diagnostics.Add($"{FirmamentV2Parser.CanonicalFieldUnknown}:{declaration.Type.Name.Text}:{field.Name.Text}");
        foreach (var statement in declaration.Body?.Statements.Where(s => s is not FieldSyntax) ?? [])
            diagnostics.Add($"{FirmamentV2Parser.CanonicalDeclarationUnknown}:{declaration.Type.Name.Text}:{(statement as DeclarationSyntax)?.Type.Name.Text ?? "expression"}");
    }
    public static bool SameField(string authored, string canonical) => authored == canonical || authored == FirmamentSourceSpelling.PreferredField(canonical);
    public IReadOnlyList<FirmamentV2AuthoredField> AuthoredFields => (declaration.Body?.Statements.OfType<FieldSyntax>() ?? [])
        .Select(f => new FirmamentV2AuthoredField(f.Name.Text, f.Span, f.Value.Span)).ToArray();
}
