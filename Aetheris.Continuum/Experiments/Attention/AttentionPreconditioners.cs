using System.Diagnostics;

namespace Aetheris.Continuum.Experiments.Attention;

public interface IAttentionPreconditioner
{
    string Name { get; }
    int Size { get; }
    int ExperimentalOperatorCalls { get; }
    double InteractionMilliseconds { get; }
    double HierarchyMilliseconds { get; }
    long EstimatedStorageBytes { get; }
    void Apply(ReadOnlySpan<double> residual, Span<double> correction);
    void ResetMetrics();
}

public abstract class AttentionPreconditionerBase(string name, int size) : IAttentionPreconditioner
{
    private long interactionTicks;
    private long hierarchyTicks;

    public string Name { get; } = name;
    public int Size { get; } = size;
    public int ExperimentalOperatorCalls { get; protected set; }
    public double InteractionMilliseconds => 1000d * interactionTicks / Stopwatch.Frequency;
    public double HierarchyMilliseconds => 1000d * hierarchyTicks / Stopwatch.Frequency;
    public abstract long EstimatedStorageBytes { get; }
    public abstract void Apply(ReadOnlySpan<double> residual, Span<double> correction);

    public virtual void ResetMetrics()
    {
        ExperimentalOperatorCalls = 0;
        interactionTicks = 0;
        hierarchyTicks = 0;
    }

    protected void AddInteractionTicks(long ticks) => interactionTicks += ticks;
    protected void AddHierarchyTicks(long ticks) => hierarchyTicks += ticks;
}

public sealed class IdentityPreconditioner(int size) : AttentionPreconditionerBase("CG", size)
{
    public override long EstimatedStorageBytes => 0;
    public override void Apply(ReadOnlySpan<double> residual, Span<double> correction) => residual.CopyTo(correction);
}

public sealed class JacobiPreconditioner : AttentionPreconditionerBase
{
    private readonly double inverseDiagonal;
    public JacobiPreconditioner(PoissonSystem system) : base("Jacobi-CG", system.UnknownCount) => inverseDiagonal = 1d / system.Diagonal;
    public override long EstimatedStorageBytes => sizeof(double);
    public override void Apply(ReadOnlySpan<double> residual, Span<double> correction)
    {
        for (var i = 0; i < residual.Length; i++) correction[i] = inverseDiagonal * residual[i];
    }
}

/// <summary>Compact symmetric interaction control: D^-1(I + beta A_neighbor), beta &lt; 1/6 preserves SPD.</summary>
public sealed class CompactInteractionPreconditioner : AttentionPreconditionerBase
{
    private readonly PoissonSystem system;
    private readonly double beta;
    private readonly double inverseDiagonal;

    public CompactInteractionPreconditioner(PoissonSystem system, double beta = 0.12d)
        : base($"Compact-symmetric(beta={beta:R})", system.UnknownCount)
    {
        if (Math.Abs(beta) >= 1d / 6d) throw new ArgumentOutOfRangeException(nameof(beta), "|beta| must be below 1/6 for the stated SPD bound.");
        this.system = system;
        this.beta = beta;
        inverseDiagonal = 1d / system.Diagonal;
    }

    public override long EstimatedStorageBytes => 0;

    public override void Apply(ReadOnlySpan<double> residual, Span<double> correction)
    {
        ExperimentalOperatorCalls++;
        var start = Stopwatch.GetTimestamp();
        var n = system.PointsPerAxis;
        for (var k = 0; k < n; k++)
        for (var j = 0; j < n; j++)
        for (var i = 0; i < n; i++)
        {
            var index = ((k * n) + j) * n + i;
            var neighbors = 0d;
            if (i > 0) neighbors += residual[index - 1];
            if (i + 1 < n) neighbors += residual[index + 1];
            if (j > 0) neighbors += residual[index - n];
            if (j + 1 < n) neighbors += residual[index + n];
            if (k > 0) neighbors += residual[index - (n * n)];
            if (k + 1 < n) neighbors += residual[index + (n * n)];
            correction[index] = inverseDiagonal * (residual[index] + (beta * neighbors));
        }
        AddInteractionTicks(Stopwatch.GetTimestamp() - start);
    }
}

