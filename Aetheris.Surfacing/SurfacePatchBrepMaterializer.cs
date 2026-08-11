using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Surfacing;

/// <summary>Builds a thin closed panel whose authoritative lower face is a bounded rectangular non-rational support.</summary>
public static class SurfacePatchBrepMaterializer
{
    public static (BrepBody? Body,IReadOnlyList<SurfacingDiagnostic> Diagnostics) Materialize(
        SurfaceGeometry support,Func<double,double,Point3D> evaluate,double thickness=1d)
    {
        ArgumentNullException.ThrowIfNull(support);ArgumentNullException.ThrowIfNull(evaluate);
        if(!double.IsFinite(thickness)||thickness<=0)return(null,[new("surfacing-panel-thickness-invalid","Panel thickness must be finite and positive.")]);
        if(support.BSplineSurfaceWithKnots is not { } spline)return(null,[new("surfacing-support-incompatible","Bounded gallery panel materialization currently requires a non-rational B-spline support.")]);
        var dz=new Vector3D(0,0,thickness);var bottom=new[]{evaluate(0,0),evaluate(1,0),evaluate(1,1),evaluate(0,1)};var top=bottom.Select(p=>p+dz).ToArray();
        if(bottom.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||!double.IsFinite(p.Z)))return (null,[new("surfacing-boundary-invalid","Patch corner coordinates must be finite.")]);
        var builder=new TopologyBuilder();var vb=bottom.Select(_=>builder.AddVertex()).ToArray();var vt=top.Select(_=>builder.AddVertex()).ToArray();var eb=Enumerable.Range(0,4).Select(i=>builder.AddEdge(vb[i],vb[(i+1)%4])).ToArray();var et=Enumerable.Range(0,4).Select(i=>builder.AddEdge(vt[i],vt[(i+1)%4])).ToArray();var ev=Enumerable.Range(0,4).Select(i=>builder.AddEdge(vb[i],vt[i])).ToArray();
        var faces=new List<FaceId>{AddFace(builder,eb.Select(Use.F).ToArray()),AddFace(builder,et.Reverse().Select(Use.R).ToArray())};for(var i=0;i<4;i++)faces.Add(AddFace(builder,[Use.F(eb[i]),Use.F(ev[(i+1)%4]),Use.R(et[i]),Use.R(ev[i])]));var shell=builder.AddShell(faces);builder.AddBody([shell]);
        var geometry=new BrepGeometryStore();var bindings=new BrepBindingModel();var surfaceId=1;geometry.AddSurface(new(surfaceId),support);bindings.AddFaceBinding(new(faces[0],new(surfaceId++),false));var translated=Translate(spline,dz);geometry.AddSurface(new(surfaceId),SurfaceGeometry.FromBSplineSurfaceWithKnots(translated));bindings.AddFaceBinding(new(faces[1],new(surfaceId++),true));
        var boundaryCurves=Boundaries(spline);for(var i=0;i<4;i++){var curve=boundaryCurves[i];geometry.AddCurve(new(i+1),CurveGeometry.FromBSpline(curve));bindings.AddEdgeBinding(new(eb[i],new(i+1),new ParameterInterval(curve.DomainStart,curve.DomainEnd)));var raised=Translate(curve,dz);geometry.AddCurve(new(i+5),CurveGeometry.FromBSpline(raised));bindings.AddEdgeBinding(new(et[i],new(i+5),new ParameterInterval(raised.DomainStart,raised.DomainEnd)));var side=Ruled(curve,dz);geometry.AddSurface(new(surfaceId),SurfaceGeometry.FromBSplineSurfaceWithKnots(side));bindings.AddFaceBinding(new(faces[i+2],new(surfaceId++)));}
        for(var i=0;i<4;i++){geometry.AddCurve(new(i+9),CurveGeometry.FromLine(new Line3Curve(bottom[i],Direction3D.Create(dz))));bindings.AddEdgeBinding(new(ev[i],new(i+9),new ParameterInterval(0,thickness)));}
        var vertexPoints=vb.Select((id,index)=>(id,bottom[index])).Concat(vt.Select((id,index)=>(id,top[index]))).ToDictionary(pair=>pair.id,pair=>pair.Item2);var body=new BrepBody(builder.Model,geometry,bindings,vertexPoints);var validation=BrepBindingValidator.Validate(body,true);
        return validation.IsSuccess?(body,[]):(null,validation.Diagnostics.Select(d=>new SurfacingDiagnostic("surfacing-brep-invalid",d.Message)).ToArray());
    }

    private readonly record struct Use(EdgeId Edge,bool Reverse){internal static Use F(EdgeId edge)=>new(edge,false);internal static Use R(EdgeId edge)=>new(edge,true);}
    private static FaceId AddFace(TopologyBuilder builder,IReadOnlyList<Use> uses){var loop=builder.AllocateLoopId();var ids=uses.Select(_=>builder.AllocateCoedgeId()).ToArray();for(var i=0;i<ids.Length;i++)builder.AddCoedge(new Coedge(ids[i],uses[i].Edge,loop,ids[(i+1)%ids.Length],ids[(i+ids.Length-1)%ids.Length],uses[i].Reverse));builder.AddLoop(new Loop(loop,ids));return builder.AddFace([loop]);}
    private static BSplineSurfaceWithKnots Translate(BSplineSurfaceWithKnots source,Vector3D offset)=>new(source.DegreeU,source.DegreeV,source.ControlPoints.Select(row=>(IReadOnlyList<Point3D>)row.Select(p=>p+offset).ToArray()).ToArray(),source.SurfaceForm,source.UClosed,source.VClosed,source.SelfIntersect,source.KnotMultiplicitiesU,source.KnotMultiplicitiesV,source.KnotValuesU,source.KnotValuesV,source.KnotSpec);
    private static BSpline3Curve Translate(BSpline3Curve source,Vector3D offset)=>new(source.Degree,source.ControlPoints.Select(p=>p+offset).ToArray(),source.KnotMultiplicities,source.KnotValues,source.CurveForm,source.ClosedCurve,source.SelfIntersect,source.KnotSpec);
    private static BSplineSurfaceWithKnots Ruled(BSpline3Curve boundary,Vector3D offset)=>new(boundary.Degree,1,boundary.ControlPoints.Select(p=>(IReadOnlyList<Point3D>)new[]{p,p+offset}).ToArray(),"RULED_SURFACE",false,false,false,boundary.KnotMultiplicities,[2,2],boundary.KnotValues,[0d,1d],"UNSPECIFIED");
    private static IReadOnlyList<BSpline3Curve> Boundaries(BSplineSurfaceWithKnots surface)
    {
        var south=surface.ControlPoints.Select(row=>row[0]).ToArray();var east=surface.ControlPoints[^1].ToArray();var north=surface.ControlPoints.Select(row=>row[^1]).Reverse().ToArray();var west=surface.ControlPoints[0].Reverse().ToArray();
        return [Curve(surface.DegreeU,south,surface.KnotMultiplicitiesU,surface.KnotValuesU,false),Curve(surface.DegreeV,east,surface.KnotMultiplicitiesV,surface.KnotValuesV,false),Curve(surface.DegreeU,north,surface.KnotMultiplicitiesU,surface.KnotValuesU,true),Curve(surface.DegreeV,west,surface.KnotMultiplicitiesV,surface.KnotValuesV,true)];
    }
    private static BSpline3Curve Curve(int degree,IReadOnlyList<Point3D> controls,IReadOnlyList<int> multiplicities,IReadOnlyList<double> knots,bool reverse)
    {var m=reverse?multiplicities.Reverse().ToArray():multiplicities.ToArray();var k=reverse?knots.Reverse().Select(value=>knots[0]+knots[^1]-value).ToArray():knots.ToArray();return new(degree,controls,m,k,"UNSPECIFIED",false,false,"UNSPECIFIED");}
}

