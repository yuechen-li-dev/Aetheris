using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class IncrementalCompilationTests
{
    private const string Recipe = """
        Template<H: Length> Struct Pad {
          Rect2 Outline { Center: [0mm,0mm]; Size: [10mm,10mm] }
          Profile P { Loop Outer { Outline |> TraceLoop } }
          Extrude Body { Profile: P From: 0mm To: H }
        }
        """;
    private static string Source(string height = "2mm", string position = "20mm") => Recipe + $$"""

        Assembly Test {
          <Assembly Test>
            <Part A = Pad<H:{{height}}>> Placement { From: Origin; To: World; } </Part>
            <Part B = Pad<H:{{height}}>> Placement { From: Origin; To: World; TranslateLocal: [{{position}},0mm,0mm]; } </Part>
          </Assembly>
          Anchor: Test;
        }
        """;

    private static AssemblyM1CompilationResult Good(AssemblyM1CompilationResult result)
    {
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        return result;
    }

    [Fact]
    public void RepeatedBuildReusesOneDefinitionAndMatchesUncachedExactOutput()
    {
        using var session = new FirmamentCompilationSession();
        var first = Good(session.Compile(Source()));
        var second = Good(session.Compile(Source()));
        var baseline = Good(new AssemblyM1Pipeline().Compile(Source()));
        Assert.Equal(1, first.Reuse!.RebuiltDefinitions);
        Assert.Equal(1, second.Reuse!.ReusedDefinitions);
        Assert.Equal(0, second.Reuse.RebuiltDefinitions);
        Assert.Equal(baseline.Geometry!.Artifact.DeterministicSha256, second.Geometry!.Artifact.DeterministicSha256);
        Assert.Equal(AssemblyIrAp242Exporter.Export(baseline).Value, AssemblyIrAp242Exporter.Export(second).Value);
        Assert.NotSame(first.Geometry!.DefinitionBodies.Values.Single(), second.Geometry.DefinitionBodies.Values.Single());
    }

    [Fact]
    public void PlacementEditReusesGeometryButRevalidatesInterference()
    {
        using var session = new FirmamentCompilationSession();
        Good(session.Compile(Source()));
        var moved = Good(session.Compile(Source(position: "30mm")));
        Assert.Equal(1, moved.Reuse!.ReusedDefinitions);
        var overlapping = session.Compile(Source(position: "00mm"));
        Assert.False(overlapping.IsSuccess);
        Assert.Equal(1, overlapping.Reuse!.ReusedDefinitions);
        Assert.Contains(overlapping.Diagnostics, d => d.Code == "assembly-solid-volume-interference");
    }

    [Fact]
    public void SpecificationAndSharedRecipeEditsInvalidate()
    {
        using var session = new FirmamentCompilationSession();
        Good(session.Compile(Source()));
        var arguments = Good(session.Compile(Source(height: "3mm")));
        Assert.Equal(1, arguments.Reuse!.RebuiltDefinitions);
        var recipe = Good(session.Compile(Source(height: "3mm").Replace("10mm", "12mm")));
        Assert.Equal(1, recipe.Reuse!.RebuiltDefinitions);
        Assert.Equal("definition-inputs-changed", recipe.Reuse.Definitions.Single().Reason);
    }

    [Fact]
    public void ReturnedMutableGeometryAndEvidenceCannotPoisonTheCache()
    {
        using var session = new FirmamentCompilationSession();
        var first = Good(session.Compile(Source()));
        var body = first.Geometry!.DefinitionBodies.Values.Single();
        var before = body.Topology.Vertices.Count();
        body.Topology.AddVertex(new Vertex(new VertexId(999999)));
        first.Geometry.Artifact.Definitions.Single().Metrics.Minimum[0] = 999999;
        var next = Good(session.Compile(Source()));
        Assert.Equal(1, next.Reuse!.ReusedDefinitions);
        Assert.Equal(before, next.Geometry!.DefinitionBodies.Values.Single().Topology.Vertices.Count());
        Assert.NotEqual(999999, next.Geometry.Artifact.Definitions.Single().Metrics.Minimum[0]);
    }

    [Fact]
    public void FailedBuildIsNotCachedAndClearAndDisposeAreExplicit()
    {
        using var session = new FirmamentCompilationSession();
        var bad = Source(height: "-2mm");
        Assert.False(session.Compile(bad).IsSuccess);
        Assert.False(session.Compile(bad).IsSuccess);
        var fixedBuild = Good(session.Compile(Source()));
        Assert.Equal(1, fixedBuild.Reuse!.RebuiltDefinitions);
        session.Clear();
        Assert.Equal(1, Good(session.Compile(Source())).Reuse!.RebuiltDefinitions);
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.Compile(Source()));
    }

    [Fact]
    public void CapacityEvictsDefinitionsWithoutChangingCorrectness()
    {
        using var session = new FirmamentCompilationSession(1);
        Good(session.Compile(Source()));
        Good(session.Compile(Source(height: "3mm")));
        Assert.Equal(1, Good(session.Compile(Source())).Reuse!.RebuiltDefinitions);
    }

    [Fact]
    public void SnapshotChangesAndMissingDocumentsCannotReuseStaleGeometry()
    {
        using var session = new FirmamentCompilationSession();
        FirmamentProjectSnapshot Project(string recipe) => new("main.firmasm", [
            KeyValuePair.Create("main.firmasm", "Include \"part.firmament\";\n" + Source()[Recipe.Length..]),
            KeyValuePair.Create("part.firmament", recipe)]);
        Good(session.CompileProject(Project(Recipe)));
        Assert.Equal(1, Good(session.CompileProject(Project(Recipe))).Reuse!.ReusedDefinitions);
        Assert.Equal(1, Good(session.CompileProject(Project(Recipe.Replace("10mm", "12mm")))).Reuse!.RebuiltDefinitions);
        var missing = new FirmamentProjectSnapshot("main.firmasm", [KeyValuePair.Create("main.firmasm", "Include \"part.firmament\";\n" + Source()[Recipe.Length..])]);
        var failed = session.CompileProject(missing);
        Assert.False(failed.IsSuccess);
        Assert.Empty(failed.Reuse!.Definitions);
    }

    [Fact]
    public void GuitarFileEditInvalidatesOnlyItsFileBackedDefinition()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm");
        var directory = Path.GetDirectoryName(path)!;
        var documents = Directory.GetFiles(directory, "*.firmament").Append(path)
            .Select(p => KeyValuePair.Create(Path.GetFileName(p), File.ReadAllText(p))).ToDictionary(p => p.Key, p => p.Value);
        using var session = new FirmamentCompilationSession();
        FirmamentProjectSnapshot Project() => new("guitar.firmasm", documents);
        var first = Good(session.CompileProject(Project()));
        var repeated = Good(session.CompileProject(Project()));
        Assert.Equal(53, repeated.Reuse!.ReusedDefinitions);
        Assert.Equal(AssemblyUsdExporter.Serialize(first.Ir!, AssemblyDisplayMeshExporter.Export(first)),
            AssemblyUsdExporter.Serialize(repeated.Ir!, AssemblyDisplayMeshExporter.Export(repeated)));
        var neckFile = documents.Keys.Single(k => k == "Neck.firmament");
        documents[neckFile] = documents[neckFile].Replace("-40mm", "-39.8mm");
        var edited = Good(session.CompileProject(Project()));
        Assert.Equal(52, edited.Reuse!.ReusedDefinitions);
        Assert.Equal(1, edited.Reuse.RebuiltDefinitions);
        Assert.Contains(edited.Reuse.Definitions, d => !d.Reused && d.DefinitionIdentity.Contains("Neck.firmament", StringComparison.Ordinal));
        Assert.NotEqual(first.Geometry!.Artifact.Definitions.Single(d => d.DefinitionIdentity.Contains("Neck.firmament", StringComparison.Ordinal)).StepSha256,
            edited.Geometry!.Artifact.Definitions.Single(d => d.DefinitionIdentity.Contains("Neck.firmament", StringComparison.Ordinal)).StepSha256);
    }
}
