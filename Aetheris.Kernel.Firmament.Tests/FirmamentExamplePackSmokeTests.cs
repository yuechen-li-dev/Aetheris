using System.Linq;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentExamplePackSmokeTests
{
    public static TheoryData<string> ExampleFiles =>
    [
        "testdata/firmament/examples/box_basic.firmament",
        "testdata/firmament/examples/cylinder_basic.firmament",
        "testdata/firmament/examples/sphere_basic.firmament",
        "testdata/firmament/examples/box_with_hole.firmament",
        "testdata/firmament/examples/placed_primitive.firmament",
        "testdata/firmament/examples/cnc_min_tool_radius_demo.firmament"
    ];

    [Theory]
    [MemberData(nameof(ExampleFiles))]
    public void ExamplePack_Files_Are_Canonical_And_Compile(string fixturePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(fixturePath);

        var formatResult = new FirmamentFormatter().Format(new FirmamentFormatRequest(new FirmamentSourceDocument(source)));
        Assert.True(formatResult.Formatting.IsSuccess);
        Assert.Equal(source, formatResult.Formatting.Value.Text);

        var compileResult = FirmamentCorpusHarness.Compile(source);
        Assert.True(compileResult.Compilation.IsSuccess);
    }

    [Theory]
    [InlineData("testdata/firmament/examples/box_basic.firmament", "base", 0, "primitive", "box")]
    [InlineData("testdata/firmament/examples/cylinder_basic.firmament", "post", 0, "primitive", "cylinder")]
    [InlineData("testdata/firmament/examples/sphere_basic.firmament", "ball", 0, "primitive", "sphere")]
    [InlineData("testdata/firmament/examples/box_with_hole.firmament", "base", 0, "primitive", "box")]
    [InlineData("testdata/firmament/examples/placed_primitive.firmament", "post", 1, "primitive", "cylinder")]
    public void ExamplePack_GoldenPath_Examples_Export(string fixturePath, string expectedFeatureId, int expectedOpIndex, string expectedBodyCategory, string expectedFeatureKind)
    {
        var first = ExportFixture(fixturePath);
        var second = ExportFixture(fixturePath);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(expectedFeatureId, first.Value.ExportedFeatureId);
        Assert.Equal(expectedOpIndex, first.Value.ExportedOpIndex);
        Assert.Equal(expectedBodyCategory, first.Value.ExportedBodyCategory);
        Assert.Equal(expectedFeatureKind, first.Value.ExportedFeatureKind);
        Assert.Equal(FirmamentStepExporter.LastExecutedGeometricBodyPolicy, first.Value.ExportBodyPolicy);
        Assert.Equal(FirmamentStepExporter.LastExecutedGeometricBodySelectionReason, first.Value.ExportBodySelectionReason);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("ISO-10303-21", first.Value.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void ExamplePack_SchemaExample_Attaches_CncSchema_And_BooleanPlan()
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/cnc_min_tool_radius_demo.firmament"));

        Assert.True(result.Compilation.IsSuccess);
        Assert.NotNull(result.Compilation.Value.CompiledSchema);
        Assert.Single(result.Compilation.Value.PrimitiveLoweringPlan!.Booleans);
        Assert.Equal("hole", result.Compilation.Value.PrimitiveLoweringPlan.Booleans.Single().FeatureId);
    }

    private static Aetheris.Kernel.Core.Results.KernelResult<FirmamentStepExportResult> ExportFixture(string fixturePath) =>
        FirmamentStepExporter.Export(new FirmamentCompileRequest(new FirmamentSourceDocument(FirmamentCorpusHarness.ReadFixtureText(fixturePath))));
}