/// <summary>
/// Symmetric positive separable kernel exp(-ManhattanDistance/length), scaled so its
/// first sine-mode response, when added to Jacobi, matches the exact inverse response.
/// </summary>
public sealed class ScreenedInteractionPreconditioner : AttentionPreconditionerBase
{
    private readonly PoissonSystem system;
    private readonly double rho;
    private readonly double inverseDiagonal;
    private readonly double kernelScale;
    private readonly double[] scratchA;
    private readonly double[] scratchB;

    public ScreenedInteractionPreconditioner(PoissonSystem system, double latticeLength = 2d, string? name = null)
        : base(name ?? $"Screened-symmetric(length={latticeLength:R})", system.UnknownCount)
    {
        if (!(latticeLength > 0d)) throw new ArgumentOutOfRangeException(nameof(latticeLength));
        this.system = system;
        rho = Math.Exp(-1d / latticeLength);
        inverseDiagonal = 1d / system.Diagonal;
        scratchA = new double[Size];
        scratchB = new double[Size];

        var mode = SpectralModes.Create(system.PointsPerAxis, 1, 1, 1);
        ApplyKernel(mode, scratchA, scratchB);
        var response = ConjugateGradient.Dot(mode, scratchB);
        var eigenvalue = SpectralModes.PoissonEigenvalue(system, 1, 1, 1);
        kernelScale = ((1d / eigenvalue) - inverseDiagonal) / response;
        if (!(kernelScale > 0d)) throw new InvalidOperationException("Screened kernel calibration did not preserve SPD.");
    }

    public override long EstimatedStorageBytes => 2L * sizeof(double) * Size;
    public double Rho => rho;
    public double KernelScale => kernelScale;

    public override void Apply(ReadOnlySpan<double> residual, Span<double> correction)
    {
        ExperimentalOperatorCalls++;
        var start = Stopwatch.GetTimestamp();
        ApplyKernel(residual, scratchA, scratchB);
        for (var i = 0; i < Size; i++) correction[i] = (inverseDiagonal * residual[i]) + (kernelScale * scratchB[i]);
        AddInteractionTicks(Stopwatch.GetTimestamp() - start);
    }

    private void ApplyKernel(ReadOnlySpan<double> source, double[] first, double[] destination)
    {
        ApplyAxis(source, first, 0);
        ApplyAxis(first, destination, 1);
        ApplyAxis(destination, first, 2);
        first.CopyTo(destination, 0);
    }

    private void ApplyAxis(ReadOnlySpan<double> source, Span<double> destination, int axis)
    {
        var n = system.PointsPerAxis;
        var line = new double[n];
        var forward = new double[n];
        var backward = new double[n];
        for (var b = 0; b < n; b++)
        for (var a = 0; a < n; a++)
        {
            for (var t = 0; t < n; t++) line[t] = source[Index(axis, t, a, b, n)];
            forward[0] = line[0];
            for (var t = 1; t < n; t++) forward[t] = line[t] + (rho * forward[t - 1]);
            backward[n - 1] = line[n - 1];
            for (var t = n - 2; t >= 0; t--) backward[t] = line[t] + (rho * backward[t + 1]);
            for (var t = 0; t < n; t++) destination[Index(axis, t, a, b, n)] = forward[t] + backward[t] - line[t];
        }
    }

    private static int Index(int axis, int t, int a, int b, int n) => axis switch
    {
        0 => ((b * n) + a) * n + t,
        1 => ((b * n) + t) * n + a,
        _ => ((t * n) + b) * n + a,
    };
}

/// <summary>Low-rank factorization of the Dirichlet Green operator in analytic lattice sine modes.</summary>
public sealed class TruncatedGreenPreconditioner : AttentionPreconditionerBase
{
    private readonly PoissonSystem system;
    private readonly double inverseDiagonal;
    private readonly (int X, int Y, int Z, double Coefficient)[] modes;

