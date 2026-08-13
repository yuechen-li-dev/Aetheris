using System.Security.Cryptography;
using System.Text;
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Reconstruction;

public sealed record NearestSurfaceHit(int TriangleIndex, Point3D Point, double Distance, Vector3D Normal, (double A, double B, double C) Barycentric);

/// <summary>Deterministic median-split triangle AABB tree used by every proximity stage.</summary>
public sealed class TriangleBvh
{
    private readonly TriangleSurfaceMesh _mesh; private readonly Node _root;
    private sealed record Node(Bounds3 Bounds, Node? Left, Node? Right, int[] Triangles);
    public TriangleBvh(TriangleSurfaceMesh mesh) { _mesh = mesh; _root = Build(Enumerable.Range(0, mesh.Triangles.Count).ToArray()); }

    public NearestSurfaceHit Nearest(Point3D query)
    {
        var best = double.PositiveInfinity; var bestIndex = -1; var bestPoint = default(Point3D); var bestBary = default((double, double, double));
        Search(_root); if (bestIndex < 0) throw new InvalidOperationException("Cannot query an empty triangle surface.");
        var t = _mesh.Triangles[bestIndex]; var normal = (_mesh.Vertices[t.B] - _mesh.Vertices[t.A]).Cross(_mesh.Vertices[t.C] - _mesh.Vertices[t.A]); if (!normal.TryNormalize(out normal)) normal = new(0, 0, 1);
        return new(bestIndex, bestPoint, double.Sqrt(best), normal, bestBary);
        void Search(Node node)
        {
            if (node.Bounds.DistanceSquared(query) > best) return;
            if (node.Left is null)
            {
                foreach (var index in node.Triangles) { var t = _mesh.Triangles[index]; var hit = Closest(query, _mesh.Vertices[t.A], _mesh.Vertices[t.B], _mesh.Vertices[t.C]); var d = (hit.Point - query).Dot(hit.Point - query); if (d < best || (d == best && index < bestIndex)) { best = d; bestIndex = index; bestPoint = hit.Point; bestBary = hit.Bary; } }
                return;
            }
            var first = node.Left!; var second = node.Right!; if (second.Bounds.DistanceSquared(query) < first.Bounds.DistanceSquared(query)) (first, second) = (second, first); Search(first); Search(second);
        }
    }

    public IReadOnlyList<int> Query(Bounds3 bounds) { var result = new List<int>(); Visit(_root); result.Sort(); return result; void Visit(Node n) { if (!n.Bounds.Intersects(bounds)) return; if (n.Left is null) result.AddRange(n.Triangles); else { Visit(n.Left); Visit(n.Right!); } } }
    private Node Build(int[] indices)
    {
        var bounds = TriangleBounds(indices); if (indices.Length <= 12) return new(bounds, null, null, indices);
        var size = bounds.Size; var axis = size.X >= size.Y && size.X >= size.Z ? 0 : size.Y >= size.Z ? 1 : 2;
        Array.Sort(indices, (a, b) => Coordinate(Centroid(a), axis).CompareTo(Coordinate(Centroid(b), axis))); var middle = indices.Length / 2;
        return new(bounds, Build(indices[..middle]), Build(indices[middle..]), []);
    }
    private Bounds3 TriangleBounds(int[] indices) => Bounds3.From(indices.SelectMany(i => { var t = _mesh.Triangles[i]; return new[] { _mesh.Vertices[t.A], _mesh.Vertices[t.B], _mesh.Vertices[t.C] }; }).ToArray());
    private Point3D Centroid(int i) { var t = _mesh.Triangles[i]; var a = _mesh.Vertices[t.A]; var b = _mesh.Vertices[t.B]; var c = _mesh.Vertices[t.C]; return new((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3, (a.Z + b.Z + c.Z) / 3); }
    private static double Coordinate(Point3D p, int axis) => axis == 0 ? p.X : axis == 1 ? p.Y : p.Z;

    private static (Point3D Point, (double, double, double) Bary) Closest(Point3D p, Point3D a, Point3D b, Point3D c)
    {
        var ab = b - a; var ac = c - a; var ap = p - a; var d1 = ab.Dot(ap); var d2 = ac.Dot(ap); if (d1 <= 0 && d2 <= 0) return (a, (1, 0, 0));
        var bp = p - b; var d3 = ab.Dot(bp); var d4 = ac.Dot(bp); if (d3 >= 0 && d4 <= d3) return (b, (0, 1, 0));
        var vc = d1 * d4 - d3 * d2; if (vc <= 0 && d1 >= 0 && d3 <= 0) { var v = d1 / (d1 - d3); return (a + ab * v, (1 - v, v, 0)); }
        var cp = p - c; var d5 = ab.Dot(cp); var d6 = ac.Dot(cp); if (d6 >= 0 && d5 <= d6) return (c, (0, 0, 1));
        var vb = d5 * d2 - d1 * d6; if (vb <= 0 && d2 >= 0 && d6 <= 0) { var w = d2 / (d2 - d6); return (a + ac * w, (1 - w, 0, w)); }
        var va = d3 * d6 - d5 * d4; if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) { var w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); return (b + (c - b) * w, (0, 1 - w, w)); }
        var denom = 1 / (va + vb + vc); var v2 = vb * denom; var w2 = vc * denom; return (a + ab * v2 + ac * w2, (1 - v2 - w2, v2, w2));
    }
}

