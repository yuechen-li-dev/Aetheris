using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
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
    double Height, double From, double To, string SourceSpan,
    string? SupportSpan = null, string? ParentSupport = null);
/// <summary>First-class finite material removal lowered through the section-stack Remove operation.</summary>
public sealed record PrismaticPocketFeature(
    string Name, string StableId, string Host, string SupportFace, string ProfileReference,
    double Depth, double HostThickness, double RemainingFloor, double MinimumFloorThickness,
    string MinimumFloorPolicySource, double From, double To, string SourceSpan,
    string? SupportSpan = null, string? ParentSupport = null);
/// <summary>
/// Typed AIR for a bounded local material delta. The profile remains semantic
/// authority until section-stack planning; no public tool body reaches B-rep.
/// </summary>
public sealed record PrismaticMaterialOffsetFeature(
    string Name, string StableId, PrismaticProfileIntent Intent, string ToolFamily,
    string Target, string Support, string ProfileReference, string Direction,
    string Termination, double From, double To, double Extent, string SourceSpan,
    IReadOnlyList<double> AuthorizedRegion,
    string? SupportSpan = null, string? ParentSupport = null)
{
    public string OperationKind => Intent == PrismaticProfileIntent.Add ? "Add" : "Remove";
}
public sealed record PrismaticShaftHoleFeature(
    string Name, string StableId, string ProfileReference, double CenterX, double CenterY, double Diameter,
    double From, double To, string SemanticRole, string SourceSpan,
    string? SupportSpan = null, string? ParentSupport = null, double? SupportBoundaryMargin = null);
/// <summary>
/// A bounded stepped through cavity in an admitted +Z prismatic compose host.
/// The two profile references are planner-owned section cuts, not Boolean tools.
/// </summary>
public sealed record PrismaticCounterboreHoleFeature(
    string Name, string StableId, string ShaftProfileReference, string CounterboreProfileReference,
    double CenterX, double CenterY, double Diameter, double CounterboreDiameter, double CounterboreDepth,
    double From, double To, string SemanticRole, string SourceSpan,
    string? SupportSpan = null, string? ParentSupport = null, double? SupportBoundaryMargin = null);
/// <summary>Exact conical entry on a circular through cavity; stations are in the composition frame.</summary>
public sealed record PrismaticCountersinkHoleFeature(
    string Name, string StableId, string ShaftProfileReference,
    double CenterX, double CenterY, double Diameter, double CountersinkDiameter, double AngleDegrees,
    double From, double To, string SemanticRole, string SourceSpan,
    string? SupportSpan = null, string? ParentSupport = null, double? SupportBoundaryMargin = null)
{
    public double SemiAngle => AngleDegrees * Math.PI / 360d;
    public double ConeDepth => (CountersinkDiameter - Diameter) / (2d * Math.Tan(SemiAngle));
    public double ConeFrom => To - ConeDepth;
    public double RadiusAt(double station) => Diameter / 2d + Math.Clamp(station - ConeFrom, 0d, ConeDepth) * Math.Tan(SemiAngle);
    public double AdditionalVoidVolume => Math.PI * ConeDepth *
        (Math.Pow(CountersinkDiameter / 2d, 2) + CountersinkDiameter * Diameter / 4d - 2d * Math.Pow(Diameter / 2d, 2)) / 3d;
}
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
    string ProfilePlane, string Axis, string ReferenceDirection, bool IsExplicit)
{
    /// <summary>The bound operand frame, when placement is inherited from a Profile.</summary>
    public ConstructionPlane? ProfileConstructionPlane { get; init; }

    /// <summary>
    /// Resolves the legacy textual placement through the shared construction-plane
    /// authority. ReferenceDirection is the local profile X axis; local Y is derived
    /// by the right-handed frame law rather than by world-plane special cases.
    /// </summary>
    public bool TryResolveConstructionPlane(out ConstructionPlane? frame, out string? diagnostic)
    {
        frame = null;
        diagnostic = null;
        if (ProfileConstructionPlane is not null)
        {
            frame = ProfileConstructionPlane;
            return true;
        }
        if (!TryAxis(Axis, out var normal) || !TryAxis(ReferenceDirection, out var reference))
        {
            diagnostic = $"compose-placement-invalid-axis:{Name}:axis={Axis}:reference={ReferenceDirection}";
            return false;
        }
        if (Math.Abs(normal.Dot(reference)) > 1e-12d)
        {
            diagnostic = $"compose-placement-reference-parallel-to-axis:{Name}:axis={Axis}:reference={ReferenceDirection}";
            return false;
        }
        var expectedNormal = ProfilePlane switch
        {
            "XY" => new Vector3D(0d, 0d, 1d),
            "XZ" => new Vector3D(0d, 1d, 0d),
            "YZ" => new Vector3D(1d, 0d, 0d),
            _ => default
        };
        if (expectedNormal.LengthSquared == 0d || Math.Abs(Math.Abs(normal.Dot(expectedNormal)) - 1d) > 1e-12d)
        {
            diagnostic = $"compose-placement-plane-axis-mismatch:{Name}:plane={ProfilePlane}:axis={Axis}";
            return false;
        }
        var axisZ = Direction3D.Create(normal);
        var axisX = Direction3D.Create(reference);
        var axisY = Direction3D.Create(normal.Cross(reference));
        frame = new ConstructionPlane(
            $"construction:compose:{Name}", $"placement:{Name}",
            new Point3D(AnchorX, AnchorY, AnchorZ), axisX, axisY, axisZ,
            "PrismaticProfilePlacement", "ComposePlacementToConstructionPlane");
        return true;
    }

    private static bool TryAxis(string value, out Vector3D axis)
    {
        axis = value switch
        {
            "+X" => new Vector3D(1d, 0d, 0d), "-X" => new Vector3D(-1d, 0d, 0d),
            "+Y" => new Vector3D(0d, 1d, 0d), "-Y" => new Vector3D(0d, -1d, 0d),
            "+Z" => new Vector3D(0d, 0d, 1d), "-Z" => new Vector3D(0d, 0d, -1d),
            _ => default
        };
        return axis.LengthSquared > 0d;
    }
}
public sealed record PrismaticProfileCompositionFeature(
    string Name, string Frame, string Axis, PrismaticProfilePlacement Placement, IReadOnlyList<PrismaticProfileOperation> Operations,
    IReadOnlyList<double> CriticalLevels, string Provenance, IReadOnlyList<PrismaticShaftHoleFeature>? ShaftHoles = null,
    IReadOnlyList<PrismaticCapsuleSlotFeature>? CapsuleSlots = null,
    IReadOnlyList<PrismaticRoundedRectangleSlotFeature>? RoundedRectangleSlots = null,
    IReadOnlyList<PrismaticConstructionPlaneBlindDrillFeature>? ConstructionPlaneBlindDrills = null,
    IReadOnlyList<PrismaticCounterboreHoleFeature>? CounterboreHoles = null,
    IReadOnlyList<PrismaticBossFeature>? Bosses = null,
    IReadOnlyList<PrismaticPocketFeature>? Pockets = null,
    IReadOnlyList<PrismaticMaterialOffsetFeature>? MaterialOffsets = null,
    IReadOnlyList<PrismaticCountersinkHoleFeature>? CountersinkHoles = null)
{
    public IEnumerable<(string Name, string StableId, string ProfileReference)> AllSlotProfiles =>
        (CapsuleSlots ?? []).Select(x => (x.Name, x.StableId, x.ProfileReference))
            .Concat((RoundedRectangleSlots ?? []).Select(x => (x.Name, x.StableId, x.ProfileReference)));
}
public sealed record PrismaticSectionRegion(
    ResolvedProfile2D Outer, IReadOnlyList<ResolvedProfile2D> Holes, IReadOnlyList<string> Provenance);
