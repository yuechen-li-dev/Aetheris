namespace Aetheris.Reconstruction;

public sealed record GlobalMatchingEdge(int A, int B, double Utility, string StableId);
public sealed record GlobalMatchingResult(IReadOnlyList<int> Mate, int MatchedEdges, int Augmentations, int BlossomContractions, string Algorithm);

/// <summary>Deterministic Edmonds blossom maximum-cardinality matching over a hard-admissible graph.</summary>
public static class GlobalQuadLayoutMatcher
{
    public static GlobalMatchingResult Match(int vertexCount, IReadOnlyList<GlobalMatchingEdge> edges, IReadOnlyList<int>? initialMate = null)
    {
        if (vertexCount < 0) throw new ArgumentOutOfRangeException(nameof(vertexCount)); ArgumentNullException.ThrowIfNull(edges);
        var graph = Enumerable.Range(0, vertexCount).Select(_ => new List<(int Vertex, double Utility, string Id)>()).ToArray();
        foreach (var edge in edges)
        {
            if (edge.A < 0 || edge.A >= vertexCount || edge.B < 0 || edge.B >= vertexCount || edge.A == edge.B || !double.IsFinite(edge.Utility)) throw new ArgumentException("Matching edges must be finite, distinct, and in range.", nameof(edges));
            graph[edge.A].Add((edge.B, edge.Utility, edge.StableId)); graph[edge.B].Add((edge.A, edge.Utility, edge.StableId));
        }
        foreach (var list in graph) list.Sort((x, y) => { var score = y.Utility.CompareTo(x.Utility); if (score != 0) return score; var id = string.CompareOrdinal(x.Id, y.Id); return id != 0 ? id : x.Vertex.CompareTo(y.Vertex); });
        var match = Enumerable.Repeat(-1, vertexCount).ToArray();
        if (initialMate is not null)
        {
            if (initialMate.Count != vertexCount) throw new ArgumentException("Initial mate vector length must equal vertex count.", nameof(initialMate));
            for (var i = 0; i < vertexCount; i++) if (initialMate[i] >= 0)
            {
                var mate = initialMate[i]; if (mate >= vertexCount || initialMate[mate] != i || !graph[i].Any(x => x.Vertex == mate)) throw new ArgumentException("Initial mates must be symmetric admissible edges.", nameof(initialMate));
                match[i] = mate;
            }
        }
        var parent = new int[vertexCount]; var basis = new int[vertexCount]; var used = new bool[vertexCount]; var blossom = new bool[vertexCount]; var queue = new Queue<int>();
        var augmentations = 0; var contractions = 0;
        int Lca(int a, int b) { var path = new bool[vertexCount]; while (true) { a = basis[a]; path[a] = true; if (match[a] < 0) break; a = parent[match[a]]; } while (true) { b = basis[b]; if (path[b]) return b; b = parent[match[b]]; } }
        void Mark(int vertex, int root, int child) { while (basis[vertex] != root) { blossom[basis[vertex]] = blossom[basis[match[vertex]]] = true; parent[vertex] = child; child = match[vertex]; vertex = parent[match[vertex]]; } }
        int Find(int root)
        {
            Array.Fill(used, false); Array.Fill(parent, -1); for (var i = 0; i < vertexCount; i++) basis[i] = i; queue.Clear(); queue.Enqueue(root); used[root] = true;
            while (queue.Count > 0)
            {
                var vertex = queue.Dequeue();
                foreach (var edge in graph[vertex])
                {
                    var next = edge.Vertex; if (basis[vertex] == basis[next] || match[vertex] == next) continue;
                    if (next == root || (match[next] >= 0 && parent[match[next]] >= 0))
                    {
                        var rootBasis = Lca(vertex, next); Array.Fill(blossom, false); Mark(vertex, rootBasis, next); Mark(next, rootBasis, vertex); contractions++;
                        for (var i = 0; i < vertexCount; i++) if (blossom[basis[i]]) { basis[i] = rootBasis; if (!used[i]) { used[i] = true; queue.Enqueue(i); } }
                    }
                    else if (parent[next] < 0) { parent[next] = vertex; if (match[next] < 0) return next; next = match[next]; used[next] = true; queue.Enqueue(next); }
                }
            }
            return -1;
        }
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var root = 0; root < vertexCount; root++) if (match[root] < 0)
            {
                var endpoint = Find(root); if (endpoint < 0) continue;
                while (endpoint >= 0) { var previous = parent[endpoint]; var next = previous < 0 ? -1 : match[previous]; match[endpoint] = previous; if (previous >= 0) match[previous] = endpoint; endpoint = next; }
                augmentations++; changed = true; break;
            }
        }
        return new(match, match.Count(x => x >= 0) / 2, augmentations, contractions, "deterministic Edmonds blossom maximum-cardinality matching over the hard-admissible layout graph");
    }
}
