using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Regions.Analytic;

/// <summary>A bounded analytic half-space: points inside the support box with n·p &lt;= offset.</summary>
public sealed class ObliqueHalfSpaceRegion : IContinuumRegion, IBoundsClassificationCapability, IBoundaryReferenceCapability, IGradientCapability, IBoundaryOffsetMapCapability, IBoundaryProjectionCapability
{
    public ObliqueHalfSpaceRegion(RegionId id, BoundingBox3D bounds, Vector3D normal, double offset)
    {
        if (!normal.TryNormalize(out var normalized))
        {
            throw new ArgumentOutOfRangeException(nameof(normal));
        }

        Id = id;
        Bounds = bounds;
        Normal = normalized;
        Offset = offset;
    }

    public RegionId Id { get; }
    public BoundingBox3D Bounds { get; }
    public Vector3D Normal { get; }
    public double Offset { get; }
    public BoundaryReference PlaneBoundaryReference => new("analytic", $"{Id}:oblique-plane", SemanticRegion: "oblique-plane");
    public double ExactPlaneBoundaryArea => 4d * double.Sqrt(2d);

    public ContinuumPointClassification Classify(Point3D point, double tolerance = 1e-9d)
    {
        var support = new AxisAlignedBoxRegion(Id, Bounds).Classify(point, tolerance);
        if (support == ContinuumPointClassification.Outside)
        {
            return support;
        }

        var value = EvaluatePlane(point);
        return double.Abs(value) <= tolerance
            ? ContinuumPointClassification.Boundary
            : value < 0d
                ? ContinuumPointClassification.Inside
                : ContinuumPointClassification.Outside;
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

        var (minimum, maximum) = PlaneRange(bounds);
        if (maximum <= tolerance)
        {
            return ContinuumBoundsClassification.Inside;
        }

        if (minimum >= -tolerance)
        {
            return ContinuumBoundsClassification.Outside;
        }

        return ContinuumBoundsClassification.Cut;
    }

    public bool TryGradient(Point3D point, out Vector3D gradient)
    {
        gradient = Normal;
        return true;
    }

    public IReadOnlyList<BoundaryReference> BoundaryCandidates(BoundingBox3D cellBounds)
    {
        var candidates = AnalyticRegionMath.BoxBoundaryCandidates(Id, Bounds, cellBounds).ToList();
        var range = PlaneRange(cellBounds);
        if (range.Minimum <= 0d && range.Maximum >= 0d)
        {
            candidates.Add(PlaneBoundaryReference);
        }

        return candidates;
    }

    public IReadOnlyList<IAnalyticBoundarySupport> BoundarySupports(BoundingBox3D cellBounds) =>
        ClassifyBounds(cellBounds) == ContinuumBoundsClassification.Cut
            ? [new ObliquePlaneBoundarySupport(PlaneBoundaryReference, Normal, Offset, ExactPlaneBoundaryArea)]
            : [];

    public bool TryProjectToBoundary(Point3D point, out BoundaryProjection projection)
    {
        var support = new ObliquePlaneBoundarySupport(PlaneBoundaryReference, Normal, Offset, ExactPlaneBoundaryArea);
        var projected = support.Project(point);
        projection = new BoundaryProjection(projected, support.MaterialSideNormal(projected), (point - projected).Length, PlaneBoundaryReference.SourceId);
        return true;
    }

    private double EvaluatePlane(Point3D point) => (Normal.X * point.X) + (Normal.Y * point.Y) + (Normal.Z * point.Z) - Offset;

    private (double Minimum, double Maximum) PlaneRange(BoundingBox3D bounds)
    {
        var min = (Normal.X >= 0d ? Normal.X * bounds.Min.X : Normal.X * bounds.Max.X)
            + (Normal.Y >= 0d ? Normal.Y * bounds.Min.Y : Normal.Y * bounds.Max.Y)
            + (Normal.Z >= 0d ? Normal.Z * bounds.Min.Z : Normal.Z * bounds.Max.Z) - Offset;
        var max = (Normal.X >= 0d ? Normal.X * bounds.Max.X : Normal.X * bounds.Min.X)
            + (Normal.Y >= 0d ? Normal.Y * bounds.Max.Y : Normal.Y * bounds.Min.Y)
            + (Normal.Z >= 0d ? Normal.Z * bounds.Max.Z : Normal.Z * bounds.Min.Z) - Offset;
        return (min, max);
    }
}
