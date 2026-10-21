using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

internal static partial class ConceptPointAuthoring
{
    /// <summary>Pure typed Function binding. Inlining is an AST transformation;
    /// arithmetic and units remain owned by FirmamentNumericEvaluation.</summary>
    internal sealed class MathCatalog(Action<string, string, FirmamentV2SourceSpan> error, FirmamentCompileTimeValues? scalars)
    {
        internal string SourceContext { get; set; } = "";
        private readonly Dictionary<string, Function> functions = new(StringComparer.Ordinal);
        private FirmamentCompileTimeValues? Values => scalars;
        private int work;
        private bool checking;
        internal IReadOnlyDictionary<string, EquationCurveAuthoring.BoundCurve> Curves { get; set; } = new Dictionary<string, EquationCurveAuthoring.BoundCurve>();

        internal string Parse(string source)
        {
            SourceContext = source;
            var declarations = FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document)
                .Where(d => d.Type.Name.Text == "Function").ToArray();
            foreach (var d in declarations)
            {
                var name = d.Name?.Text ?? "<unnamed>";
                var type = FirmamentSyntaxValues.Reference(d.ResultType) ?? "";
                var args = d.Parameters.Select(p => (Name: p.Name?.Text ?? "", Type: FirmamentSyntaxValues.Reference(p.Value) ?? "")).ToArray();
                if (d.Name is null || d.Type.Arguments.Count != 0 || d.Occurrence || d.Initializer is null || d.Body is not null || d.Modifiers.Count != 0 || d.Clauses.Count != 0 || d.Annotation is not null
                    || name is "Pow" or "Sin" or "Cos" or "Point2" or "Point3" || !Allowed(type) || args.Length > 16 || args.Any(a => a.Name.Length == 0 || !Allowed(a.Type))
                    || args.Select(a => a.Name).Distinct().Count() != args.Length || d.Parameters.Any(p => p.Default is not null)
                    || !functions.TryAdd(name, new(name, args, type, d.Initializer)))
                    error("function-definition-invalid", $"Function '{name}' requires unique typed parameters and one expression result (scalar, Point2, Point3 or a declared Record).", d.Span);
            }
            if (functions.Count > 128) error("function-limit", "A Function catalog may contain at most 128 declarations.", declarations[128].Span);
            var visited = new HashSet<string>();
            bool Cycle(string name, HashSet<string> active)
            {
                if (visited.Contains(name) || !functions.TryGetValue(name, out var function)) return false;
                if (!active.Add(name)) return true;
                var cycle = FirmamentSyntaxTraversal.Expressions(new DocumentSyntax("function", [new ExpressionStatementSyntax(function.Body, function.Body.Span)], [], function.Body.Span)).OfType<CallSyntax>()
                    .Select(c => FirmamentSyntaxValues.Reference(c.Target)).OfType<string>().Any(n => Cycle(n, active));
                active.Remove(name); visited.Add(name); return cycle;
            }
            foreach (var f in functions.Values)
                if (Cycle(f.Name, [])) error("function-cycle", $"Function '{f.Name}' has a recursive dependency; Functions must form a finite acyclic graph.", f.Body.Span);
            checking = true;
            foreach (var f in functions.Values)
            {
                work = 10000;
                try
                {
                    if (f.Body.Span.Length > 8192) throw new FormatException();
                    var bodyTree = new DocumentSyntax("function", [new ExpressionStatementSyntax(f.Body, f.Body.Span)], [], f.Body.Span);
                    if (FirmamentSyntaxTraversal.Expressions(bodyTree).OfType<ReferenceSyntax>().Any(r =>
                        !f.Parameters.Any(p => p.Name == r.Name.Text) && !functions.ContainsKey(r.Name.Text)
                        && r.Name.Text is not ("Point2" or "Point3" or "Sin" or "Cos" or "Pow") && scalars?.IsRecordType(r.Name.Text) != true)) throw new FormatException();
                    var arguments = f.Parameters.ToDictionary(p => p.Name, p => Placeholder(p.Type, 0), StringComparer.Ordinal);
                    var body = new FirmamentSyntaxSubstitution(arguments).Expression(f.Body);
                    var expanded = new Inline(this, new HashSet<string> { f.Name }).Expression(body);
                    if (!Matches(expanded, f.ReturnType)) throw new FormatException();
                }
                catch (FormatException) { error("function-body-invalid", $"Function '{f.Name}' must return {f.ReturnType} with matching units and finite, acyclic dependencies; parameters are explicit lexical inputs.", f.Body.Span); }
            }
            checking = false;
            foreach (var d in declarations.OrderByDescending(d => d.Span.Start))
                source = source.Remove(d.Span.Start, d.Span.Length).Insert(d.Span.Start, Blank(source.Substring(d.Span.Start, d.Span.Length)));
            return source;
        }

