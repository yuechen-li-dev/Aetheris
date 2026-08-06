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

public static class ProfileBoundaryChamferSourceBinder
{
    private static readonly Regex EdgeFinishHeader = new(@"\bEdgeFinish\s+(?<name>\w+)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex Target = new(@"\bTarget\s*:\s*(?<value>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*){0,2})", RegexOptions.CultureInvariant);
    private static readonly Regex On = new(@"\bOn\s*:\s*(?<value>Top|Bottom)\b", RegexOptions.CultureInvariant);
    private static readonly Regex Kind = new(@"\bKind\s*:\s*(?<value>\w+)\b", RegexOptions.CultureInvariant);
    private static readonly Regex Distance = new(@"\bDistance\s*:\s*(?<value>[-+.\deE]+)mm\b", RegexOptions.CultureInvariant);
    private static readonly Regex Radius = new(@"\bRadius\s*:\s*(?<value>[-+.\deE]+)mm\b", RegexOptions.CultureInvariant);
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
