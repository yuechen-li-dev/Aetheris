using Aetheris.Modules;

namespace Aetheris.Surfacing;

public static class SurfacingModule
{
    public static readonly AetherisModuleId Id = new("Aetheris.Surfacing");
    public static readonly ModuleVersion Version = new(0, 1, 0);
    public const string RuledSurfaceCapability = "Surfacing.RuledSurface";
    public const string RuledTransitionCapability = "Surfacing.RuledTransition";

    public static AetherisModule Definition { get; } = new(
        Id, "Surfacing", Version,
        [new(RuledSurfaceCapability, Id, Version, "Exact ruled surface between compatible boundaries."),
         new(RuledTransitionCapability, Id, Version, "Ruled transition preserving section and boundary provenance.")],
        ["Surfacing.RuledSurface", "Surfacing.RuledTransition"], ["Surfacing.RuledCanopy"],
        ["RuledSurfaceIR -> exact analytic surface/BRep", "RuledTransitionIR -> exact ruled surface/BRep"],
        ["surfacing-boundary-incompatible", "surfacing-panel-thickness-invalid"],
        [new(CoreModule.Id, new(1, 0, 0))], new("docs/modules/surfacing.md"));
}
