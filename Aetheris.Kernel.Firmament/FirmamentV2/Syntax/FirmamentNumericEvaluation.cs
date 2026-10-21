using System.Globalization;

namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

/// <summary>Finite numeric evaluation over the shared expression grammar. Domain callers
/// supply their expected unit and, where admitted, bounded intrinsic functions.</summary>
internal static partial class FirmamentNumericEvaluation
{
    internal readonly record struct Value(double Number, string Unit);

    internal static bool TryEvaluate(string source, out Value value,
        IReadOnlyDictionary<string, Value>? variables = null,
        Func<string, IReadOnlyList<Value>, Value>? functions = null, bool typeOnly = false)
    {
        value = default;
        if (source.Length > 8192) return false;
        var parsed = FirmamentSyntaxParser.ParseExpression(source);
        return parsed.Expression is not null && parsed.Diagnostics.Count == 0
            && TryEvaluate(parsed.Expression, out value, variables, functions, typeOnly);
    }

    internal static bool TryEvaluate(ExpressionSyntax? expression, out Value value,
        IReadOnlyDictionary<string, Value>? variables = null,
        Func<string, IReadOnlyList<Value>, Value>? functions = null, bool typeOnly = false)
    {
        var remaining = 8192;
        bool Evaluate(ExpressionSyntax? node, int depth, out Value result)
        {
            result = default;
            if (depth > 128 || --remaining < 0) return false;
            switch (node)
            {
                case LiteralSyntax { Kind: LiteralKind.Number } n:
                    if (!double.TryParse(n.Raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                        || !double.IsFinite(number)) return false;
                    result = new(number, ""); return true;
                case ReferenceSyntax r:
                    return variables is not null && variables.TryGetValue(r.Name.Text, out result);
                case MemberSyntax m when FirmamentSyntaxValues.Reference(m) is { } name:
                    return variables is not null && variables.TryGetValue(name, out result);
                case QuantitySyntax q when q.Unit.Text is "mm" or "deg" or "rad" or "s" or "ms":
                    if (!Evaluate(q.Value, depth + 1, out var quantity) || quantity.Unit.Length != 0) return false;
                    result = new(q.Unit.Text == "ms" ? quantity.Number / 1000d : quantity.Number, q.Unit.Text == "ms" ? "s" : q.Unit.Text); return true;
                case UnarySyntax { Operator: UnaryOperator.Positive or UnaryOperator.Negative } u:
                    if (!Evaluate(u.Operand, depth + 1, out result)) return false;
                    if (u.Operator == UnaryOperator.Negative) result = result with { Number = -result.Number };
                    return true;
                case SequenceSyntax { Kind: SequenceKind.Tuple, Items.Count: 1 } group when group.Items[0].Name is null && group.Items[0].Default is null:
                    return Evaluate(group.Items[0].Value, depth + 1, out result);
                case BinarySyntax b:
                    return Evaluate(b.Left, depth + 1, out var left) && Evaluate(b.Right, depth + 1, out var right)
                        && TryBinary(left, b.Operator, right, out result, typeOnly);
                case CallSyntax { Target: ReferenceSyntax target } call:
                    var args = new List<Value>();
                    foreach (var argument in call.Arguments)
                    {
                        if (argument.Name is not null || argument.Default is not null
                            || !Evaluate(argument.Value, depth + 1, out var arg)) return false;
                        args.Add(arg);
                    }
                    if (target.Name.Text == "Pow" && args.Count == 2 && args.All(a => a.Unit.Length == 0))
                        result = new(typeOnly ? 1 : Math.Pow(args[0].Number, args[1].Number), "");
                    else if (target.Name.Text is "Sin" or "Cos" && args.Count == 1 && args[0].Unit is "" or "rad" or "deg")
                    {
                        // Bare numbers and explicit rad quantities are radians. An Angle
                        // retains its authored unit until this intrinsic consumes it.
                        var radians = args[0].Unit == "deg" ? args[0].Number * Math.PI / 180 : args[0].Number;
                        result = new(typeOnly ? 1 : target.Name.Text == "Sin" ? Math.Sin(radians) : Math.Cos(radians), "");
                    }
                    else if (functions is not null) result = functions(target.Name.Text, args);
                    else return false;
                    return (result.Unit is "" or "mm" or "deg" or "rad" or "s" or "ms") && (typeOnly || double.IsFinite(result.Number));
                default: return false;
            }
        }
        try { return Evaluate(expression, 0, out value); }
        catch (FormatException) { value = default; return false; }
    }

    internal static bool TryBinary(Value left, BinaryOperator op, Value right, out Value result, bool typeOnly = false)
    {
        result = default;
        static FirmamentV2PrimitiveType? Type(string unit) => unit switch
        { "" => FirmamentV2PrimitiveType.Float, "mm" => FirmamentV2PrimitiveType.Length, "deg" or "rad" => FirmamentV2PrimitiveType.Angle, "s" => FirmamentV2PrimitiveType.Time, _ => null };
        var symbol = op switch { BinaryOperator.Add => "+", BinaryOperator.Subtract => "-", BinaryOperator.Multiply => "*", BinaryOperator.Divide => "/", _ => "" };
        if (Type(left.Unit) is not { } lt || Type(right.Unit) is not { } rt || ResultType(lt, symbol, rt) is not { } type
            || left.Unit.Length > 0 && right.Unit.Length > 0 && left.Unit != right.Unit
            || op == BinaryOperator.Divide && !typeOnly && right.Number == 0) return false;
        var number = typeOnly ? 1 : op switch
        { BinaryOperator.Add => left.Number + right.Number, BinaryOperator.Subtract => left.Number - right.Number,
            BinaryOperator.Multiply => left.Number * right.Number, _ => left.Number / right.Number };
        result = new(number, type is FirmamentV2PrimitiveType.Length or FirmamentV2PrimitiveType.Angle or FirmamentV2PrimitiveType.Time
            ? left.Unit.Length > 0 ? left.Unit : right.Unit : "");
        return typeOnly || double.IsFinite(result.Number);
    }

    internal static FirmamentV2PrimitiveType? ResultType(FirmamentV2PrimitiveType a, string op, FirmamentV2PrimitiveType b) => op switch
        {
            "+" or "-" when a == b && a is FirmamentV2PrimitiveType.Int or FirmamentV2PrimitiveType.Float or FirmamentV2PrimitiveType.Length or FirmamentV2PrimitiveType.Angle or FirmamentV2PrimitiveType.Time => a,
            "+" or "-" when IsNumeric(a) && IsNumeric(b) => FirmamentV2PrimitiveType.Float,
            "*" when IsNumeric(a) && IsNumeric(b) => a == FirmamentV2PrimitiveType.Int && b == FirmamentV2PrimitiveType.Int ? FirmamentV2PrimitiveType.Int : FirmamentV2PrimitiveType.Float,
            "*" when IsDim(a) && IsNumeric(b) => a,
            "*" when IsNumeric(a) && IsDim(b) => b,
            "/" when IsNumeric(a) && IsNumeric(b) => FirmamentV2PrimitiveType.Float,
            "/" when IsDim(a) && IsNumeric(b) => a,
            "/" when IsDim(a) && a == b => FirmamentV2PrimitiveType.Float,
            _ => null
        };
    private static bool IsNumeric(FirmamentV2PrimitiveType t) => t is FirmamentV2PrimitiveType.Int or FirmamentV2PrimitiveType.Float;
    private static bool IsDim(FirmamentV2PrimitiveType t) => t is FirmamentV2PrimitiveType.Length or FirmamentV2PrimitiveType.Angle or FirmamentV2PrimitiveType.Time;

    internal static bool TryEvaluateScalar(string expression, out double value, out string unit)
    {
        var success = FirmamentNumericEvaluation.TryEvaluate(expression, out var result); value = result.Number; unit = result.Unit ?? ""; return success;
    }
    internal static bool TryEvaluateScalar(string expression, IReadOnlyDictionary<string, (double Value, string Unit)> variables,
        Func<string, IReadOnlyList<(double Value, string Unit)>, (double Value, string Unit)> functions, bool typeOnly, out double value, out string unit)
    {
        var success = FirmamentNumericEvaluation.TryEvaluate(expression, out var result,
            variables.ToDictionary(p => p.Key, p => new FirmamentNumericEvaluation.Value(p.Value.Value, p.Value.Unit), StringComparer.Ordinal),
            (name, args) => { var result = functions(name, args.Select(a => (a.Number, a.Unit)).ToArray()); return new(result.Value, result.Unit); }, typeOnly);
        value = result.Number; unit = result.Unit ?? ""; return success;
    }
}
