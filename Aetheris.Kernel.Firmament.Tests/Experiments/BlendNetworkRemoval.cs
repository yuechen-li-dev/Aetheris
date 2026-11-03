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
/// point where the planes meeting there intersect. E2 extends this to cylinders and cones: a strip
/// between a plane and a revolved face across its axis becomes a circle, one along it a line, and
/// corners among curved faces are solved implicitly. Anything else refuses with a reason.
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
        // An imported body is only as exact as its source says: corners and kept edges are checked to the
        // declared distance accuracy (10 um when it declares none), as tolerant edges are on import.
        var accuracy = body.ImportQualification is { } qualification ? Math.Max(1e-6, qualification.SourceDistanceAccuracyMillimetres ?? 0.01) : 1e-6;
        var edgeFaces = new Dictionary<EdgeId, List<FaceId>>();
        foreach (var face in topology.Faces)
            foreach (var loop in face.LoopIds)
                foreach (var c in topology.GetLoop(loop).CoedgeIds)
                {
                    var e = topology.GetCoedge(c).EdgeId;
                    if (!edgeFaces.TryGetValue(e, out var list)) edgeFaces[e] = list = [];
                    list.Add(face.Id);
                }
        Implicit Surface(FaceId f) => body.TryGetFaceSurfaceGeometry(f, out var s) && s is { Kind: SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Cone }
            ? new Implicit(f, s)
            : throw new InvalidOperationException($"blend-removal-primary-unsupported: face {f.Value} borders the network and is {s?.Kind}; E2 admits planes, cylinders and cones");
        var polylines = BrepDisplayTessellator.Tessellate(body).Value.EdgePolylines.ToDictionary(p => p.EdgeId, p => p.Points);
        FaceId Other(EdgeId e, FaceId f) => edgeFaces[e].Count == 2 && edgeFaces[e][0] != edgeFaces[e][1] ? edgeFaces[e].Single(x => x != f) : throw new InvalidOperationException($"blend-removal-seam-edge: edge {e.Value} is a seam or non-manifold");
        IEnumerable<EdgeId> EdgesOf(FaceId f) => topology.GetFace(f).LoopIds.SelectMany(l => topology.GetLoop(l).CoedgeIds)
            .Select(c => topology.GetCoedge(c).EdgeId).Distinct();

        // 1. Each strip spans exactly two anchors (from classification: tangent for fillets, leaning for chamfers).
        var bridge = new Dictionary<FaceId, (FaceId A, FaceId B)>();
        var bridgeCircle = new Dictionary<FaceId, Conic?>();
        var longitudinal = new HashSet<EdgeId>();
        // A face spanning other than two anchors is a joiner (corner patch, setback, a blend that only
        // meets blends): it has no edge of its own to leave behind, so all its edges are transverse and
        // its vertices collapse into the corners around it. Its own geometry never matters.
        foreach (var s in strips)
        {
            var anchor = anchors.GetValueOrDefault(s) ?? [];
            if (anchor.Length != 2) continue;
            var (a, b) = (anchor[0], anchor[1]);
            bridge[s] = (a, b);
            bridgeCircle[s] = SharpEdge(Surface(a), Surface(b), s);
            // A strip's own seam (a ring blend) has both sides on the strip: it is transverse.
            foreach (var e in EdgesOf(s).Where(e => edgeFaces[e].Count == 2 && edgeFaces[e][0] != edgeFaces[e][1] && (Other(e, s) == a || Other(e, s) == b))) longitudinal.Add(e);
        }
        if (strips.SelectMany(EdgesOf).Any(e => edgeFaces[e].Count != 2 || (edgeFaces[e][0] == edgeFaces[e][1] && !strips.Contains(edgeFaces[e][0]))))
            throw new InvalidOperationException("blend-removal-seam-edge: a strip has a non-manifold edge");
        // A corner patch with no strip beside it is a lone corner: the fillets it joins were left out (not
        // recognized, or not selected), and without them there is no sharp corner to rebuild.
        foreach (var joiner in strips.Where(f => !bridge.ContainsKey(f)))
            if (!EdgesOf(joiner).Any(e => edgeFaces[e].Any(f => f != joiner && strips.Contains(f))))
                throw new InvalidOperationException($"blend-removal-lone-corner: face {joiner.Value} joins faces {string.Join(',', EdgesOf(joiner).SelectMany(e => edgeFaces[e]).Where(f => f != joiner).Distinct().Select(f => f.Value))}, none of them selected; select the fillets it joins too");
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
        var pinned = new HashSet<VertexId>();
        var keptEdgeAt = topology.Edges.Where(e => !touched.Contains(e.Id)).SelectMany(e => new[] { e.StartVertexId, e.EndVertexId }).ToHashSet();
        Point3D Original(VertexId v) => body.TryGetVertexPoint(v, out var p) ? p : throw new InvalidOperationException($"vertex {v.Value} has no point");
        Point3D Corner(VertexId[] members, Implicit[] around)
        {
            // Seed from a vertex a kept edge leaves from: that edge's curve already holds it, and the
            // smallest move onto the other surfaces keeps it there when the cluster is underdetermined.
            var seed = Original(members.FirstOrDefault(keptEdgeAt.Contains, members[0]));
            var where = $"cluster of vertices {string.Join(',', members.Select(m => m.Value))} on faces {string.Join(',', around.Select(p => $"{p.Face.Value}{p.Surface.Kind.ToString()[..3]}"))}";
            return around.All(p => p.Surface.Plane is not null)
                ? Solve(around.Select(p => p.Surface.Plane!.Value).ToArray(), seed, where, accuracy)
                : SolveImplicit(around, seed, where, accuracy);
        }
        foreach (var (root, members) in clusters)
        {
            var around = members.SelectMany(v => vertexFaces[v]).Where(f => !strips.Contains(f)).Distinct().Select(Surface).ToArray();
            try { corner[root] = Corner(members, around); representative[root] = members.MinBy(v => v.Value)!; continue; }
            catch (InvalidOperationException refusal) when (refusal.Message.StartsWith("blend-removal-corner-not-common", StringComparison.Ordinal))
            {
                // A strip can end on a kept face at a vertex that is already a sharp corner of the kept faces
                // (on three or more of them, exactly). That vertex stays; the strip's corner is where the rest
                // collapse, and each end edge between them becomes the kept face meeting the strip's anchor.
                var stay = members.Where(v => keptEdgeAt.Contains(v)).Where(v =>
                {
                    var kept = vertexFaces[v].Where(f => !strips.Contains(f)).ToArray();
                    return kept.Length >= 3 && kept.All(f => Math.Abs(Surface(f).F(Original(v))) <= accuracy);
                }).ToArray();
                var free = members.Except(stay).ToArray();
                if (stay.Length == 0 || free.Length == 0)
                {
                    // Most often the strip runs into another blend that was not selected with it (a chamfer cone
                    // round a rounded corner, a fillet cylinder): say which curved faces, so the caller can add them.
                    var curved = around.Where(p => p.Surface.Plane is null).Select(p => p.Face.Value).ToArray();
                    if (curved.Length == 0) throw;
                    throw new InvalidOperationException(refusal.Message + $"; it meets curved faces {string.Join(',', curved)}: if they are blends, select them too", refusal);
                }
                var strip = free.SelectMany(v => vertexFaces[v]).Where(bridge.ContainsKey).Distinct().ToArray();
                var freeAround = free.SelectMany(v => vertexFaces[v]).Where(f => !strips.Contains(f))
                    .Concat(strip.SelectMany(f => new[] { bridge[f].A, bridge[f].B })).Distinct().Select(Surface).ToArray();
                corner[root] = Corner(free, freeAround);
                representative[root] = free.MinBy(v => v.Value)!;
                pinned.UnionWith(stay);
            }
        }
        // Where a vertex goes: its cluster's corner, unless it stays. A strip's own sharp edge always ends at the corner.
        VertexId Map(VertexId v) => pinned.Contains(v) ? v : MapCorner(v);
        VertexId MapCorner(VertexId v) => clusterOf.TryGetValue(v, out var root) ? representative[root] : v;
        Point3D PointOf(VertexId mapped) => pinned.Contains(mapped) || !clusterOf.ContainsKey(mapped) ? Original(mapped) : corner[clusterOf[mapped]];

        // 3. One straight edge per (corner, corner, plane pair).
        var nextEdge = topology.Edges.Max(e => e.Id.Value) + 1;
        var nextCurve = body.Geometry.Curves.Max(c => c.Key.Value) + 1;
        var newEdges = new Dictionary<(int, int, int, int), NewEdgeData>();
        NewEdgeData NewEdge(EdgeId old, FaceId s)
        {
            var edge = topology.GetEdge(old);
            var (u, v) = (MapCorner(edge.StartVertexId), MapCorner(edge.EndVertexId));
            var circle = bridgeCircle[s];
            if (u == v && circle is null) throw new InvalidOperationException($"blend-removal-strip-collapses: edge {old.Value} has both ends in one corner");
            var (a, b) = bridge[s];
            var key = (Math.Min(u.Value, v.Value), Math.Max(u.Value, v.Value), Math.Min(a.Value, b.Value), Math.Max(a.Value, b.Value));
            if (newEdges.TryGetValue(key, out var made)) return made;
            return newEdges[key] = Build(old, u, v, circle, a, b, $"strip {s.Value}'s");
        }
        NewEdgeData Build(EdgeId old, VertexId u, VertexId v, Conic? circle, FaceId a, FaceId b, string what)
        {
            var (start, end) = u.Value < v.Value ? (u, v) : (v, u);
            var p = PointOf(start); var q = PointOf(end);
            NewEdgeData result;
            if (circle is not { } c)
            {
                var line = new Line3Curve(p, Direction3D.Create(q - p));
                result = new(new EdgeId(nextEdge++), start, end, new CurveGeometryId(nextCurve++), CurveGeometry.FromLine(line), new ParameterInterval(0, (q - p).Length));
            }
            else
            {
                // The arc runs the way the old edge ran: through the old edge's midpoint, which sits a blend
                // radius away from the sharp edge but at the same angle.
                var points = polylines[old];
                var middle = points[points.Count / 2];
                var x = c.MajorAxis ?? (p - c.Center) - c.Normal * (p - c.Center).Dot(c.Normal);
                var closed = start == end;
                var k = c.Curve(c.Normal, x);
                var ts = ConicParameter(k, p);
                if ((ConicPoint(k, ts) - p).Length > accuracy) throw new InvalidOperationException($"blend-removal-corner-off-conic: corner off {what} sharp edge by {(ConicPoint(k, ts) - p).Length:G3} mm");
                static double Ccw(double from, double to) { var d = (to - from) % (2 * Math.PI); return d < 0 ? d + 2 * Math.PI : d; }
                // Of the two arcs from start to end, the sharp edge is the one under the old edge.
                if (!closed && Ccw(ts, ConicParameter(k, middle)) > Ccw(ts, ConicParameter(k, q)))
                {
                    k = c.Curve(-c.Normal, x);
                    ts = ConicParameter(k, p);
                }
                var sweep = closed ? 2 * Math.PI : Ccw(ts, ConicParameter(k, q));
                if (sweep <= Tol) throw new InvalidOperationException($"blend-removal-strip-collapses: {what} arc has no length");
                result = new(new EdgeId(nextEdge++), start, end, new CurveGeometryId(nextCurve++), k, new ParameterInterval(ts, ts + sweep));
            }
            // The sharp edge lies on both surfaces it separates.
            var mid = result.Evaluate(0.5 * (result.Trim.Start + result.Trim.End));
            foreach (var f in new[] { a, b })
                if (Math.Abs(Surface(f).F(mid)) > accuracy) throw new InvalidOperationException($"blend-removal-sharp-edge-off-surface: {what} new edge misses face {f.Value} by {Math.Abs(Surface(f).F(mid)):G3} mm");
            return result;
        }

        // End edges: a strip's end on a kept face whose ends no longer meet (one stayed) becomes the kept face
        // meeting the anchor the staying vertex is on. Built first: the anchor's loop needs it as well.
        var endEdges = new Dictionary<EdgeId, (NewEdgeData Edge, FaceId Kept, FaceId Anchor)>();
        foreach (var e in transverse)
        {
            var edge = topology.GetEdge(e);
            var (u, v) = (Map(edge.StartVertexId), Map(edge.EndVertexId));
            if (u == v) continue;
            var kept = edgeFaces[e].Where(f => !strips.Contains(f)).ToArray();
            var s = edgeFaces[e].FirstOrDefault(bridge.ContainsKey);
            if (kept.Length != 1 || s == default)
                throw new InvalidOperationException($"blend-removal-end-edge-unsupported: edge {e.Value} keeps two vertices apart but is not a strip's end on a kept face");
            var stays = pinned.Contains(u) ? u : v;
            var anchor = new[] { bridge[s].A, bridge[s].B }.Where(f => Math.Abs(Surface(f).F(Original(stays))) <= accuracy).ToArray();
            if (anchor.Length != 1) throw new InvalidOperationException($"blend-removal-end-edge-ambiguous: vertex {stays.Value} lies on {anchor.Length} of strip {s.Value}'s anchors");
            endEdges[e] = (Build(e, u, v, SharpEdge(Surface(kept[0]), Surface(anchor[0]), s), kept[0], anchor[0], $"end edge {e.Value}'s"), kept[0], anchor[0]);
        }

        // 4. Rebuild the loops of every face outside the network.
        var model = new TopologyModel();
        var keptVertices = topology.Vertices.Select(v => Map(v.Id)).Distinct().ToArray();
        foreach (var v in keptVertices) model.AddVertex(new Vertex(v));
        var keptEdges = topology.Edges.Where(e => !touched.Contains(e.Id)).ToArray();
        foreach (var e in keptEdges) model.AddEdge(e with { StartVertexId = Map(e.StartVertexId), EndVertexId = Map(e.EndVertexId) });
        IEnumerable<NewEdgeData> AllNew() => newEdges.Values.Concat(endEdges.Values.Select(x => x.Edge));
        var edgeEnds = keptEdges.ToDictionary(e => e.Id, e => (Map(e.StartVertexId), Map(e.EndVertexId)));
        foreach (var e in endEdges.Values) edgeEnds[e.Edge.Id] = (e.Edge.Start, e.Edge.End);
        (VertexId From, VertexId To) Ends((EdgeId Edge, bool Reversed) use)
        {
            if (!edgeEnds.TryGetValue(use.Edge, out var ends)) { var made = newEdges.Values.Single(n => n.Id == use.Edge); ends = (made.Start, made.End); }
            return use.Reversed ? (ends.Item2, ends.Item1) : ends;
        }
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
                    var edge = topology.GetEdge(c.EdgeId);
                    if (endEdges.TryGetValue(c.EdgeId, out var end))
                    {
                        // The kept face keeps this end, now on the anchor it meets.
                        var start = Map(c.IsReversed ? edge.EndVertexId : edge.StartVertexId);
                        uses.Add((end.Edge.Id, start != end.Edge.Start));
                        continue;
                    }
                    if (transverse.Contains(c.EdgeId)) continue; // its ends collapse into one corner
                    var from = MapCorner(c.IsReversed ? edge.EndVertexId : edge.StartVertexId);
                    var made = NewEdge(c.EdgeId, s);
                    bool reversed;
                    if (made.Start != made.End) reversed = from != made.Start;
                    else
                    {
                        // A closed edge has one vertex: compare directions where the old edge ran.
                        var pts = polylines[c.EdgeId];
                        var i = pts.Count / 2;
                        var oldDirection = (pts[Math.Min(i + 1, pts.Count - 1)] - pts[Math.Max(i - 1, 0)]) * (c.IsReversed ? -1 : 1);
                        reversed = made.Tangent(pts[i]).Dot(oldDirection) < 0;
                    }
                    var use = (made.Id, reversed);
                    if (uses.Count == 0 || uses[^1] != use) uses.Add(use);
                }
                if (changed) changedFaces.Add(face.Id);
                if (uses.Count > 1 && uses[0] == uses[^1]) uses.RemoveAt(uses.Count - 1);
                // An anchor's loop runs along the sharp edge to the corner, then on along an end edge to the vertex
                // that stayed: insert the end edges this face owns where its loop has such a gap.
                foreach (var end in endEdges.Values.Where(x => x.Anchor == face.Id))
                    for (var i = 0; i < uses.Count; i++)
                    {
                        var to = Ends(uses[i]).To; var from = Ends(uses[(i + 1) % uses.Count]).From;
                        if (to == from) continue;
                        if (to == end.Edge.Start && from == end.Edge.End) { uses.Insert(i + 1, (end.Edge.Id, false)); break; }
                        if (to == end.Edge.End && from == end.Edge.Start) { uses.Insert(i + 1, (end.Edge.Id, true)); break; }
                    }
                if (uses.Count == 0) throw new InvalidOperationException($"blend-removal-loop-vanishes: face {face.Id.Value} loop {loopId.Value}");
                var ids = loop.CoedgeIds.Count == uses.Count && !changed ? loop.CoedgeIds.ToArray()
                    : uses.Select(_ => new CoedgeId(nextCoedge++)).ToArray();
                for (var i = 0; i < uses.Count; i++)
                    newCoedges.Add(new Coedge(ids[i], uses[i].Edge, loopId, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reversed));
                newLoops.Add(loop with { CoedgeIds = ids });
            }
        foreach (var e in AllNew()) model.AddEdge(new Edge(e.Id, e.Start, e.End));
        // Every rebuilt loop must chain: each use ends where the next begins.
        foreach (var e in newEdges.Values) edgeEnds[e.Id] = (e.Start, e.End);
        foreach (var loop in newLoops)
        {
            var uses = loop.CoedgeIds.Select(id => newCoedges.Single(c => c.Id == id)).ToArray();
            for (var i = 0; i < uses.Length; i++)
            {
                var (a, b) = edgeEnds[uses[i].EdgeId]; var (c, d) = edgeEnds[uses[(i + 1) % uses.Length].EdgeId];
                var end = uses[i].IsReversed ? a : b; var start = uses[(i + 1) % uses.Length].IsReversed ? d : c;
                if (end != start)
                    throw new InvalidOperationException($"blend-removal-loop-broken: loop {loop.Id.Value} edge {uses[i].EdgeId.Value} ends at vertex {end.Value} but edge {uses[(i + 1) % uses.Length].EdgeId.Value} starts at {start.Value}");
            }
        }
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
            points[v] = PointOf(v);
        foreach (var e in keptEdges)
        {
            var binding = body.Bindings.GetEdgeBinding(e.Id);
            var moved = clusterOf.ContainsKey(e.StartVertexId) || clusterOf.ContainsKey(e.EndVertexId);
            if (moved)
            {
                // Every face owning a re-trimmed edge needs its pcurves rebuilt, not only faces whose loops changed.
                changedFaces.UnionWith(edgeFaces[e.Id]);
                var curve = body.Geometry.GetCurve(binding.CurveGeometryId);
                var (ps, pe) = (points[Map(e.StartVertexId)], points[Map(e.EndVertexId)]);
                double s, t;
                if (curve.Line3 is { } line)
                {
                    double At(Point3D p) => (p - line.Origin).Dot(line.Direction.ToVector());
                    foreach (var p in new[] { ps, pe })
                        if ((p - line.Evaluate(At(p))).Length > accuracy) throw new InvalidOperationException($"blend-removal-corner-off-kept-edge: edge {e.Id.Value} misses its new corner by {(p - line.Evaluate(At(p))).Length:G3} mm");
                    (s, t) = (At(ps), At(pe));
                    if (!binding.OrientedEdgeSense) (s, t) = (t, s);
                }
                else if (curve.Kind is CurveGeometryKind.Circle3 or CurveGeometryKind.Ellipse3 && binding.TrimInterval is { } old)
                {
                    double Angle(Point3D p)
                    {
                        var angle = ConicParameter(curve, p);
                        var off = (ConicPoint(curve, angle) - p).Length;
                        if (off > accuracy) throw new InvalidOperationException($"blend-removal-corner-off-kept-edge: {curve.Kind} edge {e.Id.Value} misses its new corner by {off:G3} mm");
                        return angle;
                    }
                    // Unwrap each end next to where it was: corners move by a blend radius, not a turn.
                    static double Near(double angle, double reference) => reference + Math.IEEERemainder(angle - reference, 2 * Math.PI);
                    var (first, last) = binding.OrientedEdgeSense ? (ps, pe) : (pe, ps);
                    s = Near(Angle(first), old.Start);
                    t = e.StartVertexId == e.EndVertexId ? s + (old.End - old.Start) : Near(Angle(last), old.End);
                }
                else throw new InvalidOperationException($"blend-removal-moved-edge-unsupported: edge {e.Id.Value} is {curve.Kind}");
                if (t - s <= Tol) throw new InvalidOperationException($"blend-removal-edge-inverts: edge {e.Id.Value}");
                binding = binding with { TrimInterval = new ParameterInterval(s, t) };
            }
            bindings.AddEdgeBinding(binding);
        }
        foreach (var e in AllNew())
        {
            geometry.AddCurve(e.Curve, e.Geometry);
            bindings.AddEdgeBinding(new EdgeGeometryBinding(e.Id, e.Curve, e.Trim));
        }
        foreach (var b in body.Bindings.FaceBindings.Where(b => !strips.Contains(b.FaceId))) bindings.AddFaceBinding(b);
        foreach (var b in body.Bindings.FaceBoundaryRoleBindings.Where(b => !strips.Contains(b.FaceId))) bindings.AddFaceBoundaryRoleBinding(b);
        var keptCoedges = newCoedges.Select(c => c.Id).ToHashSet();
        foreach (var b in body.Bindings.PcurveBindings.Where(b => keptCoedges.Contains(b.CoedgeId) && !changedFaces.Contains(b.FaceId))) bindings.AddPcurveBinding(b);
        foreach (var b in body.Bindings.VertexLoopParameterBindings.Where(b => !changedFaces.Contains(b.FaceId) && !strips.Contains(b.FaceId))) bindings.AddVertexLoopParameterBinding(b);
        foreach (var id in bindings.EdgeBindings.Select(b => b.CurveGeometryId).Distinct().Where(id => !geometry.TryGetCurve(id, out _))) geometry.AddCurve(id, body.Geometry.GetCurve(id));
        foreach (var id in bindings.FaceBindings.Select(b => b.SurfaceGeometryId).Distinct()) geometry.AddSurface(id, body.Geometry.GetSurface(id));
        // Rebuild pcurves on the changed faces only. Populating the whole body would also invent pcurves
        // on untouched faces that never had them, which changes what the export writes for them.
        var sub = new TopologyModel();
        var subLoops = changedFaces.SelectMany(f => model.GetFace(f).LoopIds).Select(model.GetLoop).ToArray();
        var subCoedges = subLoops.SelectMany(l => l.CoedgeIds).Select(model.GetCoedge).ToArray();
        var subEdges = subCoedges.Select(c => model.GetEdge(c.EdgeId)).Distinct().ToArray();
        foreach (var v in subEdges.SelectMany(e => new[] { e.StartVertexId, e.EndVertexId }).Distinct()) sub.AddVertex(new Vertex(v));
        foreach (var e in subEdges) sub.AddEdge(e);
        foreach (var c in subCoedges) sub.AddCoedge(c);
        foreach (var l in subLoops) sub.AddLoop(l);
        foreach (var f in changedFaces) sub.AddFace(model.GetFace(f));
        var scratch = new BrepBindingModel();
        foreach (var b in bindings.EdgeBindings) scratch.AddEdgeBinding(b);
        foreach (var b in bindings.FaceBindings) scratch.AddFaceBinding(b);
        var pcurves = BrepPcurveRecovery.Populate(sub, geometry, scratch, sourceTolerance: accuracy > 1e-6 ? accuracy : null);
        if (pcurves.Diagnostics.Count > 0) throw new InvalidOperationException("blend-removal-pcurves: " + string.Join("; ", pcurves.Diagnostics.Take(3).Select(d => d.Message)));
        // Where a corner could only be met to the source's accuracy (a vertex that stayed sits a few microns
        // off the anchor in the source), the edge ending there is a tolerant edge: its traces carry the gap,
        // so the export declares it and a reader accepts it. Nothing is repaired.
        var gap = new Dictionary<EdgeId, double>();
        foreach (var e in subEdges)
        {
            var binding = bindings.GetEdgeBinding(e.Id);
            var curve = geometry.GetCurve(binding.CurveGeometryId);
            if (binding.TrimInterval is not { } trim || curve.Kind is not (CurveGeometryKind.Line3 or CurveGeometryKind.Circle3 or CurveGeometryKind.Ellipse3)) continue;
            Point3D C(double t) => curve.Line3 is { } l ? l.Evaluate(t) : ConicPoint(curve, t);
            double Miss(VertexId v) => Math.Min((points[v] - C(trim.Start)).Length, (points[v] - C(trim.End)).Length);
            var miss = Math.Max(Miss(e.StartVertexId), Miss(e.EndVertexId));
            // And how far the curve runs off the faces it bounds (a line between corners met to accuracy).
            foreach (var f in edgeFaces.TryGetValue(e.Id, out var owners) ? owners : model.Faces.Where(x => x.LoopIds.SelectMany(l => model.GetLoop(l).CoedgeIds).Any(c => model.GetCoedge(c).EdgeId == e.Id)).Select(x => x.Id).ToList())
            {
                if (strips.Contains(f) || !body.TryGetFaceSurfaceGeometry(f, out var g) || g!.Kind is not (SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Cone)) continue;
                var surface = new Implicit(f, g);
                for (var i = 0; i <= 16; i++) miss = Math.Max(miss, Math.Abs(surface.F(C(trim.Start + (trim.End - trim.Start) * i / 16))));
                // The export rewrites a line through its vertices, so a vertex off the face tilts the line off it.
                miss = Math.Max(miss, Math.Max(Math.Abs(surface.F(points[e.StartVertexId])), Math.Abs(surface.F(points[e.EndVertexId]))));
            }
            if (miss > 1e-6) gap[e.Id] = miss;
        }
        foreach (var b in scratch.PcurveBindings)
        {
            var edge = model.GetCoedge(b.CoedgeId).EdgeId;
            if (gap.TryGetValue(edge, out var miss) && miss > (b.Qualification?.QualificationToleranceMillimetres ?? 0))
            {
                var q = b.Qualification ?? new PcurveQualification(PcurveBindingOrigin.RecoveredAnalytic, 0, 0, 0, "", "");
                var method = q.RecoveryMethod.StartsWith(BrepPcurveRecovery.SourceTolerantMethod, StringComparison.Ordinal) ? q.RecoveryMethod : $"{BrepPcurveRecovery.SourceTolerantMethod}:{q.RecoveryMethod}";
                bindings.AddPcurveBinding(b with { Qualification = q with { QualificationToleranceMillimetres = miss * 1.01, RecoveryMethod = method } });
            }
            else bindings.AddPcurveBinding(b);
        }
        return new(new BrepBody(model, geometry, bindings, points, null, body.ShellRepresentation), null, newEdges.Count, clusters.Count);
    }

    private sealed record NewEdgeData(EdgeId Id, VertexId Start, VertexId End, CurveGeometryId Curve, CurveGeometry Geometry, ParameterInterval Trim)
    {
        public Point3D Evaluate(double t) => Geometry.Line3 is { } l ? l.Evaluate(t) : ConicPoint(Geometry, t);
        public Vector3D Tangent(Point3D near)
        {
            if (Geometry.Line3 is { } l) return l.Direction.ToVector();
            var t = ConicParameter(Geometry, near);
            return ConicPoint(Geometry, t + 1e-6) - ConicPoint(Geometry, t - 1e-6);
        }
    }

    /// <summary>A plane section of a cylinder or cone: a circle (minor = major, its frame set by the edge's start)
    /// or an ellipse with its major axis fixed.</summary>
    private sealed record Conic(Point3D Center, Vector3D Normal, double Major, double Minor, Vector3D? MajorAxis)
    {
        public CurveGeometry Curve(Vector3D normal, Vector3D x) => MajorAxis is null
            ? CurveGeometry.FromCircle(new Circle3Curve(Center, Direction3D.Create(normal), Major, Direction3D.Create(x)))
            : CurveGeometry.FromEllipse(new Ellipse3Curve(Center, Direction3D.Create(normal), Major, Minor, Direction3D.Create(x)));
    }

    private static (Point3D C, Vector3D X, Vector3D Y, double A, double B) Frame(CurveGeometry g) => g.Circle3 is { } c
        ? (c.Center, c.XAxis.ToVector(), c.YAxis.ToVector(), c.Radius, c.Radius)
        : (g.Ellipse3!.Value.Center, g.Ellipse3!.Value.XAxis.ToVector(), g.Ellipse3!.Value.YAxis.ToVector(), g.Ellipse3!.Value.MajorRadius, g.Ellipse3!.Value.MinorRadius);

    private static Point3D ConicPoint(CurveGeometry g, double t) { var f = Frame(g); return f.C + f.X * (f.A * Math.Cos(t)) + f.Y * (f.B * Math.Sin(t)); }

    private static double ConicParameter(CurveGeometry g, Point3D p)
    {
        var f = Frame(g); var d = p - f.C;
        var t = Math.Atan2(d.Dot(f.Y) / f.B, d.Dot(f.X) / f.A);
        return t < 0 ? t + 2 * Math.PI : t;
    }

    /// <summary>A face's support as an implicit function: signed distance for planes and cylinders, distance to
    /// the generator for cones.</summary>
    private sealed record Implicit(FaceId Face, SurfaceGeometry Surface)
    {
        public double F(Point3D x) => Eval(x).F;
        public Vector3D Gradient(Point3D x) => Eval(x).G;
        private (double F, Vector3D G) Eval(Point3D x)
        {
            if (Surface.Plane is { } p) return (p.Normal.ToVector().Dot(x - p.Origin), p.Normal.ToVector());
            var (origin, axis) = Surface.Cylinder is { } c ? (c.Origin, c.Axis.ToVector()) : (Surface.Cone!.Value.Apex, Surface.Cone!.Value.Axis.ToVector());
            var v = x - origin; var h = v.Dot(axis); var radial = v - axis * h; var rho = radial.Length;
            var unit = rho > 1e-12 ? radial * (1 / rho) : Vector3D.Zero;
            if (Surface.Cylinder is { } cylinder) return (rho - cylinder.Radius, unit);
            var a = Surface.Cone!.Value.SemiAngleRadians; var side = h < 0 ? -1d : 1d;
            return (rho * Math.Cos(a) - Math.Abs(h) * Math.Sin(a), unit * Math.Cos(a) - axis * (side * Math.Sin(a)));
        }
    }

    /// <summary>The circle where a plane meets a cylinder or cone across its axis; null for two planes or a plane
    /// along a cylinder's axis, whose sharp edge is the straight line between its corners.</summary>
    private static Conic? SharpEdge(Implicit a, Implicit b, FaceId strip)
    {
        if (a.Surface.Plane is null) (a, b) = (b, a);
        if (a.Surface.Plane is not { } plane) throw new InvalidOperationException($"blend-removal-anchor-pair-unsupported: strip {strip.Value} bridges {a.Surface.Kind} and {b.Surface.Kind}");
        if (b.Surface.Plane is not null) return null;
        var n = plane.Normal.ToVector();
        var (origin, axis) = b.Surface.Cylinder is { } c ? (c.Origin, c.Axis.ToVector()) : (b.Surface.Cone!.Value.Apex, b.Surface.Cone!.Value.Axis.ToVector());
        var along = n.Dot(axis);
        if (Math.Abs(along) < 1e-9 && b.Surface.Cylinder is not null) return null;
        if (Math.Abs(along) < 1e-9) throw new InvalidOperationException($"blend-removal-anchor-pair-unsupported: strip {strip.Value}'s plane runs along a cone");
        var center = origin + axis * ((plane.Origin - origin).Dot(n) / along);
        if (Math.Abs(Math.Abs(along) - 1) > 1e-9)
        {
            // An oblique plane cuts a cylinder in an ellipse: minor radius r across, major r/|cos| along the axis.
            if (b.Surface.Cylinder is not { } oblique) throw new InvalidOperationException($"blend-removal-anchor-pair-oblique: strip {strip.Value} meets a {b.Surface.Kind} obliquely");
            var major = axis - n * along;
            return new(center, n, oblique.Radius / Math.Abs(along), oblique.Radius, major * (1 / major.Length));
        }
        var radius = b.Surface.Cylinder is { } cylinder ? cylinder.Radius : Math.Abs((center - origin).Dot(axis)) * Math.Tan(b.Surface.Cone!.Value.SemiAngleRadians);
        if (radius < 1e-9) throw new InvalidOperationException($"blend-removal-sharp-edge-degenerate: strip {strip.Value}'s plane meets the cone at its apex");
        return new(center, axis, radius, radius, null);
    }

    /// <summary>Corner on curved faces: Gauss-Newton with the minimum-norm step from the seed, so an
    /// underdetermined cluster (two faces) lands on the nearest point of their intersection.</summary>
    private static Point3D SolveImplicit(Implicit[] surfaces, Point3D seed, string where, double accuracy)
    {
        if (surfaces.Length < 2) throw new InvalidOperationException($"blend-removal-corner-underdetermined: {where}");
        var x = seed;
        var m = surfaces.Length;
        for (var iteration = 0; iteration < 100; iteration++)
        {
            var f = surfaces.Select(s => s.F(x)).ToArray();
            if (f.Max(Math.Abs) < 1e-11) break;
            var g = surfaces.Select(s => s.Gradient(x)).ToArray();
            // delta = J^T (J J^T + lambda I)^-1 f
            var a = new double[m, m + 1];
            for (var i = 0; i < m; i++) { for (var j = 0; j < m; j++) a[i, j] = g[i].Dot(g[j]) + (i == j ? 1e-12 : 0); a[i, m] = f[i]; }
            for (var col = 0; col < m; col++)
            {
                var pivot = Enumerable.Range(col, m - col).MaxBy(r => Math.Abs(a[r, col]));
                for (var k = 0; k <= m; k++) (a[col, k], a[pivot, k]) = (a[pivot, k], a[col, k]);
                for (var r = 0; r < m; r++)
                {
                    if (r == col) continue;
                    var factor = a[r, col] / a[col, col];
                    for (var k = col; k <= m; k++) a[r, k] -= factor * a[col, k];
                }
            }
            var step = Vector3D.Zero;
            for (var i = 0; i < m; i++) step += g[i] * (a[i, m] / a[i, i]);
            x -= step;
        }
        var residual = surfaces.Max(s => Math.Abs(s.F(x)));
        if (residual > Math.Max(1e-7, accuracy)) throw new InvalidOperationException($"blend-removal-corner-not-common: {surfaces.Length} surfaces miss one point by {residual:G3} mm at {where}");
        if ((x - seed).Length > 100) throw new InvalidOperationException($"blend-removal-corner-far: corner moved {(x - seed).Length:G3} mm at {where}");
        return x;
    }

    /// <summary>Point common to the planes (least squares, then checked), or the point on their
    /// line nearest the seed when only two planes meet there.</summary>
    private static Point3D Solve(PlaneSurface[] planes, Point3D seed, string where, double accuracy)
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
        if (residual > accuracy) throw new InvalidOperationException($"blend-removal-corner-not-common: {planes.Length} planes miss one point by {residual:G3} mm at {where}");
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
