using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentFixtureCorpusTests
{
    private static readonly string CorpusRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Firmament"));
    private static readonly string V2CorpusRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2"));
    private static readonly string V2DoctrinePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/air-firmament-a2-firmament-v2-source-language-design.md"));
    private static readonly string V21DoctrinePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/air-firmament-a2-1-semantic-references-admissibility-surface-doctrine.md"));
    private static readonly string V22DoctrinePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/air-firmament-a2-2-record-derivation-with.md"));
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
    public void FirmamentV1Fixtures_RemainValid()
    {
        FirmamentFixtureCorpus_Box_RemainsParserBacked();
        FirmamentFixtureCorpus_SideHoleGoldenPath_RemainsIntegrated();
    }

    [Fact]
    public void FirmamentV2DesignFixtures_MetadataRecognized()
    {
        var fixtures = DiscoverV2Fixtures();
        Assert.Contains(fixtures, p => p.EndsWith("Primitive/valid/box-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Region/valid/side-hole-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Shell/future/open-top-box-shell-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Fillet/future/single-edge-fillet-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Invalid/invalid-missing-units-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("SemanticRefs/valid/named-box-faces-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("SemanticRefs/valid/side-hole-feature-output-aliases-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("SemanticRefs/invalid/raw-brep-id-reference-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Admissibility/invalid/degenerate-box-zero-dimension-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Admissibility/invalid/shell-thickness-collapse-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Surface/future/ruled-surface-rails-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Surface/future/offset-surface-detail-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Pattern/future/linear-pattern-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("RecordDerivation/valid/box-with-size-variant-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("RecordDerivation/valid/feature-with-radius-variant-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("RecordDerivation/valid/material-with-property-variant-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("RecordDerivation/invalid/with-degenerate-box-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("RecordDerivation/invalid/with-selector-target-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("RecordDerivation/invalid/with-unknown-field-v2.invalid.firmfixture", StringComparison.Ordinal));

        foreach (var path in fixtures)
        {
            var fixture = LoadMetadata(path);
            foreach (var key in RequiredMetadata) Assert.True(fixture.ContainsKey(key), $"{path} missing {key}");
            Assert.Equal("FirmamentV2", fixture["syntax-version"]);
            Assert.Contains(fixture["validity"], new[] { "valid", "invalid" });
            Assert.Contains(fixture["implementation"], new[] { "not-implemented", "rejected" });
            Assert.True(fixture.ContainsKey("expected-diagnostic"), $"{path} missing expected-diagnostic");
        }
    }

    [Fact]
    public void FirmamentV2DesignFixtures_AreNotTreatedAsV1ParseFailures()
    {
        foreach (var path in DiscoverV2Fixtures())
        {
            using var doc = Trace(path);
            var root = doc.RootElement;
            var metadata = LoadMetadata(path);
            Assert.False(root.GetProperty("fixture").GetProperty("parserBacked").GetBoolean());
            Assert.True(root.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean(), path);
            Assert.Equal(metadata["expected-stage"], root.GetProperty("actualStageReached").GetString());
            var diagnostics = root.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.Contains(metadata["expected-diagnostic"], diagnostics);
            Assert.DoesNotContain("air-x11-firmament-parse-failed", diagnostics);
        }
    }

    [Fact]
    public void FirmamentV2Doctrine_DocsExist()
    {
        Assert.True(File.Exists(V2DoctrinePath), V2DoctrinePath);
        var doc = File.ReadAllText(V2DoctrinePath);
        Assert.Contains("mostly frozen", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("canonical human-facing", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Feature AIR", doc, StringComparison.Ordinal);
        Assert.Contains("CIR is not the topology path", doc, StringComparison.Ordinal);
        Assert.Contains("Boolean", doc, StringComparison.Ordinal);
        Assert.Contains("not the core language model", doc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FirmamentV2SemanticRefs_MetadataRecognized()
    {
        var semanticRefs = DiscoverV2Fixtures().Where(p => p.Contains("/SemanticRefs/", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(semanticRefs);
        foreach (var path in semanticRefs)
        {
            var fixture = LoadMetadata(path);
            Assert.Equal("FirmamentV2", fixture["syntax-version"]);
            Assert.Equal("SemanticRefs", fixture["category"]);
            Assert.True(fixture.ContainsKey("expected-diagnostic"), path);
        }
    }

    [Fact]
    public void FirmamentV2SemanticRefs_NotV1ParseFailures()
    {
        foreach (var path in DiscoverV2Fixtures().Where(p => p.Contains("/SemanticRefs/", StringComparison.Ordinal)))
        {
            using var doc = Trace(path);
            var diagnostics = doc.RootElement.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.False(doc.RootElement.GetProperty("fixture").GetProperty("parserBacked").GetBoolean());
            Assert.DoesNotContain("air-x11-firmament-parse-failed", diagnostics);
        }
    }

    [Fact]
    public void FirmamentV2Admissibility_InvalidFixtures_ReportExpectedDiagnostics()
    {
        var expected = new[]
        {
            (Path: "SemanticRefs/invalid/raw-brep-id-reference-v2.invalid.firmfixture", Diagnostic: "firmament-raw-backend-id-reference-forbidden"),
            (Path: "Admissibility/invalid/degenerate-box-zero-dimension-v2.invalid.firmfixture", Diagnostic: "firmament-degenerate-dimension"),
            (Path: "Admissibility/invalid/shell-thickness-collapse-v2.invalid.firmfixture", Diagnostic: "firmament-shell-thickness-collapses-body")
        };

        foreach (var item in expected)
        {
            using var doc = Trace(Path.Combine(V2CorpusRoot, item.Path));
            var diagnostics = doc.RootElement.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.True(doc.RootElement.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean(), item.Path);
            Assert.Contains(item.Diagnostic, diagnostics);
        }
    }

    [Fact]
    public void FirmamentV2Doctrine_DocsMentionAdmissibilityAndRuledSurfacePolicy()
    {
        Assert.True(File.Exists(V21DoctrinePath), V21DoctrinePath);
        var doc = File.ReadAllText(V21DoctrinePath);
        Assert.Contains("degenerate geometry", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("compile errors", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ruled/sweep/offset-first", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Spline/NURBS limited admission", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no loops", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no conditionals", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("=>", doc, StringComparison.Ordinal);
        Assert.Contains("not BRep IDs", doc, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void FirmamentV2RecordDerivation_MetadataRecognized()
    {
        var fixtures = DiscoverV2Fixtures().Where(p => p.Contains("/RecordDerivation/", StringComparison.Ordinal)).ToArray();
        Assert.Equal(6, fixtures.Length);
        foreach (var path in fixtures)
        {
            var fixture = LoadMetadata(path);
            Assert.Equal("FirmamentV2", fixture["syntax-version"]);
            Assert.Equal("RecordDerivation", fixture["category"]);
            Assert.True(fixture.ContainsKey("expected-diagnostic"), path);
        }
    }

    [Fact]
    public void FirmamentV2RecordDerivation_NotV1ParseFailures()
    {
        foreach (var path in DiscoverV2Fixtures().Where(p => p.Contains("/RecordDerivation/", StringComparison.Ordinal)))
        {
            using var doc = Trace(path);
            var metadata = LoadMetadata(path);
            var diagnostics = doc.RootElement.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.False(doc.RootElement.GetProperty("fixture").GetProperty("parserBacked").GetBoolean());
            Assert.True(doc.RootElement.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean(), path);
            Assert.Equal(metadata["expected-stage"], doc.RootElement.GetProperty("actualStageReached").GetString());
            Assert.Contains(metadata["expected-diagnostic"], diagnostics);
            Assert.DoesNotContain("air-x11-firmament-parse-failed", diagnostics);
        }
    }

    [Fact]
    public void FirmamentV2RecordDerivation_InvalidFixtures_ReportExpectedDiagnostics()
    {
        var expected = new[]
        {
            (Path: "RecordDerivation/invalid/with-degenerate-box-v2.invalid.firmfixture", Diagnostic: "firmament-degenerate-dimension"),
            (Path: "RecordDerivation/invalid/with-selector-target-v2.invalid.firmfixture", Diagnostic: "firmament-with-requires-record"),
            (Path: "RecordDerivation/invalid/with-unknown-field-v2.invalid.firmfixture", Diagnostic: "firmament-with-field-not-found")
        };

        foreach (var item in expected)
        {
            using var doc = Trace(Path.Combine(V2CorpusRoot, item.Path));
            var diagnostics = doc.RootElement.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
            Assert.True(doc.RootElement.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean(), item.Path);
            Assert.Contains(item.Diagnostic, diagnostics);
        }
    }

    [Fact]
    public void FirmamentV2Doctrine_DocsMentionWithDerivation()
    {
        Assert.True(File.Exists(V22DoctrinePath), V22DoctrinePath);
        var doc = File.ReadAllText(V22DoctrinePath);
        Assert.Contains("immutable record derivation", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not mutation", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not topology editing", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("admissibility", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no control flow", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nested `with`", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("=>", doc, StringComparison.Ordinal);
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

    private static string[] DiscoverV2Fixtures() => Directory.EnumerateFiles(V2CorpusRoot, "*.firmfixture", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).Order(StringComparer.Ordinal).ToArray();

    private static JsonDocument Trace(string fixturePath, params string[] extraArgs)
    {
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var args = new[] { "trace", "--fixture", fixturePath, "--json" }.Concat(extraArgs).ToArray();
        var exit = Aetheris.CLI.CliRunner.Run(args, stdout, stderr);
        Assert.True(exit == 0, stderr.ToString());
        return JsonDocument.Parse(stdout.ToString());
    }
}
