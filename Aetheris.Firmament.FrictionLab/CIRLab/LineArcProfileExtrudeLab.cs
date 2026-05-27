using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record LineArcProfileExtrudeCase(string Name, LabResolvedProfile2D Profile, double Height);
public sealed record LineArcProfileExtrudeTopologySummary(bool BodyProduced, int FaceCount, int PlanarFaceCount, int CylindricalFaceCount);
public sealed record LineArcProfileExtrudeStepSummary(bool Exported, IReadOnlyList<string> PresentMarkers, IReadOnlyList<string> MissingMarkers, bool ContainsBrepWithVoids);
public sealed record LineArcProfileExtrudeRow(string CaseName, LabProfileStatus Status, bool Succeeded, LineArcProfileExtrudeTopologySummary Topology, LineArcProfileExtrudeStepSummary Step, IReadOnlyList<string> Diagnostics, string Recommendation);

public static class LineArcProfileExtrudeLab
{
    private const double Tol = 1e-6;
    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE"];
    private static readonly HashSet<string> AllowedRecommendations = ["line-arc-profile-extrude-ready-for-production-evaluation", "line-arc-profile-extrude-needs-emitter-hardening", "line-arc-profile-extrude-invalid-rejected", "line-arc-profile-extrude-deferred-topology"];

    public static IReadOnlyList<LineArcProfileExtrudeRow> RunAll() =>
    [
        Run(new("valid-rectangle-only", Rectangle(20, 10), 5)),
        Run(new("valid-rectangle-circle-hole", RectangleCircle(20, 20, 0, 0, 3), 10)),
        Run(new("valid-rectangle-slot-centered", SlotCapsuleExtrudeLab.BuildProfileForX7(30,20,0,0,12,2), 8)),
        Run(new("valid-rectangle-slot-offcenter", SlotCapsuleExtrudeLab.BuildProfileForX7(30,20,5,2,10,1.5), 8)),
        Run(new("valid-rectangle-two-circles", RectangleTwoCircles(30,20,-5,0,5,0,2), 8)),
        Run(new("invalid-zero-height", Rectangle(20,10), 0)),
        Run(new("deferred-multiple-outers", new LabResolvedProfile2D([Rectangle(20,10).Loops[0], Rectangle(10,6).Loops[0]]), 8))
    ];

