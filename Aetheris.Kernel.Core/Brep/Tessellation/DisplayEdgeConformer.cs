using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

/// <summary>
/// Makes face patches meet along their shared B-rep edges. Each face samples its boundary for itself, and two faces
/// that sample one edge differently leave slivers of daylight between them. For every edge this gathers the vertices
/// that any adjacent face (and the drawn edge) put on it, and splits each adjacent face's boundary triangles at the
/// ones it lacks, so both sides carry the same polyline. Positions are copied, never re-evaluated, so the sides agree
/// exactly. Only boundary triangles are subdivided.
/// <para>
/// A face's boundary vertex near one of its edge's samples is first moved onto that sample, up to a fifth of the chord
/// tolerance and under a third of the spacing to the next sample, so it never lands on the wrong one. Faces meshed
/// through a surface chart re-evaluate their boundary from the chart, which puts each sample on the surface rather
/// than on the edge: by the edge's distance from the surface, a few microns to a few hundredths of a millimetre on
/// imported splines, enough to break the vertex-for-vertex match with the neighbouring face.
/// </para>
/// </summary>
internal static class DisplayEdgeConformer
{
    public static IReadOnlyList<DisplayFaceMeshPatch> Conform(BrepBody body, IReadOnlyList<DisplayFaceMeshPatch> patches,
        IReadOnlyList<DisplayEdgePolyline> edges, double tolerance, double chordTolerance)
    {
        var polylines = edges.Where(e => e.Points.Count >= 2).ToDictionary(e => e.EdgeId, e => new Polyline(e.Points, tolerance));
        var faceEdges = new Dictionary<FaceId, EdgeId[]>();
        foreach (var patch in patches)
            faceEdges[patch.FaceId] = body.GetLoopIds(patch.FaceId).SelectMany(body.GetCoedgeIds)
                .Select(id => body.Topology.GetCoedge(id).EdgeId).Distinct().Where(polylines.ContainsKey).ToArray();

        // Boundary of each patch: directed triangle edges used once, with the edge each lies along.
        var boundaries = patches.Select(Boundary).ToArray();

        // Boundary vertices onto the nearest sample of the face's own edges, where the face put them on the surface
        // instead: the nearest, not the first edge's, so two faces meeting at a corner pick the same one.
        var samples = new SampleIndex(polylines, double.Max(chordTolerance / 5, 1e-9));
        patches = patches.Select((patch, i) =>
        {
            var positions = patch.Positions.ToArray();
            var moved = false;
            foreach (var v in boundaries[i].SelectMany(s => new[] { s.A, s.B }).Distinct())
                if (samples.Snap(positions[v], faceEdges[patch.FaceId]) is { } sample && sample != positions[v]) { positions[v] = sample; moved = true; }
            return moved ? patch with { Positions = positions } : patch;
        }).ToArray();
        var onEdge = new Dictionary<EdgeId, PointSet>();
        PointSet Set(EdgeId id) => onEdge.TryGetValue(id, out var set) ? set : onEdge[id] = new PointSet(polylines[id].Points, tolerance);
        for (var i = 0; i < patches.Count; i++)
            foreach (var segment in boundaries[i])
                foreach (var edge in faceEdges[patches[i].FaceId])
                {
                    var line = polylines[edge];
                    var (a, b) = (patches[i].Positions[segment.A], patches[i].Positions[segment.B]);
                    if (line.Distance(a) <= tolerance && line.Distance(b) <= tolerance) { Set(edge).Add(a); Set(edge).Add(b); }
                }

        var result = new DisplayFaceMeshPatch[patches.Count];
        for (var i = 0; i < patches.Count; i++)
        {
            var patch = patches[i];
            // Boundary vertices that two faces placed a hair apart take one position: the edge's, or the first face's.
            var snapped = patch.Positions.ToArray();
            var moved = false;
            foreach (var segment in boundaries[i])
                foreach (var edge in faceEdges[patch.FaceId])
                {
                    if (!onEdge.TryGetValue(edge, out var set)) continue;
                    foreach (var v in new[] { segment.A, segment.B })
                    {
                        var canonical = set.Canonical(patch.Positions[v]);
                        if (canonical != snapped[v] && (canonical - patch.Positions[v]).Length <= set.Weld) { snapped[v] = canonical; moved = true; }
                    }
                }
            if (moved) patch = patch with { Positions = snapped };
            var inserts = new Dictionary<(int Triangle, int Slot), List<(double T, Point3D P)>>();
            foreach (var segment in boundaries[i])
            {
                var (a, b) = (patch.Positions[segment.A], patch.Positions[segment.B]);
                var ab = b - a; var length2 = ab.Dot(ab);
                if (length2 <= tolerance * tolerance * 1e-6) continue;
                foreach (var edge in faceEdges[patch.FaceId])
                {
                    var line = polylines[edge];
                    if (line.Distance(a) > tolerance || line.Distance(b) > tolerance || !onEdge.TryGetValue(edge, out var set)) continue;
                    foreach (var p in set.Near(a, b, tolerance))
                    {
                        var t = (p - a).Dot(ab) / length2;
                        if (t <= 1e-9 || t >= 1 - 1e-9) continue;
                        if ((p - (a + (ab * t))).Length > tolerance || (p - a).Length <= set.Weld || (p - b).Length <= set.Weld) continue;
                        var key = (segment.Triangle, segment.Slot);
                        if (!inserts.TryGetValue(key, out var list)) inserts[key] = list = [];
                        if (!list.Any(x => (x.P - p).Length <= set.Weld)) list.Add((t, p));
                    }
                }
            }
            if (inserts.Count == 0) { result[i] = patch; continue; }
            // Inserted vertices take the support's exact normal where the face is analytic, oriented like the patch.
            var support = Aetheris.Kernel.Core.Brep.Blends.BlendSupport.Of(body, patch.FaceId);
            result[i] = Split(patch, inserts, (p, guess) =>
            {
                var exact = support?.Normal(p);
                if (exact is not { } n || n.Length < 1e-12) return guess;
                return n.Dot(guess) < 0 ? n * -1 : n;
            });
        }
        return result;
    }

