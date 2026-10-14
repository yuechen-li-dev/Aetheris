using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentCompatibilityAstMigrationTests
{
    [Theory]
    [InlineData("Box", "size: [30,24,12]", "size: [30mm,24mm,12mm]")]
    [InlineData("Cylinder", "radius: 4 height: 12", "radius: 4mm height: 12mm")]
    [InlineData("Cone", "bottomRadius: 4 topRadius: 2 height: 12", "bottomRadius: 4mm topRadius: 2mm height: 12mm")]
    [InlineData("Sphere", "radius: 4", "radius: 4mm")]
    [InlineData("Torus", "majorRadius: 8 minorRadius: 2", "majorRadius: 8mm minorRadius: 2mm")]
    public void CompatibilitySolids_UseNativeBindingAndPreserveStep(string type, string legacyFields, string fields)
    {
        var legacy = $"model Witness {{ units mm solid stock: {type} {{ {legacyFields} }} }}";
        var native = $"Model Witness {{ units: mm {type} stock {{ {fields} }} }}";
        var syntax = FirmamentSyntaxParser.Parse(legacy);
        Assert.True(syntax.IsSuccess);
        var parsed = FirmamentV2Parser.Parse(legacy);
        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Diagnostics));
        Assert.Equal("UnifiedCompatibility", parsed.ParserRoute);
        var built = FirmamentBuildAndExport.CompileSource(legacy);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        Assert.Equal(FirmamentBuildAndExport.CompileSource(native).Value.StepText, built.Value.StepText);
        Assert.Equal("valid", FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(parsed), legacy).Status);
    }

    [Theory]
    [InlineData("solid stock: Box { size: [30,24,12] } Use Ignored.Module;", "Use")]
    [InlineData("solid stock: Box { size: [30,24,12] heigth: 20 }", "heigth")]
    [InlineData("solid stock: Box { size: [30,24,12] Fillet lost { radius: 1 } }", "Fillet")]
    [InlineData("solid stock: Box { size: [30,24,12] } Fillet lost { radius: 1 }", "Fillet")]
    [InlineData("solid stock: Box { size: [30,24,12] } modify stock { Fillet lost { radius: 1 } }", "Fillet")]
    [InlineData("solid stock: Box { size: [30,24,12] } modify stock { hole<shaft> bore { on: face(+Z) center: [0,0] diameter: 4 end: throughAll heigth: 2 } }", "heigth")]
    public void UnknownIntent_FailsBothPathsWithOriginalLocation(string body, string token)
    {
        var source = "model Witness { units mm " + body + " }";
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Equal("UnifiedCompatibility", parsed.ParserRoute);
        var report = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(parsed, "compat.firmament"), source);
        Assert.Equal("invalid", report.Status);
        Assert.Contains(report.Diagnostics, d => d.SourceSpan is { } span && source.Substring(span.Start, span.Length) == token);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Theory]
    [InlineData("modify stock = 4 { }")]
    [InlineData("modify stock -> Box { }")]
    [InlineData("modify stock { hole<kind: Shaft> bore { on: face(+Z) center: [0,0] diameter: 4 end: throughAll } }")]
    public void UnsupportedHeaders_CannotBeDiscarded(string modification)
    {
        var source = "model Witness { units mm solid stock: Box { size: [30,24,12] } " + modification + " }";
        Assert.False(FirmamentV2Parser.Parse(source).IsSuccess);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Theory]
    [InlineData("radius: 1 center: [0,0] through: face(-X) heigth: 20")]
    [InlineData("radius: 1 center: [0,0] through: face(-X) Fillet lost { radius: 1 }")]
    public void RegionCut_ClosesItsActualFields(string body)
    {
        var source = "model Witness { units mm solid stock: Box { size: [30,24,12] } modify stock { region sideHole on face(+X) { cut Cylinder { " + body + " } } } }";
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Equal("UnifiedCompatibility", parsed.ParserRoute);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void IncidentalConceptText_CannotChooseAnotherFrontend()
    {
        const string source = "model Witness { units mm solid stock: Box { size: [30,24,12] } note: \"Concept Struct Ghost\" }";
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Equal("UnifiedCompatibility", parsed.ParserRoute);
        Assert.Contains(parsed.Diagnostics, d => d.EndsWith(":Model:note", StringComparison.Ordinal));
    }

    [Fact]
    public void DerivedRecords_RemainImmutableAndCloseTheirFields()
    {
        const string source = "model Witness { units mm solid stock: Box { size: [10,8,6] } solid tall: stock with { size: [10,8,12] } }";
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Diagnostics));
        Assert.Equal([10d, 8d, 6d], parsed.Document!.Solids[0].Box!.Size);
        Assert.Equal([10d, 8d, 12d], parsed.Document.Solids[1].Box!.Size);
        Assert.Equal("stock", parsed.Document.Solids[1].DerivedFrom);
        Assert.False(FirmamentV2Parser.Parse(source.Replace("size: [10,8,12]", "size: [10,8,12] heigth: 20")).IsSuccess);
    }

    [Fact]
    public void LowercaseAlias_PreservesProfileIdentityAndGeometry()
    {
        const string source = """
            Model Witness {
              units: mm
              Rect2 guide { center: [0mm,0mm]; size: [12mm,8mm] }
              Profile outline { guide.Bottom as south |> guide.Right as east |> guide.Top as north |> guide.Left as west |> Close }
              Extrude stock { profile: outline; from: 0mm; to: 4mm }
            }
            """;
        var syntax = FirmamentSyntaxParser.Parse(source);
        Assert.True(syntax.IsSuccess);
        Assert.Equal(["south", "east", "north", "west"], FirmamentSyntaxTraversal.Expressions(syntax.Document).OfType<AliasSyntax>().Select(a => a.Name.Text));
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Diagnostics));
        var lower = FirmamentBuildAndExport.CompileSource(source);
        var upper = FirmamentBuildAndExport.CompileSource(source.Replace(" as ", " As "));
        Assert.True(lower.IsSuccess, string.Join("; ", lower.Diagnostics.Select(d => d.Message)));
        Assert.Equal(upper.Value.StepText, lower.Value.StepText);
        Assert.Equal(source, FirmamentSourceSpelling.Prefer(source.Replace(" as ", " As ")));
        Assert.Equal(FirmamentSourceSpelling.Normalize(source), FirmamentSourceSpelling.Normalize(FirmamentSourceSpelling.Prefer(source)));
    }

    [Fact]
    public void DepthParameter_RemainsAnOrdinaryValueOutsideLegacyEnd()
    {
        const string source = "Feature Make(depth: Length) -> Boss { return Boss { size: [10mm, depth]; height: depth - 1mm } }";
        var syntax = FirmamentSyntaxParser.Parse(source);
        Assert.True(syntax.IsSuccess, string.Join("; ", syntax.Diagnostics.Select(d => d.Message)));
        var values = FirmamentSyntaxTraversal.Expressions(syntax.Document).OfType<ReferenceSyntax>().Where(r => r.Name.Text == "depth").ToArray();
        Assert.Equal(2, values.Length);
        Assert.DoesNotContain(FirmamentSyntaxTraversal.Expressions(syntax.Document), e => e is CallSyntax { Target: ReferenceSyntax { Name.Text: "depth" } });
    }

    [Fact]
    public void AliasFormatting_NeverRenamesAuthoredAsSymbols()
    {
        const string source = "Model As { units: mm Box as { size: [10mm,8mm,6mm] } }\nRecord Spec { As: Length }\nStatic data: Spec { As: 2mm }\n// guide As alias\n";
        Assert.Equal(source, FirmamentSourceSpelling.Prefer(source));
    }

    [Theory]
    [InlineData("=>")]
    [InlineData("as")]
    public void ExposureAliases_PreserveFaceAndHoleIntent(string link)
    {
        var source = $"model Witness {{ units mm solid stock: Box {{ size: [30,24,12] expose {{ face(+Z) {link} top }} }} modify stock {{ hole<shaft> bore {{ on: top center: [0,0] diameter: 4 end: throughAll }} }} }}";
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Diagnostics));
        Assert.Equal("top", Assert.Single(parsed.Document!.Solid.Box!.Exposures).Alias);
        Assert.Equal("+Z", Assert.Single(Assert.Single(parsed.Document.ModifyBlocks!).SemanticHoles).EntryFace.Axis);
        Assert.True(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }
}
