using Aetheris.Modules;
using Aetheris.Surfacing;

namespace Aetheris.SheetMetal;

public static class SheetMetalModule
{
    public static readonly AetherisModuleId Id = new("Aetheris.SheetMetal");
    public static readonly ModuleVersion Version = new(0, 1, 0);
    public static AetherisModule Definition { get; } = new(Id,"Sheet Metal",Version,[],
        ["SheetMetal.NeutralSurface (reserved)","SheetMetal.FormedState (reserved)"],[],[],[],
        [new(CoreModule.Id,new(1,0,0)),new(SurfacingModule.Id,new(0,1,0))],new("docs/modules/sheet-metal.md"));
}
