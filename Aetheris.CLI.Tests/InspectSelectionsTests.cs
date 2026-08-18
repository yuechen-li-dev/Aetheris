using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class InspectSelectionsTests
{
    [Fact]
    public void InspectSelections_ReportsSourceGroundedClosedLoop()
    {
        var root = FindRepoRoot();
        var source = Path.Combine(root, "fixtures", "Profile", "valid", "semantic-top-boundary-loop.firmament");
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

    [Fact]
    public void InspectSelections_ReportsConstructionPlaneHoleSourceToPlanEvidence()
    {
        var root = FindRepoRoot();
        var source = Path.Combine(root, "fixtures", "Hole", "valid", "construction-plane-through-hole.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();

        var exit = Aetheris.CLI.CliRunner.Run(["inspect-selections", source, "--json"], stdout, stderr);

        Assert.Equal(0, exit); Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        using var json = JsonDocument.Parse(stdout.ToString());
        var summary = json.RootElement.GetProperty("summary");
        Assert.Equal("ConstructionPlane", summary.GetProperty("placementKind").GetString());
        Assert.Equal("construction:PositiveXWorkplane", summary.GetProperty("constructionPlaneId").GetString());
        Assert.Equal("concept:SideLayout.PositiveXDatum", summary.GetProperty("sourceConceptPlaneId").GetString());
        Assert.Equal("ThroughAll", summary.GetProperty("extent").GetString());
        Assert.Equal(100, summary.GetProperty("hostInterval")[1].GetDouble());
        Assert.Equal("LocalFrameHoleBRepPlan", json.RootElement.GetProperty("plan").GetProperty("kind").GetString());
        Assert.Contains(json.RootElement.GetProperty("descendants").EnumerateArray(), descendant => descendant.GetProperty("role").GetString() == "HoleExitLoop");
        Assert.Equal(2, json.RootElement.GetProperty("descendants").EnumerateArray().Count(descendant => descendant.GetProperty("role").GetString() == "HoleWallFace"));
        Assert.All(json.RootElement.GetProperty("selectionResults").EnumerateArray(), result => Assert.True(result.GetProperty("succeeded").GetBoolean()));
    }

    [Fact]
    public void InspectSelections_ComposedBlindDrillReportsConservativeClearanceContract()
    {
        var root = FindRepoRoot();
        var source = Path.Combine(root, "fixtures", "ProfileComposition", "valid", "construction-plane-blind-drill-clearance.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();

        var exit = Aetheris.CLI.CliRunner.Run(["inspect-selections", source, "--json"], stdout, stderr);

        Assert.Equal(0, exit); Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        using var json = JsonDocument.Parse(stdout.ToString());
        var contract = json.RootElement.GetProperty("holeContract");
        Assert.Equal("FullRadiusThroughTotalDepth", contract.GetProperty("validationPolicy").GetString());
        Assert.True(contract.GetProperty("contractSatisfied").GetBoolean());
        Assert.Equal("CorridorProven", contract.GetProperty("hostTraversalClassification").GetString());
        Assert.All(contract.GetProperty("chordProofs").EnumerateArray(), proof => Assert.Equal("FullRadiusClearance", proof.GetProperty("toolPart").GetString()));
        Assert.Equal(5, json.RootElement.GetProperty("selections").GetArrayLength());
        Assert.All(json.RootElement.GetProperty("selections").EnumerateArray(), result => Assert.True(result.GetProperty("succeeded").GetBoolean()));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
