using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblySiteAuthoringTests
{
    private static string Fixture => FirmamentCorpusHarness.ResolveFixtureFullPath(
        "fixtures/Canonical/AssemblyInterfaces/AuthoringFoundation/mounting-rows.firmament");

    [Fact]
    public void RowsReuseOneComponentAndSeatInTiltedFrameWithProperClocking()
    {
        var result = new AssemblyM1Pipeline().CompileFile(Fixture);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Single(result.Ir!.AssemblyDefinitions!);
        Assert.Equal(2, result.Geometry!.DefinitionBodies.Count);
        Assert.Equal(12, result.Geometry.InstanceBodies.Count);
        var exported = AssemblyIrAp242Exporter.Export(result);
        Assert.True(exported.IsSuccess, string.Join("\n", exported.Diagnostics));
        var imported = Aetheris.Kernel.Core.Step242.Step242AssemblyImporter.Import(exported.Value);
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics));
        var geometryIds = imported.Value.Definitions.Where(d => d.Geometry is not null).Select(d => d.StableId).ToHashSet();
        Assert.Equal(12, imported.Value.Occurrences.Count(o => geometryIds.Contains(o.DefinitionStableId!)));
        var patterns = result.Ir.Patterns!;
        Assert.Equal(2, patterns.Count);
        Assert.Single(patterns.Single(p => p.Name == "BassRow").SiteRecipe!);
        Assert.Equal(2, patterns.Single(p => p.Name == "TrebleRow").SiteRecipe!.Count);
        Assert.All(patterns, p => Assert.Equal(new[] { "Low", "Middle", "High" }, p.Associations!.Select(a => a.SourceEntry)));
        var angle = -13 * Math.PI / 180;
        foreach (var (key, y) in new[] { ("Low", 20), ("Middle", 50), ("High", 80) })
        {
            var bass = result.Ir.Instances.Single(i => i.Path.ToString() == $"MountingRows.BassRow.{key}.Unit.Post").ResolvedTransform!.Matrix;
            var treble = result.Ir.Instances.Single(i => i.Path.ToString() == $"MountingRows.TrebleRow.{key}.Unit.Post").ResolvedTransform!.Matrix;
            Assert.Equal(-10, bass[12], 8); Assert.Equal(10, treble[12], 8);
            Assert.Equal(y * Math.Cos(angle), bass[13], 8);
            Assert.Equal(7 + y * Math.Sin(angle), bass[14], 8);
            Assert.Equal(bass[13], treble[13], 8); Assert.Equal(bass[14], treble[14], 8);
            Assert.Equal(1, bass[0], 8); Assert.Equal(-1, treble[0], 8);
            Assert.Equal(bass[9], treble[9], 8); Assert.Equal(bass[10], treble[10], 8);
            var leftButton = result.Ir.Instances.Single(i => i.Path.ToString() == $"MountingRows.BassRow.{key}.Unit.Button");
            var rightButton = result.Ir.Instances.Single(i => i.Path.ToString() == $"MountingRows.TrebleRow.{key}.Unit.Button");
            Assert.Equal(-15, leftButton.ResolvedTransform!.Matrix[12], 8);
            Assert.Equal(15, rightButton.ResolvedTransform!.Matrix[12], 8);
        }
    }

    [Fact]
    public void PitchChangesKeepOccurrenceIdentitiesAndMoveOnlyLaterKeys()
    {
        var source = File.ReadAllText(Fixture);
        AssemblyCompilationResult Compile(string text)
        {
            var parsed = new AssemblyM0Parser().Parse(text);
            Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
            var compiled = new AssemblyM0Compiler().Compile(parsed.Source!);
            Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics));
            return compiled;
        }
        var before = Compile(source); var after = Compile(source.Replace("30mm", "36mm"));
        foreach (var occurrence in before.Ir!.Instances)
        {
            var changed = after.Ir!.Instances.Single(i => i.Path.ToString() == occurrence.Path.ToString());
            Assert.Equal(occurrence.StableId, changed.StableId);
            if (occurrence.Path.ToString().Contains(".Low.", StringComparison.Ordinal))
                Assert.Equal(occurrence.ResolvedTransform!.Matrix, changed.ResolvedTransform!.Matrix);
        }
        Assert.NotEqual(before.Ir.Instances.Single(i => i.Path.ToString() == "MountingRows.BassRow.High.Unit").ResolvedTransform!.Matrix[13],
            after.Ir!.Instances.Single(i => i.Path.ToString() == "MountingRows.BassRow.High.Unit").ResolvedTransform!.Matrix[13]);
    }

    [Theory]
    [InlineData("[Low, Middle, High]", "[Low, Low, High]")]
    [InlineData("[Low, Middle, High]", "[]")]
    [InlineData("[0mm,30mm,0mm]", "[0mm,0mm,0mm]")]
    [InlineData("[0mm,30mm,0mm]", "[0mm,30deg,0mm]")]
    [InlineData("[0mm,30mm,0mm]", "[0mm,NaNmm,0mm]")]
    [InlineData("[0mm,30mm,0mm]", "[0mm,1e308mm,0mm]")]
    [InlineData("Across: YZ", "Across: UnknownPlane")]
    [InlineData("From BassSites", "From MissingSites")]
    [InlineData("Across: YZ;", "Across: YZ; Extra: 1;")]
    [InlineData("Across: YZ;", "Across: YZ; Across: XZ;")]
    public void InvalidSiteRecipesFailAtAuthoringBoundary(string oldValue, string newValue)
    {
        var parsed = new AssemblyM0Parser().Parse(File.ReadAllText(Fixture).Replace(oldValue, newValue));
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Code == "assembly-pattern-invalid" && d.Message.Contains("assembly-sites-invalid"));
    }
}
