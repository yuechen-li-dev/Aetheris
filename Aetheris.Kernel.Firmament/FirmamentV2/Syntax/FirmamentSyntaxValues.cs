namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

/// <summary>Literal and bounded dimensional binding over syntax. This preserves the
/// existing profile evaluator's one-unit, dimension-0/1 arithmetic contract.</summary>
public static class FirmamentSyntaxValues
{
    public static string? Reference(ExpressionSyntax? value) => value switch
    {
        ReferenceSyntax r => r.Name.Text,
        MemberSyntax m when Reference(m.Target) is { } target => target + "." + m.Member.Text,
        UnarySyntax { Operator: UnaryOperator.Positive } u when Reference(u.Operand) is { } axis => "+" + axis,
        UnarySyntax { Operator: UnaryOperator.Negative } u when Reference(u.Operand) is { } axis => "-" + axis,
        LiteralSyntax { Kind: LiteralKind.String } s => System.Text.Json.JsonSerializer.Deserialize(s.Raw, FirmamentJsonContext.Default.String),
        _ => null
    };

    public static bool TryMeasure(ExpressionSyntax? expression, string unit, out double value, string? implicitUnit = null)
    {
        value = double.NaN;
        if (!FirmamentNumericEvaluation.TryEvaluate(expression, out var result)
            || result.Unit != unit && !(result.Unit.Length == 0 && unit == implicitUnit)) return false;
        value = result.Number; return true;
    }

    public static bool Vector(ExpressionSyntax? expression, string unit, out double[] values, string? implicitUnit = null)
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
        if (items is null || items.Any(item => item.Name is not null || item.Default is not null)) return false;
        var result = new double[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i].Value;
            if (sharedUnit is not null) item = new QuantitySyntax(item, sharedUnit.Unit, item.Span);
            if (!TryMeasure(item, unit, out result[i], implicitUnit)) return false;
        }
        values = result; return true;
    }
    internal static List<(string Text, int Start)> Components(string text)
    {
        var parsed = FirmamentSyntaxParser.ParseExpression("[" + text + "]");
        return parsed.Expression is SequenceSyntax s ? s.Items.Select(a => (text.Substring(a.Value.Span.Start - 1, a.Value.Span.Length), a.Value.Span.Start - 1)).ToList() : [];
    }

}

/// <summary>Each semantic owner consumes its actual fields, then closes its scope.
/// No second field schema or spelling allowlist is maintained here.</summary>
public sealed class FirmamentSyntaxFields(DeclarationSyntax declaration, List<string> diagnostics, string? implicitUnit = null,
    Func<ExpressionSyntax, ExpressionSyntax>? bind = null)
{
    private readonly HashSet<FieldSyntax> consumed = [];
    public DeclarationSyntax Declaration => declaration;
    public ExpressionSyntax? Take(string name)
    {
        var fields = declaration.Body?.Statements.OfType<FieldSyntax>().Where(f => SameField(f.Name.Text, name)).ToArray() ?? [];
        foreach (var field in fields) consumed.Add(field);
        if (fields.Length > 1) diagnostics.Add($"firmament-v2-field-duplicate:{declaration.Type.Name.Text}:{name}");
        return fields.Length == 1 ? bind?.Invoke(fields[0].Value) ?? fields[0].Value : null;
    }
    public bool Measure(string name, string unit, out double value) => FirmamentSyntaxValues.TryMeasure(Take(name), unit, out value, implicitUnit);
    public double Number(string name, string unit, double fallback = double.NaN)
    {
        var expression = Take(name);
        if (expression is null) return fallback;
        if (FirmamentSyntaxValues.TryMeasure(expression, unit, out var value, implicitUnit)) return value;
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
    public string? Reference(string name, string? fallback = null)
    {
        var expression = Take(name);
        if (expression is null) return fallback;
        var value = FirmamentSyntaxValues.Reference(expression);
        if (value is null) Invalid(name);
        return value;
    }
    public bool Vector(string name, string unit, out double[] values) => FirmamentSyntaxValues.Vector(Take(name), unit, out values, implicitUnit);
    public void Complete(IReadOnlySet<StatementSyntax>? consumedStatements = null, bool admitAnnotation = false, bool admitInitializer = false, bool admitPlacement = false)
    {
        if (declaration.Occurrence || declaration.Parameters.Count > 0 || (!admitAnnotation && declaration.Annotation is not null) || declaration.ResultType is not null || (!admitInitializer && declaration.Initializer is not null))
            diagnostics.Add($"{FirmamentV2Parser.CanonicalDeclarationUnknown}:{declaration.Type.Name.Text}:header");
        if (!admitPlacement && declaration.Clauses.Count > 0)
            diagnostics.Add($"firmament-v2-placement-unsupported:{declaration.Type.Name.Text}:{declaration.Name?.Text}: This field-based construct does not admit header placement clauses; use its documented fields.");
        foreach (var field in declaration.Body?.Statements.OfType<FieldSyntax>() ?? [])
            if (!consumed.Contains(field)) diagnostics.Add($"{FirmamentV2Parser.CanonicalFieldUnknown}:{declaration.Type.Name.Text}:{field.Name.Text}");
        foreach (var statement in declaration.Body?.Statements.Where(s => s is not FieldSyntax && consumedStatements?.Contains(s) != true) ?? [])
            diagnostics.Add($"{FirmamentV2Parser.CanonicalDeclarationUnknown}:{declaration.Type.Name.Text}:{(statement as DeclarationSyntax)?.Type.Name.Text ?? "expression"}");
    }
    public static FirmamentSyntaxFields Object(ObjectSyntax value, string? name, List<string> diagnostics)
    {
        var type = value.Type as ReferenceSyntax;
        var typeName = type?.Name ?? new IdentifierSyntax("object", value.Span);
        if (type is null) diagnostics.Add($"{FirmamentV2Parser.CanonicalDeclarationUnknown}:object:header");
        return new(new(new(typeName, [], typeName.Span), value.Name ?? (name is null ? null : new(name, value.Span)),
            [], [], null, null, [], null, value.Body, false, value.Span), diagnostics);
    }
    public static bool SameField(string authored, string canonical) => authored == canonical || authored == FirmamentSourceSpelling.PreferredField(canonical)
        || canonical.Length>0 && authored == char.ToLowerInvariant(canonical[0])+canonical[1..];
    public IReadOnlyList<FirmamentV2AuthoredField> AuthoredFields => (declaration.Body?.Statements.OfType<FieldSyntax>() ?? [])
        .Select(f => new FirmamentV2AuthoredField(f.Name.Text, f.Span, f.Value.Span)).ToArray();
}
