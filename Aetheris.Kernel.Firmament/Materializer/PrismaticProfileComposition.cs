using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Source-owned intent for a single material body whose cross section changes only
/// at declared axial levels.  This is deliberately not a list of Boolean solids.
/// </summary>
public enum PrismaticProfileIntent { Base, Add, Remove }
public sealed record PrismaticProfileOperation(
    string Name, PrismaticProfileIntent Intent, string ProfileReference, double From, double To,
    string SemanticRole, string SourceSpan, string? SemanticFeatureId = null, string? SemanticFeatureKind = null,
    double? Diameter = null);
/// <summary>First-class connected material addition lowered through the section-stack Add operation.</summary>
public sealed record PrismaticBossFeature(
    string Name, string StableId, string Host, string SupportFace, string ProfileReference,
    double Height, double From, double To, string SourceSpan);
/// <summary>First-class finite material removal lowered through the section-stack Remove operation.</summary>
public sealed record PrismaticPocketFeature(
    string Name, string StableId, string Host, string SupportFace, string ProfileReference,
    double Depth, double HostThickness, double RemainingFloor, double MinimumFloorThickness,
    string MinimumFloorPolicySource, double From, double To, string SourceSpan);
public sealed record PrismaticShaftHoleFeature(
    string Name, string StableId, string ProfileReference, double CenterX, double CenterY, double Diameter,
    double From, double To, string SemanticRole, string SourceSpan);
/// <summary>
/// A bounded stepped through cavity in an admitted +Z prismatic compose host.
/// The two profile references are planner-owned section cuts, not Boolean tools.
/// </summary>
public sealed record PrismaticCounterboreHoleFeature(
    string Name, string StableId, string ShaftProfileReference, string CounterboreProfileReference,
    double CenterX, double CenterY, double Diameter, double CounterboreDiameter, double CounterboreDepth,
    double From, double To, string SemanticRole, string SourceSpan);
/// <summary>Source-owned transverse blind drill; its cavity is planned after host construction.</summary>
public sealed record PrismaticConstructionPlaneBlindDrillFeature(
    string Name, string StableId, string ConstructionPlaneId, string SourceConceptPlaneId,
    Point3D FrameOrigin, Direction3D AxisX, Direction3D AxisY, Direction3D AxisZ,
    double LocalCenterX, double LocalCenterY, double Diameter, double DeclaredDepth,
    bool DeclaredDepthIsTotal, double PointAngleDegrees, string SemanticRole, string SourceSpan,
    string Provenance);
/// <summary>Compiler-owned capsule contract; the generated Profile remains lowering detail.</summary>
public sealed record PrismaticCapsuleSlotFeature(
    string Name, string StableId, string ProfileReference, double CenterX, double CenterY,
    double DirectionX, double DirectionY, double Length, double Width, double From, double To,
    string Extent, string SemanticRole, string SourceSpan)
{
    public double Radius => Width / 2d;
    public double StraightSpan => Length - Width;
}
/// <summary>Compiler-owned rounded-rectangle slot contract; corner radii are explicit authored semantics.</summary>
public sealed record PrismaticRoundedRectangleSlotFeature(
    string Name, string StableId, string ProfileReference, double CenterX, double CenterY,
    double DirectionX, double DirectionY, double Length, double Width, double CornerRadius, double From, double To,
    string Extent, string SemanticRole, string SourceSpan);
public sealed record PrismaticProfilePlacement(
    string Name, double AnchorX, double AnchorY, double AnchorZ,
    string ProfilePlane, string Axis, string ReferenceDirection, bool IsExplicit);
public sealed record PrismaticProfileCompositionFeature(
    string Name, string Frame, string Axis, PrismaticProfilePlacement Placement, IReadOnlyList<PrismaticProfileOperation> Operations,
    IReadOnlyList<double> CriticalLevels, string Provenance, IReadOnlyList<PrismaticShaftHoleFeature>? ShaftHoles = null,
    IReadOnlyList<PrismaticCapsuleSlotFeature>? CapsuleSlots = null,
    IReadOnlyList<PrismaticRoundedRectangleSlotFeature>? RoundedRectangleSlots = null,
    IReadOnlyList<PrismaticConstructionPlaneBlindDrillFeature>? ConstructionPlaneBlindDrills = null,
    IReadOnlyList<PrismaticCounterboreHoleFeature>? CounterboreHoles = null,
    IReadOnlyList<PrismaticBossFeature>? Bosses = null,
    IReadOnlyList<PrismaticPocketFeature>? Pockets = null)
{
    public IEnumerable<(string Name, string StableId, string ProfileReference)> AllSlotProfiles =>
        (CapsuleSlots ?? []).Select(x => (x.Name, x.StableId, x.ProfileReference))
            .Concat((RoundedRectangleSlots ?? []).Select(x => (x.Name, x.StableId, x.ProfileReference)));
}
public sealed record PrismaticSectionRegion(
    ResolvedProfile2D Outer, IReadOnlyList<ResolvedProfile2D> Holes, IReadOnlyList<string> Provenance);
public sealed record PrismaticSectionSlab(
    double From, double To, PrismaticSectionRegion Region, IReadOnlyList<string> ActiveOperations,
    ProfileArrangement2D? Arrangement = null);
public sealed record PrismaticSectionTransition(
    double Level, IReadOnlyList<PrismaticSectionRegion> UpwardRegions, IReadOnlyList<PrismaticSectionRegion> DownwardRegions);
public sealed record PrismaticSectionStackConstruction(
    PrismaticProfileCompositionFeature Feature, IReadOnlyList<PrismaticSectionSlab> Slabs,
    IReadOnlyList<PrismaticSectionTransition> Transitions, double AnalyticVolume, IReadOnlyList<string> Diagnostics,
    IReadOnlyList<VariableOuterSectionInterval>? VariableOuterIntervals = null);
public sealed record PrismaticProfileCompositionParseResult(
    PrismaticProfileCompositionFeature? Feature, IReadOnlyDictionary<string, ResolvedProfile2D> Profiles, IReadOnlyList<string> Diagnostics,
    StaticGeometryExpansionEvidence? Expansion = null);

