namespace Aetheris.FEA.Mechanics;

/// <summary>Minimal deterministic compressed-row sparse matrix for conventional PCG.</summary>
public sealed class SparseSymmetricMatrix
{
    private readonly SortedDictionary<int, double>[] rows;

    public SparseSymmetricMatrix(int size)
    {
        if (size < 1) throw new ArgumentOutOfRangeException(nameof(size));
        Size = size;
        rows = Enumerable.Range(0, size).Select(_ => new SortedDictionary<int, double>()).ToArray();
    }

    public int Size { get; }
    public int Nonzeros => rows.Sum(row => row.Count);
    public IReadOnlyList<SortedDictionary<int, double>> Rows => rows;
    public double this[int row, int column] => rows[row].TryGetValue(column, out var value) ? value : 0;

    public void Add(int row, int column, double value)
    {
        if (!double.IsFinite(value)) throw new ArithmeticException("Sparse assembly received a non-finite value.");
        if (double.Abs(value) < 1e-30) return;
        rows[row][column] = rows[row].TryGetValue(column, out var current) ? current + value : value;
    }

    public double[] Multiply(IReadOnlyList<double> vector)
    {
        var result = new double[Size];
        for (var row = 0; row < Size; row++)
            foreach (var item in rows[row]) result[row] += item.Value * vector[item.Key];
        return result;
    }

    public double MaximumAsymmetry()
    {
        var maximum = 0d;
        for (var row = 0; row < Size; row++)
            foreach (var item in rows[row]) maximum = double.Max(maximum, double.Abs(item.Value - this[item.Key, row]));
        return maximum;
    }

    public bool IsFinite() => rows.All(row => row.Values.All(double.IsFinite));

    public SparseSymmetricMatrix Copy()
    {
        var copy = new SparseSymmetricMatrix(Size);
        for (var row = 0; row < Size; row++)
            foreach (var item in rows[row]) copy.rows[row][item.Key] = item.Value;
        return copy;
    }

    public void ApplyDirichlet(IReadOnlyDictionary<int, double> prescribed, double[] load)
    {
        foreach (var pair in prescribed.OrderBy(item => item.Key))
        {
            var dof = pair.Key;
            var value = pair.Value;
            for (var row = 0; row < Size; row++)
            {
                if (row == dof) continue;
                if (rows[row].Remove(dof, out var coefficient)) load[row] -= coefficient * value;
            }
            rows[dof].Clear();
            rows[dof][dof] = 1;
            load[dof] = value;
        }
    }
}

public static class PreconditionedConjugateGradient
{
    public static (double[] Solution, SolverConvergence Convergence) Solve(SparseSymmetricMatrix matrix, double[] rhs, double relativeTolerance = 1e-9, int? maximumIterations = null)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var n = matrix.Size;
        var max = maximumIterations ?? double.Max(100, 4 * n);
        var x = new double[n];
        var r = (double[])rhs.Clone();
        var initial = Norm(r);
        var history = new List<double> { initial };
        if (initial == 0) return (x, new(true, 0, 0, 0, history, System.Diagnostics.Stopwatch.GetElapsedTime(started)));
        var z = new double[n];
        for (var i = 0; i < n; i++) z[i] = r[i] / SafeDiagonal(matrix[i, i]);
        var p = (double[])z.Clone();
        var rz = Dot(r, z);
        var converged = false;
        var iteration = 0;
        for (; iteration < max; iteration++)
        {
            var ap = matrix.Multiply(p);
            var denominator = Dot(p, ap);
            if (!double.IsFinite(denominator) || denominator <= 0) break;
            var alpha = rz / denominator;
            for (var i = 0; i < n; i++) { x[i] += alpha * p[i]; r[i] -= alpha * ap[i]; }
            var residual = Norm(r);
            history.Add(residual);
            if (residual <= relativeTolerance * initial) { converged = true; iteration++; break; }
            for (var i = 0; i < n; i++) z[i] = r[i] / SafeDiagonal(matrix[i, i]);
            var nextRz = Dot(r, z);
            var beta = nextRz / rz;
            for (var i = 0; i < n; i++) p[i] = z[i] + beta * p[i];
            rz = nextRz;
        }
        return (x, new(converged, iteration, initial, history[^1], history, System.Diagnostics.Stopwatch.GetElapsedTime(started)));
    }

    private static double SafeDiagonal(double value) => double.Abs(value) < 1e-30 ? 1 : value;
    private static double Dot(IReadOnlyList<double> a, IReadOnlyList<double> b) { var sum = 0d; for (var i = 0; i < a.Count; i++) sum += a[i] * b[i]; return sum; }
    private static double Norm(IReadOnlyList<double> a) => double.Sqrt(Dot(a, a));
}
