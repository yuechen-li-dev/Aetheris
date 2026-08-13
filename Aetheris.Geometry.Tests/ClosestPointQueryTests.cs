using Aetheris.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Geometry.Tests;

public sealed class ClosestPointQueryTests
{
    [Fact]
    public void Point_segment_projection_is_certified_and_clamps_to_authored_domain()
    {
        var segment=Line("line",new(0,0,0),new(10,0,0));
        var result=ClosestPointQuery.Between(new Point3D(12,3,0),segment);
        Assert.Equal(DistanceQueryStatus.Available,result.Status);Assert.Equal(DistanceRelation.Separated,result.Relation);
        Assert.Equal(PredicateEvidenceKind.Certified,result.Evidence);Assert.Equal(double.Sqrt(13),result.ComputedDistance!.Value,12);
        Assert.Equal(10,result.ParameterOnB!.T);Assert.Equal(DistanceQueryPolicy.Default,result.ToleranceUsed);
    }

    [Fact]
    public void Floating_noise_is_raw_within_tolerance_and_never_fake_coincidence()
    {
        var a=Line("a",new(0,0,0),new(10,0,0));var b=Line("b",new(0,0,5e-7),new(10,0,5e-7));
        var loose=ClosestPointQuery.Between(a,b);
        var tight=ClosestPointQuery.Between(a,b,new(){LinearTolerance=1e-8});
        Assert.Equal(5e-7,loose.ComputedDistance!.Value,14);Assert.Equal(1e-6,loose.ToleranceUsed.LinearTolerance);
        Assert.Equal(DistanceRelation.WithinTolerance,loose.Relation);Assert.NotEqual(DistanceRelation.Coincident,loose.Relation);
        Assert.Equal(loose.ComputedDistance,tight.ComputedDistance);Assert.Equal(DistanceRelation.Separated,tight.Relation);
    }

    [Fact]
    public void Same_authored_identity_earns_structural_coincidence_without_direction_claim()
    {
        var a=Line("shared-locus",new(0,0,0),new(1,0,0));var reversed=Line("shared-locus",new(1,0,0),new(0,0,0));
        var result=ClosestPointQuery.Between(a,reversed);
        Assert.Equal(DistanceRelation.Coincident,result.Relation);Assert.Equal(PredicateEvidenceKind.Structural,result.Evidence);
        Assert.Equal(0,result.ComputedDistance);Assert.Equal(0,result.Statistics.CandidateCount);
    }

    [Fact]
    public void Generic_point_patch_considers_interior_boundary_and_corners()
    {
        var patch=Patch("saddle",(u,v)=>new(u,v,u*u-v*v));
        var result=ClosestPointQuery.Between(new Point3D(0,0,2),patch);
        Assert.Equal(DistanceQueryStatus.Available,result.Status);Assert.Equal(double.Sqrt(2),result.ComputedDistance!.Value,10);
        Assert.Equal(PredicateEvidenceKind.ToleranceBounded,result.Evidence);Assert.NotNull(result.DistanceLowerBound);
        Assert.Equal(1,double.Abs(result.ParameterOnB!.U!.Value),12);Assert.Equal(0,result.ParameterOnB.V!.Value,12);
    }

    [Fact]
    public void Generic_bounded_circle_projection_refines_both_global_resolutions()
    {
        var circle=BoundedParametricCurve3.Procedural("circle",new(0,2*double.Pi),t=>(new Point3D(5*double.Cos(t),5*double.Sin(t),0),new Vector3D(-5*double.Sin(t),5*double.Cos(t),0)),"test");
        var result=ClosestPointQuery.Between(new Point3D(7,0,0),circle);
        Assert.Equal(DistanceQueryStatus.Available,result.Status);Assert.Equal(2,result.ComputedDistance!.Value,10);
        Assert.Equal(PredicateEvidenceKind.ToleranceBounded,result.Evidence);
    }

