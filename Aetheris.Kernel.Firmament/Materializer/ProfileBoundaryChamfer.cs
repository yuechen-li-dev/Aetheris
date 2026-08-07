using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// A source-bound edge finish.  It deliberately contains resolved Profile identities rather
/// than BRep edge ids: the BRep is produced by this plan, never searched to discover a target.
/// </summary>
public enum ProfileBoundaryChamferSide { Top, Bottom }
public enum ProfileBoundaryChamferChainKind { SingleSegment, OpenConnectedChain, ClosedLoop }
public sealed record ProfileBoundaryChamferTarget(
    string StableId, string HostBodyId, string ProfileId, string LoopId,
    IReadOnlyList<string> SegmentIds, ProfileBoundaryChamferSide Side,
    ProfileBoundaryChamferChainKind ChainKind, string SourceSpan, string SelectionProvenance);
public sealed record ProfileBoundaryChamferPlanResult(
    bool Succeeded, BrepBody? Body, SemanticTopologyCorrespondence? Correspondence,
    ProfileBoundaryChamferTarget? Target, IReadOnlyList<string> Diagnostics);

/// <summary>
/// M1's source-selected round.  It is deliberately a separate, non-generic plan: it
/// owns one Line2 descendant and never searches a materialized B-rep for an edge.
/// </summary>
public sealed record ProfileStraightEdgeFilletPlan(
    ProfileBoundaryChamferTarget Target, double Radius, double EndClearance,
    Point3D SourceStart, Point3D SourceEnd, Point3D SpanStart, Point3D SpanEnd,
    Direction3D Tangent, Direction3D InwardNormal, Direction3D ExtrusionAxis,
    Point3D CylinderCenterlineStart, Point3D CylinderCenterlineEnd,
    Point3D CapContactStart, Point3D CapContactEnd, Point3D SideContactStart, Point3D SideContactEnd,
    string EndpointPolicy = "FilletSpanInset");
public sealed record ProfileStraightEdgeFilletPlanResult(
    bool Succeeded, BrepBody? Body, SemanticTopologyCorrespondence? Correspondence,
    ProfileStraightEdgeFilletPlan? Plan, IReadOnlyList<string> Diagnostics);

