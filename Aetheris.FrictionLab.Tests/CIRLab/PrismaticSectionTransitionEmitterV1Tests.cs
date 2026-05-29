using Aetheris.Kernel.Core.Brep.Prismatic;

namespace Aetheris.FrictionLab.Tests.CIRLab;

public sealed class PrismaticSectionTransitionEmitterV1Tests
{
    private static string Stable(PrismaticSectionTransitionResult result) => string.Join("|",
        result.Status,
        result.Succeeded,
        result.Topology.BodyProduced,
        result.Topology.SectionCount,
        result.Topology.VertexCount,
        result.Topology.EdgeCount,
        result.Topology.BottomProfileEdgeCount,
        result.Topology.TopProfileEdgeCount,
        result.Topology.TransitionEdgeCount,
        result.Topology.CapFaceCount,
        result.Topology.TransitionFaceCount,
        result.Topology.StableIntervalFaceCount,
        result.Topology.ChangedIntervalFaceCount,
        result.Topology.FaceCount,
        result.Topology.PlanarFaceCount,
        result.Topology.CylindricalFaceCount,
        result.Topology.LoopCount,
        result.Topology.CoedgeCount,
        result.Topology.Bounds,
        result.Step.Exported,
        string.Join(",", result.Step.PresentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", result.Step.MissingRequiredMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", result.Step.AbsentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", result.Step.UnexpectedPresentMarkers.OrderBy(x => x, StringComparer.Ordinal)),
        string.Join(",", result.Diagnostics.OrderBy(x => x, StringComparer.Ordinal)),
        result.Recommendation);

    [Fact]
    public void V1Emitter_IsDeterministic()
    {
        var request = SuccessRequest(Rectangle(0, 10, 8), Rectangle(1, 8, 6));

        Assert.Equal(Stable(PrismaticSectionTransitionEmitter.Emit(request)), Stable(PrismaticSectionTransitionEmitter.Emit(request)));
    }

    [Theory]
    [InlineData("rectangle", 4, 8, 12, 6, 24)]
    [InlineData("pentagon", 5, 10, 15, 7, 30)]
    [InlineData("hexagon", 6, 12, 18, 8, 36)]
    [InlineData("asymmetric", 5, 10, 15, 7, 30)]
    public void V1TwoSectionCases_SucceedWithTopologyFormulaAndStepSmoke(string name, int n, int vertices, int edges, int faces, int coedges)
    {
        var result = PrismaticSectionTransitionEmitter.Emit(name switch
        {
            "rectangle" => SuccessRequest(Rectangle(0, 10, 8), Rectangle(1, 8, 6)),
            "pentagon" => SuccessRequest(RegularPolygon(0, 5, 5), RegularPolygon(2, 4, 5)),
            "hexagon" => SuccessRequest(RegularPolygon(0, 6, 6), RegularPolygon(2, 4.5, 6)),
            _ => SuccessRequest(
                new PrismaticSection(0, [(-4, -2), (1, -3), (5, 0), (2, 3.5), (-3, 2.5)]),
                new PrismaticSection(2, [(-3.25, -2.35), (1.75, -3.35), (5.75, -0.35), (2.75, 3.15), (-2.25, 2.15)])),
        });

        Assert.Equal(PrismaticSectionTransitionStatus.Succeeded, result.Status);
        Assert.True(result.Succeeded);
        Assert.NotNull(result.Body);
        Assert.Equal(2, result.Topology.SectionCount);
        Assert.Equal(vertices, result.Topology.VertexCount);
        Assert.Equal(edges, result.Topology.EdgeCount);
        Assert.Equal(n, result.Topology.BottomProfileEdgeCount);
        Assert.Equal(n, result.Topology.TopProfileEdgeCount);
        Assert.Equal(n, result.Topology.TransitionEdgeCount);
        Assert.Equal(2, result.Topology.CapFaceCount);
        Assert.Equal(n, result.Topology.TransitionFaceCount);
        Assert.Equal(faces, result.Topology.FaceCount);
        Assert.Equal(faces, result.Topology.PlanarFaceCount);
        Assert.Equal(0, result.Topology.CylindricalFaceCount);
        Assert.Equal(faces, result.Topology.LoopCount);
        Assert.Equal(coedges, result.Topology.CoedgeCount);
        Assert.True(result.Step.Exported);
        Assert.Empty(result.Step.MissingRequiredMarkers);
        Assert.Empty(result.Step.UnexpectedPresentMarkers);
        Assert.Contains("ISO-10303-21", result.Step.PresentMarkers);
        Assert.Contains("MANIFOLD_SOLID_BREP", result.Step.PresentMarkers);
        Assert.Contains("ADVANCED_FACE", result.Step.PresentMarkers);
        Assert.Contains("PLANE", result.Step.PresentMarkers);
        Assert.Contains("CYLINDRICAL_SURFACE", result.Step.AbsentMarkers);
        Assert.Contains("BREP_WITH_VOIDS", result.Step.AbsentMarkers);
        Assert.Contains("edge-prismatic-v1-topology-validated", result.Diagnostics);
        Assert.Equal("prismatic-section-transition-ready-for-controlled-route-evaluation", result.Recommendation);
    }

    [Fact]
    public void V1ThreeSectionStablePlusTransition_PreservesSplitIntervalFaces()
    {
        var result = PrismaticSectionTransitionEmitter.Emit(SuccessRequest(Rectangle(0, 10, 8), Rectangle(5, 10, 8), Rectangle(6, 8, 6)));

        Assert.Equal(PrismaticSectionTransitionStatus.Succeeded, result.Status);
        Assert.Equal(3, result.Topology.SectionCount);
        Assert.Equal(12, result.Topology.VertexCount);
        Assert.Equal(20, result.Topology.EdgeCount);
        Assert.Equal(8, result.Topology.TransitionEdgeCount);
        Assert.Equal(2, result.Topology.CapFaceCount);
        Assert.Equal(8, result.Topology.TransitionFaceCount);
        Assert.Equal(4, result.Topology.StableIntervalFaceCount);
        Assert.Equal(4, result.Topology.ChangedIntervalFaceCount);
        Assert.Equal(10, result.Topology.FaceCount);
        Assert.Equal(10, result.Topology.PlanarFaceCount);
        Assert.Equal(10, result.Topology.LoopCount);
        Assert.Equal(40, result.Topology.CoedgeCount);
    }

    [Theory]
    [InlineData("non-increasing", "Rejected", "edge-prismatic-v1-non-increasing-sections-rejected", "edge-prismatic-v1-request-rejected:non-increasing-z")]
    [InlineData("zero-interval", "Rejected", "edge-prismatic-v1-non-increasing-sections-rejected", "edge-prismatic-v1-request-rejected:non-increasing-z")]
    [InlineData("mismatch", "Rejected", "edge-prismatic-v1-mismatched-vertex-count-rejected", "edge-prismatic-v1-request-rejected:mismatched-vertex-count")]
    [InlineData("missing-correspondence", "Rejected", "edge-prismatic-v1-missing-correspondence-rejected", "edge-prismatic-v1-request-rejected:missing-correspondence")]
    [InlineData("self-intersecting", "Rejected", "edge-prismatic-v1-invalid-profile-rejected", "edge-prismatic-v1-request-rejected:invalid-profile")]
    [InlineData("holes", "Deferred", "edge-prismatic-v1-holes-deferred", "edge-prismatic-v1-request-deferred:holes")]
    [InlineData("arcs", "Deferred", "edge-prismatic-v1-line-arc-deferred", "edge-prismatic-v1-request-deferred:line-arc-profile")]
    [InlineData("multiple-loops", "Deferred", "edge-prismatic-v1-multiple-loops-deferred", "edge-prismatic-v1-request-deferred:multiple-loops")]
    public void V1InvalidAndDeferredCases_AreClassifiedDeterministically(string name, string status, string diagnostic, string reason)
    {
        var result = PrismaticSectionTransitionEmitter.Emit(name switch
        {
            "non-increasing" => SuccessRequest(Rectangle(1, 10, 8), Rectangle(0, 8, 6)),
            "zero-interval" => SuccessRequest(Rectangle(0, 10, 8), Rectangle(0, 8, 6)),
            "mismatch" => SuccessRequest(Rectangle(0, 10, 8), RegularPolygon(1, 5, 5), 4),
            "missing-correspondence" => new PrismaticSectionTransitionRequest([Rectangle(0, 10, 8), Rectangle(1, 8, 6)], null, new PrismaticSectionTransitionOptions(true, name)),
            "self-intersecting" => SuccessRequest(new PrismaticSection(0, [(0, 0), (2, 2), (0, 2), (2, 0)]), Rectangle(1, 8, 6), 4),
            "holes" => SuccessRequest(Rectangle(0, 10, 8) with { HasHoles = true }, Rectangle(1, 8, 6) with { HasHoles = true }),
            "arcs" => SuccessRequest(Rectangle(0, 10, 8) with { HasArcs = true }, Rectangle(1, 8, 6) with { HasArcs = true }),
            _ => SuccessRequest(Rectangle(0, 10, 8) with { OuterLoopCount = 2 }, Rectangle(1, 8, 6) with { OuterLoopCount = 2 }),
        });

        Assert.Equal(status, result.Status.ToString());
        Assert.False(result.Succeeded);
        Assert.Null(result.Body);
        Assert.False(result.Topology.BodyProduced);
        Assert.Contains(diagnostic, result.Diagnostics);
        Assert.Contains(reason, result.Diagnostics);
    }

    [Fact]
    public void V1GuardrailDiagnostics_ConfirmNoForbiddenRoutes()
    {
        var result = PrismaticSectionTransitionEmitter.Emit(SuccessRequest(Rectangle(0, 10, 8), Rectangle(1, 8, 6)));

        Assert.Contains("edge-prismatic-v1-no-air-edge-sweep-used", result.Diagnostics);
        Assert.Contains("edge-prismatic-v1-no-brep-bounded-chamfer-used", result.Diagnostics);
        Assert.Contains("edge-prismatic-v1-no-topology-graft-used", result.Diagnostics);
        Assert.Contains("edge-prismatic-v1-no-3d-boolean-used", result.Diagnostics);
        Assert.Contains("edge-prismatic-v1-no-production-route-replacement", result.Diagnostics);
    }

    private static PrismaticSectionTransitionRequest SuccessRequest(params PrismaticSection[] sections) =>
        SuccessRequest(sections, sections[0].OuterLoop.Count);

    private static PrismaticSectionTransitionRequest SuccessRequest(PrismaticSection first, PrismaticSection second, int correspondenceCount) =>
        SuccessRequest([first, second], correspondenceCount);

    private static PrismaticSectionTransitionRequest SuccessRequest(PrismaticSection[] sections, int correspondenceCount) =>
        new(sections, PrismaticCorrespondenceMap.Identity(correspondenceCount), new PrismaticSectionTransitionOptions(true, "test"));

    private static PrismaticSection Rectangle(double z, double width, double depth)
    {
        var x = width * 0.5d;
        var y = depth * 0.5d;
        return new(z, [(-x, -y), (x, -y), (x, y), (-x, y)]);
    }

    private static PrismaticSection RegularPolygon(double z, double radius, int vertices)
    {
        var points = Enumerable.Range(0, vertices)
            .Select(i =>
            {
                var a = ((Math.PI * 2d) * i / vertices) - (Math.PI * 0.5d);
                return (X: Math.Cos(a) * radius, Y: Math.Sin(a) * radius);
            })
            .ToArray();
        return new(z, points);
    }
}
