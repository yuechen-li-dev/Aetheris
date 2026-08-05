using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

public abstract record LineArcProfileCurve2D;
public sealed record LineArcLineSegment2D((double X, double Y) Start, (double X, double Y) End) : LineArcProfileCurve2D;
public sealed record LineArcCircularArc2D((double X, double Y) Center, double Radius, double StartAngleRadians, double SweepAngleRadians) : LineArcProfileCurve2D;
public sealed record LineArcFullCircle2D((double X, double Y) Center, double Radius) : LineArcProfileCurve2D;
public sealed record LineArcProfileLoop2D(IReadOnlyList<LineArcProfileCurve2D> Curves, bool IsHole);
/// <summary>Local 2D boundary plus a traced immutable material frame. Height remains a local +Z distance.</summary>
public sealed record LineArcProfileExtrudeRequest(IReadOnlyList<LineArcProfileLoop2D> Loops, double Height, ConstructionPlane? ConstructionPlane = null);
public enum LineArcProfileExtrudeStatus { Succeeded, Rejected, Deferred, Failed }
public sealed record LineArcProfileExtrudeResult(LineArcProfileExtrudeStatus Status, BrepBody? Body, IReadOnlyList<string> Diagnostics, SemanticTopologyCorrespondence? Correspondence = null);

public static class LineArcProfileExtrudeEmitter
{
    private const double Tol = 1e-6;

    public static LineArcProfileExtrudeResult TryEmit(LineArcProfileExtrudeRequest request)
    {
        var d = new List<string> { "v2-v4-line-arc-profile-extrude-attempted" };
        if (!Validate(request, d)) return new(LineArcProfileExtrudeStatus.Rejected, null, d);
        d.Add("v2-v4-profile-validated");
        var ok = TryBuild(request, null, d, out var body, out var correspondence);
        if (!ok || body is null) return new(LineArcProfileExtrudeStatus.Failed, null, d.Append("v2-v4-line-arc-profile-extrude-rejected:emitter validation failed").ToArray());
        d.Add("v2-v4-cap-faces-emitted");
        d.Add("v2-v4-no-3d-boolean-used");
        d.Add("v2-v4-line-arc-profile-extrude-succeeded");
        return new(LineArcProfileExtrudeStatus.Succeeded, body, d.Distinct().OrderBy(x => x).ToArray(), correspondence);
    }

    /// <summary>Materializes a resolved authored Profile and retains direct plan-to-topology correspondence.</summary>
    public static LineArcProfileExtrudeResult TryEmit(ResolvedProfile2D profile, double height)
    {
        var validation = ResolvedProfile2DValidator.Validate(profile);
        if (!validation.IsValid) return new(LineArcProfileExtrudeStatus.Rejected, null, validation.Diagnostics);
        var request = new LineArcProfileExtrudeRequest(profile.Loops.Select(l => new LineArcProfileLoop2D(l.Segments.Select(s => s.Geometry).ToArray(), !l.IsOuter)).ToArray(), height, profile.EffectiveConstructionPlane);
        var diagnostics = new List<string> { "v2-v4-line-arc-profile-extrude-attempted", "semantic-selection-correspondence-requested" };
        if (!Validate(request, diagnostics)) return new(LineArcProfileExtrudeStatus.Rejected, null, diagnostics);
        var ok = TryBuild(request, profile, diagnostics, out var body, out var correspondence);
        return ok && body is not null
            ? new(LineArcProfileExtrudeStatus.Succeeded, body, diagnostics.Distinct().OrderBy(x => x).ToArray(), correspondence)
            : new(LineArcProfileExtrudeStatus.Failed, null, diagnostics.Append("v2-v4-line-arc-profile-extrude-rejected:emitter validation failed").ToArray());
    }

    private static bool Validate(LineArcProfileExtrudeRequest req, List<string> d)
    {
        if (!double.IsFinite(req.Height) || req.Height <= Tol) { d.Add("v2-v4-line-arc-profile-extrude-rejected:invalid height"); return false; }
        if (req.Loops.Count == 0 || req.Loops.Count(l => !l.IsHole) != 1) { d.Add("v2-v4-line-arc-profile-extrude-rejected:unsupported topology"); return false; }
        foreach (var loop in req.Loops)
        {
            if (loop.Curves.Count == 0) { d.Add("v2-v4-line-arc-profile-extrude-rejected:invalid profile"); return false; }
            foreach (var c in loop.Curves)
            {
                switch (c)
                {
                    case LineArcLineSegment2D s when Math.Sqrt(Math.Pow(s.End.X-s.Start.X,2)+Math.Pow(s.End.Y-s.Start.Y,2)) <= Tol: d.Add("v2-v4-line-arc-profile-extrude-rejected:invalid profile"); return false;
                    case LineArcCircularArc2D a when !double.IsFinite(a.Radius) || a.Radius <= Tol || Math.Abs(a.SweepAngleRadians) <= Tol: d.Add("v2-v4-line-arc-profile-extrude-rejected:unsupported curve"); return false;
                    case LineArcFullCircle2D fc when !double.IsFinite(fc.Radius) || fc.Radius <= Tol: d.Add("v2-v4-line-arc-profile-extrude-rejected:unsupported curve"); return false;
                }
            }
        }
        return true;
    }

