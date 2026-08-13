using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Reconstruction;

/// <summary>Open, possibly defective triangle-surface evidence. Unlike the export TriangleMesh, this is not required to be closed or authoritative.</summary>
public sealed record TriangleSurfaceMesh(
    IReadOnlyList<Point3D> Vertices,
    IReadOnlyList<Triangle> Triangles,
    IReadOnlyList<Vector3D>? SuppliedNormals,
    string SourceIdentity,
    string DeterministicHash,
    IReadOnlyDictionary<string, string> Provenance)
{
    public Bounds3 Bounds => Bounds3.From(Vertices);
}

public readonly record struct Triangle(int A, int B, int C)
{
    public IEnumerable<(int A, int B)> DirectedEdges()
    {
        yield return (A, B); yield return (B, C); yield return (C, A);
    }
}

public readonly record struct Bounds3(Point3D Minimum, Point3D Maximum)
{
    public Vector3D Size => Maximum - Minimum;
    public Point3D Center => new((Minimum.X + Maximum.X) / 2, (Minimum.Y + Maximum.Y) / 2, (Minimum.Z + Maximum.Z) / 2);
    public static Bounds3 From(IReadOnlyList<Point3D> points)
    {
        if (points.Count == 0) return new(new(0, 0, 0), new(0, 0, 0));
        return new(new(points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)),
            new(points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z)));
    }
    public Bounds3 Expand(double amount) => new(new(Minimum.X - amount, Minimum.Y - amount, Minimum.Z - amount), new(Maximum.X + amount, Maximum.Y + amount, Maximum.Z + amount));
    public double DistanceSquared(Point3D p)
    {
        var dx = double.Max(0, double.Max(Minimum.X - p.X, p.X - Maximum.X));
        var dy = double.Max(0, double.Max(Minimum.Y - p.Y, p.Y - Maximum.Y));
        var dz = double.Max(0, double.Max(Minimum.Z - p.Z, p.Z - Maximum.Z));
        return dx * dx + dy * dy + dz * dz;
    }
    public bool Intersects(Bounds3 other) => Minimum.X <= other.Maximum.X && Maximum.X >= other.Minimum.X && Minimum.Y <= other.Maximum.Y && Maximum.Y >= other.Minimum.Y && Minimum.Z <= other.Maximum.Z && Maximum.Z >= other.Minimum.Z;
}

public static class PlyTriangleSurfaceLoader
{
    public static TriangleSurfaceMesh LoadAscii(Stream stream, string sourceIdentity, IReadOnlyDictionary<string, string>? provenance = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var reader = new StreamReader(stream, Encoding.ASCII, false, 64 * 1024, leaveOpen: true);
        if (reader.ReadLine() != "ply") throw new InvalidDataException("Not a PLY stream.");
        var vertexCount = -1; var faceCount = -1; var format = ""; var vertexProperties = new List<string>(); var inVertex = false;
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            if (parts[0] == "format") format = parts[1];
            else if (parts[0] == "element") { inVertex = parts[1] == "vertex"; if (inVertex) vertexCount = int.Parse(parts[2], CultureInfo.InvariantCulture); else if (parts[1] == "face") faceCount = int.Parse(parts[2], CultureInfo.InvariantCulture); }
            else if (parts[0] == "property" && inVertex) vertexProperties.Add(parts[^1]);
            else if (parts[0] == "end_header") break;
        }
        if (format != "ascii" || vertexCount < 0 || faceCount < 0) throw new NotSupportedException("The bounded M0 loader supports ASCII PLY with vertex and face elements.");
        var ix = vertexProperties.IndexOf("x"); var iy = vertexProperties.IndexOf("y"); var iz = vertexProperties.IndexOf("z");
        if (ix < 0 || iy < 0 || iz < 0) throw new InvalidDataException("PLY vertex positions x/y/z are required.");
        var vertices = new Point3D[vertexCount];
        for (var i = 0; i < vertexCount; i++)
        {
            var values = (reader.ReadLine() ?? throw new EndOfStreamException()).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            vertices[i] = new(double.Parse(values[ix], CultureInfo.InvariantCulture), double.Parse(values[iy], CultureInfo.InvariantCulture), double.Parse(values[iz], CultureInfo.InvariantCulture));
        }
        var triangles = new List<Triangle>(faceCount);
        for (var i = 0; i < faceCount; i++)
        {
            var values = (reader.ReadLine() ?? throw new EndOfStreamException()).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var count = int.Parse(values[0], CultureInfo.InvariantCulture);
            if (count != 3) throw new NotSupportedException($"PLY face {i} has {count} vertices; M0 accepts triangle surfaces only.");
            triangles.Add(new(int.Parse(values[1], CultureInfo.InvariantCulture), int.Parse(values[2], CultureInfo.InvariantCulture), int.Parse(values[3], CultureInfo.InvariantCulture)));
        }
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', vertices.Select(p => $"{p.X:R},{p.Y:R},{p.Z:R}")) + ";" + string.Join('|', triangles.Select(t => $"{t.A},{t.B},{t.C}"))));
        return new(vertices, triangles, null, sourceIdentity, Convert.ToHexString(hashBytes).ToLowerInvariant(), provenance ?? new Dictionary<string, string>());
    }
}

