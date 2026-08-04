using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2LatticeFillTests
{
    [Fact]
    public void Parse_ExplicitBoxRegionAndOctetFill_PreservesSemanticParameters()
    {
        var parse = FirmamentV2Parser.Parse(Source());

        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var fill = Assert.Single(parse.Document!.LatticeFills!);
        Assert.Equal("Body", fill.Host);
        Assert.Equal("OctetTruss", fill.Pattern);
        Assert.Equal(8d, fill.CellSize);
        Assert.Equal(0.8d, fill.StrutRadius);
        Assert.Equal("Bond", fill.BoundaryPolicy);
        Assert.Equal(new[] { 24d, 24d, 16d }, fill.Region.Size);
    }

    [Fact]
    public void Parse_RejectsUnsupportedMultipleFills()
    {
        var parse = FirmamentV2Parser.Parse(Source() + "\nregion Second { box { size: [8, 8, 8] center: [20, 15, 0] } }\nfill Second { host: Body pattern: OctetTruss { cellSize: 8mm strutRadius: 0.8mm } boundaryPolicy: Bond }");

        Assert.False(parse.IsSuccess);
        Assert.Contains(FirmamentV2Parser.FillMultipleUnsupported, parse.Diagnostics);
    }

    private static string Source() => """
        model LightweightBracket {
          units mm
          template<Additive> PolymerPrototype {
            concept MinimumWallThickness: 1.2mm
            concept MinimumStrutDiameter: 1.0mm
            concept MinimumBondDiameter: 1.2mm
            concept MinimumHoleDiameter: 2.0mm
          }
          solid Body: Box { size: [80, 50, 20] }
          modify Body {
            hole<Shaft> MountHole { on: face(+Z) center: [0, 0] diameter: 12 end: throughAll }
          }
          region LightweightCore {
            box { size: [24mm, 24mm, 16mm] center: [22mm, 0mm, 0mm] }
          }
          fill LightweightCore {
            host: Body
            pattern: OctetTruss { cellSize: 8mm strutRadius: 0.8mm }
            boundaryPolicy: Bond
          }
        }
        """;
}
