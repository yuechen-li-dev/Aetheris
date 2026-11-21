using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Core.Math;
using Block = Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FirmamentSyntaxScope;

namespace Aetheris.Surfacing;

public static partial class SculptingAuthoring
{
    private static Dictionary<string, MeshSculptState> BindMeshStates(Block model, IReadOnlyDictionary<string, BodyState> bodies, string source,
        List<SculptDiagnostic> diagnostics)
    {
        var states = new Dictionary<string, MeshSculptState>(StringComparer.Ordinal);
        var blocks = Blocks(model, "MeshState");
        if (blocks.Count > 64) { diagnostics.Add(new("mesh-state-budget-exceeded", "At most 64 mesh states are admitted per model.")); return states; }
        foreach (var block in blocks)
        {
            var inputName = block.Word("Input"); var kind = block.Word("Cage");
            var subdivide = Single(block, "Subdivide", diagnostics); var move = Single(block, "Move", diagnostics);
            var scale = Single(block, "Scale", diagnostics); var crease = Single(block, "Crease", diagnostics);
            var brush = Single(block, "Brush", diagnostics);
            var strokes = Blocks(block, "Stroke");
            var fair = Single(block, "Fair", diagnostics);
            var recess = Single(block, "Recess", diagnostics);
            try
            {
                if (bodies.ContainsKey(block.Name) || states.ContainsKey(block.Name)) throw new InvalidOperationException("mesh-state-duplicate: State names must be unique across exact and mesh states.");
                MeshSculptState result;
                var operations = new[] { subdivide, move, scale, crease, brush, fair, recess }.Count(b => b is not null)
                    + (strokes.Count == 0 ? 0 : 1);
                if (kind is not null)
                {
                    if (operations != 0) throw new InvalidOperationException("mesh-conversion-operation-mixed: Conversion is one explicit state; put edits in successor states.");
                    if (inputName is null || !bodies.TryGetValue(inputName, out var body)) throw new InvalidOperationException("mesh-source-unresolved: Conversion input must name a previously admitted exact BodyState or SculptState.");
                    var segments = Integer(block, "Segments") ?? (kind == "Box" ? 1 : 4);
                    var deviation = Length(block, "MaximumDeviation", diagnostics);
                    if (deviation is null) continue;
                    var preview = Integer(block, "PreviewLevels");
                    double? rim = null;
                    if (kind == "SectionChain")
                    {
                        rim = Length(block, "MaximumRimDeviation", diagnostics);
                        if (rim is null) continue;
                    }
                    var rimSection = block.Word("RimSection");
                    var anchors = block.Children("SurfaceAnchor").Select(BindSurfaceAnchor).ToArray();
                    result = SubdivisionSculptor.Convert(body, block.Name, kind, segments, deviation.Value, preview, rim, rimSection, anchors);
                }
                else
                {
                    if (operations != 1) throw new InvalidOperationException("mesh-operation-cardinality: A mesh successor requires exactly one Subdivide, Move, Scale, Crease, Brush, Stroke, Fair or Recess operation.");
                    if (inputName is null || !states.TryGetValue(inputName, out var input)) throw new InvalidOperationException("mesh-predecessor-unresolved: Mesh edits require one previously admitted MeshState.");
                    MeshSculptOperation operation;
                    if (subdivide is not null) operation = new SubdivideCage(block.Name, Integer(subdivide, "Levels") ?? 0);
                    else if (move is not null)
                    {
                        var offset = Vector(move, "Offset", 3, diagnostics); var vertices = ResolveVertexSelection(move, input); if (offset is null) continue;
                        operation = new MoveCageVertices(block.Name, vertices, new(offset[0], offset[1], offset[2]));
                    }
                    else if (scale is not null)
                    {
                        var factor = NumberVector(scale, "Factor", 3, diagnostics); var origin = Vector(scale, "Origin", 3, diagnostics); var vertices = ResolveVertexSelection(scale, input);
                        if (factor is null || origin is null) continue;
                        operation = new ScaleCageVertices(block.Name, vertices, new(factor[0], factor[1], factor[2]), new(origin[0], origin[1], origin[2]));
                    }
                    else if (brush is not null)
                    {
                        if (brush.Take("Center") is not MemberSyntax { Target: ReferenceSyntax owner } center || owner.Name.Text != inputName)
                            throw new InvalidOperationException("mesh-brush-anchor-unresolved: Center must name a surface anchor on the immediate predecessor, such as Seed.Nose.");
                        var radius = Length(brush, "Radius", diagnostics);
                        var offset = Vector(brush, "Offset", 3, diagnostics);
                        var falloff = brush.Take("Falloff");
                        if (falloff is not null && FirmamentSyntaxValues.Reference(falloff) != "Smooth")
                            throw new InvalidOperationException("mesh-brush-falloff-unsupported: The supported falloff is Smooth.");
                        if (radius is null || offset is null) continue;
                        operation = new DisplaceCageBrush(brush.Name, center.Member.Text, radius.Value, new(offset[0], offset[1], offset[2]));
                    }
                    else if (strokes.Count != 0)
                    {
                        var stroke = BindStroke(strokes, inputName, source, diagnostics);
                        if (stroke is null) continue;
                        operation = stroke;
                    }
                    else if (fair is not null)
                    {
                        var regions = List(fair, "Regions");
                        var maximum = Length(fair, "MaximumMovement", diagnostics);
                        var iterations = Integer(fair, "Iterations") ?? 3;
                        if (maximum is null) continue;
                        operation = new FairCageRegion(fair.Name, regions, maximum.Value, iterations);
                    }
                    else if (recess is not null)
                    {
                        if (recess.Declaration.Name is null)
                            throw new InvalidOperationException("mesh-recess-name-required: Name the recess to publish its lip region.");
                        var center = Vector(recess, "Center", 2, diagnostics);
                        var size = Vector(recess, "Size", 2, diagnostics);
                        var floor = Length(recess, "Floor", diagnostics);
                        var lip = Length(recess, "Lip", diagnostics);
                        if (center is null || size is null || floor is null || lip is null) continue;
                        operation = new RecessCageRegion(recess.Name, center[0], center[1], size[0], size[1], floor.Value, lip.Value);
                    }
                    else
                    {
                        var mode = crease!.Word("Mode"); var edges = List(crease, "Edges");
                        if (mode is not ("Sharp" or "Smooth")) throw new InvalidOperationException("mesh-crease-mode-invalid: Crease mode must be Sharp or Smooth.");
                        operation = new CreaseCageEdges(block.Name, edges, mode == "Sharp");
                    }
                    operation = operation with { Preserve = List(block, "Preserve") };
                    result = SubdivisionSculptor.Apply(input, block.Name, operation);
                }
                states.Add(block.Name, result);
            }
            catch (InvalidOperationException error)
            {
                var split = error.Message.IndexOf(':');
                diagnostics.Add(new(split > 0 ? error.Message[..split] : "mesh-sculpt-failed", error.Message, block.Name));
            }
        }
        return states;
    }

