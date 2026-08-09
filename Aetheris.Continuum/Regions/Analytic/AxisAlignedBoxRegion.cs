using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Regions.Analytic;

public sealed class AxisAlignedBoxRegion : IContinuumRegion, IBoundsClassificationCapability, IBoundaryReferenceCapability, IPlanarBoundaryDomainCapability
{
    public AxisAlignedBoxRegion(RegionId id, BoundingBox3D bounds)
    {
        Id = id;
        Bounds = bounds;
    }

    public RegionId Id { get; }
    public BoundingBox3D Bounds { get; }
    public double ExactVolume => (Bounds.Max.X - Bounds.Min.X) * (Bounds.Max.Y - Bounds.Min.Y) * (Bounds.Max.Z - Bounds.Min.Z);
    public double ExactBoundaryArea
    {
        get
        {
            var x = Bounds.Max.X - Bounds.Min.X;
            var y = Bounds.Max.Y - Bounds.Min.Y;
            var z = Bounds.Max.Z - Bounds.Min.Z;
            return 2d * ((x * y) + (x * z) + (y * z));
        }
    }

    public ContinuumPointClassification Classify(Point3D point, double tolerance = 1e-9d)
    {
        if (point.X < Bounds.Min.X - tolerance || point.X > Bounds.Max.X + tolerance
            || point.Y < Bounds.Min.Y - tolerance || point.Y > Bounds.Max.Y + tolerance
            || point.Z < Bounds.Min.Z - tolerance || point.Z > Bounds.Max.Z + tolerance)
        {
            return ContinuumPointClassification.Outside;
        }

        var boundary = double.Abs(point.X - Bounds.Min.X) <= tolerance || double.Abs(point.X - Bounds.Max.X) <= tolerance
            || double.Abs(point.Y - Bounds.Min.Y) <= tolerance || double.Abs(point.Y - Bounds.Max.Y) <= tolerance
            || double.Abs(point.Z - Bounds.Min.Z) <= tolerance || double.Abs(point.Z - Bounds.Max.Z) <= tolerance;
        return boundary ? ContinuumPointClassification.Boundary : ContinuumPointClassification.Inside;
    }

    public ContinuumBoundsClassification ClassifyBounds(BoundingBox3D bounds, double tolerance = 1e-9d)
    {
        if (!AnalyticRegionMath.HasPositiveIntersection(Bounds, bounds, tolerance))
        {
            return ContinuumBoundsClassification.Outside;
        }

        return AnalyticRegionMath.Contains(Bounds, bounds, tolerance)
            ? ContinuumBoundsClassification.Inside
            : ContinuumBoundsClassification.Cut;
    }

    public IReadOnlyList<BoundaryReference> BoundaryCandidates(BoundingBox3D cellBounds) =>
        AnalyticRegionMath.BoxBoundaryCandidates(Id, Bounds, cellBounds);

    public bool TryResolvePlanarBoundary(string path,string? faceId,out PlanarBoundaryDomain domain)=>BoxPlanarBoundaryDomains.Resolve(BoxPlanarBoundaryDomains.Create(Id,Bounds),path,faceId,out domain);
}
