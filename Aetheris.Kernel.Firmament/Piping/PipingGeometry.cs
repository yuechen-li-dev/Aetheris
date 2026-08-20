using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.StandardLibrary.Materials;

namespace Aetheris.Kernel.Firmament.Piping;

internal static class PipingGeometry
{
    private static readonly ToleranceContext Tolerance = ToleranceContext.Default;

    public static KernelResult<BrepBody> Pipe(Point3D start, Point3D end, PipePolicyIr policy)
    {
        var delta = end - start;
        if (delta.Length <= Tolerance.Linear) return Fail("piping-pipe-segment-zero-length");
        var z = Direction3D.Create(delta);
        var reference = Math.Abs(z.ToVector().Z) < .9 ? new Vector3D(0,0,1) : new Vector3D(0,1,0);
        var x = Direction3D.Create(reference.Cross(z.ToVector()));
        var y = Direction3D.Create(z.ToVector().Cross(x.ToVector()));
        var outer = policy.OuterDiameterMm / 2d;
        var inner = policy.InnerDiameterMm / 2d;
        var loops = new List<LineArcProfileLoop2D> { new([new LineArcFullCircle2D((0,0), outer)], false) };
        if (inner > Tolerance.Linear) loops.Add(new([new LineArcFullCircle2D((0,0), inner)], true));
        var plane = new ConstructionPlane("piping-pipe-frame", "route-segment", start, x, y, z, "piping-route", "semantic-pipe-segment");
        var plan = ProfileExtrusionBRepPlanner.TryPlan(new(loops, delta.Length, plane, 0, delta.Length));
        if (!plan.Succeeded || plan.Plan is null) return Fail("piping-pipe-plan-failed: " + string.Join("; ", plan.Diagnostics));
        var materialized = ProfileExtrusionBRepMaterializer.TryMaterialize(plan.Plan);
        return materialized.Succeeded && materialized.Body is not null
            ? KernelResult<BrepBody>.Success(materialized.Body)
            : Fail("piping-pipe-materialization-failed: " + string.Join("; ", materialized.Diagnostics));
    }

    public static KernelResult<BrepBody> Elbow(Point3D corner, Vector3D incoming, Vector3D outgoing, PipePolicyIr policy, double bendRadius)
    {
        var u = Unit(incoming); var v = Unit(outgoing);
        if (Math.Abs(u.Dot(v)) > Tolerance.Angular) return Fail("piping-elbow-nonorthogonal");
        var built = CanonicalHollowElbow(policy, bendRadius);
        if (!built.IsSuccess || built.Value is null) return built;

        var worldX = -v; var worldY = u; var worldZ = worldX.Cross(worldY);
        var center = corner - u * bendRadius + v * bendRadius;
        var transform = Transform3D.FromRowMajor([
            worldX.X,worldX.Y,worldX.Z,0,
            worldY.X,worldY.Y,worldY.Z,0,
            worldZ.X,worldZ.Y,worldZ.Z,0,
            center.X,center.Y,center.Z,1]);
        return KernelResult<BrepBody>.Success(FirmasmAssemblyExecutor.TransformBody(built.Value, transform));
    }

    public static KernelResult<BrepBody> Proxy(KeepOutIr keepOut)
    {
        var size = keepOut.Maximum - keepOut.Minimum;
        var box = BrepPrimitives.CreateBox(size.X, size.Y, size.Z);
        if (!box.IsSuccess || box.Value is null) return box;
        var center = new Point3D((keepOut.Minimum.X+keepOut.Maximum.X)/2, (keepOut.Minimum.Y+keepOut.Maximum.Y)/2, (keepOut.Minimum.Z+keepOut.Maximum.Z)/2);
        return KernelResult<BrepBody>.Success(FirmasmAssemblyExecutor.TransformBody(box.Value, Transform3D.CreateTranslation(center - new Point3D(0,0,0))));
    }

    public static (Point3D Start, Point3D End) TrimmedRun(IReadOnlyList<RouteAnchorIr> anchors, int segment, double bendRadius)
    {
        var a=anchors[segment].Point;var b=anchors[segment+1].Point;var direction=Unit(b-a);
        var start=segment==0?a:a+direction*bendRadius;
        var end=segment==anchors.Count-2?b:b-direction*bendRadius;
        return(start,end);
    }

