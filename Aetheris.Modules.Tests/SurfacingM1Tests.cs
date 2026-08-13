using Aetheris.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Modules.Tests;

public sealed class SurfacingM1Tests
{
    private static readonly Direction3D X=Direction3D.Create(new Vector3D(1,0,0));
    private static readonly Direction3D Y=Direction3D.Create(new Vector3D(0,1,0));
    private static readonly Direction3D Z=Direction3D.Create(new Vector3D(0,0,1));

    [Fact] public void GeneralizedRuledLineArcUsesDeterministicNormalizedCorrespondence()
    {
        var line=new RuledBoundary.Line("line",new(-5,0,0),new(5,0,0));
        var arc=new RuledBoundary.Arc("arc",new(0,5,0),Z,5,X,double.Pi,double.Pi);
        var result=RuledSurfaceLowering.Lower(Ruled("mixed",line,arc));
        Assert.True(result.IsSuccess);Assert.Equal(ParameterCorrespondenceKind.SharedNormalizedNativeParameter,result.Patch!.Ir.ParameterCorrespondence);
        Assert.Equal(line.Start,result.Patch.Evaluate(0,0));Assert.Equal(SurfaceMaterializationKind.ApproximatedNonRationalBSpline,result.Patch.MaterializationKind);
        Assert.NotNull(result.Patch.ApproximationCertificate);
    }

    [Fact] public void RuledDevelopabilityIsEvidenceNotAssumption()
    {
        var saddle=RuledSurfaceLowering.Lower(RuledSurfaceLowering.Saddle("s",5,4,2)).Patch!;
        Assert.Equal(DevelopabilityKind.NonDevelopable,saddle.Developability.Kind);
        var a=new RuledBoundary.Line("a",new(0,0,0),new(10,0,0));var b=new RuledBoundary.Line("b",new(0,5,0),new(10,5,0));
        Assert.Equal(DevelopabilityKind.Developable,RuledSurfaceLowering.Lower(Ruled("plane",a,b)).Patch!.Developability.Kind);
    }

    [Fact] public void CircleParameterFrameMismatchDoesNotMasqueradeAsExactCylinder()
    {
        var a=new RuledBoundary.Circle("a",Point3D.Origin,Z,10,X);var b=new RuledBoundary.Circle("b",new(0,0,20),Z,10,Y);var result=RuledSurfaceLowering.Lower(Ruled("twisted-circles",a,b));
        Assert.True(result.IsSuccess);Assert.Equal(SurfaceMaterializationKind.ApproximatedNonRationalBSpline,result.Patch!.MaterializationKind);Assert.Equal(DevelopabilityKind.NonDevelopable,result.Patch.Developability.Kind);
    }

    [Fact] public void DegenerateRuledBoundaryIsTypedDiagnostic()
    {
        var bad=new RuledBoundary.Line("bad",Point3D.Origin,Point3D.Origin);var good=new RuledBoundary.Line("good",new(0,1,0),new(1,1,0));
        var result=RuledSurfaceLowering.Lower(Ruled("bad",bad,good));Assert.False(result.IsSuccess);Assert.Contains(result.Diagnostics,d=>d.Code=="surfacing-boundary-invalid");
    }

    [Theory]
    [InlineData("hyperbolic")][InlineData("parabolic-cylinder")][InlineData("elliptic")]
    public void PolynomialNamedSurfacesHaveAutomaticDerivativesAndNormals(string family)
    {
        var surface=family switch{"hyperbolic"=>MathematicalSurfaces.HyperbolicParaboloid("s",40,30,12),"parabolic-cylinder"=>MathematicalSurfaces.ParabolicCylinder("p",40,30,12),_=>MathematicalSurfaces.EllipticParaboloid("e",40,30,12)};
        var differential=surface.Evaluate(.25,-.2);Assert.False(differential.IsSingular);Assert.NotNull(differential.Normal);Assert.True(differential.Du.Length>0);Assert.True(differential.Dv.Length>0);
    }

