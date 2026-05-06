using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Judgment;

namespace Aetheris.Kernel.Firmament.Execution;

/// <summary>
/// CIR-F8.1 scaffold: descriptor-first path toward surface-family materialization.
/// Existing pair-specific materializers remain compatibility/fast paths.
/// </summary>
internal enum SurfacePatchFamily
{
    Planar,
    Cylindrical,
    Conical,
    Spherical,
    Toroidal,
    Spline,
    Prismatic,
    Unsupported
}

internal enum TrimCurveFamily
{
    Line,
    Circle,
    Ellipse,
    BSpline,
    Polyline,
    AlgebraicImplicit,
    Unsupported
}

internal enum TrimCurveCapability
{
    ExactSupported,
    SpecialCaseOnly,
    Deferred,
    Unsupported
}

internal enum FacePatchOrientationRole
{
    Unknown,
    Forward,
    Reversed
}

internal sealed record SourceSurfaceDescriptor(
    SurfacePatchFamily Family,
    string? ParameterPayloadReference,
    Transform3D Transform,
    string Provenance,
    string? OwningCirNodeKind,
    int? ReplayOpIndex,
    FacePatchOrientationRole OrientationRole);

internal sealed record TrimCurveDescriptor(
    TrimCurveFamily Family,
    string? ParameterPayloadReference,
    string Provenance,
    int? ReplayOpIndex,
    ParameterInterval? Domain,
    TrimCurveCapability Capability);

internal sealed record FacePatchDescriptor(
    SourceSurfaceDescriptor SourceSurface,
    IReadOnlyList<TrimCurveDescriptor> OuterLoop,
    IReadOnlyList<IReadOnlyList<TrimCurveDescriptor>> InnerLoops,
    FacePatchOrientationRole Orientation,
    string Role,
    IReadOnlyList<string> AdjacencyHints);

internal sealed record SurfaceMaterializerAdmissibility(
    bool IsAdmissible,
    string Reason,
    double Score,
    bool IsDeferred = false);

internal interface ISurfaceFamilyMaterializer
{
    SurfacePatchFamily Family { get; }
    string Name { get; }
    SurfaceMaterializerAdmissibility Evaluate(FacePatchDescriptor patch);
}

internal static class SurfaceFamilyMaterializerRegistry
{
    private static readonly JudgmentEngine<FacePatchDescriptor> Engine = new();
    private static readonly ISurfaceFamilyMaterializer[] Materializers =
    [
        new PlanarSurfaceMaterializer(),
        new CylindricalSurfaceMaterializer(),
        new ConicalSurfaceMaterializer(),
        new SphericalSurfaceMaterializer(),
        new ToroidalSurfaceMaterializer(),
        new SplineSurfaceMaterializer()
    ];

    internal static SurfaceFamilyMaterializerEvaluation Evaluate(FacePatchDescriptor patch)
    {
        var evaluations = Materializers.Select(m => new { Materializer = m, Admissibility = m.Evaluate(patch) }).ToArray();
        var candidates = evaluations.Select((entry, i) => new JudgmentCandidate<FacePatchDescriptor>(entry.Materializer.Name, _ => entry.Admissibility.IsAdmissible, _ => entry.Admissibility.Score, _ => entry.Admissibility.Reason, i)).ToArray();
        var judgment = Engine.Evaluate(patch, candidates);
        var rejected = evaluations
            .Where(e => !e.Admissibility.IsAdmissible)
            .Select(e => new JudgmentRejection(e.Materializer.Name, e.Admissibility.Reason))
            .ToArray();
        if (!judgment.IsSuccess)
        {
            return new(null, false, "No surface-family materializer admitted patch.", rejected);
        }

        return new(Materializers.Single(m => m.Name == judgment.Selection!.Value.Candidate.Name), true, "admissible", rejected);
    }
}

internal sealed record SurfaceFamilyMaterializerEvaluation(
    ISurfaceFamilyMaterializer? Selected,
    bool IsSuccess,
    string Message,
    IReadOnlyList<JudgmentRejection> Rejections);

