using Aetheris.Kernel.Core.Geometry.Curves;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// A narrow, plan-time section transition: only the outer line loop may change.
/// Inner loops are carried by stable feature identity and must be identical in local
/// Profile coordinates.  This is intentionally distinct from <see cref="PrismaticSectionSlab"/>.
/// </summary>
public sealed record VariableOuterVertexCorrespondence(string LowerSegmentStableId, string UpperSegmentStableId, int LowerVertexIndex, int UpperVertexIndex);
public sealed record VariableOuterSegmentCorrespondence(string LowerSegmentStableId, string UpperSegmentStableId, int LowerSegmentIndex, int UpperSegmentIndex);
public sealed record UnchangedInnerLoopCorrespondence(string StableId, string FeatureId, ResolvedProfile2D LowerLoop, ResolvedProfile2D UpperLoop);
public sealed record VariableOuterSectionInterval(
    string StableId, double LowerStation, double UpperStation,
    ResolvedProfile2D LowerOuter, ResolvedProfile2D UpperOuter,
    IReadOnlyList<VariableOuterVertexCorrespondence> OuterVertices,
    IReadOnlyList<VariableOuterSegmentCorrespondence> OuterSegments,
    IReadOnlyList<UnchangedInnerLoopCorrespondence> InnerLoops,
    string SemanticOwner, IReadOnlyList<string> Provenance);
public sealed record VariableOuterSectionIntervalValidation(bool IsValid, IReadOnlyList<string> Diagnostics);

public static class VariableOuterSectionIntervalValidator
{
    private const double Tol = 1e-8;

    public static VariableOuterSectionIntervalValidation Validate(VariableOuterSectionInterval interval)
    {
        var diagnostics = new List<string>();
        if (!double.IsFinite(interval.LowerStation) || !double.IsFinite(interval.UpperStation) || interval.UpperStation - interval.LowerStation <= Tol)
            diagnostics.Add("VariableOuterSectionIntervalStationInvalid");
        var lower = interval.LowerOuter.Loops.SingleOrDefault(x => x.IsOuter);
        var upper = interval.UpperOuter.Loops.SingleOrDefault(x => x.IsOuter);
        if (lower is null || upper is null) diagnostics.Add("VariableOuterSectionIntervalOuterLoopMissing");
        else
        {
            if (lower.Segments.Count != upper.Segments.Count) diagnostics.Add("VariableOuterSectionIntervalOuterVertexCountMismatch");
            if (lower.Segments.Any(x => x.Geometry is not LineArcLineSegment2D) || upper.Segments.Any(x => x.Geometry is not LineArcLineSegment2D)) diagnostics.Add("VariableOuterSectionIntervalOuterSegmentKindUnsupported");
            var expected = lower.Segments.Count;
            if (interval.OuterVertices.Count != expected || interval.OuterSegments.Count != expected) diagnostics.Add("VariableOuterSectionIntervalCorrespondenceMissing");
            if (interval.OuterVertices.Select(x => x.LowerVertexIndex).Distinct().Count() != interval.OuterVertices.Count || interval.OuterVertices.Select(x => x.UpperVertexIndex).Distinct().Count() != interval.OuterVertices.Count || interval.OuterSegments.Select(x => x.LowerSegmentIndex).Distinct().Count() != interval.OuterSegments.Count || interval.OuterSegments.Select(x => x.UpperSegmentIndex).Distinct().Count() != interval.OuterSegments.Count) diagnostics.Add("VariableOuterSectionIntervalCorrespondenceDuplicate");
            foreach (var pair in interval.OuterSegments)
                if (pair.LowerSegmentIndex < 0 || pair.LowerSegmentIndex >= expected || pair.UpperSegmentIndex < 0 || pair.UpperSegmentIndex >= expected || lower.Segments[pair.LowerSegmentIndex].Provenance.StableId != pair.LowerSegmentStableId || upper.Segments[pair.UpperSegmentIndex].Provenance.StableId != pair.UpperSegmentStableId)
                    diagnostics.Add("VariableOuterSectionIntervalCorrespondenceInvalid");
        }
        if (interval.InnerLoops.Select(x => x.FeatureId).Distinct(StringComparer.Ordinal).Count() != interval.InnerLoops.Count) diagnostics.Add("VariableOuterSectionIntervalInnerLoopDuplicate");
        foreach (var inner in interval.InnerLoops)
            if (!Equivalent(inner.LowerLoop, inner.UpperLoop)) diagnostics.Add($"VariableOuterSectionIntervalInnerLoopChanged:{inner.FeatureId}");
        return new(diagnostics.Count == 0, diagnostics.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static bool Equivalent(ResolvedProfile2D lower, ResolvedProfile2D upper)
    {
        if (lower.Loops.Count != 1 || upper.Loops.Count != 1 || lower.Loops[0].Segments.Count != upper.Loops[0].Segments.Count) return false;
        for (var i = 0; i < lower.Loops[0].Segments.Count; i++)
        {
            var a = lower.Loops[0].Segments[i].Geometry; var b = upper.Loops[0].Segments[i].Geometry;
            if (a.GetType() != b.GetType()) return false;
            if (a is LineArcLineSegment2D al && b is LineArcLineSegment2D bl && (!Near(al.Start, bl.Start) || !Near(al.End, bl.End))) return false;
            if (a is LineArcCircularArc2D aa && b is LineArcCircularArc2D ba && (!Near(aa.Center, ba.Center) || Math.Abs(aa.Radius - ba.Radius) > Tol || Math.Abs(aa.StartAngleRadians - ba.StartAngleRadians) > Tol || Math.Abs(aa.SweepAngleRadians - ba.SweepAngleRadians) > Tol)) return false;
            if (a is LineArcFullCircle2D ac && b is LineArcFullCircle2D bc && (!Near(ac.Center, bc.Center) || Math.Abs(ac.Radius - bc.Radius) > Tol)) return false;
        }
        return true;
    }
    private static bool Near((double X, double Y) a, (double X, double Y) b) => Math.Abs(a.X - b.X) <= Tol && Math.Abs(a.Y - b.Y) <= Tol;
}
