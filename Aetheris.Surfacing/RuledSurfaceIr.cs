using Aetheris.Kernel.Core.Math;

using Aetheris.Kernel.Core.Geometry.Curves;

namespace Aetheris.Surfacing;

public abstract record RuledBoundary(string StableId)
{
    public sealed record Line(string Id, Point3D Start, Point3D End) : RuledBoundary(Id);
    public sealed record Arc(string Id, Point3D Center, Direction3D Normal, double Radius, Direction3D ReferenceAxis,
        double StartAngleRadians, double SweepAngleRadians) : RuledBoundary(Id);
    public sealed record Circle(string Id, Point3D Center, Direction3D Normal, double Radius, Direction3D ReferenceAxis) : RuledBoundary(Id);
    public sealed record BSpline(string Id, BSpline3Curve Curve) : RuledBoundary(Id);
}

public sealed record BoundaryProvenance(string BoundaryStableId, string SourceIdentity, string Role);
public enum RuledConstructionKind { RuledSurface, RuledTransition }
public enum ParameterCorrespondenceKind { SharedNormalizedNativeParameter }
public enum DevelopabilityKind { Developable, NonDevelopable, Indeterminate }
public sealed record DevelopabilityEvidence(DevelopabilityKind Kind, string Method, double? MaximumTripleProduct,
    int SampleCount, string Explanation);

/// <summary>Minimum domain IR: two parameter-compatible boundaries and their source evidence.</summary>
public sealed record RuledSurfaceIr(
    string StableId,
    RuledConstructionKind Kind,
    RuledBoundary BoundaryA,
    RuledBoundary BoundaryB,
    BoundaryProvenance ProvenanceA,
    BoundaryProvenance ProvenanceB,
    bool DevelopabilityEvidencePreserved = true,
    ParameterCorrespondenceKind ParameterCorrespondence = ParameterCorrespondenceKind.SharedNormalizedNativeParameter);

public sealed record SurfacingDiagnostic(string Code, string Message);
