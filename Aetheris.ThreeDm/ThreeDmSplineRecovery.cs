using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Rhino.Geometry;
using KernelPoint = Aetheris.Kernel.Core.Math.Point3D;

namespace Aetheris.ThreeDm;

/// <summary>Import-only bridge from OpenNURBS source nets to the shared STEP spline reducers.</summary>
internal static class ThreeDmSplineRecovery
{
    internal static BSpline3Curve SourceCurve(NurbsCurve source, double scale)
    {
        var controls = Enumerable.Range(0, source.Points.Count)
            .Select(i => source.Points[i].Location)
            .Select(p => new KernelPoint(p.X * scale, p.Y * scale, p.Z * scale)).ToArray();
        var (values, multiplicities) = Knots(Enumerable.Range(0, source.Knots.Count)
            .Select(i => source.Knots[i]).ToArray());
        return new BSpline3Curve(source.Degree, controls, multiplicities, values,
            "UNSPECIFIED", source.IsClosed, false, "UNSPECIFIED");
    }

    internal static BSplineSurfaceWithKnots SourceSurface(NurbsSurface source, double scale)
    {
        var controls = new IReadOnlyList<KernelPoint>[source.Points.CountU];
        var weights = new IReadOnlyList<double>[source.Points.CountU];
        for (var u = 0; u < source.Points.CountU; u++)
        {
            var row = new KernelPoint[source.Points.CountV];
            var weightRow = new double[source.Points.CountV];
            for (var v = 0; v < source.Points.CountV; v++)
            {
                var point = source.Points.GetControlPoint(u, v);
                row[v] = new KernelPoint(point.Location.X * scale, point.Location.Y * scale, point.Location.Z * scale);
                weightRow[v] = point.Weight;
            }
            controls[u] = row;
            weights[u] = weightRow;
        }
        var (valuesU, multiplicitiesU) = Knots(Enumerable.Range(0, source.KnotsU.Count)
            .Select(i => source.KnotsU[i]).ToArray());
        var (valuesV, multiplicitiesV) = Knots(Enumerable.Range(0, source.KnotsV.Count)
            .Select(i => source.KnotsV[i]).ToArray());
        return new BSplineSurfaceWithKnots(source.OrderU - 1, source.OrderV - 1,
            controls, "UNSPECIFIED", source.IsClosed(0), source.IsClosed(1), false,
            multiplicitiesU, multiplicitiesV, valuesU, valuesV, "UNSPECIFIED", weights);
    }

    internal static RecoveryCandidate RecoverCurve(NurbsCurve source, double scale, double tolerance)
    {
        try
        {
            var weights = Enumerable.Range(0, source.Points.Count).Select(i => source.Points[i].Weight).ToArray();
            var curve = SourceCurve(source, scale);
            if (BSplineCurveRationalReduction.TryReduce(curve, weights, tolerance,
                out var reduced, out var deviation, out var reason))
            {
                var sourceDomain = source.Domain;
                var distances = Enumerable.Range(0, 65).Select(i =>
                {
                    var parameter = sourceDomain.T0 + (sourceDomain.T1 - sourceDomain.T0) * i / 64d;
                    var actual = source.PointAt(parameter);
                    var candidate = reduced.Evaluate(parameter);
                    return Distance(actual, candidate, scale);
                }).ToArray();
                return Candidate("RecoveredSpline", "AdaptiveCubic", tolerance,
                    double.Max(deviation, distances.Max()), reduced.ControlPoints.Count, 0, reason, distances);
            }
            return Candidate("RecoveredSpline", "AdaptiveCubic", tolerance, deviation, 0, 0, reason);
        }
        catch (ArgumentException ex)
        {
            return Candidate("RecoveredSpline", "AdaptiveCubic", tolerance, double.PositiveInfinity, 0, 0, ex.Message);
        }
    }

    internal static RecoveryCandidate RecoverSurface(NurbsSurface source, double scale, double tolerance)
    {
        try
        {
            var surface = SourceSurface(source, scale);
            RecoveryCandidate? last = null;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var workingTolerance = tolerance / (1 << attempt);
                if (!BSplineSurfaceRationalReduction.TryReduce(surface, workingTolerance,
                    out var reduced, out var deviation, out var reason) || reduced is null)
                    return last ?? Candidate("RecoveredSpline", "AdaptiveGreville", tolerance, deviation, 0, 0, reason);
                var domainU = source.Domain(0);
                var domainV = source.Domain(1);
                var distances = new double[17 * 17];
                for (var u = 0; u < 17; u++)
                for (var v = 0; v < 17; v++)
                {
                    var parameterU = domainU.T0 + (domainU.T1 - domainU.T0) * u / 16d;
                    var parameterV = domainV.T0 + (domainV.T1 - domainV.T0) * v / 16d;
                    distances[u * 17 + v] = Distance(source.PointAt(parameterU, parameterV),
                        reduced.Evaluate(parameterU, parameterV), scale);
                }
                var measured = double.Max(deviation, distances.Max());
                last = Candidate("RecoveredSpline", "AdaptiveGreville", tolerance,
                    measured, reduced.ControlPoints.Count, reduced.ControlPoints[0].Count,
                    $"{reason} Independent Rhino maximum {distances.Max():G6} mm; refinement attempt {attempt + 1}/3.", distances);
                if (measured <= tolerance) return last;
            }
            return last!;
        }
        catch (ArgumentException ex)
        {
            return Candidate("RecoveredSpline", "AdaptiveGreville", tolerance, double.PositiveInfinity, 0, 0, ex.Message);
        }
    }

    // OpenNURBS stores the interior/full knot vector without one knot at either end.
    private static (double[] Values, int[] Multiplicities) Knots(IReadOnlyList<double> source)
    {
        if (source.Count == 0) throw new ArgumentException("Source knot vector is empty.");
        var values = new List<double>();
        var multiplicities = new List<int>();
        for (var i = -1; i <= source.Count; i++)
        {
            var knot = source[int.Clamp(i, 0, source.Count - 1)];
            if (values.Count > 0 && knot == values[^1]) multiplicities[^1]++;
            else { values.Add(knot); multiplicities.Add(1); }
        }
        return (values.ToArray(), multiplicities.ToArray());
    }

    private static double Distance(Point3d source, KernelPoint recovered, double scale) =>
        double.Sqrt(double.Pow(source.X * scale - recovered.X, 2) +
                    double.Pow(source.Y * scale - recovered.Y, 2) +
                    double.Pow(source.Z * scale - recovered.Z, 2));

    private static RecoveryCandidate Candidate(string kind, string method, double tolerance,
        double deviation, int controlsU, int controlsV, string reason, double[]? independentDistances = null) =>
        new(kind, method,
            new RecoveryResidual(
                independentDistances is null ? 0 : double.Sqrt(independentDistances.Average(d => d * d)),
                independentDistances is null ? 0 : independentDistances.OrderBy(d => d).ElementAt(
                    (int)double.Ceiling(0.95 * independentDistances.Length) - 1),
                deviation, 0, null, independentDistances?.Length ?? 0),
            double.IsFinite(deviation) && deviation <= tolerance
                ? RecoveryQualification.WithinRecoveryTolerance : RecoveryQualification.Unresolved,
            new Dictionary<string, double> { ["controlPointsU"] = controlsU, ["controlPointsV"] = controlsV,
                ["recoveryToleranceMillimetres"] = tolerance },
            $"{reason} Max includes shared reducer validation and independent Rhino evaluation; RMS/p95 use independent samples. Support geometry only; edge/trim/loop binding and topology remain unqualified.");
}
