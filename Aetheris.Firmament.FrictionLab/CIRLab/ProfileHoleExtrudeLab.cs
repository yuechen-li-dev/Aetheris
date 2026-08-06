using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record ProfileHoleExtrudeCase(string Name, LabResolvedProfile2D Profile, double Height);
public sealed record ProfileHoleExtrudeTopologySummary(bool BodyProduced, int VertexCount, int EdgeCount, int FaceCount, int PlanarFaceCount, int CylindricalFaceCount, int LoopCount, int CoedgeCount);
public sealed record ProfileHoleExtrudeStepSummary(bool Exported, IReadOnlyList<string> PresentMarkers, IReadOnlyList<string> MissingMarkers, bool ContainsBrepWithVoids);
public sealed record ProfileHoleExtrudeRow(string CaseName, bool Succeeded, ProfileHoleExtrudeTopologySummary Topology, ProfileHoleExtrudeStepSummary Step, IReadOnlyList<string> Diagnostics, string Recommendation);

public static class ProfileHoleExtrudeLab
{
    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE", "CYLINDRICAL_SURFACE"];

    public static IReadOnlyList<ProfileHoleExtrudeRow> RunAll() =>
    [
        Run(new("valid-rect-center-hole", Rectangle(20,20,[(0d,0d,3d)]), 10)),
        Run(new("valid-rect-offcenter-hole", Rectangle(30,20,[(4d,2d,2d)]), 8)),
        Run(new("valid-rect-two-holes", Rectangle(30,20,[(-6d,0d,2d),(6d,0d,2d)]), 8)),
        Run(new("valid-orientation-reversed-input", Rectangle(20,20,[(0d,0d,3d)], true), 10)),
        Run(new("invalid-hole-outside", Rectangle(20,20,[(20d,20d,2d)]), 10)),
        Run(new("invalid-hole-touches-boundary", Rectangle(20,20,[(7d,0d,3d)]), 10)),
        Run(new("invalid-hole-overlap", Rectangle(30,20,[(-2d,0d,3d),(2d,0d,3d)]), 8)),
        Run(new("invalid-height", Rectangle(20,20,[(0d,0d,3d)]), 0)),
        Run(new("invalid-hole-radius", Rectangle(20,20,[(0d,0d,0d)]), 8)),
        Run(new("invalid-open-outer", OpenOuterWithHole(), 8)),
        Run(new("deferred-multiple-outers", MultipleOuters(), 8))
    ];

    public static ProfileHoleExtrudeRow Run(ProfileHoleExtrudeCase @case)
    {
        var diagnostics = new List<string> { "v2-x3-profile-hole-extrude-lab-started" };
        var validated = ResolvedProfile2DLab.Evaluate(@case.Name, @case.Profile);
        diagnostics.AddRange(validated.Diagnostics);
        if (validated.Status != LabProfileStatus.Succeeded)
        {
            diagnostics.Add("v2-x3-invalid-profile-rejected");
            return new(@case.Name, false, EmptyTopology(), EmptyStep(), diagnostics.Distinct().OrderBy(x=>x).ToArray(), validated.Status == LabProfileStatus.Deferred ? "profile-hole-extrude-deferred-topology" : "profile-hole-extrude-invalid-profile-rejected");
        }
        if (!double.IsFinite(@case.Height) || @case.Height <= 0)
        {
            diagnostics.Add("v2-x3-invalid-profile-rejected");
            return new(@case.Name, false, EmptyTopology(), EmptyStep(), diagnostics.Distinct().OrderBy(x=>x).ToArray(), "profile-hole-extrude-invalid-profile-rejected");
        }

        diagnostics.Add("v2-x3-resolved-profile-validated");
        var build = BuildBody(@case.Profile, @case.Height);
        if (!build.IsSuccess || build.Body is null)
        {
            diagnostics.Add($"v2-x3-topology-contract-failed:{build.Diagnostic}");
            return new(@case.Name, false, EmptyTopology(), EmptyStep(), diagnostics.Distinct().OrderBy(x=>x).ToArray(), "profile-hole-extrude-needs-emitter-parity-work");
        }

        diagnostics.Add("v2-x3-profile-hole-candidate-created");
        diagnostics.Add("v2-x3-no-3d-boolean-subtract-used");
        var topology = SummarizeTopology(build.Body);
        var holeCount = @case.Profile.Loops.Count - 1;
        var topologyOk = topology.PlanarFaceCount == 6 && topology.CylindricalFaceCount == holeCount;
        diagnostics.Add(topologyOk ? "v2-x3-topology-contract-succeeded" : $"v2-x3-topology-contract-failed:planes={topology.PlanarFaceCount};cyl={topology.CylindricalFaceCount};holes={holeCount}");
        var step = SummarizeStep(build.Body);
        diagnostics.Add(step.Exported && step.MissingMarkers.Count == 0 && !step.ContainsBrepWithVoids ? "v2-x3-step-smoke-succeeded" : "v2-x3-step-smoke-failed:missing-markers-or-voids");
        var rec = topologyOk && step.Exported && step.MissingMarkers.Count == 0 && !step.ContainsBrepWithVoids
            ? "profile-hole-extrude-ready-for-production-evaluation"
            : "profile-hole-extrude-needs-emitter-parity-work";
        return new(@case.Name, rec == "profile-hole-extrude-ready-for-production-evaluation", topology, step, diagnostics.Distinct().OrderBy(x=>x).ToArray(), rec);
    }

