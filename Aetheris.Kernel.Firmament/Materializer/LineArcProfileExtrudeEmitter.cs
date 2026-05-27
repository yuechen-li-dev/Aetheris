using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

internal abstract record LineArcProfileCurve2D;
internal sealed record LineArcLineSegment2D((double X, double Y) Start, (double X, double Y) End) : LineArcProfileCurve2D;
internal sealed record LineArcCircularArc2D((double X, double Y) Center, double Radius, double StartAngleRadians, double SweepAngleRadians) : LineArcProfileCurve2D;
internal sealed record LineArcFullCircle2D((double X, double Y) Center, double Radius) : LineArcProfileCurve2D;
internal sealed record LineArcProfileLoop2D(IReadOnlyList<LineArcProfileCurve2D> Curves, bool IsHole);
internal sealed record LineArcProfileExtrudeRequest(IReadOnlyList<LineArcProfileLoop2D> Loops, double Height);
internal enum LineArcProfileExtrudeStatus { Succeeded, Rejected, Deferred, Failed }
internal sealed record LineArcProfileExtrudeResult(LineArcProfileExtrudeStatus Status, BrepBody? Body, IReadOnlyList<string> Diagnostics);

internal static class LineArcProfileExtrudeEmitter
{
    private const double Tol = 1e-6;

