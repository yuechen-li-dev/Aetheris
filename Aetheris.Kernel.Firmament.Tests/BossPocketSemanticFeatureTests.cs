using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class BossPocketSemanticFeatureTests
{
    [Fact]
    public void PracticalBossPocketWitness_LowersThroughOneSectionStack_AndRoundTripsStep()
    {
        var parsed = PrismaticProfileCompositionParser.Parse(File.ReadAllText(Fixture("Canonical/valid/boss-pocket-mounting-block.firmament")));
        Assert.Empty(parsed.Diagnostics);
        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
        Assert.Empty(diagnostics);
        var boss = Assert.Single(stack.Feature.Bosses!);
        var pocket = Assert.Single(stack.Feature.Pockets!);
        Assert.Equal("boss:Body.MountBoss", boss.StableId);
        Assert.Equal(6d, boss.Height);
        Assert.Equal("pocket:Body.ElectronicsRecess", pocket.StableId);
        Assert.Equal(4d, pocket.Depth);
        Assert.Equal(6d, pocket.RemainingFloor);
        Assert.Equal(2d, pocket.MinimumFloorThickness);
        Assert.Contains(stack.Feature.Operations, operation => operation.SemanticFeatureKind == "Boss" && operation.Intent == PrismaticProfileIntent.Add);
        Assert.Contains(stack.Feature.Operations, operation => operation.SemanticFeatureKind == "Pocket" && operation.Intent == PrismaticProfileIntent.Remove);

        var emitted = PrismaticSectionStackEmitter.Emit(stack);
        Assert.NotNull(emitted.Body);
        var mass = BrepMassProperties.Evaluate(emitted.Body!);
        Assert.True(mass.IsEnclosed, string.Join("; ", mass.Diagnostics));
        Assert.True(mass.IsOrientationConsistent, string.Join("; ", mass.Diagnostics));
        var expectedVolume = 40d * 24d * 10d + Math.PI * 6d * 6d * 6d - 8d * 8d * 4d - Math.PI * 3d * 3d * 16d;
        Assert.Equal(expectedVolume, stack.AnalyticVolume, 6);
        var step = Step242Exporter.ExportBody(emitted.Body!);
        Assert.True(step.IsSuccess, string.Join("; ", step.Diagnostics.Select(item => item.Message)));
        var imported = Step242Importer.ImportBody(step.Value!);
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics.Select(item => item.Message)));
        var importedMass = BrepMassProperties.Evaluate(imported.Value!);
        Assert.True(importedMass.IsEnclosed, string.Join("; ", importedMass.Diagnostics));
        Assert.True(importedMass.IsOrientationConsistent, string.Join("; ", importedMass.Diagnostics));
    }

    [Fact]
    public void PublicParserAndBuild_ReportFirstClassBossAndPocketIdentity()
    {
        var source = File.ReadAllText(Fixture("Canonical/valid/boss-pocket-mounting-block.firmament"));
        var parse = FirmamentV2Parser.Parse(source);
        Assert.True(parse.IsSuccess, string.Join("; ", parse.Diagnostics));
        Assert.Equal("MountBoss", Assert.Single(parse.Document!.Bosses!).Name);
        Assert.Equal("ElectronicsRecess", Assert.Single(parse.Document.Pockets!).Name);
        Assert.Equal(FirmamentV2CanonicalSymbolKind.Boss, parse.Document.SymbolTable!.Resolve("MountBoss")!.Kind);
        Assert.Equal(FirmamentV2CanonicalSymbolKind.Pocket, parse.Document.SymbolTable.Resolve("ElectronicsRecess")!.Kind);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("; ", build.Diagnostics.Select(item => item.Message)));
        Assert.Collection(build.Value!.EngineeringFeatures!.OrderBy(item => item.Kind, StringComparer.Ordinal),
            boss => { Assert.Equal("Boss", boss.Kind); Assert.Equal("PrismaticSectionStack/Add", boss.MaterializationRoute); },
            pocket => { Assert.Equal("Pocket", pocket.Kind); Assert.Equal(6d, pocket.RemainingFloor); Assert.Equal("PrismaticSectionStack/Remove", pocket.MaterializationRoute); });
    }

    [Theory]
    [InlineData("minimumFloorThickness", "7", "Template.minimumFloorThickness")]
    [InlineData("minimumWallThickness", "7", "Template.minimumWallThickness")]
    public void PocketMinimumFloor_UsesExistingTemplateConceptChannel(string concept, string value, string policy)
    {
        var source = File.ReadAllText(Fixture("Canonical/valid/boss-pocket-mounting-block.firmament"))
            .Replace("; MinimumFloorThickness: 2mm", string.Empty, StringComparison.Ordinal)
            .Replace("    Concept Struct Layout On XY", $"    template<CNC> ShopDefault {{ concept {concept}: {value} mm }}\n\n    Concept Struct Layout On XY", StringComparison.Ordinal);
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        Assert.Null(parsed.Feature);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.StartsWith("firmament-pocket-minimum-floor-thickness:ElectronicsRecess", StringComparison.Ordinal) && diagnostic.Contains($"policy={policy}", StringComparison.Ordinal));
    }

    [Fact]
    public void PocketTemplateFloorPolicy_ParticipatesInRealBuildPath()
    {
        var source = File.ReadAllText(Fixture("Canonical/valid/boss-pocket-mounting-block.firmament"))
            .Replace("; MinimumFloorThickness: 2mm", string.Empty, StringComparison.Ordinal)
            .Replace("    Concept Struct Layout On XY", "    template<CNC> ShopDefault { concept minimumFloorThickness: 5 mm }\n\n    Concept Struct Layout On XY", StringComparison.Ordinal);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("; ", build.Diagnostics.Select(item => item.Message)));
        var pocket = Assert.Single(build.Value!.EngineeringFeatures!, item => item.Kind == "Pocket");
        Assert.Equal(5d, pocket.MinimumFloorThickness);
        Assert.Equal("Template.minimumFloorThickness", pocket.PolicySource);
    }

    [Fact]
    public void BossPocketCounterboreCombination_BuildsAndReimports()
    {
        var source = File.ReadAllText(Fixture("Canonical/valid/boss-pocket-mounting-block.firmament"))
            .Replace("Hole<Shaft> MountHole { On: +Z; Center: [-12mm, 0mm]; Diameter: 6mm; End: ThroughAll; Role: MountingHole }",
                "Hole<Counterbore> MountHole { On: +Z; Center: [-12mm, 0mm]; Diameter: 6mm; CounterboreDiameter: 10mm; CounterboreDepth: 3mm; End: ThroughAll; Role: MountingHole }", StringComparison.Ordinal);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("; ", build.Diagnostics.Select(item => item.Message)));
        var imported = Step242Importer.ImportBody(build.Value!.StepText);
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics.Select(item => item.Message)));
        Assert.True(BrepMassProperties.Evaluate(imported.Value!).IsEnclosed);
    }

    [Fact]
    public void BossPocketEdgeFinishCombination_PreservesSemanticInventory_WhenAdmitted()
    {
        var source = File.ReadAllText(Fixture("Canonical/valid/boss-pocket-mounting-block.firmament"))
            .Replace("Boss MountBoss { On: Top; Profile: BossProfile;", "Boss MountBoss { On: Top; Profile: RectBossProfile;", StringComparison.Ordinal);
        source = AddBeforeModelClose(source,
            "    Modify Body { EdgeFinish BossTop { Target: RectBossProfile.Outer On: Top Kind: Chamfer Distance: 1mm } }\n");
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(build.IsSuccess, string.Join("; ", build.Diagnostics.Select(item => item.Message)));
        Assert.Equal(["Boss", "EdgeFinish", "Pocket"], build.Value!.EngineeringFeatures!.Select(item => item.Kind).Order().ToArray());
        var imported = Step242Importer.ImportBody(build.Value.StepText);
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics.Select(item => item.Message)));
        Assert.True(BrepMassProperties.Evaluate(imported.Value!).IsEnclosed);
    }

    [Theory]
    [InlineData("9.5", "firmament-pocket-minimum-floor-thickness", "remainingFloor=0.5mm")]
    [InlineData("10", "firmament-pocket-through-depth", "remainingFloor=0mm")]
    [InlineData("11", "firmament-pocket-through-depth", "remainingFloor=-1mm")]
    public void PocketFloorGuardrails_RejectWithEngineeringQuantities(string depth, string code, string quantity)
    {
        var source = File.ReadAllText(Fixture("Canonical/valid/boss-pocket-mounting-block.firmament"))
            .Replace("Depth: 4mm", $"Depth: {depth}mm", StringComparison.Ordinal);
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        Assert.Null(parsed.Feature);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.StartsWith(code + ":ElectronicsRecess", StringComparison.Ordinal) && diagnostic.Contains(quantity, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Height: 6mm", "Height: 0mm", "firmament-boss-height-must-be-positive")]
    [InlineData("On: Top; Profile: BossProfile", "On: Bottom; Profile: BossProfile", "firmament-boss-invalid-target")]
    [InlineData("On: Top; Profile: BossProfile", "On: Top; Profile: MissingProfile", "firmament-boss-invalid-profile")]
    public void BossGuardrails_AreTyped(string before, string after, string code)
    {
        var source = File.ReadAllText(Fixture("Canonical/valid/boss-pocket-mounting-block.firmament")).Replace(before, after, StringComparison.Ordinal);
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        Assert.Null(parsed.Feature);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.StartsWith(code + ":MountBoss", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("[30mm, 0mm]", "disjoint")]
    [InlineData("[25mm, 0mm]", "tangent")]
    public void BossConnectivity_RejectsDisjointAndPointContact(string center, string _)
    {
        var source = File.ReadAllText(Fixture("Canonical/valid/boss-pocket-mounting-block.firmament"))
            .Replace("Boss MountBoss { On: Top; Profile: BossProfile;", "Boss MountBoss { On: Top; Profile: RectBossProfile;", StringComparison.Ordinal)
            .Replace("Rect2 BossPad { Center: [-12mm, 0mm]", $"Rect2 BossPad {{ Center: {center}", StringComparison.Ordinal);
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        Assert.Null(parsed.Feature);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.StartsWith("firmament-boss-disconnected-from-host:MountBoss", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Depth: 4mm", "Depth: 0mm", "firmament-pocket-depth-must-be-positive")]
    [InlineData("On: Top; Profile: PocketProfile", "On: Bottom; Profile: PocketProfile", "firmament-pocket-invalid-target")]
    [InlineData("On: Top; Profile: PocketProfile", "On: Top; Profile: MissingProfile", "firmament-pocket-invalid-profile")]
    [InlineData("MinimumFloorThickness: 2mm", "MinimumFloorThickness: 0mm", "firmament-pocket-minimum-floor-policy-invalid")]
    public void PocketGuardrails_AreTyped(string before, string after, string code)
    {
        var source = File.ReadAllText(Fixture("Canonical/valid/boss-pocket-mounting-block.firmament")).Replace(before, after, StringComparison.Ordinal);
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        Assert.Null(parsed.Feature);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.StartsWith(code + ":ElectronicsRecess", StringComparison.Ordinal));
    }

    private static string Fixture(string relative) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/FirmamentV2", relative));

    private static string AddBeforeModelClose(string source, string addition)
    {
        var close = source.LastIndexOf('}');
        return source.Insert(close, addition);
    }
}