    private static (bool IsSuccess, BrepBody? Body, string Diagnostic) BuildBody(LabResolvedProfile2D profile, double height)
    {
        var rect = profile.Loops[0].Curves.OfType<LabAirLineSegment2D>().ToArray();
        if (rect.Length != 4) return (false, null, "outer-not-rectangle");
        var pts = rect.Select(x => x.Start).ToArray();
        var minX = pts.Min(p => p.X); var maxX = pts.Max(p => p.X); var minY = pts.Min(p => p.Y); var maxY = pts.Max(p => p.Y);
        var z0 = -height / 2d; var z1 = height / 2d;
        var b = new TopologyBuilder();
        var ob = new[] { b.AddVertex(), b.AddVertex(), b.AddVertex(), b.AddVertex() };
        var ot = new[] { b.AddVertex(), b.AddVertex(), b.AddVertex(), b.AddVertex() };
        var holeCount = profile.Loops.Count - 1;
        var hb = new VertexId[holeCount]; var ht = new VertexId[holeCount];
        for (var i=0;i<holeCount;i++){ hb[i]=b.AddVertex(); ht[i]=b.AddVertex(); }
        var be=new[] { b.AddEdge(ob[0],ob[1]),b.AddEdge(ob[1],ob[2]),b.AddEdge(ob[2],ob[3]),b.AddEdge(ob[3],ob[0])};
        var te=new[] { b.AddEdge(ot[0],ot[1]),b.AddEdge(ot[1],ot[2]),b.AddEdge(ot[2],ot[3]),b.AddEdge(ot[3],ot[0])};
        var se=new[] { b.AddEdge(ob[0],ot[0]),b.AddEdge(ob[1],ot[1]),b.AddEdge(ob[2],ot[2]),b.AddEdge(ob[3],ot[3])};
        var hbe=new EdgeId[holeCount];var hte=new EdgeId[holeCount];var hse=new EdgeId[holeCount];
        for(var i=0;i<holeCount;i++){ hbe[i]=b.AddEdge(hb[i],hb[i]); hte[i]=b.AddEdge(ht[i],ht[i]); hse[i]=b.AddEdge(hb[i],ht[i]); }

        var bottomOuter = AddLoop(b,[Use.F(be[0]),Use.F(be[1]),Use.F(be[2]),Use.F(be[3])]);
        var bottomLoops = new List<LoopId>{bottomOuter};
        for(var i=0;i<holeCount;i++) bottomLoops.Add(AddLoop(b,[Use.R(hbe[i])]));
        var bottomFace = b.AddFace(bottomLoops);
        var topOuter = AddLoop(b,[Use.R(te[0]),Use.R(te[1]),Use.R(te[2]),Use.R(te[3])]);
        var topLoops = new List<LoopId>{topOuter};
        for(var i=0;i<holeCount;i++) topLoops.Add(AddLoop(b,[Use.F(hte[i])]));
        var topFace = b.AddFace(topLoops);
        var faces = new List<FaceId>{bottomFace, topFace};
        for(var i=0;i<4;i++){ var n=(i+1)%4; faces.Add(b.AddFace([AddLoop(b,[Use.F(be[i]),Use.F(se[n]),Use.R(te[i]),Use.R(se[i])])])); }
        for(var i=0;i<holeCount;i++) faces.Add(b.AddFace([AddLoop(b,[Use.F(hbe[i]),Use.F(hse[i]),Use.R(hte[i]),Use.R(hse[i])])]));
        var shell=b.AddShell(faces); b.AddBody([shell]);

        var g=new BrepGeometryStore(); var bind=new BrepBindingModel();
        var map = new Dictionary<VertexId, Point3D>{ [ob[0]]=new(minX,minY,z0),[ob[1]]=new(maxX,minY,z0),[ob[2]]=new(maxX,maxY,z0),[ob[3]]=new(minX,maxY,z0),[ot[0]]=new(minX,minY,z1),[ot[1]]=new(maxX,minY,z1),[ot[2]]=new(maxX,maxY,z1),[ot[3]]=new(minX,maxY,z1)};
        for(var i=0;i<holeCount;i++) { var c=((LabAirFullCircle2D)profile.Loops[i+1].Curves[0]).Center; map[hb[i]]=new(c.X,c.Y,z0); map[ht[i]]=new(c.X,c.Y,z1);}        
        var cid=1;
        foreach(var e in b.Model.Edges.OrderBy(x=>x.Id.Value)){
            var p0=map[e.StartVertexId]; var p1=map[e.EndVertexId];
            CurveGeometry curve;
            if(e.StartVertexId==e.EndVertexId){ var hIdx=Array.FindIndex(hb,v=>v==e.StartVertexId); if(hIdx<0) hIdx=Array.FindIndex(ht,v=>v==e.StartVertexId); var r=((LabAirFullCircle2D)profile.Loops[hIdx+1].Curves[0]).Radius; curve=CurveGeometry.FromCircle(new Circle3Curve(p0, Direction3D.Create(new Vector3D(0,0,1)), r, Direction3D.Create(new Vector3D(1,0,0)))); bind.AddEdgeBinding(new EdgeGeometryBinding(e.Id,new CurveGeometryId(cid),new ParameterInterval(0,2*Math.PI))); }
            else { curve=CurveGeometry.FromLine(new Line3Curve(p0,Direction3D.Create(p1-p0))); bind.AddEdgeBinding(new EdgeGeometryBinding(e.Id,new CurveGeometryId(cid),new ParameterInterval(0,(p1-p0).Length))); }
            g.AddCurve(new CurveGeometryId(cid++), curve);
        }
        g.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0,0,z0), Direction3D.Create(new Vector3D(0,0,-1)), Direction3D.Create(new Vector3D(1,0,0)))));
        g.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0,0,z1), Direction3D.Create(new Vector3D(0,0,1)), Direction3D.Create(new Vector3D(1,0,0)))));
        g.AddSurface(new SurfaceGeometryId(3), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(minX,minY,z0), Direction3D.Create(new Vector3D(0,-1,0)), Direction3D.Create(new Vector3D(0,0,1)))));
        g.AddSurface(new SurfaceGeometryId(4), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(maxX,minY,z0), Direction3D.Create(new Vector3D(1,0,0)), Direction3D.Create(new Vector3D(0,0,1)))));
        g.AddSurface(new SurfaceGeometryId(5), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(maxX,maxY,z0), Direction3D.Create(new Vector3D(0,1,0)), Direction3D.Create(new Vector3D(0,0,1)))));
        g.AddSurface(new SurfaceGeometryId(6), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(minX,maxY,z0), Direction3D.Create(new Vector3D(-1,0,0)), Direction3D.Create(new Vector3D(0,0,1)))));
        for(var i=0;i<holeCount;i++){ var h=((LabAirFullCircle2D)profile.Loops[i+1].Curves[0]); g.AddSurface(new SurfaceGeometryId(7+i),SurfaceGeometry.FromCylinder(new CylinderSurface(new Point3D(h.Center.X,h.Center.Y,z0),Direction3D.Create(new Vector3D(0,0,1)),h.Radius,Direction3D.Create(new Vector3D(1,0,0))))); }
        bind.AddFaceBinding(new FaceGeometryBinding(faces[0],new SurfaceGeometryId(1))); bind.AddFaceBinding(new FaceGeometryBinding(faces[1],new SurfaceGeometryId(2)));
        bind.AddFaceBinding(new FaceGeometryBinding(faces[2],new SurfaceGeometryId(3))); bind.AddFaceBinding(new FaceGeometryBinding(faces[3],new SurfaceGeometryId(4))); bind.AddFaceBinding(new FaceGeometryBinding(faces[4],new SurfaceGeometryId(5))); bind.AddFaceBinding(new FaceGeometryBinding(faces[5],new SurfaceGeometryId(6)));
        for(var i=0;i<holeCount;i++) bind.AddFaceBinding(new FaceGeometryBinding(faces[6+i],new SurfaceGeometryId(7+i)));
        return (true,new BrepBody(b.Model,g,bind,map),"");
    }

    private static LoopId AddLoop(TopologyBuilder b, IReadOnlyList<Use> uses){ var lid=b.AllocateLoopId(); var cids=uses.Select(_=>b.AllocateCoedgeId()).ToArray(); for(var i=0;i<uses.Count;i++){var n=cids[(i+1)%cids.Length];var p=cids[(i+cids.Length-1)%cids.Length]; b.AddCoedge(new Coedge(cids[i],uses[i].Edge,lid,n,p,uses[i].Rev));} b.AddLoop(new Loop(lid,cids)); return lid; }
    private readonly record struct Use(EdgeId Edge, bool Rev){ public static Use F(EdgeId e)=>new(e,false); public static Use R(EdgeId e)=>new(e,true);}    

    private static ProfileHoleExtrudeTopologySummary SummarizeTopology(BrepBody b) => new(true,b.Topology.Vertices.Count(),b.Topology.Edges.Count(),b.Topology.Faces.Count(),b.Topology.Faces.Count(f=>b.GetFaceSurface(f.Id).Kind==SurfaceGeometryKind.Plane),b.Topology.Faces.Count(f=>b.GetFaceSurface(f.Id).Kind==SurfaceGeometryKind.Cylinder),b.Topology.Loops.Count(),b.Topology.Coedges.Count());
    private static ProfileHoleExtrudeStepSummary SummarizeStep(BrepBody b){ var e=Step242Exporter.ExportBody(b); if(!e.IsSuccess||e.Value is null) return EmptyStep(); var t=e.Value; var present=RequiredStepMarkers.Where(m=>t.Contains(m,StringComparison.Ordinal)).ToArray(); return new(true,present,RequiredStepMarkers.Except(present).ToArray(),t.Contains("BREP_WITH_VOIDS",StringComparison.Ordinal)); }
    private static ProfileHoleExtrudeTopologySummary EmptyTopology()=>new(false,0,0,0,0,0,0,0);
    private static ProfileHoleExtrudeStepSummary EmptyStep()=>new(false,[],RequiredStepMarkers,false);

    private static LabResolvedProfile2D Rectangle(double w,double h, IReadOnlyList<(double X,double Y,double R)> holes, bool reverse=false)
    {
        var hw=w/2; var hh=h/2;
        var outer = reverse
            ? new LabAirLoop2D([new LabAirLineSegment2D((-hw,-hh),(-hw,hh)),new LabAirLineSegment2D((-hw,hh),(hw,hh)),new LabAirLineSegment2D((hw,hh),(hw,-hh)),new LabAirLineSegment2D((hw,-hh),(-hw,-hh))],"outer")
            : new LabAirLoop2D([new LabAirLineSegment2D((-hw,-hh),(hw,-hh)),new LabAirLineSegment2D((hw,-hh),(hw,hh)),new LabAirLineSegment2D((hw,hh),(-hw,hh)),new LabAirLineSegment2D((-hw,hh),(-hw,-hh))],"outer");
        var loops=new List<LabAirLoop2D>{outer};
        loops.AddRange(holes.Select(hole=>new LabAirLoop2D([new LabAirFullCircle2D((hole.X,hole.Y),hole.R,false)],"hole")));
        return new(loops);
    }
    private static LabResolvedProfile2D OpenOuterWithHole()=>new([new LabAirLoop2D([new LabAirLineSegment2D((-10,-10),(10,-10)),new LabAirLineSegment2D((10,-10),(10,10)),new LabAirLineSegment2D((10,10),(-10,10))],"outer"),new LabAirLoop2D([new LabAirFullCircle2D((0,0),2,false)],"hole")]);
    private static LabResolvedProfile2D MultipleOuters()=>new([Rectangle(20,20,[]).Loops[0], Rectangle(10,10,[]).Loops[0]]);
}
