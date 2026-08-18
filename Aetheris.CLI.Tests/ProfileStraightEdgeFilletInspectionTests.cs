using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class ProfileStraightEdgeFilletInspectionTests
{
    [Fact]
    public void InspectProfile_ReportsDeterministicStraightFilletPlan()
    {
        var source = Path.Combine(FindRepoRoot(), "fixtures", "Canonical", "valid", "profile-straight-edge-fillet-top.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["inspect-profile", source, "--json"], stdout, stderr);

        Assert.Equal(0, exit); Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        using var json = JsonDocument.Parse(stdout.ToString());
        var fillet = json.RootElement.GetProperty("profile").GetProperty("straightEdgeFillet");
        Assert.True(fillet.GetProperty("succeeded").GetBoolean());
        Assert.Equal("edgefinish:SouthTopRound", fillet.GetProperty("edgeFinishId").GetString());
        Assert.Equal("FilletSpanInset", fillet.GetProperty("endpointPolicy").GetString());
        Assert.Equal(new[] { 1d, 0d, 0d }, fillet.GetProperty("cylinderAxis").EnumerateArray().Select(x => x.GetDouble()).ToArray());
        Assert.Equal(new[] { 3d, 2d, 6d }, fillet.GetProperty("cylinderCenterlineStart").EnumerateArray().Select(x => x.GetDouble()).ToArray());
        Assert.Equal("DisjointNoCavitiesInBareProfileM1", fillet.GetProperty("corridorClassification").GetString());
        Assert.Contains(fillet.GetProperty("generatedDescendants").EnumerateArray().Select(x => x.GetString()), x => x!.EndsWith(":FilletSurface", StringComparison.Ordinal));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
