using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentPatternBindingTests
{
    private static string Source(string name) => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(
        "fixtures/Regression/PatternAuthoringAudit/" + name + ".firmament"));

    [Fact]
    public void UnrelatedStaticCannotCaptureExpressionFunctionParameterOrMoveFrets()
    {
        var before = new AssemblyM1Pipeline().Compile(Source("function-parameter-shadow-assembly-control"));
        var after = new AssemblyM1Pipeline().Compile(Source("function-parameter-shadow-assembly"));
        Assert.True(before.IsSuccess, string.Join("\n", before.Diagnostics));
        Assert.True(after.IsSuccess, string.Join("\n", after.Diagnostics));
        var point = after.Ir!.ConceptPoints!.Single(c => c.Name == "Board.Frets").Points.Single(p => p.Key == "Fret12");
        Assert.Equal(-314, point.Coordinates[1], 10);
        foreach (var instance in before.Ir!.Instances)
        {
            var actual = after.Ir.Instances.Single(i => i.StableId == instance.StableId);
            Assert.Equal(instance.ResolvedTransform?.Matrix, actual.ResolvedTransform?.Matrix);
        }
        Assert.Equal(344, after.Ir.Instances.Single(i => i.Path.ToString() == "PointWitness.Frets.Fret12.Fret").ResolvedTransform!.Matrix[13], 10);
        Assert.Equal(before.Geometry!.DefinitionBodies.Keys, after.Geometry!.DefinitionBodies.Keys);
    }

    [Theory]
    [InlineData("series-record-inputs")]
    [InlineData("series-template-inputs")]
    [InlineData("series-count-static")]
    [InlineData("series-pitch-static")]
    [InlineData("series-pitch-static-unshadowed")]
    [InlineData("series-module-static")]
    [InlineData("series-part-count-static-module")]
    public void PartSeriesConsumesCheckedLexicalValuesAndPreservesPatternKeys(string name)
    {
        var control = FirmamentV2Parser.Parse(Source("equation-plate"));
        var actual = FirmamentV2Parser.Parse(Source(name));
        Assert.True(actual.IsSuccess, string.Join("\n", actual.Diagnostics));
        var points = Assert.Single(actual.Document!.StaticAuthoring!.ConceptPoints!);
        Assert.Equal(new[] { -30d, -25d, -10d, 15d }, points.Points.Select(p => p.Coordinates[0]));
        Assert.Equal(control.Document!.StaticAuthoring!.ConceptPoints![0].Points.Select(p => p.StableId), points.Points.Select(p => p.StableId));
        var pattern = Assert.Single(actual.Document.StaticAuthoring.Patterns);
        Assert.Equal("Layout.Sites", pattern.Source);
        Assert.All(pattern.Associations!, a => Assert.Equal("Layout.Sites", a.SourceSet));
        Assert.Equal(4, Assert.Single(actual.Document.ModifyBlocks!).SemanticHoles.Count);
    }

    [Fact]
    public void PointErrorsKeepOffendingExpressionSpanAndHumanMessage()
    {
        var source = Source("series-unknown-field");
        var view = FirmamentPatternService.Project(source, "part.firmament", "1");
        var error = Assert.Single(view.Diagnostics, d => d.Code == "firmament-concept-points-field-unknown");
        Assert.Contains("heigth", error.Message);
        Assert.NotNull(error.Span);
        Assert.StartsWith("heigth:", source.Substring(error.Span.Start, error.Span.Length));
        Assert.Equal("Unresolved", view.ValidationLevel);
    }

    [Fact]
    public void PatternProjectionResolvesIncludedFunctionsThroughProductionModuleGraph()
    {
        var complete = Source("equation-plate");
        var at = complete.IndexOf("Model EquationPlate", StringComparison.Ordinal);
        var root = "include \"laws.firmament\"\n" + complete[at..];
        var project = new FirmamentProjectSnapshot("plate.firmament", new Dictionary<string, string>
            { ["plate.firmament"] = root, ["laws.firmament"] = complete[..at] });
        var view = FirmamentPatternService.Project(root, "plate.firmament", "r1", project);
        Assert.Empty(view.Diagnostics);
        Assert.Equal(new[] { -30d, -25d, -10d, 15d }, Assert.Single(view.Layouts).Rows.Select(r => r.Coordinates![0]));
        Assert.Equal("point => Drill(center: point)", Assert.Single(view.Patterns).Body);
    }

    [Fact]
    public void SeriesCompletionDoesNotSuggestIncompatibleKeys()
    {
        var source = Source("equation-plate");
        var offset = source.IndexOf("Points<Point2> Sites {", StringComparison.Ordinal) + "Points<Point2> Sites {".Length;
        var completion = FirmamentLanguageService.Complete(source, "part.firmament", "1", offset);
        Assert.Equal("Points", completion.Context);
        Assert.Empty(completion.Fields);
    }

    [Theory]
    [InlineData("Points<Point2> Sites {", "Points<Point2> Sites on XZ {")]
    [InlineData("Series {", "Series Named {")]
    [InlineData("Series {", "Series<Length> {")]
    public void PointAstMigrationDoesNotIgnoreNewlyRecognizedHeaders(string from, string to)
    {
        var result = FirmamentV2Parser.Parse(Source("equation-plate").Replace(from, to, StringComparison.Ordinal));
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void PointCollectionOwnerFrameCannotBeErasedIntoAValidXyPattern()
    {
        var result = FirmamentV2Parser.Parse(Source("series-owner-frame-rejected"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.SyntaxDiagnostics, d => d.Code == "firmament-concept-points-collection-owner-header-unsupported"
            && d.Message.Contains("frame", StringComparison.Ordinal));
    }

    [Fact]
    public void PointRecipesCannotEraseTypedTolerance()
    {
        var result = FirmamentV2Parser.Parse(Source("series-tolerance-input-rejected"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.SyntaxDiagnostics, d => d.Code == "firmament-concept-points-tolerance-consumer-unsupported");
    }

    [Theory]
    [InlineData("ui-created-series", 5)]
    [InlineData("ui-created-stations", 7)]
    public void SavedBrowserPatternsRetainExistingHolesAndProduceEveryNewKey(string fixture, int count)
    {
        var baseline = FirmamentV2Parser.Parse(Source("ui-edited-law"));
        var actual = FirmamentV2Parser.Parse(Source(fixture));
        Assert.True(actual.IsSuccess, string.Join("\n", actual.Diagnostics));
        var holes = Assert.Single(actual.Document!.ModifyBlocks!).SemanticHoles;
        Assert.Equal(count, holes.Count);
        foreach (var original in Assert.Single(baseline.Document!.ModifyBlocks!).SemanticHoles)
        {
            var hole = holes.Single(h => h.Name == original.Name);
            Assert.Equal(original.Center, hole.Center);
            Assert.Equal(original.ShaftDiameter, hole.ShaftDiameter);
        }
        Assert.Equal(count, actual.Document.StaticAuthoring!.Patterns.Sum(p => p.GeneratedCount));
        Assert.All(actual.Document.StaticAuthoring.Patterns.SelectMany(p => p.Associations!), a => Assert.DoesNotContain("__ConceptPoints_", a.SourceSet));
    }

    [Fact]
    public void CollectionNameInPatternClauseCannotImpersonateAPointsDeclarationForCompletion()
    {
        var source = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Set/pattern-feature-map.firmament"));
        var offset = source.IndexOf("over Points {", StringComparison.Ordinal) + "over Points {".Length;
        var completion = FirmamentLanguageService.Complete(source, "part.firmament", "1", offset);
        Assert.DoesNotContain(completion.Fields, f => f.Name.Equals("Keys", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PatternSourceRetargetUsesOneCheckedExpression()
    {
        var source = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Set/pattern-feature-map.firmament"));
        var view = FirmamentPatternService.Project(source, "part.firmament", "r1");
        var pattern = Assert.Single(view.Patterns);
        var edit = FirmamentLanguageEditService.Apply(source, "part.firmament", "r1", [
            new("insertDeclaration") { Parent = "SetFeaturePattern", Source = "Static Other: Set<Point2> { Center => Point2(0mm, 0mm) }" },
            new("setPatternSource") { Node = pattern.Node, Value = "Other" }]);
        var after = FirmamentV2Parser.Parse(edit.Text);
        Assert.True(after.IsSuccess, string.Join("\n", after.Diagnostics));
        Assert.Equal(0, Assert.Single(Assert.Single(after.Document!.ModifyBlocks!).SemanticHoles).Center.U);
    }

    [Fact]
    public void AssemblyMappingEditsKeepKeysFramesAndGeometryArguments()
    {
        var source = Source("series-assembly");
        var pattern = Assert.Single(FirmamentPatternService.Project(source, "assembly.firmament", "r1").Patterns);
        var edit = FirmamentLanguageEditService.Apply(source, "assembly.firmament", "r1", [new("setPatternBody")
        { Node = pattern.Node, Value = "point => <Part Fret = Pad<H:PadHeight(1mm)>> Placement { from: Bottom.Frame; to: BoardSeat.Frame; translateLocal: [point.X,point.Y,0mm]; } </Part>" }]);
        var before = new AssemblyM1Pipeline().Compile(source);
        var after = new AssemblyM1Pipeline().Compile(edit.Text);
        Assert.True(after.IsSuccess, string.Join("\n", after.Diagnostics));
        Assert.Equal(before.Ir!.Instances.Select(i => i.StableId), after.Ir!.Instances.Select(i => i.StableId));
        foreach (var instance in before.Ir.Instances)
            Assert.Equal(instance.ResolvedTransform?.Matrix, after.Ir.Instances.Single(i => i.StableId == instance.StableId).ResolvedTransform?.Matrix);
        Assert.Equal(before.Geometry!.DefinitionBodies.Keys, after.Geometry!.DefinitionBodies.Keys);
    }

    [Fact]
    public void KeyedSetEditingChangesAuthoredValueAndKeepsTheEntryIdentity()
    {
        var source = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Set/pattern-feature-map.firmament"));
        var view = FirmamentPatternService.Project(source, "part.firmament", "r1");
        Assert.Empty(view.Diagnostics);
        var layout = Assert.Single(view.Layouts);
        Assert.Equal("Point2(-12mm, 0mm)", layout.Rows[0].Authored);
        var edit = FirmamentLanguageEditService.Apply(source, "part.firmament", "r1", [
            new("setSetEntry") { Node = layout.Node, Field = "First", Value = "Point2(-10mm, 0mm)" }]);
        var after = FirmamentV2Parser.Parse(edit.Text);
        Assert.True(after.IsSuccess, string.Join("\n", after.Diagnostics));
        var holes = Assert.Single(after.Document!.ModifyBlocks!).SemanticHoles;
        Assert.Equal(new[] { -10d, 12d }, holes.Select(h => h.Center.U));
        var projected = FirmamentPatternService.Project(edit.Text, "part.firmament", "r2");
        Assert.Equal(layout.Rows.Select(r => r.Key), projected.Layouts.Single().Rows.Select(r => r.Key));
        Assert.Equal(layout.Rows.SelectMany(r => r.GeneratedIds), projected.Layouts.Single().Rows.SelectMany(r => r.GeneratedIds));
    }

    [Fact]
    public void PatternProjectionAndEditsKeepLawSourceKeysAndReadOnlyResults()
    {
        var source = Source("equation-plate");
        var view = FirmamentPatternService.Project(source, "part.firmament", "r1");
        Assert.Empty(view.Diagnostics);
        var layout = Assert.Single(view.Layouts);
        var pattern = Assert.Single(view.Patterns);
        Assert.Equal("Series", layout.Kind);
        Assert.Equal(4, layout.Rows.Count);
        Assert.All(layout.Rows, r => Assert.NotEmpty(r.GeneratedIds));
        Assert.Contains("siteX", pattern.Controls);
        var edit = FirmamentLanguageEditService.Apply(source, "part.firmament", "r1", [
            new("setField") { Node = FirmamentLanguageOutlineService.Outline(source, "part.firmament", "r1").Nodes.Single(n => n.Kind == "Series").Id, Field = "count", Value = "3" },
            new("setValue") { Node = "siteX", Value = "-25mm + pitch * n * n" }]);
        var projected = FirmamentPatternService.Project(edit.Text, "part.firmament", "r2");
        Assert.Empty(projected.Diagnostics);
        Assert.Equal(new[] { -25d, -20d, -5d }, projected.Layouts.Single().Rows.Select(r => r.Coordinates![0]));
        Assert.Equal(layout.Rows.Take(3).Select(r => r.StableId), projected.Layouts.Single().Rows.Select(r => r.StableId));
        Assert.Contains("position: Point2(siteX(n,5mm),0mm)", edit.Text);
        Assert.Contains("point => Drill(center: point)", edit.Text);
    }
}
