using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Semantics;
using Aetheris.Geometry;

namespace Aetheris.Piping;

public sealed record PipeSection(double OuterDiameter, double? WallThickness = null)
{ public bool IsSolid => WallThickness is null; }
public sealed record PipeRoute(string StableId, double InletLength, double BendRadius, double BendAngleRadians, double OutletLength, PipeSection Section);
public abstract record PipeRouteElement(string StableId)
{
    public sealed record Straight(string Id, Point3D Start, Point3D End) : PipeRouteElement(Id);
    public sealed record PlanarCircularBend(string Id, Point3D Center, double Radius, double AngleRadians) : PipeRouteElement(Id);
}
public sealed record PipeRouteIr(string StableId,IReadOnlyList<PipeRouteElement> Elements,string FramePolicy,string BoundaryProvenance)
{
    /// <summary>Ordered authored centerline pieces; Piping retains ownership of route intent.</summary>
    public IReadOnlyList<BoundedParametricCurve3> CenterlineCurves { get; } = Elements.Select((element, index) => element switch
    {
        PipeRouteElement.Straight line => BoundedParametricCurve3.LineSegment(
            line.StableId, line.Start, line.End, BoundaryProvenance, StableId, isGenerated: true),
        PipeRouteElement.PlanarCircularBend bend => BoundedParametricCurve3.FromCurveGeometry(
            bend.StableId,
            CurveGeometry.FromCircle(new Circle3Curve(bend.Center, Direction3D.Create(new(0, 0, 1)), bend.Radius, Direction3D.Create(new(0, -1, 0)))),
            0d, bend.AngleRadians, BoundaryProvenance, StableId, isGenerated: true),
        _ => throw new NotSupportedException($"Route element {index} has no bounded curve adapter.")
    }).ToArray();
}
public sealed record PipingDiagnostic(string Code,string Message);
public sealed record PipeRouteResult(BrepBody? Body,SemanticValue? Semantics,PipeRouteIr? Ir,IReadOnlyList<PipingDiagnostic> Diagnostics)
{ public bool IsSuccess=>Body is not null&&Semantics is not null&&Diagnostics.Count==0; }