    private static Vector3D Unit(Vector3D v) => v / v.Length;
    private static KernelResult<BrepBody> CanonicalHollowElbow(PipePolicyIr policy,double bendRadius)
    {
        var ro=policy.RadiusMm;var ri=policy.InnerDiameterMm/2;
        if(ri<=Tolerance.Linear||bendRadius<=ro+Tolerance.Linear)return Fail("piping-elbow-policy-invalid");
        var b=new TopologyBuilder();var os=b.AddVertex();var oe=b.AddVertex();var ins=b.AddVertex();var ine=b.AddVertex();
        var osr=b.AddEdge(os,os);var oer=b.AddEdge(oe,oe);var isr=b.AddEdge(ins,ins);var ier=b.AddEdge(ine,ine);var oseam=b.AddEdge(os,oe);var iseam=b.AddEdge(ins,ine);
        var outer=AddFace(b,[[(oseam,false),(oer,false),(oseam,true),(osr,true)]]);
        var inner=AddFace(b,[[(iseam,true),(isr,false),(iseam,false),(ier,true)]]);
        var start=AddFace(b,[[(osr,false)],[(isr,true)]]);var end=AddFace(b,[[(oer,true)],[(ier,false)]]);
        var shell=b.AddShell([outer,inner,start,end]);b.AddBody([shell]);
        var points=new Dictionary<VertexId,Point3D>{{os,new(bendRadius,0,ro)},{oe,new(0,bendRadius,ro)},{ins,new(bendRadius,0,ri)},{ine,new(0,bendRadius,ri)}};
        var g=new BrepGeometryStore();var bindings=new BrepBindingModel();var cy=Direction3D.Create(new Vector3D(0,1,0));var nx=Direction3D.Create(new Vector3D(-1,0,0));var z=Direction3D.Create(new Vector3D(0,0,1));var x=Direction3D.Create(new Vector3D(1,0,0));
        AddCircle(osr,new(bendRadius,0,0),cy,ro,z,0,2*Math.PI,1);AddCircle(oer,new(0,bendRadius,0),nx,ro,z,0,2*Math.PI,2);AddCircle(isr,new(bendRadius,0,0),cy,ri,z,0,2*Math.PI,3);AddCircle(ier,new(0,bendRadius,0),nx,ri,z,0,2*Math.PI,4);AddCircle(oseam,new(0,0,ro),z,bendRadius,x,0,Math.PI/2,5);AddCircle(iseam,new(0,0,ri),z,bendRadius,x,0,Math.PI/2,6);
        AddSurface(outer,SurfaceGeometry.FromTorus(new TorusSurface(new(0,0,0),z,bendRadius,ro,x)),1);AddSurface(inner,SurfaceGeometry.FromTorus(new TorusSurface(new(0,0,0),z,bendRadius,ri,x)),2);AddSurface(start,SurfaceGeometry.FromPlane(new PlaneSurface(new(bendRadius,0,0),Direction3D.Create(new Vector3D(0,-1,0)),z)),3);AddSurface(end,SurfaceGeometry.FromPlane(new PlaneSurface(new(0,bendRadius,0),nx,z)),4);
        var body=new BrepBody(b.Model,g,bindings,points);var valid=BrepBindingValidator.Validate(body,true);return valid.IsSuccess?KernelResult<BrepBody>.Success(body):KernelResult<BrepBody>.Failure(valid.Diagnostics);
        void AddCircle(EdgeId edge,Point3D center,Direction3D normal,double radius,Direction3D axis,double from,double to,int id){var cid=new CurveGeometryId(id);g.AddCurve(cid,CurveGeometry.FromCircle(new Circle3Curve(center,normal,radius,axis)));bindings.AddEdgeBinding(new(edge,cid,new ParameterInterval(from,to)));}
        void AddSurface(FaceId face,SurfaceGeometry surface,int id){var sid=new SurfaceGeometryId(id);g.AddSurface(sid,surface);bindings.AddFaceBinding(new(face,sid));}
    }
    private static FaceId AddFace(TopologyBuilder b,IReadOnlyList<IReadOnlyList<(EdgeId Edge,bool Reverse)>> loops)=>b.AddFace(loops.Select(uses=>{var loop=b.AllocateLoopId();var ids=uses.Select(_=>b.AllocateCoedgeId()).ToArray();for(var i=0;i<ids.Length;i++)b.AddCoedge(new(ids[i],uses[i].Edge,loop,ids[(i+1)%ids.Length],ids[(i+ids.Length-1)%ids.Length],uses[i].Reverse));b.AddLoop(new Loop(loop,ids));return loop;}).ToArray());
    private static KernelResult<BrepBody> Fail(string message) => KernelResult<BrepBody>.Failure([new(
        Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Core.Diagnostics.KernelDiagnosticSeverity.Error, message, "Piping.X3.Geometry")]);
}
