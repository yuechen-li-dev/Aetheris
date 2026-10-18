using System.Text.Json;
namespace Aetheris.CLI.Tests;
public sealed class RemainingDomainAdmissionCliTests
{
    [Theory]
    [InlineData("Structural/simple-structural-frame.firmament","UnifiedStructural","valid")]
    [InlineData("Sculpting/sculpt-base-housing.firmament","UnifiedSculpting","valid")]
    [InlineData("FEA/cantilever.firmament","UnifiedAnalysis","bound-not-solved")]
    [InlineData("Drawings/bearing-block-production-drawing.firmament","UnifiedDrawing","valid")]
    public void ValidationUsesTheActualDomainAdmissionLevel(string relative,string route,string status)
    {
        var output=new StringWriter();var error=new StringWriter();Assert.Equal(0,CliRunner.Run(["validate",Fixture(relative),"--json","--explain"],output,error));Assert.Empty(error.ToString());
        using var json=JsonDocument.Parse(output.ToString());var report=json.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal(status,report.GetProperty("status").GetString());Assert.Equal(route,report.GetProperty("parserRoute").GetString());
    }
    [Theory]
    [InlineData("Structural/simple-structural-frame.firmament")]
    [InlineData("Sculpting/sculpt-base-housing.firmament")]
    [InlineData("FEA/cantilever.firmament")]
    [InlineData("PlasticShell/plastic-shell-basic.firmament")]
    public void ValidateAndBuildRejectOmittedIntentWithStructuredDiagnostics(string relative)
    {
        var directory=Path.Combine(Root(),"artifacts","local","remaining-domain-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var source=File.ReadAllText(Fixture(relative));var at=source.IndexOf('{')+1;var path=Path.Combine(directory,"unknown.firmament");File.WriteAllText(path,source.Insert(at,"\nFillet Lost { radius: 1mm }\n"));
        foreach(var command in new[]{"validate","build"})
        {
            var output=new StringWriter();var error=new StringWriter();
            Assert.Equal(1,CliRunner.Run(command=="build"?[command,path,"--out",Path.Combine(directory,"model.step"),"--json"]:[command,path,"--json"],output,error));
            using var json=JsonDocument.Parse(output.ToString());var root=json.RootElement;
            if(root.TryGetProperty("firmamentV2Validation",out var validation))root=validation;
            else if(root.TryGetProperty("plasticShellValidation",out var plastic))root=plastic;
            var diagnostics=root.GetProperty("diagnostics");Assert.NotEmpty(diagnostics.EnumerateArray());
            Assert.All(diagnostics.EnumerateArray(),d=>Assert.Equal(JsonValueKind.Object,d.ValueKind));
        }
        Assert.False(File.Exists(Path.Combine(directory,"model.step")));
    }
    [Fact]
    public void NormalBuildDispatchesAnalysisToItsRealSolver()
    {
        var directory=Path.Combine(Root(),"artifacts","local","remaining-domain-tests",Guid.NewGuid().ToString("N"));var output=new StringWriter();var error=new StringWriter();
        Assert.Equal(0,CliRunner.Run(["build",Fixture("FEA/cantilever.firmament"),"--out",directory,"--json"],output,error));
        Assert.True(File.Exists(Path.Combine(directory,"analysis-ir.json")));Assert.True(File.Exists(Path.Combine(directory,"native-results.json")));
    }
    private static string Fixture(string path)=>Path.Combine(Root(),"fixtures","Canonical",path);
    private static string Root(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"Aetheris.slnx")))d=d.Parent;return d!.FullName;}
}
