using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentMechanicalFrontendMigrationTests
{
    [Theory]
    [InlineData("unknown-compose-statement", "Fillet")]
    [InlineData("unknown-boundary-field", "heigth")]
    public void LostIntent_RejectsInBothProductionPaths_AtOriginalToken(string fixture, string token)
    {
        var source = Read("FrontendX0/" + fixture);
        var parsed = FirmamentV2Parser.Parse(source);
        var report = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(parsed, "intent.firmament"), source);
        Assert.False(parsed.IsSuccess);
        Assert.Equal("invalid", report.Status);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        var diagnostic = Assert.Single(report.Diagnostics.Where(d => d.Code.EndsWith(":" + token, StringComparison.Ordinal)));
        Assert.Equal(token, source.Substring(diagnostic.SourceSpan!.Start, diagnostic.SourceSpan.Length));
        Assert.Equal("intent.firmament", diagnostic.SourceFile);
    }

    [Theory]
    [InlineData("Box Body { Size: [40mm,20mm,6mm]; heigth: 8mm }", "Box:heigth")]
    [InlineData("Box Body On XZ { Size: [40mm,20mm,6mm] }", "placement-unsupported")]
    [InlineData("Box Body { Size: [40mm,20mm,6mm] } Slot2<Unknown> unused { Center: [0mm,0mm]; Length: 12mm; Width: 3mm }", "variant-unsupported")]
    [InlineData("Box Body { Size: [40mm,20mm,6mm] } Modify Body { Fillet Lost { Radius: 1mm } }", "Modify:Fillet")]
    [InlineData("Box Body { Size: [40mm,20mm,6mm]; Size: [4mm,2mm,1mm] }", "field-duplicate:Box:Size")]
    [InlineData("Box Body { Size: [40mm,20mm,6mm] } Compose Other { }", "material-owners-unsupported")]
    [InlineData("Box Body { Size: [40mm,20mm,6mm] } Slot2 unused { Center: [0mm,0mm]; Length: 12mm; Width: 3mm; typo: ; }", "firmament-syntax-expression")]
    public void AstOwner_RejectsUnknownOrContradictoryIntent(string body, string expected)
    {
        var source = "Model Test { Units: mm " + body + " }";
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Contains(expected, StringComparison.Ordinal));
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Theory]
    [InlineData("Static unused = 4mm")]
    [InlineData("Rect2 unusedGuide { Center: [0mm,0mm]; Size: [12mm,3mm] } Profile unused { Loop Outer { unusedGuide.Bottom |> unusedGuide.Right |> unusedGuide.Top |> unusedGuide.Left |> Close } }")]
    [InlineData("Slot2 unused { center: [0mm,0mm]; length: 12mm; width: 3mm }")]
    public void UnusedConstruction_CannotSelectAnotherMechanicalGrammar(string unused)
    {
        const string stock = "Model Test { Units: mm Box Body { Size: [40mm,20mm,6mm] } }";
        var changed = stock.Insert(stock.LastIndexOf('}'), unused);
        var before = FirmamentBuildAndExport.CompileSource(stock);
        var after = FirmamentBuildAndExport.CompileSource(changed);
        Assert.True(before.IsSuccess);
        Assert.True(after.IsSuccess, string.Join("; ", after.Diagnostics.Select(d => d.Message)));
        Assert.Equal(before.Value.StepText, after.Value.StepText);
        Assert.Equal("UnifiedMechanical", FirmamentV2Parser.Parse(changed).ParserRoute);
        var body = Step242Importer.ImportBody(after.Value.StepText);
        Assert.True(body.IsSuccess);
        Assert.Equal(6, body.Value.Topology.Faces.Count());
    }

    [Fact]
    public void ProfileSelections_RetainTheirAdmittedMemberSet_AndBuild()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../fixtures/Regression/CanonicalGeometry/profile-chamfer-reflex-junction-top.firmament"));
        var source = File.ReadAllText(path);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Diagnostics));
        Assert.Equal("UnifiedMechanical", parsed.ParserRoute);
        var selection = Assert.Single(parsed.Document!.Selections!);
        Assert.Contains(".[", selection.Source, StringComparison.Ordinal);
        var report = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(parsed), source);
        Assert.Equal("valid", report.Status);
        Assert.Equal("Built", report.ValidationLevel);
        Assert.True(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void StructAndModelStock_UseTheSameAstBinderAndGeometry()
    {
        const string stock = "Model Test { Units: mm Box Body { Size: [40mm,20mm,6mm] } }";
        var alternative = stock.Replace("Model", "Struct", StringComparison.Ordinal);
        Assert.Equal("UnifiedMechanical", FirmamentV2Parser.Parse(alternative).ParserRoute);
        Assert.Equal(FirmamentBuildAndExport.CompileSource(stock).Value.StepText,
            FirmamentBuildAndExport.CompileSource(alternative).Value.StepText);
    }

    [Fact]
    public void SyntaxFailure_HasOriginalLocation_AfterComments()
    {
        const string source = "// retained provenance\nModel Test { Units: mm\nBox Body { Size: ; }\n}";
        var report = FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source), "broken.firmament");
        Assert.Equal("invalid", report.Status);
        var diagnostic = Assert.Single(report.Diagnostics, d => d.Code == "firmament-syntax-expression");
        Assert.Equal(3, diagnostic.Line);
        Assert.Equal(";", source.Substring(diagnostic.SourceSpan!.Start, diagnostic.SourceSpan.Length));
        Assert.Contains("expression", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingStaticMatch_IsStructured_BeforeExistingSemanticEvaluation()
    {
        var syntax = FirmamentSyntaxParser.Parse(FirmamentV2StaticLogicTests.Source());
        Assert.True(syntax.IsSuccess, string.Join("; ", syntax.Diagnostics.Select(d => d.Message)));
        var design = Assert.Single(FirmamentSyntaxEvidence.Declarations(syntax.Document), d => d.Name?.Text == "BracketConcept");
        Assert.IsType<MatchSyntax>(Assert.Single(design.Body!.Statements.OfType<FieldSyntax>(), f => f.Name.Text == "ChamferDistance").Value);
        Assert.True(FirmamentV2Parser.Parse(FirmamentV2StaticLogicTests.Source()).IsSuccess);
    }

    [Theory]
    [InlineData("    Box Base {", "    Box Base { heigth: 99mm;", "Box:heigth")]
    [InlineData("    Modify Base {", "    Modify Base { Fillet Lost { Radius: 1mm }", "Modify:Fillet")]
    public void ConceptValuedStock_CannotBypassMechanicalConsumption(string before, string after, string expected)
    {
        var source = FirmamentV2StaticLogicTests.Source().Replace(before, after, StringComparison.Ordinal);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Contains(expected, StringComparison.Ordinal));
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void EveryExtrusion_ContributesToTheBoundEngineeringBody()
    {
        const string source = """
            Model Stack {
                Units: mm
                Rect2 guide { Center: [0mm,0mm]; Size: [40mm,20mm] }
                Profile section { Loop Outer { guide.Bottom |> guide.Right |> guide.Top |> guide.Left |> Close } }
                Extrude bottom { Profile: section; From: 0mm; To: 2mm }
                Extrude upper { Profile: section; From: 2mm; To: 8mm }
            }
            """;
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Diagnostics));
        Assert.Equal(["bottom", "upper"], Assert.Single(parsed.Document!.Composes!).Operations);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(built.Value.StepText);
        Assert.True(imported.IsSuccess);
        var points = imported.Value.Topology.Vertices.Select(v => { Assert.True(imported.Value.TryGetVertexPoint(v.Id, out var p)); return p; }).ToArray();
        Assert.Equal(0d, points.Min(p => p.Z), 8);
        Assert.Equal(8d, points.Max(p => p.Z), 8);
    }

    [Theory]
    [InlineData("Width: 40mm")]
    [InlineData("Extent: ThroughAll")]
    public void BoundedFeatureReaders_CannotDiscardAnExpressionSuffix(string field)
    {
        var source = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../fixtures/Canonical/Features/Slots/straight-slot.firmament")));
        Assert.True(FirmamentV2Parser.Parse(source).IsSuccess);
        var invalid = source.Replace(field, field + " + 1mm", StringComparison.Ordinal);
        Assert.NotEqual(source, invalid);
        Assert.False(FirmamentV2Parser.Parse(invalid).IsSuccess);
        Assert.False(FirmamentBuildAndExport.CompileSource(invalid).IsSuccess);
    }

    private static string Read(string relative) => File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
        "../../../../fixtures/Regression", relative + ".firmament")));
}
