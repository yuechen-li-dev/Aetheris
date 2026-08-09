using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Cir;

public readonly record struct RegionId(string Value)
{
    public override string ToString() => Value;
}

public sealed record MaterialRegion(RegionId Id, string Name, string? SemanticRegion = null);

public enum ContinuumPointClassification
{
    Outside,
    Boundary,
    Inside,
}

public enum ContinuumBoundsClassification
{
    Outside,
    Inside,
    Cut,
}

/// <summary>An occupied material continuum. Occupancy is the only fundamental CIR query.</summary>
public interface IContinuumRegion
{
    RegionId Id { get; }

    BoundingBox3D Bounds { get; }

    ContinuumPointClassification Classify(Point3D point, double tolerance = 1e-9d);

    bool Contains(Point3D point, double tolerance = 1e-9d) =>
        Classify(point, tolerance) != ContinuumPointClassification.Outside;
}

/// <summary>Optional identity shared by independently lowered representations from one typed construction source.</summary>
public interface IConstructiveLineageRegion
{
    string ConstructionSourceIdentity { get; }
}

/// <summary>A sign-correct scalar field: negative is occupied, positive is outside. Magnitude is not promised to be distance.</summary>
public interface IImplicitFieldCapability
{
    double FieldValue(Point3D point);
}

/// <summary>Optional exact Euclidean signed-distance capability. Implementers must not expose this for non-isometric transforms or general CSG composition.</summary>
public interface IExactEuclideanSignedDistanceCapability : IImplicitFieldCapability
{
    double SignedDistance(Point3D point) => FieldValue(point);
}

public interface IGradientCapability
{
    bool TryGradient(Point3D point, out Vector3D gradient);
}

public readonly record struct BoundaryProjection(
    Point3D Point,
    Vector3D? Normal,
    double Distance,
    string? BoundaryId = null);

public interface IBoundaryProjectionCapability
{
    bool TryProjectToBoundary(Point3D point, out BoundaryProjection projection);
}

public interface IMaterialRegionCapability
{
    MaterialRegion? MaterialRegionAt(Point3D point);
}

/// <summary>Optional conservative or exact classification of an axis-aligned spatial cell.</summary>
public interface IBoundsClassificationCapability
{
    ContinuumBoundsClassification ClassifyBounds(BoundingBox3D bounds, double tolerance = 1e-9d);
}
