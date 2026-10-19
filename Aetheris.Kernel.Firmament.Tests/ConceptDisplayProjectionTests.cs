using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Visualization;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ConceptDisplayProjectionTests
{
    [Fact]
    public void RealPartRetainsExactConceptGuidesWithoutAddingMaterial()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Regression/Visualization/concept-structs.firmament");
        var source = File.ReadAllText(path);
        var compiled = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
        var before = compiled.Value.StepText;
        var wires = ConceptDisplayProjection.Project(compiled.Value);
        var bounds = wires.Where(w => w.Kind == "Box3").ToArray();
        Assert.Equal(12, bounds.Length);
        var corners = bounds.SelectMany(w => w.Points.Chunk(3)).DistinctBy(p => (p[0], p[1], p[2])).ToArray();
        Assert.Equal(8, corners.Length);
        Assert.Equal(80, corners.Max(p => p[0]) - corners.Min(p => p[0]));
        Assert.Equal(50, corners.Max(p => p[1]) - corners.Min(p => p[1]));
        Assert.Equal(25, corners.Max(p => p[2]) - corners.Min(p => p[2]));
        Assert.Contains(wires, w => w.Kind == "Plane");
        Assert.Contains(wires, w => w.Kind == "Axis");
        Assert.Contains(wires, w => w.Kind == "Point");
        Assert.All(wires, w => { Assert.NotEmpty(w.Provenance); Assert.All(w.Points, n => Assert.True(double.IsFinite(n))); });
        Assert.Equal(before, compiled.Value.StepText);
        Assert.Equal(before, FirmamentBuildAndExport.CompileSource(source).Value.StepText);
    }

    [Fact]
    public void AssemblyPacketsRetainPrivateConceptsAndOccurrenceFrames()
    {
        var fixture = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Assembly/annular-pair.firmament");
        var source = File.ReadAllText(fixture).Replace("Template < R: Length > Struct Sleeve {", """
            Template < R: Length > Struct Sleeve {
                Concept Struct Design { Bounds: Box3 { size: [40mm, 40mm, 20mm] } }
            """, StringComparison.Ordinal);
        var compilation = new AssemblyM1Pipeline().Compile(source, fixture);
        Assert.True(compilation.IsSuccess, string.Join("\n", compilation.Diagnostics.Select(d => d.Message)));
        var mesh = AssemblyDisplayMeshExporter.Export(compilation);
        var retained = Assert.Single(compilation.Geometry!.DefinitionConcepts).Value;
        Assert.NotEmpty(retained);
        Assert.Equal(12, Assert.Single(mesh.Definitions).Concepts.Count(w => w.Kind == "Box3"));
        Assert.Equal(60, mesh.Occurrences.Single(o => o.Path == "AnnularPair.Second").Transform[12]);
        Assert.Equal(2, mesh.Occurrences.Count(o => o.DefinitionId == mesh.Definitions[0].Id));
        Assert.Equal(2, compilation.Geometry!.Artifact.Instances.Count);
    }
}
