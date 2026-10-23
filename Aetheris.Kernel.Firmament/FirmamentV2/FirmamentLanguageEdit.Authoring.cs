using System.Globalization;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record FirmamentPlacementControls(double[] TranslationMm, string Axis, double AngleDegrees, string? ReadOnlyReason = null);

// Authoring conveniences share the structural writer and parser. They do not
// admit geometry: callers must preview/build the resulting source before commit.
public static partial class FirmamentLanguageEditService
{
    internal static FirmamentPlacementControls PlacementControls(DeclarationSyntax placement)
    {
        FirmamentPlacementControls ReadOnly() => new([0, 0, 0], "Z", 0, "This placement is driven by a frame, expression or general matrix. Its source remains authoritative; use a semantic mate or edit its defining frame.");
        bool Literal(ExpressionSyntax e) => e switch { LiteralSyntax => true, QuantitySyntax q => Literal(q.Value), UnarySyntax u => Literal(u.Operand), _ => false };
        if (placement.Initializer is SequenceSyntax sequence && sequence.Items.All(i => Literal(i.Value))
            && FirmamentSyntaxValues.Vector(sequence, "", out var m) && m.Length == 16)
        {
            var identity = new double[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, m[12], m[13], m[14], 1 };
            return m.SequenceEqual(identity) ? new([m[12], m[13], m[14]], "Z", 0) : ReadOnly();
        }
        if (placement.Initializer is not null and not ObjectSyntax || placement.Clauses.Count > 0 || placement.Type.Arguments.Count > 0
            || BodyOf(placement)?.Statements.Any(s => s is not FieldSyntax) == true) return ReadOnly();
        var groups = Fields(placement).GroupBy(f => f.Name.Text, StringComparer.OrdinalIgnoreCase).ToArray();
        if (groups.Any(g => g.Count() != 1)) return ReadOnly();
        var fields = groups.ToDictionary(g => g.Key, g => g.Single(), StringComparer.OrdinalIgnoreCase);
        if (fields.Keys.Any(k => !new[] { "from", "to", "translateLocal", "rotateLocal" }.Contains(k, StringComparer.OrdinalIgnoreCase))
            || fields.TryGetValue("from", out var from) && FirmamentSyntaxValues.Reference(from.Value) != "Origin"
            || fields.TryGetValue("to", out var to) && FirmamentSyntaxValues.Reference(to.Value) != "Parent") return ReadOnly();
        double[] translation = [0, 0, 0]; string axis = "Z"; double angle = 0;
        if (fields.TryGetValue("translateLocal", out var move))
        {
            if (move.Value is not SequenceSyntax s || s.Items.Any(i => !Literal(i.Value))
                || !FirmamentSyntaxValues.Vector(move.Value, "mm", out translation, "mm") || translation.Length != 3) return ReadOnly();
        }
        if (fields.TryGetValue("rotateLocal", out var rotate))
        {
            if (rotate.Value is not ObjectSyntax obj) return ReadOnly();
            var rotation = obj.Body.Statements.OfType<FieldSyntax>().ToArray();
            if (rotation.Length != 2 || obj.Body.Statements.Count != 2) return ReadOnly();
            axis = FirmamentSyntaxValues.Reference(rotation.FirstOrDefault(f => f.Name.Text.Equals("axis", StringComparison.OrdinalIgnoreCase))?.Value) ?? "";
            var angleValue = rotation.FirstOrDefault(f => f.Name.Text.Equals("angle", StringComparison.OrdinalIgnoreCase))?.Value;
            if (axis is not ("X" or "Y" or "Z") || angleValue is null || !Literal(angleValue)
                || !FirmamentSyntaxValues.TryMeasure(angleValue, "deg", out angle)) return ReadOnly();
        }
        return new(translation, axis, angle);
    }

    private static (string, string?) ReplaceDeclaration(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        var (id, declaration) = Find(parse, op.Node);
        if (declaration.Name is null || FirmamentLanguageOutlineService.NameIsUse(declaration))
            throw new FirmamentLanguageEditException("edit-replace-owner", "Replace a named definition, not an occurrence or a Modify target.");
        if (op.Source is null && op.Declaration is null)
            throw new FirmamentLanguageEditException("edit-input-required", "Replacement needs a declaration.");
        var indent = Indent(text, declaration.Span.Start);
        var snippet = op.Declaration is { } spec
            ? Write(spec with { Name = declaration.Name.Text }, indent, Unit(text))
            : FromSource(op.Source!, declaration.Name.Text, indent);
        CheckSnippet(snippet);
        // The statement span starts at its first token, after the existing indent.
        var next = Replace(text, declaration.Span.Start, declaration.Span.Length, snippet[indent.Length..]);
        var created = FirmamentLanguageOutlineService.Declarations(Parse(next)).First(d => d.Syntax.Span.Start == declaration.Span.Start);
        return (next, created.Id);
    }

