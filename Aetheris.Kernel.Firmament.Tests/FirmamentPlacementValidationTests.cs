using Aetheris.Kernel.Firmament.Diagnostics;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentPlacementValidationTests
{
    [Fact]
    public void Primitive_Without_Place_Remains_Valid()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m3c-valid-box-exec.firmament");
        Assert.True(result.Compilation.IsSuccess);
    }

    [Fact]
    public void Primitive_With_Origin_Place_Is_Valid()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7a-valid-box-origin-placement.firmament");
        Assert.True(result.Compilation.IsSuccess);
    }

    [Fact]
    public void Primitive_With_SelectorShaped_PlaceAnchor_Is_Valid()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7a-valid-box-selector-placement.firmament");
        Assert.True(result.Compilation.IsSuccess);
    }

    [Fact]
    public void Primitive_With_Place_Missing_On_Is_Invalid()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7a-invalid-box-placement-missing-on.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(FirmamentDiagnosticCodes.PlacementMissingRequiredField.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("'place.on'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Primitive_With_Place_Missing_Offset_Is_Invalid()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7a-invalid-box-placement-missing-offset.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(FirmamentDiagnosticCodes.PlacementMissingRequiredField.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("'place.offset'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Primitive_With_Place_Invalid_Anchor_Is_Invalid()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7a-invalid-box-placement-invalid-anchor.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(FirmamentDiagnosticCodes.PlacementInvalidAnchorShape.Value, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Primitive_With_Place_Invalid_Offset_Length_Is_Invalid()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7a-invalid-box-placement-offset-length.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(FirmamentDiagnosticCodes.PlacementInvalidOffsetShape.Value, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Primitive_With_Place_NonNumeric_Offset_Component_Is_Invalid()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7a-invalid-box-placement-offset-non-numeric.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(FirmamentDiagnosticCodes.PlacementInvalidOffsetValue.Value, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Placement_Declarations_Do_Not_Change_Primitive_Execution_Output()
    {
        var baseline = CompileFixture("testdata/firmament/fixtures/m3c-valid-box-exec.firmament");
        var withPlacement = CompileFixture("testdata/firmament/fixtures/m7a-valid-box-origin-placement.firmament");

        Assert.True(baseline.Compilation.IsSuccess);
        Assert.True(withPlacement.Compilation.IsSuccess);

        var basePrimitive = Assert.Single(baseline.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);
        var placedPrimitive = Assert.Single(withPlacement.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives);

        Assert.Equal(basePrimitive.Kind, placedPrimitive.Kind);
        Assert.Equal(basePrimitive.FeatureId, placedPrimitive.FeatureId);
        Assert.Equal(basePrimitive.Body.Topology.Faces.Count(), placedPrimitive.Body.Topology.Faces.Count());
        Assert.Equal(basePrimitive.Body.Topology.Edges.Count(), placedPrimitive.Body.Topology.Edges.Count());
    }

    [Fact]
    public void Placement_Diagnostics_Are_Deterministic()
    {
        var fixture = "testdata/firmament/fixtures/m7a-invalid-box-placement-missing-on.firmament";
        var first = CompileFixture(fixture);
        var second = CompileFixture(fixture);

        Assert.False(first.Compilation.IsSuccess);
        Assert.False(second.Compilation.IsSuccess);

        var firstDiagnostic = Assert.Single(first.Compilation.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Compilation.Diagnostics);
        Assert.Equal(firstDiagnostic.Message, secondDiagnostic.Message);
        Assert.Equal(firstDiagnostic.Code, secondDiagnostic.Code);
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(fixturePath);
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }
}