public sealed record AnalysisLatticePolicy(int BaseResolution = 24, double SurfaceBandCells = 1.75, double NormalVariationDegrees = 20, int MaximumDepth = 1);
public sealed record AnalysisCell(string StableId, Point3D Center, double HalfSize, Point3D NearestPoint, double Distance, Vector3D Normal, int SourceTriangle, int Level, bool Refined, string EvidenceClass);
public sealed record AnalysisLattice(Bounds3 Bounds, AnalysisLatticePolicy Policy, IReadOnlyList<AnalysisCell> SurfaceBandLeaves, int CandidateCellCount, int RefinedParentCount);

public static class AdaptiveSurfaceAnalyzer
{
    public static AnalysisLattice Build(TriangleSurfaceMesh mesh, TriangleBvh bvh, AnalysisLatticePolicy policy)
    {
        var bounds = mesh.Bounds; var maxSize = double.Max(bounds.Size.X, double.Max(bounds.Size.Y, bounds.Size.Z)); var cell = maxSize / policy.BaseResolution; var padded = bounds.Expand(cell); var leaves = new List<AnalysisCell>(); var candidates = 0; var refined = 0;
        for (var z = 0; z < policy.BaseResolution + 2; z++) for (var y = 0; y < policy.BaseResolution + 2; y++) for (var x = 0; x < policy.BaseResolution + 2; x++)
        {
            var center = new Point3D(padded.Minimum.X + (x + .5) * cell, padded.Minimum.Y + (y + .5) * cell, padded.Minimum.Z + (z + .5) * cell); var hit = bvh.Nearest(center); if (hit.Distance > policy.SurfaceBandCells * cell) continue; candidates++;
            var variation = CornerVariation(center, cell / 2, hit.Normal, bvh); var shouldRefine = policy.MaximumDepth > 0 && variation > policy.NormalVariationDegrees * double.Pi / 180;
            if (!shouldRefine) leaves.Add(new($"cell-0-{x:D3}-{y:D3}-{z:D3}", center, cell / 2, hit.Point, hit.Distance, hit.Normal, hit.TriangleIndex, 0, false, "ToleranceBounded"));
            else { refined++; for (var dz = -1; dz <= 1; dz += 2) for (var dy = -1; dy <= 1; dy += 2) for (var dx = -1; dx <= 1; dx += 2) { var c = center + new Vector3D(dx, dy, dz) * (cell / 4); var h = bvh.Nearest(c); if (h.Distance <= policy.SurfaceBandCells * cell) leaves.Add(new($"cell-1-{x:D3}-{y:D3}-{z:D3}-{dx}-{dy}-{dz}", c, cell / 4, h.Point, h.Distance, h.Normal, h.TriangleIndex, 1, false, "ToleranceBounded")); } }
        }
        return new(padded, policy, leaves, candidates, refined);
    }
    private static double CornerVariation(Point3D c, double h, Vector3D normal, TriangleBvh bvh) { var max = 0d; for (var z = -1; z <= 1; z += 2) for (var y = -1; y <= 1; y += 2) for (var x = -1; x <= 1; x += 2) { var n = bvh.Nearest(c + new Vector3D(x * h, y * h, z * h)).Normal; max = double.Max(max, double.Acos(double.Clamp(double.Abs(normal.Dot(n)), -1, 1))); } return max; }
}

