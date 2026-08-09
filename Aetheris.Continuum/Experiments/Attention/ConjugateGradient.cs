using System.Diagnostics;

namespace Aetheris.Continuum.Experiments.Attention;

public sealed record ResidualSample(int Iteration, double AbsoluteResidual, double RelativeResidual);

public sealed record LinearSolveResult(
    double[] Solution,
    int Iterations,
    int SparseMatvecs,
    double FinalResidual,
    double RelativeResidual,
    double TotalMilliseconds,
    double SparseMatvecMilliseconds,
    double PreconditionerMilliseconds,
    IReadOnlyList<ResidualSample> ResidualHistory);

public static class ConjugateGradient
{
    public static LinearSolveResult Solve(
        PoissonSystem system,
        ReadOnlySpan<double> rightHandSide,
        IAttentionPreconditioner preconditioner,
        double relativeTolerance = 1e-8,
        int? maximumIterations = null)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(preconditioner);
        if (rightHandSide.Length != system.UnknownCount) throw new ArgumentException("Right-hand side length mismatch.");
        if (preconditioner.Size != system.UnknownCount) throw new ArgumentException("Preconditioner size mismatch.");

        var count = system.UnknownCount;
        var x = new double[count];
        var r = rightHandSide.ToArray();
        var z = new double[count];
        var p = new double[count];
        var ap = new double[count];
        var initial = Norm(r);
        var history = new List<ResidualSample> { new(0, initial, initial == 0d ? 0d : 1d) };
        if (initial == 0d)
        {
            return new LinearSolveResult(x, 0, 0, 0d, 0d, 0d, 0d, 0d, history);
        }

        preconditioner.ResetMetrics();
        long matrixTicks = 0;
        long preconditionerTicks = 0;
        var total = Stopwatch.StartNew();
        var stamp = Stopwatch.GetTimestamp();
        preconditioner.Apply(r, z);
        preconditionerTicks += Stopwatch.GetTimestamp() - stamp;
        z.CopyTo(p, 0);
        var rz = Dot(r, z);
        if (!(rz > 0d) || !double.IsFinite(rz)) throw new InvalidOperationException("Preconditioner violated the positive-definite CG contract.");

        var limit = maximumIterations ?? Math.Max(100, 4 * system.PointsPerAxis);
        var matvecs = 0;
        var iterations = 0;
        for (var iteration = 1; iteration <= limit; iteration++)
        {
            stamp = Stopwatch.GetTimestamp();
            system.Apply(p, ap);
            matrixTicks += Stopwatch.GetTimestamp() - stamp;
            matvecs++;
            var denominator = Dot(p, ap);
            if (!(denominator > 0d) || !double.IsFinite(denominator)) throw new InvalidOperationException("Poisson operator violated the SPD CG contract.");
            var alpha = rz / denominator;
            Axpy(alpha, p, x);
            Axpy(-alpha, ap, r);
            var residual = Norm(r);
            var relative = residual / initial;
            history.Add(new ResidualSample(iteration, residual, relative));
            iterations = iteration;
            if (relative <= relativeTolerance) break;

            stamp = Stopwatch.GetTimestamp();
            preconditioner.Apply(r, z);
            preconditionerTicks += Stopwatch.GetTimestamp() - stamp;
            var nextRz = Dot(r, z);
            if (!(nextRz > 0d) || !double.IsFinite(nextRz)) throw new InvalidOperationException("Preconditioner violated the positive-definite CG contract.");
            var beta = nextRz / rz;
            for (var i = 0; i < count; i++) p[i] = z[i] + (beta * p[i]);
            rz = nextRz;
        }

        total.Stop();
        var final = history[^1];
        return new LinearSolveResult(
            x, iterations, matvecs, final.AbsoluteResidual, final.RelativeResidual,
            total.Elapsed.TotalMilliseconds,
            TicksToMilliseconds(matrixTicks), TicksToMilliseconds(preconditionerTicks), history);
    }

    internal static double Dot(ReadOnlySpan<double> left, ReadOnlySpan<double> right)
    {
        var sum = 0d;
        for (var i = 0; i < left.Length; i++) sum += left[i] * right[i];
        return sum;
    }

    internal static double Norm(ReadOnlySpan<double> value) => Math.Sqrt(Dot(value, value));

    private static void Axpy(double alpha, ReadOnlySpan<double> source, Span<double> destination)
    {
        for (var i = 0; i < source.Length; i++) destination[i] += alpha * source[i];
    }

    private static double TicksToMilliseconds(long ticks) => 1000d * ticks / Stopwatch.Frequency;
}
