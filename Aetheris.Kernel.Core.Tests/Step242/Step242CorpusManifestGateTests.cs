using System.Text;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242CorpusManifestGateTests
{
    [Fact]
    public void V0CorpusManifest_GateRules_AreSatisfied()
    {
        var manifest = Step242CorpusManifestRunner.LoadManifest("testdata/step242/manifests/v0.corpus.json");
        Assert.Equal("1", manifest.Version);

        var entries = manifest.Entries.OrderBy(e => e.Path, StringComparer.Ordinal).ToArray();
        var reports = entries.Select(entry => Step242CorpusManifestRunner.RunOne(entry)).ToArray();

        Assert.All(reports, r => Assert.False(r.ExceptionEscaped));

        foreach (var report in reports.Where(r => string.Equals(r.Group, "passRequired", StringComparison.Ordinal)))
        {
            Assert.True(string.Equals("success", report.Status, StringComparison.Ordinal), $"passRequired entry {report.Id} returned {report.Status} ({report.FirstDiagnostic.Source}: {report.FirstDiagnostic.MessagePrefix})");
            Assert.NotNull(report.CanonicalSha256);
            Assert.Matches("^[0-9a-f]{64}$", report.CanonicalSha256!);

            var manifestEntry = entries.Single(e => e.Id == report.Id);
            if (manifestEntry.ExpectTopologyCounts is not null)
            {
                Assert.Equal(manifestEntry.ExpectTopologyCounts.V, report.TopologyCounts.Vertices);
                Assert.Equal(manifestEntry.ExpectTopologyCounts.E, report.TopologyCounts.Edges);
                Assert.Equal(manifestEntry.ExpectTopologyCounts.F, report.TopologyCounts.Faces);
            }

            if (manifestEntry.ExpectHashStableAfterCanonicalization ?? true)
            {
                var rerun = Step242CorpusManifestRunner.RunOne(manifestEntry);
                Assert.Equal(report.CanonicalSha256, rerun.CanonicalSha256);
                Assert.Equal(report.ExportedCanonicalText, rerun.ExportedCanonicalText);
            }
        }

        foreach (var report in reports.Where(r => string.Equals(r.Group, "expectedFail", StringComparison.Ordinal)))
        {
            Assert.NotEqual("success", report.Status);
            var manifestEntry = entries.Single(e => e.Id == report.Id);
            var expected = Assert.IsType<Step242ExpectedDiagnostic>(manifestEntry.ExpectedFirstDiagnostic);
            Assert.Equal(expected.Code, report.FirstDiagnostic.Code);
            Assert.Equal(expected.Source, report.FirstDiagnostic.Source);
            Assert.StartsWith(expected.MessagePrefix, report.FirstDiagnostic.MessagePrefix, StringComparison.Ordinal);
        }

        // deferred items are measured/reported but do not fail the gate.
        var deferredCount = reports.Count(r => string.Equals(r.Group, "deferred", StringComparison.Ordinal));
        Assert.True(deferredCount >= 0);

        var first = Step242CorpusManifestRunner.BuildReportJson(entries);
        var second = Step242CorpusManifestRunner.BuildReportJson(entries);
        Assert.Equal(first, second);
    }

    [Fact]
    public void V0GeneratedFixtures_Exist_AreSmall_AndImportSmoke()
    {
        var manifest = Step242CorpusManifestRunner.LoadManifest("testdata/step242/manifests/v0.corpus.json");
        var generated = manifest.Entries
            .Where(e => e.Path.StartsWith("testdata/step242/generated/v0-required/", StringComparison.Ordinal))
            .OrderBy(e => e.Path, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(generated);

        foreach (var entry in generated)
        {
            var fullPath = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), entry.Path.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(fullPath), $"Missing fixture: {entry.Path}");

            var bytes = new FileInfo(fullPath).Length;
            Assert.InRange(bytes, 1, 20_000);

            var text = File.ReadAllText(fullPath, Encoding.UTF8);
            Assert.Contains("ISO-10303-21;", text, StringComparison.Ordinal);
            Assert.Contains("MANIFOLD_SOLID_BREP", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void V0CorpusManifest_StatusContract_DistinguishesPickerBlockerFromSuccess()
    {
        var manifest = Step242CorpusManifestRunner.LoadManifest("testdata/step242/manifests/v0.corpus.json");
        var blockerEntry = manifest.Entries.Single(e => e.Id == "exp_cylinder_hole_unsupported");
        var blockerReport = Step242CorpusManifestRunner.RunOne(blockerEntry, includeDisplayAudit: true);
        Assert.Equal("success", blockerReport.Status);
        Assert.Equal(string.Empty, blockerReport.FirstFailureLayer);
        Assert.Equal("pickerBlockedByTessellationSkip", blockerReport.DisplayStatus);
        Assert.Equal("picker", blockerReport.DisplayFirstFailureLayer);
        Assert.Equal("Audit.Picker", blockerReport.DisplayFirstDiagnostic.Source);

        var successEntry = manifest.Entries.Single(e => e.Id == "gen_box_v0");
        var successReport = Step242CorpusManifestRunner.RunOne(successEntry, includeDisplayAudit: true);
        Assert.Equal("success", successReport.Status);
        Assert.Equal(string.Empty, successReport.FirstFailureLayer);
        Assert.Equal("success", successReport.DisplayStatus);
        Assert.Equal(string.Empty, successReport.DisplayFirstFailureLayer);
    }

    [Fact]
    public void V0CorpusManifest_StatusContract_ParserFailureStillFailsAp242Lane()
    {
        var manifest = Step242CorpusManifestRunner.LoadManifest("testdata/step242/manifests/v0.corpus.json");
        var parserFailureEntry = manifest.Entries.Single(e => e.Id == "exp_parser_missing_paren");
        var parserFailureReport = Step242CorpusManifestRunner.RunOne(parserFailureEntry);

        Assert.Equal("parseFail", parserFailureReport.Status);
        Assert.Equal("parser", parserFailureReport.FirstFailureLayer);
        Assert.Equal("notRun", parserFailureReport.DisplayStatus);
    }
}
