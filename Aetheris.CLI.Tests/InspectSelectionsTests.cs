using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class InspectSelectionsTests
{
    [Fact]
    public void InspectSelections_ReportsSourceGroundedClosedLoop()
    {
        var root = FindRepoRoot();
        var source = Path.Combine(root, "fixtures", "FirmamentV2", "Profile", "valid", "semantic-top-boundary-loop.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["inspect-selections", source, "--json"], stdout, stderr);
        Assert.Equal(0, exit); Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        using var json = JsonDocument.Parse(stdout.ToString());
        var selection = json.RootElement.GetProperty("selections").EnumerateArray().Single();
        Assert.True(selection.GetProperty("succeeded").GetBoolean());
        Assert.True(selection.GetProperty("connectivity").GetProperty("isClosed").GetBoolean());
        Assert.Equal(4, selection.GetProperty("materializedDescendants").GetArrayLength());
        Assert.Equal("EdgeFinish", selection.GetProperty("consumer").GetString());
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
