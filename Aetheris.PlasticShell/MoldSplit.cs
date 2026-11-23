using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.PlasticShell;

/// <summary>
/// Cuts the faces that cross the parting plane into the part above it and the part below, so that each half's
/// faces end exactly on the plane. Edges are split where they cross; each crossing face is then cut along the
/// plane's trace on its surface:
/// <list type="bullet">
/// <item>a plane not parallel to the parting plane, along one line;</item>
/// <item>a cylinder lying across the pull with its axis on the parting plane, along its two silhouette lines;</item>
/// <item>a cylinder or a cone about the pull, along a circle.</item>
/// </list>
/// Any other crossing face is refused, with the reason. Points on the trace are paired where the segment between
/// them lies on the face, which the caller tests against the face's mesh.
/// </summary>
internal static class MoldSplit
{
    /// <param name="Pieces">Each piece of a cut face, and the side of the plane it lies on: +1 above, −1 below.</param>
    public sealed record Result(BrepBody Body, IReadOnlyList<int> SplitFaces, IReadOnlyDictionary<int, int> Pieces);

    /// <param name="onFace">The distance from a point to a face's mesh.</param>
    /// <param name="meshTolerance">How far a point on a face may lie from its mesh.</param>
    public static Result? AtPlane(BrepBody part, Vector3D pull, double height, double tolerance, Func<int, Point3D, double> onFace, double meshTolerance, out string? reason)
    {
        reason = null;
        var source = part.Topology;
        double H(Point3D p) => (p - Point3D.Origin).Dot(pull);
        var next = new[] { source.Vertices.Max(x => x.Id.Value), source.Edges.Max(x => x.Id.Value), source.Coedges.Max(x => x.Id.Value), source.Loops.Max(x => x.Id.Value),
            source.Faces.Max(x => x.Id.Value), source.Shells.Max(x => x.Id.Value), source.Bodies.Max(x => x.Id.Value) }.Max() + 1;
        var nextCurve = part.Geometry.Curves.Select(c => c.Key.Value).DefaultIfEmpty(0).Max() + 1;
        var points = source.Vertices.ToDictionary(v => v.Id, v => { part.TryGetVertexPoint(v.Id, out var p); return p; });
        var geometry = part.Geometry.Clone();
        var edges = source.Edges.ToDictionary(e => e.Id);
        var edgeBindings = part.Bindings.EdgeBindings.ToDictionary(b => b.EdgeId);

        // 1. Split every edge where it crosses the plane, into sub-edges in the edge's own direction.
        var pieces = new Dictionary<EdgeId, EdgeId[]>();
        foreach (var edge in source.Edges)
        {
            var binding = part.Bindings.GetEdgeBinding(edge.Id);
            var curve = part.Geometry.GetCurve(binding.CurveGeometryId);
            if (binding.TrimInterval is not { } trim) continue;
            var crossings = Crossings(curve, trim, pull, height, tolerance);
            if (crossings is null)
            {
                var (a, b) = (H(points[edge.StartVertexId]) - height, H(points[edge.EndVertexId]) - height);
                if (a * b < 0 && double.Abs(a) > tolerance && double.Abs(b) > tolerance) { reason = $"edge {edge.Id.Value} ({curve.Kind}) crosses the parting plane and is not a line or a circle"; return null; }
                continue;
            }
            if (crossings.Count == 0) continue;
            // Parameters in the edge's direction.
            var along = binding.OrientedEdgeSense ? crossings.Order().ToList() : crossings.OrderDescending().ToList();
            var ends = binding.OrientedEdgeSense ? (trim.Start, trim.End) : (trim.End, trim.Start);
            var parameters = new List<double> { ends.Item1 }; parameters.AddRange(along); parameters.Add(ends.Item2);
            var vertices = new List<VertexId> { edge.StartVertexId };
            foreach (var t in along) { var id = new VertexId(next++); points[id] = Evaluate(curve, t); vertices.Add(id); }
            vertices.Add(edge.EndVertexId);
            var subs = new EdgeId[parameters.Count - 1];
            for (var i = 0; i < subs.Length; i++)
            {
                subs[i] = new EdgeId(next++);
                edges[subs[i]] = new Edge(subs[i], vertices[i], vertices[i + 1]);
                var (lo, hi) = binding.OrientedEdgeSense ? (parameters[i], parameters[i + 1]) : (parameters[i + 1], parameters[i]);
                edgeBindings[subs[i]] = new EdgeGeometryBinding(subs[i], binding.CurveGeometryId, new ParameterInterval(lo, hi), binding.OrientedEdgeSense);
            }
            edges.Remove(edge.Id); edgeBindings.Remove(edge.Id);
            pieces[edge.Id] = subs;
        }

        // The loops with their coedges re-expressed over the split edges.
        var faceOfLoop = source.Faces.SelectMany(f => f.LoopIds.Select(l => (l, f.Id))).ToDictionary(x => x.l, x => x.Id);
        var uses = new Dictionary<LoopId, List<(EdgeId Edge, bool Reversed)>>();
        foreach (var loop in source.Loops.Where(l => l.Kind != LoopKind.Vertex))
            uses[loop.Id] = loop.CoedgeIds.Select(source.GetCoedge).SelectMany(c => pieces.TryGetValue(c.EdgeId, out var subs)
                ? (c.IsReversed ? subs.Reverse().Select(s => (s, true)) : subs.Select(s => (s, false)))
                : [(c.EdgeId, c.IsReversed)]).ToList();
        VertexId Start((EdgeId Edge, bool Reversed) u) => u.Reversed ? edges[u.Edge].EndVertexId : edges[u.Edge].StartVertexId;
        VertexId End((EdgeId Edge, bool Reversed) u) => u.Reversed ? edges[u.Edge].StartVertexId : edges[u.Edge].EndVertexId;
        int SideOf((EdgeId Edge, bool Reversed) u)
        {
            var b = edgeBindings[u.Edge]; var mid = Evaluate(geometry.GetCurve(b.CurveGeometryId), (b.TrimInterval!.Value.Start + b.TrimInterval.Value.End) / 2);
            var d = H(mid) - height;
            return double.Abs(d) <= tolerance ? 0 : double.Sign(d);
        }

        // 2. Cut each crossing face into the part above the plane and the part below.
        var newFaces = new Dictionary<FaceId, List<(FaceId Face, List<List<(EdgeId, bool)>> Loops)>>();
        var split = new List<int>();
        var pieceSides = new Dictionary<int, int>();
        foreach (var face in source.Faces)
        {
            var loops = face.LoopIds.Where(uses.ContainsKey).Select(l => uses[l]).ToList();
            var sides = loops.SelectMany(l => l.Select(SideOf)).ToArray();
            if (!(sides.Contains(1) && sides.Contains(-1))) continue;
            if (sides.Contains(0)) { reason = $"face {face.Id.Value} has an edge on the parting plane and crosses it"; return null; }
            if (face.LoopIds.Any(l => !uses.ContainsKey(l))) { reason = $"face {face.Id.Value} crosses the parting plane and has a vertex loop"; return null; }
            part.TryGetFaceSurfaceGeometry(face.Id, out var surface);
            var crossingAt = new List<VertexId>();
            foreach (var loop in loops)
                for (var i = 0; i < loop.Count; i++)
                    if (SideOf(loop[i]) != SideOf(loop[(i + 1) % loop.Count])) crossingAt.Add(End(loop[i]));
            var distinct = crossingAt.Distinct().ToList();

            // The plane's trace on the surface, and the segments of it that lie on the face.
            var segments = new List<(VertexId From, VertexId To, CurveGeometry Curve, ParameterInterval Trim)>();
            if (surface?.Plane is { } plane)
            {
                var d = plane.Normal.ToVector().Cross(pull);
                if (d.Length < 1e-9) { reason = $"face {face.Id.Value} is parallel to the parting plane"; return null; }
                d /= d.Length;
                Pair(distinct.OrderBy(v => (points[v] - Point3D.Origin).Dot(d)).ToList(), d);
            }
            else if (surface?.Cylinder is { } cylinder)
            {
                var axis = cylinder.Axis.ToVector();
                if (double.Abs(axis.Dot(pull)) < 1e-9)
                {
                    if (double.Abs(H(cylinder.Origin) - height) > tolerance) { reason = $"face {face.Id.Value} (a cylinder across the pull) crosses the parting plane off its axis, so it is not split along its silhouette"; return null; }
                    var w = axis.Cross(pull); w /= w.Length;
                    foreach (var line in distinct.GroupBy(v => double.Sign((points[v] - cylinder.Origin).Dot(w))))
                        Pair(line.OrderBy(v => (points[v] - Point3D.Origin).Dot(axis)).ToList(), axis);
                }
                else if (1 - double.Abs(axis.Dot(pull)) < 1e-9)
                    Circle(cylinder.Origin + axis * ((height - H(cylinder.Origin)) / axis.Dot(pull)), axis, cylinder.XAxis.ToVector(), cylinder.Radius);
                else { reason = $"face {face.Id.Value} (a cylinder tilted to the pull) crosses the parting plane"; return null; }
            }
            else if (surface?.Cone is { } cone && 1 - double.Abs(cone.Axis.ToVector().Dot(pull)) < 1e-9)
            {
                var axis = cone.Axis.ToVector();
                var along = (height - H(cone.Apex)) / axis.Dot(pull);
                Circle(cone.Apex + axis * along, axis, cone.ReferenceAxis.ToVector(), double.Abs(along) * double.Tan(cone.SemiAngleRadians));
            }
            else { reason = $"face {face.Id.Value} ({surface?.Kind.ToString() ?? "unknown surface"}) crosses the parting plane; only planes, cylinders across or about the pull, and cones about it are split"; return null; }

            // The trace of a surface of revolution about the pull: a circle, cut where the face's boundary crosses it.
            void Circle(Point3D centre, Vector3D axis, Vector3D reference, double radius)
            {
                var x = reference - axis * reference.Dot(axis); x /= x.Length;
                var y = axis.Cross(x);
                double Angle(VertexId v) { var r = points[v] - centre; var a = double.Atan2(r.Dot(y), r.Dot(x)); return a < 0 ? a + 2 * double.Pi : a; }
                var ordered = distinct.OrderBy(Angle).ToList();
                var circle = CurveGeometry.FromCircle(new Circle3Curve(centre, Direction3D.Create(axis), radius, Direction3D.Create(x)));
                if (ordered.Count == 1) { segments.Add((ordered[0], ordered[0], circle, new ParameterInterval(Angle(ordered[0]), Angle(ordered[0]) + 2 * double.Pi))); return; }
                // Consecutive points by angle, the last to the first across 2π; keep the arcs that lie on the face.
                var used = new HashSet<VertexId>();
                for (var k = 0; k < ordered.Count; k++)
                {
                    var (a, b) = (ordered[k], ordered[(k + 1) % ordered.Count]);
                    if (used.Contains(a) || used.Contains(b)) continue;
                    var (ta, tb) = (Angle(a), Angle(b)); if (tb <= ta) tb += 2 * double.Pi;
                    var mid = circle.Circle3!.Value.Evaluate((ta + tb) / 2);
                    if (onFace(face.Id.Value, mid) > meshTolerance) continue;
                    segments.Add((a, b, circle, new ParameterInterval(ta, tb))); used.Add(a); used.Add(b);
                }
            }

            void Pair(List<VertexId> ordered, Vector3D direction)
            {
                var used = new HashSet<VertexId>();
                for (var k = 0; k + 1 < ordered.Count; k++)
                {
                    var (a, b) = (ordered[k], ordered[k + 1]);
                    if (used.Contains(a) || used.Contains(b)) continue;
                    var mid = Point3D.Origin + ((points[a] - Point3D.Origin) + (points[b] - Point3D.Origin)) * 0.5;
                    if (onFace(face.Id.Value, mid) > meshTolerance) continue;
                    var length = (points[b] - points[a]).Length;
                    segments.Add((a, b, CurveGeometry.FromLine(new Line3Curve(points[a], Direction3D.Create(points[b] - points[a]))), new ParameterInterval(0d, length)));
                    used.Add(a); used.Add(b);
                }
            }

            var covered = segments.SelectMany(s => new[] { s.From, s.To }).ToHashSet();
            if (!distinct.All(covered.Contains) || segments.Count * 2 != distinct.Count + segments.Count(s => s.From == s.To))
            { reason = $"face {face.Id.Value}: the parting plane's trace across it does not pair up into segments on the face"; return null; }

            // The cut edges, one per segment.
            var cutAt = new Dictionary<VertexId, (EdgeId Edge, VertexId Other)>();
            foreach (var s in segments)
            {
                var id = new EdgeId(next++);
                var curveId = new CurveGeometryId(nextCurve++);
                geometry.AddCurve(curveId, s.Curve);
                edges[id] = new Edge(id, s.From, s.To);
                edgeBindings[id] = new EdgeGeometryBinding(id, curveId, s.Trim);
                cutAt[s.From] = (id, s.To);
                if (s.To != s.From) cutAt[s.To] = (id, s.From);
            }

            // Walk each piece: along the boundary on one side; where the boundary crosses, along the cut to its
            // other end, and on along the boundary there.
            var all = loops.SelectMany(l => l).ToList();
            var remaining = all.ToHashSet();
            var made = new List<(FaceId, List<List<(EdgeId, bool)>>)>();
            while (remaining.Count > 0)
            {
                var first = all.First(remaining.Contains);
                var side = SideOf(first);
                var cycle = new List<(EdgeId, bool)>();
                var current = first;
                for (var guard = 0; guard < 10000; guard++)
                {
                    cycle.Add(current); remaining.Remove(current);
                    var end = End(current);
                    var loop = loops.First(l => l.Contains(current));
                    var following = loop[(loop.IndexOf(current) + 1) % loop.Count];
                    if (SideOf(following) != side)
                    {
                        var (cut, other) = cutAt[end];
                        var cutReversed = edges[cut].StartVertexId != end || (other == end && !Leaves(face.Id, cut, side));
                        cycle.Add((cut, cutReversed));
                        var leaving = all.Where(u => Start(u) == other && SideOf(u) == side).ToArray();
                        if (leaving.Length != 1) { reason = $"face {face.Id.Value}: the cut does not continue into one boundary on its side"; return null; }
                        following = leaving[0];
                    }
                    if (following == first) break;
                    current = following;
                }
                var piece = new FaceId(next++);
                pieceSides[piece.Value] = side;
                made.Add((piece, [cycle]));
            }
            newFaces[face.Id] = made;
            split.Add(face.Id.Value);

            // A closed cut (a circle about the pull, from one point back to it) stands in, for the piece on one side,
            // for the face's boundary on the other: it runs about the axis the way that boundary does.
            bool Leaves(FaceId faceId, EdgeId cut, int pieceSide)
            {
                var circle = geometry.GetCurve(edgeBindings[cut].CurveGeometryId).Circle3!.Value;
                double Angle(Point3D p) { var r = p - circle.Center; return double.Atan2(r.Dot(circle.YAxis.ToVector()), r.Dot(circle.XAxis.ToVector())); }
                var sweep = 0d;
                foreach (var u in all.Where(u => SideOf(u) == -pieceSide))
                {
                    var b = edgeBindings[u.Edge]; var c = geometry.GetCurve(b.CurveGeometryId); var t = b.TrimInterval!.Value;
                    var samples = Enumerable.Range(0, 17).Select(k => Evaluate(c, t.Start + (t.End - t.Start) * k / 16d)).ToArray();
                    if (b.OrientedEdgeSense == u.Reversed) samples = samples.Reverse().ToArray();
                    for (var k = 1; k < samples.Length; k++)
                    {
                        var d = Angle(samples[k]) - Angle(samples[k - 1]);
                        sweep += d > double.Pi ? d - 2 * double.Pi : d < -double.Pi ? d + 2 * double.Pi : d;
                    }
                }
                return sweep > 0;
            }
        }
        if (split.Count == 0) return new(part, [], pieceSides);

        // 3. The split body: every unsplit face as it was over the split edges, the pieces in place of the rest.
        var topology = new TopologyModel();
        var bindings = new BrepBindingModel();
        foreach (var id in points.Keys) topology.AddVertex(new Vertex(id));
        var usedEdges = new HashSet<EdgeId>();
        var faceIds = new List<FaceId>();
        LoopId AddLoop(IReadOnlyList<(EdgeId Edge, bool Reversed)> cycle, LoopId? keep = null)
        {
            var loop = keep ?? new LoopId(next++);
            var ids = cycle.Select(_ => new CoedgeId(next++)).ToArray();
            for (var i = 0; i < cycle.Count; i++)
            {
                usedEdges.Add(cycle[i].Edge);
                topology.AddCoedge(new Coedge(ids[i], cycle[i].Edge, loop, ids[(i + 1) % ids.Length], ids[(i - 1 + ids.Length) % ids.Length], cycle[i].Reversed));
            }
            topology.AddLoop(new Loop(loop, ids));
            return loop;
        }
        foreach (var face in source.Faces.OrderBy(f => f.Id.Value))
        {
            var binding = part.Bindings.GetFaceBinding(face.Id);
            if (newFaces.TryGetValue(face.Id, out var made))
            {
                foreach (var (id, cycles) in made)
                {
                    var loopIds = cycles.Select(c => AddLoop(c)).ToArray();
                    topology.AddFace(new Face(id, loopIds));
                    bindings.AddFaceBinding(binding with { FaceId = id });
                    bindings.AddFaceBoundaryRoleBinding(new(id, loopIds[0], FaceBoundaryRole.Outer));
                    faceIds.Add(id);
                }
                continue;
            }
            foreach (var loopId in face.LoopIds)
            {
                if (!uses.TryGetValue(loopId, out var cycle))
                {
                    topology.AddLoop(source.GetLoop(loopId));
                    if (part.Bindings.TryGetVertexLoopParameterBinding(loopId, out var vertexLoop)) bindings.AddVertexLoopParameterBinding(vertexLoop);
                }
                else AddLoop(cycle, loopId);
                if (part.Bindings.TryGetFaceBoundaryRoleBinding(loopId, out var role)) bindings.AddFaceBoundaryRoleBinding(role);
            }
            topology.AddFace(face);
            bindings.AddFaceBinding(binding);
            faceIds.Add(face.Id);
        }
        foreach (var id in usedEdges) { topology.AddEdge(edges[id]); bindings.AddEdgeBinding(edgeBindings[id]); }
        var shell = new ShellId(next++);
        topology.AddShell(new Shell(shell, faceIds.ToArray()));
        topology.AddBody(new Body(new BodyId(next++), [shell]));
        return new(new BrepBody(topology, geometry, bindings, points), split, pieceSides);
    }

