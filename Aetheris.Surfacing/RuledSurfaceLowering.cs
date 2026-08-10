using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Surfacing;

public sealed record RuledSurfacePatch(
    RuledSurfaceIr Ir,
    SurfaceGeometry ExactSurface,
    Func<double, double, Point3D> Evaluate,
    IReadOnlyList<BoundaryProvenance> BoundaryProvenance,
    ParametricDomain Domain,
    DevelopabilityEvidence Developability,
    SurfaceMaterializationKind MaterializationKind,
    ApproximationCertificate? ApproximationCertificate);

public sealed record RuledSurfaceLoweringResult(RuledSurfacePatch? Patch, IReadOnlyList<SurfacingDiagnostic> Diagnostics)
{ public bool IsSuccess => Patch is not null && Diagnostics.All(d => !d.Code.EndsWith("invalid", StringComparison.Ordinal)); }

public static class RuledSurfaceLowering
{
    public static RuledSurfaceLoweringResult Lower(RuledSurfaceIr ir)
    {
        ArgumentNullException.ThrowIfNull(ir);
        if (!TryBoundaryEvaluator(ir.BoundaryA, out var evaluateA, out var tangentA, out var diagnosticA)) return Failure("surfacing-boundary-invalid", diagnosticA!);
        if (!TryBoundaryEvaluator(ir.BoundaryB, out var evaluateB, out var tangentB, out var diagnosticB)) return Failure("surfacing-boundary-invalid", diagnosticB!);
        Point3D Evaluate(double u,double v)=>Lerp(evaluateA!(u),evaluateB!(u),v);
        var developability=ClassifyDevelopability(evaluateA!,evaluateB!,tangentA!,tangentB!);
        if (ir.BoundaryA is RuledBoundary.Line a && ir.BoundaryB is RuledBoundary.Line b)
        {
            var surface = Bilinear(a.Start, a.End, b.Start, b.End);
            return Success(ir, SurfaceGeometry.FromBSplineSurfaceWithKnots(surface), Evaluate,developability,SurfaceMaterializationKind.ExactPolynomialBSpline,null);
        }
        if (ir.BoundaryA is RuledBoundary.Circle c0 && ir.BoundaryB is RuledBoundary.Circle c1
            && c0.Normal.ToVector().Cross(c1.Normal.ToVector()).Length <= 1e-10
            && c0.Normal.ToVector().Cross(c1.Center-c0.Center).Length <= 1e-10
            && double.Abs((c1.Center-c0.Center).Dot(c0.Normal.ToVector())) > 1e-12
            && c0.ReferenceAxis.ToVector().Cross(c1.ReferenceAxis.ToVector()).Length <= 1e-10
            && c0.ReferenceAxis.ToVector().Dot(c1.ReferenceAxis.ToVector()) > 0)
        {
            var axis = c0.Normal.ToVector();
            var separation = c1.Center - c0.Center;
            SurfaceGeometry exact;
            if (double.Abs(c0.Radius - c1.Radius) <= 1e-12)
                exact = SurfaceGeometry.FromCylinder(new CylinderSurface(c0.Center, c0.Normal, c0.Radius, c0.ReferenceAxis));
            else
            {
                var h = separation.Dot(axis);
                var slope = (c1.Radius - c0.Radius) / h;
                var apex = c0.Center - axis * (c0.Radius / slope);
                var coneAxis = slope > 0 ? c0.Normal : Direction3D.Create(-axis);
                exact = SurfaceGeometry.FromCone(new ConeSurface(apex, coneAxis, double.Atan(double.Abs(slope)), c0.ReferenceAxis));
            }
            return Success(ir, exact, Evaluate,developability,SurfaceMaterializationKind.ExactAnalytic,null);
        }
        var approximation=MaterializeRuled(ir.StableId,Evaluate,9);
        return Success(ir,SurfaceGeometry.FromBSplineSurfaceWithKnots(approximation.Surface),Evaluate,developability,
            SurfaceMaterializationKind.ApproximatedNonRationalBSpline,approximation.Certificate);
    }