/// <summary>M0 exact route: +X straight, one +90-degree XY bend, then +Y straight.</summary>
public static class PipeRouteLowering
{
    public const string CircularFramePolicy="Circular section: section phase is transported by the route plane normal; twist is immaterial and seam phase is deterministic.";
    public static PipeRouteResult Lower(PipeRoute route)
    {
        var errors=Validate(route); if(errors.Count>0)return new(null,null,null,errors);
        var r=route.Section.OuterDiameter/2;var l0=route.InletLength;var br=route.BendRadius;var l1=route.OutletLength;
        var c0=new Point3D(0,0,0);var c1=new Point3D(l0,0,0);var bc=new Point3D(l0,br,0);var c2=new Point3D(l0+br,br,0);var c3=new Point3D(l0+br,br+l1,0);
        var ir=new PipeRouteIr(route.StableId,[new PipeRouteElement.Straight(route.StableId+":straight-in",c0,c1),new PipeRouteElement.PlanarCircularBend(route.StableId+":bend-0",bc,br,route.BendAngleRadians),new PipeRouteElement.Straight(route.StableId+":straight-out",c2,c3)],CircularFramePolicy,"centerline and four cross-section rings");
        var builder=new TopologyBuilder();var rv=Enumerable.Range(0,4).Select(_=>builder.AddVertex()).ToArray();
        var rings=rv.Select(v=>builder.AddEdge(v,v)).ToArray();var seams=new[]{builder.AddEdge(rv[0],rv[1]),builder.AddEdge(rv[1],rv[2]),builder.AddEdge(rv[2],rv[3])};
        var inlet=AddFace(builder,[Use.F(rings[0])]);
        var side0=AddFace(builder,[Use.F(seams[0]),Use.F(rings[1]),Use.R(seams[0]),Use.R(rings[0])]);
        var bend=AddFace(builder,[Use.F(seams[1]),Use.F(rings[2]),Use.R(seams[1]),Use.R(rings[1])]);
        var side1=AddFace(builder,[Use.F(seams[2]),Use.F(rings[3]),Use.R(seams[2]),Use.R(rings[2])]);
        var outlet=AddFace(builder,[Use.R(rings[3])]);var shell=builder.AddShell([inlet,side0,bend,side1,outlet]);builder.AddBody([shell]);
        var dx=Direction3D.Create(new(1,0,0));var dy=Direction3D.Create(new(0,1,0));var dz=Direction3D.Create(new(0,0,1));var nx=Direction3D.Create(new(-1,0,0));var negY=Direction3D.Create(new(0,-1,0));
        var centers=new[]{c0,c1,c2,c3};var normals=new[]{dx,dx,dy,dy};var refs=new[]{negY,negY,dx,dx};
        var points=new[]{c0+negY.ToVector()*r,c1+negY.ToVector()*r,c2+dx.ToVector()*r,c3+dx.ToVector()*r};
        var geometry=new BrepGeometryStore();var bindings=new BrepBindingModel();var gid=1;
        for(var i=0;i<4;i++){geometry.AddCurve(new(gid),CurveGeometry.FromCircle(new Circle3Curve(centers[i],normals[i],r,refs[i])));bindings.AddEdgeBinding(new(rings[i],new(gid++),new ParameterInterval(0,2*double.Pi)));}
        geometry.AddCurve(new(gid),CurveGeometry.FromLine(new Line3Curve(points[0],dx)));bindings.AddEdgeBinding(new(seams[0],new(gid++),new ParameterInterval(0,l0)));
        geometry.AddCurve(new(gid),CurveGeometry.FromCircle(new Circle3Curve(bc,dz,br+r,negY)));bindings.AddEdgeBinding(new(seams[1],new(gid++),new ParameterInterval(0,double.Pi/2)));
        geometry.AddCurve(new(gid),CurveGeometry.FromLine(new Line3Curve(points[2],dy)));bindings.AddEdgeBinding(new(seams[2],new(gid++),new ParameterInterval(0,l1)));
        var sid=1;geometry.AddSurface(new(sid),SurfaceGeometry.FromPlane(new PlaneSurface(c0,nx,negY)));bindings.AddFaceBinding(new(inlet,new(sid++)));
        geometry.AddSurface(new(sid),SurfaceGeometry.FromCylinder(new CylinderSurface(c0,dx,r,negY)));bindings.AddFaceBinding(new(side0,new(sid++)));
        geometry.AddSurface(new(sid),SurfaceGeometry.FromTorus(new TorusSurface(bc,dz,br,r,negY)));bindings.AddFaceBinding(new(bend,new(sid++)));
        geometry.AddSurface(new(sid),SurfaceGeometry.FromCylinder(new CylinderSurface(c2,dy,r,dx)));bindings.AddFaceBinding(new(side1,new(sid++)));
        geometry.AddSurface(new(sid),SurfaceGeometry.FromPlane(new PlaneSurface(c3,dy,dx)));bindings.AddFaceBinding(new(outlet,new(sid++)));
        var vertexPoints=rv.Select((v,i)=>(v,points[i])).ToDictionary(x=>x.v,x=>x.Item2);var body=new BrepBody(builder.Model,geometry,bindings,vertexPoints);
        var valid=BrepBindingValidator.Validate(body,true);if(!valid.IsSuccess)return new(null,null,ir,valid.Diagnostics.Select(d=>new PipingDiagnostic("piping-brep-invalid",d.Message)).ToArray());
        var semantics=CreateSemantics(route,body,c0,c1,c2,c3,dx,dy);var semanticErrors=SemanticValueValidator.Validate(semantics);
        return semanticErrors.Count==0?new(body,semantics,ir,[]):new(null,semantics,ir,semanticErrors.Select(d=>new PipingDiagnostic(d.Code,d.Message)).ToArray());
    }

