using System.Text;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242TypedParameterValueTests
{
    [Fact]
    public void StepParser_ParsesTypedParameterValues_FromFixture()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/typed-parameter-values.step");

        var parseResult = Step242SubsetParser.Parse(text);

        Assert.True(parseResult.IsSuccess);

        var uncertaintyEntity = Assert.Single(parseResult.Value.Entities, e => e.Id == 16);
        var typedMeasure = Assert.IsType<Step242TypedValue>(uncertaintyEntity.Arguments[0]);
        Assert.Equal("LENGTH_MEASURE", typedMeasure.Name);
        Assert.Single(typedMeasure.Arguments);

        var lengthWithUnit = Assert.Single(parseResult.Value.Entities, e => e.Id == 28);
        var outerTyped = Assert.IsType<Step242TypedValue>(lengthWithUnit.Arguments[0]);
        Assert.Equal("LENGTH_MEASURE", outerTyped.Name);
        Assert.Single(outerTyped.Arguments);
    }

    [Fact]
    public void Step242SubsetDecoder_UnwrapsTypedNumericValue_ForSurfaceRadius()
    {
        var text = LoadFixture("testdata/step242/syntax-robustness/typed-parameter-values.step");
        var parseResult = Step242SubsetParser.Parse(text);
        Assert.True(parseResult.IsSuccess);

        var sphericalSurface = Assert.Single(parseResult.Value.Entities, e => e.Id == 26);
        var radiusArg = Assert.IsType<Step242TypedValue>(sphericalSurface.Arguments[2]);
        Assert.Equal("LENGTH_MEASURE", radiusArg.Name);

        var decodeResult = Step242SubsetDecoder.ReadSphericalSurface(parseResult.Value, sphericalSurface);

        Assert.True(decodeResult.IsSuccess);
        Assert.Equal(1d, decodeResult.Value.Radius, 6);
    }

    [Fact]
    public void ImportBody_FailsWithTypedValueSyntaxBucket_ForMalformedTypedValue()
    {
        const string text = "DATA;#1=ITEM(LENGTH_MEASURE(1.0";

        var result = Step242Importer.ImportBody(text);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Equal("Importer.StepSyntax.TypedValue", result.Diagnostics[0].Source);
    }

    private static string LoadFixture(string relativePath)
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path, Encoding.UTF8);
    }
}