    private readonly record struct Segment(int A, int B, int Triangle, int Slot);

    private static List<Segment> Boundary(DisplayFaceMeshPatch patch)
    {
        var count = new Dictionary<(int, int), int>();
        var indices = patch.TriangleIndices;
        for (var t = 0; t < indices.Count; t += 3)
            for (var s = 0; s < 3; s++)
            {
                var (a, b) = (indices[t + s], indices[t + ((s + 1) % 3)]);
                var key = a < b ? (a, b) : (b, a);
                count[key] = count.GetValueOrDefault(key) + 1;
            }
        var result = new List<Segment>();
        for (var t = 0; t < indices.Count; t += 3)
            for (var s = 0; s < 3; s++)
            {
                var (a, b) = (indices[t + s], indices[t + ((s + 1) % 3)]);
                if (count[a < b ? (a, b) : (b, a)] == 1) result.Add(new Segment(a, b, t / 3, s));
            }
        return result;
    }

    private static DisplayFaceMeshPatch Split(DisplayFaceMeshPatch patch, Dictionary<(int Triangle, int Slot), List<(double T, Point3D P)>> inserts,
        Func<Point3D, Vector3D, Vector3D> normalAt)
    {
        var positions = patch.Positions.ToList();
        var normals = patch.Normals.ToList();
        var indices = new List<int>(patch.TriangleIndices.Count + (inserts.Values.Sum(v => v.Count) * 6));
        int Add(Point3D p, Vector3D n) { positions.Add(p); normals.Add(n.Length > 1e-12 ? n * (1d / n.Length) : n); return positions.Count - 1; }
        var triangles = patch.TriangleIndices.Count / 3;
        for (var t = 0; t < triangles; t++)
        {
            int[] corner = [patch.TriangleIndices[3 * t], patch.TriangleIndices[(3 * t) + 1], patch.TriangleIndices[(3 * t) + 2]];
            var slots = Enumerable.Range(0, 3).Where(s => inserts.ContainsKey((t, s))).ToArray();
            if (slots.Length == 0) { indices.AddRange(corner); continue; }
            // The triangle's outline with the inserted points, in winding order.
            var ring = new List<int>();
            for (var s = 0; s < 3; s++)
            {
                ring.Add(corner[s]);
                if (!inserts.TryGetValue((t, s), out var list)) continue;
                var (na, nb) = (patch.Normals[corner[s]], patch.Normals[corner[(s + 1) % 3]]);
                foreach (var (tt, p) in list.OrderBy(x => x.T)) ring.Add(Add(p, normalAt(p, (na * (1 - tt)) + (nb * tt))));
            }
            // Fan from the corner opposite the most-split side: every new triangle keeps the original's orientation, and
            // no vertex is placed off the surface (a centroid would sit inside the chord of a curved face).
            var busiest = slots.OrderByDescending(s => inserts[(t, s)].Count).First();
            var apex = corner[(busiest + 2) % 3];
            var start = ring.IndexOf(apex);
            for (var k = 1; k < ring.Count - 1; k++) indices.AddRange([apex, ring[(start + k) % ring.Count], ring[(start + k + 1) % ring.Count]]);
        }
        return patch with { Positions = positions, Normals = normals, TriangleIndices = indices };
    }