public sealed record PrismaticSectionSlab(
    double From, double To, PrismaticSectionRegion Region, IReadOnlyList<string> ActiveOperations,
    ProfileArrangement2D? Arrangement = null,
    IReadOnlyList<PrismaticSectionRegion>? RegionSet = null)
{
    public IReadOnlyList<PrismaticSectionRegion> MaterialRegions => RegionSet ?? [Region];
}
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
    private static readonly Regex ModifyHead = new(@"\bModify\s+(?<target>\w+)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex OffsetHeader = new(@"\b(?<intent>AddOffset|RemoveOffset)\s*<\s*(?<tool>[A-Za-z_]\w*)\s*>\s+(?<n>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex OffsetTarget = new(@"\bTarget\s*:\s*(?<value>[A-Za-z_]\w*)", RegexOptions.CultureInvariant);
    private static readonly Regex OffsetDirection = new(@"\b(?:Along|Axis|Direction)\s*:\s*(?<value>[+-][XYZ])", RegexOptions.CultureInvariant);
    private static readonly Regex OffsetThroughAll = new(@"\b(?:Termination|Extent)\s*:\s*ThroughAll\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SemanticProfile = new(@"\bProfile\s*:\s*(?<profile>\w+)", RegexOptions.CultureInvariant);
    private static readonly Regex SemanticSupport = new(@"\bOn\s*:\s*(?<face>[A-Za-z_]\w*(?:\.Top)?|[+-][XYZ])", RegexOptions.CultureInvariant);
    private static readonly Regex BossHeight = new(@"\bHeight\s*:\s*(?<value>[-+.\d]+)mm", RegexOptions.CultureInvariant);
    private static readonly Regex PocketDepth = new(@"\bDepth\s*:\s*(?<value>[-+.\d]+)mm", RegexOptions.CultureInvariant);
    private static readonly Regex PocketMinimumFloor = new(@"\b[Mm]inimumFloorThickness\s*:\s*(?<value>[-+.\d]+)mm", RegexOptions.CultureInvariant);
    private static readonly Regex TemplateMinimumFloor = new(@"\bconcept\s+(?<name>minimumFloorThickness|minimumWallThickness)\s*:\s*(?<value>[-+.\d]+)\s*mm", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex CncPolicyHead = new(@"\bConcept\s+Struct\s+(?<name>[A-Za-z_]\w*)\s*:\s*CncManufacturingPolicy\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex HoleHeader = new(@"\bHole\s*<\s*(?<variant>\w+)\s*>\s+(?<n>\w+)\s*\{", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleCenter = new(@"\bCenter\s*:\s*\[(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\]", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HolePoint2Center = new(@"\bCenter\s*:\s*Point2\s*\(\s*(?<x>[-+.\d]+)mm\s*,\s*(?<y>[-+.\d]+)mm\s*\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleDiameter = new(@"\bDiameter\s*:\s*(?<d>[-+.\d]+)mm", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleCounterboreDiameter = new(@"\bCounterboreDiameter\s*:\s*(?<d>[-+.\d]+)mm", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleCounterboreDepth = new(@"\bCounterboreDepth\s*:\s*(?<d>[-+.\d]+)mm", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex HoleOn = new(@"\bOn\s*:\s*(?<face>[+-][XYZ]|[A-Za-z_]\w*)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
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

    public static bool IsCompositionSource(string source) => FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document).Any(d => d.Type.Name.Text == "Compose");

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
            // Pipeline authoring is normalized by ProfileAuthoringParser below and
            // enters Compose through the ordinary ProfileCapability boundary.
            if (body.Contains("|>", StringComparison.Ordinal)) continue;
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
            var frame = ProfileAuthoringParser.ResolveNamedConstructionPlane(source, header.Groups["layout"].Value, diagnostics);
            var profile = new ResolvedProfile2D(header.Groups["n"].Value, "XY", [new ResolvedProfileLoop2D("Outer", true, segments)], frame);
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

        var syntax = FirmamentSyntaxParser.Parse(source);
        // Low-level Profiles are ordinary operands too. Preserve their owner's
        // endpoint/frame errors rather than replacing them with a missing-profile error.
        foreach (var declaration in FirmamentSyntaxTraversal.Declarations(syntax.Document).Where(d => d.Type.Name.Text == "Profile" && d.Name is not null))
        {
            if (profiles.ContainsKey(declaration.Name!.Text)) continue;
            var resolved = ProfileAuthoringParser.ResolveNamedProfile(source, declaration.Name.Text, out var reported);
            diagnostics.AddRange(reported);
            if (resolved is not null && reported.Count == 0) profiles.Add(declaration.Name.Text, resolved);
        }
        if (diagnostics.Count > 0) return new(null, profiles, diagnostics.Distinct(StringComparer.Ordinal).ToArray());
        var authoredProfiles = profiles.Keys.ToHashSet(StringComparer.Ordinal);
        var compose = FirmamentSyntaxTraversal.Declarations(syntax.Document).FirstOrDefault(d => d.Type.Name.Text == "Compose");
        if (compose?.Body is null || compose.Name is null) return new(null, profiles, diagnostics.Append("compose-source-missing-compose").ToArray());
        if (compose.Clauses.Count > 0 || compose.Type.Arguments.Count > 0)
            return new(null, profiles, diagnostics.Append("compose-header-unsupported: Declare a supported Placement block inside Compose.").ToArray());
        var composeBody = source.Substring(compose.Body.Span.Start + 1, compose.Body.Span.Length - 2);
        composeBody = BindExternalModifyFeatures(source, compose.Name.Text, composeBody, diagnostics);
        var beforeAdmission = diagnostics.Count;
        var operationsSyntax = FirmamentSyntaxParser.Parse(composeBody);
        diagnostics.AddRange(operationsSyntax.Diagnostics.Select(d => d.Code + ":" + d.Message));
        foreach (var declaration in operationsSyntax.Document.Statements.OfType<DeclarationSyntax>())
            foreach (var repeated in (declaration.Body?.Statements.OfType<FieldSyntax>() ?? []).GroupBy(f => FirmamentSourceSpelling.PreferredField(f.Name.Text), StringComparer.Ordinal).Where(g => g.Count() > 1))
                diagnostics.Add($"firmament-v2-field-duplicate:{declaration.Type.Name.Text}:{repeated.Key}");
        foreach (var statement in operationsSyntax.Document.Statements)
        {
            if (statement is DeclarationSyntax declared && declared.Type.Name.Text is
                "Base" or "Add" or "Remove" or "Boss" or "Pocket" or "AddOffset" or "RemoveOffset" or "Hole" or "Slot" or "EdgeFinish" or "Placement" or "Point2" or "Line2" or "Circle2" or "Path" or "Profile" or "Rect2" or "Axis2" or "Plane") continue;
            diagnostics.Add($"{FirmamentV2Parser.CanonicalDeclarationUnknown}:Compose:{(statement as DeclarationSyntax)?.Type.Name.Text ?? "expression"}");
        }
        if (diagnostics.Count > beforeAdmission) return new(null, profiles, diagnostics);
        var spanInspection = ProfileAuthoringParser.InspectGeometricSpans(source);
        diagnostics.AddRange(spanInspection.Diagnostics);
        var surfaceSpans = spanInspection.Spans.Where(span => span.SpanType == "Plane").ToDictionary(span => span.SpanId, StringComparer.Ordinal);
        var placements = operationsSyntax.Document.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Placement").ToArray();
        var placement = new PrismaticProfilePlacement("LegacyImplicitWorldXY", 0d, 0d, 0d, "XY", "+Z", "+X", false);
        if (placements.Length > 1) diagnostics.Add("compose-placement-multiple-unsupported");
        if (placements.FirstOrDefault() is { } authoredPlacement)
        {
            var fields = new FirmamentSyntaxFields(authoredPlacement, diagnostics);
            var anchorValid = fields.Vector("Anchor", "mm", out var anchor) && anchor.Length == 3;
            var plane = fields.Word("ProfilePlane"); var axis = fields.Word("Axis"); var reference = fields.Word("ReferenceDirection");
            fields.Complete();
            if (!anchorValid || plane is null || axis is null || reference is null) diagnostics.Add("compose-placement-fields-invalid");
            else
            {
                placement = new(authoredPlacement.Name?.Text ?? "", anchor[0], anchor[1], anchor[2], plane, axis, reference, true);
                if (!placement.TryResolveConstructionPlane(out _, out var placementDiagnostic)) diagnostics.Add(placementDiagnostic!);
            }
        }
        var operations = new List<PrismaticProfileOperation>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in operationsSyntax.Document.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text is "Base" or "Add" or "Remove"))
        {
            var fields = new FirmamentSyntaxFields(declaration, diagnostics);
            var name = declaration.Name?.Text ?? "";
            var profile = fields.Word("Profile") ?? "";
            var from = fields.Number("From", "mm"); var to = fields.Number("To", "mm");
            var role = fields.Word("Role") ?? declaration.Type.Name.Text;
            fields.Complete();
            if (!names.Add(name)) diagnostics.Add($"compose-duplicate-operation:{name}");
            if (!profiles.ContainsKey(profile)) diagnostics.Add($"ConceptPathProfileNotAdmittedByCompose:{name}:{profile}");
            if (!double.IsFinite(from) || !double.IsFinite(to) || from >= to) diagnostics.Add($"compose-invalid-interval:{name}:{from:R}:{to:R}");
            operations.Add(new(name, Enum.Parse<PrismaticProfileIntent>(declaration.Type.Name.Text), profile, from, to, role, $"offset:{declaration.Span.Start}"));
        }
        var baseOperations = operations.Where(o => o.Intent == PrismaticProfileIntent.Base).ToArray();
        if (baseOperations.Length != 1)
            diagnostics.Add($"compose-role-cardinality:Base:expected=1:actual={baseOperations.Length}");
        var bosses = new List<PrismaticBossFeature>();
        var pockets = new List<PrismaticPocketFeature>();
        var materialOffsets = new List<PrismaticMaterialOffsetFeature>();
        // Cardinality is authored semantics, not a LINQ assertion. Never call
        // Single/SingleOrDefault while malformed input can still reach this boundary.
        var stock = baseOperations.Length == 1 ? baseOperations[0] : null;
        if (stock is not null && profiles.TryGetValue(stock.ProfileReference, out var stockProfile) && !placement.IsExplicit
            && !SameFrame(stockProfile.EffectiveConstructionPlane, ConstructionPlane.WorldXY))
        {
            placement = placement with { ProfileConstructionPlane = stockProfile.EffectiveConstructionPlane };
            // These adapters bind supports in world coordinates. Do not let them
            // reinterpret a local +Z selector as world +Z on an oriented body.
            foreach (var dependent in operationsSyntax.Document.Statements.OfType<DeclarationSyntax>()
                .Where(d => d.Type.Name.Text is "Boss" or "Pocket" or "Hole" or "Slot" or "EdgeFinish"))
                diagnostics.Add($"compose-oriented-feature-not-admitted:{dependent.Type.Name.Text}:{dependent.Name?.Text}: Oriented Compose currently admits Base/Add/Remove in one common Profile frame; this feature's support adapter requires world XY.");
        }
        if (stock is not null)
        {
            foreach (var declaration in operationsSyntax.Document.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Boss"))
            {
                var name = declaration.Name?.Text ?? "";
                var fields = new FirmamentSyntaxFields(declaration, diagnostics);
                var profileName = fields.Word("Profile"); var face = fields.Word("On"); var height = fields.Number("Height", "mm");
                fields.Complete();
                if (profileName is null || face is null || !double.IsFinite(height)) { diagnostics.Add($"firmament-boss-missing-required-field:{name}:required=On,Profile,Height"); continue; }
                if (!names.Add(name)) { diagnostics.Add($"compose-duplicate-operation:{name}"); continue; }
                if (!profiles.TryGetValue(profileName, out var bossProfile)) { diagnostics.Add($"firmament-boss-invalid-profile:{name}:profile={profileName}"); continue; }
                if (!double.IsFinite(height) || height <= 0d) { diagnostics.Add($"firmament-boss-height-must-be-positive:{name}:height={height:R}mm"); continue; }
                surfaceSpans.TryGetValue(face, out var supportSpan);
                var authoredPlane = Regex.Match(source, $@"\bPlane\s+{Regex.Escape(face)}\s*\{{\s*Origin\s*:\s*\[(?<origin>[^]]+)\]\s*;?\s*Normal\s*:\s*\[(?<normal>[^]]+)\](?:\s*;?\s*Up\s*:\s*\[(?<up>[^]]+)\])?", RegexOptions.CultureInvariant);
                if (supportSpan is null && !IsTopSelector(face) && !authoredPlane.Success) { diagnostics.Add($"firmament-boss-invalid-target:{name}:On={face}:supported=Top-or-<feature>.Top-or-Span<Plane>"); continue; }
                if (supportSpan is not null && !IsProfileFootprintInsideSpan(supportSpan, bossProfile, profiles))
                { diagnostics.Add($"firmament-feature-footprint-outside-span:{name}:{face}:profile={profileName}"); continue; }
                var candidates = supportSpan is null
                    ? authoredPlane.Success
                        ? ResolvePlaneTopSupports(authoredPlane, name, operations, bossProfile, profiles, diagnostics)
                        : ResolveTopSupports(face, stock, operations, bossProfile, profiles)
                    : ResolveSpanTopSupports(supportSpan, operations, bossProfile, profiles);
                if (candidates.Count == 0) { diagnostics.Add(face.EndsWith(".Top", StringComparison.Ordinal)
                    ? $"feature-support-not-found:{name}:{face}:host={compose.Name.Text}:profile={profileName}"
                    : $"firmament-boss-disconnected-from-host:{name}:host={compose.Name.Text}:profile={profileName}:support=Top"); continue; }
                if (candidates.Count > 1) { diagnostics.Add($"feature-support-ambiguous:Top:feature={name}:candidates={string.Join(",", candidates.Select(x => x.Name).Order(StringComparer.Ordinal))}"); continue; }
                var selectedSupport = candidates[0];
                var stableId = $"boss:{compose.Name.Text}.{name}"; var sourceSpan = $"offset:{declaration.Span.Start}";
                bosses.Add(new(name, stableId, compose.Name.Text, face, profileName, height, selectedSupport.To, selectedSupport.To + height, sourceSpan, supportSpan?.SpanId, supportSpan?.ParentId));
                operations.Add(new(name, PrismaticProfileIntent.Add, profileName, selectedSupport.To, selectedSupport.To + height, "Boss", sourceSpan, stableId, "Boss"));
            }

            foreach (var declaration in operationsSyntax.Document.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Pocket"))
            {
                var name = declaration.Name?.Text ?? "";
                var fields = new FirmamentSyntaxFields(declaration, diagnostics);
                var profileName = fields.Word("Profile"); var face = fields.Word("On"); var depthExpression = fields.Take("Depth");
                var depth = FirmamentSyntaxValues.TryMeasure(depthExpression, "mm", out var measuredDepth) ? measuredDepth : double.NaN;
                var end = fields.Word("End");
                var minimumFloorExpression = fields.Take("MinimumFloorThickness");
                var through = end == "ThroughAll";
                if (end is not null && !through || through && (depthExpression is not null || minimumFloorExpression is not null))
                { diagnostics.Add($"firmament-pocket-termination-conflict:{name}: Use End: ThroughAll alone, or Depth with a remaining floor."); continue; }
                fields.Complete();
                if (profileName is null || face is null || !through && !double.IsFinite(depth)) { diagnostics.Add($"firmament-pocket-missing-required-field:{name}:required=On,Profile,Depth-or-End-ThroughAll"); continue; }
                var body = composeBody.Substring(declaration.Body!.Span.Start + 1, declaration.Body.Span.Length - 2);
                if (!names.Add(name)) { diagnostics.Add($"compose-duplicate-operation:{name}"); continue; }
                if (!profiles.TryGetValue(profileName, out var pocketProfile)) { diagnostics.Add($"firmament-pocket-invalid-profile:{name}:profile={profileName}"); continue; }
                if (!through && (!double.IsFinite(depth) || depth <= 0d)) { diagnostics.Add($"firmament-pocket-depth-must-be-positive:{name}:depth={depth:R}mm"); continue; }
                surfaceSpans.TryGetValue(face, out var supportSpan);
                if (supportSpan is null && !IsTopSelector(face)) { diagnostics.Add($"firmament-pocket-invalid-target:{name}:On={face}:supported=Top-or-<feature>.Top-or-Span<Plane>"); continue; }
                if (supportSpan is not null && !IsProfileFootprintInsideSpan(supportSpan, pocketProfile, profiles))
                { diagnostics.Add($"firmament-feature-footprint-outside-span:{name}:{face}:profile={profileName}"); continue; }
                var candidates = supportSpan is null
                    ? ResolveTopSupports(face, stock, operations, pocketProfile, profiles, requireContainment: true)
                    : ResolveSpanTopSupports(supportSpan, operations, pocketProfile, profiles);
                if (candidates.Count == 0) { diagnostics.Add($"feature-support-not-found:{name}:{face}:profile={profileName}:footprint-must-be-enclosed-by-support"); continue; }
                if (candidates.Count > 1) { diagnostics.Add($"feature-support-ambiguous:Top:feature={name}:candidates={string.Join(",", candidates.Select(x => x.Name).Order(StringComparer.Ordinal))}"); continue; }
                var selectedSupport = candidates[0];
                if (through)
                {
                    operations.Add(new(name, PrismaticProfileIntent.Remove, profileName, stock.From, selectedSupport.To, "ThroughCut", $"offset:{declaration.Span.Start}", SemanticFeatureKind: "Pocket"));
                    continue;
                }
                var hostThickness = selectedSupport.To - selectedSupport.From; var remaining = hostThickness - depth;
                var (minimumFloor, policySource) = ResolveMinimumFloor(source, body);
                if (!double.IsFinite(minimumFloor) || minimumFloor <= 0d)
                {
                    diagnostics.Add($"firmament-pocket-minimum-floor-policy-invalid:{name}:requiredFloor={minimumFloor:R}mm:policy={policySource}");
                    continue;
                }
                if (remaining <= 1e-7d)
                {
                    diagnostics.Add($"firmament-pocket-through-depth:{name}:host={compose.Name.Text}:depth={depth:R}mm:hostThickness={hostThickness:R}mm:remainingFloor={remaining:R}mm:requiredFloor={minimumFloor:R}mm:Use Remove with Role: ThroughCut for a through cut; Pocket requires a remaining floor.");
                    continue;
                }
                if (remaining + 1e-7d < minimumFloor)
                {
                    diagnostics.Add($"firmament-pocket-minimum-floor-thickness:{name}:host={compose.Name.Text}:depth={depth:R}mm:hostThickness={hostThickness:R}mm:remainingFloor={remaining:R}mm:requiredFloor={minimumFloor:R}mm:policy={policySource}");
                    continue;
                }
                var stableId = $"pocket:{compose.Name.Text}.{name}"; var sourceSpan = $"offset:{declaration.Span.Start}";
                pockets.Add(new(name, stableId, compose.Name.Text, face, profileName, depth, hostThickness, remaining, minimumFloor, policySource, selectedSupport.To - depth, selectedSupport.To, sourceSpan, supportSpan?.SpanId, supportSpan?.ParentId));
                operations.Add(new(name, PrismaticProfileIntent.Remove, profileName, selectedSupport.To - depth, selectedSupport.To, "Pocket", sourceSpan, stableId, "Pocket"));
            }

            foreach (Match header in OffsetHeader.Matches(composeBody))
            {
                var name = header.Groups["n"].Value;
                var intentText = header.Groups["intent"].Value;
                var toolFamily = header.Groups["tool"].Value;
                var intent = intentText == "AddOffset" ? PrismaticProfileIntent.Add : PrismaticProfileIntent.Remove;
                var body = Block(composeBody, header.Index + header.Length - 1);
                if (body is null) { diagnostics.Add($"offset-malformed:{name}:unclosed"); continue; }
                if (!string.Equals(toolFamily, "Prism", StringComparison.Ordinal))
                { diagnostics.Add($"offset-tool-family-not-qualified:{name}:tool={toolFamily}:qualified=Prism"); continue; }

                var authored = operationsSyntax.Document.Statements.OfType<DeclarationSyntax>().Single(d => d.Name?.Text == name && d.Type.Name.Text == intentText);
                var fields = new FirmamentSyntaxFields(authored, diagnostics);
                var targetMatch = ReadFeatureField(fields, composeBody, OffsetTarget, "Target");
                var profileMatch = ReadFeatureField(fields, composeBody, SemanticProfile, "Profile");
                var supportMatch = ReadFeatureField(fields, composeBody, SemanticSupport, "On");
                var directions = new[] { "Along", "Axis", "Direction" }.Select(n => ReadFeatureField(fields, composeBody, OffsetDirection, n)).Where(m => m.Success).ToArray();
                var directionMatch = directions.FirstOrDefault() ?? Match.Empty;
                if (directions.Length > 1) diagnostics.Add($"offset-direction-conflict:{name}");
                var heightMatch = ReadFeatureField(fields, composeBody, BossHeight, "Height");
                var depthMatch = ReadFeatureField(fields, composeBody, PocketDepth, "Depth");
                var terminations = new[] { "Termination", "Extent" }.Select(n => ReadFeatureField(fields, composeBody, OffsetThroughAll, n)).Where(m => m.Success).ToArray();
                var throughAll = terminations.Length > 0;
                if (terminations.Length > 1) diagnostics.Add($"offset-termination-conflict:{name}");
                fields.Complete();
                if (!targetMatch.Success || !profileMatch.Success || !supportMatch.Success)
                { diagnostics.Add($"offset-missing-required-field:{name}:required=Target,On,Profile"); continue; }
                var target = targetMatch.Groups["value"].Value;
                var profileName = profileMatch.Groups["profile"].Value;
                var support = supportMatch.Groups["face"].Value;
                var direction = directionMatch.Success ? directionMatch.Groups["value"].Value : "+Z";
                if (!string.Equals(target, compose.Name.Text, StringComparison.Ordinal))
                { diagnostics.Add($"offset-target-not-found:{name}:target={target}:active={compose.Name.Text}"); continue; }
                if (!string.Equals(direction, "+Z", StringComparison.Ordinal))
                { diagnostics.Add($"offset-axis-invalid:{name}:axis={direction}:qualified=+Z-prismatic-compose"); continue; }
                if (!names.Add(name)) { diagnostics.Add($"compose-duplicate-operation:{name}"); continue; }
                if (!profiles.TryGetValue(profileName, out var offsetProfile))
                { diagnostics.Add($"offset-profile-not-found:{name}:profile={profileName}"); continue; }
                var profileArea = Math.Abs(PrismaticSectionStackCompiler.ProfileArea(offsetProfile));
                if (!double.IsFinite(profileArea) || profileArea <= 1e-9d)
                { diagnostics.Add($"offset-profile-zero-area:{name}:profile={profileName}"); continue; }
                if (offsetProfile.Loops.SelectMany(loop => loop.Segments)
                    .Any(segment => segment.Geometry is not (LineArcLineSegment2D or LineArcCircularArc2D)))
                { diagnostics.Add($"offset-profile-curve-not-qualified:{name}:profile={profileName}:qualified=line,circular-arc"); continue; }

                surfaceSpans.TryGetValue(support, out var supportSpan);
                if (supportSpan is null && !IsTopSelector(support))
                { diagnostics.Add($"offset-support-invalid:{name}:On={support}:qualified=Top-or-<feature>.Top-or-Span<Plane>"); continue; }
                // A Span is an authored support contract. Both operations fail
                // closed if their tool footprint escapes it. Edge notches remain
                // available by naming the whole Top support.
                if (supportSpan is not null && !IsProfileFootprintInsideSpan(supportSpan, offsetProfile, profiles))
                { diagnostics.Add($"offset-support-invalid:{name}:profile={profileName}:outside-span={supportSpan.SpanId}"); continue; }
                var candidates = supportSpan is null
                    ? ResolveTopSupports(support, stock, operations, offsetProfile, profiles)
                    : ResolveSpanTopSupportsForOffset(supportSpan, operations, offsetProfile, profiles);
                if (candidates.Count == 0)
                {
                    var tangent = operations.Where(operation => operation.Intent != PrismaticProfileIntent.Remove)
                        .Any(operation => profiles.TryGetValue(operation.ProfileReference, out var supportProfile)
                            && ProfilesTouch(supportProfile, offsetProfile));
                    diagnostics.Add(tangent
                        ? $"offset-tangent-contact:{name}:target={target}:profile={profileName}"
                        : intent == PrismaticProfileIntent.Add
                            ? $"offset-add-disconnected:{name}:target={target}:profile={profileName}"
                            : $"offset-no-intersection:{name}:target={target}:profile={profileName}");
                    continue;
                }
                if (candidates.Count > 1)
                { diagnostics.Add($"offset-support-ambiguous:{name}:candidates={string.Join(",", candidates.Select(x => x.Name).Order(StringComparer.Ordinal))}"); continue; }
                var selectedSupport = candidates[0];

                double extent;
                string termination;
                double from;
                double to;
                if (intent == PrismaticProfileIntent.Add)
                {
                    if (!heightMatch.Success || throughAll || depthMatch.Success)
                    { diagnostics.Add($"offset-missing-required-field:{name}:AddOffset<Prism>-requires=Height"); continue; }
                    extent = N(heightMatch, "value");
                    if (!double.IsFinite(extent) || extent <= 0d)
                    { diagnostics.Add($"offset-tool-zero-dimension:{name}:Height={extent:R}mm"); continue; }
                    termination = "Height";
                    from = selectedSupport.To;
                    to = from + extent;
                }
                else
                {
                    if (throughAll == depthMatch.Success)
                    { diagnostics.Add($"offset-missing-required-field:{name}:RemoveOffset<Prism>-requires-exactly-one-of=Depth,Termination:ThroughAll"); continue; }
                    to = selectedSupport.To;
                    if (throughAll)
                    {
                        termination = "ThroughAll";
                        from = operations.Where(operation => operation.Intent != PrismaticProfileIntent.Remove).Min(operation => operation.From);
                        extent = to - from;
                    }
                    else
                    {
                        extent = N(depthMatch, "value");
                        if (!double.IsFinite(extent) || extent <= 0d)
                        { diagnostics.Add($"offset-tool-zero-dimension:{name}:Depth={extent:R}mm"); continue; }
                        termination = "Depth";
                        from = to - extent;
                    }
                }
                if (!(from < to)) { diagnostics.Add($"offset-tool-zero-dimension:{name}:extent={extent:R}mm"); continue; }
                if (intent == PrismaticProfileIntent.Remove && throughAll)
                {
                    if (ToolContainsProfile(offsetProfile, profiles[stock.ProfileReference]))
                    { diagnostics.Add($"offset-remove-entire-body:{name}:target={target}"); continue; }
                    var splitProbe = ProfileArrangementBuilder.Compose("XY",
                        [stock with { From = 0d, To = 1d }, new(name, PrismaticProfileIntent.Remove, profileName, 0d, 1d, "MaterialOffset", $"offset:{header.Index}")],
                        profiles, $"offset-split-probe:{name}");
                    if (splitProbe.Region is null || splitProbe.Arrangement.Diagnostics.Any(item => item.Contains("disconnected", StringComparison.OrdinalIgnoreCase)))
                    { diagnostics.Add($"offset-remove-splits-body:{name}:target={target}"); continue; }
                }

                var bounds = ProfileBounds(offsetProfile);
                var stableId = $"offset:{compose.Name.Text}.{name}";
                var sourceSpan = $"offset:{header.Index}";
                materialOffsets.Add(new(name, stableId, intent, "Prism", target, support, profileName, direction,
                    termination, from, to, extent, sourceSpan,
                    [bounds.MinX, bounds.MinY, from, bounds.MaxX, bounds.MaxY, to], supportSpan?.SpanId, supportSpan?.ParentId));
                operations.Add(new(name, intent, profileName, from, to, "MaterialOffset", sourceSpan, stableId, $"{intentText}<Prism>"));
            }
        }
        var shaftHoles = new List<PrismaticShaftHoleFeature>();
        var counterboreHoles = new List<PrismaticCounterboreHoleFeature>();
        var countersinkHoles = new List<PrismaticCountersinkHoleFeature>();
        var constructionPlaneBlindDrills = new List<PrismaticConstructionPlaneBlindDrillFeature>();
        var capsuleSlots = new List<PrismaticCapsuleSlotFeature>();
        var roundedRectangleSlots = new List<PrismaticRoundedRectangleSlotFeature>();
        var materialFrom = operations.Count == 0 ? 0d : operations.Min(x => x.From);
        var materialTo = operations.Count == 0 ? 0d : operations.Max(x => x.To);
        var circularHostOperations = operations.ToArray();
        PrismaticSectionStackConstruction? circularHost = null;
        bool TryCircularHostInterval(string name, double x, double y, double entryRadius, string diagnostic, out double start, out double end)
        {
            start = end = 0d;
            // Normalize once before drilling. The semantic top is local to the
            // authored center, not the height of an unrelated boss elsewhere.
            if (circularHost is null)
            {
                var levels = circularHostOperations.SelectMany(o => new[] { o.From, o.To }).Distinct().Order().ToArray();
                var host = new PrismaticProfileCompositionFeature(compose.Name.Text, placement.ProfilePlane, placement.Axis, placement,
                    circularHostOperations, levels, "CircularCavityHost");
                circularHost = PrismaticSectionStackCompiler.Normalize(new(host, profiles, []), out var hostDiagnostics);
                diagnostics.AddRange(hostDiagnostics);
            }
            var intervals = circularHost?.Slabs.Where(s => s.MaterialRegions.Any(r =>
                PrismaticSectionStackHoleTraversal.CheckDisk(r, (x, y), 0d, s.From, s.To).Classification == SectionStackFootprintClassification.InsideMaterial)).OrderBy(s => s.From).ToArray() ?? [];
            if (intervals.Length == 0)
            { diagnostics.Add($"{diagnostic}:{name}:center-has-no-material-support"); return false; }
            if (intervals.Zip(intervals.Skip(1)).Any(p => Math.Abs(p.First.To - p.Second.From) > 1e-7d))
            { diagnostics.Add($"{diagnostic}:{name}:disconnected-axial-material-spans-not-admitted"); return false; }
            start = intervals[0].From; end = intervals[^1].To;
            var entryLevel = end;
            var exposed = circularHost!.Transitions.Where(t => Math.Abs(t.Level - entryLevel) < 1e-7d).SelectMany(t => t.UpwardRegions);
            if (!exposed.Any(r => PrismaticSectionStackHoleTraversal.CheckDisk(r, (x, y), entryRadius, entryLevel, entryLevel).Classification == SectionStackFootprintClassification.InsideMaterial))
            { diagnostics.Add($"{diagnostic}:{name}:entry-footprint-is-not-contained-in-the-exposed-local-support:radius={entryRadius:R}"); return false; }
            return true;
        }
        foreach (var declaration in operationsSyntax.Document.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Hole"))
        {
            var name = declaration.Name?.Text ?? "";
            var fields = new FirmamentSyntaxFields(declaration, diagnostics);
            var body = declaration.Body is null ? null : composeBody.Substring(declaration.Body.Span.Start + 1, declaration.Body.Span.Length - 2);
            if (body is null) { diagnostics.Add($"compose-hole-unclosed:{name}"); continue; }
            var variant = FirmamentSyntaxValues.Reference(declaration.Type.Arguments.FirstOrDefault()?.Value) ?? "";
            var schemaVariant = Enum.TryParse<FirmamentV2SemanticHoleVariant>(variant, true, out var knownVariant) ? knownVariant.ToString() : variant;
            foreach (var authored in fields.AuthoredFields)
                if (FirmamentSemanticSchemas.Get("Hole")!.Fields.FirstOrDefault(f => FirmamentSyntaxFields.SameField(authored.Name, f.Name)) is { } schemaField
                    && !schemaField.AppliesTo(schemaVariant))
                    diagnostics.Add($"{FirmamentV2Parser.CanonicalFieldUnknown}:Hole:{authored.Name}");
            fields.Take("PatternIdentity");
            var patternIdentityMatch = Regex.Match(body, @"\bPatternIdentity\s*:\s*(?<id>[A-Za-z_]\w*\.[A-Za-z_]\w*)", RegexOptions.CultureInvariant);
            var diagnosticName = patternIdentityMatch.Success ? patternIdentityMatch.Groups["id"].Value : name;
            var center = ReadFeatureField(fields, composeBody, HoleCenter, "Center"); if (!center.Success) center = ReadFeatureField(fields, composeBody, HolePoint2Center, "Center");
            var diameter = ReadFeatureField(fields, composeBody, HoleDiameter, "Diameter"); var end = ReadFeatureField(fields, composeBody, HoleEnd, "End"); var role = ReadFeatureField(fields, composeBody, HoleRole, "Role");
            var counterboreDiameter = ReadFeatureField(fields, composeBody, HoleCounterboreDiameter, "CounterboreDiameter"); var counterboreDepth = ReadFeatureField(fields, composeBody, HoleCounterboreDepth, "CounterboreDepth"); var onExpression = fields.Take("On");
            var onName = FirmamentV2Parser.MechanicalFaceAxis(onExpression, compose.Name.Text) ?? FirmamentSyntaxValues.Reference(onExpression);
            var on = HoleOn.Match("On: " + onName);
            if (onExpression is not null && (!on.Success || on.Groups["face"].Value != onName))
                diagnostics.Add($"compose-hole-support-invalid:{name}: Use a host face or an admitted Span<Plane>.");
            var from = ReadFeatureField(fields, composeBody, HoleFrom, "From"); var depthEnd = ReadFeatureField(fields, composeBody, HoleDepthEnd, "End"); var termination = ReadFeatureField(fields, composeBody, HoleTermination, "Termination");
            var countersinkDiameter = fields.Number("CountersinkDiameter", "mm");
            var countersinkAngle = fields.Number("CountersinkAngle", "deg");
            fields.Complete();
            if (from.Success)
            {
                if (!string.Equals((FirmamentSyntaxValues.Reference(declaration.Type.Arguments.FirstOrDefault()?.Value) ?? ""), "Shaft", StringComparison.OrdinalIgnoreCase)
                    || !depthEnd.Success || !termination.Success)
                { diagnostics.Add($"compose-construction-plane-blind-hole-invalid-contract:{name}"); continue; }
                if (!center.Success || !diameter.Success) { diagnostics.Add($"compose-construction-plane-blind-hole-missing-center-or-diameter:{name}"); continue; }
                var trace = ResolveConstructionPlaneTrace(source, from.Groups["plane"].Value);
                string? planeDiagnostic = null; string? frameDiagnostic = null;
                if (trace is null || !ConceptIrResolver.TryResolvePlane(source, trace, out var conceptPlane, out planeDiagnostic)
                    || conceptPlane is null
                    || !ConstructionPlane.TryTrace($"construction:{from.Groups["plane"].Value}", conceptPlane, $"offset:{declaration.Span.Start}", out var plane, out frameDiagnostic))
                { diagnostics.Add(planeDiagnostic ?? frameDiagnostic ?? $"compose-construction-plane-not-found:{from.Groups["plane"].Value}"); continue; }
                var localX = N(center, "x"); var localY = N(center, "y"); var localDiameter = N(diameter, "d"); var declaredDepth = N(depthEnd, "depth");
                var angle = termination.Groups["angle"].Success ? N(termination, "angle") : 118d;
                if (!double.IsFinite(localX) || !double.IsFinite(localY) || !double.IsFinite(localDiameter) || localDiameter <= 0d || !double.IsFinite(declaredDepth) || declaredDepth < 0d || !double.IsFinite(angle) || angle <= 0d || angle >= 180d)
                { diagnostics.Add($"compose-construction-plane-blind-hole-invalid-geometry:{name}"); continue; }
                if (!names.Add(name)) { diagnostics.Add($"compose-duplicate-operation:{name}"); continue; }
                var blindStableId = $"hole:{compose.Name.Text}.{name}";
                constructionPlaneBlindDrills.Add(new(name, blindStableId, plane!.StableId, plane.SourceConceptId, plane.Origin, plane.AxisX, plane.AxisY, plane.AxisZ,
                    localX, localY, localDiameter, declaredDepth, string.Equals(depthEnd.Groups["kind"].Value, "TotalDepth", StringComparison.OrdinalIgnoreCase), angle,
                    role.Success ? role.Groups["role"].Value : "BlindDrillPoint", $"offset:{declaration.Span.Start}", plane.Provenance));
                continue;
            }
            if (string.Equals(variant, "Countersink", StringComparison.OrdinalIgnoreCase))
            {
                if (!end.Success || !string.Equals(end.Groups["end"].Value, "ThroughAll", StringComparison.OrdinalIgnoreCase))
                { diagnostics.Add($"ComposeCountersinkEndUnsupported:{name}:supported=ThroughAll"); continue; }
                var supportName = on.Success ? on.Groups["face"].Value : null;
                surfaceSpans.TryGetValue(supportName ?? string.Empty, out var supportSpan);
                if (!on.Success || (!string.Equals(supportName, "+Z", StringComparison.OrdinalIgnoreCase) && supportSpan is null))
                { diagnostics.Add($"ComposeCountersinkAxisUnsupported:{name}:supported=+Z-or-Span<Plane>"); continue; }
                if (!center.Success || !diameter.Success)
                { diagnostics.Add($"ComposeCountersinkMissingRequiredField:{name}"); continue; }
                var cx = N(center, "x"); var cy = N(center, "y"); var shaftDiameter = N(diameter, "d");
                if (!double.IsFinite(cx) || !double.IsFinite(cy) || !double.IsFinite(shaftDiameter) || shaftDiameter <= 0d
                    || !double.IsFinite(countersinkDiameter) || countersinkDiameter <= shaftDiameter
                    || !double.IsFinite(countersinkAngle) || countersinkAngle <= 0d || countersinkAngle >= 180d)
                { diagnostics.Add($"ComposeCountersinkInvalidGeometry:{name}:require=positive-shaft-smaller-than-entry-and-angle-between-0-and-180deg"); continue; }
                var stable = $"hole:{compose.Name.Text}.{name}"; var profile = $"{name}ShaftProfile";
                var sinkSourceSpan = $"offset:{declaration.Span.Start}"; var sinkRole = role.Success ? role.Groups["role"].Value : "CountersinkHole";
                if (!TryCircularHostInterval(name, cx, cy, countersinkDiameter / 2d, "ComposeCountersinkFootprintLeavesProfileMaterial", out var sinkFrom, out var sinkTo)) continue;
                var hole = new PrismaticCountersinkHoleFeature(name, stable, profile, cx, cy, shaftDiameter, countersinkDiameter, countersinkAngle,
                    sinkFrom, sinkTo, sinkRole, sinkSourceSpan, supportSpan?.SpanId, supportSpan?.ParentId);
                if (!double.IsFinite(hole.ConeDepth) || hole.ConeFrom <= sinkFrom + 1e-8d)
                { diagnostics.Add($"ComposeCountersinkDepthExceedsHost:{name}:coneDepth={hole.ConeDepth:R}:hostDepth={sinkTo - sinkFrom:R}:require=positive-shaft-length"); continue; }
                if (supportSpan is not null)
                {
                    var normal = supportSpan.ParentPlane!.AxisZ.ToVector();
                    if (Math.Abs(normal.X) > 1e-8d || Math.Abs(normal.Y) > 1e-8d || normal.Z < 1d - 1e-8d
                        || Math.Abs(supportSpan.ParentPlane.Origin.Z - sinkTo) > 1e-7d)
                    { diagnostics.Add($"firmament-feature-support-parent-mismatch:{name}:{supportSpan.SpanId}"); continue; }
                    if (!PlanarSpanContainment.ContainsCircle(supportSpan, cx, cy, countersinkDiameter / 2d, out var margin))
                    { diagnostics.Add($"firmament-feature-footprint-outside-span:{diagnosticName}:{supportSpan.SpanId}:radius={countersinkDiameter / 2d:R}:margin={margin:R}"); continue; }
                    hole = hole with { SupportBoundaryMargin = margin };
                }
                if (!names.Add(name)) { diagnostics.Add($"compose-duplicate-operation:{name}"); continue; }
                if (profiles.ContainsKey(profile)) { diagnostics.Add($"compose-hole-profile-name-collision:{name}"); continue; }
                profiles.Add(profile, CircleProfile(profile, cx, cy, shaftDiameter / 2d, stable, sinkSourceSpan));
                countersinkHoles.Add(hole);
                operations.Add(new(name, PrismaticProfileIntent.Remove, profile, sinkFrom, sinkTo, sinkRole, sinkSourceSpan, stable, "Hole<Countersink>", shaftDiameter));
                continue;
            }
            if (string.Equals(variant, "Counterbore", StringComparison.OrdinalIgnoreCase))
            {
                if (!end.Success || !string.Equals(end.Groups["end"].Value, "ThroughAll", StringComparison.OrdinalIgnoreCase))
                { diagnostics.Add($"ComposeCounterboreEndUnsupported:{name}:supported=ThroughAll"); continue; }
                var supportName = on.Success ? on.Groups["face"].Value : null;
                surfaceSpans.TryGetValue(supportName ?? string.Empty, out var supportSpan);
                if (!on.Success || (!string.Equals(supportName, "+Z", StringComparison.OrdinalIgnoreCase) && supportSpan is null))
                { diagnostics.Add($"ComposeCounterboreAxisUnsupported:{name}:supported=+Z-or-Span<Plane>"); continue; }
                if (!center.Success || !diameter.Success || !counterboreDiameter.Success || !counterboreDepth.Success)
                { diagnostics.Add($"ComposeCounterboreMissingRequiredField:{name}"); continue; }
                var counterboreX = N(center, "x"); var counterboreY = N(center, "y"); var shaftDiameter = N(diameter, "d"); var boreDiameter = N(counterboreDiameter, "d"); var boreDepth = N(counterboreDepth, "d");
                if (!double.IsFinite(counterboreX) || !double.IsFinite(counterboreY) || !double.IsFinite(shaftDiameter) || shaftDiameter <= 0d || !double.IsFinite(boreDiameter) || boreDiameter <= shaftDiameter || !double.IsFinite(boreDepth) || boreDepth <= 0d)
                { diagnostics.Add($"ComposeCounterboreInvalidGeometry:{name}"); continue; }
                var hasLocalSupport = TryCircularHostInterval(name, counterboreX, counterboreY, boreDiameter / 2d, "ComposeCounterboreFootprintLeavesProfileMaterial", out var boreFrom, out var boreTo);
                var hostDepth = hasLocalSupport ? boreTo - boreFrom : materialTo - materialFrom;
                if (boreDepth >= hostDepth - 1e-8d)
                { diagnostics.Add($"ComposeCounterboreDepthExceedsHost:{name}:depth={boreDepth:R}:hostDepth={hostDepth:R}:require=positive-shaft-length; use Hole<Shaft> for a full-depth larger bore"); continue; }
                if (!hasLocalSupport) continue;
                if (!names.Add(name)) { diagnostics.Add($"compose-duplicate-operation:{name}"); continue; }
                var counterboreStableId = $"hole:{compose.Name.Text}.{name}";
                double? supportMargin = null;
                if (supportSpan is not null)
                {
                    var parentNormal = supportSpan.ParentPlane!.AxisZ.ToVector();
                    if (Math.Abs(parentNormal.X) > 1e-8d || Math.Abs(parentNormal.Y) > 1e-8d || parentNormal.Z < 1d - 1e-8d
                        || Math.Abs(supportSpan.ParentPlane.Origin.Z - boreTo) > 1e-7d)
                    { diagnostics.Add($"firmament-feature-support-parent-mismatch:{name}:{supportSpan.SpanId}"); continue; }
                    if (!PlanarSpanContainment.ContainsCircle(supportSpan, counterboreX, counterboreY, boreDiameter / 2d, out var margin))
                    { diagnostics.Add($"firmament-feature-footprint-outside-span:{diagnosticName}:{supportSpan.SpanId}:center=[{counterboreX:R},{counterboreY:R}]:radius={boreDiameter / 2d:R}:margin={margin:R}"); continue; }
                    supportMargin = margin;
                }
                if (IntersectsExistingCircularCavity(shaftHoles, counterboreHoles, counterboreX, counterboreY, boreDiameter / 2d))
                { diagnostics.Add($"ComposeCounterboreIntersectsExistingCavity:{name}"); continue; }
                var shaftProfile = $"{name}ShaftProfile"; var boreProfile = $"{name}CounterboreProfile";
                if (profiles.ContainsKey(shaftProfile) || profiles.ContainsKey(boreProfile)) { diagnostics.Add($"compose-hole-profile-name-collision:{name}"); continue; }
                profiles.Add(shaftProfile, CircleProfile(shaftProfile, counterboreX, counterboreY, shaftDiameter / 2d, counterboreStableId, $"offset:{declaration.Span.Start}"));
                profiles.Add(boreProfile, CircleProfile(boreProfile, counterboreX, counterboreY, boreDiameter / 2d, counterboreStableId, $"offset:{declaration.Span.Start}"));
                var counterboreSourceSpan = $"offset:{declaration.Span.Start}"; var counterboreSemanticRole = role.Success ? role.Groups["role"].Value : "CounterboreHole";
                counterboreHoles.Add(new(name, counterboreStableId, shaftProfile, boreProfile, counterboreX, counterboreY, shaftDiameter, boreDiameter, boreDepth, boreFrom, boreTo, counterboreSemanticRole, counterboreSourceSpan, supportSpan?.SpanId, supportSpan?.ParentId, supportMargin));
                operations.Add(new($"{name}Shaft", PrismaticProfileIntent.Remove, shaftProfile, boreFrom, boreTo, counterboreSemanticRole, counterboreSourceSpan, counterboreStableId, "Hole<Counterbore>", shaftDiameter));
                operations.Add(new($"{name}Counterbore", PrismaticProfileIntent.Remove, boreProfile, boreTo - boreDepth, boreTo, counterboreSemanticRole, counterboreSourceSpan, counterboreStableId, "Hole<Counterbore>", boreDiameter));
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
            var stableId = $"hole:{compose.Name.Text}.{name}";
            GeometricSpanView? shaftSupport = null; double? shaftSupportMargin = null;
            if (on.Success && !string.Equals(on.Groups["face"].Value, "+Z", StringComparison.OrdinalIgnoreCase))
            {
                if (!surfaceSpans.TryGetValue(on.Groups["face"].Value, out shaftSupport))
                { diagnostics.Add($"compose-hole-support-not-found:{name}:{on.Groups["face"].Value}"); continue; }
                var parentNormal = shaftSupport.ParentPlane!.AxisZ.ToVector();
                if (Math.Abs(parentNormal.X) > 1e-8d || Math.Abs(parentNormal.Y) > 1e-8d || parentNormal.Z < 1d - 1e-8d
                    || Math.Abs(shaftSupport.ParentPlane.Origin.Z - materialTo) > 1e-7d)
                { diagnostics.Add($"firmament-feature-support-parent-mismatch:{name}:{shaftSupport.SpanId}"); continue; }
                if (!PlanarSpanContainment.ContainsCircle(shaftSupport, x, y, d / 2d, out var margin))
                { diagnostics.Add($"firmament-feature-footprint-outside-span:{diagnosticName}:{shaftSupport.SpanId}:center=[{x:R},{y:R}]:radius={d / 2d:R}:margin={margin:R}"); continue; }
                shaftSupportMargin = margin;
            }
            var holeProfile = CircleProfile(profileName, x, y, d / 2d, stableId, $"offset:{declaration.Span.Start}");
            var validation = ResolvedProfile2DValidator.Validate(holeProfile);
            diagnostics.AddRange(validation.Diagnostics);
            if (!validation.IsValid) continue;
            profiles.Add(profileName, holeProfile);
            var semanticRole = role.Success ? role.Groups["role"].Value : "ShaftHole";
            var sourceSpan = $"offset:{declaration.Span.Start}";
            shaftHoles.Add(new(name, stableId, profileName, x, y, d, materialFrom, materialTo, semanticRole, sourceSpan, shaftSupport?.SpanId, shaftSupport?.ParentId, shaftSupportMargin));
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
        foreach (var hole in countersinkHoles)
        {
            if (IntersectsExistingCircularCavity(shaftHoles, counterboreHoles, hole.CenterX, hole.CenterY, hole.CountersinkDiameter / 2d)
                || countersinkHoles.Any(other => other != hole && CirclesTouchOrOverlap(hole.CenterX, hole.CenterY, hole.CountersinkDiameter / 2d, other.CenterX, other.CenterY, other.CountersinkDiameter / 2d)))
                diagnostics.Add($"ComposeCountersinkIntersectsExistingCavity:{hole.Name}:require=disjoint-entry-footprints");
        }
        foreach (var declaration in operationsSyntax.Document.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Slot"))
        {
            var name = declaration.Name?.Text ?? "";
            var fields = new FirmamentSyntaxFields(declaration, diagnostics); var body = declaration.Body is null ? null : composeBody.Substring(declaration.Body.Span.Start + 1, declaration.Body.Span.Length - 2);
            if (body is null) { diagnostics.Add($"SlotProfileDegenerate:{name}:unclosed"); continue; }
            var center = ReadFeatureField(fields, composeBody, SlotCenter, "Center"); var direction = ReadFeatureField(fields, composeBody, SlotDirection, "Direction"); var length = ReadFeatureField(fields, composeBody, SlotLength, "Length"); var width = ReadFeatureField(fields, composeBody, SlotWidth, "Width"); var extent = ReadFeatureField(fields, composeBody, SlotExtent, "Extent"); var role = ReadFeatureField(fields, composeBody, HoleRole, "Role");
            var corner = ReadFeatureField(fields, composeBody, SlotCornerRadius, "CornerRadius");
            fields.Complete();
            var variant=(FirmamentSyntaxValues.Reference(declaration.Type.Arguments.FirstOrDefault()?.Value) ?? "");
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
            dx/=norm; dy/=norm; var stableId=$"slot:{compose.Name.Text}.{name}"; var sourceSpan=$"offset:{declaration.Span.Start}"; var semanticRole=role.Success ? role.Groups["role"].Value : $"{variant}Slot";
            if (string.Equals(variant,"Capsule",StringComparison.OrdinalIgnoreCase))
            {
                var profileName=$"{name}CapsuleProfile"; var profile=CapsuleProfile(profileName,x,y,dx,dy,l,w,stableId,sourceSpan); var validation=ResolvedProfile2DValidator.Validate(profile); diagnostics.AddRange(validation.Diagnostics); if (!validation.IsValid) { diagnostics.Add($"SlotProfileDegenerate:{name}"); continue; }
                profiles.Add(profileName,profile); capsuleSlots.Add(new(name,stableId,profileName,x,y,dx,dy,l,w,from,to,extentText,semanticRole,sourceSpan)); operations.Add(new(name,PrismaticProfileIntent.Remove,profileName,from,to,semanticRole,sourceSpan,stableId,"Slot<Capsule>"));
            }
            else
            {
                if (!corner.Success) { diagnostics.Add($"SlotProfileDegenerate:{name}:RoundedRectangle requires CornerRadius"); continue; }
                var r=N(corner,"v"); if (!double.IsFinite(r) || r <= 0 || 2*r > Math.Min(l,w)) { diagnostics.Add($"SlotProfileDegenerate:{name}:CornerRadius={r:R} must be positive and no greater than min(Length,Width)/2"); continue; }
                var profileName=$"{name}RoundedRectangleProfile"; var profile=RoundedRectangleProfile(profileName,x,y,dx,dy,l,w,r,stableId,sourceSpan); var validation=ResolvedProfile2DValidator.Validate(profile); diagnostics.AddRange(validation.Diagnostics); if (!validation.IsValid) { diagnostics.Add($"SlotProfileDegenerate:{name}"); continue; }
                profiles.Add(profileName,profile); roundedRectangleSlots.Add(new(name,stableId,profileName,x,y,dx,dy,l,w,r,from,to,extentText,semanticRole,sourceSpan)); operations.Add(new(name,PrismaticProfileIntent.Remove,profileName,from,to,semanticRole,sourceSpan,stableId,"Slot<RoundedRectangle>"));
            }
        }
        if (placement.TryResolveConstructionPlane(out var compositionFrame, out _))
            foreach (var operation in operations.Where(o => authoredProfiles.Contains(o.ProfileReference)))
            {
                var operandFrame = profiles[operation.ProfileReference].EffectiveConstructionPlane;
                // Legacy Placement transports unplaced XY Profiles. Already
                // oriented Profiles must agree, rather than being transformed twice.
                if (placement.IsExplicit && SameFrame(operandFrame, ConstructionPlane.WorldXY)) continue;
                if (!SameFrame(operandFrame, compositionFrame!))
                    diagnostics.Add($"compose-profile-frame-mismatch:{operation.Name}:{operation.ProfileReference}: All operands must use the same construction frame; an explicit Placement cannot override an oriented Profile.");
            }
        var levels = operations.SelectMany(o => new[] { o.From, o.To }).Concat(countersinkHoles.Select(h => h.ConeFrom)).Distinct().Order().ToArray();
        var feature = diagnostics.Count == 0 ? new PrismaticProfileCompositionFeature(compose.Name.Text, placement.ProfilePlane, placement.Axis, placement, operations, levels, "parser-backed-scaffold-profile-composition", shaftHoles, capsuleSlots, roundedRectangleSlots, constructionPlaneBlindDrills, counterboreHoles, bosses, pockets, materialOffsets, countersinkHoles) : null;
        return new(feature, profiles, diagnostics.Distinct().ToArray(), expansion.Evidence);
    }

    private static bool SameFrame(ConstructionPlane left, ConstructionPlane right)
        => (left.Origin - right.Origin).LengthSquared < 1e-20
            && (left.AxisX.ToVector() - right.AxisX.ToVector()).LengthSquared < 1e-20
            && (left.AxisY.ToVector() - right.AxisY.ToVector()).LengthSquared < 1e-20
            && (left.AxisZ.ToVector() - right.AxisZ.ToVector()).LengthSquared < 1e-20;

    // Transitional engineering value readers operate on one AST field, never on
    // arbitrary surrounding source. Field consumption and statement boundaries are
    // owned by the recursive-descent frontend; existing domain checks stay here.
    private static Match ReadFeatureField(FirmamentSyntaxFields fields, string source, Regex reader, string name)
    {
        var nodes = fields.TakeAll(name);
        if (nodes.Count == 0) return Match.Empty;
        if (nodes.Count > 1) return Match.Empty; // duplicate is rejected by the owner below
        var fieldSource = source.Substring(nodes[0].Span.Start, nodes[0].Span.Length);
        var match = reader.Match(fieldSource);
        // An engineering value reader must consume the entire AST field. A
        // recognized prefix cannot discard an operator, placement or suffix.
        if (match.Success && (match.Index != 0 || !string.IsNullOrWhiteSpace(fieldSource[(match.Index + match.Length)..]))) match = Match.Empty;
        if (!match.Success && name is not ("Center" or "End")) fields.Invalid(name);
        return match;
    }

    private static bool IsTopSelector(string selector) => selector is "Top" or "+Z" || selector.EndsWith(".Top", StringComparison.Ordinal);

    private static IReadOnlyList<PrismaticProfileOperation> ResolveSpanTopSupports(
        GeometricSpanView span,
        IReadOnlyList<PrismaticProfileOperation> operations,
        ResolvedProfile2D footprint,
        IReadOnlyDictionary<string, ResolvedProfile2D> profiles)
    {
        if (span.ParentPlane is null) return [];
        var top = span.ParentPlane.Origin.Z;
        return operations.Where(operation => operation.Intent != PrismaticProfileIntent.Remove
                && Math.Abs(operation.To - top) <= PlanarSpanContainment.Tolerance
                && IsPocketFootprintInsideStock(operation, footprint, profiles))
            .ToArray();
    }

    private static IReadOnlyList<PrismaticProfileOperation> ResolveSpanTopSupportsForOffset(
        GeometricSpanView span,
        IReadOnlyList<PrismaticProfileOperation> operations,
        ResolvedProfile2D footprint,
        IReadOnlyDictionary<string, ResolvedProfile2D> profiles)
    {
        if (span.ParentPlane is null) return [];
        var top = span.ParentPlane.Origin.Z;
        return operations.Where(operation => operation.Intent != PrismaticProfileIntent.Remove
                && Math.Abs(operation.To - top) <= PlanarSpanContainment.Tolerance
                && HasConnectedBossSupport(operation, footprint, profiles))
            .ToArray();
    }

    private static (double MinX, double MinY, double MaxX, double MaxY) ProfileBounds(ResolvedProfile2D profile)
    {
        var points = profile.Loops.SelectMany(loop => loop.Segments).SelectMany(segment => segment.Geometry switch
        {
            LineArcLineSegment2D line => new[] { line.Start, line.End }.AsEnumerable(),
            LineArcCircularArc2D arc => ArcBoundsPoints(arc),
            _ => Enumerable.Empty<(double X, double Y)>()
        }).ToArray();
        return (points.Min(point => point.Item1), points.Min(point => point.Item2), points.Max(point => point.Item1), points.Max(point => point.Item2));
    }

    private static IEnumerable<(double X, double Y)> ArcBoundsPoints(LineArcCircularArc2D arc)
    {
        var endAngle = arc.StartAngleRadians + arc.SweepAngleRadians;
        var candidateAngles = new[]
        {
            arc.StartAngleRadians,
            endAngle,
            0d,
            Math.PI / 2d,
            Math.PI,
            3d * Math.PI / 2d
        };
        return candidateAngles
            .Where(angle => angle == arc.StartAngleRadians || angle == endAngle || IsAngleOnArc(angle, arc.StartAngleRadians, arc.SweepAngleRadians))
            .Select(angle => ArcPoint(arc, angle));
    }

    private static (double X, double Y) ArcPoint(LineArcCircularArc2D arc, double angle)
    {
        const double tolerance = 1e-12d;
        var normalized = NormalizeAngle(angle);
        if (Math.Abs(normalized) <= tolerance || Math.Abs(normalized - 2d * Math.PI) <= tolerance) return (arc.Center.X + arc.Radius, arc.Center.Y);
        if (Math.Abs(normalized - Math.PI / 2d) <= tolerance) return (arc.Center.X, arc.Center.Y + arc.Radius);
        if (Math.Abs(normalized - Math.PI) <= tolerance) return (arc.Center.X - arc.Radius, arc.Center.Y);
        if (Math.Abs(normalized - 3d * Math.PI / 2d) <= tolerance) return (arc.Center.X, arc.Center.Y - arc.Radius);
        return (arc.Center.X + arc.Radius * Math.Cos(angle), arc.Center.Y + arc.Radius * Math.Sin(angle));
    }

    private static bool IsAngleOnArc(double angle, double start, double sweep)
    {
        const double tolerance = 1e-12d;
        if (Math.Abs(sweep) >= 2d * Math.PI - tolerance) return true;
        var directedDelta = sweep >= 0d ? NormalizeAngle(angle - start) : NormalizeAngle(start - angle);
        return directedDelta <= Math.Abs(sweep) + tolerance;
    }

    private static double NormalizeAngle(double angle)
    {
        var normalized = angle % (2d * Math.PI);
        return normalized < 0d ? normalized + 2d * Math.PI : normalized;
    }

    private static bool ProfilesTouch(ResolvedProfile2D left, ResolvedProfile2D right)
    {
        const double tolerance = 1e-8d;
        var leftSegments = left.Loops.SelectMany(loop => loop.Segments).ToArray();
        var rightSegments = right.Loops.SelectMany(loop => loop.Segments).ToArray();
        return Endpoints(leftSegments).Any(point => rightSegments.Any(segment => DistanceToCurve(point, segment.Geometry) <= tolerance))
            || Endpoints(rightSegments).Any(point => leftSegments.Any(segment => DistanceToCurve(point, segment.Geometry) <= tolerance));
    }

    private static bool ToolContainsProfile(ResolvedProfile2D tool, ResolvedProfile2D target)
    {
        var toolArea = Math.Abs(PrismaticSectionStackCompiler.ProfileArea(tool));
        var targetArea = Math.Abs(PrismaticSectionStackCompiler.ProfileArea(target));
        return toolArea + 1e-8d >= targetArea
            && Endpoints(target.Loops.SelectMany(loop => loop.Segments)).All(point =>
                ProfileArrangementBuilder.PointInProfile(tool, point) is not ArrangementPointLocation.Outside);
    }

    private static IEnumerable<(double X, double Y)> Endpoints(IEnumerable<ResolvedProfileSegment2D> segments) =>
        segments.SelectMany(segment => segment.Geometry switch
        {
            LineArcLineSegment2D line => new[] { line.Start, line.End }.AsEnumerable(),
            LineArcCircularArc2D arc => new[]
            {
                (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)),
                (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians))
            },
            _ => Enumerable.Empty<(double X, double Y)>()
        });

    private static bool IsProfileFootprintInsideSpan(
        GeometricSpanView span,
        ResolvedProfile2D footprint,
        IReadOnlyDictionary<string, ResolvedProfile2D> profiles)
    {
        if (span.Boundary is null) return false;
        var available = profiles.ContainsKey(span.Boundary.Name)
            ? profiles
            : new Dictionary<string, ResolvedProfile2D>(profiles, StringComparer.Ordinal) { [span.Boundary.Name] = span.Boundary };
        var support = new PrismaticProfileOperation("SpanSupport", PrismaticProfileIntent.Base, span.Boundary.Name, 0d, 1d, "SpanSupport", span.Provenance);
        return IsPocketFootprintInsideStock(support, footprint, available);
    }

    // Bounded +Z prismatic support: a plane names an axial level, never an
    // arbitrary topology face or permission to add disconnected material.
    private static IReadOnlyList<PrismaticProfileOperation> ResolvePlaneTopSupports(Match plane, string feature,
        IReadOnlyList<PrismaticProfileOperation> operations, ResolvedProfile2D footprint,
        IReadOnlyDictionary<string, ResolvedProfile2D> profiles, List<string> diagnostics)
    {
        var origin = plane.Groups["origin"].Value.Split(',');
        var normal = plane.Groups["normal"].Value.Split(',');
        var up = plane.Groups["up"].Success ? plane.Groups["up"].Value.Split(',').Select(v => v.Trim()).ToArray() : new[] { "0", "1", "0" };
        if (!up.SequenceEqual(new[] { "0", "1", "0" }) || origin.Length != 3 || normal.Length != 3
            || !FirmamentNumericEvaluation.TryEvaluateScalar(origin[0], out var x, out var xu) || xu != "mm" || Math.Abs(x) > 1e-9
            || !FirmamentNumericEvaluation.TryEvaluateScalar(origin[1], out var y, out var yu) || yu != "mm" || Math.Abs(y) > 1e-9
            || !FirmamentNumericEvaluation.TryEvaluateScalar(origin[2], out var z, out var zu) || zu != "mm"
            || !double.TryParse(normal[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var nx) || nx != 0
            || !double.TryParse(normal[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var ny) || ny != 0
            || !double.TryParse(normal[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var nz) || nz != 1)
        { diagnostics.Add($"feature-support-plane-not-qualified:{feature}:qualified=origin-[0,0,z]-normal-+Z"); return []; }
        return operations.Where(o => o.Intent != PrismaticProfileIntent.Remove && Math.Abs(o.To - z) < 1e-9
            && profiles.TryGetValue(o.ProfileReference, out var profile) && HasConnectedBossSupport(o, footprint, profiles)).ToArray();
    }

    private static IReadOnlyList<PrismaticProfileOperation> ResolveTopSupports(
        string selector,
        PrismaticProfileOperation stock,
        IReadOnlyList<PrismaticProfileOperation> operations,
        ResolvedProfile2D footprint,
        IReadOnlyDictionary<string, ResolvedProfile2D> profiles,
        bool requireContainment = false)
    {
        IEnumerable<PrismaticProfileOperation> supports;
        var relative = !selector.EndsWith(".Top", StringComparison.Ordinal);
        if (selector.EndsWith(".Top", StringComparison.Ordinal))
        {
            var owner = selector[..^4];
            supports = owner == "Base"
                ? [stock]
                : operations.Where(operation => operation.Intent != PrismaticProfileIntent.Remove
                    && string.Equals(operation.Name, owner, StringComparison.Ordinal));
        }
        else supports = operations.Where(operation => operation.Intent != PrismaticProfileIntent.Remove);

        var admitted = supports.Where(support => profiles.TryGetValue(support.ProfileReference, out var supportProfile)
                && (requireContainment
                    ? IsPocketFootprintInsideStock(support, footprint, profiles)
                    : HasConnectedBossSupport(support, footprint, profiles)))
            .ToArray();
        if (!relative || admitted.Length == 0) return admitted;

        // A stepped current body can retain several upward plateaus. Resolve the
        // highest semantic support under this feature footprint, not the globally
        // largest Z face and not the original Base. Equal highest supports are a
        // typed ambiguity rather than topology-order selection.
        var currentTop = admitted.Max(operation => operation.To);
        return admitted.Where(operation => Math.Abs(operation.To - currentTop) <= 1e-9d).ToArray();
    }

    private static bool HasConnectedBossSupport(PrismaticProfileOperation stock, ResolvedProfile2D boss, IReadOnlyDictionary<string, ResolvedProfile2D> profiles)
    {
        var probe = ProfileArrangementBuilder.Compose("XY", [stock with { From = 0d, To = 1d }, new("BossSupportProbe", PrismaticProfileIntent.Add, boss.Name, 0d, 1d, "Boss", "generated")], profiles, "boss-connectivity-probe");
        if (probe.MaterialRegions.Count == 0 || probe.Arrangement.Diagnostics.Count > 0) return false;
        var stockArea = Math.Abs(PrismaticSectionStackCompiler.ProfileArea(profiles[stock.ProfileReference]));
        var bossArea = Math.Abs(PrismaticSectionStackCompiler.ProfileArea(boss));
        var overlapArea = stockArea + bossArea - probe.MaterialRegions.Sum(PrismaticSectionStackCompiler.Area);
        return overlapArea > Math.Max(1e-7d, Math.Min(stockArea, bossArea) * 1e-8d);
    }

    private static bool IsPocketFootprintInsideStock(PrismaticProfileOperation stock, ResolvedProfile2D pocket, IReadOnlyDictionary<string, ResolvedProfile2D> profiles)
    {
        var probe = ProfileArrangementBuilder.Compose("XY", [stock with { From = 0d, To = 1d }, new("PocketContainmentProbe", PrismaticProfileIntent.Remove, pocket.Name, 0d, 1d, "Pocket", "generated")], profiles, "pocket-containment-probe");
        if (probe.MaterialRegions.Count == 0 || probe.Arrangement.Diagnostics.Count > 0) return false;
        var stockArea = Math.Abs(PrismaticSectionStackCompiler.ProfileArea(profiles[stock.ProfileReference]));
        var pocketArea = Math.Abs(PrismaticSectionStackCompiler.ProfileArea(pocket));
        var retainedArea = probe.MaterialRegions.Sum(PrismaticSectionStackCompiler.Area);
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
            var wall = Regex.Match(canonical[0].Body!, @"\b[Mm]inimumWallThickness\s*:\s*(?<value>[-+.\d]+)mm", RegexOptions.CultureInvariant);
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
    public const string ComposeModifyFeatureUnsupported = "compose-modify-feature-unsupported";

    private static string BindExternalModifyFeatures(string source, string composeName, string composeBody, List<string> diagnostics)
    {
        var boundFeatures = new List<string>();
        // Modify holds feature declarations only. The readers below look for feature
        // headers, so a stray field in the block would otherwise vanish.
        foreach (var modifySyntax in FirmamentV2.Syntax.FirmamentSyntaxTraversal.Declarations(FirmamentV2.Syntax.FirmamentSyntaxParser.Parse(source).Document)
                     .Where(d => d.Type.Name.Text == "Modify" && d.Name?.Text == composeName))
            foreach (var statement in modifySyntax.Body?.Statements ?? [])
                if (statement is FirmamentV2.Syntax.FieldSyntax field) diagnostics.Add($"{FirmamentV2Parser.CanonicalFieldUnknown}:Modify:{field.Name.Text}");
                else if (statement is not FirmamentV2.Syntax.DeclarationSyntax feature) diagnostics.Add(FirmamentV2Parser.CanonicalModifyMalformed);
                // Only these lower into the section stack. Anything else would be admitted and never cut.
                else if (feature.Type.Name.Text is not ("Hole" or "Slot" or "Pattern" or "EdgeFinish"))
                    diagnostics.Add($"{ComposeModifyFeatureUnsupported}:{feature.Type.Name.Text}:{feature.Name?.Text}: {feature.Type.Name.Text} is not supported on a Profile-based body; Modify on Compose or Extrude takes Hole, Slot, Pattern and EdgeFinish. Use a Pattern of Hole for a grid of openings.");
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