public sealed record MeshValidationReport(
    int VertexCount, int TriangleCount, int ConnectedComponents, int BoundaryEdgeCount, int BoundaryLoopCount,
    int NonManifoldEdgeCount, int DegenerateTriangleCount, int DuplicateTriangleCount, int InconsistentlyOrientedEdgeCount,
    int NonFiniteVertexCount, double SurfaceArea, double SignedVolume, Bounds3 Bounds, bool OrientationConsistent,
    IReadOnlyList<IReadOnlyList<int>> BoundaryLoops, IReadOnlyList<ReconstructionDiagnostic> Diagnostics);

public enum ReconstructionDiagnosticCode { NonFiniteVertex, InvalidIndex, DegenerateTriangle, DuplicateTriangle, NonManifoldEdge, InconsistentOrientation, OpenBoundary, PoorLocalSupport, AmbiguousDirection, ChartFoldover, ExcessiveDistortion, FitResidualExceeded }
public sealed record ReconstructionDiagnostic(ReconstructionDiagnosticCode Code, string Severity, string Message, int? SourceElement = null, string EvidenceClass = "Sampled");

public static class TriangleSurfaceValidator
{
    private const double Epsilon = 1e-20;
    public static MeshValidationReport Validate(TriangleSurfaceMesh mesh)
    {
        var diagnostics = new List<ReconstructionDiagnostic>(); var edges = new Dictionary<(int, int), List<(int From, int To, int Face)>>();
        var duplicateSet = new HashSet<(int, int, int)>(); var duplicates = 0; var degenerate = 0; var area = 0d; var volume = 0d;
        var validFaces = new List<int>();
        for (var i = 0; i < mesh.Vertices.Count; i++) if (!Finite(mesh.Vertices[i])) diagnostics.Add(new(ReconstructionDiagnosticCode.NonFiniteVertex, "Error", $"Vertex {i} is non-finite.", i));
        for (var fi = 0; fi < mesh.Triangles.Count; fi++)
        {
            var t = mesh.Triangles[fi];
            if (t.A < 0 || t.B < 0 || t.C < 0 || t.A >= mesh.Vertices.Count || t.B >= mesh.Vertices.Count || t.C >= mesh.Vertices.Count) { diagnostics.Add(new(ReconstructionDiagnosticCode.InvalidIndex, "Error", $"Triangle {fi} has an invalid index.", fi)); continue; }
            validFaces.Add(fi); var ids = new[] { t.A, t.B, t.C }; Array.Sort(ids); if (!duplicateSet.Add((ids[0], ids[1], ids[2]))) { duplicates++; diagnostics.Add(new(ReconstructionDiagnosticCode.DuplicateTriangle, "Warning", $"Triangle {fi} duplicates another triangle.", fi)); }
            var a = mesh.Vertices[t.A]; var b = mesh.Vertices[t.B]; var c = mesh.Vertices[t.C]; var cross = (b - a).Cross(c - a); var twiceArea = cross.Length;
            if (twiceArea <= Epsilon) { degenerate++; diagnostics.Add(new(ReconstructionDiagnosticCode.DegenerateTriangle, "Warning", $"Triangle {fi} has zero area.", fi)); }
            area += twiceArea / 2; volume += new Vector3D(a.X, a.Y, a.Z).Dot(new Vector3D(b.X, b.Y, b.Z).Cross(new Vector3D(c.X, c.Y, c.Z))) / 6;
            foreach (var e in t.DirectedEdges()) { var key = e.A < e.B ? (e.A, e.B) : (e.B, e.A); if (!edges.TryGetValue(key, out var uses)) edges[key] = uses = []; uses.Add((e.A, e.B, fi)); }
        }
        var boundary = edges.Where(e => e.Value.Count == 1).ToArray(); var nonManifold = edges.Count(e => e.Value.Count > 2); var inconsistent = edges.Count(e => e.Value.Count == 2 && e.Value[0].From == e.Value[1].From);
        if (boundary.Length > 0) diagnostics.Add(new(ReconstructionDiagnosticCode.OpenBoundary, "Evidence", $"Source has {boundary.Length} open boundary edges."));
        if (nonManifold > 0) diagnostics.Add(new(ReconstructionDiagnosticCode.NonManifoldEdge, "Warning", $"Source has {nonManifold} non-manifold edges."));
        if (inconsistent > 0) diagnostics.Add(new(ReconstructionDiagnosticCode.InconsistentOrientation, "Warning", $"Source has {inconsistent} inconsistently oriented shared edges."));
        var loops = BuildBoundaryLoops(boundary.Select(e => e.Key));
        return new(mesh.Vertices.Count, mesh.Triangles.Count, CountComponents(mesh, validFaces, edges), boundary.Length, loops.Count, nonManifold, degenerate, duplicates, inconsistent,
            diagnostics.Count(d => d.Code == ReconstructionDiagnosticCode.NonFiniteVertex), area, volume, mesh.Bounds, inconsistent == 0, loops, diagnostics);
    }

