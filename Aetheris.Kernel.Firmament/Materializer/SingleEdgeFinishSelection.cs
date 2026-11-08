using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>A compiler-owned EdgeFinish binding for exactly one construction edge.</summary>
public sealed record SingleEdgeFinishSelection(string Body, string Face, string Target, string Label, bool FilletSupported = true)
{
    public static string? UnavailableReason(SemanticTopologyCorrespondence correspondence) => correspondence.ProvenanceChain.Contains("PlanarFaceThroughHole")
        && !correspondence.ProvenanceChain.Contains("ProfileCornerFinishHost")
        ? "Full-length corner finishes on a body with picked-face holes are not supported yet. The current geometry and source are unchanged." : null;
    public static SingleEdgeFinishSelection? Resolve(SemanticTopologyCorrespondence correspondence, EdgeId edge)
        => Index(correspondence).GetValueOrDefault(edge);

    public static IReadOnlyDictionary<EdgeId, SingleEdgeFinishSelection> Index(SemanticTopologyCorrespondence correspondence)
    {
        var output = new Dictionary<EdgeId, SingleEdgeFinishSelection>();
        var boundaries = correspondence.Descendants.Where(d => d.Edge is not null)
            .Where(d => d.Role is SemanticTopologyRole.TopBoundary or SemanticTopologyRole.LocalEndBoundary
                or SemanticTopologyRole.BottomBoundary or SemanticTopologyRole.LocalStartBoundary)
            .Select(d => (Edge: d.Edge!.Value, d.SourceStableId, Face: d.Role is SemanticTopologyRole.TopBoundary or SemanticTopologyRole.LocalEndBoundary ? "Top" : "Bottom"))
            .Distinct().ToArray();
        var counts = boundaries.GroupBy(d => (d.SourceStableId, d.Face)).ToDictionary(g => g.Key, g => g.Select(d => d.Edge).Distinct().Count());
        foreach (var group in boundaries.GroupBy(d => d.Edge))
        {
            // Cap finishes need a separate combined-history planner. Do not
            // advertise that route on the side-hole host.
            if (correspondence.ProvenanceChain.Contains("ProfileCornerFinishHost")
                && correspondence.ProvenanceChain.Contains("PlanarFaceThroughHole")) continue;
            var candidates = group.ToArray();
            if (candidates.Length != 1) continue;
            var (_, source, face) = candidates[0];
            if (!Regex.IsMatch(source, @"^profile:[A-Za-z_]\w*\.Outer\.[A-Za-z_]\w*$", RegexOptions.CultureInvariant)) continue;
            // A split descendant is only part of a source segment. Never promote it to a whole segment or chain.
            if (counts[(source, face)] != 1) continue;
            var target = source["profile:".Length..];
            output[group.Key] = new(correspondence.BodyStableId, face, target, "Extrusion end-face edge", FilletSupported: false);
        }
        foreach (var edge in correspondence.Descendants.Where(d => d.Edge is not null && d.Role is SemanticTopologyRole.ConvexLongitudinalEdge or SemanticTopologyRole.ReflexLongitudinalEdge))
            output[edge.Edge!.Value] = new(correspondence.BodyStableId, "Longitudinal", edge.SourceStableId["profile:".Length..] + ".Start",
                edge.Role == SemanticTopologyRole.ReflexLongitudinalEdge ? "Inside bend · full length" : "Outside corner · full length");
        // The existing localized Box route owns this exact face pair.
        foreach (var edge in correspondence.Descendants.Where(d => d.Edge is not null && d.Role == SemanticTopologyRole.BoxEdge
            && d.StableId == $"{correspondence.BodyStableId}.edge(TopRight)"))
            output[edge.Edge!.Value] = new(correspondence.BodyStableId, "+X", "SharedEdgePlusZ", "Shared edge · +X / +Z");
        return output;
    }
}
