using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Modules.Tests;

public sealed class SectionProfileNormalizationTests
{
    [Fact]
    public void ExistingPolynomialTracksRemainExactAndUnsupportedCurvesFailTyped()
    {
        var normalized = SectionProfileNormalizer.Normalize(Chain()).NormalizedChain!;
        var repeated = SectionProfileNormalizer.Normalize(normalized);
        Assert.True(repeated.IsSuccess);
        Assert.Empty(repeated.Curves);
        Assert.Equal(SectionChainCanonical.Fingerprint(normalized), SectionChainCanonical.Fingerprint(repeated.NormalizedChain!));
        var sections = normalized.Sections.ToArray();
        var spans = sections[0].Profile.Spans.ToArray();
        spans[0] = spans[0] with { Curve = new UnsupportedCurve() };
        sections[0] = sections[0] with { Profile = sections[0].Profile with { Spans = spans } };
        Assert.Contains(SectionProfileNormalizer.Normalize(normalized with { Sections = sections }).Diagnostics,
            d => d.Code == "section-chain-normalization-curve-unsupported");
        var built = SectionChainMaterializer.Materialize(normalized with { Sections = sections });
        Assert.False(built.IsSuccess);
        Assert.Null(built.Body);
    }

    [Fact]
    public void ChangedNormalizationGridExpandsTheReportedEditDependency()
    {
        Section Resize(Section section, double radius) => section with { Profile = section.Profile with {
            Spans = section.Profile.Spans.Select(s => s with { Curve = ((SectionProfileCurve.Arc)s.Curve) with { Radius = radius } }).ToArray() } };
        var chain = Chain();
        chain = chain with { Sections = chain.Sections.Select(s => Resize(s, 1)).ToArray() };
        var before = SectionProfileNormalizer.Normalize(chain);
        var replacement = Resize(chain.Sections[0], 2);
        var edited = SectionChainEditor.ReplaceSection(chain, replacement);
        var after = SectionProfileNormalizer.Normalize(edited.Chain);
        Assert.NotEqual(before.Decisions[0].SelectedSegmentCount, after.Decisions[0].SelectedSegmentCount);
        Assert.Equal(2, edited.Delta.RecomputedTangentFields.Count);
        Assert.Empty(edited.Delta.PreservedTerminations);
        Assert.Empty(edited.Delta.PreservedTransitions);
    }

    private sealed record UnsupportedCurve : SectionProfileCurve;

    [Fact]
    public void ArcTracksHaveBoundedErrorCommonKnotsAndExactEndpointJets()
    {
        var chain = Chain();
        var result = SectionProfileNormalizer.Normalize(chain);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal(8, result.Curves.Count);
        Assert.All(result.Curves, e =>
        {
            Assert.Equal(5, e.Degree);
            Assert.True(e.CertifiedDeviationBound <= e.RequestedTolerance);
            Assert.True(e.MaximumSampledDeviation <= e.CertifiedDeviationBound);
            Assert.True(e.EndpointPositionError < 1e-12);
            Assert.True(e.EndpointTangentAngleDegrees < 1e-9);
        });
        for (var span=0;span<4;span++)
        {
            var a=Assert.IsType<SectionProfileCurve.PolynomialBSpline>(result.NormalizedChain!.Sections[0].Profile.Spans[span].Curve);
            var b=Assert.IsType<SectionProfileCurve.PolynomialBSpline>(result.NormalizedChain.Sections[1].Profile.Spans[span].Curve);
            Assert.Equal(a.KnotValues,b.KnotValues);
            Assert.Equal(a.KnotMultiplicities,b.KnotMultiplicities);
            var spline=Curve(a);
            // Analytic derivatives, including one-sided jets at all internal polynomial joins.
            foreach(var knot in a.KnotValues.Skip(1).SkipLast(1))
            {
                Assert.True((spline.EvaluateTangent(knot-1e-10)-spline.EvaluateTangent(knot+1e-10)).Length < 1e-6);
                Assert.True((spline.EvaluateSecondDerivative(knot-1e-10)-spline.EvaluateSecondDerivative(knot+1e-10)).Length < 1e-6);
            }
            for(var i=0;i<=4096;i++)
            {
                var p=spline.Evaluate(i/4096d);
                Assert.True(Math.Abs(Math.Sqrt(p.X*p.X+p.Y*p.Y)-8) < 1e-5);
            }
        }
        Assert.IsType<SectionProfileCurve.Arc>(chain.Sections[0].Profile.Spans[0].Curve);
        Assert.Equal(result.Curves.Select(c=>c.ControlHash),SectionProfileNormalizer.Normalize(chain).Curves.Select(c=>c.ControlHash));
    }

    [Fact]
    public void NormalizedArcChainHasSharedWatertightTopologyAndDeterministicStep()
    {
        var a=SectionChainMaterializer.Materialize(Chain());
        Assert.True(a.IsSuccess,string.Join("\n",a.Diagnostics));
        Assert.True(a.Pcurves!.LoopClosureValid);
        Assert.All(a.Body!.Topology.Coedges.GroupBy(c=>c.EdgeId),g=>Assert.Equal(2,g.Count()));
        var step=Step242Exporter.ExportBody(a.Body);
        Assert.True(step.IsSuccess);
        Assert.DoesNotContain("RATIONAL",step.Value);
        Assert.True(Step242Importer.ImportBody(step.Value).IsSuccess);
        Assert.Equal(step.Value,Step242Exporter.ExportBody(SectionChainMaterializer.Materialize(Chain()).Body!).Value);
    }

