using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Queries;

public sealed class BrepSpatialQueriesRaycastTests
{
    [Fact]
    public void Box_Raycast_ThroughRayReturnsTwoSortedHits_WithExpectedNormals()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var ray = new Ray3D(new Point3D(-3d, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        var result = BrepSpatialQueries.Raycast(box, ray);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.True(result.Value[0].T < result.Value[1].T);
        Assert.Equal(new Point3D(-1d, 0d, 0d), result.Value[0].Point);
        Assert.Equal(new Point3D(1d, 0d, 0d), result.Value[1].Point);
        Assert.NotNull(result.Value[0].Normal);
        Assert.NotNull(result.Value[1].Normal);
        Assert.Equal(-1d, result.Value[0].Normal!.Value.X, 12);
        Assert.Equal(1d, result.Value[1].Normal!.Value.X, 12);
    }

    [Fact]
    public void Box_Raycast_MissReturnsEmptyHitList()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var ray = new Ray3D(new Point3D(-3d, 3d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        var result = BrepSpatialQueries.Raycast(box, ray);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public void Cylinder_Raycast_ReturnsOrderedSideAndCapHits_WithoutSeamDuplicates()
    {
        var cylinder = BrepPrimitives.CreateCylinder(2d, 6d).Value;
        // Choose a ray that hits the side first, then the top cap at a distinct t.
        var ray = new Ray3D(new Point3D(3d, 0d, 2d), Direction3D.Create(new Vector3D(-1d, 0d, 0.5d)));

        var result = BrepSpatialQueries.Raycast(cylinder, ray);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.True(result.Value[0].T < result.Value[1].T);
        Assert.All(result.Value, hit => Assert.NotNull(hit.Normal));
    }

    [Fact]
    public void Sphere_Raycast_ThroughRayReturnsTwoHits_AndTangentReturnsOneHit()
    {
        var sphere = BrepPrimitives.CreateSphere(2d).Value;

        var throughRay = new Ray3D(new Point3D(-3d, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var throughResult = BrepSpatialQueries.Raycast(sphere, throughRay);

        Assert.True(throughResult.IsSuccess);
        Assert.Equal(2, throughResult.Value.Count);
        Assert.True(throughResult.Value[0].T < throughResult.Value[1].T);

        var tangentRay = new Ray3D(new Point3D(-3d, 2d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var tangentResult = BrepSpatialQueries.Raycast(sphere, tangentRay);

        Assert.True(tangentResult.IsSuccess);
        Assert.Single(tangentResult.Value);
    }

    [Fact]
    public void UnsupportedBody_Raycast_FailsWithNotImplementedDiagnostic()
    {
        var body = new BrepBody(new TopologyModel(), new BrepGeometryStore(), new BrepBindingModel());
        var ray = new Ray3D(Point3D.Origin, Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        var result = BrepSpatialQueries.Raycast(body, ray);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.NotImplemented);
    }
}
