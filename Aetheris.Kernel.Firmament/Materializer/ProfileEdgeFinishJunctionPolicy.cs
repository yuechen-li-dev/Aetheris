using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Judgment;
using System.Text.Json.Serialization;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Source-proven construction choices, before any topology is emitted.
/// Judgment chooses a materializer; it cannot waive a geometric requirement.</summary>
public sealed record ProfileEdgeFinishJunctionDecision(
    JudgmentModelResult<string> Judgment,
    [property: JsonIgnore] CircularSegmentEdgeFinishPlan? CircularSegment)
{
    public string? Strategy => Judgment.Winner?.Value;
}

public sealed record ProfileEdgeFinishEvidence(ProfileEdgeFinishJunctionDecision Decision, double MaximumCurveDeviationMm);

public sealed record CircularSegmentEdgeFinishPlan(
    int ArcIndex, int LineIndex, double Radius, double ChordDistance,
    (double X, double Y) Center, (double X, double Y) Tangent, (double X, double Y) Inward,
    double WindingSign);

public static class ProfileEdgeFinishJunctionPolicy
{
    private sealed record Context(bool OrdinaryContacts, bool CircularSegment, bool RegularMiter, string Reason);

    public static ProfileEdgeFinishJunctionDecision Evaluate(ResolvedProfile2D profile,
        ResolvedProfileLoop2D loop, double size)
    {
        var junctions = ProfileJunctionClassifier.Classify(profile, loop);
        var circular = CircularSegment(loop);
        // Existing line-line junctions retain their established materializers.
        // A curved junction may use a common meridian only when actually tangent.
        var ordinary = junctions.All(j =>
            j.Classification == ProfileJunctionKind.Collinear ||
            loop.Segments.Single(s => s.Name == j.PredecessorSegmentId).Geometry is LineArcLineSegment2D &&
            loop.Segments.Single(s => s.Name == j.SuccessorSegmentId).Geometry is LineArcLineSegment2D);
        var regular = circular is not null && double.IsFinite(size) && size > 0 &&
            size < (circular.Radius - circular.ChordDistance) / 2d - 1e-8;
        var context = new Context(ordinary, circular is not null, regular,
            circular is null ? "Sharp line/arc junction needs a qualified shared intersection curve; the tangent-contact strategy cannot construct it."
                : $"Finish must be smaller than the circular segment inradius {(circular.Radius - circular.ChordDistance) / 2d:R}mm; equality collapses the cap.");
        var model = new JudgmentModel<string, Context>("ProfileEdgeFinish.Junction.v1",
            [new("construction-supported", (strategy, c) => new(
                strategy == "SourceContactShell" ? c.OrdinaryContacts : c.CircularSegment,
                "ProfileEdgeFinishJunctionUnsupported", 0, 0,
                strategy == "SourceContactShell" ? "Requires tangent curved joins or existing sharp line-line joins." : c.Reason)),
             new("noncollapsing-miter", (strategy, c) => new(strategy != "CircularSegmentMiter" || c.RegularMiter,
                "ProfileEdgeFinishInsetCollapse", 0, 0, c.Reason))],
            [new("source-construction-fidelity", 1, (_, _) => new(1, 1, "exact-supports", "Retain authored supports, source order and shared topology."))]);
        return new(model.Evaluate(context, [new("SourceContactShell", "SourceContactShell"),
            new("CircularSegmentMiter", "CircularSegmentMiter")]), circular);
    }

    private static CircularSegmentEdgeFinishPlan? CircularSegment(ResolvedProfileLoop2D loop)
    {
        if (loop.Segments.Count != 2) return null;
        var arcIndex = loop.Segments[0].Geometry is LineArcCircularArc2D ? 0 : 1;
        var lineIndex = 1 - arcIndex;
        if (loop.Segments[arcIndex].Geometry is not LineArcCircularArc2D arc ||
            loop.Segments[lineIndex].Geometry is not LineArcLineSegment2D line) return null;
        var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 1e-8 || !double.IsFinite(arc.Radius) || arc.Radius <= 0) return null;
        var winding = Math.Sign(arc.SweepAngleRadians);
        var tangent = (X: dx / length, Y: dy / length);
        var inward = (X: -tangent.Y * winding, Y: tangent.X * winding);
        var chord = (line.Start.X - arc.Center.X) * inward.X + (line.Start.Y - arc.Center.Y) * inward.Y;
        var angle = arc.StartAngleRadians + arc.SweepAngleRadians / 2d;
        var midDistance = arc.Radius * (Math.Cos(angle) * inward.X + Math.Sin(angle) * inward.Y);
        // The named arc must bound the circle/half-plane intersection, not its complement.
        if (Math.Abs(chord) >= arc.Radius - 1e-8 || midDistance <= chord + 1e-8) return null;
        var start = (X: arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians),
            Y: arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians));
        var end = (X: arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians),
            Y: arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians));
        if (Distance(start, line.End) > 1e-7 || Distance(end, line.Start) > 1e-7) return null;
        return new(arcIndex, lineIndex, arc.Radius, chord, arc.Center, tangent, inward, winding);
    }

    private static double Distance((double X, double Y) a, (double X, double Y) b)
        => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
