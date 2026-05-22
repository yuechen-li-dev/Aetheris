using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Step242;

internal static class Step242BsplineSurfaceRecoveryLane
{
    private const string AnalyticCylinderCandidate = "analytic_cylinder";
    private const string RejectCandidate = "reject";
    private const double AxisParallelTolerance = 1e-6d;
    private const double RadiusTolerance = 1e-5d;

    internal readonly record struct RecoveryDecision(
        string CandidateName,
        SurfaceGeometry? RecoveredSurface,
        string Reason);

    public static RecoveryDecision Decide(
        Step242ParsedEntity sourceEntity,
        BSplineSurfaceWithKnots surface)
    {
        var probe = ProbeCylinderRecovery(surface);
        var context = new RecoveryContext(
            IsRationalLike: Step242SubsetDecoder.TryGetConstructor(sourceEntity.Instance, "RATIONAL_B_SPLINE_SURFACE") is not null,
            CylinderProbe: probe);

        var engine = new JudgmentEngine<RecoveryContext>();
        var judgment = engine.Evaluate(context, BuildCandidates());
        if (!judgment.IsSuccess || !judgment.Selection.HasValue)
        {
            var reason = judgment.Rejections.Count == 0
                ? "No bounded analytic recovery candidate was admissible."
                : string.Join(" ", judgment.Rejections.Select(r => $"{r.CandidateName}: {r.Reason}."));
            return new RecoveryDecision(RejectCandidate, null, reason);
        }

        if (string.Equals(judgment.Selection.Value.Candidate.Name, AnalyticCylinderCandidate, StringComparison.Ordinal)
            && context.CylinderProbe.Cylinder.HasValue)
        {
            return new RecoveryDecision(
                AnalyticCylinderCandidate,
                SurfaceGeometry.FromCylinder(context.CylinderProbe.Cylinder.Value),
                "Recovered analytic cylinder from rational B-spline surface.");
        }

        var rejectReason = context.IsRationalLike
            ? context.CylinderProbe.Reason
            : "Surface is not a rational B-spline/NURBS-like representation.";
        return new RecoveryDecision(RejectCandidate, null, rejectReason);
    }

    private static IReadOnlyList<JudgmentCandidate<RecoveryContext>> BuildCandidates()
    {
        return
        [
            new JudgmentCandidate<RecoveryContext>(
                Name: AnalyticCylinderCandidate,
                IsAdmissible: When.All<RecoveryContext>(
                    context => context.IsRationalLike,
                    context => context.CylinderProbe.Cylinder.HasValue),
                Score: _ => 100d,
                RejectionReason: context => context.IsRationalLike
                    ? context.CylinderProbe.Reason
                    : "Surface is not a rational B-spline/NURBS-like representation.",
                TieBreakerPriority: 0),
            new JudgmentCandidate<RecoveryContext>(
                Name: RejectCandidate,
                IsAdmissible: _ => true,
                Score: _ => -1d,
                RejectionReason: context => context.CylinderProbe.Reason,
                TieBreakerPriority: 1)
        ];
    }

