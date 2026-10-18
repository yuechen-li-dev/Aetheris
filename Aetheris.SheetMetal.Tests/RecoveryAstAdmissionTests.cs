using Xunit;
namespace Aetheris.SheetMetal.Tests;
public sealed class RecoveryAstAdmissionTests
{
    [Theory]
    [InlineData("heigth: 2mm;")]
    [InlineData("Fillet Lost { radius: 1mm }")]
    [InlineData("kFactor: 0.5;")]
    public void RecoveredEvidenceCannotDropIntent(string intent)
    {
        var part=SheetMetalFirmament.CompileFile(Path.Combine(Root(),"fixtures","Canonical","SheetMetal","u-channel.firmament"));Assert.True(part.IsSuccess);
        var source=SheetMetalManufacturingArtifacts.WriteRecoveredFirmament(part.Part!,"original.step");
        var valid=SheetMetalFirmament.Compile(source,"recovered.firmament");Assert.True(valid.IsSuccess,string.Join(";",valid.Diagnostics));
        var at=source.IndexOf('{')+1;var invalid=SheetMetalFirmament.Compile(source.Insert(at,intent),"recovered.firmament");
        Assert.False(invalid.IsSuccess);Assert.Contains(invalid.Diagnostics,d=>d.SourceSpan is not null && d.SourceFile=="recovered.firmament");
    }
    private static string Root(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"Aetheris.slnx")))d=d.Parent;return d!.FullName;}
}
