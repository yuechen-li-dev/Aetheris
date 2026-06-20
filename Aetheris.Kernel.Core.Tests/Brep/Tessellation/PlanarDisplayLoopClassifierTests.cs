using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class PlanarDisplayLoopClassifierTests
{
    [Fact]
    public void PlanarLoopClassifier_ClassifiesOuterAndHoleLoops()
    {
        var result = Classifier().Classify(9, [(2, Rect(3, 3, 7, 7)), (1, Rect(0, 0, 10, 10))]);

        Assert.Contains(result, item => item.Loop.LoopId == 1 && item.Role == PlanarDisplayLoopRole.Outer && item.Reasons.Contains("largest-absolute-area"));
        Assert.Contains(result, item => item.Loop.LoopId == 2 && item.Role == PlanarDisplayLoopRole.Hole && item.Reasons.Contains("containment-depth-odd"));
    }

    [Fact]
    public void PlanarLoopClassifier_RejectsDegenerateLoop()
    {
        var result = Classifier().Classify(9, [(1, new[] { new Point3D(0, 0, 0), new Point3D(0, 0, 0), new Point3D(1, 0, 0) })]);

        var classification = Assert.Single(result);
        Assert.Equal(PlanarDisplayLoopRole.Degenerate, classification.Role);
        Assert.Contains("duplicate-point-collapse", classification.Reasons);
    }

    [Fact]
    public void PlanarLoopClassifier_Deterministic()
    {
        var loops = new (int, IReadOnlyList<Point3D>)[] { (2, Rect(3, 3, 7, 7)), (1, Rect(0, 0, 10, 10)) };

        var first = Classifier().Classify(9, loops);
        var second = Classifier().Classify(9, loops);

        Assert.Equal(first.Select(item => (item.Loop.LoopId, item.Role, string.Join("|", item.Reasons))), second.Select(item => (item.Loop.LoopId, item.Role, string.Join("|", item.Reasons))));
    }

    [Fact]
    public void PlanarWithHoles_BoundedFailureHasStableDiagnostic()
    {
        var result = Classifier().Classify(9, [(1, Rect(0, 0, 10, 10)), (2, Rect(2, 2, 8, 8)), (3, Rect(4, 4, 6, 6))]);

        Assert.Contains(result, item => item.Role == PlanarDisplayLoopRole.Island && item.Reasons.Contains("unsupported-nesting"));
    }

    private static PlanarDisplayLoopClassifier Classifier() => new();

    private static Point3D[] Rect(double minX, double minY, double maxX, double maxY) =>
    [
        new(minX, minY, 0),
        new(maxX, minY, 0),
        new(maxX, maxY, 0),
        new(minX, maxY, 0),
    ];
}
