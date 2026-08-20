using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public readonly record struct SurfaceParameterDomain(double UMin, double UMax, double VMin, double VMax)
{
    public bool IsValid => double.IsFinite(UMin) && double.IsFinite(UMax) && double.IsFinite(VMin) && double.IsFinite(VMax)
        && UMin < UMax && VMin < VMax;
}

public enum SurfacePatchClass { Analytic, NonRationalBSpline, InternalRationalNormalized }
public enum PatchBoundarySide { South, East, North, West }
public enum PatchBoundaryContinuity { G0, G1 }

public sealed record PatchBoundaryCorrespondence(
    string StableId,
    PatchBoundarySide PatchSide,
    string ExistingBoundary,
    PatchBoundaryContinuity Continuity);

public sealed record SurfaceBoundaryLoop(
    string StableId,
    IReadOnlyList<PatchBoundaryCorrespondence> Boundaries,
    bool IsOuter = true)
{
    public IReadOnlyList<SculptDiagnostic> Validate()
    {
        var diagnostics = new List<SculptDiagnostic>();
        if (!IsOuter) diagnostics.Add(new("surf-boundary-inner-loop-unsupported", "SURF-X1 currently supports one outer replacement loop and no holes in the replacement patch."));
        if (Boundaries.Count != 4 || Boundaries.Select(x => x.PatchSide).Distinct().Count() != 4)
            diagnostics.Add(new("surf-boundary-loop-invalid", "A rectangular replacement loop requires exactly one correspondence for South, East, North, and West."));
        if (Boundaries.Any(x => string.IsNullOrWhiteSpace(x.ExistingBoundary)))
            diagnostics.Add(new("surf-boundary-correspondence-invalid", "Every patch boundary must identify the existing body boundary it replaces."));
        return diagnostics;
    }
}

public abstract record BoundedSurfacePatch(
    string PatchId,
    SurfacePatchClass SurfaceClass,
    SurfaceParameterDomain ParameterDomain,
    SurfaceBoundaryLoop BoundaryLoop,
    bool ReversedOrientation)
{
    public abstract SurfaceGeometry Support { get; }
    public abstract Point3D Evaluate(double u, double v);
    public abstract int? DegreeU { get; }
    public abstract int? DegreeV { get; }
    public abstract int? ControlCountU { get; }
    public abstract int? ControlCountV { get; }
    public abstract string ExportClass { get; }
    public abstract IReadOnlyList<SculptDiagnostic> Validate();
}

public sealed record AnalyticSurfacePatch(
    string Id,
    SurfaceGeometry AnalyticSupport,
    SurfaceParameterDomain Domain,
    SurfaceBoundaryLoop Loop,
    Func<double, double, Point3D> Evaluator,
    bool Reversed = false)
    : BoundedSurfacePatch(Id, SurfacePatchClass.Analytic, Domain, Loop, Reversed)
{
    public override SurfaceGeometry Support => AnalyticSupport;
    public override Point3D Evaluate(double u, double v) => Evaluator(u, v);
    public override int? DegreeU => null;
    public override int? DegreeV => null;
    public override int? ControlCountU => null;
    public override int? ControlCountV => null;
    public override string ExportClass => AnalyticSupport.Kind.ToString();
    public override IReadOnlyList<SculptDiagnostic> Validate()
    {
        var diagnostics = Loop.Validate().ToList();
        if (!Domain.IsValid) diagnostics.Add(new("surf-patch-domain-invalid", "The analytic patch parameter domain must be finite and non-empty.", PatchId));
        if (AnalyticSupport.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots)
            diagnostics.Add(new("surf-patch-class-invalid", "An analytic patch cannot wrap a B-spline support.", PatchId));
        return diagnostics;
    }
}

public sealed record BSplineSurfacePatch : BoundedSurfacePatch
{
    public BSplineSurfacePatch(string patchId, BSplineSurfaceWithKnots spline, SurfaceParameterDomain domain,
        SurfaceBoundaryLoop boundaryLoop, bool reversedOrientation = false,
        SurfacePatchClass surfaceClass = SurfacePatchClass.NonRationalBSpline)
        : base(patchId, surfaceClass, domain, boundaryLoop, reversedOrientation)
    {
        Spline = spline ?? throw new ArgumentNullException(nameof(spline));
    }

    public BSplineSurfaceWithKnots Spline { get; }
    public override SurfaceGeometry Support => SurfaceGeometry.FromBSplineSurfaceWithKnots(Spline);
    public override Point3D Evaluate(double u, double v) => Spline.Evaluate(u, v);
    public override int? DegreeU => Spline.DegreeU;
    public override int? DegreeV => Spline.DegreeV;
    public override int? ControlCountU => Spline.ControlPoints.Count;
    public override int? ControlCountV => Spline.ControlPoints[0].Count;
    public override string ExportClass => "NonRationalBSpline";

    public override IReadOnlyList<SculptDiagnostic> Validate()
    {
        var diagnostics = BoundaryLoop.Validate().ToList();
        if (!ParameterDomain.IsValid || ParameterDomain.UMin < Spline.DomainStartU || ParameterDomain.UMax > Spline.DomainEndU
            || ParameterDomain.VMin < Spline.DomainStartV || ParameterDomain.VMax > Spline.DomainEndV)
            diagnostics.Add(new("surf-patch-domain-invalid", "The bounded patch domain must be finite, non-empty, and contained in the spline knot domain.", PatchId));
        if (SurfaceClass is not (SurfacePatchClass.NonRationalBSpline or SurfacePatchClass.InternalRationalNormalized))
            diagnostics.Add(new("surf-patch-class-invalid", "A B-spline patch must be explicitly non-rational or exactly normalized from removable rationality.", PatchId));
        return diagnostics;
    }
}

public sealed record SurfacePatchMetadata(
    string PatchId,
    SurfacePatchClass SurfaceClass,
    int? DegreeU,
    int? DegreeV,
    int? ControlCountU,
    int? ControlCountV,
    SurfaceParameterDomain ParameterDomain,
    int BoundaryLoops,
    IReadOnlyList<PatchBoundaryCorrespondence> ContinuityContracts,
    string ExportClass)
{
    public static SurfacePatchMetadata From(BoundedSurfacePatch patch) => new(
        patch.PatchId, patch.SurfaceClass, patch.DegreeU, patch.DegreeV, patch.ControlCountU, patch.ControlCountV,
        patch.ParameterDomain, 1, patch.BoundaryLoop.Boundaries, patch.ExportClass);
}

public sealed record TrimRegionResult(bool IsSuccess, BoundedSurfacePatch? Patch, IReadOnlyList<SculptDiagnostic> Diagnostics);

public static class SurfacePatchOperations
{
    public static TrimRegionResult TrimRegion(BSplineSurfacePatch patch, SurfaceParameterDomain domain)
    {
        var trimmed = patch with { ParameterDomain = domain };
        var diagnostics = trimmed.Validate();
        return diagnostics.Count == 0 ? new(true, trimmed, []) : new(false, null, diagnostics);
    }

    public static TrimRegionResult ExtendRegion(BSplineSurfacePatch patch, SurfaceParameterDomain domain)
    {
        if (domain.UMin < patch.Spline.DomainStartU || domain.UMax > patch.Spline.DomainEndU
            || domain.VMin < patch.Spline.DomainStartV || domain.VMax > patch.Spline.DomainEndV)
            return new(false, null, [new("surf-extend-law-unsupported", "A non-rational B-spline may only be extended into its existing knot support in SURF-X1; arbitrary extrapolation is not defined.", patch.PatchId)]);
        return TrimRegion(patch, domain);
    }
}
