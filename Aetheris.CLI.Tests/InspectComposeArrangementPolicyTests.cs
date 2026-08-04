using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class InspectComposeArrangementPolicyTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void InspectCompose_CtcBlockout_ReportsMultiRegionTransitionAndM8Evidence()
    {
        var source = Path.Combine(RepoRoot, "testdata/firmament/reconstructions/nist_ctc_01/ctc01_prismatic_blockout_x2.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["inspect-compose", source, "--json"], stdout, stderr);
        Assert.Equal(0, exit); Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        using var json = JsonDocument.Parse(stdout.ToString());
        var composition = json.RootElement.GetProperty("composition");
        var placement = composition.GetProperty("placement");
        Assert.True(placement.GetProperty("isExplicit").GetBoolean());
        Assert.Equal(new[] { 0d, 0d, 0d }, placement.GetProperty("anchor").EnumerateArray().Select(x => x.GetDouble()).ToArray());
        Assert.Equal("XY", placement.GetProperty("profilePlane").GetString());
        Assert.Equal("+Z", placement.GetProperty("axis").GetString());
        Assert.Equal("+X", placement.GetProperty("referenceDirection").GetString());
        Assert.Equal("NumericalWithBound", composition.GetProperty("bRepStatus").GetString());
        Assert.True(composition.GetProperty("bRepEnclosed").GetBoolean());
        var shoulder = Assert.Single(composition.GetProperty("transitions").EnumerateArray(), item => item.GetProperty("level").GetDouble() == -60d);
        Assert.Equal(2, shoulder.GetProperty("downwardRegionCount").GetInt32());
        Assert.True(composition.GetProperty("bRepPlan").GetProperty("authoritative").GetBoolean());
    }

    [Fact]
    public void InspectCompose_InvalidArrangement_RejectsBeforeBrepReport()
    {
        var source = Path.Combine(RepoRoot, "fixtures/FirmamentV2/ProfileComposition/invalid/ambiguous-tangent-crossing.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["inspect-compose", source, "--json"], stdout, stderr);
        Assert.Equal(1, exit);
        Assert.Contains("arrangement-rejected:ambiguous-tangent-crossing", stderr.ToString(), StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(stdout.ToString()));
    }

    [Fact]
    public void InspectProfile_ComposeSource_ReportsEveryCtcProfileWithProvenance()
    {
        var source = Path.Combine(RepoRoot, "testdata/firmament/reconstructions/nist_ctc_01/ctc01_prismatic_blockout_x2.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["inspect-profile", source, "--json"], stdout, stderr);
        Assert.Equal(0, exit); Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        using var json = JsonDocument.Parse(stdout.ToString());
        var profiles = json.RootElement.GetProperty("profiles").EnumerateArray().ToArray();
        Assert.Equal(7, profiles.Length);
        Assert.All(profiles, profile => Assert.True(profile.GetProperty("isValid").GetBoolean()));
        Assert.Equal(8, profiles.Where(profile => profile.GetProperty("name").GetString()!.EndsWith("Ear", StringComparison.Ordinal)).Sum(profile => profile.GetProperty("arcSegments").GetInt32()));
        Assert.All(profiles, profile => Assert.NotEmpty(profile.GetProperty("provenance").EnumerateArray()));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
