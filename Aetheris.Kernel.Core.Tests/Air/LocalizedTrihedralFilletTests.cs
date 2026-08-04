using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Air;

public sealed class LocalizedTrihedralFilletTests
{
    [Theory]
    [InlineData(10d, 8d, 6d, 1d)]
    [InlineData(10d, 8d, 6d, 2d)]
    [InlineData(12d, 5d, 7d, 1d)]
    public void EqualRadiusTrihedral_BuildsExactSphericalOctantPlan(double width, double depth, double height, double radius)
    {
        var result = AirLocalizedTrihedralFilletCompiler.Compile(Request(width, depth, height, radius, radius, radius));

        Assert.True(result.Succeeded, result.Error?.Code);
        var construction = Assert.IsType<LocalizedTrihedralFilletConstruction>(result.Construction);
        var body = Assert.IsType<BrepBody>(result.Body);
        Assert.True(result.BRepPlan!.IsAuthoritative);
        Assert.Equal(width / 2d - radius, construction.SphericalCornerPatch.Center.X, 12);
        Assert.Equal(depth / 2d - radius, construction.SphericalCornerPatch.Center.Y, 12);
        Assert.Equal(height / 2d - radius, construction.SphericalCornerPatch.Center.Z, 12);
        Assert.Equal(radius, construction.SphericalCornerPatch.Radius, 12);
        Assert.Equal(0d, construction.SphericalCornerPatch.BoundaryXZ.SphereDeviation, 12);
        Assert.Equal(0d, construction.SphericalCornerPatch.BoundaryXZ.CylinderDeviation, 12);
        Assert.Equal(0d, construction.SphericalCornerPatch.BoundaryXZ.NormalDeviation, 12);
        Assert.Equal(13, body.Topology.Vertices.Count());
        Assert.Equal(21, body.Topology.Edges.Count());
        Assert.Equal(10, body.Topology.Faces.Count());
        Assert.Equal(3, body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(1, body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Sphere));
        Assert.Equal(6, body.Geometry.Curves.Count(x => x.Value.Kind == CurveGeometryKind.Circle3));
        Assert.True(BrepExportPreflight.Validate(body).IsValid);

        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(Environment.NewLine, step.Diagnostics.Select(x => x.Message)));
        Assert.Contains("SPHERICAL_SURFACE", step.Value!, StringComparison.Ordinal);
        Assert.Contains("CYLINDRICAL_SURFACE", step.Value!, StringComparison.Ordinal);
        var imported = Step242Importer.ImportBody(step.Value!);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(x => x.Message)));
        Assert.Equal(1, imported.Value!.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Sphere));
    }

    [Fact]
    public void UnequalRadii_AreDeferredBeforeBrepEmission()
    {
        var result = AirLocalizedTrihedralFilletCompiler.Compile(Request(10, 8, 6, 1, 1, 2));
        Assert.False(result.Succeeded);
        Assert.Null(result.Body);
        Assert.Equal(TrihedralFilletErrorKind.UnequalRadiusCornerSurfaceRequired, result.Error?.Kind);
        Assert.Equal("localized-trihedral-fillet-unequal-radius-corner-surface-required", result.Error?.Code);
    }

    [Theory]
    [InlineData(0d, "localized-trihedral-fillet-radius-must-be-positive")]
    [InlineData(6d, "localized-trihedral-fillet-radius-too-large")]
    public void InvalidRadius_FailsBeforeBrepEmission(double radius, string code)
    {
        var result = AirLocalizedTrihedralFilletCompiler.Compile(Request(10, 8, 6, radius, radius, radius));
        Assert.False(result.Succeeded);
        Assert.Null(result.Body);
        Assert.Equal(code, result.Error?.Code);
    }

    [Fact]
    public void UnsupportedHistoryAndNonSharingSelection_FailBeforeBrepEmission()
    {
        var unknownHistory = AirLocalizedTrihedralFilletCompiler.Compile(Request(10, 8, 6, 1, 1, 1) with { HistoryKnown = false });
        var nonSharing = AirLocalizedTrihedralFilletCompiler.Compile(Request(10, 8, 6, 1, 1, 1) with { TargetXY = "SharedEdgePlusZ" });
        Assert.Equal("localized-trihedral-fillet-unsupported-history", unknownHistory.Error?.Code);
        Assert.Equal("localized-trihedral-fillet-edges-do-not-share-canonical-vertex", nonSharing.Error?.Code);
        Assert.Null(unknownHistory.Body);
        Assert.Null(nonSharing.Body);
    }

    [Fact]
    public void RemovedVolume_HasIndependentAnalyticDecomposition()
    {
        const double width = 10, depth = 8, height = 6, r = 1;
        // Three non-overlapping remote cylindrical strips plus the local cube less its spherical octant.
        var removed = (1d - System.Math.PI / 4d) * r * r * ((width - r) + (depth - r) + (height - r))
            + (1d - System.Math.PI / 6d) * r * r * r;
        Assert.Equal(4.983039793055287d, removed, 12);
    }

    private static AirLocalizedTrihedralFilletCompileRequest Request(double width, double depth, double height, double xz, double yz, double xy) => new(
        "Base", "Base.XZ.YZ.XY", "XZ+YZ+XY", width, depth, height,
        "+X", "SharedEdgePlusZ", "+Y", "SharedEdgePlusZ", "+X", "SharedEdgePlusY", xz, yz, xy,
        new AirSourceSpan(0, 1, "test"));
}
