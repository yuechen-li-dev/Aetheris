using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class NamedSetCliTests
{
    [Fact]
    public void Inspect_ReportsNamedEntriesAndPatternAssociations()
    {
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["inspect", Fixture("Canonical", "Set", "mounting-points.firmament"), "--json"], stdout, stderr);

        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, stderr.ToString());
        using var json = JsonDocument.Parse(stdout.ToString());
        var set = Assert.Single(json.RootElement.GetProperty("sets").EnumerateArray());
        Assert.Equal("MountPoints", set.GetProperty("setId").GetString());
        Assert.Equal("Point2", set.GetProperty("elementType").GetString());
        Assert.Equal(4, set.GetProperty("count").GetInt32());
        Assert.Equal(["LowerLeft", "LowerRight", "UpperLeft", "UpperRight"],
            set.GetProperty("entries").EnumerateArray().Select(entry => entry.GetProperty("name").GetString()));
        var polygon = Assert.Single(json.RootElement.GetProperty("polygons").EnumerateArray());
        Assert.Equal("MountBoundaryShape", polygon.GetProperty("name").GetString());
        Assert.Equal("Rhombus", polygon.GetProperty("variant").GetString());
        Assert.Equal([90d, 64d], polygon.GetProperty("diagonals").EnumerateArray().Select(value => value.GetDouble()));
        var pattern = Assert.Single(json.RootElement.GetProperty("patterns").EnumerateArray());
        Assert.Equal("Mounts.UpperRight", pattern.GetProperty("associations")[3].GetProperty("generatedId").GetString());
    }

    [Fact]
    public void Validate_OutsideEntryUsesNamedPatternIdentity()
    {
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["validate", Fixture("Invalid", "Set", "pattern-span-containment.firmament"), "--json"], stdout, stderr);

        Assert.Equal(1, exit);
        Assert.Contains("firmament-feature-footprint-outside-span:Mounts.UpperRight:MountingArea", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Inspect_ReportsClosedBoundaryFamilyAndStableGuides()
    {
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["inspect", Fixture("Canonical", "Boundary2", "closed-boundary-family.firmament"), "--json"], stdout, stderr);
        Assert.Equal(0, exit); Assert.Equal(string.Empty, stderr.ToString());
        using var json = JsonDocument.Parse(stdout.ToString());
        var boundaries = json.RootElement.GetProperty("boundaries").EnumerateArray().ToArray();
        Assert.Equal(15, boundaries.Length);
        Assert.All(boundaries, x => Assert.Equal("ClosedBoundary2", x.GetProperty("capability").GetString()));
        var rounded = boundaries.Single(x => x.GetProperty("shapeId").GetString() == "MountingRegion");
        Assert.Equal("RoundedRect2", rounded.GetProperty("shapeType").GetString());
        Assert.Contains("TopRightCorner", rounded.GetProperty("generatedGuides").EnumerateArray().Select(x => x.GetString()));
        var hex = boundaries.Single(x => x.GetProperty("shapeId").GetString() == "Hex");
        Assert.Equal(6d, hex.GetProperty("dimensions").GetProperty("VertexCount").GetDouble());
    }

    private static string Fixture(params string[] parts) =>
        Path.GetFullPath(Path.Combine([AppContext.BaseDirectory, "../../../../fixtures", .. parts]));
}
