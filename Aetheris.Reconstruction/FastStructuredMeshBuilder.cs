using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Reconstruction;

public sealed record ReconstructedQuadRegion(
    string StableId, int FaceA, int FaceB, IReadOnlyList<int> CornerVertices,
    double FieldAlignmentDegrees, double NormalDeviationDegrees, double AspectRatio,
    ISurfaceMeshBoundedPatch Support);

public sealed record ReconstructedStructuralSurface(
    IReadOnlyList<ReconstructedQuadRegion> QuadRegions,
    IReadOnlyList<int> TransitionTriangles,
    string Method,
    int MaximumAugmentingPathDepth,
    string DeterministicHash);

internal sealed record FastStructuredMeshBuild(
    SurfaceMeshDocument Document,
    ReconstructedStructuralSurface Structure,
    int BoundaryEdges,
    int NonManifoldEdges,
    string Validation);

internal static class FastStructuredMeshBuilder
{
    private sealed record Candidate(int A, int B, int[] Boundary, double Utility, double FieldDegrees, double NormalDegrees, double Aspect);

    public static FastStructuredMeshBuild Build(TriangleSurfaceMesh source, IReadOnlyList<DifferentialSample> field, double strongFeatureAngleDegrees)
    {
        var edgeFaces = EdgeFaces(source); var candidates = Candidates(source, field, edgeFaces, strongFeatureAngleDegrees);
        var byFace = candidates.SelectMany(c => new[] { (Face: c.A, Candidate: c), (Face: c.B, Candidate: c) })
            .GroupBy(x => x.Face).ToDictionary(g => g.Key,
                g => g.Select(x => x.Candidate).OrderByDescending(c => c.Utility).ThenBy(c => c.A).ThenBy(c => c.B).ToArray());
        var mate = Enumerable.Repeat(-1, source.Triangles.Count).ToArray();
        foreach (var face in Enumerable.Range(0, mate.Length).OrderBy(f => byFace.GetValueOrDefault(f)?.Length ?? int.MaxValue).ThenBy(f => f))
        {
            if (mate[face] >= 0 || !byFace.TryGetValue(face, out var options)) continue;
            var selected = options.FirstOrDefault(c => mate[c.A] < 0 && mate[c.B] < 0);
            if (selected is null) continue;
            mate[selected.A] = selected.B; mate[selected.B] = selected.A;
        }
        const int maximumAugmentations = 640;
        const int maximumDepth = 5;
        ImproveMatching(mate, byFace, maximumDepth, maximumAugmentations);

        var byPair = candidates.ToDictionary(c => Key(c.A, c.B)); var regions = new List<ReconstructedQuadRegion>();
        for (var face = 0; face < mate.Length; face++) if (mate[face] > face)
        {
            var candidate = byPair[Key(face, mate[face])]; var id = $"fast-quad-{regions.Count:D6}";
            var points = candidate.Boundary.Select(v => source.Vertices[v]).ToArray();
            regions.Add(new(id, candidate.A, candidate.B, candidate.Boundary, candidate.FieldDegrees,
                candidate.NormalDegrees, candidate.Aspect, new FastBilinearQuadPatch(id + ":support", points)));
        }
        var transitions = Enumerable.Range(0, mate.Length).Where(i => mate[i] < 0).ToArray();
        var payload = string.Join('|', regions.Select(r => string.Join(',', r.CornerVertices))) + ";" + string.Join(',', transitions);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var structure = new ReconstructedStructuralSurface(regions, transitions,
            $"deterministic field-scored local matching plus at most {maximumAugmentations} augmenting paths; no global atlas search", maximumDepth, hash);

        var vertices = source.Vertices.Select((point, i) => new SurfaceMeshVertex(i + 1, point)).ToArray();
        var patches = new List<SurfacePatch>(regions.Count + transitions.Length); var nextFace = 1;
        foreach (var region in regions)
            patches.Add(new(new FaceId(nextFace++),
                new(SurfaceMeshSupportKind.BoundedParametricPatch, BoundedPatch: region.Support, BoundedPatchStableId: region.Support.StableId), [],
                [new QuadCell(region.CornerVertices.Select(v => v + 1).ToArray())], true,
                "approximate-reconstructed-region", ChartId: region.StableId,
                PlanarPlannerPath: "native bounded bilinear parametric carrier; mesh-first Fast path"));
        foreach (var face in transitions)
        {
            var triangle = source.Triangles[face]; var support = new FastTrianglePatch($"fast-transition-{face:D6}:support",
                source.Vertices[triangle.A], source.Vertices[triangle.B], source.Vertices[triangle.C]);
            patches.Add(new(new FaceId(nextFace++),
                new(SurfaceMeshSupportKind.BoundedParametricPatch, BoundedPatch: support, BoundedPatchStableId: support.StableId), [],
                [new TriangleCell([triangle.A + 1, triangle.B + 1, triangle.C + 1]) { Provenance = SurfaceMeshCellProvenance.ResidualTransition, ExceptionalReason = "bounded local matching transition" }], true,
                "approximate-reconstructed-transition", ChartId: $"fast-transition-{face:D6}",
                PlanarPlannerPath: "native bounded triangular parametric carrier; explicit transition"));
        }
        var topology = Topology(patches.SelectMany(p => p.Cells), vertices);
        var metrics = new SurfaceMeshMetrics(patches.Count, 0, patches.Count, regions.Count, transitions.Length,
            transitions.Length, 0, 0, 0, topology.Min, topology.Max, hash, 0, topology.NonManifold,
            transitions.Length, regions.Select(r => r.AspectRatio).DefaultIfEmpty(0).Max(),
            regions.Select(r => r.NormalDeviationDegrees).DefaultIfEmpty(0).Max());
        var document = new SurfaceMeshDocument(vertices, patches, [], metrics);
        var valid = SurfaceMeshIrValidator.TryValidate(document, out var failure);
        return new(document, structure, topology.Boundary, topology.NonManifold, valid ? "Pass" : "Fail: " + failure);
    }