    private static (string, string?) ExtrudeProfile(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        var (id, profile) = Find(parse, op.Node);
        if (profile.Type.Name.Text != "Profile" || profile.Name is null)
            throw new FirmamentLanguageEditException("edit-profile-required", "Select an authored Profile to extrude.");
        var declarations = FirmamentLanguageOutlineService.Declarations(parse);
        var parent = declarations.First(d => d.Id == id).ParentId;
        var height = RequireValue(op.Value, "to");
        var bodies = Siblings(declarations, parent).Where(d => IsAuthoringBody(d.Syntax)).ToArray();
        if (bodies.Length > 0)
        {
            var existing = bodies.Length == 1 && bodies[0].Syntax.Type.Name.Text == "Extrude" ? bodies[0] : default;
            if (existing.Syntax is null || !Fields(existing.Syntax).Any(f => f.Name.Text.Equals("profile", StringComparison.OrdinalIgnoreCase)
                    && Slice(text, f.Value.Span) == profile.Name.Text))
                throw new FirmamentLanguageEditException("edit-body-occupied", "This container already has a different body. Replace its base explicitly or use another Part.");
            var to = Fields(existing.Syntax).FirstOrDefault(f => f.Name.Text.Equals("to", StringComparison.OrdinalIgnoreCase));
            return SetField(text, parse, new("setField") { Node = existing.Id, Field = to?.Name.Text ?? "to", Value = height });
        }
        return InsertDeclaration(text, parse, new("insertDeclaration") { Parent = parent, Name = op.Name ?? "Body",
            Source = $"Extrude Body {{ profile: {profile.Name.Text}; from: 0mm; to: {height}; }}" });
    }

