using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentFeatureIdentityTests
{
    private static string Fixture(string name) => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(
        "fixtures/Regression/FeatureIdentity/" + name + ".firmament")).ReplaceLineEndings("\n");

    private static FirmamentV2Document Parse(string source)
    {
        var result = FirmamentV2Parser.Parse(source);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        return result.Document!;
    }

    [Fact]
    public void NamedCallsKeepIdentityAndPublishWallSelectionWhenReorderedOrMoved()
    {
        var source = Fixture("named-mounts");
        var left = "Feature leftBore = MountHole(center: Layout.leftMount)";
        var right = "Feature rightBore = MountHole(center: Layout.rightMount)";
        var reordered = source.Replace(left, "__left__").Replace(right, left).Replace("__left__", right);
        var baseline = Parse(source);
        foreach (var text in new[] { source, reordered, source.Replace("[-12mm, 0mm]", "[-10mm, 0mm]") })
        {
            var document = Parse(text);
            Assert.Equal(baseline.FeatureInvocations!.Select(i => i.ApplicationIdentity).Order(), document.FeatureInvocations!.Select(i => i.ApplicationIdentity).Order());
            Assert.All(document.FeatureInvocations!, i => Assert.Equal("Named", i.IdentityKind));
            var build = FirmamentBuildAndExport.CompileSource(text);
            Assert.True(build.IsSuccess, string.Join("\n", build.Diagnostics));
            Assert.Equal(new[] { "leftBore", "rightBore" }, document.ModifyBlocks!.Single().SemanticHoles.Select(h => h.Name).Order());
            var projection = Assert.Single(FirmamentFieldProjector.Project(document, text, "mounts.firmament", 1), p => p.SemanticId == "stock.leftBore");
            Assert.Equal(document.FeatureInvocations!.Single(i => i.ResultIdentity == "leftBore").ApplicationIdentity, projection.FeatureApplicationIdentity);
            Assert.StartsWith("MountHole(", text.Substring(projection.ConstructSpan!.Start, projection.ConstructSpan.Length));
            Assert.All(projection.Fields, field => { Assert.False(field.Editable); Assert.Null(field.ValueSpan); });
            var hole = document.ModifyBlocks.Single().SemanticHoles.Single(h => h.Name == "leftBore");
            Assert.Equal(projection.ConstructSpan, hole.SourceSpan);
            var sourceMap = new Aetheris.Kernel.Firmament.Materializer.GeometrySourceMap(build.Value!.RuntimeCorrespondence!);
            Assert.True(sourceMap.TryGetSourceSpan("stock.leftBore", out var geometrySpan));
            Assert.Equal(projection.ConstructSpan, geometrySpan);
            Assert.Equal(text.Contains("[-10mm, 0mm]") ? -10d : -12d, hole.Center.U);
        }
    }

    [Fact]
    public void KeyedPatternReportsActualResultsAndSurvivesInsertionReorderAndRemoval()
    {
        var source = Fixture("keyed-mounts");
        var baseline = Assert.Single(Parse(source).FeatureInvocations!);
        foreach (var text in new[] { source, source.Replace("left => Point2(-12mm, 0mm)\n    right => Point2(12mm, 0mm)",
            "right => Point2(12mm, 0mm)\n    middle => Point2(0mm, 0mm)\n    left => Point2(-12mm, 0mm)"),
            source.Replace("    left => Point2(-12mm, 0mm)\n", "") })
        {
            var document = Parse(text);
            var call = Assert.Single(document.FeatureInvocations!);
            Assert.Equal("KeyedPattern", call.IdentityKind);
            Assert.Equal(baseline.ApplicationIdentity, call.ApplicationIdentity);
            Assert.Contains("mounts.right", call.GeneratedResults);
            Assert.Equal(document.StaticAuthoring!.Patterns!.Single().GeneratedIds, call.GeneratedResults);
            Assert.Equal(call.GeneratedResults.Select(n => n.Replace('.', '_')).Order(), document.ModifyBlocks!.Single().SemanticHoles.Select(h => h.Name).Order());
            var build = FirmamentBuildAndExport.CompileSource(text);
            Assert.True(build.IsSuccess, string.Join("\n", build.Diagnostics));
            Assert.All(FirmamentFieldProjector.Project(document, text, "keyed.firmament", 1).Where(p => p.FeatureApplicationIdentity is not null),
                p => Assert.Equal(call.ApplicationIdentity, p.FeatureApplicationIdentity));
        }
    }

    [Fact]
    public void EditorNamesNewCallsAndPromotesAnExistingCallWithoutChangingGeometry()
    {
        var source = Fixture("named-mounts");
        var pmi = FirmamentLanguageOutlineService.Outline(source, "mounts.firmament", "1").Nodes.Single(n => n.Kind == "Pmi");
        var anonymous = FirmamentLanguageEditService.Apply(source, "mounts.firmament", "1", [new("removeDeclaration") { Node = pmi.Id }])
            .Text.Replace("Feature leftBore = ", "");
        var call = FirmamentSyntaxTraversal.Statements(FirmamentSyntaxParser.Parse(anonymous).Document)
            .OfType<ExpressionStatementSyntax>().Single();
        var promoted = FirmamentLanguageEditService.Apply(anonymous, "mounts.firmament", "1", [new("nameFeature")
        { Span = new(call.Span.Start, call.Span.Length), Name = "leftBore" }]);
        Assert.Contains("Feature leftBore = MountHole(center: Layout.leftMount)", promoted.Text);
        Assert.Contains("MountingPlate/Modify:stock/leftBore", promoted.Nodes);
        var before = FirmamentBuildAndExport.CompileSource(anonymous);
        var after = FirmamentBuildAndExport.CompileSource(promoted.Text);
        Assert.True(before.IsSuccess, string.Join("\n", before.Diagnostics));
        Assert.True(after.IsSuccess, string.Join("\n", after.Diagnostics));
        Assert.Equal(Aetheris.Kernel.Core.Brep.Verification.BrepMassProperties.Evaluate(before.Value!.RuntimeBody!).SignedVolume,
            Aetheris.Kernel.Core.Brep.Verification.BrepMassProperties.Evaluate(after.Value!.RuntimeBody!).SignedVolume, 9);
        var insertion = FirmamentLanguageEditService.Apply(source, "mounts.firmament", "1", [new("insertFeature")
        { Node = "MountingPlate/Box:stock", Source = "MountHole(center: Point2(0mm, 0mm))" }]);
        Assert.Contains("Feature mountHole = MountHole(", insertion.Text);
        Assert.True(FirmamentBuildAndExport.CompileSource(insertion.Text).IsSuccess);
        Assert.Equal(Parse(source).FeatureInvocations!.Single(i => i.ResultIdentity == "leftBore").ApplicationIdentity,
            Parse(insertion.Text).FeatureInvocations!.Single(i => i.ResultIdentity == "leftBore").ApplicationIdentity);
        var lowercase = FirmamentLanguageEditService.Apply(source.Replace("MountHole", "mountHole"), "mounts.firmament", "1", [new("insertFeature")
        { Node = "MountingPlate/Box:stock", Source = "mountHole(center: Point2(0mm, 0mm))" }]);
        Assert.Contains("Feature mountHole2 = mountHole(", lowercase.Text);
        Assert.True(FirmamentBuildAndExport.CompileSource(lowercase.Text).IsSuccess);
        var explicitName = FirmamentLanguageEditService.Apply(source, "mounts.firmament", "1", [new("insertDeclaration")
        { Parent = "MountingPlate/Modify:stock", Source = "MountHole(center: Point2(0mm, 0mm))", Name = "centerBore" }]);
        Assert.Contains("Feature centerBore = MountHole(", explicitName.Text);
        Assert.True(FirmamentBuildAndExport.CompileSource(explicitName.Text).IsSuccess);
        var duplicate = Assert.Throws<FirmamentLanguageEditException>(() => FirmamentLanguageEditService.Apply(anonymous, "mounts.firmament", "1",
            [new("nameFeature") { Span = new(call.Span.Start, call.Span.Length), Name = "rightBore" }]));
        Assert.Equal("edit-name-taken", duplicate.Code);
        Assert.Throws<FirmamentLanguageEditException>(() => FirmamentLanguageEditService.Apply(anonymous, "mounts.firmament", "1",
            [new("nameFeature") { Span = new(call.Span.Start + 1, call.Span.Length), Name = "leftBore" }]));
    }
}
