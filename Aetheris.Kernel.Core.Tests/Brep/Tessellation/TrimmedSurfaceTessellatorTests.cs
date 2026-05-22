using Aetheris.Kernel.Core.Brep.Tessellation;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class TrimmedSurfaceTessellatorTests
{
    [Fact]
    public void EvaluatePointContainmentMetricsForTest_ReusesClassificationAndSkipsLoopChecksOutsideBounds()
    {
        var loops = new IReadOnlyList<(double U, double V)>[]
        {
            new (double U, double V)[]
            {
                (0d, 0d),
                (10d, 0d),
                (10d, 10d),
                (0d, 10d),
            },
            new (double U, double V)[]
            {
                (4d, 4d),
                (6d, 4d),
                (6d, 6d),
                (4d, 6d),
            },
        };

        var repeatedPoints = new List<(double U, double V)>();
        for (var i = 0; i < 200; i++)
        {
            repeatedPoints.Add((1d, 1d));
            repeatedPoints.Add((5d, 5d));
            repeatedPoints.Add((11d, 11d));
        }

        var metrics = TrimmedSurfaceTessellator.EvaluatePointContainmentMetricsForTest(
            loops,
            outerLoopIndex: 0,
            repeatedPoints);

        Assert.True(metrics.CacheHits >= 500, $"Expected cache reuse across repeated samples, observed {metrics.CacheHits} cache hits.");
        Assert.True(metrics.FullContainsPointEvaluations <= 4, $"Expected bounded full point-in-loop checks due to caching+bounds rejection, observed {metrics.FullContainsPointEvaluations}.");
    }
}
