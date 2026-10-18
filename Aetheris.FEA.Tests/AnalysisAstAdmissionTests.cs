using Aetheris.FEA.Firmament;
namespace Aetheris.FEA.Tests;
public sealed class AnalysisAstAdmissionTests
{
    [Theory]
    [InlineData("heigth: 1mm")]
    [InlineData("Fillet Lost { radius: 1mm }")]
    [InlineData("lattice: [4,2,1]; Lattice: [4,2,1]")]
    public void UnknownAndDuplicateAnalysisIntentRejects(string intent)
    {
        var source=Source();var at=source.IndexOf("Analysis LinearElastic",StringComparison.Ordinal);at=source.IndexOf('{',at)+1;
        var result=FirmamentAnalysisCompiler.Compile(source.Insert(at,intent+";"),"beam.firmament");
        Assert.False(result.IsSuccess);Assert.Contains(result.Diagnostics,d=>d.Provenance is {Length:>0});
    }
    [Fact]
    public void ExplicitEmptySettingsDoNotBecomeDefaults()
    {
        var source=Source();
        Assert.Contains("Lattice: [12, 2, 2]",source);Assert.Contains("Components: [X, Y, Z]",source);
        Assert.False(FirmamentAnalysisCompiler.Compile(source.Replace("Lattice: [12, 2, 2]","lattice: []",StringComparison.Ordinal)).IsSuccess);
        Assert.False(FirmamentAnalysisCompiler.Compile(source.Replace("Components: [X, Y, Z]","components: []",StringComparison.Ordinal)).IsSuccess);
    }
    [Fact]
    public void InvalidMaterialValueReturnsDiagnosticInsteadOfThrowing()
    {
        var source=Source().Replace("Material: Standard.Materials.Aluminum.6061_T6","Material Test { youngsModulus: 200GPa; poissonRatio: typo }",StringComparison.Ordinal);
        // Preserve catalogue-independent evidence even when the canonical material name changes.
        if(source==Source())source=Source().Replace("Material: Standard.Materials.Steel.S235JR","Material Test { youngsModulus: 200GPa; poissonRatio: typo }",StringComparison.Ordinal);
        var result=FirmamentAnalysisCompiler.Compile(source,"beam.firmament");
        Assert.False(result.IsSuccess);Assert.Contains(result.Diagnostics,d=>d.Code.Contains("poisson",StringComparison.Ordinal));
    }
    [Fact]
    public void AnalysisCannotFallBackAroundMalformedV2Geometry()
    {
        var source=Source().Replace("Box Beam {","Box Beam { heigth: 2mm;",StringComparison.Ordinal);
        var result=FirmamentAnalysisCompiler.Compile(source,"beam.firmament");Assert.False(result.IsSuccess);Assert.Null(result.Analysis);
    }
    private static string Source(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"Aetheris.slnx")))d=d.Parent;return File.ReadAllText(Path.Combine(d!.FullName,"fixtures","Canonical","FEA","cantilever.firmament"));}
}
