using Aetheris.Geometry;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Geometry.Tests;

public sealed class DifferentialGeometryTests
{
    private static readonly Direction3D X=Direction3D.Create(new(1,0,0));
    private static readonly Direction3D Z=Direction3D.Create(new(0,0,1));

    [Fact]
    public void AnalyticFamiliesAndExpressionsExposeKnownSecondJets()
    {
        var line=Adapt("line",CurveGeometry.FromLine(new(Point3D.Origin,X)),0,2);AssertVector(line.EvaluateJet2(1).SecondDerivative,0,0,0);
        var circle=Adapt("circle",CurveGeometry.FromCircle(new(Point3D.Origin,Z,2,X)),0,2*double.Pi);AssertVector(circle.EvaluateJet2(0).SecondDerivative,-2,0,0);
        var ellipse=Adapt("ellipse",CurveGeometry.FromEllipse(new(Point3D.Origin,Z,3,2,X)),0,double.Pi);AssertVector(ellipse.EvaluateJet2(0).SecondDerivative,-3,0,0);
        var hyperbola=Adapt("hyperbola",CurveGeometry.FromHyperbola(new(Point3D.Origin,Z,X,2,3,HyperbolaBranch.PositiveAxisU)),-1,1);AssertVector(hyperbola.EvaluateJet2(0).SecondDerivative,2,0,0);
        var splineSupport=new BSpline3Curve(2,[new(0,0,0),new(.5,0,0),new(1,1,0)],[3,3],[0,1],"PARABOLIC_ARC",false,false,"UNSPECIFIED");
        var spline=Adapt("spline",CurveGeometry.FromBSpline(splineSupport),0,1);AssertVector(spline.EvaluateJet2(.4).SecondDerivative,0,2,0);

        var t=CurveExpression.T;var one=CurveExpression.Length(1);
        var parabola=Expression("parabola",new(CurveExpression.Multiply(one,t),CurveExpression.Multiply(one,CurveExpression.Power(t,2)),CurveExpression.Length(0)),-2,2);AssertVector(parabola.EvaluateJet2(1.5).SecondDerivative,0,2,0);
        var sinusoid=Expression("sin",new(CurveExpression.Multiply(one,t),CurveExpression.Multiply(one,CurveExpression.Sin(t)),CurveExpression.Length(0)),-double.Pi,double.Pi);AssertVector(sinusoid.EvaluateJet2(double.Pi/2).SecondDerivative,0,-1,0);
        var helix=Expression("helix",new(CurveExpression.Multiply(one,CurveExpression.Cos(t)),CurveExpression.Multiply(one,CurveExpression.Sin(t)),CurveExpression.Multiply(one,t)),0,2*double.Pi);AssertVector(helix.EvaluateJet2(0).SecondDerivative,-1,0,0);
    }

    [Fact]
    public void CurveCurvatureIsEvidenceAwareAndParameterizationInvariant()
    {
        var line=Adapt("line-k",CurveGeometry.FromLine(new(Point3D.Origin,X)),0,2);var lineK=CurvatureQuery.Curve(line,1);Assert.Equal(0,lineK.Curvature!.Value,12);Assert.True(double.IsPositiveInfinity(lineK.RadiusOfCurvature!.Value));
        var circle=Adapt("circle-k",CurveGeometry.FromCircle(new(Point3D.Origin,Z,4,X)),0,2*double.Pi);var circleK=CurvatureQuery.Curve(circle,.7);Assert.Equal(.25,circleK.Curvature!.Value,12);Assert.Equal(PredicateEvidenceKind.ToleranceBounded,circleK.Evidence);Assert.NotEqual(PredicateEvidenceKind.Certified,circleK.Evidence);
        var t=CurveExpression.T;var one=CurveExpression.Length(1);var scaled=Expression("scaled-circle",new(CurveExpression.Multiply(one,CurveExpression.Cos(CurveExpression.Multiply(CurveExpression.Number(2),t))),CurveExpression.Multiply(one,CurveExpression.Sin(CurveExpression.Multiply(CurveExpression.Number(2),t))),CurveExpression.Length(0)),0,double.Pi);
        Assert.Equal(1,CurvatureQuery.Curve(scaled,.3).Curvature!.Value,11);
        var singular=BoundedParametricCurve3.Procedural("singular-k",new(0,1),x=>(new(x,0,0),Vector3D.Zero),x=>new(new(x,0,0),Vector3D.Zero,Vector3D.Zero,DifferentialSingularityKind.Singular),"fixture");
        Assert.Equal(DifferentialQueryStatus.Unknown,CurvatureQuery.Curve(singular,.5).Status);
    }