    public static RuledSurfaceIr Saddle(string stableId, double halfX, double halfY, double rise)
    {
        if (halfX <= 0 || halfY <= 0 || !double.IsFinite(rise)) throw new ArgumentOutOfRangeException(nameof(halfX));
        var a = new RuledBoundary.Line(stableId + ":y-", new(-halfX, -halfY, rise), new(halfX, -halfY, -rise));
        var b = new RuledBoundary.Line(stableId + ":y+", new(-halfX, halfY, -rise), new(halfX, halfY, rise));
        return new(stableId, RuledConstructionKind.RuledSurface, a, b,
            new(a.StableId, stableId, "boundary-a"), new(b.StableId, stableId, "boundary-b"));
    }

    internal static BSplineSurfaceWithKnots Bilinear(Point3D a0, Point3D a1, Point3D b0, Point3D b1) =>
        new(1, 1, [[a0, b0], [a1, b1]], "RULED_SURFACE", false, false, false, [2, 2], [2, 2], [0, 1], [0, 1], "UNSPECIFIED");
    private static Vector3D CircleOffset(RuledBoundary.Circle c, double u) => ArcOffset(c.Normal,c.ReferenceAxis,c.Radius,u*2d*double.Pi);
    private static Vector3D ArcOffset(Direction3D normal,Direction3D reference,double radius,double angle) =>
        reference.ToVector()*(radius*double.Cos(angle))+normal.ToVector().Cross(reference.ToVector())*(radius*double.Sin(angle));
    internal static Point3D Lerp(Point3D a, Point3D b, double t) => a + (b - a) * System.Math.Clamp(t, 0, 1);
    private static RuledSurfaceLoweringResult Success(RuledSurfaceIr ir, SurfaceGeometry surface, Func<double,double,Point3D> evaluate,
        DevelopabilityEvidence developability,SurfaceMaterializationKind materialization,ApproximationCertificate? certificate) =>
        new(new(ir, surface, evaluate, [ir.ProvenanceA, ir.ProvenanceB],new(new(0,1),new(0,1)),developability,materialization,certificate), []);
    private static RuledSurfaceLoweringResult Failure(string code, string message) => new(null, [new(code, message)]);

    internal static bool TryBoundaryEvaluator(RuledBoundary boundary,out Func<double,Point3D>? evaluate,out Func<double,Vector3D>? tangent,out string? diagnostic)
    {
        evaluate=null;tangent=null;diagnostic=null;
        switch(boundary)
        {
            case RuledBoundary.Line line when (line.End-line.Start).Length>1e-12:
                evaluate=u=>Lerp(line.Start,line.End,u);tangent=_=>line.End-line.Start;return true;
            case RuledBoundary.Arc arc when arc.Radius>0&&double.IsFinite(arc.Radius)&&double.IsFinite(arc.StartAngleRadians)&&double.IsFinite(arc.SweepAngleRadians)&&double.Abs(arc.SweepAngleRadians)>1e-12:
                evaluate=u=>arc.Center+ArcOffset(arc.Normal,arc.ReferenceAxis,arc.Radius,arc.StartAngleRadians+u*arc.SweepAngleRadians);
                tangent=u=>arc.Normal.ToVector().Cross(ArcOffset(arc.Normal,arc.ReferenceAxis,arc.Radius,arc.StartAngleRadians+u*arc.SweepAngleRadians))*arc.SweepAngleRadians;return true;
            case RuledBoundary.Circle circle when circle.Radius>0&&double.IsFinite(circle.Radius):
                evaluate=u=>circle.Center+CircleOffset(circle,u);tangent=u=>circle.Normal.ToVector().Cross(CircleOffset(circle,u))*(2d*double.Pi);return true;
            case RuledBoundary.BSpline spline:
                var span=spline.Curve.DomainEnd-spline.Curve.DomainStart;if(span<=1e-12){diagnostic="B-spline boundary domain must be non-empty.";return false;}
                evaluate=u=>spline.Curve.Evaluate(spline.Curve.DomainStart+System.Math.Clamp(u,0,1)*span);tangent=u=>spline.Curve.EvaluateTangent(spline.Curve.DomainStart+System.Math.Clamp(u,0,1)*span)*span;return true;
            default:diagnostic=$"Boundary '{boundary.StableId}' is degenerate or has non-finite parameters.";return false;
        }
    }

