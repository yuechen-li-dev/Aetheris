using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Exact, construction-owned planar support; shared by browser placement and feature admission.</summary>
public sealed record PlanarSupportDisk(double[] Center, double Radius);
public sealed record PlanarFaceSupport(double[] Origin, double[] X, double[] Y, double[] Normal, double[][] Boundary,
    IReadOnlyList<PlanarSupportDisk> ExcludedDisks);
/// <summary>Construction-owned finite cavity envelope; radius bounds its entire recess and shaft.</summary>
public sealed record PlanarHoleCavity(string StableId, Point3D Mouth, Vector3D Axis, double Depth, double Radius, IReadOnlyList<FaceId> WallFaces);

public static class PlanarFaceThroughHole
{
    public const double MinimumClearance = .01;
    private const double Tol = 1e-7;
    /// <summary>Reuse the exact profile plan as a host for the existing side-hole planner.</summary>
    public static PrismaticSectionStackEmissionResult FromProfile(LineArcProfileExtrudeResult emitted, SemanticTopologyCorrespondence correspondence)
    {
        var plan = emitted.BRepPlan ?? throw new ArgumentException("An authoritative profile plan is required.", nameof(emitted));
        var body = emitted.Body!.Clone();
        var mappings = correspondence.Descendants.Where(d => d.Face is not null && d.Role == SemanticTopologyRole.ExtrusionSideFace)
            .Select(d => new PrismaticSectionStackFacePlanMapping(d.Face!.Value, "PrismaticSide", d.SourceStableId, d.StableId, null, null, plan.Provenance)).ToArray();
        var topology = new PrismaticSectionStackTopologyPlan(plan.StableId, body.Topology, body.Geometry, body.Bindings,
            plan.Vertices.ToDictionary(v => v.Id, v => v.WorldPoint), mappings, correspondence, correspondence.ProvenanceChain);
        var host = new PrismaticSectionStackBrepPlan(plan.StableId, plan.Vertices.Count, plan.Edges.Count, plan.Faces.Count,
            "ProfilePlanSideHoleAdapter", true, correspondence, topology);
        return new(body, host, [], correspondence);
    }
    public static PlanarFaceSupport? Support(BrepBody body, FaceId id, double[]? constructionOrigin = null)
    {
        var face = body.Topology.Faces.SingleOrDefault(f => f.Id == id);
        if (face is null || face.LoopIds.Count == 0 || !body.TryGetFaceSurface(id, out var surface) || surface?.Plane is not { } plane
            || !body.Bindings.TryGetFaceBinding(id, out var binding)) return null;
        var normal = plane.Normal.ToVector() * (binding.Orientation.IsAlignedWithSurface ? 1d : -1d);
        // A support frame must survive the plain-Extrude -> Compose lowering.
        // Surface UV axes are emitter details and can swap at that boundary.
        var reference = Math.Abs(normal.X) < .9d ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0);
        var x = Direction3D.Create(reference - normal * reference.Dot(normal)).ToVector(); var y = normal.Cross(x);
        var loop = body.Topology.Loops.Single(l => l.Id == face.LoopIds[0]);
        var points = new List<Point3D>();
        foreach (var cid in loop.CoedgeIds)
        {
            var coedge = body.Topology.Coedges.Single(c => c.Id == cid);
            var edge = body.Topology.Edges.Single(e => e.Id == coedge.EdgeId);
            if (!body.Bindings.TryGetEdgeBinding(edge.Id, out var eb) || !body.Geometry.TryGetCurve(eb.CurveGeometryId, out var curve) || curve?.Kind != CurveGeometryKind.Line3) return null;
            if (!body.TryGetVertexPoint(coedge.IsReversed ? edge.EndVertexId : edge.StartVertexId, out var p)) return null;
            points.Add(p);
        }
        var boundary = points.Select(p => new[] { (p - plane.Origin).Dot(x), (p - plane.Origin).Dot(y) }).ToArray();
        if (boundary.Length < 3) return null;
        var minU = boundary.Min(p => p[0]); var minV = boundary.Min(p => p[1]);
        var origin = plane.Origin + x * minU + y * minV;
        boundary = boundary.Select(p => new[] { p[0] - minU, p[1] - minV }).ToArray();
        if (constructionOrigin is { Length: 3 })
        {
            var anchored = P(constructionOrigin);
            if (Math.Abs((anchored - origin).Dot(normal)) > Tol) return null;
            var offset = origin - anchored;
            boundary = boundary.Select(p => new[] { p[0] + offset.Dot(x), p[1] + offset.Dot(y) }).ToArray();
            origin = anchored;
        }
        var area = boundary.Select((p, i) => p[0] * boundary[(i + 1) % boundary.Length][1] - p[1] * boundary[(i + 1) % boundary.Length][0]).Sum();
        if (area < 0d) Array.Reverse(boundary);
        // Convex supports give exact half-plane clearance; other trims fail closed.
        for (var i = 0; i < boundary.Length; i++)
        {
            var a = boundary[i]; var b = boundary[(i + 1) % boundary.Length];
            if (boundary.Any(p => (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0]) < -Tol)) return null;
        }
        var excluded = new List<PlanarSupportDisk>();
        foreach (var inner in face.LoopIds.Skip(1))
        {
            Circle3Curve? circle = null; var sweep = 0d;
            foreach (var cid in body.Topology.Loops.Single(l => l.Id == inner).CoedgeIds)
            {
                var edge = body.Topology.Coedges.Single(c => c.Id == cid).EdgeId;
                if (!body.Bindings.TryGetEdgeBinding(edge, out var eb) || !body.Geometry.TryGetCurve(eb.CurveGeometryId, out var curve)
                    || curve?.Circle3 is not { } arc || eb.TrimInterval is not { } trim) return null;
                if (Math.Abs(Math.Abs(arc.Normal.ToVector().Dot(normal)) - 1d) > Tol || Math.Abs((arc.Center - origin).Dot(normal)) > Tol) return null;
                if (circle is { } previous && ((arc.Center - previous.Center).Length > Tol || Math.Abs(arc.Radius - previous.Radius) > Tol)) return null;
                circle = arc; sweep += Math.Abs(trim.End - trim.Start);
            }
            if (circle is not { } hole || Math.Abs(sweep - 2 * Math.PI) > Tol) return null;
            var offset = hole.Center - origin;
            excluded.Add(new([offset.Dot(x), offset.Dot(y)], hole.Radius));
        }
        double[] V(Vector3D v) => [v.X, v.Y, v.Z];
        return new([origin.X, origin.Y, origin.Z], V(x), V(y), V(normal), boundary, excluded);
    }

    private static bool Contains(PlanarFaceSupport frame, Point3D center, double radius)
    {
        var relative = center - P(frame.Origin); var u = relative.Dot(V(frame.X)); var v = relative.Dot(V(frame.Y));
        for (var i = 0; i < frame.Boundary.Length; i++)
        {
            var a = frame.Boundary[i]; var b = frame.Boundary[(i + 1) % frame.Boundary.Length]; var dx = b[0] - a[0]; var dy = b[1] - a[1];
            if ((dx * (v - a[1]) - dy * (u - a[0])) / Math.Sqrt(dx * dx + dy * dy) < radius + MinimumClearance - Tol) return false;
        }
        return frame.ExcludedDisks.All(d => Math.Sqrt(Math.Pow(u - d.Center[0], 2) + Math.Pow(v - d.Center[1], 2)) >= radius + d.Radius + MinimumClearance - Tol);
    }
    private static Vector3D V(double[] v) => new(v[0], v[1], v[2]);
    private static Point3D P(double[] v) => new(v[0], v[1], v[2]);
    private static bool EnvelopesSeparated(Point3D a, Vector3D az, double ad, double ar, Point3D b, Vector3D bz, double bd, double br)
    {
        static double C(Point3D p, int i) => i == 0 ? p.X : i == 1 ? p.Y : p.Z;
        static double V(Vector3D p, int i) => i == 0 ? p.X : i == 1 ? p.Y : p.Z;
        return Enumerable.Range(0, 3).Any(i => {
            var ae = ar * Math.Sqrt(Math.Max(0d, 1d - Math.Pow(V(az, i), 2))); var be = br * Math.Sqrt(Math.Max(0d, 1d - Math.Pow(V(bz, i), 2)));
            return Math.Max(C(a, i), C(a + az * ad, i)) + ae + MinimumClearance < Math.Min(C(b, i), C(b + bz * bd, i)) - be
                || Math.Max(C(b, i), C(b + bz * bd, i)) + be + MinimumClearance < Math.Min(C(a, i), C(a + az * ad, i)) - ae;
        });
    }

    /// <summary>Insert a cylindrical cavity between two proven planar supports. No guessed face IDs or Boolean tools.</summary>
    public static PrismaticSectionStackEmissionResult Apply(PrismaticSectionStackEmissionResult emitted, PrismaticFaceHoleFeature hole)
    {
        // Successful planning receipts are not errors when a later feature is refused.
        PrismaticSectionStackEmissionResult Fail(string reason) => new(null, emitted.Plan, [$"compose-face-hole:{hole.Name}:{reason}"]);
        var source = emitted.Plan?.TopologyPlan; var body = emitted.Body;
        if (source is null || body is null) return Fail("missing-host-plan");
        var matches = source.FaceMappings.Where(m => m.SourceStableId == hole.SupportSource && m.Kind is "PrismaticSide" or "HostFaceReplacement").ToArray();
        if (matches.Length != 1) return Fail("support-must-resolve-to-one-construction-face");
        var entryId = matches[0].FaceId;
        var anchor = source.Correspondence.Descendants.FirstOrDefault(d => d.Face == entryId && d.SupportOrigin is not null)?.SupportOrigin;
        var entry = Support(body, entryId, anchor);
        if (entry is null) return Fail("support-requires-a-convex-planar-face-with-straight-boundaries");
        var normal = V(entry.Normal); var axis = -normal; var radius = hole.Diameter / 2d;
        var outerRadius = hole.Variant == "Shaft" ? radius : (hole.RecessDiameter ?? double.NaN) / 2d;
        if (!double.IsFinite(radius) || radius < MinimumClearance || !double.IsFinite(outerRadius)
            || hole.Variant != "Shaft" && outerRadius - radius < MinimumClearance) return Fail("invalid-shaft-or-recess-diameter");
        var mouth = P(entry.Origin) + V(entry.X) * hole.X + V(entry.Y) * hole.Y;
        if (!double.IsFinite(hole.X) || !double.IsFinite(hole.Y) || !Contains(entry, mouth, outerRadius)) return Fail("entry-or-existing-hole-clearance-must-be-at-least-0.01mm");
        var exits = body.Topology.Faces.Where(f => f.Id != entryId).Select(f => (f.Id, Frame: Support(body, f.Id)))
            .Where(f => f.Frame is not null && V(f.Frame.Normal).Dot(normal) < -1d + 1e-7)
            .Select(f => (f.Id, Frame: f.Frame!, Depth: (P(f.Frame!.Origin) - mouth).Dot(axis)))
            .Where(f => f.Depth > MinimumClearance && Contains(f.Frame, mouth + axis * f.Depth, radius)).OrderBy(f => f.Depth).ToArray();
        if (exits.Length == 0) return Fail("no-contained-parallel-exit-support");
        var exit = exits[0]; var end = mouth + axis * exit.Depth;
        var recessDepth = 0d;
        if (hole.Variant == "Counterbore") recessDepth = hole.RecessDepth ?? double.NaN;
        if (hole.Variant == "Countersink")
        {
            var angle = hole.AngleDegrees ?? double.NaN;
            if (!double.IsFinite(angle) || angle <= 0 || angle >= 180) return Fail("countersink-angle-must-be-between-0-and-180deg");
            recessDepth = (outerRadius - radius) / Math.Tan(angle * Math.PI / 360d);
        }
        if (!double.IsFinite(recessDepth) || hole.Variant != "Shaft" && (recessDepth < MinimumClearance || exit.Depth - recessDepth < MinimumClearance))
        {
            var action = hole.Variant == "Counterbore" ? "Reduce CounterboreDepth" : "Reduce CountersinkDiameter or increase CountersinkAngle";
            var message = FormattableString.Invariant($"{hole.Variant} depth is {recessDepth:0.###} mm, but this face has {exit.Depth:0.###} mm of material along the inward cutting direction. Recess depth must be from {MinimumClearance:0.###} to {exit.Depth - MinimumClearance:0.###} mm to retain a through shaft. {action}, or use a plain through hole.");
            return Fail($"recess-depth-must-leave-at-least-0.01mm-of-shaft: {message}");
        }
        var clearedWalls = new HashSet<FaceId>();
        foreach (var cavity in source.FaceHoleCavities)
        {
            var parallel = Math.Abs(axis.Dot(cavity.Axis)) > 1d - Tol;
            var offset = cavity.Mouth - mouth; var along = offset.Dot(axis);
            var radialDistance = (offset - axis * along).Length;
            var oldEnd = along + cavity.Depth * cavity.Axis.Dot(axis);
            var separated = parallel && (Math.Max(along, oldEnd) < -MinimumClearance || Math.Min(along, oldEnd) > exit.Depth + MinimumClearance
                || radialDistance >= outerRadius + cavity.Radius + MinimumClearance - Tol);
            if (!separated && !EnvelopesSeparated(mouth, axis, exit.Depth, outerRadius, cavity.Mouth, cavity.Axis, cavity.Depth, cavity.Radius))
                return Fail("existing-hole-envelope-clearance-must-be-at-least-0.01mm");
            clearedWalls.UnionWith(cavity.WallFaces);
        }
        // Conservative corridor proof. Reject any intervening face whose bounds overlap the finite tool bounds.
        // This also refuses existing cavities or nonplanar interactions instead of silently cutting through them.
        foreach (var face in body.Topology.Faces.Where(f => f.Id != entryId && f.Id != exit.Id && !clearedWalls.Contains(f.Id)))
        {
            var edgeIds = face.LoopIds.SelectMany(l => body.Topology.Loops.Single(t => t.Id == l).CoedgeIds)
                .Select(c => body.Topology.Coedges.Single(t => t.Id == c).EdgeId).Distinct()
                .ToArray();
            var bounds = new List<Point3D>();
            foreach (var id in edgeIds)
            {
                var edge = body.Topology.Edges.Single(t => t.Id == id);
                if (!body.Bindings.TryGetEdgeBinding(id, out var binding) || !body.Geometry.TryGetCurve(binding.CurveGeometryId, out var curve)) return Fail("missing-corridor-edge-geometry");
                bounds.Add(source.VertexPoints[edge.StartVertexId]); bounds.Add(source.VertexPoints[edge.EndVertexId]);
                if (curve?.Circle3 is { } circle)
                {
                    // Bounds belong to the trimmed arc, not its entire supporting circle.
                    var circleX = circle.XAxis.ToVector(); var circleY = circle.YAxis.ToVector();
                    var trim = binding.TrimInterval ?? new ParameterInterval(0, 2 * Math.PI);
                    foreach (var angle in new[] { Math.Atan2(circleY.X, circleX.X), Math.Atan2(circleY.Y, circleX.Y), Math.Atan2(circleY.Z, circleX.Z) })
                        foreach (var extremum in new[] { angle, angle + Math.PI })
                        {
                            var turn = Math.Ceiling((trim.Start - extremum) / (2 * Math.PI));
                            var parameter = extremum + turn * 2 * Math.PI;
                            if (parameter <= trim.End + Tol) bounds.Add(circle.Evaluate(parameter));
                        }
                }
                else if (curve?.Kind != CurveGeometryKind.Line3) return Fail("unsupported-corridor-edge-geometry");
            }
            var separated = Enumerable.Range(0, 3).Any(i => {
                double C(Point3D p) => i == 0 ? p.X : i == 1 ? p.Y : p.Z;
                var n = i == 0 ? normal.X : i == 1 ? normal.Y : normal.Z;
                var extent = outerRadius * Math.Sqrt(Math.Max(0d, 1d - n * n));
                return bounds.Max(C) <= Math.Min(C(mouth), C(end)) - extent - MinimumClearance + Tol
                    || bounds.Min(C) >= Math.Max(C(mouth), C(end)) + extent + MinimumClearance - Tol;
            });
            if (!separated) return Fail("tool-corridor-interaction-not-admitted");
        }
        var excluded = new[] { entryId, exit.Id }; var topology = new TopologyModel(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel();
        foreach (var v in source.Topology.Vertices) topology.AddVertex(v);
        foreach (var e in source.Topology.Edges) topology.AddEdge(e);
        foreach (var c in source.Topology.Coedges) topology.AddCoedge(c);
        foreach (var l in source.Topology.Loops) topology.AddLoop(l);
        foreach (var f in source.Topology.Faces.Where(f => !excluded.Contains(f.Id))) topology.AddFace(f);
        foreach (var c in source.Geometry.Curves) geometry.AddCurve(c.Key, c.Value);
        foreach (var s in source.Geometry.Surfaces) geometry.AddSurface(s.Key, s.Value);
        foreach (var e in source.Bindings.EdgeBindings) bindings.AddEdgeBinding(e);
        foreach (var f in source.Bindings.FaceBindings.Where(f => !excluded.Contains(f.FaceId))) bindings.AddFaceBinding(f);
        var points = source.VertexPoints.ToDictionary(p => p.Key, p => p.Value);
        var vi = topology.Vertices.Max(v => v.Id.Value) + 1; var ei = topology.Edges.Max(e => e.Id.Value) + 1;
        var ci = topology.Coedges.Max(c => c.Id.Value) + 1; var li = topology.Loops.Max(l => l.Id.Value) + 1;
        var fi = source.Topology.Faces.Max(f => f.Id.Value) + 1; var gi = geometry.Curves.Max(c => c.Key.Value) + 1; var si = geometry.Surfaces.Max(s => s.Key.Value) + 1;
        VertexId Vertex(Point3D p) { var id = new VertexId(vi++); topology.AddVertex(new Vertex(id)); points[id] = p; return id; }
        EdgeId Edge(VertexId a, VertexId b, CurveGeometry curve, ParameterInterval trim) { var id = new EdgeId(ei++); var gid = new CurveGeometryId(gi++); topology.AddEdge(new Edge(id, a, b)); geometry.AddCurve(gid, curve); bindings.AddEdgeBinding(new(id, gid, trim, true)); return id; }
        LoopId Loop(params (EdgeId Edge, bool Reverse)[] uses) { var id = new LoopId(li++); var ids = uses.Select(_ => new CoedgeId(ci++)).ToArray(); for (var i = 0; i < ids.Length; i++) topology.AddCoedge(new(ids[i], uses[i].Edge, id, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reverse)); topology.AddLoop(new(id, ids)); return id; }
        FaceId Face(IReadOnlyList<LoopId> loops, SurfaceGeometry surface, bool sense) { var id = new FaceId(fi++); var sid = new SurfaceGeometryId(si++); topology.AddFace(new(id, loops)); geometry.AddSurface(sid, surface); bindings.AddFaceBinding(new(id, sid, sense)); return id; }
        var radial = V(entry.X); var z = Direction3D.Create(normal); var x = Direction3D.Create(radial);
        (VertexId Start, EdgeId A, EdgeId B) Ring(double station, double r)
        {
            var center = mouth + axis * station; var a = Vertex(center + radial * r); var b = Vertex(center - radial * r);
            var circle = CurveGeometry.FromCircle(new Circle3Curve(center, z, r, x));
            return (a, Edge(a, b, circle, new(0, Math.PI)), Edge(b, a, circle, new(Math.PI, 2 * Math.PI)));
        }
        var mouthRing = Ring(0d, outerRadius); var endRing = Ring(exit.Depth, radius);
        var entryLoop = Loop((mouthRing.B, true), (mouthRing.A, true)); var exitLoop = Loop((endRing.A, false), (endRing.B, false));
        FaceId Replace(FaceId id, LoopId loop) { var f = source.Topology.Faces.Single(f => f.Id == id); var binding = source.Bindings.FaceBindings.Single(b => b.FaceId == id); return Face(f.LoopIds.Concat([loop]).ToArray(), source.Geometry.Surfaces.Single(s => s.Key == binding.SurfaceGeometryId).Value, binding.Orientation.IsAlignedWithSurface); }
        var entryFace = Replace(entryId, entryLoop); var exitFace = Replace(exit.Id, exitLoop);
        var cavityFaces = new List<FaceId>(); var cavityDescendants = new List<SemanticTopologyDescendant>();
        void Wall((VertexId Start, EdgeId A, EdgeId B) top, (VertexId Start, EdgeId A, EdgeId B) bottom, SurfaceGeometry surface, SemanticTopologyRole role)
        {
            var delta = points[bottom.Start] - points[top.Start];
            var seam = Edge(top.Start, bottom.Start, CurveGeometry.FromLine(new Line3Curve(points[top.Start], Direction3D.Create(delta))), new(0, delta.Length));
            var loop = Loop((top.A, false), (top.B, false), (seam, false), (bottom.B, true), (bottom.A, true), (seam, true));
            var face = Face([loop], surface, false); cavityFaces.Add(face);
            cavityDescendants.Add(new($"{hole.StableId}:wall:{role}", "Face", role, hole.StableId, Face: face, ParentStableId: hole.StableId));
        }
        var shaftStart = mouthRing; var shaftStation = 0d;
        if (hole.Variant == "Counterbore")
        {
            var recessRing = Ring(recessDepth, outerRadius); shaftStart = Ring(recessDepth, radius); shaftStation = recessDepth;
            Wall(mouthRing, recessRing, SurfaceGeometry.FromCylinder(new CylinderSurface(mouth, z, outerRadius, x)), SemanticTopologyRole.HoleCounterboreWallFace);
            var outerLoop = Loop((recessRing.A, false), (recessRing.B, false)); var innerLoop = Loop((shaftStart.B, true), (shaftStart.A, true));
            var shoulder = Face([outerLoop, innerLoop], SurfaceGeometry.FromPlane(new PlaneSurface(mouth + axis * recessDepth, z, x)), true);
            cavityFaces.Add(shoulder);
            cavityDescendants.Add(new($"{hole.StableId}:shoulder", "Face", SemanticTopologyRole.HoleCounterboreShoulderFace, hole.StableId, Face: shoulder, ParentStableId: hole.StableId));
        }
        if (hole.Variant == "Countersink")
        {
            shaftStart = Ring(recessDepth, radius); shaftStation = recessDepth;
            var halfAngle = hole.AngleDegrees!.Value * Math.PI / 360d;
            var apex = mouth + axis * (outerRadius / Math.Tan(halfAngle));
            Wall(mouthRing, shaftStart, SurfaceGeometry.FromCone(new ConeSurface(apex, z, halfAngle, x)), SemanticTopologyRole.HoleCountersinkWallFace);
        }
        Wall(shaftStart, endRing, SurfaceGeometry.FromCylinder(new CylinderSurface(mouth + axis * shaftStation, z, radius, x)), SemanticTopologyRole.HoleWallFace);
        var shell = source.Topology.Shells.Single(); topology.AddShell(new(shell.Id, shell.FaceIds.Where(f => !excluded.Contains(f)).Concat([entryFace, exitFace]).Concat(cavityFaces).ToArray()));
        var oldBody = source.Topology.Bodies.Single(); topology.AddBody(new(oldBody.Id, oldBody.ShellIds));
        var correspondence = new SemanticTopologyCorrespondence(source.Correspondence.BodyStableId,
            source.Correspondence.Descendants.Select(item => item.Face == entryId ? item with { Face = entryFace } : item.Face == exit.Id ? item with { Face = exitFace } : item).Concat(new SemanticTopologyDescendant[] {
                new($"{hole.StableId}:entry", "Loop", SemanticTopologyRole.HoleEntryLoop, hole.StableId, Loop: entryLoop, ParentStableId: hole.StableId),
                new($"{hole.StableId}:exit", "Loop", SemanticTopologyRole.HoleExitLoop, hole.StableId, Loop: exitLoop, ParentStableId: hole.StableId)
            }).Concat(cavityDescendants).ToArray(), source.Provenance.Concat(["PlanarFaceThroughHole", "PositiveEntryExitClearance", "SeparatedCavityEnvelopes", "ConservativeFiniteCorridor"]).ToArray());
        var mappings = source.FaceMappings.Select(m => m.FaceId == entryId ? m with { FaceId = entryFace, Kind = "HostFaceReplacement" } : m.FaceId == exit.Id ? m with { FaceId = exitFace, Kind = "HostFaceReplacement" } : m).ToArray();
        var plan = new PrismaticSectionStackTopologyPlan(source.StableId, topology, geometry, bindings, points, mappings, correspondence, correspondence.ProvenanceChain)
            { FaceHoleCavities = source.FaceHoleCavities.Append(new(hole.StableId, mouth, axis, exit.Depth, outerRadius, cavityFaces)).ToArray() };
        var materialized = PrismaticSectionStackBrepMaterializer.TryMaterialize(plan);
        if (materialized.Body is null) return Fail(string.Join(";", materialized.Diagnostics));
        return new(materialized.Body, emitted.Plan! with { TopologyPlan = plan, Correspondence = correspondence, Vertices = points.Count, Edges = topology.Edges.Count(), Faces = topology.Faces.Count() }, emitted.Diagnostics, correspondence);
    }
}
