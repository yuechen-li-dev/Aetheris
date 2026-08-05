using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class PrismaticSectionStackHoleTraversalTests
{
    [Fact]
    public void AxialConstructionPlaneHole_OnePhysicalSpanMayCrossTwoPlannerSlabs()
    {
        var stack = Stack(); var placement = Placement(new Point3D(0, 0, 0), new Vector3D(0, 0, 1)); var feature = Feature(4d, placement);

        var evidence = PrismaticSectionStackHoleTraversal.Traverse(feature, stack, placement);
        var contract = HoleEndConditionContract.Evaluate(feature, evidence.HostTraversal);

        Assert.Equal(HoleHostTraversalClassification.MultipleContiguousPartitionsOfOneMaterialSpan, evidence.HostTraversal.Classification);
        Assert.Equal(2, evidence.OrderedPartitions.Count);
        Assert.Single(evidence.PhysicalSpans);
        Assert.Equal((0d, 20d), (evidence.PhysicalSpans[0].Start, evidence.PhysicalSpans[0].End));
        Assert.All(evidence.OrderedPartitions, partition => Assert.True(partition.CompleteCircularFootprintInMaterial));
        Assert.Contains(evidence.TransitionEvents, x => x.Contains("InternalPlannerPartition", StringComparison.Ordinal));
        Assert.True(contract.ContractSatisfied, string.Join(" | ", contract.Diagnostics));
    }

    [Fact]
    public void ExactDiskContainment_RejectsCenterlineThatLeavesOuterBoundary()
    {
        var stack = Stack(); var placement = Placement(new Point3D(0, 0, 0), new Vector3D(0, 0, 1)); var feature = Feature(42d, placement);

        var evidence = PrismaticSectionStackHoleTraversal.Traverse(feature, stack, placement);
        var contract = HoleEndConditionContract.Evaluate(feature, evidence.HostTraversal);

        Assert.All(evidence.FootprintChecks, check => Assert.Equal(SectionStackFootprintClassification.CrossesOuterBoundary, check.Classification));
        Assert.False(contract.ContractSatisfied);
        Assert.Contains(contract.Diagnostics, x => x.StartsWith("HoleFootprintLeavesHost", StringComparison.Ordinal));
    }

    [Fact]
    public void TransverseConstructionPlane_RejectsInsteadOfReusingBoxInterval()
    {
        var stack = Stack(); var placement = Placement(new Point3D(-20, 0, 5), new Vector3D(1, 0, 0)); var feature = Feature(4d, placement);

        var evidence = PrismaticSectionStackHoleTraversal.Traverse(feature, stack, placement);

        Assert.Empty(evidence.OrderedPartitions);
        Assert.Contains(evidence.Diagnostics, x => x.StartsWith("SectionStackHoleTransverseTraversalNotYetAdmitted", StringComparison.Ordinal));
        Assert.Contains(evidence.Provenance, x => x == "NoBoxIntervalFallback");
    }

    private static PrismaticSectionStackConstruction Stack()
    {
        const string source = """
            Concept Struct Layout On XY {
                Rect2 Guide { Center: [0mm, 0mm]; Size: [40mm, 40mm] }
            }
            Profile StockProfile Using Layout { Loop Outer {
                Segment South { Trace: Guide.Bottom; From: Guide.BottomLeft; To: Guide.BottomRight }
                Segment East { Trace: Guide.Right; From: Guide.BottomRight; To: Guide.TopRight }
                Segment North { Trace: Guide.Top; From: Guide.TopRight; To: Guide.TopLeft }
                Segment West { Trace: Guide.Left; From: Guide.TopLeft; To: Guide.BottomLeft }
            } }
            Struct Composition { Compose Stack {
                Base Lower { Profile: StockProfile; From: 0mm; To: 10mm }
                Add Upper { Profile: StockProfile; From: 10mm; To: 20mm }
            } }
            """;
        var parsed = PrismaticProfileCompositionParser.Parse(source);
        Assert.Empty(parsed.Diagnostics);
        return Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
    }

    private static AirConstructionPlaneHolePlacement Placement(Point3D origin, Vector3D axisZ)
    {
        var x = Math.Abs(axisZ.Z) > 0.5 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0);
        var y = axisZ.Z > 0.5 ? new Vector3D(0, 1, 0) : new Vector3D(0, 0, 1);
        return new("construction:test", "concept:test", origin, Direction3D.Create(x), Direction3D.Create(y), Direction3D.Create(axisZ), 0d, 0d, "test", "test");
    }

    private static AirHoleFeature Feature(double diameter, AirConstructionPlaneHolePlacement placement) =>
        AirHoleFeature.CreateConstructionPlaneSimpleShaft("Hole", "Stack.Hole", "Stack", placement, new AirHoleShaft(diameter),
            new AirHoleEndCondition.ThroughAll(), new AirProvenance("test", "test", "Hole", "Stack.Hole", "test", AirSelectionClass.None, AirRuleKind.None, "test", true, []));
}
