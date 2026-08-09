using System.Diagnostics;

namespace Aetheris.Continuum.Experiments.Attention;

public sealed record AttentionE0MethodResult(
    string Method,
    int PointsPerAxis,
    int Unknowns,
    int Nonzeros,
    int Iterations,
    double FinalResidual,
    double RelativeResidual,
    double RelativeSolutionError,
    int SparseMatvecs,
    int ExperimentalOperatorCalls,
    double SetupMilliseconds,
    double RuntimeMilliseconds,
    double SparseMatvecMilliseconds,
    double PreconditionerMilliseconds,
    double InteractionMilliseconds,
    double HierarchyMilliseconds,
    long EstimatedMemoryBytes,
    IReadOnlyList<ResidualSample> ResidualHistory);

public sealed record ModeReductionResult(string Method, string Mode, double ErrorNormAfterUnitCorrection, double ReductionFactor);
public sealed record PreconditionerContractResult(string Method, double RelativeSymmetryDefect, double MinimumObservedEnergy, bool Passed);
public sealed record InteractionWeightSample(int Rank, int I, int J, int K, double X, double Y, double Z, double Weight, double AbsoluteWeight);

public sealed record AttentionE0Benchmark(
    string Schema,
    string Hypothesis,
    string Domain,
    string BoundaryConditions,
    string ManufacturedSolution,
    double RelativeTolerance,
    IReadOnlyList<AttentionE0MethodResult> Methods,
    IReadOnlyList<ModeReductionResult> ModeAnalysis,
    IReadOnlyList<PreconditionerContractResult> MathematicalContracts,
    IReadOnlyList<InteractionWeightSample> StrongestInteractions);

public static class AttentionE0Experiments
{
    public const double RelativeTolerance = 1e-8;

    public static AttentionE0Benchmark Run(IReadOnlyList<int>? sizes = null, int repetitions = 3)
    {
        sizes ??= [8, 16, 32];
        if (sizes.Count == 0 || sizes.Any(size => size < 2 || size % 2 != 0))
            throw new ArgumentException("Experiment sizes must be non-empty, even, and at least two.", nameof(sizes));
        if (repetitions < 1) throw new ArgumentOutOfRangeException(nameof(repetitions));

        WarmUp();
        var methods = new List<AttentionE0MethodResult>();
        foreach (var size in sizes)
        {
            var problem = PoissonSystem.CreateManufactured(size);
            foreach (var factory in Factories(problem.System))
                methods.Add(RunMethod(problem, factory, repetitions));
        }

        var analysisSystem = new PoissonSystem(sizes[^1]);
        var analysisPreconditioners = Factories(analysisSystem).Select(factory => factory()).Skip(1).ToArray();
        var modeAnalysis = analysisPreconditioners.SelectMany(preconditioner => new[]
        {
            AnalyzeMode(analysisSystem, preconditioner, 1, 1, 1, "low-(1,1,1)"),
            AnalyzeMode(analysisSystem, preconditioner, analysisSystem.PointsPerAxis, analysisSystem.PointsPerAxis, analysisSystem.PointsPerAxis, "high-(n,n,n)"),
        }).ToArray();
        var contracts = analysisPreconditioners.Select(preconditioner => CheckContract(analysisSystem, preconditioner)).ToArray();

        var interactionSystem = new PoissonSystem(sizes.Contains(16) ? 16 : sizes[0]);
        var green = new TruncatedGreenPreconditioner(interactionSystem, 2);
        var targetCoordinate = interactionSystem.PointsPerAxis / 2;
        var target = interactionSystem.Flatten(targetCoordinate, targetCoordinate, targetCoordinate);
        var interactions = Enumerable.Range(0, interactionSystem.UnknownCount)
            .Select(index =>
            {
                var n = interactionSystem.PointsPerAxis;
                var i = index % n; var j = (index / n) % n; var k = index / (n * n);
                var weight = green.InteractionWeight(target, index);
                return new InteractionWeightSample(0, i, j, k, (i + 1d) * interactionSystem.Spacing,
                    (j + 1d) * interactionSystem.Spacing, (k + 1d) * interactionSystem.Spacing, weight, Math.Abs(weight));
            })
            .OrderByDescending(sample => sample.AbsoluteWeight)
            .ThenBy(sample => sample.K).ThenBy(sample => sample.J).ThenBy(sample => sample.I)
            .Take(32).Select((sample, rank) => sample with { Rank = rank + 1 }).ToArray();

        return new AttentionE0Benchmark(
            "aetheris-continuum-attention-e0-v1",
            "An analytic, SPD local/global/hierarchical residual interaction can reduce exact-system PCG work at competitive total cost.",
            "unit cube; n^3 interior Cartesian nodes",
            "homogeneous Dirichlet on all six faces",
            "u=x(1-x)y(1-y)z(1-z); continuous f sampled at nodes",
            RelativeTolerance, methods, modeAnalysis, contracts, interactions);
    }

