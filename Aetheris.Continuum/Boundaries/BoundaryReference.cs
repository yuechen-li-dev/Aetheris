using Aetheris.Kernel.Core.Math;

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
    BoundaryReference SourceBoundary { get; }

    BoundaryLocalFrame LocalFrame { get; }

    IReadOnlyList<BoundaryOffsetSample> Samples { get; }

    BoundaryApproximationMetadata Approximation { get; }
}

public readonly record struct BoundaryLocalFrame(Point3D Origin, Vector3D Normal, Vector3D TangentU, Vector3D TangentV);

public readonly record struct BoundaryOffsetSample(double U, double V, double Offset, Vector3D? Normal = null);

public sealed record BoundaryApproximationMetadata(double? MaximumError, string Method, int Version = 1);
