using System.Diagnostics;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Core.Math;
using Aetheris.Geometry;
using Aetheris.Surfacing;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentPanelPerformance(double ParseBindMilliseconds, double PanelConstructionMilliseconds);
public sealed record FirmamentPanelCompilation(string ModelName, IReadOnlyList<PanelIr> Panels,
    IReadOnlyList<string> Diagnostics, FirmamentPanelPerformance Performance,
    IReadOnlyList<ConceptIrTemplateInstantiation>? TemplateInstantiations = null)
{
    public bool IsSuccess => Panels.Count > 0 && Diagnostics.Count == 0;
}

/// <summary>Bounded Surfacing M1 bridge for ordinary canonical Firmament Panel declarations.</summary>
public static class FirmamentPanelCompiler
{
    public const string InvalidPanel = "firmament-panel-invalid";
    public const string UnsupportedSurface = "firmament-panel-surface-unsupported";
    public const string MissingSurface = "firmament-panel-surface-missing";

    public static FirmamentPanelCompilation Compile(string source,
        IReadOnlyList<ConceptIrTemplateInstantiation>? templateInstantiations = null) =>
        Bind(FirmamentSyntaxParser.Parse(source), templateInstantiations);

    internal static FirmamentPanelCompilation Bind(SyntaxParseResult syntax,
        IReadOnlyList<ConceptIrTemplateInstantiation>? templateInstantiations = null)
    {
        var parseWatch = Stopwatch.StartNew();
        var diagnostics = syntax.Diagnostics.Select(d => d.Code + ": " + d.Message).ToList();
        var roots = syntax.Document.Statements.OfType<DeclarationSyntax>().Where(d => !d.Modifiers.Any(m => m.Name.Text is "Concept" or "Template")).ToArray();
        var model = roots.FirstOrDefault(d => d.Type.Name.Text == "Model") ?? roots.FirstOrDefault(d => d.Type.Name.Text == "Struct");
        if (model is null) diagnostics.Add(FirmamentV2Parser.MissingModel);
        var units = model?.Body?.Statements.OfType<FieldSyntax>().Where(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Units")).ToArray() ?? [];
        if (units.Length == 0 || units.Any(f => FirmamentSyntaxValues.Reference(f.Value) != "mm")) diagnostics.Add(FirmamentV2Parser.MissingUnits);
        var declarations = FirmamentV2Parser.MechanicalDeclarations(syntax.Document.Statements).Where(d => d.Type.Name.Text == "Panel").ToArray();
        if (declarations.Length == 0) diagnostics.Add("firmament-panel-missing");
        CloseScope(syntax.Document.Statements, diagnostics);
        foreach (var duplicate in declarations.GroupBy(d => d.Name?.Text).Where(g => g.Count() > 1))
            diagnostics.Add($"{InvalidPanel}:{duplicate.Key}:duplicate-name");
        parseWatch.Stop();
        var constructionWatch = Stopwatch.StartNew(); var panels = new List<PanelIr>();
        foreach (var declaration in declarations)
        {
            try
            {
                var fields = new FirmamentSyntaxFields(declaration, diagnostics);
                var panel = CompilePanel(fields, diagnostics);
                fields.Complete();
                if (panel is not null) panels.Add(panel);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
            { diagnostics.Add($"{InvalidPanel}:{declaration.Name?.Text}:{exception.Message}"); }
        }
        constructionWatch.Stop();
        return new(model?.Name?.Text ?? "InvalidPanelModel", panels,
            diagnostics.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            new(parseWatch.Elapsed.TotalMilliseconds, constructionWatch.Elapsed.TotalMilliseconds), templateInstantiations);
    }

    private static void CloseScope(IEnumerable<StatementSyntax> statements, List<string> diagnostics)
    {
        foreach (var statement in statements)
        {
            if (statement is DirectiveSyntax) continue;
            if (statement is FieldSyntax f && FirmamentSyntaxFields.SameField(f.Name.Text, "Units")) continue;
            if (statement is DeclarationSyntax d)
            {
                if (d.Type.Name.Text == "Panel") continue;
                // Uninstantiated templates are definitions, never material panels.
                if (d.Modifiers.Any(m => m.Name.Text is "Concept" or "Template")) continue;
                if (d.Type.Name.Text is "Model" or "Struct" && d.Body is not null)
                {
                    if (d.Type.Arguments.Count > 0 || d.Parameters.Count > 0 || d.Clauses.Count > 0 || d.Annotation is not null || d.Initializer is not null)
                        diagnostics.Add($"firmament-v2-domain-root-header-unsupported:{d.Type.Name.Text}:{d.Name?.Text}");
                    CloseScope(d.Body.Statements, diagnostics); continue;
                }
            }
            diagnostics.Add($"{FirmamentV2Parser.CanonicalDeclarationUnknown}:Panel:{(statement as DeclarationSyntax)?.Type.Name.Text ?? (statement as FieldSyntax)?.Name.Text ?? "expression"}");
        }
    }

    private static PanelIr? CompilePanel(FirmamentSyntaxFields fields, List<string> diagnostics)
    {
        var declaration = fields.Declaration;
        var name = declaration.Name?.Text ?? "";
        if (declaration.Type.Arguments.Count > 0 || declaration.Modifiers.Count > 0 || name.Length == 0)
            diagnostics.Add($"{InvalidPanel}:{name}:header");
        if (fields.Take("Surface") is not ObjectSyntax surface)
        { diagnostics.Add($"{MissingSurface}:{name}"); return null; }
        var values = FirmamentSyntaxFields.Object(surface, name, diagnostics);
        var orientationExpression = fields.Take("Orientation");
        var orientationText = orientationExpression is null ? "Front" : FirmamentSyntaxValues.Reference(orientationExpression);
        if (orientationText is not ("Front" or "Back")) fields.Invalid("Orientation");
        var orientation = orientationText == "Back"
            ? new PanelOrientation(PanelNormalOrientation.ReversedSupportNormal, PanelMaterialSide.Back) : PanelOrientation.Front;
        var thicknessExpression = fields.Take("Thickness");
        double? thickness = thicknessExpression is null ? null : Measure(thicknessExpression, "mm", "Thickness");
        var materialExpression = fields.Take("Material");
        var material = FirmamentSyntaxValues.Reference(materialExpression);
        if (materialExpression is not null && material is null) fields.Invalid("Material");
        double Length(string field) => Required(values, field, "mm");
        PanelResult result = values.Declaration.Type.Name.Text switch
        {
            "HyperbolicParaboloid" => PanelFactory.FromParametric(MathematicalSurfaces.HyperbolicParaboloid(name, Length("Width")/2, Length("Depth")/2, Length("Rise")), orientation, thickness, material),
            "ParabolicCylinder" => PanelFactory.FromParametric(MathematicalSurfaces.ParabolicCylinder(name, Length("Width")/2, Length("Depth")/2, Length("Rise")), orientation, thickness, material),
            "EllipticParaboloid" => PanelFactory.FromParametric(MathematicalSurfaces.EllipticParaboloid(name, Length("Width")/2, Length("Depth")/2, Length("Rise")), orientation, thickness, material),
            "Helicoid" => PanelFactory.FromParametric(MathematicalSurfaces.Helicoid(name, Length("Radius"), Length("Rise"), values.Number("Turns", "", 1)), orientation, thickness, material),
            "ParametricSurface" => PanelFactory.FromParametric(Parametric(name, values), orientation, thickness, material),
            "RuledSurface" => PanelFactory.FromRuled(Ruled(name, values, RuledConstructionKind.RuledSurface, diagnostics), orientation, thickness, material),
            "RuledTransition" => PanelFactory.FromRuled(Ruled(name, values, RuledConstructionKind.RuledTransition, diagnostics), orientation, thickness, material),
            "BoundaryPatch" => PanelFactory.FromBoundaryPatch(Boundary(name, values, diagnostics), orientation, thickness, material),
            "SectionSurface" => PanelFactory.FromSectionSurface(Sections(name, values, diagnostics), orientation, thickness, material),
            _ => new(null, [new(UnsupportedSurface, $"Surface kind '{values.Declaration.Type.Name.Text}' is not admitted.")])
        };
        values.Complete();
        foreach (var diagnostic in result.Diagnostics) diagnostics.Add($"{diagnostic.Code}:{name}:{diagnostic.Message}");
        return result.Panel;
    }

    private static ParametricSurfaceIr Parametric(string name, FirmamentSyntaxFields values)
    {
        var u = Vector(values, "DomainU", "", 2); var v = Vector(values, "DomainV", "", 2);
        return new(name, SurfaceConstructionKind.ParametricSurface, new(new(u[0], u[1]), new(v[0], v[1])),
            new(Expression(values.Take("X")), Expression(values.Take("Y")), Expression(values.Take("Z"))), $"firmament:Panel:{name}:ParametricSurface");
    }

    private static RuledSurfaceIr Ruled(string name, FirmamentSyntaxFields values, RuledConstructionKind kind, List<string> diagnostics)
    {
        var a = NamedBoundary(values, "BoundaryA", diagnostics); var b = NamedBoundary(values, "BoundaryB", diagnostics);
        return new(name, kind, a, b, new(a.StableId, $"firmament:Panel:{name}", "BoundaryA"), new(b.StableId, $"firmament:Panel:{name}", "BoundaryB"));
    }

    private static BoundaryPatchIr Boundary(string name, FirmamentSyntaxFields values, List<string> diagnostics)
    {
        var south = NamedBoundary(values, "South", diagnostics); var north = NamedBoundary(values, "North", diagnostics);
        var west = NamedBoundary(values, "West", diagnostics); var east = NamedBoundary(values, "East", diagnostics);
        return new(name, south, north, west, east, new[] { south, north, west, east }
            .Select(item => new BoundaryProvenance(item.StableId, $"firmament:Panel:{name}", item.StableId)).ToArray());
    }

    private static SectionSurfaceIr Sections(string name, FirmamentSyntaxFields fields, List<string> diagnostics)
    {
        if (fields.Take("Sections") is not SequenceSyntax { Kind: SequenceKind.List } list)
            throw new ArgumentException("SectionSurface requires Sections: [ ... ].");
        var sections = new List<RuledBoundary>();
        foreach (var item in list.Items)
        {
            if (item.Name is not null || item.Default is not null || item.Value is not ObjectSyntax { Name: not null } value)
                throw new ArgumentException("Each explicit section requires a named Line, Arc or Circle object.");
            sections.Add(BoundaryValue($"{name}:{value.Name.Text}", value, diagnostics));
        }
        if (sections.Count < 2) throw new ArgumentException("SectionSurface requires at least two explicit sections.");
        return new(name, sections, sections.Select((item, index) => new BoundaryProvenance(item.StableId, $"firmament:Panel:{name}", $"section-{index}")).ToArray());
    }

    private static RuledBoundary NamedBoundary(FirmamentSyntaxFields fields, string name, List<string> diagnostics) =>
        fields.Take(name) is ObjectSyntax value ? BoundaryValue(name, value, diagnostics)
            : throw new ArgumentException($"Missing {name} boundary.");

    private static RuledBoundary BoundaryValue(string id, ObjectSyntax value, List<string> diagnostics)
    {
        var fields = FirmamentSyntaxFields.Object(value, id, diagnostics);
        Point3D Point(string field) { var v = Vector(fields, field, "mm", 3); return new(v[0], v[1], v[2]); }
        Direction3D Direction(string field) { var v = Vector(fields, field, "", 3); return Direction3D.Create(new(v[0], v[1], v[2])); }
        RuledBoundary result = fields.Declaration.Type.Name.Text switch
        {
            "Line" => new RuledBoundary.Line(id, Point("Start"), Point("End")),
            "Circle" => new RuledBoundary.Circle(id, Point("Center"), Direction("Normal"), Required(fields, "Radius", "mm"), Direction("ReferenceAxis")),
            "Arc" => new RuledBoundary.Arc(id, Point("Center"), Direction("Normal"), Required(fields, "Radius", "mm"), Direction("ReferenceAxis"), Angle(fields, "StartAngle"), Angle(fields, "SweepAngle")),
            _ => throw new NotSupportedException($"Boundary kind '{fields.Declaration.Type.Name.Text}' is not admitted.")
        };
        fields.Complete(); return result;
    }

    private static double[] Vector(FirmamentSyntaxFields fields, string name, string unit, int count) =>
        fields.Vector(name, unit, out var values) && values.Length == count ? values
            : throw new ArgumentException($"Field '{name}' requires {count} { (unit.Length == 0 ? "dimensionless" : unit) } values.");
    private static double Required(FirmamentSyntaxFields fields, string name, string unit) => Measure(fields.Take(name), unit, name);
    private static double Measure(ExpressionSyntax? expression, string unit, string name) =>
        FirmamentSyntaxValues.TryMeasure(expression, unit, out var value) ? value : throw new ArgumentException($"Field '{name}' requires a finite {unit} measure.");
    private static double Angle(FirmamentSyntaxFields fields, string name)
    {
        var value = fields.Take(name);
        return FirmamentSyntaxValues.TryMeasure(value, "deg", out var degrees) ? degrees * Math.PI / 180d : Measure(value, "rad", name);
    }

    // The surface expression algebra still owns dimensional and mathematical admission.
    // The shared parser supplies syntax; there is no second character expression parser.
    private static SurfaceScalarExpression Expression(ExpressionSyntax? value) => value switch
    {
        ReferenceSyntax { Name.Text: "u" } => SurfaceExpression.U,
        ReferenceSyntax { Name.Text: "v" } => SurfaceExpression.V,
        LiteralSyntax { Kind: LiteralKind.Number } => SurfaceExpression.Number(Measure(value, "", "expression")),
        QuantitySyntax => SurfaceExpression.Length(Measure(value, "mm", "expression")),
        SequenceSyntax { Kind: SequenceKind.Tuple, Items.Count: 1 } group => Expression(group.Items[0].Value),
        UnarySyntax { Operator: UnaryOperator.Positive } unary => Expression(unary.Operand),
        UnarySyntax { Operator: UnaryOperator.Negative } unary => SurfaceExpression.Multiply(SurfaceExpression.Number(-1), Expression(unary.Operand)),
        BinarySyntax { Operator: BinaryOperator.Add } binary => SurfaceExpression.Add(Expression(binary.Left), Expression(binary.Right)),
        BinarySyntax { Operator: BinaryOperator.Subtract } binary => SurfaceExpression.Subtract(Expression(binary.Left), Expression(binary.Right)),
        BinarySyntax { Operator: BinaryOperator.Multiply } binary => SurfaceExpression.Multiply(Expression(binary.Left), Expression(binary.Right)),
        BinarySyntax { Operator: BinaryOperator.Divide } binary => SurfaceExpression.Divide(Expression(binary.Left), Expression(binary.Right)),
        BinarySyntax { Operator: BinaryOperator.Power } binary when FirmamentSyntaxValues.TryMeasure(binary.Right, "", out var exponent)
            && exponent == Math.Truncate(exponent) && exponent is >= int.MinValue and <= int.MaxValue => SurfaceExpression.Power(Expression(binary.Left), (int)exponent),
        CallSyntax { Target: ReferenceSyntax { Name.Text: "sin" or "cos" } function, Arguments.Count: 1 } call
            when call.Arguments[0].Name is null && call.Arguments[0].Default is null =>
                function.Name.Text == "sin" ? SurfaceExpression.Sin(Expression(call.Arguments[0].Value)) : SurfaceExpression.Cos(Expression(call.Arguments[0].Value)),
        _ => throw new ArgumentException("Unsupported or missing parametric surface expression.")
    };
}
