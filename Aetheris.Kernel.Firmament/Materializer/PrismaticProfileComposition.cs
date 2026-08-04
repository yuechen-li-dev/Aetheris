using System.Globalization;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Source-owned intent for a single material body whose cross section changes only
/// at declared axial levels.  This is deliberately not a list of Boolean solids.
/// </summary>
public enum PrismaticProfileIntent { Base, Add, Remove }
public sealed record PrismaticProfileOperation(
    string Name, PrismaticProfileIntent Intent, string ProfileReference, double From, double To,
    string SemanticRole, string SourceSpan);
public sealed record PrismaticProfilePlacement(
    string Name, double AnchorX, double AnchorY, double AnchorZ,
    string ProfilePlane, string Axis, string ReferenceDirection, bool IsExplicit);
public sealed record PrismaticProfileCompositionFeature(
    string Name, string Frame, string Axis, PrismaticProfilePlacement Placement, IReadOnlyList<PrismaticProfileOperation> Operations,
    IReadOnlyList<double> CriticalLevels, string Provenance);
public sealed record PrismaticSectionRegion(
    ResolvedProfile2D Outer, IReadOnlyList<ResolvedProfile2D> Holes, IReadOnlyList<string> Provenance);
public sealed record PrismaticSectionSlab(
    double From, double To, PrismaticSectionRegion Region, IReadOnlyList<string> ActiveOperations,
    ProfileArrangement2D? Arrangement = null);
public sealed record PrismaticSectionTransition(
    double Level, IReadOnlyList<PrismaticSectionRegion> UpwardRegions, IReadOnlyList<PrismaticSectionRegion> DownwardRegions);
public sealed record PrismaticSectionStackConstruction(
    PrismaticProfileCompositionFeature Feature, IReadOnlyList<PrismaticSectionSlab> Slabs,
    IReadOnlyList<PrismaticSectionTransition> Transitions, double AnalyticVolume, IReadOnlyList<string> Diagnostics);
public sealed record PrismaticProfileCompositionParseResult(
    PrismaticProfileCompositionFeature? Feature, IReadOnlyDictionary<string, ResolvedProfile2D> Profiles, IReadOnlyList<string> Diagnostics);

