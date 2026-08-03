using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep.Prismatic;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class AirTopFaceBoundaryChamferProductionTests
{
    [Fact]
    public void FirmamentV2_Phase3PascalCase_ProducesSemanticFeatureWithSourceSpan()
    {
        var parsed = FirmamentV2Parser.Parse(Source(10, 8, 6, 1));

        Assert.True(parsed.IsSuccess, string.Join(", ", parsed.Diagnostics));
        var finish = Assert.Single(Assert.Single(parsed.Document!.ModifyBlocks!).EdgeFinishes!);
        Assert.Equal("TopBreak", finish.Name);
        Assert.Equal("+Z", finish.FaceAxis);
        Assert.Equal("Boundary", finish.Target);
        Assert.Equal("Chamfer", finish.Kind);
        Assert.Equal(1, finish.Distance);
        Assert.True(finish.SourceSpan.Length > 0);
    }

    [Theory]
    [InlineData(10, 8, 6, 1)]
    [InlineData(10, 8, 6, 2)]
    [InlineData(12, 5, 7, 1)]
    public void AirCompiler_AdmittedMatrix_ConsumesAuthoritativePlanAndMeasuresExactInset(double width, double depth, double height, double distance)
    {
        var result = AirTopFaceBoundaryChamferCompiler.Compile(Request(width, depth, height, distance));

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(AirFeatureAdmissionStatus.Admitted, result.Feature.Admission);
        Assert.Equal("+Z", result.Feature.Selection.FaceAxis);
        Assert.Equal(distance, result.Feature.Rule.Distance);
        Assert.Equal(3, result.Construction!.Profiles.Count);
        Assert.Equal([0, height - distance, height], result.Construction.Profiles.Select(p => p.Z));
        Assert.Equal("identity-by-profile-index", result.Construction.Transition.Correspondence);
        Assert.Equal(PrismaticSectionTransitionTopologyPlanner.PreserveSectionSplits, result.Construction.Transition.SplitPolicy);
        Assert.True(result.BRepPlan!.IsAuthoritative);
        Assert.NotNull(result.BRepPlan.RealizationPlan);
        Assert.Equal(12, result.BRepPlan.RealizationPlan!.Vertices.Count);
        Assert.Equal(20, result.BRepPlan.RealizationPlan.Edges.Count);
        Assert.Equal(10, result.BRepPlan.RealizationPlan.Faces.Count);
        Assert.Equal(12, result.Body!.Topology.Vertices.Count());
        Assert.Equal(20, result.Body.Topology.Edges.Count());
        Assert.Equal(10, result.Body.Topology.Faces.Count());
        Assert.Contains("edge-prismatic-v1-authoritative-topology-plan-consumed", result.Diagnostics);

        var top = result.BRepPlan.RealizationPlan.Vertices.Where(v => v.SectionIndex == 2).Select(v => v.Point).ToArray();
        Assert.Equal((-width / 2d) + distance, top.Min(p => p.X), 9);
        Assert.Equal((width / 2d) - distance, top.Max(p => p.X), 9);
        Assert.Equal((-depth / 2d) + distance, top.Min(p => p.Y), 9);
        Assert.Equal((depth / 2d) - distance, top.Max(p => p.Y), 9);
    }

    [Theory]
    [InlineData(0, "+Z", "Boundary", "air-chamfer-distance-must-be-positive")]
    [InlineData(4, "+Z", "Boundary", "air-chamfer-distance-too-large-rejected")]
    [InlineData(1, "-Z", "Boundary", "air-chamfer-unsupported-face-rejected:expected-+Z")]
    [InlineData(1, "+Z", "SingleEdge", "air-chamfer-unsupported-selection-rejected:expected-Boundary")]
    public void AirCompiler_UnsupportedDomain_IsExplicitlyRejected(double distance, string face, string target, string reason)
    {
        var request = Request(10, 8, 6, distance) with { FaceAxis = face, Target = target };
        var result = AirTopFaceBoundaryChamferCompiler.Compile(request);
        Assert.False(result.Succeeded);
        Assert.Equal(AirFeatureAdmissionStatus.Rejected, result.Feature.Admission);
        Assert.Equal(reason, result.Feature.AdmissionReason);
        Assert.Null(result.Body);
    }

    private static AirTopFaceBoundaryChamferCompileRequest Request(double width, double depth, double height, double distance) =>
        new("Base", "Base.TopBreak", "TopBreak", width, depth, height, "+Z", "Boundary", "Chamfer", distance, new AirSourceSpan(10, 20, "test"));

    private static string Source(double width, double depth, double height, double distance) => $$"""
        Model Test mm
        Box Base { Size: [{{width}}mm, {{depth}}mm, {{height}}mm] }
        Modify Base {
            EdgeFinish TopBreak {
                Face: +Z
                Target: Boundary
                Kind: Chamfer
                Distance: {{distance}}mm
            }
        }
        """;
}
