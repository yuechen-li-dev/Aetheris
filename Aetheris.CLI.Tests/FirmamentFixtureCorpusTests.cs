using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentFixtureCorpusTests
{
    private static readonly string CorpusRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Firmament"));
    private static readonly string[] RequiredMetadata = ["fixture-id", "case", "category", "validity", "implementation", "expected", "expected-stage"];

    [Fact]
    public void FirmamentFixtureCorpus_Metadata_IsRecognized()
    {
        var fixtures = DiscoverFixtures();
        Assert.NotEmpty(fixtures);
        foreach (var path in fixtures)
        {
            var fixture = LoadMetadata(path);
            foreach (var key in RequiredMetadata) Assert.True(fixture.ContainsKey(key), $"{path} missing {key}");
            Assert.Contains(fixture["validity"], new[] { "valid", "invalid" });
            Assert.Contains(fixture["implementation"], new[] { "implemented", "not-implemented", "deferred", "rejected" });
        }
    }

    [Fact]
    public void FirmamentFixtureCorpus_ValidImplementedFixtures_ReachExpectedStage()
    {
        foreach (var path in DiscoverFixtures().Where(IsValidImplemented))
        {
            using var doc = Trace(path);
            var root = doc.RootElement;
            Assert.True(root.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean(), path);
            Assert.Equal(LoadMetadata(path)["expected-stage"], root.GetProperty("actualStageReached").GetString());
        }
    }

    [Fact]
    public void FirmamentFixtureCorpus_ValidNotImplementedFixtures_ReportNotImplemented()
    {
        foreach (var path in DiscoverFixtures().Where(IsValidNotImplemented))
        {
            using var doc = Trace(path);
            var root = doc.RootElement;
            Assert.Equal("valid", root.GetProperty("fixture").GetProperty("expectation").GetString());
            Assert.True(root.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean(), path);
            Assert.Contains(root.GetProperty("actualStageReached").GetString(), new[] { "not-implemented", "deferred" });
            Assert.Contains("air-firmament-a1-feature-not-implemented", root.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()));
        }
    }

    [Fact]
    public void FirmamentFixtureCorpus_InvalidFixtures_ReportExpectedDiagnostics()
    {
        foreach (var path in DiscoverFixtures().Where(p => LoadMetadata(p)["expected"] == "invalid"))
        {
            var fixture = LoadMetadata(path);
            using var doc = Trace(path);
            var diagnostics = doc.RootElement.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.True(doc.RootElement.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean(), path);
            Assert.Contains(fixture["expected-diagnostic"], diagnostics);
        }
    }

    [Fact]
    public void FirmamentFixtureCorpus_SideHoleGoldenPath_RemainsIntegrated()
    {
        var dir = Path.Combine(Path.GetTempPath(), "aetheris-corpus-tests", Guid.NewGuid().ToString("N"));
        using var doc = Trace(Path.Combine(CorpusRoot, "Region/valid/side-hole-face-attached-region.valid.firmfixture"), "--out-dir", dir);
        var root = doc.RootElement;
        Assert.Equal("region-parent-integrated", root.GetProperty("actualStageReached").GetString());
        Assert.Contains("Integrated", root.GetProperty("regions").GetRawText(), StringComparison.Ordinal);
        Assert.Contains("Closed", root.GetProperty("regions").GetRawText(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(dir, "side-hole.step")));
        Assert.Contains("STEP smoke: succeeded", File.ReadAllText(Path.Combine(dir, "side-hole.trace.txt")), StringComparison.Ordinal);
    }

    [Fact]
    public void FirmamentFixtureCorpus_Box_RemainsParserBacked()
    {
        using var doc = Trace(Path.Combine(CorpusRoot, "Primitive/valid/box.valid.firmfixture"));
        var root = doc.RootElement;
        Assert.True(root.GetProperty("fixture").GetProperty("parserBacked").GetBoolean());
        Assert.Equal("emitted-brep", root.GetProperty("actualStageReached").GetString());
        Assert.Equal("CreateBox", root.GetProperty("featureAir").GetProperty("nodeKind").GetString());
    }

    [Fact]
    public void FirmamentFixtureCorpus_DoesNotRequireFeatureImplementation()
    {
        var future = Path.Combine(CorpusRoot, "Fillet/future/single-edge-fillet.valid.firmfixture");
        using var doc = Trace(future);
        Assert.Equal("not-implemented", doc.RootElement.GetProperty("actualStageReached").GetString());
        Assert.False(doc.RootElement.GetProperty("emission").GetProperty("succeeded").GetBoolean());
    }

    [Fact]
    public void FirmamentFixtureCorpus_Metadata_IsDeterministic()
    {
        var first = DiscoverFixtures().Select(p => p.Replace('\\', '/')).ToArray();
        var second = DiscoverFixtures().Select(p => p.Replace('\\', '/')).ToArray();
        Assert.Equal(first, second);
        Assert.Equal(Trace(Path.Combine(CorpusRoot, "Material/future/material-assignment.valid.firmfixture")).RootElement.GetRawText(), Trace(Path.Combine(CorpusRoot, "Material/future/material-assignment.valid.firmfixture")).RootElement.GetRawText());
    }

    private static bool IsValidImplemented(string path)
    {
        var fixture = LoadMetadata(path);
        return fixture["expected"] == "valid" && fixture["implementation"] == "implemented";
    }

    private static bool IsValidNotImplemented(string path)
    {
        var fixture = LoadMetadata(path);
        return fixture["expected"] == "valid" && fixture["implementation"] is "not-implemented" or "deferred";
    }

    private static IReadOnlyDictionary<string, string> LoadMetadata(string path)
    {
        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (!line.StartsWith("//", StringComparison.Ordinal)) break;
            var body = line[2..].Trim();
            var colon = body.IndexOf(':');
            if (colon <= 0) continue;
            metadata[body[..colon].Trim()] = body[(colon + 1)..].Trim();
        }
        return metadata;
    }

    private static string[] DiscoverFixtures() => Directory.EnumerateFiles(CorpusRoot, "*.firmfixture", SearchOption.AllDirectories).OrderBy(p => p.Replace('\\', '/'), StringComparer.Ordinal).ToArray();

    private static JsonDocument Trace(string fixturePath, params string[] extraArgs)
    {
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var args = new[] { "trace", "--fixture", fixturePath, "--json" }.Concat(extraArgs).ToArray();
        var exit = Aetheris.CLI.CliRunner.Run(args, stdout, stderr);
        Assert.True(exit == 0, stderr.ToString());
        return JsonDocument.Parse(stdout.ToString());
    }
}
