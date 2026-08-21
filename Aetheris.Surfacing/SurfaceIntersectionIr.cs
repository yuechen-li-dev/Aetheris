using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public enum SurfaceIntersectionClassification
{
    NoIntersection,
    SingleCurve,
    MultipleCurves,
    TangentContact,
    CoincidentRegion,
    Ambiguous,
    NumericalFailure,
    Unsupported
}

public sealed record SurfaceIntersectionToleranceEvidence(
    double IntersectionTolerance,
    double PcurveTolerance,
    double MaximumResidual,
    int Samples,
    string Method);

public sealed record SurfaceIntersectionBranch(
    string StableId,
    CurveGeometry Curve3D,
    ParameterInterval CurveDomain,
    PcurveGeometry PcurveOnA,
    PcurveGeometry PcurveOnB,
    bool OrientationOnA,
    bool OrientationOnB,
    SurfaceIntersectionToleranceEvidence Evidence);

public sealed record SurfaceIntersectionResult(
    string SupportA,
    string SupportB,
    SurfaceIntersectionClassification Classification,
    IReadOnlyList<SurfaceIntersectionBranch> Branches,
    string? SelectedBranch,
    IReadOnlyList<string> Diagnostics)
{
    public bool IsSuccess => Classification is SurfaceIntersectionClassification.SingleCurve or SurfaceIntersectionClassification.MultipleCurves
        && Branches.Count > 0;
}

public sealed record SurfaceIntersectionRequest(
    string SupportA,
    SurfaceGeometry SurfaceA,
    SurfaceParameterDomain DomainA,
    string SupportB,
    SurfaceGeometry SurfaceB,
    SurfaceParameterDomain DomainB,
    double IntersectionTolerance = 1e-7,
    double PcurveTolerance = 1e-6,
    SurfaceParameterPoint? SeedOnA = null);

/// <summary>
/// Bounded, explicitly qualified surface-intersection facade. It retains exact curves for the
/// analytic and isoparametric cases it claims and rejects all other combinations by classification.
/// </summary>
public static class BoundedSurfaceIntersector
{
    public static SurfaceIntersectionResult Intersect(SurfaceIntersectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.DomainA.IsValid || !request.DomainB.IsValid || request.IntersectionTolerance <= 0d || request.PcurveTolerance <= 0d)
            return Failure(request, SurfaceIntersectionClassification.NumericalFailure, "surf-intersection-domain-invalid");

        if (request.SurfaceA.Plane is { } parallelA && request.SurfaceB.Plane is { } parallelB
            && parallelA.Normal.ToVector().Cross(parallelB.Normal.ToVector()).Length <= request.IntersectionTolerance)
        {
            var separation = double.Abs((parallelB.Origin - parallelA.Origin).Dot(parallelA.Normal.ToVector()));
            return Failure(request, separation <= request.IntersectionTolerance
                ? SurfaceIntersectionClassification.CoincidentRegion : SurfaceIntersectionClassification.NoIntersection,
                separation <= request.IntersectionTolerance ? "surf-intersection-coincident-region" : "surf-intersection-none");
        }

        var branches = (request.SurfaceA.Kind, request.SurfaceB.Kind) switch
        {
            (SurfaceGeometryKind.Plane, SurfaceGeometryKind.Plane) => PlanePlane(request),
            (SurfaceGeometryKind.Plane, SurfaceGeometryKind.Cylinder) => PlaneCylinder(request, false),
            (SurfaceGeometryKind.Cylinder, SurfaceGeometryKind.Plane) => PlaneCylinder(Swap(request), true),
            (SurfaceGeometryKind.Plane, SurfaceGeometryKind.BSplineSurfaceWithKnots) => PlaneSpline(request, false),
            (SurfaceGeometryKind.BSplineSurfaceWithKnots, SurfaceGeometryKind.Plane) => PlaneSpline(Swap(request), true),
            _ => null
        };
        if (branches is null) return Failure(request, SurfaceIntersectionClassification.Unsupported, "surf-intersection-class-unsupported");
        if (branches.Count == 0) return Failure(request, SurfaceIntersectionClassification.NoIntersection, "surf-intersection-none");

