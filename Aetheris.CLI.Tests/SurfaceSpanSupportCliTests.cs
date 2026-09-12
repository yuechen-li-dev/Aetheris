using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class SurfaceSpanSupportCliTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void InspectSpans_ReportsResolvedSurfaceDomainAndExpandedConsumers()
    {
        var source = Fixture("Canonical", "Span", "plane-hole-support.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["inspect-spans", source, "--json"], stdout, stderr);

        Assert.Equal(0, exit);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        using var json = JsonDocument.Parse(stdout.ToString());
        var span = Assert.Single(json.RootElement.GetProperty("spans").EnumerateArray());
        Assert.Equal("MountingArea", span.GetProperty("spanId").GetString());
        Assert.Equal("TopSupport", span.GetProperty("parentId").GetString());
        Assert.Equal(2880d, span.GetProperty("area").GetDouble(), 8);
        Assert.Equal(4, span.GetProperty("consumerReferences").GetArrayLength());
        Assert.Equal("Valid", span.GetProperty("validity").GetString());
    }

    [Fact]
    public void InspectCompose_ReportsPatternFeatureSpanAndParentSupport()
    {
        var source = Fixture("Canonical", "Span", "plane-hole-support.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["inspect-compose", source, "--json"], stdout, stderr);

        Assert.Equal(0, exit);
        Assert.True(string.IsNullOrWhiteSpace(stderr.ToString()), stderr.ToString());
        using var json = JsonDocument.Parse(stdout.ToString());
        var holes = json.RootElement.GetProperty("composition").GetProperty("counterboreHoles").EnumerateArray().ToArray();
        Assert.Equal(4, holes.Length);
        Assert.All(holes, hole =>
        {
            var support = hole.GetProperty("support");
            Assert.Equal("Span<Plane>", support.GetProperty("kind").GetString());
            Assert.Equal("MountingArea", support.GetProperty("id").GetString());
            Assert.Equal("TopSupport", support.GetProperty("parent").GetString());
            Assert.True(support.GetProperty("boundaryMargin").GetDouble() > 0d);
        });
    }

    [Fact]
    public void Validate_CrossingPatternInstance_FailsWithTypedMarginDiagnostic()
    {
        var source = Fixture("Invalid", "Span", "plane-hole-footprint-crossing.firmament");
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["validate", source, "--json"], stdout, stderr);

        Assert.Equal(1, exit);
        using var json = JsonDocument.Parse(stdout.ToString());
        var diagnostics = json.RootElement.GetProperty("firmamentV2Validation").GetProperty("diagnostics").EnumerateArray();
        Assert.Contains(diagnostics, item => item.GetProperty("code").GetString()!
            .StartsWith("firmament-feature-footprint-outside-span:MountPattern_3:MountingArea", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_CrossingPatternInstance_IsAtomicAndWritesNoStep()
    {
        var output = Path.Combine(Path.GetTempPath(), $"invalid-span-{Guid.NewGuid():N}.step");
        try
        {
            var stdout = new StringWriter(); var stderr = new StringWriter();
            var exit = Aetheris.CLI.CliRunner.Run([
                "build", Fixture("Invalid", "Span", "plane-hole-footprint-crossing.firmament"), "--output", output, "--json"
            ], stdout, stderr);

            Assert.Equal(1, exit);
            Assert.False(File.Exists(output));
            Assert.Contains("firmament-feature-footprint-outside-span:MountPattern_3:MountingArea", stdout.ToString() + stderr, StringComparison.Ordinal);
        }
        finally { if (File.Exists(output)) File.Delete(output); }
    }

    [Theory]
    [InlineData("plane-boundary-open.firmament", "firmament-span-surface-boundary-open:InvalidArea")]
    [InlineData("plane-boundary-off-parent.firmament", "firmament-span-surface-boundary-off-parent:InvalidArea")]
    public void Validate_InvalidSurfaceBoundary_IsFatal(string file, string expected)
    {
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["validate", Fixture("Invalid", "Span", file), "--json"], stdout, stderr);

        Assert.Equal(1, exit);
        using var json = JsonDocument.Parse(stdout.ToString());
        var report = json.RootElement.GetProperty("firmamentV2Validation");
        Assert.Equal("invalid", report.GetProperty("status").GetString());
        Assert.Contains(report.GetProperty("diagnostics").EnumerateArray(), item =>
            item.GetProperty("code").GetString() == expected && item.GetProperty("severity").GetString() == "fatal");
    }

    private static string Fixture(params string[] parts) => Path.Combine([RepoRoot, "fixtures", .. parts]);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
