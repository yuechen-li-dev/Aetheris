using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Surfaces;

public sealed record BSplineSurfaceWithKnots
{
    public BSplineSurfaceWithKnots(
        int degreeU,
        int degreeV,
        IReadOnlyList<IReadOnlyList<Point3D>> controlPoints,
        string surfaceForm,
        bool uClosed,
        bool vClosed,
        bool selfIntersect,
        IReadOnlyList<int> knotMultiplicitiesU,
        IReadOnlyList<int> knotMultiplicitiesV,
        IReadOnlyList<double> knotValuesU,
        IReadOnlyList<double> knotValuesV,
        string knotSpec,
        IReadOnlyList<IReadOnlyList<double>>? weights = null)
    {
        if (degreeU < 1 || degreeV < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(degreeU), "Surface degrees must be greater than or equal to one.");
        }

        if (controlPoints is null || controlPoints.Count < degreeU + 1)
        {
            throw new ArgumentException("Control net U count must be at least degree_u + 1.", nameof(controlPoints));
        }

        var controlCountV = controlPoints[0]?.Count ?? 0;
        if (controlCountV < degreeV + 1)
        {
            throw new ArgumentException("Control net V count must be at least degree_v + 1.", nameof(controlPoints));
        }

        for (var i = 0; i < controlPoints.Count; i++)
        {
            if (controlPoints[i] is null || controlPoints[i].Count != controlCountV)
            {
                throw new ArgumentException("All control net rows must be non-null and have identical V cardinality.", nameof(controlPoints));
            }
            if (controlPoints[i].Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z)))
            {
                throw new ArgumentException("Control net points must have finite coordinates.", nameof(controlPoints));
            }
        }

        var fullKnotsU = ExpandKnots(knotMultiplicitiesU, knotValuesU);
        var fullKnotsV = ExpandKnots(knotMultiplicitiesV, knotValuesV);

        var expectedKnotCountU = controlPoints.Count + degreeU + 1;
        var expectedKnotCountV = controlCountV + degreeV + 1;
        if (fullKnotsU.Count != expectedKnotCountU)
        {
            throw new ArgumentException($"Expanded U knot count must be {expectedKnotCountU} for degree_u {degreeU} and {controlPoints.Count} control rows.");
        }

        if (fullKnotsV.Count != expectedKnotCountV)
        {
            throw new ArgumentException($"Expanded V knot count must be {expectedKnotCountV} for degree_v {degreeV} and {controlCountV} control columns.");
        }

        EnsureNonDecreasing(fullKnotsU, "U");
        EnsureNonDecreasing(fullKnotsV, "V");

        if (weights is not null)
        {
            if (weights.Count != controlPoints.Count)
            {
                throw new ArgumentException("Weight net must have the same U cardinality as the control net.", nameof(weights));
            }

            for (var i = 0; i < weights.Count; i++)
            {
                if (weights[i] is null || weights[i].Count != controlCountV)
                {
                    throw new ArgumentException("Weight net rows must have the same V cardinality as the control net.", nameof(weights));
                }

                if (weights[i].Any(w => !double.IsFinite(w) || w <= 0d))
                {
                    throw new ArgumentException("Weights must be finite and strictly positive.", nameof(weights));
                }
            }

            // All-unit weights are an ordinary polynomial surface; keep that representation canonical.
            if (weights.All(row => row.All(w => double.Abs(w - 1d) <= 1e-12d)))
            {
                weights = null;
            }
        }

        DegreeU = degreeU;
        DegreeV = degreeV;
        if (weights is not null)
        {
            Weights = weights.Select(row => row.ToArray()).ToArray();
            _weightedNet = controlPoints
                .Select((row, i) => (IReadOnlyList<Point3D>)row.Select((p, j) => new Point3D(p.X * weights[i][j], p.Y * weights[i][j], p.Z * weights[i][j])).ToArray())
                .ToArray();
            _weightNet = weights
                .Select(row => (IReadOnlyList<Point3D>)row.Select(w => new Point3D(w, 0d, 0d)).ToArray())
                .ToArray();
        }

        ControlPoints = controlPoints.Select(row => row.ToArray()).ToArray();
        SurfaceForm = surfaceForm;
        UClosed = uClosed;
        VClosed = vClosed;
        SelfIntersect = selfIntersect;
        KnotMultiplicitiesU = knotMultiplicitiesU.ToArray();
        KnotMultiplicitiesV = knotMultiplicitiesV.ToArray();
        KnotValuesU = knotValuesU.ToArray();
        KnotValuesV = knotValuesV.ToArray();
        FullKnotsU = fullKnotsU;
        FullKnotsV = fullKnotsV;
        KnotSpec = knotSpec;
    }

    private readonly IReadOnlyList<IReadOnlyList<Point3D>>? _weightedNet;
    private readonly IReadOnlyList<IReadOnlyList<Point3D>>? _weightNet;

    /// <summary>Rational (NURBS) weights parallel to <see cref="ControlPoints"/>; null for a polynomial surface.</summary>
    public IReadOnlyList<IReadOnlyList<double>>? Weights { get; }

    public bool IsRational => Weights is not null;

    /// <summary>Returns a copy of this surface carrying the given weight net.</summary>
    public BSplineSurfaceWithKnots WithWeights(IReadOnlyList<IReadOnlyList<double>>? weights) => new(
        DegreeU, DegreeV, ControlPoints, SurfaceForm, UClosed, VClosed, SelfIntersect,
        KnotMultiplicitiesU, KnotMultiplicitiesV, KnotValuesU, KnotValuesV, KnotSpec, weights);

    public int DegreeU { get; }
    public int DegreeV { get; }
    public IReadOnlyList<IReadOnlyList<Point3D>> ControlPoints { get; }
    public string SurfaceForm { get; }
    public bool UClosed { get; }
    public bool VClosed { get; }
    public bool SelfIntersect { get; }
    public IReadOnlyList<int> KnotMultiplicitiesU { get; }
    public IReadOnlyList<int> KnotMultiplicitiesV { get; }
    public IReadOnlyList<double> KnotValuesU { get; }
    public IReadOnlyList<double> KnotValuesV { get; }
    public IReadOnlyList<double> FullKnotsU { get; }
    public IReadOnlyList<double> FullKnotsV { get; }
    public string KnotSpec { get; }

    public double DomainStartU => FullKnotsU[DegreeU];

    public double DomainEndU => FullKnotsU[FullKnotsU.Count - DegreeU - 1];

    public double DomainStartV => FullKnotsV[DegreeV];

    public double DomainEndV => FullKnotsV[FullKnotsV.Count - DegreeV - 1];

    public Point3D Evaluate(double u, double v)
    {
        if (!double.IsFinite(u) || !double.IsFinite(v))
        {
            throw new ArgumentOutOfRangeException(nameof(u), "Parameters must be finite.");
        }

        var uClamped = ClampToDomain(u, DomainStartU, DomainEndU);
        var vClamped = ClampToDomain(v, DomainStartV, DomainEndV);

        if (_weightedNet is not null && _weightNet is not null)
        {
            var numerator = EvaluateNet(_weightedNet, uClamped, vClamped);
            var weight = EvaluateNet(_weightNet, uClamped, vClamped).X;
            return new Point3D(numerator.X / weight, numerator.Y / weight, numerator.Z / weight);
        }

        return EvaluateNet(ControlPoints, uClamped, vClamped);
    }

    private Point3D EvaluateNet(IReadOnlyList<IReadOnlyList<Point3D>> net, double uClamped, double vClamped)
    {
        if (double.Abs(uClamped - DomainEndU) <= 1e-12d && double.Abs(vClamped - DomainEndV) <= 1e-12d)
        {
            return net[^1][^1];
        }

        var rowPoints = new Point3D[net.Count];
        for (var i = 0; i < net.Count; i++)
        {
            rowPoints[i] = EvaluateCurve(
                net[i],
                DegreeV,
                FullKnotsV,
                DomainStartV,
                DomainEndV,
                vClamped);
        }

        return EvaluateCurve(
            rowPoints,
            DegreeU,
            FullKnotsU,
            DomainStartU,
            DomainEndU,
            uClamped);
    }

    private static IReadOnlyList<double> ExpandKnots(IReadOnlyList<int> multiplicities, IReadOnlyList<double> values)
    {
        if (multiplicities is null || values is null || multiplicities.Count == 0 || multiplicities.Count != values.Count)
        {
            throw new ArgumentException("Knot multiplicities and values must be non-empty and have matching counts.");
        }

        var knots = new List<double>();
        for (var i = 0; i < multiplicities.Count; i++)
        {
            if (multiplicities[i] <= 0 || !double.IsFinite(values[i]))
            {
                throw new ArgumentException("Knot multiplicities must be positive and knot values must be finite.");
            }

            for (var j = 0; j < multiplicities[i]; j++)
            {
                knots.Add(values[i]);
            }
        }

        return knots;
    }

    private static void EnsureNonDecreasing(IReadOnlyList<double> knots, string dimension)
    {
        for (var i = 1; i < knots.Count; i++)
        {
            if (knots[i] < knots[i - 1])
            {
                throw new ArgumentException($"Expanded {dimension} knot vector must be non-decreasing.");
            }
        }
    }

    private static Point3D EvaluateCurve(
        IReadOnlyList<Point3D> controlPoints,
        int degree,
        IReadOnlyList<double> fullKnots,
        double domainStart,
        double domainEnd,
        double parameter)
    {
        var u = ClampToDomain(parameter, domainStart, domainEnd);
        if (double.Abs(u - domainEnd) <= 1e-12d)
        {
            return controlPoints[^1];
        }

        var span = FindSpan(controlPoints.Count, degree, fullKnots, u);
        var d = new Point3D[degree + 1];
        for (var j = 0; j <= degree; j++)
        {
            d[j] = controlPoints[span - degree + j];
        }

        for (var r = 1; r <= degree; r++)
        {
            for (var j = degree; j >= r; j--)
            {
                var leftKnot = fullKnots[span - degree + j];
                var rightKnot = fullKnots[span + 1 + j - r];
                var denominator = rightKnot - leftKnot;
                var alpha = double.Abs(denominator) <= 1e-15d ? 0d : (u - leftKnot) / denominator;
                d[j] = Lerp(d[j - 1], d[j], alpha);
            }
        }

        return d[degree];
    }

    private static int FindSpan(int controlPointCount, int degree, IReadOnlyList<double> fullKnots, double parameter)
    {
        var n = controlPointCount - 1;
        if (parameter >= fullKnots[n + 1])
        {
            return n;
        }

        var low = degree;
        var high = n + 1;
        var mid = (low + high) / 2;
        while (parameter < fullKnots[mid] || parameter >= fullKnots[mid + 1])
        {
            if (parameter < fullKnots[mid])
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

    private static double ClampToDomain(double parameter, double start, double end)
    {
        if (parameter <= start)
        {
            return start;
        }

        if (parameter >= end)
        {
            return end;
        }

        return parameter;
    }

    private static Point3D Lerp(Point3D left, Point3D right, double alpha)
    {
        var clamped = System.Math.Clamp(alpha, 0d, 1d);
        var delta = right - left;
        return left + (delta * clamped);
    }
}