    private static DisplaceCageStroke? BindStroke(IReadOnlyList<Block> strokes, string inputName, string source, List<SculptDiagnostic> diagnostics)
    {
        var seeds = strokes.Where(s => s.Declaration.Modifiers.Count == 0).ToArray();
        var mirrors = strokes.Where(s => s.Declaration.Modifiers.Select(m => m.Name.Text).SequenceEqual(["Mirrored"])).ToArray();
        if (seeds.Length != 1 || mirrors.Length > 1 || seeds.Length + mirrors.Length != strokes.Count
            || strokes.Any(s => s.Declaration.Name is null))
            throw new InvalidOperationException("mesh-stroke-declarations-invalid: Use one named Stroke and at most one Mirrored Stroke from that declaration.");
        var seed = seeds[0];
        StrokeMirror? reflected = null;
        if (mirrors.SingleOrDefault() is { } mirror)
        {
            mirror.AllowModifier("Mirrored").AdmitPlacement();
            if (mirror.Name == seed.Name || mirror.Declaration.Clauses is not [{ Kind: ClauseKind.From, Value: ReferenceSyntax from }]
                || from.Name.Text != seed.Name)
                throw new InvalidOperationException("mesh-stroke-mirror-source: A Mirrored Stroke must have a distinct name and derive from the Stroke in the same MeshState.");
            var acrossName = mirror.Word("Across");
            var planeErrors = new List<string>();
            if (acrossName is null
                || !SemanticSymmetryAuthoring.TryResolvePlane(source, acrossName, planeErrors, out var plane, out _)
                || plane is null || planeErrors.Count != 0)
                throw new InvalidOperationException("mesh-stroke-mirror-plane-invalid: Across must resolve to a named symmetry plane.");
            reflected = new(mirror.Name, acrossName, plane.Origin, plane.Normal.ToVector());
        }

        var path = List(seed, "Path");
        var prefix = inputName + ".";
        if (path.Any(name => !name.StartsWith(prefix, StringComparison.Ordinal) || name[prefix.Length..].Contains('.')))
            throw new InvalidOperationException("mesh-stroke-anchor-unresolved: Path must name surface anchors on the immediate predecessor.");
        var radius = Length(seed, "Radius", diagnostics);
        var offset = Vector(seed, "Offset", 3, diagnostics);
        var falloff = seed.Take("Falloff");
        if (falloff is not null && FirmamentSyntaxValues.Reference(falloff) != "Smooth")
            throw new InvalidOperationException("mesh-stroke-falloff-unsupported: The supported falloff is Smooth.");
        return radius is null || offset is null ? null
            : new(seed.Name, path.Select(name => name[prefix.Length..]).ToArray(), radius.Value, new(offset[0], offset[1], offset[2]), reflected);
    }

    private static MeshSectionAnchor BindSurfaceAnchor(Block anchor)
    {
        var section = anchor.Word("Section");
        var span = anchor.Word("Span");
        if (anchor.Declaration.Name is null || section is null || span is null)
            throw new InvalidOperationException("mesh-anchor-invalid: A named SurfaceAnchor requires section and span.");
        return new(anchor.Name, section, span);
    }

    private static IReadOnlyList<string> ResolveVertexSelection(Block edit, MeshSculptState input)
    {
        var vertices = List(edit, "Vertices");
        var regions = List(edit, "Regions");
        if (regions.Count == 0) return vertices;
        if (vertices.Count != 0 || regions.Distinct(StringComparer.Ordinal).Count() != regions.Count)
            throw new InvalidOperationException("mesh-region-selection-invalid: Select unique regions or explicit vertices.");
        var available = input.VertexRegions;
        foreach (var name in regions)
            if (available is null || !available.ContainsKey(name))
                throw new InvalidOperationException($"mesh-region-unresolved: Cage region '{name}' is unavailable on the predecessor.");
        return regions.SelectMany(name => available![name]).Distinct(StringComparer.Ordinal).ToArray();
    }
}
