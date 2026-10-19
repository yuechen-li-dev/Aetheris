using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>
/// Exact construction for the five regular convex stocks and the all-edge rolling
/// ball finish on those stocks or a Box. This is deliberately not a general BRep
/// fillet: the original convex face lattice is the construction authority.
/// </summary>
public static class ConvexStockPrimitives
{
    public static bool IsRegularFaceCount(int count) => count is 4 or 6 or 8 or 12 or 20;

    public static KernelResult<BrepBody> CreateRegular(int faceCount, double edgeLength, double filletRadius = 0)
    {
        if (!IsRegularFaceCount(faceCount)) return Failure("PolyhedronFaceCountUnsupported: Regular convex Polyhedron<N> admits 4, 6, 8, 12 or 20 faces.");
        if (!Positive(edgeLength)) return Failure("PolyhedronEdgeLengthInvalid: edgeLength must be finite and positive.");
        var phi = (1 + double.Sqrt(5)) / 2;
        var vertices = new List<Point3D>();
        int[] signs = [-1, 1];
        switch (faceCount)
        {
            case 4: vertices.AddRange([new(1, 1, 1), new(1, -1, -1), new(-1, 1, -1), new(-1, -1, 1)]); break;
            case 6:
                foreach (var x in signs) foreach (var y in signs) foreach (var z in signs) vertices.Add(new(x, y, z));
                break;
            case 8:
                vertices.AddRange([new(1, 0, 0), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0), new(0, 0, 1), new(0, 0, -1)]);
                break;
            case 12:
                foreach (var x in signs) foreach (var y in signs) foreach (var z in signs) vertices.Add(new(x, y, z));
                foreach (var a in signs) foreach (var b in signs)
                    vertices.AddRange([new(0, a / phi, b * phi), new(a / phi, b * phi, 0), new(b * phi, 0, a / phi)]);
                break;
            case 20:
                foreach (var a in signs) foreach (var b in signs)
                    vertices.AddRange([new(0, a, b * phi), new(a, b * phi, 0), new(b * phi, 0, a)]);
                break;
        }
        var length = vertices.SelectMany((p, i) => vertices.Skip(i + 1).Select(q => (p - q).Length)).Min();
        var scale = edgeLength / length;
        return Build(vertices.Select(p => new Point3D(p.X * scale, p.Y * scale, p.Z * scale)).ToArray(), filletRadius, faceCount);
    }

    /// <summary>Kernel-centered Box; Firmament applies its existing bottom-on-XY stock frame.</summary>
    public static KernelResult<BrepBody> CreateRoundedBox(double x, double y, double z, double radius)
    {
        if (!Positive(x) || !Positive(y) || !Positive(z)) return Failure("BoxSizeInvalid: dimensions must be finite and positive.");
        if (!Positive(radius)) return Failure("UniformFilletRadiusInvalid: radius must be finite and positive.");
        var points = new List<Point3D>();
        foreach (var a in new[] { -1, 1 }) foreach (var b in new[] { -1, 1 }) foreach (var c in new[] { -1, 1 })
            points.Add(new(a * x / 2, b * y / 2, c * z / 2));
        return Build(points, radius, 6);
    }

    private sealed record StockFace(int[] Vertices, Vector3D Normal, double Offset);
    private readonly record struct Use(EdgeId Edge, bool Reverse);

    private static KernelResult<BrepBody> Build(IReadOnlyList<Point3D> points, double radius, int expectedFaces)
    {
        if (!double.IsFinite(radius) || radius < 0) return Failure("UniformFilletRadiusInvalid: radius must be finite and positive.");
        var extent = points.Max(p => (p - Point3D.Origin).Length);
        if (!double.IsFinite(extent) || extent < 1e-7) return Failure("ConvexStockSizeUnsupported: stock is below the kernel construction tolerance or exceeds finite range.");
        var tolerance = System.Math.Max(1e-9, extent * 1e-10);
        var faces = Hull(points, tolerance);
        if (faces.Count != expectedFaces) return Failure("ConvexStockHullInvalid: the regular stock did not produce its expected closed face lattice.");
        var limit = faces.Min(f => f.Offset);
        if (radius > 0 && radius >= limit - tolerance)
            return Failure(FormattableString.Invariant($"UniformFilletRadiusTooLarge: radius {radius:R}mm must be smaller than the stock inradius {limit:R}mm, leaving positive planar faces and edge spans."));
        if (radius > 0 && radius <= tolerance) return Failure("UniformFilletRadiusInvalid: radius is below the stock construction tolerance.");

        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var positions = new Dictionary<VertexId, Point3D>();
        var resultFaces = new List<FaceId>();
        var contact = new Dictionary<(int Vertex, int Face), VertexId>();
        var lines = new Dictionary<(int A, int B, int Face), EdgeId>();
        var arcs = new Dictionary<(int Vertex, int A, int B), EdgeId>();
        var incident = Enumerable.Range(0, points.Count).Select(v => Enumerable.Range(0, faces.Count).Where(f => faces[f].Vertices.Contains(v)).ToArray()).ToArray();
        var inner = radius == 0 ? points.ToArray() : Enumerable.Range(0, points.Count).Select(v => OffsetVertex(incident[v], faces, radius)).ToArray();
        if (inner.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Z)))
            return Failure("ConvexStockOffsetInvalid: the stock's incident planes do not have a finite common inward offset.");

        VertexId Vertex(int v, int f)
        {
            var key = (v, radius == 0 ? -1 : f);
            if (contact.TryGetValue(key, out var found)) return found;
            var id = builder.AddVertex();
            positions[id] = inner[v] + faces[f].Normal * radius;
            contact.Add(key, id); return id;
        }
        EdgeId Curve(VertexId a, VertexId b, CurveGeometry curve, double end)
        {
            var edge = builder.AddEdge(a, b); var id = new CurveGeometryId(geometry.Curves.Count() + 1);
            geometry.AddCurve(id, curve); bindings.AddEdgeBinding(new(edge, id, new(0, end))); return edge;
        }
        Use Line(int a, int b, int f)
        {
            var key = (System.Math.Min(a, b), System.Math.Max(a, b), radius == 0 ? -1 : f);
            if (!lines.TryGetValue(key, out var edge))
            {
                var start = Vertex(key.Item1, f); var end = Vertex(key.Item2, f); var vector = positions[end] - positions[start];
                edge = Curve(start, end, CurveGeometry.FromLine(new(positions[start], Direction3D.Create(vector))), vector.Length);
                lines.Add(key, edge);
            }
            return new(edge, a > b);
        }
        Use Arc(int v, int a, int b)
        {
            var key = (v, System.Math.Min(a, b), System.Math.Max(a, b));
            if (!arcs.TryGetValue(key, out var edge))
            {
                var n = faces[key.Item2].Normal; var m = faces[key.Item3].Normal;
                edge = Curve(Vertex(v, key.Item2), Vertex(v, key.Item3),
                    CurveGeometry.FromCircle(new(inner[v], Direction3D.Create(n.Cross(m)), radius, Direction3D.Create(n))),
                    double.Acos(double.Clamp(n.Dot(m), -1, 1)));
                arcs.Add(key, edge);
            }
            return new(edge, a > b);
        }
        void Face(IReadOnlyList<Use> uses, SurfaceGeometry surface, Vector3D outward)
        {
            // Orient the shared coedges from material-outward winding, independently
            // of edge allocation order. Curves/surfaces remain exact analytic supports.
            var polygon = uses.Select(u => positions[u.Reverse ? builder.Model.GetEdge(u.Edge).EndVertexId : builder.Model.GetEdge(u.Edge).StartVertexId]).ToArray();
            var area = Vector3D.Zero;
            for (var i = 0; i < polygon.Length; i++) area += (polygon[i] - polygon[0]).Cross(polygon[(i + 1) % polygon.Length] - polygon[0]);
            if (area.Dot(outward) < 0) uses = uses.Reverse().Select(u => new Use(u.Edge, !u.Reverse)).ToArray();
            var loop = builder.AllocateLoopId(); var ids = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
            for (var i = 0; i < ids.Length; i++) builder.AddCoedge(new(ids[i], uses[i].Edge, loop, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reverse));
            builder.AddLoop(new Loop(loop, ids)); var face = builder.AddFace([loop]); resultFaces.Add(face);
            var id = new SurfaceGeometryId(geometry.Surfaces.Count() + 1); geometry.AddSurface(id, surface); bindings.AddFaceBinding(new(face, id));
        }
        for (var f = 0; f < faces.Count; f++)
        {
            var face = faces[f]; var n = Direction3D.Create(face.Normal); var x = Direction3D.Create(points[face.Vertices[1]] - points[face.Vertices[0]]);
            Face(face.Vertices.Select((v, i) => Line(v, face.Vertices[(i + 1) % face.Vertices.Length], f)).ToArray(),
                SurfaceGeometry.FromPlane(new(points[face.Vertices[0]], n, x)), face.Normal);
        }
        if (radius > 0)
        {
            var edges = new Dictionary<(int A, int B), List<int>>();
            for (var f = 0; f < faces.Count; f++)
                foreach (var pair in faces[f].Vertices.Select((v, i) => (A: System.Math.Min(v, faces[f].Vertices[(i + 1) % faces[f].Vertices.Length]), B: System.Math.Max(v, faces[f].Vertices[(i + 1) % faces[f].Vertices.Length]))))
                { if (!edges.TryGetValue(pair, out var owners)) edges.Add(pair, owners = []); owners.Add(f); }
            foreach (var (edge, owners) in edges.OrderBy(e => e.Key.A).ThenBy(e => e.Key.B))
            {
                if (owners.Count != 2) return Failure("ConvexStockHullInvalid: an edge does not have exactly two incident faces.");
                var a = owners[0]; var b = owners[1]; var normal = faces[a].Normal + faces[b].Normal;
                Face([Line(edge.A, edge.B, a), Arc(edge.B, a, b), Line(edge.B, edge.A, b), Arc(edge.A, b, a)],
                    SurfaceGeometry.FromCylinder(new(inner[edge.A], Direction3D.Create(inner[edge.B] - inner[edge.A]), radius, Direction3D.Create(faces[a].Normal))), normal);
            }
            for (var v = 0; v < points.Count; v++)
            {
                var normal = incident[v].Aggregate(Vector3D.Zero, (n, f) => n + faces[f].Normal);
                var axis = Direction3D.Create(normal); var x = Tangent(axis.ToVector()); var y = axis.ToVector().Cross(x);
                var ordered = incident[v].OrderBy(f => double.Atan2(faces[f].Normal.Dot(y), faces[f].Normal.Dot(x))).ToArray();
                Face(ordered.Select((f, i) => Arc(v, f, ordered[(i + 1) % ordered.Length])).ToArray(),
                    SurfaceGeometry.FromSphere(new(inner[v], axis, radius, Direction3D.Create(x))), normal);
            }
        }
        builder.AddBody([builder.AddShell(resultFaces)]);
        var body = new BrepBody(builder.Model, geometry, bindings, positions);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess ? KernelResult<BrepBody>.Success(body) : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static List<StockFace> Hull(IReadOnlyList<Point3D> points, double tolerance)
    {
        var found = new Dictionary<string, StockFace>(StringComparer.Ordinal);
        for (var a = 0; a < points.Count; a++) for (var b = a + 1; b < points.Count; b++) for (var c = b + 1; c < points.Count; c++)
        {
            var cross = (points[b] - points[a]).Cross(points[c] - points[a]);
            if (cross.Length <= tolerance * tolerance) continue;
            var normal = cross * (1 / cross.Length); var d = normal.Dot(points[a] - Point3D.Origin);
            if (d < 0) { normal *= -1; d *= -1; }
            if (d <= tolerance || points.Any(p => normal.Dot(p - Point3D.Origin) > d + tolerance)) continue;
            var coplanar = Enumerable.Range(0, points.Count).Where(i => double.Abs(normal.Dot(points[i] - Point3D.Origin) - d) <= tolerance).ToArray();
            var key = string.Join(',', coplanar); if (found.ContainsKey(key)) continue;
            var x = Tangent(normal); var y = normal.Cross(x);
            var ordered = coplanar.OrderBy(i => double.Atan2((points[i] - Point3D.Origin).Dot(y), (points[i] - Point3D.Origin).Dot(x))).ToArray();
            found.Add(key, new(ordered, normal, d));
        }
        return found.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Value).ToList();
    }

    private static Point3D OffsetVertex(IReadOnlyList<int> incident, IReadOnlyList<StockFace> faces, double radius)
    {
        for (var a = 0; a < incident.Count; a++) for (var b = a + 1; b < incident.Count; b++) for (var c = b + 1; c < incident.Count; c++)
        {
            var f = faces[incident[a]]; var g = faces[incident[b]]; var h = faces[incident[c]];
            var det = f.Normal.Dot(g.Normal.Cross(h.Normal)); if (double.Abs(det) < 1e-10) continue;
            var p = (g.Normal.Cross(h.Normal) * (f.Offset - radius) + h.Normal.Cross(f.Normal) * (g.Offset - radius) + f.Normal.Cross(g.Normal) * (h.Offset - radius)) * (1 / det);
            return Point3D.Origin + p;
        }
        return new(double.NaN, double.NaN, double.NaN);
    }

    private static Vector3D Tangent(Vector3D normal)
    {
        var axis = double.Abs(normal.X) < 0.8 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0);
        return Direction3D.Create(axis - normal * axis.Dot(normal)).ToVector();
    }
    private static bool Positive(double value) => double.IsFinite(value) && value > 0;
    private static KernelResult<BrepBody> Failure(string message) => KernelResult<BrepBody>.Failure([
        new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, "ConvexStock.Admission")]);
}