        branches = branches.OrderBy(branch => branch.StableId, StringComparer.Ordinal).ToArray();
        var selected = SelectBranch(branches, request.SeedOnA);
        return new(request.SupportA, request.SupportB,
            branches.Count == 1 ? SurfaceIntersectionClassification.SingleCurve : SurfaceIntersectionClassification.MultipleCurves,
            branches, selected, selected is null ? ["surf-intersection-ambiguous"] : ["surf-intersection-qualified", "surf-branch-selected-by-judgment"]);
    }

    private static IReadOnlyList<SurfaceIntersectionBranch>? PlanePlane(SurfaceIntersectionRequest request)
    {
        var a = request.SurfaceA.Plane!.Value;
        var b = request.SurfaceB.Plane!.Value;
        var na = a.Normal.ToVector();
        var nb = b.Normal.ToVector();
        var direction = na.Cross(nb);
        if (direction.Length <= request.IntersectionTolerance)
        {
            var separation = double.Abs((b.Origin - a.Origin).Dot(na));
            return separation <= request.IntersectionTolerance ? null : [];
        }
        var ca = na.Dot(a.Origin - Point3D.Origin);
        var cb = nb.Dot(b.Origin - Point3D.Origin);
        var pointVector = ((nb.Cross(direction) * ca) + (direction.Cross(na) * cb)) / direction.LengthSquared;
        var origin = Point3D.Origin + pointVector;
        var dir = Direction3D.Create(direction);
        var interval = new ParameterInterval(-DomainScale(request.DomainA), DomainScale(request.DomainA));
        var start = origin + (dir.ToVector() * interval.Start);
        var end = origin + (dir.ToVector() * interval.End);
        var pa = PcurveGeometry.Line(interval, PlaneUv(a, start), PlaneUv(a, end));
        var pb = PcurveGeometry.Line(interval, PlaneUv(b, start), PlaneUv(b, end));
        return [Branch(request, "line", CurveGeometry.FromLine(new Line3Curve(origin, dir)), interval, pa, pb)];
    }

    private static IReadOnlyList<SurfaceIntersectionBranch>? PlaneCylinder(SurfaceIntersectionRequest request, bool reverseOutput)
    {
        var plane = request.SurfaceA.Plane!.Value;
        var cylinder = request.SurfaceB.Cylinder!.Value;
        var alignment = double.Abs(plane.Normal.ToVector().Dot(cylinder.Axis.ToVector()));
        if (alignment < 1d - request.IntersectionTolerance) return null;
        var axial = (plane.Origin - cylinder.Origin).Dot(cylinder.Axis.ToVector()) / plane.Normal.ToVector().Dot(cylinder.Axis.ToVector());
        if (axial < request.DomainB.VMin - request.IntersectionTolerance || axial > request.DomainB.VMax + request.IntersectionTolerance) return [];
        var center = cylinder.Origin + (cylinder.Axis.ToVector() * axial);
        var interval = new ParameterInterval(request.DomainB.UMin, request.DomainB.UMax);
        var circle = CurveGeometry.FromCircle(new Circle3Curve(center, cylinder.Axis, cylinder.Radius, cylinder.XAxis));
        var onPlane = PcurveGeometry.Circle(interval, PlaneUv(plane, center), cylinder.Radius, cylinder.Radius);
        var onCylinder = PcurveGeometry.Line(interval, new(interval.Start, axial), new(interval.End, axial));
        var branch = reverseOutput
            ? Branch(request, "circle", circle, interval, onCylinder, onPlane)
            : Branch(request, "circle", circle, interval, onPlane, onCylinder);
        return [branch];
    }

    private static IReadOnlyList<SurfaceIntersectionBranch>? PlaneSpline(SurfaceIntersectionRequest request, bool reverseOutput)
    {
        var plane = request.SurfaceA.Plane!.Value;
        var spline = request.SurfaceB.BSplineSurfaceWithKnots!;
        var branches = new List<SurfaceIntersectionBranch>();
        foreach (var uIndex in new[] { 0, spline.ControlPoints.Count - 1 }.Distinct())
        {
            var row = spline.ControlPoints[uIndex];
            if (!row.All(point => OnPlane(plane, point, request.IntersectionTolerance))) continue;
            var u = Greville(spline.FullKnotsU, spline.DegreeU, uIndex);
            if (u < request.DomainB.UMin - request.IntersectionTolerance || u > request.DomainB.UMax + request.IntersectionTolerance) continue;
            var curve = new BSpline3Curve(spline.DegreeV, row, spline.KnotMultiplicitiesV, spline.KnotValuesV, "UNSPECIFIED", spline.VClosed, false, spline.KnotSpec);
            var interval = new ParameterInterval(request.DomainB.VMin, request.DomainB.VMax);
            var onSpline = PcurveGeometry.Line(interval, new(u, interval.Start), new(u, interval.End));
            var onPlane = PcurveGeometry.Polyline(interval, Sample(interval, 65, t => PlaneUv(plane, curve.Evaluate(t))));
            branches.Add(reverseOutput
                ? Branch(request, $"iso-u-{u:R}", CurveGeometry.FromBSpline(curve), interval, onSpline, onPlane)
                : Branch(request, $"iso-u-{u:R}", CurveGeometry.FromBSpline(curve), interval, onPlane, onSpline));
        }
        foreach (var vIndex in new[] { 0, spline.ControlPoints[0].Count - 1 }.Distinct())
        {
            var column = spline.ControlPoints.Select(row => row[vIndex]).ToArray();
            if (!column.All(point => OnPlane(plane, point, request.IntersectionTolerance))) continue;
            var v = Greville(spline.FullKnotsV, spline.DegreeV, vIndex);
            if (v < request.DomainB.VMin - request.IntersectionTolerance || v > request.DomainB.VMax + request.IntersectionTolerance) continue;
            var curve = new BSpline3Curve(spline.DegreeU, column, spline.KnotMultiplicitiesU, spline.KnotValuesU, "UNSPECIFIED", spline.UClosed, false, spline.KnotSpec);
            var interval = new ParameterInterval(request.DomainB.UMin, request.DomainB.UMax);
            var onSpline = PcurveGeometry.Line(interval, new(interval.Start, v), new(interval.End, v));
            var onPlane = PcurveGeometry.Polyline(interval, Sample(interval, 65, t => PlaneUv(plane, curve.Evaluate(t))));
            branches.Add(reverseOutput
                ? Branch(request, $"iso-v-{v:R}", CurveGeometry.FromBSpline(curve), interval, onSpline, onPlane)
                : Branch(request, $"iso-v-{v:R}", CurveGeometry.FromBSpline(curve), interval, onPlane, onSpline));
        }
        return branches.GroupBy(branch => branch.StableId, StringComparer.Ordinal).Select(group => group.First()).ToArray();
    }

    private static string? SelectBranch(IReadOnlyList<SurfaceIntersectionBranch> branches, SurfaceParameterPoint? seed)
    {
        if (branches.Count == 1) return branches[0].StableId;
        var context = new BranchSelectionContext(branches, seed);
        var candidates = branches.Select((branch, index) => new JudgmentCandidate<BranchSelectionContext>(branch.StableId,
            _ => seed.HasValue,
            c => -DistanceSquared(branch.PcurveOnA.Evaluate(Mid(branch.CurveDomain)), c.Seed!.Value),
            _ => "Multiple branches require a seed/reference boundary.", index)).ToArray();
        var result = new JudgmentEngine<BranchSelectionContext>().Evaluate(context, candidates);
        return result.IsSuccess ? result.Selection!.Value.Candidate.Name : null;
    }

    private static SurfaceIntersectionBranch Branch(SurfaceIntersectionRequest request, string suffix, CurveGeometry curve,
        ParameterInterval interval, PcurveGeometry a, PcurveGeometry b)
        => new($"{request.SupportA}|{request.SupportB}|{suffix}", curve, interval, a, b, true, true,
            new(request.IntersectionTolerance, request.PcurveTolerance, 0d, 65, "exact-qualified"));

    private static SurfaceIntersectionRequest Swap(SurfaceIntersectionRequest r)
        => new(r.SupportB, r.SurfaceB, r.DomainB, r.SupportA, r.SurfaceA, r.DomainA, r.IntersectionTolerance, r.PcurveTolerance, r.SeedOnA);
    private static SurfaceIntersectionResult Failure(SurfaceIntersectionRequest r, SurfaceIntersectionClassification c, string d)
        => new(r.SupportA, r.SupportB, c, [], null, [d]);
    private static bool OnPlane(PlaneSurface plane, Point3D point, double tolerance)
        => double.Abs((point - plane.Origin).Dot(plane.Normal.ToVector())) <= tolerance;
    private static SurfaceParameterPoint PlaneUv(PlaneSurface plane, Point3D point)
    {
        var delta = point - plane.Origin;
        return new(delta.Dot(plane.UAxis.ToVector()), delta.Dot(plane.VAxis.ToVector()));
    }
    private static double Greville(IReadOnlyList<double> knots, int degree, int index)
        => Enumerable.Range(1, degree).Average(offset => knots[index + offset]);
    private static double DomainScale(SurfaceParameterDomain domain)
        => double.Max(domain.UMax - domain.UMin, domain.VMax - domain.VMin);
    private static double Mid(ParameterInterval interval) => (interval.Start + interval.End) * .5d;
    private static double DistanceSquared(SurfaceParameterPoint a, SurfaceParameterPoint b)
        => ((a.U - b.U) * (a.U - b.U)) + ((a.V - b.V) * (a.V - b.V));
    private static IReadOnlyList<SurfaceParameterPoint> Sample(ParameterInterval interval, int count, Func<double, SurfaceParameterPoint> evaluator)
        => Enumerable.Range(0, count).Select(i => evaluator(interval.Start + ((interval.End - interval.Start) * i / (count - 1d)))).ToArray();
    private sealed record BranchSelectionContext(IReadOnlyList<SurfaceIntersectionBranch> Branches, SurfaceParameterPoint? Seed);
}
