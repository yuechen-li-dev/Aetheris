using Aetheris.Continuum.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Continuum.Backends.Sdf;

/// <summary>Adapts the preserved SDF node/tape runtime to the broader CIR occupancy contract.</summary>
public sealed class SdfContinuumRegion : IContinuumRegion, ISignedDistanceCapability, IGradientCapability, IBoundsClassificationCapability
{
    private readonly CirTape tape;

    public SdfContinuumRegion(RegionId id, CirNode root)
    {
        Id = id;
        Root = root ?? throw new ArgumentNullException(nameof(root));
        tape = CirTapeLowerer.Lower(root);
        Bounds = new BoundingBox3D(root.Bounds.Min, root.Bounds.Max);
    }

    public RegionId Id { get; }
    public CirNode Root { get; }
    public BoundingBox3D Bounds { get; }

    public double SignedDistance(Point3D point) => tape.Evaluate(point);

    public ContinuumPointClassification Classify(Point3D point, double tolerance = 1e-9d)
    {
        var value = SignedDistance(point);
        return double.Abs(value) <= tolerance
            ? ContinuumPointClassification.Boundary
            : value < 0d
                ? ContinuumPointClassification.Inside
                : ContinuumPointClassification.Outside;
    }

    public ContinuumBoundsClassification ClassifyBounds(BoundingBox3D bounds, double tolerance = 1e-9d)
    {
        var interval = tape.EvaluateInterval(new CirBounds(bounds.Min, bounds.Max));
        var center = new Point3D(
            (bounds.Min.X + bounds.Max.X) * 0.5d,
            (bounds.Min.Y + bounds.Max.Y) * 0.5d,
            (bounds.Min.Z + bounds.Max.Z) * 0.5d);
        if (interval.MaxValue <= tolerance && SignedDistance(center) <= 0d)
        {
            return ContinuumBoundsClassification.Inside;
        }

        if (interval.MinValue >= -tolerance && SignedDistance(center) > 0d)
        {
            return ContinuumBoundsClassification.Outside;
        }

        return ContinuumBoundsClassification.Cut;
    }

    public bool TryGradient(Point3D point, out Vector3D gradient)
    {
        const double h = 1e-6d;
        gradient = new Vector3D(
            (SignedDistance(new Point3D(point.X + h, point.Y, point.Z)) - SignedDistance(new Point3D(point.X - h, point.Y, point.Z))) / (2d * h),
            (SignedDistance(new Point3D(point.X, point.Y + h, point.Z)) - SignedDistance(new Point3D(point.X, point.Y - h, point.Z))) / (2d * h),
            (SignedDistance(new Point3D(point.X, point.Y, point.Z + h)) - SignedDistance(new Point3D(point.X, point.Y, point.Z - h))) / (2d * h));
        return gradient.TryNormalize(out gradient, ToleranceContext.Default);
    }
}
