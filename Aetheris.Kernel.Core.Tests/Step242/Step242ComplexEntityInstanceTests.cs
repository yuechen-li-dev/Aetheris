using System.Text;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242ComplexEntityInstanceTests
{
    [Fact]
    public void StepParser_ParsesComplexEntityInstance_Assignment()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/complex-context-min.step");

        var parseResult = Step242SubsetParser.Parse(text);

        Assert.True(parseResult.IsSuccess);
        var contextEntity = Assert.Single(parseResult.Value.Entities, e => e.Id == 5);
        var complex = Assert.IsType<Step242ComplexEntityInstance>(contextEntity.Instance);
        Assert.Equal(4, complex.Constructors.Count);
        Assert.Equal("GEOMETRIC_REPRESENTATION_CONTEXT", complex.Constructors[0].Name);
        Assert.Equal("GLOBAL_UNCERTAINTY_ASSIGNED_CONTEXT", complex.Constructors[1].Name);
        Assert.Equal("GLOBAL_UNIT_ASSIGNED_CONTEXT", complex.Constructors[2].Name);
        Assert.Equal("REPRESENTATION_CONTEXT", complex.Constructors[3].Name);
    }

    [Fact]
    public void Step242SubsetDecoder_CanResolveRepresentationContext_FromComplexInstance()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/complex-context-min.step");

        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var shapeRepEntity = Assert.Single(parseResult.Value.Entities, e => e.Id == 13);
        var contextRefResult = Step242SubsetDecoder.ReadReference(shapeRepEntity, 2, "ADVANCED_BREP_SHAPE_REPRESENTATION context");
        Assert.True(contextRefResult.IsSuccess);

        var contextEntityResult = parseResult.Value.TryGetEntity(contextRefResult.Value.TargetId, "REPRESENTATION_CONTEXT");
        Assert.True(contextEntityResult.IsSuccess);

        var geometricContext = Step242SubsetDecoder.TryGetConstructor(contextEntityResult.Value.Instance, "GEOMETRIC_REPRESENTATION_CONTEXT");
        Assert.NotNull(geometricContext);

        var representationContext = Step242SubsetDecoder.TryGetConstructor(contextEntityResult.Value.Instance, "REPRESENTATION_CONTEXT");
        Assert.NotNull(representationContext);

        var importResult = Step242Importer.ImportBody(text);
        Assert.False(importResult.IsSuccess);
        Assert.DoesNotContain(importResult.Diagnostics, d => d.Message.Contains("Expected identifier", StringComparison.Ordinal));
    }

    private static string LoadFixture(string relativePath)
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
