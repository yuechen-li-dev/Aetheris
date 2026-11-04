using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Blends;

/// <summary>
/// Every edge rounded by a rolling ball of one radius, built locally on the body's own face graph. For a radius
/// small against the body the topology is kept: each face stays on its surface and is trimmed back to where the
/// ball touches it, each edge becomes the tube around the ball centre's path (a cylinder around a line, a torus
/// around a circle) and each three-edge corner a piece of the ball. Across a convex edge the ball is inside the
/// material and cuts it away; across a concave edge it is outside and fills the corner (the same tube, facing its
/// axis). The ball centre is where the faces moved by the radius meet, so nothing here is approximated.
///
/// Admits faces on planes, cylinders and cones; line edges between faces that share the line's direction (planes
/// along it, cylinders around it); circle edges between faces that share the circle's axis; corners where three
/// edges of one kind meet; seams of periodic faces. The input's loop and face orientations are not trusted: both are
/// computed from the geometry. Refuses the rest by name, and a radius too large for the body (an edge or a face
/// used up, the ball reaching a face beyond its two) as such.
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
        public double Sense { get; set; } = sense;
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
                case SurfaceGeometryKind.Sphere:
                {
                    var c = Surface.Sphere!.Value; var q = x - c.Center;
                    return c.Center + (q.Length > 1e-14 ? q * (c.Radius / q.Length) : c.XAxis.ToVector() * c.Radius);
                }
                case SurfaceGeometryKind.Torus:
                {
                    var t = Surface.Torus!.Value; var tube = Tube(t, x); var q = x - tube;
                    return tube + (q.Length > 1e-14 ? q * (t.MinorRadius / q.Length) : t.Axis.ToVector() * t.MinorRadius);
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
                case SurfaceGeometryKind.Sphere: n = Unit(Foot(x) - Surface.Sphere!.Value.Center); break;
                case SurfaceGeometryKind.Torus: n = Unit(Foot(x) - Tube(Surface.Torus!.Value, x)); break;
                default:
                {
                    var k = Surface.Cone!.Value; var a = k.Axis.ToVector();
                    n = Radial(x - k.Apex, a, k.XAxis()) - a * double.Tan(k.SemiAngleRadians);
                    n *= 1 / n.Length; break;
                }
            }
            return n * Sense;
        }

        /// <summary>Signed distance to the surface, positive on the material side.</summary>
        public double Inside(Point3D x) { var f = Foot(x); return -Outward(x).Dot(x - f); }

        /// <summary>The nearest point of the torus's tube circle.</summary>
        private static Point3D Tube(TorusSurface t, Point3D x)
        {
            var a = t.Axis.ToVector(); var r = Radial(x - t.Center, a, t.XAxis.ToVector());
            return t.Center + r * t.MajorRadius;
        }

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

    /// <summary>Which edges a pass rounds: every sharp edge, or only the concave ones (the first pass where convex
    /// and concave edges meet at a corner: fillets before rounds).</summary>
    internal enum Pass { All, ConcaveOnly }

    internal const string MixedCode = "uniform-rounding-mixed-vertex-unsupported";

    /// <summary>A sharp edge to round: its two faces (A walks it forward), its shape and side.</summary>
    private sealed class Sharp
    {
        public required Edge Edge { get; init; }
        public required Shape Shape { get; init; }
        public required Support A { get; init; }
        public required Support B { get; init; }
        public bool Closed => Edge.StartVertexId == Edge.EndVertexId;
        // +1 across a convex edge (the ball inside the material, cutting it away), -1 across a concave one (the ball
        // outside, filling the corner).
        public required double Side { get; init; }
        // Closed circles: the ball centre at the seam's meridian.
        public Point3D Centre { get; set; }
    }

    private enum VertexKind { Untouched, Corner, End, Junction }

    /// <summary>What becomes of a vertex: kept; a piece of the ball where three rounded edges meet; the end of a
    /// rounded edge's strip on the face across it (the two other edges kept); or two rounded edges whose strips
    /// run on into each other where their faces meet tangentially. One ball centre each.</summary>
    private sealed class Plan
    {
        public required VertexKind Kind { get; init; }
        public Point3D Centre { get; init; }
        // Corner: its three faces. End: the strip's two faces, then the face it ends on. Junction: the common face,
        // then the two tangent faces.
        public FaceId[] Faces { get; init; } = [];
        public EdgeId[] Rounded { get; init; } = [];
        public double Side { get; init; }
    }

    /// <summary>Every sharp edge rounded, in one pass, or in two where convex and concave edges meet at a corner:
    /// the concave edges filled first, then everything that is still sharp rounded (on a body this construction
    /// built, so its orientation is trusted).</summary>
    internal static Outcome RoundAll(BrepBody body, double radius)
    {
        var outcome = Round(body, radius, Pass.All, trusted: false);
        if (outcome.Refusal?.Code != MixedCode || outcome.Refusal.Message.Contains("two concave", StringComparison.Ordinal)) return outcome;
        var filled = Round(body, radius, Pass.ConcaveOnly, trusted: false);
        return filled.Body is null ? filled : Round(filled.Body, radius, Pass.All, trusted: true);
    }

    internal static Outcome Round(BrepBody body, double radius, Pass pass = Pass.All, bool trusted = false)
    {
        var topology = body.Topology;
        var points = VertexPoints(body);
        if (points.Count != topology.Vertices.Count())
            return Refuse("uniform-rounding-edge-unsupported", "some vertices of the body have no position");
        var extent = points.Values.Max(p => (p - Point3D.Origin).Length);
        var tol = double.Max(1e-9, (extent + 1) * 1e-10) * 1e2;
        if (radius <= 1e3 * tol) return Refuse("uniform-rounding-radius-invalid", $"radius {radius:G6} mm is below the construction tolerance");

        // Faces: supports with their outward side. A body this construction built may carry its own blends.
        var supports = new Dictionary<FaceId, Support>();
        foreach (var face in topology.Faces.OrderBy(f => f.Id.Value))
        {
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null
                || surface.Kind is not (SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Cone) && !(trusted && surface.Kind is SurfaceGeometryKind.Sphere or SurfaceGeometryKind.Torus))
                return Refuse("uniform-rounding-curved-face-unsupported", $"rounding every edge supports faces on planes, cylinders and cones for now; face {face.Id.Value} is {surface?.Kind.ToString() ?? "unbound"}");
            body.Bindings.TryGetFaceBinding(face.Id, out var binding);
            supports.Add(face.Id, new Support(face.Id, surface, binding.Orientation.IsAlignedWithSurface ? 1 : -1));
        }
        var faceOfLoop = topology.Faces.SelectMany(f => f.LoopIds.Select(l => (l, f.Id))).ToDictionary(x => x.l, x => x.Id);
        var uses = topology.Coedges.GroupBy(c => c.EdgeId).ToDictionary(g => g.Key, g => g.ToArray());

        // Edges: shapes, seams (one face on both sides) and pairs of faces.
        var shapes = new Dictionary<EdgeId, Shape>();
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
                if (!trusted && (shape.Arc || supports[f0].Kind == SurfaceGeometryKind.Plane))
                    return Refuse("uniform-rounding-edge-unsupported", $"edge {edge.Id.Value} has face {f0.Value} on both sides and is not the seam of a cylinder or cone");
                seams.Add(edge.Id, f0); continue;
            }
            pairs.Add(edge.Id, (f0, f1));
        }
        var boundaries = topology.Faces.ToDictionary(f => f.Id, f => f.LoopIds.SelectMany(l => topology.GetLoop(l).CoedgeIds.Select(c => topology.GetCoedge(c).EdgeId)).Distinct().Select(e => shapes[e]).ToArray());

        // Orientation: every edge of a face walked with the face's interior on its left about the outward normal.
        // A body from elsewhere is not trusted for it (its loops' senses are not even consistent within one loop,
        // and a revolved body's cap can be bound facing into its material): outward sides by ray parity from just
        // off each face, each edge's direction by stepping off it into the face. Seams are walked both ways.
        var oriented = new Dictionary<FaceId, List<List<(EdgeId Edge, bool Reversed)>>>();
        if (trusted)
        {
            foreach (var face in topology.Faces)
                oriented.Add(face.Id, face.LoopIds.Select(l => topology.GetLoop(l).CoedgeIds.Select(topology.GetCoedge).Select(c => (c.EdgeId, c.IsReversed)).ToList()).ToList());
        }
        else
        {
            var span = 1e-6 * (extent + 1);
            foreach (var face in topology.Faces.OrderBy(f => f.Id.Value))
            {
                var support = supports[face.Id];
                var edge = face.LoopIds.SelectMany(l => topology.GetLoop(l).CoedgeIds.Select(c => topology.GetCoedge(c).EdgeId)).FirstOrDefault(e => !seams.ContainsKey(e));
                var shape = shapes[edge]; var p = shape.At(0.5); var t = shape.Tangent(p);
                var w = Unit(support.Outward(p).Cross(t));
                var onFace = new[] { support.Foot(p + w * span * 1e3), support.Foot(p - w * span * 1e3) }.Where(q => InsideFace(support, boundaries[face.Id], q)).ToArray();
                if (onFace.Length != 1) return Refuse("uniform-rounding-edge-unsupported", $"could not find the inside of face {face.Id.Value}");
                var normal = support.Outward(onFace[0]) * support.Sense; // the surface's own normal there
                var outside = !Inside(supports.Values, boundaries, onFace[0] + normal * (span * 1e2));
                var inside = Inside(supports.Values, boundaries, onFace[0] - normal * (span * 1e2));
                if (outside != inside) return Refuse("uniform-rounding-edge-unsupported", $"could not tell which side of face {face.Id.Value} the material is on");
                support.Sense = outside ? 1 : -1;
            }
            foreach (var face in topology.Faces.OrderBy(f => f.Id.Value))
            {
                var support = supports[face.Id];
                var loops = new List<List<(EdgeId, bool)>>();
                foreach (var loopId in face.LoopIds)
                {
                    var loop = new List<(EdgeId, bool)>();
                    foreach (var edge in topology.GetLoop(loopId).CoedgeIds.Select(c => topology.GetCoedge(c).EdgeId).Distinct())
                    {
                        if (seams.ContainsKey(edge)) { loop.Add((edge, false)); loop.Add((edge, true)); continue; }
                        var shape = shapes[edge]; var p = shape.At(0.5); var t = shape.Tangent(p);
                        var w = Unit(support.Outward(p).Cross(t));
                        var (left, right) = (InsideFace(support, boundaries[face.Id], support.Foot(p + w * span)), InsideFace(support, boundaries[face.Id], support.Foot(p - w * span)));
                        if (left == right)
                            return Refuse("uniform-rounding-edge-unsupported", $"could not tell which side of edge {edge.Value} face {face.Id.Value} lies on");
                        loop.Add((edge, !left));
                    }
                    loops.Add(loop);
                }
                oriented.Add(face.Id, loops);
            }
        }

        // Edges between two faces: tangent ones are kept; sharp ones have a side (A walks the edge forward, and across
        // a convex edge B's interior, left of B's direction about its outward normal, lies behind A's tangent plane).
        var sharp = new Dictionary<EdgeId, Sharp>();
        var kept = new Dictionary<EdgeId, (FaceId F0, FaceId F1, double Side)>(); // side 0: tangent
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
            {
                // Two faces on one plane: one face split in two (some constructions leave a wall cut at a pocket's
                // depth). The edge between them is not an edge of the part.
                if (a.Kind == SurfaceGeometryKind.Plane && b.Kind == SurfaceGeometryKind.Plane)
                    return Refuse("uniform-rounding-split-face-unsupported", $"faces {f0.Value} and {f1.Value} lie on one plane and meet at edge {edgeId.Value}: the body has a face split in two, and rounding every edge needs each face whole for now");
                kept.Add(edgeId, (f0, f1, 0)); continue;
            }
            var side = na.Dot(nb.Cross(shape.Tangent(p) * -1)) < 0 ? 1d : -1d;
            if (pass == Pass.ConcaveOnly && side > 0) { kept.Add(edgeId, (f0, f1, side)); continue; }
            if (!Shares(a, shape, tol) || !Shares(b, shape, tol))
                return Refuse("uniform-rounding-edge-unsupported", $"rounding every edge needs each edge's faces to run along its line or around its circle's axis: edge {edgeId.Value} {(shape.Arc ? "is a circle" : "is a line")} between a {Article(a.Kind)} and a {Article(b.Kind)} face that do not, so the ball's path along it is not a line or a circle");
            sharp.Add(edgeId, new Sharp { Edge = edge, Shape = shape, A = a, B = b, Side = side });
        }
        string Kind(EdgeId e) => sharp.TryGetValue(e, out var s) ? s.Side > 0 ? "convex" : "concave" : kept[e].Side > 0 ? "convex" : kept[e].Side < 0 ? "concave" : "tangent";
        FaceId[] FacesOf(EdgeId e) => sharp.TryGetValue(e, out var s) ? [s.A.Face, s.B.Face] : [kept[e].F0, kept[e].F1];

        // Vertices.
        var plans = new Dictionary<VertexId, Plan>();
        foreach (var vertex in topology.Vertices.OrderBy(v => v.Id.Value))
        {
            var open = topology.Edges.Where(e => (e.StartVertexId == vertex.Id || e.EndVertexId == vertex.Id) && e.StartVertexId != e.EndVertexId && !seams.ContainsKey(e.Id)).ToArray();
            var rounded = open.Where(e => sharp.ContainsKey(e.Id)).Select(e => e.Id).ToArray();
            var others = open.Where(e => !sharp.ContainsKey(e.Id)).Select(e => e.Id).ToArray();
            if (rounded.Length == 0) { plans.Add(vertex.Id, new Plan { Kind = VertexKind.Untouched }); continue; }
            var joins = string.Join(", ", open.Select(e => $"{e.Id.Value} ({Kind(e.Id)})"));
            var sides = rounded.Select(e => sharp[e].Side).Concat(others.Where(e => kept[e].Side != 0).Select(e => kept[e].Side)).Distinct().ToArray();
            var faces = open.SelectMany(e => FacesOf(e.Id)).Distinct().ToArray();
            var side = sharp[rounded[0]].Side;
            if (rounded.Length == 3 && others.Length == 0 && faces.Length == 3)
            {
                if (sides.Length > 1)
                    return Refuse(MixedCode, $"rounding every edge does not support corners where convex and concave edges meet yet: vertex {vertex.Id.Value} joins edges {joins}");
                var centre = Centre(faces.Select(f => supports[f]).ToArray(), points[vertex.Id], [side * radius, side * radius, side * radius], tol);
                if (centre is null || (centre.Value - points[vertex.Id]).Length > 50 * radius)
                    return TooLarge(radius, $"the ball does not fit in the corner at vertex {vertex.Id.Value}");
                plans.Add(vertex.Id, new Plan { Kind = VertexKind.Corner, Centre = centre.Value, Faces = faces, Rounded = rounded, Side = side });
                continue;
            }
            if (rounded.Length == 1 && others.Length == 2 && faces.Length == 3 && others.All(e => kept[e].Side != 0) && pass == Pass.ConcaveOnly)
            {
                // The strip ends on the face across the vertex, which must stand square to it: the ball's
                // cross-section is then where the strip meets that face.
                var s = sharp[rounded[0]]; var across = faces.Single(f => f != s.A.Face && f != s.B.Face);
                if (s.Shape.Arc || supports[across].Kind != SurfaceGeometryKind.Plane || supports[across].Surface.Plane!.Value.Normal.ToVector().Cross(Unit(s.Shape.End - s.Shape.Start)).Length > 1e-9)
                    return Refuse("uniform-rounding-vertex-unsupported", $"at vertex {vertex.Id.Value} the concave edge {rounded[0].Value} ends on face {across.Value}, which does not stand square to it; filling it there is not built yet");
                var centre = Centre([s.A, s.B, supports[across]], points[vertex.Id], [side * radius, side * radius, 0], tol);
                if (centre is null || new[] { s.A, s.B }.Any(f => double.Abs(supports[across].Inside(f.Foot(centre.Value))) > tol))
                    return TooLarge(radius, $"the ball does not fit where edge {rounded[0].Value} ends at vertex {vertex.Id.Value}");
                plans.Add(vertex.Id, new Plan { Kind = VertexKind.End, Centre = centre.Value, Faces = [s.A.Face, s.B.Face, across], Rounded = rounded, Side = side });
                continue;
            }
            if (rounded.Length == 2 && others.Length == 1 && kept[others[0]].Side == 0 && faces.Length == 3 && sides.Length == 1)
            {
                // Two strips run on into each other: their common face, and the two faces meeting tangentially.
                var (e1, e2) = (sharp[rounded[0]], sharp[rounded[1]]);
                var common = new[] { e1.A.Face, e1.B.Face }.Intersect([e2.A.Face, e2.B.Face]).ToArray();
                var tangent = shapes[others[0]];
                if (common.Length != 1 || tangent.Arc)
                    return Refuse("uniform-rounding-vertex-unsupported", $"vertex {vertex.Id.Value} joins edges {joins}; two strips meeting where faces join tangentially need a straight tangent edge and one common face");
                var g = common[0]; var (fa, fb) = (kept[others[0]].F0, kept[others[0]].F1);
                var d = Unit(tangent.End - tangent.Start) * (topology.GetEdge(others[0]).StartVertexId == vertex.Id ? 1 : -1);
                var centre = Junction(supports[g], supports[fa], points[vertex.Id], d, side * radius, tol);
                if (centre is null || double.Abs(supports[fb].Inside(centre.Value) - side * radius) > tol * 1e2)
                    return TooLarge(radius, $"the ball does not fit where the strips meet at vertex {vertex.Id.Value}");
                plans.Add(vertex.Id, new Plan { Kind = VertexKind.Junction, Centre = centre.Value, Faces = [g, fa, fb], Rounded = rounded, Side = side });
                continue;
            }
            if (sides.Length > 1)
                return Refuse(MixedCode, rounded.Length == 2 && pass == Pass.ConcaveOnly
                    ? $"rounding every edge does not support corners where two concave edges and a convex one meet: vertex {vertex.Id.Value} joins edges {joins} (filling the concave edges leaves the convex one between two fills, which does not round to a torus)"
                    : $"rounding every edge does not support corners where convex and concave edges meet yet: vertex {vertex.Id.Value} joins edges {joins}");
            return Refuse("uniform-rounding-vertex-unsupported", $"vertex {vertex.Id.Value} joins edges {joins}; rounding needs three edges at a corner, or two strips meeting where faces join tangentially");
        }

        // Closed circles: the ball centre at the seam's meridian (or the circle's own start).
        foreach (var s in sharp.Values.Where(s => s.Closed))
        {
            var seam = seams.Where(x => x.Value == s.A.Face || x.Value == s.B.Face).Select(x => shapes[x.Key].Start).ToArray();
            var c = s.Shape.Center!.Value; var nu = s.Shape.Normal;
            var x0 = Unit(s.Shape.Start - c);
            if (seam.Length > 0)
            {
                var meridian = Unit((seam[0] - c) - nu * (seam[0] - c).Dot(nu));
                if (seam.Skip(1).Any(q => Unit((q - c) - nu * (q - c).Dot(nu)).Dot(meridian) < 1 - 1e-9))
                    return Refuse("uniform-rounding-edge-unsupported", $"the faces around circle edge {s.Edge.Id.Value} have seams at different angles");
                x0 = meridian;
            }
            var centre = Meridian(s.A, s.B, c, nu, x0, s.Shape.Start, s.Side * radius, tol, (s.Shape.Start - c).Length);
            if (centre is null) return TooLarge(radius, $"the ball does not fit along circle edge {s.Edge.Id.Value}");
            s.Centre = centre.Value;
        }

        return Build(body, radius, tol, points, supports, oriented, shapes, sharp, kept, seams, plans, boundaries);
    }

    private static Outcome Build(BrepBody body, double radius, double tol, Dictionary<VertexId, Point3D> points, Dictionary<FaceId, Support> supports,
        Dictionary<FaceId, List<List<(EdgeId Edge, bool Reversed)>>> oriented, Dictionary<EdgeId, Shape> shapes, Dictionary<EdgeId, Sharp> sharp,
        Dictionary<EdgeId, (FaceId F0, FaceId F1, double Side)> kept, Dictionary<EdgeId, FaceId> seams, Dictionary<VertexId, Plan> plans,
        Dictionary<FaceId, Shape[]> boundaries)
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
        // The first loop is the face's outer boundary, and is bound as such: STEP writes it as the outer bound, and
        // a reader takes the face's side from it.
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
            for (var i = 0; i < loopIds.Count; i++)
                bindings.AddFaceBoundaryRoleBinding(new FaceBoundaryRoleBinding(face, loopIds[i], i == 0 ? FaceBoundaryRole.Outer : FaceBoundaryRole.Inner));
            var sid = new SurfaceGeometryId(geometry.Surfaces.Count() + 1); geometry.AddSurface(sid, surface);
            bindings.AddFaceBinding(new FaceGeometryBinding(face, sid, aligned));
        }
        VertexId StartOfEdge(EdgeId e) => builder.Model.GetEdge(e).StartVertexId;

        // Vertices: kept ones once; where the ball touches each face at a corner, an end or a junction (both tangent
        // faces at one point); for closed circles, where the ball at the meridian touches each side.
        var keep = new Dictionary<VertexId, VertexId>();
        VertexId Kept(VertexId v) => keep.TryGetValue(v, out var k) ? k : keep[v] = NewVertex(points[v]);
        var contact = new Dictionary<(VertexId Vertex, FaceId Face), VertexId>();
        foreach (var (vertex, plan) in plans.OrderBy(p => p.Key.Value))
        {
            switch (plan.Kind)
            {
                case VertexKind.Corner:
                    foreach (var face in plan.Faces) contact.Add((vertex, face), NewVertex(supports[face].Foot(plan.Centre)));
                    break;
                case VertexKind.End:
                    foreach (var face in plan.Faces.Take(2)) contact.Add((vertex, face), NewVertex(supports[face].Foot(plan.Centre)));
                    break;
                case VertexKind.Junction:
                    contact.Add((vertex, plan.Faces[0]), NewVertex(supports[plan.Faces[0]].Foot(plan.Centre)));
                    var touch = NewVertex(supports[plan.Faces[1]].Foot(plan.Centre));
                    contact.Add((vertex, plan.Faces[1]), touch); contact.Add((vertex, plan.Faces[2]), touch);
                    break;
            }
        }
        var ring = new Dictionary<(EdgeId Edge, FaceId Face), VertexId>();
        foreach (var s in sharp.Values.Where(s => s.Closed))
        {
            ring.Add((s.Edge.Id, s.A.Face), NewVertex(s.A.Foot(s.Centre)));
            ring.Add((s.Edge.Id, s.B.Face), NewVertex(s.B.Foot(s.Centre)));
        }

        // Rounded edges: contact edges on each face, the ball's cross-section at each end (shared where two strips
        // meet), and the strip.
        var along = new Dictionary<(EdgeId Edge, FaceId Face), EdgeId>();
        var section = new Dictionary<(EdgeId Edge, VertexId Vertex), EdgeId>();
        var shared = new Dictionary<VertexId, EdgeId>();
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
                section.Add((s.Edge.Id, s.Edge.StartVertexId), NewEdge(a, b, Cross(positions[a], positions[b], s.Centre, Unit(nu.Cross(s.Centre - c)))));
                var axisCentre = c + nu * (s.Centre - c).Dot(nu); var major = (s.Centre - axisCentre).Length;
                if (major <= radius + tol) return TooLarge(radius, $"the ball along circle edge {s.Edge.Id.Value} reaches the circle's axis");
                strips.Add((s, SurfaceGeometry.FromTorus(new TorusSurface(axisCentre, Direction3D.Create(nu), major, radius, Direction3D.Create(s.Centre - axisCentre)))));
                continue;
            }

            var (start, end) = (s.Edge.StartVertexId, s.Edge.EndVertexId);
            var (c0, c1) = (plans[start].Centre, plans[end].Centre);
            foreach (var face in new[] { s.A, s.B })
            {
                var (v0, v1) = (contact[(start, face.Face)], contact[(end, face.Face)]);
                var path = Trimmed(shape, positions[v0], positions[v1], tol);
                if (path is null) return TooLarge(radius, $"the rounding uses up edge {s.Edge.Id.Value} on face {face.Face.Value}");
                along.Add((s.Edge.Id, face.Face), NewEdge(v0, v1, path));
            }
            foreach (var (vertex, centre) in new[] { (start, c0), (end, c1) })
            {
                if (plans[vertex].Kind == VertexKind.Junction && shared.TryGetValue(vertex, out var existing)) { section.Add((s.Edge.Id, vertex), existing); continue; }
                var (a, b) = (contact[(vertex, s.A.Face)], contact[(vertex, s.B.Face)]);
                var cut = NewEdge(a, b, Cross(positions[a], positions[b], centre, shape.Tangent(shape.Arc ? Project(centre, shape) : centre)));
                section.Add((s.Edge.Id, vertex), cut);
                if (plans[vertex].Kind == VertexKind.Junction) shared.Add(vertex, cut);
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

        // Kept edges, trimmed to wherever their ends went: a kept vertex, the contact on the face they share with a
        // strip that ends there, or the tangent faces' common contact.
        VertexId EndOf(EdgeId edge, VertexId vertex)
        {
            var plan = plans.GetValueOrDefault(vertex);
            if (plan is null || plan.Kind == VertexKind.Untouched) return Kept(vertex);
            var faces = kept[edge] is var k ? new[] { k.F0, k.F1 } : [];
            return contact[(vertex, faces.First(f => contact.ContainsKey((vertex, f))))];
        }
        var keptEdges = new Dictionary<EdgeId, EdgeId>();
        foreach (var edge in kept.Keys.Concat(seams.Keys.Where(e => !sharp.Values.Any(s => s.Closed && (s.A.Face == seams[e] || s.B.Face == seams[e])))).OrderBy(e => e.Value))
        {
            var original = topology.GetEdge(edge); var shape = shapes[edge];
            var (v0, v1) = (EndOf(edge, original.StartVertexId), original.StartVertexId == original.EndVertexId ? EndOf(edge, original.StartVertexId) : EndOf(edge, original.EndVertexId));
            var path = original.StartVertexId == original.EndVertexId ? shape with { Start = positions[v0], End = positions[v0] } : Trimmed(shape, positions[v0], positions[v1], tol);
            if (path is null) return TooLarge(radius, $"the rounding uses up edge {edge.Value}");
            keptEdges.Add(edge, NewEdge(v0, v1, path));
        }

        // Seams of faces whose rims are rounded: the generator between the contacts at either end.
        var seam = new Dictionary<EdgeId, EdgeId>();
        foreach (var (edge, face) in seams.Where(x => !keptEdges.ContainsKey(x.Key)))
        {
            var shape = shapes[edge];
            VertexId? Nearest(Point3D p) => sharp.Values.Where(s => s.Closed && (s.A.Face == face || s.B.Face == face)).Select(s => (Shape: s.Shape, Vertex: ring[(s.Edge.Id, face)]))
                .Concat(kept.Keys.Where(e => topology.GetEdge(e).StartVertexId == topology.GetEdge(e).EndVertexId && (kept[e].F0 == face || kept[e].F1 == face)).Select(e => (Shape: shapes[e], Vertex: builder.Model.GetEdge(keptEdges[e]).StartVertexId)))
                .OrderBy(x => double.Abs((p - x.Shape.Center!.Value).Dot(x.Shape.Normal))).Select(x => (VertexId?)x.Vertex).FirstOrDefault();
            if (Nearest(shape.Start) is not { } v0 || Nearest(shape.End) is not { } v1 || v0 == v1)
                return Refuse("uniform-rounding-edge-unsupported", $"seam edge {edge.Value} on face {face.Value} does not run between two circle edges");
            if ((positions[v1] - positions[v0]).Dot(shape.End - shape.Start) <= tol || Unit(positions[v1] - positions[v0]).Cross(Unit(shape.End - shape.Start)).Length > 1e-7)
                return TooLarge(radius, $"the rounding uses up face {face.Value} along its seam");
            seam.Add(edge, NewEdge(v0, v1, new Shape(positions[v0], positions[v1], null, Vector3D.Zero, 0)));
        }

        // Strips: the contact on A from the end back to the start, the section at the start across to B, the
        // contact on B, the section at the end back to A. Each section's direction from its own ends.
        var stripUse = new Dictionary<EdgeId, bool>();
        var stripLoops = new List<(Sharp Sharp, SurfaceGeometry Surface, List<(EdgeId, bool)> Loop)>();
        foreach (var (s, surface) in strips)
        {
            var a = along[(s.Edge.Id, s.A.Face)]; var b = along[(s.Edge.Id, s.B.Face)];
            var first = section[(s.Edge.Id, s.Edge.StartVertexId)];
            var last = s.Closed ? first : section[(s.Edge.Id, s.Edge.EndVertexId)];
            var firstReversed = StartOfEdge(first) != StartOfEdge(a);
            var lastReversed = s.Closed ? !firstReversed : StartOfEdge(last) != builder.Model.GetEdge(b).EndVertexId;
            stripUse[first] = firstReversed; if (!s.Closed) stripUse[last] = lastReversed;
            stripLoops.Add((s, surface, [(a, true), (first, firstReversed), (b, false), (last, lastReversed)]));
        }

        // Each loop's uses in walking order, from the new edges' shared vertices.
        List<(EdgeId Edge, bool Reversed)>? Chain(List<(EdgeId Edge, bool Reversed)> uses)
        {
            VertexId From((EdgeId Edge, bool Reversed) u) { var e = builder.Model.GetEdge(u.Edge); return u.Reversed ? e.EndVertexId : e.StartVertexId; }
            VertexId To((EdgeId Edge, bool Reversed) u) { var e = builder.Model.GetEdge(u.Edge); return u.Reversed ? e.StartVertexId : e.EndVertexId; }
            var rest = uses.ToList(); var ordered = new List<(EdgeId, bool)> { rest[0] }; rest.RemoveAt(0);
            while (rest.Count > 0)
            {
                // A circle closed on the vertex goes first: otherwise a seam's return would leave it behind.
                var next = rest.FindIndex(u => From(u) == To(ordered[^1]) && To(u) == From(u));
                if (next < 0) next = rest.FindIndex(u => From(u) == To(ordered[^1]));
                if (next < 0) return null;
                ordered.Add(rest[next]); rest.RemoveAt(next);
            }
            return From(ordered[0]) == To(ordered[^1]) ? ordered : null;
        }

        // Original faces, trimmed: each edge replaced by its contact edge, kept edge or rebuilt seam, same
        // direction; a strip that ends on the face leaves its end section in the loop where the vertex was.
        var originalLoops = new Dictionary<FaceId, List<List<(EdgeId Edge, bool Reversed)>>>();
        foreach (var face in topology.Faces.OrderBy(f => f.Id.Value))
        {
            var ends = plans.Where(p => p.Value.Kind == VertexKind.End && p.Value.Faces[2] == face.Id)
                .Select(p => (Edge: section[(p.Value.Rounded[0], p.Key)], Vertex: p.Key)).ToList();
            var loops = new List<List<(EdgeId Edge, bool Reversed)>>();
            foreach (var loop in oriented[face.Id])
            {
                var mapped = loop.Select(u => (seam.TryGetValue(u.Edge, out var rebuilt) ? rebuilt : keptEdges.TryGetValue(u.Edge, out var k) ? k : along[(u.Edge, face.Id)], u.Reversed)).ToList();
                var touching = ends.Where(x => topology.Edges.Any(e => (e.StartVertexId == x.Vertex || e.EndVertexId == x.Vertex) && loop.Any(u => u.Edge == e.Id))).ToList();
                mapped.AddRange(touching.Select(x => (x.Edge, !stripUse[x.Edge])));
                var chained = Chain(mapped);
                if (chained is null)
                {
                    var walk = string.Join(" ", mapped.Select(u => { var e = builder.Model.GetEdge(u.Item1); return $"{u.Item1.Value}{(u.Item2 ? "-" : "+")}:{e.StartVertexId.Value}>{e.EndVertexId.Value}"; }));
                    return Refuse("uniform-rounding-edge-unsupported", $"a boundary of face {face.Id.Value} does not close after rounding ({walk})");
                }
                loops.Add(chained);
            }
            // Outer first: the loop enclosing the most area (in the plane; a curved face here has one loop).
            if (supports[face.Id].Kind == SurfaceGeometryKind.Plane && loops.Count > 1)
            {
                var plane = supports[face.Id].Surface.Plane!.Value;
                double Enclosed(List<(EdgeId Edge, bool Reversed)> loop) => double.Abs(Area(loop.SelectMany(u => { var pts = built[u.Edge].Points(24).ToList(); if (u.Reversed) pts.Reverse(); return pts.Take(pts.Count - 1); })
                    .Select(p => ((p - plane.Origin).Dot(plane.UAxis.ToVector()), (p - plane.Origin).Dot(plane.VAxis.ToVector()))).ToList()));
                loops = loops.OrderByDescending(Enclosed).ToList();
            }
            originalLoops.Add(face.Id, loops);
            NewFace(loops.Select(l => (IReadOnlyList<(EdgeId, bool)>)l).ToList(), supports[face.Id].Surface, supports[face.Id].Sense > 0);
        }

        // A concave strip faces its own axis: the body's outward normal opposes the tube's.
        foreach (var (s, surface, loop) in stripLoops)
            NewFace([loop], surface, s.Side > 0);

        // Corners: the three sections, each against the direction its strip uses it.
        foreach (var (vertex, plan) in plans.Where(p => p.Value.Kind == VertexKind.Corner).OrderBy(p => p.Key.Value))
        {
            var ordered = Chain(plan.Rounded.Select(e => (section[(e, vertex)], !stripUse[section[(e, vertex)]])).ToList());
            if (ordered is null) return Refuse("uniform-rounding-vertex-unsupported", $"the corner at vertex {vertex.Value} does not close");
            var axis = Unit(plan.Faces.Aggregate(Vector3D.Zero, (n, f) => n + supports[f].Outward(plan.Centre)));
            NewFace([ordered], SurfaceGeometry.FromSphere(new SphereSurface(plan.Centre, Direction3D.Create(axis), radius, Direction3D.Create(Perpendicular(axis)))), plan.Side > 0);
        }

        // Every edge used once each way (a seam twice by one face).
        var wrong = builder.Model.Coedges.GroupBy(c => c.EdgeId).Where(g => g.Count() != 2 || g.First().IsReversed == g.Last().IsReversed).Select(g => g.Key.Value).ToArray();
        if (wrong.Length > 0) return Refuse("uniform-rounding-build-failed", $"the rounded body does not use edges {string.Join(", ", wrong.Take(8))} once each way");

        // Fit: the ball touches nothing beyond its own faces, and planar faces keep their loops simple.
        var refusal = Clearance(boundaries, radius, tol, supports, sharp, plans) ?? Planar(radius, tol, supports, shapes, built, originalLoops);
        if (refusal is not null) return new Outcome { Refusal = refusal };

        builder.AddBody([builder.AddShell(resultFaces)]);
        var result = new BrepBody(builder.Model, geometry, bindings, positions);
        var validation = BrepBindingValidator.Validate(result, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess ? new Outcome { Body = result }
            : Refuse("uniform-rounding-build-failed", string.Join("; ", validation.Diagnostics.Select(d => d.Message)));
    }

    /// <summary>The same line or circle between new ends, still running the original way; null if it folded.</summary>
    private static Shape? Trimmed(Shape shape, Point3D p0, Point3D p1, double tol)
    {
        if (!shape.Arc)
            return (p1 - p0).Dot(shape.End - shape.Start) > tol * (shape.End - shape.Start).Length ? new Shape(p0, p1, null, Vector3D.Zero, 0) : null;
        var c = shape.Center!.Value; var nu = shape.Normal; var centre = c + nu * (p0 - c).Dot(nu);
        var sweep = Angle(p0 - centre, p1 - centre, nu);
        return (p0 - centre).Length > tol && sweep > 1e-9 && double.Abs(sweep - shape.Sweep) < double.Pi / 2 ? new Shape(p0, p1, centre, nu, sweep) : null;
    }

    /// <summary>The ball centre at the given signed inward distances from three faces (negative: outside, across
    /// concave edges; zero: on the face), by Newton on their distance functions (whose gradients are the inward
    /// normals); exact in one step on planes.</summary>
    private static Point3D? Centre(Support[] faces, Point3D vertex, double[] targets, double tol)
    {
        var x = vertex - Unit(faces.Zip(targets).Where(p => p.Second != 0).Aggregate(Vector3D.Zero, (n, p) => n + p.First.Outward(vertex))) * targets[0];
        for (var i = 0; i < 60; i++)
        {
            var rows = faces.Select(f => f.Outward(x) * -1).ToArray();
            var residual = faces.Select((f, k) => f.Inside(x) - targets[k]).ToArray();
            if (residual.All(r => double.Abs(r) <= tol * 1e-3)) return x;
            var det = rows[0].Dot(rows[1].Cross(rows[2]));
            if (double.Abs(det) < 1e-12) return null;
            var step = (rows[1].Cross(rows[2]) * -residual[0] + rows[2].Cross(rows[0]) * -residual[1] + rows[0].Cross(rows[1]) * -residual[2]) * (1 / det);
            x += step;
        }
        return faces.Select((f, k) => double.Abs(f.Inside(x) - targets[k])).All(r => r <= tol) ? x : null;
    }

    /// <summary>Where two strips meet across a tangent edge: the ball touching the tangent faces at one point of
    /// the edge and the common face, found along the edge from the vertex.</summary>
    private static Point3D? Junction(Support common, Support tangent, Point3D vertex, Vector3D along, double target, double tol)
    {
        Point3D Ball(double s) { var q = vertex + along * s; return q - tangent.Outward(q) * target; }
        var t = 0d;
        for (var i = 0; i < 60; i++)
        {
            var f = common.Inside(Ball(t)) - target;
            if (double.Abs(f) <= tol * 1e-3) return Ball(t);
            var h = 1e-6 * (double.Abs(target) + double.Abs(t));
            var slope = (common.Inside(Ball(t + h)) - common.Inside(Ball(t - h))) / (2 * h);
            if (double.Abs(slope) < 1e-12) return null;
            t -= f / slope;
        }
        return null;
    }

    /// <summary>The ball centre at signed inward distance <paramref name="radius"/> from two coaxial faces, in the
    /// half-plane at the meridian.</summary>
    private static Point3D? Meridian(Support a, Support b, Point3D centre, Vector3D axis, Vector3D meridian, Point3D onEdge, double radius, double tol, double rho0)
    {
        var start = centre + meridian * rho0;
        var guess = start - Unit(a.Outward(start) + b.Outward(start)) * radius;
        var (rho, z) = ((guess - centre).Dot(meridian), (guess - centre).Dot(axis));
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
    /// is not one of their own (a strip's own include the face it ends on and the faces it meets tangentially),
    /// wherever that face actually is.</summary>
    private static Refusal? Clearance(Dictionary<FaceId, Shape[]> boundaries, double radius, double tol, Dictionary<FaceId, Support> supports,
        Dictionary<EdgeId, Sharp> sharp, Dictionary<VertexId, Plan> plans)
    {
        IEnumerable<(Point3D Centre, FaceId[] Own, string Where)> Samples()
        {
            foreach (var s in sharp.Values)
            {
                if (s.Closed)
                {
                    var c = s.Shape.Center!.Value;
                    for (var i = 0; i < 24; i++) yield return (Rotate(s.Centre, c, s.Shape.Normal, Turn * i / 24), [s.A.Face, s.B.Face], $"along edge {s.Edge.Id.Value}");
                    continue;
                }
                var (p0, p1) = (plans[s.Edge.StartVertexId], plans[s.Edge.EndVertexId]);
                var own = new[] { s.A.Face, s.B.Face }.Concat(new[] { p0, p1 }.Where(p => p.Kind is VertexKind.End or VertexKind.Junction).SelectMany(p => p.Faces)).Distinct().ToArray();
                var (c0, c1) = (p0.Centre, p1.Centre);
                for (var i = 1; i < 8; i++)
                {
                    var t = i / 8d;
                    yield return (!s.Shape.Arc ? c0 + (c1 - c0) * t : Rotate(c0, s.Shape.Center!.Value, s.Shape.Normal, Angle(c0 - Axial(c0, s.Shape), c1 - Axial(c0, s.Shape), s.Shape.Normal) * t), own, $"along edge {s.Edge.Id.Value}");
                }
            }
            foreach (var (vertex, plan) in plans.Where(p => p.Value.Kind == VertexKind.Corner)) yield return (plan.Centre, plan.Faces, $"at vertex {vertex.Value}");
        }
        foreach (var (centre, own, where) in Samples())
            foreach (var (face, support) in supports)
            {
                if (own.Contains(face)) continue;
                var foot = support.Foot(centre);
                // Touching counts: a ball that touches a third face leaves a face pinched to nothing there.
                if ((foot - centre).Length > radius + tol) continue;
                if (InsideFace(support, boundaries[face], foot))
                    return TooLarge(radius, $"the ball {where} reaches face {face.Value}").Refusal;
            }
        return null;
    }

    /// <summary>Each planar face's new loops keep their winding and do not cross one another.</summary>
    private static Refusal? Planar(double radius, double tol, Dictionary<FaceId, Support> supports,
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
            var total = 0d;
            var segments = new List<((double X, double Y) P, (double X, double Y) Q, EdgeId Edge)>();
            for (var i = 0; i < newLoops.Count; i++)
            {
                var polyline = Polyline(newLoops[i].Select(e => (built[e.Edge], e.Reversed)));
                if (double.Abs(Area(polyline)) <= tol * tol)
                    return TooLarge(radius, $"the rounding shrinks a boundary of face {face.Value} to nothing").Refusal;
                total += Area(polyline);
                foreach (var (edge, reversed) in newLoops[i])
                {
                    var pts = built[edge].Points(24).Select(Flat).ToList();
                    for (var k = 0; k + 1 < pts.Count; k++) segments.Add((pts[k], pts[k + 1], edge));
                }
            }
            // Walked with the interior on the left, the loops enclose a positive area about the outward normal.
            if (total * supports[face].Sense <= tol * tol)
                return TooLarge(radius, $"the rounding turns face {face.Value} inside out").Refusal;
            for (var i = 0; i < segments.Count; i++)
                for (var j = i + 1; j < segments.Count; j++)
                    if (segments[i].Edge != segments[j].Edge && Crosses(segments[i].P, segments[i].Q, segments[j].P, segments[j].Q, tol))
                        return TooLarge(radius, $"the rounding makes the boundaries of face {face.Value} cross").Refusal;
        }
        return null;
    }

    /// <summary>Whether a point is inside the body: the parity of a ray at a generic direction against every face,
    /// each hit on a face's surface counted when it lies inside that face.</summary>
    private static bool Inside(IEnumerable<Support> faces, Dictionary<FaceId, Shape[]> boundaries, Point3D x)
    {
        var d = Unit(new Vector3D(0.5772156649, 0.6180339887, 0.5335));
        var crossings = 0;
        foreach (var face in faces)
            foreach (var t in Hits(face, x, d))
                if (t > 0 && InsideFace(face, boundaries[face.Face], x + d * t)) crossings++;
        return crossings % 2 == 1;
    }

    private static IEnumerable<double> Hits(Support face, Point3D x, Vector3D d)
    {
        switch (face.Kind)
        {
            case SurfaceGeometryKind.Plane:
            {
                var p = face.Surface.Plane!.Value; var n = p.Normal.ToVector(); var den = n.Dot(d);
                if (double.Abs(den) > 1e-15) yield return n.Dot(p.Origin - x) / den;
                yield break;
            }
            case SurfaceGeometryKind.Cylinder:
            {
                var c = face.Surface.Cylinder!.Value; var a = c.Axis.ToVector();
                var (q, e) = (Flatten(x - c.Origin, a), Flatten(d, a));
                foreach (var t in Quadratic(e.Dot(e), 2 * q.Dot(e), q.Dot(q) - c.Radius * c.Radius)) yield return t;
                yield break;
            }
            default:
            {
                var k = face.Surface.Cone!.Value; var a = k.Axis.ToVector(); var tan = double.Tan(k.SemiAngleRadians);
                var (q, e) = (x - k.Apex, d);
                var (qa, ea) = (q.Dot(a), e.Dot(a)); var (qr, er) = (Flatten(q, a), Flatten(e, a));
                foreach (var t in Quadratic(er.Dot(er) - tan * tan * ea * ea, 2 * (qr.Dot(er) - tan * tan * qa * ea), qr.Dot(qr) - tan * tan * qa * qa))
                    if (qa + ea * t >= 0) yield return t;
                yield break;
            }
        }
    }

    private static IEnumerable<double> Quadratic(double a, double b, double c)
    {
        if (double.Abs(a) < 1e-15) { if (double.Abs(b) > 1e-15) yield return -c / b; yield break; }
        var disc = b * b - 4 * a * c;
        if (disc < 0) yield break;
        var root = double.Sqrt(disc);
        yield return (-b - root) / (2 * a); yield return (-b + root) / (2 * a);
    }

    /// <summary>Whether a point on the face's surface lies inside the face, by even-odd crossings against its
    /// boundary: in the plane, of a ray at a generic angle with its lines and arcs, exactly; on a cylinder or cone,
    /// of the axial ray with the circles over the point's angle (generators and seams run along the ray).</summary>
    private static bool InsideFace(Support face, IReadOnlyList<Shape> boundary, Point3D q)
    {
        var crossings = 0;
        if (face.Kind == SurfaceGeometryKind.Plane)
        {
            var plane = face.Surface.Plane!.Value; var n = plane.Normal.ToVector();
            var d = plane.UAxis.ToVector() * double.Cos(0.6180339887) + plane.VAxis.ToVector() * double.Sin(0.6180339887);
            foreach (var shape in boundary)
            {
                if (!shape.Arc)
                {
                    // q + t d = a + s (b - a), t > 0, 0 <= s < 1, within the plane.
                    var e = shape.End - shape.Start; var den = n.Dot(d.Cross(e));
                    if (double.Abs(den) < 1e-15) continue;
                    var tq = n.Dot((shape.Start - q).Cross(e)) / den; var sq = n.Dot((shape.Start - q).Cross(d)) / den;
                    if (tq > 0 && sq >= 0 && sq < 1) crossings++;
                    continue;
                }
                var c = shape.Center!.Value; var r = (shape.Start - c).Length; var f = q - c;
                var (b, k) = (f.Dot(d), f.Dot(f) - r * r); var disc = b * b - k;
                if (disc <= 0) continue;
                foreach (var t in new[] { -b - double.Sqrt(disc), -b + double.Sqrt(disc) })
                    if (t > 0 && (shape.Sweep >= Turn - 1e-12 || Angle(shape.Start - c, q + d * t - c, shape.Normal) <= shape.Sweep)) crossings++;
            }
            return crossings % 2 == 1;
        }
        // A blend this construction made (a sphere corner, a closed rim's torus) is never asked: it only comes back
        // as a face of a second pass, never in the way of a ball there.
        if (face.Kind is not (SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Cone)) return false;
        var (origin, axis) = face.Kind == SurfaceGeometryKind.Cylinder
            ? (face.Surface.Cylinder!.Value.Origin, face.Surface.Cylinder!.Value.Axis.ToVector())
            : (face.Surface.Cone!.Value.Apex, face.Surface.Cone!.Value.Axis.ToVector());
        var height = (q - origin).Dot(axis); var radial = Flatten(q - origin, axis);
        foreach (var shape in boundary.Where(x => x.Arc))
        {
            var c = shape.Center!.Value;
            if ((c - origin).Dot(axis) <= height) continue;
            if (shape.Sweep >= Turn - 1e-12 || Angle(shape.Start - c, radial, shape.Normal) <= shape.Sweep) crossings++;
        }
        return crossings % 2 == 1;
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
