using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Construction AIR normalized from a local Profile plus one immutable material frame.</summary>
public sealed record ProfileExtrusionConstructionAir(
    string StableId, string ProfileStableId, ConstructionPlane Frame,
    double LocalStartDepth, double LocalEndDepth, IReadOnlyList<LineArcProfileLoop2D> Loops,
    IReadOnlyList<string> Provenance);

public enum ProfileExtrusionPlanRole
{
    LocalStartVertex, LocalEndVertex, LocalStartBoundary, LocalEndBoundary, LongitudinalBoundary,
    LocalStartCapLoop, LocalEndCapLoop, SideLoop, LocalStartCapFace, LocalEndCapFace, SideFace,
    OuterBoundary, InnerBoundary, BodyShell, Body
}

public sealed record ProfileExtrusionPlanVertex(string StableId, VertexId Id, Point3D WorldPoint, ProfileExtrusionPlanRole Role, string SourceStableId);
public sealed record ProfileExtrusionPlanCurve(string StableId, CurveGeometryId Id, CurveGeometry Geometry, ParameterInterval Trim, bool OrientedEdgeSense, string SourceStableId);
public sealed record ProfileExtrusionPlanEdge(string StableId, EdgeId Id, VertexId StartVertexId, VertexId EndVertexId, CurveGeometryId CurveId, string SourceStableId, ProfileExtrusionPlanRole Role);
public sealed record ProfileExtrusionPlanDirectedEdgeUse(string StableId, CoedgeId Id, EdgeId EdgeId, bool IsReversed, DirectedEdgeUse Traversal);
public sealed record ProfileExtrusionPlanLoop(string StableId, LoopId Id, IReadOnlyList<ProfileExtrusionPlanDirectedEdgeUse> Uses, string SourceStableId, ProfileExtrusionPlanRole Role);
public sealed record ProfileExtrusionPlanSurface(string StableId, SurfaceGeometryId Id, SurfaceGeometry Geometry, bool SameSense, string SourceStableId, ProfileExtrusionPlanRole Role);
public sealed record ProfileExtrusionPlanFace(string StableId, FaceId Id, IReadOnlyList<LoopId> LoopIds, SurfaceGeometryId SurfaceId, bool SameSense, string SourceStableId, ProfileExtrusionPlanRole Role);
public sealed record ProfileExtrusionPlanShell(string StableId, ShellId Id, IReadOnlyList<FaceId> FaceIds);
public sealed record ProfileExtrusionPlanBody(string StableId, BodyId Id, IReadOnlyList<ShellId> ShellIds);

/// <summary>
/// Immutable exact topology authority for Profile extrusion.  It is deliberately composed of reusable
/// BRep primitives so local-frame Hole X2 can append its own curves, surfaces, loops and descendants
/// without a second topology system.
/// </summary>
public sealed record ProfileExtrusionBRepPlan(
    string StableId,
    ProfileExtrusionConstructionAir Construction,
    IReadOnlyList<ProfileExtrusionPlanVertex> Vertices,
    IReadOnlyList<ProfileExtrusionPlanCurve> Curves,
    IReadOnlyList<ProfileExtrusionPlanEdge> Edges,
    IReadOnlyList<ProfileExtrusionPlanLoop> Loops,
    IReadOnlyList<ProfileExtrusionPlanSurface> Surfaces,
    IReadOnlyList<ProfileExtrusionPlanFace> Faces,
    ProfileExtrusionPlanShell Shell,
    ProfileExtrusionPlanBody Body,
    SemanticTopologyCorrespondence Correspondence,
    IReadOnlyList<string> Provenance,
    IReadOnlyList<string> Diagnostics)
{
    public bool IsAuthoritative => true;
    public int CoedgeCount => Loops.Sum(x => x.Uses.Count);
}

public sealed record ProfileExtrusionPlanResult(bool Succeeded, ProfileExtrusionBRepPlan? Plan, IReadOnlyList<string> Diagnostics);
public sealed record ProfileExtrusionMaterializationResult(bool Succeeded, BrepBody? Body, IReadOnlyList<string> Diagnostics);

public static class ProfileExtrusionBRepPlanner
{
    private const double Tol = 1e-6;

