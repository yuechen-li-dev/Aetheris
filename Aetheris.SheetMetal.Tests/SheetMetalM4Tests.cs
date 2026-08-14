using Aetheris.Kernel.Firmament.Materializer;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalM4Tests
{
    private static readonly string RepoRoot=FindRepoRoot();
    [Fact]
    public void AuthoredFlatCarriesValidatedExactBlankRegionsAndAnalyticCut()
    {
        var result=SheetMetalFirmament.Compile("""
            SheetMetal ExactFlat { Thickness: 1mm; Base Main { Profile: Rectangle { Width: 60mm; Height: 40mm; }; }
              Flange FrontWall { From: Main.Front; Height: 12mm; Angle: 90deg; Radius: 1mm; }
              Hole Mount { On: Main; Center: (30mm, 20mm); Diameter: 4mm; }
            }
            """);
        Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(x=>x.Message)));
        var flat=result.FlatPattern!;Assert.True(flat.ExactBlankContour is not null,string.Join('\n',flat.Diagnostics.Select(x=>$"{x.Code}: {x.Message}")));var blankValidation=PlanarContourKernel.Validate(flat.ExactBlankContour!);Assert.True(blankValidation.IsValid,string.Join('\n',blankValidation.Diagnostics.Select(x=>$"{x.Code}: {x.Message}")));
        Assert.All(flat.Regions2D,x=>Assert.NotNull(x.ExactContour));
        var cut=Assert.Single(flat.CutLoops);Assert.NotNull(cut.ExactContour);Assert.All(cut.ExactContour!.OuterLoop.Segments,x=>Assert.IsType<LineArcCircularArc2D>(x.Geometry));
        Assert.True(SheetMetalFlatPatternValidation.Validate(flat).ExactContoursValid);
        Assert.True(SheetMetalManufacturingArtifacts.BuildFlatBody(result.Part!,flat).IsSuccess);
        Assert.Contains("exact-blank-contour",SheetMetalSvgRenderer.Render(flat),StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Rectangular",SheetCornerPolicy.RectangularRelief,typeof(LineArcLineSegment2D))]
    [InlineData("Round",SheetCornerPolicy.RoundRelief,typeof(LineArcCircularArc2D))]
    public void ReliefPoliciesCutExactBlankAndManufacturingBody(string relief,SheetCornerPolicy policy,Type expectedCurve)
    {
        var result=SheetMetalFirmament.Compile($$"""
            SheetMetal ReliefCase { Thickness: 1mm; Base Main { Profile: Rectangle { Width: 50mm; Height: 40mm; }; }
              Flange Front { From: Main.Front; Height: 12mm; Angle: 90deg; Radius: 1mm; Relief: {{relief}}; }
              Flange Right { From: Main.Right; Height: 12mm; Angle: 90deg; Radius: 1mm; Relief: {{relief}}; }
            }
            """);
        Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(x=>x.Message)));Assert.Equal(policy,Assert.Single(result.Part!.Corners!).Policy);
        var flat=result.FlatPattern!;var loop=Assert.Single(flat.ReliefLoops!);Assert.Contains(loop.ExactContour.OuterLoop.Segments,x=>x.Geometry.GetType()==expectedCurve);Assert.True(flat.ExactBlankContour is not null,string.Join('\n',flat.Diagnostics.Select(x=>$"{x.Code}: {x.Message}")));
        Assert.True(PlanarContourKernel.Validate(loop.ExactContour).IsValid);Assert.True(SheetMetalManufacturingArtifacts.BuildFlatBody(result.Part,flat).IsSuccess);
    }

    [Fact]
    public void CanonicalPathsExposeBaseFlangeNestedBendAndFlatCorrespondence()
    {
        var source="""
            SheetMetal Chained { Thickness: 1mm; Base Main { Profile: Rectangle { Width: 50mm; Height: 30mm; }; }
              Flange FrontWall { From: Main.Front; Height: 12mm; Angle: 90deg; Radius: 1mm; }
              Flange Lip { From: FrontWall.Outer; Height: 5mm; Angle: 45deg; Radius: 1mm; Direction: Down; }
            }
            """;
        var result=SheetMetalFirmament.Compile(source);Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(x=>x.Message)));
        var paths=SheetMetalConceptPaths.Inspect(result.Spec!,result.Part!,result.FlatPattern!);
        Assert.Contains(paths,x=>x.Path=="Main.Front"&&x.Capabilities.Contains("FlangeAttachable"));
        Assert.Contains(paths,x=>x.Path=="FrontWall.Outer"&&x.Capabilities.Contains("FlangeAttachable"));
        Assert.Contains(paths,x=>x.Path=="FrontWall.Bend"&&x.FormedId=="FrontWallBend");
        Assert.Contains(paths,x=>x.Path=="Flat.FrontWall.Bend"&&x.FlatId=="flat-FrontWallBend");
    }

    [Theory]
    [InlineData("Main.Center","sheetmetal-incompatible-edge-capability","PointCapable")]
    [InlineData("Main.InnerFace","sheetmetal-concept-member-not-exposed","Available public members")]
    [InlineData("Wall.InnerFace","sheetmetal-concept-member-not-exposed","Root, Outer, Left, Right")]
    public void InvalidSemanticMemberDiagnostics_TeachPublicSurface(string from,string code,string message)
    {
        var source=$$"""
            SheetMetal BadPath { Thickness: 1mm; Base Main { Profile: Rectangle { Width: 30mm; Height: 20mm; }; }
              Flange Wall { From: {{from}}; Height: 8mm; Angle: 90deg; Radius: 1mm; }
            }
            """;
        var result=SheetMetalFirmament.Compile(source);Assert.False(result.IsSuccess);Assert.Contains(result.Diagnostics,x=>x.Code==code&&x.Message.Contains(message,StringComparison.Ordinal));
    }

    [Fact]
    public void ModuleTemplatesLowerThroughOrdinarySemanticsAndKeepPathShape()
    {
        var policy=new SheetMetalTemplatePolicy(1.2,1.5,.42,"5052-H32",SheetCornerPolicy.Mitered,SheetReliefPolicy.None);
        var l=SheetMetalFirmament.Compile(SheetMetalTemplates.LBracket("L",new(40,30,12,policy)));
        var u=SheetMetalFirmament.Compile(SheetMetalTemplates.UChannel("U",new(50,30,15,policy)));
        var a=SheetMetalFirmament.Compile(SheetMetalTemplates.FourWallTray("TrayA",new(80,60,18,policy)));
        var b=SheetMetalFirmament.Compile(SheetMetalTemplates.FourWallTray("TrayB",new(120,90,25,policy)));
        Assert.True(l.IsSuccess);Assert.True(u.IsSuccess);Assert.True(a.IsSuccess,string.Join('\n',a.Diagnostics.Select(x=>x.Message)));Assert.True(b.IsSuccess);
        Assert.Single(l.Part!.Bends);Assert.Equal(2,u.Part!.Bends.Count);Assert.Equal(4,a.Part!.Bends.Count);Assert.NotNull(a.FlatPattern!.ExactBlankContour);
        var shapeA=SheetMetalConceptPaths.Inspect(a.Spec!,a.Part!,a.FlatPattern!).Select(x=>x.Path).ToArray();var shapeB=SheetMetalConceptPaths.Inspect(b.Spec!,b.Part!,b.FlatPattern!).Select(x=>x.Path).ToArray();
        Assert.Equal(shapeA,shapeB);Assert.Equal(SheetMetalDfmStatus.Pass,SheetMetalDfm.Evaluate(a.Part,a.FlatPattern).Overall);
    }

    [Fact]
    public void LlmPsuEnclosure_UsesOnlyHighLevelPathsAndProducesExactArtifacts()
    {
        var path=Path.Combine(RepoRoot,"fixtures/FirmamentV2/SheetMetal/m4-psu-enclosure.firmament");var source=File.ReadAllText(path);var result=SheetMetalFirmament.CompileFile(path);
        Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(x=>x.Message)));Assert.DoesNotContain("region-",source,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("face-",source,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("edge-",source,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(5,result.Part!.Bends.Count);Assert.Equal(7,result.Part.Features.Count);Assert.True(result.FlatPattern!.ExactBlankContour is not null,string.Join('\n',result.FlatPattern.Diagnostics.Select(x=>$"{x.Code}: {x.Message}")));Assert.Equal(4,result.Part.Corners!.Count);Assert.Contains(result.Part.Corners,x=>x.Policy==SheetCornerPolicy.Mitered);Assert.Contains(result.Part.Corners,x=>x.Policy==SheetCornerPolicy.Open);
        var paths=SheetMetalConceptPaths.Inspect(result.Spec!,result.Part,result.FlatPattern);Assert.Contains(paths,x=>x.Path=="FrontWall.Outer");Assert.Contains(paths,x=>x.Path=="FrontLip.Bend");
        Assert.True(SheetMetalManufacturingArtifacts.BuildFlatBody(result.Part,result.FlatPattern).IsSuccess);
    }

    [Fact]
    public void SemanticDfmRepairFixture_ConvergesWithoutBrepIds()
    {
        var bad=SheetMetalFirmament.CompileFile(Path.Combine(RepoRoot,"fixtures/FirmamentV2/SheetMetal/m4-bad-tray-dfm.firmament"));var fixedPart=SheetMetalFirmament.CompileFile(Path.Combine(RepoRoot,"fixtures/FirmamentV2/SheetMetal/m4-fixed-tray-dfm.firmament"));Assert.True(bad.IsSuccess);Assert.True(fixedPart.IsSuccess);
        var before=SheetMetalDfm.Evaluate(bad.Part!,bad.FlatPattern);var after=SheetMetalDfm.Evaluate(fixedPart.Part!,fixedPart.FlatPattern);Assert.Equal(SheetMetalDfmStatus.Warning,before.Overall);Assert.Contains(before.Findings,x=>x.RuleId=="sheetmetal-dfm-corner-resolution"&&x.SuggestedFix is not null);Assert.Equal(SheetMetalDfmStatus.Pass,after.Overall);
    }

    private static string FindRepoRoot(){var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"Aetheris.slnx")))dir=dir.Parent;return dir?.FullName??throw new InvalidOperationException("Repo root not found.");}
}
