using Aetheris.Kernel.Core.Import;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class ImportOrchestratorTests
{
    [Fact]
    public void Import_Ap242ExactSource_SelectsStepConnectorAndExactLaneDeterministically()
    {
        var orchestrator = ImportOrchestrator.CreateDefault();
        var request = new ImportRequest(Step242FixtureCorpus.CanonicalBoxGolden);

        var first = orchestrator.Import(request);
        var second = orchestrator.Import(request);

        Assert.True(first.BodyResult.IsSuccess, string.Join(" | ", first.BodyResult.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        Assert.True(second.BodyResult.IsSuccess, string.Join(" | ", second.BodyResult.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        Assert.Equal("step-ap242-connector", first.Connector);
        Assert.Equal(ImportLaneKind.ExactBRep, first.Lane);
        Assert.Equal(first.Connector, second.Connector);
        Assert.Equal(first.Lane, second.Lane);
    }

    [Fact]
    public void Import_Ap242TessellatedSource_SelectsTessellatedLaneDeterministically()
    {
        var orchestrator = ImportOrchestrator.CreateDefault();
        var text = LoadFixture("testdata/step242/nist/FTC/nist_ftc_08_asme1_ap242-e1-tg.stp");

        var first = orchestrator.Import(new ImportRequest(text));
        var second = orchestrator.Import(new ImportRequest(text));

        Assert.True(first.BodyResult.IsSuccess, string.Join(" | ", first.BodyResult.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        Assert.True(second.BodyResult.IsSuccess, string.Join(" | ", second.BodyResult.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        Assert.Equal("step-ap242-connector", first.Connector);
        Assert.Equal(ImportLaneKind.Tessellated, first.Lane);
        Assert.Equal(first.Connector, second.Connector);
        Assert.Equal(first.Lane, second.Lane);
    }

    private static string LoadFixture(string relativePath)
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path);
    }
}
