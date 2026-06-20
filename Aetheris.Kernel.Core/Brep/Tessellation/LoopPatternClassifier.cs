namespace Aetheris.Kernel.Core.Brep.Tessellation;

internal enum LoopPatternKind
{
    CircleOnly,
    MixedLineCircle,
    MixedCircleBspline,
    SingleCircleFiveBspline,
    ConeOrRevolved,
    BsplineConeOrRevolved,
    MixedLineBspline,
    BsplineOnly,
    Other
}

internal sealed record LoopPatternEvidence(int Coedges, int UniqueEdges, int LineCoedges, int CircleCoedges, int BSplineCoedges, int SeamUses);
internal sealed record LoopPatternClassification(LoopPatternKind Kind, string Label, LoopPatternEvidence Evidence, IReadOnlyList<string> Diagnostics);

internal sealed class LoopPatternClassifier
{
    private readonly IReadOnlyList<LoopPatternRule> _rules =
    [
        new("single-circle-seam", e => e.LineCoedges == 0 && e.CircleCoedges == e.Coedges && e.Coedges == 1 && e.UniqueEdges == 1 && e.SeamUses == 1, LoopPatternKind.CircleOnly, "single-coedge circle-only seam-reused revolved loop"),
        new("four-circle-nonseam", e => e.LineCoedges == 0 && e.CircleCoedges == e.Coedges && e.Coedges == 4 && e.UniqueEdges == 4 && e.SeamUses == 0, LoopPatternKind.CircleOnly, "four-coedge circle-only non-seam revolved loop"),
        new("circle-only", e => e.LineCoedges == 0 && e.CircleCoedges == e.Coedges, LoopPatternKind.CircleOnly, e => e.SeamUses > 0 ? "circle-only seam reused loop" : "circle-only non-seam loop"),
        new("repeated-line-circle", e => e.LineCoedges >= 2 && e.CircleCoedges >= 2 && e.Coedges >= 5, LoopPatternKind.MixedLineCircle, "repeated mixed line/circle revolved loop"),
        new("repeated-circle-bspline", e => e.LineCoedges == 0 && e.CircleCoedges >= 2 && e.BSplineCoedges >= 1 && e.Coedges >= 5, LoopPatternKind.MixedCircleBspline, "repeated mixed circle/bspline revolved loop"),
        new("six-single-circle-five-bspline", e => e.LineCoedges == 0 && e.CircleCoedges == 1 && e.BSplineCoedges == 5 && e.Coedges == 6 && e.UniqueEdges == 6, LoopPatternKind.SingleCircleFiveBspline, "six-coedge single-circle/five-bspline revolved loop"),
        new("three-line-circle", e => e.LineCoedges >= 2 && e.CircleCoedges >= 1 && e.Coedges == 3, LoopPatternKind.ConeOrRevolved, "three-coedge cone/revolved loop"),
        new("three-bspline", e => e.BSplineCoedges == 3 && e.Coedges == 3 && e.UniqueEdges == 3, LoopPatternKind.BsplineConeOrRevolved, "three-coedge cone/revolved bspline loop"),
        new("three-circle-bspline", e => e.CircleCoedges == 1 && e.BSplineCoedges == 2 && e.Coedges == 3 && e.UniqueEdges == 3, LoopPatternKind.MixedCircleBspline, "three-coedge cone/revolved mixed circle/bspline loop"),
        new("four-line-circle", e => e.LineCoedges == 2 && e.CircleCoedges == 2 && e.Coedges == 4, LoopPatternKind.MixedLineCircle, "four-coedge mixed line/circle loop"),
        new("four-circle-bspline", e => e.LineCoedges == 0 && e.CircleCoedges == 2 && e.BSplineCoedges == 2 && e.Coedges == 4, LoopPatternKind.MixedCircleBspline, "four-coedge mixed circle/bspline loop"),
        new("four-single-circle-three-bspline", e => e.LineCoedges == 0 && e.CircleCoedges == 1 && e.BSplineCoedges == 3 && e.Coedges == 4 && e.UniqueEdges >= 3, LoopPatternKind.MixedCircleBspline, "four-coedge single-circle/three-bspline revolved loop"),
        new("four-three-circle-single-bspline", e => e.LineCoedges == 0 && e.CircleCoedges == 3 && e.BSplineCoedges == 1 && e.Coedges == 4 && e.UniqueEdges == 4, LoopPatternKind.MixedCircleBspline, "four-coedge three-circle/single-bspline revolved loop"),
        new("four-line-bspline", e => e.LineCoedges == 2 && e.CircleCoedges == 0 && e.BSplineCoedges == 2 && e.Coedges == 4 && e.UniqueEdges == 4, LoopPatternKind.MixedLineBspline, "four-coedge mixed line/bspline revolved loop"),
        new("four-bspline", e => e.LineCoedges == 0 && e.CircleCoedges == 0 && e.BSplineCoedges == 4 && e.Coedges == 4, LoopPatternKind.BsplineOnly, "four-coedge bspline-only revolved loop"),
        new("six-bspline-seam", e => e.LineCoedges == 0 && e.CircleCoedges == 0 && e.BSplineCoedges == 6 && e.Coedges == 6 && e.UniqueEdges == 6 && e.SeamUses > 0, LoopPatternKind.BsplineOnly, "six-coedge bspline-only seam-reused revolved loop"),
        new("six-bspline", e => e.LineCoedges == 0 && e.CircleCoedges == 0 && e.BSplineCoedges == 6 && e.Coedges == 6 && e.UniqueEdges == 6, LoopPatternKind.BsplineOnly, "six-coedge bspline-only revolved loop"),
    ];

    public LoopPatternClassification Classify(LoopPatternEvidence evidence)
    {
        foreach (var rule in _rules)
        {
            if (rule.Predicate(evidence))
            {
                return new LoopPatternClassification(rule.Kind, rule.LabelFactory(evidence), evidence, [$"rule={rule.Name}"]);
            }
        }

        return new LoopPatternClassification(LoopPatternKind.Other, $"other (coedges={evidence.Coedges}, uniqueEdges={evidence.UniqueEdges})", evidence, ["rule=other"]);
    }

    private sealed record LoopPatternRule(string Name, Func<LoopPatternEvidence, bool> Predicate, LoopPatternKind Kind, Func<LoopPatternEvidence, string> LabelFactory)
    {
        public LoopPatternRule(string name, Func<LoopPatternEvidence, bool> predicate, LoopPatternKind kind, string label)
            : this(name, predicate, kind, _ => label) { }
    }
}
