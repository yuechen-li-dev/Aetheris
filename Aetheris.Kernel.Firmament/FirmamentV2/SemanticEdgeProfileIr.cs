using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public enum SemanticEdgeAnchorKind { FromStart, FromEnd, CenteredAt }

public sealed record SemanticEdgeAnchorIr(SemanticEdgeAnchorKind Kind, double Offset);

public abstract record SemanticEdgeFragmentIr(
    string Name, string StableId, string Kind, SemanticEdgeAnchorIr Anchor,
    double Span, double Depth, int Side, string SourceSpan);

public sealed record SemanticEdgeTabIr(string Name, string StableId, SemanticEdgeAnchorIr Anchor, double Width, double Extension, int Side, string SourceSpan)
    : SemanticEdgeFragmentIr(Name, StableId, "Tab", Anchor, Width, Extension, Side, SourceSpan);
public sealed record SemanticEdgeNotchIr(string Name, string StableId, SemanticEdgeAnchorIr Anchor, double Width, double Depth, int Side, string SourceSpan)
    : SemanticEdgeFragmentIr(Name, StableId, "Notch", Anchor, Width, Depth, Side, SourceSpan);
public sealed record SemanticEdgeChamferIr(string Name, string StableId, SemanticEdgeAnchorIr Anchor, double Run, double Offset, int Side, string SourceSpan)
    : SemanticEdgeFragmentIr(Name, StableId, "Chamfer", Anchor, Run, Offset, Side, SourceSpan);
public sealed record SemanticEdgeCutbackIr(string Name, string StableId, SemanticEdgeAnchorIr Anchor, double Run, double Offset, int Side, string SourceSpan)
    : SemanticEdgeFragmentIr(Name, StableId, "Cutback", Anchor, Run, Offset, Side, SourceSpan);
public sealed record SemanticEdgeStepIr(string Name, string StableId, SemanticEdgeAnchorIr Anchor, double Width, double Rise, int Side, string SourceSpan)
    : SemanticEdgeFragmentIr(Name, StableId, "Step", Anchor, Width, Rise, Side, SourceSpan);
public sealed record SemanticEdgeSteppedNotchIr(
    string Name, string StableId, SemanticEdgeAnchorIr Anchor, double Width, double Depth,
    double ShoulderDepth, double OuterChamfer, double InnerChamfer, int Side, string SourceSpan)
    : SemanticEdgeFragmentIr(Name, StableId, "SteppedNotch", Anchor, Width, Depth, Side, SourceSpan);

/// <summary>Semantic modification program for one directed, named carrier edge.</summary>
public sealed record SemanticEdgeProfileIr(
    string OwnerPath, string StableId, SemanticProfilePoint Start, SemanticProfilePoint End,
    IReadOnlyList<SemanticEdgeFragmentIr> Fragments, string LocalFrame, string Provenance);

public sealed record ResolvedSemanticEdgeMemberIr(
    string Name, string StableId, string Kind, double StartU, double EndU,
    IReadOnlyList<ResolvedSemanticProfileCurveIr> CurveDescendants, string SourceSpan,
    bool IsGeneratedCarrier);

public sealed record ResolvedSemanticEdgeProfileIr(
    SemanticEdgeProfileIr Source, IReadOnlyList<ResolvedSemanticEdgeMemberIr> OrderedMembers,
    string DeterministicHash, TimeSpan ResolutionTime)
{
    public IReadOnlyList<LineArcProfileCurve2D> ExactReplacementChain =>
        OrderedMembers.SelectMany(member => member.CurveDescendants).Select(curve => curve.Geometry).ToArray();
}

public sealed record SemanticEdgeProfileResolution(ResolvedSemanticEdgeProfileIr? Profile, IReadOnlyList<string> Diagnostics)
{
    public bool IsSuccess => Profile is not null && Diagnostics.Count == 0;
}

/// <summary>
/// Resolves independently anchored fragments in the owner's u/v frame and inserts every
/// untouched carrier span. It is intentionally bounded to baseline-returning fragments.
/// </summary>
public static class SemanticEdgeProfileResolver
{
    private const double Tolerance = 1e-8;

