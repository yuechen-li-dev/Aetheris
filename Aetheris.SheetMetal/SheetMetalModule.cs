using Aetheris.Modules;
using Aetheris.Surfacing;

namespace Aetheris.SheetMetal;

public static class SheetMetalModule
{
    public static readonly AetherisModuleId Id = new("Aetheris.SheetMetal");
    public static readonly ModuleVersion Version = new(0, 2, 0);
    public static AetherisModule Definition { get; } = new(Id,"Sheet Metal",Version,
        [
            Capability("SheetMetal.ConstantThickness", "Tolerance-bounded planar and coaxial cylindrical thickness recognition."),
            Capability("SheetMetal.BendRecovery", "Bounded planar-cylinder-planar bend recovery with source-face evidence."),
            Capability("SheetMetal.FlatPattern", "Deterministic K-factor lowering to flat IR, AP242 solid, recovered Firmament intent, bend lines, and mapped cuts."),
            Capability("SheetMetal.AuthoredBracket", "Firmament V2 module syntax and exact formed topology for two-flange brackets.")
        ],
        ["SheetMetal","ConstantThickness","Flattenable","HasFlatPattern","BendCapable"],
        ["TwoFlangeBracket<Spec>"],
        ["Firmament SheetMetal -> SheetMetalPartIr -> explicit formed BRep + SheetMetalFlatPatternIr","STEP BRep -> recognized SheetMetalPartIr -> bounded flat pattern -> flat AP242 + recovered Firmament + SVG"],
        SheetMetalDiagnosticCodes.All,
        [new(CoreModule.Id,new(1,0,0)),new(SurfacingModule.Id,new(0,1,0))],new("docs/modules/sheet-metal.md"));

    private static ModuleCapability Capability(string id, string description) => new(id, Id, Version, description);
}
