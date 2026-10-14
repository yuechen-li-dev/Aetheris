using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentDomainFrontendMigrationTests
{
    [Theory]
    [InlineData("Gear", "heigth: 10mm")]
    [InlineData("Gear", "Module: 2mm")]
    [InlineData("Gear", "Fillet Lost { Radius: 1mm }")]
    [InlineData("Revolve", "heigth: 10mm")]
    [InlineData("Revolve", "Angle: half")]
    [InlineData("Revolve", "Fillet Lost { Radius: 1mm }")]
    [InlineData("Panel", "heigth: 10mm")]
    [InlineData("Panel", "Thickness: 2mm; Thickness: 3mm")]
    [InlineData("Panel", "Fillet Lost { Radius: 1mm }")]
    public void UnconsumedDomainIntentRejectsParsingValidationAndBuild(string kind, string intent)
    {
        var source = Source(kind).Replace("// intent", intent, StringComparison.Ordinal);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.StartsWith("Unified", parsed.ParserRoute);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(build.IsSuccess);
        var validation = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(parsed, sourceText: source), source);
        Assert.Equal("invalid", validation.Status);
        Assert.Contains(validation.Diagnostics, d => d.Severity == "fatal" && d.SourceSpan is not null);
    }

    [Theory]
    [InlineData("Gear", "Module: 2mm", "Module: 2mm + 1")]
    [InlineData("Gear", "Axis: [0,0,1]", "Axis: Unknown")]
    [InlineData("Gear", "Teeth: 24", "Teeth: 24.5")]
    [InlineData("Revolve", "Angle: full", "Angle: full + 1mm")]
    [InlineData("Revolve", "Revolve Turn", "Revolve Turn On XZ")]
    [InlineData("Revolve", "Direction: [0,1,0]", "Direction: [0,1,0]; rotaton: 90deg")]
    [InlineData("Panel", "Rise: 6mm", "Rise: 6mm; heigth: 1mm")]
    [InlineData("Panel", "// intent", "Orientation: 2mm")]
    public void DomainValuesAndFramesCannotSilentlyDefault(string kind, string before, string after)
    {
        var source = Source(kind).Replace(before, after, StringComparison.Ordinal);
        Assert.False(FirmamentV2Parser.Parse(source).IsSuccess);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void WrapperFreeRevolveRetainsAdmissionAndIdenticalGeometry()
    {
        var wrapped = Source("Revolve");
        var standalone = wrapped.Replace("Model Ring { Units: mm;", "", StringComparison.Ordinal);
        standalone = standalone[..standalone.LastIndexOf('}')];
        var first = FirmamentBuildAndExport.CompileSource(wrapped);
        var second = FirmamentBuildAndExport.CompileSource(standalone);
        Assert.True(first.IsSuccess, string.Join("\n", first.Diagnostics));
        Assert.True(second.IsSuccess, string.Join("\n", second.Diagnostics));
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.True(second.Value.Revolve!.StepReimportedManifold);
        Assert.Equal("UnifiedRevolve", FirmamentV2Parser.Parse(standalone).ParserRoute);
        Assert.False(FirmamentBuildAndExport.CompileSource(standalone + "\nRevolve Other { Profile: Section; About: Axis; Angle: half }").IsSuccess);
    }

    [Fact]
    public void GearDimensionalExpressionsAndFieldAliasesRetainGeometry()
    {
        var source = Source("Gear");
        var equivalent = source.Replace("Module: 2mm", "module: 1mm * 2", StringComparison.Ordinal);
        var first = FirmamentBuildAndExport.CompileSource(source); var second = FirmamentBuildAndExport.CompileSource(equivalent);
        Assert.True(first.IsSuccess); Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Equal("UnifiedGear", FirmamentV2Parser.Parse(equivalent).ParserRoute);
    }

    [Fact]
    public void MultipleGearDefinitionsAreInspectableButNormalValidationDoesNotPromiseSingleBodyBuild()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/GearInterfaces/spur-pair.firmament");
        Assert.True(GearAuthoring.Parse(source).IsSuccess);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess);
        var report = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(parsed, sourceText: source), source);
        Assert.Equal("invalid", report.Status); Assert.Equal("BuildRejected", report.ValidationLevel);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        Assert.Contains(report.Diagnostics, d => d.Message.Contains("use-Assembly", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("solid-loft-common-ray.firmament", "Reference: [1, 0, 0]", "Reference: [1, 0, 0]; heigth: 1mm")]
    [InlineData("solid-loft-common-ray.firmament", "Reference: [1, 0, 0]", "Reference: [1, 0, 0]; Fillet Lost { Radius: 1mm }")]
    [InlineData("solid-loft-common-ray.firmament", "Rule: Ruled", "Rule: Ruled; Rule: Other")]
    [InlineData("solid-loft-common-ray.firmament", "Loft Body", "Loft<policy: Hollow> Body")]
    [InlineData("two-section-ruled.firmament", "Transition: Ruled", "Transition: Ruled; heigth: 1mm")]
    [InlineData("two-section-ruled.firmament", "Transition: Ruled", "Transition: 1mm")]
    [InlineData("two-section-ruled.firmament", "Start: Cap", "Start: 1mm")]
    [InlineData("two-section-ruled.firmament", "Seam: Bottom", "Seam: 1mm")]
    [InlineData("two-section-ruled.firmament", "Seam: Bottom", "Seam: Bottom; heigth: 1mm")]
    [InlineData("two-section-ruled.firmament", "Seam: Bottom", "Seam: Bottom; Fillet Lost { Radius: 1mm }")]
    public void SectionOwnersConsumeAllFieldsAndStatements(string file, string before, string after)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/SectionChain/" + file).Replace(before, after, StringComparison.Ordinal);
        Assert.False(SectionChainAuthoringParser.Compile(source, materialize: false).IsSuccess);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess); Assert.Equal("UnifiedSectionChain", parsed.ParserRoute);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void LegacyUnitHeaderUsesMechanicalAstAndRetainsExactStep()
    {
        var header = FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/FrontendMigration/legacy-unit-header.firmament");
        var explicitBody = "Model Old { Units: mm; " + header[(header.IndexOf('\n') + 1)..] + " }";
        var first = FirmamentBuildAndExport.CompileSource(header); var second = FirmamentBuildAndExport.CompileSource(explicitBody);
        Assert.True(first.IsSuccess); Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Equal("UnifiedMechanical", FirmamentV2Parser.Parse(header).ParserRoute);
        Assert.False(FirmamentBuildAndExport.CompileSource(header + "\nFillet Lost { Radius: 1mm }").IsSuccess);
    }

    [Fact]
    public void NamedSurfaceSectionSyntaxPreservesIdentityWithoutCapturingWith()
    {
        var syntax = FirmamentSyntaxParser.Parse(FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/FrontendMigration/named-surface-section-syntax.firmament"));
        Assert.True(syntax.IsSuccess, string.Join("\n", syntax.Diagnostics));
        var items = Assert.IsType<SequenceSyntax>(Assert.Single(syntax.Document.Statements.OfType<DeclarationSyntax>()).Initializer).Items;
        Assert.Equal(["S0", "S1"], items.Select(i => Assert.IsType<ObjectSyntax>(i.Value).Name!.Text));
        Assert.IsType<WithSyntax>(FirmamentSyntaxParser.ParseExpression("Base with { Size: [1mm,2mm,3mm] }").Expression);
    }

    [Fact]
    public void SurfaceExpressionMigrationPreservesWorldPointsAndBoundaryIdentities()
    {
        var parsed = FirmamentV2Parser.Parse(FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/FrontendMigration/panel-parametric.firmament"));
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        var panel = Assert.Single(parsed.Document!.Panels!);
        Assert.Equal("panel:Saddle", panel.StableId);
        Assert.Equal("Back", panel.Orientation.MaterialSide.ToString());
        Assert.Equal(["West", "North", "East", "South"], panel.BoundaryEdges.Select(edge => edge.Name));
        foreach (var u in new[] { -1d, 0d, 1d })
        foreach (var v in new[] { -1d, 0d, 1d })
        {
            var point = panel.AuthoredPatch.EvaluatePoint(u, v);
            Assert.Equal(point, panel.SurfaceConstruction.Evaluate((u + 1) / 2, (v + 1) / 2));
            Assert.Equal(20 * u, point.X, 9);
            Assert.Equal(15 * v, point.Y, 9);
            Assert.Equal(6 * u * v, point.Z, 9);
        }
    }

    [Fact]
    public void GearDefinitionExtractionLeavesAssemblyRequirementsWithTheirOwner()
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/AssemblyInterfaces/gear-with-ordinary-part.firmament")
            + "\nInterface<Axial> OptionalCapability { Role A requires AxisCapable; Role B requires AxisCapable; }";
        var definitions = GearAuthoring.ParseDefinitions(source);
        Assert.True(definitions.IsSuccess, string.Join("\n", definitions.Diagnostics));
        Assert.Single(definitions.Gears);
        var assembly = new Aetheris.Kernel.Firmament.Assembly.AssemblyM0Parser().Parse(source);
        Assert.True(assembly.IsSuccess, string.Join("\n", assembly.Diagnostics));
        Assert.False(GearAuthoring.Parse(source).IsSuccess);
    }

    [Theory]
    [InlineData("Gear", "GearModel")]
    [InlineData("Panel", "PanelModel")]
    public void CompileTimeDefinitionsDoNotBecomeTheDisplayedModelOwner(string kind, string owner)
    {
        var source = "Concept Struct ConstructionData { Origin: Point3(0mm,0mm,0mm) }\n" + Source(kind);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
        Assert.Equal(owner, parsed.Document!.ModelName);
        if (kind == "Gear")
        {
            var built = FirmamentBuildAndExport.CompileSource(source);
            Assert.True(built.IsSuccess, string.Join("\n", built.Diagnostics));
            Assert.Equal(FirmamentBuildAndExport.CompileSource(Source(kind)).Value.StepText, built.Value.StepText);
        }
    }

    [Theory]
    [InlineData("WireForm")]
    [InlineData("Sweep")]
    [InlineData("Plateau")]
    public void AdmittedAstCannotBeReroutedByConstructTextInComments(string domain)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/FrontendMigration/incidental-domain-text.firmament").Replace("$domain", domain, StringComparison.Ordinal);
        var plain = source[..source.IndexOf("/*", StringComparison.Ordinal)];
        var first = FirmamentBuildAndExport.CompileSource(plain); var second = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(first.IsSuccess, string.Join("\n", first.Diagnostics));
        Assert.True(second.IsSuccess, string.Join("\n", second.Diagnostics));
        Assert.Equal("UnifiedMechanical", FirmamentV2Parser.Parse(source).ParserRoute);
        Assert.Equal(first.Value.StepText, second.Value.StepText);
    }

    [Theory]
    [InlineData("Width: 2mm", "Width: 2mm; heigth: 1mm")]
    [InlineData("Width: 2mm", "Width: 2mm; Fillet Lost { Radius: 1mm }")]
    [InlineData("Plateau Raised", "Plateau Raised On XZ")]
    public void PlateauIntentIsBoundBeforeItsAdmittedBaseCanExecute(string before, string after)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText("fixtures/Canonical/Surfacing/g2-planar-plateau.firmament").Replace(before, after, StringComparison.Ordinal);
        Assert.False(FirmamentV2Parser.Parse(source).IsSuccess);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    private static string Source(string kind) => FirmamentCorpusHarness.ReadFixtureText("fixtures/Regression/FrontendMigration/" + kind.ToLowerInvariant() + ".firmament");
}
