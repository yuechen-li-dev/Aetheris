using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class ConceptPointAuthoringTests
{
    private static string Source => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(
        "fixtures/Canonical/AssemblyInterfaces/AuthoringFoundation/concept-points.firmament"));

    [Fact]
    public void CheckedLocalPointsExpandThroughOrdinaryAssemblyAndExport()
    {
        var result = new AssemblyM1Pipeline().Compile(Source);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal(2, result.Geometry!.DefinitionBodies.Count);
        Assert.Equal(13, result.Geometry.InstanceBodies.Count);
        var collections = result.Ir!.ConceptPoints!;
        Assert.Equal(3, collections.Count);
        var crossings = collections.Single(c => c.Name == "Nut.Crossings");
        Assert.Equal("Point2", crossings.ElementType);
        Assert.Equal(new[] { "Bass", "Middle", "Treble" }, crossings.Points.Select(p => p.Key));
        Assert.All(crossings.Points, p => Assert.Equal(2, p.Coordinates.Count));
        Assert.Contains("Linear", crossings.Recipe);
        var twelfth = collections.Single(c => c.Name == "Board.Frets").Points.Single(p => p.Key == "Fret12");
        Assert.Equal(-314, twelfth.Coordinates[1], 10);
        var fret = result.Ir.Instances.Single(i => i.Path.ToString() == "PointWitness.Frets.Fret12.Fret");
        Assert.Equal(344, fret.ResolvedTransform!.Matrix[13], 10);
        Assert.Equal(62, fret.ResolvedTransform.Matrix[14], 10);
        var bass = result.Ir.Instances.Single(i => i.Path.ToString() == "PointWitness.Bass");
        Assert.Equal(-18, bass.ResolvedTransform!.Matrix[12], 10);
        Assert.Equal(2, bass.ResolvedTransform.Matrix[14], 10);
        var pattern = Assert.Single(result.Ir.Patterns!);
        Assert.Equal("Board.Frets", pattern.Source);
        Assert.Equal(12, pattern.GeneratedCount);
        Assert.Equal("Board.Frets", pattern.Associations![11].SourceSet);
        Assert.Contains(fret.Provenance, p => p.Stage == "assembly-pattern");
        var step = AssemblyIrAp242Exporter.Export(result);
        Assert.True(step.IsSuccess, string.Join("\n", step.Diagnostics));
        var imported = Aetheris.Kernel.Core.Step242.Step242AssemblyImporter.Import(step.Value);
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics));
        var definitionIds = imported.Value.Definitions.Where(d => d.Geometry is not null).Select(d => d.StableId).ToHashSet();
        Assert.Equal(13, imported.Value.Occurrences.Count(o => definitionIds.Contains(o.DefinitionStableId!)));
    }

    [Fact]
    public void SeriesChangesMoveGeometryWithoutChangingKeyedIdentities()
    {
        AssemblyIr Compile(string source)
        {
            var parsed = new AssemblyM0Parser().Parse(source);
            Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
            var result = new AssemblyM0Compiler().Compile(parsed.Source!);
            Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
            return result.Ir!;
        }
        var before = Compile(Source);
        var after = Compile(Source.Replace("628mm", "630mm"));
        Assert.Equal(before.ConceptPoints!.SelectMany(c => c.Points).Select(p => p.StableId),
            after.ConceptPoints!.SelectMany(c => c.Points).Select(p => p.StableId));
        foreach (var occurrence in before.Instances)
            Assert.Equal(occurrence.StableId, after.Instances.Single(i => i.Path.ToString() == occurrence.Path.ToString()).StableId);
        Assert.NotEqual(before.Instances.Single(i => i.Path.ToString() == "PointWitness.Frets.Fret12.Fret").ResolvedTransform!.Matrix[13],
            after.Instances.Single(i => i.Path.ToString() == "PointWitness.Frets.Fret12.Fret").ResolvedTransform!.Matrix[13]);
    }

    [Theory]
    [InlineData("Count: 12", "Count: 1025")]
    [InlineData("Count: 12", "Count: 0")]
    [InlineData("First: 1", "First: -1")]
    [InlineData("First: 1", "First: 2147483647")]
    [InlineData("[Bass, Middle, Treble]", "[Bass, Bass, Treble]")]
    [InlineData("[Bass, Middle, Treble]", "Bass, Middle, Treble")]
    [InlineData("[18mm,0mm]", "[0mm,0mm]")]
    [InlineData("[18mm,0mm]", "[18deg,0mm]")]
    [InlineData("[18mm,0mm]", "[1e308mm,0mm]")]
    [InlineData("Point2(-18mm,0mm)", "Point3(-18mm,0mm,0mm)")]
    [InlineData("Nut.Crossings.Bass.X", "Nut.Crossings.Unknown.X")]
    [InlineData("Nut.Crossings.Bass.X", "Nut.Crossings.Bass.Z")]
    [InlineData("Points<Point2> Crossings", "Points<Point4> Crossings")]
    [InlineData("scale * (1.0 - Pow(2.0, -n / 12.0))", "Distance(n,scale)")]
    [InlineData("scale * (1.0 - Pow(2.0, -n / 12.0))", "Missing(n)")]
    [InlineData("scale * (1.0 - Pow(2.0, -n / 12.0))", "undefined + scale")]
    [InlineData("scale * (1.0 - Pow(2.0, -n / 12.0))", "scale + 1deg")]
    [InlineData("scale * (1.0 - Pow(2.0, -n / 12.0))", "scale / 0")]
    [InlineData("Pow(2.0, -n / 12.0)", "Pow(2mm, -n / 12.0)")]
    [InlineData("Distance(n,628mm)", "Distance(1.5,628mm)")]
    [InlineData("Distance(n,628mm)", "Distance(n,628deg)")]
    [InlineData("Distance(n,628mm)", "Distance(1e30,628mm)")]
    [InlineData("Function Distance", "Comptime Function Distance")]
    public void InvalidRecipesAndImpureOrUnboundedFunctionsFailClosed(string from, string to)
    {
        var result = new AssemblyM0Parser().Parse(Source.Replace(from, to));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "assembly-pattern-invalid"
            && d.Message.Contains("firmament-concept-points-", StringComparison.Ordinal));
    }

    [Fact]
    public void FunctionsAreAvailableToPartAuthoringWithoutAComptimeKeyword()
    {
        var parsed = FirmamentV2Parser.Parse(File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(
            "fixtures/Canonical/AssemblyInterfaces/AuthoringFoundation/scalar-functions-part.firmament")));
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
    }

    [Fact]
    public void PointCollectionsAlsoRemainTypedAndErasedInPartAuthoring()
    {
        var parsed = FirmamentV2Parser.Parse(File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(
            "fixtures/Canonical/AssemblyInterfaces/AuthoringFoundation/concept-points-part.firmament")));
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var points = Assert.Single(parsed.Document!.StaticAuthoring!.ConceptPoints!);
        Assert.Equal("Point2", points.ElementType);
        Assert.Equal(2, points.Points.Count);
    }

    [Fact]
    public void TinyFunctionResultsRoundTripIntoPhysicalPlacement()
    {
        var source = "Function Tiny() -> Length = 1e-20mm;\n" + Source.Replace("Nut.Crossings.Bass.X", "Tiny()");
        var result = new AssemblyM1Pipeline().Compile(source);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        var bass = result.Ir!.Instances.Single(i => i.Path.ToString() == "PointWitness.Bass");
        Assert.Equal(1e-20, bass.ResolvedTransform!.Matrix[12]);
    }

    [Fact]
    public void EvaluationBudgetsStopDeepSyntaxAndBranchingFunctionGraphs()
    {
        var nested = new string('(', 130) + "scale" + new string(')', 130);
        var deep = new AssemblyM0Parser().Parse(Source.Replace("scale * (1.0 - Pow(2.0, -n / 12.0))", nested));
        Assert.False(deep.IsSuccess);
        var graph = string.Join("\n", Enumerable.Range(0, 20).Select(n =>
            $"Function Branch{n}(x: Length) -> Length = " + (n == 19 ? "x;" : $"Branch{n+1}(x) + Branch{n+1}(x);")));
        var excessive = new AssemblyM0Parser().Parse(graph + "\n" + Source.Replace("PadHeight(1mm)", "Branch0(1mm)"));
        Assert.False(excessive.IsSuccess);
        Assert.Contains(excessive.Diagnostics, d => d.Message.Contains("function-call-invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void GuitarUsesNutLocalCrossingsAndEquationDrivenFrets()
    {
        var result = new AssemblyM1Pipeline().CompileFile(FirmamentCorpusHarness.ResolveFixtureFullPath(
            "fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal(99, result.Geometry!.InstanceBodies.Count);
        var crossings = result.Ir!.ConceptPoints!.Single(c => c.Name == "NutLayout.Crossings");
        Assert.Equal(6, crossings.Points.Count);
        Assert.Equal(-18, crossings.Points[0].Coordinates[0], 10);
        Assert.Equal(18, crossings.Points[5].Coordinates[0], 10);
        var frets = result.Ir.ConceptPoints!.Single(c => c.Name == "FretboardLayout.Positions");
        Assert.Equal(22, frets.Points.Count);
        for (var n = 1; n <= 22; n++)
        {
            var fret = result.Ir.Instances.Single(i => i.Path.ToString() == $"GuitarX0.Neck.Frets.Fret{n}.Fret");
            Assert.Equal(658 - 628 * (1 - Math.Pow(2, -n / 12d)), fret.ResolvedTransform!.Matrix[13], 9);
            Assert.Equal(62, fret.ResolvedTransform.Matrix[14], 9);
        }
    }
}
