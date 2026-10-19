using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aetheris.Surfacing;

/// <summary>Reuse exact construction replay, then execute the typed cage program.</summary>
public static class MeshConstructionReplayer
{
    private sealed record SavedMesh(string Schema, string SourceJson, string Cage, int Segments, double MaximumDeviationMm, IReadOnlyList<MeshSculptOperation> Operations);
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static string Serialize(MeshConstruction construction)
    {
        var source = ConstructionStateSerializer.Serialize(construction.Source);
        if (!source.IsSuccess) throw new InvalidOperationException(string.Join(";", source.Diagnostics.Select(d => d.Message)));
        return JsonSerializer.Serialize(new SavedMesh(construction.Schema, source.Json!, construction.Cage, construction.Segments, construction.MaximumDeviationMm, construction.Operations), Options);
    }
    public static MeshSculptState Replay(string json, string name = "Replayed")
    {
        var saved = JsonSerializer.Deserialize<SavedMesh>(json, Options) ?? throw new InvalidOperationException("mesh-replay-invalid: Empty mesh recipe.");
        if (saved.Schema != SubdivisionSculptor.Schema || saved.Operations is null || saved.Operations.Count > 64 || saved.Operations.Any(o => o is null))
            throw new InvalidOperationException("mesh-replay-version-unsupported: Unknown schema or exceeded operation budget.");
        var source = ConstructionStateSerializer.Deserialize(saved.SourceJson);
        if (!source.IsSuccess) throw new InvalidOperationException(string.Join(";", source.Diagnostics.Select(d => d.Message)));
        var replay = ConstructionStateReplayer.Replay(source.ConstructionState!);
        if (!replay.IsSuccess || replay.OutputState is null) throw new InvalidOperationException(string.Join(";", replay.Diagnostics.Select(d => d.Message)));
        var state = SubdivisionSculptor.Convert(replay.OutputState, name, saved.Cage, saved.Segments, saved.MaximumDeviationMm);
        foreach (var operation in saved.Operations) state = SubdivisionSculptor.Apply(state, name, operation);
        SubdivisionSculptor.PrepareExport(state.Cage, state.Construction.MaximumDeviationMm);
        return state;
    }
}
