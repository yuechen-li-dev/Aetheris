using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class StructuralCliTests
{
    [Fact]
    public void BuildAndInspect_ExposeStructuralArtifactAndCutList()
    {
        var root = FindRoot(); var source = Path.Combine(root,"fixtures","Canonical","Structural","welded-workbench.firmament");
        using var temp = new Temp(); var step=Path.Combine(temp.Path,"workbench.step");var output=new StringWriter();var error=new StringWriter();
        Assert.Equal(0,CliRunner.Run(["build",source,"--output",step,"--json"],output,error));Assert.Empty(error.ToString());Assert.True(File.Exists(step));Assert.True(File.Exists(Path.ChangeExtension(step,".cutlist.json")));
        using(var json=JsonDocument.Parse(output.ToString())){var structural=json.RootElement.GetProperty("structural");Assert.Equal(10,structural.GetProperty("bodyCount").GetInt32());Assert.Equal(4,structural.GetProperty("cutList").GetArrayLength());Assert.True(structural.GetProperty("membersEnclosed").GetBoolean());}
        output.GetStringBuilder().Clear();Assert.Equal(0,CliRunner.Run(["inspect",source,"--json"],output,error));using var inspection=JsonDocument.Parse(output.ToString());Assert.Equal("Structural",inspection.RootElement.GetProperty("domain").GetString());
    }
    private static string FindRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"Aetheris.slnx")))d=d.Parent;return d?.FullName??throw new DirectoryNotFoundException();}
    private sealed class Temp:IDisposable{public Temp(){Path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"aetheris-x2-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path);}public string Path{get;}public void Dispose(){if(Directory.Exists(Path))Directory.Delete(Path,true);}}
}
