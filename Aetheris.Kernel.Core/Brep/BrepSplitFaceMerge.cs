using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>
/// A clean-up pass: faces a construction left split on one surface are merged back, and the edge pairs that merging
/// leaves running on through a vertex are fused.
/// <list type="bullet">
/// <item>Planes merge across the lines between them.</item>
/// <item>Cylinders merge across the circles between them (a wall cut at a layer's height), and across the
/// generators between them until merging would close the ring: a full cylinder keeps two faces, as interchange
/// files usually do, so no seam edge is introduced.</item>
/// <item>Two lines on one line, or two arcs on one circle, that meet at a vertex no other edge uses, between the same
/// two faces, become one edge.</item>
/// </list>
/// Faces merge only when they have the same outward side and the edge between them is walked once each way; a group
/// whose boundary does not re-chain, or a curved face that would need more than one loop, is left as it was. The
/// geometry is never changed: every face keeps its surface and every edge its curve. Returns the input unchanged
/// when there is nothing to merge.
/// </summary>
public static class BrepSplitFaceMerge
{
    public static BrepBody Merge(BrepBody body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var topology = body.Topology;
        // Pcurves are derived and tied to coedges this pass renumbers: they are not carried over (export derives
        // what it needs). Vertex loops are not merged here, so a body with them is left as it is.
        if (body.Bindings.VertexLoopParameterBindings.Any()) return body;
        var points = new Dictionary<VertexId, Point3D>();
        foreach (var v in topology.Vertices)
        {
            if (!body.TryGetVertexPoint(v.Id, out var p)) return body;
            points[v.Id] = p;
        }
        var faces = topology.Faces.OrderBy(f => f.Id.Value).ToArray();
        var surfaces = new Dictionary<FaceId, (SurfaceGeometry Surface, FaceGeometryBinding Binding)>();
        foreach (var face in faces)
        {
            if (!body.Bindings.TryGetFaceBinding(face.Id, out var binding) || !body.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var surface) || surface is null) return body;
            surfaces[face.Id] = (surface, binding);
        }
        var uses = faces.SelectMany(f => f.LoopIds.SelectMany(l => topology.GetLoop(l).CoedgeIds.Select(c => (Coedge: topology.GetCoedge(c), Face: f.Id))))
            .GroupBy(u => u.Coedge.EdgeId).ToDictionary(g => g.Key, g => g.ToArray());
        var extent = points.Values.Select(p => (p - Point3D.Origin).Length).DefaultIfEmpty(0).Max();
        var tol = double.Max(1e-9, (extent + 1) * 1e-10) * 1e2;

        // Candidate edges: two distinct faces on one surface, same outward side, walked once each way.
        var planeOrCircle = new List<EdgeId>();
        var generators = new List<(EdgeId Edge, string Ring)>();
        foreach (var edge in topology.Edges.OrderBy(e => e.Id.Value))
        {
            if (!uses.TryGetValue(edge.Id, out var pair) || pair.Length != 2 || pair[0].Face == pair[1].Face || pair[0].Coedge.IsReversed == pair[1].Coedge.IsReversed) continue;
            var (a, b) = (surfaces[pair[0].Face], surfaces[pair[1].Face]);
            if (!body.Bindings.TryGetEdgeBinding(edge.Id, out var eb) || !body.Geometry.TryGetCurve(eb.CurveGeometryId, out var curve) || curve is null) continue;
            if (a.Surface.Plane is { } pa && b.Surface.Plane is { } pb && curve.Kind == CurveGeometryKind.Line3)
            {
                var (na, nb) = (Outward(pa.Normal.ToVector(), a.Binding), Outward(pb.Normal.ToVector(), b.Binding));
                if (na.Dot(nb) > 1 - 1e-12 && double.Abs(na.Dot(pb.Origin - pa.Origin)) <= tol) planeOrCircle.Add(edge.Id);
            }
            else if (a.Surface.Cylinder is { } ca && b.Surface.Cylinder is { } cb && a.Binding.Orientation == b.Binding.Orientation && SameCylinder(ca, cb, tol))
            {
                if (curve.Kind == CurveGeometryKind.Circle3) planeOrCircle.Add(edge.Id);
                else if (curve.Kind == CurveGeometryKind.Line3) generators.Add((edge.Id, RingKey(ca)));
            }
        }
        if (planeOrCircle.Count == 0 && generators.Count == 0) return body;