    [Fact]
    public void ExpressionPatchesExposeSecondJetsAndClassicalCurvatures()
    {
        var saddle=MathematicalSurfaces.HyperbolicParaboloid("saddle",2,3,4).Patch;var sj=saddle.EvaluateJet2(0,0);AssertVector(sj.Duv,0,0,4);Assert.Equal(0,sj.Duu.Length,12);Assert.True(CurvatureQuery.Patch(saddle,0,0).GaussianCurvature<0);
        var cylinder=MathematicalSurfaces.ParabolicCylinder("parabolic-cylinder",2,3,4).Patch;AssertVector(cylinder.EvaluateJet2(0,0).Duu,0,0,8);
        var bowl=MathematicalSurfaces.EllipticParaboloid("bowl",2,3,4).Patch;var bowlK=CurvatureQuery.Patch(bowl,0,0);Assert.True(bowlK.GaussianCurvature>0);Assert.NotNull(bowlK.K1);Assert.NotNull(bowlK.K2);
        var helicoid=MathematicalSurfaces.Helicoid("helicoid",2,1).Patch;Assert.True(helicoid.EvaluateJet2(.5,.25).Duv.Length>0);

        var plane=Patch("plane",SurfaceExpression.Multiply(SurfaceExpression.Length(2),SurfaceExpression.U),SurfaceExpression.Multiply(SurfaceExpression.Length(3),SurfaceExpression.V),SurfaceExpression.Length(0));var pk=CurvatureQuery.Patch(plane,0,0);Assert.Equal(0,pk.K1!.Value,12);Assert.Equal(0,pk.K2!.Value,12);Assert.Null(pk.Direction1);
        var sphere=Sphere("sphere",5);var sk=CurvatureQuery.Patch(sphere,0,0);Assert.Equal(.2,double.Abs(sk.K1!.Value),8);Assert.Equal(.2,double.Abs(sk.K2!.Value),8);
        var circularCylinder=Cylinder("cylinder",4);var ck=CurvatureQuery.Patch(circularCylinder,.4,.2);Assert.Equal(.25,double.Max(double.Abs(ck.K1!.Value),double.Abs(ck.K2!.Value)),10);Assert.Equal(0,double.Min(double.Abs(ck.K1.Value),double.Abs(ck.K2.Value)),10);
    }

    [Fact]
    public void SurfaceCurvatureAndNormalCurvatureIgnoreParameterScalingAndReversal()
    {
        var a=ParabolicPatch("a",1,false);var b=ParabolicPatch("b",3,true);var ka=CurvatureQuery.Patch(a,0,0);var kb=CurvatureQuery.Patch(b,0,0);
        Assert.Equal(ka.GaussianCurvature!.Value,kb.GaussianCurvature!.Value,10);Assert.Equal(double.Abs(ka.MeanCurvature!.Value),double.Abs(kb.MeanCurvature!.Value),10);
        var na=CurvatureQuery.NormalCurvature(a,0,0,new(1,0,0));var nb=CurvatureQuery.NormalCurvature(b,0,0,new(1,0,0));Assert.Equal(double.Abs(na.Curvature!.Value),double.Abs(nb.Curvature!.Value),10);
    }

    [Fact]
    public void ExistingPanelMatesDistinguishG0G1G2AndUnknownWithSampledEvidence()
    {
        var crease=PanelPair("crease",x=>SurfaceExpression.Length(0),x=>SurfaceExpression.Multiply(SurfaceExpression.Length(1),x));
        var g0=Validate(crease,PanelContinuity.PositionG0);Assert.Equal("valid",g0.Status);
        var g1Fail=Validate(crease,PanelContinuity.TangentG1);Assert.Equal("invalid",g1Fail.Status);Assert.True(g1Fail.MaximumAngularResidualRadians>0);

        var curvatureBreak=PanelPair("curvature-break",x=>SurfaceExpression.Length(0),x=>SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.Power(x,2)));
        Assert.Equal("valid",Validate(curvatureBreak,PanelContinuity.TangentG1).Status);var g2Fail=Validate(curvatureBreak,PanelContinuity.CurvatureG2);Assert.Equal("invalid",g2Fail.Status);Assert.True(g2Fail.MaximumNormalCurvatureResidual>0);

