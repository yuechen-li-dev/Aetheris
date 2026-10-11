using System.Collections.Immutable;

namespace Aetheris.Cloth3D;

public enum ClothContactFeature3D
{
    Face,
    Edge,
    Vertex,
    EdgePair,
}

public sealed record ClothContactPair3D(ClothContactFeature3D Kind,
    ImmutableArray<int> Vertices, int Feature);

public sealed record ClothContactEdge3D(int A, int B, ImmutableArray<int> Opposite);

/// <summary>Complete candidate set and conflict-free contact tape compiled from topology.
/// Exhaustive candidates deliberately avoid a broadphase omission. This foundation is
/// bounded to 128 vertices; spatial acceleration belongs above the same narrowphase.</summary>
public sealed class ClothContactTopology3D
{
    public ImmutableArray<ClothContactEdge3D> Edges { get; }
    public ImmutableArray<ImmutableArray<int>> Neighbors { get; }
    public ImmutableArray<ImmutableArray<int>> IncidentFaces { get; }
    public ImmutableArray<ClothContactPair3D> Pairs { get; }
    public ImmutableArray<ImmutableArray<int>> Colors { get; }
    public ImmutableArray<(int Left, int Right)> ReductionNodes { get; }
    public ImmutableArray<ImmutableArray<int>> ReductionLevels { get; }
    public ImmutableArray<int> Roots { get; }

    private ClothContactTopology3D(ClothDefinition3D definition)
    {
        int count = definition.Positions.Length;
        if (count > 128)
        {
            throw new NotSupportedException("AUR-CLOTH-004: Exhaustive offset contact supports at most 128 vertices; accelerated candidate search is not implemented.");
        }
        var neighbors = Enumerable.Range(0, count).Select(_ => new SortedSet<int>()).ToArray();
        var faces = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
        var edges = new SortedDictionary<(int A, int B), List<int>>();
        for (int face = 0; face < definition.Indices.Length / 3; face++)
        {
            int a = definition.Indices[face * 3];
            int b = definition.Indices[face * 3 + 1];
            int c = definition.Indices[face * 3 + 2];
            faces[a].Add(face);
            faces[b].Add(face);
            faces[c].Add(face);
            AddEdge(a, b, c);
            AddEdge(b, c, a);
            AddEdge(c, a, b);
        }
        if (neighbors.Any(row => row.Count > 32) || faces.Any(row => row.Count > 32))
        {
            throw new NotSupportedException("AUR-CLOTH-005: Offset contact supports at most 32 adjacent features per vertex.");
        }
        Edges = edges.Select(entry => new ClothContactEdge3D(entry.Key.A, entry.Key.B,
            entry.Value.ToImmutableArray())).ToImmutableArray();
        Neighbors = neighbors.Select(row => row.ToImmutableArray()).ToImmutableArray();
        IncidentFaces = faces.Select(row => row.ToImmutableArray()).ToImmutableArray();
        var pairs = new List<ClothContactPair3D>();
        for (int vertex = 0; vertex < count; vertex++)
        {
            for (int face = 0; face < definition.Indices.Length / 3; face++)
            {
                int a = definition.Indices[face * 3];
                int b = definition.Indices[face * 3 + 1];
                int c = definition.Indices[face * 3 + 2];
                if (vertex != a && vertex != b && vertex != c)
                {
                    pairs.Add(new(ClothContactFeature3D.Face, [vertex, a, b, c], face));
                }
            }
            for (int edge = 0; edge < Edges.Length; edge++)
            {
                var segment = Edges[edge];
                if (vertex != segment.A && vertex != segment.B)
                {
                    pairs.Add(new(ClothContactFeature3D.Edge, [vertex, segment.A, segment.B], edge));
                }
            }
            for (int other = vertex + 1; other < count; other++)
            {
                pairs.Add(new(ClothContactFeature3D.Vertex, [vertex, other], other));
            }
        }
        for (int first = 0; first < Edges.Length; first++)
        {
            var a = Edges[first];
            for (int second = first + 1; second < Edges.Length; second++)
            {
                var b = Edges[second];
                if (a.A != b.A && a.A != b.B && a.B != b.A && a.B != b.B)
                {
                    pairs.Add(new(ClothContactFeature3D.EdgePair, [a.A, a.B, b.A, b.B], second));
                }
            }
        }
        Pairs = pairs.ToImmutableArray();
        // Normal-block tests read one-rings outside the four written vertices.
        // Include those reads in coloring so another lane cannot mutate them.
        Colors = ClothCompiler3D.Color(Pairs.Select(pair => pair.Vertices
            .Concat(pair.Kind is ClothContactFeature3D.Vertex or ClothContactFeature3D.EdgePair
                ? pair.Vertices.SelectMany(vertex => Neighbors[vertex]) : [])
            .Concat(pair.Kind == ClothContactFeature3D.Edge ? Edges[pair.Feature].Opposite : [])
            .Distinct().Order().ToImmutableArray()));

        // Parallel reduction levels contain no dynamic nested traversal. Every leaf
        // is a candidate distance, each vertex root gathers ALL participating features.
        var leaves = Enumerable.Range(0, count).Select(_ => new List<int>()).ToArray();
        for (int pair = 0; pair < pairs.Count; pair++)
        {
            if (pairs[pair].Kind is ClothContactFeature3D.Face or ClothContactFeature3D.EdgePair)
            {
                foreach (int vertex in pairs[pair].Vertices)
                {
                    leaves[vertex].Add(pair);
                }
            }
        }
        var nodes = new List<(int Left, int Right)>();
        var levels = new List<List<int>>();
        var roots = new List<int>();
        foreach (var leaf in leaves)
        {
            var current = leaf;
            int level = 0;
            while (current.Count > 1)
            {
                if (levels.Count == level)
                {
                    levels.Add([]);
                }
                var next = new List<int>();
                for (int index = 0; index < current.Count; index += 2)
                {
                    if (index + 1 == current.Count)
                    {
                        next.Add(current[index]);
                        continue;
                    }
                    int node = nodes.Count;
                    nodes.Add((current[index], current[index + 1]));
                    levels[level].Add(node);
                    next.Add(pairs.Count + node);
                }
                current = next;
                level++;
            }
            roots.Add(current.Count == 0 ? -1 : current[0]);
        }
        ReductionNodes = nodes.ToImmutableArray();
        ReductionLevels = levels.Select(level => level.ToImmutableArray()).ToImmutableArray();
        Roots = roots.ToImmutableArray();

        void AddEdge(int a, int b, int opposite)
        {
            neighbors[a].Add(b);
            neighbors[b].Add(a);
            var key = (Math.Min(a, b), Math.Max(a, b));
            if (!edges.TryGetValue(key, out var list))
            {
                list = [];
                edges.Add(key, list);
            }
            list.Add(opposite);
        }
    }

    internal static ClothContactTopology3D Compile(ClothDefinition3D definition) => new(definition);
}
