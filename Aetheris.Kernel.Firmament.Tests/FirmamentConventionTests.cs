using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentConventionTests
{
    [Fact]
    public void MechanicalPassPreservesAuthoredDataArgumentsMembersAndOpaqueText()
    {
        const string source = """
            include "My/Part.firmament";
            Record Spec { Width: Length; Material: String; }
            Static Stock: Spec { Width: 10mm; Material: "Steel // From: Size:"; }
            Static Points: Set<Point2> { Start => Point2(0mm,0mm) }
            Template<Width: Length> Struct Panel {
              Box Body { Size: [Width, 8mm, 4mm]; }
              Feature Extra = Make(Size: Stock.Width);
              expose { Semantic BottomSeat { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; } }
            }
            Subassembly Unit {
              <Assembly Unit>
                <Part Panel = Panel<Width:10mm>>
                  Material: Steel with { Appearance: Nickel; };
                  Placement { From: BottomSeat.Frame; To: World; TranslateLocal: [0mm,0mm,4mm]; }
                </Part>
              </Assembly>
              Anchor: Unit;
            }
            // From: Width: Include "leave alone";
            """;
        var once = FirmamentLanguageAnalysisService.FormatConventions(source, "module.firmament", "1");
        Assert.Contains("Record Spec { Width: Length; Material: String; }", once.Text);
        Assert.Contains("Width: 10mm; Material: \"Steel // From: Size:\"", once.Text);
        Assert.Contains("Start => Point2", once.Text);
        Assert.Contains("Template<Width: Length>", once.Text);
        Assert.Contains("Make(Size: Stock.Width)", once.Text);
        Assert.Contains("Panel<Width:10mm>", once.Text);
        Assert.Contains("size: [Width, 8mm, 4mm]", once.Text);
        Assert.Contains("material: Steel with { appearance: Nickel; }", once.Text);
        Assert.Contains("from: BottomSeat.Frame; to: World; translateLocal:", once.Text);
        Assert.Contains("// From: Width: Include \"leave alone\";", once.Text);
        Assert.Equal(once.Text, FirmamentLanguageAnalysisService.FormatConventions(once.Text, "module.firmament", "2").Text);
        Assert.Equal(FirmamentSourceSpelling.Normalize(source), FirmamentSourceSpelling.Normalize(once.Text));
    }

    [Fact]
    public void NativeFormatterAndLowercaseFieldsBindToSamePartWithoutRenaming()
    {
        const string source = "Model Witness { Units: mm; Box Body { Size: [10mm,8mm,4mm] } }";
        var formatted = FirmamentLanguageAnalysisService.Format(source, "m.firmament", "1").Text;
        Assert.Contains("units: mm", formatted);
        Assert.Contains("Box Body", formatted);
        Assert.Contains("size:", formatted);
        var before = FirmamentV2Parser.Parse(source);
        var after = FirmamentV2Parser.Parse(formatted);
        Assert.True(after.IsSuccess, string.Join("\n", after.Diagnostics));
        Assert.Equal(((FirmamentV2BoxRecord)before.Document!.Solids[0].Primitive).Size,
            ((FirmamentV2BoxRecord)after.Document!.Solids[0].Primitive).Size);
        Assert.Equal("Body", after.Document.Solids[0].Name);
        var analysis = FirmamentLanguageAnalysisService.Analyze(formatted, "m.firmament", "1");
        Assert.Contains(analysis.Tokens, t => t.Kind == "field" && formatted.Substring(t.Start, t.Length) == "size");
    }

    [Fact]
    public void LinkAliasesAreScopedAndUserReferencesRemainCaseSensitive()
    {
        const string source = """
            Concept Struct Layout On XY { Point2 Center { Position: [0mm,0mm] } }
            Profile Outline Using Layout { From: Center }
            Pattern Pins Over Layout.Points { point => <Part Pin = Drum<R:1mm>> </Part> }
            Model M { Box On { Size: [1mm,1mm,1mm] } Box B = On; }
            """;
        var text = FirmamentSourceSpelling.Prefer(source);
        Assert.Contains("Layout on XY", text);
        Assert.Contains("Outline using Layout", text);
        Assert.Contains("Pins over Layout.Points", text);
        Assert.Contains("Box On", text);
        Assert.Contains("Box B = On", text);
        Assert.DoesNotContain("Layout.points", text);
    }

    [Fact]
    public void SectionPatternsAndNamedVocabularyCollisionsKeepTheirOwners()
    {
        const string source = """
            Static Stations: Set<Point2> { Start => Point2(0mm,0mm) }
            SectionChain Body {
              Pattern Sections Over Stations {
                station => Section Station {
                  Frame: Plane { Origin: [0mm,0mm,4mm]; Normal: [0,0,1]; Up: [0,1,0] }
                  Profile: Outline;
                  Seam: Start;
                }
              }
            }
            Point2 Start { Position: [0mm,0mm] }
            Interface<Fixed> Seat { Datum: Layout.Plane; Members: [Product.Body.Seat] }
            Mate BodyOnSeat: Seat { Member: Product.Body.Seat; At: Layout.Position; Orientation: SameDirection; }
            """;
        var preferred = FirmamentSourceSpelling.Prefer(source);
        Assert.Contains("Sections over Stations", preferred);
        Assert.Contains("frame: Plane { origin:", preferred);
        Assert.Contains("profile: Outline", preferred);
        Assert.Contains("Point2 Start { position:", preferred);
        Assert.Contains("datum: Layout.Plane; members:", preferred);
        Assert.Contains("member: Product.Body.Seat; at:", preferred);
        Assert.Equal(source, FirmamentSourceSpelling.Normalize(preferred));
    }

    [Fact]
    public void LegacyModelGrammarRemainsWithItsExistingOwner()
    {
        const string source = """
            model Legacy {
              units mm
              solid stock: Box {
                size: [10, 8, 4]
                expose {
                  face(+X) => entrance
                  face(-X) => exit
                }
              }
            }
            """;
        Assert.Equal(source, FirmamentSourceSpelling.Normalize(source));
        Assert.Equal(source, FirmamentSourceSpelling.Prefer(source));
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics));
    }

    [Fact]
    public void UnknownOwnersAndEscapedStringsAreNeverGuessed()
    {
        const string source = "Custom Example { Width: 3mm }\n// Size: 1\nModel M { Units: mm; Note N { Text: \"A \\\" Size: \\\\\"; Target: M; } }";
        var text = FirmamentSourceSpelling.Prefer(source);
        Assert.StartsWith("Custom Example { Width: 3mm }", text);
        Assert.Contains("// Size: 1", text);
        Assert.Contains("\"A \\\" Size: \\\\\"", text);
        const string collision = "Custom Box { Size: [1mm,2mm,3mm] }\nCustom<Length> Box { Size: [1mm,2mm,3mm] }";
        Assert.Equal(collision, FirmamentSourceSpelling.Prefer(collision));
        const string afterTag = "<Assembly Unit><Part Pad = Pad<H:2mm>> Placement { from: Origin; to: World; } </Part></Assembly>";
        Assert.Contains("Placement { From: Origin; To: World; }", FirmamentSourceSpelling.Normalize(afterTag));
    }

    [Fact]
    public void AssemblyPreferredFieldsPreserveOccurrencesAndDefinitionIds()
    {
        const string source = """
            Template<H: Length> Struct Pad {
              Rect2 Outline { Center: [0mm,0mm]; Size: [4mm,2mm] }
              Profile P { Loop Outer { Outline |> TraceLoop } }
              Extrude Body { Profile: P; From: 0mm; To: H }
            }
            Assembly Product {
              <Assembly Product>
                <Part Body = Pad<H:4mm>>
                  Placement { From: Origin; To: World; }
                </Part>
              </Assembly>
              Anchor: Product;
            }
            """;
        var before = new AssemblyM0Parser().Parse(source);
        var after = new AssemblyM0Parser().Parse(FirmamentSourceSpelling.Prefer(source));
        Assert.True(before.IsSuccess, string.Join("\n", before.Diagnostics));
        Assert.True(after.IsSuccess, string.Join("\n", after.Diagnostics));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Source!.Root),
            System.Text.Json.JsonSerializer.Serialize(after.Source!.Root));
    }
}
