using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Geometry.Surfaces;

/// <summary>
/// Replaces a rational (NURBS) surface with a non-rational B-spline that follows it within a stated tolerance.
/// <para>
/// Aetheris carries analytic geometry, and a spline face that is really a primitive is recovered as one. What is left
/// over are genuine free-form blends: variable-radius fillets and vertex blends that no primitive describes. Those
/// still must not enter the kernel as NURBS, because a second surface representation costs every later stage a second
/// code path. Since a circular section is not polynomial, the reduction is an approximation by construction, and the
/// only honest way to run it is against a budget the source file itself sets.
/// </para>
/// <para>
/// The reduction refines the knot vectors inside the existing domain and interpolates the rational surface at the
/// refined basis's Greville abscissae. Refinement leaves the domain and the parameter map alone, and interpolating at
/// matched parameters keeps the parameter map to within the same budget as the geometry, so trim intervals and
/// pcurves stay meaningful rather than merely landing somewhere on the new surface.
/// </para>
/// </summary>
internal static class BSplineSurfaceRationalReduction
{
    /// <summary>Fraction of the source file's declared accuracy the reduction is required to stay inside.</summary>
    private const double DeclaredAccuracyFraction = 0.1d;

    /// <summary>Budget relative to the control net when a file declares no accuracy of its own.</summary>
    private const double UndeclaredAccuracyRelativeTolerance = 1e-7d;

    private const double MinimumTolerance = 1e-12d;

    /// <summary>
    /// Knot refinement converges as the span size to the degree plus one, so a handful of doublings is decisive; a
    /// cap keeps a pathological surface from growing an enormous control net instead of failing honestly.
    /// </summary>
    private const int MaximumSpanSubdivision = 32;

    private const int DeviationSamplesPerSpan = 3;
    private const int MinimumDeviationSamples = 24;
    private const int MaximumDeviationSamples = 160;

    /// <summary>The budget this reduction must meet, from the accuracy the source file declares for its geometry.</summary>
    public static double ResolveTolerance(BSplineSurfaceWithKnots surface, double? declaredAccuracyMillimetres)
    {
        if (declaredAccuracyMillimetres is { } declared && double.IsFinite(declared) && declared > 0d)
        {
            return double.Max(declared * DeclaredAccuracyFraction, MinimumTolerance);
        }

        return double.Max(ControlNetDiagonal(surface) * UndeclaredAccuracyRelativeTolerance, MinimumTolerance);
    }

