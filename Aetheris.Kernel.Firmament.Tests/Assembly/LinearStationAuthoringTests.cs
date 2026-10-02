using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class LinearStationAuthoringTests
{
    private static string Source => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(
        "fixtures/Canonical/AssemblyInterfaces/AuthoringFoundation/linear-stations.firmament"));

    private static AssemblyIr Compile(string source)
    {
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var compiled = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics));
        return compiled.Ir!;
    }

    [Fact]
    public void StationsNormalizeDirectionAndFollowThePlacedBoardsPublishedFrame()
    {
        var result = new AssemblyM1Pipeline().Compile(Source);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal(2, result.Geometry!.DefinitionBodies.Count);
        Assert.Equal(4, result.Geometry.InstanceBodies.Count);
        var collection = Assert.Single(result.Ir!.ConceptPoints!);
        Assert.Equal(new[] { "First", "Second", "Third" }, collection.Points.Select(p => p.Key));
        Assert.Equal(new[] { -10d, -23.5, 4 }, collection.Points.Select(p => p.Coordinates[1]));
        Assert.Contains("Stations", collection.Recipe);
        var first = result.Ir.Instances.Single(i => i.Path.ToString() == "StationWitness.Markers.First.Inlay");
        Assert.Equal(10, first.ResolvedTransform!.Matrix[12], 10);
        Assert.Equal(18, first.ResolvedTransform.Matrix[13], 10);
        Assert.Equal(20, first.ResolvedTransform.Matrix[14], 10);
        // A board placement change propagates via its port, not station coordinates.
        var moved = new AssemblyM1Pipeline().Compile(Source.Replace("20mm,30mm", "20mm,37mm"));
        Assert.True(moved.IsSuccess, string.Join("\n", moved.Diagnostics));
        foreach (var inlay in result.Ir.Instances.Where(i => i.Path.ToString().EndsWith(".Inlay", StringComparison.Ordinal)))
        {
            var next = moved.Ir!.Instances.Single(i => i.StableId == inlay.StableId);
            Assert.Equal(inlay.ResolvedTransform!.Matrix[14] + 7, next.ResolvedTransform!.Matrix[14], 10);
        }
        var step = AssemblyIrAp242Exporter.Export(result);
        Assert.True(step.IsSuccess, string.Join("\n", step.Diagnostics));
        var imported = Aetheris.Kernel.Core.Step242.Step242AssemblyImporter.Import(step.Value);
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics));
    }

    [Fact]
    public void ReorderingAndEditingStationsPreserveKeyedIdentitiesAndMoveOnlyEditedStation()
    {
        var before = Compile(Source);
        var after = Compile(Source.Replace("First: 10mm; Second: 23.5mm; Third: -4mm;", "Third: -4mm; Second: 24mm; First: 10mm;"));
        var points = Assert.Single(after.ConceptPoints!).Points;
        Assert.Equal(new[] { "Third", "Second", "First" }, points.Select(p => p.Key));
        Assert.Equal(new[] { 0, 1, 2 }, points.Select(p => p.Ordinal));
        foreach (var point in Assert.Single(before.ConceptPoints!).Points)
            Assert.Equal(point.StableId, points.Single(p => p.Key == point.Key).StableId);
        foreach (var occurrence in before.Instances)
        {
            var next = after.Instances.Single(i => i.StableId == occurrence.StableId);
            if (occurrence.Path.ToString() == "StationWitness.Markers.Second.Inlay")
                Assert.Equal(occurrence.ResolvedTransform!.Matrix[14] - .5, next.ResolvedTransform!.Matrix[14], 10);
            else Assert.Equal(occurrence.ResolvedTransform!.Matrix, next.ResolvedTransform!.Matrix);
        }
    }

    [Fact]
    public void Point3StationsAndLargeFiniteDirectionsUseTheSameBoundedRecipe()
    {
        var result = Compile(Source.Replace("Points<Point2>", "Points<Point3>").Replace("Point2(0mm,0mm)", "Point3(1mm,2mm,3mm)")
            .Replace("[0,-2]", "[0,0,-1e308]"));
        var points = Assert.Single(result.ConceptPoints!).Points;
        Assert.Equal(new[] { 1d, 2, -7 }, points[0].Coordinates);
        Assert.Equal(new[] { 1d, 2, 7 }, points[2].Coordinates);
    }

    [Fact]
    public void NestedScalarCallsInDirectionAndStationDistancesStayDimensionChecked()
    {
        var result = Compile(Source.Replace("[0,-2]", "[0,-Pow(2,3)]").Replace("First: 10mm", "First: 5mm * Pow(2,1)"));
        Assert.Equal(-10, Assert.Single(result.ConceptPoints!).Points[0].Coordinates[1]);
    }

    [Theory]
    [InlineData("[0,-2]", "[0,0]")]
    [InlineData("[0,-2]", "[0,-2mm]")]
    [InlineData("[0,-2]", "[0,-2,0]")]
    [InlineData("First: 10mm", "First: 10deg")]
    [InlineData("First: 10mm", "First: 10")]
    [InlineData("First: 10mm", "First: 10mm; First: 11mm")]
    [InlineData("First: 10mm", "First: 1e309mm")]
    [InlineData("Direction: [0,-2];", "Direction: [0,-2]; Step: [0mm,-2mm];")]
    [InlineData("Points<Point2> Seats {", "Points<Point2> Seats { Keys: [First, Second, Third];")]
    [InlineData("First: 10mm; Second: 23.5mm; Third: -4mm;", "")]
    public void InvalidStationRecipesFailClosed(string from, string to)
    {
        var parsed = new AssemblyM0Parser().Parse(Source.Replace(from, to));
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Message.Contains("firmament-concept-points-"));
    }

    [Fact]
    public void TooManyStationsAndCoordinateOverflowAreRejected()
    {
        var many = string.Join(";", Enumerable.Range(0, 1025).Select(i => $"Station{i}: {i}mm")) + ";";
        Assert.False(new AssemblyM0Parser().Parse(Source.Replace("First: 10mm; Second: 23.5mm; Third: -4mm;", many)).IsSuccess);
        Assert.False(new AssemblyM0Parser().Parse(Source.Replace("Point2(0mm,0mm)", "Point2(0mm,-1e308mm)")
            .Replace("First: 10mm", "First: 1e308mm")).IsSuccess);
    }
}
