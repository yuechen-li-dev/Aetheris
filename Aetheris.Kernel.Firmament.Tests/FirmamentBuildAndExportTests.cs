using System.Text;
using System.Linq;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentBuildAndExportTests
{
    [Fact]
    public void Run_BoxBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/box_basic.firmament",
            "box_basic.step",
            "base",
            0,
            "primitive",
            "box");
    }

    [Fact]
    public void Run_CylinderBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/cylinder_basic.firmament",
            "cylinder_basic.step",
            "post",
            0,
            "primitive",
            "cylinder");
    }


    [Fact]
    public void Run_SphereBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/sphere_basic.firmament",
            "sphere_basic.step",
            "ball",
            0,
            "primitive",
            "sphere");
    }

    [Fact]
    public void Run_TorusBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/torus_basic.firmament",
            "torus_basic.step",
            "donut1",
            0,
            "primitive",
            "torus");
    }

    [Fact]
    public void Run_ConeFrustumBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/cone_frustum_basic.firmament",
            "cone_frustum_basic.step",
            "frustum1",
            0,
            "primitive",
            "cone");
    }

    [Fact]
    public void Run_ConePointedTopZeroExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/cone_pointed_top_zero.firmament",
            "cone_pointed_top_zero.step",
            "pointed1",
            0,
            "primitive",
            "cone");
    }

    [Fact]
    public void Run_BoxAddBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/box_add_basic.firmament",
            "box_add_basic.step",
            "joined",
            1,
            "boolean",
            "add");
    }

    [Fact]
    public void Run_BooleanAddBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_add_basic.firmament",
            "boolean_add_basic.step",
            "joined",
            2,
            "boolean",
            "add");
    }

    [Fact]
    public void Run_BooleanSubtractBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_subtract_basic.firmament",
            "boolean_subtract_basic.step",
            "carved",
            2,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_BooleanIntersectBasicExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_intersect_basic.firmament",
            "boolean_intersect_basic.step",
            "overlap",
            2,
            "boolean",
            "intersect");
    }

    [Fact]
    public void Run_BooleanBoxCylinderHoleExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_box_cylinder_hole.firmament",
            "boolean_box_cylinder_hole.step",
            "hole",
            1,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_BooleanBoxConeThroughHoleExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_box_cone_throughhole_basic.firmament",
            "boolean_box_cone_throughhole_basic.step",
            "cut",
            1,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_BooleanTwoCylinderHolesExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_two_cylinder_holes_basic.firmament",
            "boolean_two_cylinder_holes_basic.step",
            "hole_b",
            2,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_BooleanCylinderConeHolesExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_cylinder_cone_holes_basic.firmament",
            "boolean_cylinder_cone_holes_basic.step",
            "cut_b",
            2,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_BooleanBoxSphereCavityExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/boolean_box_sphere_cavity_basic.firmament",
            "boolean_box_sphere_cavity_basic.step",
            "cavity",
            1,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_RibbedSupportF1Example_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/ribbed_support_f1.firmament",
            "ribbed_support_f1.step",
            "wall",
            1,
            "boolean",
            "add");
    }

    [Fact]
    public void Run_F2FlangeCenterBoreExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/f2_flange_center_bore.firmament",
            "f2_flange_center_bore.step",
            "center_bore",
            1,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_P1BlindHoleOnFaceSemanticExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/p1_blind_hole_on_face_semantic.firmament",
            "p1_blind_hole_on_face_semantic.step",
            "blind_hole_tool",
            1,
            "primitive",
            "cylinder");
    }

    [Fact]
    public void Run_P1FlangeRadialHoleSemanticExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/p1_flange_radial_hole_semantic.firmament",
            "p1_flange_radial_hole_semantic.step",
            "radial_hole_tool",
            1,
            "primitive",
            "cylinder");
    }

    [Fact]
    public void Run_P2LinearHoleRowExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/p2_linear_hole_row.firmament",
            "p2_linear_hole_row.step",
            "hole_cut_1__lin3",
            4,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_P2FlangeBoltCirclePatternExample_Writes_Default_Export_Artifact()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/examples/p2_flange_bolt_circle_pattern.firmament",
            "p2_flange_bolt_circle_pattern.step",
            "bolt_hole_1__cir5",
            7,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_FrictionLabBlindHoleMountBlock_NowBuildsAndExports()
    {
        var fullSourcePath = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "Aetheris.Firmament.FrictionLab/Cases/blind-hole-mount-block/part.firmament");

        var first = FirmamentBuildAndExport.Run(fullSourcePath);
        var second = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEmpty(first.Value.Export.StepText);
        Assert.Equal(first.Value.Export.StepText, second.Value.Export.StepText);
        Assert.Contains("MANIFOLD_SOLID_BREP", first.Value.Export.StepText, StringComparison.Ordinal);
        Assert.Contains("CYLINDRICAL_SURFACE", first.Value.Export.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_FrictionLabBlindHoleMountBlock_ExportedStep_Has_TopFace_Opening_Not_ContainedVoidShell()
    {
        var fullSourcePath = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "Aetheris.Firmament.FrictionLab/Cases/blind-hole-mount-block/part.firmament");
        var result = FirmamentBuildAndExport.Run(fullSourcePath);
        Assert.True(result.IsSuccess);

        var import = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(result.Value.Export.StepText);
        Assert.True(import.IsSuccess);

        var body = import.Value;
        Assert.DoesNotContain("BREP_WITH_VOIDS", result.Value.Export.StepText, StringComparison.Ordinal);

        var topFace = body.Topology.Faces
            .Select(face =>
            {
                body.TryGetFaceSurfaceGeometry(face.Id, out var surface);
                return (face, surface);
            })
            .Where(entry => entry.surface?.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Plane)
            .Where(entry => entry.surface!.Plane!.Value.Normal.ToVector().Z > 0.5d)
            .OrderByDescending(entry => entry.surface!.Plane!.Value.Origin.Z)
            .First();

        Assert.Equal(2, topFace.face.LoopIds.Count);
    }

    [Fact]
    public void Run_FrictionLabCounterboreHole_NowBuildsAndExports()
    {
        var fullSourcePath = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "Aetheris.Firmament.FrictionLab/Cases/counterbore-hole/part.firmament");

        var first = FirmamentBuildAndExport.Run(fullSourcePath);
        var second = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEmpty(first.Value.Export.StepText);
        Assert.Equal(first.Value.Export.StepText, second.Value.Export.StepText);
        Assert.Contains("MANIFOLD_SOLID_BREP", first.Value.Export.StepText, StringComparison.Ordinal);
        Assert.Contains("CYLINDRICAL_SURFACE", first.Value.Export.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void Semantic_Placement_Build_Is_Deterministic()
    {
        var fullSourcePath = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmament/examples/p1_flange_radial_hole_semantic.firmament");
        var first = FirmamentBuildAndExport.Run(fullSourcePath);
        var second = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Export.StepText, second.Value.Export.StepText);
    }

    [Fact]
    public void P2_CircularPattern_Build_Is_Deterministic()
    {
        var fullSourcePath = FirmamentCorpusHarness.ResolveFixtureFullPath("testdata/firmament/examples/p2_flange_bolt_circle_pattern.firmament");
        var first = FirmamentBuildAndExport.Run(fullSourcePath);
        var second = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Export.StepText, second.Value.Export.StepText);
    }

    [Fact]
    public void Run_ContainedBoxWithCylinderHoleFixture_Builds_And_Writes_Export()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/fixtures/m10h1-unsupported-box-with-cylinder-hole.firmament",
            "m10h1-unsupported-box-with-cylinder-hole.step",
            "hole",
            1,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_AddThenSubtract_ReentrySafeRootFixture_Builds_And_Writes_Export()
    {
        AssertExampleBuildAndExport(
            "testdata/firmament/fixtures/m13b-invalid-composed-reenter-safe-family.firmament",
            "m13b-invalid-composed-reenter-safe-family.step",
            "hole",
            2,
            "boolean",
            "subtract");
    }

    [Fact]
    public void Run_FrictionLabMountingBracketBasic_Builds_And_Exports_After_G8_IndependentHoleSupport()
    {
        var fullSourcePath = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "Aetheris.Firmament.FrictionLab/Cases/mounting-bracket-basic/part.firmament");
        var result = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("upright_hole", result.Value.Export.ExportedFeatureId);
        Assert.Equal("boolean", result.Value.Export.ExportedBodyCategory);
        Assert.Equal("subtract", result.Value.Export.ExportedFeatureKind);
        Assert.Contains("MANIFOLD_SOLID_BREP", result.Value.Export.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_FrictionLabSimpleBracket_NowBuildsAndExports()
    {
        var fullSourcePath = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "Aetheris.Firmament.FrictionLab/Cases/simple-bracket/part.firmament");
        var result = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("bracket", result.Value.Export.ExportedFeatureId);
        Assert.Equal("boolean", result.Value.Export.ExportedBodyCategory);
        Assert.Equal("add", result.Value.Export.ExportedFeatureKind);
        Assert.Contains("MANIFOLD_SOLID_BREP", result.Value.Export.StepText, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_FrictionLabSimpleBracket_ExportedStep_Matches_FL2i_Canonical_LShape_Constraints()
    {
        var fullSourcePath = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "Aetheris.Firmament.FrictionLab/Cases/simple-bracket/part.firmament");
        var build = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.True(build.IsSuccess, string.Join(Environment.NewLine, build.Diagnostics.Select(d => d.Message)));
        Assert.Equal("bracket", build.Value.Export.ExportedFeatureId);
        Assert.Equal("boolean", build.Value.Export.ExportedBodyCategory);
        Assert.Equal("add", build.Value.Export.ExportedFeatureKind);

        Assert.DoesNotContain("TRIANGULATED_FACE_SET", build.Value.Export.StepText, StringComparison.Ordinal);
        Assert.True(CountOccurrences(build.Value.Export.StepText, "ADVANCED_FACE") > 6);

        var sourceText = File.ReadAllText(fullSourcePath, Encoding.UTF8);
        Assert.Contains("ops[2]", sourceText, StringComparison.Ordinal);
        Assert.Contains(@"size[3]:
      60
      20
      10", sourceText, StringComparison.Ordinal);
        Assert.Contains(@"size[3]:
        10
        20
        60", sourceText, StringComparison.Ordinal);
        Assert.Contains(@"offset[3]:
        30
        10
        0", sourceText, StringComparison.Ordinal);
        Assert.Contains(@"offset[3]:
        5
        10
        0", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("op: subtract", sourceText, StringComparison.Ordinal);

        var pointPattern = new System.Text.RegularExpressions.Regex(
            @"CARTESIAN_POINT\([^\)]*\(([-0-9.E+]+),([-0-9.E+]+),([-0-9.E+]+)\)\)",
            System.Text.RegularExpressions.RegexOptions.Compiled);
        var points = pointPattern.Matches(build.Value.Export.StepText)
            .Select(match => (
                X: double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                Y: double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                Z: double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture)))
            .ToArray();
        Assert.NotEmpty(points);

        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);
        var minZ = points.Min(p => p.Z);
        var maxZ = points.Max(p => p.Z);

        Assert.Equal(60d, maxX - minX, 6);
        Assert.Equal(20d, maxY - minY, 6);
        Assert.Equal(60d, maxZ - minZ, 6);
        Assert.Equal(0d, minX, 6);
        Assert.Equal(60d, maxX, 6);
        Assert.Equal(0d, minY, 6);
        Assert.Equal(20d, maxY, 6);
        Assert.Equal(0d, minZ, 6);
        Assert.Equal(60d, maxZ, 6);

        Assert.DoesNotContain("B_SPLINE", build.Value.Export.StepText, StringComparison.Ordinal);
        Assert.DoesNotContain("TOROIDAL_SURFACE", build.Value.Export.StepText, StringComparison.Ordinal);
        Assert.DoesNotContain("CONICAL_SURFACE", build.Value.Export.StepText, StringComparison.Ordinal);
        Assert.DoesNotContain("SPHERICAL_SURFACE", build.Value.Export.StepText, StringComparison.Ordinal);
    }


    [Fact]
    public void Run_FrictionLabMountingBracketBasic_ContinuedRoot_SubtractPlacement_Uses_World_Frame()
    {
        var sourceText = File.ReadAllText(
            Path.Combine(FirmamentCorpusHarness.RepoRoot(), "Aetheris.Firmament.FrictionLab/Cases/mounting-bracket-basic/part.firmament"),
            Encoding.UTF8);
        var compiler = new FirmamentCompiler();
        var compilation = compiler.Compile(new FirmamentCompileRequest(new FirmamentSourceDocument(sourceText))).Compilation;

        Assert.True(compilation.IsSuccess, string.Join(Environment.NewLine, compilation.Diagnostics.Select(d => d.Message)));
        var finalBody = Assert.Single(compilation.Value.PrimitiveExecutionResult!.ExecutedBooleans, b => b.FeatureId == "upright_hole").Body;
        Assert.NotNull(finalBody.SafeBooleanComposition);
        var composition = finalBody.SafeBooleanComposition!;
        Assert.Equal(2, composition.Holes.Count);

        Assert.Contains(composition.Holes, hole => Math.Abs(hole.CenterX + 15d) < 1e-6d && Math.Abs(hole.CenterY) < 1e-6d);
        Assert.Contains(composition.Holes, hole => Math.Abs(hole.CenterX - 26d) < 1e-6d && Math.Abs(hole.CenterY) < 1e-6d);
    }

    [Fact]
    public void Run_FrictionLabMountingBracketBasic_ExportedStep_Holes_Are_At_Intended_X_Positions()
    {
        var fullSourcePath = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "Aetheris.Firmament.FrictionLab/Cases/mounting-bracket-basic/part.firmament");
        var result = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        var import = Aetheris.Kernel.Core.Step242.Step242Importer.ImportBody(result.Value.Export.StepText);
        Assert.True(import.IsSuccess, string.Join(Environment.NewLine, import.Diagnostics.Select(d => d.Message)));

        var cylinderOriginsX = import.Value.Topology.Faces
            .Select(face =>
            {
                import.Value.TryGetFaceSurfaceGeometry(face.Id, out var surface);
                return surface;
            })
            .Where(surface => surface?.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cylinder)
            .Select(surface => surface!.Cylinder!.Value.Origin.X)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        Assert.Contains(cylinderOriginsX, x => Math.Abs(x + 15d) < 1e-6d);
        Assert.Contains(cylinderOriginsX, x => Math.Abs(x - 26d) < 1e-6d);
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m10j-unsupported-box-add-cylinder.firmament", "m10j-unsupported-box-add-cylinder.step", "joined", "add")]
    [InlineData("testdata/firmament/fixtures/m10j-unsupported-box-intersect-cylinder.firmament", "m10j-unsupported-box-intersect-cylinder.step", "overlap", "intersect")]
    [InlineData("testdata/firmament/fixtures/m10l-unsupported-box-add-sphere.firmament", "m10l-unsupported-box-add-sphere.step", "joined", "add")]
    [InlineData("testdata/firmament/fixtures/m10l-unsupported-box-intersect-sphere.firmament", "m10l-unsupported-box-intersect-sphere.step", "overlap", "intersect")]
    [InlineData("testdata/firmament/fixtures/m10m-unsupported-box-subtract-cone.firmament", "m10m-unsupported-box-subtract-cone.step", "tapered_cut", "subtract")]
    [InlineData("testdata/firmament/fixtures/m10m-unsupported-box-add-cone.firmament", "m10m-unsupported-box-add-cone.step", "joined", "add")]
    [InlineData("testdata/firmament/fixtures/m10m-unsupported-box-intersect-cone.firmament", "m10m-unsupported-box-intersect-cone.step", "overlap", "intersect")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-overlapping-composed-holes.firmament", "m13a-unsupported-overlapping-composed-holes.step", "hole_b", "subtract")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-tangent-composed-holes.firmament", "m13a-unsupported-tangent-composed-holes.step", "hole_b", "subtract")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-add-ordering.firmament", "m13a-unsupported-composed-add-ordering.step", "joined", "add")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-sphere.firmament", "m13a-unsupported-composed-subtract-sphere.step", "cavity", "subtract")]
    [InlineData("testdata/firmament/fixtures/m13a-unsupported-composed-subtract-box.firmament", "m13a-unsupported-composed-subtract-box.step", "notch", "subtract")]
    [InlineData("testdata/firmament/fixtures/m13c-unsupported-cylinder-root-follow-on-hole.firmament", "m13c-unsupported-cylinder-root-follow-on-hole.step", "bolt_hole", "subtract")]
    public void Run_Unsupported_MixedPrimitive_Fixtures_Fail_And_Do_Not_Write_Fallback_Export(
        string sourcePath,
        string expectedFileName,
        string expectedFeatureId,
        string expectedKind)
    {
        AssertUnsupportedBuildAndExport(sourcePath, expectedFileName, expectedFeatureId, expectedKind);
    }

    [Fact]
    public void Run_FrictionLabShaftKeyway_RemainsExplicitlyUnsupported()
    {
        var fullSourcePath = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "Aetheris.Firmament.FrictionLab/Cases/shaft-keyway/part.firmament");
        var result = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Message.Contains("Requested boolean feature 'keyway' (subtract) could not be executed.", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Source == "BrepBoolean.RebuildResult"
            && diagnostic.Message.Contains("bounded keyway family excludes centerline-spanning trenches", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_FrictionLabPocketedPlate_NowFailsWithExplicitEnclosedPocketDiagnostic()
    {
        var fullSourcePath = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "Aetheris.Firmament.FrictionLab/Cases/pocketed-plate/part.firmament");
        var result = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Source == "BrepBoolean.RebuildResult"
            && diagnostic.Message.Contains("requires the subtract box to open to exactly one exterior root face", StringComparison.Ordinal));
    }

    private static void AssertExampleBuildAndExport(string sourcePath, string expectedFileName, string expectedFeatureId, int expectedOpIndex, string expectedBodyCategory, string expectedFeatureKind)
    {
        var fullSourcePath = FirmamentCorpusHarness.ResolveFixtureFullPath(sourcePath);
        var expectedOutputPath = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "testdata", "firmament", "exports", expectedFileName);
        if (File.Exists(expectedOutputPath))
        {
            File.Delete(expectedOutputPath);
        }

        var result = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(fullSourcePath, result.Value.SourcePath);
        Assert.Equal(expectedOutputPath, result.Value.OutputPath);
        Assert.Equal(expectedFeatureId, result.Value.Export.ExportedFeatureId);
        Assert.Equal(expectedOpIndex, result.Value.Export.ExportedOpIndex);
        Assert.Equal(expectedBodyCategory, result.Value.Export.ExportedBodyCategory);
        Assert.Equal(expectedFeatureKind, result.Value.Export.ExportedFeatureKind);
        Assert.True(File.Exists(expectedOutputPath));
        Assert.Equal(result.Value.Export.StepText, File.ReadAllText(expectedOutputPath, Encoding.UTF8));
    }

    private static void AssertUnsupportedBuildAndExport(string sourcePath, string expectedFileName, string expectedFeatureId, string expectedKind)
    {
        var fullSourcePath = FirmamentCorpusHarness.ResolveFixtureFullPath(sourcePath);
        var expectedOutputPath = Path.Combine(FirmamentCorpusHarness.RepoRoot(), "testdata", "firmament", "exports", expectedFileName);
        if (File.Exists(expectedOutputPath))
        {
            File.Delete(expectedOutputPath);
        }

        var result = FirmamentBuildAndExport.Run(fullSourcePath);

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(expectedOutputPath));
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains($"Requested boolean feature '{expectedFeatureId}' ({expectedKind}) could not be executed.", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, diagnostic => HasExpectedMixedPrimitiveFailure(diagnostic.Message));
    }

    private static bool HasExpectedMixedPrimitiveFailure(string message)
        => message.Contains("M13 only supports recognized axis-aligned boxes from BrepPrimitives.CreateBox(...).", StringComparison.Ordinal)
           || message.Contains("sequential safe composition only supports subtracting supported cylinder/cone analytic holes", StringComparison.Ordinal)
           || message.Contains("safe subtract", StringComparison.Ordinal)
           || message.Contains("unsupported follow-on tool kind", StringComparison.Ordinal)
           || message.Contains("Boolean feature", StringComparison.Ordinal)
           || message.Contains("analytic hole surface kind", StringComparison.Ordinal)
           || message.Contains("fully enclosed spherical cavity", StringComparison.Ordinal);

    private static int CountOccurrences(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }
}
