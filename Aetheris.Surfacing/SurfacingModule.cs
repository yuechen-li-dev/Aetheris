using Aetheris.Modules;

namespace Aetheris.Surfacing;

public static class SurfacingModule
{
    public static readonly AetherisModuleId Id = new("Aetheris.Surfacing");
    public static readonly ModuleVersion Version = new(0, 2, 0);
    public const string RuledSurfaceCapability = "Surfacing.RuledSurface";
    public const string RuledTransitionCapability = "Surfacing.RuledTransition";
    public const string ParametricSurfaceCapability = "Surfacing.ParametricSurface";
    public const string SectionSurfaceCapability = "Surfacing.SectionSurface";
    public const string BoundaryPatchCapability = "Surfacing.BoundaryPatch";

    public static AetherisModule Definition { get; } = new(
        Id, "Surfacing", Version,
        [new(RuledSurfaceCapability, Id, Version, "Deterministically corresponded ruled surface between line, arc, circle, or non-rational B-spline boundaries."),
         new(RuledTransitionCapability, Id, Version, "Ruled transition preserving section and boundary provenance."),
         new(ParametricSurfaceCapability,Id,Version,"Unit-aware bounded equation surface with automatic derivatives and normals."),
         new(SectionSurfaceCapability,Id,Version,"Ordered-section surface with explicit non-rational materialization evidence."),
         new(BoundaryPatchCapability,Id,Version,"Four-boundary G0 patch preserving boundary intent.")],
        ["Surfacing.RuledPanelConcept", "Surfacing.SurfacePatchConcept"], ["Surfacing.RuledCanopy","Surfacing.HyperbolicParaboloid","Surfacing.ParabolicCylinder","Surfacing.EllipticParaboloid","Surfacing.Helicoid"],
        ["RuledSurfaceIR -> exact analytic or certified non-rational support/BRep", "ParametricSurfaceIR -> evaluator -> certified non-rational B-spline/BRep","SectionSurfaceIR/BoundaryPatchIR -> certified non-rational B-spline/BRep"],
        ["surfacing-boundary-incompatible", "surfacing-boundary-corners-inconsistent","surfacing-tangent-constraint-unsupported","surfacing-panel-thickness-invalid"],
        [new(CoreModule.Id, new(1, 0, 0))], new("docs/modules/surfacing.md"));
}