    private static (string, string?) PocketProfile(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        if (op.Node is null && op.Declaration is { } shape)
        {
            var (targetId, target) = Find(parse, op.Parent);
            if (shape.Kind is not ("Circle2" or "Rect2"))
                throw new FirmamentLanguageEditException("edit-pocket-shape", "Choose Circle2 or Rect2, or an existing authored Profile.");
            var parent = FirmamentLanguageOutlineService.Declarations(parse).First(d => d.Id == targetId).ParentId;
            var stem = RequireIdentifier(op.Name ?? "Recess", "pocket name");
            var taken = FirmamentLanguageOutlineService.Declarations(parse).Select(d => d.Syntax.Name?.Text).ToHashSet();
            if (new[] { stem, stem + "Outline", stem + "Profile" }.Any(taken.Contains))
                throw new FirmamentLanguageEditException("edit-name-taken", "Choose a unique pocket name.");
            var (shaped, _) = InsertDeclaration(text, parse, new("insertDeclaration") { Parent = parent,
                Declaration = shape with { Name = stem + "Outline" } });
            var (profiled, profileNode) = InsertDeclaration(shaped, Parse(shaped), new("insertDeclaration") { Parent = parent,
                Source = $"Profile {stem}Profile {{ Loop Outer {{ {stem}Outline |> TraceLoop }} }}" });
            return PocketProfile(profiled, Parse(profiled), op with { Node = profileNode, Declaration = null });
        }
        var (profileId, profile) = Find(parse, op.Node);
        var (bodyId, body) = Find(parse, op.Parent);
        if (profile.Type.Name.Text != "Profile" || profile.Name is null)
            throw new FirmamentLanguageEditException("edit-profile-required", "Select an authored closed Profile for the pocket.");
        var declarations = FirmamentLanguageOutlineService.Declarations(parse);
        if (declarations.First(d => d.Id == profileId).ParentId != declarations.First(d => d.Id == bodyId).ParentId)
            throw new FirmamentLanguageEditException("edit-profile-scope", "The pocket Profile and body must belong to the same container.");
        var pocket = $"Pocket {RequireIdentifier(op.Name ?? "Recess", "pocket name")} {{ on: Top; profile: {profile.Name.Text}; depth: {RequireValue(op.Value, "depth")}; }}";
        if (body.Type.Name.Text == "Compose")
            return InsertDeclaration(text, parse, new("insertDeclaration") { Parent = bodyId, Source = pocket });
        if (body.Clauses.Count > 0 || BodyOf(body)?.Statements.Any(s => s is not FieldSyntax) == true)
            throw new FirmamentLanguageEditException("edit-pocket-base-fields", "This base has additional authored clauses or statements. Convert it to Compose explicitly so those requirements are preserved.");
        if (body.Type.Name.Text == "Box" && Fields(body).ToArray() is [var size]
            && size.Name.Text.Equals("size", StringComparison.OrdinalIgnoreCase) && size.Value is SequenceSyntax { Items.Count: 3 } dimensions)
        {
            var container = declarations.First(d => d.Id == bodyId).ParentId;
            var stem = body.Name!.Text + "Stock";
            if (declarations.Any(d => d.Syntax.Name?.Text is { } n && (n == stem + "Outline" || n == stem + "Profile")))
                throw new FirmamentLanguageEditException("edit-name-taken", "The generated stock-profile names already exist; use an explicit profile-based body.");
            var (withOutline, _) = InsertDeclaration(text, parse, new("insertDeclaration") { Parent = container,
                Source = $"Rect2 {stem}Outline {{ center: [0mm,0mm]; size: [{Slice(text, dimensions.Items[0].Value.Span)}, {Slice(text, dimensions.Items[1].Value.Span)}]; }}" });
            var (withProfile, _) = InsertDeclaration(withOutline, Parse(withOutline), new("insertDeclaration") { Parent = container,
                Source = $"Profile {stem}Profile {{ Loop Outer {{ {stem}Outline |> TraceLoop }} }}" });
            return ReplaceDeclaration(withProfile, Parse(withProfile), new("replaceDeclaration") { Node = bodyId,
                Source = $"Compose {body.Name.Text} {{\n    Base Stock {{ profile: {stem}Profile; from: 0mm; to: {Slice(text, dimensions.Items[2].Value.Span)}; role: Stock; }}\n    {pocket}\n}}" });
        }
        if (body.Type.Name.Text != "Extrude" || body.Name is null)
            throw new FirmamentLanguageEditException("edit-pocket-base", "A pocket requires a profile-based Extrude or Compose. Choose a sketch-based part; primitive bodies keep their existing support limits.");
        var fields = Fields(body).ToArray();
        if (fields.Any(f => !new[] { "profile", "from", "to" }.Contains(f.Name.Text, StringComparer.OrdinalIgnoreCase)))
            throw new FirmamentLanguageEditException("edit-pocket-base-fields", "This Extrude has additional authored fields. Convert it to Compose explicitly so those requirements are preserved.");
        var stock = string.Join(" ", fields.Select(f => $"{f.Name.Text}: {Slice(text, f.Value.Span)};"));
        return ReplaceDeclaration(text, parse, new("replaceDeclaration") { Node = bodyId,
            Source = $"Compose {body.Name.Text} {{\n    Base Stock {{ {stock} role: Stock; }}\n    {pocket}\n}}" });
    }

