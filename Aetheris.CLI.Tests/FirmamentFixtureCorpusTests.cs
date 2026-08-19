using System.Text.Json;

namespace Aetheris.CLI.Tests;

public sealed class FirmamentFixtureCorpusTests
{
    private static readonly string CorpusRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Speculative"));
    private static readonly string V2CorpusRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures"));
    private static readonly string V2DoctrinePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/development/milestones/general/air-firmament-a2-firmament-v2-source-language-design.md"));
    private static readonly string V21DoctrinePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/development/milestones/general/air-firmament-a2-1-semantic-references-admissibility-surface-doctrine.md"));
    private static readonly string V22DoctrinePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/development/milestones/general/air-firmament-a2-2-record-derivation-with.md"));
    private static readonly string V23DoctrinePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../docs/development/milestones/general/air-firmament-a2-3-dfm-templates-concepts-pmi.md"));
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
        Assert.Contains(fixtures, p => p.EndsWith("Regression/Primitive/valid/box-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Regression/Region/valid/side-hole-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Speculative/Shell/future/open-top-box-shell-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Speculative/Fillet/future/single-edge-fillet-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Compatibility/LegacyAliases/Invalid/invalid-missing-units-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Regression/SemanticRefs/valid/named-box-faces-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Speculative/SemanticRefs/valid/side-hole-feature-output-aliases-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Compatibility/LegacyAliases/Invalid/SemanticRefs/raw-brep-id-reference-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Compatibility/LegacyAliases/Invalid/Admissibility/degenerate-box-zero-dimension-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Compatibility/LegacyAliases/Invalid/Admissibility/shell-thickness-collapse-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Speculative/Surface/future/ruled-surface-rails-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Speculative/Surface/future/offset-surface-detail-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Speculative/Pattern/future/linear-pattern-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Regression/RecordDerivation/valid/box-with-size-variant-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Speculative/RecordDerivation/valid/feature-with-radius-variant-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Speculative/RecordDerivation/valid/material-with-property-variant-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Compatibility/LegacyAliases/Invalid/RecordDerivation/with-degenerate-box-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Compatibility/LegacyAliases/Invalid/RecordDerivation/with-selector-target-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Compatibility/LegacyAliases/Invalid/RecordDerivation/with-unknown-field-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Speculative/Templates/compatibility-syntax/cnc-template-min-tool-radius-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Speculative/Templates/compatibility-syntax/fdm-template-wall-overhang-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Speculative/Templates/compatibility-syntax/sheet-metal-template-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Compatibility/LegacyAliases/Invalid/Templates/template-concept-unit-mismatch-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Compatibility/LegacyAliases/Invalid/Templates/template-unknown-process-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Speculative/PMI/future/pmi-datum-flatness-position-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Speculative/PMI/future/pmi-material-surface-finish-v2.valid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Invalid/PMI/pmi-raw-brep-id-target-v2.invalid.firmfixture", StringComparison.Ordinal));
        Assert.Contains(fixtures, p => p.EndsWith("Regression/Decompile/ctc-01-candidate-v2.firmfixture", StringComparison.Ordinal));