    public static bool TryReduce(
        BSplineSurfaceWithKnots surface,
        double tolerance,
        out BSplineSurfaceWithKnots? reduced,
        out double deviation,
        out string reason)
    {
        reduced = null;
        deviation = double.PositiveInfinity;

        if (!surface.IsRational)
        {
            reason = "surface is already non-rational.";
            return false;
        }

        // Refinement subdivides the spans between declared knots, which only describes the domain when the vector is
        // clamped to it. An unclamped vector is left alone rather than reinterpreted.
        if (!IsClamped(surface.KnotMultiplicitiesU, surface.DegreeU) || !IsClamped(surface.KnotMultiplicitiesV, surface.DegreeV))
        {
            reason = "knot vectors are not clamped to the surface domain, so the spans cannot be refined safely.";
            return false;
        }

        var best = double.PositiveInfinity;

        BSplineSurfaceWithKnots? Attempt(int subdivisionU, int subdivisionV)
        {
            var refinedU = Subdivide(surface.KnotValuesU, surface.KnotMultiplicitiesU, subdivisionU);
            var refinedV = Subdivide(surface.KnotValuesV, surface.KnotMultiplicitiesV, subdivisionV);
            if (!TryInterpolate(surface, refinedU, refinedV, out var attempt) || attempt is null)
            {
                return null;
            }

            var measured = MaximumDeviation(surface, attempt);
            best = double.Min(best, measured);
            return measured <= tolerance ? attempt : null;
        }

        // Refine both directions together until the budget is met, then relax each one on its own. Only the
        // directions that actually carry the rational sections need the extra spans, and a control net refined
        // where it did not need to be costs every later evaluation and every exported point for nothing.
        var uniform = 0;
        for (var subdivision = 1; subdivision <= MaximumSpanSubdivision; subdivision *= 2)
        {
            if (Attempt(subdivision, subdivision) is not null)
            {
                uniform = subdivision;
                break;
            }
        }

        if (uniform == 0)
        {
            deviation = best;
            reason = $"no refinement up to {MaximumSpanSubdivision} spans per knot interval followed the rational surface to {tolerance:G4} mm (closest {best:G4} mm)";
            return false;
        }

        var subdivisionU = uniform;
        var subdivisionV = uniform;
        for (var trial = uniform / 2; trial >= 1; trial /= 2)
        {
            if (Attempt(trial, subdivisionV) is null)
            {
                break;
            }

            subdivisionU = trial;
        }

        for (var trial = uniform / 2; trial >= 1; trial /= 2)
        {
            if (Attempt(subdivisionU, trial) is null)
            {
                break;
            }

            subdivisionV = trial;
        }

        var final = Attempt(subdivisionU, subdivisionV);
        if (final is null)
        {
            deviation = best;
            reason = $"refinement was not reproducible for the relaxed span counts {subdivisionU}x{subdivisionV}";
            return false;
        }

        reduced = final;
        deviation = MaximumDeviation(surface, final);
        reason = $"follows the rational surface to {deviation:G4} mm at matched parameters, within the {tolerance:G4} mm budget, using a {final.ControlPoints.Count}x{final.ControlPoints[0].Count} control net";
        return true;
    }

    private static bool IsClamped(IReadOnlyList<int> multiplicities, int degree)
        => multiplicities.Count >= 2 && multiplicities[0] == degree + 1 && multiplicities[^1] == degree + 1;

    /// <summary>Splits every interval between declared knots into <paramref name="subdivision"/> equal parts.</summary>
    private static (double[] Values, int[] Multiplicities) Subdivide(IReadOnlyList<double> values, IReadOnlyList<int> multiplicities, int subdivision)
    {
        var refinedValues = new List<double>(values.Count * subdivision) { values[0] };
        var refinedMultiplicities = new List<int>(values.Count * subdivision) { multiplicities[0] };

        for (var index = 0; index + 1 < values.Count; index++)
        {
            var start = values[index];
            var end = values[index + 1];
            for (var step = 1; step < subdivision; step++)
            {
                refinedValues.Add(start + ((end - start) * step / subdivision));
                refinedMultiplicities.Add(1);
            }

            refinedValues.Add(end);
            refinedMultiplicities.Add(multiplicities[index + 1]);
        }

        return (refinedValues.ToArray(), refinedMultiplicities.ToArray());
    }