public sealed record DifferentialSample(int TriangleIndex, Point3D Point, Vector3D Normal, Vector3D PrincipalDirection, Vector3D CrossDirection, double CurvatureProxy, double Conditioning, bool DirectionKnown, string EvidenceClass);
public sealed record CrossFieldSummary(int SampleCount, int KnownDirectionCount, int UnknownDirectionCount, int SingularityCount, double MeanNeighborMismatchDegrees, string Representation, string Propagation);
public sealed record RecoveredChart(string StableId, int[] SourceTriangles, Point3D Origin, Vector3D Normal, Vector3D UAxis, Vector3D VAxis, double UMin, double UMax, double VMin, double VMax, double[] HeightCoefficients, double RmsResidual, double MaxResidual, double? AngleDistortionP95, double? AreaDistortionP95, int Foldovers, string Status, BoundedParametricPatch3 Patch);
public sealed record ChartNetwork(IReadOnlyList<RecoveredChart> Charts, IReadOnlyList<RecoveredSeam> Seams, IReadOnlyList<ReconstructionDiagnostic> Diagnostics, IReadOnlyDictionary<string, double> ObjectiveWeights);
public sealed record RecoveredSeam(string StableId, string ChartA, string ChartB, int SourceEdgeCount, double G0Residual, string Authority);

