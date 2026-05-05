using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Queries;

public sealed class BrepSpatialQueriesPointClassificationTests
{
    [Fact]
    public void Box_ClassifyPoint_CoversInsideOutsideBoundaryAndNearBoundaryTolerance()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var tolerance = new ToleranceContext(1e-6, 1e-9);

        Assert.Equal(PointContainment.Inside, BrepSpatialQueries.ClassifyPoint(box, new Point3D(0d, 0d, 0d), tolerance: tolerance).Value);
        Assert.Equal(PointContainment.Outside, BrepSpatialQueries.ClassifyPoint(box, new Point3D(2d, 0d, 0d), tolerance: tolerance).Value);
        Assert.Equal(PointContainment.Boundary, BrepSpatialQueries.ClassifyPoint(box, new Point3D(1d, 0d, 0d), tolerance: tolerance).Value);

        // Boundary is preferred when the point falls within linear tolerance of a face.
        Assert.Equal(PointContainment.Boundary, BrepSpatialQueries.ClassifyPoint(box, new Point3D(1d - 5e-7, 0d, 0d), tolerance: tolerance).Value);
    }

    [Fact]
    public void Cylinder_ClassifyPoint_CoversInsideOutsideAndBoundaryOnSideAndCap()
    {
        var cylinder = BrepPrimitives.CreateCylinder(2d, 6d).Value;

        Assert.Equal(PointContainment.Inside, BrepSpatialQueries.ClassifyPoint(cylinder, new Point3D(0d, 0d, 0d)).Value);
        Assert.Equal(PointContainment.Outside, BrepSpatialQueries.ClassifyPoint(cylinder, new Point3D(3d, 0d, 0d)).Value);
        Assert.Equal(PointContainment.Boundary, BrepSpatialQueries.ClassifyPoint(cylinder, new Point3D(2d, 0d, 0d)).Value);
        Assert.Equal(PointContainment.Boundary, BrepSpatialQueries.ClassifyPoint(cylinder, new Point3D(0d, 1d, 3d)).Value);
    }

    [Fact]
    public void Sphere_ClassifyPoint_CoversInsideOutsideAndBoundary()
    {
        var sphere = BrepPrimitives.CreateSphere(5d).Value;

        Assert.Equal(PointContainment.Inside, BrepSpatialQueries.ClassifyPoint(sphere, new Point3D(0d, 0d, 0d)).Value);
        Assert.Equal(PointContainment.Outside, BrepSpatialQueries.ClassifyPoint(sphere, new Point3D(6d, 0d, 0d)).Value);
        Assert.Equal(PointContainment.Boundary, BrepSpatialQueries.ClassifyPoint(sphere, new Point3D(5d, 0d, 0d)).Value);
    }

    [Fact]
    public void PrimitiveBody_ClassifyPoint_UsesJudgmentShellAndReportsSelectedCandidate()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var result = BrepSpatialQueries.ClassifyPoint(box, Point3D.Origin);

        Assert.True(result.IsSuccess);
        Assert.Equal(PointContainment.Inside, result.Value);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("strategy selected: primitive_analytic", StringComparison.Ordinal));
    }

    [Fact]
    public void UnsupportedBody_ClassifyPoint_ReturnsUnknownWithDiagnostic()
    {
        var body = new BrepBody(new TopologyModel(), new BrepGeometryStore(), new BrepBindingModel());

        var result = BrepSpatialQueries.ClassifyPoint(body, Point3D.Origin);

        Assert.True(result.IsSuccess);
        Assert.Equal(PointContainment.Unknown, result.Value);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("supports primitive", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("strategy selected: unknown", StringComparison.Ordinal));
    }
}