        private bool Allowed(string type) => type is "Int" or "Float" or "Length" or "Angle" or "Point2" or "Point3" || scalars?.IsRecordType(type) == true;
        private static string Unit(string type) => type switch { "Length" => "mm", "Angle" => "deg", _ => "" };
        private static ExpressionSyntax ParseValue(string text) => FirmamentSyntaxParser.ParseExpression(text).Expression ?? throw new FormatException();

        private ExpressionSyntax Placeholder(string type, int depth)
        {
            if (depth > 32) throw new FormatException();
            if (type is "Point2" or "Point3") return ParseValue(type + "(" + string.Join(",", Enumerable.Repeat("1mm", type == "Point2" ? 2 : 3)) + ")");
            if (scalars?.RecordFields(type) is { } fields)
                return ParseValue(type + " { " + string.Join(";", fields.Select(f => f.Key + ": " + FirmamentSyntaxWriter.Expression(Placeholder(f.Value, depth + 1)))) + " }");
            if (type is not ("Int" or "Float" or "Length" or "Angle")) throw new FormatException();
            return ParseValue("1" + Unit(type));
        }

        private bool Matches(ExpressionSyntax expression, string type)
        {
            if (type is "Point2" or "Point3")
                return expression is CallSyntax { Target: ReferenceSyntax t } p && t.Name.Text == type
                    && p.Arguments.Count == (type == "Point2" ? 2 : 3) && p.Arguments.All(a => a.Name is null && a.Default is null && Matches(a.Value, "Length"));
            if (scalars?.RecordFields(type) is { } fields)
            {
                if (expression is not ObjectSyntax o || FirmamentSyntaxValues.Reference(o.Type) != type || o.Name is not null
                    || o.Body.Statements.Count != fields.Count || o.Body.Statements.Any(s => s is not FieldSyntax)) return false;
                var actual = o.Body.Statements.Cast<FieldSyntax>().ToArray();
                return actual.Select(f => f.Name.Text).Distinct().Count() == fields.Count
                    && actual.All(f => fields.TryGetValue(f.Name.Text, out var fieldType) && Matches(f.Value, fieldType));
            }
            if (type is not ("Int" or "Float" or "Length" or "Angle")) return false;
            if (type == "Int" && curveParameter is not null && FirmamentSyntaxTraversal.Expressions(expression).OfType<ReferenceSyntax>().Any(r => r.Name.Text == curveParameter)) return false;
            var variables = curveParameter is null ? null : new Dictionary<string, FirmamentNumericEvaluation.Value> { [curveParameter] = new(1, "") };
            if (!FirmamentNumericEvaluation.TryEvaluate(expression, out var result, variables, typeOnly: checking)) return false;
            return (type == "Angle" ? result.Unit is "deg" or "rad" : result.Unit == Unit(type))
                && (checking || type != "Int" || Math.Abs(result.Number) <= 9007199254740991d && result.Number == Math.Truncate(result.Number));
        }