    public static ProfileExtrusionPlanResult TryPlan(LineArcProfileExtrudeRequest request, ResolvedProfile2D? sourceProfile = null)
    {
        var diagnostics = new List<string> { "ProfileExtrusionConstructionAir", "ProfileExtrusionBRepPlan" };
        if (!Validate(request, diagnostics)) return new(false, null, diagnostics);
        var frame = request.ConstructionPlane ?? ConstructionPlane.WorldXY;
        var start = request.LocalStartDepth ?? -request.Height / 2d;
        var end = request.LocalEndDepth ?? request.Height / 2d;
        var sourceId = sourceProfile?.Name ?? "request:profile";
        var air = new ProfileExtrusionConstructionAir(
            $"air:profile-extrusion:{sourceId}:{frame.StableId}", sourceId, frame, start, end, request.Loops,
            ["Profile", "ConstructionPlane:" + frame.StableId, "local-coordinate-construction"]);

        var vertices = new List<ProfileExtrusionPlanVertex>(); var curves = new List<ProfileExtrusionPlanCurve>();
        var edges = new List<ProfileExtrusionPlanEdge>(); var loops = new List<ProfileExtrusionPlanLoop>();
        var surfaces = new List<ProfileExtrusionPlanSurface>(); var faces = new List<ProfileExtrusionPlanFace>();
        var descendants = new List<SemanticTopologyDescendant>();
        var vertexByPoint = new Dictionary<(long X, long Y, bool End), ProfileExtrusionPlanVertex>();
        var verticalByPoint = new Dictionary<(long X, long Y), ProfileExtrusionPlanEdge>();
        var nextVertex = 1; var nextCurve = 1; var nextEdge = 1; var nextCoedge = 1; var nextLoop = 1; var nextSurface = 1; var nextFace = 1;
        (long X, long Y) Key((double X, double Y) p) => ((long)Math.Round(p.X / Tol), (long)Math.Round(p.Y / Tol));
        string LoopSource(int li) => sourceProfile is null ? $"profile:{sourceId}.Loop{li}" : $"profile:{sourceProfile.Name}.{sourceProfile.Loops[li].Name}";
        string SegmentSource(int li, int si) => sourceProfile?.Loops[li].Segments[si].Provenance.StableId ?? $"{LoopSource(li)}.Segment{si}";

        ProfileExtrusionPlanVertex Vertex((double X, double Y) point, bool isEnd, string source)
        {
            var key = (Key(point).X, Key(point).Y, isEnd);
            if (vertexByPoint.TryGetValue(key, out var existing)) return existing;
            var role = isEnd ? ProfileExtrusionPlanRole.LocalEndVertex : ProfileExtrusionPlanRole.LocalStartVertex;
            var created = new ProfileExtrusionPlanVertex($"plan:{source}:vertex:{(isEnd ? "local-end" : "local-start")}", new VertexId(nextVertex++), frame.ToWorld(point, isEnd ? end : start), role, source);
            vertices.Add(created); vertexByPoint.Add(key, created); return created;
        }
        ProfileExtrusionPlanCurve Curve(string stable, CurveGeometry geometry, ParameterInterval trim, string source, bool orientedEdgeSense = true)
        { var created = new ProfileExtrusionPlanCurve(stable, new CurveGeometryId(nextCurve++), geometry, trim, orientedEdgeSense, source); curves.Add(created); return created; }
        ProfileExtrusionPlanEdge Edge(string stable, ProfileExtrusionPlanVertex a, ProfileExtrusionPlanVertex b, ProfileExtrusionPlanCurve curve, string source, ProfileExtrusionPlanRole role)
        { var created = new ProfileExtrusionPlanEdge(stable, new EdgeId(nextEdge++), a.Id, b.Id, curve.Id, source, role); edges.Add(created); return created; }
        ProfileExtrusionPlanEdge Vertical((double X, double Y) point, ProfileExtrusionPlanVertex a, ProfileExtrusionPlanVertex b, string source)
        {
            var key = Key(point); if (verticalByPoint.TryGetValue(key, out var existing)) return existing;
            var curve = Curve($"plan:{source}:curve:longitudinal", CurveGeometry.FromLine(new Line3Curve(a.WorldPoint, Direction3D.Create(b.WorldPoint - a.WorldPoint))), new(0, Math.Abs(end - start)), source);
            var created = Edge($"plan:{source}:edge:longitudinal", a, b, curve, source, ProfileExtrusionPlanRole.LongitudinalBoundary); verticalByPoint.Add(key, created); return created;
        }
        ProfileExtrusionPlanLoop Loop(string stable, IReadOnlyList<(ProfileExtrusionPlanEdge Edge, bool Reverse)> uses, string source, ProfileExtrusionPlanRole role)
        {
            var material = uses.Select((u, index) => new ProfileExtrusionPlanDirectedEdgeUse(
                $"{stable}:use:{index}", new CoedgeId(nextCoedge++), u.Edge.Id, u.Reverse,
                DirectedEdgeUse.Resolve(new Edge(u.Edge.Id, u.Edge.StartVertexId, u.Edge.EndVertexId), u.Reverse))).ToArray();
            var loop = new ProfileExtrusionPlanLoop(stable, new LoopId(nextLoop++), material, source, role); loops.Add(loop); return loop;
        }

        var startCapLoops = new List<ProfileExtrusionPlanLoop>(); var endCapLoops = new List<ProfileExtrusionPlanLoop>();
        var sideSpecs = new List<(ProfileExtrusionPlanLoop Loop, SurfaceGeometry Surface, string SegmentSource, string LoopSource, ProfileExtrusionPlanEdge StartCap, ProfileExtrusionPlanEdge EndCap, ProfileExtrusionPlanEdge StartLongitudinal, ProfileExtrusionPlanEdge EndLongitudinal, bool IsHole)>();
        for (var li = 0; li < request.Loops.Count; li++)
        {
            var loop = request.Loops[li]; var startUses = new List<(ProfileExtrusionPlanEdge, bool)>(); var endUses = new List<(ProfileExtrusionPlanEdge, bool)>();
            for (var si = 0; si < loop.Curves.Count; si++)
            {
                var curve = loop.Curves[si]; var segmentSource = SegmentSource(li, si); var prefix = $"plan:{segmentSource}";
                ProfileExtrusionPlanVertex sv0; ProfileExtrusionPlanVertex sv1; ProfileExtrusionPlanVertex ev0; ProfileExtrusionPlanVertex ev1;
                ProfileExtrusionPlanEdge startEdge; ProfileExtrusionPlanEdge endEdge; ProfileExtrusionPlanEdge startLongitudinal; ProfileExtrusionPlanEdge endLongitudinal; SurfaceGeometry sideSurface;
                switch (curve)
                {
                    case LineArcLineSegment2D line:
                    {
                        sv0 = Vertex(line.Start, false, segmentSource); sv1 = Vertex(line.End, false, segmentSource); ev0 = Vertex(line.Start, true, segmentSource); ev1 = Vertex(line.End, true, segmentSource);
                        startEdge = Edge(prefix + ":edge:local-start", sv0, sv1, Curve(prefix + ":curve:local-start", CurveGeometry.FromLine(new Line3Curve(sv0.WorldPoint, Direction3D.Create(sv1.WorldPoint - sv0.WorldPoint))), new(0, (sv1.WorldPoint - sv0.WorldPoint).Length), segmentSource), segmentSource, ProfileExtrusionPlanRole.LocalStartBoundary);
                        endEdge = Edge(prefix + ":edge:local-end", ev0, ev1, Curve(prefix + ":curve:local-end", CurveGeometry.FromLine(new Line3Curve(ev0.WorldPoint, Direction3D.Create(ev1.WorldPoint - ev0.WorldPoint))), new(0, (ev1.WorldPoint - ev0.WorldPoint).Length), segmentSource), segmentSource, ProfileExtrusionPlanRole.LocalEndBoundary);
                        startLongitudinal = Vertical(line.Start, sv0, ev0, segmentSource); endLongitudinal = Vertical(line.End, sv1, ev1, segmentSource);
                        var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y; var nx = loop.IsHole ? -dy : dy; var ny = loop.IsHole ? dx : -dx;
                        sideSurface = SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld(line.Start, start), Direction3D.Create(frame.ToWorldDirection(new Vector3D(nx, ny, 0))), frame.AxisZ));
                        break;
                    }
                    case LineArcCircularArc2D arc:
                    {
                        var s = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)); var e = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians));
                        sv0 = Vertex(s, false, segmentSource); sv1 = Vertex(e, false, segmentSource); ev0 = Vertex(s, true, segmentSource); ev1 = Vertex(e, true, segmentSource);
                        var rawEnd = arc.StartAngleRadians + arc.SweepAngleRadians; var trim = new ParameterInterval(Math.Min(arc.StartAngleRadians, rawEnd), Math.Max(arc.StartAngleRadians, rawEnd)); var oriented = arc.SweepAngleRadians > 0d;
                        startEdge = Edge(prefix + ":edge:local-start", sv0, sv1, Curve(prefix + ":curve:local-start", CurveGeometry.FromCircle(new Circle3Curve(frame.ToWorld(arc.Center, start), frame.AxisZ, arc.Radius, frame.AxisX)), trim, segmentSource, oriented), segmentSource, ProfileExtrusionPlanRole.LocalStartBoundary);
                        endEdge = Edge(prefix + ":edge:local-end", ev0, ev1, Curve(prefix + ":curve:local-end", CurveGeometry.FromCircle(new Circle3Curve(frame.ToWorld(arc.Center, end), frame.AxisZ, arc.Radius, frame.AxisX)), trim, segmentSource, oriented), segmentSource, ProfileExtrusionPlanRole.LocalEndBoundary);
                        startLongitudinal = Vertical(s, sv0, ev0, segmentSource); endLongitudinal = Vertical(e, sv1, ev1, segmentSource);
                        sideSurface = SurfaceGeometry.FromCylinder(new CylinderSurface(frame.ToWorld(arc.Center, start), frame.AxisZ, arc.Radius, frame.AxisX));
                        break;
                    }
                    case LineArcFullCircle2D circle:
                    {
                        sv0 = Vertex(circle.Center, false, segmentSource); sv1 = sv0; ev0 = Vertex(circle.Center, true, segmentSource); ev1 = ev0;
                        var trim = new ParameterInterval(0, 2 * Math.PI);
                        startEdge = Edge(prefix + ":edge:local-start", sv0, sv0, Curve(prefix + ":curve:local-start", CurveGeometry.FromCircle(new Circle3Curve(sv0.WorldPoint, frame.AxisZ, circle.Radius, frame.AxisX)), trim, segmentSource), segmentSource, ProfileExtrusionPlanRole.LocalStartBoundary);
                        endEdge = Edge(prefix + ":edge:local-end", ev0, ev0, Curve(prefix + ":curve:local-end", CurveGeometry.FromCircle(new Circle3Curve(ev0.WorldPoint, frame.AxisZ, circle.Radius, frame.AxisX)), trim, segmentSource), segmentSource, ProfileExtrusionPlanRole.LocalEndBoundary);
                        startLongitudinal = Vertical(circle.Center, sv0, ev0, segmentSource); endLongitudinal = startLongitudinal;
                        sideSurface = SurfaceGeometry.FromCylinder(new CylinderSurface(sv0.WorldPoint, frame.AxisZ, circle.Radius, frame.AxisX));
                        break;
                    }
                    default: throw new InvalidOperationException("ProfileExtrusionUnsupportedCurve");
                }
                // Loop winding is authored in local Profile coordinates (outer CCW, inner CW).
                // Preserve it here; cap-face orientation is carried by the planned cap plane, not by
                // reversing individual coedges and breaking directed closure.
                startUses.Add((startEdge, false)); endUses.Add((endEdge, false));
                var sideLoop = Loop(prefix + ":loop:side", [(startEdge, false), (endLongitudinal, false), (endEdge, true), (startLongitudinal, true)], segmentSource, ProfileExtrusionPlanRole.SideLoop);
                sideSpecs.Add((sideLoop, sideSurface, segmentSource, LoopSource(li), startEdge, endEdge, startLongitudinal, endLongitudinal, loop.IsHole));
            }
            var loopSource = LoopSource(li); var roleStart = ProfileExtrusionPlanRole.LocalStartCapLoop; var roleEnd = ProfileExtrusionPlanRole.LocalEndCapLoop;
            startCapLoops.Add(Loop($"plan:{loopSource}:loop:local-start", startUses, loopSource, roleStart)); endCapLoops.Add(Loop($"plan:{loopSource}:loop:local-end", endUses, loopSource, roleEnd));
        }

        var startSurface = new ProfileExtrusionPlanSurface("plan:cap:local-start:surface", new SurfaceGeometryId(nextSurface++), SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld((0, 0), start), Direction3D.Create(-frame.AxisZ.ToVector()), frame.AxisX)), true, sourceId, ProfileExtrusionPlanRole.LocalStartCapFace);
        var endSurface = new ProfileExtrusionPlanSurface("plan:cap:local-end:surface", new SurfaceGeometryId(nextSurface++), SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld((0, 0), end), frame.AxisZ, frame.AxisX)), true, sourceId, ProfileExtrusionPlanRole.LocalEndCapFace);
        surfaces.Add(startSurface); surfaces.Add(endSurface);
        var startFace = new ProfileExtrusionPlanFace("plan:cap:local-start:face", new FaceId(nextFace++), startCapLoops.Select(x => x.Id).ToArray(), startSurface.Id, true, sourceId, ProfileExtrusionPlanRole.LocalStartCapFace);
        var endFace = new ProfileExtrusionPlanFace("plan:cap:local-end:face", new FaceId(nextFace++), endCapLoops.Select(x => x.Id).ToArray(), endSurface.Id, true, sourceId, ProfileExtrusionPlanRole.LocalEndCapFace);
        faces.Add(startFace); faces.Add(endFace);
        foreach (var (sideLoop, surface, segmentSource, loopSource, startEdge, endEdge, startLongitudinal, endLongitudinal, isHole) in sideSpecs)
        {
            // A cylindrical inner loop bounds void material: retain its analytic
            // outward support normal but reverse the face sense, so shell/mass
            // orientation subtracts rather than adds the circular shaft.
            var sideSameSense = !isHole;
            var plannedSurface = new ProfileExtrusionPlanSurface($"plan:{segmentSource}:surface:side", new SurfaceGeometryId(nextSurface++), surface, sideSameSense, segmentSource, ProfileExtrusionPlanRole.SideFace); surfaces.Add(plannedSurface);
            var sideFace = new ProfileExtrusionPlanFace($"plan:{segmentSource}:face:side", new FaceId(nextFace++), [sideLoop.Id], plannedSurface.Id, sideSameSense, segmentSource, ProfileExtrusionPlanRole.SideFace); faces.Add(sideFace);
            if (sourceProfile is not null)
            {
                var prefix = $"material:{sourceProfile.Name}:{segmentSource}";
                descendants.Add(new(prefix + ":local-start", "Edge", SemanticTopologyRole.LocalStartBoundary, segmentSource, Edge: startEdge.Id, ParentStableId: segmentSource));
                descendants.Add(new(prefix + ":local-end", "Edge", SemanticTopologyRole.LocalEndBoundary, segmentSource, Edge: endEdge.Id, ParentStableId: segmentSource));
                descendants.Add(new(prefix + ":bottom", "Edge", SemanticTopologyRole.BottomBoundary, segmentSource, Edge: startEdge.Id, ParentStableId: segmentSource));
                descendants.Add(new(prefix + ":top", "Edge", SemanticTopologyRole.TopBoundary, segmentSource, Edge: endEdge.Id, ParentStableId: segmentSource));
                descendants.Add(new(prefix + ":vertical-start", "Edge", SemanticTopologyRole.VerticalExtrusionEdge, segmentSource, Edge: startLongitudinal.Id, ParentStableId: segmentSource));
                descendants.Add(new(prefix + ":vertical-end", "Edge", SemanticTopologyRole.VerticalExtrusionEdge, segmentSource, Edge: endLongitudinal.Id, ParentStableId: segmentSource));
                descendants.Add(new(prefix + ":side", "Face", isHole && surface.Kind == SurfaceGeometryKind.Cylinder ? SemanticTopologyRole.HoleWallFace : SemanticTopologyRole.ExtrusionSideFace, segmentSource, Face: sideFace.Id, ParentStableId: segmentSource));
                if (isHole && surface.Kind == SurfaceGeometryKind.Cylinder)
                    descendants.Add(new($"material:{sourceProfile.Name}:{loopSource}:cylindrical-wall:{sideFace.Id.Value}", "Face", SemanticTopologyRole.HoleWallFace, loopSource, Face: sideFace.Id, ParentStableId: loopSource));
            }
        }
        if (sourceProfile is not null)
            for (var li = 0; li < sourceProfile.Loops.Count; li++)
            {
                var loopSource = LoopSource(li); var prefix = $"material:{sourceProfile.Name}:{loopSource}";
                descendants.Add(new(prefix + ":local-start-loop", "Loop", SemanticTopologyRole.LocalStartCapLoop, loopSource, Loop: startCapLoops[li].Id, ParentStableId: loopSource));
                descendants.Add(new(prefix + ":local-end-loop", "Loop", SemanticTopologyRole.LocalEndCapLoop, loopSource, Loop: endCapLoops[li].Id, ParentStableId: loopSource));
                descendants.Add(new(prefix + ":bottom-loop", "Loop", SemanticTopologyRole.BottomFaceBoundaryLoop, loopSource, Loop: startCapLoops[li].Id, ParentStableId: loopSource));
                descendants.Add(new(prefix + ":top-loop", "Loop", SemanticTopologyRole.TopFaceBoundaryLoop, loopSource, Loop: endCapLoops[li].Id, ParentStableId: loopSource));
            }
        var shell = new ProfileExtrusionPlanShell("plan:shell:0", new ShellId(1), faces.Select(x => x.Id).ToArray()); var body = new ProfileExtrusionPlanBody("plan:body:0", new BodyId(1), [shell.Id]);
        var correspondence = new SemanticTopologyCorrespondence(sourceId, descendants, ["ConceptPlane:" + frame.SourceConceptId, "ConstructionPlane:" + frame.StableId, "Profile", "ResolvedProfile2D", "ProfileExtrusionConstructionAir", "ProfileExtrusionBRepPlan", "AuthoritativeBRepPlan"]);
        diagnostics.AddRange(["ProfileExtrusionPlanAuthoritative", "ProfileExtrusionTopologyPlanned", "v2-v4-no-3d-boolean-used"]);
        return new(true, new ProfileExtrusionBRepPlan($"brep-plan:profile-extrusion:{sourceId}:{frame.StableId}", air, vertices, curves, edges, loops, surfaces, faces, shell, body, correspondence, correspondence.ProvenanceChain, diagnostics), diagnostics);
    }

    private static bool Validate(LineArcProfileExtrudeRequest req, List<string> d)
    {
        var start = req.LocalStartDepth ?? -req.Height / 2d; var end = req.LocalEndDepth ?? req.Height / 2d;
        if (!double.IsFinite(req.Height) || req.Height <= Tol) { d.Add("ProfileExtrusionPlanInvalid: invalid height"); return false; }
        if (!double.IsFinite(start) || !double.IsFinite(end) || end - start <= Tol) { d.Add("ProfileExtrusionPlanInvalid: local depth interval"); return false; }
        if (req.Loops.Count == 0 || req.Loops.Count(l => !l.IsHole) != 1) { d.Add("ProfileExtrusionPlanInvalid: exactly one outer loop"); return false; }
        var frame = req.ConstructionPlane ?? ConstructionPlane.WorldXY;
        if (Math.Abs(frame.Determinant - 1d) > 1e-10) { d.Add("ProfileExtrusionFrameInvalid: construction plane must be right handed"); return false; }
        foreach (var loop in req.Loops)
        {
            if (loop.Curves.Count == 0) { d.Add("ProfileExtrusionLoopOpen: empty loop"); return false; }
            foreach (var curve in loop.Curves)
                switch (curve)
                {
                    case LineArcLineSegment2D line when Distance(line.Start, line.End) <= Tol: d.Add("ProfileExtrusionPlanInvalid: zero length line"); return false;
                    case LineArcCircularArc2D arc when !double.IsFinite(arc.Radius) || arc.Radius <= Tol || Math.Abs(arc.SweepAngleRadians) <= Tol: d.Add("ProfileExtrusionUnsupportedCurve: invalid arc"); return false;
                    case LineArcFullCircle2D circle when !double.IsFinite(circle.Radius) || circle.Radius <= Tol: d.Add("ProfileExtrusionUnsupportedCurve: invalid circle"); return false;
                    case not (LineArcLineSegment2D or LineArcCircularArc2D or LineArcFullCircle2D): d.Add("ProfileExtrusionUnsupportedCurve"); return false;
                }
        }
        return true;
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}

