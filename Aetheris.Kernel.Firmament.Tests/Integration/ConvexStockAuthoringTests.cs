using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests.Integration;

public sealed class ConvexStockAuthoringTests
{
    [Theory]
    [InlineData("Ellipsoid stock { radii: [18mm, 12mm, 25mm] }", "Ellipsoid stock { Radii: [18mm, 12mm, 25mm] }")]
    [InlineData("Polyhedron<12> stock { edgeLength: 20mm }", "Polyhedron<12> stock { EdgeLength: 20mm }")]
    public void CanonicalFieldsAndCompatibilitySpellingProduceIdenticalEngineeringGeometry(string preferred, string compatible)
    {
        var a = FirmamentBuildAndExport.CompileSource("Model Part { units: mm; " + preferred + " }");
        var b = FirmamentBuildAndExport.CompileSource("Model Part { Units: mm; " + compatible + " }");
        Assert.True(a.IsSuccess, string.Join("\n", a.Diagnostics)); Assert.True(b.IsSuccess, string.Join("\n", b.Diagnostics));
        Assert.Equal(a.Value.StepText, b.Value.StepText);
    }

    [Theory]
    [InlineData("Polyhedron<12> stock { edgeLength: dimension }", "Polyhedron<12> stock { edgeLength: 20mm }")]
    [InlineData("Ellipsoid stock { radii: [dimension, 12mm, 25mm] }", "Ellipsoid stock { radii: [20mm, 12mm, 25mm] }")]
    public void NewPrimitivesUseTheExistingCompileTimeValueBinder(string bound, string literal)
    {
        var a = FirmamentBuildAndExport.CompileSource("Model Part { units: mm; Static dimension: Length = 20mm; " + bound + " }");
        var b = FirmamentBuildAndExport.CompileSource("Model Part { units: mm; " + literal + " }");
        Assert.True(a.IsSuccess, string.Join("\n", a.Diagnostics)); Assert.True(b.IsSuccess, string.Join("\n", b.Diagnostics));
        Assert.Equal(a.Value.StepText, b.Value.StepText);
    }

    [Fact]
    public void RoundingKeepsItsFeatureIdentityAndUsesActualBrepDisplay()
    {
        const string source = "Model Die { units: mm; Box stock { size: [20mm, 20mm, 20mm] } Modify stock { EdgeFinish<Fillet> rounding { target: AllEdges; radius: 2mm } } }";
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
        Assert.Equal(26, built.Value.RuntimeBody!.Topology.Faces.Count());
        Assert.Equal("mesh", built.Value.Cir!.Qualification); Assert.Null(built.Value.Cir.RuntimeRoot);
        var feature = Assert.Single(built.Value.EngineeringFeatures!);
        Assert.Equal("edgefinish:rounding", feature.FeatureId); Assert.Equal("AllEdges", feature.Support); Assert.Equal(2, feature.Extent);
        Assert.NotNull(feature.SupportSpan);
        Assert.Equal(built.Value.StepText, FirmamentBuildAndExport.CompileSource(source).Value.StepText);
    }

    [Fact]
    public void PolyhedronFaceCountCanUseAnExistingTypedCompileTimeInteger()
    {
        var bound = FirmamentBuildAndExport.CompileSource("Model Die { units: mm; Static faces: Int = 12; Polyhedron<faces> stock { edgeLength: 20mm } }");
        var literal = FirmamentBuildAndExport.CompileSource("Model Die { units: mm; Polyhedron<12> stock { edgeLength: 20mm } }");
        Assert.True(bound.IsSuccess, string.Join("\n", bound.Diagnostics)); Assert.True(literal.IsSuccess);
        Assert.Equal(literal.Value.StepText, bound.Value.StepText);
    }

    [Theory]
    [InlineData("Ellipsoid stock { ra", "radii")]
    [InlineData("Polyhedron<12> stock { ed", "edgeLength")]
    public void CompletionUsesTheSharedPrimitiveSchema(string source, string expected)
    {
        var completion = FirmamentLanguageService.Complete(source, "memory", "1", source.Length);
        Assert.Contains(completion.Fields, f => FirmamentSourceSpelling.PreferredField(f.Name) == expected);
    }
}
