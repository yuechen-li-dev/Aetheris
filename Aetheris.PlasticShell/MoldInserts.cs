using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.PlasticShell;

/// <summary>
/// The core and cavity inserts of a two-plate tool, cut from a block around the part. Nothing is intersected: every
/// face of the part already knows which half forms it, so each insert is assembled from
/// <list type="bullet">
/// <item>the part's faces of its half, reversed (the insert's material is outside the part), on the part's own
/// edges and exact surfaces;</item>
/// <item>the parting face: the block's section at the parting plane less the part's outline there, bounded on the
/// inside by the part's own parting-line edges;</item>
/// <item>a shut-off face across each opening the two halves meet in;</item>
/// <item>the rest of the block on its side of the parting plane.</item>
/// </list>
/// A side action's slide carries a pin: the action's faces reversed, closed by a cap across the mouth where the
/// action's faces meet the rest of the part; the half the mouth opens into gets the same cap, the other way round.
/// Block = cavity + core + part + pins, which the evidence states.
/// </summary>
internal static class MoldInserts
{
    public sealed record Slide(string Name, Vector3D Pull, IReadOnlyList<int> Faces);

    public sealed record Result(BrepBody Cavity, BrepBody Core, IReadOnlyList<(string Name, BrepBody Body)> Slides, MoldInsertEvidence Evidence);

    private sealed record Mouth(IReadOnlyList<int> Edges, Vector3D Normal);

    public static Result? Build(BrepBody part, IReadOnlyDictionary<int, MoldFaceSide> sideOf, IReadOnlyList<MoldPartingLoop> loops, Vector3D pull,
        double partingHeight, double margin, (Point3D Min, Point3D Max) meshBounds, IReadOnlyList<Slide> slides, out string? reason)
    {
        reason = null;
        var faces = part.Topology.Faces.Select(f => f.Id.Value).ToArray();
        var slideOf = slides.SelectMany(s => s.Faces.Select(f => (f, s))).ToDictionary(x => x.f, x => x.s);
        var unassigned = faces.Where(f => !slideOf.ContainsKey(f) && sideOf.GetValueOrDefault(f, MoldFaceSide.Vertical) is not (MoldFaceSide.Positive or MoldFaceSide.Negative)).ToArray();
        if (unassigned.Length > 0) { reason = $"face(s) {string.Join(", ", unassigned)} are formed by neither half alone (split, parallel to the pull with both ways open, or formed by an action)"; return null; }
        if (loops.Any(l => l.Kind == MoldPartingLoopKind.Stepped)) { reason = "the parting line leaves the parting plane, so the parting face is not flat"; return null; }
        var parting = loops.Where(l => l.Kind == MoldPartingLoopKind.Parting).ToArray();
        if (parting.Length == 0) { reason = "the part has no parting line on the parting plane"; return null; }

        // The pull frame.
        var helper = double.Abs(pull.X) < 0.9 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0);
        var u = helper - pull * helper.Dot(pull); u /= u.Length;
        var v = pull.Cross(u);
        double U(Point3D p) => (p - Point3D.Origin).Dot(u); double V(Point3D p) => (p - Point3D.Origin).Dot(v); double H(Point3D p) => (p - Point3D.Origin).Dot(pull);
        var corners = new[] { meshBounds.Min, meshBounds.Max, new Point3D(meshBounds.Min.X, meshBounds.Min.Y, meshBounds.Max.Z), new Point3D(meshBounds.Min.X, meshBounds.Max.Y, meshBounds.Min.Z),
            new Point3D(meshBounds.Max.X, meshBounds.Min.Y, meshBounds.Min.Z), new Point3D(meshBounds.Max.X, meshBounds.Max.Y, meshBounds.Min.Z), new Point3D(meshBounds.Max.X, meshBounds.Min.Y, meshBounds.Max.Z), new Point3D(meshBounds.Min.X, meshBounds.Max.Y, meshBounds.Max.Z) };
        var (u0, u1) = (corners.Min(U) - margin, corners.Max(U) + margin);
        var (v0, v1) = (corners.Min(V) - margin, corners.Max(V) + margin);
        var (h0, h1) = (corners.Min(H) - margin, corners.Max(H) + margin);
        Point3D At(double a, double b, double h) => Point3D.Origin + u * a + v * b + pull * h;