    public static (BrepBody? Body, IReadOnlyList<PipingDiagnostic> Diagnostics) LowerStraight(double length, PipeSection section)
    {
        if (!double.IsFinite(length) || length <= 0 || !double.IsFinite(section.OuterDiameter) || section.OuterDiameter <= 0)
            return (null, [new("piping-route-invalid", "Straight PathPipe requires positive finite length and diameter.")]);
        if (!section.IsSolid) return (null, [new("piping-wall-not-supported", "M0 straight PathPipe admits solid circular section only.")]);
        var frame = new ExtrudeFrame3D(Point3D.Origin, Direction3D.Create(new Vector3D(0, 1, 0)), Direction3D.Create(new Vector3D(0, 0, 1)));
        var axis = new RevolveAxis3D(Point3D.Origin, new Vector3D(1, 0, 0));
        var result = BrepRevolve.Create([new(section.OuterDiameter / 2, 0), new(section.OuterDiameter / 2, length)], frame, axis);
        return result.IsSuccess ? (result.Value, []) : (null, result.Diagnostics.Select(d => new PipingDiagnostic("piping-brep-invalid", d.Message)).ToArray());
    }
    private static List<PipingDiagnostic> Validate(PipeRoute route)
    {var d=new List<PipingDiagnostic>();if(string.IsNullOrWhiteSpace(route.StableId)||route.InletLength<=0||route.OutletLength<=0||!double.IsFinite(route.InletLength)||!double.IsFinite(route.OutletLength))d.Add(new("piping-route-invalid","Route identity and positive finite straight lengths are required."));if(!double.IsFinite(route.Section.OuterDiameter)||route.Section.OuterDiameter<=0)d.Add(new("piping-route-invalid","Outer diameter must be finite and positive."));if(!double.IsFinite(route.BendRadius)||route.BendRadius<=route.Section.OuterDiameter/2)d.Add(new("piping-bend-radius-invalid","Centerline bend radius must exceed the pipe outer radius."));if(double.Abs(route.BendAngleRadians-double.Pi/2)>1e-12)d.Add(new("piping-route-invalid","M0 admits one positive 90-degree planar bend."));if(route.Section.WallThickness is not null)d.Add(new("piping-wall-not-supported","M0 exact route materialization admits solid circular section only; wall metadata is reserved."));return d;}
    private static SemanticValue CreateSemantics(PipeRoute route,BrepBody body,Point3D c0,Point3D bendIn,Point3D bendOut,Point3D c3,Direction3D dx,Direction3D dy)
    {SemanticSourceSpan gen=SemanticSourceSpan.Generated("Aetheris.Piping");var members=new[]{Point("inlet",c0),Axis("inletAxis",c0,dx),Point("outlet",c3),Axis("outletAxis",c3,dy),Point("bendStart",bendIn),Point("bendEnd",bendOut),Dimension("diameter",route.Section.OuterDiameter),new SemanticValue(route.StableId+":centerline",new("PipeCenterline"),bindings:[new ConstructionIdentityBinding(route.StableId+":centerline")],provenance:[new("PipingLowering",route.StableId,"exact line-arc-line centerline",gen)],generatedSourceSpan:gen,exposedName:"centerline")};return new(route.StableId,new("PipeRoute"),[new BodyCapability(),new ExactGeometryCapability(),new SelectableCapability()],[new ExactBrepBodyBinding(body,route.StableId+":body")],members,[new("PipingLowering",route.StableId,CircularFramePolicy,gen)],generatedSourceSpan:gen);
        SemanticValue Point(string n,Point3D p)=>new(route.StableId+":"+n,new("Point3"),[new PointCapability()],[new ExactPointBinding(p.X,p.Y,p.Z,route.StableId+":"+n)],provenance:[new("PipingLowering",route.StableId,n,gen)],generatedSourceSpan:gen,exposedName:n);
        SemanticValue Axis(string n,Point3D p,Direction3D a)=>new(route.StableId+":"+n,new("Axis"),[new AxisCapability()],[new ExactAxisBinding(p.X,p.Y,p.Z,a.X,a.Y,a.Z,route.StableId+":"+n)],provenance:[new("PipingLowering",route.StableId,n,gen)],generatedSourceSpan:gen,exposedName:n);
        SemanticValue Dimension(string n,double value)=>new(route.StableId+":"+n,new("Length"),[new DimensionalCapability()],[new TolerancedDimensionBinding(value,0,0,"mm",route.StableId+":"+n)],provenance:[new("PipingLowering",route.StableId,n,gen)],generatedSourceSpan:gen,exposedName:n);}
    private readonly record struct Use(EdgeId Edge,bool Reverse){public static Use F(EdgeId e)=>new(e,false);public static Use R(EdgeId e)=>new(e,true);}
    private static FaceId AddFace(TopologyBuilder b,IReadOnlyList<Use> uses){var loop=b.AllocateLoopId();var ids=uses.Select(_=>b.AllocateCoedgeId()).ToArray();for(var i=0;i<ids.Length;i++)b.AddCoedge(new Coedge(ids[i],uses[i].Edge,loop,ids[(i+1)%ids.Length],ids[(i+ids.Length-1)%ids.Length],uses[i].Reverse));b.AddLoop(new Loop(loop,ids));return b.AddFace([loop]);}
}

public static class StandardPipeElbowTemplate
{public static PipeRoute Create(string stableId,double nominalDiameter,double straightLength,double bendRadius)=>new(stableId,straightLength,bendRadius,double.Pi/2,straightLength,new(nominalDiameter));}
