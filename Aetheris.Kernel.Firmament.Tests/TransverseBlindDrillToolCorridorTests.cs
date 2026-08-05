using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class TransverseBlindDrillToolCorridorTests
{
    [Fact]
    public void PlusXBlindDrill_ProvesFullRadiusClearanceAcrossTwoZSlabs()
    {
        var stack = Stack(40, 20); var placement = Placement(); var feature = Feature(4, 10, placement);

        var proof = TransverseBlindDrillToolCorridor.Prove(feature, stack, placement);

        Assert.True(proof.Classification == BlindDrillToolCorridorClassification.CorridorProven, string.Join(" | ", proof.Diagnostics));
        Assert.Equal(BlindDrillClearancePolicy.FullRadiusThroughTotalDepth, proof.ValidationPolicy);
        Assert.Equal(2, proof.ShaftSliceProofs.Count);
        Assert.Empty(proof.ConeSliceProofs);
        Assert.All(proof.ShaftSliceProofs, x =>
        {
            Assert.Equal("FullRadiusClearance", x.ToolPart);
            Assert.Equal(SectionRectangleCorridorClassification.FullyContained, x.Classification);
        });
        Assert.Equal(10d, proof.ShaftDepth);
        Assert.InRange(proof.TipLength, 1.2d, 1.21d);
    }

    [Fact]
    public void PlusXBlindDrill_RejectsShaftChordThatLeavesHost()
    {
        var stack = Stack(40, 12); var placement = Placement(); var feature = Feature(14, 10, placement);

        var proof = TransverseBlindDrillToolCorridor.Prove(feature, stack, placement);

        Assert.Equal(BlindDrillToolCorridorClassification.FullRadiusTipClearanceFailed, proof.Classification);
        Assert.Contains(proof.Diagnostics, x => x.StartsWith("ToolCorridorFailure: part=FullRadiusClearance", StringComparison.Ordinal));
    }

    private static PrismaticSectionStackConstruction Stack(double width, double depth)
    {
        var source = $$"""
            Concept Struct Layout On XY { Rect2 Guide { Center: [0mm, 0mm]; Size: [{{width}}mm, {{depth}}mm] } }
            Profile Stock Using Layout { Loop Outer {
                Segment South { Trace: Guide.Bottom; From: Guide.BottomLeft; To: Guide.BottomRight }
                Segment East { Trace: Guide.Right; From: Guide.BottomRight; To: Guide.TopRight }
                Segment North { Trace: Guide.Top; From: Guide.TopRight; To: Guide.TopLeft }
                Segment West { Trace: Guide.Left; From: Guide.TopLeft; To: Guide.BottomLeft }
            } }
            Struct Composition { Compose Host {
                Base Lower { Profile: Stock; From: -10mm; To: 0mm }
                Add Upper { Profile: Stock; From: 0mm; To: 10mm }
            } }
            """;
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        Assert.Empty(parsed.Diagnostics);
        return Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
    }

    private static AirConstructionPlaneHolePlacement Placement() => new("construction:+X", "concept:+X", new Point3D(-20, 0, 0),
        Direction3D.Create(new Vector3D(0, 1, 0)), Direction3D.Create(new Vector3D(0, 0, 1)), Direction3D.Create(new Vector3D(1, 0, 0)),
        0, 0, "test", "test");

    private static AirHoleFeature Feature(double diameter, double shaftDepth, AirConstructionPlaneHolePlacement placement) =>
        AirHoleFeature.CreateConstructionPlaneSimpleShaft("SideBlind", "Host.SideBlind", "Host", placement, new AirHoleShaft(diameter),
            new AirHoleEndCondition.ShaftDepth(shaftDepth), new AirProvenance("test", "test", "SideBlind", "Host.SideBlind", "test", AirSelectionClass.None, AirRuleKind.None, "test", true, []),
            new AirHoleTermination.DrillPoint());
}
