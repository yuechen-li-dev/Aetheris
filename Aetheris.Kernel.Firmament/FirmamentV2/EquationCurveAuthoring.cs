using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>Resolved compile-time curve. Coordinates are local millimetres;
/// representation accuracy is distinct from an engineering tolerance.</summary>
public sealed record FirmamentEquationCurve(string StableId, string Name, string ElementType,
    string Parameter, double Start, double End, string Frame, string Representation, string GeometryKind,
    double? MaxDeviation, double ErrorBound, IReadOnlyList<FirmamentEquationCurveSegment> Segments,
    bool Closed, bool Periodic, string Equation, FirmamentV2SourceSpan? SourceSpan)
{
    public IReadOnlyList<string> GeneratedGuides { get; init; } = [];
}
public sealed record FirmamentEquationCurveSegment(double Start, double End, string Kind,
    IReadOnlyList<IReadOnlyList<double>> Controls);

/// <summary>Local AST admission and lowering to existing construction guides.
/// This owner does not choose a document parser or execute host-language code.</summary>
internal static class EquationCurveAuthoring
{
    internal sealed record BoundCurve(FirmamentEquationCurve Value, ExpressionSyntax Point, ConstructionPlane? Plane);
    internal sealed record Result(string Source, IReadOnlyList<BoundCurve> Curves);

