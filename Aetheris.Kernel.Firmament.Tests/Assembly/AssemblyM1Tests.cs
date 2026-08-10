using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyM1Tests
{
    [Fact]
    public void TemplateRecordParts_ExecuteAsReusedExactWorldGeometryWithResidualValidation()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/AssemblyM1/template-block-pair.firmament");
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
        Assert.Equal(2.5, world.OriginZ, 8); // local box center 7.5mm, Mate-derived seating transform -5mm

        var automatic = Assert.Single(first.Ir.DimensionalRelations, relation => relation.Provenance == "Interface:SeatedAxis.Fit");
        Assert.Equal(-5, automatic.Nominal, 8);
        Assert.Equal("mate:TemplateBlockPair:Seat", automatic.MateStableId);
        Assert.Equal("interface:SeatedAxis", automatic.InterfaceStableId);
        Assert.Contains(automatic.SourceProvenance!, item => item.Stage == "static-record" && item.Identity == "MovingSpec");
        Assert.Contains(automatic.SourceProvenance!, item => item.Stage == "template-specialization");
        var stackup = Assert.Single(first.Ir.ToleranceStackups);
        Assert.True(stackup.Passed);
        Assert.Contains(Assert.Single(stackup.Contributions).SourceProvenance!, item => item.Stage == "static-record" && item.Identity == "FixedSpec");
    }
}
