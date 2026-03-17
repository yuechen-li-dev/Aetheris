using System.Linq;
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
    public void Primitive_With_SelectorShaped_PlaceAnchor_Uses_Selector_Contracts()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7b-valid-placement-selector-anchors.firmament");
        Assert.True(result.Compilation.IsSuccess);
    }

    [Fact]
    public void Primitive_With_Sphere_Surface_PlaceAnchor_Is_Valid()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7b-valid-placement-selector-anchor-sphere.firmament");
        Assert.True(result.Compilation.IsSuccess);
    }

    [Fact]
    public void Primitive_With_Boolean_Edges_PlaceAnchor_Runtime_Is_Deterministically_Reported_When_Unresolved()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7b-valid-placement-selector-anchor-boolean.firmament");
        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(FirmamentDiagnosticCodes.ValidationTargetSelectorResolvedEmpty.Value, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Primitive_With_SelectorShaped_PlaceAnchor_Unknown_Root_Is_Invalid()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7b-invalid-placement-selector-root-missing.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(FirmamentDiagnosticCodes.ValidationTargetUnknownSelectorRootFeatureId.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("'place.on'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Primitive_With_SelectorShaped_PlaceAnchor_Forward_Root_Is_Invalid()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7b-invalid-placement-selector-root-forward.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(FirmamentDiagnosticCodes.ValidationTargetUnknownSelectorRootFeatureId.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("'future'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("'place.on'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Primitive_With_SelectorShaped_PlaceAnchor_Illegal_Box_Port_Is_Invalid()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7b-invalid-placement-selector-port-box.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(FirmamentDiagnosticCodes.ValidationTargetSelectorPortNotAllowedForFeatureKind.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("'circular_edges'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("'box'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("'place.on'", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Primitive_With_SelectorShaped_PlaceAnchor_Illegal_Sphere_Port_Is_Invalid()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7b-invalid-placement-selector-port-sphere.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(FirmamentDiagnosticCodes.ValidationTargetSelectorPortNotAllowedForFeatureKind.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("'top_face'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("'sphere'", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("'place.on'", diagnostic.Message, StringComparison.Ordinal);
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
    public void Placement_Runtime_Selector_Resolution_Failure_Is_Diagnostic()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7c-invalid-placement-selector-runtime-empty.firmament");

        Assert.False(result.Compilation.IsSuccess);
        var diagnostic = Assert.Single(result.Compilation.Diagnostics);
        Assert.Contains(FirmamentDiagnosticCodes.ValidationTargetSelectorResolvedEmpty.Value, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("resolved empty", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
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



    [Fact]
    public void Selector_Placement_On_TopFace_Lands_At_Expected_Anchor_Point()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7c-valid-selector-top-face-anchor.firmament");
        Assert.True(result.Compilation.IsSuccess);

        var placed = result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives.Single(p => p.FeatureId == "placed");
        Assert.True(placed.Body.TryGetVertexPoint(new Aetheris.Kernel.Core.Topology.VertexId(1), out var p1));
        Assert.Equal(new Aetheris.Kernel.Core.Math.Point3D(-1d, -1d, 4d), p1);
    }

    [Fact]
    public void Selector_Placement_On_Vertices_And_Edges_Uses_Deterministic_Centroid_Aggregates()
    {
        var result = CompileFixture("testdata/firmament/fixtures/m7c-valid-selector-vertices-edges-anchors.firmament");
        Assert.True(result.Compilation.IsSuccess);

        var vertexPlaced = result.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives.Single(p => p.FeatureId == "on_vertices");
        Assert.True(vertexPlaced.Body.TryGetVertexPoint(new Aetheris.Kernel.Core.Topology.VertexId(1), out var v1));
        Assert.Equal(new Aetheris.Kernel.Core.Math.Point3D(-0.5d, -1.25d, 2d), v1);

        var edgePlaced = result.Compilation.Value.PrimitiveExecutionResult.ExecutedPrimitives.Single(p => p.FeatureId == "on_edges");
        Assert.True(edgePlaced.Body.TryGetVertexPoint(new Aetheris.Kernel.Core.Topology.VertexId(1), out var e1));
        Assert.Equal(new Aetheris.Kernel.Core.Math.Point3D(-0.5d, -1.25d, 0d), e1);
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(fixturePath);
        var compiler = new FirmamentCompiler();
        return compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
    }
}
