using Aetheris.Modules;
using Aetheris.Surfacing;

namespace Aetheris.SheetMetal;

public static class SheetMetalModule
{
    public static readonly AetherisModuleId Id = new("Aetheris.SheetMetal");
    public static readonly ModuleVersion Version = new(0, 5, 0);
    public static AetherisModule Definition { get; } = new(Id,"Sheet Metal",Version,
        [
            Capability("SheetMetal.ConstantThickness", "Tolerance-bounded planar and coaxial cylindrical thickness recognition."),
            Capability("SheetMetal.BendRecovery", "Bounded planar-cylinder-planar bend recovery with source-face evidence."),
            Capability("SheetMetal.FlatPattern", "Deterministic K-factor lowering to flat IR, AP242 solid, recovered Firmament intent, bend lines, and mapped cuts."),
            Capability("SheetMetal.AuthoredMultiFlange", "Source-independent rectangular base/flange graph with parent-flange continuation, arbitrary 0<angle<180 degree cylindrical bends, and Up/Down direction."),
            Capability("SheetMetal.ExactFormedBody", "Explicit stitched planar/cylindrical skins, thickness walls, and region-owned circular/profile cuts; closed-manifold AP242 without generic Boolean union."),
            Capability("SheetMetal.ExactPlanarContour", "Shared native line/arc/circle outer/inner contour IR with bounded intersection, trim, split, offset, composition, and manufacturing validation."),
            Capability("SheetMetal.StitchedAuthoredFlat", "Authored graph traversal, shared bend allowance, exact profile composition when admitted, mapped analytic cuts/reliefs, physical flat AP242, and SVG."),
            Capability("SheetMetal.CornerRelief", "Bounded adjacent-flange Open, Mitered, RectangularRelief, and RoundRelief semantics with deterministic automatic dimensions and correspondence."),
            Capability("SheetMetal.BendTermination", "Stable finite-bend StartTermination/EndTermination semantics with Natural, Trimmed, Rounded, and bounded Auto treatments lowered through semantic ProfileDelta ancestry."),
            Capability("SheetMetal.ConceptPaths", "Canonical base/flange/corner/bend and formed/flat public paths with typed capabilities and CLI inspection."),
            Capability("SheetMetal.Templates", "Module-owned LBracket, UChannel, and FourWallTray engineering-parameter generators using ordinary Sheet Metal lowering and DFM."),
            Capability("SheetMetal.IntentRecovery", "Two-layer forensic recovery and explicit human/LLM reconstructed authority with bounded nominal/grouping suggestions."),
            Capability("SheetMetal.IntentComparison", "Localized formed-boundary, bend, cut, and flat-pattern residuals under an explicit reconstruction policy."),
            Capability("SheetMetal.SourceEdgeFlat", "Ordered exact source-line vertices are preserved in flat regions when the recovered loop is valid; invalid loops retain deterministic hull fallback."),
            Capability("SheetMetal.CornerReliefEvidence", "Bounded corner relationships and proximity-gated relief candidates remain classified evidence, never silent authored truth."),
            Capability("SheetMetal.DfmM4", "Parameterized radius, flange, cut, exact-blank, exact-relief, corner-resolution, and flat-overlap diagnostics with semantic subjects and bounded suggestions.")
        ],
        ["SheetMetal","ConstantThickness","Flattenable","HasFlatPattern","BendCapable"],
        ["AuthoredSheetMetal<RegionBendGraph>","RecoveredSheetMetalEvidence<Part>"],
        ["Firmament SheetMetal -> SheetMetalPartIr region/bend/corner graph -> explicit formed BRep + stitched SheetMetalFlatPatternIr","STEP BRep -> recognized SheetMetalPartIr -> bounded flat pattern -> recovered Firmament + SVG"],
        SheetMetalDiagnosticCodes.All,
        [new(CoreModule.Id,new(1,0,0)),new(SurfacingModule.Id,new(0,1,0))],new("docs/modules/sheet-metal.md"));

    private static ModuleCapability Capability(string id, string description) => new(id, Id, Version, description);
}
