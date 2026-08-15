using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

/// <summary>
/// Semantic profile authority above exact curve segmentation. Members are stable engineering
/// identities; their generated curve descendants are deliberately allowed to be one-to-many.
/// This is a bounded resolver, not a general geometric constraint solver.
/// </summary>
public sealed record SemanticProfileIr(
    string Name,
    string StableId,
    string LocalFrame,
    SemanticProfilePoint Start,
    double InitialHeadingDegrees,
    IReadOnlyList<SemanticProfileMemberIr> Members,
    IReadOnlyList<SemanticProfileConstraintIr> Constraints,
    IReadOnlyList<SemanticProfileDatumIr> Datums,
    string Provenance);

public readonly record struct SemanticProfilePoint(double X, double Y);
public sealed record SemanticProfileDatumIr(string Name, string StableId, SemanticProfilePoint Point, string Provenance);
public sealed record SemanticProfileConstraintIr(string Name, string StableId, string Kind, IReadOnlyList<string> Members, string Provenance);

public abstract record SemanticProfileMemberIr(string Name, string StableId, string Kind, string SourceSpan);
public sealed record SemanticProfileSpanIr(string Name, string StableId, double Length, double? TurnDegrees, double? HeadingDegrees, SemanticProfilePoint? Target, string SourceSpan)
    : SemanticProfileMemberIr(Name, StableId, "Span", SourceSpan);
public sealed record SemanticProfileArcTransitionIr(string Name, string StableId, double Radius, double TurnDegrees, string SourceSpan)
    : SemanticProfileMemberIr(Name, StableId, "ArcTransition", SourceSpan);
public sealed record SemanticProfileChamferIr(string Name, string StableId, double Run, double Offset, int Side, string SourceSpan)
    : SemanticProfileMemberIr(Name, StableId, "Chamfer", SourceSpan);
public sealed record SemanticProfileStepIr(string Name, string StableId, double Run, double Rise, int Side, string SourceSpan)
    : SemanticProfileMemberIr(Name, StableId, "Step", SourceSpan);
public sealed record SemanticProfileNotchIr(string Name, string StableId, double Width, double Depth, int Side, string SourceSpan)
    : SemanticProfileMemberIr(Name, StableId, "Notch", SourceSpan);
public sealed record SemanticProfileCutbackIr(string Name, string StableId, double Run, double Offset, int Side, string SourceSpan)
    : SemanticProfileMemberIr(Name, StableId, "Cutback", SourceSpan);
public sealed record SemanticProfileTabIr(string Name, string StableId, double Width, double Extension, int Side, string SourceSpan)
    : SemanticProfileMemberIr(Name, StableId, "Tab", SourceSpan);
public sealed record SemanticProfileCloseIr(string Name, string StableId, string SourceSpan)
    : SemanticProfileMemberIr(Name, StableId, "Close", SourceSpan);

public sealed record ResolvedSemanticProfileCurveIr(string StableId, int Ordinal, LineArcProfileCurve2D Geometry, string Provenance);
public sealed record ResolvedSemanticProfileMemberIr(
    string Name,
    string StableId,
    string Kind,
    SemanticProfilePoint Start,
    SemanticProfilePoint End,
    IReadOnlyList<ResolvedSemanticProfileCurveIr> CurveDescendants,
    string SourceSpan);