    internal static Result Expand(string source, ConceptPointAuthoring.MathCatalog math, FirmamentCompileTimeValues? scalars,
        Action<string, string, FirmamentV2SourceSpan> error)
    {
        var document = FirmamentSyntaxParser.Parse(source).Document;
        var declarations = FirmamentSyntaxTraversal.Declarations(document).ToArray();
        var curves = new List<BoundCurve>();
        var changes = new List<(FirmamentV2SourceSpan Span, string Text)>();
        var lowered = new Dictionary<string, string>(StringComparer.Ordinal);
        var frames = new Dictionary<string, string>(StringComparer.Ordinal);
        var generated = new List<string>();
        var occupied = document.Tokens.Where(t => t.Kind == SyntaxTokenKind.Identifier).Select(t => t.Text).ToHashSet(StringComparer.Ordinal);
        string Fresh(string id)
        {
            var stem = "__Equation_" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(id)))[..20];
            var name = stem; var n = 0; while (!occupied.Add(name)) name = stem + "_" + ++n; return name;
        }
        foreach (var d in declarations.Where(d => d.Type.Name.Text is "Curve2" or "Curve3" && d.Body is not null))
        {
            // Existing profile-copy Curve2 remains owned by ConceptProfileDerivation.
            if (!d.Body!.Statements.OfType<FieldSyntax>().Any(f => f.Name.Text.Equals("parameter", StringComparison.OrdinalIgnoreCase)) && d.Type.Name.Text == "Curve2") continue;
            if (curves.Count >= 128) { error("curve-limit", "Use at most 128 equation curves per compilation.", d.Span); break; }
            var owner = declarations.SingleOrDefault(o => o.Type.Name.Text == "Struct" && o.Modifiers.Any(m => m.Name.Text == "Concept") && o.Body?.Statements.Contains(d) == true);
            if (owner?.Name is null || d.Name is null || d.Occurrence || d.Type.Arguments.Count != 0 || d.Modifiers.Count != 0 || d.HasParameterList
                || d.Annotation is not null || d.ResultType is not null || d.Initializer is not null || d.Clauses.Count != 0
                || owner.Annotation is not null || owner.HasParameterList || owner.Modifiers.Count != 1 || owner.Type.Arguments.Count != 0
                || owner.Occurrence || owner.Initializer is not null || owner.ResultType is not null
                || owner.Clauses.Count > 1 || owner.Clauses.Any(c => c.Kind != ClauseKind.On || c.Value is not ReferenceSyntax))
            { error("curve-header-invalid", "Equation Curve2/Curve3 requires a named declaration directly inside a plain Concept Struct (optionally on XY/XZ/YZ).", d.Span); continue; }
            var identity = owner.Name.Text + "." + d.Name.Text;
            if (curves.Any(c => c.Value.Name == identity)) { error("curve-duplicate", $"Duplicate curve '{identity}'.", d.Span); continue; }
            var fields = new Dictionary<string, FieldSyntax>(StringComparer.OrdinalIgnoreCase); var valid = true;
            foreach (var s in d.Body.Statements)
                if (s is not FieldSyntax f || !fields.TryAdd(f.Name.Text, f)) { error("curve-field-invalid", $"Curve '{identity}' requires unique named fields.", s.Span); valid = false; }
            ExpressionSyntax? Take(string name) => fields.Remove(name, out var f) ? f.Value : null;
            var parameterExpression = Take("parameter"); var parameter = parameterExpression is ReferenceSyntax p ? p.Name.Text : null;
            var domain = Take("domain") as SequenceSyntax;
            var from = Take("from"); var on = Take("on"); var representation = FirmamentSyntaxValues.Reference(Take("representation")) ?? "Exact";
            var deviation = Take("maxDeviation"); var closedExpression = Take("closed"); var periodicExpression = Take("periodic");
            foreach (var f in fields.Values) { error("curve-field-unknown", $"Unknown field '{f.Name.Text}' in {d.Type.Name.Text} '{identity}'.", f.Span); valid = false; }
            if (parameter is null || from is null || domain is not { Kind: SequenceKind.List, Items.Count: 2 } || domain.Items.Any(a => a.Name is not null || a.Default is not null))
            { error("curve-domain-invalid", $"Curve '{identity}' requires parameter: identifier, domain: [start,end] and a typed Point expression.", d.Span); continue; }
            bool Scalar(ExpressionSyntax e, string unit, out double value)
            {
                if (scalars is not null && FirmamentSyntaxTraversal.Expressions(e).Any(x => FirmamentSyntaxValues.Reference(x) is { } reference && scalars.TryValue(reference, out var v) && v.Tolerance is not null))
                { error("curve-tolerance-unsupported", "A curve cannot discard a manufacturing tolerance. maxDeviation is a separate representation error budget.", e.Span); value = double.NaN; return false; }
                return math.Evaluate(e, new Dictionary<string, (double, string)>(), out value, out var actual) && actual == unit;
            }
            if (!Scalar(domain.Items[0].Value, "", out var start) || !Scalar(domain.Items[1].Value, "", out var end) || end <= start || !double.IsFinite(end - start))
            { error("curve-domain-invalid", $"Curve '{identity}' needs a finite, increasing dimensionless domain.", domain.Span); continue; }
            bool Flag(ExpressionSyntax? e, out bool value)
            { value = e is LiteralSyntax { Raw: "true" }; return e is null or LiteralSyntax { Raw: "true" or "false" }; }
            if (!Flag(closedExpression, out var closed) || !Flag(periodicExpression, out var periodic)) { error("curve-closure-invalid", "closed and periodic must be Boolean literals.", d.Span); continue; }
            closed |= periodic;
            var type = d.Type.Name.Text == "Curve2" ? "Point2" : "Point3";
            var dimension = type == "Point2" ? 2 : 3;
            var frame = FirmamentSyntaxValues.Reference(on) ?? (type == "Point3" ? "World" : owner.Clauses.FirstOrDefault(c => c.Kind == ClauseKind.On) is { } clause ? FirmamentSyntaxValues.Reference(clause.Value) : null);
            ConstructionPlane? plane = null;
            if (frame is null || on is not null && FirmamentSyntaxValues.Reference(on) is null || type == "Point3" && (frame != "World" || owner.Clauses.Count != 0))
            { error("curve-frame-unsupported", $"Curve '{identity}' requires an explicit Curve2 plane, or Curve3 on World in its definition's coordinates.", on?.Span ?? d.Span); continue; }
            if (type == "Point2")
            {
                var errors = new List<string>();
                var frameSource = source;
                var lookup = frame;
                if (frame is "XY" or "XZ" or "YZ") { lookup = Fresh(identity + ":lookup"); frameSource += $"\nConcept Struct {lookup} on {frame} {{ }}"; }
                else
                {
                    lookup = Fresh(identity + ":lookup");
                    frameSource += $"\nConstruction Plane {lookup} {{ Trace: {(frame.Contains('.') ? frame : owner.Name.Text + "." + frame)} }}";
                }
                plane = ProfileAuthoringParser.ResolveNamedConstructionPlane(frameSource, lookup, errors);
                if (plane is null) { error("curve-frame-unresolved", $"Curve '{identity}' cannot resolve plane '{frame}': {string.Join("; ", errors)}", on?.Span ?? d.Span); continue; }
            }
            double? maximum = null;
            if (representation is not ("Exact" or "Approximate") || representation == "Exact" && deviation is not null
                || representation == "Approximate" && (deviation is null || !Scalar(deviation, "mm", out var limit) || limit <= 0))
            { error("curve-representation-invalid", "Use Exact without maxDeviation, or Approximate with a positive maxDeviation Length.", deviation?.Span ?? d.Span); continue; }
            if (deviation is not null && Scalar(deviation, "mm", out var budget)) maximum = budget;
            if (!valid) continue;
            try
            {
                // Manufacturing tolerance must not disappear through Function inputs.
                if (scalars is not null && FirmamentSyntaxTraversal.Expressions(from).Any(x => FirmamentSyntaxValues.Reference(x) is { } reference && reference != parameter
                    && scalars.TryValue(reference, out var v) && v.Tolerance is not null)) throw new FormatException("A manufacturing tolerance cannot be erased into curve coordinates.");
                var point = math.BindCurve(from, parameter);
                if (point is not CallSyntax { Target: ReferenceSyntax target } c || target.Name.Text != type || c.Arguments.Count != dimension || c.Arguments.Any(a => a.Name is not null || a.Default is not null)) throw new FormatException($"from must return {type}.");
                var coordinates = c.Arguments.Select(a => a.Value).ToArray();
                var variables = new Dictionary<string, FirmamentNumericEvaluation.Value> { [parameter] = new(1, "") };
                if (coordinates.Any(e => !FirmamentNumericEvaluation.TryEvaluate(e, out var v, variables, typeOnly: true) || v.Unit != "mm")) throw new FormatException("Every coordinate must have Length type.");
                double[] At(double t) => coordinates.Select(e => FirmamentNumericEvaluation.TryEvaluate(e, out var v,
                    new Dictionary<string, FirmamentNumericEvaluation.Value> { [parameter] = new(t, "") }) ? v.Number : throw new FormatException("Undefined/non-finite equation value.")).ToArray();
                var segments = new List<FirmamentEquationCurveSegment>();
                var kind = "CubicBezier"; var errorBound = 0d;
                var polynomials = coordinates.Select(e => FirmamentNumericEvaluation.TryPolynomial(e, parameter, out var coefficients) ? coefficients : null).ToArray();
                if (representation == "Exact" && polynomials.All(p => p is not null))
                {
                    var a = At(start); var b = At(end); var h = end - start;
                    double[] Derivative(double t) => polynomials.Select(p => p![1] + 2 * p[2] * t + 3 * p[3] * t * t).ToArray();
                    var da = Derivative(start); var db = Derivative(end);
                    kind = polynomials.All(p => p![2] == 0 && p[3] == 0) ? "Line" : "CubicBezier";
                    segments.Add(new(start, end, kind, kind == "Line" ? [a, b] : [a, a.Zip(da, (x, v) => x + v * h / 3).ToArray(), b.Zip(db, (x, v) => x - v * h / 3).ToArray(), b]));
                }
                else if (representation == "Exact" && dimension == 2 && TryEllipse(coordinates, parameter, start, end, out var ellipse))
                { kind = ellipse.Kind; segments.Add(ellipse); }
                else if (representation == "Exact") throw new FormatException("Exact lowering currently admits degree <= 3 polynomials and axis-aligned full circles/ellipses. Request Approximate with maxDeviation for other bounded laws.");
                else
                {
                    kind = "Polyline";
                    void Divide(double a, double b, double[] pa, double[] pb, int depth)
                    {
                        if (segments.Count + curves.Sum(c => c.Value.Segments.Count) >= 4096 || depth >= 24) throw new FormatException("Approximation exhausted 4096 total segments or 24 subdivisions before proving maxDeviation.");
                        var bounds = coordinates.Select(e => FirmamentNumericEvaluation.TrySecondDerivativeBound(e, parameter, a, b, out var m) ? m : double.NaN).ToArray();
                        double EndpointError(double t, double[] point) => Math.Sqrt(coordinates.Select((e, i) =>
                            FirmamentNumericEvaluation.TryCurveEvaluationError(e, parameter, t, point[i], out var uncertainty) ? uncertainty * uncertainty : double.NaN).Sum());
                        var norm = Math.Sqrt(bounds.Sum(m => m * m));
                        var bound = Math.BitIncrement(norm * (b - a) * (b - a) / 8 + Math.Max(EndpointError(a, pa), EndpointError(b, pb)));
                        if (double.IsFinite(bound) && bound <= maximum)
                        { segments.Add(new(a, b, "Line", [pa, pb])); errorBound = Math.Max(errorBound, bound); return; }
                        var mid = a + (b - a) / 2;
                        if (mid == a || mid == b) throw new FormatException("Parameter refinement reached floating-point resolution without establishing maxDeviation.");
                        var pm = At(mid); Divide(a, mid, pa, pm, depth + 1); Divide(mid, b, pm, pb, depth + 1);
                    }
                    Divide(start, end, At(start), At(end), 0);
                }
                if (segments.SelectMany(s => s.Controls).SelectMany(p => p).Any(v => !double.IsFinite(v))) throw new FormatException("Curve representation exceeds finite coordinates.");
                var first = At(start); var last = At(end);
                if (closed && Distance(first, last) > 1e-8) throw new FormatException("closed/periodic endpoints do not coincide; no closing segment is inserted.");
                if (periodic)
                {
                    // Polynomial periodicity can be proved directly. Full conics
                    // have matching derivatives by their recognized construction.
                    if (polynomials.All(p => p is not null))
                    {
                        var da = polynomials.Select(p => p![1] + 2 * p[2] * start + 3 * p[3] * start * start).ToArray();
                        var db = polynomials.Select(p => p![1] + 2 * p[2] * end + 3 * p[3] * end * end).ToArray();
                        if (Distance(da, db) > 1e-8) throw new FormatException("periodic requires matching endpoint first derivatives.");
                    }
                    else if (kind is not ("Circle" or "Ellipse")) throw new FormatException("Periodicity proof for this law is not yet admitted; use closed for positional closure only.");
                }
                if (segments.All(s => s.Controls.All(p => Distance(p, first) <= 1e-12))) throw new FormatException("Curve is degenerate.");
                var value = new FirmamentEquationCurve("concept-curve:" + identity, identity, type, parameter, start, end, frame,
                    representation, kind, maximum, errorBound, segments, closed, periodic, FirmamentSyntaxWriter.Expression(from), scalars?.OriginalPointSpan(d.Span, source) ?? d.Span);
                curves.Add(new(value, point, plane));
                if (type == "Point3") { changes.Add((d.Span, Blank(source.Substring(d.Span.Start, d.Span.Length)))); continue; }
                var names = new List<string>(); var text = new List<string>();
                string Point(IReadOnlyList<double> xy) { var name = Fresh(identity + ":point:" + text.Count); text.Add($"Point2 {name} {{ Position: [{string.Join(",", xy.Select(x => ConceptPointAuthoring.Literal(x, "mm")))}] }}"); return name; }
                foreach (var s in segments)
                {
                    var name = Fresh(identity + ":segment:" + names.Count); names.Add(name);
                    if (s.Kind is "Circle" or "Ellipse")
                    {
                        var center = Point(s.Controls[0]); var radii = s.Controls[1];
                        text.Add(s.Kind == "Circle" ? $"Circle2 {name} {{ Center: {center}; Radius: {ConceptPointAuthoring.Literal(radii[0], "mm")} }}"
                            : $"Ellipse2Guide {name} {{ Center: {center}; SemiAxes: [{ConceptPointAuthoring.Literal(Math.Max(radii[0], radii[1]), "mm")},{ConceptPointAuthoring.Literal(Math.Min(radii[0], radii[1]), "mm")}]; Rotation: {(radii[1] > radii[0] ? "90deg" : "0deg")} }}");
                    }
                    else
                    {
                        var points = s.Controls.Select(Point).ToArray();
                        text.Add(s.Kind == "Line" ? $"Line2 {name} {{ From: {points[0]}; To: {points[1]} }}"
                            : $"CubicBezier2 {name} {{ From: {points[0]}; Control1: {points[1]}; Control2: {points[2]}; To: {points[3]} }}");
                    }
                }
                changes.Add((d.Span, string.Join("\n", text)));
                curves[^1] = curves[^1] with { Value = value with { GeneratedGuides = names } };
                lowered[identity] = string.Join(" |> ", names);
                var frameName = Fresh(identity + ":frame"); frames[identity] = frameName;
                generated.Add(frame is "XY" or "XZ" or "YZ" ? $"Concept Struct {frameName} on {frame} {{ }}" : $"Construction Plane {frameName} {{ Trace: {(frame.Contains('.') ? frame : owner.Name.Text + "." + frame)} }}");
            }
            catch (FormatException e) { error("curve-admission-failed", $"Curve '{identity}': {e.Message}", from.Span); }
        }
        math.Curves = curves.ToDictionary(c => c.Value.Name, StringComparer.Ordinal);
        foreach (var profile in declarations.Where(d => d.Type.Name.Text == "Profile" && d.Body is not null))
        {
            var refs = FirmamentSyntaxTraversal.Expressions(new DocumentSyntax("profile", [profile], [], profile.Span))
                .Select(FirmamentSyntaxValues.Reference).OfType<string>().Where(lowered.ContainsKey).Distinct().ToArray();
            if (refs.Length == 0) continue;
            var used = refs.Select(r => curves.Single(c => c.Value.Name == r)).ToArray();
            if (used.Any(c => !SamePlane(c.Plane!, used[0].Plane!))) { error("curve-frame-mismatch", "A Profile cannot mix equation curves from different planes.", profile.Span); continue; }
            var existing = profile.Clauses.FirstOrDefault(c => c.Kind == ClauseKind.Using || c.Kind == ClauseKind.On);
            if (existing is not null)
            {
                var diagnostics = new List<string>();
                var plane = ProfileAuthoringParser.ResolveNamedConstructionPlane(source, FirmamentSyntaxValues.Reference(existing.Value) ?? "", diagnostics);
                if (plane is null || !SamePlane(plane, used[0].Plane!)) { error("curve-frame-mismatch", "Profile placement disagrees with its equation curve's plane.", existing.Span); continue; }
            }
            try
            {
                var rewriter = new CurveReferences(lowered, curves);
                var rewritten = (DeclarationSyntax)rewriter.Statement(profile);
                if (existing is null) rewritten = rewritten with { Clauses = [new(ClauseKind.Using, FirmamentSyntaxParser.ParseExpression(frames[refs[0]]).Expression!, profile.Span)] };
                changes.Add((profile.Span, FirmamentSyntaxWriter.Statement(rewritten)));
            }
            catch (FormatException e) { error("curve-consumer-invalid", e.Message, profile.Span); }
        }
        foreach (var change in changes.OrderByDescending(c => c.Span.Start)) source = source.Remove(change.Span.Start, change.Span.Length).Insert(change.Span.Start, change.Text);
        return new(source + (generated.Count == 0 ? "" : "\n" + string.Join("\n", generated)), curves);
    }

    private sealed class CurveReferences(IReadOnlyDictionary<string, string> lowered, IReadOnlyList<BoundCurve> curves) : FirmamentSyntaxRewriter
    {
        internal override ExpressionSyntax Expression(ExpressionSyntax e)
        {
            if (e is UnarySyntax { Operator: UnaryOperator.Reverse } reverse
                && FirmamentSyntaxValues.Reference(reverse.Operand) is { } reversed && lowered.TryGetValue(reversed, out var ordered))
            {
                if (curves.Single(c => c.Value.Name == reversed).Value.GeometryKind is "Circle" or "Ellipse")
                    throw new FormatException($"Use {reversed} |> TraceLoop for a complete circle/ellipse; an open traversal requires explicit endpoints.");
                return FirmamentSyntaxParser.ParseExpression(string.Join(" |> ", ordered.Split(" |> ").Reverse().Select(n => "Reverse " + n))).Expression!;
            }
            if (e is BinarySyntax { Operator: BinaryOperator.Pipeline, Right: ReferenceSyntax { Name.Text: "TraceLoop" } } pipeline
                && FirmamentSyntaxValues.Reference(pipeline.Left) is { } reference && lowered.TryGetValue(reference, out var chain))
            {
                var curve = curves.Single(c => c.Value.Name == reference).Value;
                if (!curve.Closed && curve.GeometryKind is not ("Circle" or "Ellipse")) throw new FormatException($"TraceLoop requires closed: true on '{reference}'.");
                return FirmamentSyntaxParser.ParseExpression(chain + (curve.GeometryKind is "Circle" or "Ellipse" ? " |> TraceLoop" : "")).Expression!;
            }
            if (FirmamentSyntaxValues.Reference(e) is { } name && lowered.TryGetValue(name, out var text))
            {
                if (curves.Single(c => c.Value.Name == name).Value.GeometryKind is "Circle" or "Ellipse")
                    throw new FormatException($"Use {name} |> TraceLoop for a complete circle/ellipse; an open traversal requires explicit endpoints.");
                return FirmamentSyntaxParser.ParseExpression(text).Expression!;
            }
            return base.Expression(e);
        }
    }
    private static bool SamePlane(ConstructionPlane a, ConstructionPlane b) => a.Origin == b.Origin && a.AxisX == b.AxisX && a.AxisY == b.AxisY && a.AxisZ == b.AxisZ;
    internal static ResolvedProfile2D RestoreProvenance(ResolvedProfile2D profile, IReadOnlyList<FirmamentEquationCurve> curves)
    {
        var guides = curves.SelectMany(c => c.GeneratedGuides.Select(g => (Guide: g, Curve: c))).ToDictionary(p => p.Guide, p => p.Curve);
        return profile with { Loops = profile.Loops.Select(loop => loop with { Segments = loop.Segments.Select(segment =>
        {
            if (!guides.TryGetValue(segment.Provenance.TracedFrom ?? "", out var curve)) return segment;
            return segment with { Provenance = segment.Provenance with { ConceptStableId = curve.StableId, TracedFrom = curve.Name,
                SourceSpan = curve.SourceSpan is { } span ? $"offset:{span.Start}:length:{span.Length}" : segment.Provenance.SourceSpan,
                Derivation = $"Equation:{curve.Name};representation:{curve.Representation};errorBound:{curve.ErrorBound:R}mm", SourceFrame = curve.Frame } };
        }).ToArray() }).ToArray() };
    }
    private static double Distance(IReadOnlyList<double> a, IReadOnlyList<double> b) => Math.Sqrt(a.Zip(b, (x, y) => (x - y) * (x - y)).Sum());
    private static string Blank(string text) => new(text.Select(c => c is '\r' or '\n' ? c : ' ').ToArray());

    private static bool TryEllipse(ExpressionSyntax[] coordinates, string parameter, double start, double end, out FirmamentEquationCurveSegment ellipse)
    {
        ellipse = null!;
        // Recognize offset + radius * Cos/Sin(affine angle); no sample-based guessing.
        bool Axis(ExpressionSyntax e, string intrinsic, out double center, out double radius, out double phase, out double rate)
        {
            center = 0; radius = 1; phase = rate = 0;
            if (e is BinarySyntax { Operator: BinaryOperator.Add } add)
            {
                if (FirmamentNumericEvaluation.TryEvaluate(add.Left, out var a) && a.Unit == "mm") { center = a.Number; e = add.Right; }
                else if (FirmamentNumericEvaluation.TryEvaluate(add.Right, out a) && a.Unit == "mm") { center = a.Number; e = add.Left; }
                else return false;
            }
            if (e is not BinarySyntax { Operator: BinaryOperator.Multiply } product) return false;
            ExpressionSyntax trig;
            if (FirmamentNumericEvaluation.TryEvaluate(product.Left, out var r) && r.Unit == "mm") trig = product.Right;
            else if (FirmamentNumericEvaluation.TryEvaluate(product.Right, out r) && r.Unit == "mm") trig = product.Left;
            else return false;
            radius = r.Number;
            if (radius <= 0 || trig is not CallSyntax { Target: ReferenceSyntax t, Arguments.Count: 1 } call || t.Name.Text != intrinsic) return false;
            var arg = call.Arguments[0].Value;
            if (!FirmamentNumericEvaluation.TryPolynomial(arg, parameter, out var p) || p[2] != 0 || p[3] != 0
                || !FirmamentNumericEvaluation.TryEvaluate(arg, out var type, new Dictionary<string, FirmamentNumericEvaluation.Value> { [parameter] = new(1, "") }, typeOnly: true)) return false;
            var factor = type.Unit == "deg" ? Math.PI / 180 : 1; phase = p[0] * factor; rate = p[1] * factor; return true;
        }
        if (!Axis(coordinates[0], "Cos", out var x, out var rx, out var px, out var wx) || !Axis(coordinates[1], "Sin", out var y, out var ry, out var py, out var wy)
            || px != py || wx != wy || Math.Abs(wx * (end - start)) != 2 * Math.PI) return false;
        ellipse = new(start, end, rx == ry ? "Circle" : "Ellipse", [new[] { x, y }, new[] { rx, ry }]); return true;
    }
}