/// <summary>
/// Bounded parser for the composition source form.  It intentionally reuses the
/// same Point2/Line2/Circle2/Profile vocabulary as the first profile route.
/// </summary>
public static class PrismaticProfileCompositionParser
{
    public const string SegmentEndpointMustReferenceNamedPoint = "ProfileSegmentEndpointMustReferenceNamedPoint";
    private static readonly Regex Point = new(@"\bPoint2\s+(?<n>\w+)\s*\{\s*Position\s*:\s*(?:\[|Point2\s*\()\s*(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\s*(?:\]|\))", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Line = new(@"\bLine2\s+(?<n>\w+)\s*\{\s*From\s*:\s*(?<a>\w+)\s*;?\s*To\s*:\s*(?<b>\w+)", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Circle = new(@"\bCircle2\s+(?<n>\w+)\s*\{\s*Center\s*:\s*(?<c>\w+)\s*;?\s*Radius\s*:\s*(?<r>[-+.\d]+)mm", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Rect = new(@"\bRect2\s+(?<n>\w+)\s*\{\s*Center\s*:\s*(?:\[|Point2\s*\()\s*(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\s*(?:\]|\))\s*;?\s*Size\s*:\s*\[(?<w>[-+.\d]+)mm\s*,\s*(?<h>[-+.\d]+)mm\]", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex ProfileHead = new(@"\bProfile\s+(?<n>\w+)\s+Using\s+(?<layout>\w+)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex Segment = new(@"\bSegment\s+(?<n>\w+)\s*\{\s*Trace\s*:\s*(?<trace>[\w.]+)\s*;?\s*From\s*:\s*(?<from>[\w.]+)\s*;?\s*To\s*:\s*(?<to>[\w.]+)(?:\s*;?\s*Sweep\s*:\s*(?<sweep>Clockwise|CounterClockwise))?", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex ComposeHead = new(@"\bCompose\s+(?<n>\w+)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex ModifyHead = new(@"\bModify\s+(?<target>\w+)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex Placement = new(@"\bPlacement\s+(?<n>\w+)\s*\{\s*Anchor\s*:\s*\[(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\s*,\s*(?<z>[-+.\d]+)mm\]\s*;?\s*ProfilePlane\s*:\s*(?<plane>\w+)\s*;?\s*Axis\s*:\s*(?<axis>[+-][XYZ])\s*;?\s*ReferenceDirection\s*:\s*(?<reference>[+-][XYZ])", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex Operation = new(@"\b(?<intent>Base|Add|Remove)\s+(?<n>\w+)\s*\{\s*Profile\s*:\s*(?<profile>\w+)\s*;?\s*From\s*:\s*(?<from>[-+.\d]+)mm\s*;?\s*To\s*:\s*(?<to>[-+.\d]+)mm(?:\s*;?\s*Role\s*:\s*(?<role>\w+))?", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex BossHeader = new(@"\bBoss\s+(?<n>\w+)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex PocketHeader = new(@"\bPocket\s+(?<n>\w+)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex SemanticProfile = new(@"\bProfile\s*:\s*(?<profile>\w+)", RegexOptions.CultureInvariant);
    private static readonly Regex SemanticSupport = new(@"\bOn\s*:\s*(?<face>\w+|[+-][XYZ])", RegexOptions.CultureInvariant);
    private static readonly Regex BossHeight = new(@"\bHeight\s*:\s*(?<value>[-+.\d]+)mm", RegexOptions.CultureInvariant);
    private static readonly Regex PocketDepth = new(@"\bDepth\s*:\s*(?<value>[-+.\d]+)mm", RegexOptions.CultureInvariant);
    private static readonly Regex PocketMinimumFloor = new(@"\bMinimumFloorThickness\s*:\s*(?<value>[-+.\d]+)mm", RegexOptions.CultureInvariant);
    private static readonly Regex TemplateMinimumFloor = new(@"\bconcept\s+(?<name>minimumFloorThickness|minimumWallThickness)\s*:\s*(?<value>[-+.\d]+)\s*mm", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex CncPolicyHead = new(@"\bConcept\s+Struct\s+(?<name>[A-Za-z_]\w*)\s*:\s*CncManufacturingPolicy\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex HoleHeader = new(@"\bHole\s*<\s*(?<variant>\w+)\s*>\s+(?<n>\w+)\s*\{", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleCenter = new(@"\bCenter\s*:\s*\[(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HolePoint2Center = new(@"\bCenter\s*:\s*Point2\s*\(\s*(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\s*\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleDiameter = new(@"\bDiameter\s*:\s*(?<d>[-+.\d]+)mm", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleCounterboreDiameter = new(@"\bCounterboreDiameter\s*:\s*(?<d>[-+.\d]+)mm", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleCounterboreDepth = new(@"\bCounterboreDepth\s*:\s*(?<d>[-+.\d]+)mm", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleOn = new(@"\bOn\s*:\s*(?<face>[+-][XYZ])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleEnd = new(@"\bEnd\s*:\s*(?<end>\w+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleDepthEnd = new(@"\bEnd\s*:\s*(?<kind>ShaftDepth|TotalDepth)\s*\(\s*(?<depth>[-+.\d]+)mm\s*\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleFrom = new(@"\bFrom\s*:\s*(?<plane>\w+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleTermination = new(@"\bTermination\s*:\s*DrillPoint\b(?:\s*\{\s*PointAngle\s*:\s*(?<angle>[-+.\d]+)deg\s*\})?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleRole = new(@"\bRole\s*:\s*(?<role>\w+)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SlotHeader = new(@"\bSlot\s*<\s*(?<variant>\w+)\s*>\s+(?<n>\w+)\s*\{", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SlotCenter = new(@"\bCenter\s*:\s*(?:Point2\s*\()?\[?(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\]?\)?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SlotDirection = new(@"\bDirection\s*:\s*(?:Vector2\s*\()?\[?(?<x>[-+.\d]+)\s*,\s*(?<y>[-+.\d]+)\]?\)?", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SlotLength = new(@"\bLength\s*:\s*(?<v>[-+.\d]+)mm", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SlotWidth = new(@"\bWidth\s*:\s*(?<v>[-+.\d]+)mm", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SlotCornerRadius = new(@"\bCornerRadius\s*:\s*(?<v>[-+.\d]+)mm", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SlotExtent = new(@"\bExtent\s*:\s*(?<v>ThroughAll|Between\s*\(\s*(?<from>[-+.\d]+)mm\s*,\s*(?<to>[-+.\d]+)mm\s*\))", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool IsCompositionSource(string source) => ComposeHead.IsMatch(source);

    public static PrismaticProfileCompositionParseResult Parse(string source)
    {
        var expansion = StaticGeometryExpansion.Expand(source);
        if (expansion.Diagnostics.Count > 0) return new(null, new Dictionary<string, ResolvedProfile2D>(), expansion.Diagnostics, expansion.Evidence);
        source = expansion.Source;
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
            foreach (Match rawSegment in Regex.Matches(body, @"\bSegment\s+(?<name>\w+)\s*\{[\s\S]*?\b(?<endpoint>From|To)\s*:\s*(?<value>\[[^\]]*\]|Point2\s*\([^)]*\))", RegexOptions.CultureInvariant))
                diagnostics.Add($"{SegmentEndpointMustReferenceNamedPoint}:{rawSegment.Groups["name"].Value}:{rawSegment.Groups["endpoint"].Value}");
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

        // Profile is the capability boundary. Concept Path is normalized by the
        // canonical profile binder before Compose resolves its operands, so the
        // section-stack compiler never needs a path-specific lowering branch.
        foreach (var semantic in FirmamentSemanticValues.FromProfilesAndConceptPaths(source, reportedDiagnostics: diagnostics))
        {
            if (!semantic.Capabilities.Supports<ComposeOperandCapability>()) continue;
            var consumed = ProfileSemanticConsumer.RequireProfile(new(semantic, [], SemanticSourceSpan.Generated("Compose")), "Compose");
            if (!consumed.IsSuccess) { diagnostics.AddRange(consumed.Diagnostics.Select(item => item.Code + ":" + item.Message)); continue; }
            if (!profiles.TryAdd(consumed.Value!.Name, consumed.Value))
                diagnostics.Add($"compose-profile-duplicate:{consumed.Value.Name}");
        }

        var compose = ComposeHead.Match(source);
        if (!compose.Success) return new(null, profiles, diagnostics.Append("compose-source-missing-compose").ToArray());
        var composeBody = Block(source, compose.Index + compose.Length - 1);
        if (composeBody is null) return new(null, profiles, diagnostics.Append("compose-source-unclosed-compose").ToArray());
        composeBody = BindExternalModifyFeatures(source, compose.Groups["n"].Value, composeBody, diagnostics);
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
            if (!profiles.ContainsKey(profile)) diagnostics.Add($"ConceptPathProfileNotAdmittedByCompose:{name}:{profile}");
            if (!double.IsFinite(from) || !double.IsFinite(to) || from >= to) diagnostics.Add($"compose-invalid-interval:{name}:{from:R}:{to:R}");
            operations.Add(new(name, Enum.Parse<PrismaticProfileIntent>(match.Groups["intent"].Value), profile, from, to, match.Groups["role"].Success ? match.Groups["role"].Value : match.Groups["intent"].Value, $"offset:{match.Index}"));
        }
        if (operations.Count(o => o.Intent == PrismaticProfileIntent.Base) != 1) diagnostics.Add("compose-requires-exactly-one-base-operation");
        var bosses = new List<PrismaticBossFeature>();
        var pockets = new List<PrismaticPocketFeature>();
        var stock = operations.SingleOrDefault(operation => operation.Intent == PrismaticProfileIntent.Base);
        if (stock is not null)
        {
            foreach (Match header in BossHeader.Matches(composeBody))
            {
                var name = header.Groups["n"].Value;
                var body = Block(composeBody, header.Index + header.Length - 1);
                if (body is null) { diagnostics.Add($"firmament-boss-malformed:{name}:unclosed"); continue; }
                var profileMatch = SemanticProfile.Match(body); var support = SemanticSupport.Match(body); var heightMatch = BossHeight.Match(body);
                if (!profileMatch.Success || !support.Success || !heightMatch.Success) { diagnostics.Add($"firmament-boss-missing-required-field:{name}:required=On,Profile,Height"); continue; }
                var profileName = profileMatch.Groups["profile"].Value; var face = support.Groups["face"].Value; var height = N(heightMatch, "value");
                if (!names.Add(name)) { diagnostics.Add($"compose-duplicate-operation:{name}"); continue; }
                if (!profiles.TryGetValue(profileName, out var bossProfile)) { diagnostics.Add($"firmament-boss-invalid-profile:{name}:profile={profileName}"); continue; }
                if (face is not ("Top" or "+Z")) { diagnostics.Add($"firmament-boss-invalid-target:{name}:On={face}:supported=Top"); continue; }
                if (!double.IsFinite(height) || height <= 0d) { diagnostics.Add($"firmament-boss-height-must-be-positive:{name}:height={height:R}mm"); continue; }
                if (!HasConnectedBossSupport(stock, bossProfile, profiles)) { diagnostics.Add($"firmament-boss-disconnected-from-host:{name}:host={compose.Groups["n"].Value}:profile={profileName}:support=Top"); continue; }
                var stableId = $"boss:{compose.Groups["n"].Value}.{name}"; var sourceSpan = $"offset:{header.Index}";
                bosses.Add(new(name, stableId, compose.Groups["n"].Value, "Top", profileName, height, stock.To, stock.To + height, sourceSpan));
                operations.Add(new(name, PrismaticProfileIntent.Add, profileName, stock.To, stock.To + height, "Boss", sourceSpan, stableId, "Boss"));
            }

            foreach (Match header in PocketHeader.Matches(composeBody))
            {
                var name = header.Groups["n"].Value;
                var body = Block(composeBody, header.Index + header.Length - 1);
                if (body is null) { diagnostics.Add($"firmament-pocket-malformed:{name}:unclosed"); continue; }
                var profileMatch = SemanticProfile.Match(body); var support = SemanticSupport.Match(body); var depthMatch = PocketDepth.Match(body);
                if (!profileMatch.Success || !support.Success || !depthMatch.Success) { diagnostics.Add($"firmament-pocket-missing-required-field:{name}:required=On,Profile,Depth"); continue; }
                var profileName = profileMatch.Groups["profile"].Value; var face = support.Groups["face"].Value; var depth = N(depthMatch, "value");
                if (!names.Add(name)) { diagnostics.Add($"compose-duplicate-operation:{name}"); continue; }
                if (!profiles.TryGetValue(profileName, out var pocketProfile)) { diagnostics.Add($"firmament-pocket-invalid-profile:{name}:profile={profileName}"); continue; }
                if (face is not ("Top" or "+Z")) { diagnostics.Add($"firmament-pocket-invalid-target:{name}:On={face}:supported=Top"); continue; }
                if (!double.IsFinite(depth) || depth <= 0d) { diagnostics.Add($"firmament-pocket-depth-must-be-positive:{name}:depth={depth:R}mm"); continue; }
                if (!IsPocketFootprintInsideStock(stock, pocketProfile, profiles)) { diagnostics.Add($"firmament-pocket-invalid-profile:{name}:profile={profileName}:footprint-must-be-enclosed-by-host"); continue; }
                var hostThickness = stock.To - stock.From; var remaining = hostThickness - depth;
                var (minimumFloor, policySource) = ResolveMinimumFloor(source, body);
                if (!double.IsFinite(minimumFloor) || minimumFloor <= 0d)
                {
                    diagnostics.Add($"firmament-pocket-minimum-floor-policy-invalid:{name}:requiredFloor={minimumFloor:R}mm:policy={policySource}");
                    continue;
                }
                if (remaining <= 1e-7d)
                {
                    diagnostics.Add($"firmament-pocket-through-depth:{name}:host={compose.Groups["n"].Value}:depth={depth:R}mm:hostThickness={hostThickness:R}mm:remainingFloor={remaining:R}mm:requiredFloor={minimumFloor:R}mm");
                    continue;
                }
                if (remaining + 1e-7d < minimumFloor)
                {
                    diagnostics.Add($"firmament-pocket-minimum-floor-thickness:{name}:host={compose.Groups["n"].Value}:depth={depth:R}mm:hostThickness={hostThickness:R}mm:remainingFloor={remaining:R}mm:requiredFloor={minimumFloor:R}mm:policy={policySource}");
                    continue;
                }
                var stableId = $"pocket:{compose.Groups["n"].Value}.{name}"; var sourceSpan = $"offset:{header.Index}";
                pockets.Add(new(name, stableId, compose.Groups["n"].Value, "Top", profileName, depth, hostThickness, remaining, minimumFloor, policySource, stock.To - depth, stock.To, sourceSpan));
                operations.Add(new(name, PrismaticProfileIntent.Remove, profileName, stock.To - depth, stock.To, "Pocket", sourceSpan, stableId, "Pocket"));
            }
        }
        var shaftHoles = new List<PrismaticShaftHoleFeature>();
        var counterboreHoles = new List<PrismaticCounterboreHoleFeature>();
        var constructionPlaneBlindDrills = new List<PrismaticConstructionPlaneBlindDrillFeature>();
        var capsuleSlots = new List<PrismaticCapsuleSlotFeature>();
        var roundedRectangleSlots = new List<PrismaticRoundedRectangleSlotFeature>();
        var materialFrom = operations.Count == 0 ? 0d : operations.Min(x => x.From);
        var materialTo = operations.Count == 0 ? 0d : operations.Max(x => x.To);
        foreach (Match header in HoleHeader.Matches(composeBody))
        {
            var name = header.Groups["n"].Value;
            var body = Block(composeBody, header.Index + header.Length - 1);
            if (body is null) { diagnostics.Add($"compose-hole-unclosed:{name}"); continue; }
            var center = HoleCenter.Match(body); if (!center.Success) center = HolePoint2Center.Match(body);
            var diameter = HoleDiameter.Match(body); var end = HoleEnd.Match(body); var role = HoleRole.Match(body);
            var counterboreDiameter = HoleCounterboreDiameter.Match(body); var counterboreDepth = HoleCounterboreDepth.Match(body); var on = HoleOn.Match(body);
            var from = HoleFrom.Match(body); var depthEnd = HoleDepthEnd.Match(body); var termination = HoleTermination.Match(body);
            if (from.Success)
            {
                if (!string.Equals(header.Groups["variant"].Value, "Shaft", StringComparison.OrdinalIgnoreCase)
                    || !depthEnd.Success || !termination.Success)
                { diagnostics.Add($"compose-construction-plane-blind-hole-invalid-contract:{name}"); continue; }
                if (!center.Success || !diameter.Success) { diagnostics.Add($"compose-construction-plane-blind-hole-missing-center-or-diameter:{name}"); continue; }
                var trace = ResolveConstructionPlaneTrace(source, from.Groups["plane"].Value);
                string? planeDiagnostic = null; string? frameDiagnostic = null;
                if (trace is null || !ConceptIrResolver.TryResolvePlane(source, trace, out var conceptPlane, out planeDiagnostic)
                    || conceptPlane is null
                    || !ConstructionPlane.TryTrace($"construction:{from.Groups["plane"].Value}", conceptPlane, $"offset:{header.Index}", out var plane, out frameDiagnostic))
                { diagnostics.Add(planeDiagnostic ?? frameDiagnostic ?? $"compose-construction-plane-not-found:{from.Groups["plane"].Value}"); continue; }
                var localX = N(center, "x"); var localY = N(center, "y"); var localDiameter = N(diameter, "d"); var declaredDepth = N(depthEnd, "depth");
                var angle = termination.Groups["angle"].Success ? N(termination, "angle") : 118d;
                if (!double.IsFinite(localX) || !double.IsFinite(localY) || !double.IsFinite(localDiameter) || localDiameter <= 0d || !double.IsFinite(declaredDepth) || declaredDepth < 0d || !double.IsFinite(angle) || angle <= 0d || angle >= 180d)
                { diagnostics.Add($"compose-construction-plane-blind-hole-invalid-geometry:{name}"); continue; }
                if (!names.Add(name)) { diagnostics.Add($"compose-duplicate-operation:{name}"); continue; }
                var blindStableId = $"hole:{compose.Groups["n"].Value}.{name}";
                constructionPlaneBlindDrills.Add(new(name, blindStableId, plane!.StableId, plane.SourceConceptId, plane.Origin, plane.AxisX, plane.AxisY, plane.AxisZ,
                    localX, localY, localDiameter, declaredDepth, string.Equals(depthEnd.Groups["kind"].Value, "TotalDepth", StringComparison.OrdinalIgnoreCase), angle,
                    role.Success ? role.Groups["role"].Value : "BlindDrillPoint", $"offset:{header.Index}", plane.Provenance));
                continue;
            }
            var variant = header.Groups["variant"].Value;
            if (string.Equals(variant, "Counterbore", StringComparison.OrdinalIgnoreCase))
            {
                if (!end.Success || !string.Equals(end.Groups["end"].Value, "ThroughAll", StringComparison.OrdinalIgnoreCase))
                { diagnostics.Add($"ComposeCounterboreEndUnsupported:{name}:supported=ThroughAll"); continue; }
                if (!on.Success || !string.Equals(on.Groups["face"].Value, "+Z", StringComparison.OrdinalIgnoreCase))
                { diagnostics.Add($"ComposeCounterboreAxisUnsupported:{name}:supported=+Z"); continue; }
                if (!center.Success || !diameter.Success || !counterboreDiameter.Success || !counterboreDepth.Success)
                { diagnostics.Add($"ComposeCounterboreMissingRequiredField:{name}"); continue; }
                var counterboreX = N(center, "x"); var counterboreY = N(center, "y"); var shaftDiameter = N(diameter, "d"); var boreDiameter = N(counterboreDiameter, "d"); var boreDepth = N(counterboreDepth, "d");
                if (!double.IsFinite(counterboreX) || !double.IsFinite(counterboreY) || !double.IsFinite(shaftDiameter) || shaftDiameter <= 0d || !double.IsFinite(boreDiameter) || boreDiameter <= shaftDiameter || !double.IsFinite(boreDepth) || boreDepth <= 0d)
                { diagnostics.Add($"ComposeCounterboreInvalidGeometry:{name}"); continue; }
                var hostDepth = materialTo - materialFrom;
                if (boreDepth > hostDepth + 1e-8d)
                { diagnostics.Add($"ComposeCounterboreDepthExceedsHost:{name}:depth={boreDepth:R}:hostDepth={hostDepth:R}"); continue; }
                if (!names.Add(name)) { diagnostics.Add($"compose-duplicate-operation:{name}"); continue; }
                var counterboreStableId = $"hole:{compose.Groups["n"].Value}.{name}";
                if (!CircleContainedInBaseProfile(operations, profiles, counterboreX, counterboreY, boreDiameter / 2d))
                { diagnostics.Add($"ComposeCounterboreFootprintLeavesProfileMaterial:{name}:radius={boreDiameter / 2d:R}"); continue; }
                if (IntersectsExistingCircularCavity(shaftHoles, counterboreHoles, counterboreX, counterboreY, boreDiameter / 2d))
                { diagnostics.Add($"ComposeCounterboreIntersectsExistingCavity:{name}"); continue; }
                var shaftProfile = $"{name}ShaftProfile"; var boreProfile = $"{name}CounterboreProfile";
                if (profiles.ContainsKey(shaftProfile) || profiles.ContainsKey(boreProfile)) { diagnostics.Add($"compose-hole-profile-name-collision:{name}"); continue; }
                profiles.Add(shaftProfile, CircleProfile(shaftProfile, counterboreX, counterboreY, shaftDiameter / 2d, counterboreStableId, $"offset:{header.Index}"));
                profiles.Add(boreProfile, CircleProfile(boreProfile, counterboreX, counterboreY, boreDiameter / 2d, counterboreStableId, $"offset:{header.Index}"));
                var counterboreSourceSpan = $"offset:{header.Index}"; var counterboreSemanticRole = role.Success ? role.Groups["role"].Value : "CounterboreHole";
                counterboreHoles.Add(new(name, counterboreStableId, shaftProfile, boreProfile, counterboreX, counterboreY, shaftDiameter, boreDiameter, boreDepth, materialFrom, materialTo, counterboreSemanticRole, counterboreSourceSpan));
                operations.Add(new($"{name}Shaft", PrismaticProfileIntent.Remove, shaftProfile, materialFrom, materialTo, counterboreSemanticRole, counterboreSourceSpan, counterboreStableId, "Hole<Counterbore>", shaftDiameter));
                operations.Add(new($"{name}Counterbore", PrismaticProfileIntent.Remove, boreProfile, materialTo - boreDepth, materialTo, counterboreSemanticRole, counterboreSourceSpan, counterboreStableId, "Hole<Counterbore>", boreDiameter));
                continue;
            }
            if (!string.Equals(variant, "Shaft", StringComparison.OrdinalIgnoreCase)
                || !end.Success || !string.Equals(end.Groups["end"].Value, "ThroughAll", StringComparison.OrdinalIgnoreCase))
            { diagnostics.Add($"compose-hole-unsupported-variant-or-end:{name}"); continue; }
            if (!center.Success || !diameter.Success) { diagnostics.Add($"compose-hole-missing-center-or-diameter:{name}"); continue; }
            var x = N(center, "x"); var y = N(center, "y"); var d = N(diameter, "d");
            if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(d) || d <= 0d) { diagnostics.Add($"compose-hole-invalid-center-or-diameter:{name}"); continue; }
            if (!names.Add(name)) { diagnostics.Add($"compose-duplicate-operation:{name}"); continue; }
            var profileName = $"{name}ShaftProfile";
            if (profiles.ContainsKey(profileName)) { diagnostics.Add($"compose-hole-profile-name-collision:{name}:{profileName}"); continue; }
            var stableId = $"hole:{compose.Groups["n"].Value}.{name}";
            var holeProfile = CircleProfile(profileName, x, y, d / 2d, stableId, $"offset:{header.Index}");
            var validation = ResolvedProfile2DValidator.Validate(holeProfile);
            diagnostics.AddRange(validation.Diagnostics);
            if (!validation.IsValid) continue;
            profiles.Add(profileName, holeProfile);
            var semanticRole = role.Success ? role.Groups["role"].Value : "ShaftHole";
            var sourceSpan = $"offset:{header.Index}";
            shaftHoles.Add(new(name, stableId, profileName, x, y, d, materialFrom, materialTo, semanticRole, sourceSpan));
            operations.Add(new(name, PrismaticProfileIntent.Remove, profileName, materialFrom, materialTo, semanticRole, sourceSpan, stableId, "Hole<Shaft>", d));
        }
        // Source order must not decide whether an intersecting shaft/counterbore
        // pair is accepted.  Counterbores normally reject immediately against
        // preceding shafts; this final symmetric check covers a shaft authored
        // after a counterbore without changing the established shaft-only route.
        foreach (var shaft in shaftHoles)
        foreach (var counterbore in counterboreHoles)
            if (CirclesTouchOrOverlap(shaft.CenterX, shaft.CenterY, shaft.Diameter / 2d, counterbore.CenterX, counterbore.CenterY, counterbore.CounterboreDiameter / 2d))
                diagnostics.Add($"ComposeCounterboreIntersectsExistingCavity:{counterbore.Name}:shaft={shaft.Name}");
        foreach (Match header in SlotHeader.Matches(composeBody))
        {
            var name = header.Groups["n"].Value; var body = Block(composeBody, header.Index + header.Length - 1);
            if (body is null) { diagnostics.Add($"SlotProfileDegenerate:{name}:unclosed"); continue; }
            var center = SlotCenter.Match(body); var direction = SlotDirection.Match(body); var length = SlotLength.Match(body); var width = SlotWidth.Match(body); var extent = SlotExtent.Match(body); var role = HoleRole.Match(body);
            var variant=header.Groups["variant"].Value;
            if (!string.Equals(variant,"Capsule",StringComparison.OrdinalIgnoreCase) && !string.Equals(variant,"RoundedRectangle",StringComparison.OrdinalIgnoreCase)) { diagnostics.Add($"UnsupportedSlotHostTopology:{name}:supported variants are Capsule and RoundedRectangle"); continue; }
            if (!center.Success || !direction.Success || !length.Success || !width.Success || !extent.Success) { diagnostics.Add($"SlotProfileDegenerate:{name}:missing Center, Direction, Length, Width, or Extent"); continue; }
            var x=N(center,"x"); var y=N(center,"y"); var dx=N(direction,"x"); var dy=N(direction,"y"); var l=N(length,"v"); var w=N(width,"v");
            if (l <= 0 || !double.IsFinite(l)) { diagnostics.Add($"SlotLengthMustBePositive:{name}:length={l:R}"); continue; }
            if (w <= 0 || !double.IsFinite(w)) { diagnostics.Add($"SlotWidthMustBePositive:{name}:width={w:R}"); continue; }
            if (string.Equals(variant,"Capsule",StringComparison.OrdinalIgnoreCase) && l < w) { diagnostics.Add($"SlotLengthLessThanWidth:{name}:length={l:R}:width={w:R}"); continue; }
            if (string.Equals(variant,"Capsule",StringComparison.OrdinalIgnoreCase) && l == w) { diagnostics.Add($"SlotProfileDegenerate:{name}:Length == Width is rejected; use Hole<Shaft> for a circular opening"); continue; }
            var norm=Math.Sqrt(dx*dx+dy*dy); if (!double.IsFinite(norm) || norm <= 1e-12) { diagnostics.Add($"SlotDirectionDegenerate:{name}:direction=[{dx:R},{dy:R}]"); continue; }
            if (!names.Add(name)) { diagnostics.Add($"compose-duplicate-operation:{name}"); continue; }
            var from=materialFrom; var to=materialTo; var extentText=extent.Groups["v"].Value;
            if (!string.Equals(extentText, "ThroughAll", StringComparison.OrdinalIgnoreCase)) { from=N(extent,"from"); to=N(extent,"to"); if (from >= to) { diagnostics.Add($"SlotExtentInvalid:{name}:entry={from:R}:exit={to:R}"); continue; } if (to <= materialFrom || from >= materialTo) { diagnostics.Add($"SlotDoesNotIntersectMaterial:{name}:host=[{materialFrom:R},{materialTo:R}]"); continue; } from=Math.Max(from,materialFrom); to=Math.Min(to,materialTo); }
            dx/=norm; dy/=norm; var stableId=$"slot:{compose.Groups["n"].Value}.{name}"; var sourceSpan=$"offset:{header.Index}"; var semanticRole=role.Success ? role.Groups["role"].Value : $"{variant}Slot";
            if (string.Equals(variant,"Capsule",StringComparison.OrdinalIgnoreCase))
            {
                var profileName=$"{name}CapsuleProfile"; var profile=CapsuleProfile(profileName,x,y,dx,dy,l,w,stableId,sourceSpan); var validation=ResolvedProfile2DValidator.Validate(profile); diagnostics.AddRange(validation.Diagnostics); if (!validation.IsValid) { diagnostics.Add($"SlotProfileDegenerate:{name}"); continue; }
                profiles.Add(profileName,profile); capsuleSlots.Add(new(name,stableId,profileName,x,y,dx,dy,l,w,from,to,extentText,semanticRole,sourceSpan)); operations.Add(new(name,PrismaticProfileIntent.Remove,profileName,from,to,semanticRole,sourceSpan,stableId,"Slot<Capsule>"));
            }
            else
            {
                var corner=SlotCornerRadius.Match(body); if (!corner.Success) { diagnostics.Add($"SlotProfileDegenerate:{name}:RoundedRectangle requires CornerRadius"); continue; }
                var r=N(corner,"v"); if (!double.IsFinite(r) || r <= 0 || 2*r > Math.Min(l,w)) { diagnostics.Add($"SlotProfileDegenerate:{name}:CornerRadius={r:R} must be positive and no greater than min(Length,Width)/2"); continue; }
                var profileName=$"{name}RoundedRectangleProfile"; var profile=RoundedRectangleProfile(profileName,x,y,dx,dy,l,w,r,stableId,sourceSpan); var validation=ResolvedProfile2DValidator.Validate(profile); diagnostics.AddRange(validation.Diagnostics); if (!validation.IsValid) { diagnostics.Add($"SlotProfileDegenerate:{name}"); continue; }
                profiles.Add(profileName,profile); roundedRectangleSlots.Add(new(name,stableId,profileName,x,y,dx,dy,l,w,r,from,to,extentText,semanticRole,sourceSpan)); operations.Add(new(name,PrismaticProfileIntent.Remove,profileName,from,to,semanticRole,sourceSpan,stableId,"Slot<RoundedRectangle>"));
            }
        }
        var levels = operations.SelectMany(o => new[] { o.From, o.To }).Distinct().Order().ToArray();
        var feature = diagnostics.Count == 0 ? new PrismaticProfileCompositionFeature(compose.Groups["n"].Value, "XY", "+Z", placement, operations, levels, "parser-backed-scaffold-profile-composition", shaftHoles, capsuleSlots, roundedRectangleSlots, constructionPlaneBlindDrills, counterboreHoles, bosses, pockets) : null;
        return new(feature, profiles, diagnostics.Distinct().ToArray(), expansion.Evidence);
    }

    private static bool HasConnectedBossSupport(PrismaticProfileOperation stock, ResolvedProfile2D boss, IReadOnlyDictionary<string, ResolvedProfile2D> profiles)
    {
        var probe = ProfileArrangementBuilder.Compose("XY", [stock with { From = 0d, To = 1d }, new("BossSupportProbe", PrismaticProfileIntent.Add, boss.Name, 0d, 1d, "Boss", "generated")], profiles, "boss-connectivity-probe");
        if (probe.Region is null || probe.Arrangement.Diagnostics.Count > 0) return false;
        var stockArea = Math.Abs(PrismaticSectionStackCompiler.ProfileArea(profiles[stock.ProfileReference]));
        var bossArea = Math.Abs(PrismaticSectionStackCompiler.ProfileArea(boss));
        var overlapArea = stockArea + bossArea - PrismaticSectionStackCompiler.Area(probe.Region);
        return overlapArea > Math.Max(1e-7d, Math.Min(stockArea, bossArea) * 1e-8d);
    }

    private static bool IsPocketFootprintInsideStock(PrismaticProfileOperation stock, ResolvedProfile2D pocket, IReadOnlyDictionary<string, ResolvedProfile2D> profiles)
    {
        var probe = ProfileArrangementBuilder.Compose("XY", [stock with { From = 0d, To = 1d }, new("PocketContainmentProbe", PrismaticProfileIntent.Remove, pocket.Name, 0d, 1d, "Pocket", "generated")], profiles, "pocket-containment-probe");
        if (probe.Region is null || probe.Arrangement.Diagnostics.Count > 0 || probe.Region.Holes.Count == 0) return false;
        var stockArea = Math.Abs(PrismaticSectionStackCompiler.ProfileArea(profiles[stock.ProfileReference]));
        var pocketArea = Math.Abs(PrismaticSectionStackCompiler.ProfileArea(pocket));
        var retainedArea = PrismaticSectionStackCompiler.Area(probe.Region);
        return Math.Abs((stockArea - retainedArea) - pocketArea) <= Math.Max(1e-7d, pocketArea * 1e-8d);
    }

    private static (double Value, string Source) ResolveMinimumFloor(string source, string pocketBody)
    {
        var local = PocketMinimumFloor.Match(pocketBody);
        if (local.Success) return (N(local, "value"), "Pocket.MinimumFloorThickness");

        var canonical = CncPolicyHead.Matches(source).Cast<Match>()
            .Select(match => (Match: match, Body: Block(source, source.IndexOf('{', match.Index))))
            .Where(candidate => candidate.Body is not null)
            .ToArray();
        if (canonical.Length > 1) return (double.NaN, "CncManufacturingPolicy:ambiguous-multiple-authorities");
        if (canonical.Length == 1)
        {
            var floor = PocketMinimumFloor.Match(canonical[0].Body!);
            if (floor.Success) return (N(floor, "value"), "CncManufacturingPolicy.MinimumFloorThickness");
            var wall = Regex.Match(canonical[0].Body!, @"\bMinimumWallThickness\s*:\s*(?<value>[-+.\d]+)mm", RegexOptions.CultureInvariant);
            if (wall.Success) return (N(wall, "value"), "CncManufacturingPolicy.MinimumWallThickness");
        }

        // Historical lowercase templates remain readable so persisted Preview sources
        // do not change behavior; new authoring uses the typed policy contract above.
        var concepts = TemplateMinimumFloor.Matches(source).Cast<Match>().ToArray();
        var floors = concepts.Where(match => string.Equals(match.Groups["name"].Value, "minimumFloorThickness", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (floors.Length > 1) return (double.NaN, "Template.minimumFloorThickness:ambiguous-multiple-authorities");
        if (floors.Length == 1) return (N(floors[0], "value"), "Template.minimumFloorThickness");
        var walls = concepts.Where(match => string.Equals(match.Groups["name"].Value, "minimumWallThickness", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (walls.Length > 1) return (double.NaN, "Template.minimumWallThickness:ambiguous-multiple-authorities");
        return walls.Length == 1 ? (N(walls[0], "value"), "Template.minimumWallThickness") : (1d, "Preview3.boundedDefault");
    }

    // The canonical Modify form is body-level syntax. Bind feature declarations
    // targeting this Compose body into the existing section-stack feature path so
    // they cannot be admitted and then silently omitted from exported geometry.
    private static string BindExternalModifyFeatures(string source, string composeName, string composeBody, List<string> diagnostics)
    {
        var boundFeatures = new List<string>();
        foreach (Match modify in ModifyHead.Matches(source))
        {
            var body = Block(source, source.IndexOf('{', modify.Index));
            if (body is null || !Regex.IsMatch(body, @"\b(?:Hole\s*<|Slot\s*<|Pattern\s+)", RegexOptions.CultureInvariant)) continue;
            if (!string.Equals(modify.Groups["target"].Value, composeName, StringComparison.Ordinal))
            {
                diagnostics.Add($"{FirmamentV2Parser.ModifyTargetNotBoundToActiveCompose}:target={modify.Groups["target"].Value}:compose={composeName}:features must target the active Compose body.");
                continue;
            }
            boundFeatures.Add(body);
        }
        return boundFeatures.Count == 0 ? composeBody : composeBody + Environment.NewLine + string.Join(Environment.NewLine, boundFeatures);
    }

    private static ResolvedProfile2D CircleProfile(string profileName, double x, double y, double radius, string holeStableId, string sourceSpan)
    {
        var names = new[] { "EastToNorth", "NorthToWest", "WestToSouth", "SouthToEast" };
        var segments = Enumerable.Range(0, 4).Select(i => new ResolvedProfileSegment2D(
            names[i], new LineArcCircularArc2D((x, y), radius, i * Math.PI / 2d, Math.PI / 2d),
            new($"profile:{profileName}.Outer.{names[i]}", $"{holeStableId}.axis", sourceSpan, $"Hole<Shaft>({holeStableId})", "XY"))).ToArray();
        return new(profileName, "XY", [new ResolvedProfileLoop2D("Outer", true, segments)]);
    }
    private static bool CircleContainedInBaseProfile(IReadOnlyList<PrismaticProfileOperation> operations, IReadOnlyDictionary<string, ResolvedProfile2D> profiles, double x, double y, double radius)
    {
        var baseOperation = operations.SingleOrDefault(operation => operation.Intent == PrismaticProfileIntent.Base);
        if (baseOperation is null || !profiles.TryGetValue(baseOperation.ProfileReference, out var profile) || ProfileArrangementBuilder.PointInProfile(profile, (x, y)) != ArrangementPointLocation.Inside) return false;
        return profile.Loops.Single().Segments.All(segment => DistanceToCurve((x, y), segment.Geometry) > radius + 1e-8d);
    }
    private static bool IntersectsExistingCircularCavity(IReadOnlyList<PrismaticShaftHoleFeature> shafts, IReadOnlyList<PrismaticCounterboreHoleFeature> counterbores, double x, double y, double radius) =>
        shafts.Any(hole => CirclesTouchOrOverlap(x, y, radius, hole.CenterX, hole.CenterY, hole.Diameter / 2d)) ||
        counterbores.Any(hole => CirclesTouchOrOverlap(x, y, radius, hole.CenterX, hole.CenterY, hole.CounterboreDiameter / 2d));
    private static bool CirclesTouchOrOverlap(double ax, double ay, double ar, double bx, double by, double br) => Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by)) <= ar + br + 1e-8d;
    private static double DistanceToCurve((double X, double Y) point, LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => DistanceToSegment(point, line.Start, line.End),
        LineArcCircularArc2D arc => DistanceToArc(point, arc),
        _ => double.NaN
    };
    private static double DistanceToSegment((double X, double Y) point, (double X, double Y) start, (double X, double Y) end)
    {
        var dx = end.X - start.X; var dy = end.Y - start.Y; var lengthSquared = dx * dx + dy * dy;
        var t = Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0d, 1d);
        return Math.Sqrt(Math.Pow(point.X - (start.X + t * dx), 2d) + Math.Pow(point.Y - (start.Y + t * dy), 2d));
    }
    private static double DistanceToArc((double X, double Y) point, LineArcCircularArc2D arc)
    {
        var dx = point.X - arc.Center.X; var dy = point.Y - arc.Center.Y; var radial = Math.Sqrt(dx * dx + dy * dy); var angle = Math.Atan2(dy, dx);
        var delta = angle - arc.StartAngleRadians;
        if (arc.SweepAngleRadians >= 0d) while (delta < 0d) delta += 2d * Math.PI; else while (delta > 0d) delta -= 2d * Math.PI;
        if (delta / arc.SweepAngleRadians is >= -1e-8d and <= 1d + 1e-8d) return Math.Abs(radial - arc.Radius);
        var a = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)); var b = (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians));
        return Math.Min(Math.Sqrt(Math.Pow(point.X - a.Item1, 2d) + Math.Pow(point.Y - a.Item2, 2d)), Math.Sqrt(Math.Pow(point.X - b.Item1, 2d) + Math.Pow(point.Y - b.Item2, 2d)));
    }
    private static ResolvedProfile2D CapsuleProfile(string profileName, double x, double y, double dx, double dy, double length, double width, string slotId, string sourceSpan)
    {
        var r=width/2d; var h=(length-width)/2d; var nx=-dy; var ny=dx;
        var a=(X:x-dx*h,Y:y-dy*h); var b=(X:x+dx*h,Y:y+dy*h);
        var p0=(X:a.X+nx*r,Y:a.Y+ny*r); var p1=(X:b.X+nx*r,Y:b.Y+ny*r); var p2=(X:b.X-nx*r,Y:b.Y-ny*r); var p3=(X:a.X-nx*r,Y:a.Y-ny*r);
        var start=Math.Atan2(ny,nx); var end=Math.Atan2(-ny,-nx);
        ResolvedProfileSegment2D S(string role, LineArcProfileCurve2D curve) => new(role,curve,new($"profile:{profileName}.Outer.{role}",$"{slotId}.{role}",sourceSpan,$"Slot<Capsule>({slotId})","XY"));
        return new(profileName,"XY",[new ResolvedProfileLoop2D("Outer",true,[S("NegativeSide",new LineArcLineSegment2D(p3,p2)),S("EndCap",new LineArcCircularArc2D(b,r,end,Math.PI)),S("PositiveSide",new LineArcLineSegment2D(p1,p0)),S("StartCap",new LineArcCircularArc2D(a,r,start,Math.PI))])]);
    }
    private static ResolvedProfile2D RoundedRectangleProfile(string profileName, double x, double y, double dx, double dy, double length, double width, double r, string slotId, string sourceSpan)
    {
        var nx=-dy; var ny=dx; (double X,double Y) P(double u,double v)=>(x+dx*u+nx*v,y+dy*u+ny*v); var hx=length/2d; var hy=width/2d;
        ResolvedProfileSegment2D S(string role,LineArcProfileCurve2D curve)=>new(role,curve,new($"profile:{profileName}.Outer.{role}",$"{slotId}.{role}",sourceSpan,$"Slot<RoundedRectangle>({slotId})","XY"));
        return new(profileName,"XY",[new ResolvedProfileLoop2D("Outer",true,[
            S("NegativeSide",new LineArcLineSegment2D(P(-hx+r,-hy),P(hx-r,-hy))), S("EndNegativeCorner",new LineArcCircularArc2D(P(hx-r,-hy+r),r,Math.Atan2(-ny, -nx),Math.PI/2d)),
            S("EndSide",new LineArcLineSegment2D(P(hx,-hy+r),P(hx,hy-r))), S("EndPositiveCorner",new LineArcCircularArc2D(P(hx-r,hy-r),r,Math.Atan2(dy,dx),Math.PI/2d)),
            S("PositiveSide",new LineArcLineSegment2D(P(hx-r,hy),P(-hx+r,hy))), S("StartPositiveCorner",new LineArcCircularArc2D(P(-hx+r,hy-r),r,Math.Atan2(ny,nx),Math.PI/2d)),
            S("StartSide",new LineArcLineSegment2D(P(-hx,hy-r),P(-hx,-hy+r))), S("StartNegativeCorner",new LineArcCircularArc2D(P(-hx+r,-hy+r),r,Math.Atan2(-dy,-dx),Math.PI/2d))
        ])]);
    }

    private static double N(Match match, string name) => double.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture);
    private static string? ResolveConstructionPlaneTrace(string source, string name)
    {
        var declaration = Regex.Match(source, $@"\bConstruction\s+Plane\s+{Regex.Escape(name)}\s*\{{\s*Trace\s*:\s*(?<trace>[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return declaration.Success ? declaration.Groups["trace"].Value : null;
    }
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
