using Aetheris.Modules;

namespace Aetheris.Surfacing;

public static class SurfacingModule
{
    public static readonly AetherisModuleId Id = new("Aetheris.Surfacing");
    public static readonly ModuleVersion Version = new(0, 3, 0);
    public const string RuledSurfaceCapability = "Surfacing.RuledSurface";
    public const string RuledTransitionCapability = "Surfacing.RuledTransition";
    public const string ParametricSurfaceCapability = "Surfacing.ParametricSurface";
    public const string SectionSurfaceCapability = "Surfacing.SectionSurface";
    public const string BoundaryPatchCapability = "Surfacing.BoundaryPatch";
    public const string PanelCapability = "Surfacing.Panel";
    public const string BoundedSculptingCapability = "Surfacing.BoundedSculpting";

    public static AetherisModule Definition { get; } = new(
        Id, "Surfacing", Version,
        [new(RuledSurfaceCapability, Id, Version, "Deterministically corresponded ruled surface between line, arc, circle, or non-rational B-spline boundaries."),
         new(RuledTransitionCapability, Id, Version, "Ruled transition preserving section and boundary provenance."),
         new(ParametricSurfaceCapability,Id,Version,"Unit-aware bounded equation surface with automatic derivatives and normals."),
         new(SectionSurfaceCapability,Id,Version,"Ordered-section surface with explicit non-rational materialization evidence."),
         new(BoundaryPatchCapability,Id,Version,"Four-boundary G0 patch preserving boundary intent."),
         new(PanelCapability,Id,Version,"Bounded oriented engineering surface with ordered semantic boundary edges and developability evidence."),
         new(BoundedSculptingCapability,Id,Version,"Immutable single-predecessor BodyState derivation with bounded OffsetRegion and rectangular ReplaceRegion authority, mixed G0/G1 patch contracts, preservation, and inspectable GeometricDelta evidence.")],
        ["Surfacing.PanelConcept", "Surfacing.SurfacePatchConcept"], ["Surfacing.RuledCanopyPanel","Surfacing.HyperbolicParaboloid","Surfacing.ParabolicCylinder","Surfacing.EllipticParaboloid","Surfacing.Helicoid"],
        ["Surface construction -> PanelIR -> semantic edges", "Panel edge Interface/Mate -> deterministic G0 evidence", "PanelIR -> bounded BRep-backed STEP export envelope"],
        ["panel-incomplete-boundary", "panel-boundary-orientation-inconsistent","panel-mate-endpoint-mismatch","panel-mate-g0-failure","panel-mate-g1-failure","panel-mate-g1-unknown","panel-mate-g2-failure","panel-mate-g2-unknown","surfacing-panel-thickness-invalid","sculpt-outside-authorized-region","sculpt-preservation-failed","sculpt-self-intersection","surf-boundary-g0-violation","surf-boundary-g1-violation","surf-selector-target-replaced","surf-surface-export-normalization-failed"],
        [new(CoreModule.Id, new(1, 0, 0))], new("docs/development/milestones/modules/surfacing.md"));
}
