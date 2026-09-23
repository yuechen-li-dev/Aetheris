using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Build-local index of the construction correspondence, never a geometry recognizer.</summary>
public sealed class GeometrySourceMap
{
    private readonly Dictionary<FaceId, SemanticTopologyDescendant> _faces = new();
    private readonly Dictionary<EdgeId, SemanticTopologyDescendant> _edges = new();
    private readonly Dictionary<string, IReadOnlyList<SemanticTopologyDescendant>> _semanticKeys;
    private readonly Dictionary<string, IReadOnlyList<SemanticTopologyDescendant>> _sourceSymbols;
    private readonly IReadOnlyDictionary<string, FirmamentV2SourceSpan> _sourceSpans;

    public GeometrySourceMap(SemanticTopologyCorrespondence correspondence)
    {
        ArgumentNullException.ThrowIfNull(correspondence);
        foreach (var descendant in correspondence.Descendants)
        {
            if (descendant.Face is { } face && !_faces.TryAdd(face, descendant))
                throw new InvalidOperationException($"Duplicate construction correspondence for BRep face {face.Value}.");
            if (descendant.Edge is { } edge && !_edges.TryAdd(edge, descendant))
                throw new InvalidOperationException($"Duplicate construction correspondence for BRep edge {edge.Value}.");
        }
        var semanticGroups = correspondence.Descendants.GroupBy(item => item.StableId, StringComparer.Ordinal).ToArray();
        foreach (var group in semanticGroups)
            if (group.Select(item => (item.Role, item.SourceStableId, item.ParentStableId)).Distinct().Skip(1).Any())
                throw new InvalidOperationException($"Conflicting construction origins for semantic key '{group.Key}'.");
        _semanticKeys = semanticGroups
            .ToDictionary(group => group.Key, group => (IReadOnlyList<SemanticTopologyDescendant>)group.ToArray(), StringComparer.Ordinal);
        _sourceSymbols = correspondence.Descendants.GroupBy(item => item.ParentStableId ?? item.SourceStableId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<SemanticTopologyDescendant>)group.ToArray(), StringComparer.Ordinal);
        _sourceSpans = correspondence.SourceSpans ?? new Dictionary<string, FirmamentV2SourceSpan>();
    }

    public bool TryGetByBrepFace(FaceId face, out SemanticTopologyDescendant descendant) => _faces.TryGetValue(face, out descendant!);
    public bool TryGetByBrepEdge(EdgeId edge, out SemanticTopologyDescendant descendant) => _edges.TryGetValue(edge, out descendant!);
    public IReadOnlyList<SemanticTopologyDescendant> GetEntitiesForSemanticKey(string key) => _semanticKeys.GetValueOrDefault(key) ?? [];
    public IReadOnlyList<SemanticTopologyDescendant> GetEntitiesForSourceSymbol(string symbol) => _sourceSymbols.GetValueOrDefault(symbol) ?? [];
    public bool TryGetSourceSpan(string symbol, out FirmamentV2SourceSpan span) => _sourceSpans.TryGetValue(symbol, out span!);
}