public static class ProfileBoundaryChamferSourceBinder
{
    private static readonly Regex EdgeFinishHeader = new(@"\bEdgeFinish\s+(?<name>\w+)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex Target = new(@"\bTarget\s*:\s*(?<value>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*){0,2})", RegexOptions.CultureInvariant);
    private static readonly Regex On = new(@"\bOn\s*:\s*(?<value>Top|Bottom)\b", RegexOptions.CultureInvariant);
    private static readonly Regex Kind = new(@"\bKind\s*:\s*(?<value>\w+)\b", RegexOptions.CultureInvariant);
    private static readonly Regex Distance = new(@"\bDistance\s*:\s*(?<value>[-+.\deE]+)mm\b", RegexOptions.CultureInvariant);
    private static readonly Regex Radius = new(@"\bRadius\s*:\s*(?<value>[-+.\deE]+)mm\b", RegexOptions.CultureInvariant);
    private static readonly Regex EndClearance = new(@"\bEndClearance\s*:\s*(?<value>[-+.\deE]+)mm\b", RegexOptions.CultureInvariant);
    private static readonly Regex SelectionHeader = new(@"\bSelection\s+(?<name>\w+)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex CanonicalSelection = new(@"\bSource\s*:\s*(?<profile>\w+)\.(?<loop>\w+)\.\[\s*(?<segments>[\w\s,]+)\s*\]", RegexOptions.CultureInvariant);
    private static readonly Regex HistoricalSelection = new(@"\bSource\s*:\s*(?<profile>\w+)\.ProfileSegments\s*\(\s*\[(?<segments>[\w\s,]+)\]\s*\)", RegexOptions.CultureInvariant);
    private static readonly Regex Require = new(@"\bRequire\s*:\s*(?<value>ConnectedChain|ClosedLoop)\b", RegexOptions.CultureInvariant);

    public static bool HasSemanticProfileBoundaryFinish(string source) =>
        EdgeFinishHeader.Matches(source).Cast<Match>().Any(header =>
        {
            var body = Block(source, header.Index + header.Length - 1);
            return body is not null && On.IsMatch(body);
        });

    public static bool HasProfileBoundaryFillet(string source) => EdgeFinishHeader.Matches(source).Cast<Match>().Any(header =>
    {
        var body = Block(source, header.Index + header.Length - 1);
        return body is not null && On.IsMatch(body) && string.Equals(Kind.Match(body).Groups["value"].Value, "Fillet", StringComparison.Ordinal);
    });

    /// <summary>
    /// Resolves the source-bound fillet target into Profile order.  This is shared
    /// target semantics with chamfer, not B-rep edge discovery: a direct segment,
    /// named connected-chain selection, and an outer loop all produce one ordered
    /// target.  M1 materialization still only consumes the single-segment shape.
    /// </summary>
    public static bool TryBindFillet(string source, ResolvedProfile2D profile, string hostBodyId, out ProfileBoundaryChamferTarget? target, out double radius, out double endClearance, out string? diagnostic)
    {
        target = null; radius = 0d; endClearance = 0d; diagnostic = null;
        var finish = EdgeFinishHeader.Matches(source).Cast<Match>().Select(match => (Match: match, Body: Block(source, match.Index + match.Length - 1))).FirstOrDefault(x => x.Body is not null && On.IsMatch(x.Body) && string.Equals(Kind.Match(x.Body).Groups["value"].Value, "Fillet", StringComparison.Ordinal));
        if (finish.Body is null) { diagnostic = "ProfileBoundaryFilletBoundaryRequired"; return false; }
        var radiusMatch = Radius.Match(finish.Body); var clearanceMatch = EndClearance.Match(finish.Body);
        if (!radiusMatch.Success || !double.TryParse(radiusMatch.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out radius) || !double.IsFinite(radius) || radius <= 0d) { diagnostic = "ProfileBoundaryFilletRadiusMustBePositive"; return false; }
        var targetMatch = Target.Match(finish.Body); var on = On.Match(finish.Body);
        if (!targetMatch.Success || !on.Success) { diagnostic = "ProfileBoundaryFilletDeclarationInvalid"; return false; }
        var rawTarget = targetMatch.Groups["value"].Value;
        string requestedProfile; string requestedLoop; IReadOnlyList<string> requestedSegments;
        ProfileBoundaryChamferChainKind chainKind; string provenance;
        var parts = rawTarget.Split('.');
        if (parts.Length is 2 or 3)
        {
            requestedProfile = parts[0]; requestedLoop = parts[1];
            requestedSegments = parts.Length == 3 ? [parts[2]] : [];
            chainKind = parts.Length == 3 ? ProfileBoundaryChamferChainKind.SingleSegment : ProfileBoundaryChamferChainKind.ClosedLoop;
            provenance = "DirectProfileBoundaryTarget";
        }
        else
        {
            var selection = SelectionHeader.Matches(source).Cast<Match>()
                .Select(match => (Match: match, Body: Block(source, match.Index + match.Length - 1)))
                .FirstOrDefault(x => string.Equals(x.Match.Groups["name"].Value, rawTarget, StringComparison.Ordinal));
            if (selection.Body is null) { diagnostic = "ProfileBoundaryFilletTargetUnknown"; return false; }
            var canonical = CanonicalSelection.Match(selection.Body); var historical = HistoricalSelection.Match(selection.Body);
            var selectionSource = canonical.Success ? canonical : historical; var requirement = Require.Match(selection.Body);
            if (!selectionSource.Success || !requirement.Success) { diagnostic = "ProfileBoundaryFilletSelectionKindUnsupported"; return false; }
            requestedProfile = selectionSource.Groups["profile"].Value;
            requestedLoop = canonical.Success ? canonical.Groups["loop"].Value : "Outer";
            requestedSegments = selectionSource.Groups["segments"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            chainKind = string.Equals(requirement.Groups["value"].Value, "ClosedLoop", StringComparison.Ordinal)
                ? ProfileBoundaryChamferChainKind.ClosedLoop : ProfileBoundaryChamferChainKind.OpenConnectedChain;
            provenance = $"Selection:{rawTarget}";
        }
        if (!string.Equals(requestedProfile, profile.Name, StringComparison.Ordinal)) { diagnostic = "ProfileBoundaryFilletProfileUnknown"; return false; }
        var loop = profile.Loops.SingleOrDefault(x => string.Equals(x.Name, requestedLoop, StringComparison.Ordinal));
        if (loop is null) { diagnostic = "ProfileBoundaryFilletLoopUnknown"; return false; }
        if (!loop.IsOuter) { diagnostic = "ProfileBoundaryFilletInnerLoopUnsupported"; return false; }
        if (requestedSegments.Count == 0) requestedSegments = loop.Segments.Select(segment => segment.Name).ToArray();
        if (requestedSegments.Distinct(StringComparer.Ordinal).Count() != requestedSegments.Count) { diagnostic = "ProfileBoundaryFilletDuplicateSegment"; return false; }
        var indices = requestedSegments.Select(id => IndexOf(loop, id)).ToArray();
        if (indices.Any(index => index < 0)) { diagnostic = "ProfileBoundaryFilletSegmentUnknown"; return false; }
        var ordered = NormalizeConnectedOrder(loop, indices, out var closed);
        if (ordered is null) { diagnostic = "ProfileBoundaryFilletDisconnectedChain"; return false; }
        if (chainKind == ProfileBoundaryChamferChainKind.ClosedLoop && !closed) { diagnostic = "ProfileBoundaryFilletSelectionMustClose"; return false; }
        if (chainKind == ProfileBoundaryChamferChainKind.OpenConnectedChain && closed) { diagnostic = "ProfileBoundaryFilletSelectionMustBeOpen"; return false; }
        if (closed) chainKind = ProfileBoundaryChamferChainKind.ClosedLoop;
        else if (ordered.Count == 1) chainKind = ProfileBoundaryChamferChainKind.SingleSegment;
        var orderedSegments = ordered.Select(index => loop.Segments[index]).ToArray();
        if (orderedSegments.Any(segment => segment.Geometry is not LineArcLineSegment2D)) { diagnostic = "ProfileBoundaryFilletSegmentKindUnsupported"; return false; }

        endClearance = clearanceMatch.Success && double.TryParse(clearanceMatch.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var explicitClearance) ? explicitClearance : radius;
        if (!double.IsFinite(endClearance) || endClearance <= 0d) { diagnostic = "ProfileBoundaryFilletEndClearanceMustBePositive"; return false; }
        target = new($"edgefinish:{finish.Match.Groups["name"].Value}", hostBodyId, profile.Name, loop.Name,
            orderedSegments.Select(segment => segment.Name).ToArray(), Enum.Parse<ProfileBoundaryChamferSide>(on.Groups["value"].Value),
            chainKind, $"offset:{finish.Match.Index}", provenance);
        return true;
    }

    public static bool TryBind(string source, ResolvedProfile2D profile, string hostBodyId, out ProfileBoundaryChamferTarget? target, out double distance, out string? diagnostic)
    {
        target = null; distance = 0d; diagnostic = null;
        var finish = EdgeFinishHeader.Matches(source).Cast<Match>().Select(match => (Match: match, Body: Block(source, match.Index + match.Length - 1))).FirstOrDefault(x => x.Body is not null && On.IsMatch(x.Body));
        if (finish.Body is null) { diagnostic = "ProfileBoundaryChamferBoundaryRequired"; return false; }
        var name = finish.Match.Groups["name"].Value;
        var targetMatch = Target.Match(finish.Body); var on = On.Match(finish.Body); var kind = Kind.Match(finish.Body); var amount = Distance.Match(finish.Body);
        if (!targetMatch.Success || !on.Success || !kind.Success) { diagnostic = "ProfileBoundaryChamferDeclarationInvalid"; return false; }
        if (string.Equals(kind.Groups["value"].Value, "Fillet", StringComparison.Ordinal))
        {
            diagnostic = Radius.IsMatch(finish.Body) ? "ProfileBoundaryFilletNotMaterialized" : "ProfileBoundaryFilletRadiusRequired";
            return false;
        }
        if (!amount.Success) { diagnostic = "ProfileBoundaryChamferDeclarationInvalid"; return false; }
        if (!string.Equals(kind.Groups["value"].Value, "Chamfer", StringComparison.Ordinal)) { diagnostic = "ProfileBoundaryChamferKindUnsupported"; return false; }
        if (!double.TryParse(amount.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out distance) || !double.IsFinite(distance) || distance <= 0d) { diagnostic = "ProfileBoundaryChamferDistanceMustBePositive"; return false; }

        var rawTarget = targetMatch.Groups["value"].Value;
        string requestedProfile; string loop; IReadOnlyList<string> segments; ProfileBoundaryChamferChainKind chainKind; string provenance;
        var parts = rawTarget.Split('.');
        if (parts.Length >= 2)
        {
            requestedProfile = parts[0]; loop = parts[1];
            if (parts.Length == 3) { segments = [parts[2]]; chainKind = ProfileBoundaryChamferChainKind.SingleSegment; }
            else { segments = []; chainKind = ProfileBoundaryChamferChainKind.ClosedLoop; }
            provenance = "DirectProfileBoundaryTarget";
        }
        else
        {
            var selection = SelectionHeader.Matches(source).Cast<Match>().Select(match => (Match: match, Body: Block(source, match.Index + match.Length - 1))).FirstOrDefault(x => x.Match.Groups["name"].Value == rawTarget);
            if (selection.Body is null) { diagnostic = "ProfileBoundaryChamferTargetUnknown"; return false; }
            var canonical = CanonicalSelection.Match(selection.Body); var historical = HistoricalSelection.Match(selection.Body); var sourceMatch = canonical.Success ? canonical : historical;
            var require = Require.Match(selection.Body);
            if (!sourceMatch.Success || !require.Success) { diagnostic = "ProfileBoundaryChamferSelectionKindUnsupported"; return false; }
            requestedProfile = sourceMatch.Groups["profile"].Value; loop = canonical.Success ? canonical.Groups["loop"].Value : "Outer";
            segments = sourceMatch.Groups["segments"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            chainKind = string.Equals(require.Groups["value"].Value, "ClosedLoop", StringComparison.Ordinal) ? ProfileBoundaryChamferChainKind.ClosedLoop : ProfileBoundaryChamferChainKind.OpenConnectedChain;
            provenance = $"Selection:{rawTarget}";
        }
        if (!string.Equals(requestedProfile, profile.Name, StringComparison.Ordinal)) { diagnostic = "ProfileBoundaryChamferProfileUnknown"; return false; }
        var resolvedLoop = profile.Loops.SingleOrDefault(x => string.Equals(x.Name, loop, StringComparison.Ordinal));
        if (resolvedLoop is null) { diagnostic = "ProfileBoundaryChamferLoopUnknown"; return false; }
        if (!resolvedLoop.IsOuter) { diagnostic = "ProfileBoundaryChamferInnerLoopUnsupported"; return false; }
        if (segments.Count == 0) segments = resolvedLoop.Segments.Select(x => x.Name).ToArray();
        if (segments.Distinct(StringComparer.Ordinal).Count() != segments.Count) { diagnostic = "ProfileBoundaryChamferDuplicateSegment"; return false; }
        var indices = segments.Select(id => IndexOf(resolvedLoop, id)).ToArray();
        if (indices.Any(index => index < 0)) { diagnostic = "ProfileBoundaryChamferSegmentUnknown"; return false; }
        var ordered = NormalizeConnectedOrder(resolvedLoop, indices, out var closed);
        if (ordered is null) { diagnostic = "ProfileBoundaryChamferDisconnectedChain"; return false; }
        if (chainKind == ProfileBoundaryChamferChainKind.ClosedLoop && !closed) { diagnostic = "ProfileBoundaryChamferSelectionMustClose"; return false; }
        if (chainKind == ProfileBoundaryChamferChainKind.OpenConnectedChain && closed) { diagnostic = "ProfileBoundaryChamferSelectionMustBeOpen"; return false; }
        if (closed) chainKind = ProfileBoundaryChamferChainKind.ClosedLoop;
        else if (ordered.Count == 1) chainKind = ProfileBoundaryChamferChainKind.SingleSegment;
        target = new($"edgefinish:{name}", hostBodyId, profile.Name, loop, ordered.Select(index => resolvedLoop.Segments[index].Name).ToArray(),
            Enum.Parse<ProfileBoundaryChamferSide>(on.Groups["value"].Value), chainKind, $"offset:{finish.Match.Index}", provenance);
        return true;
    }

    private static int IndexOf(ResolvedProfileLoop2D loop, string name) => loop.Segments.Select((segment, index) => (segment, index)).Where(x => x.segment.Name == name).Select(x => x.index).DefaultIfEmpty(-1).Single();
    private static IReadOnlyList<int>? NormalizeConnectedOrder(ResolvedProfileLoop2D loop, IReadOnlyList<int> indices, out bool closed)
    {
        var selected = indices.ToHashSet(); var count = loop.Segments.Count; closed = selected.Count == count;
        if (closed) return Enumerable.Range(0, count).ToArray();
        var starts = selected.Where(index => !selected.Contains((index + count - 1) % count)).ToArray();
        if (starts.Length != 1) return null;
        var result = new List<int>(); var current = starts[0];
        while (selected.Contains(current)) { result.Add(current); current = (current + 1) % count; }
        return result.Count == selected.Count ? result : null;
    }
    private static string? Block(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++) { if (text[i] == '{') depth++; else if (text[i] == '}' && --depth == 0) return text[(open + 1)..i]; }
        return null;
    }
}

/// <summary>Exact polyhedral section transition for an outer, line-only Profile boundary.</summary>
public static class ProfileBoundaryChamferPlanner
{
    private const double Tol = 1e-8;
    public static ProfileBoundaryChamferPlanResult TryPlan(ResolvedProfile2D profile, ProfileBoundaryChamferTarget target, double distance)
    {
        ProfileBoundaryChamferPlanResult Fail(string code) => new(false, null, null, target, [code]);
        var loop = profile.Loops.SingleOrDefault(x => x.Name == target.LoopId);
        if (loop is null) return Fail("ProfileBoundaryChamferLoopUnknown");
        if (!loop.IsOuter) return Fail("ProfileBoundaryChamferInnerLoopUnsupported");
        if (profile.Loops.Count != 1) return Fail("ProfileBoundaryChamferInnerLoopUnsupported");
        if (loop.Segments.Any(x => x.Geometry is not LineArcLineSegment2D)) return Fail("ProfileBoundaryChamferSegmentKindUnsupported");
        var lines = loop.Segments.Select(x => (LineArcLineSegment2D)x.Geometry).ToArray();
        var selected = target.SegmentIds.Select(id => Array.FindIndex(loop.Segments.ToArray(), x => x.Name == id)).ToHashSet();
        if (selected.Count != target.SegmentIds.Count || selected.Any(x => x < 0)) return Fail("ProfileBoundaryChamferSegmentUnknown");
        var start = profile.LocalStartDepth ?? -1d; var end = profile.LocalEndDepth ?? 1d;
        var thickness = end - start;
        if (distance >= thickness - Tol) return Fail("ProfileBoundaryChamferDistanceExceedsHostThickness");
        var area = SignedArea(lines.Select(x => x.Start).ToArray());
        if (Math.Abs(area) <= Tol) return Fail("ProfileBoundaryChamferInsetCollapse");
        var junctions = ProfileJunctionClassifier.Classify(profile, loop);
        if (junctions.Any(x => (x.Classification is ProfileJunctionKind.Collinear or ProfileJunctionKind.Degenerate) &&
            (selected.Contains(IndexOf(loop, x.PredecessorSegmentId)) || selected.Contains(IndexOf(loop, x.SuccessorSegmentId)))))
            return Fail("ProfileBoundaryChamferJunctionDegenerate");
        var inset = BuildInset(lines, selected, area, distance, out var insetDiagnostic);
        if (inset is null) return Fail(insetDiagnostic!);
        return BuildBody(profile, target, lines, selected, inset, start, end, distance);
    }

    /// <summary>Creates the upper, source-derived section used by a composed variable interval.</summary>
    public static bool TryCreateInsetOuterProfile(ResolvedProfile2D profile, ProfileBoundaryChamferTarget target, double distance, out ResolvedProfile2D? insetProfile, out string? diagnostic)
    {
        insetProfile = null; diagnostic = null;
        var loop = profile.Loops.SingleOrDefault(x => x.Name == target.LoopId);
        if (loop is null || !loop.IsOuter || loop.Segments.Any(x => x.Geometry is not LineArcLineSegment2D)) { diagnostic = "ProfileBoundaryChamferSegmentKindUnsupported"; return false; }
        var lines = loop.Segments.Select(x => (LineArcLineSegment2D)x.Geometry).ToArray(); var selected = target.SegmentIds.Select(id => Array.FindIndex(loop.Segments.ToArray(), x => x.Name == id)).ToHashSet();
        if (selected.Count != lines.Length || selected.Any(x => x < 0)) { diagnostic = "ProfileBoundaryChamferComposeWholeLoopRequired"; return false; }
        var junctions = ProfileJunctionClassifier.Classify(profile, loop);
        if (junctions.Any(x => x.Classification is ProfileJunctionKind.Collinear or ProfileJunctionKind.Degenerate)) { diagnostic = "ProfileBoundaryChamferJunctionDegenerate"; return false; }
        var inset = BuildInset(lines, selected, SignedArea(lines.Select(x => x.Start).ToArray()), distance, out diagnostic);
        if (inset is null) return false;
        var segments = lines.Select((_, i) => new ResolvedProfileSegment2D(loop.Segments[i].Name, new LineArcLineSegment2D(inset[i], inset[(i + 1) % inset.Length]), loop.Segments[i].Provenance with { StableId = loop.Segments[i].Provenance.StableId + ":inset", Derivation = "ProfileBoundaryChamferInset" })).ToArray();
        insetProfile = new ResolvedProfile2D(profile.Name + ":inset", profile.PlaneFrame, [new ResolvedProfileLoop2D(loop.Name, true, segments)], profile.ConstructionPlane, profile.LocalStartDepth, profile.LocalEndDepth);
        return true;
    }

    private static (double X, double Y)[]? BuildInset(IReadOnlyList<LineArcLineSegment2D> lines, HashSet<int> selected, double signedArea, double distance, out string? diagnostic)
    {
        diagnostic = null; var count = lines.Count; var result = new (double X, double Y)[count];
        for (var vertex = 0; vertex < count; vertex++)
        {
            var previous = (vertex + count - 1) % count; var next = vertex;
            var previousSelected = selected.Contains(previous); var nextSelected = selected.Contains(next);
            var original = lines[next].Start;
            if (!previousSelected && !nextSelected) { result[vertex] = original; continue; }
            if (previousSelected && nextSelected)
            {
                if (!TryOffsetIntersection(lines[previous], lines[next], signedArea, distance, out result[vertex])) { diagnostic = "ProfileBoundaryChamferJunctionUnsupported"; return null; }
            }
            else
            {
                var line = previousSelected ? lines[previous] : lines[next];
                var (nx, ny) = InwardNormal(line, signedArea);
                result[vertex] = (original.X + nx * distance, original.Y + ny * distance);
            }
        }
        for (var i = 0; i < count; i++)
        {
            var line = lines[i]; var a = selected.Contains(i) ? result[i] : line.Start; var b = selected.Contains(i) ? result[(i + 1) % count] : line.End;
            if (Distance(a, b) <= Tol) { diagnostic = "ProfileBoundaryChamferInsetCollapse"; return null; }
        }
        if (HasSelfIntersection(result)) { diagnostic = "ProfileBoundaryChamferOffsetSelfIntersection"; return null; }
        return result;
    }

    private static bool HasSelfIntersection(IReadOnlyList<(double X, double Y)> polygon)
    {
        for (var i = 0; i < polygon.Count; i++)
        for (var j = i + 1; j < polygon.Count; j++)
        {
            if (j == i || (j + 1) % polygon.Count == i || (i + 1) % polygon.Count == j) continue;
            if (ProperIntersection(polygon[i], polygon[(i + 1) % polygon.Count], polygon[j], polygon[(j + 1) % polygon.Count])) return true;
        }
        return false;
    }

    private static bool ProperIntersection((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
    {
        var abC = Orientation(a, b, c); var abD = Orientation(a, b, d); var cdA = Orientation(c, d, a); var cdB = Orientation(c, d, b);
        return abC * abD < -Tol && cdA * cdB < -Tol;
    }
    private static double Orientation((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) =>
        (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool TryOffsetIntersection(LineArcLineSegment2D first, LineArcLineSegment2D second, double area, double distance, out (double X, double Y) point)
    {
        var (n1x, n1y) = InwardNormal(first, area); var (n2x, n2y) = InwardNormal(second, area);
        var a = (X: first.Start.X + n1x * distance, Y: first.Start.Y + n1y * distance);
        var b = (X: second.Start.X + n2x * distance, Y: second.Start.Y + n2y * distance);
        var r = (X: first.End.X - first.Start.X, Y: first.End.Y - first.Start.Y); var s = (X: second.End.X - second.Start.X, Y: second.End.Y - second.Start.Y);
        var cross = r.X * s.Y - r.Y * s.X;
        if (Math.Abs(cross) <= Tol) { point = default; return false; }
        var q = (X: b.X - a.X, Y: b.Y - a.Y); var t = (q.X * s.Y - q.Y * s.X) / cross;
        point = (a.X + t * r.X, a.Y + t * r.Y); return double.IsFinite(point.X) && double.IsFinite(point.Y);
    }
    private static (double X, double Y) InwardNormal(LineArcLineSegment2D line, double area)
    {
        var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y; var length = Math.Sqrt(dx * dx + dy * dy);
        // A CCW outer loop has material on the left; CW has it on the right.
        return area > 0d ? (-dy / length, dx / length) : (dy / length, -dx / length);
    }

    private static ProfileBoundaryChamferPlanResult BuildBody(ResolvedProfile2D profile, ProfileBoundaryChamferTarget target, IReadOnlyList<LineArcLineSegment2D> lines, HashSet<int> selected, (double X, double Y)[] inset, double start, double end, double distance)
    {
        var builder = new TopologyBuilder(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel(); var points = new Dictionary<VertexId, Point3D>();
        var resolvedLoop = profile.Loops.Single(x => x.Name == target.LoopId);
        var frame = profile.EffectiveConstructionPlane; var vertices = new Dictionary<string, VertexId>(StringComparer.Ordinal); var edges = new Dictionary<(VertexId, VertexId), EdgeId>(); var descendants = new List<SemanticTopologyDescendant>();
        VertexId Vertex(string key, (double X, double Y) xy, double depth)
        {
            if (vertices.TryGetValue(key, out var existing)) return existing;
            var id = builder.AddVertex(); vertices.Add(key, id); points.Add(id, frame.ToWorld(xy, depth)); return id;
        }
        EdgeId Edge(VertexId a, VertexId b)
        {
            var key = a.Value < b.Value ? (a, b) : (b, a);
            if (edges.TryGetValue(key, out var existing)) return existing;
            var id = builder.AddEdge(a, b); edges.Add(key, id);
            var from = points[a]; var to = points[b]; var length = (to - from).Length;
            var curve = new CurveGeometryId(geometry.Curves.Count() + 1);
            geometry.AddCurve(curve, CurveGeometry.FromLine(new Line3Curve(from, Direction3D.Create(to - from))));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(id, curve, new ParameterInterval(0d, length), true)); return id;
        }
        // The topology model requires coedge next/previous ids at construction.  Use a local helper
        // that creates them with known ids before registering the loop.
        FaceId AddFace(string stableId, IReadOnlyList<VertexId> boundary, SemanticTopologyRole role, string source, string? parent = null)
        {
            var loopId = builder.AllocateLoopId(); var coedgeIds = Enumerable.Range(0, boundary.Count).Select(_ => new CoedgeId(builder.Model.Coedges.Count() + 1 + _)).ToArray();
            for (var i = 0; i < boundary.Count; i++)
            {
                var a = boundary[i]; var b = boundary[(i + 1) % boundary.Count]; var edge = Edge(a, b); var modelEdge = builder.Model.Edges.Single(x => x.Id == edge);
                builder.AddCoedge(new Coedge(coedgeIds[i], edge, loopId, coedgeIds[(i + 1) % boundary.Count], coedgeIds[(i + boundary.Count - 1) % boundary.Count], modelEdge.StartVertexId != a));
            }
            builder.AddLoop(new Loop(loopId, coedgeIds));
            var faceId = builder.AddFace([loopId]);
            var p0 = points[boundary[0]]; Vector3D? normal = null;
            for (var i = 1; i < boundary.Count - 1 && normal is null; i++)
            {
                var candidate = (points[boundary[i]] - p0).Cross(points[boundary[i + 1]] - p0);
                if (candidate.Length > Tol) normal = candidate;
            }
            if (normal is null) throw new InvalidOperationException($"ProfileBoundaryChamferDegenerateFace:{stableId}");
            var surface = new SurfaceGeometryId(geometry.Surfaces.Count() + 1);
            geometry.AddSurface(surface, SurfaceGeometry.FromPlane(new PlaneSurface(p0, Direction3D.Create(normal.Value), Direction3D.Create(points[boundary[1]] - p0))));
            bindings.AddFaceBinding(new FaceGeometryBinding(faceId, surface, true));
            descendants.Add(new(stableId, "Face", role, source, Face: faceId, ParentStableId: parent)); return faceId;
        }

        var n = lines.Count; var lower = new VertexId[n]; var middle = new VertexId[n]; var modified = new VertexId[n]; var originalTop = new VertexId[n]; var upper = new VertexId[n];
        var transitionStart = target.Side == ProfileBoundaryChamferSide.Top ? end - distance : start;
        var transitionEnd = target.Side == ProfileBoundaryChamferSide.Top ? end : start + distance;
        for (var i = 0; i < n; i++)
        {
            var p = lines[i].Start;
            lower[i] = Vertex($"lower:{i}", p, start); middle[i] = Vertex($"middle:{i}", p, transitionStart);
            originalTop[i] = Vertex($"original-top:{i}", p, transitionEnd); modified[i] = Vertex($"modified:{i}", inset[i], target.Side == ProfileBoundaryChamferSide.Top ? end : start);
            upper[i] = Vertex($"upper:{i}", p, end);
        }
        var topBoundary = new List<VertexId>();
        for (var i = 0; i < n; i++)
        {
            var previousSelected = selected.Contains((i + n - 1) % n); var nextSelected = selected.Contains(i);
            // An open run terminates at one planar triangular patch on either side.
            // The cap therefore meets the inset endpoint directly; retaining both
            // the original and inset vertex would create a zero-area cap spur.
            topBoundary.Add(previousSelected || nextSelected
                ? modified[i]
                : target.Side == ProfileBoundaryChamferSide.Top ? originalTop[i] : lower[i]);
        }
        if (target.Side == ProfileBoundaryChamferSide.Top)
        {
            AddFace("profile-boundary-chamfer:bottom-cap", lower.Reverse().ToArray(), SemanticTopologyRole.BottomFaceBoundaryLoop, $"profile:{profile.Name}.{target.LoopId}");
            foreach (var i in Enumerable.Range(0, n)) AddFace($"profile-boundary-chamfer:lower-side:{i}", [middle[(i + 1) % n], middle[i], lower[i], lower[(i + 1) % n]], SemanticTopologyRole.ExtrusionSideFace, loopSource(profile, target, i));
            AddTransition(middle, originalTop, modified, topBoundary, true);
            AddFace("profile-boundary-chamfer:top-cap", topBoundary, SemanticTopologyRole.TopFaceBoundaryLoop, $"profile:{profile.Name}.{target.LoopId}");
        }
        else
        {
            AddFace("profile-boundary-chamfer:bottom-cap", topBoundary.Reverse<VertexId>().ToArray(), SemanticTopologyRole.BottomFaceBoundaryLoop, $"profile:{profile.Name}.{target.LoopId}");
            AddTransition(lower, originalTop, modified, topBoundary, false);
            foreach (var i in Enumerable.Range(0, n)) AddFace($"profile-boundary-chamfer:upper-side:{i}", [originalTop[i], originalTop[(i + 1) % n], upper[(i + 1) % n], upper[i]], SemanticTopologyRole.ExtrusionSideFace, loopSource(profile, target, i));
            AddFace("profile-boundary-chamfer:top-cap", upper, SemanticTopologyRole.TopFaceBoundaryLoop, $"profile:{profile.Name}.{target.LoopId}");
        }
        if (builder.AddShell(builder.Model.Faces.Select(x => x.Id).ToArray()).Value != 1 || builder.AddBody([new ShellId(1)]).Value != 1) return new(false, null, null, target, ["ProfileBoundaryChamferTopologyPlanInvalid"]);
        var body = new BrepBody(builder.Model, geometry, bindings, points);
        foreach (var junction in ProfileJunctionClassifier.Classify(profile, resolvedLoop).Where(junction =>
                     selected.Contains(IndexOf(resolvedLoop, junction.PredecessorSegmentId)) &&
                     selected.Contains(IndexOf(resolvedLoop, junction.SuccessorSegmentId))))
        {
            descendants.Add(new($"{target.StableId}:{junction.Classification}({junction.VertexId})", "Junction", SemanticTopologyRole.EdgeFinishReplacementFace,
                $"profile:{profile.Name}.{target.LoopId}:{junction.PredecessorSegmentId}->{junction.SuccessorSegmentId}", ParentStableId: target.StableId));
        }
        var correspondence = new SemanticTopologyCorrespondence(target.HostBodyId, descendants, ["ResolvedProfile2D", "ProfileJunctionClassification", "ProfileBoundaryChamferTarget", "ProfileBoundaryChamferSectionStackPlan", "AuthoritativeBRepPlan"]);
        return new(true, body, correspondence, target, ["ProfileBoundaryChamferSectionStackPlan", "ProfileBoundaryChamferExactPlanarFaces"]);

        void AddTransition(VertexId[] originalAtStart, VertexId[] originalAtEnd, VertexId[] insetAtModified, IReadOnlyList<VertexId> cap, bool isTop)
        {
            for (var i = 0; i < n; i++)
            {
                var next = (i + 1) % n;
                if (selected.Contains(i))
                {
                    var face = isTop
                        ? new[] { originalAtStart[i], originalAtStart[next], insetAtModified[next], insetAtModified[i] }
                        : new[] { originalAtEnd[next], originalAtEnd[i], insetAtModified[i], insetAtModified[next] };
                    AddFace($"{target.StableId}:ChamferFace({resolvedLoop.Segments[i].Name})", face, SemanticTopologyRole.EdgeFinishReplacementFace, loopSource(profile, target, i), target.StableId);
                    var insetEdge = Edge(insetAtModified[i], insetAtModified[next]);
                    descendants.Add(new($"{target.StableId}:InsetEdge({resolvedLoop.Segments[i].Name})", "Edge", SemanticTopologyRole.TopBoundary, loopSource(profile, target, i), Edge: insetEdge, ParentStableId: target.StableId));
                }
                else
                {
                    var prevSelected = selected.Contains((i + n - 1) % n);
                    var startBoundary = prevSelected ? insetAtModified[i] : originalAtEnd[i];
                    var endBoundary = selected.Contains(next) ? insetAtModified[next] : originalAtEnd[next];
                    if (isTop)
                    {
                        AddFace($"profile-boundary-chamfer:transition-side:{i}:a", [endBoundary, startBoundary, originalAtStart[i]], SemanticTopologyRole.ExtrusionSideFace, loopSource(profile, target, i));
                        AddFace($"profile-boundary-chamfer:transition-side:{i}:b", [endBoundary, originalAtStart[i], originalAtStart[next]], SemanticTopologyRole.ExtrusionSideFace, loopSource(profile, target, i));
                    }
                    else
                    {
                        if (prevSelected || selected.Contains(next))
                        {
                            // At a Bottom-chain termination the changed cap endpoint lies on
                            // the cap plane.  Splitting through the original lower endpoint
                            // would create a collinear triangle (the old failure mode).
                            AddFace($"profile-boundary-chamfer:transition-side:{i}", [originalAtEnd[i], cap[i], cap[next], originalAtEnd[next]], SemanticTopologyRole.ExtrusionSideFace, loopSource(profile, target, i));
                        }
                        else
                        {
                            AddFace($"profile-boundary-chamfer:transition-side:{i}:a", [originalAtEnd[next], originalAtEnd[i], originalAtStart[i]], SemanticTopologyRole.ExtrusionSideFace, loopSource(profile, target, i));
                            AddFace($"profile-boundary-chamfer:transition-side:{i}:b", [originalAtEnd[next], originalAtStart[i], originalAtStart[next]], SemanticTopologyRole.ExtrusionSideFace, loopSource(profile, target, i));
                        }
                    }
                }
            }
        }
        static string loopSource(ResolvedProfile2D p, ProfileBoundaryChamferTarget t, int i) => $"profile:{p.Name}.{t.LoopId}.{p.Loops.Single(x => x.Name == t.LoopId).Segments[i].Name}";
    }

    private static double SignedArea(IReadOnlyList<(double X, double Y)> points) => points.Select((p, i) => p.X * points[(i + 1) % points.Count].Y - points[(i + 1) % points.Count].X * p.Y).Sum() * .5d;
    private static double Distance((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
    private static int IndexOf(ResolvedProfileLoop2D loop, string segmentId) => loop.Segments.Select((segment, index) => (segment, index)).Single(x => x.segment.Name == segmentId).index;
}

/// <summary>
/// Authoritative M1 materializer for a finite straight Profile-boundary round.  The
/// end faces are quarter-discs in planes normal to the selected segment; this keeps
/// the adjacent Profile junctions sharp and outside the solved topology.
/// </summary>
public static class ProfileStraightEdgeFilletPlanner
{
    private const double Tol = 1e-8;

    public static ProfileStraightEdgeFilletPlanResult TryPlan(ResolvedProfile2D profile, ProfileBoundaryChamferTarget target, double radius, double clearance)
    {
        ProfileStraightEdgeFilletPlanResult Fail(string code) => new(false, null, null, null, [code]);
        var loop = profile.Loops.SingleOrDefault(x => x.Name == target.LoopId);
        if (loop is null) return Fail("ProfileBoundaryFilletLoopUnknown");
        if (!loop.IsOuter || profile.Loops.Count != 1) return Fail("ProfileBoundaryFilletInnerLoopUnsupported");
        if (target.ChainKind == ProfileBoundaryChamferChainKind.ClosedLoop) return Fail("ProfileBoundaryFilletLoopTopologyNotMaterialized");
        if (target.ChainKind == ProfileBoundaryChamferChainKind.OpenConnectedChain || target.SegmentIds.Count != 1) return Fail("ProfileBoundaryFilletJunctionTopologyNotMaterialized");
        if (loop.Segments.Any(x => x.Geometry is not LineArcLineSegment2D)) return Fail("ProfileBoundaryFilletEndpointTerminationUnsupported");
        var index = loop.Segments.Select((x, i) => (x, i)).Where(x => x.x.Name == target.SegmentIds[0]).Select(x => x.i).DefaultIfEmpty(-1).Single();
        if (index < 0) return Fail("ProfileBoundaryFilletSegmentUnknown");
        var line = (LineArcLineSegment2D)loop.Segments[index].Geometry;
        var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y; var length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 2d * clearance + Tol) return Fail("ProfileBoundaryFilletSegmentTooShort");
        var start = profile.LocalStartDepth ?? -1d; var end = profile.LocalEndDepth ?? 1d; var thickness = end - start;
        if (radius >= thickness - Tol) return Fail("ProfileBoundaryFilletRadiusExceedsHost");
        var signedArea = loop.Segments.Cast<ResolvedProfileSegment2D>().Select(x => (LineArcLineSegment2D)x.Geometry).Select((x, i) => x.Start.X * ((LineArcLineSegment2D)loop.Segments[(i + 1) % loop.Segments.Count].Geometry).Start.Y - ((LineArcLineSegment2D)loop.Segments[(i + 1) % loop.Segments.Count].Geometry).Start.X * x.Start.Y).Sum() * .5d;
        if (Math.Abs(signedArea) <= Tol) return Fail("ProfileBoundaryFilletEndpointTerminationUnsupported");
        var frame = profile.EffectiveConstructionPlane;
        var tangent = Direction3D.Create(frame.ToWorldDirection(new Vector3D(dx / length, dy / length, 0d)));
        var inward2 = signedArea > 0d ? (-dy / length, dx / length) : (dy / length, -dx / length);
        var inward = Direction3D.Create(frame.ToWorldDirection(new Vector3D(inward2.Item1, inward2.Item2, 0d)));
        var axis = frame.AxisZ;
        var station = target.Side == ProfileBoundaryChamferSide.Top ? end : start;
        var axialIntoBody = target.Side == ProfileBoundaryChamferSide.Top ? -axis.ToVector() : axis.ToVector();
        var sourceStart = frame.ToWorld(line.Start, station); var sourceEnd = frame.ToWorld(line.End, station);
        var spanStart = sourceStart + tangent.ToVector() * clearance; var spanEnd = sourceEnd - tangent.ToVector() * clearance;
        var centerStart = spanStart + inward.ToVector() * radius + axialIntoBody * radius;
        var centerEnd = spanEnd + inward.ToVector() * radius + axialIntoBody * radius;
        var capStart = spanStart + inward.ToVector() * radius; var capEnd = spanEnd + inward.ToVector() * radius;
        var sideStart = spanStart + axialIntoBody * radius; var sideEnd = spanEnd + axialIntoBody * radius;
        var plan = new ProfileStraightEdgeFilletPlan(target, radius, clearance, sourceStart, sourceEnd, spanStart, spanEnd, tangent, inward, axis, centerStart, centerEnd, capStart, capEnd, sideStart, sideEnd);
        return BuildBody(profile, loop, index, plan, start, end);
    }

    private static ProfileStraightEdgeFilletPlanResult BuildBody(ResolvedProfile2D profile, ResolvedProfileLoop2D loop, int selected, ProfileStraightEdgeFilletPlan plan, double start, double end)
    {
        ProfileStraightEdgeFilletPlanResult Fail(string code) => new(false, null, null, plan, [code]);
        var builder = new TopologyBuilder(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel(); var points = new Dictionary<VertexId, Point3D>();
        var vertices = new Dictionary<string, VertexId>(StringComparer.Ordinal); var edges = new Dictionary<(VertexId, VertexId), EdgeId>(); var descendants = new List<SemanticTopologyDescendant>();
        VertexId Vertex(string key, Point3D point) { if (vertices.TryGetValue(key, out var id)) return id; id = builder.AddVertex(); vertices[key] = id; points[id] = point; return id; }
        var curveId = 1; var surfaceId = 1;
        EdgeId LineEdge(VertexId a, VertexId b)
        {
            var key = a.Value < b.Value ? (a, b) : (b, a); if (edges.TryGetValue(key, out var id)) return id;
            id = builder.AddEdge(a, b); edges[key] = id; var from = points[a]; var to = points[b];
            var curve = new CurveGeometryId(curveId++); geometry.AddCurve(curve, CurveGeometry.FromLine(new Line3Curve(from, Direction3D.Create(to - from))));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(id, curve, new ParameterInterval(0d, (to - from).Length), true)); return id;
        }
        EdgeId ArcEdge(VertexId a, VertexId b, Point3D center)
        {
            var key = a.Value < b.Value ? (a, b) : (b, a); if (edges.TryGetValue(key, out var id)) return id;
            id = builder.AddEdge(a, b); edges[key] = id;
            // X is from centre to the cap contact.  With tangent x cap-normal, +pi/2 reaches the side contact.
            var capNormal = plan.Target.Side == ProfileBoundaryChamferSide.Top ? plan.ExtrusionAxis : Direction3D.Create(-plan.ExtrusionAxis.ToVector());
            var curve = new CurveGeometryId(curveId++); geometry.AddCurve(curve, CurveGeometry.FromCircle(new Circle3Curve(center, plan.Tangent, plan.Radius, capNormal)));
            var capPoint = center + capNormal.ToVector() * plan.Radius;
            var capIsStart = (points[a] - capPoint).Length <= Tol;
            var top = plan.Target.Side == ProfileBoundaryChamferSide.Top;
            var trim = top ? new ParameterInterval(0d, Math.PI / 2d) : new ParameterInterval(-Math.PI / 2d, 0d);
            // The binding trim is monotone; coedge orientation carries the opposite endpoint order.
            bindings.AddEdgeBinding(new EdgeGeometryBinding(id, curve, trim, top ? capIsStart : !capIsStart)); return id;
        }
        FaceId Face(string stable, IReadOnlyList<(VertexId Vertex, bool Arc, Point3D? Center)> boundary, SurfaceGeometry surface, SemanticTopologyRole role, string source, string? parent = null)
        {
            var loopId = builder.AllocateLoopId(); var coedges = Enumerable.Range(0, boundary.Count).Select(_ => new CoedgeId(builder.Model.Coedges.Count() + 1 + _)).ToArray();
            for (var i = 0; i < boundary.Count; i++)
            {
                var current = boundary[i]; var next = boundary[(i + 1) % boundary.Count];
                var edge = current.Arc ? ArcEdge(current.Vertex, next.Vertex, current.Center!.Value) : LineEdge(current.Vertex, next.Vertex);
                var modelEdge = builder.Model.Edges.Single(x => x.Id == edge);
                builder.AddCoedge(new Coedge(coedges[i], edge, loopId, coedges[(i + 1) % boundary.Count], coedges[(i + boundary.Count - 1) % boundary.Count], modelEdge.StartVertexId != current.Vertex));
            }
            builder.AddLoop(new Loop(loopId, coedges)); var face = builder.AddFace([loopId]); var sid = new SurfaceGeometryId(surfaceId++); geometry.AddSurface(sid, surface); bindings.AddFaceBinding(new FaceGeometryBinding(face, sid, true));
            descendants.Add(new(stable, "Face", role, source, Face: face, ParentStableId: parent)); return face;
        }
        static (VertexId Vertex, bool Arc, Point3D? Center) L(VertexId v) => (v, false, null);
        static (VertexId Vertex, bool Arc, Point3D? Center) A(VertexId v, Point3D c) => (v, true, c);

        var frame = profile.EffectiveConstructionPlane; var n = loop.Segments.Count; var lower = new VertexId[n]; var upper = new VertexId[n];
        for (var i = 0; i < n; i++)
        {
            var line = (LineArcLineSegment2D)loop.Segments[i].Geometry;
            lower[i] = Vertex($"lower:{i}", frame.ToWorld(line.Start, start)); upper[i] = Vertex($"upper:{i}", frame.ToWorld(line.Start, end));
        }
        var sharpStart = Vertex("sharp-start", plan.SpanStart); var sharpEnd = Vertex("sharp-end", plan.SpanEnd);
        var capStart = Vertex("cap-start", plan.CapContactStart); var capEnd = Vertex("cap-end", plan.CapContactEnd);
        var sideStart = Vertex("side-start", plan.SideContactStart); var sideEnd = Vertex("side-end", plan.SideContactEnd);
        var isTop = plan.Target.Side == ProfileBoundaryChamferSide.Top;
        var sourceStart = isTop ? upper[selected] : lower[selected]; var sourceEnd = isTop ? upper[(selected + 1) % n] : lower[(selected + 1) % n];
        var capPlane = SurfaceGeometry.FromPlane(new PlaneSurface(isTop ? plan.SourceStart : frame.ToWorld((0d, 0d), start), isTop ? plan.ExtrusionAxis : Direction3D.Create(-plan.ExtrusionAxis.ToVector()), plan.Tangent));
        var otherCapPlane = SurfaceGeometry.FromPlane(new PlaneSurface(isTop ? frame.ToWorld((0d, 0d), start) : frame.ToWorld((0d, 0d), end), isTop ? Direction3D.Create(-plan.ExtrusionAxis.ToVector()) : plan.ExtrusionAxis, plan.Tangent));
        var capBoundary = new List<(VertexId Vertex, bool Arc, Point3D? Center)>();
        for (var i = 0; i < n; i++)
        {
            capBoundary.Add(L(isTop ? upper[i] : lower[i]));
            if (i == selected) capBoundary.AddRange([L(sharpStart), L(capStart), L(capEnd), L(sharpEnd)]);
        }
        Face(isTop ? $"{plan.Target.StableId}:top-cap" : $"{plan.Target.StableId}:bottom-cap", (isTop ? capBoundary : capBoundary.AsEnumerable().Reverse()).ToArray(), capPlane, isTop ? SemanticTopologyRole.TopFaceBoundaryLoop : SemanticTopologyRole.BottomFaceBoundaryLoop, $"profile:{profile.Name}.{loop.Name}", plan.Target.StableId);
        Face(isTop ? $"{plan.Target.StableId}:bottom-cap" : $"{plan.Target.StableId}:top-cap", (isTop ? lower.Reverse() : upper).Select(L).ToArray(), otherCapPlane, isTop ? SemanticTopologyRole.BottomFaceBoundaryLoop : SemanticTopologyRole.TopFaceBoundaryLoop, $"profile:{profile.Name}.{loop.Name}", plan.Target.StableId);

        for (var i = 0; i < n; i++)
        {
            var next = (i + 1) % n; var line = (LineArcLineSegment2D)loop.Segments[i].Geometry;
            var localDx = line.End.X - line.Start.X; var localDy = line.End.Y - line.Start.Y; var localLength = Math.Sqrt(localDx * localDx + localDy * localDy);
            var outNormal = Direction3D.Create(frame.ToWorldDirection(new Vector3D(localDy / localLength, -localDx / localLength, 0d)));
            var localTangent = Direction3D.Create(frame.ToWorldDirection(new Vector3D(localDx / localLength, localDy / localLength, 0d)));
            var surface = SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld(line.Start, start), outNormal, localTangent));
            if (i != selected)
                Face($"profile:{profile.Name}.{loop.Name}.{loop.Segments[i].Name}:side", [L(lower[i]), L(lower[next]), L(upper[next]), L(upper[i])], surface, SemanticTopologyRole.ExtrusionSideFace, $"profile:{profile.Name}.{loop.Name}.{loop.Segments[i].Name}");
            else
            {
                var a = isTop ? new[] { L(lower[i]), L(lower[next]), L(upper[next]), L(sharpEnd), L(sideEnd), L(sideStart), L(sharpStart), L(upper[i]) }
                    : new[] { L(lower[i]), L(sharpStart), L(sideStart), L(sideEnd), L(sharpEnd), L(lower[next]), L(upper[next]), L(upper[i]) };
                Face($"profile:{profile.Name}.{loop.Name}.{loop.Segments[i].Name}:trimmed-side", a, surface, SemanticTopologyRole.ExtrusionSideFace, $"profile:{profile.Name}.{loop.Name}.{loop.Segments[i].Name}", plan.Target.StableId);
            }
        }
        var cylinder = SurfaceGeometry.FromCylinder(new CylinderSurface(plan.CylinderCenterlineStart, plan.Tangent, plan.Radius, isTop ? plan.ExtrusionAxis : Direction3D.Create(-plan.ExtrusionAxis.ToVector())));
        Face($"{plan.Target.StableId}:FilletSurface", [L(capStart), A(capEnd, plan.CylinderCenterlineEnd), L(sideEnd), A(sideStart, plan.CylinderCenterlineStart)], cylinder, SemanticTopologyRole.FilletSurface, plan.Target.StableId, plan.Target.StableId);
        var startTerm = SurfaceGeometry.FromPlane(new PlaneSurface(plan.SpanStart, Direction3D.Create(-plan.Tangent.ToVector()), isTop ? plan.ExtrusionAxis : Direction3D.Create(-plan.ExtrusionAxis.ToVector())));
        var endTerm = SurfaceGeometry.FromPlane(new PlaneSurface(plan.SpanEnd, plan.Tangent, isTop ? plan.ExtrusionAxis : Direction3D.Create(-plan.ExtrusionAxis.ToVector())));
        Face($"{plan.Target.StableId}:StartTerminationFace", [L(sharpStart), A(capStart, plan.CylinderCenterlineStart), L(sideStart)], startTerm, SemanticTopologyRole.StartTerminationFace, plan.Target.StableId, plan.Target.StableId);
        Face($"{plan.Target.StableId}:EndTerminationFace", [L(sharpEnd), A(sideEnd, plan.CylinderCenterlineEnd), L(capEnd)], endTerm, SemanticTopologyRole.EndTerminationFace, plan.Target.StableId, plan.Target.StableId);

        var shell = builder.AddShell(builder.Model.Faces.Select(x => x.Id).ToArray()); builder.AddBody([shell]);
        var body = new BrepBody(builder.Model, geometry, bindings, points);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        if (!validation.IsSuccess) return Fail("ProfileBoundaryFilletTopologyPlanInvalid");
        AddEdgeDescendant("CapContactEdge", LineEdge(capStart, capEnd), SemanticTopologyRole.CapContactEdge);
        AddEdgeDescendant("SideContactEdge", LineEdge(sideStart, sideEnd), SemanticTopologyRole.SideContactEdge);
        AddEdgeDescendant("StartEndpointArc", ArcEdge(capStart, sideStart, plan.CylinderCenterlineStart), SemanticTopologyRole.StartEndpointArc);
        AddEdgeDescendant("EndEndpointArc", ArcEdge(capEnd, sideEnd, plan.CylinderCenterlineEnd), SemanticTopologyRole.EndEndpointArc);
        AddEdgeDescendant("RetainedStartSharpEdge", LineEdge(sourceStart, sharpStart), SemanticTopologyRole.RetainedStartSharpEdge);
        AddEdgeDescendant("RetainedEndSharpEdge", LineEdge(sharpEnd, sourceEnd), SemanticTopologyRole.RetainedEndSharpEdge);
        var correspondence = new SemanticTopologyCorrespondence(plan.Target.HostBodyId, descendants, ["ResolvedProfile2D", "ProfileStraightEdgeFilletPlan", "FilletSpanInset", "AuthoritativeBRepPlan"]);
        return new(true, body, correspondence, plan, ["ProfileStraightEdgeFilletPlan", "ProfileBoundaryFilletExactQuarterCylinder", "ProfileBoundaryFilletEndpointTerminationFaces"]);

        void AddEdgeDescendant(string name, EdgeId edge, SemanticTopologyRole role) => descendants.Add(new($"{plan.Target.StableId}:{name}", "Edge", role, plan.Target.StableId, Edge: edge, ParentStableId: plan.Target.StableId));
    }
}

/// <summary>
/// The authoritative Profile-fillet dispatch.  M1 remains a self-contained
/// finite-span construction; M2 owns the first connected topology instead of
/// trying to join two already-emitted M1 bodies.
/// </summary>
public enum ProfileFilletRollEndKind { EndpointTermination, ConvexJunction }
public sealed record ProfileFilletStraightRollPlan(string SegmentId, Direction3D Tangent, Direction3D InwardNormal, Point3D ExternalCenter, Point3D JunctionCenter, ProfileFilletRollEndKind ExternalEnd, ProfileFilletRollEndKind JunctionEnd);
public sealed record ProfileConvexSphericalJunctionPlan(string VertexId, ProfileJunctionClassification Classification, Point3D Center, double Radius, Point3D CapContact, Point3D SideAContact, Point3D SideBContact);
public sealed record ProfileFilletShellPlan(ProfileBoundaryChamferTarget Target, double Radius, double EndClearance, IReadOnlyList<ProfileFilletStraightRollPlan> Rolls, ProfileConvexSphericalJunctionPlan Junction, string EndpointPolicy = "ExternalEndpointsOnly");
public sealed record ProfileFilletShellPlanResult(bool Succeeded, BrepBody? Body, SemanticTopologyCorrespondence? Correspondence, ProfileFilletShellPlan? Plan, ProfileStraightEdgeFilletPlan? SingleSegmentPlan, IReadOnlyList<string> Diagnostics);

public static class ProfileFilletShellPlanner
{
    private const double Tol = 1e-8;

    public static ProfileFilletShellPlanResult TryPlan(ResolvedProfile2D profile, ProfileBoundaryChamferTarget target, double radius, double clearance)
    {
        ProfileFilletShellPlanResult Fail(string code) => new(false, null, null, null, null, [code]);
        if (target.ChainKind == ProfileBoundaryChamferChainKind.SingleSegment)
        {
            var m1 = ProfileStraightEdgeFilletPlanner.TryPlan(profile, target, radius, clearance);
            return new(m1.Succeeded, m1.Body, m1.Correspondence, null, m1.Plan, m1.Diagnostics);
        }
        if (target.ChainKind == ProfileBoundaryChamferChainKind.ClosedLoop) return Fail("ProfileBoundaryFilletLoopTopologyNotMaterialized");
        if (target.SegmentIds.Count != 2) return Fail("ProfileBoundaryFilletJunctionTopologyNotMaterialized");
        var loop = profile.Loops.SingleOrDefault(x => x.Name == target.LoopId);
        if (loop is null) return Fail("ProfileBoundaryFilletLoopUnknown");
        if (!loop.IsOuter || profile.Loops.Count != 1) return Fail("ProfileBoundaryFilletInnerLoopUnsupported");
        if (loop.Segments.Any(segment => segment.Geometry is not LineArcLineSegment2D)) return Fail("ProfileBoundaryFilletSegmentKindUnsupported");
        if (!double.IsFinite(radius) || radius <= Tol) return Fail("ProfileBoundaryFilletRadiusMustBePositive");
        if (!double.IsFinite(clearance) || clearance <= Tol) return Fail("ProfileBoundaryFilletEndClearanceMustBePositive");
        var a = IndexOf(loop, target.SegmentIds[0]); var b = IndexOf(loop, target.SegmentIds[1]); var count = loop.Segments.Count;
        if (a < 0 || b < 0) return Fail("ProfileBoundaryFilletSegmentUnknown");
        if ((a + 1) % count != b) return Fail("ProfileBoundaryFilletDisconnectedChain");
        var classification = ProfileJunctionClassifier.Classify(profile, loop).Single(x => x.PredecessorSegmentId == target.SegmentIds[0] && x.SuccessorSegmentId == target.SegmentIds[1]);
        if (classification.Classification == ProfileJunctionKind.ReflexProfileJunction) return Fail("ProfileBoundaryFilletReflexJunctionUnsupported");
        if (classification.Classification == ProfileJunctionKind.Collinear) return Fail("ProfileBoundaryFilletConvexJunctionCollinear");
        if (classification.Classification != ProfileJunctionKind.ConvexProfileJunction) return Fail("ProfileBoundaryFilletConvexJunctionDegenerate");
        if (Math.Abs(classification.MaterialInteriorAngleRadians - Math.PI / 2d) > Tol) return Fail("ProfileBoundaryFilletConvexAngleUnsupported");
        var lineA = (LineArcLineSegment2D)loop.Segments[a].Geometry; var lineB = (LineArcLineSegment2D)loop.Segments[b].Geometry;
        var lengthA = Length(lineA); var lengthB = Length(lineB);
        if (lengthA <= clearance + radius + Tol || lengthB <= clearance + radius + Tol) return Fail("ProfileBoundaryFilletConvexRadiusTooLarge");
        var start = profile.LocalStartDepth ?? -1d; var end = profile.LocalEndDepth ?? 1d;
        if (radius >= end - start - Tol) return Fail("ProfileBoundaryFilletRadiusExceedsHost");
        return Build(profile, loop, target, radius, clearance, a, b, lineA, lineB, classification, start, end);
    }

    private static ProfileFilletShellPlanResult Build(ResolvedProfile2D profile, ResolvedProfileLoop2D loop, ProfileBoundaryChamferTarget target, double radius, double clearance, int a, int b, LineArcLineSegment2D lineA, LineArcLineSegment2D lineB, ProfileJunctionClassification classification, double start, double end)
    {
        ProfileFilletShellPlanResult Fail(string code) => new(false, null, null, null, null, [code]);
        var frame = profile.EffectiveConstructionPlane; var isTop = target.Side == ProfileBoundaryChamferSide.Top;
        var capOut = isTop ? frame.AxisZ : Direction3D.Create(-frame.AxisZ.ToVector()); var axialInto = -capOut.ToVector(); var station = isTop ? end : start;
        var ta = Direction(lineA, frame); var tb = Direction(lineB, frame); var signedArea = SignedArea(loop);
        if (Math.Abs(signedArea) <= Tol) return Fail("ProfileBoundaryFilletConvexJunctionDegenerate");
        var na = Inward(lineA, signedArea, frame); var nb = Inward(lineB, signedArea, frame);
        if (Math.Abs(ta.ToVector().Dot(tb.ToVector())) > Tol || Math.Abs(na.ToVector().Dot(tb.ToVector()) - 1d) > Tol || Math.Abs(nb.ToVector().Dot(-ta.ToVector()) - 1d) > Tol)
            return Fail("ProfileBoundaryFilletConvexAngleUnsupported");
        var sourceAStart = frame.ToWorld(lineA.Start, station); var sourceBEnd = frame.ToWorld(lineB.End, station); var vertex = frame.ToWorld(lineA.End, station);
        var sharpA = sourceAStart + ta.ToVector() * clearance; var sharpB = sourceBEnd - tb.ToVector() * clearance;
        var capA = sharpA + na.ToVector() * radius; var sideAExternal = sharpA + axialInto * radius; var centerA = sharpA + na.ToVector() * radius + axialInto * radius;
        var capB = sharpB + nb.ToVector() * radius; var sideBExternal = sharpB + axialInto * radius; var centerB = sharpB + nb.ToVector() * radius + axialInto * radius;
        var center = vertex + na.ToVector() * radius + nb.ToVector() * radius + axialInto * radius;
        var capJunction = center + capOut.ToVector() * radius; var sideA = center - na.ToVector() * radius; var sideB = center - nb.ToVector() * radius; var verticalDepth = vertex + axialInto * radius;
        var rolls = new[] { new ProfileFilletStraightRollPlan(loop.Segments[a].Name, ta, na, centerA, center, ProfileFilletRollEndKind.EndpointTermination, ProfileFilletRollEndKind.ConvexJunction), new ProfileFilletStraightRollPlan(loop.Segments[b].Name, tb, nb, centerB, center, ProfileFilletRollEndKind.EndpointTermination, ProfileFilletRollEndKind.ConvexJunction) };
        var junction = new ProfileConvexSphericalJunctionPlan(classification.VertexId, classification, center, radius, capJunction, sideA, sideB);
        var plan = new ProfileFilletShellPlan(target, radius, clearance, rolls, junction);

        var builder = new TopologyBuilder(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel(); var points = new Dictionary<VertexId, Point3D>();
        var vertices = new Dictionary<string, VertexId>(StringComparer.Ordinal); var edges = new Dictionary<(VertexId, VertexId), EdgeId>(); var descendants = new List<SemanticTopologyDescendant>(); var curveId = 1; var surfaceId = 1;
        VertexId Vertex(string key, Point3D point) { if (vertices.TryGetValue(key, out var id)) return id; id = builder.AddVertex(); vertices[key] = id; points[id] = point; return id; }
        EdgeId LineEdge(VertexId x, VertexId y) { var key = x.Value < y.Value ? (x, y) : (y, x); if (edges.TryGetValue(key, out var id)) return id; id = builder.AddEdge(x, y); edges[key] = id; var curve = new CurveGeometryId(curveId++); geometry.AddCurve(curve, CurveGeometry.FromLine(new Line3Curve(points[x], Direction3D.Create(points[y] - points[x])))); bindings.AddEdgeBinding(new EdgeGeometryBinding(id, curve, new ParameterInterval(0d, (points[y] - points[x]).Length), true)); return id; }
        EdgeId ArcEdge(VertexId x, VertexId y, Point3D c) { var key = x.Value < y.Value ? (x, y) : (y, x); if (edges.TryGetValue(key, out var id)) return id; var ux = points[x] - c; var uy = points[y] - c; var normal = ux.Cross(uy); if (normal.Length <= Tol) throw new InvalidOperationException($"ProfileBoundaryFilletConvexTrimDegenerate:{x.Value}->{y.Value}"); id = builder.AddEdge(x, y); edges[key] = id; var curve = new CurveGeometryId(curveId++); geometry.AddCurve(curve, CurveGeometry.FromCircle(new Circle3Curve(c, Direction3D.Create(normal), radius, Direction3D.Create(ux)))); bindings.AddEdgeBinding(new EdgeGeometryBinding(id, curve, new ParameterInterval(0d, Math.PI / 2d), true)); return id; }
        FaceId Face(string stable, IReadOnlyList<(VertexId Vertex, bool Arc, Point3D? Center)> boundary, SurfaceGeometry surface, SemanticTopologyRole role, string source, string? parent = null) { var loopId = builder.AllocateLoopId(); var coedges = Enumerable.Range(0, boundary.Count).Select(_ => builder.AllocateCoedgeId()).ToArray(); for (var i = 0; i < boundary.Count; i++) { var current = boundary[i]; var next = boundary[(i + 1) % boundary.Count]; var edge = current.Arc ? ArcEdge(current.Vertex, next.Vertex, current.Center!.Value) : LineEdge(current.Vertex, next.Vertex); var modelEdge = builder.Model.Edges.Single(item => item.Id == edge); builder.AddCoedge(new Coedge(coedges[i], edge, loopId, coedges[(i + 1) % boundary.Count], coedges[(i + boundary.Count - 1) % boundary.Count], modelEdge.StartVertexId != current.Vertex)); } builder.AddLoop(new Loop(loopId, coedges)); var face = builder.AddFace([loopId]); var surfaceIdValue = new SurfaceGeometryId(surfaceId++); geometry.AddSurface(surfaceIdValue, surface); bindings.AddFaceBinding(new FaceGeometryBinding(face, surfaceIdValue, true)); descendants.Add(new(stable, "Face", role, source, Face: face, ParentStableId: parent)); return face; }
        static (VertexId Vertex, bool Arc, Point3D? Center) L(VertexId x) => (x, false, null); static (VertexId Vertex, bool Arc, Point3D? Center) A(VertexId x, Point3D c) => (x, true, c);

        var n = loop.Segments.Count; var lower = new VertexId[n]; var upper = new VertexId[n];
        for (var i = 0; i < n; i++) { var line = (LineArcLineSegment2D)loop.Segments[i].Geometry; lower[i] = Vertex($"lower:{i}", frame.ToWorld(line.Start, start)); upper[i] = Vertex($"upper:{i}", frame.ToWorld(line.Start, end)); }
        var cap = isTop ? upper : lower; var opposite = isTop ? lower : upper; var sharpAV = Vertex("sharp-a", sharpA); var sharpBV = Vertex("sharp-b", sharpB); var capAV = Vertex("cap-a", capA); var capBV = Vertex("cap-b", capB); var sideAEV = Vertex("side-a-external", sideAExternal); var sideBEV = Vertex("side-b-external", sideBExternal); var capJV = Vertex("junction-cap", capJunction); var sideAV = Vertex("junction-side-a", sideA); var sideBV = Vertex("junction-side-b", sideB); var depthV = Vertex("junction-vertical-depth", verticalDepth);
        var capBoundary = new List<(VertexId Vertex, bool Arc, Point3D? Center)>();
        for (var i = 0; i < n; i++) { if (i == a) { capBoundary.AddRange([L(cap[i]), L(sharpAV), L(capAV), L(capJV), L(capBV), L(sharpBV)]); continue; } if (i == b) continue; capBoundary.Add(L(cap[i])); }
        var capPlane = SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld((0d, 0d), station), capOut, ta)); var oppositePlane = SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld((0d, 0d), isTop ? start : end), Direction3D.Create(-capOut.ToVector()), ta));
        Face($"{target.StableId}:{(isTop ? "top" : "bottom")}-cap", isTop ? capBoundary : capBoundary.AsEnumerable().Reverse().ToArray(), capPlane, isTop ? SemanticTopologyRole.TopFaceBoundaryLoop : SemanticTopologyRole.BottomFaceBoundaryLoop, $"profile:{profile.Name}.{loop.Name}", target.StableId);
        Face($"{target.StableId}:{(isTop ? "bottom" : "top")}-cap", (isTop ? lower.Reverse() : upper).Select(L).ToArray(), oppositePlane, isTop ? SemanticTopologyRole.BottomFaceBoundaryLoop : SemanticTopologyRole.TopFaceBoundaryLoop, $"profile:{profile.Name}.{loop.Name}", target.StableId);
        for (var i = 0; i < n; i++) { var next = (i + 1) % n; var line = (LineArcLineSegment2D)loop.Segments[i].Geometry; var tangent = Direction(line, frame); var inward = Inward(line, signedArea, frame); var sideSurface = SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld(line.Start, start), Direction3D.Create(-inward.ToVector()), tangent)); if (i == a) Face($"profile:{profile.Name}.{loop.Name}.{loop.Segments[i].Name}:trimmed-side", [L(opposite[i]), L(opposite[next]), L(depthV), L(sideAV), L(sideAEV), L(sharpAV), L(cap[i])], sideSurface, SemanticTopologyRole.ExtrusionSideFace, $"profile:{profile.Name}.{loop.Name}.{loop.Segments[i].Name}", target.StableId); else if (i == b) Face($"profile:{profile.Name}.{loop.Name}.{loop.Segments[i].Name}:trimmed-side", [L(opposite[i]), L(opposite[next]), L(cap[next]), L(sharpBV), L(sideBEV), L(sideBV), L(depthV)], sideSurface, SemanticTopologyRole.ExtrusionSideFace, $"profile:{profile.Name}.{loop.Name}.{loop.Segments[i].Name}", target.StableId); else Face($"profile:{profile.Name}.{loop.Name}.{loop.Segments[i].Name}:side", [L(lower[i]), L(lower[next]), L(upper[next]), L(upper[i])], sideSurface, SemanticTopologyRole.ExtrusionSideFace, $"profile:{profile.Name}.{loop.Name}.{loop.Segments[i].Name}"); }
        var cylinderA = SurfaceGeometry.FromCylinder(new CylinderSurface(centerA, ta, radius, capOut)); var cylinderB = SurfaceGeometry.FromCylinder(new CylinderSurface(center, tb, radius, capOut)); var sphere = SurfaceGeometry.FromSphere(new SphereSurface(center, capOut, radius, Direction3D.Create(-na.ToVector())));
        Face($"{target.StableId}:FilletSurface({loop.Segments[a].Name})", [L(capAV), A(capJV, center), L(sideAV), A(sideAEV, centerA)], cylinderA, SemanticTopologyRole.FilletSurface, $"profile:{profile.Name}.{loop.Name}.{loop.Segments[a].Name}", target.StableId);
        Face($"{target.StableId}:FilletSurface({loop.Segments[b].Name})", [L(capJV), A(capBV, centerB), L(sideBEV), A(sideBV, center)], cylinderB, SemanticTopologyRole.FilletSurface, $"profile:{profile.Name}.{loop.Name}.{loop.Segments[b].Name}", target.StableId);
        Face($"{target.StableId}:ConvexJunctionPatch({loop.Segments[a].Name},{loop.Segments[b].Name})", [A(capJV, center), A(sideBV, center), A(sideAV, center)], sphere, SemanticTopologyRole.ConvexJunctionPatch, classification.VertexId, target.StableId);
        Face($"{target.StableId}:ConvexJunctionSupport", [L(depthV), A(sideBV, center), L(sideAV)], SurfaceGeometry.FromPlane(new PlaneSurface(verticalDepth, capOut, ta)), SemanticTopologyRole.EdgeFinishReplacementFace, classification.VertexId, target.StableId);
        Face($"{target.StableId}:StartTerminationFace", [L(sharpAV), A(capAV, centerA), L(sideAEV)], SurfaceGeometry.FromPlane(new PlaneSurface(sharpA, Direction3D.Create(-ta.ToVector()), capOut)), SemanticTopologyRole.StartTerminationFace, target.StableId, target.StableId);
        Face($"{target.StableId}:EndTerminationFace", [L(sharpBV), A(sideBEV, centerB), L(capBV)], SurfaceGeometry.FromPlane(new PlaneSurface(sharpB, tb, capOut)), SemanticTopologyRole.EndTerminationFace, target.StableId, target.StableId);
        var shell = builder.AddShell(builder.Model.Faces.Select(face => face.Id).ToArray()); builder.AddBody([shell]); var body = new BrepBody(builder.Model, geometry, bindings, points); var validation = BrepBindingValidator.Validate(body, true); if (!validation.IsSuccess) return Fail("ProfileBoundaryFilletConvexTopologyPlanInvalid");
        void AddEdge(string name, EdgeId edge, SemanticTopologyRole role, string source) => descendants.Add(new($"{target.StableId}:{name}", "Edge", role, source, Edge: edge, ParentStableId: target.StableId));
        AddEdge($"CapContactEdge({loop.Segments[a].Name})", LineEdge(capAV, capJV), SemanticTopologyRole.CapContactEdge, loop.Segments[a].Provenance.StableId); AddEdge($"CapContactEdge({loop.Segments[b].Name})", LineEdge(capJV, capBV), SemanticTopologyRole.CapContactEdge, loop.Segments[b].Provenance.StableId); AddEdge($"SideContactEdge({loop.Segments[a].Name})", LineEdge(sideAV, sideAEV), SemanticTopologyRole.SideContactEdge, loop.Segments[a].Provenance.StableId); AddEdge($"SideContactEdge({loop.Segments[b].Name})", LineEdge(sideBEV, sideBV), SemanticTopologyRole.SideContactEdge, loop.Segments[b].Provenance.StableId); AddEdge("JunctionToRollA", ArcEdge(capJV, sideAV, center), SemanticTopologyRole.JunctionToRollA, classification.VertexId); AddEdge("JunctionToRollB", ArcEdge(sideBV, capJV, center), SemanticTopologyRole.JunctionToRollB, classification.VertexId);
        var correspondence = new SemanticTopologyCorrespondence(target.HostBodyId, descendants, ["ResolvedProfile2D", "ProfileFilletShellPlan", "StraightRoll", "ConvexSphericalJunction", "AuthoritativeBRepPlan"]);
        return new(true, body, correspondence, plan, null, ["ProfileFilletShellPlan", "ProfileBoundaryFilletExactQuarterCylinders", "ProfileBoundaryFilletConvexSphericalJunction"]);
    }