internal sealed class PlanarSurfaceMaterializer : ISurfaceFamilyMaterializer
{
    public SurfacePatchFamily Family => SurfacePatchFamily.Planar;
    public string Name => "surface_family_planar";

    public SurfaceMaterializerAdmissibility Evaluate(FacePatchDescriptor patch)
    {
        if (patch.SourceSurface.Family != SurfacePatchFamily.Planar) return new(false, "Source surface family mismatch.", 0d);
        if (patch.OuterLoop.Any(t => t.Capability != TrimCurveCapability.ExactSupported) || patch.InnerLoops.SelectMany(l => l).Any(t => t.Capability != TrimCurveCapability.ExactSupported))
        {
            return new(false, "Trim capability requires exact-supported curves for planar scaffold.", 0d);
        }

        return new(true, "admissible", 10d);
    }
}

internal sealed class CylindricalSurfaceMaterializer : ISurfaceFamilyMaterializer
{
    public SurfacePatchFamily Family => SurfacePatchFamily.Cylindrical;
    public string Name => "surface_family_cylindrical";
    public SurfaceMaterializerAdmissibility Evaluate(FacePatchDescriptor patch)
        => patch.SourceSurface.Family == SurfacePatchFamily.Cylindrical
            && patch.OuterLoop.All(t => t.Capability == TrimCurveCapability.ExactSupported)
            ? new(true, "admissible", 9d)
            : new(false, "Requires cylindrical source and exact-supported trims.", 0d);
}

internal sealed class ConicalSurfaceMaterializer : ISurfaceFamilyMaterializer
{
    public SurfacePatchFamily Family => SurfacePatchFamily.Conical;
    public string Name => "surface_family_conical";
    public SurfaceMaterializerAdmissibility Evaluate(FacePatchDescriptor patch)
        => patch.SourceSurface.Family == SurfacePatchFamily.Conical
            ? new(false, "Conical descriptor family recognized, materialization deferred in CIR-F8.1.", 1d, IsDeferred: true)
            : new(false, "Source surface family mismatch.", 0d);
}

internal sealed class SphericalSurfaceMaterializer : ISurfaceFamilyMaterializer
{
    public SurfacePatchFamily Family => SurfacePatchFamily.Spherical;
    public string Name => "surface_family_spherical";
    public SurfaceMaterializerAdmissibility Evaluate(FacePatchDescriptor patch)
        => patch.SourceSurface.Family == SurfacePatchFamily.Spherical
            && patch.OuterLoop.All(t => t.Capability == TrimCurveCapability.ExactSupported)
            ? new(true, "admissible", 8d)
            : new(false, "Requires spherical source and exact-supported trims.", 0d);
}

internal sealed class ToroidalSurfaceMaterializer : ISurfaceFamilyMaterializer
{
    public SurfacePatchFamily Family => SurfacePatchFamily.Toroidal;
    public string Name => "surface_family_toroidal";
    public SurfaceMaterializerAdmissibility Evaluate(FacePatchDescriptor patch)
        => patch.SourceSurface.Family == SurfacePatchFamily.Toroidal
            ? new(false, "Toroidal descriptor family recognized, materialization deferred in CIR-F8.1.", 1d, IsDeferred: true)
            : new(false, "Source surface family mismatch.", 0d);
}

internal sealed class SplineSurfaceMaterializer : ISurfaceFamilyMaterializer
{
    public SurfacePatchFamily Family => SurfacePatchFamily.Spline;
    public string Name => "surface_family_spline";
    public SurfaceMaterializerAdmissibility Evaluate(FacePatchDescriptor patch)
        => patch.SourceSurface.Family == SurfacePatchFamily.Spline
            ? new(false, "Spline descriptor family recognized, materialization deferred in CIR-F8.1.", 1d, IsDeferred: true)
            : new(false, "Source surface family mismatch.", 0d);
}
