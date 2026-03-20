using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Firmament.Lowering;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentBooleanCanonicalSuccessTests
{
    public static TheoryData<string, string, FirmamentLoweredBooleanKind> CanonicalBooleanExamples =>
        new()
        {
            { "testdata/firmament/examples/boolean_add_basic.firmament", "joined", FirmamentLoweredBooleanKind.Add },
            { "testdata/firmament/examples/boolean_subtract_basic.firmament", "carved", FirmamentLoweredBooleanKind.Subtract },
            { "testdata/firmament/examples/boolean_intersect_basic.firmament", "overlap", FirmamentLoweredBooleanKind.Intersect },
            { "testdata/firmament/examples/boolean_box_cylinder_hole.firmament", "hole", FirmamentLoweredBooleanKind.Subtract },
            { "testdata/firmament/examples/boolean_box_cone_throughhole_basic.firmament", "cut", FirmamentLoweredBooleanKind.Subtract },
            { "testdata/firmament/examples/boolean_two_cylinder_holes_basic.firmament", "hole_b", FirmamentLoweredBooleanKind.Subtract },
            { "testdata/firmament/examples/boolean_cylinder_cone_holes_basic.firmament", "cut_b", FirmamentLoweredBooleanKind.Subtract }
        };

    [Theory]
    [MemberData(nameof(CanonicalBooleanExamples))]
    public void CanonicalBooleanExamples_Compile_Execute_WithoutDiagnostics(string fixturePath, string expectedFeatureId, FirmamentLoweredBooleanKind expectedKind)
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));

        Assert.True(result.Compilation.IsSuccess);
        Assert.Empty(result.Compilation.Diagnostics);

        var execution = result.Compilation.Value.PrimitiveExecutionResult!;
        var executedBoolean = Assert.Single(execution.ExecutedBooleans, boolean => boolean.FeatureId == expectedFeatureId);
        Assert.Equal(expectedKind, executedBoolean.Kind);
        Assert.NotEmpty(executedBoolean.Body.Topology.Bodies);
        Assert.NotEmpty(executedBoolean.Body.Topology.Faces);
        Assert.DoesNotContain(result.Compilation.Diagnostics, diagnostic => diagnostic.Message.Contains("Requested boolean feature", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("testdata/firmament/examples/boolean_add_basic.firmament", "joined", 2, "add")]
    [InlineData("testdata/firmament/examples/boolean_subtract_basic.firmament", "carved", 2, "subtract")]
    [InlineData("testdata/firmament/examples/boolean_intersect_basic.firmament", "overlap", 2, "intersect")]
    [InlineData("testdata/firmament/examples/boolean_box_cylinder_hole.firmament", "hole", 1, "subtract")]
    [InlineData("testdata/firmament/examples/boolean_box_cone_throughhole_basic.firmament", "cut", 1, "subtract")]
    [InlineData("testdata/firmament/examples/boolean_two_cylinder_holes_basic.firmament", "hole_b", 2, "subtract")]
    [InlineData("testdata/firmament/examples/boolean_cylinder_cone_holes_basic.firmament", "cut_b", 2, "subtract")]
    public void CanonicalBooleanExamples_Export_Deterministically_WithExpectedMarkers(string fixturePath, string expectedFeatureId, int expectedOpIndex, string expectedKind)
    {
        var first = ExportFixture(fixturePath);
        var second = ExportFixture(fixturePath);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(expectedFeatureId, first.Value.ExportedFeatureId);
        Assert.Equal(expectedOpIndex, first.Value.ExportedOpIndex);
        Assert.Equal("boolean", first.Value.ExportedBodyCategory);
        Assert.Equal(expectedKind, first.Value.ExportedFeatureKind);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("ISO-10303-21", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("ADVANCED_FACE", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("PLANE", first.Value.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void SequentialSafeCompositionExamples_ExportExpectedAnalyticMarkers()
    {
        var twoCylinder = ExportFixture("testdata/firmament/examples/boolean_two_cylinder_holes_basic.firmament");
        var cylinderCone = ExportFixture("testdata/firmament/examples/boolean_cylinder_cone_holes_basic.firmament");

        Assert.True(twoCylinder.IsSuccess);
        Assert.Contains("CYLINDRICAL_SURFACE", twoCylinder.Value.StepText, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(twoCylinder.Value.StepText, "CYLINDRICAL_SURFACE"));

        Assert.True(cylinderCone.IsSuccess);
        Assert.Contains("CYLINDRICAL_SURFACE", cylinderCone.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("CONICAL_SURFACE", cylinderCone.Value.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void OutsideCanonicalSubset_RemainsRejected_WithoutSilentFallback()
    {
        var unsupportedSingleBoxSubtract = FirmamentCorpusHarness.Compile(
            FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m3d-valid-subtract-exec.firmament"));
        var supportedCylinderHoleExport = ExportFixture("testdata/firmament/examples/boolean_box_cylinder_hole.firmament");
        var supportedConeHoleExport = ExportFixture("testdata/firmament/examples/boolean_box_cone_throughhole_basic.firmament");
        var unsupportedCylinderHoleExport = ExportFixture("testdata/firmament/fixtures/m10h1-unsupported-box-with-cylinder-hole.firmament");
        var unsupportedConeHoleExport = ExportFixture("testdata/firmament/fixtures/m10m-unsupported-box-subtract-cone.firmament");

        Assert.False(unsupportedSingleBoxSubtract.Compilation.IsSuccess);
        Assert.Contains(unsupportedSingleBoxSubtract.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && diagnostic.Message.Contains("Requested boolean feature 'cut' (subtract) could not be executed.", StringComparison.Ordinal));

        Assert.True(supportedCylinderHoleExport.IsSuccess);
        Assert.Contains("CYLINDRICAL_SURFACE", supportedCylinderHoleExport.Value.StepText, StringComparison.Ordinal);

        Assert.True(supportedConeHoleExport.IsSuccess);
        Assert.Contains("CONICAL_SURFACE", supportedConeHoleExport.Value.StepText, StringComparison.Ordinal);

        Assert.False(unsupportedCylinderHoleExport.IsSuccess);
        Assert.Contains(unsupportedCylinderHoleExport.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && diagnostic.Message.Contains("Requested boolean feature 'hole' (subtract) could not be executed.", StringComparison.Ordinal));
        Assert.DoesNotContain(unsupportedCylinderHoleExport.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("requires at least one executed primitive or boolean body", StringComparison.Ordinal));

        Assert.False(unsupportedConeHoleExport.IsSuccess);
        Assert.Contains(unsupportedConeHoleExport.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && diagnostic.Message.Contains("Requested boolean feature 'tapered_cut' (subtract) could not be executed.", StringComparison.Ordinal));
        Assert.DoesNotContain(unsupportedConeHoleExport.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("requires at least one executed primitive or boolean body", StringComparison.Ordinal));
    }

    public static TheoryData<string, string, string> UnsupportedMixedPrimitiveBooleanFixtures =>
        new()
        {
            { "testdata/firmament/fixtures/m10j-unsupported-box-add-cylinder.firmament", "joined", "add" },
            { "testdata/firmament/fixtures/m10h1-unsupported-box-with-cylinder-hole.firmament", "hole", "subtract" },
            { "testdata/firmament/fixtures/m10j-unsupported-box-intersect-cylinder.firmament", "overlap", "intersect" },
            { "testdata/firmament/fixtures/m10l-unsupported-box-subtract-sphere-contained.firmament", "cavity", "subtract" },
            { "testdata/firmament/fixtures/m10l-unsupported-box-add-sphere.firmament", "joined", "add" },
            { "testdata/firmament/fixtures/m10l-unsupported-box-intersect-sphere.firmament", "overlap", "intersect" },
            { "testdata/firmament/fixtures/m10m-unsupported-box-subtract-cone.firmament", "tapered_cut", "subtract" },
            { "testdata/firmament/fixtures/m10m-unsupported-box-add-cone.firmament", "joined", "add" },
            { "testdata/firmament/fixtures/m10m-unsupported-box-intersect-cone.firmament", "overlap", "intersect" },
            { "testdata/firmament/fixtures/m10n-unsupported-box-subtract-torus.firmament", "ring_cut", "subtract" },
            { "testdata/firmament/fixtures/m10n-unsupported-box-add-torus.firmament", "joined", "add" },
            { "testdata/firmament/fixtures/m10n-unsupported-box-intersect-torus.firmament", "overlap", "intersect" }
        };

    [Theory]
    [MemberData(nameof(UnsupportedMixedPrimitiveBooleanFixtures))]
    public void MixedPrimitiveBooleans_Remain_Unsupported_And_Fail_Loudly(string fixturePath, string expectedFeatureId, string expectedKind)
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));

        Assert.False(result.Compilation.IsSuccess);
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && diagnostic.Source == "firmament"
            && diagnostic.Message.Contains($"Requested boolean feature '{expectedFeatureId}' ({expectedKind}) could not be executed.", StringComparison.Ordinal));
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && HasExpectedMixedPrimitiveFailure(diagnostic.Message));
    }


    [Theory]
    [InlineData("testdata/firmament/fixtures/m10l-unsupported-box-subtract-sphere-touching-boundary.firmament", "tangent_cavity")]
    [InlineData("testdata/firmament/fixtures/m10l-unsupported-box-subtract-sphere-partially-outside.firmament", "leaking_cavity")]
    public void BoxSphereSubtract_OutsideProvenSubset_RemainsUnsupported(string fixturePath, string expectedFeatureId)
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));

        Assert.False(result.Compilation.IsSuccess);
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && diagnostic.Source == "firmament"
            && diagnostic.Message.Contains($"Requested boolean feature '{expectedFeatureId}' (subtract) could not be executed.", StringComparison.Ordinal));
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && HasExpectedMixedPrimitiveFailure(diagnostic.Message));
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-overlapping-composed-holes.firmament", "hole_b", "HoleInterference")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-tangent-composed-holes.firmament", "hole_b", "TangentContact")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-add-ordering.firmament", "joined", "violates safe subtract feature-graph ordering")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-sphere.firmament", "cavity", "unsupported follow-on tool kind 'sphere'")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-box.firmament", "notch", "unsupported follow-on tool kind 'box'")]
    [InlineData("testdata/firmament/fixtures/m13b-invalid-composed-reenter-safe-family.firmament", "hole", "outside that supported family")]
    public void SequentialCompositionOutsideSafeSubset_RemainsRejectedWithoutFallback(string fixturePath, string expectedFeatureId, string expectedMessageFragment)
    {
        var result = FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));
        var export = ExportFixture(fixturePath);

        Assert.False(result.Compilation.IsSuccess);
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && diagnostic.Message.Contains($"Requested boolean feature '{expectedFeatureId}'", StringComparison.Ordinal));
        Assert.Contains(result.Compilation.Diagnostics, diagnostic => diagnostic.Message.Contains(expectedMessageFragment, StringComparison.Ordinal));

        Assert.False(export.IsSuccess);
        Assert.Contains(export.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && diagnostic.Message.Contains($"Requested boolean feature '{expectedFeatureId}'", StringComparison.Ordinal));
        Assert.DoesNotContain(export.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("requires at least one executed primitive or boolean body", StringComparison.Ordinal));
    }

    public static TheoryData<string, string, string> UnsupportedBoxTorusVariantSources =>
        new()
        {
            { CreateBoxTorusSource("subtract", "ring_cut", "from", "contained", 0d, 0d, 0d), "ring_cut", "subtract" },
            { CreateBoxTorusSource("subtract", "offset_ring_cut", "from", "offset", 6d, 0d, 0d), "offset_ring_cut", "subtract" },
            { CreateBoxTorusSource("subtract", "face_ring_cut", "from", "intersecting", 0d, 0d, 8d, 8d, 3d), "face_ring_cut", "subtract" },
            { CreateBoxTorusSource("subtract", "outside_ring_cut", "from", "outside", 20d, 0d, 0d), "outside_ring_cut", "subtract" },
            { CreateBoxTorusSource("add", "joined", "to", "contained", 0d, 0d, 0d), "joined", "add" },
            { CreateBoxTorusSource("add", "offset_joined", "to", "offset", 6d, 0d, 0d), "offset_joined", "add" },
            { CreateBoxTorusSource("add", "face_joined", "to", "intersecting", 0d, 0d, 8d, 8d, 3d), "face_joined", "add" },
            { CreateBoxTorusSource("add", "outside_joined", "to", "outside", 20d, 0d, 0d), "outside_joined", "add" },
            { CreateBoxTorusSource("intersect", "overlap", "left", "contained", 0d, 0d, 0d), "overlap", "intersect" },
            { CreateBoxTorusSource("intersect", "offset_overlap", "left", "offset", 6d, 0d, 0d), "offset_overlap", "intersect" },
            { CreateBoxTorusSource("intersect", "face_overlap", "left", "intersecting", 0d, 0d, 8d, 8d, 3d), "face_overlap", "intersect" },
            { CreateBoxTorusSource("intersect", "outside_overlap", "left", "outside", 20d, 0d, 0d), "outside_overlap", "intersect" }
        };

    [Theory]
    [MemberData(nameof(UnsupportedBoxTorusVariantSources))]
    public void BoxTorusVariants_RemainUnsupported_Across_Audited_Positions(string source, string expectedFeatureId, string expectedKind)
    {
        var result = FirmamentCorpusHarness.Compile(source);

        Assert.False(result.Compilation.IsSuccess);
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && diagnostic.Source == "firmament"
            && diagnostic.Message.Contains($"Requested boolean feature '{expectedFeatureId}' ({expectedKind}) could not be executed.", StringComparison.Ordinal));
        Assert.Contains(result.Compilation.Diagnostics, diagnostic =>
            diagnostic.Code == KernelDiagnosticCode.NotImplemented
            && HasExpectedMixedPrimitiveFailure(diagnostic.Message));
    }

    private static bool HasExpectedMixedPrimitiveFailure(string message)
        => message.Contains("M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", StringComparison.Ordinal)
           || message.Contains("sequential safe composition only supports subtracting supported cylinder/cone through-holes", StringComparison.Ordinal)
           || message.Contains("feature-graph ordering", StringComparison.Ordinal)
           || message.Contains("unsupported follow-on tool kind", StringComparison.Ordinal)
           || message.Contains("analytic hole candidate failed diagnostic", StringComparison.Ordinal)
           || message.Contains("analytic hole surface kind", StringComparison.Ordinal);

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    private static string CreateBoxTorusSource(string op, string featureId, string targetField, string nameSuffix, double offsetX, double offsetY, double offsetZ, double majorRadius = 6d, double minorRadius = 2d) =>
        $"""
        firmament:
          version: 1
        
        model:
          name: m10n_box_torus_{nameSuffix}_{op}
          units: mm
        
        ops[2]:
          -
            op: box
            id: base
            size[3]:
              40
              30
              12
        
          -
            op: {op}
            id: {featureId}
            {targetField}: base
            with:
              op: torus
              major_radius: {majorRadius}
              minor_radius: {minorRadius}
            place:
              on: origin
              offset[3]:
                {offsetX}
                {offsetY}
                {offsetZ}
        """;

    private static Aetheris.Kernel.Core.Results.KernelResult<FirmamentStepExportResult> ExportFixture(string fixturePath) =>
        FirmamentStepExporter.Export(new FirmamentCompileRequest(new FirmamentSourceDocument(FirmamentCorpusHarness.ReadFixtureText(fixturePath))));
}
