namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

internal static partial class FirmamentNumericEvaluation
{
    /// <summary>Coefficient proof, not interpolation. Only degree <= 3 polynomials
    /// are admitted here; numeric constants use the ordinary scalar evaluator.</summary>
    internal static bool TryPolynomial(ExpressionSyntax expression, string parameter, out double[] coefficients)
    {
        double[]? Visit(ExpressionSyntax e, int depth)
        {
            if (depth > 128) return null;
            if (e is ReferenceSyntax r && r.Name.Text == parameter) return [0, 1, 0, 0];
            if (!FirmamentSyntaxTraversal.Expressions(e).OfType<ReferenceSyntax>().Any(r => r.Name.Text == parameter)
                && TryEvaluate(e, out var constant)) return [constant.Number, 0, 0, 0];
            if (e is QuantitySyntax q) return Visit(q.Value, depth + 1);
            if (e is SequenceSyntax { Kind: SequenceKind.Tuple, Items.Count: 1 } g) return Visit(g.Items[0].Value, depth + 1);
            if (e is UnarySyntax { Operator: UnaryOperator.Positive or UnaryOperator.Negative } u && Visit(u.Operand, depth + 1) is { } v)
                return u.Operator == UnaryOperator.Positive ? v : v.Select(x => -x).ToArray();
            if (e is CallSyntax { Target: ReferenceSyntax { Name.Text: "Pow" }, Arguments.Count: 2 } power
                && TryEvaluate(power.Arguments[1].Value, out var exponent) && exponent.Unit == "" && exponent.Number is >= 0 and <= 3 && exponent.Number == Math.Truncate(exponent.Number))
            {
                var basis = Visit(power.Arguments[0].Value, depth + 1); if (basis is null) return null;
                double[]? result = [1, 0, 0, 0];
                for (var i = 0; i < exponent.Number && result is not null; i++) result = Product(result, basis);
                return result;
            }
            if (e is not BinarySyntax b || Visit(b.Left, depth + 1) is not { } left || Visit(b.Right, depth + 1) is not { } right) return null;
            return b.Operator switch
            {
                BinaryOperator.Add => left.Zip(right, (x, y) => x + y).ToArray(),
                BinaryOperator.Subtract => left.Zip(right, (x, y) => x - y).ToArray(),
                BinaryOperator.Multiply => Product(left, right),
                BinaryOperator.Divide when right[0] != 0 && right.Skip(1).All(x => x == 0) => left.Select(x => x / right[0]).ToArray(),
                _ => null
            };
        }
        static double[]? Product(double[] a, double[] b)
        {
            var c = new double[7]; for (var i = 0; i < 4; i++) for (var j = 0; j < 4; j++) c[i + j] += a[i] * b[j];
            return c.Skip(4).Any(x => x != 0) ? null : c.Take(4).ToArray();
        }
        coefficients = Visit(expression, 0) ?? [];
        return coefficients.Length == 4 && coefficients.All(double.IsFinite);
    }

