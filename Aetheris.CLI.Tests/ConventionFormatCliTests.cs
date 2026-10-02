using System.Text.Json;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.CLI.Tests;

public sealed class ConventionFormatCliTests
{
    [Fact]
    public void ConventionBatchIsDryByDefaultPreparesBeforeWritesAndIsIdempotent()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        var output = Path.Combine(directory!.FullName, "artifacts", "local", "casing", "cli-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(output);
        var path = Path.Combine(output, "part.firmament");
        const string source = "Model Witness { Units: mm; Box Body { Size: [10mm,8mm,4mm] } }";
        File.WriteAllText(path, source);
        var stdout = new StringWriter(); var stderr = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["format", path, "--json"], stdout, stderr));
        Assert.Equal(source, File.ReadAllText(path));
        Assert.Equal(1, CliRunner.Run(["format", path, Path.Combine(output, "missing.firmament"), "--write"], stdout, stderr));
        Assert.Equal(source, File.ReadAllText(path));
        stdout.GetStringBuilder().Clear(); stderr.GetStringBuilder().Clear();
        Assert.Equal(0, CliRunner.Run(["format", path, "--write", "--json"], stdout, stderr));
        var preferred = File.ReadAllText(path);
        Assert.Contains("Box Body { size:", preferred);
        Assert.True(FirmamentV2Parser.Parse(preferred).IsSuccess);
        stdout.GetStringBuilder().Clear();
        Assert.Equal(0, CliRunner.Run(["format", path, "--write", "--json"], stdout, stderr));
        using var json = JsonDocument.Parse(stdout.ToString());
        Assert.False(json.RootElement.GetProperty("files")[0].GetProperty("changed").GetBoolean());
        Assert.Equal(preferred, File.ReadAllText(path));
    }
}
