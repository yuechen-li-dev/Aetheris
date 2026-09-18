using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class ContinuumToolsCliTests
{
    [Fact]
    public void HelpIsDiscoverableForEveryBoundedSubcommand()
    {
        foreach(var command in new[]{"pat-query","contour","make-fixtures"})
        {
            var stdout=new StringWriter();var stderr=new StringWriter();
            Assert.Equal(0,CliRunner.Run(["continuum",command,"--help"],stdout,stderr));
            Assert.Contains("Usage: aetheris continuum",stdout.ToString());Assert.Equal(string.Empty,stderr.ToString());
        }
    }

    [Fact]
    public void GeneratedFixturesRunThroughPatAndContourCommands()
    {
        var root=Path.Combine(Path.GetTempPath(),"aetheris-continuum-cli-"+Guid.NewGuid().ToString("N"));
        try
        {
            AssertRun(["continuum","make-fixtures","--out-dir",root]);
            var patOutput=Path.Combine(root,"results.json");
            AssertRun(["continuum","pat-query",Path.Combine(root,"sphere-point-tori.json"),Path.Combine(root,"sphere-queries.json"),"--out",patOutput]);
            var mesh=Path.Combine(root,"box.obj");var report=Path.Combine(root,"box-report.json");
            AssertRun(["continuum","contour",Path.Combine(root,"sharp-box-grid.json"),"--out",mesh,"--report",report]);
            Assert.True(File.Exists(patOutput));Assert.True(File.Exists(mesh));Assert.True(File.Exists(report));
            using var json=JsonDocument.Parse(File.ReadAllText(report));
            Assert.Equal(0,json.RootElement.GetProperty("diagnostics").GetProperty("boundaryEdges").GetInt32());
            Assert.Equal(0,json.RootElement.GetProperty("diagnostics").GetProperty("nonManifoldEdges").GetInt32());
        }
        finally
        {
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }

    private static void AssertRun(string[] args)
    {
        var stdout=new StringWriter();var stderr=new StringWriter();var exit=CliRunner.Run(args,stdout,stderr);
        Assert.True(exit==0,$"Exit {exit}. stderr: {stderr} stdout: {stdout}");
    }
}
