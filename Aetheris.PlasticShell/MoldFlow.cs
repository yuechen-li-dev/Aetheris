using Aetheris.Kernel.Core.Math;

namespace Aetheris.PlasticShell;

/// <summary>
/// The fill, sampled. The part's interior is voxelized (cells whose centres lie inside the mesh, by ray parity along
/// columns) at a third of the thinnest wall or finer, bounded by a cell budget. A shortest-path fill from the cells at
/// the gates then gives each cell its flow length (the path through the material) and its flow ratio (each step over
/// the local wall, summed: L/t). Filling the volume rather than the surface keeps the two skins of a wall one front.
/// <para>
/// Knit lines are where fronts meet: neighbouring cells reached from different gates, or arriving more than 45° apart
/// (shallower, it is a meld line, which does not show) along fill paths that went round
/// opposite sides of something: walking both paths back together, the segment between them leaves the material while
/// they are more than a few cells apart (the two sides of a front split around a hole, or a wall's front meeting the
/// one coming over the top). Paths the grid merely routes differently through open material do not count. Each knit
/// line is a cluster of such cells, with the faces it reaches.
/// </para>
/// </summary>
internal static class MoldFlow
{
    private const int CellBudget = 600_000;
    private const double MeldAngleDegrees = 45;

    public static MoldFlowEvidence? Fill(MoldMeshRays mesh, Point3D min, Point3D max, double thinnest,
        IReadOnlyList<(string Name, Point3D At)> gates, Func<int, double> wallOf, IReadOnlySet<int> cosmetic)
    {
        if (gates.Count == 0) return null;
        var extent = max - min;
        var h = double.Max(thinnest / 3, double.Cbrt(extent.X * extent.Y * extent.Z / CellBudget));
        h = double.Max(h, 1e-3);
        var origin = min - new Vector3D(h, h, h);
        int nx = (int)double.Ceiling(extent.X / h) + 2, ny = (int)double.Ceiling(extent.Y / h) + 2, nz = (int)double.Ceiling(extent.Z / h) + 2;
        var count = nx * ny * nz;
        int Index(int i, int j, int k) => (k * ny + j) * nx + i;
        Point3D Centre(int c) { var i = c % nx; var j = c / nx % ny; var k = c / (nx * ny); return origin + new Vector3D((i + 0.5) * h, (j + 0.5) * h, (k + 0.5) * h); }

        // Inside cells, by parity of the mesh crossings along each column; a column whose crossings do not pair up
        // (it grazes an edge) is shifted slightly and cast again.
        var inside = new bool[count];
        var z = new Vector3D(0, 0, 1);
        for (var j = 0; j < ny; j++)
            for (var i = 0; i < nx; i++)
            {
                IReadOnlyList<double>? crossings = null;
                foreach (var jitter in new[] { 1e-4, 3.7e-3, -5.3e-3 })
                {
                    var start = origin + new Vector3D((i + 0.5 + jitter) * h, (j + 0.5 + jitter * 0.61) * h, 0);
                    var found = mesh.Crossings(start, z, 1e-9 * h);
                    if (found.Count % 2 == 0) { crossings = found; break; }
                }
                if (crossings is null) continue;
                for (var p = 0; p + 1 < crossings.Count; p += 2)
                {
                    var k0 = (int)double.Ceiling(crossings[p] / h - 0.5); var k1 = (int)double.Floor(crossings[p + 1] / h - 0.5);
                    for (var k = int.Max(k0, 0); k <= int.Min(k1, nz - 1); k++) inside[Index(i, j, k)] = true;
                }
            }
        var cells = inside.Count(b => b);
        if (cells == 0) return null;

        // The wall at each cell: the wall measured behind the face nearest it.
        var wall = new double[count];
        for (var c = 0; c < count; c++)
            if (inside[c]) wall[c] = double.Max(wallOf(mesh.Locate(Centre(c))?.Face ?? -1), h);

        // Shortest-path fill over the 26 neighbours from every gate at once.
        var ratio = new double[count]; Array.Fill(ratio, double.PositiveInfinity);
        var length = new double[count];
        var parent = new int[count]; Array.Fill(parent, -1);
        var label = new int[count]; Array.Fill(label, -1);
        var queue = new PriorityQueue<int, double>();
        for (var g = 0; g < gates.Count; g++)
        {
            var seed = NearestInside(gates[g].At);
            if (seed < 0 || ratio[seed] == 0) continue;
            ratio[seed] = 0; label[seed] = g; queue.Enqueue(seed, 0);
        }
        int NearestInside(Point3D p)
        {
            var (ci, cj, ck) = ((int)((p.X - origin.X) / h), (int)((p.Y - origin.Y) / h), (int)((p.Z - origin.Z) / h));
            var best = -1; var bestDistance = double.PositiveInfinity;
            for (var dk = -3; dk <= 3; dk++) for (var dj = -3; dj <= 3; dj++) for (var di = -3; di <= 3; di++)
                    {
                        var (i, j, k) = (ci + di, cj + dj, ck + dk);
                        if (i < 0 || j < 0 || k < 0 || i >= nx || j >= ny || k >= nz || !inside[Index(i, j, k)]) continue;
                        var d = (Centre(Index(i, j, k)) - p).Length;
                        if (d < bestDistance) { bestDistance = d; best = Index(i, j, k); }
                    }
            return best;
        }
        var offsets = new List<(int Di, int Dj, int Dk, double Step)>();
        for (var dk = -1; dk <= 1; dk++) for (var dj = -1; dj <= 1; dj++) for (var di = -1; di <= 1; di++)
                    if (di != 0 || dj != 0 || dk != 0) offsets.Add((di, dj, dk, h * double.Sqrt(di * di + dj * dj + dk * dk)));
        while (queue.TryDequeue(out var c, out var at))
        {
            if (at > ratio[c]) continue;
            var (i, j, k) = (c % nx, c / nx % ny, c / (nx * ny));
            foreach (var (di, dj, dk, step) in offsets)
            {
                var (a, b, e) = (i + di, j + dj, k + dk);
                if (a < 0 || b < 0 || e < 0 || a >= nx || b >= ny || e >= nz) continue;
                var n = Index(a, b, e);
                if (!inside[n]) continue;
                var next = at + step / ((wall[c] + wall[n]) / 2);
                if (next >= ratio[n]) continue;
                ratio[n] = next; length[n] = length[c] + step; parent[n] = c; label[n] = label[c];
                queue.Enqueue(n, next);
            }
        }

        var filled = 0; var worst = -1; var longest = -1;
        for (var c = 0; c < count; c++)
        {
            if (!inside[c] || double.IsPositiveInfinity(ratio[c])) continue;
            filled++;
            if (worst < 0 || ratio[c] > ratio[worst]) worst = c;
            if (longest < 0 || length[c] > length[longest]) longest = c;
        }
        if (filled == 0) return new("no gate reaches the part's interior", h, cells, 0, 0, min, null, 1, []);

        // Knit lines: face neighbours filled from different gates, or round opposite sides of something.
        int Ancestor(int c, int steps) { for (var s = 0; s < steps && parent[c] >= 0; s++) c = parent[c]; return c; }
        Vector3D Arrival(int c)
        {
            var d = Centre(c) - Centre(Ancestor(c, 6));
            return d.Length > 0 ? d / d.Length : d;
        }
        bool Inside(Point3D p)
        {
            var (i, j, k) = ((int)double.Floor((p.X - origin.X) / h), (int)double.Floor((p.Y - origin.Y) / h), (int)double.Floor((p.Z - origin.Z) / h));
            return i >= 0 && j >= 0 && k >= 0 && i < nx && j < ny && k < nz && inside[Index(i, j, k)];
        }
        bool Leaves(Point3D a, Point3D b)
        {
            var steps = (int)double.Ceiling((b - a).Length / (h / 2));
            for (var s = 1; s < steps; s++) if (!Inside(a + (b - a) * ((double)s / steps))) return true;
            return false;
        }
        bool Apart(int c, int n)
        {
            // Back along both paths together, testing the segment between them every few steps, until they join.
            var (a, b) = (c, n);
            for (var step = 1; step <= 1024; step++)
            {
                if (parent[a] >= 0) a = parent[a];
                if (parent[b] >= 0) b = parent[b];
                if (a == b) return false;
                if (step % 4 == 0)
                {
                    var (pa, pb) = (Centre(a), Centre(b));
                    if ((pa - pb).Length > 4 * h && Leaves(pa, pb)) return true;
                }
                if (parent[a] < 0 && parent[b] < 0) return false;
            }
            return false;
        }
        var knit = new Dictionary<int, double>();
        var minimumRun = 4 * h;
        var forward = offsets.Where(o => o.Dk > 0 || o.Dk == 0 && (o.Dj > 0 || o.Dj == 0 && o.Di > 0)).ToArray();
        for (var c = 0; c < count; c++)
        {
            if (!inside[c] || double.IsPositiveInfinity(ratio[c])) continue;
            var (i, j, k) = (c % nx, c / nx % ny, c / (nx * ny));
            // Each pair of the 26 neighbours once: the fill trees can meet across a diagonal.
            foreach (var (di, dj, dk, _) in forward)
            {
                var (a, b, e) = (i + di, j + dj, k + dk);
                if (a < 0 || b < 0 || a >= nx || b >= ny || e >= nz) continue;
                var n = Index(a, b, e);
                if (!inside[n] || double.IsPositiveInfinity(ratio[n]) || parent[n] == c || parent[c] == n) continue;
                if (length[c] < minimumRun || length[n] < minimumRun) continue;
                var angle = double.Acos(double.Clamp(Arrival(c).Dot(Arrival(n)), -1, 1)) * 180 / double.Pi;
                // One gate's front folding on itself at a shallow angle is a meld line, which does not show.
                if (label[c] == label[n] && (angle < MeldAngleDegrees || !Apart(c, n))) continue;
                knit[c] = double.Max(knit.GetValueOrDefault(c), angle);
                knit[n] = double.Max(knit.GetValueOrDefault(n), angle);
            }
        }

        // Cluster the meeting cells across gaps of a cell; a cluster of fewer than three is the grid's noise.
        var lines = new List<MoldKnitLineEvidence>();
        var seen = new HashSet<int>();
        foreach (var start in knit.Keys.Order())
        {
            if (!seen.Add(start)) continue;
            var members = new List<int>();
            var stack = new Stack<int>(); stack.Push(start);
            while (stack.TryPop(out var c))
            {
                members.Add(c);
                var (i, j, k) = (c % nx, c / nx % ny, c / (nx * ny));
                for (var dk = -2; dk <= 2; dk++) for (var dj = -2; dj <= 2; dj++) for (var di = -2; di <= 2; di++)
                        {
                            var (a, b, e) = (i + di, j + dj, k + dk);
                            if (a < 0 || b < 0 || e < 0 || a >= nx || b >= ny || e >= nz) continue;
                            var n = Index(a, b, e);
                            if (knit.ContainsKey(n) && seen.Add(n)) stack.Push(n);
                        }
            }
            if (members.Count < 3) continue;
            var points = members.Select(Centre).ToArray();
            var centroid = Point3D.Origin + points.Aggregate(Vector3D.Zero, (s, p) => s + (p - Point3D.Origin)) / points.Length;
            var from = points.MaxBy(p => (p - centroid).Length);
            var to = points.MaxBy(p => (p - from).Length);
            var faces = new SortedSet<int>();
            foreach (var p in points)
                if (mesh.Locate(p, 1.5 * h) is { } near) faces.Add(near.Face);
            var names = members.Select(m => label[m]).Distinct().Order().Select(g => gates[g].Name).ToArray();
            lines.Add(new(names, centroid, from, to, (to - from).Length + h, members.Max(m => knit[m]), faces.ToArray(), faces.Any(cosmetic.Contains)));
        }

        var last = Centre(longest);
        return new($"Shortest-path fill over {cells} voxel cells of {h:0.###} mm inside the part (26 neighbours), each step over the wall measured behind the nearest face; knit lines where neighbouring cells are reached from different gates, or by paths round opposite sides of an obstacle. Sampled: a cell is a third of the thinnest wall or coarser.",
            h, cells, ratio[worst], length[longest], last, mesh.Locate(last)?.Face, 1 - (double)filled / cells,
            lines.OrderByDescending(l => l.Length).ToArray());
    }
}