    public static LineArcProfileExtrudeRow Run(LineArcProfileExtrudeCase c)
    {
        var d = new List<string> { "v2-x7-line-arc-profile-extrude-lab-started" };
        var validated = ResolvedProfile2DLab.Evaluate(c.Name, c.Profile);
        d.AddRange(validated.Diagnostics);
        if (validated.Status != LabProfileStatus.Succeeded)
            return Stop(c.Name, validated.Status, d, "v2-x7-profile-extrude-deferred:profile-validation", validated.Status == LabProfileStatus.Deferred ? "line-arc-profile-extrude-deferred-topology" : "line-arc-profile-extrude-invalid-rejected");
        if (!double.IsFinite(c.Height) || c.Height <= Tol)
            return Stop(c.Name, LabProfileStatus.Failed, d, "v2-x7-profile-extrude-rejected:height<=0", "line-arc-profile-extrude-invalid-rejected");

        d.Add("v2-x7-profile-validated");
        var built = TryBuild(c.Profile, c.Height, d, out var body);
        if (!built || body is null)
            return Stop(c.Name, LabProfileStatus.Failed, d, "v2-x7-profile-extrude-rejected:build-failed", "line-arc-profile-extrude-needs-emitter-hardening");

        d.Add("v2-x7-cap-faces-emitted");
        d.Add("v2-x7-no-3d-boolean-used");
        var top = new LineArcProfileExtrudeTopologySummary(true, body.Topology.Faces.Count(), body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane), body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder));
        var step = SummarizeStep(body, top.CylindricalFaceCount > 0);
        if (step.Exported && step.MissingMarkers.Count == 0 && !step.ContainsBrepWithVoids) d.Add("v2-x7-step-smoke-succeeded"); else d.Add("v2-x7-step-smoke-failed:markers");
        d.Add("v2-x7-profile-extrude-succeeded");
        var rec = step.Exported && step.MissingMarkers.Count == 0 ? "line-arc-profile-extrude-ready-for-production-evaluation" : "line-arc-profile-extrude-needs-emitter-hardening";
        return new(c.Name, LabProfileStatus.Succeeded, rec.Contains("ready"), top, step, d.Distinct().OrderBy(x=>x).ToArray(), rec);
    }

    private static bool TryBuild(LabResolvedProfile2D profile, double h, List<string> d, out BrepBody? body)
    {
        body = null;
        var b = new TopologyBuilder();
        var z0 = -h / 2d; var z1 = h / 2d;
        var bottomLoops = new List<LoopId>(); var topLoops = new List<LoopId>();
        var sideFaces = new List<(LoopId Loop, SurfaceGeometry Surface, string Diag)>();
        var points = new Dictionary<VertexId, Point3D>();
        var edgeCurves = new Dictionary<EdgeId, CurveGeometry>();

        foreach (var (loop, loopIndex) in profile.Loops.Select((x, i) => (x, i)))
        {
            var isHole = loopIndex > 0;
            var bottomUses = new List<Use>(); var topUses = new List<Use>();
            foreach (var curve in loop.Curves)
            {
                switch (curve)
                {
                    case LabAirLineSegment2D line:
                    {
                        var vb0 = b.AddVertex(); var vb1 = b.AddVertex(); var vt0 = b.AddVertex(); var vt1 = b.AddVertex();
                        points[vb0] = new(line.Start.X, line.Start.Y, z0); points[vb1] = new(line.End.X, line.End.Y, z0);
                        points[vt0] = new(line.Start.X, line.Start.Y, z1); points[vt1] = new(line.End.X, line.End.Y, z1);
                        var eb = b.AddEdge(vb0, vb1); var et = b.AddEdge(vt0, vt1); var es0 = b.AddEdge(vb0, vt0); var es1 = b.AddEdge(vb1, vt1);
                        edgeCurves[eb] = CurveGeometry.FromLine(new Line3Curve(points[vb0], Direction3D.Create(points[vb1] - points[vb0])));
                        edgeCurves[et] = CurveGeometry.FromLine(new Line3Curve(points[vt0], Direction3D.Create(points[vt1] - points[vt0])));
                        edgeCurves[es0] = CurveGeometry.FromLine(new Line3Curve(points[vb0], Direction3D.Create(points[vt0] - points[vb0])));
                        edgeCurves[es1] = CurveGeometry.FromLine(new Line3Curve(points[vb1], Direction3D.Create(points[vt1] - points[vb1])));
                        bottomUses.Add(isHole ? Use.R(eb) : Use.F(eb)); topUses.Add(isHole ? Use.F(et) : Use.R(et));
                        var loopId = AddLoop(b, [Use.F(eb), Use.F(es1), Use.R(et), Use.R(es0)]);
                        var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y;
                        var nx = isHole ? -dy : dy; var ny = isHole ? dx : -dx;
                        sideFaces.Add((loopId, SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(line.Start.X, line.Start.Y, z0), Direction3D.Create(new Vector3D(nx, ny, 0)), Direction3D.Create(new Vector3D(0,0,1)))), "v2-x7-line-edge-side-face-emitted"));
                        break;
                    }
                    case LabAirCircularArc2D arc:
                    {
                        var s = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians));
                        var e = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians));
                        var vb0=b.AddVertex();var vb1=b.AddVertex();var vt0=b.AddVertex();var vt1=b.AddVertex();
                        points[vb0]=new(s.Item1,s.Item2,z0); points[vb1]=new(e.Item1,e.Item2,z0); points[vt0]=new(s.Item1,s.Item2,z1); points[vt1]=new(e.Item1,e.Item2,z1);
                        var eb=b.AddEdge(vb0,vb1); var et=b.AddEdge(vt0,vt1); var es0=b.AddEdge(vb0,vt0); var es1=b.AddEdge(vb1,vt1);
                        edgeCurves[eb]=CurveGeometry.FromCircle(new Circle3Curve(new Point3D(arc.Center.X,arc.Center.Y,z0),Direction3D.Create(new Vector3D(0,0,1)),arc.Radius,Direction3D.Create(new Vector3D(1,0,0))));
                        edgeCurves[et]=CurveGeometry.FromCircle(new Circle3Curve(new Point3D(arc.Center.X,arc.Center.Y,z1),Direction3D.Create(new Vector3D(0,0,1)),arc.Radius,Direction3D.Create(new Vector3D(1,0,0))));
                        edgeCurves[es0]=CurveGeometry.FromLine(new Line3Curve(points[vb0], Direction3D.Create(points[vt0]-points[vb0]))); edgeCurves[es1]=CurveGeometry.FromLine(new Line3Curve(points[vb1], Direction3D.Create(points[vt1]-points[vb1])));
                        bottomUses.Add(isHole ? Use.R(eb) : Use.F(eb)); topUses.Add(isHole ? Use.F(et) : Use.R(et));
                        sideFaces.Add((AddLoop(b,[Use.F(eb),Use.F(es1),Use.R(et),Use.R(es0)]), SurfaceGeometry.FromCylinder(new CylinderSurface(new Point3D(arc.Center.X,arc.Center.Y,z0),Direction3D.Create(new Vector3D(0,0,1)),arc.Radius,Direction3D.Create(new Vector3D(1,0,0)))), "v2-x7-arc-edge-side-face-emitted"));
                        break;
                    }
                    case LabAirFullCircle2D circle:
                    {
                        var vb=b.AddVertex(); var vt=b.AddVertex(); points[vb]=new(circle.Center.X,circle.Center.Y,z0); points[vt]=new(circle.Center.X,circle.Center.Y,z1);
                        var eb=b.AddEdge(vb,vb); var et=b.AddEdge(vt,vt); var es=b.AddEdge(vb,vt);
                        edgeCurves[eb]=CurveGeometry.FromCircle(new Circle3Curve(points[vb],Direction3D.Create(new Vector3D(0,0,1)),circle.Radius,Direction3D.Create(new Vector3D(1,0,0))));
                        edgeCurves[et]=CurveGeometry.FromCircle(new Circle3Curve(points[vt],Direction3D.Create(new Vector3D(0,0,1)),circle.Radius,Direction3D.Create(new Vector3D(1,0,0))));
                        edgeCurves[es]=CurveGeometry.FromLine(new Line3Curve(points[vb],Direction3D.Create(points[vt]-points[vb])));
                        bottomUses.Add(isHole ? Use.R(eb) : Use.F(eb)); topUses.Add(isHole ? Use.F(et) : Use.R(et));
                        sideFaces.Add((AddLoop(b,[Use.F(eb),Use.F(es),Use.R(et),Use.R(es)]), SurfaceGeometry.FromCylinder(new CylinderSurface(points[vb],Direction3D.Create(new Vector3D(0,0,1)),circle.Radius,Direction3D.Create(new Vector3D(1,0,0)))), "v2-x7-full-circle-side-face-emitted"));
                        break;
                    }
                    default: d.Add("v2-x7-profile-extrude-deferred:unsupported-curve"); return false;
                }
            }
            bottomLoops.Add(AddLoop(b, bottomUses));
            topLoops.Add(AddLoop(b, topUses));
        }

        var bottomFace = b.AddFace(bottomLoops);
        var topFace = b.AddFace(topLoops);
        var faceIds = new List<FaceId> { bottomFace, topFace };
        foreach (var sf in sideFaces) { faceIds.Add(b.AddFace([sf.Loop])); d.Add(sf.Diag); }
        var shell = b.AddShell(faceIds); b.AddBody([shell]);

        var g = new BrepGeometryStore(); var bind = new BrepBindingModel();
        var cid = 1;
        foreach (var e in b.Model.Edges.OrderBy(x=>x.Id.Value)) { g.AddCurve(new CurveGeometryId(cid), edgeCurves[e.Id]); bind.AddEdgeBinding(new EdgeGeometryBinding(e.Id,new CurveGeometryId(cid),new ParameterInterval(0,2*Math.PI))); cid++; }
        g.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0,0,z0),Direction3D.Create(new Vector3D(0,0,-1)),Direction3D.Create(new Vector3D(1,0,0)))));
        g.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0,0,z1),Direction3D.Create(new Vector3D(0,0,1)),Direction3D.Create(new Vector3D(1,0,0)))));
        bind.AddFaceBinding(new FaceGeometryBinding(bottomFace, new SurfaceGeometryId(1)));
        bind.AddFaceBinding(new FaceGeometryBinding(topFace, new SurfaceGeometryId(2)));
        var sid = 3;
        for (var i = 0; i < sideFaces.Count; i++) { g.AddSurface(new SurfaceGeometryId(sid), sideFaces[i].Surface); bind.AddFaceBinding(new FaceGeometryBinding(faceIds[2 + i], new SurfaceGeometryId(sid))); sid++; }
        body = new BrepBody(b.Model, g, bind, points);
        return true;
    }

    private static LineArcProfileExtrudeRow Stop(string name, LabProfileStatus status, List<string> d, string diag, string rec)
    {
        d.Add(diag);
        return new(name, status, false, new(false,0,0,0), new(false,[],[],false), d.Distinct().OrderBy(x=>x).ToArray(), AllowedRecommendations.Contains(rec) ? rec : "line-arc-profile-extrude-needs-emitter-hardening");
    }

    private static LineArcProfileExtrudeStepSummary SummarizeStep(BrepBody b, bool needsCylinder)
    {
        var markers = needsCylinder ? RequiredStepMarkers.Concat(["CYLINDRICAL_SURFACE"]).ToArray() : RequiredStepMarkers;
        var ex = Step242Exporter.ExportBody(b);
        if (!ex.IsSuccess || ex.Value is null) return new(false, [], markers.OrderBy(x=>x, StringComparer.Ordinal).ToArray(), false);
        var txt = ex.Value;
        var present = markers.Where(m => txt.Contains(m, StringComparison.Ordinal)).ToArray();
        return new(true, present.OrderBy(x=>x, StringComparer.Ordinal).ToArray(), markers.Except(present).OrderBy(x=>x, StringComparer.Ordinal).ToArray(), txt.Contains("BREP_WITH_VOIDS", StringComparison.Ordinal));
    }

    private static LoopId AddLoop(TopologyBuilder b, IReadOnlyList<Use> uses){ var lid=b.AllocateLoopId(); var cids=uses.Select(_=>b.AllocateCoedgeId()).ToArray(); for(var i=0;i<uses.Count;i++){var n=cids[(i+1)%cids.Length];var p=cids[(i+cids.Length-1)%cids.Length]; b.AddCoedge(new Coedge(cids[i],uses[i].Edge,lid,n,p,uses[i].Rev));} b.AddLoop(new Loop(lid,cids)); return lid; }
    private readonly record struct Use(EdgeId Edge, bool Rev){ public static Use F(EdgeId e)=>new(e,false); public static Use R(EdgeId e)=>new(e,true);}    

    private static LabResolvedProfile2D Rectangle(double w, double h) => new([new LabAirLoop2D([new LabAirLineSegment2D((-w/2,-h/2),(w/2,-h/2)),new LabAirLineSegment2D((w/2,-h/2),(w/2,h/2)),new LabAirLineSegment2D((w/2,h/2),(-w/2,h/2)),new LabAirLineSegment2D((-w/2,h/2),(-w/2,-h/2))], "outer")]);
    private static LabResolvedProfile2D RectangleCircle(double w,double h,double cx,double cy,double r)=>new([Rectangle(w,h).Loops[0], new LabAirLoop2D([new LabAirFullCircle2D((cx,cy),r,false)],"hole")]);
    private static LabResolvedProfile2D RectangleTwoCircles(double w,double h,double c1x,double c1y,double c2x,double c2y,double r)=>new([Rectangle(w,h).Loops[0],new LabAirLoop2D([new LabAirFullCircle2D((c1x,c1y),r,false)],"hole"),new LabAirLoop2D([new LabAirFullCircle2D((c2x,c2y),r,false)],"hole")]);
}
