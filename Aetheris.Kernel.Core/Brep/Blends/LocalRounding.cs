using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Blends;

/// <summary>
/// Every convex edge rounded by a rolling ball of one radius, built locally on the body's own face graph. For a
/// radius small against the body the topology is kept: each face stays on its surface and is trimmed back to where
/// the ball touches it, each edge becomes the tube around the ball centre's path (a cylinder around a line, a torus
/// around a circle) and each three-edge corner a piece of the ball. The ball centre is where the faces moved in by
/// the radius meet, so nothing here is approximated.
///
/// Admits faces on planes, cylinders and cones; line edges between faces that share the line's direction (planes
/// along it, cylinders around it); circle edges between faces that share the circle's axis; corners where three
/// edges meet; seams of periodic faces. Refuses the rest by name, and a radius too large for the body (an edge or a
/// face used up, the ball reaching a face beyond its two) as such.
/// </summary>
internal static class LocalRounding
{
    internal sealed record Refusal(string Code, string Message);

    internal sealed class Outcome
    {
        public BrepBody? Body { get; init; }
        public Refusal? Refusal { get; init; }
    }

    private const double Turn = 2 * double.Pi;

    /// <summary>One face's surface and its outward side, as a distance function.</summary>
    private sealed class Support(FaceId face, SurfaceGeometry surface, double sense)
    {
        public FaceId Face { get; } = face;
        public double Sense { get; } = sense;
        public SurfaceGeometry Surface { get; } = surface;
        public SurfaceGeometryKind Kind => Surface.Kind;

        public Point3D Foot(Point3D x)
        {
            switch (Kind)
            {
                case SurfaceGeometryKind.Plane:
                {
                    var p = Surface.Plane!.Value; var n = p.Normal.ToVector();
                    return x - n * n.Dot(x - p.Origin);
                }
                case SurfaceGeometryKind.Cylinder:
                {
                    var c = Surface.Cylinder!.Value; var a = c.Axis.ToVector(); var q = x - c.Origin; var h = q.Dot(a);
                    return c.Origin + a * h + Radial(q, a, c.XAxis.ToVector()) * c.Radius;
                }
                default:
                {
                    var k = Surface.Cone!.Value; var a = k.Axis.ToVector(); var q = x - k.Apex; var h = q.Dot(a);
                    var (s, co) = (double.Sin(k.SemiAngleRadians), double.Cos(k.SemiAngleRadians));
                    var along = double.Max(0, (q - a * h).Length * s + h * co);
                    return k.Apex + (Radial(q, a, k.XAxis()) * s + a * co) * along;
                }
            }
        }

        /// <summary>Unit outward normal of the body at the foot of the point.</summary>
        public Vector3D Outward(Point3D x)
        {
            Vector3D n;
            switch (Kind)
            {
                case SurfaceGeometryKind.Plane: n = Surface.Plane!.Value.Normal.ToVector(); break;
                case SurfaceGeometryKind.Cylinder:
                {
                    var c = Surface.Cylinder!.Value;
                    n = Radial(x - c.Origin, c.Axis.ToVector(), c.XAxis.ToVector()); break;
                }
                default:
                {
                    var k = Surface.Cone!.Value; var a = k.Axis.ToVector();
                    n = Radial(x - k.Apex, a, k.XAxis()) - a * double.Tan(k.SemiAngleRadians);
                    n *= 1 / n.Length; break;
                }
            }
            return n * sense;
        }

        /// <summary>Signed distance to the surface, positive on the material side.</summary>
        public double Inside(Point3D x) { var f = Foot(x); return -Outward(x).Dot(x - f); }

        private static Vector3D Radial(Vector3D q, Vector3D axis, Vector3D fallback)
        {
            var r = q - axis * q.Dot(axis);
            return r.Length > 1e-14 ? r * (1 / r.Length) : fallback;
        }
    }

    /// <summary>An edge's shape from its start to its end: a segment, or an arc about a normal (right-handed).</summary>
    private sealed record Shape(Point3D Start, Point3D End, Point3D? Center, Vector3D Normal, double Sweep)
    {
        public bool Arc => Center is not null;

        public Point3D At(double t) => !Arc ? Start + (End - Start) * t : Rotate(Start, Center!.Value, Normal, Sweep * t);

        public Vector3D Tangent(Point3D p) => !Arc ? Unit(End - Start) : Unit(Normal.Cross(p - Center!.Value));

        public IEnumerable<Point3D> Points(int arcSegments) =>
            Arc ? Enumerable.Range(0, arcSegments + 1).Select(i => At(i / (double)arcSegments)) : [Start, End];
    }

    /// <summary>A sharp edge to round: its two faces (A uses it forward), its shape, and what it is built from.</summary>
    private sealed class Sharp
    {
        public required Edge Edge { get; init; }
        public required Shape Shape { get; init; }
        public required Support F0 { get; init; }
        public required Support F1 { get; init; }
        // Once the faces' loops are oriented: A walks the edge forward, B backward.
        public required Support A { get; init; }
        public required Support B { get; init; }
        public bool Closed => Edge.StartVertexId == Edge.EndVertexId;
        // Closed circles: the ball centre, and where it touches A and B, at the seam's meridian.
        public Point3D Centre { get; set; }
    }