    [Fact] public void HyperbolicParaboloidMatchesMeaningfulRiseParameters()
    {
        var surface=MathematicalSurfaces.HyperbolicParaboloid("pringles",40,30,12);
        Assert.Equal(new Point3D(40,30,12),surface.Evaluate(1,1).Point);Assert.Equal(Point3D.Origin,surface.Evaluate(0,0).Point);
    }

    [Fact] public void HelicoidIsExactAtAuthoringLevelAndExplicitlyApproximatedForStepSupport()
    {
        var surface=MathematicalSurfaces.Helicoid("helicoid",20,10);
        Assert.Equal(new Point3D(20,0,0),surface.Evaluate(1,0).Point);Assert.False(surface.Evaluate(.75,.2).IsSingular);
        var materialized=ParametricSurfaceMaterializer.Materialize(surface,17,17,.05);Assert.Equal(SurfaceMaterializationKind.ApproximatedNonRationalBSpline,materialized.Kind);
        Assert.Equal("helicoid",materialized.Certificate.SourceIdentity);Assert.True(materialized.Certificate.MaximumSampledPositionResidual>0);
    }

    [Fact] public void ParametricPointComponentsAreUnitChecked()
    {
        Assert.Throws<ArgumentException>(()=>new ParametricSurfaceIr("bad",SurfaceConstructionKind.ParametricSurface,new(new(0,1),new(0,1)),new(SurfaceExpression.U,SurfaceExpression.Length(1),SurfaceExpression.Length(1)),"test"));
        Assert.Throws<ArgumentException>(()=>SurfaceExpression.Sin(SurfaceExpression.Length(1)));
    }

    [Fact] public void SingularParameterPointIsObservable()
    {
        var zero=SurfaceExpression.Length(0);var surface=new ParametricSurfaceIr("singular",SurfaceConstructionKind.ParametricSurface,new(new(0,1),new(0,1)),new(SurfaceExpression.Multiply(SurfaceExpression.Length(1),SurfaceExpression.U),zero,zero),"test");
        Assert.True(surface.Evaluate(.5,.5).IsSingular);Assert.Null(surface.Evaluate(.5,.5).Normal);
    }

    [Fact] public void ParametricEvaluationRejectsOutsideAuthoredDomain()
    {var surface=MathematicalSurfaces.HyperbolicParaboloid("domain",2,2,1);Assert.Throws<ArgumentOutOfRangeException>(()=>surface.Evaluate(1.01,0));}

    [Fact] public void ThreeSectionSurfaceInterpolatesAuthoritativeSectionsAndPreservesIdentity()
    {
        var sections=new RuledBoundary[]{Line("a",0,0),Line("b",5,3),Line("c",10,0)};var provenance=sections.Select((s,i)=>new BoundaryProvenance(s.StableId,"fixture",$"section-{i}")).ToArray();
        var result=SectionSurfaceLowering.Lower(new("fairing",sections,provenance));Assert.True(result.IsSuccess);Assert.Equal(SurfaceConstructionKind.SectionSurface,result.Patch!.ConstructionKind);
        Assert.Equal(new Point3D(0,5,3),result.Patch.Evaluate(.5,.5));Assert.Equal(3,result.Patch.Provenance.Count);Assert.Equal(DevelopabilityKind.Indeterminate,result.Patch.Developability.Kind);
    }

    [Fact] public void TwoSectionSurfaceRetainsRuledTransitionSemantics()
    {
        var sections=new RuledBoundary[]{Line("a",0,0),Line("b",5,2)};var p=sections.Select((s,i)=>new BoundaryProvenance(s.StableId,"fixture",$"section-{i}")).ToArray();var result=SectionSurfaceLowering.Lower(new("transition",sections,p));
        Assert.True(result.IsSuccess);Assert.Equal(SurfaceConstructionKind.RuledTransition,result.Patch!.ConstructionKind);
    }

