# STEP NURBS recovery X0

**X0 verdict: Meaningful progression.** Aetheris now imports the supplied McMaster threaded bolt through its exact BRep path, including weighted curve `#2175`, and preserves its source topology. Generic weighted curves that do not recognize as circles are adaptively approximated as polynomial cubic B-splines. The supplied body tessellates and exports/reimports through STEP. At the X0 checkpoint, full pcurve qualification remained open; see `STEP-RECOVERED-PCURVE-X1.md` for the subsequent trim-binding result.

## Policy and units

The importer tries analytic recognition first, preserves directly supported non-rational splines exactly, then attempts bounded generic recovery. `ImportPolicy.RecoveryToleranceMillimetres` defaults to **0.1 mm**. STEP source lengths are normalized to millimetres before recovery. This engineering budget is distinct from kernel topology tolerances and the much stricter analytic recognition gates. Imported generic curves and surfaces carry `SplineRecoveryProvenance` with source kind, method, requested budget, maximum observed deviation, and analytic recognition result. The reported deviation is a sampled measurement, not a certified supremum.

For rational 3D curves, the shared `BSpline3Curve.EvaluateRational` evaluates the source with its original control points, knots, and weights. `BSplineCurveRationalReduction` refines individual nonzero knot spans and stops once a seven-point interior check per candidate span meets the requested budget, with explicit depth and segment caps. Uniform weights cancel and preserve the polynomial spline exactly. A failed bounded fit produces a diagnostic instead of discarding weights. Rational surfaces use the existing `BSplineSurfaceRationalReduction` at the engineering budget; source rational surfaces remain the fallback when reduction fails, as supported by the legacy interchange route.

## Mandatory local witness

The supplied `91280A546_Medium-Strength Class 8.8 Steel Hex Head Screw.STEP` is kept outside the repository. With the new importer, `aetheris analyze <file> --json` reports one body, one shell, **147 faces, 405 edges, 268 vertices**, an enclosed-manifold structural assessment, **160 recovered curves**, **3 recovered surfaces**, and **0 unsupported entities**. The worst sampled recovery deviation is **0.023580323 mm**, below the 0.1 mm default. The normalized bounding box is approximately `13.0 × 15.011107 × 56.432813 mm`. These counts describe imported topology, not feature history or semantic thread recognition.

The local opt-in test uses `AETHERIS_STEP_NURBS_WITNESS=<path>` and asserts import, topology, tessellation, STEP export/reimport, and less than 0.01 mm sampled corresponding-spline-edge drift after roundtrip. Missing local witness data skips the qualification work. A deterministic diagnostic [isometric wireframe](../../artifacts/local/step-nurbs-recovery-x0/mcmaster-bolt.svg) was generated from the imported body; it covers all 405 boundary edges and 131 of 147 faces with trimmed isolines. No SolidWorks/Fusion reference image was supplied in this task, so a visual match is not claimed.

The first attempted witness import, still using the older source-accuracy surface reduction budget, ran more than two minutes and was stopped. With the explicit 0.1 mm surface budget, the CLI analysis completed in about two seconds on this machine. This is a measured before/after bound, not a controlled benchmark. The existing 3DM recovery inspection contributes the same-net candidate idea, while the rational evaluator now lives on the shared kernel spline type; a full 3DM BRep importer is outside this milestone.

## Remaining boundary

The supplied source has no imported pcurve bindings. The diagnostic `wireframe` path attempted pcurve recovery and produced **579** bindings with a small residual for those recovered, but its result is not fully qualified. Its diagnostics include **54** cone-family exclusions and **177** non-rational B-spline inverse failures. Some inverse residuals are several millimetres, well outside the engineering budget. Accordingly, `pcurve/3D` agreement for every recovered trim is not yet established, and this milestone is not accepted as complete. The BRep tessellation and STEP roundtrip checks establish downstream usability of this witness; they do not substitute for all-trim pcurve qualification.

## Validation

The full solution builds with zero errors and existing Web.Runtime WASM warnings. The kernel fast lane passes **1,002/1,002** tests; the full kernel lane, including the NIST corpus, passes **1,135/1,135**. The serial solution test gate has one failure in an unchanged OBJ hex-bolt assertion (`MeshObj_HexBolt_ExportsStructuredPolygonsDirectlyFromSurfaceMeshIr`, expected 905 polygons, actual 908). The local McMaster witness test and the corrected CLI analyze baseline test pass. `git diff --check` passes.

## Reproduction

```powershell
$env:AETHERIS_STEP_NURBS_WITNESS = '<local McMaster STEP path>'
dotnet test Aetheris.Kernel.Core.Tests -c Release --filter FullyQualifiedName~Step242LocalRationalWitnessTests
dotnet run --project Aetheris.CLI -c Release -- analyze $env:AETHERIS_STEP_NURBS_WITNESS --json
dotnet run --project Aetheris.CLI -c Release -- wireframe $env:AETHERIS_STEP_NURBS_WITNESS --out artifacts/local/step-nurbs-recovery-x0/mcmaster-bolt.svg --view iso --density 8 --json
```
