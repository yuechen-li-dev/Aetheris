using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

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
        Assert.Equal(ImportRepresentationTruthKind.ExactBRep, first.RepresentationTruth);
        Assert.Equal(first.Connector, second.Connector);
        Assert.Equal(first.Lane, second.Lane);
        Assert.Equal(first.RepresentationTruth, second.RepresentationTruth);
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
        Assert.Equal(ImportRepresentationTruthKind.TessellatedOrFaceted, first.RepresentationTruth);
        Assert.Equal(first.Connector, second.Connector);
        Assert.Equal(first.Lane, second.Lane);
        Assert.Equal(first.RepresentationTruth, second.RepresentationTruth);
    }

    [Fact]
    public void TessellatedLane_ImportsTgRootViaExtractedLaneOwner_Deterministically()
    {
        var parse = Step242SubsetParser.Parse(LoadFixture("testdata/step242/nist/FTC/nist_ftc_08_asme1_ap242-e1-tg.stp"));
        Assert.True(parse.IsSuccess);

        var lane = new Step242TessellatedImportLane();
        var document = new StepParsedSourceDocument(parse.Value);

        var first = lane.Import(document, new ImportPolicy(ImportLaneKind.Tessellated));
        var second = lane.Import(document, new ImportPolicy(ImportLaneKind.Tessellated));

        Assert.True(first.IsSuccess, string.Join(" | ", first.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        Assert.True(second.IsSuccess, string.Join(" | ", second.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));

        Assert.Equal(first.Value.Topology.Faces.Count(), second.Value.Topology.Faces.Count());
        Assert.Equal(first.Value.Topology.Edges.Count(), second.Value.Topology.Edges.Count());
        Assert.All(first.Value.Geometry.Surfaces, surface => Assert.Equal(Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Plane, surface.Value.Kind));
    }

    [Fact]
    public void ExactLane_ImportsExactRootViaExtractedLaneOwner()
    {
        var parse = Step242SubsetParser.Parse(Step242FixtureCorpus.CanonicalBoxGolden);
        Assert.True(parse.IsSuccess);

        var lane = new Step242ExactBRepImportLane();
        var document = new StepParsedSourceDocument(parse.Value);

        var import = lane.Import(document, new ImportPolicy(ImportLaneKind.ExactBRep));

        Assert.True(import.IsSuccess, string.Join(" | ", import.Diagnostics.Select(d => $"{d.Source}: {d.Message}")));
        Assert.Equal(6, import.Value.Topology.Faces.Count());
    }

    [Fact]
    public void CreateDefault_AllowsAdditionalSourceFamilyRegistrationWithoutChangingStepBehavior()
    {
        var orchestrator = ImportOrchestrator.CreateDefault(builder =>
            builder
                .AddConnector(new StubSourceConnector())
                .AddLane(new StubImportLane()));

        var result = orchestrator.Import(new ImportRequest("FIRMAMENT:test"));

        Assert.True(result.BodyResult.IsSuccess);
        Assert.Equal("stub-connector", result.Connector);
        Assert.Equal(ImportLaneKind.Compatibility, result.Lane);
        Assert.Equal("FIRMAMENT", result.SourceFamily);
    }

    private static string LoadFixture(string relativePath)
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path);
    }

    private sealed class StubParsedSourceDocument : IParsedSourceDocument
    {
        public string SourceFamily => "FIRMAMENT";
    }

    private sealed class StubSourceConnector : ISourceConnector
    {
        public string Name => "stub-connector";

        public bool CanOpen(ImportRequest request) => request.SourceText.StartsWith("FIRMAMENT:", StringComparison.Ordinal);

        public KernelResult<IParsedSourceDocument> Parse(ImportRequest request)
            => KernelResult<IParsedSourceDocument>.Success(new StubParsedSourceDocument());
    }

    private sealed class StubImportLane : IImportLane
    {
        public string Name => "stub-lane";

        public ImportLaneKind Kind => ImportLaneKind.Compatibility;

        public bool CanImport(IParsedSourceDocument document, ImportPolicy policy)
            => document is StubParsedSourceDocument;

        public KernelResult<BrepBody> Import(IParsedSourceDocument document, ImportPolicy policy)
            => KernelResult<BrepBody>.Success(new BrepBody(new TopologyModel(), new BrepGeometryStore(), new BrepBindingModel()));
    }
}
