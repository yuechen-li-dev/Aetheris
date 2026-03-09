using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242HoleSemanticsTests
{
    [Fact]
    public void ImportBody_PlanarFaceWithHole_ImportsAndPreservesMultipleLoops()
    {
        var import = Step242Importer.ImportBody(Step242FixtureCorpus.PlanarFaceWithRectangularHole);

        Assert.True(import.IsSuccess);
        var body = import.Value;

        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);

        var face = Assert.Single(body.Topology.Faces);
        Assert.True(face.LoopIds.Count >= 2);

        var tessellationException = Record.Exception(() => BrepDisplayTessellator.Tessellate(body));
        Assert.Null(tessellationException);
    }

    [Fact]
    public void ImportBody_MultiLoopPlanarEqualAreas_ReturnsAmbiguousOuterValidationFailure()
    {
        var import = Step242Importer.ImportBody(Step242FixtureCorpus.PlanarFaceEqualAreaLoops);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.ValidationFailed, diagnostic.Code);
        Assert.Equal("Importer.LoopRole.AmbiguousOuter", diagnostic.Source);
    }

    [Fact]
    public void ImportBody_MultiLoopPlanarInnerOutside_ReturnsInnerDisjointAfterNormalizationValidationFailure()
    {
        var import = Step242Importer.ImportBody(Step242FixtureCorpus.PlanarFaceInnerOutsideOuter);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.ValidationFailed, diagnostic.Code);
        Assert.Equal("Importer.LoopRole.InnerDisjointAfterNormalization", diagnostic.Source);
        Assert.StartsWith("Inner loop could not be normalized: disjoint.", diagnostic.Message, StringComparison.Ordinal);
    }


    [Fact]
    public void ImportBody_ManyPlanarSingleEdgeCircularHoles_ClassifiesDeterministically()
    {
        var first = Step242Importer.ImportBody(Step242FixtureCorpus.PlanarFaceManyCircularSingleEdgeHolesScrambledBounds);
        var second = Step242Importer.ImportBody(Step242FixtureCorpus.PlanarFaceManyCircularSingleEdgeHolesScrambledBounds);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);

        var firstFace = Assert.Single(first.Value.Topology.Faces);
        var secondFace = Assert.Single(second.Value.Topology.Faces);

        Assert.Equal(4, firstFace.LoopIds.Count);
        Assert.Equal(firstFace.LoopIds.Select(l => l.Value), secondFace.LoopIds.Select(l => l.Value));

        Assert.Equal(4, firstFace.LoopIds[0].Value);
        Assert.All(firstFace.LoopIds.Skip(1), loopId => Assert.True(loopId.Value is >= 1 and <= 3));
    }

    [Fact]
    public void ImportBody_MultiLoopSphericalFace_ReturnsUnsupportedSurfaceForHolesValidationFailure()
    {
        var import = Step242Importer.ImportBody(Step242FixtureCorpus.SphericalFaceWithTwoLoops);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.ValidationFailed, diagnostic.Code);
        Assert.Equal("Importer.LoopRole.UnsupportedSurfaceForHoles.Sphere", diagnostic.Source);
    }

    [Fact]
    public void ImportBody_MultiLoopCylindricalFace_ReturnsDeterministicCylinderMappingFailure()
    {
        var import = Step242Importer.ImportBody(Step242FixtureCorpus.CylindricalFaceWithTwoLoops);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.ValidationFailed, diagnostic.Code);
        Assert.Equal("Topology.GraphValidator", diagnostic.Source);
        Assert.StartsWith("Loop ", diagnostic.Message, StringComparison.Ordinal);
    }
}
