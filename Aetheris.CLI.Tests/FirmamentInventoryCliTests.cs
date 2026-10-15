using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentInventoryCliTests
{
    private static string Fixture(string name) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../fixtures/Regression/Inventory",name));

    [Theory]
    [InlineData("machine.firmasm")]
    [InlineData("factory.firmament")]
    public void NormalValidationBuildsAndAdmitsTheModuleGraph(string name)
    {
        var stdout=new StringWriter(); var stderr=new StringWriter();
        Assert.Equal(0,CliRunner.Run(["validate",Fixture(name),"--json"],stdout,stderr));
        using var json=JsonDocument.Parse(stdout.ToString());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Built",json.RootElement.GetProperty("validationLevel").GetString());
    }

    [Fact]
    public void InventoryLookupErrorIsLocatedAndNeitherValidateNorBuildProducesSuccess()
    {
        var path=Fixture("missing-member.invalid.firmament"); var stdout=new StringWriter();
        Assert.Equal(1,CliRunner.Run(["validate",path,"--json"],stdout,new StringWriter()));
        using var json=JsonDocument.Parse(stdout.ToString());
        var error=Assert.Single(json.RootElement.GetProperty("diagnostics").EnumerateArray(),d => d.GetProperty("code").GetString()=="firmament-inventory-member-unresolved");
        Assert.Equal(path,error.GetProperty("sourceSpan").GetProperty("source").GetString());
        Assert.True(error.GetProperty("sourceSpan").GetProperty("start").GetInt32()>0);
        var directory=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../artifacts/local/firmament-inventory/cli-"+Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        try
        {
            var step=Path.Combine(directory,"rejected.step");
            Assert.Equal(1,CliRunner.Run(["build",path,"--output",step,"--json"],new StringWriter(),new StringWriter()));
            Assert.False(File.Exists(step));
        }
        finally { Directory.Delete(directory,true); }
    }

    [Fact]
    public void InspectionRetainsTypedInventoryBindingsAndSourceDependencyHashes()
    {
        var stdout=new StringWriter();
        Assert.Equal(0,CliRunner.Run(["asm","inspect",Fixture("machine.firmasm"),"--json"],stdout,new StringWriter()));
        using var json=JsonDocument.Parse(stdout.ToString());
        var ir=json.RootElement.GetProperty("assemblyIr");
        Assert.Contains(ir.GetProperty("inventoryBindings").EnumerateArray(),b => b.GetProperty("name").GetString()=="Components.drive" && b.GetProperty("kind").GetString()=="Subassembly");
        Assert.Equal(4,ir.GetProperty("sourceDependencies").GetArrayLength());
    }
}
