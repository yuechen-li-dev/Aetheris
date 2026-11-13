using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Maps the IDs captured by the Box extrusion itself to existing axis selectors.</summary>
public static class BoxConstructionCorrespondence
{
    /// <summary>The retained world-Z stock caps emitted by the qualified box/hole construction.</summary>
    public static IReadOnlyList<SemanticTopologyDescendant> RetainedCaps(string bodyStableId, FaceId bottom, FaceId top, BrepBody? body = null) =>
    [
        new($"{bodyStableId}.face(-Z)", "Face", SemanticTopologyRole.BoxFace, bodyStableId, Face: bottom, ParentStableId: bodyStableId,
            FirmamentSelector: "face(-Z)", Addressability: SemanticTopologyAddressability.DerivedStable),
        new($"{bodyStableId}.face(+Z)", "Face", SemanticTopologyRole.BoxFace, bodyStableId, Face: top, ParentStableId: bodyStableId,
            FirmamentSelector: "face(+Z)", Addressability: SemanticTopologyAddressability.DerivedStable),
        .. body is null ? [] : RetainedSides(bodyStableId, body)
    ];

    /// <summary>The stock's four walls, each named by its axis face when it is the single planar face bounding the part
    /// in that direction (the meaning face(+X) has in PMI). A wall the construction split or recessed stays unnamed.</summary>
    public static IReadOnlyList<SemanticTopologyDescendant> RetainedSides(string bodyStableId, BrepBody body) =>
        new[] { ("-X", "minus_x_face"), ("+X", "plus_x_face"), ("-Y", "minus_y_face"), ("+Y", "plus_y_face") }
            .Select(side => (side.Item1, Faces: Execution.FirmamentSelectorResolver.ResolveSupportPlaneFaces(body, $"{bodyStableId}.{side.Item2}")))
            .Where(side => side.Faces.Count == 1)
            .Select(side => new SemanticTopologyDescendant($"{bodyStableId}.face({side.Item1})", "Face", SemanticTopologyRole.BoxFace, bodyStableId,
                Face: side.Faces[0], ParentStableId: bodyStableId, FirmamentSelector: $"face({side.Item1})", Addressability: SemanticTopologyAddressability.DerivedStable))
            .ToArray();
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
        descendants.Add(new SemanticTopologyDescendant(
            $"{bodyStableId}.edge(TopRight)", "Edge", SemanticTopologyRole.BoxEdge,
            bodyStableId, Edge: topology.TopEdges[1], ParentStableId: bodyStableId,
            Addressability: SemanticTopologyAddressability.DerivedStable));
        return new(bodyStableId, descendants, ["FirmamentBox", "BrepPrimitives.CreateBox", "BrepExtrudeConstructionTopology"],
            sourceSpan is null ? null : new Dictionary<string, FirmamentV2SourceSpan> { [bodyStableId] = sourceSpan });
    }
}
