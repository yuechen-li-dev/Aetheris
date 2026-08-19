using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2CanonicalSafetyAndLabelingTests
{
    [Theory]
    [InlineData("counterbore.firmament", FirmamentV2SemanticHoleVariant.Counterbore)]
    [InlineData("countersink.firmament", FirmamentV2SemanticHoleVariant.Countersink)]
    public void CanonicalHoleFamilies_BindCompleteVariantContracts(string fixture, FirmamentV2SemanticHoleVariant expected)
    {
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(Canonical(fixture)));

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var hole = Assert.Single(Assert.Single(parse.Document!.ModifyBlocks!).SemanticHoles);
        Assert.Equal(expected, hole.Variant);
        Assert.True(expected == FirmamentV2SemanticHoleVariant.Counterbore ? hole.CounterboreDiameter is > 0d && hole.CounterboreDepth is > 0d : hole.CountersinkDiameter is > 0d && hole.CountersinkAngleDegrees is > 0d);
    }

    [Theory]
    [InlineData("Hole<Blind> Pilot { On: +Z Center: Point2(0mm, 0mm) Diameter: 5mm End: ThroughAll }")]
    [InlineData("Hole<Unknown> Pilot { On: +Z Center: Point2(0mm, 0mm) Diameter: 5mm End: ThroughAll }")]
    public void CanonicalUnknownHoleVariant_IsNeverDiscarded(string declaration)
    {
        var parse = FirmamentV2Parser.Parse($"Model Bad {{ Units: mm Box Base {{ Size: [10mm, 10mm, 10mm] }} Modify Base {{ {declaration} }} }}");

        Assert.False(parse.IsSuccess);
        Assert.Contains(FirmamentV2Parser.HoleVariantUnknown, parse.Diagnostics);
    }

    [Theory]
    [InlineData("CounterboreDiameter: 10mm", FirmamentV2Parser.HoleCounterboreInvalid)]
    [InlineData("CounterboreDepth: 3mm", FirmamentV2Parser.HoleCounterboreInvalid)]
    public void CanonicalCounterbore_MissingVariantFields_AreDiagnostics(string fields, string diagnostic)
    {
        var source = $"Model Bad {{ Units: mm Box Base {{ Size: [10mm, 10mm, 10mm] }} Modify Base {{ Hole<Counterbore> Pilot {{ On: +Z Center: Point2(0mm, 0mm) Diameter: 5mm {fields} End: ThroughAll }} }} }}";
        var parse = FirmamentV2Parser.Parse(source);
        Assert.False(parse.IsSuccess);
        Assert.Contains(diagnostic, parse.Diagnostics);
    }

    [Fact]
    public void CanonicalHole_UnknownField_IsNeverDiscarded()
    {
        var parse = FirmamentV2Parser.Parse("""
            Model Bad {
              Units: mm
              Box Base { Size: [10mm, 10mm, 10mm] }
              Modify Base {
                Hole<Shaft> Pilot {
                  On: +Z
                  Center: Point2(0mm, 0mm)
                  Diameter: 5mm
                  Unexpected: 1mm
                  End: ThroughAll
                }
              }
            }
            """);

        Assert.False(parse.IsSuccess);
        Assert.Contains(FirmamentV2Parser.CanonicalFieldUnknown, parse.Diagnostics);
    }

    [Fact]
    public void CanonicalInlineStepRecognitionAndReplacement_UseAnalysisFaceIds()
    {
        var fixture = Canonical("inline-step-recognize-replace.firmament");
        var parse = FirmamentV2Parser.Parse(File.ReadAllText(fixture), Path.GetDirectoryName(fixture));

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var region = Assert.Single(parse.Document!.RecognizedRegions!);
        Assert.Equal("#191", Assert.Single(region.FaceRefs));
        var replacement = Assert.Single(parse.Document.Replacements!);
        Assert.Equal("Source.face(\"#191\")", replacement.PlacementTarget);
    }

    [Theory]
    [InlineData("Recognise Source { }")]
    [InlineData("InlineStepp Source { Path: \"source.step\" }")]
    public void CanonicalDeclarationTypos_AreNeverSilentlyIgnored(string declaration)
    {
        var parse = FirmamentV2Parser.Parse($"Model Bad {{ Units: mm Box Base {{ Size: [10mm, 10mm, 10mm] }} {declaration} }}");
        Assert.False(parse.IsSuccess);
        Assert.Contains(FirmamentV2Parser.CanonicalDeclarationUnrecognized, parse.Diagnostics);
    }

    private static string Canonical(string name)
    {
        var domain = name == "inline-step-recognize-replace.firmament" ? "Integration" : "Features/Holes";
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Canonical", domain, name));
    }
}
