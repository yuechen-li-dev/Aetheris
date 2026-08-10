using Aetheris.Modules;

namespace Aetheris.Piping;

public static class PipingModule
{
    public static readonly AetherisModuleId Id = new("Aetheris.Piping");
    public static readonly ModuleVersion Version = new(0, 1, 0);
    public const string PathPipeCapability = "Piping.PathPipe";
    public const string PipeRouteCapability = "Piping.PipeRoute";
    public static AetherisModule Definition { get; } = new(Id, "Piping", Version,
        [new(PathPipeCapability,Id,Version,"Circular pipe driven by an engineering centerline."),new(PipeRouteCapability,Id,Version,"Validated straight/planar-bend pipe route.")],
        ["Piping.PipeRoute","Piping.PipeSection"],["Piping.StandardPipeElbow"],
        ["PipeRouteIR -> analytic cylinders/torus -> exact BRep"],
        ["piping-route-invalid","piping-bend-radius-invalid","piping-wall-not-supported"],
        [new(CoreModule.Id,new(1,0,0))],new("docs/modules/piping.md"));
}
