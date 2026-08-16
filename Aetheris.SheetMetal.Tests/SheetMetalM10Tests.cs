using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalM10Tests
{
    private const string Library="""
Record RecessSpec {
  Center: Length
  Lead: Length
  Run: Length
  Depth: Length
  AttachmentInset: Length
  AttachmentSpan: Length
}

Template < P: RecessSpec, Owner: ProfilePath >
ProfileDelta Recess {
  On: Owner;
  Anchor: CenteredAt P.Center;
  Side: Inward;
  Level Carrier { Offset: 0mm; }
  Level Deep { Offset: P.Depth; }
  Level Attachment { Offset: P.AttachmentInset; }
  Transition LeadIn { Kind: Diagonal; Run: P.Lead; To: Deep; }
  Span LeftRun { Run: P.Run; At: Deep; }
  Transition RiseToAttachment { Kind: Step; To: Attachment; }
  Span AttachmentLand { Run: P.AttachmentSpan; At: Attachment; Expose: ServiceFlangeAttachment; Capabilities: [FlangeAttachable, BendAttachable, FeatureAttachable, StableSemanticIdentity]; }
  Transition FallFromAttachment { Kind: Step; To: Deep; }
  Span RightRun { Run: P.Run; At: Deep; }
  Transition LeadOut { Kind: Diagonal; Run: P.Lead; To: Carrier; }
}
""";

    [Fact]
    public void Unknown_user_named_template_feature_materializes_on_ordinary_extrusion()
    {
        var source=Library+"""
Static Chosen: RecessSpec = RecessSpec {
  Center: 50mm; Lead: 5mm; Run: 8mm; Depth: 6mm; AttachmentInset: 2mm; AttachmentSpan: 20mm;
}
Rect2 PlateBase { Center: [50mm, 20mm]; Size: [100mm, 40mm]; }
ProfileDelta AlienRabbet = Recess < P: Chosen, Owner: PlateBase.Bottom >
Profile Plate From PlateBase
Extrude Solid { Profile: Plate; From: 0mm; To: 3mm; }
""";
        var expanded=FirmamentTemplateSourceCompiler.Expand(source,out var expansionDiagnostics);
        Assert.NotNull(expanded);Assert.Empty(expansionDiagnostics);
        Assert.DoesNotContain("Recess <",expanded!.ExpandedSource,StringComparison.Ordinal);
        var parsed=ProfileAuthoringParser.Parse(expanded.ExpandedSource);
        Assert.Empty(parsed.Diagnostics);Assert.NotNull(parsed.Profile);
        var outer=Assert.Single(parsed.Profile!.Loops);
        Assert.Contains(outer.Segments,segment=>segment.Name.Contains("AlienRabbet",StringComparison.Ordinal));
        Assert.Contains(outer.Segments,segment=>segment.Provenance.ConceptStableId.Contains("AttachmentLand",StringComparison.Ordinal));
    }

    [Fact]
    public void Template_delta_drives_sheet_profile_and_exposed_attachment_path_once()
    {
        var source=Library+"""
Static Service: RecessSpec = RecessSpec {
  Center: 60mm; Lead: 6mm; Run: 10mm; Depth: 8mm; AttachmentInset: 3mm; AttachmentSpan: 40mm;
}
ProfileDelta UserServiceRecess = Recess < P: Service, Owner: Wall.Outer >
SheetMetal DeltaTray {
  Thickness: 1mm;
  Base Deck { Profile: Rectangle { Width: 120mm; Height: 80mm; }; }
  Flange Wall { From: Deck.Front; Height: 20mm; Angle: 90deg; Radius: 2mm; }
  Flange Child { From: Wall.ServiceFlangeAttachment; Height: 12mm; Angle: 45deg; Radius: 2mm; }
}
""";
        var result=SheetMetalFirmament.Compile(source);
        Assert.True(result.IsSuccess,string.Join("; ",result.Diagnostics.Select(x=>x.Message)));
        var delta=Assert.Single(result.Spec!.SemanticLayout.ProfileDeltas!);
        Assert.Equal("Wall.UserServiceRecess",delta.Path);
        var attachment=Assert.Single(result.Part!.AttachmentPaths!);
        Assert.Equal("Wall.ServiceFlangeAttachment",attachment.StableId);
        Assert.Equal(40,(attachment.End-attachment.Start).Length,8);Assert.Equal(3,attachment.Inset,8);
        Assert.Equal(40,result.Part.Regions.Single(x=>x.StableId=="ChildBendRegion").Cylinder!.AxisLength,8);
        var wall=result.Part.Regions.Single(x=>x.StableId=="Wall");
        Assert.Contains(wall.ExactContour!.OuterLoop.Segments,segment=>segment.StableId.Contains("AttachmentLand",StringComparison.Ordinal));
    }

    [Fact]
    public void Delta_conflicts_and_open_programs_fail_with_semantic_diagnostics()
    {
        var open=new SemanticProfileDeltaIr("Broken","Plate.Broken",new(SemanticEdgeAnchorKind.FromStart,2),-1,
            [new("Carrier","Plate.Broken.Carrier",0,"test"),new("Deep","Plate.Broken.Deep",4,"test")],
            [new("Enter","Plate.Broken.Enter",SemanticProfileDeltaMemberKind.Diagonal,3,"Deep",null,[],"test")],"test");
        var resolution=SemanticEdgeProfileResolver.Resolve(new("Plate.Bottom","Plate.Bottom",new(0,0),new(20,0),[open],"uv","test"));
        Assert.False(resolution.IsSuccess);
        Assert.Contains(resolution.Diagnostics,diagnostic=>diagnostic.StartsWith("semantic-profile-delta-open:",StringComparison.Ordinal));
    }

    [Fact]
    public void Multiple_delta_programs_compose_when_disjoint_and_report_both_paths_when_overlapping()
    {
        SemanticProfileDeltaIr Delta(string name,double center)=>new(name,$"Plate.{name}",new(SemanticEdgeAnchorKind.CenteredAt,center),-1,
            [new("Carrier",$"Plate.{name}.Carrier",0,"test"),new("Inset",$"Plate.{name}.Inset",2,"test")],
            [new("Enter",$"Plate.{name}.Enter",SemanticProfileDeltaMemberKind.Step,0,"Inset",null,[],"test"),
             new("Land",$"Plate.{name}.Land",SemanticProfileDeltaMemberKind.Span,10,"Inset",null,[],"test"),
             new("Exit",$"Plate.{name}.Exit",SemanticProfileDeltaMemberKind.Step,0,"Carrier",null,[],"test")],"test");
        var disjoint=SemanticEdgeProfileResolver.Resolve(new("Plate.Bottom","Plate.Bottom",new(0,0),new(100,0),[Delta("A",20),Delta("B",70)],"uv","test"));
        Assert.True(disjoint.IsSuccess,string.Join(';',disjoint.Diagnostics));
        var overlapping=SemanticEdgeProfileResolver.Resolve(new("Plate.Bottom","Plate.Bottom",new(0,0),new(100,0),[Delta("A",20),Delta("B",25)],"uv","test"));
        Assert.False(overlapping.IsSuccess);
        Assert.Contains("semantic-edge-fragment-overlap:Plate.Bottom:Plate.A:Plate.B",overlapping.Diagnostics);
    }

    [Fact]
    public void Local_round_transition_lowers_to_exact_arc_descendants()
    {
        var delta=new SemanticProfileDeltaIr("Rounded","Plate.Rounded",new(SemanticEdgeAnchorKind.FromStart,5),-1,
            [new("Carrier","Plate.Rounded.Carrier",0,"test"),new("Deep","Plate.Rounded.Deep",8,"test")],
            [new("EnterRound","Plate.Rounded.EnterRound",SemanticProfileDeltaMemberKind.Round,6,"Deep",null,[],"test",5),
             new("ExitRound","Plate.Rounded.ExitRound",SemanticProfileDeltaMemberKind.Round,6,"Carrier",null,[],"test",5)],"test");
        var resolution=SemanticEdgeProfileResolver.Resolve(new("Plate.Bottom","Plate.Bottom",new(0,0),new(30,0),[delta],"uv","test"));
        Assert.True(resolution.IsSuccess,string.Join(';',resolution.Diagnostics));
        var rounded=resolution.Profile!.OrderedMembers.Single(member=>member.StableId=="Plate.Rounded");
        Assert.All(rounded.CurveDescendants,curve=>Assert.IsType<Aetheris.Kernel.Firmament.Materializer.LineArcCircularArc2D>(curve.Geometry));
    }

    [Fact]
    public void Concave_round_selects_the_alternate_exact_arc_without_moving_endpoints()
    {
        SemanticProfileDeltaIr Delta(bool concave)=>new("Rounded","Plate.Rounded",new(SemanticEdgeAnchorKind.FromStart,5),-1,
            [new("Carrier","Plate.Rounded.Carrier",0,"test"),new("Deep","Plate.Rounded.Deep",8,"test")],
            [new("EnterRound","Plate.Rounded.EnterRound",SemanticProfileDeltaMemberKind.Round,6,"Deep",null,[],"test",6,concave),
             new("ExitRound","Plate.Rounded.ExitRound",SemanticProfileDeltaMemberKind.Round,6,"Carrier",null,[],"test",6,concave)],"test");
        LineArcCircularArc2D First(bool concave)=>Assert.IsType<LineArcCircularArc2D>(SemanticEdgeProfileResolver.Resolve(
            new("Plate.Bottom","Plate.Bottom",new(0,0),new(30,0),[Delta(concave)],"uv","test")).Profile!.OrderedMembers
            .Single(member=>member.StableId=="Plate.Rounded").CurveDescendants[0].Geometry);
        var convex=First(false);var concave=First(true);
        Assert.NotEqual(convex.Center,concave.Center);
        Assert.Equal(6,concave.Radius,8);
    }

    [Fact]
    public void Authored_slot_remains_an_exact_capsule_through_formed_and_flat_materialization()
    {
        const string source="""
        Concept Struct SlotLayout {
          Pattern Vents {
            On: Deck;
            Feature: Slot { Length: 10mm; Width: 40mm; };
            Center: Deck.Center;
            Count: 1;
            Pitch: (0mm, 0mm);
          }
        }
        SheetMetal SlotCoupon {
          Intent: SlotLayout;
          Thickness: 1mm;
          Base Deck { Profile: Rectangle { Width: 80mm; Height: 60mm; }; }
          Flange Wall { From: Deck.Front; Height: 10mm; Angle: 90deg; Radius: 2mm; }
        }
        """;
        var result=SheetMetalFirmament.Compile(source);
        Assert.True(result.IsSuccess,string.Join(';',result.Diagnostics.Select(x=>x.Message)));
        var feature=Assert.Single(result.Part!.Features);
        Assert.Equal(SheetFeatureKind.Slot,feature.Kind);
        Assert.Equal(2,feature.ExactContour!.OuterLoop.Segments.Count(x=>x.Geometry is LineArcCircularArc2D));
        Assert.All(feature.ExactContour.OuterLoop.Segments.Where(x=>x.Geometry is LineArcCircularArc2D),
            segment=>Assert.Equal(5,((LineArcCircularArc2D)segment.Geometry).Radius,8));
        var flatCut=Assert.Single(result.FlatPattern!.CutLoops);
        Assert.Equal(2,flatCut.ExactContour!.OuterLoop.Segments.Count(x=>x.Geometry is LineArcCircularArc2D));
        Assert.True(result.Part.FormedBody!.Geometry.Curves.Count(x=>x.Value.Kind==CurveGeometryKind.Circle3&&Math.Abs(x.Value.Circle3!.Value.Radius-5)<1e-8)>=4);
        Assert.True(BrepExportPreflight.Validate(result.Part.FormedBody).IsValid);
    }
}