    private sealed class Polyline(IReadOnlyList<Point3D> points, double cutoff)
    {
        private double Cutoff { get; } = cutoff;
        public IReadOnlyList<Point3D> Points { get; } = points;
        private readonly (Point3D Min, Point3D Max) box = (new(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)), new(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z)));

        public double Distance(Point3D p)
        {
            // The distance to the bounding box is a lower bound: far points never walk the segments.
            var (dx, dy, dz) = (double.Max(0, double.Max(box.Min.X - p.X, p.X - box.Max.X)), double.Max(0, double.Max(box.Min.Y - p.Y, p.Y - box.Max.Y)),
                double.Max(0, double.Max(box.Min.Z - p.Z, p.Z - box.Max.Z)));
            var outside = double.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            if (outside > Cutoff) return outside;
            var best = double.MaxValue;
            for (var i = 0; i + 1 < Points.Count; i++) best = double.Min(best, SegmentDistance(p, Points[i], Points[i + 1]));
            return best;
        }
    }

    /// <summary>The points known to lie on one edge, deduplicated.</summary>
    /// <summary>Every edge's samples, hashed by position. A point snaps to the nearest sample of the given edges within
    /// the radius, and within a third of the spacing from that sample to the next along its edge.</summary>
    private sealed class SampleIndex
    {
        private readonly double radius;
        private readonly Dictionary<(long, long, long), List<(Point3D P, double Reach, EdgeId Edge)>> cells = [];

        public SampleIndex(IReadOnlyDictionary<EdgeId, Polyline> lines, double radius)
        {
            this.radius = radius;
            foreach (var (edge, line) in lines)
                for (var i = 0; i < line.Points.Count; i++)
                {
                    var spacing = double.MaxValue;
                    if (i > 0) spacing = double.Min(spacing, (line.Points[i] - line.Points[i - 1]).Length);
                    if (i + 1 < line.Points.Count) spacing = double.Min(spacing, (line.Points[i + 1] - line.Points[i]).Length);
                    var reach = double.Min(radius, spacing / 3);
                    var key = Key(line.Points[i]);
                    if (!cells.TryGetValue(key, out var list)) cells[key] = list = [];
                    list.Add((line.Points[i], reach, edge));
                }
        }

        private (long, long, long) Key(Point3D p) => ((long)double.Floor(p.X / radius), (long)double.Floor(p.Y / radius), (long)double.Floor(p.Z / radius));

        public Point3D? Snap(Point3D p, IReadOnlyCollection<EdgeId> edges)
        {
            var (x, y, z) = Key(p);
            Point3D? best = null; var distance = double.MaxValue;
            for (var dx = -1; dx <= 1; dx++) for (var dy = -1; dy <= 1; dy++) for (var dz = -1; dz <= 1; dz++)
                        if (cells.TryGetValue((x + dx, y + dy, z + dz), out var list))
                            foreach (var (q, reach, edge) in list)
                            {
                                if (!edges.Contains(edge)) continue;
                                var d = (q - p).Length;
                                if (d <= reach && d < distance) (best, distance) = (q, d);
                            }
            return best;
        }
    }

    private sealed class PointSet
    {
        private readonly List<Point3D> points = [];
        public double Weld { get; }
        public PointSet(IEnumerable<Point3D> seed, double tolerance) { Weld = double.Max(1e-9, tolerance * 1e-3); foreach (var p in seed) Add(p); }
        public void Add(Point3D p) { if (!points.Any(q => (q - p).Length <= Weld)) points.Add(p); }
        /// <summary>The point this set holds for <paramref name="p"/>: the first one added within the weld distance.</summary>
        public Point3D Canonical(Point3D p) { foreach (var q in points) if ((q - p).Length <= Weld) return q; return p; }
        public IEnumerable<Point3D> Near(Point3D a, Point3D b, double tolerance)
        {
            var (minX, maxX) = (double.Min(a.X, b.X) - tolerance, double.Max(a.X, b.X) + tolerance);
            var (minY, maxY) = (double.Min(a.Y, b.Y) - tolerance, double.Max(a.Y, b.Y) + tolerance);
            var (minZ, maxZ) = (double.Min(a.Z, b.Z) - tolerance, double.Max(a.Z, b.Z) + tolerance);
            return points.Where(p => p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY && p.Z >= minZ && p.Z <= maxZ);
        }
    }

    private static double SegmentDistance(Point3D p, Point3D a, Point3D b)
    {
        var ab = b - a; var l = ab.Dot(ab);
        var t = l <= 0 ? 0 : double.Clamp((p - a).Dot(ab) / l, 0, 1);
        return (p - (a + (ab * t))).Length;
    }
}
