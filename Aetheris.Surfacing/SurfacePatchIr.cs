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
    public static SurfacePatchMetadata From(BoundedSurfacePatch patch, int boundaryLoops = 1) => new(
        patch.PatchId, patch.SurfaceClass, patch.DegreeU, patch.DegreeV, patch.ControlCountU, patch.ControlCountV,
        patch.ParameterDomain, boundaryLoops, patch.BoundaryLoop.Boundaries, patch.ExportClass);
}

public sealed record TrimRegionResult(bool IsSuccess, BoundedSurfacePatch? Patch, IReadOnlyList<SculptDiagnostic> Diagnostics);

public enum SurfaceExtensionMethod { AnalyticIdentity, EndpointTangentContinuation }

public sealed record ExtendedSurfaceSupport(
    SurfaceGeometry OriginalSupport,
    SurfaceParameterDomain OriginalDomain,
    SurfaceParameterDomain ExtendedDomain,
    SurfaceExtensionMethod Method,
    string ContinuityAtOriginalBoundary,
    Func<double, double, Point3D> Evaluator)
{
    public Point3D Evaluate(double u, double v)
    {
        if (u < ExtendedDomain.UMin || u > ExtendedDomain.UMax || v < ExtendedDomain.VMin || v > ExtendedDomain.VMax)
            throw new ArgumentOutOfRangeException(nameof(u), "Evaluation is outside the authorized extension domain.");
        return Evaluator(u, v);
    }
}

public sealed record SurfaceExtensionResult(bool IsSuccess, ExtendedSurfaceSupport? Support, IReadOnlyList<SculptDiagnostic> Diagnostics);

public static class SurfaceSupportExtension
{
    public static SurfaceExtensionResult Extend(SurfaceGeometry support, SurfaceParameterDomain original, SurfaceParameterDomain requested,
        double maximumRelativeExtension = .25d)
    {
        if (!original.IsValid || !requested.IsValid || requested.UMin > original.UMin || requested.UMax < original.UMax
            || requested.VMin > original.VMin || requested.VMax < original.VMax)
            return Failure("surf-extension-domain-invalid", "The requested domain must finitely contain the original domain.");
        var uSpan = original.UMax - original.UMin;
        var vSpan = original.VMax - original.VMin;
        if (original.UMin - requested.UMin > uSpan * maximumRelativeExtension
            || requested.UMax - original.UMax > uSpan * maximumRelativeExtension
            || original.VMin - requested.VMin > vSpan * maximumRelativeExtension
            || requested.VMax - original.VMax > vSpan * maximumRelativeExtension)
            return Failure("surf-extension-unsupported", $"Extension exceeds the bounded {maximumRelativeExtension:P0} per-side stability envelope.");

        if (support.Kind is SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Cone)
        {
            Point3D EvaluateAnalytic(double u, double v) => support.Kind switch
            {
                SurfaceGeometryKind.Plane => support.Plane!.Value.Evaluate(u, v),
                SurfaceGeometryKind.Cylinder => support.Cylinder!.Value.Evaluate(u, v),
                SurfaceGeometryKind.Cone => support.Cone!.Value.Evaluate(u, v),
                _ => throw new InvalidOperationException()
            };
            return new(true, new(support, original, requested, SurfaceExtensionMethod.AnalyticIdentity, "Exact analytic continuation", EvaluateAnalytic), []);
        }
        if (support.BSplineSurfaceWithKnots is not { } spline)
            return Failure("surf-extension-unsupported", $"Surface family {support.Kind} is not in the bounded extension matrix.");
        if (spline.DegreeU > 3 || spline.DegreeV > 3 || spline.SelfIntersect)
            return Failure("surf-extension-unsupported", "Endpoint-tangent continuation is limited to non-self-intersecting degree <= 3 B-spline supports.");

        Point3D EvaluateSpline(double u, double v)
        {
            var uc = System.Math.Clamp(u, original.UMin, original.UMax);
            var vc = System.Math.Clamp(v, original.VMin, original.VMax);
            var basePoint = spline.Evaluate(uc, vc);
            var hu = double.Max(uSpan * 1e-4d, 1e-7d);
            var hv = double.Max(vSpan * 1e-4d, 1e-7d);
            var u0 = System.Math.Clamp(uc - hu, original.UMin, original.UMax);
            var u1 = System.Math.Clamp(uc + hu, original.UMin, original.UMax);
            var v0 = System.Math.Clamp(vc - hv, original.VMin, original.VMax);
            var v1 = System.Math.Clamp(vc + hv, original.VMin, original.VMax);
            var du = (spline.Evaluate(u1, vc) - spline.Evaluate(u0, vc)) / (u1 - u0);
            var dv = (spline.Evaluate(uc, v1) - spline.Evaluate(uc, v0)) / (v1 - v0);
            return basePoint + (du * (u - uc)) + (dv * (v - vc));
        }
        return new(true, new(support, original, requested, SurfaceExtensionMethod.EndpointTangentContinuation,
            "C1 by endpoint first-derivative continuation; mixed outside-corner term intentionally zero", EvaluateSpline), []);
    }

    private static SurfaceExtensionResult Failure(string code, string message) => new(false, null, [new(code, message)]);
}

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
