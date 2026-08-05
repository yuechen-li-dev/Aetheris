using Aetheris.Kernel.Core.Air;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Authoritative semantic wrapper for the local-frame hole topology.  The shared
/// ProfileExtrusionBRepPlan remains the owner of vertices, curve supports,
/// DirectedEdgeUse loops, surfaces, faces, shell, and materialization identity.
/// This wrapper owns the Hole-specific frame, host interval, and descendant roles.
/// </summary>
internal sealed record LocalFrameHoleBRepPlan(
    string StableId,
    string FeatureId,
    AirConstructionPlaneHolePlacement Placement,
    (double Start, double End) HostMaterialInterval,
    ProfileExtrusionBRepPlan Topology,
    SemanticTopologyCorrespondence Correspondence,
    IReadOnlyList<string> Provenance)
{
    public static LocalFrameHoleBRepPlan FromProfilePlan(AirHoleFeature feature, AirConstructionPlaneHolePlacement placement,
        (double Start, double End) interval, ProfileExtrusionBRepPlan topology)
    {
        var innerStart = topology.Loops.Single(x => x.Role == ProfileExtrusionPlanRole.LocalStartCapLoop && x.SourceStableId.EndsWith("Loop1", StringComparison.Ordinal));
        var innerEnd = topology.Loops.Single(x => x.Role == ProfileExtrusionPlanRole.LocalEndCapLoop && x.SourceStableId.EndsWith("Loop1", StringComparison.Ordinal));
        var walls = topology.Faces.Where(x => x.Role == ProfileExtrusionPlanRole.SideFace &&
            topology.Surfaces.Single(s => s.Id == x.SurfaceId).Geometry.Kind == Aetheris.Kernel.Core.Geometry.SurfaceGeometryKind.Cylinder).ToArray();
        var source = feature.FeatureId;
        var descendants = new List<SemanticTopologyDescendant>
        {
            new($"plan:{source}:mouth-loop", "Loop", SemanticTopologyRole.HoleEntryLoop, source, Loop: innerStart.Id, ParentStableId: source),
            new($"plan:{source}:exit-loop", "Loop", SemanticTopologyRole.HoleExitLoop, source, Loop: innerEnd.Id, ParentStableId: source),
            
        };
        descendants.AddRange(walls.Select((wall, index) => new SemanticTopologyDescendant($"plan:{source}:shaft-wall:{index}", "Face", SemanticTopologyRole.HoleWallFace, source, Face: wall.Id, ParentStableId: source,
            GeometryPreview: $"radius={feature.Shaft.Radius:R};localZ=[{interval.Start:R},{interval.End:R}]")));
        descendants.AddRange(innerStart.Uses.Select(use => new SemanticTopologyDescendant($"plan:{source}:mouth-edge:{use.EdgeId.Value}", "Edge", SemanticTopologyRole.TopBoundary, source, Edge: use.EdgeId, ParentStableId: source)));
        descendants.AddRange(innerEnd.Uses.Select(use => new SemanticTopologyDescendant($"plan:{source}:exit-edge:{use.EdgeId.Value}", "Edge", SemanticTopologyRole.BottomBoundary, source, Edge: use.EdgeId, ParentStableId: source)));
        var provenance = new[] { "HoleBRepPlan", "ConstructionPlanePlacement", "HostMaterialIntervalQuery", "ProfileExtrusionBRepPlan", "AuthoritativeBRepPlan",
            "ConstructionPlane:" + placement.ConstructionPlaneId, "ConceptPlane:" + placement.SourceConceptPlaneId };
        return new($"brep-plan:hole:{source}:{placement.ConstructionPlaneId}", source, placement, interval, topology,
            new SemanticTopologyCorrespondence(feature.TargetBodyId ?? "semantic-hole-host", descendants, provenance), provenance);
    }
}