    private static int IndexOf(ResolvedProfileLoop2D loop, string segmentId) => loop.Segments.Select((segment, index) => (segment, index)).Where(item => item.segment.Name == segmentId).Select(item => item.index).DefaultIfEmpty(-1).Single();
    private static double Length(LineArcLineSegment2D line) => Math.Sqrt(Math.Pow(line.End.X - line.Start.X, 2d) + Math.Pow(line.End.Y - line.Start.Y, 2d));
    private static Direction3D Direction(LineArcLineSegment2D line, ConstructionPlane frame) { var length = Length(line); return Direction3D.Create(frame.ToWorldDirection(new Vector3D((line.End.X - line.Start.X) / length, (line.End.Y - line.Start.Y) / length, 0d))); }
    private static Direction3D Inward(LineArcLineSegment2D line, double signedArea, ConstructionPlane frame) { var length = Length(line); var normal = signedArea > 0d ? new Vector3D(-(line.End.Y - line.Start.Y) / length, (line.End.X - line.Start.X) / length, 0d) : new Vector3D((line.End.Y - line.Start.Y) / length, -(line.End.X - line.Start.X) / length, 0d); return Direction3D.Create(frame.ToWorldDirection(normal)); }
    private static double SignedArea(ResolvedProfileLoop2D loop) => loop.Segments.Cast<ResolvedProfileSegment2D>().Select(segment => (LineArcLineSegment2D)segment.Geometry).Select((line, index) => line.Start.X * ((LineArcLineSegment2D)loop.Segments[(index + 1) % loop.Segments.Count].Geometry).Start.Y - ((LineArcLineSegment2D)loop.Segments[(index + 1) % loop.Segments.Count].Geometry).Start.X * line.Start.Y).Sum() * .5d;
}