        // The outermost loop on the parting plane bounds the parting face; loops inside it ring openings and are
        // shut-offs, at the plane or off it.
        var polygons = loops.ToDictionary(l => l, l => Polygon(part, l.EdgeIds, U, V));
        var outer = parting.MaxBy(l => double.Abs(Area(polygons[l])))!;
        var shutOffs = loops.Where(l => l != outer).ToArray();
        foreach (var loop in shutOffs.Where(l => l.Kind == MoldPartingLoopKind.Parting))
            if (!Inside(polygons[outer], polygons[loop][0])) { reason = "the parting plane meets the part in more than one outline"; return null; }

        // Each slide's mouth: the edges between its faces and the rest of the part, in one flat loop across its pull,
        // opening into one half.
        var faceOfLoop = part.Topology.Faces.SelectMany(f => f.LoopIds.Select(l => (l, f.Id.Value))).ToDictionary(x => x.l, x => x.Value);
        var edgeFaces = part.Topology.Coedges.GroupBy(c => c.EdgeId.Value).ToDictionary(g => g.Key, g => g.Select(c => faceOfLoop[c.LoopId]).ToArray());
        var mouths = new Dictionary<MoldFaceSide, List<Mouth>> { [MoldFaceSide.Positive] = [], [MoldFaceSide.Negative] = [] };
        var pinMouths = new Dictionary<Slide, Mouth>();
        foreach (var slide in slides)
        {
            var own = slide.Faces.ToHashSet();
            var mouthEdges = edgeFaces.Where(e => e.Value.Count(own.Contains) == 1).Select(e => e.Key).Order().ToArray();
            var owners = mouthEdges.Select(e => sideOf.GetValueOrDefault(edgeFaces[e].First(f => !own.Contains(f)), MoldFaceSide.Vertical)).Distinct().ToArray();
            if (mouthEdges.Length == 0) { reason = $"action {slide.Name}'s faces meet no other face of the part"; return null; }
            if (owners.Length != 1 || owners[0] is not (MoldFaceSide.Positive or MoldFaceSide.Negative))
            { reason = $"action {slide.Name}'s mouth (edges {string.Join(", ", mouthEdges)}) does not open into one half"; return null; }
            var heights = mouthEdges.SelectMany(e => Samples(part, new EdgeId(e))).Select(p => (p - Point3D.Origin).Dot(slide.Pull)).ToArray();
            if (heights.Max() - heights.Min() > 1e-6 * double.Max(1d, (meshBounds.Max - meshBounds.Min).Length))
            { reason = $"action {slide.Name}'s mouth is not flat across its pull, so its pin has no flat cap"; return null; }
            mouths[owners[0]].Add(new(mouthEdges, -slide.Pull));
            pinMouths[slide] = new(mouthEdges, slide.Pull);
        }

        var cavity = Assemble(part, f => !slideOf.ContainsKey(f) && sideOf.GetValueOrDefault(f, MoldFaceSide.Vertical) == MoldFaceSide.Positive, outer, shutOffs, pull, partingHeight, h1, (u0, u1, v0, v1), At, mouths[MoldFaceSide.Positive], null, out var convention, out reason);
        if (cavity is null) return null;
        var core = Assemble(part, f => !slideOf.ContainsKey(f) && sideOf.GetValueOrDefault(f, MoldFaceSide.Vertical) == MoldFaceSide.Negative, outer, shutOffs, -pull, partingHeight, h0, (u0, u1, v0, v1), At, mouths[MoldFaceSide.Negative], null, out _, out reason);
        if (core is null) return null;
        var pins = new List<(string, BrepBody)>();
        foreach (var slide in slides)
        {
            var own = slide.Faces.ToHashSet();
            var pin = Assemble(part, own.Contains, null, [], slide.Pull, 0, 0, default, At, [pinMouths[slide]], convention, out _, out reason);
            if (pin is null) { reason = $"action {slide.Name}'s pin: {reason}"; return null; }
            pins.Add((slide.Name, pin));
        }