        var smooth=PanelPair("smooth",x=>SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.Power(x,2)),x=>SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.Power(x,2)));
        var g2=Validate(smooth,PanelContinuity.CurvatureG2);Assert.Equal("valid",g2.Status);Assert.Equal(PredicateEvidenceKind.Sampled,g2.Evidence);Assert.Equal(0,g2.MaximumNormalCurvatureResidual!.Value,9);
        var flipped=smooth.Right with{Orientation=new(PanelNormalOrientation.ReversedSupportNormal,PanelMaterialSide.Back)};Assert.Equal("valid",Validate((smooth.Left,flipped),PanelContinuity.CurvatureG2).Status);

        var original=smooth.Right.AuthoredPatch;var firstOnly=BoundedParametricPatch3.Procedural("first-only",original.Domain,(u,v)=>original.EvaluateJet1(u,v),"fixture");var unavailable=smooth.Right with{SurfaceConstruction=smooth.Right.SurfaceConstruction with{AuthoredPatch=firstOnly}};var unknown=Validate((smooth.Left,unavailable),PanelContinuity.CurvatureG2);Assert.Equal("unknown",unknown.Status);Assert.Equal(PredicateEvidenceKind.Sampled,unknown.Evidence);
    }

    [Fact]
    public void LocalMinimumFixtureExposesDifferentialIngredientsWithoutContactOrderClaim()
    {
        var t=CurveExpression.T;var one=CurveExpression.Length(1);var value=CurveExpression.Multiply(one,CurveExpression.Power(t,2));var scalar=value.Evaluate(0,0);Assert.Equal(0,scalar.Value,12);Assert.Equal(0,scalar.Du,12);Assert.Equal(2,scalar.Duu,12);
        var parabola=Expression("local-minimum",new(CurveExpression.Multiply(one,t),value,CurveExpression.Length(0)),-1,1);var jet=parabola.EvaluateJet2(0);AssertVector(jet.FirstDerivative,1,0,0);AssertVector(jet.SecondDerivative,0,2,0);Assert.Equal(DifferentialQueryStatus.Available,CurvatureQuery.Curve(parabola,0).Status);
    }

    private static PanelMateEvidence Validate((PanelIr Left,PanelIr Right) pair,PanelContinuity continuity)=>PanelNetworkValidator.Validate([pair.Left,pair.Right],[new("seam",pair.Left["East"],pair.Right["West"],continuity)]).Mates.Single();
    private static (PanelIr Left,PanelIr Right) PanelPair(string id,Func<SurfaceScalarExpression,SurfaceScalarExpression> leftZ,Func<SurfaceScalarExpression,SurfaceScalarExpression> rightZ)
    {
        ParametricSurfaceIr Side(string suffix,double min,double max,Func<SurfaceScalarExpression,SurfaceScalarExpression> z){var u=SurfaceExpression.U;return new(id+suffix,SurfaceConstructionKind.ParametricSurface,new(new(min,max),new(-1,1)),new(SurfaceExpression.Multiply(SurfaceExpression.Length(1),u),SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.V),z(u)),"fixture");}
        return(PanelFactory.FromParametric(Side("-left",-1,0,leftZ),controlCountU:9,controlCountV:3,tolerance:.01).Panel!,PanelFactory.FromParametric(Side("-right",0,1,rightZ),controlCountU:9,controlCountV:3,tolerance:.01).Panel!);
    }
    private static BoundedParametricPatch3 Patch(string id,SurfaceScalarExpression x,SurfaceScalarExpression y,SurfaceScalarExpression z)=>new(id,new(new(-1,1),new(-1,1)),new(x,y,z),"fixture");
    private static BoundedParametricPatch3 ParabolicPatch(string id,double scale,bool reverse){var u=SurfaceExpression.Multiply(SurfaceExpression.Number(reverse?-scale:scale),SurfaceExpression.U);return Patch(id,SurfaceExpression.Multiply(SurfaceExpression.Length(1),u),SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.V),SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.Power(u,2)));}
    private static BoundedParametricPatch3 Sphere(string id,double r){var u=SurfaceExpression.U;var v=SurfaceExpression.V;var cv=SurfaceExpression.Cos(v);return new(id,new(new(-.5,.5),new(-.5,.5)),new(SurfaceExpression.Multiply(SurfaceExpression.Length(r),SurfaceExpression.Multiply(cv,SurfaceExpression.Cos(u))),SurfaceExpression.Multiply(SurfaceExpression.Length(r),SurfaceExpression.Multiply(cv,SurfaceExpression.Sin(u))),SurfaceExpression.Multiply(SurfaceExpression.Length(r),SurfaceExpression.Sin(v))),"fixture");}
    private static BoundedParametricPatch3 Cylinder(string id,double r){var u=SurfaceExpression.U;return new(id,new(new(-1,1),new(-1,1)),new(SurfaceExpression.Multiply(SurfaceExpression.Length(r),SurfaceExpression.Cos(u)),SurfaceExpression.Multiply(SurfaceExpression.Length(r),SurfaceExpression.Sin(u)),SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.V)),"fixture");}
    private static BoundedParametricCurve3 Adapt(string id,CurveGeometry curve,double start,double end)=>BoundedParametricCurve3.FromCurveGeometry(id,curve,start,end,"fixture");
    private static BoundedParametricCurve3 Expression(string id,CurvePointExpression expression,double start,double end)=>new(id,new(start,end),expression,"fixture");
    private static void AssertVector(Vector3D actual,double x,double y,double z){Assert.Equal(x,actual.X,9);Assert.Equal(y,actual.Y,9);Assert.Equal(z,actual.Z,9);}
}