public sealed record SurfacingGalleryEntry(string StableId,SurfaceConstructionKind ConstructionKind,SurfaceGeometry Support,
    Func<double,double,Point3D> Evaluate,SurfaceMaterializationKind MaterializationKind,ApproximationCertificate? Approximation,
    DevelopabilityEvidence Developability,int SourceDeclarations,string AuthoringSummary,PanelIr Panel);

public static class SurfacingGallery
{
    public static IReadOnlyList<SurfacingGalleryEntry> Build()
    {
        var entries=new List<SurfacingGalleryEntry>();
        AddParametric(MathematicalSurfaces.HyperbolicParaboloid("pringles-saddle",40,30,12),9,9,4,"named HyperbolicParaboloid(width, depth, rise)");
        var canopyIr=RuledSurfaceLowering.Saddle("twisted-ruled-canopy",40,25,12);var canopy=RuledSurfaceLowering.Lower(canopyIr).Patch!;AddRuled(canopyIr,canopy,4,"two authoritative lines and straight rulings");
        var conoidA=new RuledBoundary.Line("conoid:axis",new(-35,-20,0),new(35,-20,0));var conoidB=new RuledBoundary.Arc("conoid:arch",new(0,20,0),Direction3D.Create(new(0,-1,0)),35,Direction3D.Create(new(-1,0,0)),0,double.Pi);
        var conoidIr=new RuledSurfaceIr("conoid-panel",RuledConstructionKind.RuledSurface,conoidA,conoidB,new(conoidA.StableId,"gallery","axis"),new(conoidB.StableId,"gallery","arch"));AddRuled(conoidIr,RuledSurfaceLowering.Lower(conoidIr).Patch!,5,"line-to-arc ruled conoid family");
        var south=new RuledBoundary.Line("panel:south",new(-30,-20,0),new(30,-20,0));var north=new RuledBoundary.Line("panel:north",new(-30,20,5),new(30,20,5));
        RuledBoundary west=SideArc("panel:west",-30);RuledBoundary east=SideArc("panel:east",30);
        var boundaries=new RuledBoundary[]{south,north,west,east};var bp=boundaries.Select(b=>new BoundaryProvenance(b.StableId,"gallery:boundary-panel",b.StableId)).ToArray();var boundaryIr=new BoundaryPatchIr("four-boundary-panel",south,north,west,east,bp);var panel=BoundaryPatchLowering.Lower(boundaryIr).Patch!;AddConstructed(panel,PanelFactory.FromBoundaryPatch(boundaryIr).Panel!,8,"four boundary curves; no authored control net");
        var sections=new RuledBoundary[]{Section("fairing:s0",0,0,26),Section("fairing:s1",20,12,34),Section("fairing:s2",40,4,22),Section("fairing:s3",60,0,14)};var sp=sections.Select((s,i)=>new BoundaryProvenance(s.StableId,"gallery:fairing",$"section-{i}")).ToArray();var sectionIr=new SectionSurfaceIr("section-fairing",sections,sp);var fairing=SectionSurfaceLowering.Lower(sectionIr).Patch!;AddConstructed(fairing,PanelFactory.FromSectionSurface(sectionIr).Panel!,7,"four ordered semantic sections");
        AddParametric(MathematicalSurfaces.Helicoid("helicoid-panel",32,18,.75),17,17,5,"named Helicoid(radius, rise, turns)");
        return entries;

        void AddParametric(ParametricSurfaceIr source,int cu,int cv,int declarations,string summary){var mat=ParametricSurfaceMaterializer.Materialize(source,cu,cv,.1);var p=PanelFactory.FromParametric(source,controlCountU:cu,controlCountV:cv).Panel!;entries.Add(new(source.StableId,source.ConstructionKind,SurfaceGeometry.FromBSplineSurfaceWithKnots(mat.Surface),(u,v)=>source.Evaluate(source.Domain.U.Map(u),source.Domain.V.Map(v)).Point,mat.Kind,mat.Certificate,new(DevelopabilityKind.Indeterminate,"parametric curvature classification",null,0,"Not assumed developable."),declarations,summary,p));}
        void AddRuled(RuledSurfaceIr source,RuledSurfacePatch patch,int declarations,string summary)=>entries.Add(new(patch.Ir.StableId,patch.Ir.Kind==RuledConstructionKind.RuledTransition?SurfaceConstructionKind.RuledTransition:SurfaceConstructionKind.RuledSurface,patch.ExactSurface,patch.Evaluate,patch.MaterializationKind,patch.ApproximationCertificate,patch.Developability,declarations,summary,PanelFactory.FromRuled(source).Panel!));
        void AddConstructed(ConstructedSurfacePatch patch,PanelIr p,int declarations,string summary)=>entries.Add(new(patch.StableId,patch.ConstructionKind,patch.Support,patch.Evaluate,patch.MaterializationKind,patch.ApproximationCertificate,patch.Developability,declarations,summary,p));
    }

