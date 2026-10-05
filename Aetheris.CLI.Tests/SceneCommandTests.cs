using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class SceneCommandTests
{
    [Fact]
    public void RealSceneInspectionExportsAndStepBoundaryAreExplicit()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null && !File.Exists(Path.Combine(root.FullName,"Aetheris.slnx"))) root=root.Parent;
        Assert.NotNull(root);
        var input=Path.Combine(root!.FullName,"fixtures/Canonical/Scene/room.firmament");
        var output=Path.Combine(root.FullName,"artifacts/local/tests/scene",Guid.NewGuid().ToString("N")+".glb");
        var stdout=new StringWriter(); var stderr=new StringWriter();
        Assert.Equal(0,CliRunner.Run(["inspect",input,"--json"],stdout,stderr));
        using(var report=JsonDocument.Parse(stdout.ToString())) Assert.Equal("mm",report.RootElement.GetProperty("internalUnits").GetString());
        stdout.GetStringBuilder().Clear();
        Assert.True(CliRunner.Run(["scene","export-glb",input,output,"--json"],stdout,stderr) == 0,stderr.ToString());
        var bytes=File.ReadAllBytes(output);
        stdout.GetStringBuilder().Clear();
        Assert.Equal(0,CliRunner.Run(["scene","export-glb",input,output,"--hide-boundary","hall.ceiling","--json"],stdout,stderr));
        using(var report=JsonDocument.Parse(stdout.ToString()))
            Assert.Equal("hall.ceiling",report.RootElement.GetProperty("hiddenBoundaries")[0].GetString());
        bytes=File.ReadAllBytes(output);
        Assert.NotEqual(0,CliRunner.Run(["scene","export-glb",input,output,"--hide-boundary","missing.ceiling"],stdout,stderr));
        Assert.Equal(bytes,File.ReadAllBytes(output));
        Assert.NotEqual(0,CliRunner.Run(["scene","export-glb",input,output,"--repeat","0"],stdout,stderr));
        Assert.Equal(bytes,File.ReadAllBytes(output));
        Assert.NotEqual(0,CliRunner.Run(["build",input,"--json"],stdout,stderr));
        Assert.Contains("scene-step-export-unsupported",stderr.ToString());
        stdout.GetStringBuilder().Clear(); Assert.Equal(0,CliRunner.Run(["validate",input,"--json"],stdout,stderr));
    }
}
