using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentStepExporterTests
{
    [Fact]
    public void Export_SingleBoxFixture_Returns_Explicit_Metadata_And_Persisted_Artifact()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("testdata/firmament/fixtures/m10a-valid-single-box-export.firmament");

        var first = Export(source);
        var second = Export(source);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(FirmamentStepExporter.LastExecutedGeometricBodyPolicy, first.Value.ExportBodyPolicy);
        Assert.Equal(FirmamentStepExporter.LastExecutedGeometricBodySelectionReason, first.Value.ExportBodySelectionReason);
        Assert.Equal("base", first.Value.ExportedFeatureId);
        Assert.Equal(0, first.Value.ExportedOpIndex);
        Assert.Equal("primitive", first.Value.ExportedBodyCategory);
        Assert.Equal("box", first.Value.ExportedFeatureKind);
        Assert.NotEmpty(first.Value.StepText);
        Assert.Contains("ISO-10303-21", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", first.Value.StepText, StringComparison.Ordinal);
        Assert.Equal(first.Value.StepText, second.Value.StepText);

        var artifactPath = WriteExportArtifact("m10a-box.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }

    [Fact]
    public void Export_PlacedPrimitiveFixture_Exports_Successfully_With_Metadata_And_Deterministic_Text()
    {
        var first = ExportFixture("testdata/firmament/fixtures/m10a-valid-placed-box-export.firmament");
        var second = ExportFixture("testdata/firmament/fixtures/m10a-valid-placed-box-export.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("placed_box", first.Value.ExportedFeatureId);
        Assert.Equal(0, first.Value.ExportedOpIndex);
        Assert.Equal("primitive", first.Value.ExportedBodyCategory);
        Assert.Equal("box", first.Value.ExportedFeatureKind);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("CARTESIAN_POINT", first.Value.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_BooleanFixture_Exports_Successfully_With_Metadata_And_Deterministic_Text()
    {
        var first = ExportFixture("testdata/firmament/fixtures/m10a-valid-boolean-export.firmament");
        var second = ExportFixture("testdata/firmament/fixtures/m10a-valid-boolean-export.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("joined", first.Value.ExportedFeatureId);
        Assert.Equal(1, first.Value.ExportedOpIndex);
        Assert.Equal("boolean", first.Value.ExportedBodyCategory);
        Assert.Equal("add", first.Value.ExportedFeatureKind);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("ADVANCED_FACE", first.Value.StepText, StringComparison.Ordinal);

        var artifactPath = WriteExportArtifact("m10a-boolean.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }

    [Fact]
    public void Export_SupportedBoxCylinderHole_Exports_Deterministically_With_Semantic_Diameter_Pmi()
    {
        var first = ExportFixture("testdata/firmament/examples/boolean_box_cylinder_hole.firmament");
        var second = ExportFixture("testdata/firmament/examples/boolean_box_cylinder_hole.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Equal("hole", first.Value.ExportedFeatureId);
        Assert.Equal(1, first.Value.ExportedOpIndex);
        Assert.Equal("boolean", first.Value.ExportedBodyCategory);
        Assert.Equal("subtract", first.Value.ExportedFeatureKind);
        Assert.Contains("CYLINDRICAL_SURFACE", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("SHAPE_ASPECT", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("PROPERTY_DEFINITION_REPRESENTATION", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("SHAPE_DIMENSION_REPRESENTATION", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("MEASURE_REPRESENTATION_ITEM('diameter',8,#", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("firmament-feature:hole", first.Value.StepText, StringComparison.Ordinal);
        Assert.DoesNotContain("DRAUGHTING_CALLOUT", first.Value.StepText, StringComparison.Ordinal);
        Assert.DoesNotContain("ANNOTATION_PLANE", first.Value.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_SupportedBoxConeThroughHole_Exports_Deterministically_With_ConicalSurface_And_Persisted_Artifact()
    {
        var first = ExportFixture("testdata/firmament/examples/boolean_box_cone_throughhole_basic.firmament");
        var second = ExportFixture("testdata/firmament/examples/boolean_box_cone_throughhole_basic.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Equal("cut", first.Value.ExportedFeatureId);
        Assert.Equal(1, first.Value.ExportedOpIndex);
        Assert.Equal("boolean", first.Value.ExportedBodyCategory);
        Assert.Equal("subtract", first.Value.ExportedFeatureKind);
        Assert.Contains("CONICAL_SURFACE", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", first.Value.StepText, StringComparison.Ordinal);

        var artifactPath = WriteExportArtifact("boolean_box_cone_throughhole_basic.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }

    [Fact]
    public void Export_SupportedTwoCylinderComposition_Exports_Two_Semantic_Diameter_Items_Deterministically()
    {
        var first = ExportFixture("testdata/firmament/examples/boolean_two_cylinder_holes_basic.firmament");
        var second = ExportFixture("testdata/firmament/examples/boolean_two_cylinder_holes_basic.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Equal("hole_b", first.Value.ExportedFeatureId);
        Assert.Equal(2, first.Value.ExportedOpIndex);
        Assert.Equal("boolean", first.Value.ExportedBodyCategory);
        Assert.Equal("subtract", first.Value.ExportedFeatureKind);
        Assert.Equal(2, CountOccurrences(first.Value.StepText, "CYLINDRICAL_SURFACE"));
        Assert.Equal(2, CountOccurrences(first.Value.StepText, "SHAPE_ASPECT"));
        Assert.Equal(2, CountOccurrences(first.Value.StepText, "MEASURE_REPRESENTATION_ITEM"));
        Assert.Contains("firmament-feature:hole_a", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("firmament-feature:hole_b", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("MEASURE_REPRESENTATION_ITEM('diameter',8,#", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("MEASURE_REPRESENTATION_ITEM('diameter',6,#", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", first.Value.StepText, StringComparison.Ordinal);

        var artifactPath = WriteExportArtifact("boolean_two_cylinder_holes_basic.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }

    [Fact]
    public void Export_SupportedCylinderConeComposition_Exports_Only_Cylinder_Diameter_Pmi()
    {
        var first = ExportFixture("testdata/firmament/examples/boolean_cylinder_cone_holes_basic.firmament");
        var second = ExportFixture("testdata/firmament/examples/boolean_cylinder_cone_holes_basic.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Equal("cut_b", first.Value.ExportedFeatureId);
        Assert.Equal(2, first.Value.ExportedOpIndex);
        Assert.Equal("boolean", first.Value.ExportedBodyCategory);
        Assert.Equal("subtract", first.Value.ExportedFeatureKind);
        Assert.Contains("CYLINDRICAL_SURFACE", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("CONICAL_SURFACE", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("firmament-feature:hole_a", first.Value.StepText, StringComparison.Ordinal);
        Assert.DoesNotContain("firmament-feature:cut_b", first.Value.StepText, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(first.Value.StepText, "MEASURE_REPRESENTATION_ITEM"));
        Assert.Contains("MEASURE_REPRESENTATION_ITEM('diameter',8,#", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("MANIFOLD_SOLID_BREP", first.Value.StepText, StringComparison.Ordinal);

        var artifactPath = WriteExportArtifact("boolean_cylinder_cone_holes_basic.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }

    [Fact]
    public void Export_SupportedBoxSphereCavity_Uses_BrepWithVoids_And_Is_Deterministic()
    {
        var first = ExportFixture("testdata/firmament/examples/boolean_box_sphere_cavity_basic.firmament");
        var second = ExportFixture("testdata/firmament/examples/boolean_box_sphere_cavity_basic.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Equal("cavity", first.Value.ExportedFeatureId);
        Assert.Equal(1, first.Value.ExportedOpIndex);
        Assert.Equal("boolean", first.Value.ExportedBodyCategory);
        Assert.Equal("subtract", first.Value.ExportedFeatureKind);
        Assert.Contains("SPHERICAL_SURFACE", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("BREP_WITH_VOIDS", first.Value.StepText, StringComparison.Ordinal);
        Assert.Contains("ORIENTED_CLOSED_SHELL", first.Value.StepText, StringComparison.Ordinal);
        Assert.DoesNotContain("MANIFOLD_SOLID_BREP", first.Value.StepText, StringComparison.Ordinal);

        var artifactPath = WriteExportArtifact("boolean_box_sphere_cavity_basic.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }


    [Fact]
    public void Export_NonCylinderOrFailedBooleanCases_DoNotEmit_SemanticPmi()
    {
        var boxSubtract = ExportFixture("testdata/firmament/examples/boolean_subtract_basic.firmament");
        var coneHole = ExportFixture("testdata/firmament/examples/boolean_box_cone_throughhole_basic.firmament");
        var containedCylinder = ExportFixture("testdata/firmament/fixtures/m10h1-unsupported-box-with-cylinder-hole.firmament");
        var supportedSphereCavity = ExportFixture("testdata/firmament/examples/boolean_box_sphere_cavity_basic.firmament");
        var unsupportedTorus = ExportFixture("testdata/firmament/fixtures/m10n-unsupported-box-subtract-torus.firmament");

        Assert.True(boxSubtract.IsSuccess);
        Assert.DoesNotContain("SHAPE_ASPECT", boxSubtract.Value.StepText, StringComparison.Ordinal);
        Assert.DoesNotContain("MEASURE_REPRESENTATION_ITEM", boxSubtract.Value.StepText, StringComparison.Ordinal);

        Assert.True(coneHole.IsSuccess);
        Assert.DoesNotContain("SHAPE_ASPECT", coneHole.Value.StepText, StringComparison.Ordinal);
        Assert.DoesNotContain("MEASURE_REPRESENTATION_ITEM", coneHole.Value.StepText, StringComparison.Ordinal);
        Assert.DoesNotContain("DRAUGHTING_CALLOUT", coneHole.Value.StepText, StringComparison.Ordinal);
        Assert.DoesNotContain("ANNOTATION_PLANE", coneHole.Value.StepText, StringComparison.Ordinal);

        Assert.True(containedCylinder.IsSuccess);
        Assert.Contains("SHAPE_ASPECT", containedCylinder.Value.StepText, StringComparison.Ordinal);
        Assert.True(supportedSphereCavity.IsSuccess);
        Assert.False(unsupportedTorus.IsSuccess);
    }

    [Fact]
    public void Export_ContainedBoxWithCylinderHole_Succeeds_Deterministically_Without_Fallback()
    {
        var first = ExportFixture("testdata/firmament/fixtures/m10h1-unsupported-box-with-cylinder-hole.firmament");
        var second = ExportFixture("testdata/firmament/fixtures/m10h1-unsupported-box-with-cylinder-hole.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("BREP_WITH_VOIDS", first.Value.StepText, StringComparison.Ordinal);
    }


    [Theory]
    [InlineData("testdata/firmament/fixtures/m10l-unsupported-box-subtract-sphere-touching-boundary.firmament", "tangent_cavity")]
    [InlineData("testdata/firmament/fixtures/m10l-unsupported-box-subtract-sphere-partially-outside.firmament", "leaking_cavity")]
    [InlineData("testdata/firmament/fixtures/m10l-unsupported-box-add-sphere.firmament", "joined")]
    [InlineData("testdata/firmament/fixtures/m10l-unsupported-box-intersect-sphere.firmament", "overlap")]
    [InlineData("testdata/firmament/fixtures/m10m-unsupported-box-subtract-cone.firmament", "tapered_cut")]
    [InlineData("testdata/firmament/fixtures/m10m-unsupported-box-add-cone.firmament", "joined")]
    [InlineData("testdata/firmament/fixtures/m10m-unsupported-box-intersect-cone.firmament", "overlap")]
    [InlineData("testdata/firmament/fixtures/m10n-unsupported-box-subtract-torus.firmament", "ring_cut")]
    [InlineData("testdata/firmament/fixtures/m10n-unsupported-box-add-torus.firmament", "joined")]
    [InlineData("testdata/firmament/fixtures/m10n-unsupported-box-intersect-torus.firmament", "overlap")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-overlapping-composed-holes.firmament", "hole_b")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-tangent-composed-holes.firmament", "hole_b")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-add-ordering.firmament", "joined")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-sphere.firmament", "cavity")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-box.firmament", "notch")]
    [InlineData("testdata/firmament/fixtures/m13b-invalid-composed-reenter-safe-family.firmament", "hole")]
    public void Export_UnsupportedMixedPrimitiveBooleanFixtures_Fail_Loudly_Without_Fallback(string fixturePath, string expectedFeatureId)
    {
        var first = ExportFixture(fixturePath);
        var second = ExportFixture(fixturePath);

        Assert.False(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Contains(first.Diagnostics, diagnostic => diagnostic.Message.Contains($"Requested boolean feature '{expectedFeatureId}'", StringComparison.Ordinal));
        Assert.Contains(first.Diagnostics, diagnostic => HasExpectedMixedPrimitiveFailure(diagnostic.Message));
        Assert.DoesNotContain(first.Diagnostics, diagnostic => diagnostic.Message.Contains("requires at least one executed primitive or boolean body", StringComparison.Ordinal));
    }

    public static TheoryData<string, string> UnsupportedBoxTorusVariantSources =>
        new()
        {
            { CreateBoxTorusSource("subtract", "ring_cut", "from", 0d, 0d, 0d), "ring_cut" },
            { CreateBoxTorusSource("subtract", "offset_ring_cut", "from", 6d, 0d, 0d), "offset_ring_cut" },
            { CreateBoxTorusSource("subtract", "face_ring_cut", "from", 0d, 0d, 8d, 8d, 3d), "face_ring_cut" },
            { CreateBoxTorusSource("subtract", "outside_ring_cut", "from", 20d, 0d, 0d), "outside_ring_cut" },
            { CreateBoxTorusSource("add", "joined", "to", 0d, 0d, 0d), "joined" },
            { CreateBoxTorusSource("add", "offset_joined", "to", 6d, 0d, 0d), "offset_joined" },
            { CreateBoxTorusSource("add", "face_joined", "to", 0d, 0d, 8d, 8d, 3d), "face_joined" },
            { CreateBoxTorusSource("add", "outside_joined", "to", 20d, 0d, 0d), "outside_joined" },
            { CreateBoxTorusSource("intersect", "overlap", "left", 0d, 0d, 0d), "overlap" },
            { CreateBoxTorusSource("intersect", "offset_overlap", "left", 6d, 0d, 0d), "offset_overlap" },
            { CreateBoxTorusSource("intersect", "face_overlap", "left", 0d, 0d, 8d, 8d, 3d), "face_overlap" },
            { CreateBoxTorusSource("intersect", "outside_overlap", "left", 20d, 0d, 0d), "outside_overlap" }
        };

    [Theory]
    [MemberData(nameof(UnsupportedBoxTorusVariantSources))]
    public void Export_BoxTorusVariants_Fail_Loudly_Without_Fallback(string source, string expectedFeatureId)
    {
        var first = Export(source);
        var second = Export(source);

        Assert.False(first.IsSuccess);
        Assert.False(second.IsSuccess);
        Assert.Equal(first.Diagnostics, second.Diagnostics);
        Assert.Contains(first.Diagnostics, diagnostic => diagnostic.Message.Contains($"Requested boolean feature '{expectedFeatureId}'", StringComparison.Ordinal));
        Assert.Contains(first.Diagnostics, diagnostic => HasExpectedMixedPrimitiveFailure(diagnostic.Message));
        Assert.DoesNotContain(first.Diagnostics, diagnostic => diagnostic.Message.Contains("requires at least one executed primitive or boolean body", StringComparison.Ordinal));
    }

    private static bool HasExpectedMixedPrimitiveFailure(string message)
        => message.Contains("M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", StringComparison.Ordinal)
           || message.Contains("sequential safe composition only supports subtracting supported cylinder/cone analytic holes", StringComparison.Ordinal)
           || message.Contains("safe subtract", StringComparison.Ordinal)
           || message.Contains("unsupported follow-on tool kind", StringComparison.Ordinal)
           || message.Contains("Boolean feature", StringComparison.Ordinal)
           || message.Contains("analytic hole surface kind", StringComparison.Ordinal)
           || message.Contains("fully enclosed spherical cavity", StringComparison.Ordinal);

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

    [Fact]
    public void Export_SchemaPresent_Model_Still_Exports_With_Stable_Metadata_And_Deterministic_Text()
    {
        var first = ExportFixture("testdata/firmament/fixtures/m10b-valid-schema-box-export.firmament");
        var second = ExportFixture("testdata/firmament/fixtures/m10b-valid-schema-box-export.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("schema_box", first.Value.ExportedFeatureId);
        Assert.Equal(0, first.Value.ExportedOpIndex);
        Assert.Equal("primitive", first.Value.ExportedBodyCategory);
        Assert.Equal("box", first.Value.ExportedFeatureKind);
        Assert.Equal(first.Value.ExportBodyPolicy, second.Value.ExportBodyPolicy);
        Assert.Equal(first.Value.ExportBodySelectionReason, second.Value.ExportBodySelectionReason);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
    }

    [Fact]
    public void Export_CylinderExample_Exports_Successfully_With_Metadata_Deterministic_Text_And_Persisted_Artifact()
    {
        var first = ExportFixture("testdata/firmament/examples/cylinder_basic.firmament");
        var second = ExportFixture("testdata/firmament/examples/cylinder_basic.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("post", first.Value.ExportedFeatureId);
        Assert.Equal(0, first.Value.ExportedOpIndex);
        Assert.Equal("primitive", first.Value.ExportedBodyCategory);
        Assert.Equal("cylinder", first.Value.ExportedFeatureKind);
        Assert.NotEmpty(first.Value.StepText);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("CYLINDRICAL_SURFACE", first.Value.StepText, StringComparison.Ordinal);

        var artifactPath = WriteExportArtifact("m10c-cylinder.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }


    [Fact]
    public void Export_SphereExample_Exports_Successfully_With_Metadata_Deterministic_Text_And_Persisted_Artifact()
    {
        var first = ExportFixture("testdata/firmament/examples/sphere_basic.firmament");
        var second = ExportFixture("testdata/firmament/examples/sphere_basic.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("ball", first.Value.ExportedFeatureId);
        Assert.Equal(0, first.Value.ExportedOpIndex);
        Assert.Equal("primitive", first.Value.ExportedBodyCategory);
        Assert.Equal("sphere", first.Value.ExportedFeatureKind);
        Assert.NotEmpty(first.Value.StepText);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("SPHERICAL_SURFACE", first.Value.StepText, StringComparison.Ordinal);

        var artifactPath = WriteExportArtifact("m10d-sphere.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }

    [Fact]
    public void Export_TorusExample_Exports_Successfully_With_Metadata_Deterministic_Text_And_Persisted_Artifact()
    {
        var first = ExportFixture("testdata/firmament/examples/torus_basic.firmament");
        var second = ExportFixture("testdata/firmament/examples/torus_basic.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("donut1", first.Value.ExportedFeatureId);
        Assert.Equal(0, first.Value.ExportedOpIndex);
        Assert.Equal("primitive", first.Value.ExportedBodyCategory);
        Assert.Equal("torus", first.Value.ExportedFeatureKind);
        Assert.NotEmpty(first.Value.StepText);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("TOROIDAL_SURFACE", first.Value.StepText, StringComparison.Ordinal);

        var artifactPath = WriteExportArtifact("m10g2-torus.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }

    [Fact]
    public void Export_Placed_Sphere_And_Torus_Remain_Supported()
    {
        const string sphereSource = """
firmament:
  version: 1

model:
  name: placed_sphere_export
  units: mm

ops[2]:
  -
    op: box
    id: base
    size[3]:
      8
      8
      4

  -
    op: sphere
    id: ball
    radius: 3
    place:
      on: base.top_face
      offset[3]:
        0
        0
        0
""";

        const string torusSource = """
firmament:
  version: 1

model:
  name: placed_torus_export
  units: mm

ops[2]:
  -
    op: box
    id: base
    size[3]:
      8
      8
      4

  -
    op: torus
    id: donut
    major_radius: 5
    minor_radius: 2
    place:
      on: base.top_face
      offset[3]:
        0
        0
        0
""";

        var sphereExport = Export(sphereSource);
        var torusExport = Export(torusSource);

        Assert.True(sphereExport.IsSuccess);
        Assert.True(torusExport.IsSuccess);
        Assert.Equal("ball", sphereExport.Value.ExportedFeatureId);
        Assert.Equal("sphere", sphereExport.Value.ExportedFeatureKind);
        Assert.Contains("SPHERICAL_SURFACE", sphereExport.Value.StepText, StringComparison.Ordinal);
        Assert.Equal("donut", torusExport.Value.ExportedFeatureId);
        Assert.Equal("torus", torusExport.Value.ExportedFeatureKind);
        Assert.Contains("TOROIDAL_SURFACE", torusExport.Value.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_ConeExample_Exports_Successfully_With_Metadata_Deterministic_Text_And_Persisted_Artifact()
    {
        var first = ExportFixture("testdata/firmament/examples/cone_frustum_basic.firmament");
        var second = ExportFixture("testdata/firmament/examples/cone_frustum_basic.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("frustum1", first.Value.ExportedFeatureId);
        Assert.Equal(0, first.Value.ExportedOpIndex);
        Assert.Equal("primitive", first.Value.ExportedBodyCategory);
        Assert.Equal("cone", first.Value.ExportedFeatureKind);
        Assert.NotEmpty(first.Value.StepText);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("CONICAL_SURFACE", first.Value.StepText, StringComparison.Ordinal);

        var artifactPath = WriteExportArtifact("m10e-cone.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }

    [Fact]
    public void Export_PointedConeExample_Exports_Successfully_With_Metadata_Deterministic_Text_And_Persisted_Artifact()
    {
        var first = ExportFixture("testdata/firmament/examples/cone_pointed_top_zero.firmament");
        var second = ExportFixture("testdata/firmament/examples/cone_pointed_top_zero.firmament");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("pointed1", first.Value.ExportedFeatureId);
        Assert.Equal(0, first.Value.ExportedOpIndex);
        Assert.Equal("primitive", first.Value.ExportedBodyCategory);
        Assert.Equal("cone", first.Value.ExportedFeatureKind);
        Assert.NotEmpty(first.Value.StepText);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Contains("CONICAL_SURFACE", first.Value.StepText, StringComparison.Ordinal);

        var artifactPath = WriteExportArtifact("m10f2-pointed-cone.step", first.Value.StepText);
        Assert.True(File.Exists(artifactPath));
        Assert.Equal(first.Value.StepText, File.ReadAllText(artifactPath));
    }

    [Fact]
    public void Export_MultipleExecutedGeometryBodies_Selects_Last_Executed_Geometric_Feature_Body()
    {
        var compile = CompileFixture("testdata/firmament/fixtures/m10h1-valid-mixed-primitive-boolean-validation.firmament");
        Assert.True(compile.Compilation.IsSuccess);

        var export = FirmamentStepExporter.Export(compile.Compilation.Value);
        Assert.True(export.IsSuccess);
        Assert.Equal(FirmamentStepExporter.LastExecutedGeometricBodyPolicy, export.Value.ExportBodyPolicy);
        Assert.Equal(FirmamentStepExporter.LastExecutedGeometricBodySelectionReason, export.Value.ExportBodySelectionReason);
        Assert.Equal("cap", export.Value.ExportedFeatureId);
        Assert.Equal(2, export.Value.ExportedOpIndex);
        Assert.Equal("primitive", export.Value.ExportedBodyCategory);
        Assert.Equal("box", export.Value.ExportedFeatureKind);

        var expected = Step242Exporter.ExportBody(
            compile.Compilation.Value.PrimitiveExecutionResult!.ExecutedPrimitives.Single(p => p.FeatureId == "cap").Body);
        Assert.True(expected.IsSuccess);
        Assert.Equal(expected.Value, export.Value.StepText);
    }

    [Fact]
    public void Export_ValidationOps_Do_Not_Become_Export_Bodies_When_Final_Geometric_Body_Precedes_Validation()
    {
        const string source = """
firmament:
  version: 1

model:
  name: validation_after_geometry
  units: mm

ops[3]:
  -
    op: box
    id: base
    size[3]:
      2
      3
      4

  -
    op: box
    id: cap
    size[3]:
      5
      6
      7

  -
    op: expect_exists
    target: cap
""";

        var result = Export(source);

        Assert.True(result.IsSuccess);
        Assert.Equal("cap", result.Value.ExportedFeatureId);
        Assert.Equal(1, result.Value.ExportedOpIndex);
        Assert.Equal("primitive", result.Value.ExportedBodyCategory);
        Assert.Equal("box", result.Value.ExportedFeatureKind);
    }

    [Fact]
    public void Export_NoExecutedGeometricBody_Fails_Deterministically()
    {
        var first = ExportFixture("testdata/firmament/fixtures/m10a-no-export-body.firmament");
        var second = ExportFixture("testdata/firmament/fixtures/m10a-no-export-body.firmament");

        Assert.False(first.IsSuccess);
        Assert.False(second.IsSuccess);
        var firstDiagnostic = Assert.Single(first.Diagnostics);
        var secondDiagnostic = Assert.Single(second.Diagnostics);
        Assert.Equal(firstDiagnostic, secondDiagnostic);
        Assert.Contains("requires at least one executed primitive or boolean body", firstDiagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_ValidationFailure_Prevents_Export()
    {
        var result = ExportFixture("testdata/firmament/fixtures/m1a-invalid-sphere-missing-radius.firmament");

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Contains("[FIRM-STRUCT-0009]", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_SchemaPresence_Does_Not_Change_Step_Semantics_For_Equivalent_Geometry()
    {
        const string baseline = """
firmament:
  version: 1

model:
  name: schema_semantics_baseline
  units: mm

ops[1]:
  -
    op: box
    id: base
    size[3]:
      5
      6
      7
""";

        const string withSchema = """
firmament:
  version: 1

model:
  name: schema_semantics_with_schema
  units: mm

schema:
  process: additive
  printer_resolution: 0.1

ops[1]:
  -
    op: box
    id: base
    size[3]:
      5
      6
      7
""";

        var first = Export(baseline);
        var second = Export(withSchema);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.ExportedFeatureId, second.Value.ExportedFeatureId);
        Assert.Equal(first.Value.ExportedOpIndex, second.Value.ExportedOpIndex);
        Assert.Equal(first.Value.ExportedBodyCategory, second.Value.ExportedBodyCategory);
        Assert.Equal(first.Value.ExportedFeatureKind, second.Value.ExportedFeatureKind);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
    }

    private static Aetheris.Kernel.Core.Results.KernelResult<FirmamentStepExportResult> ExportFixture(string fixturePath) =>
        Export(FirmamentCorpusHarness.ReadFixtureText(fixturePath));

    private static Aetheris.Kernel.Core.Results.KernelResult<FirmamentStepExportResult> Export(string source)
    {
        var request = new FirmamentCompileRequest(new FirmamentSourceDocument(source));
        return FirmamentStepExporter.Export(request);
    }

    private static FirmamentCompileResult CompileFixture(string fixturePath) =>
        FirmamentCorpusHarness.Compile(FirmamentCorpusHarness.ReadFixtureText(fixturePath));

    private static string CreateBoxTorusSource(string op, string featureId, string targetField, double offsetX, double offsetY, double offsetZ, double majorRadius = 6d, double minorRadius = 2d) =>
        $"""
        firmament:
          version: 1
        
        model:
          name: m10n_box_torus_export_{featureId}
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

    private static string WriteExportArtifact(string fileName, string stepText)
    {
        var path = Path.Combine(
            FirmamentCorpusHarness.RepoRoot(),
            "testdata",
            "firmament",
            "exports",
            fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, stepText);
        return path;
    }
}
