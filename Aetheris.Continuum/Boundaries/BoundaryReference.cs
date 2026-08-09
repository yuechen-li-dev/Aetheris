using Aetheris.Kernel.Core.Math;
using Aetheris.Continuum.Lattice;

namespace Aetheris.Continuum.Boundaries;

public sealed record BoundaryReference(
    string SourceRepresentation,
    string SourceId,
    string? ExactBrepFaceId = null,
    string? SemanticRegion = null);

public interface IBoundaryReferenceCapability
{
    IReadOnlyList<BoundaryReference> BoundaryCandidates(BoundingBox3D cellBounds);
}

/// <summary>
/// M1 seam for a derived local boundary cache. Implementations are never exact geometry authority.
/// </summary>
public interface IBoundaryOffsetMap
{
    CellIndex CellIndex { get; }

    BoundaryReference SourceBoundary { get; }

    BoundaryLocalFrame LocalFrame { get; }

    IReadOnlyList<BoundaryOffsetSample> Samples { get; }

    BoundaryMapDomain Domain { get; }

    BoundaryApproximationMetadata Approximation { get; }

    BoundaryMapEvaluation Evaluate(double u, double v);

    /// <summary>Positive on the owning BRep trim, negative outside; infinity means unbounded support.</summary>
    double SourceTrimSignedDistance(Point3D position) => double.PositiveInfinity;
}

public readonly record struct BoundaryLocalFrame(Point3D Origin, Vector3D Normal, Vector3D TangentU, Vector3D TangentV);

public readonly record struct BoundaryOffsetSample(double U, double V, double Offset, Vector3D? Normal = null);

public readonly record struct BoundaryMapDomain(double MinimumU, double MaximumU, double MinimumV, double MaximumV);

public readonly record struct BoundaryMapEvaluation(Point3D Position, Vector3D Normal, double Offset);

public sealed record BoundaryApproximationMetadata(
    double MaximumPositionError,
    double RmsPositionError,
    double MeanPositionError,
    double MaximumNormalAngleDegrees,
    double RmsNormalAngleDegrees,
    string Method,
    int ResolutionU,
    int ResolutionV,
    int IndependentValidationPointCount,
    bool IsAccepted,
    int Version = 1,
    EngineeringBoundaryMapCertificate? RuntimeCertificate = null);

public enum BoundaryMapCertificateDecision
{
    Acceptable,
    RefineMap,
    Invalid,
}

/// <summary>
/// A deterministic conservative engineering estimate. It is not a formal proof for arbitrary surfaces.
/// </summary>
public sealed record EngineeringBoundaryMapCertificate(
    BoundaryMapCertificateDecision Decision,
    double PositionErrorBound,
    double NormalAngleBoundDegrees,
    int ExactQueryCount,
    string Basis);

public sealed record BoundaryOffsetMapErrorPolicy(
    double MaximumPositionError = 0.005d,
    double MaximumNormalAngleDegrees = 2d,
    int MaximumResolution = 8);