        var block = (u1 - u0) * (v1 - v0) * (h1 - h0);
        var cavityMass = BrepMassProperties.Evaluate(cavity);
        var coreMass = BrepMassProperties.Evaluate(core);
        if (!cavityMass.IsEnclosed || !cavityMass.IsOrientationConsistent || !coreMass.IsEnclosed || !coreMass.IsOrientationConsistent)
        { reason = "an insert did not close into a consistently oriented solid"; return null; }
        var slideEvidence = new List<MoldSlideEvidence>();
        foreach (var (name, pin) in pins)
        {
            var mass = BrepMassProperties.Evaluate(pin);
            if (!mass.IsEnclosed || !mass.IsOrientationConsistent || mass.SignedVolume <= 0) { reason = $"action {name}'s pin did not close into a consistently oriented solid"; return null; }
            var slide = slides.First(s => s.Name == name);
            slideEvidence.Add(new(name, Direction3D.Create(slide.Pull), mass.SignedVolume, pin.Topology.Faces.Count(), slide.Faces));
        }
        return new(cavity, core, pins, new(margin, At(u0, v0, h0), At(u1, v1, h1), block, cavityMass.SignedVolume, coreMass.SignedVolume,
            BrepMassProperties.Evaluate(part).AbsoluteVolume, cavity.Topology.Faces.Count(), core.Topology.Faces.Count(), shutOffs.Length, slideEvidence));
    }

    /// <summary>
    /// One insert: the part's faces of a half (<paramref name="member"/>) reversed, the parting face at
    /// <paramref name="partingHeight"/>, shut-off faces, a cap across each slide's mouth that opens into this half,
    /// and the block from the parting plane to <paramref name="farHeight"/>. <paramref name="away"/> points from the
    /// parting plane into this half. Without a parting loop it is a slide's pin: its faces and its mouth's cap, with
    /// the loop convention (<paramref name="given"/>) read off an insert.
    /// </summary>
    private static BrepBody? Assemble(BrepBody part, Func<int, bool> member, MoldPartingLoop? outer,
        IReadOnlyList<MoldPartingLoop> shutOffs, Vector3D away, double partingHeight, double farHeight, (double U0, double U1, double V0, double V1) box,
        Func<double, double, double, Point3D> at, IReadOnlyList<Mouth> mouths, int? given, out int convention, out string? reason)
    {
        reason = null; convention = given ?? 0;
        var source = part.Topology;
        var topology = new TopologyModel();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var points = new Dictionary<VertexId, Point3D>();
        var next = new[] { source.Vertices.Max(x => x.Id.Value), source.Edges.Max(x => x.Id.Value), source.Coedges.Max(x => x.Id.Value), source.Loops.Max(x => x.Id.Value), source.Faces.Max(x => x.Id.Value) }
            .Max() + 1;
        var nextCurve = part.Geometry.Curves.Select(c => c.Key.Value).DefaultIfEmpty(0).Max() + 1;
        var nextSurface = part.Geometry.Surfaces.Select(c => c.Key.Value).DefaultIfEmpty(0).Max() + 1;
        var faceIds = new List<FaceId>();
        var copiedVertices = new HashSet<VertexId>(); var copiedEdges = new HashSet<EdgeId>();

        void CopyEdge(EdgeId id)
        {
            if (!copiedEdges.Add(id)) return;
            var edge = source.GetEdge(id);
            foreach (var vertex in new[] { edge.StartVertexId, edge.EndVertexId })
                if (copiedVertices.Add(vertex)) { topology.AddVertex(new Vertex(vertex)); part.TryGetVertexPoint(vertex, out var p); points[vertex] = p; }
            topology.AddEdge(edge);
            var binding = part.Bindings.GetEdgeBinding(id);
            if (!geometry.TryGetCurve(binding.CurveGeometryId, out _)) geometry.AddCurve(binding.CurveGeometryId, part.Geometry.GetCurve(binding.CurveGeometryId));
            bindings.AddEdgeBinding(binding);
        }

        // The part's faces of this half, reversed: each loop runs backwards and the face opposes its surface.
        foreach (var face in source.Faces.Where(f => member(f.Id.Value)).OrderBy(f => f.Id.Value))
        {
            foreach (var loopId in face.LoopIds)
            {
                var loop = source.GetLoop(loopId);
                if (loop.Kind == LoopKind.Vertex)
                {
                    var vertex = loop.VertexLoopVertexId!.Value;
                    if (copiedVertices.Add(vertex)) { topology.AddVertex(new Vertex(vertex)); part.TryGetVertexPoint(vertex, out var p); points[vertex] = p; }
                    topology.AddLoop(loop);
                    if (part.Bindings.TryGetVertexLoopParameterBinding(loopId, out var vertexLoop)) bindings.AddVertexLoopParameterBinding(vertexLoop);
                    continue;
                }
                var coedges = loop.CoedgeIds.Select(source.GetCoedge).Reverse().ToArray();
                for (var i = 0; i < coedges.Length; i++)
                {
                    CopyEdge(coedges[i].EdgeId);
                    topology.AddCoedge(coedges[i] with
                    {
                        NextCoedgeId = coedges[(i + 1) % coedges.Length].Id,
                        PrevCoedgeId = coedges[(i - 1 + coedges.Length) % coedges.Length].Id,
                        IsReversed = !coedges[i].IsReversed,
                    });
                }
                topology.AddLoop(new Loop(loopId, coedges.Select(c => c.Id).ToArray()));
                if (part.Bindings.TryGetFaceBoundaryRoleBinding(loopId, out var role)) bindings.AddFaceBoundaryRoleBinding(role);
            }
            topology.AddFace(face);
            var faceBinding = part.Bindings.GetFaceBinding(face.Id);
            if (!geometry.TryGetSurface(faceBinding.SurfaceGeometryId, out _)) geometry.AddSurface(faceBinding.SurfaceGeometryId, part.Geometry.GetSurface(faceBinding.SurfaceGeometryId));
            bindings.AddFaceBinding(faceBinding with { Orientation = faceBinding.Orientation.Reversed() });
            faceIds.Add(face.Id);
        }

        // Along a parting edge this half's reversed face runs against the other half's original direction, so the
        // parting and shut-off faces take this half's original direction there.
        var faceOfLoop = source.Faces.SelectMany(f => f.LoopIds.Select(l => (l, f.Id.Value))).ToDictionary(x => x.l, x => x.Value);
        (EdgeId Edge, bool Reversed)[] Chain(IReadOnlyList<int> loopEdges)
        {
            var uses = loopEdges.Select(e => source.Coedges.Single(c => c.EdgeId.Value == e && member(faceOfLoop[c.LoopId]))).ToList();
            var ordered = new List<(EdgeId, bool)>();
            var current = uses[0]; uses.RemoveAt(0);
            ordered.Add((current.EdgeId, current.IsReversed));
            VertexId End((EdgeId Edge, bool Reversed) use) { var e = source.GetEdge(use.Edge); return use.Reversed ? e.StartVertexId : e.EndVertexId; }
            VertexId Start(Coedge c) { var e = source.GetEdge(c.EdgeId); return c.IsReversed ? e.EndVertexId : e.StartVertexId; }
            while (uses.Count > 0)
            {
                var end = End(ordered[^1]);
                var following = uses.FirstOrDefault(c => Start(c) == end);
                if (following is null) return [];
                uses.Remove(following);
                ordered.Add((following.EdgeId, following.IsReversed));
            }
            foreach (var use in ordered) CopyEdge(use.Item1);
            return ordered.ToArray();
        }

        // New edges and planar faces: lines between new or copied vertices, each edge shared by the two faces that
        // meet on it.
        var lines = new Dictionary<(int, int), EdgeId>();
        VertexId NewVertex(Point3D p) { var id = new VertexId(next++); topology.AddVertex(new Vertex(id)); points[id] = p; return id; }
        (EdgeId, bool) Line(VertexId from, VertexId to)
        {
            var key = (int.Min(from.Value, to.Value), int.Max(from.Value, to.Value));
            if (!lines.TryGetValue(key, out var id))
            {
                id = new EdgeId(next++);
                topology.AddEdge(new Edge(id, from, to));
                var curve = new CurveGeometryId(nextCurve++);
                geometry.AddCurve(curve, CurveGeometry.FromLine(new Line3Curve(points[from], Direction3D.Create(points[to] - points[from]))));
                bindings.AddEdgeBinding(new EdgeGeometryBinding(id, curve, new ParameterInterval(0d, (points[to] - points[from]).Length)));
                lines[key] = id;
            }
            return (id, topology.GetEdge(id).StartVertexId != from);
        }
        LoopId AddLoop(IReadOnlyList<(EdgeId Edge, bool Reversed)> uses)
        {
            var loop = new LoopId(next++);
            var ids = uses.Select(_ => new CoedgeId(next++)).ToArray();
            for (var i = 0; i < uses.Count; i++)
                topology.AddCoedge(new Coedge(ids[i], uses[i].Edge, loop, ids[(i + 1) % ids.Length], ids[(i - 1 + ids.Length) % ids.Length], uses[i].Reversed));
            topology.AddLoop(new Loop(loop, ids));
            return loop;
        }
        void AddFace(Vector3D normal, Point3D origin, Vector3D xAxis, IReadOnlyList<(IReadOnlyList<(EdgeId, bool)> Uses, bool Outer)> faceLoops)
        {
            var face = new FaceId(next++);
            var loopIds = faceLoops.Select(l => AddLoop(l.Uses)).ToArray();
            topology.AddFace(new Face(face, loopIds));
            for (var i = 0; i < loopIds.Length; i++) bindings.AddFaceBoundaryRoleBinding(new(face, loopIds[i], faceLoops[i].Outer ? FaceBoundaryRole.Outer : FaceBoundaryRole.Inner));
            var surface = new SurfaceGeometryId(nextSurface++);
            geometry.AddSurface(surface, SurfaceGeometry.FromPlane(new PlaneSurface(origin, Direction3D.Create(normal), Direction3D.Create(xAxis))));
            bindings.AddFaceBinding(new FaceGeometryBinding(face, surface));
            faceIds.Add(face);
        }

        Vector3D Across(Vector3D normal)
        {
            var helper = double.Abs(normal.X) < 0.9 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0);
            var axis = helper - normal * helper.Dot(normal); return axis / axis.Length;
        }
        // A cap across each mouth, in this body's own direction along the mouth's edges.
        string? Caps(int outerTurn)
        {
            foreach (var mouth in mouths)
            {
                var chain = Chain(mouth.Edges);
                if (chain.Length == 0) return $"a slide's mouth (edges {string.Join(", ", mouth.Edges)}) does not close into one loop";
                var ring = Ring(part, chain);
                if (double.Sign(Turn(ring, mouth.Normal)) != outerTurn) return $"a slide's mouth (edges {string.Join(", ", mouth.Edges)}) does not face its cap";
                AddFace(mouth.Normal, ring[0], Across(mouth.Normal), [(chain, true)]);
            }
            return null;
        }
        if (outer is null)
        {
            if ((reason = Caps(convention)) is not null) return null;
            return Finish();
        }

        // The convention for an outer loop's turn about its face's outward normal is read off the part's own parting
        // line: inside the parting face, it is an inner loop.
        var outerChain = Chain(outer.EdgeIds);
        if (outerChain.Length == 0) { reason = "the parting line does not close into one loop"; return null; }
        var partingNormal = -away;
        var x = Across(away);
        var innerTurn = double.Sign(Turn(Ring(part, outerChain), partingNormal));
        var outerTurn = -innerTurn;
        convention = outerTurn;

        // The block's corners: at the parting plane and at the far end of this half.
        var (u0, u1, v0, v1) = box;
        var near = new[] { at(u0, v0, partingHeight), at(u1, v0, partingHeight), at(u1, v1, partingHeight), at(u0, v1, partingHeight) }.Select(NewVertex).ToArray();
        var far = new[] { at(u0, v0, farHeight), at(u1, v0, farHeight), at(u1, v1, farHeight), at(u0, v1, farHeight) }.Select(NewVertex).ToArray();
        IReadOnlyList<(EdgeId, bool)> Box(IReadOnlyList<VertexId> vertices, Vector3D normal)
        {
            var ordered = double.Sign(Turn(vertices.Select(v => points[v]).ToArray(), normal)) == outerTurn ? vertices : vertices.Reverse().ToArray();
            return ordered.Select((vertex, i) => Line(vertex, ordered[(i + 1) % ordered.Count])).ToArray();
        }

        // Parting face: the block's section less the part's outline.
        AddFace(partingNormal, points[near[0]], x, [(Box(near, partingNormal), true), (outerChain, false)]);
        // Shut-offs: each opening's loop, closed by a face across it.
        foreach (var loop in shutOffs)
        {
            var chain = Chain(loop.EdgeIds);
            if (chain.Length == 0) { reason = $"a shut-off loop (edges {string.Join(", ", loop.EdgeIds)}) does not close"; return null; }
            var ring = Ring(part, chain);
            if (double.Sign(Turn(ring, partingNormal)) != outerTurn) { reason = $"the shut-off at {loop.MinimumHeight:0.###} mm does not face this half"; return null; }
            AddFace(partingNormal, ring[0], x, [(chain, true)]);
        }
        // The block: its far face and four sides.
        AddFace(away, points[far[0]], x, [(Box(far, away), true)]);
        for (var i = 0; i < 4; i++)
        {
            var side = new[] { near[i], near[(i + 1) % 4], far[(i + 1) % 4], far[i] };
            var a = points[near[i]]; var b = points[near[(i + 1) % 4]];
            var normal = (b - a).Cross(away); normal /= normal.Length;
            var centre = Point3D.Origin + ((points[near[0]] - Point3D.Origin) + (points[near[2]] - Point3D.Origin)) * 0.5;
            if (normal.Dot(a - centre) < 0) normal = -normal;
            AddFace(normal, a, (b - a) / (b - a).Length, [(Box(side, normal), true)]);
        }
        if ((reason = Caps(outerTurn)) is not null) return null;
        return Finish();

        BrepBody Finish()
        {
            var shell = new ShellId(next++);
            topology.AddShell(new Shell(shell, faceIds.ToArray()));
            topology.AddBody(new Body(new BodyId(next++), [shell]));
            return new BrepBody(topology, geometry, bindings, points);
        }
    }

    /// <summary>Points along a chain of edge uses, in traversal order, a few per curved edge.</summary>
    private static Point3D[] Ring(BrepBody part, IReadOnlyList<(EdgeId Edge, bool Reversed)> chain)
        => chain.SelectMany(use =>
        {
            var points = Samples(part, use.Edge);
            return (use.Reversed ? points.Reverse() : points).Take(points.Count - 1);
        }).ToArray();

    /// <summary>Points along an edge from its start vertex to its end vertex.</summary>
    internal static IReadOnlyList<Point3D> Samples(BrepBody part, EdgeId id)
    {
        var edge = part.Topology.GetEdge(id);
        part.TryGetVertexPoint(edge.StartVertexId, out var start); part.TryGetVertexPoint(edge.EndVertexId, out var end);
        if (!part.TryGetEdgeCurveGeometry(id, out var curve) || curve is null || !part.Bindings.TryGetEdgeBinding(id, out var binding) || binding.TrimInterval is not { } trim
            || curve.Kind == CurveGeometryKind.Line3)
            return [start, end];
        var points = new List<Point3D> { start };
        for (var k = 1; k < 8; k++)
        {
            var t = trim.Start + (trim.End - trim.Start) * k / 8d;
            Point3D? p = curve.Kind switch
            {
                CurveGeometryKind.Circle3 => curve.Circle3!.Value.Evaluate(t),
                CurveGeometryKind.Ellipse3 => curve.Ellipse3!.Value.Evaluate(t),
                CurveGeometryKind.BSpline3 => curve.BSpline3!.Value.Evaluate(t),
                _ => null,
            };
            if (p is { } q) points.Add(q);
        }
        // The curve's parameter may run against the edge.
        if (points.Count > 2 && (points[1] - start).Length > (points[^1] - start).Length) points = [start, .. points.Skip(1).Reverse()];
        points.Add(end);
        return points;
    }

    /// <summary>Twice the signed area of a closed polygon about <paramref name="normal"/>.</summary>
    private static double Turn(IReadOnlyList<Point3D> ring, Vector3D normal)
    {
        var sum = Vector3D.Zero;
        for (var i = 0; i < ring.Count; i++) sum += (ring[i] - Point3D.Origin).Cross(ring[(i + 1) % ring.Count] - Point3D.Origin);
        return sum.Dot(normal);
    }

    private static IReadOnlyList<(double X, double Y)> Polygon(BrepBody part, IReadOnlyList<int> edges, Func<Point3D, double> u, Func<Point3D, double> v)
        => edges.SelectMany(e => Samples(part, new EdgeId(e))).Select(p => (u(p), v(p))).ToArray();

    private static double Area(IReadOnlyList<(double X, double Y)> polygon)
    {
        // The loop's vertices in edge order (each edge contributes both ends); the hull area orders loops by size.
        var xs = polygon.Select(p => p.X).ToArray(); var ys = polygon.Select(p => p.Y).ToArray();
        return (xs.Max() - xs.Min()) * (ys.Max() - ys.Min());
    }

    private static bool Inside(IReadOnlyList<(double X, double Y)> polygon, (double X, double Y) p)
    {
        var xs = polygon.Select(q => q.X).ToArray(); var ys = polygon.Select(q => q.Y).ToArray();
        return p.X > xs.Min() && p.X < xs.Max() && p.Y > ys.Min() && p.Y < ys.Max();
    }
}
