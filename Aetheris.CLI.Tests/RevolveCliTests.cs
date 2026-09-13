using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class RevolveCliTests
{
    private static readonly string Repo = FindRepo();

    [Fact]
    public void InspectReportsBoundedAngleAxisAndFrame()
    {
        var output = new StringWriter(); var error = new StringWriter();
        var exit = CliRunner.Run(["inspect", Fixture("Canonical/Revolve/partial-quarter.firmament"), "--json"], output, error);
        Assert.Equal(0, exit); Assert.Empty(error.ToString());
        using var json = JsonDocument.Parse(output.ToString()); var root = json.RootElement;
        Assert.Equal("Revolve", root.GetProperty("domain").GetString());
        var revolve = root.GetProperty("revolve");
        Assert.Equal(90d, revolve.GetProperty("sweepDegrees").GetDouble());
        Assert.Equal("quarter", revolve.GetProperty("alias").GetString());
        Assert.Equal("Partial", revolve.GetProperty("classification").GetString());
        Assert.Equal("MainAxis", revolve.GetProperty("axis").GetString());
        Assert.Equal("XY", revolve.GetProperty("profileFrame").GetString());
    }

    [Fact]
    public void ValidateRejectsZeroAngleAsFatalSemanticDiagnostic()
    {
        var output = new StringWriter(); var error = new StringWriter();
        var exit = CliRunner.Run(["validate", Fixture("Invalid/Revolve/zero-angle.firmament"), "--json"], output, error);
        Assert.Equal(1, exit); Assert.Empty(error.ToString());
        using var json = JsonDocument.Parse(output.ToString());
        var validation = json.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("invalid", validation.GetProperty("status").GetString());
        Assert.Contains(validation.GetProperty("diagnostics").EnumerateArray(), item => item.GetString() == "firmament-revolve-zero-angle:Bad");
    }

    private static string Fixture(string relative) => Path.Combine(Repo, "fixtures", relative.Replace('/', Path.DirectorySeparatorChar));
    private static string FindRepo()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