    private static (string, string?) PatternFeature(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        var (id, seed) = Find(parse, op.Node);
        var grid = op.Grid ?? throw new FirmamentLanguageEditException("edit-input-required", "Specify rows, columns and grid pitch.");
        if (grid.Columns < 1 || grid.Rows < 1 || (long)grid.Columns * grid.Rows is < 2 or > 1024
            || !double.IsFinite(grid.PitchXmm) || !double.IsFinite(grid.PitchYmm)
            || grid.Columns > 1 && grid.PitchXmm == 0 || grid.Rows > 1 && grid.PitchYmm == 0)
            throw new FirmamentLanguageEditException("edit-grid-invalid", "Use 2–1024 total instances and finite nonzero pitch along each repeated direction.");
        var declarations = FirmamentLanguageOutlineService.Declarations(parse);
        var modifyId = declarations.First(d => d.Id == id).ParentId;
        var modify = Find(parse, modifyId).Syntax;
        if (seed.Type.Name.Text != "Hole" || modify.Type.Name.Text != "Modify" || seed.Name is null || seed.Body is null || seed.Clauses.Count > 0 || seed.Initializer is not null
            || seed.Body.Statements.Any(s => s is not FieldSyntax))
            throw new FirmamentLanguageEditException("edit-pattern-seed", "Select a field-based Hole in Modify. Other feature families retain their existing Pattern authoring path.");
        var fields = Fields(seed).ToArray();
        var center = fields.FirstOrDefault(f => f.Name.Text.Equals("center", StringComparison.OrdinalIgnoreCase));
        if (center?.Value is not CallSyntax { Target: ReferenceSyntax { Name.Text: "Point2" }, Arguments.Count: 2 } point)
            throw new FirmamentLanguageEditException("edit-pattern-center", "This grid action needs an authored Point2(x,y) center. It preserves expressions inside those coordinates; other center constructions require an explicit Feature/Set Pattern.");
        var container = declarations.First(d => d.Id == modifyId).ParentId;
        var name = RequireIdentifier(op.Name ?? "Copies", "Pattern name");
        var taken = declarations.Select(d => d.Syntax.Name?.Text).ToHashSet(StringComparer.Ordinal);
        if (new[] { name, name + "Points", name + "Recipe" }.Any(taken.Contains))
            throw new FirmamentLanguageEditException("edit-name-taken", $"Choose a different Pattern name; {name} or one of its supporting declarations already exists.");
        var x = Slice(text, point.Arguments[0].Value.Span); var y = Slice(text, point.Arguments[1].Value.Span);
        var points = new List<string>();
        for (var row = 0; row < grid.Rows; row++) for (var col = 0; col < grid.Columns; col++)
        {
            if (row == 0 && col == 0) continue; // Keep the original seed and its stable identity.
            points.Add($"r{row}c{col} => Point2(({x}) + {Number(col * grid.PitchXmm)}mm, ({y}) + {Number(row * grid.PitchYmm)}mm)");
        }
        var type = Slice(text, seed.Type.Span);
        var recipeFields = string.Join(" ", fields.Select(f => $"{f.Name.Text}: {(f == center ? "center" : Slice(text, f.Value.Span))};"));
        var (withRecipe, _) = InsertDeclaration(text, parse, new("insertDeclaration") { Parent = container,
            Source = $"Feature {name}Recipe(center: Point2) -> {type} {{ return {type} {{ {recipeFields} }} }}" });
        var (withPoints, _) = InsertDeclaration(withRecipe, Parse(withRecipe), new("insertDeclaration") { Parent = container,
            Source = $"Static {name}Points: Set<Point2> {{\n{string.Join("\n", points)}\n}}" });
        return InsertDeclaration(withPoints, Parse(withPoints), new("insertDeclaration") { Parent = modifyId, After = id,
            Source = $"Pattern {name} over {name}Points {{ p => {name}Recipe(center: p) }}" });
    }

    private static (string, string?) SetPlacement(string text, SyntaxParseResult parse, FirmamentLanguageEditOperation op)
    {
        var target = FindStatement(parse, op.Node);
        if (target.Statement is not DeclarationSyntax { Occurrence: true } occurrence || occurrence.Type.Name.Text is not ("Part" or "Assembly" or "Subassembly"))
            throw new FirmamentLanguageEditException("edit-placement-owner", "Select a placed Part or Subassembly.");
        var placement = op.Placement ?? throw new FirmamentLanguageEditException("edit-input-required", "Specify translation and rotation.");
        if (placement.TranslationMm.Length != 3 || placement.TranslationMm.Any(v => !double.IsFinite(v))
            || !double.IsFinite(placement.AngleDegrees) || placement.Axis is not ("X" or "Y" or "Z"))
            throw new FirmamentLanguageEditException("edit-placement-invalid", "Use three finite millimetre coordinates, an X/Y/Z axis and a finite angle in degrees.");
        var source = $"Placement {{ from: Origin; to: Parent; translateLocal: [{string.Join(", ", placement.TranslationMm.Select(v => Number(v) + "mm"))}]; rotateLocal: {{ axis: {placement.Axis}; angle: {Number(placement.AngleDegrees)}deg }} }}";
        // Occurrence bodies use the same structural insertion boundaries as definitions.
        var outline = FirmamentLanguageOutlineService.Outline(text, "edit", "edit");
        var children = outline.Nodes.Where(n => n.ParentId == target.Id && n.Kind == "Placement").ToArray();
        if (children.Length > 1) throw new FirmamentLanguageEditException("edit-placement-ambiguous", "This occurrence has multiple placements. Reconcile them in source first.");
        if (children.Length == 1)
        {
            var old = children[0]; var indent = Indent(text, old.Span.Start);
            return (Replace(text, old.Span.Start, old.Span.Length, FromSource(source, null, indent)[indent.Length..]), target.Id);
        }
        return InsertDeclaration(text, parse, new("insertDeclaration") { Parent = target.Id, Source = source });
    }

    private static bool IsAuthoringBody(DeclarationSyntax d) => d.Type.Name.Text is "Box" or "Cylinder" or "Sphere" or "Ellipsoid" or "Polyhedron" or "Extrude" or "Compose" or "Revolve" or "Sweep" or "Loft";
    private static string Slice(string source, FirmamentV2SourceSpan span) => source.Substring(span.Start, span.Length);
    private static string Number(double value) => value.ToString("G17", CultureInfo.InvariantCulture);
}