    public static LineArcProfileExtrudeResult TryEmit(LineArcProfileExtrudeRequest request)
    {
        var d = new List<string> { "v2-v4-line-arc-profile-extrude-attempted" };
        if (!Validate(request, d)) return new(LineArcProfileExtrudeStatus.Rejected, null, d);
        d.Add("v2-v4-profile-validated");
        var ok = TryBuild(request, d, out var body);
        if (!ok || body is null) return new(LineArcProfileExtrudeStatus.Failed, null, d.Append("v2-v4-line-arc-profile-extrude-rejected:emitter validation failed").ToArray());
        d.Add("v2-v4-cap-faces-emitted");
        d.Add("v2-v4-no-3d-boolean-used");
        d.Add("v2-v4-line-arc-profile-extrude-succeeded");
        return new(LineArcProfileExtrudeStatus.Succeeded, body, d.Distinct().OrderBy(x => x).ToArray());
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

    private static bool TryBuild(LineArcProfileExtrudeRequest profile, List<string> d, out BrepBody? body)
    {
        body = null;
        var h = profile.Height;
        var b = new TopologyBuilder();
        var z0 = -h / 2d; var z1 = h / 2d;
        var bottomLoops = new List<LoopId>(); var topLoops = new List<LoopId>();
        var sideFaces = new List<(LoopId Loop, SurfaceGeometry Surface, string Diag)>();
        var points = new Dictionary<VertexId, Point3D>();
        var edgeCurves = new Dictionary<EdgeId, CurveGeometry>();

        foreach (var loop in profile.Loops)
        {
            var bottomUses = new List<Use>(); var topUses = new List<Use>();
            foreach (var curve in loop.Curves)
            {
                switch (curve)
                {
                    case LineArcLineSegment2D line:
                    {
                        var vb0 = b.AddVertex(); var vb1 = b.AddVertex(); var vt0 = b.AddVertex(); var vt1 = b.AddVertex();
                        points[vb0] = new(line.Start.X, line.Start.Y, z0); points[vb1] = new(line.End.X, line.End.Y, z0);
                        points[vt0] = new(line.Start.X, line.Start.Y, z1); points[vt1] = new(line.End.X, line.End.Y, z1);
                        var eb = b.AddEdge(vb0, vb1); var et = b.AddEdge(vt0, vt1); var es0 = b.AddEdge(vb0, vt0); var es1 = b.AddEdge(vb1, vt1);
                        edgeCurves[eb] = CurveGeometry.FromLine(new Line3Curve(points[vb0], Direction3D.Create(points[vb1] - points[vb0])));
                        edgeCurves[et] = CurveGeometry.FromLine(new Line3Curve(points[vt0], Direction3D.Create(points[vt1] - points[vt0])));
                        edgeCurves[es0] = CurveGeometry.FromLine(new Line3Curve(points[vb0], Direction3D.Create(points[vt0] - points[vb0])));
                        edgeCurves[es1] = CurveGeometry.FromLine(new Line3Curve(points[vb1], Direction3D.Create(points[vt1] - points[vb1])));
                        bottomUses.Add(loop.IsHole ? Use.R(eb) : Use.F(eb)); topUses.Add(loop.IsHole ? Use.F(et) : Use.R(et));
                        var loopId = AddLoop(b, [Use.F(eb), Use.F(es1), Use.R(et), Use.R(es0)]);
                        var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y; var nx = loop.IsHole ? -dy : dy; var ny = loop.IsHole ? dx : -dx;
                        sideFaces.Add((loopId, SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(line.Start.X, line.Start.Y, z0), Direction3D.Create(new Vector3D(nx, ny, 0)), Direction3D.Create(new Vector3D(0, 0, 1)))), "v2-v4-line-edge-side-face-emitted"));
                        break;
                    }
                    case LineArcCircularArc2D arc:
                    {
                        var s = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians));
                        var e = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians));
                        var vb0=b.AddVertex();var vb1=b.AddVertex();var vt0=b.AddVertex();var vt1=b.AddVertex(); points[vb0]=new(s.Item1,s.Item2,z0); points[vb1]=new(e.Item1,e.Item2,z0); points[vt0]=new(s.Item1,s.Item2,z1); points[vt1]=new(e.Item1,e.Item2,z1);
                        var eb=b.AddEdge(vb0,vb1); var et=b.AddEdge(vt0,vt1); var es0=b.AddEdge(vb0,vt0); var es1=b.AddEdge(vb1,vt1);
                        edgeCurves[eb]=CurveGeometry.FromCircle(new Circle3Curve(new Point3D(arc.Center.X,arc.Center.Y,z0),Direction3D.Create(new Vector3D(0,0,1)),arc.Radius,Direction3D.Create(new Vector3D(1,0,0))));
                        edgeCurves[et]=CurveGeometry.FromCircle(new Circle3Curve(new Point3D(arc.Center.X,arc.Center.Y,z1),Direction3D.Create(new Vector3D(0,0,1)),arc.Radius,Direction3D.Create(new Vector3D(1,0,0))));
                        edgeCurves[es0]=CurveGeometry.FromLine(new Line3Curve(points[vb0], Direction3D.Create(points[vt0]-points[vb0]))); edgeCurves[es1]=CurveGeometry.FromLine(new Line3Curve(points[vb1], Direction3D.Create(points[vt1]-points[vb1])));
                        bottomUses.Add(loop.IsHole ? Use.R(eb) : Use.F(eb)); topUses.Add(loop.IsHole ? Use.F(et) : Use.R(et));
                        sideFaces.Add((AddLoop(b,[Use.F(eb),Use.F(es1),Use.R(et),Use.R(es0)]), SurfaceGeometry.FromCylinder(new CylinderSurface(new Point3D(arc.Center.X,arc.Center.Y,z0),Direction3D.Create(new Vector3D(0,0,1)),arc.Radius,Direction3D.Create(new Vector3D(1,0,0)))), "v2-v4-arc-edge-side-face-emitted"));
                        break;
                    }
                    case LineArcFullCircle2D circle:
                    {
                        var vb=b.AddVertex(); var vt=b.AddVertex(); points[vb]=new(circle.Center.X,circle.Center.Y,z0); points[vt]=new(circle.Center.X,circle.Center.Y,z1);
                        var eb=b.AddEdge(vb,vb); var et=b.AddEdge(vt,vt); var es=b.AddEdge(vb,vt);
                        edgeCurves[eb]=CurveGeometry.FromCircle(new Circle3Curve(points[vb],Direction3D.Create(new Vector3D(0,0,1)),circle.Radius,Direction3D.Create(new Vector3D(1,0,0))));
                        edgeCurves[et]=CurveGeometry.FromCircle(new Circle3Curve(points[vt],Direction3D.Create(new Vector3D(0,0,1)),circle.Radius,Direction3D.Create(new Vector3D(1,0,0))));
                        edgeCurves[es]=CurveGeometry.FromLine(new Line3Curve(points[vb],Direction3D.Create(points[vt]-points[vb])));
                        bottomUses.Add(loop.IsHole ? Use.R(eb) : Use.F(eb)); topUses.Add(loop.IsHole ? Use.F(et) : Use.R(et));
                        sideFaces.Add((AddLoop(b,[Use.F(eb),Use.F(es),Use.R(et),Use.R(es)]), SurfaceGeometry.FromCylinder(new CylinderSurface(points[vb],Direction3D.Create(new Vector3D(0,0,1)),circle.Radius,Direction3D.Create(new Vector3D(1,0,0)))), "v2-v4-full-circle-side-face-emitted"));
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
        g.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0,0,z0),Direction3D.Create(new Vector3D(0,0,-1)),Direction3D.Create(new Vector3D(1,0,0)))));
        g.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0,0,z1),Direction3D.Create(new Vector3D(0,0,1)),Direction3D.Create(new Vector3D(1,0,0)))));
        bind.AddFaceBinding(new FaceGeometryBinding(bottomFace, new SurfaceGeometryId(1))); bind.AddFaceBinding(new FaceGeometryBinding(topFace, new SurfaceGeometryId(2)));
        var sid = 3; for (var i = 0; i < sideFaces.Count; i++) { g.AddSurface(new SurfaceGeometryId(sid), sideFaces[i].Surface); bind.AddFaceBinding(new FaceGeometryBinding(faceIds[2+i], new SurfaceGeometryId(sid))); sid++; }
        body = new BrepBody(b.Model, g, bind, points); return true;
    }

    private static LoopId AddLoop(TopologyBuilder b, IReadOnlyList<Use> uses){ var lid=b.AllocateLoopId(); var cids=uses.Select(_=>b.AllocateCoedgeId()).ToArray(); for(var i=0;i<uses.Count;i++){var n=cids[(i+1)%cids.Length];var p=cids[(i+cids.Length-1)%cids.Length]; b.AddCoedge(new Coedge(cids[i],uses[i].Edge,lid,n,p,uses[i].Rev));} b.AddLoop(new Loop(lid,cids)); return lid; }
    private readonly record struct Use(EdgeId Edge, bool Rev){ public static Use F(EdgeId e)=>new(e,false); public static Use R(EdgeId e)=>new(e,true);} 
}