    [Fact]
    public void LineOnlyG1IsNotReparameterizedAndG0RemainsRuled()
    {
        var lines=SectionChainTemplates.ErgonomicFairingG1();
        var normalized=SectionProfileNormalizer.Normalize(lines);
        Assert.Empty(normalized.Curves);
        Assert.Equal(SectionChainCanonical.Fingerprint(lines),SectionChainCanonical.Fingerprint(normalized.NormalizedChain!));
        var ruled=SectionChainMaterializer.Materialize(lines with { Continuity=SectionChainContinuity.G0,TransitionPolicy=SectionTransitionPolicy.Ruled });
        Assert.True(ruled.IsSuccess,string.Join("\n",ruled.Diagnostics));
        Assert.Null(ruled.ProfileNormalization);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(1e-20)]
    public void ImpossibleToleranceFailsWithoutBody(double tolerance)
    {
        var result=SectionChainMaterializer.Materialize(Chain() with { ProfileApproximationTolerance=tolerance });
        Assert.False(result.IsSuccess);
        Assert.Null(result.Body);
        Assert.Contains(result.Diagnostics,d=>d.Code.StartsWith("section-chain-normalization-tolerance"));
    }

    [Theory]
    [InlineData(0,1)]
    [InlineData(8,0)]
    [InlineData(-8,1)]
    [InlineData(8,7)]
    public void DegenerateArcFailsTyped(double radius,double sweep)
    {
        var chain=Chain();var sections=chain.Sections.ToArray();var spans=sections[0].Profile.Spans.ToArray();
        spans[0]=spans[0] with {Curve=new SectionProfileCurve.Arc(new(0,0),radius,0,sweep)};
        sections[0]=sections[0] with {Profile=sections[0].Profile with {Spans=spans}};
        var result=SectionProfileNormalizer.Normalize(chain with {Sections=sections});
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,d=>d.Code=="section-chain-normalization-arc-invalid");
    }

    [Fact]
    public void MixedLineArcCorrespondenceNormalizesLineExactly()
    {
        var chain=Chain();var sections=chain.Sections.ToArray();var spans=sections[0].Profile.Spans.ToArray();
        spans[0]=spans[0] with {Curve=new SectionProfileCurve.Line(new(8,0),new(0,8))};
        sections[0]=sections[0] with {Profile=sections[0].Profile with {Spans=spans}};
        var result=SectionProfileNormalizer.Normalize(chain with {Sections=sections});
        Assert.True(result.IsSuccess);
        var c=Curve((SectionProfileCurve.PolynomialBSpline)result.NormalizedChain!.Sections[0].Profile.Spans[0].Curve);
        for(var i=0;i<=100;i++) Assert.True((c.Evaluate(i/100d)-new Point3D(8-8*i/100d,8*i/100d,0)).Length < 1e-12);
    }

    [Fact]
    public void TopologyMismatchFailsBeforeNormalization()
    {
        var chain=Chain();var sections=chain.Sections.ToArray();
        sections[1]=sections[1] with {Profile=sections[1].Profile with {Spans=sections[1].Profile.Spans.Take(3).ToArray()}};
        Assert.Contains(SectionProfileNormalizer.Normalize(chain with {Sections=sections}).Diagnostics,d=>d.Code=="section-chain-correspondence-topology-mismatch");
    }

    [Fact]
    public void NormalizationDoesNotEraseSourceLineArcCurvatureJump()
    {
        var result=SectionProfileNormalizer.Normalize(Chain());
        var c=Curve((SectionProfileCurve.PolynomialBSpline)result.NormalizedChain!.Sections[0].Profile.Spans[0].Curve);
        var d=c.EvaluateTangent(0);var dd=c.EvaluateSecondDerivative(0);
        var curvature=d.Cross(dd).Length/Math.Pow(d.Length,3);
        Assert.InRange(curvature,1d/8-1e-10,1d/8+1e-10);
        // An adjacent straight profile has curvature zero. Normalization is not a G2 repair.
        Assert.True(curvature>0.12);
    }

    private static BSpline3Curve Curve(SectionProfileCurve.PolynomialBSpline c)=>new(c.Degree,c.ControlPoints.Select(p=>new Point3D(p.X,p.Y,0)).ToArray(),c.KnotMultiplicities,c.KnotValues,"UNSPECIFIED",false,false,"UNSPECIFIED");
    private static SectionChain Chain()
    {
        Section Section(string name,double radius,double z)=>new(name,SectionFrame.Create(new(0,0,z),new(1,0,0),new(0,1,0)),
            new(name+"Profile",Enumerable.Range(0,4).Select(i=>new SectionProfileSpan("Arc"+i,new SectionProfileCurve.Arc(new(0,0),radius,i*Math.PI/2,Math.PI/2))).ToArray(),"Arc0"));
        return new("ArcNormalization",[Section("A",8,0),Section("B",9,12)],[],SectionTransitionPolicy.SmoothPolynomial,SectionTermination.Cap,SectionTermination.Cap,SectionChainContinuity.G1);
    }
}