    private static IReadOnlyList<Func<IAttentionPreconditioner>> Factories(PoissonSystem system) =>
    [
        () => new IdentityPreconditioner(system.UnknownCount),
        () => new JacobiPreconditioner(system),
        () => new CompactInteractionPreconditioner(system),
        () => new ScreenedInteractionPreconditioner(system),
        () => new TruncatedGreenPreconditioner(system, 2),
        () => new HierarchicalScreenedPreconditioner(system),
        () => new TwoLevelCoarseGridPreconditioner(system),
    ];

    private static AttentionE0MethodResult RunMethod(PoissonProblem problem, Func<IAttentionPreconditioner> factory, int repetitions)
    {
        var runs = new List<(LinearSolveResult Solve, IAttentionPreconditioner Preconditioner, double Setup)>(repetitions);
        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            var setup = Stopwatch.StartNew();
            var preconditioner = factory();
            setup.Stop();
            var solve = ConjugateGradient.Solve(problem.System, problem.Forcing, preconditioner, RelativeTolerance,
                maximumIterations: Math.Max(200, 8 * problem.System.PointsPerAxis));
            runs.Add((solve, preconditioner, setup.Elapsed.TotalMilliseconds));
        }

        var selected = runs.OrderBy(run => run.Solve.TotalMilliseconds).ElementAt(repetitions / 2);
        var verifiedResidual = ExactResidual(problem.System, selected.Solve.Solution, problem.Forcing);
        var solutionError = RelativeError(selected.Solve.Solution, problem.ExactSolution);
        var workingStorage = 5L * sizeof(double) * problem.System.UnknownCount;
        return new AttentionE0MethodResult(
            selected.Preconditioner.Name, problem.System.PointsPerAxis, problem.System.UnknownCount,
            problem.System.NonzeroCount, selected.Solve.Iterations, verifiedResidual,
            verifiedResidual / ConjugateGradient.Norm(problem.Forcing), solutionError,
            selected.Solve.SparseMatvecs, selected.Preconditioner.ExperimentalOperatorCalls,
            selected.Setup, selected.Solve.TotalMilliseconds, selected.Solve.SparseMatvecMilliseconds,
            selected.Solve.PreconditionerMilliseconds, selected.Preconditioner.InteractionMilliseconds,
            selected.Preconditioner.HierarchyMilliseconds, workingStorage + selected.Preconditioner.EstimatedStorageBytes,
            selected.Solve.ResidualHistory);
    }

    private static ModeReductionResult AnalyzeMode(PoissonSystem system, IAttentionPreconditioner preconditioner,
        int x, int y, int z, string name)
    {
        var error = SpectralModes.Create(system.PointsPerAxis, x, y, z);
        var residual = new double[system.UnknownCount];
        var correction = new double[system.UnknownCount];
        system.Apply(error, residual);
        preconditioner.Apply(residual, correction);
        for (var i = 0; i < error.Length; i++) error[i] -= correction[i];
        var after = ConjugateGradient.Norm(error);
        return new ModeReductionResult(preconditioner.Name, name, after, after);
    }

    private static PreconditionerContractResult CheckContract(PoissonSystem system, IAttentionPreconditioner preconditioner)
    {
        var x = new double[system.UnknownCount];
        var y = new double[system.UnknownCount];
        for (var i = 0; i < x.Length; i++)
        {
            x[i] = Math.Sin((i + 1d) * 0.37d) + (0.01d * (i % 11));
            y[i] = Math.Cos((i + 1d) * 0.19d) - (0.02d * (i % 7));
        }
        var px = new double[x.Length]; var py = new double[x.Length];
        preconditioner.Apply(x, px); preconditioner.Apply(y, py);
        var xPy = ConjugateGradient.Dot(x, py); var yPx = ConjugateGradient.Dot(y, px);
        var defect = Math.Abs(xPy - yPx) / Math.Max(1d, Math.Max(Math.Abs(xPy), Math.Abs(yPx)));
        var energy = Math.Min(ConjugateGradient.Dot(x, px), ConjugateGradient.Dot(y, py));
        return new PreconditionerContractResult(preconditioner.Name, defect, energy, defect < 1e-11 && energy > 0d);
    }

    private static double ExactResidual(PoissonSystem system, ReadOnlySpan<double> solution, ReadOnlySpan<double> forcing)
    {
        var applied = new double[system.UnknownCount];
        system.Apply(solution, applied);
        for (var i = 0; i < applied.Length; i++) applied[i] -= forcing[i];
        return ConjugateGradient.Norm(applied);
    }

    private static double RelativeError(ReadOnlySpan<double> actual, ReadOnlySpan<double> expected)
    {
        var difference = new double[actual.Length];
        for (var i = 0; i < actual.Length; i++) difference[i] = actual[i] - expected[i];
        return ConjugateGradient.Norm(difference) / ConjugateGradient.Norm(expected);
    }

    private static void WarmUp()
    {
        var problem = PoissonSystem.CreateManufactured(4);
        _ = ConjugateGradient.Solve(problem.System, problem.Forcing, new JacobiPreconditioner(problem.System), 1e-4, 20);
    }
}
