using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyM1Tests
{
    [Fact]
    public void AuthoredSectionChainFile_MaterializesRuledPartInProductStep()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/ThreeDm/lamp-visible-intent.firmament");
        var compilation = new AssemblyM1Pipeline().CompileFile(path);
        Assert.True(compilation.IsSuccess, string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => item.Message)));

        Assert.Equal(7, compilation.Geometry!.InstanceBodies.Count);
        Assert.DoesNotContain(compilation.Geometry.InstanceBodies.Keys, name =>
            name.Contains("Cable", StringComparison.OrdinalIgnoreCase) || name.Contains("Bulb", StringComparison.OrdinalIgnoreCase));
        var shade = compilation.Geometry.InstanceBodies.Single(item => item.Key.EndsWith(".Shade", StringComparison.Ordinal)).Value;
        Assert.Contains(shade.Geometry.Surfaces, item => item.Value.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots);

        var step = AssemblyIrAp242Exporter.Export(compilation);
        Assert.True(step.IsSuccess, string.Join(Environment.NewLine, step.Diagnostics.Select(item => item.Message)));
        var imported = Step242AssemblyImporter.Import(step.Value);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(item => item.Message)));
        Assert.Equal(7, imported.Value.Occurrences.Count);
    }

    [Fact]
    public void ModelTargetTemplates_MaterializeAnalyticPrimitiveDefinitionsAndExportProductOccurrences()
    {
        const string source = """
            Template < R: Length > Model Ball { Units: mm Sphere Body { Radius: R } }
            Template < R: Length > Model Stem { Units: mm Cylinder Body { Radius: R; Height: 20mm } }
            Assembly Pair {
              <Assembly Pair>
                <Part Knob = Ball<R: 5mm>> Placement LegacyExplicit = [1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]; </Part>
                <Part Shaft = Stem<R: 2mm>> Placement LegacyExplicit = [1,0,0,0,0,1,0,0,0,0,1,0,0,0,-25,1]; </Part>
              </Assembly>
              Anchor: Pair;
            }
            """;
        var path = Path.Combine(Path.GetTempPath(), "aetheris-model-template-assembly-" + Guid.NewGuid().ToString("N") + ".firmament");
        try
        {
            File.WriteAllText(path, source);
            var compilation = new AssemblyM1Pipeline().CompileFile(path);
            Assert.True(compilation.IsSuccess, string.Join(Environment.NewLine, compilation.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
            Assert.Equal(2, compilation.Geometry!.DefinitionBodies.Count);
            Assert.Contains(compilation.Geometry.DefinitionBodies.Values, body => body.Geometry.Surfaces.Any(surface => surface.Value.Kind == SurfaceGeometryKind.Sphere));
            Assert.Contains(compilation.Geometry.DefinitionBodies.Values, body => body.Geometry.Surfaces.Any(surface => surface.Value.Kind == SurfaceGeometryKind.Cylinder));

            var exported = AssemblyIrAp242Exporter.Export(compilation);
            Assert.True(exported.IsSuccess, string.Join(Environment.NewLine, exported.Diagnostics.Select(item => item.Message)));
            var product = Step242AssemblyImporter.Import(exported.Value);
            Assert.True(product.IsSuccess, string.Join(Environment.NewLine, product.Diagnostics.Select(item => item.Message)));
            Assert.Equal(2, product.Value.Occurrences.Count);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void TemplateRecordParts_ExecuteAsReusedExactWorldGeometryWithResidualValidation()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Assembly/template-block-pair.firmament");
        var first = new AssemblyM1Pipeline().CompileFile(path);
        var second = new AssemblyM1Pipeline().CompileFile(path);

        Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}")));
        Assert.Equal("aetheris/assembly-ir/m1", first.Ir!.Schema);
        Assert.Equal(2, first.Geometry!.Artifact.Definitions.Count);
        Assert.Equal(2, first.Geometry.Artifact.Instances.Count);
        Assert.All(first.Geometry.Artifact.Definitions, definition => Assert.Contains(definition.Provenance, item => item.Stage == "static-record"));
        Assert.All(first.Geometry.Artifact.MateResiduals, residual => Assert.True(residual.Passed));
        Assert.All(first.Ir.PlacementConstraints, constraint => Assert.Equal("geometry-validated", constraint.Status));
        Assert.Equal(first.Geometry.Artifact.DeterministicSha256, second.Geometry!.Artifact.DeterministicSha256);

        var moving = first.Ir.Instances.Single(instance => instance.Path.ToString() == "TemplateBlockPair.Moving");
        var axis = moving.SemanticRoot.ExposedMembers["Interface"].ExposedMembers["Axis"];
        var world = Assert.IsType<ExactAxisBinding>(AssemblyWorldQuery.Resolve(first.Ir, axis.StableIdentity));
        Assert.Equal(0, world.OriginX, 8);
        Assert.Equal(0, world.OriginY, 8);
        Assert.Equal(14, world.OriginZ, 8); // local center 4mm, with Moving.Base seated on Fixed.Seat at Z=10mm

        var automatic = Assert.Single(first.Ir.DimensionalRelations, relation => relation.Provenance == "Interface:SeatedAxis.Fit");
        Assert.Equal(2, automatic.Nominal, 8);
        Assert.Equal("mate:TemplateBlockPair:Seat", automatic.MateStableId);
        Assert.Equal("interface:SeatedAxis", automatic.InterfaceStableId);
        Assert.Contains(automatic.SourceProvenance!, item => item.Stage == "static-record" && item.Identity == "MovingSpec");
        Assert.Contains(automatic.SourceProvenance!, item => item.Stage == "template-specialization");
        var stackup = Assert.Single(first.Ir.ToleranceStackups);
        Assert.True(stackup.Passed);
        Assert.Contains(Assert.Single(stackup.Contributions).SourceProvenance!, item => item.Stage == "static-record" && item.Identity == "FixedSpec");
    }

    [Fact]
    public void PositiveVolumeOccurrenceOverlap_IsFatalAfterExactMaterialization()
    {
        var canonicalPath = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Assembly/template-block-pair.firmament");
        var source = File.ReadAllText(canonicalPath).Replace(
            "Lower PlaneCoincident Moving.Base Fixed.Seat;",
            "Lower PlaneCoincident Moving.Seat Fixed.Seat;",
            StringComparison.Ordinal);
        var temporaryPath = Path.Combine(Path.GetTempPath(), "aetheris-overlap-" + Guid.NewGuid().ToString("N") + ".firmament");
        try
        {
            File.WriteAllText(temporaryPath, source);
            var result = new AssemblyM1Pipeline().CompileFile(temporaryPath);

            Assert.False(result.IsSuccess);
            var diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == "assembly-solid-volume-interference");
            Assert.Equal(AssemblyDiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Contains("TemplateBlockPair.Fixed", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("TemplateBlockPair.Moving", diagnostic.Message, StringComparison.Ordinal);
            Assert.Contains("positive-volume overlap is not", diagnostic.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