    internal static Outcome Round(BrepBody body, double radius)
    {
        var topology = body.Topology;
        var points = VertexPoints(body);
        if (points.Count != topology.Vertices.Count())
            return Refuse("uniform-rounding-edge-unsupported", "some vertices of the body have no position");
        var extent = points.Values.Max(p => (p - Point3D.Origin).Length);
        var tol = double.Max(1e-9, (extent + 1) * 1e-10) * 1e2;

        // Faces: supports with their outward side.
        var supports = new Dictionary<FaceId, Support>();
        foreach (var face in topology.Faces.OrderBy(f => f.Id.Value))
        {
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null || surface.Kind is not (SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Cone))
                return Refuse("uniform-rounding-curved-face-unsupported", $"rounding every edge supports faces on planes, cylinders and cones for now; face {face.Id.Value} is {surface?.Kind.ToString() ?? "unbound"}");
            body.Bindings.TryGetFaceBinding(face.Id, out var binding);
            supports.Add(face.Id, new Support(face.Id, surface, binding.Orientation.IsAlignedWithSurface ? 1 : -1));
        }
        var faceOfLoop = topology.Faces.SelectMany(f => f.LoopIds.Select(l => (l, f.Id))).ToDictionary(x => x.l, x => x.Id);
        var uses = topology.Coedges.GroupBy(c => c.EdgeId).ToDictionary(g => g.Key, g => g.ToArray());

        // Edges: shapes, seams, and the sharp convex edges to round.
        var shapes = new Dictionary<EdgeId, Shape>();
        var sharp = new Dictionary<EdgeId, Sharp>();
        var seams = new Dictionary<EdgeId, FaceId>();
        var pairs = new Dictionary<EdgeId, (FaceId F0, FaceId F1)>();
        foreach (var edge in topology.Edges.OrderBy(e => e.Id.Value))
        {
            if (!uses.TryGetValue(edge.Id, out var pair) || pair.Length != 2)
                return Refuse("uniform-rounding-edge-unsupported", $"edge {edge.Id.Value} is not shared by exactly two faces");
            var shape = ShapeOf(body, edge, points);
            if (shape is null)
                return Refuse("uniform-rounding-edge-unsupported", $"edge {edge.Id.Value} has no line or circle geometry this rounding can follow");
            shapes.Add(edge.Id, shape);
            var (f0, f1) = (faceOfLoop[pair[0].LoopId], faceOfLoop[pair[1].LoopId]);
            if (f0 == f1)
            {
                if (shape.Arc || supports[f0].Kind == SurfaceGeometryKind.Plane)
                    return Refuse("uniform-rounding-edge-unsupported", $"edge {edge.Id.Value} has face {f0.Value} on both sides and is not the seam of a cylinder or cone");
                seams.Add(edge.Id, f0); continue;
            }
            pairs.Add(edge.Id, (f0, f1));
        }

        // Orientation: each face's outer loop walked counterclockwise about its outward normal, inner loops
        // clockwise, by exact signed area. The input's coedge senses are not relied on.
        var oriented = new Dictionary<FaceId, List<List<(EdgeId Edge, bool Reversed)>>>();
        foreach (var face in topology.Faces.OrderBy(f => f.Id.Value))
        {
            var loops = face.LoopIds.Select(l => topology.GetLoop(l).CoedgeIds.Select(topology.GetCoedge).Select(c => (c.EdgeId, c.IsReversed)).ToList()).ToList();
            var areas = loops.Select(l => SignedArea(supports[face.Id], l, shapes)).ToArray();
            var outer = Array.IndexOf(areas, areas.MaxBy(double.Abs));
            for (var i = 0; i < loops.Count; i++)
                if (areas[i] > 0 != (i == outer)) { loops[i].Reverse(); loops[i] = loops[i].Select(u => (u.EdgeId, !u.IsReversed)).ToList(); }
            oriented.Add(face.Id, loops);
        }

        // Sharp convex edges: A walks the edge forward, B backward; B's interior is left of B's direction about its
        // outward normal, and across a convex edge it lies behind A's tangent plane.
        foreach (var (edgeId, (f0, f1)) in pairs)
        {
            var edge = topology.GetEdge(edgeId); var shape = shapes[edgeId];
            var on0 = oriented[f0].SelectMany(l => l).Where(u => u.Edge == edgeId).ToArray();
            var on1 = oriented[f1].SelectMany(l => l).Where(u => u.Edge == edgeId).ToArray();
            if (on0.Length != 1 || on1.Length != 1 || on0[0].Reversed == on1[0].Reversed)
                return Refuse("uniform-rounding-edge-unsupported", $"faces {f0.Value} and {f1.Value} do not walk edge {edgeId.Value} in opposite directions once oriented");
            var (a, b) = on0[0].Reversed ? (supports[f1], supports[f0]) : (supports[f0], supports[f1]);
            var p = shape.At(0.5);
            var (na, nb) = (a.Outward(p), b.Outward(p));
            if (na.Cross(nb).Length < 1e-7 && na.Dot(nb) > 0)
                return Refuse("uniform-rounding-tangent-edge-unsupported", $"rounding every edge does not support edges where faces meet tangentially yet: edge {edgeId.Value} between faces {a.Face.Value} and {b.Face.Value} is smooth");
            var intoB = nb.Cross(shape.Tangent(p) * -1);
            if (na.Dot(intoB) > -1e-9)
                return Refuse("uniform-rounding-concave-unsupported", $"rounding every edge supports convex edges only for now: edge {edgeId.Value} between faces {a.Face.Value} and {b.Face.Value} is a concave edge (closing concave edges with the ball is not built yet)");
            if (!Shares(a, shape, tol) || !Shares(b, shape, tol))
                return Refuse("uniform-rounding-edge-unsupported", $"rounding every edge needs each edge's faces to run along its line or around its circle's axis: edge {edgeId.Value} {(shape.Arc ? "is a circle" : "is a line")} between a {Article(a.Kind)} and a {Article(b.Kind)} face that do not, so the ball's path along it is not a line or a circle");
            sharp.Add(edgeId, new Sharp { Edge = edge, Shape = shape, F0 = a, F1 = b, A = a, B = b });
        }

