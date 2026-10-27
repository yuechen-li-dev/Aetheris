using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Brep;

/// <summary>Whole-span lift bounds for polynomial Bezier patch boundaries.
/// Unsupported representations retain the ordinary sampled qualification.</summary>
internal static class BrepPcurveLiftBounds
{
    private const double MachinePrecision = 2.2204460492503131e-16;

    internal static bool TryBoundaryBound(SurfaceGeometry surface, CurveGeometry curve,
        PcurveGeometry pcurve, ParameterInterval interval, out double bound)
    {
        bound = double.PositiveInfinity;
        if (surface.BSplineSurfaceWithKnots is not { IsRational: false } patch
            || curve.BSpline3 is not { } edge || pcurve.Kind != PcurveGeometryKind.Line
            || pcurve.Points.Count != 2 || pcurve.Domain != interval
            || !IsBezier(patch.DegreeU, patch.ControlPoints.Count, patch.FullKnotsU)
            || !IsBezier(patch.DegreeV, patch.ControlPoints[0].Count, patch.FullKnotsV)
            || !IsBezier(edge.Degree, edge.ControlPoints.Count, edge.FullKnots)
            || !double.IsFinite(interval.Start) || !double.IsFinite(interval.End)
            || !double.IsFinite(interval.End - interval.Start)
            || double.Abs(interval.End - interval.Start) <= 1e-15
            || double.Min(interval.Start, interval.End) < edge.DomainStart
            || double.Max(interval.Start, interval.End) > edge.DomainEnd)
            return false;

        // Derivative control hulls bound the support's sensitivity to UV error.
        // This also covers a recovered line slightly displaced from the ideal
        // boundary: its endpoints bound its entire affine displacement.
        var lu = 0d; var lv = 0d; var scale = 1d;
        for (var i = 0; i <= patch.DegreeU; i++)
            for (var j = 0; j <= patch.DegreeV; j++)
            {
                var point = patch.ControlPoints[i][j];
                scale = double.Max(scale, Magnitude(point));
                if (i < patch.DegreeU)
                    lu = double.Max(lu, (patch.ControlPoints[i + 1][j] - point).Length);
                if (j < patch.DegreeV)
                    lv = double.Max(lv, (patch.ControlPoints[i][j + 1] - point).Length);
            }
        foreach (var point in edge.ControlPoints) scale = double.Max(scale, Magnitude(point));
        lu *= patch.DegreeU / (patch.DomainEndU - patch.DomainStartU);
        lv *= patch.DegreeV / (patch.DomainEndV - patch.DomainStartV);

        var order = (patch.DegreeU + 1d) * (patch.DegreeV + 1d);
        var roundoff = 128d * MachinePrecision * scale * order * order;
        // Both evaluators snap parameters within 1e-12 of the domain end.
        // Include that displacement and arithmetic allowance instead of calling
        // the observed endpoint fit an exact mathematical identity.
        var uAllowance = ParameterAllowance(patch.DomainStartU, patch.DomainEndU,
            pcurve.Points[0].U, pcurve.Points[1].U, order);
        var vAllowance = ParameterAllowance(patch.DomainStartV, patch.DomainEndV,
            pcurve.Points[0].V, pcurve.Points[1].V, order);
        for (var side = 0; side < 4; side++)
        {
            var fixedU = side < 2;
            var degree = fixedU ? patch.DegreeV : patch.DegreeU;
            var start = fixedU ? patch.DomainStartV : patch.DomainStartU;
            var end = fixedU ? patch.DomainEndV : patch.DomainEndU;
            if (edge.Degree != degree || edge.DomainStart != start || edge.DomainEnd != end) continue;
            for (var reverse = 0; reverse < 2; reverse++)
            {
                var hull = 0d;
                for (var k = 0; k <= degree; k++)
                {
                    var index = reverse == 0 ? k : degree - k;
                    var point = fixedU
                        ? patch.ControlPoints[side == 0 ? 0 : patch.DegreeU][index]
                        : patch.ControlPoints[index][side == 2 ? 0 : patch.DegreeV];
                    hull = double.Max(hull, (edge.ControlPoints[k] - point).Length);
                }
                SurfaceParameterPoint Ideal(double t)
                {
                    var varying = reverse == 0 ? t : end - (t - start);
                    return fixedU
                        ? new(side == 0 ? patch.DomainStartU : patch.DomainEndU, varying)
                        : new(varying, side == 2 ? patch.DomainStartV : patch.DomainEndV);
                }
                var a = Ideal(interval.Start); var b = Ideal(interval.End);
                var du = double.Max(double.Abs(pcurve.Points[0].U - a.U), double.Abs(pcurve.Points[1].U - b.U));
                var dv = double.Max(double.Abs(pcurve.Points[0].V - a.V), double.Abs(pcurve.Points[1].V - b.V));
                // Equal Bernstein bases make the difference a convex combination
                // of paired control differences. Add the UV Lipschitz bound.
                var candidate = double.BitIncrement(hull + lu * (du + uAllowance) + lv * (dv + vAllowance) + roundoff);
                if (double.IsFinite(candidate)) bound = double.Min(bound, candidate);
            }
        }
        return double.IsFinite(bound);
    }

    private static bool IsBezier(int degree, int controls, IReadOnlyList<double> knots)
    {
        // Keep the certificate away from near-degenerate parameter ranges and
        // the evaluator's absolute 1e-12 end-snap scale. These cases still sample.
        if (degree is < 1 or > 8 || controls != degree + 1 || knots.Count != 2 * controls
            || !double.IsFinite(knots[0]) || !double.IsFinite(knots[^1])
            || !double.IsFinite(knots[^1] - knots[0]) || knots[^1] - knots[0] <= 1e-9) return false;
        for (var i = 0; i < controls; i++)
            if (knots[i] != knots[0] || knots[i + controls] != knots[^1]) return false;
        return true;
    }

    private static double Magnitude(Point3D point) => double.Max(double.Abs(point.X), double.Max(double.Abs(point.Y), double.Abs(point.Z)));

    private static double ParameterAllowance(double start, double end, double a, double b, double order) =>
        2e-12 + 128d * MachinePrecision * order * double.Max(1d,
            double.Max(double.Max(double.Abs(start), double.Abs(end)), double.Max(double.Abs(a), double.Abs(b))));
}
