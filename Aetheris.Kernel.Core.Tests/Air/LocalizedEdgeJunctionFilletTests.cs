using Aetheris.Kernel.Core.Air;

namespace Aetheris.Kernel.Core.Tests.Air;

/// <summary>Proof-carrying admission tests for AIR-FILLET-JUNCTION-M4 investigation.</summary>
public sealed class LocalizedEdgeJunctionFilletTests
{
    [Theory]
    [InlineData(10d, 8d, 6d, 1d)]
    [InlineData(10d, 8d, 6d, 2d)]
    [InlineData(12d, 5d, 7d, 1d)]
    public void EqualRadiusTwoEdgeCandidate_RejectsSphereBecauseItForcesTheThirdFillet(double width, double depth, double height, double radius)
    {
        var result = AirLocalizedEdgeJunctionFilletCompiler.Compile(Request(width, depth, height, radius, radius));

        Assert.False(result.Succeeded);
        Assert.Equal(FilletJunctionErrorKind.CornerPatchSurfaceRequired, result.Error?.Kind);
        Assert.Equal("localized-fillet-junction-corner-patch-surface-required", result.Error?.Code);
        var construction = Assert.IsType<LocalizedFilletJunctionConstruction>(result.Construction);
        var candidate = construction.Candidate;
        Assert.Equal(radius, candidate.Radius, 9);
        Assert.Equal(width / 2d - radius, candidate.Center.X, 9);
        Assert.Equal(depth / 2d - radius, candidate.Center.Y, 9);
        Assert.Equal(height / 2d - radius, candidate.Center.Z, 9);
        Assert.Equal(0d, candidate.CylinderTangencyDeviation, 12);
        Assert.Equal(System.Math.PI * radius / 2d, candidate.ThirdBoundaryLength, 9);
        Assert.Equal(0d, candidate.ThirdBoundaryTrim.Start, 12);
        Assert.Equal(System.Math.PI / 2d, candidate.ThirdBoundaryTrim.End, 12);
        Assert.Equal("CylindricalFillet(SharedEdge(+X,+Y))", candidate.RequiredAdjacentSurface);
        Assert.Contains(result.Diagnostics, d => d == "support-plane-intersections=three tangent points, not trim curves");
    }

    [Theory]
    [InlineData(1d, 2d, "localized-fillet-junction-radius-mismatch")]
    [InlineData(0d, 0d, "localized-fillet-junction-radius-must-be-positive")]
    [InlineData(6d, 6d, "localized-fillet-junction-radius-too-large")]
    public void JunctionAdmission_RejectsInvalidParametersBeforeAnyConstruction(double first, double second, string code)
    {
        var result = AirLocalizedEdgeJunctionFilletCompiler.Compile(Request(10, 8, 6, first, second));
        Assert.False(result.Succeeded);
        Assert.Null(result.Construction);
        Assert.Equal(code, result.Error?.Code);
    }

    private static AirLocalizedEdgeJunctionFilletCompileRequest Request(double width, double depth, double height, double first, double second) => new(
        "Base", "Base.First.Second", "First+Second", width, depth, height,
        "+X", "SharedEdgePlusZ", "+Y", "SharedEdgePlusZ", first, second, new AirSourceSpan(0, 1, "test"));
}
