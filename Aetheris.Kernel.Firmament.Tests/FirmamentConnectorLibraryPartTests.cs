using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Firmament.Connectors;
using Aetheris.Kernel.Firmament.Lanes;
using Aetheris.Kernel.Firmament.Lowering;
using Aetheris.Kernel.Firmament.ParsedModel;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentConnectorLibraryPartTests
{
    [Fact]
    public void StandardLibrary_CubeWithHole_Part_Succeeds()
    {
        var result = StandardLibraryReusableParts.CreateCubeWithCylindricalHole();

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.Topology.Faces);
        Assert.Contains(
            result.Value.Topology.Faces,
            face => result.Value.TryGetFaceSurfaceGeometry(face.Id, out var surface)
                && surface?.Kind == SurfaceGeometryKind.Cylinder);
    }

    [Fact]
    public void Firmament_Connector_Resolves_StandardLibrary_Part()
    {
        var result = FirmamentPartLibraryConnector.ResolvePart("standard_library/cube_with_cylindrical_hole");

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.Topology.Faces);
    }

    [Fact]
    public void Firmament_Uses_CubeWithHole_LibraryPart_EndToEnd()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/library_part_cube_with_hole_basic.firmament");

        var compile = FirmamentCorpusHarness.Compile(source);

        Assert.True(compile.Compilation.IsSuccess);
        var execution = compile.Compilation.Value.PrimitiveExecutionResult!;
        var primitive = Assert.Single(execution.ExecutedPrimitives);
        Assert.Equal("lib_part_1", primitive.FeatureId);
        Assert.Equal(FirmamentLoweredPrimitiveKind.LibraryPart, primitive.Kind);
    }

    [Fact]
    public void ConnectorPart_With_SlotCut_Composition_Succeeds()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/composed_part_with_slot.firmament");

        var compile = FirmamentCorpusHarness.Compile(source);

        Assert.True(compile.Compilation.IsSuccess);
        Assert.DoesNotContain(
            compile.Compilation.Diagnostics,
            diagnostic => diagnostic.Message.Contains("slot_carve", StringComparison.Ordinal));
    }

    [Fact]
    public void Firmament_Composed_Model_Export_Succeeds()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/examples/composed_part_with_slot.firmament");

        var compile = FirmamentCorpusHarness.Compile(source);
        Assert.True(compile.Compilation.IsSuccess);

        var export = FirmamentStepExporter.Export(new FirmamentCompileRequest(new FirmamentSourceDocument(source)));
        Assert.True(export.IsSuccess);
    }

    [Fact]
    public void LaneConnectorResponsibility_Is_Not_Blurred()
    {
        Assert.False(FirmamentLaneOperationCatalog.IsLaneRoutedOperation(FirmamentKnownOpKind.LibraryPart));
        Assert.True(FirmamentLaneOperationCatalog.IsLaneRoutedOperation(FirmamentKnownOpKind.RoundedCornerBox));
        Assert.True(FirmamentLaneOperationCatalog.IsLaneRoutedOperation(FirmamentKnownOpKind.SlotCut));
    }
}
