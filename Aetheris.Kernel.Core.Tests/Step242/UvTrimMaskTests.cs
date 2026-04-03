using Aetheris.Kernel.Core.Brep.Tessellation;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class UvTrimMaskTests
{
    [Fact]
    public void Contains_OuterLoopOnly_AcceptsInsideAndRejectsOutside()
    {
        var mask = new UvTrimMask(
            outerLoop:
            [
                new UvPoint(0d, 0d),
                new UvPoint(1d, 0d),
                new UvPoint(1d, 1d),
                new UvPoint(0d, 1d),
            ],
            innerLoops: []);

        Assert.True(mask.Contains(new UvPoint(0.5d, 0.5d)));
        Assert.False(mask.Contains(new UvPoint(1.2d, 0.5d)));
    }

    [Fact]
    public void Contains_OuterPlusHole_RejectsHoleAndAcceptsRing()
    {
        var mask = new UvTrimMask(
            outerLoop:
            [
                new UvPoint(0d, 0d),
                new UvPoint(1d, 0d),
                new UvPoint(1d, 1d),
                new UvPoint(0d, 1d),
            ],
            innerLoops:
            [
                [
                    new UvPoint(0.3d, 0.3d),
                    new UvPoint(0.7d, 0.3d),
                    new UvPoint(0.7d, 0.7d),
                    new UvPoint(0.3d, 0.7d),
                ],
            ]);

        Assert.False(mask.Contains(new UvPoint(0.5d, 0.5d)));
        Assert.True(mask.Contains(new UvPoint(0.2d, 0.5d)));
    }

    [Fact]
    public void Determinism_RepeatedConstructionAndQueries_AreEquivalent()
    {
        IReadOnlyList<UvPoint> outer =
        [
            new(0d, 0d),
            new(1d, 0d),
            new(1d, 1d),
            new(0d, 1d),
        ];
        IReadOnlyList<UvPoint> hole =
        [
            new(0.35d, 0.35d),
            new(0.65d, 0.35d),
            new(0.65d, 0.65d),
            new(0.35d, 0.65d),
        ];

        var first = new UvTrimMask(outer, [hole]);
        var second = new UvTrimMask(outer, [hole]);
        UvPoint[] probes =
        [
            new(0.1d, 0.1d),
            new(0.5d, 0.5d),
            new(0.9d, 0.9d),
            new(1.2d, 0.5d),
            new(0.35d, 0.5d),
        ];

        var firstResults = probes.Select(first.Contains).ToArray();
        var secondResults = probes.Select(second.Contains).ToArray();

        Assert.Equal(firstResults, secondResults);
        Assert.True(first.KeepCellByCorners(
            new UvPoint(0d, 0d),
            new UvPoint(0.2d, 0d),
            new UvPoint(0d, 0.2d),
            new UvPoint(0.2d, 0.2d)));
        Assert.False(first.KeepCellByCorners(
            new UvPoint(0.4d, 0.4d),
            new UvPoint(0.6d, 0.4d),
            new UvPoint(0.4d, 0.6d),
            new UvPoint(0.6d, 0.6d)));
    }
}