        private sealed class Inline(MathCatalog owner, HashSet<string> active) : FirmamentSyntaxRewriter
        {
            internal override ExpressionSyntax Expression(ExpressionSyntax e)
            {
                if (--owner.work < 0 || active.Count > 32) throw new FormatException();
                if (FirmamentSyntaxValues.Reference(e) is { } input && e is ReferenceSyntax or MemberSyntax
                    && input.Split('.')[0] != owner.curveParameter
                    && owner.Values?.TryValue(input, out var requirement) == true && requirement.Tolerance is not null)
                    throw new FormatException("Function evaluation cannot erase a typed tolerance; use a tolerance-preserving engineering consumer and explicit PMI.");
                if (e is CallSyntax { Target: MemberSyntax { Member.Text: "At" } member, Arguments.Count: 1 } at
                    && FirmamentSyntaxValues.Reference(member.Target) is { } path && owner.Curves.TryGetValue(path, out var curve))
                {
                    if (at.Arguments[0].Name is not null || at.Arguments[0].Default is not null
                        || !FirmamentNumericEvaluation.TryEvaluate(Expression(at.Arguments[0].Value), out var parameter) || parameter.Unit != ""
                        || parameter.Number < curve.Value.Start || parameter.Number > curve.Value.End) throw new FormatException();
                    return Expression(new FirmamentSyntaxSubstitution(new Dictionary<string, ExpressionSyntax> { [curve.Value.Parameter] = ParseValue(Literal(parameter.Number, "")) }).Expression(curve.Point));
                }
                if (e is CallSyntax { Target: ReferenceSyntax target } call && owner.functions.TryGetValue(target.Name.Text, out var function))
                {
                    if (call.Arguments.Count != function.Parameters.Count || call.Arguments.Any(a => a.Name is not null || a.Default is not null)
                        || function.Body.Span.Length > 8192) throw new FormatException();
                    var args = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
                    for (var i = 0; i < function.Parameters.Count; i++)
                    {
                        var arg = Expression(call.Arguments[i].Value);
                        if (!owner.Matches(arg, function.Parameters[i].Type)) throw new FormatException();
                        args.Add(function.Parameters[i].Name, arg);
                    }
                    if (owner.checking && owner.curveParameter is null) return owner.Placeholder(function.ReturnType, 0);
                    if (!active.Add(function.Name)) throw new FormatException();
                    try
                    {
                        var value = Expression(new FirmamentSyntaxSubstitution(args).Expression(function.Body));
                        // Unbound curve parameters are checked by the curve owner after
                        // typed placeholders prove the declaration's result shape.
                        if (!owner.Matches(value, function.ReturnType)) throw new FormatException();
                        return value;
                    }
                    finally { active.Remove(function.Name); }
                }
                if (e is ReferenceSyntax r && r.Name.Text != owner.curveParameter && owner.Values?.TryRecord(r.Name.Text, out var record) == true)
                {
                    if (record.Fields.Keys.Any(field => owner.Values.TryValue(record.Name + "." + field, out var value) && value.Tolerance is not null)) throw new FormatException();
                    return Expression(ParseValue(record.TypeName + " { " + string.Join(";", record.Fields.Select(f => f.Key + ": " + f.Value)) + " }"));
                }
                var result = base.Expression(e);
                // Admit the whole constructor before projecting any member. Otherwise
                // `.point` could erase unknown/duplicate sibling fields or bad units.
                if (result is ObjectSyntax obj && (FirmamentSyntaxValues.Reference(obj.Type) is not { } recordType || !owner.Matches(obj, recordType)))
                    throw new FormatException("Record construction requires its declared fields and matching types.");
                if (result is CallSyntax { Target: ReferenceSyntax pointType } point && pointType.Name.Text is "Point2" or "Point3" && !owner.Matches(point, pointType.Name.Text))
                    throw new FormatException("Point construction requires exactly two/three Length coordinates.");
                if (result is MemberSyntax m)
                {
                    if (m.Target is ObjectSyntax o)
                        return o.Body.Statements.OfType<FieldSyntax>().SingleOrDefault(f => f.Name.Text == m.Member.Text)?.Value ?? throw new FormatException();
                    if (m.Target is CallSyntax { Target: ReferenceSyntax t } p && t.Name.Text is "Point2" or "Point3")
                    {
                        var index = "XYZ".IndexOf(m.Member.Text, StringComparison.Ordinal);
                        if (m.Member.Text.Length != 1 || index < 0 || index >= p.Arguments.Count) throw new FormatException();
                        return p.Arguments[index].Value;
                    }
                }
                return owner.Values?.NominalExpression(result, owner.curveParameter is null ? null : new HashSet<string> { owner.curveParameter }) ?? result;
            }
        }

