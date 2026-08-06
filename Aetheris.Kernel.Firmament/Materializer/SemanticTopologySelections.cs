using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Topology;
using System.Text.RegularExpressions;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Stable roles assigned while an authoritative plan is materialized.  These are not geometric queries.</summary>
public enum SemanticTopologyRole { Unknown, ProfileVertex, VerticalExtrusionEdge, LocalStartBoundary, LocalEndBoundary, LocalStartCapLoop, LocalEndCapLoop, TopBoundary, BottomBoundary, ExtrusionSideFace, TopFaceBoundaryLoop, BottomFaceBoundaryLoop, HoleEntryLoop, HoleExitLoop, HoleWallFace, HoleShaftToDrillPointLoop, HoleShaftToDrillPointEdge, HoleDrillPointFace, HoleTipVertex, HoleCounterboreMouthLoop, HoleCounterboreWallFace, HoleCounterboreShoulderLoop, HoleCounterboreShaftWallFace, SlotEntryLoop, SlotExitLoop, SlotWallFace, SlotStraightWallFace, SlotEndWallFace, ComposeTransition, EdgeFinishReplacementFace }
public enum SemanticSelectionRequirement { ExactlyOne, OneOrMore, ConnectedChain, ClosedLoop, NonEmptyFaceSet }
public enum SemanticSelectionFailure { None, SemanticSourceNotFound, NoMaterializedDescendants, AmbiguousBodyContext, SelectionCardinalityMismatch, DescendantsNotConnected, DescendantsBranch, DescendantsDoNotClose, MixedBoundaryRoles, UnsupportedTopologyChange, SelectionConsumerMismatch }

/// <summary>Immutable public materialization evidence.  The opaque materialized id is diagnostic data, never authoring syntax.</summary>
public sealed record SemanticTopologyDescendant(
    string StableId, string Kind, SemanticTopologyRole Role, string SourceStableId,
    EdgeId? Edge = null, FaceId? Face = null, LoopId? Loop = null, VertexId? Vertex = null,
    string? ParentStableId = null, string? GeometryPreview = null);

public sealed record SemanticTopologyCorrespondence(
    string BodyStableId,
    IReadOnlyList<SemanticTopologyDescendant> Descendants,
    IReadOnlyList<string> ProvenanceChain)
{
    public IReadOnlyList<SemanticTopologyDescendant> FromSources(IEnumerable<string> sources, SemanticTopologyRole? role) =>
        Descendants.Where(x => sources.Contains(x.SourceStableId, StringComparer.Ordinal) && (role is null || x.Role == role)).ToArray();
}

public sealed record SemanticSelectionRequest(
    string StableId, string Label, string BodyStableId, IReadOnlyList<string> SourceStableIds,
    SemanticTopologyRole? Role, SemanticSelectionRequirement Require, string SourceSpan, string Consumer = "inspection");
public sealed record SemanticSelectionDiagnostic(string Code, string Message, string SourceSpan, IReadOnlyList<string> ProvenanceChain);
public sealed record SemanticSelectionResolution(
    bool Succeeded, SemanticSelectionFailure Failure, SemanticSelectionRequest Request,
    IReadOnlyList<SemanticTopologyDescendant> Descendants, IReadOnlyList<SemanticTopologyDescendant> OrderedChain,
    bool IsConnected, bool IsClosed, IReadOnlyList<SemanticSelectionDiagnostic> Diagnostics);

