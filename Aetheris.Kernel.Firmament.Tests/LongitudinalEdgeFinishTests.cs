using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class LongitudinalEdgeFinishTests
{
    private static string Bracket {
        get { var s=FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/OrientedComposeHoles/bracket-foot-hole.firmament"); return s[..s.IndexOf("  Modify",StringComparison.Ordinal)]+"}"; }
    }
    private static string Source(string target,string kind,double size,string plane="XZ") => Bracket.Replace("on XZ", "on "+plane,StringComparison.Ordinal)[..^1]
        + FormattableString.Invariant($"Modify bracket {{ EdgeFinish Bend {{ Face: Longitudinal Target: section.Outer.{target}.Start Kind: {kind} {(kind=="Fillet"?"Radius":"Distance")}: {size}mm }} }} }}");

    [Theory]
    [InlineData("Vertex3_Vertex4", "Fillet", 2d, true)]
    [InlineData("Vertex2_Vertex3", "Fillet", .5d, false)]
    [InlineData("Vertex3_Vertex4", "Chamfer", 2d, true)]
    [InlineData("Vertex2_Vertex3", "Chamfer", .5d, false)]
    public void InsideAndOutsideFinish_SweepTheCompleteWidth_AndRetainExactSectionArea(string target,string kind,double size,bool inside)
    {
        var source=Source(target,kind,size);
        var original=FirmamentBuildAndExport.CompileSource(Bracket).Value;
        var map=original.RuntimeCorrespondence!;
        var role=inside?SemanticTopologyRole.ReflexLongitudinalEdge:SemanticTopologyRole.ConvexLongitudinalEdge;
        var edge=Assert.Single(map.Descendants,d=>d.Role==role && d.SourceStableId=="profile:section.Outer."+target);
        var selected=SingleEdgeFinishSelection.Resolve(map,edge.Edge!.Value)!;
        Assert.Equal("Longitudinal",selected.Face); Assert.True(selected.FilletSupported);
        Assert.Equal("section.Outer."+target+".Start",selected.Target);
        var bound=ProfileLongitudinalEdgeFinish.Apply(source,FirmamentV2.ProfileAuthoringParser.Parse(Bracket).Profile!,"bracket");
        Assert.Null(bound.Diagnostic); Assert.NotNull(bound.Profile);
        var change=size*size*(kind=="Fillet"?1-Math.PI/4:.5)*(inside?1:-1);
        Assert.Equal(201+change,ResolvedProfile2DValidator.Validate(bound.Profile).SignedArea,8);
        var build=FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess,string.Join(";",build.Diagnostics.Select(d=>d.Message)));
        var body=build.Value.RuntimeBody!;
        var imported=Step242Importer.ImportBody(build.Value.StepText); Assert.True(imported.IsSuccess);
        foreach(var b in new[]{body,imported.Value}) {
            var mass=BrepMassProperties.Evaluate(b); Assert.True(mass.IsEnclosed); Assert.True(mass.IsOrientationConsistent);
            Assert.InRange(Math.Abs(mass.AbsoluteVolume-(201+change)*20),0,Math.Max(mass.ErrorBound??0,1e-6));
            var points=b.Topology.Vertices.Select(v=>{ Assert.True(b.TryGetVertexPoint(v.Id,out var p)); return p; }).ToArray();
            Assert.Equal(-20,points.Min(p=>p.Y),8); Assert.Equal(0,points.Max(p=>p.Y),8);
            if(kind=="Fillet") {
                var surface=Assert.Single(b.Geometry.Surfaces,s=>s.Value.Cylinder is not null).Value.Cylinder!.Value;
                Assert.Equal(size,surface.Radius,8); Assert.Equal(1,Math.Abs(surface.Axis.Y),8);
                // The blend ends on both stock caps, rather than stopping short inside a side face.
                Assert.Equal(9,b.Topology.Faces.Count());
            }
        }
    }

    [Theory]
    [InlineData(0d)] [InlineData(-1d)] [InlineData(27d)] [InlineData(50d)]
    public void InvalidInsideSize_IsRefusedWithoutAResult(double size)
    {
        var result=FirmamentBuildAndExport.CompileSource(Source("Vertex3_Vertex4","Fillet",size));
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics,d=>d.Message.Contains("longitudinal-edge-finish:",StringComparison.Ordinal));
    }

    [Fact]
    public void LargeOutsideRound_CannotCrossTheOtherArmOfTheBracket()
    {
        var result=FirmamentBuildAndExport.CompileSource(Source("Vertex0_Vertex1","Fillet",12));
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics,d=>d.Message.Contains("another boundary",StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("XY")] [InlineData("YZ")] [InlineData("XZ")]
    public void ConstructionPlaneControlsTheFullLengthBlend(string plane)
    {
        var result=FirmamentBuildAndExport.CompileSource(Source("Vertex3_Vertex4","Fillet",2,plane));
        Assert.True(result.IsSuccess,string.Join(";",result.Diagnostics.Select(d=>d.Message)));
        Assert.True(BrepMassProperties.Evaluate(result.Value.RuntimeBody!).IsEnclosed);
    }

    [Fact]
    public void PreferredFieldSpellingAndVariantRetainTheSameFeature()
    {
        var source=Source("Vertex3_Vertex4","Fillet",2).Replace("EdgeFinish Bend", "EdgeFinish<Fillet> Bend",StringComparison.Ordinal)
            .Replace("Kind: Fillet", "",StringComparison.Ordinal).Replace("Face:","face:",StringComparison.Ordinal)
            .Replace("Target:","target:",StringComparison.Ordinal).Replace("Radius:","radius:",StringComparison.Ordinal);
        var result=FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess,string.Join(";",result.Diagnostics.Select(d=>d.Message)));
        var feature=Assert.Single(result.Value.EngineeringFeatures!);
        Assert.Equal("Longitudinal",feature.Support); Assert.Equal("Add",feature.MaterialEffect);
    }

    [Fact]
    public void InsetTerminationField_IsNotSilentlyAcceptedOnAFullLengthFinish()
    {
        var source=Source("Vertex3_Vertex4","Fillet",2).Replace("Radius: 2mm", "Radius: 2mm EndClearance: 1mm",StringComparison.Ordinal);
        var result=FirmamentBuildAndExport.CompileSource(source);
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics,d=>d.Message.Contains("EndClearance",StringComparison.Ordinal));
    }

    [Fact]
    public void AnonymousFinish_IsDiagnosedInsteadOfThrowing()
    {
        var source=Source("Vertex3_Vertex4","Fillet",2).Replace("EdgeFinish Bend", "EdgeFinish",StringComparison.Ordinal);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }
}