public static class StructuredSurfaceRecovery
{
    public static (IReadOnlyList<DifferentialSample> Samples, CrossFieldSummary Summary) EstimateField(TriangleSurfaceMesh mesh)
    {
        var adjacency = FaceAdjacency(mesh); var samples = new DifferentialSample[mesh.Triangles.Count]; var mismatch = new List<double>();
        for (var i = 0; i < mesh.Triangles.Count; i++)
        {
            var t = mesh.Triangles[i]; var a = mesh.Vertices[t.A]; var b = mesh.Vertices[t.B]; var c = mesh.Vertices[t.C]; var n = (b - a).Cross(c - a); var knownNormal = n.TryNormalize(out n); var edges = new[] { b - a, c - b, a - c }; var d = edges.OrderByDescending(e => e.Length).First(); d -= n * d.Dot(n); var known = knownNormal && d.TryNormalize(out d);
            var neighborNormals = adjacency[i].Select(j => FaceNormal(mesh, j)).Where(v => v.Length > 0).ToArray(); var curvature = neighborNormals.Length == 0 ? 0 : neighborNormals.Average(v => double.Acos(double.Clamp(n.Dot(v), -1, 1))) / double.Max(1e-12, edges.Average(e => e.Length));
            samples[i] = new(i, new((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3, (a.Z + b.Z + c.Z) / 3), n, d, d, curvature, known ? 1 / (1 + neighborNormals.Select(v => double.Acos(double.Clamp(n.Dot(v), -1, 1))).DefaultIfEmpty(0).Average()) : 0, known, "Heuristic");
        }
        // Deterministic four-fold transport: choose the quarter-turn representative closest to the lowest-index visited neighbor.
        var visited = new bool[samples.Length]; foreach (var root in Enumerable.Range(0, samples.Length)) if (!visited[root]) { var queue = new Queue<int>(); queue.Enqueue(root); visited[root] = true; while (queue.Count > 0) { var i = queue.Dequeue(); foreach (var j in adjacency[i].Order()) { if (!samples[j].DirectionKnown) continue; var transported = Project(samples[i].CrossDirection, samples[j].Normal); var candidates = Four(samples[j].PrincipalDirection, samples[j].Normal); var chosen = candidates.OrderByDescending(c => transported.Dot(c)).ThenBy(c => c.X).ThenBy(c => c.Y).ThenBy(c => c.Z).First(); samples[j] = samples[j] with { CrossDirection = chosen }; var angle = double.Acos(double.Clamp(double.Abs(transported.Dot(chosen)), -1, 1)); mismatch.Add(double.Min(angle, double.Abs(double.Pi / 2 - angle))); if (!visited[j]) { visited[j] = true; queue.Enqueue(j); } } } }
        var unknown = samples.Count(s => !s.DirectionKnown); return (samples, new(samples.Length, samples.Length - unknown, unknown, unknown, mismatch.Count == 0 ? 0 : mismatch.Average() * 180 / double.Pi, "unoriented tangent cross {d,-d,nxd,-nxd}", "stable-index breadth-first quarter-turn transport"));
    }

    public static ChartNetwork BuildCharts(TriangleSurfaceMesh mesh, IReadOnlyList<DifferentialSample> field, int spatialBins = 5, int minimumFaces = 8)
    {
        var bounds = mesh.Bounds; var adjacency = FaceAdjacency(mesh); var labels = new (int Axis, int I, int J)[mesh.Triangles.Count];
        for (var f = 0; f < field.Count; f++) { var n = field[f].Normal; var axis = DominantSignedAxis(n); var (u, v) = AxisCoordinates(field[f].Point, axis); var (umin, umax, vmin, vmax) = AxisBounds(bounds, axis); labels[f] = (axis, Bin(u, umin, umax, spatialBins), Bin(v, vmin, vmax, spatialBins)); }
        var groups = new List<List<int>>(); var seen = new bool[mesh.Triangles.Count];
        foreach (var root in Enumerable.Range(0, mesh.Triangles.Count)) if (!seen[root]) { var group = new List<int>(); var q = new Queue<int>(); q.Enqueue(root); seen[root] = true; while (q.Count > 0) { var f = q.Dequeue(); group.Add(f); foreach (var n in adjacency[f].Order()) if (!seen[n] && labels[n] == labels[root]) { seen[n] = true; q.Enqueue(n); } } groups.Add(group); }
        // Deterministic bounded utility growth: undersupported regions choose the neighbor with
        // greatest shared boundary, then lowest normal-label discontinuity, then stable id.
        var parent = Enumerable.Range(0, groups.Count).ToArray(); var faceGroup = new int[mesh.Triangles.Count]; for (var g = 0; g < groups.Count; g++) foreach (var f in groups[g]) faceGroup[f] = g;
        int Root(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        for (var pass = 0; pass < 8; pass++)
        {
            var members = Enumerable.Range(0, groups.Count).GroupBy(Root).ToDictionary(g => g.Key, g => g.SelectMany(i => groups[i]).ToArray()); var changed = false;
            foreach (var small in members.Where(p => p.Value.Length < minimumFaces).OrderBy(p => p.Value.Min()))
            {
                var candidates = new Dictionary<int, int>(); foreach (var f in small.Value) foreach (var n in adjacency[f]) { var r = Root(faceGroup[n]); if (r != Root(small.Key)) candidates[r] = candidates.GetValueOrDefault(r) + 1; }
                if (candidates.Count == 0) continue; var sourceLabel = labels[small.Value.Min()]; var target = candidates.OrderByDescending(p => p.Value).ThenBy(p => labels[members[p.Key].Min()].Axis == sourceLabel.Axis ? 0 : 1).ThenBy(p => members[p.Key].Min()).First().Key; parent[Root(small.Key)] = Root(target); changed = true;
            }
            if (!changed) break;
        }
        var ordered = Enumerable.Range(0, groups.Count).GroupBy(Root).Select(g => g.SelectMany(i => groups[i]).Order().ToList()).OrderBy(g => g.Min()).ToArray(); var charts = new List<RecoveredChart>(); var faceChart = new int[mesh.Triangles.Count]; Array.Fill(faceChart, -1); var diagnostics = new List<ReconstructionDiagnostic>();
        for (var ci = 0; ci < ordered.Length; ci++) { var chart = FitChart(mesh, field, ordered[ci], $"chart-{ci:D4}", minimumFaces); charts.Add(chart); foreach (var f in ordered[ci]) faceChart[f] = ci; if (chart.Status != "Accepted") diagnostics.Add(new(ReconstructionDiagnosticCode.PoorLocalSupport, "Warning", $"{chart.StableId}: {chart.Status}", ordered[ci].Min())); }
        var seamCounts = new SortedDictionary<(int, int), int>(); foreach (var (a, neighbors) in adjacency.Select((n, i) => (i, n))) foreach (var b in neighbors) if (a < b && faceChart[a] != faceChart[b]) { var key = faceChart[a] < faceChart[b] ? (faceChart[a], faceChart[b]) : (faceChart[b], faceChart[a]); seamCounts[key] = seamCounts.GetValueOrDefault(key) + 1; }
        var seams = seamCounts.Select((p, i) => new RecoveredSeam($"seam-{i:D4}", charts[p.Key.Item1].StableId, charts[p.Key.Item2].StableId, p.Value, 0, "single source-evidence seam polyline; neighboring fitted boundaries require future reconciliation")).ToArray();
        return new(charts, seams, diagnostics, new Dictionary<string, double> { ["normalDiscontinuity"] = 4, ["fieldMismatch"] = 2, ["spatialCompactness"] = 1, ["panelCountPenalty"] = .05, ["poorSupportPenalty"] = 10 });
    }

    private static RecoveredChart FitChart(TriangleSurfaceMesh mesh, IReadOnlyList<DifferentialSample> field, List<int> faces, string id, int minimumFaces)
    {
        var normal = faces.Select(f => field[f].Normal).Aggregate(new Vector3D(0, 0, 0), (a, b) => a + b); if (!normal.TryNormalize(out normal)) normal = new(0, 0, 1);
        var uAxis = Project(field[faces.Min()].CrossDirection, normal); if (!uAxis.TryNormalize(out uAxis)) uAxis = double.Abs(normal.X) < .9 ? normal.Cross(new(1, 0, 0)) : normal.Cross(new(0, 1, 0)); uAxis.TryNormalize(out uAxis); var vAxis = normal.Cross(uAxis); vAxis.TryNormalize(out vAxis);
        var points = faces.SelectMany(f => { var t = mesh.Triangles[f]; return new[] { mesh.Vertices[t.A], mesh.Vertices[t.B], mesh.Vertices[t.C] }; }).Distinct().ToArray(); var origin = new Point3D(points.Average(p => p.X), points.Average(p => p.Y), points.Average(p => p.Z));
        var local = points.Select(p => { var d = p - origin; return (U: d.Dot(uAxis), V: d.Dot(vAxis), H: d.Dot(normal)); }).ToArray(); var umin = local.Min(p => p.U); var umax = local.Max(p => p.U); var vmin = local.Min(p => p.V); var vmax = local.Max(p => p.V); if (umax - umin < 1e-12) umax = umin + 1e-12; if (vmax - vmin < 1e-12) vmax = vmin + 1e-12;
        var coefficients = LeastSquares(local); var residuals = local.Select(p => double.Abs(p.H - Height(coefficients, p.U, p.V))).ToArray(); var patch = CreatePatch(id, origin, uAxis, vAxis, normal, umin, umax, vmin, vmax, coefficients);
        var status = faces.Count < minimumFaces ? "PoorLocalSupport" : "Accepted"; return new(id, faces.Order().ToArray(), origin, normal, uAxis, vAxis, umin, umax, vmin, vmax, coefficients, double.Sqrt(residuals.Average(r => r * r)), residuals.Max(), null, null, 0, status, patch);
    }

    private static BoundedParametricPatch3 CreatePatch(string id, Point3D o, Vector3D eu, Vector3D ev, Vector3D n, double umin, double umax, double vmin, double vmax, double[] c)
    {
        var u = SurfaceExpression.Add(SurfaceExpression.Length(umin), SurfaceExpression.Multiply(SurfaceExpression.Length(umax - umin), SurfaceExpression.U)); var v = SurfaceExpression.Add(SurfaceExpression.Length(vmin), SurfaceExpression.Multiply(SurfaceExpression.Length(vmax - vmin), SurfaceExpression.V));
        var h = SurfaceExpression.Add(SurfaceExpression.Length(c[0]), SurfaceExpression.Add(SurfaceExpression.Multiply(SurfaceExpression.Number(c[1]), u), SurfaceExpression.Add(SurfaceExpression.Multiply(SurfaceExpression.Number(c[2]), v), SurfaceExpression.Add(SurfaceExpression.Multiply(SurfaceExpression.Divide(SurfaceExpression.Number(c[3]), SurfaceExpression.Length(1)), SurfaceExpression.Power(u, 2)), SurfaceExpression.Add(SurfaceExpression.Multiply(SurfaceExpression.Divide(SurfaceExpression.Number(c[4]), SurfaceExpression.Length(1)), SurfaceExpression.Multiply(u, v)), SurfaceExpression.Multiply(SurfaceExpression.Divide(SurfaceExpression.Number(c[5]), SurfaceExpression.Length(1)), SurfaceExpression.Power(v, 2)))))));
        SurfaceScalarExpression Component(double origin, double a, double b, double nn) => SurfaceExpression.Add(SurfaceExpression.Length(origin), SurfaceExpression.Add(SurfaceExpression.Multiply(SurfaceExpression.Number(a), u), SurfaceExpression.Add(SurfaceExpression.Multiply(SurfaceExpression.Number(b), v), SurfaceExpression.Multiply(SurfaceExpression.Number(nn), h))));
        return new(id, new(new(0, 1), new(0, 1)), new(Component(o.X, eu.X, ev.X, n.X), Component(o.Y, eu.Y, ev.Y, n.Y), Component(o.Z, eu.Z, ev.Z, n.Z)), $"quadratic least-squares fit from source triangles:{string.Join(',', id)}", GeometryRepresentationKind.SampledApproximation);
    }

    private static double[] LeastSquares((double U, double V, double H)[] points)
    {
        const int n = 6; var a = new double[n, n]; var b = new double[n]; foreach (var p in points) { var row = new[] { 1d, p.U, p.V, p.U * p.U, p.U * p.V, p.V * p.V }; for (var i = 0; i < n; i++) { b[i] += row[i] * p.H; for (var j = 0; j < n; j++) a[i, j] += row[i] * row[j]; } } for (var i = 0; i < n; i++) a[i, i] += 1e-16;
        for (var k = 0; k < n; k++) { var pivot = Enumerable.Range(k, n - k).OrderByDescending(i => double.Abs(a[i, k])).First(); if (pivot != k) { for (var j = k; j < n; j++) (a[k, j], a[pivot, j]) = (a[pivot, j], a[k, j]); (b[k], b[pivot]) = (b[pivot], b[k]); } var d = a[k, k]; if (double.Abs(d) < 1e-30) continue; for (var j = k; j < n; j++) a[k, j] /= d; b[k] /= d; for (var i = 0; i < n; i++) if (i != k) { var q = a[i, k]; for (var j = k; j < n; j++) a[i, j] -= q * a[k, j]; b[i] -= q * b[k]; } } return b;
    }
    private static double Height(double[] c, double u, double v) => c[0] + c[1] * u + c[2] * v + c[3] * u * u + c[4] * u * v + c[5] * v * v;
    private static List<int>[] FaceAdjacency(TriangleSurfaceMesh mesh) { var result = Enumerable.Range(0, mesh.Triangles.Count).Select(_ => new List<int>()).ToArray(); var edges = new Dictionary<(int, int), int>(); for (var f = 0; f < mesh.Triangles.Count; f++) foreach (var e in mesh.Triangles[f].DirectedEdges()) { var key = e.A < e.B ? e : (e.B, e.A); if (edges.TryGetValue(key, out var other)) { result[f].Add(other); result[other].Add(f); } else edges[key] = f; } return result; }
    private static Vector3D FaceNormal(TriangleSurfaceMesh mesh, int f) { var t = mesh.Triangles[f]; var n = (mesh.Vertices[t.B] - mesh.Vertices[t.A]).Cross(mesh.Vertices[t.C] - mesh.Vertices[t.A]); return n.TryNormalize(out n) ? n : new(0, 0, 0); }
    private static Vector3D Project(Vector3D d, Vector3D n) { var p = d - n * d.Dot(n); return p.TryNormalize(out p) ? p : d; }
    private static Vector3D[] Four(Vector3D d, Vector3D n) { var p = n.Cross(d); p.TryNormalize(out p); return [d, d * -1, p, p * -1]; }
    private static int DominantSignedAxis(Vector3D n) { var ax = double.Abs(n.X); var ay = double.Abs(n.Y); var az = double.Abs(n.Z); return ax >= ay && ax >= az ? (n.X >= 0 ? 0 : 1) : ay >= az ? (n.Y >= 0 ? 2 : 3) : (n.Z >= 0 ? 4 : 5); }
    private static (double U, double V) AxisCoordinates(Point3D p, int axis) => (axis / 2) switch { 0 => (p.Y, p.Z), 1 => (p.X, p.Z), _ => (p.X, p.Y) };
    private static (double, double, double, double) AxisBounds(Bounds3 b, int axis) => (axis / 2) switch { 0 => (b.Minimum.Y, b.Maximum.Y, b.Minimum.Z, b.Maximum.Z), 1 => (b.Minimum.X, b.Maximum.X, b.Minimum.Z, b.Maximum.Z), _ => (b.Minimum.X, b.Maximum.X, b.Minimum.Y, b.Maximum.Y) };
    private static int Bin(double x, double min, double max, int count) => int.Clamp((int)((x - min) / double.Max(1e-30, max - min) * count), 0, count - 1);
}

public sealed record StructuredVertex(int Id, Point3D Point, string ChartId, double U, double V);
public sealed record StructuredQuad(int A, int B, int C, int D, string ChartId);
public sealed record StructuredSurfaceMesh(IReadOnlyList<StructuredVertex> Vertices, IReadOnlyList<StructuredQuad> Quads, int TriangleCount, int NgonCount, string DeterministicHash, int BoundaryEdgeCount, int NonManifoldEdgeCount, int CrackCount);

public static class PanelSurfaceMeshLowering
{
    public static StructuredSurfaceMesh Lower(ChartNetwork network, int segments = 6)
    {
        var vertices = new List<StructuredVertex>(); var quads = new List<StructuredQuad>();
        foreach (var chart in network.Charts.Where(c => c.Status == "Accepted").OrderBy(c => c.StableId))
        {
            var offset = vertices.Count; for (var j = 0; j <= segments; j++) for (var i = 0; i <= segments; i++) { var u = i / (double)segments; var v = j / (double)segments; vertices.Add(new(vertices.Count, chart.Patch.EvaluatePoint(u, v), chart.StableId, u, v)); }
            for (var j = 0; j < segments; j++) for (var i = 0; i < segments; i++) { var a = offset + j * (segments + 1) + i; quads.Add(new(a, a + 1, a + segments + 2, a + segments + 1, chart.StableId)); }
        }
        var edges = new Dictionary<(string, long, long, long, long, long, long), int>(); foreach (var q in quads) foreach (var (a, b) in new[] { (q.A, q.B), (q.B, q.C), (q.C, q.D), (q.D, q.A) }) { var p = vertices[a].Point; var r = vertices[b].Point; var x = Key(p); var y = Key(r); var key = string.CompareOrdinal(x.Item1, y.Item1) <= 0 ? (q.ChartId, x.Item2, x.Item3, x.Item4, y.Item2, y.Item3, y.Item4) : (q.ChartId, y.Item2, y.Item3, y.Item4, x.Item2, x.Item3, x.Item4); edges[key] = edges.GetValueOrDefault(key) + 1; }
        var canonical = string.Join('|', vertices.Select(v => $"{v.Point.X:R},{v.Point.Y:R},{v.Point.Z:R}")) + ";" + string.Join('|', quads.Select(q => $"{q.A},{q.B},{q.C},{q.D}")); var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new(vertices, quads, 0, 0, hash, edges.Count(e => e.Value == 1), edges.Count(e => e.Value > 2), network.Seams.Count);
        static (string, long, long, long) Key(Point3D p) => (FormattableString.Invariant($"{p.X:R},{p.Y:R},{p.Z:R}"), (long)double.Round(p.X * 1e12), (long)double.Round(p.Y * 1e12), (long)double.Round(p.Z * 1e12));
    }
}

public sealed record DistributionMetrics(double Mean, double Maximum, double Rms, double P50, double P90, double P95, double P99);
public sealed record ReconstructionErrorReport(DistributionMetrics SourceToPanels, DistributionMetrics RemeshToSource, double SampledBidirectionalHausdorff, DistributionMetrics NormalAngleDegrees, string EvidenceClass);
public static class ReconstructionErrorEvaluator
{
    public static ReconstructionErrorReport Evaluate(TriangleSurfaceMesh source, TriangleBvh bvh, ChartNetwork charts, StructuredSurfaceMesh remesh)
    {
        var sourceErrors = new List<double>(); var normalErrors = new List<double>(); foreach (var chart in charts.Charts) foreach (var f in chart.SourceTriangles) { var t = source.Triangles[f]; var a = source.Vertices[t.A]; var b = source.Vertices[t.B]; var c = source.Vertices[t.C]; var actual = new Point3D((a.X+b.X+c.X)/3,(a.Y+b.Y+c.Y)/3,(a.Z+b.Z+c.Z)/3); var d=actual-chart.Origin; var u=double.Clamp((d.Dot(chart.UAxis)-chart.UMin)/(chart.UMax-chart.UMin),0,1); var v=double.Clamp((d.Dot(chart.VAxis)-chart.VMin)/(chart.VMax-chart.VMin),0,1); var sample = chart.Patch.Evaluate(u, v); sourceErrors.Add((actual - sample.Point).Length); if (sample.Normal is { } n) normalErrors.Add(double.Acos(double.Clamp(double.Abs(n.ToVector().Dot(FaceNormal(source, f))), -1, 1)) * 180 / double.Pi); }
        var reverse = remesh.Vertices.Select(v => bvh.Nearest(v.Point).Distance).ToArray(); var forwardMetrics = Dist(sourceErrors); var reverseMetrics = Dist(reverse); return new(forwardMetrics, reverseMetrics, double.Max(forwardMetrics.Maximum, reverseMetrics.Maximum), Dist(normalErrors), "Sampled");
    }
    private static Vector3D FaceNormal(TriangleSurfaceMesh m, int f) { var t = m.Triangles[f]; var n = (m.Vertices[t.B] - m.Vertices[t.A]).Cross(m.Vertices[t.C] - m.Vertices[t.A]); return n.TryNormalize(out n) ? n : new(0, 0, 0); }
    private static DistributionMetrics Dist(IEnumerable<double> input) { var v = input.Order().ToArray(); if (v.Length == 0) return new(0, 0, 0, 0, 0, 0, 0); double P(double p) => v[(int)double.Clamp(double.Ceiling(p * v.Length) - 1, 0, v.Length - 1)]; return new(v.Average(), v[^1], double.Sqrt(v.Average(x => x * x)), P(.5), P(.9), P(.95), P(.99)); }
}