        internal ExpressionSyntax Bind(ExpressionSyntax expression, IReadOnlyDictionary<string, (double Value, string Unit)>? variables = null, bool symbolic = false)
        {
            work = 10000;
            var saved = checking;
            checking = symbolic;
            try
            {
                // Symbolic parameters use typed leaves during call admission. Restore
                // the parameter expression after inlining so curve analysis sees it.
                var local = variables?.ToDictionary(p => p.Key, p => ParseValue(Literal(p.Value.Value, p.Value.Unit)), StringComparer.Ordinal) ?? [];
                return new Inline(this, []).Expression(new FirmamentSyntaxSubstitution(local).Expression(expression));
            }
            finally { checking = saved; }
        }
        internal bool Evaluate(ExpressionSyntax expression, IReadOnlyDictionary<string, (double Value, string Unit)> variables, out double value, out string unit)
        {
            value = double.NaN; unit = "";
            try
            {
                if (!FirmamentNumericEvaluation.TryEvaluate(Bind(expression, variables), out var result)) return false;
                value = result.Number; unit = result.Unit; return true;
            }
            catch (FormatException) { return false; }
        }
        internal ExpressionSyntax BindCurve(ExpressionSyntax expression, string parameter)
        {
            // Curve parameters have Float type but must survive in the resulting AST.
            // A type-only variable is recognized by Matches, never substituted numerically.
            curveParameter = parameter;
            try { return Bind(expression, symbolic: true); }
            finally { curveParameter = null; }
        }
        private string? curveParameter;

        internal string Normalize(string source)
        {
            SourceContext = source;
            var changes = new List<(FirmamentV2SourceSpan Span, string Text)>();
            bool Known(CallSyntax call) => FirmamentSyntaxValues.Reference(call.Target) is { } name && (functions.ContainsKey(name) || name is "Sin" or "Cos")
                || call.Target is MemberSyntax { Member.Text: "At" } m && FirmamentSyntaxValues.Reference(m.Target) is { } path && Curves.ContainsKey(path);
            foreach (var expression in FirmamentSyntaxTraversal.Expressions(FirmamentSyntaxParser.Parse(source).Document)
                .Where(e => e is CallSyntax c && Known(c) || e is MemberSyntax && FirmamentSyntaxTraversal.Expressions(e).OfType<CallSyntax>().Any(Known))
                .OrderBy(c => c.Span.Start).ThenByDescending(c => c.Span.Length))
            {
                if (changes.Any(c => Contains(c.Span, expression.Span))) continue;
                try { changes.Add((expression.Span, Render(Bind(expression)))); }
                catch (FormatException exception) { error("function-call-invalid", "Cannot evaluate Function/curve call: check argument types, units, parameter domain, tolerated inputs and finite evaluation limits. " + exception.Message, expression.Span); }
            }
            return Apply(source, changes);
        }
        private string Render(ExpressionSyntax value)
        {
            if (value is CallSyntax { Target: ReferenceSyntax t } p && t.Name.Text is "Point2" or "Point3")
                return t.Name.Text + "(" + string.Join(",", p.Arguments.Select(a => Render(a.Value))) + ")";
            if (value is ObjectSyntax o) return FirmamentSyntaxValues.Reference(o.Type) + " { " + string.Join(";", o.Body.Statements.Cast<FieldSyntax>().Select(f => f.Name.Text + ": " + Render(f.Value))) + " }";
            if (!FirmamentNumericEvaluation.TryEvaluate(value, out var scalar)) throw new FormatException();
            return Literal(scalar.Number, scalar.Unit);
        }
    }
}