    private static bool TryBuild(LineArcProfileExtrudeRequest profile, ResolvedProfile2D? sourceProfile, List<string> d, out BrepBody? body, out SemanticTopologyCorrespondence? correspondence)
    {
        body = null; correspondence = null;
        var h = profile.Height;
        var frame = profile.ConstructionPlane ?? ConstructionPlane.WorldXY;
        var b = new TopologyBuilder();
        var z0 = -h / 2d; var z1 = h / 2d;
        var bottomLoops = new List<LoopId>(); var topLoops = new List<LoopId>();
        var sideFaces = new List<(LoopId Loop, SurfaceGeometry Surface, string Diag, string? Source, EdgeId Bottom, EdgeId Top, EdgeId StartVertical, EdgeId EndVertical)>();
        var descendants = new List<SemanticTopologyDescendant>();
        var points = new Dictionary<VertexId, Point3D>();
        var edgeCurves = new Dictionary<EdgeId, CurveGeometry>();
        // A profile loop owns its vertices.  Adjacent curves that meet at the same
        // resolved point must reuse the same top/bottom vertices and vertical edge;
        // geometric coincidence is not an authoritative topology connection.
        var bottomVertices = new Dictionary<(long X, long Y), VertexId>();
        var topVertices = new Dictionary<(long X, long Y), VertexId>();
        var verticalEdges = new Dictionary<(long X, long Y), EdgeId>();
        (long X, long Y) Key((double X, double Y) p) => ((long)Math.Round(p.X / Tol), (long)Math.Round(p.Y / Tol));
        VertexId Vertex((double X, double Y) p, double z, Dictionary<(long X, long Y), VertexId> cache)
        {
            var key = Key(p);
            if (cache.TryGetValue(key, out var existing)) return existing;
            var created = b.AddVertex(); points[created] = frame.ToWorld(p, z); cache.Add(key, created); return created;
        }
        EdgeId Vertical((double X, double Y) p, VertexId bottom, VertexId top)
        {
            var key = Key(p);
            if (verticalEdges.TryGetValue(key, out var existing)) return existing;
            var created = b.AddEdge(bottom, top);
            edgeCurves[created] = CurveGeometry.FromLine(new Line3Curve(points[bottom], Direction3D.Create(points[top] - points[bottom])));
            verticalEdges.Add(key, created); return created;
        }

        for (var loopIndex = 0; loopIndex < profile.Loops.Count; loopIndex++)
        {
            var loop = profile.Loops[loopIndex];
            var bottomUses = new List<Use>(); var topUses = new List<Use>();
            for (var curveIndex = 0; curveIndex < loop.Curves.Count; curveIndex++)
            {
                var curve = loop.Curves[curveIndex];
                var source = sourceProfile?.Loops[loopIndex].Segments[curveIndex].Provenance.StableId;
                switch (curve)
                {
                    case LineArcLineSegment2D line:
                    {
                        var vb0 = Vertex(line.Start, z0, bottomVertices); var vb1 = Vertex(line.End, z0, bottomVertices); var vt0 = Vertex(line.Start, z1, topVertices); var vt1 = Vertex(line.End, z1, topVertices);
                        var eb = b.AddEdge(vb0, vb1); var et = b.AddEdge(vt0, vt1); var es0 = Vertical(line.Start, vb0, vt0); var es1 = Vertical(line.End, vb1, vt1);
                        edgeCurves[eb] = CurveGeometry.FromLine(new Line3Curve(points[vb0], Direction3D.Create(points[vb1] - points[vb0])));
                        edgeCurves[et] = CurveGeometry.FromLine(new Line3Curve(points[vt0], Direction3D.Create(points[vt1] - points[vt0])));
                        bottomUses.Add(loop.IsHole ? Use.R(eb) : Use.F(eb)); topUses.Add(loop.IsHole ? Use.R(et) : Use.F(et));
                        var loopId = AddLoop(b, [Use.F(eb), Use.F(es1), Use.R(et), Use.R(es0)]);
                        var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y; var nx = loop.IsHole ? -dy : dy; var ny = loop.IsHole ? dx : -dx;
                        sideFaces.Add((loopId, SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld(line.Start, z0), Direction3D.Create(frame.ToWorldDirection(new Vector3D(nx, ny, 0))), frame.AxisZ)), "v2-v4-line-edge-side-face-emitted", source, eb, et, es0, es1));
                        break;
                    }
                    case LineArcCircularArc2D arc:
                    {
                        var s = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians));
                        var e = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians));
                        var vb0=Vertex(s,z0,bottomVertices);var vb1=Vertex(e,z0,bottomVertices);var vt0=Vertex(s,z1,topVertices);var vt1=Vertex(e,z1,topVertices);
                        var eb=b.AddEdge(vb0,vb1); var et=b.AddEdge(vt0,vt1); var es0=Vertical(s,vb0,vt0); var es1=Vertical(e,vb1,vt1);
                        edgeCurves[eb]=CurveGeometry.FromCircle(new Circle3Curve(frame.ToWorld(arc.Center,z0),frame.AxisZ,arc.Radius,frame.AxisX));
                        edgeCurves[et]=CurveGeometry.FromCircle(new Circle3Curve(frame.ToWorld(arc.Center,z1),frame.AxisZ,arc.Radius,frame.AxisX));
                        bottomUses.Add(loop.IsHole ? Use.R(eb) : Use.F(eb)); topUses.Add(loop.IsHole ? Use.R(et) : Use.F(et));
                        sideFaces.Add((AddLoop(b,[Use.F(eb),Use.F(es1),Use.R(et),Use.R(es0)]), SurfaceGeometry.FromCylinder(new CylinderSurface(frame.ToWorld(arc.Center,z0),frame.AxisZ,arc.Radius,frame.AxisX)), "v2-v4-arc-edge-side-face-emitted", source, eb, et, es0, es1));
                        break;
                    }
                    case LineArcFullCircle2D circle:
                    {
                        var vb=b.AddVertex(); var vt=b.AddVertex(); points[vb]=frame.ToWorld(circle.Center,z0); points[vt]=frame.ToWorld(circle.Center,z1);
                        var eb=b.AddEdge(vb,vb); var et=b.AddEdge(vt,vt); var es=b.AddEdge(vb,vt);
                        edgeCurves[eb]=CurveGeometry.FromCircle(new Circle3Curve(points[vb],frame.AxisZ,circle.Radius,frame.AxisX));
                        edgeCurves[et]=CurveGeometry.FromCircle(new Circle3Curve(points[vt],frame.AxisZ,circle.Radius,frame.AxisX));
                        edgeCurves[es]=CurveGeometry.FromLine(new Line3Curve(points[vb],Direction3D.Create(points[vt]-points[vb])));
                        bottomUses.Add(loop.IsHole ? Use.R(eb) : Use.F(eb)); topUses.Add(loop.IsHole ? Use.R(et) : Use.F(et));
                        sideFaces.Add((AddLoop(b,[Use.F(eb),Use.F(es),Use.R(et),Use.R(es)]), SurfaceGeometry.FromCylinder(new CylinderSurface(points[vb],frame.AxisZ,circle.Radius,frame.AxisX)), "v2-v4-full-circle-side-face-emitted", source, eb, et, es, es));
                        break;
                    }
                }
            }
            bottomLoops.Add(AddLoop(b, bottomUses));
            topLoops.Add(AddLoop(b, topUses));
        }

        var bottomFace = b.AddFace(bottomLoops); var topFace = b.AddFace(topLoops); var faceIds = new List<FaceId> { bottomFace, topFace };
        foreach (var sf in sideFaces) { faceIds.Add(b.AddFace([sf.Loop])); d.Add(sf.Diag); }
        var shell = b.AddShell(faceIds); b.AddBody([shell]);
        var g = new BrepGeometryStore(); var bind = new BrepBindingModel(); var cid = 1;
        foreach (var e in b.Model.Edges.OrderBy(x => x.Id.Value)) { g.AddCurve(new CurveGeometryId(cid), edgeCurves[e.Id]); bind.AddEdgeBinding(new EdgeGeometryBinding(e.Id, new CurveGeometryId(cid), new ParameterInterval(0, 2 * Math.PI))); cid++; }
        g.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld((0,0),z0), Direction3D.Create(-frame.AxisZ.ToVector()), frame.AxisX)));
        g.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld((0,0),z1), frame.AxisZ, frame.AxisX)));
        bind.AddFaceBinding(new FaceGeometryBinding(bottomFace, new SurfaceGeometryId(1))); bind.AddFaceBinding(new FaceGeometryBinding(topFace, new SurfaceGeometryId(2)));
        var sid = 3; for (var i = 0; i < sideFaces.Count; i++) { g.AddSurface(new SurfaceGeometryId(sid), sideFaces[i].Surface); bind.AddFaceBinding(new FaceGeometryBinding(faceIds[2+i], new SurfaceGeometryId(sid))); sid++; }
        body = new BrepBody(b.Model, g, bind, points);
        if (sourceProfile is not null)
        {
            for (var i = 0; i < sideFaces.Count; i++)
            {
                var side = sideFaces[i]; if (side.Source is null) continue;
                var prefix = $"material:{sourceProfile.Name}:{side.Source}";
                descendants.Add(new($"{prefix}:local-start", "Edge", SemanticTopologyRole.LocalStartBoundary, side.Source, Edge: side.Bottom, ParentStableId: side.Source));
                descendants.Add(new($"{prefix}:local-end", "Edge", SemanticTopologyRole.LocalEndBoundary, side.Source, Edge: side.Top, ParentStableId: side.Source));
                // Compatibility aliases: Top/Bottom mean local +Z/-Z, never world Z.
                descendants.Add(new($"{prefix}:bottom", "Edge", SemanticTopologyRole.BottomBoundary, side.Source, Edge: side.Bottom, ParentStableId: side.Source));
                descendants.Add(new($"{prefix}:top", "Edge", SemanticTopologyRole.TopBoundary, side.Source, Edge: side.Top, ParentStableId: side.Source));
                descendants.Add(new($"{prefix}:vertical-start", "Edge", SemanticTopologyRole.VerticalExtrusionEdge, side.Source, Edge: side.StartVertical, ParentStableId: side.Source));
                descendants.Add(new($"{prefix}:vertical-end", "Edge", SemanticTopologyRole.VerticalExtrusionEdge, side.Source, Edge: side.EndVertical, ParentStableId: side.Source));
                descendants.Add(new($"{prefix}:side", "Face", SemanticTopologyRole.ExtrusionSideFace, side.Source, Face: faceIds[2 + i], ParentStableId: side.Source));
            }
            for (var i = 0; i < sourceProfile.Loops.Count; i++)
            {
                var loopSource = $"profile:{sourceProfile.Name}.{sourceProfile.Loops[i].Name}";
                descendants.Add(new($"material:{sourceProfile.Name}:{loopSource}:local-start-loop", "Loop", SemanticTopologyRole.LocalStartCapLoop, loopSource, Loop: bottomLoops[i], ParentStableId: loopSource));
                descendants.Add(new($"material:{sourceProfile.Name}:{loopSource}:local-end-loop", "Loop", SemanticTopologyRole.LocalEndCapLoop, loopSource, Loop: topLoops[i], ParentStableId: loopSource));
                descendants.Add(new($"material:{sourceProfile.Name}:{loopSource}:bottom-loop", "Loop", SemanticTopologyRole.BottomFaceBoundaryLoop, loopSource, Loop: bottomLoops[i], ParentStableId: loopSource));
                descendants.Add(new($"material:{sourceProfile.Name}:{loopSource}:top-loop", "Loop", SemanticTopologyRole.TopFaceBoundaryLoop, loopSource, Loop: topLoops[i], ParentStableId: loopSource));
            }
            correspondence = new(sourceProfile.Name, descendants, ["ConceptPlane:" + frame.SourceConceptId, "ConstructionPlane:" + frame.StableId, "Profile", "ResolvedProfile2D", "LineArcProfileExtrude", "AuthoritativeBRepPlan"]);
        }
        return true;
    }

    private static LoopId AddLoop(TopologyBuilder b, IReadOnlyList<Use> uses){ var lid=b.AllocateLoopId(); var cids=uses.Select(_=>b.AllocateCoedgeId()).ToArray(); for(var i=0;i<uses.Count;i++){var n=cids[(i+1)%cids.Length];var p=cids[(i+cids.Length-1)%cids.Length]; b.AddCoedge(new Coedge(cids[i],uses[i].Edge,lid,n,p,uses[i].Rev));} b.AddLoop(new Loop(lid,cids)); return lid; }
    private readonly record struct Use(EdgeId Edge, bool Rev){ public static Use F(EdgeId e)=>new(e,false); public static Use R(EdgeId e)=>new(e,true);} 
}
