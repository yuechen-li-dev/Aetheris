using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Air;

public sealed class LocalizedEdgeJunctionFilletTests
{
    [Theory]
    [InlineData(10d, 8d, 6d, 1d)]
    [InlineData(10d, 8d, 6d, 2d)]
    [InlineData(12d, 5d, 7d, 1d)]
    public void EqualRadiusTwoEdgeJunction_BuildsOneExactDirectEllipsePlan(double width, double depth, double height, double radius)
    {
        var result = AirLocalizedEdgeJunctionFilletCompiler.Compile(Request(width, depth, height, radius, radius));

        Assert.True(result.Succeeded, result.Error?.Code);
        var construction = Assert.IsType<LocalizedFilletJunctionConstruction>(result.Construction);
        var body = Assert.IsType<BrepBody>(result.Body);
        Assert.True(result.BRepPlan!.IsAuthoritative);
        Assert.Equal(0d, construction.Closure.SurfaceADeviation, 12);
        Assert.Equal(0d, construction.Closure.SurfaceBDeviation, 12);
        Assert.Equal(radius * System.Math.Sqrt(2d), construction.Closure.SharedCurve.MajorRadius, 9);
        Assert.Equal(radius, construction.Closure.SharedCurve.MinorRadius, 9);
        Assert.Equal(11, body.Topology.Vertices.Count());
        Assert.Equal(17, body.Topology.Edges.Count());
        Assert.Equal(8, body.Topology.Faces.Count());
        Assert.Equal(2, body.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(1, body.Geometry.Curves.Count(x => x.Value.Kind == CurveGeometryKind.Ellipse3));
        Assert.True(BrepExportPreflight.Validate(body).IsValid);

        foreach (var edgeBinding in body.Bindings.EdgeBindings)
        {
            var edge = body.Topology.GetEdge(edgeBinding.EdgeId);
            var curve = body.Geometry.GetCurve(edgeBinding.CurveGeometryId);
            var start = curve.Kind switch
            {
                CurveGeometryKind.Line3 => curve.Line3!.Value.Evaluate(edgeBinding.TrimInterval!.Value.Start),
                CurveGeometryKind.Circle3 => curve.Circle3!.Value.Evaluate(edgeBinding.TrimInterval!.Value.Start),
                CurveGeometryKind.Ellipse3 => curve.Ellipse3!.Value.Evaluate(edgeBinding.TrimInterval!.Value.Start),
                _ => throw new InvalidOperationException(),
            };
            Assert.True(body.TryGetVertexPoint(edge.StartVertexId, out var expectedStart));
            Assert.InRange((start - expectedStart).Length, 0d, 1e-9);
        }

        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        Assert.True(step.IsSuccess, string.Join(Environment.NewLine, step.Diagnostics.Select(x => x.Message)));
        Assert.Contains("ELLIPSE", step.Value!, StringComparison.Ordinal);
        var imported = Step242Importer.ImportBody(step.Value!);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(x => x.Message)));
        Assert.Equal(2, imported.Value!.Geometry.Surfaces.Count(x => x.Value.Kind == SurfaceGeometryKind.Cylinder));
        Assert.Equal(1, imported.Value.Geometry.Curves.Count(x => x.Value.Kind == CurveGeometryKind.Ellipse3));
    }

    [Theory]
    [InlineData(1d, 2d, "localized-fillet-junction-radius-mismatch")]
    [InlineData(0d, 0d, "localized-fillet-junction-radius-must-be-positive")]
    [InlineData(6d, 6d, "localized-fillet-junction-radius-too-large")]
    public void JunctionAdmission_RejectsInvalidParametersBeforeEmission(double first, double second, string code)
    {
        var result = AirLocalizedEdgeJunctionFilletCompiler.Compile(Request(10, 8, 6, first, second));
        Assert.False(result.Succeeded);
        Assert.Null(result.Body);
        Assert.Equal(code, result.Error?.Code);
    }

    [Fact]
    public void DirectIntersectionOverlapVolume_AgreesWithIndependentNumericalIntegration()
    {
        const double r = 1d;
        var exactOverlap = r * r * r * (5d / 3d - System.Math.PI / 2d);
        const int intervals = 100_000;
        var step = r / intervals;
        var numerical = 0d;
        for (var i = 0; i <= intervals; i++)
        {
            var z = i * step;
            var area = System.Math.Pow(r - System.Math.Sqrt(r * r - z * z), 2d);
            numerical += area * (i is 0 or intervals ? .5d : 1d);
        }
        numerical *= step;
        Assert.InRange(numerical, exactOverlap - 3e-8, exactOverlap + 3e-8);
    }

    private static AirLocalizedEdgeJunctionFilletCompileRequest Request(double width, double depth, double height, double first, double second) => new(
        "Base", "Base.First.Second", "First+Second", width, depth, height,
        "+X", "SharedEdgePlusZ", "+Y", "SharedEdgePlusZ", first, second, new AirSourceSpan(0, 1, "test"));
}