    public TruncatedGreenPreconditioner(PoissonSystem system, int modesPerAxis)
        : base($"Truncated-Green(rank={modesPerAxis * modesPerAxis * modesPerAxis})", system.UnknownCount)
    {
        if (modesPerAxis < 1 || modesPerAxis > system.PointsPerAxis) throw new ArgumentOutOfRangeException(nameof(modesPerAxis));
        this.system = system;
        inverseDiagonal = 1d / system.Diagonal;
        modes = (from z in Enumerable.Range(1, modesPerAxis)
                 from y in Enumerable.Range(1, modesPerAxis)
                 from x in Enumerable.Range(1, modesPerAxis)
                 let lambda = SpectralModes.PoissonEigenvalue(system, x, y, z)
                 select (x, y, z, (1d / lambda) - inverseDiagonal)).ToArray();
        if (modes.Any(mode => mode.Coefficient <= 0d)) throw new InvalidOperationException("Selected Green factors do not preserve SPD.");
    }

    public int Rank => modes.Length;
    public override long EstimatedStorageBytes => modes.Length * (3L * sizeof(int) + sizeof(double));

    public override void Apply(ReadOnlySpan<double> residual, Span<double> correction)
    {
        ExperimentalOperatorCalls++;
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < Size; i++) correction[i] = inverseDiagonal * residual[i];
        var n = system.PointsPerAxis;
        var normalization = Math.Pow(2d / (n + 1d), 1.5d);
        foreach (var mode in modes)
        {
            var projection = 0d;
            var index = 0;
            for (var k = 1; k <= n; k++)
            for (var j = 1; j <= n; j++)
            for (var i = 1; i <= n; i++, index++)
            {
                projection += normalization * Math.Sin(Math.PI * mode.X * i / (n + 1d))
                    * Math.Sin(Math.PI * mode.Y * j / (n + 1d))
                    * Math.Sin(Math.PI * mode.Z * k / (n + 1d)) * residual[index];
            }

            index = 0;
            var scale = mode.Coefficient * projection * normalization;
            for (var k = 1; k <= n; k++)
            for (var j = 1; j <= n; j++)
            for (var i = 1; i <= n; i++, index++)
            {
                correction[index] += scale * Math.Sin(Math.PI * mode.X * i / (n + 1d))
                    * Math.Sin(Math.PI * mode.Y * j / (n + 1d))
                    * Math.Sin(Math.PI * mode.Z * k / (n + 1d));
            }
        }
        AddInteractionTicks(Stopwatch.GetTimestamp() - start);
    }

    public double InteractionWeight(int target, int source)
    {
        var n = system.PointsPerAxis;
        var normalization = Math.Pow(2d / (n + 1d), 1.5d);
        var (ti, tj, tk) = Unflatten(target, n);
        var (si, sj, sk) = Unflatten(source, n);
        var value = target == source ? inverseDiagonal : 0d;
        foreach (var mode in modes)
        {
            var qt = normalization * Math.Sin(Math.PI * mode.X * (ti + 1) / (n + 1d)) * Math.Sin(Math.PI * mode.Y * (tj + 1) / (n + 1d)) * Math.Sin(Math.PI * mode.Z * (tk + 1) / (n + 1d));
            var qs = normalization * Math.Sin(Math.PI * mode.X * (si + 1) / (n + 1d)) * Math.Sin(Math.PI * mode.Y * (sj + 1) / (n + 1d)) * Math.Sin(Math.PI * mode.Z * (sk + 1) / (n + 1d));
            value += mode.Coefficient * qt * qs;
        }
        return value;
    }

    private static (int I, int J, int K) Unflatten(int index, int n) => (index % n, (index / n) % n, index / (n * n));
}

/// <summary>
/// 2x2x2 macro-token restriction, deterministic coarse interaction, and constant scatter.
/// The averaging/scatter pair differs only by a positive scalar, so the composed operator is symmetric.
/// </summary>
public sealed class HierarchicalScreenedPreconditioner : AttentionPreconditionerBase
{
    private readonly PoissonSystem fine;
    private readonly PoissonSystem coarse;
    private readonly ScreenedInteractionPreconditioner coarseInteraction;
    private readonly double[] restricted;
    private readonly double[] coarseCorrection;
    private readonly double fineInverseDiagonal;
    private readonly double coarseScale;