    private static DevelopabilityEvidence ClassifyDevelopability(Func<double,Point3D> a,Func<double,Point3D> b,Func<double,Vector3D> da,Func<double,Vector3D> db)
    {
        var maximum=0d;const int count=17;
        for(var i=0;i<count;i++){var u=i/(double)(count-1);var derivative=da(u);var ruling=b(u)-a(u);var deltaDerivative=db(u)-derivative;var scale=derivative.Length*ruling.Length*deltaDerivative.Length;var normalized=scale<=1e-15?0d:double.Abs(derivative.Dot(ruling.Cross(deltaDerivative)))/scale;maximum=double.Max(maximum,normalized);}
        var kind=maximum<=1e-9?DevelopabilityKind.Developable:DevelopabilityKind.NonDevelopable;
        return new(kind,"normalized scalar triple product C0'(u) · (ruling(u) × ruling'(u))",maximum,count,
            kind==DevelopabilityKind.Developable?"Sampled rulings satisfy the ruled developability condition.":"Sampled rulings exhibit non-zero distribution parameter; ruled does not imply developable.");
    }

    private static ParametricMaterialization MaterializeRuled(string id,Func<double,double,Point3D> evaluate,int count)
    {
        var controls=new Point3D[count][];for(var i=0;i<count;i++){controls[i]=new Point3D[2];controls[i][0]=evaluate(i/(double)(count-1),0);controls[i][1]=evaluate(i/(double)(count-1),1);}
        var values=Enumerable.Range(0,count).Select(i=>i/(double)(count-1)).ToArray();var mult=Enumerable.Repeat(1,count).ToArray();mult[0]=2;mult[^1]=2;
        var spline=new BSplineSurfaceWithKnots(1,1,controls,"RULED_SURFACE",false,false,false,mult,[2,2],values,[0,1],"UNSPECIFIED");var residual=0d;
        for(var i=0;i<count-1;i++){var u=(i+.5)/(count-1);for(var j=0;j<=4;j++){var v=j/4d;residual=double.Max(residual,(evaluate(u,v)-spline.Evaluate(u,v)).Length);}}
        if(residual>0.1&&count<129)return MaterializeRuled(id,evaluate,System.Math.Min(129,count*2-1));
        if(residual>0.1)throw new InvalidOperationException($"Ruled materialization did not meet 0.1 mm within the bounded 129-sample grid; sampled residual was {residual:G6} mm.");
        return new(spline,new(0.1,residual,null,count,2,"adaptive uniform native-parameter samples; residuals at span midpoints; normal deviation not sampled",id),SurfaceMaterializationKind.ApproximatedNonRationalBSpline);
    }
}

