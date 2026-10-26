using System.Globalization;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentDraftElement(string Id, string Name, string Kind, string? Parent,
    FirmamentOutlineSpan Span, IReadOnlyList<FirmamentDraftField> Fields);
public sealed record FirmamentDraftField(string Name, string Value, double? Number, string? Unit, bool Editable);
public sealed record FirmamentDraftPoint(string Name, double X, double Y);
public sealed record FirmamentDraftConstruction(IReadOnlyList<FirmamentDraftElement> Elements,
    IReadOnlyList<FirmamentDraftPoint> Points, IReadOnlyList<FirmamentSketchCurve> Guides,
    IReadOnlyList<FirmamentOutlineDiagnostic> Diagnostics, string ValidationLevel)
{
    public IReadOnlyList<FirmamentDraftFrame> Frames { get; init; } = [];
    public IReadOnlyList<FirmamentSketchShape> Shapes { get; init; } = [];
    public IReadOnlyList<FirmamentDraftSpan> Spans { get; init; } = [];
    /// <summary>Axis2 declarations in a layout: unbounded reference directions, never traced.</summary>
    public IReadOnlyList<FirmamentDraftAxis> Axes { get; init; } = [];
}
public sealed record FirmamentDraftAxis(string Id, string Name, string Owner, double[] Origin, double[] Direction);
public sealed record FirmamentDraftSpan(string Id, string Owner, string Reference, string From, string To, FirmamentSketchCurve Curve);
public sealed record FirmamentDraftFrame(string Owner, FirmamentSketchFrame Frame);
public sealed record FirmamentDraftTrace(string Reference, string? From = null, string? To = null,
    string? Alias = null, bool Reverse = false, bool Connect = false, bool WholeLoop = false);
public sealed record FirmamentDraftOperation(string Action, string? Parent = null, string? Name = null,
    string? Kind = null, string Plane = "XY", double X = 0, double Y = 0, double Length = 40,
    double Radius = 10, double Turn = 0, string? From = null, string? To = null,
    IReadOnlyList<FirmamentDraftTrace>? Trace = null, string? Field = null, double Value = 0,
    double Width = 60, double Height = 40, string? Reference = null, double InsetX = 8, double InsetY = 8,
    bool SharedInset = true, string? TargetProfile = null, double DirectionX = 1, double DirectionY = 0);

