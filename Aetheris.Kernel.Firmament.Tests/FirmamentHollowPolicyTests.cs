using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentHollowPolicyTests
{
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