        var parent = faces.ToDictionary(f => f.Id, f => f.Id);
        FaceId Root(FaceId f) { while (parent[f] != f) f = parent[f] = parent[parent[f]]; return f; }
        var absorbed = new HashSet<EdgeId>();
        void Join(EdgeId e)
        {
            var (x, y) = (Root(uses[e][0].Face), Root(uses[e][1].Face));
            if (x == y) return;
            if (x.Value > y.Value) (x, y) = (y, x);
            parent[y] = x;
        }
        foreach (var e in planeOrCircle) { absorbed.Add(e); Join(e); }
        // Around a cylinder: merge across generators, but leave two faces where they would close a ring.
        foreach (var ring in generators.GroupBy(g => g.Ring))
        {
            var edges = ring.Select(g => g.Edge).ToList();
            var groups = edges.SelectMany(e => uses[e].Select(u => Root(u.Face))).Distinct().Count();
            var closes = edges.Select(e => (Root(uses[e][0].Face), Root(uses[e][1].Face))).Distinct().Count() >= groups;
            foreach (var e in edges)
            {
                var (x, y) = (Root(uses[e][0].Face), Root(uses[e][1].Face));
                if (x == y) { absorbed.Add(e); continue; }
                if (closes && groups <= 2) continue;
                absorbed.Add(e); Join(e); groups--;
            }
        }
        // An edge with the same group on both sides that was not absorbed would become a seam: keep its faces apart.
        foreach (var (edge, pair) in uses)
            if (pair.Length == 2 && pair[0].Face != pair[1].Face && !absorbed.Contains(edge) && Root(pair[0].Face) == Root(pair[1].Face))
                return body;

        // New loops for each merged group; unmerged faces keep theirs.
        var loopsOf = new Dictionary<FaceId, List<List<(EdgeId Edge, bool Reversed)>>>();
        foreach (var group in faces.GroupBy(f => Root(f.Id)))
        {
            var members = group.Select(f => f.Id).ToArray();
            var all = members.SelectMany(f => topology.GetFace(f).LoopIds.SelectMany(l => topology.GetLoop(l).CoedgeIds.Select(c => topology.GetCoedge(c))))
                .Where(c => !absorbed.Contains(c.EdgeId)).Select(c => (c.EdgeId, c.IsReversed)).ToList();
            if (members.Length == 1) { loopsOf[group.Key] = topology.GetFace(group.Key).LoopIds.Select(l => topology.GetLoop(l).CoedgeIds.Select(c => topology.GetCoedge(c)).Select(c => (c.EdgeId, c.IsReversed)).ToList()).ToList(); continue; }
            var chained = Chain(topology, all);
            if (chained is null || surfaces[group.Key].Surface.Plane is null && chained.Count > 1) return body;
            loopsOf[group.Key] = chained;
        }

        // Fuse edge pairs left running on through a vertex: same line or circle, same two faces, nothing else there.
        var kept = topology.Edges.Where(e => !absorbed.Contains(e.Id)).Select(e => e.Id).ToHashSet();
        var faceOfEdge = loopsOf.SelectMany(x => x.Value.SelectMany(l => l).Select(u => (u.Edge, Face: x.Key))).ToLookup(x => x.Edge, x => x.Face);
        var fused = new Dictionary<EdgeId, (VertexId Start, VertexId End, CurveGeometry Curve, ParameterInterval? Trim, bool Sense)>();
        var replaced = new Dictionary<EdgeId, EdgeId>(); // edge -> the fused edge that absorbed it
        var changed = new HashSet<EdgeId>();             // fused edges, which may run the other way now
        var definition = kept.ToDictionary(e => e, e =>
        {
            var edge = topology.GetEdge(e); body.Bindings.TryGetEdgeBinding(e, out var b); body.Geometry.TryGetCurve(b.CurveGeometryId, out var c);
            return (Start: edge.StartVertexId, End: edge.EndVertexId, Curve: c!, Trim: b.TrimInterval, Sense: b.OrientedEdgeSense);
        });
        var byVertex = kept.SelectMany(e => new[] { (definition[e].Start, e), (definition[e].End, e) }).Where(x => definition[x.e].Start != definition[x.e].End).ToLookup(x => x.Item1, x => x.e);
        foreach (var vertex in topology.Vertices.Select(v => v.Id).OrderBy(v => v.Value))
        {
            var at = byVertex[vertex].Distinct().Select(e => Current(e)).Distinct().ToArray();
            if (at.Length != 2) continue;
            var (e1, e2) = (at[0], at[1]);
            var f1 = faceOfEdge[e1].OrderBy(f => f.Value).ToArray(); var f2 = faceOfEdge[e2].OrderBy(f => f.Value).ToArray();
            if (!f1.SequenceEqual(f2)) continue;
            var (d1, d2) = (definition[e1], definition[e2]);
            var merged = Fuse(d1, d2, vertex, points, tol);
            if (merged is null) continue;
            definition[e1] = merged.Value; definition.Remove(e2); kept.Remove(e2); replaced[e2] = e1; changed.Add(e1);
        }
        EdgeId Current(EdgeId e) { while (replaced.TryGetValue(e, out var r)) e = r; return e; }