    private static void ImproveMatching(int[] mate, Dictionary<int, Candidate[]> byFace, int maximumDepth, int maximumAugmentations)
    {
        var augmentations = 0; var changed = true;
        while (changed && augmentations < maximumAugmentations)
        {
            changed = false;
            foreach (var start in Enumerable.Range(0, mate.Length).Where(i => mate[i] < 0))
            {
                var visited = new bool[mate.Length]; visited[start] = true;
                var path = new List<Candidate>();
                if (!Search(start, 0)) continue;
                // Apply removals first and additions second so shared path vertices end paired.
                for (var i = 1; i < path.Count; i += 2) { var c = path[i]; mate[c.A] = mate[c.A] == c.B ? -1 : mate[c.A]; mate[c.B] = mate[c.B] == c.A ? -1 : mate[c.B]; }
                for (var i = 0; i < path.Count; i += 2) { var c = path[i]; mate[c.A] = c.B; mate[c.B] = c.A; }
                augmentations++; changed = true; break;

                bool Search(int current, int depth)
                {
                    foreach (var candidate in byFace.GetValueOrDefault(current) ?? [])
                    {
                        var next = candidate.A == current ? candidate.B : candidate.A;
                        if (visited[next]) continue;
                        visited[next] = true; path.Add(candidate);
                        if (mate[next] < 0) return true;
                        var partner = mate[next]; var matched = byFace[next].First(c => Key(c.A, c.B) == Key(next, partner));
                        path.Add(matched);
                        if (depth < maximumDepth && !visited[partner])
                        {
                            visited[partner] = true;
                            if (Search(partner, depth + 1)) return true;
                            visited[partner] = false;
                        }
                        path.RemoveAt(path.Count - 1); path.RemoveAt(path.Count - 1); visited[next] = false;
                    }
                    return false;
                }
            }
        }
    }

    private static List<Candidate> Candidates(TriangleSurfaceMesh mesh, IReadOnlyList<DifferentialSample> field,
        Dictionary<(int, int), List<int>> edges, double strongFeatureAngleDegrees)
    {
        var result = new List<Candidate>(edges.Count);
        foreach (var item in edges.Where(e => e.Value.Count == 2))
        {
            var a = item.Value[0]; var b = item.Value[1]; if (!TryBoundary(mesh.Triangles[a], mesh.Triangles[b], out var boundary)) continue;
            var points = boundary.Select(v => mesh.Vertices[v]).ToArray(); if (!NonFolded(points)) continue;
            var fieldDegrees = Enumerable.Range(0, 4).Select(i => FieldDeviation(points[(i + 1) % 4] - points[i], field[a])).Average();
            var lengths = Enumerable.Range(0, 4).Select(i => (points[(i + 1) % 4] - points[i]).Length).ToArray();
            var aspect = lengths.Max() / Math.Max(1e-15, lengths.Min());
            var normalDegrees = Math.Acos(Math.Clamp(field[a].Normal.Dot(field[b].Normal), -1, 1)) * 180 / Math.PI;
            if (normalDegrees > strongFeatureAngleDegrees) continue;
            var utility = .5 * (1 - fieldDegrees / 45) + .35 / aspect + .15 * (1 - normalDegrees / 180);
            result.Add(new(Math.Min(a, b), Math.Max(a, b), boundary, utility, fieldDegrees, normalDegrees, aspect));
        }
        return result;
    }

    private static bool TryBoundary(Triangle a, Triangle b, out int[] boundary)
    {
        var directed = a.DirectedEdges().Concat(b.DirectedEdges()).ToArray();
        var counts = directed.GroupBy(e => Key(e.A, e.B)).ToDictionary(g => g.Key, g => g.Count());
        var outer = directed.Where(e => counts[Key(e.A, e.B)] == 1).ToArray();
        if (outer.Length != 4) { boundary = []; return false; }
        var next = outer.ToDictionary(e => e.A, e => e.B); var start = outer.Min(e => e.A); var list = new List<int> { start }; var current = start;
        for (var i = 0; i < 3; i++) { if (!next.TryGetValue(current, out current) || list.Contains(current)) { boundary = []; return false; } list.Add(current); }
        if (!next.TryGetValue(current, out var close) || close != start) { boundary = []; return false; }
        boundary = list.ToArray(); return true;
    }

