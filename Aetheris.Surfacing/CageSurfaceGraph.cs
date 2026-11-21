using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

/// <summary>Distances follow cage edges measured between their limit positions.</summary>
internal sealed class CageSurfaceGraph
{
    private readonly Dictionary<string, List<(string Id, double Length)>> links;

    internal CageSurfaceGraph(SubdivisionCage cage, IReadOnlyList<CageEdge> edges, IReadOnlySet<string> pinned)
    {
        var points = SubdivisionSculptor.LimitVertices(cage).ToDictionary(v => v.Id, v => v.Position, StringComparer.Ordinal);
        links = points.Keys.ToDictionary(id => id, _ => new List<(string, double)>(), StringComparer.Ordinal);
        foreach (var edge in edges)
        {
            if (pinned.Contains(edge.A) || pinned.Contains(edge.B)) continue;
            var length = (points[edge.A] - points[edge.B]).Length;
            links[edge.A].Add((edge.B, length));
            links[edge.B].Add((edge.A, length));
        }
    }

    internal Dictionary<string, double> Influences(IEnumerable<string> path, double radius)
    {
        var distance = Search(path, radius);
        foreach (var (id, travel) in distance)
        {
            var t = travel / radius;
            distance[id] = (1 - t) * (1 - t) * (1 + 2 * t);
        }
        return distance;
    }

    internal HashSet<string> Trace(IReadOnlyList<string> anchors)
    {
        var path = new HashSet<string>(StringComparer.Ordinal) { anchors[0] };
        for (var i = 1; i < anchors.Count; i++)
        {
            var start = anchors[i - 1];
            var end = anchors[i];
            var parents = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!Search([start], double.PositiveInfinity, end, parents).ContainsKey(end))
                throw new InvalidOperationException("mesh-stroke-path-disconnected: Protected controls separate consecutive stroke anchors.");
            for (var vertex = end; vertex != start; vertex = parents[vertex])
                path.Add(vertex);
        }
        return path;
    }

    private Dictionary<string, double> Search(IEnumerable<string> origins, double cutoff, string? end = null,
        Dictionary<string, string>? parents = null)
    {
        var distance = new Dictionary<string, double>(StringComparer.Ordinal);
        var queue = new PriorityQueue<string, double>();
        foreach (var origin in origins)
            if (distance.TryAdd(origin, 0)) queue.Enqueue(origin, 0);
        while (queue.TryDequeue(out var current, out var travel))
        {
            if (distance[current] != travel) continue;
            if (current == end) break;
            foreach (var (next, length) in links[current])
            {
                var candidate = travel + length;
                if (candidate >= cutoff || candidate >= distance.GetValueOrDefault(next, double.PositiveInfinity)) continue;
                distance[next] = candidate;
                if (parents is not null) parents[next] = current;
                queue.Enqueue(next, candidate);
            }
        }
        return distance;
    }
}