/// <summary>Bounded source transactions for construction, trace and extrusion. Open construction is
/// explicitly not an admitted part. All geometry comes from the existing Profile binding owner.</summary>
public static class FirmamentIntelliDraft
{
    public static FirmamentDraftConstruction Project(string source, string document)
    {
        var parsed = FirmamentSyntaxParser.Parse(source, document);
        var declarations = FirmamentLanguageOutlineService.Declarations(parsed);
        FirmamentDraftField Field(FieldSyntax f)
        {
            var unit = FirmamentSyntaxValues.TryMeasure(f.Value, "deg", out var value) ? "deg"
                : FirmamentSyntaxValues.TryMeasure(f.Value, "mm", out value) ? "mm" : null;
            return new(f.Name.Text, source.Substring(f.Value.Span.Start, f.Value.Span.Length), unit is null ? null : value,
                unit, unit is not null && FirmamentSketchService.Literal(f.Value));
        }
        var elements = declarations.Where(d => FirmamentSketchService.EditableShapes.Contains(d.Syntax.Type.Name.Text) || d.Syntax.Type.Name.Text is "Model" or "Struct" or "Static" or "Point2" or "Line2" or "Axis2" or "Path" or "Line" or "Arc" or "Close" or "Profile" or "Extrude")
            .Select(d => new FirmamentDraftElement(d.Id, d.Syntax.Name?.Text ?? d.Id, d.Syntax.Type.Name.Text, d.ParentId,
                new(d.Syntax.Span.Start, d.Syntax.Span.Length), (d.Syntax.Body?.Statements.OfType<FieldSyntax>() ?? [])
                .Select(Field).Concat(d.Syntax.Initializer is { } initial ? [new FirmamentDraftField("value", source.Substring(initial.Span.Start, initial.Span.Length),
                    FirmamentSyntaxValues.TryMeasure(initial, "mm", out var number) ? number : null, "mm", FirmamentSketchService.Literal(initial))] : []).ToArray())).ToArray();
        var diagnostics = parsed.Diagnostics.Where(d => d.Severity is "error" or "fatal")
            .Select(d => new FirmamentOutlineDiagnostic("error", d.Code, d.Message, new(d.SourceSpan.Start, d.SourceSpan.Length))).ToList();
        var binding = new FirmamentDomainBinding(source, document, "Model", new HashSet<string>());
        diagnostics.AddRange(binding.Diagnostics.Where(d => d.Severity is "error" or "fatal")
            .Select(d => new FirmamentOutlineDiagnostic("error", d.Code, d.Message,
                d.SourceSpan is { } s ? new(s.Start, s.Length) : null)));
        if (diagnostics.Count > 0) return new(elements, [], [], diagnostics, "Unresolved");
        var duplicate = elements.Where(e => FirmamentSketchService.EditableShapes.Contains(e.Kind) || e.Kind is "Point2" or "Line2" or "Axis2" or "Path")
            .GroupBy(e => e.Name).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            return new(elements, [], [], [new("error", "draft-guide-ambiguous", $"Construction preview requires an unambiguous guide name: '{duplicate.Key}'.", duplicate.First().Span)], "Unresolved");
        var bound = binding.InspectConstruction();
        foreach (var code in bound.Diagnostics)
            diagnostics.Add(new("error", code.Split(':')[0], $"Construction could not resolve: {code}. Check the named guide or Path step. Location is the enclosing Model; the resolver did not supply a more precise source span.", elements.FirstOrDefault(e => e.Kind == "Model")?.Span));
        var curves = bound.Guides.Select(g =>
        {
            var owner = elements.FirstOrDefault(e => e.Name == g.Key || g.Key.StartsWith(e.Name + ".", StringComparison.Ordinal) && (e.Kind == "Path" || FirmamentSketchService.EditableShapes.Contains(e.Kind)));
            var span = owner is null ? "unavailable: expanded construction" : $"{document}:{owner.Span.Start}:{owner.Span.Length}";
            return FirmamentSketchService.Curve(new(g.Key, g.Value,
                new("construction:" + g.Key, "construction:" + g.Key, span, "ConstructionGuide", "Local2D")));
        }).ToArray();
        var frames = new List<FirmamentDraftFrame>();
        foreach (var layout in elements.Where(e => e.Kind == "Struct"))
        {
            var frameDiagnostics = new List<string>();
            var frame = ProfileAuthoringParser.ResolveNamedConstructionPlane(binding.Source, layout.Name, frameDiagnostics);
            if (frame is not null) frames.Add(new(layout.Id, new(layout.Name,
                [frame.Origin.X, frame.Origin.Y, frame.Origin.Z], [frame.AxisX.X, frame.AxisX.Y, frame.AxisX.Z],
                [frame.AxisY.X, frame.AxisY.Y, frame.AxisY.Z], [frame.AxisZ.X, frame.AxisZ.Y, frame.AxisZ.Z])));
        }
        var spans = ProfileAuthoringParser.ConstructionSpans(bound.Points, bound.Guides).Select(s =>
        {
            // Rect2 sides, and named Line2 and Circle2 guides, are clickable spans between named points.
            var owner = elements.FirstOrDefault(e => s.Guide.StartsWith(e.Name + ".", StringComparison.Ordinal) && e.Kind == "Rect2"
                || s.Guide == e.Name && e.Kind is "Line2" or "Circle2");
            var id = s.Guide + ":" + s.From + ":" + s.To;
            var parentCurve = curves.Single(c => c.Name == s.Guide);
            return new FirmamentDraftSpan(id, owner?.Id ?? "", s.Guide, s.From, s.To,
                FirmamentSketchService.Curve(new(id, s.Geometry, parentCurve.Provenance! with { StableId = "construction-span:" + id })));
        }).Where(s => s.Owner.Length > 0).ToArray();
        return new(elements, bound.Points.Select(p => new FirmamentDraftPoint(p.Key, p.Value.X, p.Value.Y)).ToArray(),
            curves, diagnostics, diagnostics.Count == 0 ? "ConstructionResolvedNotTraced" : "Unresolved")
            { Frames = frames, Shapes = FirmamentSketchService.ConstructionShapes(source, document, "construction"), Spans = spans, Axes = Axes(elements) };
    }

