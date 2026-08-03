using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentV2ConceptPoint3HoleTests
{
    [Fact]
    public void ConceptPoint3_LowersThroughSemanticHoleAirWithTypedPlacementProvenance()
    {
        var parse = FirmamentV2Parser.Parse(Source);
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        var holes = Assert.Single(parse.Document!.ModifyBlocks!).SemanticHoles;
        Assert.Equal(2, holes.Count);
        Assert.Equal([-30d, 30d], holes.Select(h => h.Center.U));
        Assert.Equal([0, 1], holes.Select(h => h.ResolvedCenter!.Ordinal));
        Assert.Equal(["concept:BracketConcept.MountPoints[0]", "concept:BracketConcept.MountPoints[1]"], holes.Select(h => h.ResolvedCenter!.StableId));
        Assert.All(holes, h =>
        {
            Assert.Equal(25d, h.ResolvedCenter!.Z);
            Assert.Equal(0d, h.ResolvedCenter.PlaneDistance);
            Assert.Equal("Base.Top", h.ResolvedCenter.PlacementFace);
        });

        var air = FirmamentV2SemanticHoleLowering.LowerSemanticHoles(parse.Document);
        Assert.Equal(2, air.Count);
        Assert.All(air, feature =>
        {
            Assert.Equal(AirHoleStackKind.SimpleShaft, feature.Stack.Kind);
            Assert.Equal(8.5d, feature.Shaft.Diameter);
            Assert.NotNull(feature.Placement.ResolvedPoint3);
            Assert.Equal("CONCEPT-MATERIALIZATION-M2", feature.Provenance.Milestone);
            Assert.Contains(feature.Provenance.Notes, n => n.StartsWith("center-stable-id:concept:BracketConcept.MountPoints[", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void ConceptPoint3_NotOnSelectedPlane_IsRejectedWithoutProjection()
    {
        var source = Source.Replace("Within: Bounds.Face(+Z).Inset(10mm)", "Within: Bounds.Face(-Z).Inset(10mm)", StringComparison.Ordinal);
        var parse = FirmamentV2Parser.Parse(source);
        Assert.False(parse.IsSuccess);
        Assert.Contains(parse.Diagnostics, d => d.StartsWith(ConceptIrResolver.PointNotOnPlacementPlane + ":BracketConcept.MountPoints[0]", StringComparison.Ordinal));
    }

    internal const string Source = """
        Concept MountingFrame {
            Bounds: Box3
            TopPlane: Plane
            CenterAxis: Axis
            MountPoints: Point3[]
        }
        Concept Struct BracketConcept: MountingFrame {
            Bounds: Box3 { Size: [80mm, 50mm, 25mm] }
            TopPlane: Bounds.Face(+Z)
            CenterAxis: Bounds.Center.Axis(+Z)
            MountPoints: Grid {
                Within: Bounds.Face(+Z).Inset(10mm)
                Columns: 2
                Rows: 1
            }
        }
        Struct Bracket: MountingFrame {
            Box Base { Bounds: BracketConcept.Bounds }
            Modify Base {
                hole<shaft> LeftMount {
                    on: Base.Top
                    center: BracketConcept.MountPoints[0]
                    diameter: 8.5mm
                    end: throughAll
                }
                hole<shaft> RightMount {
                    on: Base.Top
                    center: BracketConcept.MountPoints[1]
                    diameter: 8.5mm
                    end: throughAll
                }
            }
            Expose {
                Bounds: BracketConcept.Bounds
                TopPlane: Base.Top
                CenterAxis: BracketConcept.CenterAxis
                MountPoints: BracketConcept.MountPoints
            }
        }
        """;
}