public sealed record ResolvedSemanticProfileIr(
    SemanticProfileIr Source,
    IReadOnlyList<ResolvedSemanticProfileMemberIr> Members,
    IReadOnlyDictionary<string, SemanticProfilePoint> Landmarks,
    string DeterministicHash,
    TimeSpan ResolutionTime)
{
    public IReadOnlyList<LineArcProfileCurve2D> ExactCurveChain => Members.SelectMany(member => member.CurveDescendants).Select(curve => curve.Geometry).ToArray();

    /// <summary>The explicit semantic-to-exact boundary. PlanarContour2 remains topology authority after this call.</summary>
    public ResolvedProfile2D LowerToResolvedProfile(ConstructionPlane? constructionPlane = null, double? localStartDepth = null, double? localEndDepth = null)
    {
        var segments = Members.SelectMany(member => member.CurveDescendants.Select(curve => new ResolvedProfileSegment2D(
            curve.StableId, curve.Geometry, new(curve.StableId, member.StableId, member.SourceSpan, curve.Provenance, Source.LocalFrame)))).ToArray();
        return new(Source.Name, Source.LocalFrame, [new("Outer", true, segments)], constructionPlane, localStartDepth, localEndDepth);
    }

    public PlanarContour2 LowerToPlanarContour2(string? provenance = null) =>
        PlanarContourKernel.FromResolvedProfile(LowerToResolvedProfile(), provenance ?? $"SemanticProfileMIR:{Source.StableId}:{DeterministicHash}");
}

public sealed record SemanticProfileResolution(ResolvedSemanticProfileIr? Profile, IReadOnlyList<string> Diagnostics)
{
    public bool IsSuccess => Profile is not null && Diagnostics.Count == 0;
}

public static class SemanticProfileMirResolver
{
    private const double Tolerance = 1e-9;