    private static bool NonFolded(Point3D[] p)
    {
        var normal = (p[1] - p[0]).Cross(p[2] - p[0]) + (p[2] - p[0]).Cross(p[3] - p[0]);
        return normal.Length > 1e-15 && Enumerable.Range(0, 4).All(i =>
            (p[(i + 1) % 4] - p[i]).Cross(p[(i + 2) % 4] - p[(i + 1) % 4]).Dot(normal) >= -1e-12);
    }

    private static double FieldDeviation(Vector3D edge, DifferentialSample sample)
    {
        edge -= sample.Normal * edge.Dot(sample.Normal); if (!edge.TryNormalize(out edge) || !sample.DirectionKnown) return 45;
        var d = Math.Abs(edge.Dot(sample.CrossDirection)); var q = Math.Abs(edge.Dot(sample.Normal.Cross(sample.CrossDirection)));
        return Math.Acos(Math.Clamp(Math.Max(d, q), -1, 1)) * 180 / Math.PI;
    }

    private static Dictionary<(int, int), List<int>> EdgeFaces(TriangleSurfaceMesh mesh)
    {
        var result = new Dictionary<(int, int), List<int>>();
        for (var face = 0; face < mesh.Triangles.Count; face++) foreach (var edge in mesh.Triangles[face].DirectedEdges())
        { var key = Key(edge.A, edge.B); if (!result.TryGetValue(key, out var faces)) result[key] = faces = []; faces.Add(face); }
        return result;
    }

    private static (int Boundary, int NonManifold, double Min, double Max) Topology(IEnumerable<SurfaceMeshCell> cells, IReadOnlyList<SurfaceMeshVertex> vertices)
    {
        var points = vertices.ToDictionary(v => v.Id, v => v.Position); var edges = new Dictionary<(int, int), (int Count, double Length)>();
        foreach (var cell in cells) for (var i = 0; i < cell.VertexIds.Count; i++)
        { var a = cell.VertexIds[i]; var b = cell.VertexIds[(i + 1) % cell.VertexIds.Count]; var key = Key(a, b); edges[key] = (edges.GetValueOrDefault(key).Count + 1, (points[a] - points[b]).Length); }
        return (edges.Count(e => e.Value.Count == 1), edges.Count(e => e.Value.Count > 2), edges.Values.Min(e => e.Length), edges.Values.Max(e => e.Length));
    }

    private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);
}

internal sealed class FastBilinearQuadPatch(string stableId, Point3D[] corners) : ISurfaceMeshBoundedPatch
{
    public string StableId { get; } = stableId;
    public double MinimumU => 0; public double MaximumU => 1; public double MinimumV => 0; public double MaximumV => 1;
    public SurfaceMeshParametricJet Evaluate(double u, double v)
    {
        var p0 = corners[0]; var p1 = corners[1]; var p2 = corners[2]; var p3 = corners[3];
        var point = p0 + (p1 - p0) * (u * (1 - v)) + (p3 - p0) * ((1 - u) * v) + (p2 - p0) * (u * v);
        var du = (p1 - p0) * (1 - v) + (p2 - p3) * v;
        var dv = (p3 - p0) * (1 - u) + (p2 - p1) * u;
        return new(point, du, dv);
    }
    public bool TryProject(Point3D point, out double u, out double v)
    {
        u = v = .5;
        for (var i = 0; i < 8; i++)
        {
            var jet = Evaluate(u, v); var residual = point - jet.Point;
            var aa = jet.Du.Dot(jet.Du); var ab = jet.Du.Dot(jet.Dv); var bb = jet.Dv.Dot(jet.Dv); var det = aa * bb - ab * ab;
            if (Math.Abs(det) < 1e-24) break;
            var ar = jet.Du.Dot(residual); var br = jet.Dv.Dot(residual);
            u = Math.Clamp(u + (bb * ar - ab * br) / det, 0, 1); v = Math.Clamp(v + (aa * br - ab * ar) / det, 0, 1);
        }
        return true;
    }
}

internal sealed class FastTrianglePatch(string stableId, Point3D a, Point3D b, Point3D c) : ISurfaceMeshBoundedPatch
{
    public string StableId { get; } = stableId;
    public double MinimumU => 0; public double MaximumU => 1; public double MinimumV => 0; public double MaximumV => 1;
    public SurfaceMeshParametricJet Evaluate(double u, double v) => new(a + (b - a) * u + (c - a) * v, b - a, c - a);
    public bool TryProject(Point3D point, out double u, out double v) { u = v = 1d / 3; return true; }
}