/// <summary>Source-grounded resolver.  It only consumes plan/materialization correspondence emitted by the producer.</summary>
public static class SemanticTopologySelectionResolver
{
    public static SemanticSelectionResolution Resolve(BrepBody body, SemanticTopologyCorrespondence correspondence, SemanticSelectionRequest request)
    {
        SemanticSelectionResolution Fail(SemanticSelectionFailure failure, string code, string message, IReadOnlyList<SemanticTopologyDescendant>? descendants = null) =>
            new(false, failure, request, descendants ?? [], [], false, false,
                [new(code, message, request.SourceSpan, correspondence.ProvenanceChain)]);
        if (!string.Equals(request.BodyStableId, correspondence.BodyStableId, StringComparison.Ordinal))
            return Fail(SemanticSelectionFailure.AmbiguousBodyContext, "AmbiguousBodyContext", $"Requested body '{request.BodyStableId}' does not own this correspondence.");
        if (request.SourceStableIds.Count == 0)
            return Fail(SemanticSelectionFailure.SemanticSourceNotFound, "SemanticSourceNotFound", "Selection has no authored semantic sources.");
        var known = correspondence.Descendants.Select(x => x.SourceStableId).ToHashSet(StringComparer.Ordinal);
        var missing = request.SourceStableIds.Where(x => !known.Contains(x)).ToArray();
        if (missing.Length > 0)
            return Fail(SemanticSelectionFailure.SemanticSourceNotFound, "SemanticSourceNotFound", $"No provenance source for: {string.Join(", ", missing)}.");
        var candidates = correspondence.FromSources(request.SourceStableIds, request.Role);
        if (candidates.Count == 0)
            return Fail(SemanticSelectionFailure.NoMaterializedDescendants, "NoMaterializedDescendants", "The selected authored source has no descendants with the requested topology role.");
        if (request.Require == SemanticSelectionRequirement.ExactlyOne && candidates.Count != 1)
            return Fail(SemanticSelectionFailure.SelectionCardinalityMismatch, "SelectionCardinalityMismatch", $"Expected exactly one descendant; found {candidates.Count}.", candidates);
        if (request.Require == SemanticSelectionRequirement.NonEmptyFaceSet && !candidates.Any(x => x.Face is not null))
            return Fail(SemanticSelectionFailure.SelectionConsumerMismatch, "SelectionConsumerMismatch", "NonEmptyFaceSet requires face descendants.", candidates);
        if (request.Require is not (SemanticSelectionRequirement.ConnectedChain or SemanticSelectionRequirement.ClosedLoop))
            return new(true, SemanticSelectionFailure.None, request, candidates, [], false, false, []);
        if (request.Require == SemanticSelectionRequirement.ClosedLoop && candidates.All(x => x.Loop is not null))
        {
            foreach (var candidate in candidates)
            {
                var loop = body.Topology.Loops.Single(x => x.Id == candidate.Loop!.Value);
                // Exact circular boundaries can be represented by two analytic
                // arc uses; directed closure below remains the authoritative
                // non-degeneracy test.
                if (loop.CoedgeIds.Count < 2) return Fail(SemanticSelectionFailure.DescendantsDoNotClose, "DescendantsDoNotClose", "A loop descendant is degenerate.", candidates);
                var uses = loop.CoedgeIds.Select(id => body.Topology.Coedges.Single(x => x.Id == id)).Select(c => DirectedEdgeUse.Resolve(body.Topology.Edges.Single(e => e.Id == c.EdgeId), c)).ToArray();
                if (uses.Where((use, index) => use.EndVertexId != uses[(index + 1) % uses.Length].StartVertexId).Any())
                    return Fail(SemanticSelectionFailure.DescendantsDoNotClose, "DescendantsDoNotClose", "A loop descendant does not have directed closure.", candidates);
            }
            return new(true, SemanticSelectionFailure.None, request, candidates, candidates, true, true, []);
        }
        if (candidates.Any(x => x.Edge is null))
            return Fail(SemanticSelectionFailure.SelectionConsumerMismatch, "SelectionConsumerMismatch", "Chain and loop requirements require edge descendants.", candidates);
        var duplicate = candidates.GroupBy(x => x.Edge!.Value).FirstOrDefault(x => x.Count() > 1);
        if (duplicate is not null)
            return Fail(SemanticSelectionFailure.DescendantsBranch, "DescendantsBranch", "A descendant edge appears more than once.", candidates);
        var edges = candidates.Select(x => (Descendant: x, Edge: body.Topology.Edges.Single(e => e.Id == x.Edge!.Value))).ToArray();
        var incident = new Dictionary<VertexId, List<(SemanticTopologyDescendant Descendant, Edge Edge)>>();
        foreach (var item in edges)
        {
            // DirectedEdgeUse is the shared convention even when the source segment's orientation is not required.
            var use = DirectedEdgeUse.Resolve(item.Edge, false);
            foreach (var vertex in new[] { use.StartVertexId, use.EndVertexId })
            {
                if (!incident.TryGetValue(vertex, out var list)) incident[vertex] = list = [];
                list.Add(item);
            }
        }
        if (incident.Values.Any(x => x.Count > 2))
            return Fail(SemanticSelectionFailure.DescendantsBranch, "DescendantsBranch", "A selected descendant vertex has more than two incident selected edges.", candidates);
        var visited = new HashSet<EdgeId>(); var queue = new Queue<VertexId>(); queue.Enqueue(incident.Keys.MinBy(x => x.Value));
        while (queue.Count > 0) foreach (var item in incident[queue.Dequeue()]) if (visited.Add(item.Edge.Id))
        {
            var use = DirectedEdgeUse.Resolve(item.Edge, false); queue.Enqueue(use.StartVertexId); queue.Enqueue(use.EndVertexId);
        }
        if (visited.Count != edges.Length)
            return Fail(SemanticSelectionFailure.DescendantsNotConnected, "DescendantsNotConnected", "Selected descendants are not one connected component.", candidates);
        var closed = incident.Values.All(x => x.Count == 2);
        if (request.Require == SemanticSelectionRequirement.ClosedLoop && !closed)
            return Fail(SemanticSelectionFailure.DescendantsDoNotClose, "DescendantsDoNotClose", "Selected descendants do not return to their first vertex.", candidates);
        if (request.Require == SemanticSelectionRequirement.ConnectedChain && closed)
            return Fail(SemanticSelectionFailure.SelectionCardinalityMismatch, "SelectionCardinalityMismatch", "ConnectedChain requires an open chain; the descendants form a closed loop.", candidates);
        var start = closed ? incident.Keys.MinBy(x => x.Value) : incident.Where(x => x.Value.Count == 1).OrderBy(x => x.Key.Value).First().Key;
        var ordered = new List<SemanticTopologyDescendant>(); var previous = default(EdgeId?); var current = start;
        while (ordered.Count < edges.Length)
        {
            var next = incident[current].Where(x => previous is null || x.Edge.Id != previous.Value).OrderBy(x => x.Descendant.StableId, StringComparer.Ordinal).First();
            ordered.Add(next.Descendant); previous = next.Edge.Id;
            var use = DirectedEdgeUse.Resolve(next.Edge, false); current = use.StartVertexId == current ? use.EndVertexId : use.StartVertexId;
        }
        return new(true, SemanticSelectionFailure.None, request, candidates, ordered, true, closed, []);
    }
}

