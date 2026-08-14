using Aetheris.Modules;
using Aetheris.Surfacing;

namespace Aetheris.SheetMetal;

public static class SheetMetalModule
{
    public static readonly AetherisModuleId Id = new("Aetheris.SheetMetal");
    public static readonly ModuleVersion Version = new(0, 3, 0);
    public static AetherisModule Definition { get; } = new(Id,"Sheet Metal",Version,
        [
            Capability("SheetMetal.ConstantThickness", "Tolerance-bounded planar and coaxial cylindrical thickness recognition."),
            Capability("SheetMetal.BendRecovery", "Bounded planar-cylinder-planar bend recovery with source-face evidence."),
            Capability("SheetMetal.FlatPattern", "Deterministic K-factor lowering to flat IR, AP242 solid, recovered Firmament intent, bend lines, and mapped cuts."),
            Capability("SheetMetal.AuthoredBracket", "Firmament V2 module syntax and exact formed topology for two-flange brackets."),
            Capability("SheetMetal.IntentRecovery", "Two-layer forensic recovery and explicit human/LLM reconstructed authority with bounded nominal/grouping suggestions."),
            Capability("SheetMetal.IntentComparison", "Localized formed-boundary, bend, cut, and flat-pattern residuals under an explicit reconstruction policy."),
            Capability("SheetMetal.SourceEdgeFlat", "Ordered exact source-line vertices are preserved in flat regions when the recovered loop is valid; invalid loops retain deterministic hull fallback."),
            Capability("SheetMetal.CornerReliefEvidence", "Bounded corner relationships and proximity-gated relief candidates remain classified evidence, never silent authored truth."),
            Capability("SheetMetal.DfmM2", "Parameterized radius, cut-to-bend, cut-to-edge, and flat-overlap diagnostics with semantic subjects and bounded suggestions.")
        ],
        ["SheetMetal","ConstantThickness","Flattenable","HasFlatPattern","BendCapable"],
        ["TwoFlangeBracket<Spec>"],
        ["Firmament SheetMetal -> SheetMetalPartIr -> explicit formed BRep + SheetMetalFlatPatternIr","STEP BRep -> recognized SheetMetalPartIr -> bounded flat pattern -> flat AP242 + recovered Firmament + SVG"],
        SheetMetalDiagnosticCodes.All,
        [new(CoreModule.Id,new(1,0,0)),new(SurfacingModule.Id,new(0,1,0))],new("docs/modules/sheet-metal.md"));

    private static ModuleCapability Capability(string id, string description) => new(id, Id, Version, description);
}