    public static FirmamentLanguageEdit Edit(string source, string document, string revision, FirmamentDraftOperation request)
    {
        try { return EditOnce(source, document, revision, request); }
        catch (FirmamentLanguageEditException error) when (request is { Action: "trace", Trace: { Count: > 1 } picks }
            && picks.All(t => t.Connect) && error.Message.Contains("outer winding must be CounterClockwise", StringComparison.Ordinal))
        {
            // Edges picked on the canvas carry no intended direction. A clockwise pick order is
            // written in the counter-clockwise order an outer loop requires; the geometry is the same.
            return EditOnce(source, document, revision, request with { Trace = picks.Reverse().ToArray() });
        }
    }

    private static FirmamentLanguageEdit EditOnce(string source, string document, string revision, FirmamentDraftOperation request)
    {
        var view = Project(source, document);
        var operations = new List<FirmamentLanguageEditOperation>();
        FirmamentDraftElement Owner(params string[] kinds) => view.Elements.SingleOrDefault(e => e.Id == request.Parent && kinds.Contains(e.Kind))
            ?? throw Refuse("draft-owner-required", "Choose the owning Model, construction layout or Path.");
        string Name()
        {
            var name = FirmamentLanguageEditService.RequireIdentifier(request.Name, "construction name");
            if (view.Elements.Any(e => e.Name == name && e.Id != request.TargetProfile)) throw Refuse("draft-name-exists", $"'{name}' already exists. Choose a stable, unique name.");
            return name;
        }
        string Point(string? name) => view.Points.Any(p => p.Name == name) ? name! : throw Refuse("draft-point-required", $"Choose a resolved named point, instead of '{name}'.");
        void Insert(string parent, string text) => operations.Add(new("insertDeclaration") { Parent = parent, Source = text });
        var model = view.Elements.SingleOrDefault(e => e.Kind == "Model")
            ?? throw Refuse("draft-model-required", "IntelliDraft requires one authored Model.");
        switch (request.Action)
        {
            case "shape":
                var shapeOwner = Owner("Struct");
                var shapeName = Name();
                if (request.Kind is null || !FirmamentSketchService.EditableShapes.Contains(request.Kind))
                    throw Refuse("draft-shape-unsupported", "Choose Rect2, Circle2, Ellipse2, Slot2, RoundedRect2 or SmoothRoundedRect2.");
                var centerFields = $"center: [{Mm(request.X)}, {Mm(request.Y)}]";
                var shapeFields = request.Kind switch
                {
                    "Rect2" => $"size: [{Positive(request.Width)}mm, {Positive(request.Height)}mm]",
                    "Circle2" => $"radius: {Positive(request.Radius)}mm",
                    "Ellipse2" => $"axisLengths: [{Positive(request.Width)}mm, {Positive(request.Height)}mm]",
                    "Slot2" => $"length: {Positive(request.Width)}mm; width: {Positive(request.Height)}mm; rotation: {Number(request.Turn)}deg",
                    "RoundedRect2" => $"size: [{Positive(request.Width)}mm, {Positive(request.Height)}mm]; radius: {Positive(request.Radius)}mm",
                    "SmoothRoundedRect2" => $"size: [{Positive(request.Width)}mm, {Positive(request.Height)}mm]; cornerExtent: {Positive(request.Radius)}mm",
                    _ => throw Refuse("draft-shape-unsupported", "Choose a supported construction shape.")
                };
                Insert(shapeOwner.Id, $"{request.Kind} {shapeName} {{ {centerFields}; {shapeFields} }}");
                break;
            case "rectangle":
                var rectangleOwner = Owner("Struct");
                var rectangleName = Name();
                var usedNames = view.Elements.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
                string Parameter(string stem, string value, string parent)
                {
                    var fresh = stem; for (var i = 2; !usedNames.Add(fresh); i++) fresh = stem + i;
                    Insert(parent, $"Static {fresh}: Length = {value}"); return fresh;
                }
                string cx, cy, width, height;
                if (request.Reference is null)
                {
                    cx = Parameter(rectangleName + "CenterX", Mm(request.X), model.Id);
                    cy = Parameter(rectangleName + "CenterY", Mm(request.Y), model.Id);
                    width = Parameter(rectangleName + "Width", Positive(request.Width) + "mm", model.Id);
                    height = Parameter(rectangleName + "Height", Positive(request.Height) + "mm", model.Id);
                }
                else
                {
                    var reference = view.Shapes.SingleOrDefault(s => s.Id == request.Reference && s.Kind == "Rect2")
                        ?? throw Refuse("draft-rectangle-required", "Choose a retained Rect2 as the reference.");
                    var authored = FirmamentLanguageOutlineService.Declarations(FirmamentSyntaxParser.Parse(source)).Single(d => d.Id == reference.Id);
                    if (authored.ParentId != rectangleOwner.Id || reference.Controls.Any(c => c.Unit == "deg" && c.Value != 0))
                        throw Refuse("draft-inset-frame-unsupported", "Inset rectangles currently require an unrotated reference in the same Concept Struct.");
                    var ix = request.InsetX; var iy = request.SharedInset ? ix : request.InsetY;
                    var referenceWidth = reference.Controls.Single(c => c.Label == "Width").Value;
                    var referenceHeight = reference.Controls.Single(c => c.Label == "Height").Value;
                    if (!(ix > 0 && ix < referenceWidth && iy > 0 && iy < referenceHeight))
                        throw Refuse("draft-inset-invalid", "Insets must be positive and smaller than the reference width and height.");
                    string[] Share(string fieldName, string[] suffixes)
                    {
                        var field = authored.Syntax.Body!.Statements.OfType<FieldSyntax>().Single(f => FirmamentSyntaxFields.SameField(f.Name.Text, fieldName));
                        if (field.Value is not SequenceSyntax { Kind: SequenceKind.List } values || values.Items.Count != 2)
                            throw Refuse("draft-reference-expression-unsupported", "The reference rectangle needs an authored two-component center and size.");
                        var names = values.Items.Select((a, i) => a.Value is ReferenceSyntax ? source.Substring(a.Value.Span.Start, a.Value.Span.Length)
                            : Parameter(reference.Name + suffixes[i], source.Substring(a.Value.Span.Start, a.Value.Span.Length), model.Id)).ToArray();
                        operations.Add(new("setField") { Node = reference.Id, Field = field.Name.Text, Value = $"[{names[0]}, {names[1]}]" });
                        return names;
                    }
                    var center = Share("center", ["CenterX", "CenterY"]); var size = Share("size", ["Width", "Height"]);
                    var insetX = Parameter(rectangleName + (request.SharedInset ? "Wall" : "InsetX"), Mm(ix), model.Id);
                    var insetY = request.SharedInset ? insetX : Parameter(rectangleName + "InsetY", Mm(iy), model.Id);
                    cx = center[0] + " + " + insetX + " / 2"; cy = center[1] + " + " + insetY + " / 2";
                    width = size[0] + " - " + insetX; height = size[1] + " - " + insetY;
                }
                Insert(rectangleOwner.Id, $"Rect2 {rectangleName} {{ center: [{cx}, {cy}]; size: [{width}, {height}] }}");
                break;
            case "parameter":
                var parameter = Owner("Static");
                var parameterField = parameter.Fields.SingleOrDefault(f => f.Name == "value" && f.Editable && f.Number is not null)
                    ?? throw Refuse("draft-parameter-driven", "Choose a literal Length parameter. Expressions and typed tolerances retain their authoring inputs.");
                operations.Add(new("setValue") { Node = parameter.Id, Value = Mm(request.Value) });
                break;
            case "movePoint":
                var point = Owner("Point2");
                var syntax = FirmamentLanguageOutlineService.Declarations(FirmamentSyntaxParser.Parse(source)).Single(d => d.Id == point.Id).Syntax;
                var position = syntax.Body?.Statements.OfType<FieldSyntax>().SingleOrDefault(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Position"));
                if (position?.Value is not SequenceSyntax { Kind: SequenceKind.List } list || list.Items.Count != 2 || list.Items.Any(a => !FirmamentSketchService.Literal(a.Value)))
                    throw Refuse("draft-point-driven", "This point is driven by expressions. Edit its driving parameters; moving it will not replace those relationships.");
                operations.Add(new("setField") { Node = point.Id, Field = position.Name.Text, Value = $"[{Mm(request.X)}, {Mm(request.Y)}]" });
                break;
            case "dimension":
                var dimensionOwner = Owner("Line", "Arc", "Circle2", "Path", "Extrude");
                var field = dimensionOwner.Fields.SingleOrDefault(f => f.Name == request.Field && f.Editable)
                    ?? throw Refuse("draft-dimension-driven", "Choose an authored literal dimension. Expressions retain their upstream relationships.");
                operations.Add(new("setField") { Node = dimensionOwner.Id, Field = field.Name, Value = Number(request.Value) + field.Unit });
                break;
            case "layout":
                if (request.Plane is not ("XY" or "XZ" or "YZ")) throw Refuse("draft-plane-required", "Choose XY, XZ or YZ.");
                Insert(model.Id, $"Concept Struct {Name()} on {request.Plane} {{ }}");
                break;
            case "point":
                Insert(Owner("Struct").Id, $"Point2 {Name()} {{ position: [{Mm(request.X)}, {Mm(request.Y)}] }}");
                break;
            case "axis":
                if (!double.IsFinite(request.DirectionX) || !double.IsFinite(request.DirectionY) || Math.Abs(request.DirectionX) + Math.Abs(request.DirectionY) < 1e-12)
                    throw Refuse("draft-axis-direction", "Draw the axis with two different points.");
                Insert(Owner("Struct").Id, $"Axis2 {Name()} {{ origin: [{Mm(request.X)}, {Mm(request.Y)}]; direction: [{Number(request.DirectionX)}, {Number(request.DirectionY)}] }}");
                break;
            case "line":
                Insert(Owner("Struct").Id, $"Line2 {Name()} {{ from: {Point(request.From)}; to: {Point(request.To)} }}");
                break;
            case "circle":
                Insert(Owner("Struct").Id, $"Circle2 {Name()} {{ center: {Point(request.From)}; radius: {Positive(request.Radius)}mm }}");
                break;
            case "path":
                Insert(model.Id, $"Path {Name()} {{ start: Point2({Mm(request.X)}, {Mm(request.Y)}); heading: {Number(request.Turn)}deg }}");
                break;
            case "step":
                var path = Owner("Path");
                if (view.Elements.Any(e => e.Parent == path.Id && e.Kind == "Close"))
                    throw Refuse("draft-path-closed", "This Path is closed. Undo Close before appending a step.");
                var stepName = Name();
                Insert(path.Id, request.Kind switch
                {
                    "Line" => $"Line {stepName} {{ length: {Positive(request.Length)}mm; turn: {Number(request.Turn)}deg }}",
                    "Arc" => $"Arc {stepName} {{ radius: {Positive(request.Radius)}mm; turn: {Number(request.Turn)}deg }}",
                    "Close" => $"Close {stepName}",
                    _ => throw Refuse("draft-step-required", "Choose Line, Arc or Close.")
                });
                break;
            case "trace":
                var owner = Owner("Struct", "Path");
                var name = Name();
                string? after = null;
                if (request.TargetProfile is not null)
                {
                    var target = view.Elements.SingleOrDefault(e => e.Id == request.TargetProfile && e.Kind == "Profile" && e.Parent == model.Id && e.Name == name)
                        ?? throw Refuse("draft-profile-required", "Choose the authored Profile being replaced.");
                    after = view.Elements.LastOrDefault(e => e.Parent == model.Id && e.Span.Start < target.Span.Start)?.Id;
                    operations.Add(new("removeDeclaration") { Node = target.Id });
                }
                void Profile(string text) => operations.Add(new("insertDeclaration") { Parent = model.Id, Source = text, After = after, First = request.TargetProfile is not null && after is null });
                if (owner.Kind == "Path") Profile($"Profile {name} from {owner.Name};");
                else
                {
                    var entries = request.Trace ?? [];
                    if (entries.Count == 0) throw Refuse("draft-trace-empty", "Add guides in boundary order before tracing.");
                    (double X, double Y)? previous = null;
                    (double X, double Y)? At(string? name) => view.Points.Where(p => p.Name == name).Select(p => ((double X, double Y)?)(p.X, p.Y)).FirstOrDefault();
                    // The first picked span has no predecessor: run it toward the span picked after it.
                    if (entries is [{ Connect: true } first, { Connect: true } second, ..] && At(first.From) is { } a && At(first.To) is { } b
                        && At(second.From) is { } c && At(second.To) is { } d
                        && (ProfileAuthoringParser.SameConstructionPoint(a, c) || ProfileAuthoringParser.SameConstructionPoint(a, d))
                        && !ProfileAuthoringParser.SameConstructionPoint(b, c) && !ProfileAuthoringParser.SameConstructionPoint(b, d))
                        previous = b;
                    string Entry(FirmamentDraftTrace entry)
                    {
                        if (entry.WholeLoop)
                        {
                            if (entries.Count != 1 || !view.Shapes.Any(s => s.Name == entry.Reference))
                                throw Refuse("draft-whole-loop-required", "A complete shape outline must be the only entry in this outer trace.");
                            return entry.Reference + " |> TraceLoop";
                        }
                        if (!view.Guides.Any(g => g.Name == entry.Reference)) throw Refuse("draft-guide-required", $"Unresolved guide '{entry.Reference}'.");
                        if (entry.Connect)
                        {
                            var start = view.Points.SingleOrDefault(p => p.Name == entry.From); var end = view.Points.SingleOrDefault(p => p.Name == entry.To);
                            if (start is null || end is null) throw Refuse("draft-point-required", "Select a span with named construction endpoints.");
                            // A circle span is the counter-clockwise arc from its first to its second point.
                            // Joining it backwards keeps that arc and traces it clockwise.
                            var circle = view.Guides.Any(g => g.Name == entry.Reference && g.Kind == "Circle");
                            if (previous is { } p && ProfileAuthoringParser.SameConstructionPoint(p, (end.X, end.Y)))
                            { (start, end) = (end, start); if (circle) entry = entry with { Reverse = !entry.Reverse }; }
                            if (previous is { } current && !ProfileAuthoringParser.SameConstructionPoint(current, (start.X, start.Y)))
                                throw Refuse("draft-trace-disconnected", $"'{entry.Reference}' does not join the preceding span. Select a connected span or reorder the trace.");
                            if (entry.Alias is null && entries.Count(e => e.Reference == entry.Reference) > 1)
                                throw Refuse("draft-edge-name-required", $"Give the separate spans of '{entry.Reference}' distinct edge names.");
                            entry = entry with { From = start.Name, To = end.Name, Alias = entry.Alias ?? entry.Reference.Replace('.', '_') };
                        }
                        previous = entry.To is null ? null : view.Points.Where(p => p.Name == entry.To).Select(p => ((double X, double Y)?)(p.X, p.Y)).SingleOrDefault();
                        var text = entry.Reference;
                        if (entry.From is not null || entry.To is not null) text += $" from {Point(entry.From)} to {Point(entry.To)}";
                        if (entry.Alias is not null) text += " as " + FirmamentLanguageEditService.RequireIdentifier(entry.Alias, "edge alias");
                        return (entry.Reverse ? "Reverse " : "") + text; // prefix form, as in Loop Inner { Reverse a.Bottom |> … }
                    }
                    Profile($"Profile {name} using {owner.Name} {{ Loop Outer {{ {string.Join(" |> ", entries.Select(Entry))}{(entries.Count == 1 && entries[0].WholeLoop ? "" : " |> Close")} }} }}");
                }
                break;
            case "extrude":
                var profile = Owner("Profile");
                Insert(model.Id, $"Extrude {Name()} {{ profile: {profile.Name}; from: 0mm; to: {Positive(request.Length)}mm }}");
                break;
            default: throw Refuse("draft-action-unknown", $"Unknown IntelliDraft operation '{request.Action}'.");
        }
        var result = FirmamentLanguageEditService.Apply(source, document, revision, operations);
        var candidate = Project(result.Text, document);
        if (candidate.Diagnostics.FirstOrDefault(d => d.Severity is "error" or "fatal") is { } diagnostic)
            throw Refuse(diagnostic.Code, diagnostic.Message);
        if (request.Action == "trace")
        {
            var projected = FirmamentSketchService.Project(result.Text, document, revision);
            if (projected.Diagnostics.FirstOrDefault(d => d.Severity is "error" or "fatal") is { } error) throw Refuse(error.Code, error.Message);
            if (projected.ValidationLevel != "ProfileResolvedNotBuilt") throw Refuse("draft-trace-unresolved", "The trace did not resolve to a closed Profile.");
        }
        return result;
    }
    private static IReadOnlyList<FirmamentDraftAxis> Axes(IReadOnlyList<FirmamentDraftElement> elements)
    {
        static double[]? Pair(FirmamentDraftField? field, string unit)
        {
            var parts = field?.Value.Trim().TrimStart('[').TrimEnd(']').Split(',');
            if (parts is not { Length: 2 }) return null;
            var result = new double[2];
            for (var i = 0; i < 2; i++)
                if (!FirmamentNumericEvaluation.TryEvaluateScalar(parts[i].Trim(), out result[i], out var u) || u != unit) return null;
            return result;
        }
        return elements.Where(e => e.Kind == "Axis2").Select(e =>
        {
            var origin = Pair(e.Fields.FirstOrDefault(f => FirmamentSyntaxFields.SameField(f.Name, "Origin")), "mm");
            var direction = Pair(e.Fields.FirstOrDefault(f => FirmamentSyntaxFields.SameField(f.Name, "Direction")), "");
            if (origin is null || direction is null || Math.Abs(direction[0]) + Math.Abs(direction[1]) < 1e-12) return null;
            var length = Math.Sqrt(direction[0] * direction[0] + direction[1] * direction[1]);
            return new FirmamentDraftAxis(e.Id, e.Name, e.Parent ?? "", origin, [direction[0] / length, direction[1] / length]);
        }).OfType<FirmamentDraftAxis>().ToArray();
    }
    private static string Number(double value) => double.IsFinite(value) ? value.ToString("R", CultureInfo.InvariantCulture)
        : throw Refuse("draft-number-invalid", "Use a finite dimension or angle.");
    private static string Positive(double value) => value > 0 ? Number(value) : throw Refuse("draft-size-invalid", "Use a positive dimension.");
    private static string Mm(double value) => Number(value) + "mm";
    private static FirmamentLanguageEditException Refuse(string code, string message) => new(code, message);
}
