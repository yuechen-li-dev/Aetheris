using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalM5Tests
{
    [Fact]
    public void FourSemanticReliefsComposeOneExactConnectedBlank()
    {
        var result=SheetMetalFirmament.Compile("""
            SheetMetal FourReliefTray {
              Thickness: 1.2mm; KFactor: 0.42;
              Base Base { Profile: Rectangle { Width: 80mm; Height: 60mm; }; }
              Flange Front { From: Base.Front; Height: 18mm; Angle: 90deg; Radius: 1.5mm; Relief: Rectangular; }
              Flange Right { From: Base.Right; Height: 18mm; Angle: 90deg; Radius: 1.5mm; Relief: Rectangular; }
              Flange Rear { From: Base.Rear; Height: 18mm; Angle: 90deg; Radius: 1.5mm; Relief: Rectangular; }
              Flange Left { From: Base.Left; Height: 18mm; Angle: 90deg; Radius: 1.5mm; Relief: Rectangular; }
            }
            """);
        Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(x=>$"{x.Code}: {x.Message}")));
        Assert.NotNull(result.FlatPattern!.ExactBlankContour);
        Assert.Equal(4,result.FlatPattern.ReliefLoops!.Count);
        Assert.Equal(4,result.FlatPattern.CompositionPlan!.CornerResolutions.Count);
        Assert.Equal(8,result.FlatPattern.CompositionPlan.MaterialAdditions.Count);
        Assert.True(PlanarContourKernel.Validate(result.FlatPattern.ExactBlankContour!).IsValid);
        Assert.Equal(SheetMetalDfmStatus.Pass,SheetMetalDfm.Evaluate(result.Part!,result.FlatPattern).Overall);
    }

    [Fact]
    public void UserDefinedGenericSheetMetalTemplateUsesRecordRequireAndConcept()
    {
        var source="""
            Concept SheetMetalPart {
              Base: SheetRegion
              Flat: FlatPattern
            }
            Record SensorBracketSpec { Width: Length Depth: Length Height: Length Thickness: Length Radius: Length }
            Static Small: SensorBracketSpec = SensorBracketSpec { Width: 42mm Depth: 24mm Height: 12mm Thickness: 1mm Radius: 1mm }
            Template < Spec: SensorBracketSpec >
            SheetMetal SensorBracket: SheetMetalPart {
              Require Positive => Spec.Width > 0mm && Spec.Height > 0mm
              Thickness: Spec.Thickness;
              Base Base { Profile: Rectangle { Width: Spec.Width; Height: Spec.Depth; }; }
              Flange SensorWall { From: Base.Front; Height: Spec.Height; Angle: 90deg; Radius: Spec.Radius; }
            }
            SheetMetal MySensorBracket = SensorBracket < Spec: Small >
            """;
        var result=SheetMetalFirmament.Compile(source);
        Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(x=>$"{x.Code}: {x.Message}")));
        var instance=Assert.Single(result.TemplateInstantiations!);
        Assert.Equal("SensorBracket",instance.Template);
        Assert.Equal("MySensorBracket",instance.Instance);
        Assert.NotNull(result.FlatPattern!.ExactBlankContour);
    }

    [Fact]
    public void ClaimedSheetMetalConceptMissingMemberIsTypedCompileFailure()
    {
        var source="""
            Concept Tray {
              Base: SheetRegion
              Front: SheetFlange
              Rear: SheetFlange
              Flat: FlatPattern
            }
            Template < PartWidth: Length >
            SheetMetal BadTray: Tray {
              Thickness: 1mm;
              Base Base { Profile: Rectangle { Width: PartWidth; Height: 30mm; }; }
              Flange Front { From: Base.Front; Height: 10mm; Angle: 90deg; Radius: 1mm; }
            }
            SheetMetal Broken = BadTray < PartWidth: 40mm >
            """;
        var result=SheetMetalFirmament.Compile(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,x=>x.Code=="firmament-concept-missing-member"&&x.Message.Contains("Rear",StringComparison.Ordinal));
    }

    [Fact]
    public void ClaimedSheetMetalConceptIncompatibleMemberTypeIsTypedCompileFailure()
    {
        var result=SheetMetalFirmament.Compile("""
            Concept WrongPart { Base: SheetFlange }
            Template < PartWidth: Length > SheetMetal Wrong: WrongPart {
              Thickness: 1mm;
              Base Base { Profile: Rectangle { Width: PartWidth; Height: 30mm; }; }
              Flange Wall { From: Base.Front; Height: 10mm; Angle: 90deg; Radius: 1mm; }
            }
            SheetMetal Broken = Wrong < PartWidth: 40mm >
            """);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,x=>x.Code=="firmament-concept-type-mismatch"&&x.Message.Contains("SheetRegion",StringComparison.Ordinal));
    }

    [Fact]
    public void StandardLibraryTrayAndLidAreOrdinaryGenericTemplateSpecializations()
    {
        var tray=SheetMetalFirmament.Compile(SheetMetalTemplateLibrary.Source+"""
            Static DemoTray: TraySpec = TraySpec { Width: 90mm Depth: 65mm WallHeight: 20mm Thickness: 1.2mm InsideRadius: 1.5mm KFactor: 0.42 ReliefPolicy: Round }
            SheetMetal TrayA = FourWallTray < Spec: DemoTray >
            """);
        var lid=SheetMetalFirmament.Compile(SheetMetalTemplateLibrary.Source+"""
            Static DemoLid: LidSpec = LidSpec { Width: 92mm Depth: 67mm SkirtHeight: 8mm Clearance: 1mm Thickness: 1.2mm InsideRadius: 1.5mm KFactor: 0.42 ReliefPolicy: Rectangular }
            SheetMetal LidA = RemovablePanLid < Spec: DemoLid >
            """);
        Assert.True(tray.IsSuccess,string.Join('\n',tray.Diagnostics.Select(x=>$"{x.Code}: {x.Message}")));
        Assert.True(lid.IsSuccess,string.Join('\n',lid.Diagnostics.Select(x=>$"{x.Code}: {x.Message}")));
        Assert.NotNull(tray.FlatPattern!.ExactBlankContour);Assert.NotNull(lid.FlatPattern!.ExactBlankContour);
        Assert.Equal(4,tray.Part!.Reliefs!.Count);Assert.Equal(4,lid.Part!.Reliefs!.Count);
    }

    [Fact]
    public void ForgeFacingMakeEnclosureSpecializesSameTemplateAndKeepsPathShape()
    {
        var a=SheetMetalProductFamilies.MakeEnclosure(new(120,90,35,6,1.2,1.5),"NetworkA");
        var b=SheetMetalProductFamilies.MakeEnclosure(new(240,180,60,8,1.5,2),"PsuB");
        Assert.NotEqual(a.SpecializationIdentity,b.SpecializationIdentity);
        Assert.Equal(a.SemanticPaths.Select(x=>x.Path),b.SemanticPaths.Select(x=>x.Path));
        Assert.NotNull(a.FlatPattern.ExactBlankContour);Assert.NotNull(b.FlatPattern.ExactBlankContour);
        Assert.Equal(SheetMetalDfmStatus.Pass,a.Dfm.Overall);Assert.Equal(SheetMetalDfmStatus.Pass,b.Dfm.Overall);
        Assert.Contains(a.SemanticPaths,x=>x.Path=="Body.Front");
        Assert.Contains(a.SemanticPaths,x=>x.Path=="FrontLip.Outer");
        Assert.Equal(a.Compilation.FlatPattern!.CutLoops.Count,a.Fabrication.InnerCutContours.Count);
    }

    [Fact]
    public void MultiCutNetworkFixtureExportsAnEnclosedReimportableFlatStep()
    {
        var root=FindRepoRoot();var result=SheetMetalFirmament.CompileFile(Path.Combine(root,"fixtures/Canonical/SheetMetal/network-appliance-enclosure.firmament"));
        Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(x=>$"{x.Code}: {x.Message}")));
        Assert.Equal(12,result.FlatPattern!.CutLoops.Count);Assert.Equal(12,result.FlatPattern.ExactBlankContour!.InnerLoops.Count);
        var flat=SheetMetalManufacturingArtifacts.BuildFlatBody(result.Part!,result.FlatPattern);Assert.True(flat.IsSuccess,string.Join('\n',flat.Diagnostics.Select(x=>x.Message)));
        var step=Step242Exporter.ExportBody(flat.Body!);Assert.True(step.IsSuccess);
        var imported=Step242Importer.ImportBody(step.Value);Assert.True(imported.IsSuccess);
        var preflight=BrepExportPreflight.Validate(imported.Value);Assert.True(preflight.IsValid,string.Join('\n',preflight.Diagnostics.Select(x=>$"{x.Code}: {x.Message}")));
        Assert.Equal(SheetMetalDfmStatus.Pass,SheetMetalDfm.Evaluate(result.Part!,result.FlatPattern).Overall);
        Assert.Contains(SheetMetalConceptPaths.Inspect(result.Spec!,result.Part!,result.FlatPattern),x=>x.Path=="Rear.RearEthernet");
    }

    private static string FindRepoRoot(){var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"Aetheris.slnx")))dir=dir.Parent;return dir?.FullName??throw new InvalidOperationException("Repo root not found.");}
}
