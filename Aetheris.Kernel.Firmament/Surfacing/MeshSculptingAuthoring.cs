using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Core.Math;
using Block = Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FirmamentSyntaxScope;

namespace Aetheris.Surfacing;

public static partial class SculptingAuthoring
{
    private static Dictionary<string, MeshSculptState> BindMeshStates(Block model, IReadOnlyDictionary<string, BodyState> bodies, List<SculptDiagnostic> diagnostics)
    {
        var states = new Dictionary<string, MeshSculptState>(StringComparer.Ordinal);
        var blocks = Blocks(model, "MeshState");
        if (blocks.Count > 64) { diagnostics.Add(new("mesh-state-budget-exceeded", "At most 64 mesh states are admitted per model.")); return states; }
        foreach (var block in blocks)
        {
            var inputName = block.Word("Input"); var kind = block.Word("Cage");
            var subdivide = Single(block, "Subdivide", diagnostics); var move = Single(block, "Move", diagnostics);
            var scale = Single(block, "Scale", diagnostics); var crease = Single(block, "Crease", diagnostics);
            try
            {
                if (bodies.ContainsKey(block.Name) || states.ContainsKey(block.Name)) throw new InvalidOperationException("mesh-state-duplicate: State names must be unique across exact and mesh states.");
                MeshSculptState result;
                var operations = new[] { subdivide, move, scale, crease }.Count(b => b is not null);
                if (kind is not null)
                {
                    if (operations != 0) throw new InvalidOperationException("mesh-conversion-operation-mixed: Conversion is one explicit state; put edits in successor states.");
                    if (inputName is null || !bodies.TryGetValue(inputName, out var body)) throw new InvalidOperationException("mesh-source-unresolved: Conversion input must name a previously admitted exact BodyState or SculptState.");
                    var segments = Integer(block, "Segments") ?? (kind == "Box" ? 1 : 4);
                    var deviation = Length(block, "MaximumDeviation", diagnostics);
                    if (deviation is null) continue;
                    result = SubdivisionSculptor.Convert(body, block.Name, kind, segments, deviation.Value);
                }
                else
                {
                    if (operations != 1) throw new InvalidOperationException("mesh-operation-cardinality: A mesh successor requires exactly one Subdivide, Move, Scale or Crease operation.");
                    if (inputName is null || !states.TryGetValue(inputName, out var input)) throw new InvalidOperationException("mesh-predecessor-unresolved: Mesh edits require one previously admitted MeshState.");
                    MeshSculptOperation operation;
                    if (subdivide is not null) operation = new SubdivideCage(block.Name, Integer(subdivide, "Levels") ?? 0);
                    else if (move is not null)
                    {
                        var offset = Vector(move, "Offset", 3, diagnostics); var vertices = List(move, "Vertices"); if (offset is null) continue;
                        operation = new MoveCageVertices(block.Name, vertices, new(offset[0], offset[1], offset[2]));
                    }
                    else if (scale is not null)
                    {
                        var factor = NumberVector(scale, "Factor", 3, diagnostics); var origin = Vector(scale, "Origin", 3, diagnostics); var vertices = List(scale, "Vertices");
                        if (factor is null || origin is null) continue;
                        operation = new ScaleCageVertices(block.Name, vertices, new(factor[0], factor[1], factor[2]), new(origin[0], origin[1], origin[2]));
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
}