        // Corners: every vertex on an open sharp edge joins exactly three sharp edges and three faces.
        var corners = new Dictionary<VertexId, (Point3D Centre, EdgeId[] Edges, FaceId[] Faces)>();
        foreach (var vertex in topology.Vertices.OrderBy(v => v.Id.Value))
        {
            var incident = topology.Edges.Where(e => e.StartVertexId == vertex.Id || e.EndVertexId == vertex.Id).ToArray();
            var open = incident.Where(e => e.StartVertexId != e.EndVertexId).ToArray();
            if (open.Length == 0) continue;
            if (open.Any(e => seams.ContainsKey(e.Id)) && open.All(e => seams.ContainsKey(e.Id))) continue; // seam ends
            if (open.Length != 3 || open.Any(e => !sharp.ContainsKey(e.Id)))
                return Refuse("uniform-rounding-vertex-unsupported", $"vertex {vertex.Id.Value} joins {open.Length} edges; rounding every edge on a body with curved faces needs three sharp edges at each corner for now");
            var faces = open.SelectMany(e => new[] { sharp[e.Id].F0.Face, sharp[e.Id].F1.Face }).Distinct().ToArray();
            if (faces.Length != 3)
                return Refuse("uniform-rounding-vertex-unsupported", $"vertex {vertex.Id.Value} joins {faces.Length} faces with three edges; rounding needs three faces at each corner");
            var centre = Centre(faces.Select(f => supports[f]).ToArray(), points[vertex.Id], radius, tol);
            if (centre is null || (centre.Value - points[vertex.Id]).Length > 50 * radius)
                return TooLarge(radius, $"the ball does not fit in the corner at vertex {vertex.Id.Value}");
            corners.Add(vertex.Id, (centre.Value, open.Select(e => e.Id).ToArray(), faces));
        }

        // Closed circles: the ball centre at the seam's meridian (or the circle's own start).
        foreach (var s in sharp.Values.Where(s => s.Closed))
        {
            var seam = seams.Where(x => x.Value == s.F0.Face || x.Value == s.F1.Face).Select(x => shapes[x.Key].Start).ToArray();
            var c = s.Shape.Center!.Value; var nu = s.Shape.Normal;
            var x0 = Unit(s.Shape.Start - c);
            if (seam.Length > 0)
            {
                var meridian = Unit((seam[0] - c) - nu * (seam[0] - c).Dot(nu));
                if (seam.Skip(1).Any(q => Unit((q - c) - nu * (q - c).Dot(nu)).Dot(meridian) < 1 - 1e-9))
                    return Refuse("uniform-rounding-edge-unsupported", $"the faces around circle edge {s.Edge.Id.Value} have seams at different angles");
                x0 = meridian;
            }
            var centre = Meridian(s.F0, s.F1, c, nu, x0, s.Shape.Start, radius, tol, (s.Shape.Start - c).Length);
            if (centre is null) return TooLarge(radius, $"the ball does not fit along circle edge {s.Edge.Id.Value}");
            s.Centre = centre.Value;
        }

