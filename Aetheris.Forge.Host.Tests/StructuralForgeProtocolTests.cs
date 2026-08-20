using System.Text.Json;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Forge.Host.Tests;

public sealed class StructuralForgeProtocolTests
{
    [Fact]
    public void Workbench_ListDescribeInvoke_UsesProtocolV1AndReturnsAssemblyAndCutList()
    {
        var host=new ForgeProtocolHost();const string id="Standard.Structural.WeldedWorkbench";
        Assert.Contains(host.ListTemplates().Templates,x=>x.Id==id);var description=Assert.IsType<ForgeTemplateDescription>(host.DescribeTemplate(id));Assert.Equal(6,description.Parameters.Single().Fields!.Count);Assert.Contains(ForgeArtifactKind.CutListJson,description.Artifacts);
        using var output=new Temp();var args=new Dictionary<string,JsonElement>{{"width",JsonSerializer.SerializeToElement("1000 mm")},{"depth",JsonSerializer.SerializeToElement("600 mm")},{"height",JsonSerializer.SerializeToElement("800 mm")},{"tubeSize",JsonSerializer.SerializeToElement("40 mm")},{"wallThickness",JsonSerializer.SerializeToElement("3 mm")},{"material",JsonSerializer.SerializeToElement("Standard.Materials.Steel.ASTM_A36")}};
        var result=host.InvokeTemplate(id,new(1,args,[ForgeArtifactKind.StepAp242,ForgeArtifactKind.CutListJson]),output.Path);Assert.True(result.Success,string.Join(Environment.NewLine,result.Diagnostics.Select(x=>x.Code+": "+x.Message)));Assert.Equal(2,result.Artifacts.Count);
        var step=File.ReadAllText(Path.Combine(output.Path,result.Artifacts.Single(x=>x.Kind==ForgeArtifactKind.StepAp242).Path));var imported=Step242AssemblyImporter.Import(step);Assert.True(imported.IsSuccess);Assert.Equal(10,imported.Value.Occurrences.Count);
        using var cut=JsonDocument.Parse(File.ReadAllText(Path.Combine(output.Path,result.Artifacts.Single(x=>x.Kind==ForgeArtifactKind.CutListJson).Path)));Assert.Equal(4,cut.RootElement.GetProperty("entries").GetArrayLength());
    }
    private sealed class Temp:IDisposable{public Temp(){Path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"forge-x2-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path);}public string Path{get;}public void Dispose(){if(Directory.Exists(Path))Directory.Delete(Path,true);}}
}
