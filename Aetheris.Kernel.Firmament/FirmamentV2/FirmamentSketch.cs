using System.Globalization;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentSketchChoice(string Id, string Name);
public sealed record FirmamentSketchControl(string Id, string Label, string Authored, double Value, string Unit,
    bool Editable, string? Parameter, string? ReadOnlyReason, FirmamentOutlineSpan Span);
public sealed record FirmamentSketchShape(string Id, string Name, string Kind, double[] Center,
    IReadOnlyList<FirmamentSketchControl> Controls);
public sealed record FirmamentSketchCurve(string Id, string Name, string Kind, double[][] Points,
    double[]? Center = null, double Radius = 0, double StartAngle = 0, double Sweep = 0,
    double MinorRadius = 0, double Rotation = 0, ProfileSegmentProvenance? Provenance = null);
public sealed record FirmamentSketchLoop(string Name, bool IsOuter, IReadOnlyList<FirmamentSketchCurve> Curves);
public sealed record FirmamentSketchFrame(string Name, double[] Origin, double[] X, double[] Y, double[] Normal);
public sealed record FirmamentSketch(string Version, string Document, string Revision, string? Node,
    IReadOnlyList<FirmamentSketchChoice> Profiles, IReadOnlyList<FirmamentSketchChoice> EmptyModels,
    IReadOnlyList<FirmamentSketchChoice> OpeningReferences,
    FirmamentSketchFrame? Frame, IReadOnlyList<FirmamentSketchLoop> Loops, IReadOnlyList<FirmamentSketchShape> Shapes,
    IReadOnlyList<FirmamentOutlineDiagnostic> Diagnostics, string ValidationLevel);
public sealed record FirmamentSketchChange(string Control, double Value);
public sealed record FirmamentSketchEditRequest(string Kind, string? Node = null, string? Shape = null,
    IReadOnlyList<FirmamentSketchChange>? Changes = null, double X = 0, double Y = 0,
    string? Reference = null, string? Anchor = null, double Diameter = 4, double Width = 80, double Height = 50);

/// <summary>Source-owned 2D projection and edits. It reuses domain binding, closed-boundary admission,
/// Profile resolution and structural transactions. A resolved sketch is deliberately not a built solid.</summary>
public static class FirmamentSketchService
{
    private static readonly HashSet<string> EditableShapes = ["Rect2", "Circle2", "Slot2", "RoundedRect2"];
    private static readonly IReadOnlySet<string> DomainResults = new HashSet<string>(StringComparer.Ordinal);
    private sealed record Target(string Node, string? Field, ExpressionSyntax Expression, string? Parameter);

    public static FirmamentSketch Project(string source, string document, string revision, string? node = null)
        => Read(source, document, revision, node).View;

