using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Cir;

namespace Aetheris.Kernel.Firmament.Execution;

internal sealed record SurfaceFamilyShellAssemblyResult(
    bool Success,
    BrepBody? Body,
    string Pattern,
    ShellClosureReadiness Readiness,
    bool FullShellAssembled,
    int PlanarPatchCount,
    bool CylindricalPatchConsumed,
    IReadOnlyList<string> Diagnostics);

internal static class SurfaceFamilyShellAssembler
{
    internal static SurfaceFamilyShellAssemblyResult TryAssembleBoxMinusCylinder(
        CirNode root,
        NativeGeometryReplayLog? replayLog = null)
    {
        _ = replayLog;
        var dryRun = ShellStitchingDryRunPlanner.Generate(root);
        var diagnostics = new List<string>
        {
            "pattern: subtract(box,cylinder)",
            $"readiness-gate-state: {dryRun.Readiness}"
        };

        var planarPatchCount = dryRun.PlannedPatches.Count(p => p.SurfaceFamily == SurfacePatchFamily.Planar);
        var hasCylinderPatch = dryRun.PlannedPatches.Any(p => p.SurfaceFamily == SurfacePatchFamily.Cylindrical);
        diagnostics.Add($"planar-patches-consumed: {planarPatchCount}");
        diagnostics.Add($"cylindrical-patch-consumed: {hasCylinderPatch}");

        if (dryRun.Readiness != ShellClosureReadiness.ReadyForAssemblyEvidence)
        {
            diagnostics.Add("readiness-gate-rejected: no shell-readiness, no assembly.");
            diagnostics.AddRange(dryRun.Diagnostics);
            return new(false, null, CirBrepMaterializer.BoxMinusCylinderPattern, dryRun.Readiness, false, planarPatchCount, hasCylinderPatch, diagnostics);
        }

        diagnostics.Add("readiness-gate-accepted: shell evidence is ready.");
        diagnostics.Add("shell-assembly-blocked: emitted patch bodies do not currently provide stable cross-patch topology identity remap metadata required for deterministic merge-by-evidence.");
        diagnostics.Add("next-blocker: add explicit emitted-edge identity mapping from InternalTrimIdentityToken -> patch-local edge/coedge ids.");
        diagnostics.AddRange(dryRun.Diagnostics);

        return new(false, null, CirBrepMaterializer.BoxMinusCylinderPattern, dryRun.Readiness, false, planarPatchCount, hasCylinderPatch, diagnostics);
    }
}
