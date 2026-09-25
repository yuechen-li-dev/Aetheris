using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentHollowPolicyTests
{
    [Fact]
    public void CylinderHollow_BuildsAndReimportsThroughProductionPath()
    {
        const string source = """
            Struct UtensilHolderBody {
              Cylinder<Hollow> Body {
                Radius: 56mm
                Height: 142mm
                WallThickness: 0.8mm
                Openings: [Top]
              }
            }
            """;
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.True(parsed.IsSuccess, string.Join(", ", parsed.Diagnostics));
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join(", ", built.Diagnostics.Select(x => x.Message)));
        Assert.Equal("Cylinder", built.Value.Hollow!.Primitive);
        Assert.Equal(2, built.Value.Hollow.Cylinders);
        Assert.True(built.Value.Hollow.ReimportedManifold);
    }

    [Fact]
    public void CylinderHollow_RejectsUnloweredPerforationRatherThanExportingPlainBody()
    {
        const string source = """
            Struct UtensilHolder {
              Cylinder<Hollow> Body {
                Radius: 56mm Height: 142mm WallThickness: 0.8mm Openings: [Top]
              }
              Modify Body {
                Perforation Vents { On: +Z; Diameter: 9mm; Layout: Grid; Pitch: 13mm; Margin: 12mm }
              }
            }
            """;
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.False(parsed.IsSuccess);
        Assert.Contains("firmament-v2-hollow-modify-unsupported", parsed.Diagnostics);
    }

    [Fact]
    public void RoundedBoxHollow_ParsesAsConstrainedConstructionPolicy()
    {
        var result = FirmamentV2Parser.Parse("""
            Struct Enclosure {
              RoundedBox<Hollow> Body {
                Size: [120mm, 80mm, 24mm]
                CornerRadius: 12mm
                WallThickness: 2mm
                Openings: [Top]
              }
            }
            """);
        Assert.True(result.IsSuccess, string.Join(", ", result.Diagnostics));
        Assert.Equal(FirmamentV2ConstructionPolicy.Hollow, result.Document!.Solid.ConstructionPolicy);
        Assert.Equal(2, result.Document.Solid.Hollow!.WallThickness);
    }

    [Fact]
    public void HollowPolicy_RejectsUnsupportedWitnessAndMultipleOpenings()
    {
        var unsupported = FirmamentV2Parser.Parse("Struct X { Box<Hollow> B { Size: [1mm,1mm,1mm] WallThickness: 1mm Openings: [Top] } }");
        Assert.Contains(FirmamentV2Parser.PrimitiveDoesNotSatisfyHollowConstructible, unsupported.Diagnostics);
        var openings = FirmamentV2Parser.Parse("Struct X { Frustum<Hollow> B { BottomRadius: 32mm TopRadius: 43mm Height: 90mm WallThickness: 2mm Openings: [Top, Bottom] } }");
        Assert.Contains(FirmamentV2Parser.MultipleOpeningsNotSupported, openings.Diagnostics);
    }
}
