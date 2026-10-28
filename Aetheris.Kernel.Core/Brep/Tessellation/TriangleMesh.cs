using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

/// <summary>Final derived triangle-only mesh.  It deliberately has no B-rep or trim knowledge.</summary>
public sealed record TriangleMesh(
    IReadOnlyList<Point3D> Positions,
    IReadOnlyList<Vector3D> Normals,
    IReadOnlyList<int> TriangleIndices,
    IReadOnlySet<(int A, int B)> HardEdges,
    string DeterministicHash);

public sealed record TriangleMeshTopologyReport(
    bool IsWatertight,
    bool IsConnected,
    bool IsOutwardOriented,
    int CrackCount,
    int NonManifoldEdgeCount,
    int DuplicateTriangleCount,
    int ZeroAreaTriangleCount,
    int TriangleCount,
    int VertexCount,
    double SignedVolume)
{
    public int OrientationConflictEdgeCount { get; init; }
    public bool IsConsistentlyOriented { get; init; }
    public bool HasValidSignedVolume { get; init; }
}

public static class TriangleMeshValidator
{
    private const double AreaEpsilon = 1e-18d;

    public static bool TryValidateClosed(TriangleMesh mesh, out TriangleMeshTopologyReport report, out string? failure)
    {
        var edges = new Dictionary<(int A, int B), (int Count, int Balance)>();
        var triangles = new HashSet<(int A, int B, int C)>();
        var zero = 0; var duplicate = 0;
        if (mesh.Positions.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Z)))
        { failure = "Triangle mesh contains a nonfinite position."; report = Empty(mesh); return false; }
        for (var i = 0; i < mesh.TriangleIndices.Count; i += 3)
        {
            if (i + 2 >= mesh.TriangleIndices.Count) { failure = "Triangle index buffer is not a multiple of three."; report = Empty(mesh); return false; }
            var a = mesh.TriangleIndices[i]; var b = mesh.TriangleIndices[i + 1]; var c = mesh.TriangleIndices[i + 2];
            if (a < 0 || b < 0 || c < 0 || a >= mesh.Positions.Count || b >= mesh.Positions.Count || c >= mesh.Positions.Count) { failure = "Triangle references an invalid vertex."; report = Empty(mesh); return false; }
            var canonical = new[] { a, b, c }; Array.Sort(canonical);
            if (!triangles.Add((canonical[0], canonical[1], canonical[2]))) duplicate++;
            if (((mesh.Positions[b] - mesh.Positions[a]).Cross(mesh.Positions[c] - mesh.Positions[a])).Length <= AreaEpsilon) zero++;
            AddEdge(a, b); AddEdge(b, c); AddEdge(c, a);
        }
        var cracks = edges.Count(pair => pair.Value.Count == 1);
        var nonmanifold = edges.Count(pair => pair.Value.Count != 2);
        var conflicts = edges.Count(pair => pair.Value.Count == 2 && pair.Value.Balance != 0);
        var connected = IsConnected(mesh);
        var volume = SignedVolume(mesh);
        var consistent = cracks == 0 && nonmanifold == 0 && conflicts == 0;
        var validVolume = consistent && connected && duplicate == 0 && zero == 0 && double.IsFinite(volume);
        var outward = validVolume && volume > 0d;
        report = new TriangleMeshTopologyReport(outward, connected, outward, cracks, nonmanifold, duplicate, zero,
            mesh.TriangleIndices.Count / 3, mesh.Positions.Count, volume)
        { OrientationConflictEdgeCount = conflicts, IsConsistentlyOriented = consistent, HasValidSignedVolume = validVolume };
        failure = report.IsWatertight ? null : $"Triangle mesh topology failed: cracks={cracks}, nonmanifold={nonmanifold}, orientationConflicts={conflicts}, duplicates={duplicate}, zeroArea={zero}, connected={connected}, signedVolumeValid={validVolume}, signedVolume={volume:R}.";
        return report.IsWatertight;

        void AddEdge(int x, int y)
        {
            var edge = x < y ? (x, y) : (y, x);
            var old = edges.GetValueOrDefault(edge);
            edges[edge] = (old.Count + 1, old.Balance + (x < y ? 1 : -1));
        }
    }

    private static TriangleMeshTopologyReport Empty(TriangleMesh mesh) => new(false, false, false, 0, 0, 0, 0, mesh.TriangleIndices.Count / 3, mesh.Positions.Count, 0d);

    private static bool IsConnected(TriangleMesh mesh)
    {
        if (mesh.TriangleIndices.Count == 0) return false;
        var adjacency = new Dictionary<int, HashSet<int>>();
        for (var i = 0; i < mesh.TriangleIndices.Count; i += 3)
        {
            for (var j = 0; j < 3; j++)
            {
                var a = mesh.TriangleIndices[i + j]; var b = mesh.TriangleIndices[i + ((j + 1) % 3)];
                if (!adjacency.TryGetValue(a, out var linked)) adjacency[a] = linked = [];
                linked.Add(b);
                if (!adjacency.TryGetValue(b, out linked)) adjacency[b] = linked = [];
                linked.Add(a);
            }
        }
        var seen = new HashSet<int>(); var pending = new Stack<int>(); pending.Push(adjacency.Keys.Min());
        while (pending.Count > 0) foreach (var next in adjacency[pending.Pop()]) if (seen.Add(next)) pending.Push(next);
        return seen.Count == adjacency.Count;
    }

    private static double SignedVolume(TriangleMesh mesh)
    {
        if (mesh.Positions.Count == 0) return 0;
        // A nearby anchor avoids cancellation for closed parts far from world
        // zero. This is numerical conditioning, not a closure/orientation proof.
        var origin = mesh.Positions[0];
        var volume = 0d; var compensation = 0d;
        for (var i = 0; i < mesh.TriangleIndices.Count; i += 3)
        {
            var a = mesh.Positions[mesh.TriangleIndices[i]]; var b = mesh.Positions[mesh.TriangleIndices[i + 1]]; var c = mesh.Positions[mesh.TriangleIndices[i + 2]];
            var term = (a - origin).Dot((b - origin).Cross(c - origin)) / 6d;
            var corrected = term - compensation;
            var next = volume + corrected;
            compensation = (next - volume) - corrected;
            volume = next;
        }
        return volume;
    }
}