    private static RuledBoundary.BSpline SideArc(string id,double x)
    {
        var points=new[]{new Point3D(x,-20,0),new Point3D(x-5,-7,8),new Point3D(x-5,7,10),new Point3D(x,20,5)};
        return new(id,new BSpline3Curve(3,points,[4,4],[0d,1d],"UNSPECIFIED",false,false,"UNSPECIFIED"));
    }
    private static RuledBoundary.BSpline Section(string id,double x,double z,double width)
    {
        var half=width/2;var points=new[]{new Point3D(-half,x,z),new Point3D(-half/3,x,z+4),new Point3D(half/3,x,z+4),new Point3D(half,x,z)};
        return new(id,new BSpline3Curve(3,points,[4,4],[0d,1d],"UNSPECIFIED",false,false,"UNSPECIFIED"));
    }
}

public sealed record PanelShowcase(string StableId,IReadOnlyList<PanelIr> Panels,IReadOnlyList<PanelMateRequest> Mates,PanelNetworkReport Network);

public static class PanelShowcases
{
    /// <summary>Four individually planar/developable strips joined into a deterministic folded canopy.</summary>
    public static PanelShowcase DevelopableFoldedCanopy()
    {
        var stations=new[]{(-24d,0d),(-12d,7d),(0d,-2d),(12d,8d),(24d,0d)};
        var panels=new List<PanelIr>();
        for(var i=0;i<stations.Length-1;i++)
        {
            var a=new RuledBoundary.Line($"fold:{i}:south",new(-35,stations[i].Item1,stations[i].Item2),new(35,stations[i].Item1,stations[i].Item2));
            var b=new RuledBoundary.Line($"fold:{i}:north",new(-35,stations[i+1].Item1,stations[i+1].Item2),new(35,stations[i+1].Item1,stations[i+1].Item2));
            var ir=new RuledSurfaceIr($"folded-canopy:{i}",RuledConstructionKind.RuledSurface,a,b,new(a.StableId,"showcase:folded-canopy","south"),new(b.StableId,"showcase:folded-canopy","north"));
            panels.Add(PanelFactory.FromRuled(ir,thickness:1.2,material:"Aluminum").Panel!);
        }
        var mates=Enumerable.Range(0,panels.Count-1).Select(i=>new PanelMateRequest($"folded-canopy:seam:{i}",panels[i]["North"],panels[i+1]["South"])).ToArray();
        var network=PanelNetworkValidator.Validate(panels,mates);
        return new("panel-showcase:developable-folded-canopy",panels,mates,network);
    }
}