/// <summary>
/// Bounded parser for the composition source form.  It intentionally reuses the
/// same Point2/Line2/Circle2/Profile vocabulary as the first profile route.
/// </summary>
public static class PrismaticProfileCompositionParser
{
    private static readonly Regex Point = new(@"\bPoint2\s+(?<n>\w+)\s*\{\s*Position\s*:\s*\[(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\]", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Line = new(@"\bLine2\s+(?<n>\w+)\s*\{\s*From\s*:\s*(?<a>\w+)\s*;?\s*To\s*:\s*(?<b>\w+)", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Circle = new(@"\bCircle2\s+(?<n>\w+)\s*\{\s*Center\s*:\s*(?<c>\w+)\s*;?\s*Radius\s*:\s*(?<r>[-+.\d]+)mm", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Rect = new(@"\bRect2\s+(?<n>\w+)\s*\{\s*Center\s*:\s*\[(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\]\s*;?\s*Size\s*:\s*\[(?<w>[-+.\d]+)mm\s*,\s*(?<h>[-+.\d]+)mm\]", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex ProfileHead = new(@"\bProfile\s+(?<n>\w+)\s+Using\s+(?<layout>\w+)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex Segment = new(@"\bSegment\s+(?<n>\w+)\s*\{\s*Trace\s*:\s*(?<trace>[\w.]+)\s*;?\s*From\s*:\s*(?<from>[\w.]+)\s*;?\s*To\s*:\s*(?<to>[\w.]+)(?:\s*;?\s*Sweep\s*:\s*(?<sweep>Clockwise|CounterClockwise))?", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex ComposeHead = new(@"\bCompose\s+(?<n>\w+)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex Placement = new(@"\bPlacement\s+(?<n>\w+)\s*\{\s*Anchor\s*:\s*\[(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\s*,\s*(?<z>[-+.\d]+)mm\]\s*;?\s*ProfilePlane\s*:\s*(?<plane>\w+)\s*;?\s*Axis\s*:\s*(?<axis>[+-][XYZ])\s*;?\s*ReferenceDirection\s*:\s*(?<reference>[+-][XYZ])", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Operation = new(@"\b(?<intent>Base|Add|Remove)\s+(?<n>\w+)\s*\{\s*Profile\s*:\s*(?<profile>\w+)\s*;?\s*From\s*:\s*(?<from>[-+.\d]+)mm\s*;?\s*To\s*:\s*(?<to>[-+.\d]+)mm(?:\s*;?\s*Role\s*:\s*(?<role>\w+))?", RegexOptions.Singleline | RegexOptions.CultureInvariant);

    public static bool IsCompositionSource(string source) => ComposeHead.IsMatch(source);

    public static PrismaticProfileCompositionParseResult Parse(string source)
    {
        var diagnostics = new List<string>();
        var points = Point.Matches(source).ToDictionary(m => m.Groups["n"].Value, m => (X: N(m, "x"), Y: N(m, "y")), StringComparer.Ordinal);
        foreach (Match match in Rect.Matches(source))
        {
            var width=N(match,"w"); var height=N(match,"h"); var x=N(match,"x"); var y=N(match,"y"); var name=match.Groups["n"].Value;
            if (!double.IsFinite(width) || !double.IsFinite(height) || width<=0 || height<=0) { diagnostics.Add($"rect2-invalid-size:{name}"); continue; }
            points[$"{name}.BottomLeft"]=(x-width/2,y-height/2); points[$"{name}.BottomRight"]=(x+width/2,y-height/2); points[$"{name}.TopRight"]=(x+width/2,y+height/2); points[$"{name}.TopLeft"]=(x-width/2,y+height/2);
        }
        var lines = new Dictionary<string, LineArcLineSegment2D>(StringComparer.Ordinal);
        foreach (Match match in Line.Matches(source))
        {
            if (!points.TryGetValue(match.Groups["a"].Value, out var a) || !points.TryGetValue(match.Groups["b"].Value, out var b))
                diagnostics.Add($"compose-layout-unresolved-line:{match.Groups["n"].Value}");
            else lines[match.Groups["n"].Value] = new(a, b);
        }
        foreach (Match match in Rect.Matches(source))
        {
            var n=match.Groups["n"].Value;
            if (points.TryGetValue($"{n}.BottomLeft",out var bl) && points.TryGetValue($"{n}.BottomRight",out var br) && points.TryGetValue($"{n}.TopRight",out var tr) && points.TryGetValue($"{n}.TopLeft",out var tl))
            { lines[$"{n}.Bottom"]=new(bl,br); lines[$"{n}.Right"]=new(br,tr); lines[$"{n}.Top"]=new(tr,tl); lines[$"{n}.Left"]=new(tl,bl); }
        }
        var circles = new Dictionary<string, ((double X, double Y) Center, double Radius)>(StringComparer.Ordinal);
        foreach (Match match in Circle.Matches(source))
        {
            if (!points.TryGetValue(match.Groups["c"].Value, out var center)) diagnostics.Add($"compose-layout-unresolved-circle:{match.Groups["n"].Value}");
            else circles[match.Groups["n"].Value] = (center, N(match, "r"));
        }

        var profiles = new Dictionary<string, ResolvedProfile2D>(StringComparer.Ordinal);
        foreach (Match header in ProfileHead.Matches(source))
        {
            var body = Block(source, header.Index + header.Length - 1);
            if (body is null) { diagnostics.Add($"compose-profile-unclosed:{header.Groups["n"].Value}"); continue; }
            var segments = new List<ResolvedProfileSegment2D>();
            foreach (Match match in Segment.Matches(body))
            {
                var name = match.Groups["n"].Value;
                if (!points.TryGetValue(match.Groups["from"].Value, out var from) || !points.TryGetValue(match.Groups["to"].Value, out var to)) { diagnostics.Add($"compose-profile-segment-unresolved:{name}"); continue; }
                LineArcProfileCurve2D? geometry = null;
                var trace = match.Groups["trace"].Value;
                if (lines.ContainsKey(trace)) geometry = new LineArcLineSegment2D(from, to);
                else if (circles.TryGetValue(trace, out var circle) && match.Groups["sweep"].Success)
                {
                    var start = Math.Atan2(from.Y - circle.Center.Y, from.X - circle.Center.X);
                    var sweep = Math.Atan2(to.Y - circle.Center.Y, to.X - circle.Center.X) - start;
                    if (match.Groups["sweep"].Value == "CounterClockwise") while (sweep <= 0) sweep += 2 * Math.PI;
                    else while (sweep >= 0) sweep -= 2 * Math.PI;
                    geometry = new LineArcCircularArc2D(circle.Center, circle.Radius, start, sweep);
                }
                else diagnostics.Add($"compose-profile-guide-missing-or-arc-sweep:{header.Groups["n"].Value}.{name}");
                if (geometry is not null) segments.Add(new(name, geometry, new($"profile:{header.Groups["n"].Value}.Outer.{name}", $"concept:{header.Groups["layout"].Value}.{trace}", name, $"Trace({trace})", "XY")));
            }
            var profile = new ResolvedProfile2D(header.Groups["n"].Value, "XY", [new ResolvedProfileLoop2D("Outer", true, segments)]);
            var validation = ResolvedProfile2DValidator.Validate(profile);
            diagnostics.AddRange(validation.Diagnostics);
            if (validation.IsValid) profiles.Add(profile.Name, profile);
        }

        var compose = ComposeHead.Match(source);
        if (!compose.Success) return new(null, profiles, diagnostics.Append("compose-source-missing-compose").ToArray());
        var composeBody = Block(source, compose.Index + compose.Length - 1);
        if (composeBody is null) return new(null, profiles, diagnostics.Append("compose-source-unclosed-compose").ToArray());
        var placementMatch = Placement.Match(composeBody);
        var placement = new PrismaticProfilePlacement("LegacyImplicitWorldXY", 0d, 0d, 0d, "XY", "+Z", "+X", false);
        if (placementMatch.Success)
        {
            placement = new(placementMatch.Groups["n"].Value, N(placementMatch, "x"), N(placementMatch, "y"), N(placementMatch, "z"), placementMatch.Groups["plane"].Value, placementMatch.Groups["axis"].Value, placementMatch.Groups["reference"].Value, true);
            if (placement.ProfilePlane != "XY" || placement.Axis != "+Z" || placement.ReferenceDirection != "+X")
                diagnostics.Add($"compose-placement-unsupported-orientation:{placement.Name}:plane={placement.ProfilePlane}:axis={placement.Axis}:reference={placement.ReferenceDirection}");
            if (Math.Abs(placement.AnchorX) > 1e-12d || Math.Abs(placement.AnchorY) > 1e-12d || Math.Abs(placement.AnchorZ) > 1e-12d)
                diagnostics.Add($"compose-placement-unsupported-nonzero-anchor:{placement.Name}:[{placement.AnchorX:R},{placement.AnchorY:R},{placement.AnchorZ:R}]");
        }
        var operations = new List<PrismaticProfileOperation>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Operation.Matches(composeBody))
        {
            var name = match.Groups["n"].Value;
            var profile = match.Groups["profile"].Value;
            var from = N(match, "from"); var to = N(match, "to");
            if (!names.Add(name)) diagnostics.Add($"compose-duplicate-operation:{name}");
            if (!profiles.ContainsKey(profile)) diagnostics.Add($"compose-operation-unresolved-profile:{name}:{profile}");
            if (!double.IsFinite(from) || !double.IsFinite(to) || from >= to) diagnostics.Add($"compose-invalid-interval:{name}:{from:R}:{to:R}");
            operations.Add(new(name, Enum.Parse<PrismaticProfileIntent>(match.Groups["intent"].Value), profile, from, to, match.Groups["role"].Success ? match.Groups["role"].Value : match.Groups["intent"].Value, $"offset:{match.Index}"));
        }
        if (operations.Count(o => o.Intent == PrismaticProfileIntent.Base) != 1) diagnostics.Add("compose-requires-exactly-one-base-operation");
        var levels = operations.SelectMany(o => new[] { o.From, o.To }).Distinct().Order().ToArray();
        var feature = diagnostics.Count == 0 ? new PrismaticProfileCompositionFeature(compose.Groups["n"].Value, "XY", "+Z", placement, operations, levels, "parser-backed-scaffold-profile-composition") : null;
        return new(feature, profiles, diagnostics.Distinct().ToArray());
    }

    private static double N(Match match, string name) => double.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture);
    private static string? Block(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return text[(open + 1)..i];
        }
        return null;
    }
}
