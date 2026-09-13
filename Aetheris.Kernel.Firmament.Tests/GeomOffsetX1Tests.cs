using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class GeomOffsetX1Tests
{
    [Fact]
    public void PrismAddAndFiniteRemove_LowerThroughTypedAirAndExactSectionStack()
    {
        var parsed = PrismaticProfileCompositionParser.Parse(Source("""
            AddOffset<Prism> Pad { Target: Plate; On: Top; Profile: PadProfile; Height: 3mm }
            RemoveOffset<Prism> Groove { Target: Plate; On: StockBody.Top; Profile: GrooveProfile; Depth: 4mm }
            """));

        Assert.Empty(parsed.Diagnostics);
        var feature = Assert.IsType<PrismaticProfileCompositionFeature>(parsed.Feature);
        Assert.Collection(feature.MaterialOffsets!,
            add =>
            {
                Assert.Equal(("Add", "Prism", "Plate", "Height"), (add.OperationKind, add.ToolFamily, add.Target, add.Termination));
                Assert.Equal([5d, -4d, 10d, 15d, 4d, 13d], add.AuthorizedRegion);
            },
            remove =>
            {
                Assert.Equal(("Remove", "Prism", "Plate", "Depth"), (remove.OperationKind, remove.ToolFamily, remove.Target, remove.Termination));
                Assert.Equal([13d, -3d, 6d, 23d, 3d, 10d], remove.AuthorizedRegion);
            });
        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var stackDiagnostics));
        Assert.Empty(stackDiagnostics);
        var correspondence = Assert.IsType<SemanticTopologyCorrespondence>(PrismaticSectionStackEmitter.Emit(stack).Correspondence);
        Assert.Contains(correspondence.Descendants, item => item.SourceStableId == "offset:Plate.Pad" && item.Role == SemanticTopologyRole.ExtrusionSideFace);
        Assert.Contains(correspondence.Descendants, item => item.SourceStableId == "offset:Plate.Groove" && item.Role == SemanticTopologyRole.ExtrusionSideFace);

        var result = FirmamentBuildAndExport.CompileSource(Source("""
            AddOffset<Prism> Pad { Target: Plate; On: Top; Profile: PadProfile; Height: 3mm }
            RemoveOffset<Prism> Groove { Target: Plate; On: StockBody.Top; Profile: GrooveProfile; Depth: 4mm }
            """));
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        Assert.Equal(2, result.Value.EngineeringFeatures!.Count);
        Assert.All(result.Value.EngineeringFeatures, item => Assert.Contains("Offset<Prism>", item.Kind));
        var imported = Step242Importer.ImportBody(result.Value.StepText);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(item => item.Message)));
        Assert.All(imported.Value.Topology.Edges, edge => Assert.Equal(2, imported.Value.Topology.Coedges.Count(use => use.EdgeId == edge.Id)));
        Assert.All(imported.Value.Topology.Faces, face => Assert.Equal(SurfaceGeometryKind.Plane, imported.Value.GetFaceSurface(face.Id).Kind));
        var mass = BrepMassProperties.Evaluate(imported.Value);
        Assert.NotEqual(BrepMassPropertiesStatus.Unavailable, mass.Status);
        Assert.Equal(12000d + 240d - 168d, mass.AbsoluteVolume, 6);
    }

    [Fact]
    public void PrismThroughNotch_MayCrossTargetBoundaryAndRemainsOneBody()
    {
        var result = FirmamentBuildAndExport.CompileSource(Source("""
            RemoveOffset<Prism> Notch { Target: Plate; On: Top; Profile: NotchProfile; Termination: ThroughAll }
            """));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(item => item.Message)));
        var report = Assert.Single(result.Value.EngineeringFeatures!);
        Assert.Equal("ThroughAll", report.ExtentKind);
        var imported = Step242Importer.ImportBody(result.Value.StepText);
        Assert.True(imported.IsSuccess);
        Assert.Single(imported.Value.Topology.Bodies);
        Assert.Equal(11400d, BrepMassProperties.Evaluate(imported.Value).AbsoluteVolume, 6);
    }

    [Theory]
    [InlineData("RemoveOffset<Prism> Bad { Target: Missing; On: Top; Profile: GrooveProfile; Depth: 4mm }", "offset-target-not-found")]
    [InlineData("RemoveOffset<Cylinder> Bad { Target: Plate; On: Top; Profile: GrooveProfile; Depth: 4mm }", "offset-tool-family-not-qualified")]
    [InlineData("RemoveOffset<Prism> Bad { Target: Plate; On: Top; Profile: GrooveProfile; Depth: 0mm }", "offset-tool-zero-dimension")]
    [InlineData("AddOffset<Prism> Bad { Target: Plate; On: Top; Profile: PadProfile; Height: 0mm }", "offset-tool-zero-dimension")]
    [InlineData("RemoveOffset<Prism> Bad { Target: Plate; On: Top; Profile: GrooveProfile; Along: +X; Depth: 4mm }", "offset-axis-invalid")]
    [InlineData("AddOffset<Prism> Bad { Target: Plate; On: Top; Profile: FarProfile; Height: 3mm }", "offset-add-disconnected")]
    [InlineData("AddOffset<Prism> Bad { Target: Plate; On: Top; Profile: TangentProfile; Height: 3mm }", "offset-tangent-contact")]
    [InlineData("AddOffset<Prism> Bad { Target: Plate; On: Top; Profile: CircleProfile; Height: 3mm }", "offset-profile-curve-not-qualified")]
    [InlineData("RemoveOffset<Prism> Bad { Target: Plate; On: Top; Profile: SplitProfile; Termination: ThroughAll }", "offset-remove-splits-body")]
    [InlineData("RemoveOffset<Prism> Bad { Target: Plate; On: Top; Profile: StockProfile; Termination: ThroughAll }", "offset-remove-entire-body")]
    public void InvalidPrismOffsets_FailClosedWithoutArtifact(string operation, string expected)
    {
        var result = FirmamentBuildAndExport.CompileSource(Source(operation));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, item => item.Message.Contains(expected, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SameSource_ProducesByteIdenticalStepAndStableAir()
    {
        var source = Source("RemoveOffset<Prism> Groove { Target: Plate; On: Top; Profile: GrooveProfile; Depth: 4mm }");
        var a = FirmamentBuildAndExport.CompileSource(source);
        var b = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(a.IsSuccess && b.IsSuccess, string.Join(Environment.NewLine, a.Diagnostics.Concat(b.Diagnostics).Select(item => item.Message)));
        Assert.Equal(a.Value.StepText, b.Value.StepText);
        var firstAir = Assert.Single(PrismaticProfileCompositionParser.Parse(source).Feature!.MaterialOffsets!);
        var secondAir = Assert.Single(PrismaticProfileCompositionParser.Parse(source).Feature!.MaterialOffsets!);
        Assert.Equal((firstAir.StableId, firstAir.OperationKind, firstAir.ToolFamily, firstAir.Target,
            firstAir.Support, firstAir.ProfileReference, firstAir.Direction, firstAir.Termination,
            firstAir.From, firstAir.To, firstAir.Extent),
            (secondAir.StableId, secondAir.OperationKind, secondAir.ToolFamily, secondAir.Target,
            secondAir.Support, secondAir.ProfileReference, secondAir.Direction, secondAir.Termination,
            secondAir.From, secondAir.To, secondAir.Extent));
        Assert.Equal(firstAir.AuthorizedRegion, secondAir.AuthorizedRegion);
    }

    [Fact]
    public void RoundedPrism_AuthorizedRegionUsesExactArcExtrema()
    {
        var parsed = PrismaticProfileCompositionParser.Parse(Source("AddOffset<Prism> RoundPad { Target: Plate; On: Top; Profile: RoundedProfile; Height: 2mm }"));

        Assert.Empty(parsed.Diagnostics);
        var offset = Assert.Single(parsed.Feature!.MaterialOffsets!);
        Assert.Collection(offset.AuthorizedRegion,
            value => Assert.Equal(-3d, value, 12), value => Assert.Equal(-4d, value, 12),
            value => Assert.Equal(10d, value, 12), value => Assert.Equal(5d, value, 12),
            value => Assert.Equal(2d, value, 12), value => Assert.Equal(12d, value, 12));
    }

    private static string Source(string operation) => $$"""
        Model OffsetPrism {
            Units: mm
            Rect2 Stock { Center: [0mm,0mm]; Size: [40mm,30mm] }
            Rect2 Pad { Center: [10mm,0mm]; Size: [10mm,8mm] }
            Rect2 Groove { Center: [18mm,0mm]; Size: [10mm,6mm] }
            Rect2 Notch { Center: [19mm,0mm]; Size: [10mm,10mm] }
            Rect2 Far { Center: [80mm,0mm]; Size: [4mm,4mm] }
            Rect2 Tangent { Center: [22mm,0mm]; Size: [4mm,4mm] }
            Rect2 Split { Center: [0mm,0mm]; Size: [4mm,40mm] }
            Circle2 Round { Center: [0mm,0mm]; Radius: 3mm }
            RoundedRect2 Rounded { Center: [1mm,-1mm]; Size: [8mm,6mm]; Radius: 2mm }
            Profile StockProfile { Loop Outer { Stock.Bottom |> Stock.Right |> Stock.Top |> Stock.Left |> Close } }
            Profile PadProfile { Loop Outer { Pad.Bottom |> Pad.Right |> Pad.Top |> Pad.Left |> Close } }
            Profile GrooveProfile { Loop Outer { Groove.Bottom |> Groove.Right |> Groove.Top |> Groove.Left |> Close } }
            Profile NotchProfile { Loop Outer { Notch.Bottom |> Notch.Right |> Notch.Top |> Notch.Left |> Close } }
            Profile FarProfile { Loop Outer { Far.Bottom |> Far.Right |> Far.Top |> Far.Left |> Close } }
            Profile TangentProfile { Loop Outer { Tangent.Bottom |> Tangent.Right |> Tangent.Top |> Tangent.Left |> Close } }
            Profile SplitProfile { Loop Outer { Split.Bottom |> Split.Right |> Split.Top |> Split.Left |> Close } }
            Profile CircleProfile { Loop Outer { Round |> TraceLoop } }
            Profile RoundedProfile { Loop Outer { Rounded |> TraceLoop } }
            Compose Plate {
                Base StockBody { Profile: StockProfile; From: 0mm; To: 10mm; Role: Stock }
                {{operation}}
            }
        }
        """;
}
