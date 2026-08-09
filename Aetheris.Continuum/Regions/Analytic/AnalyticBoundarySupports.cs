using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Lattice;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Regions.Analytic;

internal sealed class CylindricalWallBoundarySupport : IAnalyticBoundarySupport
{
    private readonly Point3D _center;
    private readonly double _radius;
    private readonly double _height;

    public CylindricalWallBoundarySupport(BoundaryReference reference, Point3D center, double radius, double height)
    {
        Reference = reference;
        _center = center;
        _radius = radius;
        _height = height;
    }

    public BoundaryReference Reference { get; }
    public double ExactArea => 2d * double.Pi * _radius * _height;

    public Point3D Project(Point3D point)
    {
        var radial = new Vector3D(point.X - _center.X, point.Y - _center.Y, 0d);
        if (!radial.TryNormalize(out var normal))
        {
            normal = new Vector3D(1d, 0d, 0d);
        }

        return new Point3D(_center.X + (normal.X * _radius), _center.Y + (normal.Y * _radius), point.Z);
    }

    public Vector3D MaterialSideNormal(Point3D boundaryPoint)
    {
        var radial = new Vector3D(boundaryPoint.X - _center.X, boundaryPoint.Y - _center.Y, 0d);
        if (!radial.TryNormalize(out var normal))
        {
            throw new InvalidOperationException("Cylinder normal is undefined on its axis.");
        }

        return normal;
    }

    public IBoundaryOffsetMap CreateOffsetMap(
        CellIndex cellIndex,
        BoundingBox3D cellBounds,
        int resolution,
        BoundaryOffsetMapErrorPolicy policy,
        BoundaryEvaluationCache? cache = null)
    {
        var center = Center(cellBounds);
        var origin = Project(center);
        var normal = MaterialSideNormal(origin);
        var tangentU = new Vector3D(-normal.Y, normal.X, 0d);
        var tangentV = new Vector3D(0d, 0d, 1d);
        var corners = Corners2D(cellBounds);
        var minimumU = corners.Min(point => (point - origin).Dot(tangentU));
        var maximumU = corners.Max(point => (point - origin).Dot(tangentU));
        var domain = new BoundaryMapDomain(minimumU, maximumU, cellBounds.Min.Z - origin.Z, cellBounds.Max.Z - origin.Z);
        var frame = new BoundaryLocalFrame(origin, normal, tangentU, tangentV);
        var frameX = BitConverter.DoubleToInt64Bits(normal.X);
        var frameY = BitConverter.DoubleToInt64Bits(normal.Y);
        return BoundaryMapBuilder.Build(
            cellIndex,
            Reference,
            frame,
            domain,
            resolution,
            policy,
            (u, _) => Exact(u, normal, tangentU),
            (u, _) => new ExactBoundaryEvaluation(
                AnalyticContinuumReferences.CylinderLocalOffset(_radius, u),
                AnalyticContinuumReferences.CylinderLocalMaterialSideNormal(_radius, u, normal, tangentU)),
            (u, _) => new BoundaryEvaluationKey(Reference.SourceId, frameX, frameY, BitConverter.DoubleToInt64Bits(u), 0),
            cache);
    }

    private ExactBoundaryEvaluation Exact(double u, Vector3D normal, Vector3D tangent)
    {
        var radicand = (_radius * _radius) - (u * u);
        if (radicand <= 0d)
        {
            throw new InvalidOperationException("Cylinder cannot be represented as a single local offset graph over this cell.");
        }

        var offset = double.Sqrt(radicand) - _radius;
        var exactNormal = (normal * ((_radius + offset) / _radius)) + (tangent * (u / _radius));
        exactNormal.TryNormalize(out exactNormal);
        return new ExactBoundaryEvaluation(offset, exactNormal);
    }

    private static Point3D Center(BoundingBox3D bounds) => new(
        (bounds.Min.X + bounds.Max.X) * 0.5d,
        (bounds.Min.Y + bounds.Max.Y) * 0.5d,
        (bounds.Min.Z + bounds.Max.Z) * 0.5d);