        return Build(body, radius, tol, supports, oriented, shapes, sharp, seams, corners);
    }

    private static Outcome Build(BrepBody body, double radius, double tol, Dictionary<FaceId, Support> supports, Dictionary<FaceId, List<List<(EdgeId Edge, bool Reversed)>>> oriented,
        Dictionary<EdgeId, Shape> shapes, Dictionary<EdgeId, Sharp> sharp, Dictionary<EdgeId, FaceId> seams,
        Dictionary<VertexId, (Point3D Centre, EdgeId[] Edges, FaceId[] Faces)> corners)
    {
        var topology = body.Topology;
        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var positions = new Dictionary<VertexId, Point3D>();
        var built = new Dictionary<EdgeId, Shape>();
        var resultFaces = new List<FaceId>();

        VertexId NewVertex(Point3D p) { var id = builder.AddVertex(); positions[id] = p; return id; }
        EdgeId NewEdge(VertexId a, VertexId b, Shape shape)
        {
            var edge = builder.AddEdge(a, b); var id = new CurveGeometryId(geometry.Curves.Count() + 1);
            if (shape.Arc)
            {
                var c = shape.Center!.Value;
                geometry.AddCurve(id, CurveGeometry.FromCircle(new Circle3Curve(c, Direction3D.Create(shape.Normal), (shape.Start - c).Length, Direction3D.Create(shape.Start - c))));
                bindings.AddEdgeBinding(new(edge, id, new(0, shape.Sweep)));
            }
            else
            {
                geometry.AddCurve(id, CurveGeometry.FromLine(new Line3Curve(shape.Start, Direction3D.Create(shape.End - shape.Start))));
                bindings.AddEdgeBinding(new(edge, id, new(0, (shape.End - shape.Start).Length)));
            }
            built.Add(edge, shape); return edge;
        }
        void NewFace(IReadOnlyList<IReadOnlyList<(EdgeId Edge, bool Reversed)>> loops, SurfaceGeometry surface, bool aligned)
        {
            var loopIds = new List<LoopId>();
            foreach (var loop in loops)
            {
                var id = builder.AllocateLoopId(); var ids = loop.Select(_ => builder.AllocateCoedgeId()).ToArray();
                for (var i = 0; i < ids.Length; i++)
                    builder.AddCoedge(new(ids[i], loop[i].Edge, id, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], loop[i].Reversed));
                builder.AddLoop(new Loop(id, ids)); loopIds.Add(id);
            }
            var face = builder.AddFace(loopIds); resultFaces.Add(face);
            var sid = new SurfaceGeometryId(geometry.Surfaces.Count() + 1); geometry.AddSurface(sid, surface);
            bindings.AddFaceBinding(new FaceGeometryBinding(face, sid, aligned));
        }

        // Contact vertices: where the ball at a corner touches each of its faces; for closed circles, where the ball
        // at the meridian touches each side.
        var contact = new Dictionary<(VertexId Vertex, FaceId Face), VertexId>();
        foreach (var (vertex, corner) in corners)
            foreach (var face in corner.Faces)
                contact.Add((vertex, face), NewVertex(supports[face].Foot(corner.Centre)));
        var ring = new Dictionary<(EdgeId Edge, FaceId Face), VertexId>();
        foreach (var s in sharp.Values.Where(s => s.Closed))
        {
            ring.Add((s.Edge.Id, s.A.Face), NewVertex(s.A.Foot(s.Centre)));
            ring.Add((s.Edge.Id, s.B.Face), NewVertex(s.B.Foot(s.Centre)));
        }

        // Contact edges along each sharp edge, on each of its faces; the ball's cross-section arcs at its ends.
        var along = new Dictionary<(EdgeId Edge, FaceId Face), EdgeId>();
        var section = new Dictionary<(EdgeId Edge, VertexId Vertex), EdgeId>();
        var strips = new List<(Sharp Sharp, SurfaceGeometry Surface)>();
        foreach (var s in sharp.Values.OrderBy(s => s.Edge.Id.Value))
        {
            var shape = s.Shape;
            if (s.Closed)
            {
                var c = shape.Center!.Value; var nu = shape.Normal;
                foreach (var face in new[] { s.A, s.B })
                {
                    var v = ring[(s.Edge.Id, face.Face)]; var p = positions[v]; var centre = c + nu * (p - c).Dot(nu);
                    if ((p - centre).Length <= tol) return TooLarge(radius, $"the contact with face {face.Face.Value} along circle edge {s.Edge.Id.Value} shrinks to a point");
                    along.Add((s.Edge.Id, face.Face), NewEdge(v, v, new Shape(p, p, centre, nu, Turn)));
                }
                var (a, b) = (ring[(s.Edge.Id, s.A.Face)], ring[(s.Edge.Id, s.B.Face)]);
                var tangent = Unit(nu.Cross(s.Centre - c));
                section.Add((s.Edge.Id, s.Edge.StartVertexId), NewEdge(a, b, Cross(positions[a], positions[b], s.Centre, tangent)));
                var axisCentre = c + nu * (s.Centre - c).Dot(nu); var major = (s.Centre - axisCentre).Length;
                if (major <= radius + tol) return TooLarge(radius, $"the ball along circle edge {s.Edge.Id.Value} reaches the circle's axis");
                strips.Add((s, SurfaceGeometry.FromTorus(new TorusSurface(axisCentre, Direction3D.Create(nu), major, radius, Direction3D.Create(s.Centre - axisCentre)))));
                continue;
            }

            var (start, end) = (s.Edge.StartVertexId, s.Edge.EndVertexId);
            var (c0, c1) = (corners[start].Centre, corners[end].Centre);
            foreach (var face in new[] { s.A, s.B })
            {
                var (v0, v1) = (contact[(start, face.Face)], contact[(end, face.Face)]);
                var (p0, p1) = (positions[v0], positions[v1]);
                Shape? path = null;
                if (!shape.Arc)
                {
                    if ((p1 - p0).Dot(shape.End - shape.Start) <= tol * (shape.End - shape.Start).Length)
                        return TooLarge(radius, $"the rounding uses up edge {s.Edge.Id.Value} on face {face.Face.Value}");
                    path = new Shape(p0, p1, null, Vector3D.Zero, 0);
                }
                else
                {
                    var c = shape.Center!.Value; var nu = shape.Normal; var centre = c + nu * (p0 - c).Dot(nu);
                    var sweep = Angle(p0 - centre, p1 - centre, nu);
                    if ((p0 - centre).Length <= tol || double.Abs(sweep - shape.Sweep) >= double.Pi / 2 || sweep <= 1e-9)
                        return TooLarge(radius, $"the rounding uses up edge {s.Edge.Id.Value} on face {face.Face.Value}");
                    path = new Shape(p0, p1, centre, nu, sweep);
                }
                along.Add((s.Edge.Id, face.Face), NewEdge(v0, v1, path));
            }
            foreach (var (vertex, centre) in new[] { (start, c0), (end, c1) })
            {
                var (a, b) = (contact[(vertex, s.A.Face)], contact[(vertex, s.B.Face)]);
                section.Add((s.Edge.Id, vertex), NewEdge(a, b, Cross(positions[a], positions[b], centre, shape.Tangent(shape.Arc ? Project(centre, shape) : centre))));
            }
            if (!shape.Arc)
            {
                if ((c1 - c0).Dot(shape.End - shape.Start) <= tol || Unit(c1 - c0).Cross(Unit(shape.End - shape.Start)).Length > 1e-7)
                    return TooLarge(radius, $"the ball's path along edge {s.Edge.Id.Value} folds back");
                strips.Add((s, SurfaceGeometry.FromCylinder(new CylinderSurface(c0, Direction3D.Create(c1 - c0), radius, Direction3D.Create(positions[contact[(start, s.A.Face)]] - c0)))));
            }
            else
            {
                var c = shape.Center!.Value; var nu = shape.Normal; var axisCentre = c + nu * (c0 - c).Dot(nu); var major = (c0 - axisCentre).Length;
                if (major <= radius + tol) return TooLarge(radius, $"the ball along circle edge {s.Edge.Id.Value} reaches the circle's axis");
                strips.Add((s, SurfaceGeometry.FromTorus(new TorusSurface(axisCentre, Direction3D.Create(nu), major, radius, Direction3D.Create(c0 - axisCentre)))));
            }
        }

        // Seams: the generator between the contacts at either end of the periodic face.
        var seam = new Dictionary<EdgeId, EdgeId>();
        foreach (var (edge, face) in seams)
        {
            var shape = shapes[edge];
            VertexId? Nearest(Point3D p) => sharp.Values.Where(s => s.Closed && (s.A.Face == face || s.B.Face == face))
                .OrderBy(s => double.Abs((p - s.Shape.Center!.Value).Dot(s.Shape.Normal))).Select(s => (VertexId?)ring[(s.Edge.Id, face)]).FirstOrDefault();
            if (Nearest(shape.Start) is not { } v0 || Nearest(shape.End) is not { } v1 || v0 == v1)
                return Refuse("uniform-rounding-edge-unsupported", $"seam edge {edge.Value} on face {face.Value} does not run between two circle edges");
            if ((positions[v1] - positions[v0]).Dot(shape.End - shape.Start) <= tol)
                return TooLarge(radius, $"the rounding uses up face {face.Value} along its seam");
            seam.Add(edge, NewEdge(v0, v1, new Shape(positions[v0], positions[v1], null, Vector3D.Zero, 0)));
        }

        // Original faces, trimmed: each coedge replaced by its contact edge (or rebuilt seam), same direction.
        var originalLoops = new Dictionary<FaceId, List<List<(EdgeId Edge, bool Reversed)>>>();
        foreach (var face in topology.Faces.OrderBy(f => f.Id.Value))
        {
            var loops = oriented[face.Id].Select(loop => loop.Select(u => (seam.TryGetValue(u.Edge, out var rebuilt) ? rebuilt : along[(u.Edge, face.Id)], u.Reversed)).ToList()).ToList();
            originalLoops.Add(face.Id, loops);
            body.Bindings.TryGetFaceBinding(face.Id, out var binding);
            NewFace(loops.Select(l => (IReadOnlyList<(EdgeId, bool)>)l).ToList(), supports[face.Id].Surface, binding.Orientation.IsAlignedWithSurface);
        }

        // Strips: the contact on A against A's direction, the section at the start, the contact on B, the section
        // at the end back. A uses the edge forward, so every shared edge is used once each way.
        foreach (var (s, surface) in strips)
        {
            var a = along[(s.Edge.Id, s.A.Face)]; var b = along[(s.Edge.Id, s.B.Face)];
            var first = section[(s.Edge.Id, s.Edge.StartVertexId)];
            var last = s.Closed ? first : section[(s.Edge.Id, s.Edge.EndVertexId)];
            NewFace([[(a, true), (first, false), (b, false), (last, true)]], surface, true);
        }

        // Corners: the three sections, each against the direction its strip uses it.
        foreach (var (vertex, corner) in corners.OrderBy(c => c.Key.Value))
        {
            var arcs = corner.Edges.Select(e => (Edge: section[(e, vertex)], Reversed: sharp[e].Edge.StartVertexId == vertex)).ToList();
            var ordered = new List<(EdgeId, bool)> { arcs[0] }; arcs.RemoveAt(0);
            while (arcs.Count > 0)
            {
                var (last, reversed) = ordered[^1]; var e = builder.Model.GetEdge(last); var end = reversed ? e.StartVertexId : e.EndVertexId;
                var next = arcs.FindIndex(x => (x.Reversed ? builder.Model.GetEdge(x.Edge).EndVertexId : builder.Model.GetEdge(x.Edge).StartVertexId) == end);
                if (next < 0) return Refuse("uniform-rounding-vertex-unsupported", $"the corner at vertex {vertex.Value} does not close");
                ordered.Add(arcs[next]); arcs.RemoveAt(next);
            }
            var axis = Unit(corner.Faces.Aggregate(Vector3D.Zero, (n, f) => n + supports[f].Outward(corner.Centre)));
            NewFace([ordered], SurfaceGeometry.FromSphere(new SphereSurface(corner.Centre, Direction3D.Create(axis), radius, Direction3D.Create(Perpendicular(axis)))), true);
        }

        // Fit: the ball touches nothing beyond its own faces, and planar faces keep their loops simple.
        var refusal = Clearance(body, radius, tol, supports, sharp, corners) ?? Planar(radius, tol, oriented, supports, shapes, built, originalLoops);
        if (refusal is not null) return new Outcome { Refusal = refusal };

        builder.AddBody([builder.AddShell(resultFaces)]);
        var result = new BrepBody(builder.Model, geometry, bindings, positions);
        var validation = BrepBindingValidator.Validate(result, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess ? new Outcome { Body = result }
            : Refuse("uniform-rounding-build-failed", string.Join("; ", validation.Diagnostics.Select(d => d.Message)));
    }

    /// <summary>The ball centre at inward distance r from three faces, by Newton on their distance functions
    /// (whose gradients are the inward normals); exact in one step on planes.</summary>
    private static Point3D? Centre(Support[] faces, Point3D vertex, double radius, double tol)
    {
        var x = vertex - Unit(faces.Aggregate(Vector3D.Zero, (n, f) => n + f.Outward(vertex))) * radius;
        for (var i = 0; i < 60; i++)
        {
            var rows = faces.Select(f => f.Outward(x) * -1).ToArray();
            var residual = faces.Select(f => f.Inside(x) - radius).ToArray();
            if (residual.All(r => double.Abs(r) <= tol * 1e-3)) return x;
            var det = rows[0].Dot(rows[1].Cross(rows[2]));
            if (double.Abs(det) < 1e-12) return null;
            var step = (rows[1].Cross(rows[2]) * -residual[0] + rows[2].Cross(rows[0]) * -residual[1] + rows[0].Cross(rows[1]) * -residual[2]) * (1 / det);
            x += step;
        }
        return faces.All(f => double.Abs(f.Inside(x) - radius) <= tol) ? x : null;
    }

    /// <summary>The ball centre at inward distance r from two coaxial faces, in the half-plane at the meridian.</summary>
    private static Point3D? Meridian(Support a, Support b, Point3D centre, Vector3D axis, Vector3D meridian, Point3D onEdge, double radius, double tol, double rho0)
    {
        var start = centre + meridian * rho0;
        var (rho, z) = (rho0, 0d);
        var guess = start - Unit(a.Outward(start) + b.Outward(start)) * radius;
        (rho, z) = ((guess - centre).Dot(meridian), (guess - centre).Dot(axis));
        for (var i = 0; i < 60; i++)
        {
            var x = centre + meridian * rho + axis * z;
            var (ra, rb) = (a.Inside(x) - radius, b.Inside(x) - radius);
            if (double.Abs(ra) <= tol * 1e-3 && double.Abs(rb) <= tol * 1e-3) return x;
            var (ga, gb) = (a.Outward(x) * -1, b.Outward(x) * -1);
            var (j00, j01, j10, j11) = (ga.Dot(meridian), ga.Dot(axis), gb.Dot(meridian), gb.Dot(axis));
            var det = j00 * j11 - j01 * j10;
            if (double.Abs(det) < 1e-12) return null;
            rho -= (ra * j11 - rb * j01) / det; z -= (rb * j00 - ra * j10) / det;
        }
        return null;
    }

    /// <summary>Sampled ball centres along every strip and at every corner stay further than r from each face that
    /// is not one of their own, wherever that face actually is.</summary>
    private static Refusal? Clearance(BrepBody body, double radius, double tol, Dictionary<FaceId, Support> supports,
        Dictionary<EdgeId, Sharp> sharp, Dictionary<VertexId, (Point3D Centre, EdgeId[] Edges, FaceId[] Faces)> corners)
    {
        IEnumerable<(Point3D Centre, FaceId[] Own, string Where)> Samples()
        {
            foreach (var s in sharp.Values)
            {
                var own = new[] { s.A.Face, s.B.Face };
                if (s.Closed)
                {
                    var c = s.Shape.Center!.Value;
                    for (var i = 0; i < 24; i++) yield return (Rotate(s.Centre, c, s.Shape.Normal, Turn * i / 24), own, $"along edge {s.Edge.Id.Value}");
                    continue;
                }
                var (c0, c1) = (corners[s.Edge.StartVertexId].Centre, corners[s.Edge.EndVertexId].Centre);
                for (var i = 1; i < 8; i++)
                {
                    var t = i / 8d;
                    yield return (!s.Shape.Arc ? c0 + (c1 - c0) * t : Rotate(c0, s.Shape.Center!.Value, s.Shape.Normal, Angle(c0 - Axial(c0, s.Shape), c1 - Axial(c0, s.Shape), s.Shape.Normal) * t), own, $"along edge {s.Edge.Id.Value}");
                }
            }
            foreach (var (vertex, corner) in corners) yield return (corner.Centre, corner.Faces, $"at vertex {vertex.Value}");
        }
        foreach (var (centre, own, where) in Samples())
            foreach (var (face, support) in supports)
            {
                if (own.Contains(face)) continue;
                var foot = support.Foot(centre);
                // Touching counts: a ball that touches a third face leaves a face pinched to nothing there.
                if ((foot - centre).Length > radius + tol) continue;
                var on = FaceDomainQuery.TryClassifyPointOnFace(body, face, foot);
                if (on.Classification is FaceDomainClassification.Inside or FaceDomainClassification.OnBoundary)
                    return TooLarge(radius, $"the ball {where} reaches face {face.Value}").Refusal;
            }
        return null;
    }

    /// <summary>Each planar face's new loops keep their winding and do not cross one another.</summary>
    private static Refusal? Planar(double radius, double tol, Dictionary<FaceId, List<List<(EdgeId Edge, bool Reversed)>>> oriented, Dictionary<FaceId, Support> supports,
        Dictionary<EdgeId, Shape> shapes, Dictionary<EdgeId, Shape> built, Dictionary<FaceId, List<List<(EdgeId Edge, bool Reversed)>>> loops)
    {
        foreach (var (face, newLoops) in loops)
        {
            if (supports[face].Kind != SurfaceGeometryKind.Plane) continue;
            var plane = supports[face].Surface.Plane!.Value;
            var (u, v, n) = (plane.UAxis.ToVector(), plane.VAxis.ToVector(), plane.Normal.ToVector());
            (double X, double Y) Flat(Point3D p) => ((p - plane.Origin).Dot(u), (p - plane.Origin).Dot(v));
            List<(double X, double Y)> Polyline(IEnumerable<(Shape Shape, bool Reversed)> edges) =>
                edges.SelectMany(e => { var pts = e.Shape.Points(24).ToList(); if (e.Reversed) pts.Reverse(); return pts.Take(pts.Count - 1); }).Select(Flat).ToList();
            var original = oriented[face].Select(l => Polyline(l.Select(u => (shapes[u.Edge], u.Reversed)))).ToList();
            var segments = new List<((double X, double Y) P, (double X, double Y) Q, EdgeId Edge)>();
            for (var i = 0; i < newLoops.Count; i++)
            {
                var polyline = Polyline(newLoops[i].Select(e => (built[e.Edge], e.Reversed)));
                if (double.Sign(Area(polyline)) != double.Sign(Area(original[i])) || double.Abs(Area(polyline)) <= tol * tol)
                    return TooLarge(radius, $"the rounding turns a boundary of face {face.Value} inside out").Refusal;
                foreach (var (edge, reversed) in newLoops[i])
                {
                    var pts = built[edge].Points(24).Select(Flat).ToList();
                    for (var k = 0; k + 1 < pts.Count; k++) segments.Add((pts[k], pts[k + 1], edge));
                }
            }
            for (var i = 0; i < segments.Count; i++)
                for (var j = i + 1; j < segments.Count; j++)
                    if (segments[i].Edge != segments[j].Edge && Crosses(segments[i].P, segments[i].Q, segments[j].P, segments[j].Q, tol))
                        return TooLarge(radius, $"the rounding makes the boundaries of face {face.Value} cross").Refusal;
        }
        return null;
    }

    /// <summary>A loop's signed area about the face's outward normal: in the plane, by the shoelace over its
    /// vertices plus each arc's circular segment; on a cylinder or cone, in the (angle, height) chart, where its
    /// circles and generators are straight and counterclockwise is the surface normal.</summary>
    private static double SignedArea(Support face, List<(EdgeId Edge, bool Reversed)> loop, Dictionary<EdgeId, Shape> shapes)
    {
        var sense = face.Sense;
        var area = 0d;
        if (face.Kind == SurfaceGeometryKind.Plane)
        {
            var plane = face.Surface.Plane!.Value; var n = plane.Normal.ToVector();
            var (u, v) = (plane.UAxis.ToVector(), plane.VAxis.ToVector());
            var corners = loop.Select(x => StartOf(shapes[x.Edge], x.Reversed)).Select(q => ((q - plane.Origin).Dot(u), (q - plane.Origin).Dot(v))).ToList();
            for (var i = 0; i < corners.Count; i++) { var (a, b) = (corners[i], corners[(i + 1) % corners.Count]); area += (a.Item1 * b.Item2 - b.Item1 * a.Item2) / 2; }
            foreach (var (edge, reversed) in loop)
            {
                var shape = shapes[edge]; if (!shape.Arc) continue;
                var r = (shape.Start - shape.Center!.Value).Length; var turn = (reversed ? shape.Normal * -1 : shape.Normal).Dot(n) > 0 ? 1 : -1;
                area += turn * r * r / 2 * (shape.Sweep - double.Sin(shape.Sweep));
            }
            return sense * area;
        }
        var (origin, axis, x0) = face.Kind == SurfaceGeometryKind.Cylinder
            ? (face.Surface.Cylinder!.Value.Origin, face.Surface.Cylinder!.Value.Axis.ToVector(), face.Surface.Cylinder!.Value.XAxis.ToVector())
            : (face.Surface.Cone!.Value.Apex, face.Surface.Cone!.Value.Axis.ToVector(), face.Surface.Cone!.Value.XAxis());
        var theta = double.Atan2((StartOf(shapes[loop[0].Edge], loop[0].Reversed) - origin).Dot(axis.Cross(x0)), (StartOf(shapes[loop[0].Edge], loop[0].Reversed) - origin).Dot(x0));
        var chart = new List<(double T, double H)>();
        foreach (var (edge, reversed) in loop)
        {
            var shape = shapes[edge];
            chart.Add((theta, (StartOf(shape, reversed) - origin).Dot(axis)));
            if (shape.Arc) theta += shape.Sweep * ((reversed ? shape.Normal * -1 : shape.Normal).Dot(axis) > 0 ? 1 : -1);
        }
        for (var i = 0; i < chart.Count; i++) { var (a, b) = (chart[i], chart[(i + 1) % chart.Count]); area += (a.T * b.H - b.T * a.H) / 2; }
        return sense * area;
    }

    private static Point3D StartOf(Shape shape, bool reversed) => reversed ? shape.End : shape.Start;

    /// <summary>Vertex positions, falling back to the curve's ends where a body leaves a vertex unplaced.</summary>
    private static Dictionary<VertexId, Point3D> VertexPoints(BrepBody body)
    {
        var points = new Dictionary<VertexId, Point3D>();
        foreach (var vertex in body.Topology.Vertices) if (body.TryGetVertexPoint(vertex.Id, out var p)) points[vertex.Id] = p;
        foreach (var edge in body.Topology.Edges)
        {
            if (points.ContainsKey(edge.StartVertexId) && points.ContainsKey(edge.EndVertexId)) continue;
            if (!body.Bindings.TryGetEdgeBinding(edge.Id, out var b) || b.TrimInterval is not { } t || !body.Geometry.TryGetCurve(b.CurveGeometryId, out var curve) || curve is null) continue;
            Point3D? At(double s) => curve.Kind switch { CurveGeometryKind.Line3 => curve.Line3!.Value.Evaluate(s), CurveGeometryKind.Circle3 => curve.Circle3!.Value.Evaluate(s), _ => null };
            var (first, last) = b.OrientedEdgeSense ? (t.Start, t.End) : (t.End, t.Start);
            if (!points.ContainsKey(edge.StartVertexId) && At(first) is { } a) points[edge.StartVertexId] = a;
            if (!points.ContainsKey(edge.EndVertexId) && At(last) is { } z) points[edge.EndVertexId] = z;
        }
        return points;
    }

    private static Shape? ShapeOf(BrepBody body, Edge edge, Dictionary<VertexId, Point3D> points)
    {
        if (!body.Bindings.TryGetEdgeBinding(edge.Id, out var binding) || !body.Geometry.TryGetCurve(binding.CurveGeometryId, out var curve) || curve is null) return null;
        var (start, end) = (points[edge.StartVertexId], points[edge.EndVertexId]);
        switch (curve.Kind)
        {
            case CurveGeometryKind.Line3:
                return (end - start).Length > 0 ? new Shape(start, end, null, Vector3D.Zero, 0) : null;
            case CurveGeometryKind.Circle3:
            {
                var circle = curve.Circle3!.Value; var c = circle.Center; var n = circle.Normal.ToVector();
                var span = binding.TrimInterval is { } t ? double.Abs(t.End - t.Start) : Turn;
                if (edge.StartVertexId == edge.EndVertexId)
                    return new Shape(start, start, c, binding.OrientedEdgeSense ? n : n * -1, Turn);
                var forward = Angle(start - c, end - c, n);
                if (double.Abs(forward - span) < 1e-6) return new Shape(start, end, c, n, forward);
                if (double.Abs(Turn - forward - span) < 1e-6) return new Shape(start, end, c, n * -1, Turn - forward);
                return null;
            }
            default: return null;
        }
    }

    /// <summary>The face runs along the edge: a plane containing its line, a cylinder around a parallel axis; or
    /// around the circle's axis: a plane across it, a cylinder or cone on it.</summary>
    private static bool Shares(Support face, Shape shape, double tol)
    {
        var s = face.Surface;
        if (!shape.Arc)
        {
            var d = Unit(shape.End - shape.Start);
            return face.Kind switch
            {
                SurfaceGeometryKind.Plane => double.Abs(s.Plane!.Value.Normal.ToVector().Dot(d)) < 1e-9,
                SurfaceGeometryKind.Cylinder => s.Cylinder!.Value.Axis.ToVector().Cross(d).Length < 1e-9,
                _ => false,
            };
        }
        var (c, n) = (shape.Center!.Value, shape.Normal);
        bool OnAxis(Point3D origin, Vector3D axis) => axis.Cross(n).Length < 1e-9 && ((c - origin) - axis * (c - origin).Dot(axis)).Length <= tol;
        return face.Kind switch
        {
            SurfaceGeometryKind.Plane => s.Plane!.Value.Normal.ToVector().Cross(n).Length < 1e-9,
            SurfaceGeometryKind.Cylinder => OnAxis(s.Cylinder!.Value.Origin, s.Cylinder!.Value.Axis.ToVector()),
            _ => OnAxis(s.Cone!.Value.Apex, s.Cone!.Value.Axis.ToVector()),
        };
    }

    /// <summary>The ball's cross-section at a centre: the arc from one contact to the other the short way round.</summary>
    private static Shape Cross(Point3D a, Point3D b, Point3D centre, Vector3D tangent)
    {
        var sweep = Angle(a - centre, b - centre, tangent);
        return sweep <= double.Pi ? new Shape(a, b, centre, tangent, sweep) : new Shape(a, b, centre, tangent * -1, Turn - sweep);
    }

    private static Point3D Project(Point3D p, Shape arc) => arc.Center!.Value + Unit(Flatten(p - arc.Center!.Value, arc.Normal)) * (arc.Start - arc.Center!.Value).Length;

    private static Point3D Axial(Point3D p, Shape arc) => arc.Center!.Value + arc.Normal * (p - arc.Center!.Value).Dot(arc.Normal);

    private static Vector3D Flatten(Vector3D v, Vector3D n) => v - n * v.Dot(n);

    /// <summary>Angle from a to b about n, in [0, 2π).</summary>
    private static double Angle(Vector3D a, Vector3D b, Vector3D n)
    {
        var (fa, fb) = (Flatten(a, n), Flatten(b, n));
        var angle = double.Atan2(n.Dot(fa.Cross(fb)), fa.Dot(fb));
        return angle < -1e-12 ? angle + Turn : double.Max(angle, 0);
    }

    private static Point3D Rotate(Point3D p, Point3D centre, Vector3D n, double angle)
    {
        var axial = centre + n * (p - centre).Dot(n); var r = p - axial; var y = n.Cross(r);
        return axial + r * double.Cos(angle) + y * double.Sin(angle);
    }

    private static double Area(List<(double X, double Y)> p)
    {
        var area = 0d;
        for (var i = 0; i < p.Count; i++) { var (a, b) = (p[i], p[(i + 1) % p.Count]); area += a.X * b.Y - b.X * a.Y; }
        return area / 2;
    }

    private static bool Crosses((double X, double Y) p, (double X, double Y) q, (double X, double Y) r, (double X, double Y) s, double tol)
    {
        double Cross2((double X, double Y) o, (double X, double Y) a, (double X, double Y) b) => (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        var (d1, d2, d3, d4) = (Cross2(r, s, p), Cross2(r, s, q), Cross2(p, q, r), Cross2(p, q, s));
        var eps = tol * tol;
        return (d1 > eps && d2 < -eps || d1 < -eps && d2 > eps) && (d3 > eps && d4 < -eps || d3 < -eps && d4 > eps);
    }

    private static Vector3D Unit(Vector3D v) => v * (1 / v.Length);

    private static Vector3D Perpendicular(Vector3D n) => Unit(double.Abs(n.X) < 0.9 ? n.Cross(new Vector3D(1, 0, 0)) : n.Cross(new Vector3D(0, 1, 0)));

    private static string Article(SurfaceGeometryKind kind) => kind.ToString().ToLowerInvariant();

    private static Vector3D XAxis(this ConeSurface cone) => cone.Evaluate(0, 1) - (cone.Apex + cone.Axis.ToVector()) is var r && r.Length > 0 ? r * (1 / r.Length) : Perpendicular(cone.Axis.ToVector());

    private static Outcome Refuse(string code, string message) => new() { Refusal = new(code, message) };

    private static Outcome TooLarge(double radius, string why) => Refuse(UniformRounding.TooLargeCode, $"radius {radius:G6} mm does not fit: {why}");
}