    /// <summary>The parameters strictly inside <paramref name="trim"/> where a line or circle crosses the plane, or null for other curves.</summary>
    private static IReadOnlyList<double>? Crossings(CurveGeometry curve, ParameterInterval trim, Vector3D pull, double height, double tolerance)
    {
        double H(Point3D p) => (p - Point3D.Origin).Dot(pull);
        var (lo, hi) = (double.Min(trim.Start, trim.End), double.Max(trim.Start, trim.End));
        if (curve.Kind == CurveGeometryKind.Line3)
        {
            var line = curve.Line3!.Value;
            var rate = line.Direction.ToVector().Dot(pull);
            if (double.Abs(rate) < 1e-12) return [];
            var t = (height - H(line.Origin)) / rate;
            return t > lo + tolerance / double.Abs(rate) && t < hi - tolerance / double.Abs(rate) ? [t] : [];
        }
        if (curve.Kind == CurveGeometryKind.Circle3)
        {
            var circle = curve.Circle3!.Value;
            var (a, b) = (circle.Radius * circle.XAxis.ToVector().Dot(pull), circle.Radius * circle.YAxis.ToVector().Dot(pull));
            var amplitude = double.Sqrt(a * a + b * b);
            var offset = height - H(circle.Center);
            if (amplitude <= double.Abs(offset) + tolerance) return [];
            var phase = double.Atan2(b, a); var spread = double.Acos(offset / amplitude);
            var margin = tolerance / circle.Radius;
            var found = new List<double>();
            foreach (var root in new[] { phase + spread, phase - spread })
                for (var k = -3; k <= 3; k++)
                {
                    var t = root + 2 * double.Pi * k;
                    if (t > lo + margin && t < hi - margin && found.All(f => double.Abs(f - t) > margin)) found.Add(t);
                }
            return found;
        }
        return null;
    }

    private static Point3D Evaluate(CurveGeometry curve, double t) => curve.Kind switch
    {
        CurveGeometryKind.Line3 => curve.Line3!.Value.Evaluate(t),
        CurveGeometryKind.Circle3 => curve.Circle3!.Value.Evaluate(t),
        CurveGeometryKind.Ellipse3 => curve.Ellipse3!.Value.Evaluate(t),
        CurveGeometryKind.BSpline3 => curve.BSpline3!.Value.Evaluate(t),
        _ => throw new NotSupportedException(curve.Kind.ToString()),
    };
}