    public static SemanticEdgeProfileResolution Resolve(SemanticEdgeProfileIr source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var clock = Stopwatch.StartNew();
        var diagnostics = new List<string>();
        var dx = source.End.X - source.Start.X; var dy = source.End.Y - source.Start.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (!double.IsFinite(length) || length <= Tolerance)
            return new(null, [$"semantic-edge-owner-degenerate:{source.OwnerPath}"]);
        var tx = dx / length; var ty = dy / length; var nx = -ty; var ny = tx;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var placed = new List<(SemanticEdgeFragmentIr Fragment, double Start, double End)>();
        foreach (var fragment in source.Fragments)
        {
            if (!ids.Add(fragment.StableId)) { diagnostics.Add($"semantic-edge-duplicate-fragment:{fragment.StableId}"); continue; }
            if (!double.IsFinite(fragment.Span) || fragment.Span <= Tolerance || !double.IsFinite(fragment.Depth) || fragment.Depth <= Tolerance || fragment.Side is not (-1 or 1))
            { diagnostics.Add($"semantic-edge-invalid-fragment:{fragment.StableId}:{fragment.Kind}"); continue; }
            if (fragment is SemanticEdgeSteppedNotchIr stepped &&
                (!double.IsFinite(stepped.ShoulderDepth) || stepped.ShoulderDepth < -Tolerance || stepped.ShoulderDepth >= stepped.Depth - Tolerance ||
                 !double.IsFinite(stepped.OuterChamfer) || stepped.OuterChamfer < -Tolerance || !double.IsFinite(stepped.InnerChamfer) || stepped.InnerChamfer <= Tolerance ||
                 2d * (stepped.OuterChamfer + stepped.InnerChamfer) >= stepped.Width - Tolerance || stepped.ShoulderDepth + Tolerance < stepped.OuterChamfer))
            { diagnostics.Add($"semantic-edge-invalid-stepped-notch:{fragment.StableId}"); continue; }
            if (!double.IsFinite(fragment.Anchor.Offset)) { diagnostics.Add($"semantic-edge-invalid-anchor:{fragment.StableId}"); continue; }
            var start = fragment.Anchor.Kind switch
            {
                SemanticEdgeAnchorKind.FromStart => fragment.Anchor.Offset,
                SemanticEdgeAnchorKind.FromEnd => length - fragment.Anchor.Offset - fragment.Span,
                SemanticEdgeAnchorKind.CenteredAt => fragment.Anchor.Offset - fragment.Span / 2d,
                _ => double.NaN
            };
            var end = start + fragment.Span;
            if (start < -Tolerance || end > length + Tolerance)
            { diagnostics.Add($"semantic-edge-fragment-out-of-bounds:{fragment.StableId}:{start:R}:{end:R}:owner={source.OwnerPath}:length={length:R}"); continue; }
            placed.Add((fragment, Math.Max(0, start), Math.Min(length, end)));
        }
        placed.Sort((a, b) => { var c = a.Start.CompareTo(b.Start); return c != 0 ? c : StringComparer.Ordinal.Compare(a.Fragment.StableId, b.Fragment.StableId); });
        for (var i = 1; i < placed.Count; i++)
            if (placed[i].Start < placed[i - 1].End - Tolerance)
                diagnostics.Add($"semantic-edge-fragment-overlap:{source.OwnerPath}:{placed[i - 1].Fragment.StableId}:{placed[i].Fragment.StableId}");
        if (diagnostics.Count > 0) return new(null, diagnostics);

        var result = new List<ResolvedSemanticEdgeMemberIr>(); var cursor = 0d; var carrier = 0;
        foreach (var item in placed)
        {
            if (item.Start > cursor + Tolerance)
                result.Add(Member($"Carrier{carrier++:D2}", $"{source.StableId}.Carrier{carrier - 1:D2}", "Carrier", cursor, item.Start,
                    [Line(cursor, 0, item.Start, 0)], "generated:untouched-carrier", true));
            result.Add(Fragment(item.Fragment, item.Start, item.End));
            cursor = item.End;
        }
        if (cursor < length - Tolerance)
            result.Add(Member($"Carrier{carrier++:D2}", $"{source.StableId}.Carrier{carrier - 1:D2}", "Carrier", cursor, length,
                [Line(cursor, 0, length, 0)], "generated:untouched-carrier", true));
        clock.Stop();
        var hashText = string.Join("|", result.SelectMany(x => x.CurveDescendants).Select(x => $"{x.StableId}:{x.Geometry}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashText))).ToLowerInvariant();
        return new(new(source, result, hash, clock.Elapsed), []);

        ResolvedSemanticEdgeMemberIr Fragment(SemanticEdgeFragmentIr f, double a, double b)
        {
            var v = f.Side * f.Depth;
            IReadOnlyList<LineArcProfileCurve2D> curves = f switch
            {
                SemanticEdgeTabIr or SemanticEdgeNotchIr or SemanticEdgeStepIr =>
                    [Line(a, 0, a, v), Line(a, v, b, v), Line(b, v, b, 0)],
                SemanticEdgeChamferIr or SemanticEdgeCutbackIr =>
                    [Line(a, 0, (a + b) / 2d, v), Line((a + b) / 2d, v, b, 0)],
                SemanticEdgeSteppedNotchIr stepped => SteppedNotch(stepped, a, b),
                _ => []
            };
            return Member(f.Name, f.StableId, f.Kind, a, b, curves, f.SourceSpan, false);
        }
        IReadOnlyList<LineArcProfileCurve2D> SteppedNotch(SemanticEdgeSteppedNotchIr f, double a, double b)
        {
            var side = f.Side; var points = new List<(double U, double V)> { (a, 0) };
            if (f.OuterChamfer > Tolerance) points.Add((a + f.OuterChamfer, side * f.OuterChamfer));
            var shoulderU = a + f.OuterChamfer; points.Add((shoulderU, side * f.ShoulderDepth));
            points.Add((shoulderU + f.InnerChamfer, side * f.Depth));
            points.Add((b - f.OuterChamfer - f.InnerChamfer, side * f.Depth));
            points.Add((b - f.OuterChamfer, side * f.ShoulderDepth));
            if (f.OuterChamfer > Tolerance) points.Add((b - f.OuterChamfer, side * f.OuterChamfer));
            points.Add((b, 0));
            return Enumerable.Range(0, points.Count - 1).Select(i => (LineArcProfileCurve2D)Line(points[i].U, points[i].V, points[i + 1].U, points[i + 1].V)).ToArray();
        }
        ResolvedSemanticEdgeMemberIr Member(string name, string id, string kind, double a, double b, IReadOnlyList<LineArcProfileCurve2D> curves, string span, bool generated) =>
            new(name, id, kind, a, b, curves.Select((curve, ordinal) => new ResolvedSemanticProfileCurveIr($"{id}.curve{ordinal:D2}", ordinal, curve, generated ? $"generated-carrier:{source.OwnerPath}" : $"lowered-from:{id}")).ToArray(), span, generated);
        LineArcLineSegment2D Line(double u0, double v0, double u1, double v1) => new(ToWorld(u0, v0), ToWorld(u1, v1));
        (double X, double Y) ToWorld(double u, double v) => (source.Start.X + tx * u + nx * v, source.Start.Y + ty * u + ny * v);
    }
}
