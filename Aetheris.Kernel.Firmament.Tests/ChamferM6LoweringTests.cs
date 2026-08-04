using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ChamferM6LoweringTests
{
    [Theory]
    [InlineData(20, 50, 1)]
    [InlineData(7.5, 18, 2)]
    [InlineData(12, 9, 0.75)]
    public void CircularTopRim_GeneratesExactRevolutionWitnessAndChangedAnalyticBody(double radius, double height, double distance)
    {
        var result = AirCylinderTopRimChamferCompiler.Compile(Request(radius, height, distance));

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Null(result.Error);
        Assert.True(result.Construction!.Witness.CompilerGenerated);
        Assert.Equal([(radius, -height / 2), (radius, height / 2 - distance), (radius - distance, height / 2)],
            result.Construction.Witness.ReplacementProfile.Select(p => (p.X, p.Y)).ToArray());
        Assert.True(result.BRepPlan!.IsAuthoritative);
        Assert.NotNull(result.BRepPlan.RevolvedRealizationPlan);
        // Periodic rims now share their seam vertices; the old six-vertex shape was
        // coincident but topologically disconnected and could not be enforced.
        Assert.Equal(3, result.Body!.Topology.Vertices.Count());
        Assert.Equal(5, result.Body.Topology.Edges.Count());
        Assert.Equal(4, result.Body.Topology.Faces.Count());
        Assert.Equal(1, result.Body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(1, result.Body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cone));
        Assert.Equal(2, result.Body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Plane));
        Assert.Contains("revolved-profile-authoritative-topology-plan-consumed", result.Diagnostics);
        Assert.True(BrepExportPreflight.Validate(result.Body!).IsValid);
    }

    [Theory]
    [InlineData(20, 50, 0, "InvalidAuthoredInput", "chamfer-invalid-distance:must-be-positive")]
    [InlineData(5, 20, 5, "DistanceTooLarge", "chamfer-distance-too-large:circular-top-rim")]
    [InlineData(20, 0.5, 1, "DistanceTooLarge", "chamfer-distance-too-large:circular-top-rim")]
    public void CircularTopRim_InvalidDistance_ReturnsTypedErrorBeforeTopology(
        double radius, double height, double distance, string kind, string code)
    {
        var result = AirCylinderTopRimChamferCompiler.Compile(Request(radius, height, distance));
        Assert.False(result.Succeeded);
        Assert.Equal(kind, result.Error!.Kind.ToString());
        Assert.Equal(code, result.Error.Code);
        Assert.Null(result.BRepPlan);
        Assert.Null(result.Body);
    }

    [Theory]
    [InlineData("RectangularConcavePocketRim", "MissingConstructionWitness", "chamfer-missing-construction-witness:section-transition-does-not-support-holes")]
    [InlineData("SingleStraightConvexEdge", "MissingConstructionWitness", "chamfer-missing-construction-witness:localized-planar-replacement-not-implemented")]
    [InlineData("AdjacentEdgeJunction", "ConstructionWitnessRequired", "chamfer-corner-construction-witness-required:authoritative-brep-plan")]
    public void DeferredFamilies_ReturnPreciseConstructionErrors(
        string family, string kind, string code)
    {
        var result = AirDeferredChamferLowerer.Lower(new("feature", Enum.Parse<AirDeferredChamferFamily>(family), "history-known semantic selection", 1,
            ["support geometry declared", "material side declared", "retained/replacement ownership attempted"]));
        Assert.False(result.IsSuccess);
        Assert.Equal(kind, result.Error!.Kind.ToString());
        Assert.Equal(code, result.Error.Code);
        Assert.Equal("FeatureAIR->ConstructionAIR", result.Error.Stage);
        Assert.NotEmpty(result.Error.Evidence!);
    }

    [Fact]
    public void RectangularBaseline_UsesSameTypedLoweringBoundary()
    {
        var request = new AirTopFaceBoundaryChamferCompileRequest("Base", "Base.TopBreak", "TopBreak", 10, 8, 6, "+Z", "Boundary", "Chamfer", 1, new AirSourceSpan(0, 1, "test"));
        var ok = AirTopFaceBoundaryChamferCompiler.Lower(request);
        var invalid = AirTopFaceBoundaryChamferCompiler.Lower(request with { Distance = 4 });
        Assert.True(ok.IsSuccess);
        Assert.Equal(3, ok.Value!.Profiles.Count);
        Assert.Equal(ChamferLoweringErrorKind.DistanceTooLarge, invalid.Error!.Kind);
    }

    private static AirCylinderTopRimChamferCompileRequest Request(double radius, double height, double distance) =>
        new("Base", "Base.TopRim", "TopRim", radius, height, "+Z", "Boundary", "Chamfer", distance, new AirSourceSpan(0, 1, "test"));
}