public sealed record VertexSet(SemanticSelectionResolution Resolution);
public sealed record EdgeSet(SemanticSelectionResolution Resolution);
public sealed record FaceSet(SemanticSelectionResolution Resolution);
public sealed record LoopSet(SemanticSelectionResolution Resolution) { public bool IsExplicitlyClosed => Resolution.IsClosed; }
public sealed record Chain(SemanticSelectionResolution Resolution) { public IReadOnlyList<SemanticTopologyDescendant> OrderedEdges => Resolution.OrderedChain; }

/// <summary>Deliberately bounded Firmament surface: named Profile members plus a boundary role and shape contract.</summary>
public static class SemanticSelectionSourceParser
{
    private static readonly Regex Header = new(@"\bSelection\s+(?<name>\w+)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex Source = new(@"\bSource\s*:\s*(?<profile>\w+)\.ProfileSegments\s*\(\s*\[(?<members>[\w\s,]+)\]\s*\)", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex LoopSource = new(@"\bSource\s*:\s*(?<profile>\w+)\.ProfileLoop\s*\(\s*(?<loop>\w+)\s*\)", RegexOptions.Singleline | RegexOptions.CultureInvariant);
    private static readonly Regex HoleSource = new(@"\bSource\s*:\s*Hole\s*\(\s*(?<hole>\w+)\s*\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex SlotSource = new(@"\bSource\s*:\s*Slot\s*\(\s*(?<slot>\w+)\s*\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex Target = new(@"\bTarget\s*:\s*(?<role>TopBoundary|BottomBoundary|SideBoundary|HoleEntry|HoleExit|HoleWall|ShaftToDrillPoint|DrillPoint|Tip|SlotEntry|SlotExit|SlotWall|SlotStraightWall|SlotEndWall)", RegexOptions.CultureInvariant);
    private static readonly Regex Require = new(@"\bRequire\s*:\s*(?<shape>ExactlyOne|OneOrMore|ConnectedChain|ClosedLoop|NonEmptyFaceSet)", RegexOptions.CultureInvariant);

    public static IReadOnlyList<SemanticSelectionRequest> Parse(string source, ResolvedProfile2D profile, string bodyStableId, out IReadOnlyList<string> diagnostics)
    {
        var output = new List<SemanticSelectionRequest>(); var errors = new List<string>();
        foreach (Match header in Header.Matches(source))
        {
            var body = Block(source, header.Index + header.Length - 1);
            if (body is null) { errors.Add($"selection-unclosed:{header.Groups["name"].Value}"); continue; }
            var sourceMatches = Source.Matches(body); var loopMatch = LoopSource.Match(body); var holeMatch = HoleSource.Match(body); var slotMatch = SlotSource.Match(body); var target = Target.Match(body); var require = Require.Match(body);
            if (!target.Success || !require.Success || (sourceMatches.Count == 0 && !loopMatch.Success && !holeMatch.Success && !slotMatch.Success)) { errors.Add($"selection-invalid:{header.Groups["name"].Value}"); continue; }
            var boundary = target.Groups["role"].Value;
            var role = slotMatch.Success
                ? boundary switch { "SlotEntry" => SemanticTopologyRole.SlotEntryLoop, "SlotExit" => SemanticTopologyRole.SlotExitLoop, "SlotWall" => SemanticTopologyRole.SlotWallFace, "SlotStraightWall" => SemanticTopologyRole.SlotStraightWallFace, "SlotEndWall" => SemanticTopologyRole.SlotEndWallFace, _ => SemanticTopologyRole.Unknown }
                : holeMatch.Success
                ? boundary switch { "HoleEntry" => SemanticTopologyRole.HoleEntryLoop, "HoleExit" => SemanticTopologyRole.HoleExitLoop, "HoleWall" => SemanticTopologyRole.HoleWallFace, "ShaftToDrillPoint" => SemanticTopologyRole.HoleShaftToDrillPointLoop, "DrillPoint" => SemanticTopologyRole.HoleDrillPointFace, "Tip" => SemanticTopologyRole.HoleTipVertex, _ => SemanticTopologyRole.Unknown }
                : loopMatch.Success
                ? boundary switch { "TopBoundary" => SemanticTopologyRole.TopFaceBoundaryLoop, "BottomBoundary" => SemanticTopologyRole.BottomFaceBoundaryLoop, _ => SemanticTopologyRole.ExtrusionSideFace }
                : boundary switch { "TopBoundary" => SemanticTopologyRole.TopBoundary, "BottomBoundary" => SemanticTopologyRole.BottomBoundary, _ => SemanticTopologyRole.ExtrusionSideFace };
            var sources = slotMatch.Success
                ? new[] { $"slot:{bodyStableId}.{slotMatch.Groups["slot"].Value}" }
                : holeMatch.Success
                ? new[] { $"hole:{bodyStableId}.{holeMatch.Groups["hole"].Value}" }
                : sourceMatches.Count > 0
                ? sourceMatches.SelectMany(match => match.Groups["members"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => $"profile:{match.Groups["profile"].Value}.Outer.{x}")).ToArray()
                : [$"profile:{loopMatch.Groups["profile"].Value}.{loopMatch.Groups["loop"].Value}"];
            // Compose selections may name any one of the parsed profiles; body context is checked by the resolver.
            output.Add(new($"selection:{header.Groups["name"].Value}", header.Groups["name"].Value, bodyStableId, sources, role,
                Enum.Parse<SemanticSelectionRequirement>(require.Groups["shape"].Value), $"offset:{header.Index}", slotMatch.Success ? "Slot" : holeMatch.Success ? "Hole" : "EdgeFinish"));
        }
        diagnostics = errors; return output;
    }

    private static string? Block(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++) { if (text[i] == '{') depth++; else if (text[i] == '}' && --depth == 0) return text[(open + 1)..i]; }
        return null;
    }
}
