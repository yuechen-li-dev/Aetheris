using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class PrismaticSectionStackPlanMaterializationTests
{
    [Fact]
    public void PlanFirstMaterialization_PreservesConstructionFaceMappings()
    {
        var parsed = PrismaticProfileCompositionParser.Parse(Source);
        var stack = Assert.IsType<PrismaticSectionStackConstruction>(PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics));
        Assert.Empty(diagnostics);

        var planned = PrismaticSectionStackEmitter.TryPlan(stack);
        var summary = Assert.IsType<PrismaticSectionStackBrepPlan>(planned.Plan);
        var topology = Assert.IsType<PrismaticSectionStackTopologyPlan>(summary.TopologyPlan);
        var materialized = PrismaticSectionStackBrepMaterializer.TryMaterialize(topology);

        Assert.NotNull(materialized.Body);
        Assert.Empty(materialized.Diagnostics);
        Assert.Equal(summary.Faces, materialized.Body!.Topology.Faces.Count());
        Assert.Equal(summary.Edges, materialized.Body.Topology.Edges.Count());
        Assert.NotEmpty(topology.FaceMappings);
        Assert.All(topology.FaceMappings.Where(x => x.Kind == "PrismaticSide"), mapping =>
        {
            Assert.NotNull(mapping.SlabFrom);
            Assert.NotNull(mapping.SlabTo);
            Assert.NotEmpty(mapping.SourceStableId);
            Assert.True(topology.Bindings.TryGetFaceBinding(mapping.FaceId, out _));
        });
        Assert.Contains("compose-plan-first-materialization-boundary", planned.Diagnostics);
    }

    private const string Source = """
        Concept Struct Layout On XY { Rect2 Guide { Center: [0mm, 0mm]; Size: [20mm, 12mm] } }
        Profile Stock Using Layout { Loop Outer {
            Segment South { Trace: Guide.Bottom; From: Guide.BottomLeft; To: Guide.BottomRight }
            Segment East { Trace: Guide.Right; From: Guide.BottomRight; To: Guide.TopRight }
            Segment North { Trace: Guide.Top; From: Guide.TopRight; To: Guide.TopLeft }
            Segment West { Trace: Guide.Left; From: Guide.TopLeft; To: Guide.BottomLeft }
        } }
        Struct Composition { Compose Host { Base Lower { Profile: Stock; From: 0mm; To: 5mm } Add Upper { Profile: Stock; From: 5mm; To: 10mm } } }
        """;
}