        foreach (var path in fixtures)
        {
            var fixture = LoadMetadata(path);
            foreach (var key in RequiredMetadata) Assert.True(fixture.ContainsKey(key), $"{path} missing {key}");
            Assert.Equal("FirmamentV2", fixture["syntax-version"]);
            Assert.Contains(fixture["validity"], new[] { "valid", "invalid", "semantic-candidate" });
            Assert.Contains(fixture["implementation"], new[] { "not-implemented", "rejected", "parser-backed", "design-only-not-implemented" });
            if (fixture["implementation"] != "parser-backed" && fixture["validity"] != "semantic-candidate") Assert.True(fixture.ContainsKey("expected-diagnostic"), $"{path} missing expected-diagnostic");
        }
    }

    [Fact]
    public void FirmamentV2DesignFixtures_AreNotTreatedAsV1ParseFailures()
    {
        foreach (var path in DiscoverV2TraceFixtures())
        {
            var metadata = LoadMetadata(path);
            if (string.Equals(metadata["expected-stage"], "validation-report", StringComparison.Ordinal))
            {
                // Validation-report fixtures prove parser/report behavior through `validate`, not lowering `trace`.
                using var doc = Validate(path);
                var validation = doc.RootElement.GetProperty("firmamentV2Validation");
                var validationStatus = validation.GetProperty("status").GetString();
                if (metadata["expected"] == "valid")
                {
                    Assert.Contains(validationStatus, new[] { "valid", "valid-with-deferred-export" });
                }
                else
                {
                    Assert.Equal("invalid", validationStatus);
                }
                var diagnostics = validation.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetProperty("code").GetString()).ToArray();
                if (metadata.TryGetValue("expected-diagnostic", out var expectedValidationDiagnostic)) Assert.Contains(expectedValidationDiagnostic, diagnostics);
                // The public report itself proves V2 routing. Successful parser trace events
                // are intentionally filtered from user-facing diagnostics.
                Assert.All(diagnostics, code => Assert.StartsWith("firmament-v2-", code, StringComparison.Ordinal));
                Assert.DoesNotContain("air-x11-firmament-parse-failed", diagnostics);
                continue;
            }

            using var trace = Trace(path);
            var root = trace.RootElement;
            var isParserBacked = metadata["implementation"] == "parser-backed";
            Assert.Equal(isParserBacked, root.GetProperty("fixture").GetProperty("parserBacked").GetBoolean());
            Assert.True(root.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean(), path);
            Assert.Equal(metadata["expected-stage"], root.GetProperty("actualStageReached").GetString());
            var traceDiagnostics = root.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
            if (metadata.TryGetValue("expected-diagnostic", out var expectedTraceDiagnostic)) Assert.Contains(expectedTraceDiagnostic, traceDiagnostics);
            Assert.DoesNotContain("air-x11-firmament-parse-failed", traceDiagnostics);
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
            if (fixture["implementation"] != "parser-backed" || fixture["validity"] == "invalid") if (fixture["implementation"] != "parser-backed") Assert.True(fixture.ContainsKey("expected-diagnostic"), path);
        }
    }

    [Fact]
    public void FirmamentV2SemanticRefs_NotV1ParseFailures()
    {
        foreach (var path in DiscoverV2Fixtures().Where(p => p.Contains("/SemanticRefs/", StringComparison.Ordinal)))
        {
            using var doc = Trace(path);
            var diagnostics = doc.RootElement.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
            var fixture = LoadMetadata(path);
            if (string.Equals(fixture.GetValueOrDefault("implementation"), "parser-backed", StringComparison.Ordinal))
            {
                Assert.True(doc.RootElement.GetProperty("fixture").GetProperty("parserBacked").GetBoolean());
                Assert.Equal("FirmamentV2Parser", doc.RootElement.GetProperty("frontend").GetProperty("parserName").GetString());
            }
            else
            {
                if (!path.Contains("template-v2-", StringComparison.Ordinal))
            {
                Assert.False(doc.RootElement.GetProperty("fixture").GetProperty("parserBacked").GetBoolean());
            }
            }
            Assert.DoesNotContain("air-x11-firmament-parse-failed", diagnostics);
        }
    }

    [Fact]
    public void FirmamentV2Admissibility_InvalidFixtures_ReportExpectedDiagnostics()
    {
        var expected = new[]
        {
            (Path: "Compatibility/LegacyAliases/Invalid/SemanticRefs/raw-brep-id-reference-v2.invalid.firmfixture", Diagnostic: "firmament-raw-backend-id-reference-forbidden"),
            (Path: "Compatibility/LegacyAliases/Invalid/Admissibility/degenerate-box-zero-dimension-v2.invalid.firmfixture", Diagnostic: "firmament-degenerate-dimension"),
            (Path: "Compatibility/LegacyAliases/Invalid/Admissibility/shell-thickness-collapse-v2.invalid.firmfixture", Diagnostic: "firmament-shell-thickness-collapses-body")
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
        Assert.Equal(10, fixtures.Length);
        foreach (var path in fixtures)
        {
            var fixture = LoadMetadata(path);
            Assert.Equal("FirmamentV2", fixture["syntax-version"]);
            Assert.Equal("RecordDerivation", fixture["category"]);
            if (fixture["implementation"] != "parser-backed" || fixture["validity"] == "invalid") Assert.True(fixture.ContainsKey("expected-diagnostic"), path);
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
            Assert.Equal(metadata["implementation"] == "parser-backed", doc.RootElement.GetProperty("fixture").GetProperty("parserBacked").GetBoolean());
            Assert.True(doc.RootElement.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean(), path);
            Assert.Equal(metadata["expected-stage"], doc.RootElement.GetProperty("actualStageReached").GetString());
            if (metadata.TryGetValue("expected-diagnostic", out var expectedDiagnostic)) Assert.Contains(expectedDiagnostic, diagnostics);
            Assert.DoesNotContain("air-x11-firmament-parse-failed", diagnostics);
        }
    }

    [Fact]
    public void FirmamentV2RecordDerivation_InvalidFixtures_ReportExpectedDiagnostics()
    {
        var expected = new[]
        {
            (Path: "Compatibility/LegacyAliases/Invalid/RecordDerivation/with-degenerate-box-v2.invalid.firmfixture", Diagnostic: "firmament-degenerate-dimension"),
            (Path: "Compatibility/LegacyAliases/Invalid/RecordDerivation/with-selector-target-v2.invalid.firmfixture", Diagnostic: "firmament-v2-with-requires-record"),
            (Path: "Compatibility/LegacyAliases/Invalid/RecordDerivation/with-unknown-field-v2.invalid.firmfixture", Diagnostic: "firmament-v2-with-field-not-found")
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
    public void FirmamentV2Templates_MetadataRecognized()
    {
        var fixtures = DiscoverV2Fixtures().Where(p => p.Contains("/Templates/", StringComparison.Ordinal)).ToArray();
        Assert.Equal(7, fixtures.Length);
        foreach (var path in fixtures)
        {
            var fixture = LoadMetadata(path);
            Assert.Equal("FirmamentV2", fixture["syntax-version"]);
            Assert.Equal("Templates", fixture["category"]);
            Assert.True(fixture.ContainsKey("expected-diagnostic"), path);
        }
    }

    [Fact]
    public void FirmamentV2Templates_NotV1ParseFailures()
    {
        foreach (var path in DiscoverV2Fixtures().Where(p => p.Contains("/Templates/", StringComparison.Ordinal)))
        {
            using var doc = Trace(path);
            var diagnostics = doc.RootElement.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
            if (!path.Contains("template-v2-", StringComparison.Ordinal))
            {
                Assert.False(doc.RootElement.GetProperty("fixture").GetProperty("parserBacked").GetBoolean());
            }
            Assert.True(doc.RootElement.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean(), path);
            Assert.DoesNotContain("air-x11-firmament-parse-failed", diagnostics);
        }
    }

    [Fact]
    public void FirmamentV2Templates_InvalidFixtures_ReportExpectedDiagnostics()
    {
        var expected = new[]
        {
            (Path: "Compatibility/LegacyAliases/Invalid/Templates/template-concept-unit-mismatch-v2.invalid.firmfixture", Diagnostic: "firmament-concept-unit-mismatch"),
            (Path: "Compatibility/LegacyAliases/Invalid/Templates/template-unknown-process-v2.invalid.firmfixture", Diagnostic: "firmament-template-process-unknown"),
            (Path: "Compatibility/LegacyAliases/Invalid/Templates/template-v2-cnc-min-tool-radius-enforced.invalid.firmfixture", Diagnostic: "firmament-v2-dfm-minimum-tool-radius-violation"),
            (Path: "Compatibility/LegacyAliases/Invalid/Templates/template-v2-concept-unit-mismatch-rejected-at-build.invalid.firmfixture", Diagnostic: "firmament-v2-dfm-concept-unit-mismatch")
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
    public void FirmamentV2PMI_MetadataRecognized()
    {
        var fixtures = DiscoverV2Fixtures().Where(p => p.Contains("/PMI/", StringComparison.Ordinal)).ToArray();
        Assert.Equal(5, fixtures.Length);
        foreach (var path in fixtures)
        {
            var fixture = LoadMetadata(path);
            Assert.Equal("FirmamentV2", fixture["syntax-version"]);
            Assert.Equal("PMI", fixture["category"]);
            if (fixture["implementation"] != "parser-backed") Assert.True(fixture.ContainsKey("expected-diagnostic"), path);
        }
    }

    [Fact]
    public void FirmamentV2PMI_InvalidFixtures_ReportExpectedDiagnostics()
    {
        using var doc = Trace(Path.Combine(V2CorpusRoot, "Invalid/PMI/pmi-raw-brep-id-target-v2.invalid.firmfixture"));
        var diagnostics = doc.RootElement.GetProperty("diagnostics").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.True(doc.RootElement.GetProperty("fixture").GetProperty("expectationSatisfied").GetBoolean());
        Assert.Contains("firmament-raw-backend-id-reference-forbidden", diagnostics);
    }

    [Fact]
    public void FirmamentV2Doctrine_DocsMentionTemplatesConceptsPMI()
    {
        Assert.True(File.Exists(V23DoctrinePath), V23DoctrinePath);
        var doc = File.ReadAllText(V23DoctrinePath);
        Assert.Contains("template<Process>", doc, StringComparison.Ordinal);
        Assert.Contains("concept", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PMI", doc, StringComparison.Ordinal);
        Assert.Contains("GD&T is one category inside PMI", doc, StringComparison.Ordinal);
        Assert.Contains("material", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("surface finish", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not C++-style metaprogramming", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not hidden Excel tables", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no STEP PMI export in A2.3", doc, StringComparison.OrdinalIgnoreCase);
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

    private static string[] DiscoverV2Fixtures() => Directory.EnumerateFiles(V2CorpusRoot, "*.firmfixture", SearchOption.AllDirectories)
        .Where(path => string.Equals(LoadMetadata(path).GetValueOrDefault("syntax-version"), "FirmamentV2", StringComparison.Ordinal))
        .Select(p => p.Replace('\\', '/'))
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string[] DiscoverV2TraceFixtures() => DiscoverV2Fixtures().Where(path => path.EndsWith(".valid.firmfixture", StringComparison.Ordinal) || path.EndsWith(".invalid.firmfixture", StringComparison.Ordinal)).ToArray();

    private static JsonDocument Trace(string fixturePath, params string[] extraArgs)
    {
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var args = new[] { "trace", "--fixture", fixturePath, "--json" }.Concat(extraArgs).ToArray();
        var exit = Aetheris.CLI.CliRunner.Run(args, stdout, stderr);
        Assert.True(exit == 0, stderr.ToString());
        return JsonDocument.Parse(stdout.ToString());
    }

    private static JsonDocument Validate(string fixturePath)
    {
        var stdout = new StringWriter(); var stderr = new StringWriter();
        var exit = Aetheris.CLI.CliRunner.Run(["validate", fixturePath, "--json"], stdout, stderr);
        var metadata = LoadMetadata(fixturePath);
        Assert.Equal(metadata["expected"] == "valid" ? 0 : 1, exit);
        Assert.Equal(string.Empty, stderr.ToString());
        return JsonDocument.Parse(stdout.ToString());
    }
}