    public HierarchicalScreenedPreconditioner(PoissonSystem system, double coarseLatticeLength = 2d)
        : base($"Hierarchical-screened(2x2x2,length={coarseLatticeLength:R})", system.UnknownCount)
    {
        if (system.PointsPerAxis % 2 != 0) throw new ArgumentException("The 2x2x2 hierarchy requires an even lattice size.", nameof(system));
        fine = system;
        coarse = new PoissonSystem(system.PointsPerAxis / 2);
        coarseInteraction = new ScreenedInteractionPreconditioner(coarse, coarseLatticeLength, "macro-screened");
        restricted = new double[coarse.UnknownCount];
        coarseCorrection = new double[coarse.UnknownCount];
        fineInverseDiagonal = 1d / fine.Diagonal;
        coarseScale = coarse.InverseSpacingSquared / (0.5d * fine.InverseSpacingSquared);
    }

    public override long EstimatedStorageBytes => coarseInteraction.EstimatedStorageBytes + (2L * sizeof(double) * coarse.UnknownCount);

    public override void ResetMetrics()
    {
        base.ResetMetrics();
        coarseInteraction.ResetMetrics();
    }

    public override void Apply(ReadOnlySpan<double> residual, Span<double> correction)
    {
        ExperimentalOperatorCalls++;
        var hierarchyStart = Stopwatch.GetTimestamp();
        Restrict2x2x2(residual, restricted, fine.PointsPerAxis);
        AddHierarchyTicks(Stopwatch.GetTimestamp() - hierarchyStart);

        var interactionStart = Stopwatch.GetTimestamp();
        coarseInteraction.Apply(restricted, coarseCorrection);
        AddInteractionTicks(Stopwatch.GetTimestamp() - interactionStart);

        hierarchyStart = Stopwatch.GetTimestamp();
        var n = fine.PointsPerAxis;
        var m = coarse.PointsPerAxis;
        for (var k = 0; k < n; k++)
        for (var j = 0; j < n; j++)
        for (var i = 0; i < n; i++)
        {
            var fineIndex = ((k * n) + j) * n + i;
            var coarseIndex = (((k / 2) * m) + (j / 2)) * m + (i / 2);
            correction[fineIndex] = (fineInverseDiagonal * residual[fineIndex]) + (coarseScale * coarseCorrection[coarseIndex]);
        }
        AddHierarchyTicks(Stopwatch.GetTimestamp() - hierarchyStart);
    }

    public static void Restrict2x2x2(ReadOnlySpan<double> fine, Span<double> coarse, int n)
    {
        if (n < 2 || n % 2 != 0 || fine.Length != n * n * n || coarse.Length != (n / 2) * (n / 2) * (n / 2))
            throw new ArgumentException("Fine/coarse vectors must describe an even 2x2x2 hierarchy.");
        var m = n / 2;
        for (var ck = 0; ck < m; ck++)
        for (var cj = 0; cj < m; cj++)
        for (var ci = 0; ci < m; ci++)
        {
            var sum = 0d;
            for (var dk = 0; dk < 2; dk++)
            for (var dj = 0; dj < 2; dj++)
            for (var di = 0; di < 2; di++)
            {
                var i = (2 * ci) + di; var j = (2 * cj) + dj; var k = (2 * ck) + dk;
                sum += fine[((k * n) + j) * n + i];
            }
            coarse[((ck * m) + cj) * m + ci] = sum / 8d;
        }
    }
}

/// <summary>
/// Conventional two-level control with the same 2x2x2 transfer and a fixed SPD
/// weighted-Jacobi polynomial approximation of the Galerkin coarse inverse.
/// </summary>
public sealed class TwoLevelCoarseGridPreconditioner : AttentionPreconditionerBase
{
    private readonly PoissonSystem fine;
    private readonly int coarseSize;
    private readonly int smoothingSteps;
    private readonly double[] restricted;
    private readonly double[] coarseCorrection;
    private readonly double[] iterate;
    private readonly double[] work;
    private readonly double fineInverseDiagonal;

