using Aetheris.Kernel.Core.Brep.Tessellation;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class LoopPatternClassifierTests
{
    [Theory]
    [InlineData(1, 1, 0, 1, 0, 1, "single-coedge circle-only seam-reused revolved loop")]
    [InlineData(4, 4, 0, 4, 0, 0, "four-coedge circle-only non-seam revolved loop")]
    [InlineData(5, 5, 2, 3, 0, 0, "repeated mixed line/circle revolved loop")]
    [InlineData(5, 5, 0, 2, 3, 0, "repeated mixed circle/bspline revolved loop")]
    [InlineData(6, 6, 0, 1, 5, 0, "six-coedge single-circle/five-bspline revolved loop")]
    [InlineData(2, 2, 1, 0, 1, 0, "other (coedges=2, uniqueEdges=2)")]
    public void LoopPatternClassifier_RefactorPreservesKnownLabels(int coedges, int uniqueEdges, int lines, int circles, int bsplines, int seams, string label)
    {
        var result = new LoopPatternClassifier().Classify(new LoopPatternEvidence(coedges, uniqueEdges, lines, circles, bsplines, seams));

        Assert.Equal(label, result.Label);
        Assert.Equal(coedges, result.Evidence.Coedges);
        Assert.NotEmpty(result.Diagnostics);
    }
}