    private static (FirmamentSketch View, Dictionary<string, Target> Targets) Read(string source, string document, string revision, string? node)
    {
        var parsed = FirmamentSyntaxParser.Parse(source, document);
        var declarations = FirmamentLanguageOutlineService.Declarations(parsed);
        var outline = FirmamentLanguageOutlineService.Outline(source, document, revision);
        var profiles = declarations.Where(d => d.Syntax.Type.Name.Text == "Profile" && d.Syntax.Name is not null
            && !InRecipe(d.Id)).Select(d => new FirmamentSketchChoice(d.Id, d.Syntax.Name!.Text)).ToArray();
        var empty = declarations.Where(d => d.Syntax.Type.Name.Text == "Model" && d.Syntax.Body is not null
            && !FirmamentV2Parser.MechanicalDeclarations(d.Syntax.Body.Statements).Any(c => FirmamentV2Parser.IsPrimitiveDeclaration(c)
                || c.Type.Name.Text is "Extrude" or "Revolve" or "Compose" or "InlineStep" or "Sculpt"))
            .Select(d => new FirmamentSketchChoice(d.Id, d.Syntax.Name?.Text ?? d.Id)).ToArray();
        node ??= profiles.FirstOrDefault()?.Id;
        // A shape node opens the Profile that consumes it; a shared shape needs an explicit Profile choice.
        if (node is not null && !profiles.Any(p => p.Id == node))
        {
            var candidates = profiles.Where(p => Reachable(p.Id).Contains(node)).ToArray();
            if (candidates.Length == 1) node = candidates[0].Id;
        }
        var errors = parsed.Diagnostics.Select(d => Diagnostic(d.Code, d.Message, d.SourceSpan)).ToList();
        var targets = new Dictionary<string, Target>(StringComparer.Ordinal);
        FirmamentSketch Result(FirmamentSketchFrame? frame = null, IReadOnlyList<FirmamentSketchLoop>? loops = null,
            IReadOnlyList<FirmamentSketchShape>? shapes = null) => new("firmament-sketch/1", document, revision, node,
                profiles, empty, declarations.Where(d => reachableOpening(d.Id, shapes)).Select(d => new FirmamentSketchChoice(d.Id, d.Syntax.Name!.Text)).ToArray(), frame, loops ?? [], shapes ?? [], errors,
                errors.Count == 0 && frame is not null ? "ProfileResolvedNotBuilt" : "Unresolved");
        if (node is null) return (Result(), targets);
        var selected = declarations.FirstOrDefault(d => d.Id == node);
        if (selected.Syntax?.Type.Name.Text != "Profile")
        {
            errors.Add(Diagnostic("sketch-profile-required", "Choose the Profile to edit. A shape may be shared by several Profiles.", selected.Syntax?.Span));
            return (Result(), targets);
        }
        if (profiles.Count(p => p.Name == selected.Syntax.Name!.Text) != 1)
        {
            errors.Add(Diagnostic("sketch-profile-ambiguous", "This projection requires a uniquely named Profile; choose a document with one unambiguous authored owner.", selected.Syntax.Span));
            return (Result(), targets);
        }
        if (errors.Count > 0) return (Result(), targets);
        var binding = new FirmamentDomainBinding(source, document, "Model", DomainResults);
        errors.AddRange(binding.Diagnostics.Where(d => d.Severity is "error" or "fatal").Select(d => Diagnostic(d.Code, d.Message, d.SourceSpan)));
        if (errors.Count > 0) return (Result(), targets);
        var profile = binding.ResolveProfile(selected.Syntax.Name!.Text, out var codes);
        errors.AddRange(codes.Select(c => Diagnostic(c.Split(':')[0], c, selected.Syntax.Span)));
        if (profile is null || errors.Count > 0) return (Result(), targets);
        var reachable = Reachable(node);
        var shapes = new List<FirmamentSketchShape>();
        foreach (var item in declarations.Where(d => reachable.Contains(d.Id) && EditableShapes.Contains(d.Syntax.Type.Name.Text) && !InRecipe(d.Id)))
        {
            var controls = new List<FirmamentSketchControl>();
            var center = new double[2];
            foreach (var field in item.Syntax.Body?.Statements.OfType<FieldSyntax>() ?? [])
            {
                var expression = field.Value;
                var nominal = binding.Nominal(expression);
                var unit = FirmamentSyntaxFields.SameField(field.Name.Text, "Rotation") ? "deg" : "mm";
                var parts = Components(expression);
                var values = Components(nominal);
                if (parts.Length > 0 && values.Length == parts.Length)
                {
                    for (var i = 0; i < parts.Length; i++) Add(parts[i], values[i], i);
                }
                else Add(expression, nominal, null);

                void Add(ExpressionSyntax authored, ExpressionSyntax effective, int? axis)
                {
                    if (!FirmamentSyntaxValues.TryMeasure(effective, unit, out var number) || !double.IsFinite(number)) return;
                    var id = item.Id + "/" + field.Name.Text + (axis is null ? "" : "/" + axis);
                    var target = new Target(item.Id, field.Name.Text, authored, null);
                    if (!Literal(authored))
                    {
                        // Only a directly named, literal Static is writable upstream. Arithmetic,
                        // toleranced values and scoped records retain their expression and provenance.
                        var edge = outline.Edges.Where(e => e.From == item.Id && e.Span.Start == authored.Span.Start && !e.Ambiguous).ToArray();
                        if (authored is ReferenceSyntax && edge.Length == 1)
                        {
                            var parameter = declarations.FirstOrDefault(d => d.Id == edge[0].To);
                            if (parameter.Syntax is { Type.Name.Text: "Static", Initializer: { } initial } && Literal(initial))
                                target = new(parameter.Id, null, initial, parameter.Syntax.Name?.Text);
                        }
                    }
                    var editable = Literal(target.Expression);
                    if (editable) targets[id] = target;
                    var label = FieldLabel(field.Name.Text, axis);
                    controls.Add(new(id, label, Slice(source, authored.Span), number, unit, editable, target.Parameter,
                        editable ? null : "Driven by an expression. Edit its inputs in Parameters or Source; dragging will not replace it.",
                        new(authored.Span.Start, authored.Span.Length)));
                    if (FirmamentSyntaxFields.SameField(field.Name.Text, "Center") && axis is 0 or 1) center[axis.Value] = number;
                }
            }
            shapes.Add(new(item.Id, item.Syntax.Name!.Text, item.Syntax.Type.Name.Text, center, controls));
        }
        var plane = profile.EffectiveConstructionPlane;
        return (Result(new(profile.PlaneFrame, [plane.Origin.X, plane.Origin.Y, plane.Origin.Z],
            [plane.AxisX.X, plane.AxisX.Y, plane.AxisX.Z], [plane.AxisY.X, plane.AxisY.Y, plane.AxisY.Z],
            [plane.AxisZ.X, plane.AxisZ.Y, plane.AxisZ.Z]),
            profile.Loops.Select(l => new FirmamentSketchLoop(l.Name, l.IsOuter, l.Segments.Select(Curve).ToArray())).ToArray(), shapes), targets);

        bool InRecipe(string id) => declarations.Any(d => (id == d.Id || id.StartsWith(d.Id + "/", StringComparison.Ordinal))
            && (d.Syntax.Type.Name.Text is "Template" or "Feature" || d.Syntax.Modifiers.Any(m => m.Name.Text == "Template")));
        HashSet<string> Reachable(string root)
        {
            var found = new HashSet<string>(StringComparer.Ordinal); var queue = new Queue<string>(); queue.Enqueue(root);
            while (queue.TryDequeue(out var id))
            {
                if (!found.Add(id)) continue;
                foreach (var child in outline.Nodes.FirstOrDefault(n => n.Id == id)?.Children ?? []) queue.Enqueue(child);
                foreach (var edge in outline.Edges.Where(e => e.From == id && !e.Ambiguous && e.ToDocument is null)) queue.Enqueue(edge.To);
            }
            return found;
        }
        bool reachableOpening(string id, IReadOnlyList<FirmamentSketchShape>? shapes)
        {
            var shape = shapes?.FirstOrDefault(s => s.Id == id && s.Kind == "Rect2");
            if (shape is null || shape.Controls.Any(c => c.Unit == "deg" && c.Value != 0)) return false;
            var entry = declarations.Single(d => d.Id == id);
            return declarations.Any(d => d.Id == entry.ParentId && d.Syntax.Type.Name.Text == "Model")
                && new[] { "Width", "Height", "Center X", "Center Y" }.All(label => shape.Controls.Any(c => c.Label == label))
                && declarations.FirstOrDefault(d => d.Id == node).Syntax?.Body is { } body
                && !body.Statements.OfType<FieldSyntax>().Any(f => FirmamentSyntaxFields.SameField(f.Name.Text, "From"))
                && outline.Nodes.FirstOrDefault(n => n.ParentId == node && n.Name == "Outer") is { } outer
                && Reachable(outer.Id).Contains(id);
        }
    }

