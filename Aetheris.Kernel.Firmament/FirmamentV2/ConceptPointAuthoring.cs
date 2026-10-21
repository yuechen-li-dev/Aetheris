using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using System.Globalization;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Checked finite point recipes over the unified AST. Existing keyed Set and
/// Pattern owners still perform construction expansion; this owner transports values only.</summary>
internal static class ConceptPointAuthoring
{
    internal const string Prefix = "firmament-concept-points-";
    internal sealed record Result(string Source, IReadOnlyList<ConceptIrKeyedPointSetValue> Points, MathCatalog Math);
    private sealed record Function(string Name, IReadOnlyList<(string Name, string Type)> Parameters, string ReturnType, ExpressionSyntax Body);

    internal sealed class MathCatalog
    {
        private int remainingCalls;
        internal string SourceContext { get; set; } = "";
        private readonly Dictionary<string, Function> functions = new(StringComparer.Ordinal);
        private readonly Action<string, string, FirmamentV2SourceSpan> error;
        internal MathCatalog(Action<string, string, FirmamentV2SourceSpan> error) => this.error = error;
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
                    || name == "Pow" || !Allowed(type) || args.Length > 16 || args.Any(a => a.Name.Length == 0 || !Allowed(a.Type))
                    || args.Select(a => a.Name).Distinct().Count() != args.Length || d.Parameters.Any(p => p.Default is not null)
                    || !functions.TryAdd(name, new(name, args, type, d.Initializer)))
                    error("function-definition-invalid", $"Function '{name}' requires unique typed scalar parameters and one expression result.", d.Span);
            }
            if (functions.Count > 128) error("function-limit", "A Function catalog may contain at most 128 declarations.", declarations[128].Span);
            var checkedFunctions = new HashSet<string>(StringComparer.Ordinal);
            IEnumerable<string> Dependencies(Function f) => FirmamentSyntaxTraversal.Expressions(f.Body).OfType<CallSyntax>()
                .Select(c => FirmamentSyntaxValues.Reference(c.Target)).OfType<string>();
            bool Cycle(string name, HashSet<string> active)
            {
                if (!functions.TryGetValue(name, out var f) || checkedFunctions.Contains(name)) return false;
                if (!active.Add(name)) return true;
                var cycle = Dependencies(f).Any(n => Cycle(n, active)); active.Remove(name);
                if (!cycle) checkedFunctions.Add(name); return cycle;
            }
            foreach (var f in functions.Values)
            {
                var variables = f.Parameters.ToDictionary(p => p.Name, p => new FirmamentNumericEvaluation.Value(1, Unit(p.Type)), StringComparer.Ordinal);
                if (f.Body.Span.Length > 8192 || !FirmamentNumericEvaluation.TryEvaluate(f.Body, out var result, variables, (n, a) => Call(n, a, true, []), true) || result.Unit != Unit(f.ReturnType))
                    error("function-body-invalid", $"Function '{f.Name}' must return {f.ReturnType}; use its parameters and admitted scalar Functions with matching units.", f.Body.Span);
                if (Cycle(f.Name, [])) error("function-cycle", $"Function '{f.Name}' has a recursive dependency; Functions must form a finite acyclic graph.", f.Body.Span);
            }
            // Keep offsets stable for original point diagnostics. Functions have no material output.
            foreach (var d in declarations.OrderByDescending(d => d.Span.Start))
                source = source.Remove(d.Span.Start, d.Span.Length).Insert(d.Span.Start, Blank(source.Substring(d.Span.Start, d.Span.Length)));
            return source;
        }
        private FirmamentNumericEvaluation.Value Call(string name, IReadOnlyList<FirmamentNumericEvaluation.Value> args, bool typeOnly, HashSet<string> active)
        {
            if (!typeOnly && --remainingCalls < 0 || !functions.TryGetValue(name, out var f) || args.Count != f.Parameters.Count || active.Count >= 32 || !active.Add(name)) throw new FormatException();
            try
            {
                for (var i = 0; i < args.Count; i++)
                    if (args[i].Unit != Unit(f.Parameters[i].Type) || !typeOnly && f.Parameters[i].Type == "Int" && !ValidInt(args[i].Number)) throw new FormatException();
                if (typeOnly) return new(1, Unit(f.ReturnType));
                var variables = f.Parameters.Select((p, i) => (p.Name, Value: args[i])).ToDictionary(p => p.Name, p => p.Value, StringComparer.Ordinal);
                if (f.Body.Span.Length > 8192 || !FirmamentNumericEvaluation.TryEvaluate(f.Body, out var result, variables, (n, a) => Call(n, a, false, active))
                    || result.Unit != Unit(f.ReturnType) || f.ReturnType == "Int" && !ValidInt(result.Number)) throw new FormatException();
                return result;
            }
            finally { active.Remove(name); }
        }
        internal bool Evaluate(ExpressionSyntax expression, IReadOnlyDictionary<string, (double Value, string Unit)> variables, out double value, out string unit)
        {
            remainingCalls = 10000;
            value = double.NaN; unit = "";
            if (expression.Span.Length > 8192 || !FirmamentNumericEvaluation.TryEvaluate(expression, out var result,
                variables.ToDictionary(p => p.Key, p => new FirmamentNumericEvaluation.Value(p.Value.Value, p.Value.Unit)), (n, a) => Call(n, a, false, []))) return false;
            value = result.Number; unit = result.Unit; return true;
        }
        internal string Normalize(string source)
        {
            SourceContext = source;
            var changes = new List<(FirmamentV2SourceSpan Span, string Text)>();
            foreach (var call in FirmamentSyntaxTraversal.Expressions(FirmamentSyntaxParser.Parse(source).Document).OfType<CallSyntax>().OrderBy(c => c.Span.Start).ThenByDescending(c => c.Span.Length))
            {
                if (FirmamentSyntaxValues.Reference(call.Target) is not { } name || !functions.ContainsKey(name)
                    || changes.Any(c => Contains(c.Span, call.Span))) continue;
                if (Evaluate(call, new Dictionary<string, (double, string)>(), out var value, out var unit)) changes.Add((call.Span, Literal(value, unit)));
                else { error("function-call-invalid", $"Cannot evaluate Function '{name}': check argument types, units and finite evaluation limits.", call.Span); }
            }
            return Apply(source, changes);
        }
        private static bool ValidInt(double value) => Math.Abs(value) <= 9007199254740991d && value == Math.Truncate(value);
        private static bool Allowed(string type) => type is "Int" or "Float" or "Length" or "Angle";
        private static string Unit(string type) => type switch { "Length" => "mm", "Angle" => "deg", _ => "" };
    }

    internal static Result? Expand(string source, List<string> diagnostics, FirmamentCompileTimeValues? scalars = null)
    {
        var input = source;
        MathCatalog? math = null;
        void Error(string code, string message, FirmamentV2SourceSpan span)
        {
            diagnostics.Add(Prefix + code);
            var d = new FirmamentV2ValidationDiagnostic(Prefix + code, "error", message) { SourceSpan = span, IsOriginalSource = true, Stage = "Point/law binding" };
            scalars?.AddPointDiagnostic(d, math?.SourceContext ?? input);
        }
        math = new MathCatalog(Error); source = math.Parse(source);
        if (diagnostics.Any(d => d.StartsWith(Prefix, StringComparison.Ordinal))) return null;
        var syntax = FirmamentSyntaxParser.Parse(source);
        var collections = new List<ConceptIrKeyedPointSetValue>();
        var changes = new List<(FirmamentV2SourceSpan Span, string Text)>();
        var total = 0;
        foreach (var owner in FirmamentSyntaxTraversal.Declarations(syntax.Document).Where(d => d.Type.Name.Text == "Struct" && d.Modifiers.Any(m => m.Name.Text == "Concept") && d.Body is not null))
        {
            var pointDeclarations = owner.Body!.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Points").ToArray();
            if (pointDeclarations.Length == 0) continue;
            if (owner.Type.Arguments.Count > 0 || owner.Clauses.Count > 0 || owner.Annotation is not null || owner.HasParameterList || owner.ResultType is not null
                || owner.Modifiers.Any(m => m.Name.Text != "Concept"))
            {
                Error("collection-owner-header-unsupported",
                    "Local Points recipes do not consume a Concept Struct placement, parameter or contract header. Keep the layout unplaced and bind its physical support/frame explicitly in the Pattern recipe.",
                    owner.Clauses.FirstOrDefault()?.Value.Span ?? owner.Annotation?.Span ?? owner.Span);
                continue;
            }
            var generated = new List<string>();
            foreach (var collection in pointDeclarations)
            {
                var identity = owner.Name!.Text + "." + collection.Name?.Text;
                if (collections.Any(c => c.Name == identity)) { Error("collection-duplicate", $"Duplicate point collection '{identity}'.", collection.Span); continue; }
                var element = collection.Type.Arguments.Count == 1 ? FirmamentSyntaxValues.Reference(collection.Type.Arguments[0].Value) : null;
                if (element is not ("Point2" or "Point3") || collection.Body is null || collection.Name is null)
                { Error("collection-syntax-invalid", $"'{identity}' requires Points<Point2> or Points<Point3> with a body.", collection.Span); continue; }
                if (!PlainHeader(collection, named: true) || collection.Type.Arguments.Any(a => a.Name is not null))
                { Error("collection-header-unsupported", "Points requires its named Point2/Point3 collection and body, without placement or other header clauses.", collection.Span); continue; }
                var dimension = element == "Point2" ? 2 : 3;
                var fields = Fields(collection, false);
                var points = new List<ConceptIrKeyedPointValue>();
                var recipes = collection.Body.Statements.OfType<DeclarationSyntax>().ToArray();
                var recipe = recipes.Length == 1 ? recipes[0] : null;
                ExpressionSyntax Bound(ExpressionSyntax expression, IReadOnlyDictionary<string, (double, string)> locals)
                {
                    if (scalars is null) return expression;
                    foreach (var e in FirmamentSyntaxTraversal.Expressions(expression))
                        if (FirmamentSyntaxValues.Reference(e) is { } reference && !locals.ContainsKey(reference.Split('.')[0])
                            && scalars.TryValue(reference, out var value) && value.Tolerance is not null)
                            Error("tolerance-consumer-unsupported", $"Point layout '{identity}' cannot discard the tolerance on '{reference}'; retain it on the owning material feature and explicit PMI.", e.Span);
                    return scalars.NominalExpression(expression, locals.Keys.ToHashSet(StringComparer.Ordinal));
                }
                bool Number(ExpressionSyntax? expression, string expected, IReadOnlyDictionary<string, (double, string)> locals, out double number)
                {
                    number = double.NaN;
                    if (expression is not null && math.Evaluate(Bound(expression, locals), locals, out number, out var unit) && unit == expected) return true;
                    Error("point-expression-invalid", $"Layout '{identity}' requires a finite {(expected == "mm" ? "Length" : "dimensionless number")}; check names, units and Function arguments.", expression?.Span ?? collection.Span); return false;
                }
                double[]? Point(ExpressionSyntax? expression, IReadOnlyDictionary<string, (double, string)> locals)
                {
                    IReadOnlyList<ArgumentSyntax>? items = expression switch
                    {
                        CallSyntax { Target: ReferenceSyntax t } c when t.Name.Text == element => c.Arguments,
                        SequenceSyntax { Kind: SequenceKind.List } s => s.Items,
                        _ => null
                    };
                    if (items is null || items.Count != dimension || items.Any(i => i.Name is not null))
                    { Error("point-arity-invalid", $"'{identity}' requires {element} with exactly {dimension} Length coordinates.", expression?.Span ?? collection.Span); return null; }
                    var values = new double[dimension];
                    for (var i = 0; i < dimension; i++) if (!Number(items[i].Value, "mm", locals, out values[i])) return null;
                    return values;
                }
                void Add(string key, double[]? value, FirmamentV2SourceSpan span)
                {
                    if (value is null) return;
                    if (value.Any(v => !double.IsFinite(v))) { Error("point-expression-invalid", $"Point '{identity}.{key}' exceeds finite coordinate bounds.", span); return; }
                    if (++total > 4096 || points.Count >= 1024) { if (total == 4097 || points.Count == 1024) Error("point-limit", "Use at most 1024 points per collection and 4096 in total.", span); return; }
                    if (points.Any(p => p.Key == key)) { Error("keys-invalid", $"Duplicate point key '{key}' in '{identity}'.", span); return; }
                    points.Add(new("concept:" + identity + "." + key, key, points.Count, element, value, identity + "." + key) { SourceSpan = scalars?.OriginalPointSpan(span, input) });
                }
                var empty = new Dictionary<string, (double, string)>();
                if (recipes.Length == 0)
                {
                    foreach (var f in fields.Values) Add(f.Name.Text, Point(f.Value, empty), f.Span);
                    if (fields.Count == 0) Error("point-limit", "A Points collection requires at least one member; an empty typed Set can represent no instances.", collection.Span);
                }
                else if (recipes.Length != 1 || recipe?.Type.Name.Text is not ("Series" or "Linear") || recipe.Body is null)
                    Error("recipe-invalid", $"'{identity}' admits one Linear or Series recipe, or explicit keyed points.", collection.Span);
                else
                {
                    if (!PlainHeader(recipe, named: false) || recipe.Type.Arguments.Count != 0)
                    { Error("recipe-header-unsupported", $"{recipe.Type.Name.Text} requires an anonymous recipe body, without extra header clauses.", recipe.Span); continue; }
                    foreach (var child in recipe.Body.Statements.OfType<DeclarationSyntax>().Where(d => recipe.Type.Name.Text == "Series" || d.Type.Name.Text != "Stations"))
                        Error("recipe-invalid", $"Unexpected child '{child.Type.Name.Text}' in {recipe.Type.Name.Text}.", child.Span);
                    var entries = Fields(recipe); ExpressionSyntax? Take(string n) => entries.Remove(n, out var f) ? f.Value : null;
                    if (recipe.Type.Name.Text == "Series")
                    {
                        var indexExpression = Take("index"); var index = FirmamentSyntaxValues.Reference(indexExpression);
                        var firstExpression = Take("first"); var countExpression = Take("count"); var prefixExpression = Take("keyPrefix"); var prefix = FirmamentSyntaxValues.Reference(prefixExpression); var position = Take("position");
                        if (fields.Count > 0) foreach (var f in fields.Values) Error("series-fields-invalid", "Series cannot be combined with Keys or explicit point members.", f.Span);
                        if (indexExpression is not ReferenceSyntax || prefixExpression is not ReferenceSyntax || index is null || prefix is null)
                            Error("series-fields-invalid", "Series index and keyPrefix must be identifiers.", recipe.Span);
                        else if (Number(firstExpression, "", empty, out var first) && Number(countExpression, "", empty, out var amount))
                        {
                            if (first != Math.Truncate(first) || first < 0 || amount != Math.Truncate(amount) || amount is < 1 or > 1024 || first + amount > int.MaxValue)
                                Error("series-bounds-invalid", "Series first must be a nonnegative Int, count must be 1–1024, and the index range must fit Int32.", countExpression?.Span ?? recipe.Span);
                            else for (var i = 0; i < (int)amount; i++) { var n = (int)first + i; var local = new Dictionary<string, (double, string)> { { index, (n, "") } }; Add(prefix + n.ToString(CultureInfo.InvariantCulture), Point(position, local), position?.Span ?? recipe.Span); }
                        }
                    }
                    else
                    {
                        var start = Point(Take("start"), empty); var stations = recipe.Body.Statements.OfType<DeclarationSyntax>().ToArray();
                        if (stations.Length == 1 && stations[0].Type.Name.Text == "Stations")
                        {
                            if (!PlainHeader(stations[0], named: false) || stations[0].Type.Arguments.Count != 0)
                                Error("recipe-header-unsupported", "Stations requires an anonymous body of named Length fields.", stations[0].Span);
                            var directionExpression = Take("direction"); var items = directionExpression is SequenceSyntax { Kind: SequenceKind.List } vector ? vector.Items : null;
                            var direction = new double[dimension]; var valid = items is not null && items.Count == dimension && items.All(i => i.Name is null);
                            if (valid) for (var i = 0; i < dimension; i++) valid &= Number(items![i].Value, "", empty, out direction[i]);
                            var scale = direction.Max(v => Math.Abs(v));
                            if (!valid || scale == 0) Error("stations-direction-invalid", "Stations requires a nonzero dimensionless direction matching Point2/Point3.", directionExpression?.Span ?? recipe.Span);
                            else if (start is not null)
                            {
                                var norm = Math.Sqrt(direction.Sum(v => (v / scale) * (v / scale))); direction = direction.Select(v => (v / scale) / norm).ToArray();
                                foreach (var child in stations[0].Body?.Statements.OfType<DeclarationSyntax>() ?? [])
                                    Error("recipe-invalid", "Stations contains named Length fields only.", child.Span);
                                foreach (var f in Fields(stations[0], false).Values) if (Number(f.Value, "mm", empty, out var distance)) Add(f.Name.Text, start.Zip(direction, (s, d) => s + d * distance).ToArray(), f.Span);
                            }
                            if (fields.Count > 0) foreach (var f in fields.Values) Error("stations-fields-invalid", "Stations cannot be combined with Keys or point members.", f.Span);
                        }
                        else
                        {
                            var step = Point(Take("step"), empty); var keys = fields.Remove(fields.Keys.FirstOrDefault(k => k.Equals("keys", StringComparison.OrdinalIgnoreCase)) ?? "keys", out var keyField) ? keyField.Value as SequenceSyntax : null;
                            if (stations.Length > 0 || keys is not { Kind: SequenceKind.List } || keys.Items.Count is < 1 or > 1024 || keys.Items.Any(k => k.Name is not null || k.Value is not ReferenceSyntax) || step is null || step.All(v => v == 0))
                                Error("linear-invalid", "Linear requires unique identifier Keys, Start and a nonzero Step, or Direction with named Stations.", recipe.Span);
                            else if (start is not null) for (var i = 0; i < keys.Items.Count; i++) Add(FirmamentSyntaxValues.Reference(keys.Items[i].Value)!, start.Zip(step, (s, d) => s + i * d).ToArray(), keys.Items[i].Value.Span);
                        }
                    }
                    foreach (var f in entries.Values) Error("field-unknown", $"Unknown field '{f.Name.Text}' in {recipe.Type.Name.Text} for '{identity}'.", f.Span);
                    foreach (var f in fields.Values) Error("field-unknown", $"Unknown field '{f.Name.Text}' in Points for '{identity}'.", f.Span);
                }
                if (points.Count == 0 && !diagnostics.Any(d => d.StartsWith(Prefix, StringComparison.Ordinal)))
                    Error("point-limit", "A point recipe requires at least one keyed member; use an empty typed Set for no instances.", collection.Span);
                collections.Add(new("concept:" + identity, identity, element, points, source.Substring(collection.Span.Start, collection.Span.Length), identity)
                { SourceSpan = scalars?.OriginalPointSpan(collection.Span, input), RecipeKind = recipe?.Type.Name.Text ?? "Explicit" });
                generated.Add($"Static {SetName(identity)}: Set<{element}> {{\n" + string.Join("\n", points.Select(p => $"{p.Key} => {element}({string.Join(",", p.Coordinates.Select(v => Literal(v, "mm")))})")) + "\n}");
                changes.Add((collection.Span, ""));
            }
            var retained = owner.Body.Statements.Where(s => !pointDeclarations.Any(d => ReferenceEquals(d, s))).ToArray();
            if (retained.Length == 0) { changes.RemoveAll(c => Contains(owner.Span, c.Span)); changes.Add((owner.Span, string.Join("\n", generated))); }
            else changes.Add((new(owner.Span.Start + owner.Span.Length, 0), "\n" + string.Join("\n", generated)));
        }
        if (diagnostics.Any(d => d.StartsWith(Prefix, StringComparison.Ordinal))) return null;
        source = Apply(source, changes);
        math.SourceContext = source;
        // Resolve only actual expression spans; labels, strings and comments are not substitutions.
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in collections)
        {
            values[c.Name] = SetName(c.Name);
            foreach (var p in c.Points)
            {
                var path = c.Name + "." + p.Key; var point = $"{p.ElementType}({string.Join(",", p.Coordinates.Select(v => Literal(v, "mm")))})";
                values[path] = point; values[path + ".Position"] = point;
                for (var i = 0; i < p.Coordinates.Count; i++) { values[path + "." + "XYZ"[i]] = Literal(p.Coordinates[i], "mm"); values[path + ".Position." + "XYZ"[i]] = Literal(p.Coordinates[i], "mm"); }
            }
        }
        var replacements = new List<(FirmamentV2SourceSpan Span, string Text)>();
        foreach (var e in FirmamentSyntaxTraversal.Expressions(FirmamentSyntaxParser.Parse(source).Document).OrderBy(e => e.Span.Start).ThenByDescending(e => e.Span.Length))
        {
            if (replacements.Any(r => Contains(r.Span, e.Span)) || FirmamentSyntaxValues.Reference(e) is not { } name) continue;
            if (values.TryGetValue(name, out var v)) replacements.Add((e.Span, v));
            else if (collections.Any(c => name.StartsWith(c.Name + ".", StringComparison.Ordinal)))
                Error("point-member-unresolved", $"Unknown point/member '{name}'; Point2 has X/Y and Point3 has X/Y/Z.", e.Span);
        }
        return diagnostics.Any(d => d.StartsWith(Prefix, StringComparison.Ordinal)) ? null : new(Apply(source, replacements), collections, math);

        Dictionary<string, FieldSyntax> Fields(DeclarationSyntax d, bool vocabulary = true)
        {
            var result = new Dictionary<string, FieldSyntax>(vocabulary ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (var f in d.Body?.Statements.OfType<FieldSyntax>() ?? [])
                if (!result.TryAdd(f.Name.Text, f)) Error("field-invalid", $"Duplicate field/key '{f.Name.Text}' in '{d.Name?.Text ?? d.Type.Name.Text}'.", f.Span);
            foreach (var s in d.Body?.Statements.Where(s => s is not FieldSyntax and not DeclarationSyntax) ?? [])
                Error("recipe-invalid", "Point recipes require named fields or their documented child recipe.", s.Span);
            return result;
        }
    }
    private static bool PlainHeader(DeclarationSyntax declaration, bool named) =>
        (named ? declaration.Name is not null : declaration.Name is null) && !declaration.Occurrence
        && declaration.Modifiers.Count == 0 && declaration.Parameters.Count == 0 && !declaration.HasParameterList
        && declaration.Annotation is null && declaration.ResultType is null && declaration.Initializer is null
        && declaration.Clauses.Count == 0;

    private static bool Contains(FirmamentV2SourceSpan outer, FirmamentV2SourceSpan inner) => outer.Start <= inner.Start && outer.Start + outer.Length >= inner.Start + inner.Length;
    private static string Apply(string source, IEnumerable<(FirmamentV2SourceSpan Span, string Text)> changes)
    { foreach (var c in changes.OrderByDescending(c => c.Span.Start)) source = source.Remove(c.Span.Start, c.Span.Length).Insert(c.Span.Start, c.Text); return source; }
    private static string Blank(string source) => new(source.Select(c => c is '\r' or '\n' ? c : ' ').ToArray());
    internal static IReadOnlyList<string> Components(string text) => FirmamentSyntaxValues.Components(text).Select(c => c.Text).ToArray();
    internal static string SetName(string identity) => "__ConceptPoints_" + identity.Split('.').Aggregate("", (name, part) => name + part.Length.ToString(CultureInfo.InvariantCulture) + "_" + part);
    internal static string Literal(double value, string unit)
    {
        // Existing point grammars consume plain decimals. Expand round-trip exponent
        // spelling without rounding: a tiny Float may later be multiplied by a large Length.
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        var exponentAt = text.IndexOf('E');
        if (exponentAt < 0) return text + unit;
        var mantissa = text[..exponentAt];
        var sign = mantissa.StartsWith('-') ? "-" : "";
        mantissa = mantissa.TrimStart('-');
        var decimalAt = mantissa.IndexOf('.');
        if (decimalAt < 0) decimalAt = mantissa.Length;
        decimalAt += int.Parse(text[(exponentAt + 1)..], CultureInfo.InvariantCulture);
        var digits = mantissa.Replace(".", "", StringComparison.Ordinal);
        var plain = decimalAt <= 0 ? "0." + new string('0', -decimalAt) + digits
            : decimalAt >= digits.Length ? digits + new string('0', decimalAt - digits.Length)
            : digits.Insert(decimalAt, ".");
        return sign + plain + unit;
    }
}