        // Rebuild.
        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var positions = new Dictionary<VertexId, Point3D>();
        var vertexMap = new Dictionary<VertexId, VertexId>();
        VertexId V(VertexId old) { if (!vertexMap.TryGetValue(old, out var v)) { v = builder.AddVertex(); vertexMap[old] = v; positions[v] = points[old]; } return v; }
        var edgeMap = new Dictionary<EdgeId, (EdgeId Edge, bool Flip)>();
        foreach (var e in kept.OrderBy(e => e.Value))
        {
            var d = definition[e];
            var id = builder.AddEdge(V(d.Start), V(d.End));
            var cid = new CurveGeometryId(geometry.Curves.Count() + 1);
            geometry.AddCurve(cid, d.Curve);
            bindings.AddEdgeBinding(new EdgeGeometryBinding(id, cid, d.Trim, d.Sense));
            edgeMap[e] = (id, false);
        }
        var resultFaces = new List<FaceId>();
        foreach (var (root, loops) in loopsOf.OrderBy(x => x.Key.Value))
        {
            var loopIds = new List<LoopId>();
            foreach (var loop in OrderLoops(loops, surfaces[root].Surface, topology, points))
            {
                // Uses of a fused-away edge collapse into the use of the edge that absorbed it.
                var mapped = new List<(EdgeId Edge, bool Reversed)>();
                var olds = new List<EdgeId>();
                foreach (var (edge, reversed) in loop)
                {
                    var target = Current(edge);
                    // Two pieces fused into one edge are one use; a seam walked out and back stays two.
                    if (mapped.Count > 0 && mapped[^1].Edge == edgeMap[target].Edge && olds[^1] != edge) continue;
                    mapped.Add((edgeMap[target].Edge, DirectionOf(edge, reversed, target))); olds.Add(edge);
                }
                if (mapped.Count > 1 && mapped[0].Edge == mapped[^1].Edge && olds[0] != olds[^1]) mapped.RemoveAt(mapped.Count - 1);
                var lid = builder.AllocateLoopId(); var cids = mapped.Select(_ => builder.AllocateCoedgeId()).ToArray();
                for (var i = 0; i < cids.Length; i++)
                    builder.AddCoedge(new Coedge(cids[i], mapped[i].Edge, lid, cids[(i + 1) % cids.Length], cids[(i + cids.Length - 1) % cids.Length], mapped[i].Reversed));
                builder.AddLoop(new Loop(lid, cids)); loopIds.Add(lid);
            }
            var face = builder.AddFace(loopIds); resultFaces.Add(face);
            for (var i = 0; i < loopIds.Count; i++)
                bindings.AddFaceBoundaryRoleBinding(new FaceBoundaryRoleBinding(face, loopIds[i], i == 0 ? FaceBoundaryRole.Outer : FaceBoundaryRole.Inner));
            var sid = new SurfaceGeometryId(geometry.Surfaces.Count() + 1); geometry.AddSurface(sid, surfaces[root].Surface);
            bindings.AddFaceBinding(new FaceGeometryBinding(face, sid, surfaces[root].Binding.Orientation.IsAlignedWithSurface));
        }
        builder.AddBody([builder.AddShell(resultFaces)]);
        var result = new BrepBody(builder.Model, geometry, bindings, positions);
        // Every loop walks head to tail, and every edge is used once each way: otherwise keep the body as it was.
        LastFailure = Connected(result);
        if (LastFailure is not null) return body;
        return BrepBindingValidator.Validate(result, requireAllEdgeAndFaceBindings: true).IsSuccess ? result : body;

