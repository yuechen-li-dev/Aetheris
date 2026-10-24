using Aetheris.FEA.Firmament;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.Drawing;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament.Piping;
using Aetheris.Kernel.Firmament.Structural;
using Aetheris.PlasticShell;
using Aetheris.SheetMetal;
using Aetheris.Surfacing;

namespace Aetheris.CLI;

internal enum FirmamentCommandDomain
{
    Mechanical, Drawing, Analysis, Garment, Assembly, Scene, PlasticShell,
    Sculpting, Plateau, Piping, Structural, SectionChain, WireForm, Sweep, SheetMetal
}

/// <summary>One command-level selection order. Parsing and capability admission
/// remain with the existing compiler/domain owners.</summary>
internal static class FirmamentCommandRouting
{
    public static FirmamentCommandDomain Select(string source, string path, FirmamentFrontendSchema? schema)
    {
        if (FirmamentDrawingCompiler.IsDrawingSource(source)) return FirmamentCommandDomain.Drawing;
        if (FirmamentAnalysisCompiler.IsAnalysisSource(source)) return FirmamentCommandDomain.Analysis;
        if (Aetheris.Kernel.Firmament.Garment.GarmentAuthoring.HasRoot(source)) return FirmamentCommandDomain.Garment;
        if (Path.GetExtension(path).Equals(".firmasm", StringComparison.OrdinalIgnoreCase) || FirmamentLanguageAnalysisService.HasAssemblyRoot(source, path)) return FirmamentCommandDomain.Assembly;
        if (Aetheris.Kernel.Firmament.Scene.SceneAuthoring.HasRoot(source)) return FirmamentCommandDomain.Scene;
        if (PlasticShellFirmament.LooksLikePlasticShell(source)) return FirmamentCommandDomain.PlasticShell;
        if (SculptingAuthoring.IsSculptingSource(source)) return FirmamentCommandDomain.Sculpting;
        if (PlateauAuthoring.IsSource(source)) return FirmamentCommandDomain.Plateau;
        if (PipingAuthoring.IsPipingSource(source)) return FirmamentCommandDomain.Piping;
        if (StructuralAuthoring.IsStructuralSource(source)) return FirmamentCommandDomain.Structural;
        if (schema == FirmamentFrontendSchema.SectionChain || SectionChainAuthoringParser.IsSectionChainSource(source)) return FirmamentCommandDomain.SectionChain;
        if (WireFormAuthoring.IsWireFormSource(source)) return FirmamentCommandDomain.WireForm;
        if (CircularSweepAuthoring.IsSweepSource(source)) return FirmamentCommandDomain.Sweep;
        if (SheetMetalFirmament.LooksLikeSheetMetal(source)) return FirmamentCommandDomain.SheetMetal;
        return FirmamentCommandDomain.Mechanical;
    }
}