    // Outward-rounded interval jets enclose the whole parameter interval, including
    // derivatives. No sampled residual is promoted to a geometric error bound.
    private readonly record struct Interval(double Lo, double Hi)
    {
        internal static Interval Point(double x) => new(x, x);
        internal double Magnitude => Math.Max(Math.Abs(Lo), Math.Abs(Hi));
        public static Interval operator +(Interval a, Interval b) => new(Math.BitDecrement(a.Lo + b.Lo), Math.BitIncrement(a.Hi + b.Hi));
        public static Interval operator -(Interval a) => new(-a.Hi, -a.Lo);
        public static Interval operator -(Interval a, Interval b) => a + -b;
        public static Interval operator *(Interval a, Interval b)
        {
            double[] p = [a.Lo * b.Lo, a.Lo * b.Hi, a.Hi * b.Lo, a.Hi * b.Hi];
            return new(Math.BitDecrement(p.Min()), Math.BitIncrement(p.Max()));
        }
        internal Interval Inverse() => Lo <= 0 && Hi >= 0 ? throw new FormatException() : new(Math.BitDecrement(1 / Hi), Math.BitIncrement(1 / Lo));
        internal Interval Trig(bool cosine)
        {
            // Enclose range reduction and the Taylor polynomial with directed
            // arithmetic, rather than assuming Math.Sin is correctly rounded.
            if (!double.IsFinite(Lo) || !double.IsFinite(Hi) || Hi - Lo >= 2 * Math.PI || Magnitude > 1e6) return new(-1, 1);
            var turns = Math.Round((Lo / 2 + Hi / 2) / (2 * Math.PI));
            var pi = new Interval(Math.BitDecrement(Math.PI), Math.BitIncrement(Math.PI));
            var x = this - pi * Point(2 * turns);
            if (x.Magnitude > 4) return new(-1, 1);
            var square = x * x;
            var term = cosine ? Point(1) : x;
            var sum = term;
            for (var k = 1; k <= 24; k++)
            {
                var denominator = cosine ? (2 * k - 1) * (2 * k) : (2 * k) * (2 * k + 1);
                term = -term * square * Point(denominator).Inverse(); sum += term;
            }
            // |x| <= 4; the first omitted Taylor term is below 1e-34.
            return new(Math.Max(-1, Math.BitDecrement(sum.Lo - 1e-34)), Math.Min(1, Math.BitIncrement(sum.Hi + 1e-34)));
        }
    }
    private readonly record struct Jet(Interval Value, Interval First, Interval Second)
    {
        internal static Jet Constant(double x) => new(Interval.Point(x), Interval.Point(0), Interval.Point(0));
        public static Jet operator +(Jet a, Jet b) => new(a.Value + b.Value, a.First + b.First, a.Second + b.Second);
        public static Jet operator -(Jet a) => new(-a.Value, -a.First, -a.Second);
        public static Jet operator *(Jet a, Jet b) => new(a.Value * b.Value, a.First * b.Value + a.Value * b.First,
            a.Second * b.Value + Interval.Point(2) * a.First * b.First + a.Value * b.Second);
        internal Jet Inverse()
        {
            var inv = Value.Inverse();
            return new(inv, -First * inv * inv, Interval.Point(2) * First * First * inv * inv * inv - Second * inv * inv);
        }
    }
    internal static bool TrySecondDerivativeBound(ExpressionSyntax expression, string parameter, double start, double end, out double bound)
    {
        var success = TryCurveJet(expression, parameter, start, end, out var jet);
        bound = jet.Second.Magnitude;
        return success && double.IsFinite(bound);
    }
    internal static bool TryCurveEvaluationError(ExpressionSyntax expression, string parameter, double t, double evaluated, out double error)
    {
        var success = TryCurveJet(expression, parameter, t, t, out var jet);
        error = Math.Max(Math.Abs(evaluated - jet.Value.Lo), Math.Abs(evaluated - jet.Value.Hi));
        return success && double.IsFinite(error);
    }
    private static bool TryCurveJet(ExpressionSyntax expression, string parameter, double start, double end, out Jet jet)
    {
        Jet Visit(ExpressionSyntax e, int depth)
        {
            if (depth > 128) throw new FormatException();
            if (e is ReferenceSyntax r && r.Name.Text == parameter) return new(new(start, end), Interval.Point(1), Interval.Point(0));
            if (!FirmamentSyntaxTraversal.Expressions(e).OfType<ReferenceSyntax>().Any(r => r.Name.Text == parameter)
                && TryEvaluate(e, out var constant)) return Jet.Constant(constant.Number);
            if (e is QuantitySyntax q) return Visit(q.Value, depth + 1);
            if (e is SequenceSyntax { Kind: SequenceKind.Tuple, Items.Count: 1 } g) return Visit(g.Items[0].Value, depth + 1);
            if (e is UnarySyntax { Operator: UnaryOperator.Positive or UnaryOperator.Negative } u) return u.Operator == UnaryOperator.Positive ? Visit(u.Operand, depth + 1) : -Visit(u.Operand, depth + 1);
            if (e is BinarySyntax b)
            {
                var a = Visit(b.Left, depth + 1); var v = Visit(b.Right, depth + 1);
                return b.Operator switch { BinaryOperator.Add => a + v, BinaryOperator.Subtract => a + -v,
                    BinaryOperator.Multiply => a * v, BinaryOperator.Divide => a * v.Inverse(), _ => throw new FormatException() };
            }
            if (e is CallSyntax { Target: ReferenceSyntax t } call)
            {
                if (t.Name.Text is "Sin" or "Cos" && call.Arguments.Count == 1)
                {
                    var arg = call.Arguments[0].Value;
                    if (!TryEvaluate(arg, out var type, new Dictionary<string, Value> { [parameter] = new(1, "") }, typeOnly: true)) throw new FormatException();
                    var a = Visit(arg, depth + 1);
                    if (type.Unit == "deg") a *= Jet.Constant(Math.PI / 180);
                    // Global [-1,1] enclosures avoid missed extrema and huge-angle
                    // argument reduction assumptions. Refinement still shrinks h².
                    var sin = a.Value.Trig(false); var cos = a.Value.Trig(true);
                    return t.Name.Text == "Sin" ? new(sin, cos * a.First, -sin * a.First * a.First + cos * a.Second)
                        : new(cos, -sin * a.First, -cos * a.First * a.First - sin * a.Second);
                }
                if (t.Name.Text == "Pow" && call.Arguments.Count == 2 && TryEvaluate(call.Arguments[1].Value, out var exponent)
                    && exponent.Unit == "" && exponent.Number == Math.Truncate(exponent.Number) && Math.Abs(exponent.Number) <= 8)
                {
                    var a = Visit(call.Arguments[0].Value, depth + 1); var result = Jet.Constant(1);
                    for (var i = 0; i < Math.Abs(exponent.Number); i++) result *= a;
                    return exponent.Number < 0 ? result.Inverse() : result;
                }
            }
            throw new FormatException();
        }
        try { jet = Visit(expression, 0); return true; }
        catch (FormatException) { jet = default; return false; }
    }
}
