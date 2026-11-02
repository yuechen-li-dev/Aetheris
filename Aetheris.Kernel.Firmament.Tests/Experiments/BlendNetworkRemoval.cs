using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Tests.Experiments;

/// <summary>
/// E1 of blend clean-up: delete a network of blend strips bounded by planes and rebuild the
/// sharp geometry from the extended planes. Each strip becomes the line where its two bridged
/// planes meet; every cluster of vertices joined by a strip's end or miter edges becomes the
/// point where the planes meeting there intersect. Experimental: planar primaries only;
/// anything else refuses with a reason.
/// </summary>
internal static class BlendNetworkRemoval
{
    private const double Tol = 1e-6;

    internal sealed record Result(BrepBody? Body, string? Refusal, int NewEdges, int Clusters);

    internal static Result Remove(BrepBody body, IReadOnlySet<FaceId> strips, IReadOnlyDictionary<FaceId, FaceId[]> anchors)
    {
        try { return Run(body, strips, anchors); }
        catch (InvalidOperationException refusal) { return new(null, refusal.Message, 0, 0); }
    }

    private static Result Run(BrepBody body, IReadOnlySet<FaceId> strips, IReadOnlyDictionary<FaceId, FaceId[]> anchors)
    {
        var topology = body.Topology;
        var edgeFaces = new Dictionary<EdgeId, List<FaceId>>();
        foreach (var face in topology.Faces)
            foreach (var loop in face.LoopIds)
                foreach (var c in topology.GetLoop(loop).CoedgeIds)
                {
                    var e = topology.GetCoedge(c).EdgeId;
                    if (!edgeFaces.TryGetValue(e, out var list)) edgeFaces[e] = list = [];
                    list.Add(face.Id);
                }
        PlaneSurface Plane(FaceId f) => body.TryGetFaceSurfaceGeometry(f, out var s) && s?.Plane is { } p ? p
            : throw new InvalidOperationException($"blend-removal-primary-not-planar: face {f.Value} borders the network and is {s?.Kind}; E1 admits planes only");
        var lengths = BrepDisplayTessellator.Tessellate(body).Value.EdgePolylines
            .ToDictionary(p => p.EdgeId, p => p.Points.Zip(p.Points.Skip(1)).Sum(q => (q.Second - q.First).Length));
        FaceId Other(EdgeId e, FaceId f) => edgeFaces[e].Count == 2 && edgeFaces[e][0] != edgeFaces[e][1] ? edgeFaces[e].Single(x => x != f) : throw new InvalidOperationException($"blend-removal-seam-edge: edge {e.Value} is a seam or non-manifold");
        IEnumerable<EdgeId> EdgesOf(FaceId f) => topology.GetFace(f).LoopIds.SelectMany(l => topology.GetLoop(l).CoedgeIds)
            .Select(c => topology.GetCoedge(c).EdgeId).Distinct();

        // 1. Each strip spans exactly two anchors (from classification: tangent for fillets, leaning for chamfers).
        var bridge = new Dictionary<FaceId, (FaceId A, FaceId B)>();
        var longitudinal = new HashSet<EdgeId>();
        foreach (var s in strips)
        {
            var anchor = anchors.GetValueOrDefault(s) ?? [];
            if (anchor.Length != 2) throw new InvalidOperationException($"blend-removal-strip-not-two-anchored: face {s.Value} spans {anchor.Length} faces; corners are E3");
            var (a, b) = (anchor[0], anchor[1]);
            Plane(a); Plane(b);
            bridge[s] = (a, b);
            foreach (var e in EdgesOf(s).Where(e => edgeFaces[e].Count == 2 && (Other(e, s) == a || Other(e, s) == b))) longitudinal.Add(e);
        }
        if (strips.SelectMany(EdgesOf).Any(e => edgeFaces[e].Count != 2 || edgeFaces[e][0] == edgeFaces[e][1]))
            throw new InvalidOperationException("blend-removal-seam-edge: a strip has a seam or non-manifold edge");
        var touched = strips.SelectMany(EdgesOf).ToHashSet();
        var transverse = touched.Where(e => !longitudinal.Contains(e)).ToHashSet();

        // 2. Vertex clusters: vertices joined by transverse (end, miter) edges collapse to one corner.
        var parent = new Dictionary<VertexId, VertexId>();
        VertexId Find(VertexId v) { if (!parent.ContainsKey(v)) parent[v] = v; while (parent[v] != v) v = parent[v] = parent[parent[v]]; return v; }
        foreach (var e in touched) { var edge = topology.GetEdge(e); Find(edge.StartVertexId); Find(edge.EndVertexId); }
        foreach (var e in transverse) { var edge = topology.GetEdge(e); parent[Find(edge.StartVertexId)] = Find(edge.EndVertexId); }
        var clusterOf = parent.Keys.ToDictionary(v => v, Find);
        var clusters = clusterOf.GroupBy(p => p.Value).ToDictionary(g => g.Key, g => g.Select(p => p.Key).ToArray());

        // Each cluster's corner: the point where the outside planes around its vertices meet.
        var vertexFaces = new Dictionary<VertexId, HashSet<FaceId>>();
        foreach (var (e, faces) in edgeFaces)
            foreach (var v in new[] { topology.GetEdge(e).StartVertexId, topology.GetEdge(e).EndVertexId })
            {
                if (!vertexFaces.TryGetValue(v, out var set)) vertexFaces[v] = set = [];
                set.UnionWith(faces);
            }
        var corner = new Dictionary<VertexId, Point3D>();
        var representative = new Dictionary<VertexId, VertexId>();
        foreach (var (root, members) in clusters)
        {
            var planes = members.SelectMany(v => vertexFaces[v]).Where(f => !strips.Contains(f)).Distinct().Select(f => (Face: f, Plane: Plane(f))).ToArray();
            body.TryGetVertexPoint(members[0], out var seed);
            corner[root] = Solve(planes.Select(p => p.Plane).ToArray(), seed,
                $"cluster of vertices {string.Join(',', members.Select(m => m.Value))} on faces {string.Join(',', planes.Select(p => p.Face.Value))}");
            representative[root] = members.MinBy(v => v.Value)!;
        }
        VertexId Map(VertexId v) => clusterOf.TryGetValue(v, out var root) ? representative[root] : v;

        // 3. One straight edge per (corner, corner, plane pair).
        var nextEdge = topology.Edges.Max(e => e.Id.Value) + 1;
        var nextCurve = body.Geometry.Curves.Max(c => c.Key.Value) + 1;
        var newEdges = new Dictionary<(int, int, int, int), (EdgeId Id, VertexId Start, VertexId End, CurveGeometryId Curve, Line3Curve Line, double Length)>();
        (EdgeId Id, VertexId Start, VertexId End, CurveGeometryId Curve, Line3Curve Line, double Length) NewEdge(EdgeId old, FaceId s)
        {
            var edge = topology.GetEdge(old);
            var (u, v) = (Map(edge.StartVertexId), Map(edge.EndVertexId));
            if (u == v) throw new InvalidOperationException($"blend-removal-strip-collapses: edge {old.Value} has both ends in one corner");
            var (a, b) = bridge[s];
            var key = (Math.Min(u.Value, v.Value), Math.Max(u.Value, v.Value), Math.Min(a.Value, b.Value), Math.Max(a.Value, b.Value));
            if (newEdges.TryGetValue(key, out var made)) return made;
            var (start, end) = u.Value < v.Value ? (u, v) : (v, u);
            var p = corner[clusterOf[start]]; var q = corner[clusterOf[end]];
            var line = new Line3Curve(p, Direction3D.Create(q - p));
            return newEdges[key] = (new EdgeId(nextEdge++), start, end, new CurveGeometryId(nextCurve++), line, (q - p).Length);
        }

        // 4. Rebuild the loops of every face outside the network.
        var model = new TopologyModel();
        var keptVertices = topology.Vertices.Select(v => Map(v.Id)).Distinct().ToArray();
        foreach (var v in keptVertices) model.AddVertex(new Vertex(v));
        var keptEdges = topology.Edges.Where(e => !touched.Contains(e.Id)).ToArray();
        foreach (var e in keptEdges) model.AddEdge(e with { StartVertexId = Map(e.StartVertexId), EndVertexId = Map(e.EndVertexId) });
        var changedFaces = new HashSet<FaceId>();
        var newCoedges = new List<Coedge>();
        var newLoops = new List<Loop>();
        var nextCoedge = topology.Coedges.Max(c => c.Id.Value) + 1;
        foreach (var face in topology.Faces.Where(f => !strips.Contains(f.Id)))
            foreach (var loopId in face.LoopIds)
            {
                var loop = topology.GetLoop(loopId);
                var uses = new List<(EdgeId Edge, bool Reversed)>();
                var changed = false;
                foreach (var c in loop.CoedgeIds.Select(topology.GetCoedge))
                {
                    if (!touched.Contains(c.EdgeId)) { uses.Add((c.EdgeId, c.IsReversed)); continue; }
                    changed = true;
                    var s = Other(c.EdgeId, face.Id);
                    if (transverse.Contains(c.EdgeId)) continue; // its ends collapse into one corner
                    var edge = topology.GetEdge(c.EdgeId);
                    var from = Map(c.IsReversed ? edge.EndVertexId : edge.StartVertexId);
                    var made = NewEdge(c.EdgeId, s);
                    var use = (made.Id, from != made.Start);
                    if (uses.Count == 0 || uses[^1] != use) uses.Add(use);
                }
                if (changed) changedFaces.Add(face.Id);
                if (uses.Count > 1 && uses[0] == uses[^1]) uses.RemoveAt(uses.Count - 1);
                if (uses.Count == 0) throw new InvalidOperationException($"blend-removal-loop-vanishes: face {face.Id.Value} loop {loopId.Value}");
                var ids = loop.CoedgeIds.Count == uses.Count && !changed ? loop.CoedgeIds.ToArray()
                    : uses.Select(_ => new CoedgeId(nextCoedge++)).ToArray();
                for (var i = 0; i < uses.Count; i++)
                    newCoedges.Add(new Coedge(ids[i], uses[i].Edge, loopId, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reversed));
                newLoops.Add(loop with { CoedgeIds = ids });
            }
        foreach (var e in newEdges.Values) model.AddEdge(new Edge(e.Id, e.Start, e.End));
        foreach (var c in newCoedges) model.AddCoedge(c);
        foreach (var l in newLoops) model.AddLoop(l);
        foreach (var f in topology.Faces.Where(f => !strips.Contains(f.Id))) model.AddFace(f);
        foreach (var shell in topology.Shells) model.AddShell(shell with { FaceIds = shell.FaceIds.Where(f => !strips.Contains(f)).ToArray() });
        foreach (var b in topology.Bodies) model.AddBody(b);

        // 5. Bindings and geometry: kept edges re-trimmed to moved corners, new lines bound, pcurves rebuilt.
        var bindings = new BrepBindingModel();
        var geometry = new BrepGeometryStore();
        var points = new Dictionary<VertexId, Point3D>();
        foreach (var v in keptVertices)
            points[v] = clusterOf.TryGetValue(v, out var root) ? corner[root] : body.TryGetVertexPoint(v, out var p) ? p : throw new InvalidOperationException($"vertex {v.Value} has no point");
        foreach (var e in keptEdges)
        {
            var binding = body.Bindings.GetEdgeBinding(e.Id);
            var moved = clusterOf.ContainsKey(e.StartVertexId) || clusterOf.ContainsKey(e.EndVertexId);
            if (moved)
            {
                var curve = body.Geometry.GetCurve(binding.CurveGeometryId);
                if (curve.Line3 is not { } line) throw new InvalidOperationException($"blend-removal-moved-edge-not-straight: edge {e.Id.Value} is {curve.Kind}");
                double At(Point3D p) => (p - line.Origin).Dot(line.Direction.ToVector());
                var (s, t) = (At(points[Map(e.StartVertexId)]), At(points[Map(e.EndVertexId)]));
                if (!binding.OrientedEdgeSense) (s, t) = (t, s);
                if (t - s <= Tol) throw new InvalidOperationException($"blend-removal-edge-inverts: edge {e.Id.Value}");
                binding = binding with { TrimInterval = new ParameterInterval(s, t) };
            }
            bindings.AddEdgeBinding(binding);
        }
        foreach (var e in newEdges.Values)
        {
            geometry.AddCurve(e.Curve, CurveGeometry.FromLine(e.Line));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(e.Id, e.Curve, new ParameterInterval(0, e.Length)));
        }
        foreach (var b in body.Bindings.FaceBindings.Where(b => !strips.Contains(b.FaceId))) bindings.AddFaceBinding(b);
        foreach (var b in body.Bindings.FaceBoundaryRoleBindings.Where(b => !strips.Contains(b.FaceId))) bindings.AddFaceBoundaryRoleBinding(b);
        var keptCoedges = newCoedges.Select(c => c.Id).ToHashSet();
        foreach (var b in body.Bindings.PcurveBindings.Where(b => keptCoedges.Contains(b.CoedgeId) && !changedFaces.Contains(b.FaceId))) bindings.AddPcurveBinding(b);
        foreach (var b in body.Bindings.VertexLoopParameterBindings.Where(b => !changedFaces.Contains(b.FaceId) && !strips.Contains(b.FaceId))) bindings.AddVertexLoopParameterBinding(b);
        foreach (var id in bindings.EdgeBindings.Select(b => b.CurveGeometryId).Distinct().Where(id => !geometry.TryGetCurve(id, out _))) geometry.AddCurve(id, body.Geometry.GetCurve(id));
        foreach (var id in bindings.FaceBindings.Select(b => b.SurfaceGeometryId).Distinct()) geometry.AddSurface(id, body.Geometry.GetSurface(id));
        var pcurves = BrepPcurveRecovery.Populate(model, geometry, bindings);
        if (pcurves.Diagnostics.Count > 0) throw new InvalidOperationException("blend-removal-pcurves: " + string.Join("; ", pcurves.Diagnostics.Take(3).Select(d => d.Message)));
        return new(new BrepBody(model, geometry, bindings, points, null, body.ShellRepresentation), null, newEdges.Count, clusters.Count);
    }

    /// <summary>Point common to the planes (least squares, then checked), or the point on their
    /// line nearest the seed when only two planes meet there.</summary>
    private static Point3D Solve(PlaneSurface[] planes, Point3D seed, string where)
    {
        if (planes.Length < 2) throw new InvalidOperationException($"blend-removal-corner-underdetermined: {where}");
        var n = planes.Select(p => p.Normal.ToVector()).ToArray();
        var d = planes.Select(p => p.Normal.ToVector().Dot(p.Origin - Point3D.Origin)).ToArray();
        Point3D x;
        if (planes.Length == 2 || Rank(n) == 2)
        {
            // Nearest point to the seed on the line where the planes meet.
            var direction = n[0].Cross(n[1]);
            if (direction.Length < 1e-9) throw new InvalidOperationException($"blend-removal-planes-parallel: {where}");
            var m = new[] { n[0], n[1], direction };
            x = Solve3(m, [d[0], d[1], direction.Dot(seed - Point3D.Origin)]);
        }
        else
        {
            // Normal equations over all planes.
            var a = new double[3, 3]; var r = new double[3];
            for (var k = 0; k < n.Length; k++)
            {
                var v = new[] { n[k].X, n[k].Y, n[k].Z };
                for (var i = 0; i < 3; i++) { r[i] += v[i] * d[k]; for (var j = 0; j < 3; j++) a[i, j] += v[i] * v[j]; }
            }
            x = Solve3([new(a[0, 0], a[0, 1], a[0, 2]), new(a[1, 0], a[1, 1], a[1, 2]), new(a[2, 0], a[2, 1], a[2, 2])], r);
        }
        var residual = n.Zip(d).Max(p => Math.Abs(p.First.Dot(x - Point3D.Origin) - p.Second));
        if (residual > 1e-6) throw new InvalidOperationException($"blend-removal-corner-not-common: {planes.Length} planes miss one point by {residual:G3} mm at {where}");
        return x;
    }

    private static int Rank(Vector3D[] n)
    {
        var det = n.Length < 3 ? 0 : Enumerable.Range(0, n.Length).SelectMany(i => Enumerable.Range(i + 1, n.Length - i - 1).SelectMany(j =>
            Enumerable.Range(j + 1, n.Length - j - 1).Select(k => Math.Abs(n[i].Dot(n[j].Cross(n[k])))))).DefaultIfEmpty(0).Max();
        return det > 1e-9 ? 3 : 2;
    }

    private static Point3D Solve3(Vector3D[] rows, double[] rhs)
    {
        var det = rows[0].Dot(rows[1].Cross(rows[2]));
        if (Math.Abs(det) < 1e-12) throw new InvalidOperationException("blend-removal-corner-singular");
        var c0 = rows[1].Cross(rows[2]); var c1 = rows[2].Cross(rows[0]); var c2 = rows[0].Cross(rows[1]);
        var v = (c0 * rhs[0] + c1 * rhs[1] + c2 * rhs[2]) * (1 / det);
        return Point3D.Origin + v;
    }
}