    private static Point3D[] Corners2D(BoundingBox3D bounds) =>
    [
        new Point3D(bounds.Min.X, bounds.Min.Y, bounds.Min.Z),
        new Point3D(bounds.Max.X, bounds.Min.Y, bounds.Min.Z),
        new Point3D(bounds.Max.X, bounds.Max.Y, bounds.Min.Z),
        new Point3D(bounds.Min.X, bounds.Max.Y, bounds.Min.Z),
    ];
}

internal sealed class ObliquePlaneBoundarySupport : IAnalyticBoundarySupport
{
    private readonly Vector3D _planeNormal;
    private readonly double _offset;

    public ObliquePlaneBoundarySupport(BoundaryReference reference, Vector3D planeNormal, double offset, double exactArea)
    {
        Reference = reference;
        _planeNormal = planeNormal;
        _offset = offset;
        ExactArea = exactArea;
    }

    public BoundaryReference Reference { get; }
    public double ExactArea { get; }

    public Point3D Project(Point3D point)
    {
        var distance = _planeNormal.Dot(new Vector3D(point.X, point.Y, point.Z)) - _offset;
        return point - (_planeNormal * distance);
    }

    public Vector3D MaterialSideNormal(Point3D boundaryPoint) => -_planeNormal;

    public IBoundaryOffsetMap CreateOffsetMap(
        CellIndex cellIndex,
        BoundingBox3D cellBounds,
        int resolution,
        BoundaryOffsetMapErrorPolicy policy,
        BoundaryEvaluationCache? cache = null)
    {
        if (double.Abs(_planeNormal.Z) > 1e-12d)
        {
            throw new NotSupportedException("M1 column maps require a plane parallel to the lattice Z extrusion.");
        }

        var center = new Point3D(
            (cellBounds.Min.X + cellBounds.Max.X) * 0.5d,
            (cellBounds.Min.Y + cellBounds.Max.Y) * 0.5d,
            (cellBounds.Min.Z + cellBounds.Max.Z) * 0.5d);
        var origin = Project(center);
        var normal = -_planeNormal;
        var tangentU = new Vector3D(-normal.Y, normal.X, 0d);
        var tangentV = new Vector3D(0d, 0d, 1d);
        var corners = new[]
        {
            new Point3D(cellBounds.Min.X, cellBounds.Min.Y, cellBounds.Min.Z),
            new Point3D(cellBounds.Max.X, cellBounds.Min.Y, cellBounds.Min.Z),
            new Point3D(cellBounds.Max.X, cellBounds.Max.Y, cellBounds.Min.Z),
            new Point3D(cellBounds.Min.X, cellBounds.Max.Y, cellBounds.Min.Z),
        };
        var minimumU = corners.Min(point => (point - origin).Dot(tangentU));
        var maximumU = corners.Max(point => (point - origin).Dot(tangentU));
        var domain = new BoundaryMapDomain(minimumU, maximumU, cellBounds.Min.Z - origin.Z, cellBounds.Max.Z - origin.Z);
        var frame = new BoundaryLocalFrame(origin, normal, tangentU, tangentV);
        return BoundaryMapBuilder.Build(
            cellIndex,
            Reference,
            frame,
            domain,
            resolution,
            policy,
            (_, _) => new ExactBoundaryEvaluation(0d, normal),
            (_, _) => new ExactBoundaryEvaluation(AnalyticContinuumReferences.PlaneLocalOffset(), AnalyticContinuumReferences.PlaneMaterialSideNormal(_planeNormal)),
            (u, _) => new BoundaryEvaluationKey(Reference.SourceId, BitConverter.DoubleToInt64Bits(normal.X), BitConverter.DoubleToInt64Bits(normal.Y), BitConverter.DoubleToInt64Bits(u), 0),
            cache);
    }
}
