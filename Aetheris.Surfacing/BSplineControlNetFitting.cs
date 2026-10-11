using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

/// <summary>Shared IDs are exact positional constraints between polynomial patch control nets.</summary>
public sealed record SplineFitPatch(string Id, BSplineSurfaceWithKnots Surface, IReadOnlyList<int> ControlIds);
public sealed record SplineFitSample(int Patch, double U, double V, Point3D Target, double Weight = 1);
public sealed record SplineFitOptions(double SeedWeight = .000001, double FairnessWeight = .01);
public sealed record SplineFitResult(IReadOnlyList<SplineFitPatch> Patches, double RmsError, double MaximumError,
    int SharedControlCount, int SampleCount);

/// <summary>Bounded linear least squares in control positions, using the kernel evaluator for
/// basis weights. Fairness is a parameter-space authoring regularizer, not fabric mechanics.</summary>
public static class BSplineControlNetFitting
{
    public static SplineFitResult Fit(IReadOnlyList<SplineFitPatch> patches,
        IReadOnlyList<SplineFitSample> samples, SplineFitOptions? options = null)
    {
        options ??= new();
        if (patches.Count is < 1 or > 8 || samples.Count is < 1 or > 8192
            || !double.IsFinite(options.SeedWeight) || options.SeedWeight <= 0
            || !double.IsFinite(options.FairnessWeight) || options.FairnessWeight < 0)
        {
            throw new ArgumentException("spline-fit-budget: Require bounded patches/samples and finite positive seed regularization.");
        }
        var initial = new SortedDictionary<int, Point3D>();
        foreach (var patch in patches)
        {
            var surface = patch.Surface;
            int count = surface.ControlPoints.Count * surface.ControlPoints[0].Count;
            if (surface.IsRational || count > 128 || patch.ControlIds.Count != count || string.IsNullOrWhiteSpace(patch.Id))
            {
                throw new ArgumentException("spline-fit-patch: Require polynomial patches with one shared ID per control.");
            }
            int local = 0;
            foreach (var row in surface.ControlPoints)
            {
                foreach (var point in row)
                {
                    int id = patch.ControlIds[local++];
                    if (initial.TryGetValue(id, out var existing) && (existing - point).Length > 1e-8)
                    {
                        throw new ArgumentException("spline-fit-shared-control: Shared controls need identical seed positions.");
                    }
                    initial[id] = point;
                }
            }
        }
        if (initial.Count > 128)
        {
            throw new ArgumentException("spline-fit-controls: Maximum 128 independent controls.");
        }
        var ids = initial.Keys.ToArray();
        var indices = ids.Select((id, index) => (id, index)).ToDictionary(pair => pair.id, pair => pair.index);
        var matrix = new double[ids.Length, ids.Length];
        var rhs = new double[ids.Length, 3];
        var basis = patches.Select(patch => BasisSurfaces(patch.Surface)).ToArray();
        foreach (var sample in samples)
        {
            if (sample.Patch < 0 || sample.Patch >= patches.Count || !double.IsFinite(sample.Weight) || sample.Weight <= 0
                || !Finite(sample.Target))
            {
                throw new ArgumentException("spline-fit-sample: Invalid patch, weight or target.");
            }
            var patch = patches[sample.Patch];
            if (!double.IsFinite(sample.U) || !double.IsFinite(sample.V)
                || sample.U < patch.Surface.DomainStartU || sample.U > patch.Surface.DomainEndU
                || sample.V < patch.Surface.DomainStartV || sample.V > patch.Surface.DomainEndV)
            {
                throw new ArgumentException("spline-fit-domain: Sample lies outside the source domain.");
            }
            var weights = new double[ids.Length];
            for (int control = 0; control < patch.ControlIds.Count; control++)
            {
                weights[indices[patch.ControlIds[control]]] += basis[sample.Patch][control].Evaluate(sample.U, sample.V).X;
            }
            AddRow(weights, sample.Target, sample.Weight);
        }
        for (int index = 0; index < ids.Length; index++)
        {
            var weights = new double[ids.Length];
            weights[index] = 1;
            AddRow(weights, initial[ids[index]], options.SeedWeight);
        }
        foreach (var patch in patches)
        {
            int rows = patch.Surface.ControlPoints.Count;
            int columns = patch.Surface.ControlPoints[0].Count;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (row > 0 && row < rows - 1)
                    {
                        Fair((row - 1) * columns + column, row * columns + column, (row + 1) * columns + column);
                    }
                    if (column > 0 && column < columns - 1)
                    {
                        Fair(row * columns + column - 1, row * columns + column, row * columns + column + 1);
                    }
                }
            }
            void Fair(int a, int b, int c)
            {
                var weights = new double[ids.Length];
                weights[indices[patch.ControlIds[a]]] += 1;
                weights[indices[patch.ControlIds[b]]] -= 2;
                weights[indices[patch.ControlIds[c]]] += 1;
                AddRow(weights, new(0, 0, 0), options.FairnessWeight);
            }
        }
        var solution = Solve(matrix, rhs);
        var fitted = patches.Select(patch => patch with
        {
            Surface = Copy(patch.Surface, Enumerable.Range(0, patch.Surface.ControlPoints.Count)
                .Select(row => (IReadOnlyList<Point3D>)Enumerable.Range(0, patch.Surface.ControlPoints[0].Count)
                    .Select(column =>
                    {
                        int index = indices[patch.ControlIds[row * patch.Surface.ControlPoints[0].Count + column]];
                        return new Point3D(solution[index, 0], solution[index, 1], solution[index, 2]);
                    }).ToArray()).ToArray()),
        }).ToArray();
        double squared = 0;
        double maximum = 0;
        foreach (var sample in samples)
        {
            double error = (fitted[sample.Patch].Surface.Evaluate(sample.U, sample.V) - sample.Target).Length;
            squared += error * error;
            maximum = Math.Max(maximum, error);
        }
        return new(fitted, Math.Sqrt(squared / samples.Count), maximum, ids.Length, samples.Count);

        void AddRow(double[] weights, Point3D target, double weight)
        {
            for (int row = 0; row < ids.Length; row++)
            {
                if (weights[row] == 0)
                {
                    continue;
                }
                double scaled = weights[row] * weight;
                rhs[row, 0] += scaled * target.X;
                rhs[row, 1] += scaled * target.Y;
                rhs[row, 2] += scaled * target.Z;
                for (int column = 0; column < ids.Length; column++)
                {
                    matrix[row, column] += scaled * weights[column];
                }
            }
        }
    }

    public static BSplineSurfaceWithKnots Copy(BSplineSurfaceWithKnots source, IReadOnlyList<IReadOnlyList<Point3D>> controls)
    {
        return new(source.DegreeU, source.DegreeV, controls, source.SurfaceForm, source.UClosed, source.VClosed,
            source.SelfIntersect, source.KnotMultiplicitiesU, source.KnotMultiplicitiesV,
            source.KnotValuesU, source.KnotValuesV, source.KnotSpec, source.Weights);
    }

    private static BSplineSurfaceWithKnots[] BasisSurfaces(BSplineSurfaceWithKnots source)
    {
        int columns = source.ControlPoints[0].Count;
        return Enumerable.Range(0, source.ControlPoints.Count * columns).Select(active => Copy(source,
            Enumerable.Range(0, source.ControlPoints.Count).Select(row => (IReadOnlyList<Point3D>)
                Enumerable.Range(0, columns).Select(column => new Point3D(row * columns + column == active ? 1 : 0, 0, 0))
                    .ToArray()).ToArray())).ToArray();
    }

    private static bool Finite(Point3D point) => double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

    private static double[,] Solve(double[,] matrix, double[,] rhs)
    {
        int count = matrix.GetLength(0);
        var factor = new double[count, count];
        for (int row = 0; row < count; row++)
        {
            for (int column = 0; column <= row; column++)
            {
                double value = matrix[row, column];
                for (int inner = 0; inner < column; inner++)
                {
                    value -= factor[row, inner] * factor[column, inner];
                }
                if (row == column)
                {
                    if (!double.IsFinite(value) || value <= 0)
                    {
                        throw new InvalidOperationException("spline-fit-conditioning: Normal system is not positive definite.");
                    }
                    factor[row, column] = Math.Sqrt(value);
                }
                else
                {
                    factor[row, column] = value / factor[column, column];
                }
            }
        }
        var result = new double[count, 3];
        for (int axis = 0; axis < 3; axis++)
        {
            for (int row = 0; row < count; row++)
            {
                double value = rhs[row, axis];
                for (int inner = 0; inner < row; inner++)
                {
                    value -= factor[row, inner] * result[inner, axis];
                }
                result[row, axis] = value / factor[row, row];
            }
            for (int row = count - 1; row >= 0; row--)
            {
                double value = result[row, axis];
                for (int inner = row + 1; inner < count; inner++)
                {
                    value -= factor[inner, row] * result[inner, axis];
                }
                result[row, axis] = value / factor[row, row];
                if (!double.IsFinite(result[row, axis]))
                {
                    throw new InvalidOperationException("spline-fit-conditioning: Nonfinite fitted control.");
                }
            }
        }
        return result;
    }
}