    private static bool TryInterpolate(
        BSplineSurfaceWithKnots surface,
        (double[] Values, int[] Multiplicities) refinedU,
        (double[] Values, int[] Multiplicities) refinedV,
        out BSplineSurfaceWithKnots? candidate)
    {
        candidate = null;
        var knotsU = Expand(refinedU.Values, refinedU.Multiplicities);
        var knotsV = Expand(refinedV.Values, refinedV.Multiplicities);
        var countU = knotsU.Length - surface.DegreeU - 1;
        var countV = knotsV.Length - surface.DegreeV - 1;
        if (countU < surface.DegreeU + 1 || countV < surface.DegreeV + 1)
        {
            return false;
        }

        var sitesU = GrevilleAbscissae(knotsU, surface.DegreeU, countU);
        var sitesV = GrevilleAbscissae(knotsV, surface.DegreeV, countV);
        var collocationU = Collocation(knotsU, surface.DegreeU, countU, sitesU);
        var collocationV = Collocation(knotsV, surface.DegreeV, countV, sitesV);

        var permutationU = new int[countU];
        var permutationV = new int[countV];
        if (!TryFactor(collocationU, permutationU) || !TryFactor(collocationV, permutationV))
        {
            return false;
        }

        // Interpolate along u for every v site, then along v: the tensor-product system separates.
        var intermediate = new Vector3D[countV][];
        var column = new Vector3D[countU];
        for (var siteV = 0; siteV < countV; siteV++)
        {
            for (var siteU = 0; siteU < countU; siteU++)
            {
                column[siteU] = surface.Evaluate(sitesU[siteU], sitesV[siteV]) - Point3D.Origin;
            }

            intermediate[siteV] = Solve(collocationU, permutationU, column);
        }

        var net = new IReadOnlyList<Point3D>[countU];
        var row = new Vector3D[countV];
        for (var indexU = 0; indexU < countU; indexU++)
        {
            for (var siteV = 0; siteV < countV; siteV++)
            {
                row[siteV] = intermediate[siteV][indexU];
            }

            net[indexU] = Solve(collocationV, permutationV, row).Select(vector => Point3D.Origin + vector).ToArray();
        }

        try
        {
            candidate = new BSplineSurfaceWithKnots(
                surface.DegreeU,
                surface.DegreeV,
                net,
                surface.SurfaceForm,
                surface.UClosed,
                surface.VClosed,
                surface.SelfIntersect,
                refinedU.Multiplicities,
                refinedV.Multiplicities,
                refinedU.Values,
                refinedV.Values,
                "UNSPECIFIED");
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>Largest gap between the two surfaces at the same parameters, which also bounds the parameter drift.</summary>
    private static double MaximumDeviation(BSplineSurfaceWithKnots surface, BSplineSurfaceWithKnots candidate)
    {
        var samplesU = SampleCount(candidate.KnotValuesU.Count);
        var samplesV = SampleCount(candidate.KnotValuesV.Count);
        var worst = 0d;
        for (var indexU = 0; indexU < samplesU; indexU++)
        {
            var u = Lerp(surface.DomainStartU, surface.DomainEndU, indexU / (double)(samplesU - 1));
            for (var indexV = 0; indexV < samplesV; indexV++)
            {
                var v = Lerp(surface.DomainStartV, surface.DomainEndV, indexV / (double)(samplesV - 1));
                var gap = (surface.Evaluate(u, v) - candidate.Evaluate(u, v)).Length;
                if (!double.IsFinite(gap))
                {
                    return double.PositiveInfinity;
                }

                worst = double.Max(worst, gap);
            }
        }

        return worst;
    }

    private static int SampleCount(int distinctKnotCount)
        => System.Math.Clamp((distinctKnotCount - 1) * DeviationSamplesPerSpan, MinimumDeviationSamples, MaximumDeviationSamples);

    private static double[] Expand(IReadOnlyList<double> values, IReadOnlyList<int> multiplicities)
    {
        var knots = new List<double>();
        for (var index = 0; index < values.Count; index++)
        {
            for (var repeat = 0; repeat < multiplicities[index]; repeat++)
            {
                knots.Add(values[index]);
            }
        }

        return knots.ToArray();
    }

    /// <summary>
    /// Greville abscissae are the natural interpolation sites for a B-spline basis: one per control point, each
    /// inside its own function's support, which is what makes the collocation matrix invertible.
    /// </summary>
    private static double[] GrevilleAbscissae(IReadOnlyList<double> knots, int degree, int count)
    {
        var sites = new double[count];
        for (var index = 0; index < count; index++)
        {
            var sum = 0d;
            for (var offset = 1; offset <= degree; offset++)
            {
                sum += knots[index + offset];
            }

            sites[index] = sum / degree;
        }

        return sites;
    }

    private static double[,] Collocation(IReadOnlyList<double> knots, int degree, int count, IReadOnlyList<double> sites)
    {
        var matrix = new double[count, count];
        for (var siteIndex = 0; siteIndex < count; siteIndex++)
        {
            var span = FindSpan(count, degree, knots, sites[siteIndex]);
            var values = BasisValues(span, sites[siteIndex], degree, knots);
            for (var offset = 0; offset <= degree; offset++)
            {
                matrix[siteIndex, span - degree + offset] = values[offset];
            }
        }

        return matrix;
    }

    private static int FindSpan(int count, int degree, IReadOnlyList<double> knots, double parameter)
    {
        var last = count - 1;
        if (parameter >= knots[last + 1])
        {
            return last;
        }

        if (parameter <= knots[degree])
        {
            return degree;
        }

        var low = degree;
        var high = last + 1;
        var mid = (low + high) / 2;
        while (parameter < knots[mid] || parameter >= knots[mid + 1])
        {
            if (parameter < knots[mid])
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

    private static double[] BasisValues(int span, double parameter, int degree, IReadOnlyList<double> knots)
    {
        var values = new double[degree + 1];
        var left = new double[degree + 1];
        var right = new double[degree + 1];
        values[0] = 1d;
        for (var level = 1; level <= degree; level++)
        {
            left[level] = parameter - knots[span + 1 - level];
            right[level] = knots[span + level] - parameter;
            var saved = 0d;
            for (var index = 0; index < level; index++)
            {
                var denominator = right[index + 1] + left[level - index];
                var scaled = denominator == 0d ? 0d : values[index] / denominator;
                values[index] = saved + (right[index + 1] * scaled);
                saved = left[level - index] * scaled;
            }

            values[level] = saved;
        }

        return values;
    }

    private static bool TryFactor(double[,] matrix, int[] permutation)
    {
        var size = permutation.Length;
        for (var index = 0; index < size; index++)
        {
            permutation[index] = index;
        }

        for (var column = 0; column < size; column++)
        {
            var pivotRow = column;
            var best = double.Abs(matrix[column, column]);
            for (var row = column + 1; row < size; row++)
            {
                var magnitude = double.Abs(matrix[row, column]);
                if (magnitude > best)
                {
                    best = magnitude;
                    pivotRow = row;
                }
            }

            if (!(best > 0d))
            {
                return false;
            }

            if (pivotRow != column)
            {
                for (var index = 0; index < size; index++)
                {
                    (matrix[column, index], matrix[pivotRow, index]) = (matrix[pivotRow, index], matrix[column, index]);
                }

                (permutation[column], permutation[pivotRow]) = (permutation[pivotRow], permutation[column]);
            }

            for (var row = column + 1; row < size; row++)
            {
                var factor = matrix[row, column] / matrix[column, column];
                matrix[row, column] = factor;
                for (var index = column + 1; index < size; index++)
                {
                    matrix[row, index] -= factor * matrix[column, index];
                }
            }
        }

        return true;
    }

    private static Vector3D[] Solve(double[,] factored, int[] permutation, IReadOnlyList<Vector3D> rightHandSide)
    {
        var size = permutation.Length;
        var forward = new Vector3D[size];
        for (var row = 0; row < size; row++)
        {
            var sum = rightHandSide[permutation[row]];
            for (var column = 0; column < row; column++)
            {
                sum -= factored[row, column] * forward[column];
            }

            forward[row] = sum;
        }

        var solution = new Vector3D[size];
        for (var row = size - 1; row >= 0; row--)
        {
            var sum = forward[row];
            for (var column = row + 1; column < size; column++)
            {
                sum -= factored[row, column] * solution[column];
            }

            solution[row] = sum * (1d / factored[row, row]);
        }

        return solution;
    }

    private static double ControlNetDiagonal(BSplineSurfaceWithKnots surface)
    {
        var points = surface.ControlPoints.SelectMany(row => row).ToArray();
        if (points.Length == 0)
        {
            return 0d;
        }

        return new Vector3D(
            points.Max(point => point.X) - points.Min(point => point.X),
            points.Max(point => point.Y) - points.Min(point => point.Y),
            points.Max(point => point.Z) - points.Min(point => point.Z)).Length;
    }

    private static double Lerp(double start, double end, double fraction) => start + ((end - start) * fraction);
}
