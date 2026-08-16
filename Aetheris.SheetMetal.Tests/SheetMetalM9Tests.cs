using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Firmament.Materializer;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalM9Tests
{
    private const string InsetPartialFlange = """
    SheetMetal InsetPartialFlange {
      Thickness: 1mm;
      KFactor: 0.5;
      Base Panel { Profile: Rectangle { Width: 100mm; Height: 60mm; }; }
      Flange Wall { From: Panel.Rear; Height: 20mm; Angle: 90deg; Radius: 2mm; }
      AttachmentPath ServiceFlangeAttachment {
        On: Wall.Outer;
        Inset: 2mm;
        Span: 40mm;
        SpanOffset: 5mm;
        Release: ToCarrier;
      }
      Flange ServiceFlange { From: Wall.ServiceFlangeAttachment; Height: 12mm; Angle: 45deg; Radius: 2mm; }
    }
    """;

    [Fact]
    public void InsetPartialAttachmentPath_IsDistinctStableAndDrivesOrdinaryFlange()
    {
        var result=SheetMetalFirmament.Compile(InsetPartialFlange);
        Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(x=>$"{x.Code}: {x.Message}")));
        var path=Assert.Single(result.Part!.AttachmentPaths!);
        Assert.Equal("Wall.ServiceFlangeAttachment",path.StableId);
        Assert.Equal("Wall",path.OwningRegionId);
        Assert.Equal("Wall.Outer",path.CarrierPath);
        Assert.Equal(2,path.Inset,8);Assert.Equal(40,(path.End-path.Start).Length,8);
        Assert.Contains(SheetPathCapability.FlangeAttachable,path.Capabilities);
        Assert.Equal(40,result.Part.Regions.Single(x=>x.StableId=="ServiceFlangeBendRegion").Cylinder!.AxisLength,8);
        Assert.Contains(result.Part.Correspondence!,x=>x.SemanticId==path.StableId&&x.Kind=="AttachmentPath");
        var concepts=SheetMetalConceptPaths.Inspect(result.Spec!,result.Part,result.FlatPattern!);
        Assert.Contains(concepts,x=>x.Path=="Wall.Outer"&&x.Capabilities.Contains("FreeEdge"));
        Assert.Contains(concepts,x=>x.Path==path.StableId&&x.Kind=="SheetAttachmentPath"&&x.Capabilities.Contains("FlangeAttachable"));
        Assert.True(BrepExportPreflight.Validate(result.Part.FormedBody!).IsValid);
        var flat=Assert.IsType<SheetMetalFlatPatternIr>(result.FlatPattern);
        var flatValidation=SheetMetalFlatPatternValidation.Validate(flat);
        Assert.True(flatValidation.ExactContoursValid,string.Join('\n',flatValidation.Diagnostics.Select(x=>$"{x.Code}: {x.Message}")));
        var repeated=SheetMetalFirmament.Compile(InsetPartialFlange);
        Assert.Equal(flat.DeterministicHash,repeated.FlatPattern!.DeterministicHash);
        var repeatedPath=Assert.Single(repeated.Part!.AttachmentPaths!);
        Assert.Equal((path.StableId,path.Start,path.End,path.Inset,path.SpanOffset),(repeatedPath.StableId,repeatedPath.Start,repeatedPath.End,repeatedPath.Inset,repeatedPath.SpanOffset));
        Assert.Equal(path.Capabilities,repeatedPath.Capabilities);
    }

    [Theory]
    [InlineData("Inset: -1mm; Span: 20mm;","sheetmetal-attachment-path-offset")]
    [InlineData("Inset: 2mm; Span: 0mm;","sheetmetal-attachment-path-span")]
    [InlineData("Inset: 25mm; Span: 20mm;","sheetmetal-attachment-path-offset")]
    [InlineData("Inset: 2mm; Span: 110mm;","sheetmetal-attachment-path-span")]
    public void AttachmentPath_RejectsImpossibleGeometry(string geometry,string code)
    {
        var source=$$"""
        SheetMetal InvalidPath {
          Thickness: 1mm;
          Base Panel { Profile: Rectangle { Width: 100mm; Height: 60mm; }; }
          Flange Wall { From: Panel.Rear; Height: 20mm; Angle: 90deg; Radius: 2mm; }
          AttachmentPath Service { On: Wall.Outer; {{geometry}} Release: ToCarrier; }
          Flange Lip { From: Wall.Service; Height: 12mm; Angle: 45deg; Radius: 2mm; }
        }
        """;
        var result=SheetMetalFirmament.Compile(source);
        Assert.False(result.IsSuccess);Assert.Contains(result.Diagnostics,x=>x.Code==code);
    }

    [Fact]
    public void InsetPath_RequiresExplicitReleaseGeometry()
    {
        var result=SheetMetalFirmament.Compile(InsetPartialFlange.Replace("Release: ToCarrier;",string.Empty));
        Assert.False(result.IsSuccess);Assert.Contains(result.Diagnostics,x=>x.Code=="sheetmetal-attachment-path-release");
    }

    [Fact]
    public void FullEdgeFlangeSyntax_RemainsUnchanged()
    {
        var result=SheetMetalFirmament.Compile("""
        SheetMetal LegacySurface {
          Thickness: 1mm;
          Base Panel { Profile: Rectangle { Width: 100mm; Height: 60mm; }; }
          Flange Wall { From: Panel.Rear; Height: 20mm; Angle: 90deg; Radius: 2mm; }
          Flange Lip { From: Wall.Outer; Height: 12mm; Angle: 45deg; Radius: 2mm; }
        }
        """);
        Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(x=>x.Message)));
        Assert.Empty(result.Part!.AttachmentPaths!);
    }

    [Fact]
    public void RoundedSemanticCorner_RemainsAnalyticInRegionFormedAndFlat()
    {
        var result=SheetMetalFirmament.Compile("""
        SheetMetal RoundedWall {
          Thickness: 1mm;
          Base Panel { Profile: Rectangle { Width: 100mm; Height: 60mm; }; }
          Flange Wall { From: Panel.Rear; Height: 20mm; Angle: 90deg; Radius: 2mm; }
          CornerProfile Wall.OuterStart { Round EndRound { Radius: 5mm; } }
        }
        """);
        Assert.True(result.IsSuccess,string.Join('\n',result.Diagnostics.Select(x=>$"{x.Code}: {x.Message}")));
        var region=result.Part!.Regions.Single(x=>x.StableId=="Wall");
        var regionArc=Assert.Single(region.ExactContour!.OuterLoop.Segments,x=>x.Geometry is LineArcCircularArc2D);
        Assert.Equal("Wall.OuterStart.EndRound.curve00",regionArc.StableId);
        var flatRegion=result.FlatPattern!.Regions2D.Single(x=>x.SourceRegionId=="Wall");
        Assert.Single(flatRegion.ExactContour!.OuterLoop.Segments,x=>x.Geometry is LineArcCircularArc2D);
        Assert.True(result.Part.FormedBody!.Geometry.Curves.Count(x=>x.Value.Kind==CurveGeometryKind.Circle3&&Math.Abs(x.Value.Circle3!.Value.Radius-5)<1e-8)>=2);
        Assert.Contains(result.Part.Correspondence!,x=>x.SemanticId=="Wall.OuterStart"&&x.Kind=="ProfileCorner");
    }
}
