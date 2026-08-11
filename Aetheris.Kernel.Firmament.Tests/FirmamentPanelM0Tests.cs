using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Surfacing;
using Xunit;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentPanelM0Tests
{
    [Theory]
    [MemberData(nameof(Sources))]
    public void OrdinaryFirmamentAuthorsSupportedSurfacingConstructions(string expectedKind,string panelSource)
    {
        var source=$"Model PanelModel {{ Units: mm; {panelSource} }}";
        var result=FirmamentV2Parser.Parse(source);
        Assert.True(result.IsSuccess,string.Join(Environment.NewLine,result.Diagnostics));
        var panel=Assert.Single(result.Document!.Panels!);
        Assert.Equal(expectedKind,panel.SurfaceConstruction.Kind.ToString());
        Assert.Equal(["South","East","North","West"],panel.BoundaryEdges.Select(edge=>edge.Name));
    }

    [Fact]
    public void ParametricEquationPanelBindsDomainOrientationAndMetadata()
    {
        var result=FirmamentV2Parser.Parse("""
            Model Equations {
              Units: mm;
              Panel Saddle {
                Surface: ParametricSurface {
                  DomainU: [-1, 1]; DomainV: [-1, 1];
                  X: 20mm * u; Y: 15mm * v; Z: 6mm * u * v;
                }
                Orientation: Back; Thickness: 1.2mm; Material: "Aluminum";
              }
            }
            """);
        Assert.True(result.IsSuccess,string.Join(Environment.NewLine,result.Diagnostics));
        var panel=Assert.Single(result.Document!.Panels!);Assert.Equal(PanelMaterialSide.Back,panel.Orientation.MaterialSide);Assert.Equal(1.2,panel.Thickness);Assert.Equal("Aluminum",panel.Material);
    }

    [Fact]
    public void RecordTemplateSpecializesToPanelBeforeSurfacingBridge()
    {
        var result=FirmamentV2Parser.Parse("""
            Model GeneratedCanopy {
              Units: mm;
              Record CanopySpec { Width: Length; Depth: Length; Rise: Length; }
              Static CanopyValues: CanopySpec = CanopySpec { Width: 70mm; Depth: 36mm; Rise: 9mm; }
              Template < Spec: CanopySpec > Panel RuledCanopy {
                Surface: RuledSurface {
                  BoundaryA: Line { Start: [-35mm, -18mm, 9mm]; End: [35mm, -18mm, -9mm]; }
                  BoundaryB: Line { Start: [-35mm, 18mm, -9mm]; End: [35mm, 18mm, 9mm]; }
                }
              }
              Panel Canopy = RuledCanopy < Spec: CanopyValues >
            }
            """);
        Assert.True(result.IsSuccess,string.Join(Environment.NewLine,result.Diagnostics));
        Assert.Equal("panel:Canopy",Assert.Single(result.Document!.Panels!).StableId);
        Assert.Single(result.Document.TemplateInstantiations!);
    }

    public static IEnumerable<object[]> Sources()
    {
        yield return ["HyperbolicParaboloid","Panel P { Surface: HyperbolicParaboloid { Width: 40mm; Depth: 30mm; Rise: 6mm; } }"];
        yield return ["RuledSurface","Panel P { Surface: RuledSurface { BoundaryA: Line { Start: [-5mm,0mm,0mm]; End: [5mm,0mm,0mm]; } BoundaryB: Line { Start: [-5mm,5mm,1mm]; End: [5mm,5mm,1mm]; } } }"];
        yield return ["RuledTransition","Panel P { Surface: RuledTransition { BoundaryA: Line { Start: [-5mm,0mm,0mm]; End: [5mm,0mm,0mm]; } BoundaryB: Line { Start: [-5mm,5mm,1mm]; End: [5mm,5mm,1mm]; } } }"];
        yield return ["BoundaryPatch","Panel P { Surface: BoundaryPatch { South: Line { Start: [-5mm,0mm,0mm]; End: [5mm,0mm,0mm]; } North: Line { Start: [-5mm,5mm,1mm]; End: [5mm,5mm,1mm]; } West: Line { Start: [-5mm,0mm,0mm]; End: [-5mm,5mm,1mm]; } East: Line { Start: [5mm,0mm,0mm]; End: [5mm,5mm,1mm]; } } }"];
        yield return ["SectionSurface","Panel P { Surface: SectionSurface { Sections: [ Line S0 { Start: [-5mm,0mm,0mm]; End: [5mm,0mm,0mm]; } Line S1 { Start: [-5mm,3mm,2mm]; End: [5mm,3mm,2mm]; } Line S2 { Start: [-5mm,6mm,0mm]; End: [5mm,6mm,0mm]; } ] } }"];
    }
}