    public TwoLevelCoarseGridPreconditioner(PoissonSystem system, int coarseSmoothingSteps = 8)
        : base($"Two-level-control(Jacobi-{coarseSmoothingSteps})", system.UnknownCount)
    {
        if (system.PointsPerAxis % 2 != 0) throw new ArgumentException("The 2x2x2 hierarchy requires an even lattice size.", nameof(system));
        if (coarseSmoothingSteps < 1) throw new ArgumentOutOfRangeException(nameof(coarseSmoothingSteps));
        fine = system;
        coarseSize = system.PointsPerAxis / 2;
        smoothingSteps = coarseSmoothingSteps;
        var count = coarseSize * coarseSize * coarseSize;
        restricted = new double[count];
        coarseCorrection = new double[count];
        iterate = new double[count];
        work = new double[count];
        fineInverseDiagonal = 1d / system.Diagonal;
    }

    public override long EstimatedStorageBytes => 4L * sizeof(double) * restricted.Length;

    public override void Apply(ReadOnlySpan<double> residual, Span<double> correction)
    {
        ExperimentalOperatorCalls++;
        var start = Stopwatch.GetTimestamp();
        HierarchicalScreenedPreconditioner.Restrict2x2x2(residual, restricted, fine.PointsPerAxis);
        Array.Clear(iterate);
        const double omega = 2d / 3d;
        var inverseCoarseDiagonal = 1d / (3d * fine.InverseSpacingSquared);
        for (var step = 0; step < smoothingSteps; step++)
        {
            ApplyGalerkinCoarse(iterate, work);
            for (var i = 0; i < iterate.Length; i++) iterate[i] += omega * inverseCoarseDiagonal * (restricted[i] - work[i]);
        }
        iterate.CopyTo(coarseCorrection, 0);

        var n = fine.PointsPerAxis;
        var m = coarseSize;
        for (var k = 0; k < n; k++)
        for (var j = 0; j < n; j++)
        for (var i = 0; i < n; i++)
        {
            var fineIndex = ((k * n) + j) * n + i;
            var coarseIndex = (((k / 2) * m) + (j / 2)) * m + (i / 2);
            correction[fineIndex] = (fineInverseDiagonal * residual[fineIndex]) + coarseCorrection[coarseIndex];
        }
        AddHierarchyTicks(Stopwatch.GetTimestamp() - start);
    }

    private void ApplyGalerkinCoarse(ReadOnlySpan<double> source, Span<double> destination)
    {
        var m = coarseSize;
        var offDiagonal = 0.5d * fine.InverseSpacingSquared;
        for (var k = 0; k < m; k++)
        for (var j = 0; j < m; j++)
        for (var i = 0; i < m; i++)
        {
            var index = ((k * m) + j) * m + i;
            var value = 6d * source[index];
            if (i > 0) value -= source[index - 1];
            if (i + 1 < m) value -= source[index + 1];
            if (j > 0) value -= source[index - m];
            if (j + 1 < m) value -= source[index + m];
            if (k > 0) value -= source[index - (m * m)];
            if (k + 1 < m) value -= source[index + (m * m)];
            destination[index] = offDiagonal * value;
        }
    }
}

internal static class SpectralModes
{
    public static double[] Create(int n, int modeX, int modeY, int modeZ)
    {
        var result = new double[n * n * n];
        var normalization = Math.Pow(2d / (n + 1d), 1.5d);
        var index = 0;
        for (var k = 1; k <= n; k++)
        for (var j = 1; j <= n; j++)
        for (var i = 1; i <= n; i++, index++)
            result[index] = normalization * Math.Sin(Math.PI * modeX * i / (n + 1d))
                * Math.Sin(Math.PI * modeY * j / (n + 1d)) * Math.Sin(Math.PI * modeZ * k / (n + 1d));
        return result;
    }

    public static double PoissonEigenvalue(PoissonSystem system, int x, int y, int z)
    {
        var n = system.PointsPerAxis;
        return 2d * system.InverseSpacingSquared * (3d
            - Math.Cos(Math.PI * x / (n + 1d))
            - Math.Cos(Math.PI * y / (n + 1d))
            - Math.Cos(Math.PI * z / (n + 1d)));
    }
}
