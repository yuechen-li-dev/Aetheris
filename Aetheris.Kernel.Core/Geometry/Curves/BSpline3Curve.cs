using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Curves;

public readonly record struct BSpline3Curve
{
    public BSpline3Curve(
        int degree,
        IReadOnlyList<Point3D> controlPoints,
        IReadOnlyList<int> knotMultiplicities,
        IReadOnlyList<double> knotValues,
        string curveForm,
        bool closedCurve,
        bool selfIntersect,
        string knotSpec)
    {
        if (degree < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(degree), "Degree must be greater than or equal to one.");
        }

        if (controlPoints is null || controlPoints.Count < degree + 1)
        {
            throw new ArgumentException("Control point count must be at least degree + 1.", nameof(controlPoints));
        }

        if (knotMultiplicities is null || knotValues is null || knotMultiplicities.Count == 0 || knotMultiplicities.Count != knotValues.Count)
        {
            throw new ArgumentException("Knot multiplicities and values must be non-empty and have matching counts.");
        }

        var fullKnots = ExpandKnots(knotMultiplicities, knotValues);
        var expectedKnotCount = controlPoints.Count + degree + 1;
        if (fullKnots.Count != expectedKnotCount)
        {
            throw new ArgumentException($"Expanded knot count must be {expectedKnotCount} for degree {degree} and {controlPoints.Count} control points.");
        }

        for (var i = 1; i < fullKnots.Count; i++)
        {
            if (fullKnots[i] < fullKnots[i - 1])
            {
                throw new ArgumentException("Expanded knot vector must be non-decreasing.");
            }
        }

        Degree = degree;
        ControlPoints = controlPoints.ToArray();
        KnotMultiplicities = knotMultiplicities.ToArray();
        KnotValues = knotValues.ToArray();
        FullKnots = fullKnots;
        CurveForm = curveForm;
        ClosedCurve = closedCurve;
        SelfIntersect = selfIntersect;
        KnotSpec = knotSpec;

        DomainStart = FullKnots[Degree];
        DomainEnd = FullKnots[FullKnots.Count - Degree - 1];
        if (DomainEnd < DomainStart)
        {
            throw new ArgumentException("Computed knot domain is invalid.");
        }
    }

    public int Degree { get; }

    public IReadOnlyList<Point3D> ControlPoints { get; }

    public IReadOnlyList<int> KnotMultiplicities { get; }

    public IReadOnlyList<double> KnotValues { get; }

    public IReadOnlyList<double> FullKnots { get; }

    public string CurveForm { get; }

    public bool ClosedCurve { get; }

    public bool SelfIntersect { get; }

    public string KnotSpec { get; }

    public double DomainStart { get; }

    public double DomainEnd { get; }

    public Point3D Evaluate(double parameter)
    {
        if (!double.IsFinite(parameter))
        {
            throw new ArgumentOutOfRangeException(nameof(parameter), "Parameter must be finite.");
        }

        var p = Degree;
        var u = ClampToDomain(parameter);

        if (double.Abs(u - DomainEnd) <= 1e-12d)
        {
            return ControlPoints[^1];
        }

        var span = FindSpan(u);
        var d = new Point3D[p + 1];
        for (var j = 0; j <= p; j++)
        {
            d[j] = ControlPoints[span - p + j];
        }

        for (var r = 1; r <= p; r++)
        {
            for (var j = p; j >= r; j--)
            {
                var leftKnot = FullKnots[span - p + j];
                var rightKnot = FullKnots[span + 1 + j - r];
                var denominator = rightKnot - leftKnot;
                var alpha = double.Abs(denominator) <= 1e-15d ? 0d : (u - leftKnot) / denominator;
                d[j] = Lerp(d[j - 1], d[j], alpha);
            }
        }

        return d[p];
    }

    /// <summary>Evaluates the exact polynomial derivative of this non-rational B-spline.</summary>
    public Vector3D EvaluateTangent(double parameter)
    {
        if (!double.IsFinite(parameter)) throw new ArgumentOutOfRangeException(nameof(parameter), "Parameter must be finite.");
        var derivativeDegree = Degree - 1;
        var derivativePoints = new Vector3D[ControlPoints.Count - 1];
        for (var index = 0; index < derivativePoints.Length; index++)
        {
            var denominator = FullKnots[index + Degree + 1] - FullKnots[index + 1];
            derivativePoints[index] = double.Abs(denominator) <= 1e-15d
                ? new Vector3D(0d, 0d, 0d)
                : (ControlPoints[index + 1] - ControlPoints[index]) * (Degree / denominator);
        }

        var knots = FullKnots.Skip(1).SkipLast(1).ToArray();
        var u = ClampToDomain(parameter);
        if (derivativeDegree == 0)
        {
            var span = FindSpan(derivativePoints.Length, derivativeDegree, knots, u);
            return derivativePoints[span];
        }
        if (double.Abs(u - DomainEnd) <= 1e-12d) return derivativePoints[^1];
        var derivativeSpan = FindSpan(derivativePoints.Length, derivativeDegree, knots, u);
        var d = new Vector3D[derivativeDegree + 1];
        for (var j = 0; j <= derivativeDegree; j++) d[j] = derivativePoints[derivativeSpan - derivativeDegree + j];
        for (var r = 1; r <= derivativeDegree; r++)
        {
            for (var j = derivativeDegree; j >= r; j--)
            {
                var leftKnot = knots[derivativeSpan - derivativeDegree + j];
                var rightKnot = knots[derivativeSpan + 1 + j - r];
                var denominator = rightKnot - leftKnot;
                var alpha = double.Abs(denominator) <= 1e-15d ? 0d : (u - leftKnot) / denominator;
                d[j] = d[j - 1] + ((d[j] - d[j - 1]) * alpha);
            }
        }
        return d[derivativeDegree];
    }

    public IReadOnlyList<ParameterInterval> GetNonZeroKnotSpans(ParameterInterval interval)
    {
        var start = double.Max(DomainStart, double.Min(interval.Start, interval.End));
        var end = double.Min(DomainEnd, double.Max(interval.Start, interval.End));
        if (end <= start) return [];
        var spans = new List<ParameterInterval>();
        for (var index = Degree; index < FullKnots.Count - Degree - 1; index++)
        {
            var spanStart = double.Max(start, FullKnots[index]);
            var spanEnd = double.Min(end, FullKnots[index + 1]);
            if (spanEnd > spanStart) spans.Add(new ParameterInterval(spanStart, spanEnd));
        }
        if (interval.End < interval.Start) spans.Reverse();
        return interval.End < interval.Start
            ? spans.Select(span => new ParameterInterval(span.End, span.Start)).ToArray()
            : spans;
    }

    private double ClampToDomain(double parameter)
    {
        if (parameter <= DomainStart)
        {
            return DomainStart;
        }

        if (parameter >= DomainEnd)
        {
            return DomainEnd;
        }

        return parameter;
    }

    private int FindSpan(double u)
    {
        var n = ControlPoints.Count - 1;
        if (u >= FullKnots[n + 1])
        {
            return n;
        }

        var low = Degree;
        var high = n + 1;
        var mid = (low + high) / 2;
        while (u < FullKnots[mid] || u >= FullKnots[mid + 1])
        {
            if (u < FullKnots[mid])
            {
                high = mid;
            }
            else
            {
                low = mid;
            }

            mid = (low + high) / 2;
        }

        return mid;
    }

    private static int FindSpan(int controlPointCount, int degree, IReadOnlyList<double> knots, double u)
    {
        var n = controlPointCount - 1;
        if (u >= knots[n + 1]) return n;
        var low = degree;
        var high = n + 1;
        var mid = (low + high) / 2;
        while (u < knots[mid] || u >= knots[mid + 1])
        {
            if (u < knots[mid]) high = mid; else low = mid;
            mid = (low + high) / 2;
        }
        return mid;
    }

    private static IReadOnlyList<double> ExpandKnots(IReadOnlyList<int> multiplicities, IReadOnlyList<double> values)
    {
        var knots = new List<double>();
        for (var i = 0; i < multiplicities.Count; i++)
        {
            var multiplicity = multiplicities[i];
            var value = values[i];
            if (multiplicity <= 0 || !double.IsFinite(value))
            {
                throw new ArgumentException("Knot multiplicities must be positive and knot values must be finite.");
            }

            for (var k = 0; k < multiplicity; k++)
            {
                knots.Add(value);
            }
        }

        return knots;
    }

    private static Point3D Lerp(Point3D left, Point3D right, double alpha)
    {
        var clamped = System.Math.Clamp(alpha, 0d, 1d);
        var delta = right - left;
        return left + (delta * clamped);
    }
}