/// <summary>Closed exact panel materialization for line-line ruled patches; preserves the ruled support surfaces.</summary>
public static class RuledSurfacePanelMaterializer
{
    public static (BrepBody? Body, IReadOnlyList<SurfacingDiagnostic> Diagnostics) Materialize(RuledSurfaceIr ir, double thickness)
    {
        if (!double.IsFinite(thickness) || thickness <= 0) return (null, [new("surfacing-panel-thickness-invalid", "Panel thickness must be finite and positive.")]);
        if (ir.BoundaryA is not RuledBoundary.Line a || ir.BoundaryB is not RuledBoundary.Line b)
            return (null, [new("surfacing-boundary-incompatible", "M0 panel materialization admits line-line ruled surfaces only.")]);
        var bottom = new[] { a.Start, a.End, b.End, b.Start };
        var dz = new Vector3D(0, 0, thickness);
        var top = bottom.Select(p => p + dz).ToArray();
        var builder = new TopologyBuilder();
        var vb = bottom.Select(_ => builder.AddVertex()).ToArray(); var vt = top.Select(_ => builder.AddVertex()).ToArray();
        var eb = Enumerable.Range(0,4).Select(i => builder.AddEdge(vb[i], vb[(i+1)%4])).ToArray();
        var et = Enumerable.Range(0,4).Select(i => builder.AddEdge(vt[i], vt[(i+1)%4])).ToArray();
        var ev = Enumerable.Range(0,4).Select(i => builder.AddEdge(vb[i], vt[i])).ToArray();
        var faces = new List<FaceId> { AddFace(builder, eb.Select(Use.F).ToArray()), AddFace(builder, et.Reverse().Select(Use.R).ToArray()) };
        for (var i=0;i<4;i++) faces.Add(AddFace(builder,[Use.F(eb[i]),Use.F(ev[(i+1)%4]),Use.R(et[i]),Use.R(ev[i])]));
        var shell=builder.AddShell(faces); builder.AddBody([shell]);
        var geometry=new BrepGeometryStore(); var bindings=new BrepBindingModel(); var curve=1;
        BindLines(eb,bottom); BindLines(et,top); BindLines(ev,Enumerable.Range(0,4).SelectMany(i=>new[]{bottom[i],top[i]}).ToArray());
        var sid=1;
        geometry.AddSurface(new(sid), SurfaceGeometry.FromBSplineSurfaceWithKnots(RuledSurfaceLowering.Bilinear(a.Start,a.End,b.Start,b.End))); bindings.AddFaceBinding(new(faces[0],new(sid++),false));
        geometry.AddSurface(new(sid), SurfaceGeometry.FromBSplineSurfaceWithKnots(RuledSurfaceLowering.Bilinear(a.Start+dz,a.End+dz,b.Start+dz,b.End+dz))); bindings.AddFaceBinding(new(faces[1],new(sid++),true));
        for(var i=0;i<4;i++) { var u=bottom[(i+1)%4]-bottom[i]; var normal=Direction3D.Create(u.Cross(dz)); geometry.AddSurface(new(sid),SurfaceGeometry.FromPlane(new PlaneSurface(bottom[i],normal,Direction3D.Create(u)))); bindings.AddFaceBinding(new(faces[i+2],new(sid++))); }
        var points=vb.Select((v,i)=>(v,bottom[i])).Concat(vt.Select((v,i)=>(v,top[i]))).ToDictionary(x=>x.v,x=>x.Item2);
        var body=new BrepBody(builder.Model,geometry,bindings,points); var valid=BrepBindingValidator.Validate(body,true);
        return valid.IsSuccess ? (body,[]) : (null,valid.Diagnostics.Select(d=>new SurfacingDiagnostic("surfacing-brep-invalid",d.Message)).ToArray());

        void BindLines(IReadOnlyList<EdgeId> edges,IReadOnlyList<Point3D> points)
        { for(var i=0;i<edges.Count;i++){var p0=points.Count==8?points[i*2]:points[i];var p1=points.Count==8?points[i*2+1]:points[(i+1)%points.Count];var length=(p1-p0).Length;geometry.AddCurve(new(curve),CurveGeometry.FromLine(new Line3Curve(p0,Direction3D.Create(p1-p0))));bindings.AddEdgeBinding(new(edges[i],new(curve++),new ParameterInterval(0,length)));} }
    }
    private readonly record struct Use(EdgeId Edge,bool Reverse){public static Use F(EdgeId e)=>new(e,false);public static Use R(EdgeId e)=>new(e,true);}
    private static FaceId AddFace(TopologyBuilder b,IReadOnlyList<Use> uses){var loop=b.AllocateLoopId();var ids=uses.Select(_=>b.AllocateCoedgeId()).ToArray();for(var i=0;i<ids.Length;i++)b.AddCoedge(new Coedge(ids[i],uses[i].Edge,loop,ids[(i+1)%ids.Length],ids[(i+ids.Length-1)%ids.Length],uses[i].Reverse));b.AddLoop(new Loop(loop,ids));return b.AddFace([loop]);}
}

public static class RuledCanopyTemplate
{
    public static RuledSurfaceIr Create(string stableId, double width, double depth, double rise) => RuledSurfaceLowering.Saddle(stableId,width/2,depth/2,rise);
}
