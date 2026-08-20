using System.Text.Json;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Forge.Host.Tests;

public sealed class PipingForgeProtocolTests
{
    [Fact]
    public void PumpSkid_ListDescribeInvoke_UsesProtocolV1AndReturnsRoutedAssembly()
    {
        const string id="Standard.Piping.PumpSkid";var host=new ForgeProtocolHost();
        Assert.Contains(host.ListTemplates().Templates,x=>x.Id==id);
        var description=Assert.IsType<ForgeTemplateDescription>(host.DescribeTemplate(id));
        Assert.Equal(4,description.Parameters.Single().Fields!.Count);Assert.Equal([ForgeArtifactKind.StepAp242],description.Artifacts);
        var args=new Dictionary<string,JsonElement>{{"outerDiameter",JsonSerializer.SerializeToElement("25 mm")},{"wallThickness",JsonSerializer.SerializeToElement("2 mm")},{"clearance",JsonSerializer.SerializeToElement("30 mm")},{"material",JsonSerializer.SerializeToElement("Standard.Materials.StainlessSteel.304_Annealed")}};
        using var output=new Temp();var result=host.InvokeTemplate(id,new(1,args,[ForgeArtifactKind.StepAp242]),output.Path);
        Assert.True(result.Success,string.Join(Environment.NewLine,result.Diagnostics.Select(x=>x.Code+": "+x.Message)));
        var artifact=Assert.Single(result.Artifacts);Assert.Equal("pump-skid.step",artifact.Path);
        var imported=Step242AssemblyImporter.Import(File.ReadAllText(Path.Combine(output.Path,artifact.Path)));
        Assert.True(imported.IsSuccess,string.Join(Environment.NewLine,imported.Diagnostics.Select(x=>x.Message)));
        Assert.True(imported.Value.Occurrences.Count>=5);
        Assert.Equal(2,imported.Value.Definitions.Count(x=>x.Name.StartsWith("nozzle:",StringComparison.Ordinal)));
    }
    private sealed class Temp:IDisposable{public Temp(){Path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"forge-x3-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path);}public string Path{get;}public void Dispose(){if(Directory.Exists(Path))Directory.Delete(Path,true);}}
}