/// <summary>
/// Conservative source-space corridor check used before a composed host can ever
/// attempt a future M2 materialization. M1 still rejects composed hosts after a
/// disjoint proof; a collision receives the more useful feature-specific code.
/// </summary>
public sealed record ProfileStraightEdgeFilletCorridor(
    string EdgeFinishId, string ProfileId, string LoopId, string SegmentId,
    double Radius, double EndClearance, double From, double To,
    (double X, double Y) SpanStart, (double X, double Y) SpanEnd);
public sealed record ProfileStraightEdgeFilletAdmission(
    bool Disjoint, ProfileStraightEdgeFilletCorridor? Corridor, IReadOnlyList<string> Diagnostics);

public static class ProfileStraightEdgeFilletAdmissionChecker
{
    private const double Tol = 1e-8;

    public static ProfileStraightEdgeFilletAdmission Check(PrismaticSectionStackConstruction stack, ResolvedProfile2D profile, ProfileBoundaryChamferTarget target, double radius, double clearance)
    {
        var loop = profile.Loops.SingleOrDefault(x => x.Name == target.LoopId);
        if (loop is null || target.SegmentIds.Count != 1 || loop.Segments.SingleOrDefault(x => x.Name == target.SegmentIds[0])?.Geometry is not LineArcLineSegment2D line)
            return new(false, null, ["ProfileBoundaryFilletEndpointTerminationUnsupported"]);
        var length = Math.Sqrt(Math.Pow(line.End.X - line.Start.X, 2d) + Math.Pow(line.End.Y - line.Start.Y, 2d));
        if (length <= 2d * clearance + Tol) return new(false, null, ["ProfileBoundaryFilletSegmentTooShort"]);
        var dx = (line.End.X - line.Start.X) / length; var dy = (line.End.Y - line.Start.Y) / length;
        var station = target.Side == ProfileBoundaryChamferSide.Top ? stack.Feature.CriticalLevels.Max() : stack.Feature.CriticalLevels.Min();
        var from = target.Side == ProfileBoundaryChamferSide.Top ? station - radius : station;
        var to = target.Side == ProfileBoundaryChamferSide.Top ? station : station + radius;
        var corridor = new ProfileStraightEdgeFilletCorridor(target.StableId, profile.Name, loop.Name, target.SegmentIds[0], radius, clearance, from, to,
            (line.Start.X + dx * clearance, line.Start.Y + dy * clearance), (line.End.X - dx * clearance, line.End.Y - dy * clearance));
        var diagnostics = new List<string>();
        foreach (var hole in stack.Feature.ShaftHoles ?? []) AddIfCollision(hole.StableId, "Shaft", hole.CenterX, hole.CenterY, hole.Diameter / 2d, hole.From, hole.To, "ProfileBoundaryFilletIntersectsShaft");
        foreach (var hole in stack.Feature.CounterboreHoles ?? [])
        {
            AddIfCollision(hole.StableId, "Counterbore", hole.CenterX, hole.CenterY, hole.CounterboreDiameter / 2d, Math.Max(hole.From, hole.To - hole.CounterboreDepth), hole.To, "ProfileBoundaryFilletIntersectsCounterbore");
            AddIfCollision(hole.StableId, "CounterboreShaft", hole.CenterX, hole.CenterY, hole.Diameter / 2d, hole.From, Math.Max(hole.From, hole.To - hole.CounterboreDepth), "ProfileBoundaryFilletIntersectsCounterbore");
        }
        return new(diagnostics.Count == 0, corridor, diagnostics.Distinct(StringComparer.Ordinal).ToArray());

        void AddIfCollision(string feature, string kind, double x, double y, double cavityRadius, double cavityFrom, double cavityTo, string code)
        {
            // Touching is rejected too. The radial test is the Minkowski sum of the
            // finite span and the radius-r fillet strip; it is conservative by design.
            var overlapsAxially = cavityFrom <= corridor.To + Tol && cavityTo >= corridor.From - Tol;
            if (!overlapsAxially || PointSegmentDistance((x, y), corridor.SpanStart, corridor.SpanEnd) > cavityRadius + radius + Tol) return;
            diagnostics.Add($"{code}:edgeFinish={corridor.EdgeFinishId}:cavity={feature}:kind={kind}:profile={corridor.ProfileId}.{corridor.LoopId}.{corridor.SegmentId}:span=({corridor.SpanStart.X:R},{corridor.SpanStart.Y:R})->({corridor.SpanEnd.X:R},{corridor.SpanEnd.Y:R}):radius={radius:R}");
        }
    }

    private static double PointSegmentDistance((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y; var lengthSquared = dx * dx + dy * dy;
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0d, 1d);
        var ox = p.X - (a.X + t * dx); var oy = p.Y - (a.Y + t * dy); return Math.Sqrt(ox * ox + oy * oy);
    }
}
