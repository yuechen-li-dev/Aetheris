using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Maps the IDs captured by the Box extrusion itself to existing axis selectors.</summary>
public static class BoxConstructionCorrespondence
{
    public static SemanticTopologyCorrespondence Create(string bodyStableId, BrepExtrudeConstructionTopology topology, FirmamentV2SourceSpan? sourceSpan = null)
    {
        ArgumentNullException.ThrowIfNull(topology);
        if (topology.SideFaces.Count != 4 || topology.TopEdges.Count != 4)
            throw new ArgumentException("A Box extrusion must have exactly four ordered sides and top edges.", nameof(topology));

        // BrepPrimitives.CreateBox authors its rectangle counterclockwise from
        // (-X,-Y), so extrusion side indices have these fixed semantic roles.
        var faces = new[]
        {
            (topology.BottomFace, "-Z"), (topology.TopFace, "+Z"),
            (topology.SideFaces[0], "-Y"), (topology.SideFaces[1], "+X"),
            (topology.SideFaces[2], "+Y"), (topology.SideFaces[3], "-X")
        };
        var descendants = faces.Select(pair => new SemanticTopologyDescendant(
            $"{bodyStableId}.face({pair.Item2})", "Face", SemanticTopologyRole.BoxFace,
            bodyStableId, Face: pair.Item1, ParentStableId: bodyStableId,
            FirmamentSelector: $"face({pair.Item2})",
            Addressability: SemanticTopologyAddressability.DerivedStable)).ToList();
        descendants.Add(new SemanticTopologyDescendant(
            $"{bodyStableId}.edge(TopFront)", "Edge", SemanticTopologyRole.BoxEdge,
            bodyStableId, Edge: topology.TopEdges[0], ParentStableId: bodyStableId,
            Addressability: SemanticTopologyAddressability.DerivedStable));
        return new(bodyStableId, descendants, ["FirmamentBox", "BrepPrimitives.CreateBox", "BrepExtrudeConstructionTopology"],
            sourceSpan is null ? null : new Dictionary<string, FirmamentV2SourceSpan> { [bodyStableId] = sourceSpan });
    }
}