/// <summary>Strict plan consumer. It has no geometric matching, adjacency inference, or semantic planning branch.</summary>
public static class ProfileExtrusionBRepMaterializer
{
    public static ProfileExtrusionMaterializationResult TryMaterialize(ProfileExtrusionBRepPlan plan)
    {
        var diagnostics = new List<string> { "ProfileExtrusionPlanMaterializationStarted" };
        if (!Validate(plan, diagnostics)) return new(false, null, diagnostics);
        try
        {
            var builder = new TopologyBuilder(); var points = new Dictionary<VertexId, Point3D>();
            foreach (var v in plan.Vertices.OrderBy(x => x.Id.Value)) { var actual = builder.AddVertex(); if (actual != v.Id) return Diverged(); points.Add(v.Id, v.WorldPoint); }
            foreach (var e in plan.Edges.OrderBy(x => x.Id.Value)) { var actual = builder.AddEdge(e.StartVertexId, e.EndVertexId); if (actual != e.Id) return Diverged(); }
            foreach (var l in plan.Loops.OrderBy(x => x.Id.Value))
            {
                if (builder.AllocateLoopId() != l.Id) return Diverged();
                var ids = l.Uses.OrderBy(x => x.Id.Value).Select(x => x.Id).ToArray();
                for (var i = 0; i < l.Uses.Count; i++) { var use = l.Uses[i]; builder.AddCoedge(new Coedge(use.Id, use.EdgeId, l.Id, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], use.IsReversed)); }
                builder.AddLoop(new Loop(l.Id, ids));
            }
            foreach (var f in plan.Faces.OrderBy(x => x.Id.Value)) { var actual = builder.AddFace(f.LoopIds); if (actual != f.Id) return Diverged(); }
            if (builder.AddShell(plan.Shell.FaceIds) != plan.Shell.Id || builder.AddBody(plan.Body.ShellIds) != plan.Body.Id) return Diverged();
            var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel();
            foreach (var c in plan.Curves.OrderBy(x => x.Id.Value)) geometry.AddCurve(c.Id, c.Geometry);
            foreach (var e in plan.Edges) { var curve = plan.Curves.Single(x => x.Id == e.CurveId); bindings.AddEdgeBinding(new EdgeGeometryBinding(e.Id, e.CurveId, curve.Trim, curve.OrientedEdgeSense)); }
            foreach (var s in plan.Surfaces.OrderBy(x => x.Id.Value)) geometry.AddSurface(s.Id, s.Geometry);
            foreach (var f in plan.Faces) bindings.AddFaceBinding(new FaceGeometryBinding(f.Id, f.SurfaceId, f.SameSense));
            diagnostics.Add("ProfileExtrusionPlanMaterialized");
            return new(true, new BrepBody(builder.Model, geometry, bindings, points), diagnostics);
        }
        catch (Exception ex) { diagnostics.Add("ProfileExtrusionMaterializerDiverged: " + ex.Message); return new(false, null, diagnostics); }