        // A use of an old edge, re-expressed on the edge now carrying it: walk it the same way in space.
        bool DirectionOf(EdgeId old, bool reversed, EdgeId target)
        {
            if (old == target && !changed.Contains(target)) return reversed;
            var e = topology.GetEdge(old);
            var from = reversed ? e.EndVertexId : e.StartVertexId;
            var to = reversed ? e.StartVertexId : e.EndVertexId;
            // The fused edge runs from its start to its end; the old piece is walked its way when it moves forward along it.
            var d = definition[target];
            return !(Along(d, points[to]) > Along(d, points[from]));
        }
    }

    /// <summary>Why the last merge was abandoned (a loop that does not walk head to tail), for diagnosis.</summary>
    [ThreadStatic] internal static string? LastFailure;

    private static string? Connected(BrepBody body)
    {
        var t = body.Topology;
        foreach (var face in t.Faces)
        foreach (var loopId in face.LoopIds)
        {
            var coedges = t.GetLoop(loopId).CoedgeIds.Select(t.GetCoedge).ToArray();
            for (var i = 0; i < coedges.Length; i++)
            {
                var (a, b) = (coedges[i], coedges[(i + 1) % coedges.Length]);
                var (ea, eb) = (t.GetEdge(a.EdgeId), t.GetEdge(b.EdgeId));
                var end = a.IsReversed ? ea.StartVertexId : ea.EndVertexId;
                var start = b.IsReversed ? eb.EndVertexId : eb.StartVertexId;
                if (end != start)
                {
                    var walk = string.Join(" ", coedges.Select(c => { var e = t.GetEdge(c.EdgeId); body.Bindings.TryGetEdgeBinding(e.Id, out var eb); body.Geometry.TryGetCurve(eb.CurveGeometryId, out var cv); return $"{e.Id.Value}{(c.IsReversed ? "-" : "+")}:{e.StartVertexId.Value}>{e.EndVertexId.Value}[{cv?.Kind}]"; }));
                    body.TryGetFaceSurface(face.Id, out var sf);
                    return $"face {face.Id.Value} ({sf?.Kind}) loop {loopId.Value}: coedge {a.Id.Value} (edge {a.EdgeId.Value}) ends at vertex {end.Value}, the next starts at {start.Value}; walk {walk}";
                }
            }
        }
        var bad = t.Coedges.GroupBy(c => c.EdgeId).FirstOrDefault(g => g.Count() != 2 || g.First().IsReversed == g.Last().IsReversed);
        return bad is null ? null : $"edge {bad.Key.Value} is not used once each way";
    }

    private static double Along((VertexId Start, VertexId End, CurveGeometry Curve, ParameterInterval? Trim, bool Sense) d, Point3D p)
    {
        if (d.Curve.Line3 is { } line) return (p - line.Origin).Dot(line.Direction.ToVector()) * (d.Sense ? 1 : -1);
        var circle = d.Curve.Circle3!.Value; var q = p - circle.Center;
        var angle = double.Atan2(q.Dot(circle.YAxis.ToVector()), q.Dot(circle.XAxis.ToVector()));
        var start = d.Trim?.Start ?? 0;
        var t = angle; while (t < start - 1e-9) t += 2 * double.Pi; while (t > start + 2 * double.Pi) t -= 2 * double.Pi;
        return (t - start) * (d.Sense ? 1 : -1);
    }

    /// <summary>Two edges on one line or one circle meeting at <paramref name="vertex"/>, as one edge.</summary>
    private static (VertexId Start, VertexId End, CurveGeometry Curve, ParameterInterval? Trim, bool Sense)? Fuse(
        (VertexId Start, VertexId End, CurveGeometry Curve, ParameterInterval? Trim, bool Sense) a,
        (VertexId Start, VertexId End, CurveGeometry Curve, ParameterInterval? Trim, bool Sense) b,
        VertexId vertex, Dictionary<VertexId, Point3D> points, double tol)
    {
        var farA = a.Start == vertex ? a.End : a.Start; var farB = b.Start == vertex ? b.End : b.Start;
        if (farA == farB) return null;
        if (a.Curve.Line3 is { } la && b.Curve.Line3 is { } lb)
        {
            var dir = la.Direction.ToVector();
            if (dir.Cross(lb.Direction.ToVector()).Length > 1e-12) return null;
            var (p0, p1) = (points[farA], points[farB]);
            var off = p1 - la.Origin; if ((off - dir * off.Dot(dir)).Length > tol) return null;
            // From one far end to the other, through the vertex.
            var through = points[vertex];
            if ((through - p0).Dot(p1 - through) <= 0) return null;
            var line = new Line3Curve(p0, Direction3D.Create(p1 - p0));
            return (farA, farB, CurveGeometry.FromLine(line), new ParameterInterval(0, (p1 - p0).Length), true);
        }
        if (a.Curve.Circle3 is { } ca && b.Curve.Circle3 is { } cb)
        {
            if ((ca.Center - cb.Center).Length > tol || double.Abs(ca.Radius - cb.Radius) > tol || ca.Normal.ToVector().Cross(cb.Normal.ToVector()).Length > 1e-12) return null;
            var n = ca.Normal.ToVector();
            double Angle(Point3D p) { var q = p - ca.Center; return double.Atan2(q.Dot(ca.YAxis.ToVector()), q.Dot(ca.XAxis.ToVector())); }
            // Sweep from farA through the vertex to farB about the normal, whichever way passes the vertex.
            double Ccw(double from, double to) { var d = to - from; while (d < 0) d += 2 * double.Pi; while (d >= 2 * double.Pi) d -= 2 * double.Pi; return d; }
            var (t0, tv, t1) = (Angle(points[farA]), Angle(points[vertex]), Angle(points[farB]));
            if (Ccw(t0, tv) < Ccw(t0, t1))
            {
                var trim = new ParameterInterval(t0, t0 + Ccw(t0, t1));
                return (farA, farB, a.Curve, trim, true);
            }
            var back = new ParameterInterval(t1, t1 + Ccw(t1, t0));
            return (farB, farA, a.Curve, back, true);
        }
        return null;
    }

    private static List<List<(EdgeId Edge, bool Reversed)>> OrderLoops(List<List<(EdgeId Edge, bool Reversed)>> loops, SurfaceGeometry surface,
        TopologyModel topology, Dictionary<VertexId, Point3D> points)
    {
        if (surface.Plane is not { } plane || loops.Count < 2) return loops;
        // Outer first: the loop enclosing the most area, from its vertices (arcs do not change which of a planar
        // face's nested loops is largest).
        double Area(List<(EdgeId Edge, bool Reversed)> loop)
        {
            var pts = loop.Select(u => { var e = topology.GetEdge(u.Edge); return points[u.Reversed ? e.EndVertexId : e.StartVertexId]; })
                .Select(p => ((p - plane.Origin).Dot(plane.UAxis.ToVector()), (p - plane.Origin).Dot(plane.VAxis.ToVector()))).ToList();
            var a = 0d; for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++) a += pts[j].Item1 * pts[i].Item2 - pts[i].Item1 * pts[j].Item2;
            return double.Abs(a);
        }
        return loops.OrderByDescending(Area).ToList();
    }

    private static List<List<(EdgeId Edge, bool Reversed)>>? Chain(TopologyModel topology, List<(EdgeId Edge, bool Reversed)> uses)
    {
        VertexId From((EdgeId Edge, bool Reversed) u) { var e = topology.GetEdge(u.Edge); return u.Reversed ? e.EndVertexId : e.StartVertexId; }
        VertexId To((EdgeId Edge, bool Reversed) u) { var e = topology.GetEdge(u.Edge); return u.Reversed ? e.StartVertexId : e.EndVertexId; }
        var rest = uses.ToList(); var loops = new List<List<(EdgeId, bool)>>();
        while (rest.Count > 0)
        {
            var loop = new List<(EdgeId Edge, bool Reversed)> { rest[0] }; rest.RemoveAt(0);
            while (From(loop[0]) != To(loop[^1]))
            {
                var candidates = rest.Select((u, i) => (u, i)).Where(x => From(x.u) == To(loop[^1])).ToArray();
                // Where the merged boundary touches itself, more than one use leaves the vertex: that is ambiguous.
                if (candidates.Length != 1) return null;
                loop.Add(candidates[0].u); rest.RemoveAt(candidates[0].i);
            }
            loops.Add(loop);
        }
        return loops;
    }

    private static Vector3D Outward(Vector3D normal, FaceGeometryBinding binding) => binding.Orientation.IsAlignedWithSurface ? normal : normal * -1;

    private static bool SameCylinder(Geometry.Surfaces.CylinderSurface a, Geometry.Surfaces.CylinderSurface b, double tol)
    {
        var axis = a.Axis.ToVector(); var d = b.Origin - a.Origin;
        return double.Abs(a.Radius - b.Radius) <= tol && axis.Cross(b.Axis.ToVector()).Length < 1e-12 && (d - axis * d.Dot(axis)).Length <= tol;
    }

    private static string RingKey(Geometry.Surfaces.CylinderSurface c)
    {
        // Coaxial, same radius: the same ring. Rounded so equal cylinders from different constructions agree.
        var axis = c.Axis.ToVector(); if (axis.X < 0 || axis.X == 0 && (axis.Y < 0 || axis.Y == 0 && axis.Z < 0)) axis *= -1;
        var o = c.Origin - axis * (c.Origin - Point3D.Origin).Dot(axis);
        static string R(double v) => double.Round(v, 6).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        return $"{R(axis.X)},{R(axis.Y)},{R(axis.Z)}|{R(o.X)},{R(o.Y)},{R(o.Z)}|{R(c.Radius)}";
    }
}
