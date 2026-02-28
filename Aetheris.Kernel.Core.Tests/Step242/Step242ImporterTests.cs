using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242ImporterTests
{
    [Fact]
    public void ImportBody_KnownGoodM22SubsetText_ReturnsValidatedBody()
    {
        var fixtureText = Step242FixtureCorpus.CanonicalBoxGolden;

        var import = Step242Importer.ImportBody(fixtureText);

        Assert.True(import.IsSuccess);
        var validation = BrepBindingValidator.Validate(import.Value, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);
        Assert.NotEmpty(tessellation.Value.FacePatches);
    }

    [Fact]
    public void ExportImportRoundTrip_BoxSubset_PreservesBasicTopologyInvariants()
    {
        var boxResult = BrepPrimitives.CreateBox(4d, 6d, 8d);
        Assert.True(boxResult.IsSuccess);

        var export = Step242Exporter.ExportBody(boxResult.Value);
        Assert.True(export.IsSuccess);

        var import = Step242Importer.ImportBody(export.Value);

        Assert.True(import.IsSuccess);
        Assert.Equal(boxResult.Value.Topology.Vertices.Count(), import.Value.Topology.Vertices.Count());
        Assert.Equal(boxResult.Value.Topology.Edges.Count(), import.Value.Topology.Edges.Count());
        Assert.Equal(boxResult.Value.Topology.Faces.Count(), import.Value.Topology.Faces.Count());

        var tessellation = BrepDisplayTessellator.Tessellate(import.Value);
        Assert.True(tessellation.IsSuccess);
    }

    [Fact]
    public void ImportBody_MalformedStep_ReturnsDiagnosticWithoutThrowing()
    {
        var malformed = Step242FixtureCorpus.MalformedMissingParen;

        var import = Step242Importer.ImportBody(malformed);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.InvalidArgument, diagnostic.Code);
        Assert.Equal(KernelDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Parser", diagnostic.Source);
    }

    [Fact]
    public void ImportBody_UnsupportedEntityInParseableText_ReturnsNotImplementedDiagnostic()
    {
        var unsupported = Step242FixtureCorpus.UnsupportedSphericalSurface;

        var import = Step242Importer.ImportBody(unsupported);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal(KernelDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Entity:1", diagnostic.Source);
        Assert.Contains("unsupported", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

}
