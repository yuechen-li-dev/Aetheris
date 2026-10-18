using Xunit;
namespace Aetheris.PlasticShell.Tests;
public sealed class PlasticShellAstAdmissionTests
{
    [Theory]
    [InlineData("heigth: 3mm")]
    [InlineData("Fillet Lost { radius: 1mm }")]
    [InlineData("height: 1mm")]
    public void UnconsumedOrDuplicateIntentCannotBuild(string intent)
    {
        var source=Source();var at=source.IndexOf("PlasticShell ",StringComparison.Ordinal);at=source.IndexOf('{',at)+1;
        var result=PlasticShellFirmament.Compile(source.Insert(at,intent+";"),"shell.firmament");
        Assert.False(result.IsSuccess);Assert.Null(result.State);Assert.Contains(result.Diagnostics,d=>d.SourceSpan is not null && d.SourceFile=="shell.firmament");
    }
    [Fact]
    public void ModernAndHistoricalFieldsRetainTheSameManufacturedGeometry()
    {
        var source=Source();var a=PlasticShellFirmament.Compile(source);var b=PlasticShellFirmament.Compile(source.Replace("BottomRadius:","bottomRadius:",StringComparison.Ordinal));
        Assert.True(a.IsSuccess);Assert.True(b.IsSuccess,string.Join(";",b.Diagnostics));
        Assert.Equal(PlasticShellStepExporter.Export(a.State!,a.ModelName).Step,PlasticShellStepExporter.Export(b.State!,b.ModelName).Step);
    }
    private static string Source(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"Aetheris.slnx")))d=d.Parent;return File.ReadAllText(Path.Combine(d!.FullName,"fixtures","Canonical","PlasticShell","plastic-shell-basic.firmament"));}
}