    public static SemanticProfileResolution Resolve(SemanticProfileIr profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var clock = Stopwatch.StartNew();
        var diagnostics = new List<string>();
        var resolved = new List<ResolvedSemanticProfileMemberIr>();
        var landmarks = new Dictionary<string, SemanticProfilePoint>(StringComparer.Ordinal)
        {
            [profile.Name + ".Start"] = profile.Start,
            [profile.StableId + ".Start"] = profile.Start
        };
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var current = profile.Start;
        var heading = profile.InitialHeadingDegrees;

        foreach (var member in profile.Members)
        {
            if (!identities.Add(member.StableId))
            {
                diagnostics.Add($"semantic-profile-duplicate-member:{member.StableId}");
                continue;
            }

            var start = current;
            var curves = new List<LineArcProfileCurve2D>();
            void AddLine(SemanticProfilePoint end) { curves.Add(new LineArcLineSegment2D(ToTuple(current), ToTuple(end))); current = end; }
            void AddBoxExcursion(double width, double depth, int side)
            {
                AddLine(AdvanceLocal(current, heading, 0d, side * depth));
                AddLine(Advance(current, heading, width));
                AddLine(AdvanceLocal(current, heading, 0d, -side * depth));
            }
            switch (member)
            {
                case SemanticProfileSpanIr span:
                    if (span.TurnDegrees is not null && span.HeadingDegrees is not null)
                    { diagnostics.Add($"semantic-profile-span-direction-conflict:{member.StableId}"); continue; }
                    if (span.Target is { } target)
                    {
                        if (Distance(current, target) <= Tolerance) { diagnostics.Add($"semantic-profile-zero-length:{member.StableId}"); continue; }
                        curves.Add(new LineArcLineSegment2D(ToTuple(current), ToTuple(target)));
                        current = target;
                        heading = Heading(start, current);
                    }
                    else
                    {
                        if (!Positive(span.Length)) { diagnostics.Add($"semantic-profile-invalid-span:{member.StableId}:Length must be positive"); continue; }
                        heading = span.HeadingDegrees ?? (heading + (span.TurnDegrees ?? 0d));
                        current = Advance(current, heading, span.Length);
                        curves.Add(new LineArcLineSegment2D(ToTuple(start), ToTuple(current)));
                    }
                    break;
                case SemanticProfileArcTransitionIr arc:
                    if (!Positive(arc.Radius) || !FiniteNonZero(arc.TurnDegrees) || Math.Abs(arc.TurnDegrees) >= 360d - Tolerance)
                    { diagnostics.Add($"semantic-profile-invalid-arc-transition:{member.StableId}"); continue; }
                    var sweep = Degrees(arc.TurnDegrees); var tangent = Degrees(heading); var sign = Math.Sign(sweep);
                    var center = new SemanticProfilePoint(current.X - sign * arc.Radius * Math.Sin(tangent), current.Y + sign * arc.Radius * Math.Cos(tangent));
                    var startAngle = Math.Atan2(current.Y - center.Y, current.X - center.X);
                    curves.Add(new LineArcCircularArc2D(ToTuple(center), arc.Radius, startAngle, sweep));
                    current = new(center.X + arc.Radius * Math.Cos(startAngle + sweep), center.Y + arc.Radius * Math.Sin(startAngle + sweep));
                    heading += arc.TurnDegrees;
                    break;
                case SemanticProfileChamferIr chamfer:
                    if (!Positive(chamfer.Run) || !Positive(chamfer.Offset) || !ValidSide(chamfer.Side)) { Invalid(member, "Run and Offset must be positive and Side must be Left or Right"); continue; }
                    current = AdvanceLocal(current, heading, chamfer.Run, chamfer.Side * chamfer.Offset);
                    curves.Add(new LineArcLineSegment2D(ToTuple(start), ToTuple(current)));
                    break;
                case SemanticProfileCutbackIr cutback:
                    if (!Positive(cutback.Run) || !Positive(cutback.Offset) || !ValidSide(cutback.Side)) { Invalid(member, "Run and Offset must be positive and Side must be Left or Right"); continue; }
                    current = AdvanceLocal(current, heading, cutback.Run, cutback.Side * cutback.Offset);
                    curves.Add(new LineArcLineSegment2D(ToTuple(start), ToTuple(current)));
                    break;
                case SemanticProfileStepIr step:
                    if (!Positive(step.Run) || !Positive(step.Rise) || !ValidSide(step.Side)) { Invalid(member, "Run and Rise must be positive and Side must be Left or Right"); continue; }
                    AddLine(Advance(current, heading, step.Run));
                    AddLine(AdvanceLocal(current, heading, 0d, step.Side * step.Rise));
                    break;
                case SemanticProfileNotchIr notch:
                    if (!Positive(notch.Width) || !Positive(notch.Depth) || !ValidSide(notch.Side)) { Invalid(member, "Width and Depth must be positive and Side must be Left or Right"); continue; }
                    AddBoxExcursion(notch.Width, notch.Depth, notch.Side);
                    break;
                case SemanticProfileTabIr tab:
                    if (!Positive(tab.Width) || !Positive(tab.Extension) || !ValidSide(tab.Side)) { Invalid(member, "Width and Extension must be positive and Side must be Left or Right"); continue; }
                    AddBoxExcursion(tab.Width, tab.Extension, tab.Side);
                    break;
                case SemanticProfileCloseIr:
                    if (Distance(current, profile.Start) <= Tolerance) { diagnostics.Add($"semantic-profile-zero-length:{member.StableId}"); continue; }
                    current = profile.Start;
                    curves.Add(new LineArcLineSegment2D(ToTuple(start), ToTuple(current)));
                    break;
                default:
                    diagnostics.Add($"semantic-profile-unsupported-member:{member.StableId}:{member.Kind}");
                    continue;
            }

            var descendants = curves.Select((geometry, ordinal) => new ResolvedSemanticProfileCurveIr(
                $"{member.StableId}.curve{ordinal:D2}", ordinal, geometry, $"lowered-from:{member.StableId}")).ToArray();
            resolved.Add(new(member.Name, member.StableId, member.Kind, start, current, descendants, member.SourceSpan));
            landmarks[$"{profile.Name}.{member.Name}.Start"] = start;
            landmarks[$"{profile.Name}.{member.Name}.End"] = current;
        }

        foreach (var constraint in profile.Constraints)
        {
            var selected = constraint.Members.Select(id => profile.Members.FirstOrDefault(member => member.StableId == id || member.Name == id || member.StableId == profile.StableId + "." + id)).ToArray();
            var missing = constraint.Members.Where((_, index) => selected[index] is null).ToArray();
            if (missing.Length > 0) diagnostics.Add($"semantic-profile-constraint-missing-member:{constraint.StableId}:{string.Join(',', missing)}");
            else if (constraint.Kind.Equals("RequiredMembers", StringComparison.OrdinalIgnoreCase)) { }
            else if (constraint.Kind.Equals("EqualSize", StringComparison.OrdinalIgnoreCase))
            {
                var signatures = selected.Select(member => SizeSignature(member!)).Distinct(StringComparer.Ordinal).ToArray();
                if (signatures.Length != 1) diagnostics.Add($"semantic-profile-equal-size-mismatch:{constraint.StableId}:{string.Join(',', constraint.Members)}");
            }
            else if (constraint.Kind.Equals("Mirror", StringComparison.OrdinalIgnoreCase))
            {
                if (selected.Length != 2 || SizeSignature(selected[0]!) != SizeSignature(selected[1]!) || Side(selected[0]!) != -Side(selected[1]!))
                    diagnostics.Add($"semantic-profile-mirror-mismatch:{constraint.StableId}:{string.Join(',', constraint.Members)}");
            }
            else diagnostics.Add($"semantic-profile-constraint-unsupported:{constraint.StableId}:{constraint.Kind}");
        }

        clock.Stop();
        if (diagnostics.Count > 0) return new(null, diagnostics);
        var hashText = string.Join("|", resolved.SelectMany(member => member.CurveDescendants).Select(curve => $"{curve.StableId}:{curve.Geometry}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashText))).ToLowerInvariant();
        return new(new(profile, resolved, landmarks, hash, clock.Elapsed), diagnostics);

        void Invalid(SemanticProfileMemberIr member, string reason) => diagnostics.Add($"semantic-profile-invalid-{member.Kind.ToLowerInvariant()}:{member.StableId}:{reason}");
    }

    private static bool Positive(double value) => double.IsFinite(value) && value > Tolerance;
    private static bool FiniteNonZero(double value) => double.IsFinite(value) && Math.Abs(value) > Tolerance;
    private static bool ValidSide(int value) => value is -1 or 1;
    private static int Side(SemanticProfileMemberIr member) => member switch
    {
        SemanticProfileChamferIr value => value.Side,
        SemanticProfileStepIr value => value.Side,
        SemanticProfileNotchIr value => value.Side,
        SemanticProfileCutbackIr value => value.Side,
        SemanticProfileTabIr value => value.Side,
        _ => 0
    };
    private static string SizeSignature(SemanticProfileMemberIr member) => member switch
    {
        SemanticProfileChamferIr value => $"Chamfer:{value.Run:R}:{value.Offset:R}",
        SemanticProfileStepIr value => $"Step:{value.Run:R}:{value.Rise:R}",
        SemanticProfileNotchIr value => $"Notch:{value.Width:R}:{value.Depth:R}",
        SemanticProfileCutbackIr value => $"Cutback:{value.Run:R}:{value.Offset:R}",
        SemanticProfileTabIr value => $"Tab:{value.Width:R}:{value.Extension:R}",
        SemanticProfileSpanIr value => $"Span:{value.Length:R}",
        SemanticProfileArcTransitionIr value => $"ArcTransition:{value.Radius:R}:{Math.Abs(value.TurnDegrees):R}",
        _ => member.Kind
    };
    private static double Degrees(double value) => value * Math.PI / 180d;
    private static (double X, double Y) ToTuple(SemanticProfilePoint point) => (point.X, point.Y);
    private static double Distance(SemanticProfilePoint a, SemanticProfilePoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static double Heading(SemanticProfilePoint a, SemanticProfilePoint b) => Math.Atan2(b.Y - a.Y, b.X - a.X) * 180d / Math.PI;
    private static SemanticProfilePoint Advance(SemanticProfilePoint point, double headingDegrees, double length)
    {
        var radians = Degrees(headingDegrees);
        return new(point.X + length * Math.Cos(radians), point.Y + length * Math.Sin(radians));
    }
    private static SemanticProfilePoint AdvanceLocal(SemanticProfilePoint point, double headingDegrees, double run, double normal)
    {
        var radians = Degrees(headingDegrees);
        return new(point.X + run * Math.Cos(radians) - normal * Math.Sin(radians), point.Y + run * Math.Sin(radians) + normal * Math.Cos(radians));
    }
}
