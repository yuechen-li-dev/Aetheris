using Aetheris.Continuum.Experiments.Attention;

namespace Aetheris.Continuum.Tests.Experiments.Attention;

public sealed class AttentionE0Tests
{
    [Fact]
    public void SevenPointPoissonAssemblyHasExpectedInteriorAndBoundaryRows()
    {
        var system = new PoissonSystem(4);

        Assert.Equal(64, system.UnknownCount);
        Assert.Equal(352, system.NonzeroCount);
        Assert.Equal(7, system.Row(1, 1, 1).Count);
        Assert.Equal(4, system.Row(0, 0, 0).Count);
        Assert.Equal(system.Diagonal, system.Row(1, 1, 1).Single(entry => entry.Row == entry.Column).Value);
        Assert.All(system.Row(1, 1, 1).Where(entry => entry.Row != entry.Column),
            entry => Assert.Equal(-system.InverseSpacingSquared, entry.Value));
    }

    [Fact]
    public void CgAndJacobiConvergeAgainstIndependentManufacturedSolution()
    {
        var problem = PoissonSystem.CreateManufactured(8);
        var cg = ConjugateGradient.Solve(problem.System, problem.Forcing, new IdentityPreconditioner(problem.System.UnknownCount));
        var jacobi = ConjugateGradient.Solve(problem.System, problem.Forcing, new JacobiPreconditioner(problem.System));

        Assert.True(cg.RelativeResidual < 1e-8);
        Assert.True(jacobi.RelativeResidual < 1e-8);
        AssertRelativeErrorBelow(cg.Solution, problem.ExactSolution, 1e-8);
        AssertRelativeErrorBelow(jacobi.Solution, problem.ExactSolution, 1e-8);
        Assert.Equal(cg.Iterations, jacobi.Iterations); // Uniform Jacobi is only scalar scaling here.
    }

    [Fact]
    public void ScreenedKernelIsBitwiseDeterministic()
    {
        var system = new PoissonSystem(8);
        var kernel = new ScreenedInteractionPreconditioner(system);
        var residual = Enumerable.Range(0, system.UnknownCount).Select(i => Math.Sin(i * 0.17d)).ToArray();
        var first = new double[system.UnknownCount];
        var second = new double[system.UnknownCount];

        kernel.Apply(residual, first);
        kernel.Apply(residual, second);

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("compact")]
    [InlineData("screened")]
    [InlineData("green")]
    [InlineData("hierarchical")]
    [InlineData("coarse-control")]
    public void ExperimentalPreconditionersAreNumericallySymmetricAndPositive(string kind)
    {
        var system = new PoissonSystem(8);
        IAttentionPreconditioner preconditioner = kind switch
        {
            "compact" => new CompactInteractionPreconditioner(system),
            "screened" => new ScreenedInteractionPreconditioner(system),
            "green" => new TruncatedGreenPreconditioner(system, 2),
            "hierarchical" => new HierarchicalScreenedPreconditioner(system),
            _ => new TwoLevelCoarseGridPreconditioner(system),
        };
        var x = Enumerable.Range(0, system.UnknownCount).Select(i => Math.Sin((i + 1d) * 0.31d)).ToArray();
        var y = Enumerable.Range(0, system.UnknownCount).Select(i => Math.Cos((i + 1d) * 0.23d)).ToArray();
        var px = new double[x.Length]; var py = new double[y.Length];
        preconditioner.Apply(x, px); preconditioner.Apply(y, py);
        var xPy = Dot(x, py); var yPx = Dot(y, px);

        Assert.True(Math.Abs(xPy - yPx) / Math.Max(1d, Math.Abs(xPy)) < 1e-11);
        Assert.True(Dot(x, px) > 0d);
        Assert.True(Dot(y, py) > 0d);
    }

    [Fact]
    public void HierarchyAveragesEachTwoByTwoByTwoMacroTokenDeterministically()
    {
        var fine = Enumerable.Range(1, 64).Select(value => (double)value).ToArray();
        var coarse = new double[8];

        HierarchicalScreenedPreconditioner.Restrict2x2x2(fine, coarse, 4);

        Assert.Equal(11.5d, coarse[0]);
        Assert.Equal(13.5d, coarse[1]);
        Assert.Equal(19.5d, coarse[2]);
        Assert.Equal(53.5d, coarse[7]);
        var repeated = new double[8];
        HierarchicalScreenedPreconditioner.Restrict2x2x2(fine, repeated, 4);
        Assert.Equal(coarse, repeated);
    }

    [Fact]
    public void AttentionPathsConvergeByExactSparseResidual()
    {
        var problem = PoissonSystem.CreateManufactured(8);
        IAttentionPreconditioner[] preconditioners =
        [
            new CompactInteractionPreconditioner(problem.System),
            new ScreenedInteractionPreconditioner(problem.System),
            new TruncatedGreenPreconditioner(problem.System, 2),
            new HierarchicalScreenedPreconditioner(problem.System),
        ];

        foreach (var preconditioner in preconditioners)
        {
            var result = ConjugateGradient.Solve(problem.System, problem.Forcing, preconditioner);
            var applied = new double[problem.System.UnknownCount];
            problem.System.Apply(result.Solution, applied);
            for (var i = 0; i < applied.Length; i++) applied[i] -= problem.Forcing[i];
            Assert.True(Math.Sqrt(Dot(applied, applied)) / Math.Sqrt(Dot(problem.Forcing, problem.Forcing)) < 1e-8,
                $"{preconditioner.Name} failed the exact residual check.");
        }
    }

    private static void AssertRelativeErrorBelow(double[] actual, double[] expected, double tolerance)
    {
        var difference = actual.Zip(expected, (a, e) => a - e).ToArray();
        Assert.True(Math.Sqrt(Dot(difference, difference) / Dot(expected, expected)) < tolerance);
    }

    private static double Dot(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        var sum = 0d;
        for (var i = 0; i < left.Count; i++) sum += left[i] * right[i];
        return sum;
    }
}
