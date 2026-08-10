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
    IReadOnlyList<BoundaryProvenance> BoundaryProvenance);

public sealed record RuledSurfaceLoweringResult(RuledSurfacePatch? Patch, IReadOnlyList<SurfacingDiagnostic> Diagnostics)
{ public bool IsSuccess => Patch is not null && Diagnostics.All(d => !d.Code.EndsWith("invalid", StringComparison.Ordinal)); }

public static class RuledSurfaceLowering
{
    public static RuledSurfaceLoweringResult Lower(RuledSurfaceIr ir)
    {
        ArgumentNullException.ThrowIfNull(ir);
        if (ir.BoundaryA is RuledBoundary.Line a && ir.BoundaryB is RuledBoundary.Line b)
        {
            if ((a.End - a.Start).Length <= 1e-12 || (b.End - b.Start).Length <= 1e-12)
                return Failure("surfacing-boundary-invalid", "Ruled line boundaries must have non-zero length.");
            var surface = Bilinear(a.Start, a.End, b.Start, b.End);
            return Success(ir, SurfaceGeometry.FromBSplineSurfaceWithKnots(surface), (u, v) => Lerp(Lerp(a.Start, a.End, u), Lerp(b.Start, b.End, u), v));
        }
        if (ir.BoundaryA is RuledBoundary.Circle c0 && ir.BoundaryB is RuledBoundary.Circle c1)
        {
            var axis = c0.Normal.ToVector();
            var separation = c1.Center - c0.Center;
            if (c0.Radius <= 0 || c1.Radius <= 0 || axis.Cross(c1.Normal.ToVector()).Length > 1e-10
                || axis.Cross(separation).Length > 1e-10 || double.Abs(separation.Dot(axis)) <= 1e-12)
                return Failure("surfacing-boundary-incompatible", "M0 circle boundaries must be positive, coaxial, parallel, and separated.");
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
            return Success(ir, exact, (u, v) => Lerp(c0.Center + CircleOffset(c0, u), c1.Center + CircleOffset(c1, u), v));
        }
        return Failure("surfacing-boundary-incompatible", "M0 requires line-line or coaxial circle-circle boundary families.");
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
    private static Vector3D CircleOffset(RuledBoundary.Circle c, double u) =>
        c.ReferenceAxis.ToVector() * (c.Radius * double.Cos(u * 2 * double.Pi)) + c.Normal.ToVector().Cross(c.ReferenceAxis.ToVector()) * (c.Radius * double.Sin(u * 2 * double.Pi));
    private static Point3D Lerp(Point3D a, Point3D b, double t) => a + (b - a) * System.Math.Clamp(t, 0, 1);
    private static RuledSurfaceLoweringResult Success(RuledSurfaceIr ir, SurfaceGeometry surface, Func<double,double,Point3D> evaluate) =>
        new(new(ir, surface, evaluate, [ir.ProvenanceA, ir.ProvenanceB]), []);
    private static RuledSurfaceLoweringResult Failure(string code, string message) => new(null, [new(code, message)]);
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
