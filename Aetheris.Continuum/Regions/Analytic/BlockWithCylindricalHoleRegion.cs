using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Regions.Analytic;

/// <summary>Axis-aligned block minus a Z-axis cylindrical through-hole.</summary>
public sealed class BlockWithCylindricalHoleRegion : IContinuumRegion, IBoundsClassificationCapability, IBoundaryReferenceCapability, IBoundaryOffsetMapCapability, IBoundaryProjectionCapability, IGradientCapability
{
    public BlockWithCylindricalHoleRegion(RegionId id, BoundingBox3D blockBounds, double holeRadius, Point3D? holeCenter = null)
    {
        if (holeRadius <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(holeRadius));
        }

        Id = id;
        Bounds = blockBounds;
        HoleRadius = holeRadius;
        HoleCenter = holeCenter ?? new Point3D(
            (blockBounds.Min.X + blockBounds.Max.X) * 0.5d,
            (blockBounds.Min.Y + blockBounds.Max.Y) * 0.5d,
            (blockBounds.Min.Z + blockBounds.Max.Z) * 0.5d);
    }

    public RegionId Id { get; }
    public BoundingBox3D Bounds { get; }
    public double HoleRadius { get; }
    public Point3D HoleCenter { get; }
    public double Height => Bounds.Max.Z - Bounds.Min.Z;
    public BoundaryReference CylindricalWallReference => new("analytic", $"{Id}:cylindrical-hole", SemanticRegion: "cylindrical-hole-wall");
    public double ExactCylindricalBoundaryArea => 2d * double.Pi * HoleRadius * Height;
    public double ExactVolume => ((Bounds.Max.X - Bounds.Min.X) * (Bounds.Max.Y - Bounds.Min.Y) * Height) - (double.Pi * HoleRadius * HoleRadius * Height);
    public double ExactBoundaryArea
    {
        get
        {
            var x = Bounds.Max.X - Bounds.Min.X;
            var y = Bounds.Max.Y - Bounds.Min.Y;
            return (2d * ((x * y) + (x * Height) + (y * Height)))
                - (2d * double.Pi * HoleRadius * HoleRadius)
                + (2d * double.Pi * HoleRadius * Height);
        }
    }

    public ContinuumPointClassification Classify(Point3D point, double tolerance = 1e-9d)
    {
        var block = new AxisAlignedBoxRegion(Id, Bounds).Classify(point, tolerance);
        if (block == ContinuumPointClassification.Outside)
        {
            return block;
        }

        var dx = point.X - HoleCenter.X;
        var dy = point.Y - HoleCenter.Y;
        var radial = double.Sqrt((dx * dx) + (dy * dy));
        if (radial < HoleRadius - tolerance)
        {
            return ContinuumPointClassification.Outside;
        }

        return block == ContinuumPointClassification.Boundary || double.Abs(radial - HoleRadius) <= tolerance
            ? ContinuumPointClassification.Boundary
            : ContinuumPointClassification.Inside;
    }

    public ContinuumBoundsClassification ClassifyBounds(BoundingBox3D bounds, double tolerance = 1e-9d)
    {
        if (!AnalyticRegionMath.HasPositiveIntersection(Bounds, bounds, tolerance))
        {
            return ContinuumBoundsClassification.Outside;
        }

        if (!AnalyticRegionMath.Contains(Bounds, bounds, tolerance))
        {
            return ContinuumBoundsClassification.Cut;
        }

        var minX = bounds.Min.X <= HoleCenter.X && bounds.Max.X >= HoleCenter.X
            ? 0d
            : double.Min(double.Abs(bounds.Min.X - HoleCenter.X), double.Abs(bounds.Max.X - HoleCenter.X));
        var minY = bounds.Min.Y <= HoleCenter.Y && bounds.Max.Y >= HoleCenter.Y
            ? 0d
            : double.Min(double.Abs(bounds.Min.Y - HoleCenter.Y), double.Abs(bounds.Max.Y - HoleCenter.Y));
        var minRadiusSquared = (minX * minX) + (minY * minY);
        var maxRadiusSquared = new[]
        {
            RadiusSquared(bounds.Min.X, bounds.Min.Y),
            RadiusSquared(bounds.Min.X, bounds.Max.Y),
            RadiusSquared(bounds.Max.X, bounds.Min.Y),
            RadiusSquared(bounds.Max.X, bounds.Max.Y),
        }.Max();
        var radiusSquared = HoleRadius * HoleRadius;
        if (minRadiusSquared >= radiusSquared - tolerance)
        {
            return ContinuumBoundsClassification.Inside;
        }

        if (maxRadiusSquared <= radiusSquared + tolerance)
        {
            return ContinuumBoundsClassification.Outside;
        }

        return ContinuumBoundsClassification.Cut;
    }

    public IReadOnlyList<BoundaryReference> BoundaryCandidates(BoundingBox3D cellBounds)
    {
        var candidates = AnalyticRegionMath.BoxBoundaryCandidates(Id, Bounds, cellBounds).ToList();
        candidates.Add(CylindricalWallReference);
        return candidates;
    }

    public IReadOnlyList<IAnalyticBoundarySupport> BoundarySupports(BoundingBox3D cellBounds) =>
        ClassifyBounds(cellBounds) == ContinuumBoundsClassification.Cut
            ? [new CylindricalWallBoundarySupport(CylindricalWallReference, HoleCenter, HoleRadius, Height)]
            : [];

    public bool TryProjectToBoundary(Point3D point, out BoundaryProjection projection)
    {
        var support = new CylindricalWallBoundarySupport(CylindricalWallReference, HoleCenter, HoleRadius, Height);
        var projected = support.Project(point);
        projection = new BoundaryProjection(projected, support.MaterialSideNormal(projected), (point - projected).Length, CylindricalWallReference.SourceId);
        return true;
    }

    public bool TryGradient(Point3D point, out Vector3D gradient)
    {
        var radial = new Vector3D(point.X - HoleCenter.X, point.Y - HoleCenter.Y, 0d);
        return radial.TryNormalize(out gradient);
    }

    private double RadiusSquared(double x, double y)
    {
        var dx = x - HoleCenter.X;
        var dy = y - HoleCenter.Y;
        return (dx * dx) + (dy * dy);
    }
}
