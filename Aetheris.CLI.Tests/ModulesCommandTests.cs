using System.Text.Json;
using Xunit;

namespace Aetheris.CLI.Tests;

public sealed class ModulesCommandTests
{
    [Fact]
    public void ModulesJsonInspectsStableBuiltInsAndCapabilities()
    {
        var stdout=new StringWriter();var stderr=new StringWriter();var exit=CliRunner.Run(["modules","--json"],stdout,stderr);
        Assert.Equal(0,exit);Assert.Empty(stderr.ToString());using var json=JsonDocument.Parse(stdout.ToString());var modules=json.RootElement.EnumerateArray().ToArray();Assert.Equal(["Aetheris.Core","Aetheris.Piping","Aetheris.Surfacing","Aetheris.PlasticShell","Aetheris.SheetMetal"],modules.Select(m=>m.GetProperty("id").GetString()));Assert.Contains(modules.SelectMany(m=>m.GetProperty("capabilities").EnumerateArray()),c=>c.GetString()=="Piping.PipeRoute");Assert.Contains(modules.SelectMany(m=>m.GetProperty("capabilities").EnumerateArray()),c=>c.GetString()=="PlasticShell.Intent");
    }

    [Fact]
    public void ModulesTextMakesBuiltInNonPluginScopeExplicit()
    {
        var stdout=new StringWriter();var stderr=new StringWriter();Assert.Equal(0,CliRunner.Run(["modules"],stdout,stderr));Assert.Contains("no dynamic plugins",stdout.ToString());Assert.Contains("Aetheris.Surfacing 0.3.0",stdout.ToString());
    }
}
