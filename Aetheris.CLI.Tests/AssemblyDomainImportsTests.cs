using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class AssemblyDomainImportsTests
{
    [Theory]
    [InlineData("network-appliance-product-m6.firmasm")]
    [InlineData("network-appliance-product-m7.firmasm")]
    public void SheetMetalAssemblyAdmitsStepAndReportsDisplayCapabilityAfterDomainBinding(string name)
    {
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../.."));
        var source = Path.Combine(repo,"fixtures/Compatibility/Firmasm/SheetMetal",name);
        var output = Path.Combine(repo,"artifacts/local/editable-v8-modules/cli-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try {
            foreach (var command in new[] { new[]{"asm","inspect",source,"--json"},new[]{"validate",source,"--json"} }) {
                var stdout = new StringWriter(); var stderr = new StringWriter();
                Assert.Equal(0,CliRunner.Run(command,stdout,stderr));
                using var json = JsonDocument.Parse(stdout.ToString());
                Assert.True(json.RootElement.GetProperty("success").GetBoolean());
                if(command[0]=="validate") Assert.Equal("Built",json.RootElement.GetProperty("validationLevel").GetString());
            }
            var step=Path.Combine(output,"product.step");
            Assert.Equal(0,CliRunner.Run(["build",source,"--output",step,"--json"],new StringWriter(),new StringWriter()));
            Assert.Contains("ISO-10303-21",File.ReadAllText(step));
            var glb=Path.Combine(output,"product.glb");
            var displayError = new StringWriter();
            Assert.Equal(1,CliRunner.Run(["asm","export-glb",source,glb,"--json"],new StringWriter(),displayError));
            Assert.Contains("assembly-mesh-definition-failed",displayError.ToString());
            Assert.False(File.Exists(glb));
        } finally { Directory.Delete(output,true); }
    }
}
