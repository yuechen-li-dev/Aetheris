using Aetheris.Kernel.Firmament.FirmamentV2;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalProfileM2Tests
{
    [Fact]
    public void EdgeProgram_RejectsCrossKindOverlapBeforeBrepLoweringAndNamesBothFragments()
    {
        const string source = """
            Concept Struct Layout {
                Tab MountTab { On: Lip.Outer; Center: 50mm; Width: 30mm; Extension: 5mm; }
                SteppedNotch CableNotch {
                    On: Lip.Outer; Center: 60mm; Width: 20mm; Depth: 8mm;
                    ShoulderDepth: 4mm; OuterChamfer: 1mm; InnerChamfer: 1mm; Side: Inward;
                }
            }
            SheetMetal Conflict {
                Thickness: 1mm;
                Base Panel { Profile: Rectangle { Width: 100mm; Height: 60mm; }; }
                Flange Lip { From: Panel.Rear; Height: 15mm; Angle: 90deg; Radius: 2mm; }
            }
            """;

        var result = SheetMetalFirmament.Compile(source);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics, x => x.Code == "sheetmetal-edge-profile-invalid");
        Assert.Contains("Layout.MountTab", diagnostic.Message);
        Assert.Contains("Layout.CableNotch", diagnostic.Message);
        Assert.Null(result.Part);
    }

    [Fact]
    public void AdjacentFragments_DoNotCreateZeroLengthCarrier()
    {
        var first = new SemanticEdgeTabIr("A", "Edge.A", new(SemanticEdgeAnchorKind.FromStart, 10), 10, 3, 1, "test");
        var second = new SemanticEdgeNotchIr("B", "Edge.B", new(SemanticEdgeAnchorKind.FromStart, 20), 10, 3, -1, "test");

        var result = SemanticEdgeProfileResolver.Resolve(new("Plate.Bottom", "Edge", new(0, 0), new(40, 0), [second, first], "u/v", "test"));

        Assert.True(result.IsSuccess);
        Assert.Equal(["Carrier", "Tab", "Notch", "Carrier"], result.Profile!.OrderedMembers.Select(x => x.Kind));
        Assert.All(result.Profile.OrderedMembers, x => Assert.True(x.EndU - x.StartU > 1e-8));
    }
}
