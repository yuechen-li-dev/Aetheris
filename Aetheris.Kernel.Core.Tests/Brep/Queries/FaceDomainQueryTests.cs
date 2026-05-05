using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Core.Tests.Brep.Queries;

public sealed class FaceDomainQueryTests
{
    [Fact]
    public void PlanarFace_Inside_ReturnsInsideWithDiagnostics()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var faceId = box.Topology.Faces.OrderBy(f => f.Id.Value).First().Id;

        var result = FaceDomainQuery.TryClassifyPointOnFace(box, faceId, new Point3D(0d, 0d, -1d), ToleranceContext.Default);

        Assert.True(result.IsSuccess);
        Assert.Equal(FaceDomainClassification.Inside, result.Classification);
        Assert.Equal("FaceDomainQuery.Planar", result.Source);
        Assert.NotNull(result.ProjectedUv);
    }

    [Fact]
    public void PlanarFace_Outside_ReturnsOutside()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var faceId = box.Topology.Faces.OrderBy(f => f.Id.Value).First().Id;

        var result = FaceDomainQuery.TryClassifyPointOnFace(box, faceId, new Point3D(2d, 0d, -1d));

        Assert.True(result.IsSuccess);
        Assert.Equal(FaceDomainClassification.Outside, result.Classification);
    }

    [Fact]
    public void PlanarFace_Boundary_ReturnsOnBoundaryAndNearEdgeFlag()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var faceId = box.Topology.Faces.OrderBy(f => f.Id.Value).First().Id;

        var result = FaceDomainQuery.TryClassifyPointOnFace(box, faceId, new Point3D(1d, 0d, -1d));

        Assert.True(result.IsSuccess);
        Assert.Equal(FaceDomainClassification.OnBoundary, result.Classification);
        Assert.True(result.NearEdge);
        Assert.True(result.BoundaryDistance <= ToleranceContext.Default.Linear + 1e-12d);
    }

    [Fact]
    public void CurvedFace_Unsupported_ReturnsStructuredReason()
    {
        var cylinder = BrepPrimitives.CreateCylinder(2d, 6d).Value;
        var faceId = cylinder.Topology.Faces.Single(f => cylinder.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder).Id;

        var result = FaceDomainQuery.TryClassifyPointOnFace(cylinder, faceId, new Point3D(2d, 0d, 0d));

        Assert.False(result.IsSuccess);
        Assert.Equal(FaceDomainClassification.Unsupported, result.Classification);
        Assert.True(result.UnsupportedSurface);
        Assert.Contains("not implemented", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("FaceDomainQuery.UnsupportedCylinder", result.Source);
    }
}