    private static List<IReadOnlyList<int>> BuildBoundaryLoops(IEnumerable<(int A, int B)> input)
    {
        var unused = new HashSet<(int, int)>(input.Select(e => e.A < e.B ? e : (e.B, e.A))); var adjacency = new Dictionary<int, SortedSet<int>>();
        foreach (var (a, b) in unused) { if (!adjacency.TryGetValue(a, out var aa)) adjacency[a] = aa = []; aa.Add(b); if (!adjacency.TryGetValue(b, out var bb)) adjacency[b] = bb = []; bb.Add(a); }
        var loops = new List<IReadOnlyList<int>>();
        while (unused.Count > 0)
        {
            var first = unused.OrderBy(e => e.Item1).ThenBy(e => e.Item2).First(); var path = new List<int> { first.Item1 }; var previous = -1; var current = first.Item1;
            while (true)
            {
                var next = adjacency[current].Where(n => n != previous && unused.Contains(current < n ? (current, n) : (n, current))).DefaultIfEmpty(-1).First();
                if (next < 0) break; unused.Remove(current < next ? (current, next) : (next, current)); path.Add(next); previous = current; current = next; if (current == path[0]) break;
            }
            loops.Add(path);
        }
        return loops;
    }

    private static int CountComponents(TriangleSurfaceMesh mesh, IReadOnlyList<int> faces, Dictionary<(int, int), List<(int From, int To, int Face)>> edges)
    {
        var adjacent = faces.ToDictionary(f => f, _ => new List<int>()); foreach (var uses in edges.Values) for (var i = 0; i < uses.Count; i++) for (var j = i + 1; j < uses.Count; j++) { adjacent[uses[i].Face].Add(uses[j].Face); adjacent[uses[j].Face].Add(uses[i].Face); }
        var seen = new HashSet<int>(); var count = 0; foreach (var face in faces) if (seen.Add(face)) { count++; var stack = new Stack<int>(); stack.Push(face); while (stack.Count > 0) foreach (var n in adjacent[stack.Pop()]) if (seen.Add(n)) stack.Push(n); } return count;
    }
    private static bool Finite(Point3D p) => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
}