    [Fact]
    public void Generic_curve_curve_curve_patch_and_patch_patch_are_deterministic()
    {
        var parabola=Curve("parabola",t=>new(t,t*t,2));var parabola2=Curve("parabola-2",t=>new(t,t*t,4));
        var plane=Patch("plane",(u,v)=>new(u,v,0));var plane3=Patch("plane-3",(u,v)=>new(u,v,3));
        var cc=ClosestPointQuery.Between(parabola,parabola2);var cp=ClosestPointQuery.Between(parabola,plane);var pp=ClosestPointQuery.Between(plane,plane3);
        Assert.Equal(2,cc.ComputedDistance!.Value,10);Assert.Equal(2,cp.ComputedDistance!.Value,10);Assert.Equal(3,pp.ComputedDistance!.Value,10);
        Assert.All(new[]{cc,cp,pp},r=>Assert.Equal(DistanceQueryStatus.Available,r.Status));
        var repeated=ClosestPointQuery.Between(plane,plane3);
        Assert.Equal(pp.ComputedDistance,repeated.ComputedDistance);Assert.Equal(pp.PointOnA,repeated.PointOnA);
        Assert.Equal(pp.PointOnB,repeated.PointOnB);Assert.Equal(pp.Statistics,repeated.Statistics);
    }

    [Fact]
    public void Panel_edges_dogfood_public_clearance_query()
    {
        var panel=RuledCanopyPanelTemplate.Create("clearance-panel",10,4,0).Panel!;
        var result=ClosestPointQuery.Between(panel["South"].AuthoredCurve,panel["North"].AuthoredCurve);
        Assert.Equal(DistanceQueryStatus.Available,result.Status);Assert.Equal(DistanceRelation.Separated,result.Relation);
        Assert.Equal(PredicateEvidenceKind.Certified,result.Evidence);Assert.Equal(4,result.ComputedDistance!.Value,10);
    }

    [Fact]
    public void Non_finite_generic_evaluation_returns_unknown_with_typed_diagnostic()
    {
        var bad=BoundedParametricCurve3.Procedural("bad",new(-1,1),t=>(new Point3D(double.NaN,0,0),new Vector3D(1,0,0)),"fixture");
        var result=ClosestPointQuery.Between(Point3D.Origin,bad);
        Assert.Equal(DistanceQueryStatus.Unknown,result.Status);Assert.Equal(DistanceRelation.Unknown,result.Relation);
        Assert.Contains(result.Diagnostics,d=>d.Code==GeometryQueryDiagnosticCode.NonFiniteEvaluation);
    }

    [Fact]
    public void Insufficient_whole_domain_budget_returns_unknown_without_a_local_minimum_claim()
    {
        var a=Patch("budget-a",(u,v)=>new(u,v,0));var b=Patch("budget-b",(u,v)=>new(u,v,1));
        var result=ClosestPointQuery.Between(a,b,new(){SubdivisionBudget=16});
        Assert.Equal(DistanceQueryStatus.Unknown,result.Status);Assert.Equal(DistanceRelation.Unknown,result.Relation);
        Assert.Null(result.ComputedDistance);Assert.Contains(result.Diagnostics,d=>d.Code==GeometryQueryDiagnosticCode.SubdivisionBudgetExhausted);
    }

    [Fact]
    public void Clearance_expectation_validates_without_moving_geometry()
    {
        var result=ClosestPointQuery.Between(Line("a",new(0,0,0),new(1,0,0)),Line("b",new(0,0,1),new(1,0,1)));
        Assert.True(new ClearanceExpectation(.9).Evaluate(result).Satisfied);Assert.False(new ClearanceExpectation(1.1).Evaluate(result).Satisfied);
    }

    private static BoundedParametricCurve3 Line(string id,Point3D a,Point3D b)=>BoundedParametricCurve3.LineSegment(id,a,b,"test");
    private static BoundedParametricCurve3 Curve(string id,Func<double,Point3D> f)=>BoundedParametricCurve3.Procedural(id,new(-1,1),t=>(f(t),new Vector3D(1,2*t,0)),"test");
    private static BoundedParametricPatch3 Patch(string id,Func<double,double,Point3D> f)=>BoundedParametricPatch3.Procedural(id,
        new(new(-1,1),new(-1,1)),(u,v)=>new(f(u,v),new Vector3D(1,0,2*u),new Vector3D(0,1,-2*v),null,false),"test");
}
