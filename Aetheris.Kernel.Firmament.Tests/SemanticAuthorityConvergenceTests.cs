using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SemanticAuthorityConvergenceTests
{
    [Fact]
    public void SequentialBosses_ResolveTopAgainstCurrentFootprintSupport()
    {
        var source=File.ReadAllText(Fixture("Canonical/Features/Composition/boss-stack-current-top.firmament"));
        var parsed=PrismaticProfileCompositionParser.Parse(source);
        Assert.Empty(parsed.Diagnostics);
        var stack=Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed,out var diagnostics));
        Assert.Empty(diagnostics);
        Assert.Collection(stack.Feature.Bosses!,
            middle=>{Assert.Equal((10d,16d),(middle.From,middle.To));Assert.Equal("Top",middle.SupportFace);},
            upper=>{Assert.Equal((16d,20d),(upper.From,upper.To));Assert.Equal("Top",upper.SupportFace);});
        Assert.Equal(20d,stack.Slabs.Max(x=>x.To));
        Assert.All(stack.Slabs.Zip(stack.Slabs.Skip(1)),pair=>Assert.True(pair.First.To<=pair.Second.From));
        var emitted=PrismaticSectionStackEmitter.Emit(stack);Assert.NotNull(emitted.Body);
        Assert.True(BrepMassProperties.Evaluate(emitted.Body!).IsEnclosed);
        var step=Step242Exporter.ExportBody(emitted.Body!);Assert.True(step.IsSuccess,string.Join("; ",step.Diagnostics.Select(x=>x.Message)));
        var imported=Step242Importer.ImportBody(step.Value!);Assert.True(imported.IsSuccess,string.Join("; ",imported.Diagnostics.Select(x=>x.Message)));
        Assert.True(BrepMassProperties.Evaluate(imported.Value!).IsEnclosed);
    }

    [Fact]
    public void ExplicitBaseTop_RemainsDistinctFromRelativeCurrentTop()
    {
        var source=File.ReadAllText(Fixture("Canonical/Features/Composition/boss-stack-current-top.firmament"))
            .Replace("Boss Upper { On: Top;","Boss Upper { On: Base.Top;",StringComparison.Ordinal);
        var parsed=PrismaticProfileCompositionParser.Parse(source);Assert.Empty(parsed.Diagnostics);
        var upper=Assert.Single(parsed.Feature!.Bosses!,x=>x.Name=="Upper");
        Assert.Equal((10d,14d),(upper.From,upper.To));Assert.Equal("Base.Top",upper.SupportFace);
        var build=FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess,string.Join("; ",build.Diagnostics.Select(x=>x.Message)));
    }

    [Fact]
    public void EqualCurrentTopSupports_FailWithTypedAmbiguity()
    {
        var parsed=PrismaticProfileCompositionParser.Parse(File.ReadAllText(Fixture("Invalid/FeatureComposition/feature-support-top-ambiguous.firmament")));
        Assert.Null(parsed.Feature);
        Assert.Contains(parsed.Diagnostics,x=>x.StartsWith("feature-support-ambiguous:Top:feature=Bridge:candidates=Left,Right",StringComparison.Ordinal));
    }

    [Fact]
    public void MultipleBaseRoles_FailTypedWithoutRawSingleException()
    {
        var exception=Record.Exception(()=>PrismaticProfileCompositionParser.Parse(File.ReadAllText(Fixture("Invalid/FeatureComposition/compose-multiple-base.firmament"))));
        Assert.Null(exception);
        var parsed=PrismaticProfileCompositionParser.Parse(File.ReadAllText(Fixture("Invalid/FeatureComposition/compose-multiple-base.firmament")));
        Assert.Null(parsed.Feature);
        Assert.Contains("compose-role-cardinality:Base:expected=1:actual=2",parsed.Diagnostics);
    }

    [Fact]
    public void PrimitiveBoxCounterboreThenChamfer_UsesCombinedCurrentStatePlanAndRoundTrips()
    {
        var build=FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture("Canonical/Features/Composition/box-counterbore-chamfer.firmament")));
        Assert.True(build.IsSuccess,string.Join("; ",build.Diagnostics.Select(x=>x.Message)));
        Assert.Equal("CombinedHoleEdgeFinish",build.Value!.Combined!.Route);
        var bottomArea=10d*15d;var topArea=8d*13d;
        var chamferedHost=bottomArea*19d+(bottomArea+topArea+Math.Sqrt(bottomArea*topArea))/3d;
        var expected=chamferedHost-Math.PI*2d*2d*20d-Math.PI*(3.5d*3.5d-2d*2d)*3d;
        Assert.Equal(expected,build.Value.Combined.FinalAnalyticVolume,6);
        var imported=Step242Importer.ImportBody(build.Value.StepText);Assert.True(imported.IsSuccess,string.Join("; ",imported.Diagnostics.Select(x=>x.Message)));
        Assert.True(BrepMassProperties.Evaluate(imported.Value!).IsEnclosed);
        Assert.True(build.Value.StepText.Contains("CYLINDRICAL_SURFACE",StringComparison.Ordinal));
        var repeated=FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture("Canonical/Features/Composition/box-counterbore-chamfer.firmament")));
        Assert.True(repeated.IsSuccess,string.Join("; ",repeated.Diagnostics.Select(x=>x.Message)));
        Assert.Equal(build.Value.StepText,repeated.Value!.StepText);
    }

    private static string Fixture(string relative)=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../fixtures",relative));
}
