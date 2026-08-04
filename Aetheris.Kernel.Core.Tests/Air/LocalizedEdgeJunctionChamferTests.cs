using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Air;

public sealed class LocalizedEdgeJunctionChamferTests
{
    [Theory]
    [InlineData(10d, 8d, 6d, 1d)]
    [InlineData(10d, 8d, 6d, 2d)]
    [InlineData(12d, 5d, 7d, 1d)]
    public void EqualDistanceTwoEdgeJunction_BuildsOneExactDirectMiterPlan(double width, double depth, double height, double distance)
    {
        var result = AirLocalizedEdgeJunctionChamferCompiler.Compile(Request(width, depth, height, distance, distance));

        Assert.True(result.Succeeded, result.Error?.Code);
        var construction = Assert.IsType<LocalizedEdgeJunctionConstruction>(result.Construction);
        var plan = Assert.IsType<Aetheris.Kernel.Core.Air.BRepPlan.AirBRepPlan>(result.BRepPlan);
        var body = Assert.IsType<BrepBody>(result.Body);
        Assert.True(plan.IsAuthoritative);
        Assert.NotNull(plan.LocalizedEdgeJunctionRealizationPlan);
        Assert.Equal("MiteredReplacementBoundary", construction.CornerPatch.Kind);
        Assert.Equal(2, construction.CornerPatch.Boundary.Count);
        Assert.Equal(11, body.Topology.Vertices.Count());
        Assert.Equal(17, body.Topology.Edges.Count());
        Assert.Equal(8, body.Topology.Faces.Count());
        Assert.Equal(8, body.Geometry.Surfaces.Count(surface => surface.Value.Kind == SurfaceGeometryKind.Plane));
        Assert.True(BrepExportPreflight.Validate(body).IsValid);

        var p = construction.CornerPatch.Boundary[0];
        var q = construction.CornerPatch.Boundary[1];
        Assert.Equal(width / 2d, p.X, 9);
        Assert.Equal(height / 2d - distance, p.Z, 9);
        Assert.Equal(width / 2d - distance, q.X, 9);
        Assert.Equal(depth / 2d - distance, q.Y, 9);
        Assert.Equal(height / 2d, q.Z, 9);
        Assert.Equal(width * depth * height - distance * distance * (width + depth) / 2d + distance * distance * distance / 3d,
            AnalyticVolume(width, depth, height, distance), 9);

        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(Environment.NewLine, step.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(step.Value!);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));
        Assert.Equal(11, imported.Value!.Topology.Vertices.Count());
        Assert.Equal(17, imported.Value.Topology.Edges.Count());
        Assert.Equal(8, imported.Value.Topology.Faces.Count());
    }

    [Theory]
    [InlineData(1d, 2d, "localized-junction-parameter-mismatch")]
    [InlineData(0d, 0d, "localized-junction-distance-must-be-positive")]
    [InlineData(6d, 6d, "localized-junction-distance-too-large")]
    public void JunctionAdmission_RejectsUnsupportedParametersBeforeEmission(double first, double second, string code)
    {
        var result = AirLocalizedEdgeJunctionChamferCompiler.Compile(Request(10, 8, 6, first, second));
        Assert.False(result.Succeeded);
        Assert.Null(result.Body);
        Assert.Equal(code, result.Error?.Code);
    }

    private static AirLocalizedEdgeJunctionChamferCompileRequest Request(double width, double depth, double height, double first, double second) => new(
        "Base", "Base.First.Second", "First+Second", width, depth, height,
        "+X", "SharedEdgePlusZ", "+Y", "SharedEdgePlusZ", first, second, new AirSourceSpan(0, 1, "test"));

    private static double AnalyticVolume(double width, double depth, double height, double distance) =>
        width * depth * height - distance * distance * (width + depth) / 2d + distance * distance * distance / 3d;
}