    private static CylinderProbe ProbeCylinderRecovery(BSplineSurfaceWithKnots surface)
    {
        if (!TryReadTwoRingCubicProfile(surface, out var ring0, out var ring1))
        {
            return new CylinderProbe(null, "Surface does not match bounded 2x4 linear-by-cubic profile required for cylinder recovery.");
        }

        var center0 = Midpoint(ring0[0], ring0[3]);
        var center1 = Midpoint(ring1[0], ring1[3]);
        var axisVector = center1 - center0;
        if (axisVector.Length <= RadiusTolerance || !Direction3D.TryCreate(axisVector, out var axis))
        {
            return new CylinderProbe(null, "Cylinder recovery requires non-degenerate axial separation between profile rings.");
        }

        var radius0Start = (ring0[0] - center0).Length;
        var radius0End = (ring0[3] - center0).Length;
        var radius1Start = (ring1[0] - center1).Length;
        var radius1End = (ring1[3] - center1).Length;
        var radius = (radius0Start + radius0End + radius1Start + radius1End) * 0.25d;
        if (!double.IsFinite(radius) || radius <= RadiusTolerance)
        {
            return new CylinderProbe(null, "Cylinder recovery requires a strictly positive finite radius.");
        }

        if (double.Abs(radius0Start - radius) > RadiusTolerance
            || double.Abs(radius0End - radius) > RadiusTolerance
            || double.Abs(radius1Start - radius) > RadiusTolerance
            || double.Abs(radius1End - radius) > RadiusTolerance)
        {
            return new CylinderProbe(null, "Cylinder recovery requires ring endpoint radii to match within tolerance.");
        }

        var mid0RadiusA = (ring0[1] - center0).Length;
        var mid0RadiusB = (ring0[2] - center0).Length;
        var mid1RadiusA = (ring1[1] - center1).Length;
        var mid1RadiusB = (ring1[2] - center1).Length;
        if (mid0RadiusA <= radius || mid0RadiusB <= radius || mid1RadiusA <= radius || mid1RadiusB <= radius)
        {
            return new CylinderProbe(null, "Cylinder recovery requires rational arc control points that bulge beyond circle radius.");
        }

        for (var i = 0; i < 4; i++)
        {
            var span = ring1[i] - ring0[i];
            if (!Direction3D.TryCreate(span, out var spanDir))
            {
                return new CylinderProbe(null, "Cylinder recovery requires non-degenerate rail spans between profile rings.");
            }

            var parallel = double.Abs(spanDir.ToVector().Dot(axis.ToVector()));
            if (parallel < 1d - AxisParallelTolerance)
            {
                return new CylinderProbe(null, "Cylinder recovery requires profile rails to be axis-parallel.");
            }
        }

        if (!Direction3D.TryCreate(ring0[0] - center0, out var referenceAxis))
        {
            return new CylinderProbe(null, "Cylinder recovery requires a non-degenerate reference axis.");
        }

        return new CylinderProbe(new CylinderSurface(center0, axis, radius, referenceAxis), "Bounded cylinder candidate is admissible.");
    }

    private static bool TryReadTwoRingCubicProfile(BSplineSurfaceWithKnots surface, out Point3D[] ring0, out Point3D[] ring1)
    {
        ring0 = [];
        ring1 = [];

        if (surface.DegreeU == 1
            && surface.DegreeV == 3
            && surface.ControlPoints.Count == 2
            && surface.ControlPoints[0].Count == 4)
        {
            ring0 = [surface.ControlPoints[0][0], surface.ControlPoints[0][1], surface.ControlPoints[0][2], surface.ControlPoints[0][3]];
            ring1 = [surface.ControlPoints[1][0], surface.ControlPoints[1][1], surface.ControlPoints[1][2], surface.ControlPoints[1][3]];
            return true;
        }

        if (surface.DegreeU == 3
            && surface.DegreeV == 1
            && surface.ControlPoints.Count == 4
            && surface.ControlPoints[0].Count == 2)
        {
            ring0 = [surface.ControlPoints[0][0], surface.ControlPoints[1][0], surface.ControlPoints[2][0], surface.ControlPoints[3][0]];
            ring1 = [surface.ControlPoints[0][1], surface.ControlPoints[1][1], surface.ControlPoints[2][1], surface.ControlPoints[3][1]];
            return true;
        }

        return false;
    }

    private static Point3D Midpoint(Point3D a, Point3D b) => new((a.X + b.X) * 0.5d, (a.Y + b.Y) * 0.5d, (a.Z + b.Z) * 0.5d);

    private readonly record struct RecoveryContext(bool IsRationalLike, CylinderProbe CylinderProbe);

    private readonly record struct CylinderProbe(CylinderSurface? Cylinder, string Reason);
}
