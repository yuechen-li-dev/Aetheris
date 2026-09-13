using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class SemanticSymmetryCliTests
{
    [Fact]
    public void Inspect_reports_mirror_and_radial_provenance()
    {
        var mirrorOut = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["inspect", Fixture("Canonical", "Symmetry", "mirrored-hole.firmament"), "--json"], mirrorOut, new StringWriter()));
        using var mirrorJson = JsonDocument.Parse(mirrorOut.ToString());
        var mirror = Assert.Single(mirrorJson.RootElement.GetProperty("mirrors").EnumerateArray());
        Assert.Equal("LeftMount", mirror.GetProperty("source").GetString());
        Assert.Equal("RightMount", mirror.GetProperty("destination").GetString());
        Assert.Equal("CanonicalRightHanded", mirror.GetProperty("handednessCorrection").GetString());

        var radialOut = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["inspect", Fixture("Canonical", "Symmetry", "radial-bolt-circle.firmament"), "--json"], radialOut, new StringWriter()));
        using var radialJson = JsonDocument.Parse(radialOut.ToString());
        var radial = Assert.Single(radialJson.RootElement.GetProperty("radialPatterns").EnumerateArray());
        Assert.Equal(6, radial.GetProperty("count").GetInt32());
        Assert.Equal("FullOpenEndpoint", radial.GetProperty("distribution").GetString());
        Assert.Equal(Enumerable.Range(0, 6).Select(index => $"BoltCircle.Instance{index}"),
            radial.GetProperty("instanceTransforms").EnumerateArray().Select(instance => instance.GetProperty("identity").GetString()));
    }

    [Fact]
    public void Inspect_resolves_semantic_boundary_then_mirrored_profile_for_revolve()
    {
        var stdout = new StringWriter();
        Assert.Equal(0, CliRunner.Run(["inspect", Fixture("Canonical", "Symmetry", "mirrored-revolve.firmament"), "--json"], stdout, new StringWriter()));
        using var json = JsonDocument.Parse(stdout.ToString());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("Revolve", json.RootElement.GetProperty("domain").GetString());
        var revolve = json.RootElement.GetProperty("revolve");
        Assert.Equal("UpperSection", revolve.GetProperty("profile").GetString());
        Assert.Equal(180d, revolve.GetProperty("sweepDegrees").GetDouble(), 10);
    }

    [Theory]
    [InlineData("count-zero.firmament", "firmament-symmetry-radial-count-invalid")]
    [InlineData("invalid-axis.firmament", "firmament-symmetry-radial-axis-invalid")]
    [InlineData("invalid-angle.firmament", "firmament-symmetry-radial-angle-invalid")]
    [InlineData("invalid-plane.firmament", "firmament-symmetry-plane-invalid")]
    [InlineData("identity-collision.firmament", "firmament-symmetry-identity-collision")]
    [InlineData("unsupported-mirror-type.firmament", "firmament-symmetry-type-unsupported")]
    [InlineData("unsupported-selector.firmament", "firmament-symmetry-feature-center-unsupported")]
    public void Invalid_symmetry_fixtures_are_fatal(string name, string code)
    {
        var stdout = new StringWriter();
        Assert.Equal(1, CliRunner.Run(["validate", Fixture("Invalid", "Symmetry", name), "--json"], stdout, new StringWriter()));
        using var json = JsonDocument.Parse(stdout.ToString());
        var report = json.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("invalid", report.GetProperty("status").GetString());
        Assert.Contains(report.GetProperty("diagnostics").EnumerateArray(), diagnostic =>
            diagnostic.GetProperty("code").GetString()!.StartsWith(code, StringComparison.Ordinal)
            && diagnostic.GetProperty("severity").GetString() == "fatal");
    }

    private static string Fixture(params string[] parts) =>
        Path.GetFullPath(Path.Combine([AppContext.BaseDirectory, "../../../../fixtures", .. parts]));
}