    [Fact] public void FourBoundaryPatchInterpolatesEveryBoundary()
    {
        var south=new RuledBoundary.Line("south",new(-10,-5,0),new(10,-5,0));var north=new RuledBoundary.Line("north",new(-10,5,0),new(10,5,0));
        var west=new RuledBoundary.Arc("west",new(-10,0,0),Z,5,Direction3D.Create(new Vector3D(0,-1,0)),0,double.Pi);
        var east=new RuledBoundary.Arc("east",new(10,0,0),Z,5,Direction3D.Create(new Vector3D(0,-1,0)),0,double.Pi);
        var boundaries=new RuledBoundary[]{south,north,west,east};var provenance=boundaries.Select(b=>new BoundaryProvenance(b.StableId,"panel",b.StableId)).ToArray();var result=BoundaryPatchLowering.Lower(new("panel",south,north,west,east,provenance));
        Assert.True(result.IsSuccess,string.Join(';',result.Diagnostics.Select(d=>d.Message)));Assert.Equal(south.Start,result.Patch!.Evaluate(0,0));Assert.Equal(north.End,result.Patch.Evaluate(1,1));Assert.Equal(SurfaceConstructionKind.BoundaryPatch,result.Patch.ConstructionKind);
    }

    [Fact] public void BoundaryPatchRejectsCornerAndUnsupportedTangentRequests()
    {
        var s=Line("s",0,0);var n=Line("n",5,0);var w=new RuledBoundary.Line("w",new(-5,0,0),new(-5,5,0));var e=new RuledBoundary.Line("e",new(5,0,0),new(6,5,0));var p=new[]{s,n,w,e}.Select(b=>new BoundaryProvenance(b.StableId,"x",b.StableId)).ToArray();
        Assert.Contains(BoundaryPatchLowering.Lower(new("bad",s,n,w,e,p)).Diagnostics,d=>d.Code=="surfacing-boundary-corners-inconsistent");
        Assert.Contains(BoundaryPatchLowering.Lower(new("g1",s,n,w,new RuledBoundary.Line("e2",new(5,0,0),new(5,5,0)),p,BoundaryContinuity.TangentG1)).Diagnostics,d=>d.Code=="surfacing-tangent-constraint-unsupported");
    }

    [Fact] public void NonRationalBSplineContractRejectsInvalidDataAndHasNoWeights()
    {
        Assert.DoesNotContain(typeof(BSplineSurfaceWithKnots).GetProperties(),property=>property.Name.Contains("Weight",StringComparison.OrdinalIgnoreCase));
        Assert.Throws<ArgumentException>(()=>new BSplineSurfaceWithKnots(1,1,[[new(double.NaN,0,0),Point3D.Origin],[Point3D.Origin,Point3D.Origin]],"x",false,false,false,[2,2],[2,2],[0,1],[0,1],"x"));
    }

    [Fact] public void ApproximationControlNetAndCertificateAreDeterministic()
    {
        var source=MathematicalSurfaces.Helicoid("h",12,6);var a=ParametricSurfaceMaterializer.Materialize(source,9,9);var b=ParametricSurfaceMaterializer.Materialize(source,9,9);
        Assert.Equal(a.Surface.ControlPoints.SelectMany(x=>x),b.Surface.ControlPoints.SelectMany(x=>x));Assert.Equal(a.Certificate,b.Certificate);
    }

    [Fact] public void SixShapeGalleryBuildsBoundedBrepAndExportsStep()
    {
        var gallery=SurfacingGallery.Build();Assert.Equal(6,gallery.Count);
        foreach(var entry in gallery){var materialized=SurfacePatchBrepMaterializer.Materialize(entry.Support,entry.Evaluate);Assert.NotNull(materialized.Body);var step=Step242Exporter.ExportBody(materialized.Body!);Assert.True(step.IsSuccess,string.Join(';',step.Diagnostics.Select(d=>d.Message)));Assert.Contains("B_SPLINE_SURFACE_WITH_KNOTS",step.Value);}
    }

    private static RuledSurfaceIr Ruled(string id,RuledBoundary a,RuledBoundary b)=>new(id,RuledConstructionKind.RuledSurface,a,b,new(a.StableId,id,"a"),new(b.StableId,id,"b"));
    private static RuledBoundary.Line Line(string id,double y,double z)=>new(id,new(-5,y,z),new(5,y,z));
}
