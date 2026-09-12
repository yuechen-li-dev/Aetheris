using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2NamedSetTests
{
    [Fact]
    public void PointSet_PreservesNamesOrderValuesAndNamedAccess()
    {
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(Fixture("Canonical", "Set", "named-point-access.firmament")));

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var set = Assert.Single(parse.Document!.StaticAuthoring!.Sets!);
        Assert.Equal("Point2", set.ElementType);
        Assert.Equal(["South", "East", "North", "West"], set.Entries.Select(entry => entry.Name));
        Assert.Equal([0, 1, 2, 3], set.Entries.Select(entry => entry.SourceOrder));
        Assert.All(set.Entries, entry => Assert.True(entry.Provenance.Length > 0));
        Assert.NotNull(parse.Document.Profiles);
    }

    [Fact]
    public void RecordSet_FeedsExistingTemplatePatternWithNamedIdentities()
    {
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(Fixture("Canonical", "Set", "record-payloads.firmament")));

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var set = Assert.Single(parse.Document!.StaticAuthoring!.Sets!);
        Assert.Equal("MountSpec", set.ElementType);
        Assert.Equal("Point2(-20mm, 0mm)", set.Entries[0].RecordFields!["Center"]);
        var pattern = Assert.Single(parse.Document.StaticAuthoring.Patterns);
        Assert.Equal(["MountPattern.Left", "MountPattern.Right"], pattern.GeneratedIds);
        Assert.Equal(["Left", "Right"], pattern.Associations!.Select(item => item.SourceEntry));
        Assert.Equal(2, parse.Document.ModifyBlocks!.Single().SemanticHoles.Count);
    }

    [Fact]
    public void SetPattern_MapsFeatureInSourceOrderAndRetainsEntryIdentity()
    {
        var source = File.ReadAllText(Fixture("Canonical", "Set", "pattern-feature-map.firmament"));
        var expansionDiagnostics = new List<string>();
        var featureExpansion = FirmamentV2FeatureExpansion.Expand(source, expansionDiagnostics);
        Assert.NotNull(featureExpansion);
        var staticDiagnostics = new List<string>();
        var staticExpansion = CanonicalStaticAuthoring.Expand(featureExpansion.Source, staticDiagnostics);
        Assert.NotNull(staticExpansion);
        var first = FirmamentV2Parser.Parse(source);
        var second = FirmamentV2Parser.Parse(source);

        Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics) + Environment.NewLine + staticExpansion.Source);
        Assert.True(second.IsSuccess, string.Join(Environment.NewLine, second.Diagnostics));
        var pattern = Assert.Single(first.Document!.StaticAuthoring!.Patterns);
        Assert.Equal(["Drills.First", "Drills.Second"], pattern.GeneratedIds);
        Assert.Equal(["Drills_First", "Drills_Second"], first.Document.ModifyBlocks!.Single().SemanticHoles.Select(hole => hole.Name));
        Assert.Equal(first.Document.ModifyBlocks!.Single().SemanticHoles.Select(hole => hole.Center), second.Document!.ModifyBlocks!.Single().SemanticHoles.Select(hole => hole.Center));
    }

    [Theory]
    [InlineData("duplicate-entry-name.firmament", "firmament-v2-static-set-duplicate-entry:Points:A")]
    [InlineData("wrong-element-type.firmament", "firmament-v2-static-set-entry-type-mismatch:Points:A:expected-Point2")]
    [InlineData("invalid-pattern-target.firmament", "firmament-v2-static-pattern-source-invalid:Bad")]
    public void InvalidSets_FailTyped(string file, string diagnostic)
    {
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(Fixture("Invalid", "Set", file)));
        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, item => item == diagnostic);
    }

    [Fact]
    public void EmptySet_IsValidAndPatternProducesNoInstances()
    {
        var parse = FirmamentV2Parser.Parse("""
            Model EmptySet {
                Units: mm
                Static Points: Set<Point2> { }
                Box Base { Size: [20mm, 20mm, 2mm] }
                Modify Base { Pattern None Over Points { point => Hole<Shaft> H { On: +Z Center: point Diameter: 2mm End: ThroughAll } } }
            }
            """);
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        Assert.Empty(Assert.Single(parse.Document!.StaticAuthoring!.Sets!).Entries);
        Assert.Empty(Assert.Single(parse.Document.StaticAuthoring.Patterns).GeneratedIds);
    }

    [Fact]
    public void ExistingArrayEnumAndMatchRemainValid()
    {
        var array = FirmamentV2Parser.Parse(File.ReadAllText(Fixture("Canonical", "Patterns", "record-array-hole-pattern.firmament")));
        var modern = FirmamentV2Parser.Parse(File.ReadAllText(Fixture("Canonical", "Templates", "generic-mounting-plate.firmament")));
        Assert.True(array.IsSuccess, string.Join(Environment.NewLine, array.Diagnostics));
        Assert.True(modern.IsSuccess, string.Join(Environment.NewLine, modern.Diagnostics));
    }

    [Fact]
    public void MountingSet_UsesExistingSpanContainmentAndExportsArrayParity()
    {
        var named = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture("Canonical", "Set", "mounting-points.firmament")));
        var repeated = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture("Canonical", "Set", "mounting-points.firmament")));
        var array = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture("Canonical", "Span", "plane-hole-support.firmament")));
        var invalid = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture("Invalid", "Set", "pattern-span-containment.firmament")));

        Assert.True(named.IsSuccess, string.Join(Environment.NewLine, named.Diagnostics.Select(item => item.Message)));
        Assert.True(repeated.IsSuccess, string.Join(Environment.NewLine, repeated.Diagnostics.Select(item => item.Message)));
        Assert.Equal(named.Value.StepText, repeated.Value.StepText);
        Assert.True(array.IsSuccess, string.Join(Environment.NewLine, array.Diagnostics.Select(item => item.Message)));
        Assert.Equal(array.Value.StepText, named.Value.StepText);
        Assert.False(invalid.IsSuccess);
        Assert.Contains(invalid.Diagnostics, item => item.Message.StartsWith("firmament-feature-footprint-outside-span:Mounts.UpperRight:MountingArea", StringComparison.Ordinal));
    }

    [Fact]
    public void Polygon2Rhombus_LowersToOrdinaryClosedProfileAndRetainsTypedMetadata()
    {
        var source = File.ReadAllText(Fixture("Canonical", "Set", "mounting-points.firmament"));
        var expansionDiagnostics = new List<string>();
        var expansion = Polygon2RhombusAuthoring.Expand(source, expansionDiagnostics);
        Assert.NotNull(expansion);
        var featureExpansion = FirmamentV2FeatureExpansion.Expand(expansion.Source, expansionDiagnostics);
        Assert.NotNull(featureExpansion);
        var templateExpansion = FirmamentV2TemplateExpansion.Expand(featureExpansion.Source, expansionDiagnostics);
        Assert.NotNull(templateExpansion);
        var staticExpansion = CanonicalStaticAuthoring.Expand(templateExpansion.Source, expansionDiagnostics);
        Assert.NotNull(staticExpansion);
        var composition = Aetheris.Kernel.Firmament.Materializer.PrismaticProfileCompositionParser.Parse(staticExpansion.Source);
        Assert.True(composition.Feature is not null, string.Join(Environment.NewLine, composition.Diagnostics));
        var parse = FirmamentV2Parser.Parse(source);
        var profile = ProfileAuthoringParser.ResolveNamedProfile(source, "MountBoundary", out var diagnostics);

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics) + Environment.NewLine + staticExpansion.Source);
        var polygon = Assert.Single(parse.Document!.Polygons!);
        Assert.Equal("MountBoundaryShape", polygon.Name);
        Assert.Equal("Rhombus", polygon.Variant);
        Assert.Equal([0d, 0d, 90d, 64d], [polygon.CenterX, polygon.CenterY, polygon.DiagonalX, polygon.DiagonalY]);
        Assert.Equal(4, polygon.GeneratedPoints.Count);
        Assert.Equal(4, polygon.GeneratedEdges.Count);
        Assert.Equal(FirmamentV2CanonicalSymbolKind.Polygon2, parse.Document.SymbolTable!.Resolve("MountBoundaryShape")!.Kind);
        Assert.NotNull(profile);
        Assert.Empty(diagnostics);
        Assert.Equal(4, profile.Loops.Single().Segments.Count);
        Assert.Equal(2880d, Math.Abs(Aetheris.Kernel.Firmament.Materializer.ResolvedProfile2DValidator.Validate(profile).SignedArea), 8);
    }

    [Theory]
    [InlineData("Hexagon", "[90mm,64mm]", "firmament-polygon2-variant-unsupported:P:Hexagon")]
    [InlineData("Rhombus", "[90mm,0mm]", "firmament-polygon2-invalid-geometry:P")]
    public void Polygon2_InvalidClosedVariantOrGeometryFailsTyped(string variant, string diagonals, string diagnostic)
    {
        var parse = FirmamentV2Parser.Parse($$"""
            Model InvalidPolygon {
                Units: mm
                Polygon2<{{variant}}> P { Center: [0mm,0mm]; Diagonals: {{diagonals}} }
                Box Base { Size: [20mm,20mm,2mm] }
            }
            """);

        Assert.False(parse.IsSuccess);
        Assert.Contains(diagnostic, parse.Diagnostics);
    }

    private static string Fixture(params string[] parts) =>
        Path.GetFullPath(Path.Combine([AppContext.BaseDirectory, "../../../../fixtures", .. parts]));
}