        ProfileExtrusionMaterializationResult Diverged() { diagnostics.Add("ProfileExtrusionMaterializerDiverged: planned identity did not match materialized identity"); return new(false, null, diagnostics); }
    }

    private static bool Validate(ProfileExtrusionBRepPlan plan, List<string> d)
    {
        if (!plan.IsAuthoritative || plan.Vertices.Count == 0 || plan.Edges.Count == 0 || plan.Faces.Count < 3) { d.Add("ProfileExtrusionPlanInvalid: incomplete topology"); return false; }
        foreach (var loop in plan.Loops)
        {
            if (loop.Uses.Count == 0) { d.Add("ProfileExtrusionLoopOpen: empty planned loop"); return false; }
            for (var i = 0; i < loop.Uses.Count; i++)
            {
                var use = loop.Uses[i]; var next = loop.Uses[(i + 1) % loop.Uses.Count];
                if (use.Traversal.EndVertexId != next.Traversal.StartVertexId) { d.Add($"ProfileExtrusionLoopOrientationInvalid: {loop.StableId}"); return false; }
            }
        }
        if (plan.Faces.Any(f => !plan.Surfaces.Any(s => s.Id == f.SurfaceId))) { d.Add("ProfileExtrusionSurfaceBindingInvalid"); return false; }
        return true;
    }
}
