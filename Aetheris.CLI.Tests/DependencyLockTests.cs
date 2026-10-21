using System.Text.Json;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class DependencyLockTests
{
    [Fact]
    public void RealCliLocksValidatesAndBuildsWithoutMutatingTheReceipt()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        var directory = Path.Combine(root!.FullName,"artifacts/local/imported-resource-modules/tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var source = Path.Combine(directory,"main.firmasm");
        var asset = Path.Combine(directory,"bearing.step");
        File.WriteAllText(asset,Step242Exporter.ExportBody(BrepPrimitives.CreateBox(4,6,8).Value).Value);
        File.WriteAllText(source,"include bearing: Part = Step(\"bearing\"); Assembly Machine { <Assembly Machine><Part A = bearing>Placement { from: Origin; to: World; }</Part></Assembly> Anchor: Machine; }");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        int Run(params string[] args) { stdout.GetStringBuilder().Clear(); stderr.GetStringBuilder().Clear(); return CliRunner.Run(args,stdout,stderr); }
        Assert.Equal(1,Run("validate",source,"--locked","--json"));
        Assert.Contains("firmament-lock-required",stdout.ToString());
        Assert.Equal(0,Run("lock",source,"--json"));
        var path = Path.Combine(directory,"firmament.lock.toml");
        var receipt = File.ReadAllText(path);
        Assert.Equal(0,Run("validate",source,"--locked","--json"));
        Assert.Equal(0,Run("build",source,"--locked","--output",Path.Combine(directory,"machine.step"),"--json"));
        Assert.True(Step242AssemblyImporter.Import(File.ReadAllText(Path.Combine(directory,"machine.step"))).IsSuccess);
        Assert.Equal(receipt,File.ReadAllText(path));
        File.AppendAllText(asset,"\n");
        foreach (var command in new[]{"validate","build"})
        {
            Assert.Equal(1,Run(command,source,"--locked","--json"));
            using var result = JsonDocument.Parse(stdout.ToString());
            Assert.False(result.RootElement.GetProperty("success").GetBoolean());
            Assert.Contains(result.RootElement.GetProperty("diagnostics").EnumerateArray(),d => d.GetProperty("code").GetString()=="firmament-lock-dependency-mismatch");
        }
        Assert.Equal(1,Run("lock",source,"--json"));
        Assert.Equal(receipt,File.ReadAllText(path));
        Assert.Equal(0,Run("lock",source,"--update","--json"));
        Assert.NotEqual(receipt,File.ReadAllText(path));
        Assert.Equal(0,Run("validate",source,"--locked","--json"));
    }
}
