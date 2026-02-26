using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Brep;

public sealed class BrepGeometryStoreTests
{
    [Fact]
    public void GeometryTypedIds_ExposeValidityAndValueEquality()
    {
        var curveIdA = new CurveGeometryId(3);
        var curveIdB = new CurveGeometryId(3);
        var surfaceId = new SurfaceGeometryId(4);

        Assert.Equal(curveIdA, curveIdB);
        Assert.True(curveIdA.IsValid);
        Assert.False(CurveGeometryId.Invalid.IsValid);
        Assert.True(surfaceId.IsValid);
        Assert.False(SurfaceGeometryId.Invalid.IsValid);
    }

    [Fact]
    public void GeometryStore_AddGetTryGetWorks()
    {
        var store = new BrepGeometryStore();
        var line = CurveGeometry.FromLine(new Line3Curve(Point3D.Origin, Direction3D.Create(new Vector3D(1d, 0d, 0d))));
        var plane = SurfaceGeometry.FromPlane(new PlaneSurface(
            Point3D.Origin,
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            Direction3D.Create(new Vector3D(1d, 0d, 0d))));

        store.AddCurve(new CurveGeometryId(1), line);
        store.AddSurface(new SurfaceGeometryId(1), plane);

        Assert.True(store.TryGetCurve(new CurveGeometryId(1), out var foundCurve));
        Assert.Equal(CurveGeometryKind.Line3, foundCurve!.Kind);
        Assert.Equal(line, store.GetCurve(new CurveGeometryId(1)));

        Assert.True(store.TryGetSurface(new SurfaceGeometryId(1), out var foundSurface));
        Assert.Equal(SurfaceGeometryKind.Plane, foundSurface!.Kind);
        Assert.Equal(plane, store.GetSurface(new SurfaceGeometryId(1)));
    }

    [Fact]
    public void GeometryStore_DuplicateIdAddThrows()
    {
        var store = new BrepGeometryStore();
        var curveId = new CurveGeometryId(1);

        store.AddCurve(curveId, CurveGeometry.FromLine(new Line3Curve(Point3D.Origin, Direction3D.Create(new Vector3D(1d, 0d, 0d)))));

        Assert.Throws<ArgumentException>(() =>
            store.AddCurve(curveId, CurveGeometry.FromLine(new Line3Curve(Point3D.Origin, Direction3D.Create(new Vector3D(0d, 1d, 0d))))));
    }
}