    public static FirmamentLanguageEdit Edit(string source, string document, string revision, FirmamentSketchEditRequest request)
    {
        var operations = new List<FirmamentLanguageEditOperation>();
        if (request.Kind == "create")
        {
            var view = Project(source, document, revision);
            if (!view.EmptyModels.Any(m => m.Id == request.Node)) throw Refuse("sketch-model-occupied", "Choose an empty Model. A new sketch part creates one profile and one extrusion.");
            if (request.Shape is null || !EditableShapes.Contains(request.Shape)) throw Refuse("sketch-shape-unsupported", "Choose Rectangle, Circle, Slot or Rounded rectangle.");
            var names = FirmamentLanguageOutlineService.Outline(source, document, revision).Nodes.Select(n => n.Name).ToHashSet();
            string Fresh(string stem) { var name = stem; for (var i = 2; names.Contains(name); i++) name = stem + i; names.Add(name); return name; }
            var shape = Fresh("Outline"); var profile = Fresh("Section"); var body = Fresh("Body");
            if (!double.IsFinite(request.Width) || !double.IsFinite(request.Height) || request.Width <= 0 || request.Height <= 0
                || !double.IsFinite(request.X) || !double.IsFinite(request.Y)) throw Refuse("sketch-number-invalid", "Use finite positive sizes and a finite center.");
            var fields = request.Shape switch
            {
                "Circle2" => $"diameter: {Length(request.Width)}", "Slot2" => $"length: {Length(request.Width)} width: {Length(request.Height)}",
                "RoundedRect2" => $"size: [{Length(request.Width)}, {Length(request.Height)}] radius: {Length(Math.Min(request.Width, request.Height) / 8)}",
                _ => $"size: [{Length(request.Width)}, {Length(request.Height)}]"
            };
            operations.Add(new("insertDeclaration") { Parent = request.Node, Source = $"{request.Shape} {shape} {{ center: [{Length(request.X)}, {Length(request.Y)}] {fields} }}" });
            operations.Add(new("insertDeclaration") { Parent = request.Node, Source = $"Profile {profile} {{ Loop Outer {{ {shape} |> TraceLoop }} }}" });
            operations.Add(new("insertDeclaration") { Parent = request.Node, Source = $"Extrude {body} {{ profile: {profile} from: 0mm to: 6mm }}" });
        }
        else if (request.Kind == "opening")
        {
            var view = Project(source, document, revision, request.Node);
            if (view.ValidationLevel != "ProfileResolvedNotBuilt" || !view.OpeningReferences.Any(r => r.Id == request.Reference))
                throw Refuse("sketch-opening-reference-unsupported", "An opening requires an unrotated Model-level Rect2 and an explicit-loop Profile. Other reference families are not qualified yet.");
            if (!double.IsFinite(request.X) || !double.IsFinite(request.Y) || request.X < 0 || request.Y < 0 || !double.IsFinite(request.Diameter) || request.Diameter <= 0)
                throw Refuse("sketch-opening-size-invalid", "Use finite nonnegative edge insets and a positive opening diameter.");
            var signs = request.Anchor switch
            {
                "BottomLeft" => (-1, -1), "BottomRight" => (1, -1), "TopRight" => (1, 1), "TopLeft" => (-1, 1),
                "Left" => (-1, 0), "Right" => (1, 0), "Bottom" => (0, -1), "Top" => (0, 1),
                "Center" => (0, 0), _ => throw Refuse("sketch-anchor-unsupported", "Choose Center, a named corner or an edge midpoint.")
            };
            var referenceShape = view.Shapes.Single(s => s.Id == request.Reference);
            double Dimension(string label) => referenceShape.Controls.Single(c => c.Label == label).Value;
            var halfWidth = Dimension("Width") / 2; var halfHeight = Dimension("Height") / 2;
            var px = Dimension("Center X") + signs.Item1 * (halfWidth - request.X);
            var py = Dimension("Center Y") + signs.Item2 * (halfHeight - request.Y);
            var radius = request.Diameter / 2;
            if (Math.Abs(px - Dimension("Center X")) + radius >= halfWidth
                || Math.Abs(py - Dimension("Center Y")) + radius >= halfHeight)
                throw Refuse("sketch-opening-outside", "The entire circular opening must lie strictly inside the rectangular outline. Increase the edge inset or reduce its diameter.");
            foreach (var loop in view.Loops.Where(l => !l.IsOuter))
            {
                if (loop.Curves.Count != 1 || loop.Curves[0].Kind != "Circle")
                    throw Refuse("sketch-opening-clearance-unqualified", "Adding an opening beside this noncircular inner loop requires a qualified clearance check; edit it in source.");
                var circle = loop.Curves[0]; var dx = px - circle.Center![0]; var dy = py - circle.Center[1];
                if (Math.Sqrt(dx * dx + dy * dy) <= radius + circle.Radius)
                    throw Refuse("sketch-opening-overlap", $"The new opening overlaps or touches '{loop.Name}'. Choose another anchor or inset.");
            }
            var declarations = FirmamentLanguageOutlineService.Declarations(FirmamentSyntaxParser.Parse(source));
            var rectangle = declarations.Single(d => d.Id == request.Reference);
            var names = declarations.Select(d => d.Syntax.Name?.Text).ToHashSet();
            string Fresh(string stem) { var name = stem; for (var i = 2; names.Contains(name); i++) name = stem + i; names.Add(name); return name; }
            var fields = rectangle.Syntax.Body!.Statements.OfType<FieldSyntax>().ToArray();
            var center = fields.Single(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Center"));
            var size = fields.Single(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Size"));
            var c = Components(center.Value); var s = Components(size.Value);
            if (c.Length != 2 || s.Length != 2) throw Refuse("sketch-reference-expression-unsupported", "This rectangle's center and size need two authored components.");
            // Share the existing driving expressions by naming them, exactly as the Parameters
            // sheet does. No sampled edge coordinates or new relation evaluator are persisted.
            string Parameter(ExpressionSyntax expression, string suffix)
            {
                var text = Slice(source, expression.Span);
                if (expression is ReferenceSyntax) return text;
                var name = Fresh(rectangle.Syntax.Name!.Text + suffix);
                operations.Add(new("insertDeclaration") { Parent = rectangle.ParentId, First = true, Source = $"Static {name}: Length = {text}" });
                return name;
            }
            var cx = Parameter(c[0], "CenterX"); var cy = Parameter(c[1], "CenterY");
            var width = Parameter(s[0], "Width"); var height = Parameter(s[1], "Height");
            operations.Add(new("setField") { Node = rectangle.Id, Field = center.Name.Text, Value = $"[{cx}, {cy}]" });
            operations.Add(new("setField") { Node = rectangle.Id, Field = size.Name.Text, Value = $"[{width}, {height}]" });
            string Position(string origin, string dimension, int sign, double inset) => sign == 0 ? origin
                : $"{origin} {(sign < 0 ? "-" : "+")} ({dimension} / 2 - {Length(inset)})";
            var name = Fresh("MountOpening");
            operations.Add(new("insertDeclaration") { Parent = rectangle.ParentId, Source = $"Circle2 {name} {{ center: [{Position(cx, width, signs.Item1, request.X)}, {Position(cy, height, signs.Item2, request.Y)}] diameter: {Length(request.Diameter)} }}" });
            operations.Add(new("insertDeclaration") { Parent = view.Node, Source = $"Loop {name} {{ {name} |> TraceLoop }}" });
        }
        else if (request.Kind == "dimensions")
        {
            var (view, targets) = Read(source, document, revision, request.Node);
            if (view.ValidationLevel != "ProfileResolvedNotBuilt") throw Refuse("sketch-unresolved", "Resolve the selected profile before editing its dimensions.");
            var changes = request.Changes ?? [];
            if (changes.Count == 0 || changes.Select(c => c.Control).Distinct().Count() != changes.Count) throw Refuse("sketch-edit-empty", "Supply unique dimension changes.");
            var replacements = new Dictionary<(string Node, string? Field), List<(ExpressionSyntax Expression, string Value)>>();
            foreach (var change in changes)
            {
                if (!targets.TryGetValue(change.Control, out var target)) throw Refuse("sketch-dimension-readonly", "The dimension is driven by an expression or is no longer available. Edit its inputs instead.");
                if (!double.IsFinite(change.Value)) throw Refuse("sketch-number-invalid", "A dimension must be finite.");
                var control = view.Shapes.SelectMany(s => s.Controls).Single(c => c.Id == change.Control);
                var key = (target.Node, target.Field);
                if (!replacements.TryGetValue(key, out var list)) replacements[key] = list = [];
                var value = change.Value.ToString("R", CultureInfo.InvariantCulture) + control.Unit;
                if (list.Any(p => p.Expression.Span == target.Expression.Span && p.Value != value))
                    throw Refuse("sketch-shared-parameter-conflict", "These dimensions share a parameter and requested different values. Edit the parameter once.");
                list.Add((target.Expression, value));
            }
            var declarations = FirmamentLanguageOutlineService.Declarations(FirmamentSyntaxParser.Parse(source));
            foreach (var (key, replacementsForField) in replacements)
            {
                var declaration = declarations.Single(d => d.Id == key.Node).Syntax;
                var value = key.Field is null ? declaration.Initializer! : declaration.Body!.Statements.OfType<FieldSyntax>().Single(f => f.Name.Text == key.Field).Value;
                var text = Slice(source, value.Span);
                foreach (var replacement in replacementsForField.Distinct().OrderByDescending(p => p.Expression.Span.Start))
                    text = text.Remove(replacement.Expression.Span.Start - value.Span.Start, replacement.Expression.Span.Length)
                        .Insert(replacement.Expression.Span.Start - value.Span.Start, replacement.Value);
                operations.Add(new(key.Field is null ? "setValue" : "setField") { Node = key.Node, Field = key.Field, Value = text });
            }
        }
        else throw Refuse("sketch-operation-unsupported", "That sketch operation has not been qualified.");
        var result = FirmamentLanguageEditService.Apply(source, document, revision, operations);
        // No silently clamped radii, changed winding or implicit geometry repair. Reuse admission
        // before returning a source transaction; the caller still performs the full engineering build.
        var after = Project(result.Text, document, revision, request.Kind == "create" ? result.Nodes[^2] : request.Node);
        if (after.ValidationLevel != "ProfileResolvedNotBuilt")
            throw Refuse("sketch-profile-invalid", string.Join("\n", after.Diagnostics.Select(d => d.Message)));
        return result;
    }

    private static string FieldLabel(string name, int? axis) => (name.ToLowerInvariant(), axis) switch
    {
        ("size", 0) => "Width", ("size", 1) => "Height", ("center", 0) => "Center X", ("center", 1) => "Center Y",
        ("length", _) => "Overall length", _ => name + (axis is null ? "" : " " + axis)
    };
    private static ExpressionSyntax[] Components(ExpressionSyntax expression) => expression switch
    {
        SequenceSyntax { Kind: SequenceKind.List } list => list.Items.Select(a => a.Value).ToArray(),
        CallSyntax { Target: ReferenceSyntax { Name.Text: "Point2" } } call => call.Arguments.Select(a => a.Value).ToArray(),
        _ => []
    };
    private static bool Literal(ExpressionSyntax expression) => expression switch
    {
        LiteralSyntax { Kind: LiteralKind.Number } => true,
        QuantitySyntax { Value: LiteralSyntax { Kind: LiteralKind.Number } } => true,
        UnarySyntax { Operator: UnaryOperator.Positive or UnaryOperator.Negative } unary => Literal(unary.Operand),
        _ => false
    };
    private static string Slice(string source, FirmamentV2SourceSpan span) => source.Substring(span.Start, span.Length);
    private static FirmamentOutlineDiagnostic Diagnostic(string code, string message, FirmamentV2SourceSpan? span) => new("error", code, message,
        span is null ? null : new(span.Start, span.Length));
    private static FirmamentLanguageEditException Refuse(string code, string message) => new(code, message);
    private static string Length(double value) => value.ToString("R", CultureInfo.InvariantCulture) + "mm";
    private static FirmamentSketchCurve Curve(ResolvedProfileSegment2D segment)
    {
        static double[] P((double X, double Y) p) => [p.X, p.Y];
        return segment.Geometry switch
        {
            LineArcLineSegment2D line => new(segment.Provenance.StableId, segment.Name, "Line", [P(line.Start), P(line.End)], Provenance: segment.Provenance),
            LineArcCubicBezier2D cubic => new(segment.Provenance.StableId, segment.Name, "Cubic", [P(cubic.Start), P(cubic.Control1), P(cubic.Control2), P(cubic.End)], Provenance: segment.Provenance),
            LineArcCircularArc2D arc => new(segment.Provenance.StableId, segment.Name, "Arc", [], P(arc.Center), arc.Radius, arc.StartAngleRadians, arc.SweepAngleRadians, Provenance: segment.Provenance),
            LineArcFullCircle2D circle => new(segment.Provenance.StableId, segment.Name, "Circle", [], P(circle.Center), circle.Radius, Sweep: 2 * Math.PI, Provenance: segment.Provenance),
            LineArcFullEllipse2D ellipse => new(segment.Provenance.StableId, segment.Name, "Ellipse", [], P(ellipse.Center), ellipse.MajorRadius, Sweep: 2 * Math.PI, MinorRadius: ellipse.MinorRadius, Rotation: ellipse.RotationRadians, Provenance: segment.Provenance),
            _ => throw new NotSupportedException($"Sketch projection does not support {segment.Geometry.GetType().Name}.")
        };
    }
}
