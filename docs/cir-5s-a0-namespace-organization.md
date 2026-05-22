# CIR-5S-A0 Namespace Organization (CirCore / Materializer / Diagnostics)

## Purpose
This A0 milestone performs **mechanical sorting only** for CIR/FRep-adjacent code to reduce namespace confusion before production FRep→BRep recovery planning/materialization work.

No algorithms were rewritten and no behavior changes were intended.

## Target split
- **CirCore**: CIR/FRep representation + evaluation/analysis primitives.
- **Materializer**: FRep/CIR → BRep recovery/materialization policy and emit path.
- **Diagnostics**: readiness, dry-run, pairing/stitching evidence, and pressure-test scaffolding.

## Inventory (Phase 1)

| Current file | Current namespace | Proposed area | Proposed namespace | Reason | Risk | Notes |
|---|---|---|---|---|---|---|
| `Aetheris.Kernel.Core/Cir/CirNode.cs` | `Aetheris.Kernel.Core.Cir` | LeaveInPlace (CirCore already) | `Aetheris.Kernel.Core.Cir` | Core CIR model. | Low | CirCore concept already represented by `Core/Cir` folder. |
| `Aetheris.Kernel.Core/Cir/CirNodes.cs` | `Aetheris.Kernel.Core.Cir` | LeaveInPlace (CirCore already) | `Aetheris.Kernel.Core.Cir` | Core CIR operations. | Low | No move needed in A0. |
| `Aetheris.Kernel.Core/Cir/CirTape.cs` | `Aetheris.Kernel.Core.Cir` | LeaveInPlace (CirCore already) | `Aetheris.Kernel.Core.Cir` | Tape representation/eval. | Low | No move needed in A0. |
| `Aetheris.Kernel.Core/Cir/CirAnalysis.cs` | `Aetheris.Kernel.Core.Cir` | LeaveInPlace (CirCore already) | `Aetheris.Kernel.Core.Cir` | CIR evaluation/interval analysis. | Low | No move needed in A0. |
| `Aetheris.Kernel.Core/Cir/CirRegionPlanner.cs` | `Aetheris.Kernel.Core.Cir` | LeaveInPlace (CirCore already) | `Aetheris.Kernel.Core.Cir` | Region planning core. | Low | No move needed in A0. |
| `Aetheris.Kernel.Core/Cir/CirAdaptiveVolumeEstimator.cs` | `Aetheris.Kernel.Core.Cir` | LeaveInPlace (CirCore already) | `Aetheris.Kernel.Core.Cir` | Core adaptive evaluation. | Low | No move needed in A0. |
| `Aetheris.Kernel.Firmament/Execution/CirBrepMaterializer.cs` | `Aetheris.Kernel.Firmament.Execution` | Materializer | `Aetheris.Kernel.Firmament.Materializer` | Primary CIR→BRep recovery/materialization path. | Low | Moved in A0. |
| `Aetheris.Kernel.Firmament/Execution/CirBoxCylinderRecognizer.cs` | `Aetheris.Kernel.Firmament.Execution` | Materializer | `Aetheris.Kernel.Firmament.Materializer` | Recovery recognizer/policy signal. | Low | Moved in A0. |
| `Aetheris.Kernel.Firmament/Execution/NativeGeometryRematerializer.cs` | `Aetheris.Kernel.Firmament.Execution` | Materializer | `Aetheris.Kernel.Firmament.Materializer` | Rematerialization entrypoint. | Low | Moved in A0. |
| `Aetheris.Kernel.Firmament/Execution/MaterializationReadinessAnalyzer.cs` | `Aetheris.Kernel.Firmament.Execution` | Diagnostics | `Aetheris.Kernel.Firmament.Diagnostics` | Readiness diagnostics/reporting. | Low | Moved in A0. |
| `Aetheris.Kernel.Firmament/Execution/FacePatchCandidateDryRun.cs` | `Aetheris.Kernel.Firmament.Execution` | Diagnostics | `Aetheris.Kernel.Firmament.Diagnostics` | Dry-run patch descriptors. | Low | Moved in A0. |
| `Aetheris.Kernel.Firmament/Execution/TopologyPairingEvidenceDryRun.cs` | `Aetheris.Kernel.Firmament.Execution` | Diagnostics | `Aetheris.Kernel.Firmament.Diagnostics` | Pairing evidence diagnostics. | Low | Moved in A0. |
| `Aetheris.Kernel.Firmament/Execution/TopologyAssemblyDryRun.cs` | `Aetheris.Kernel.Firmament.Execution` | Diagnostics | `Aetheris.Kernel.Firmament.Diagnostics` | Topology assembly dry-run. | Low | Moved in A0. |
| `Aetheris.Kernel.Firmament/Execution/ShellStitchingDryRun.cs` | `Aetheris.Kernel.Firmament.Execution` | Diagnostics | `Aetheris.Kernel.Firmament.Diagnostics` | Shell stitching dry-run diagnostics. | Low | Moved in A0. |
| `Aetheris.Kernel.Firmament/Execution/EmittedTokenPairingAnalyzer.cs` | `Aetheris.Kernel.Firmament.Execution` | Diagnostics | `Aetheris.Kernel.Firmament.Diagnostics` | Token pairing diagnostics. | Low | Moved in A0. |
| `Aetheris.Kernel.Firmament/Execution/SurfacePatchDescriptorScaffold.cs` | `Aetheris.Kernel.Firmament.Execution` | Diagnostics | `Aetheris.Kernel.Firmament.Diagnostics` | Diagnostic scaffolding. | Medium | Referenced by multiple tests; moved with namespace updates. |
| `Aetheris.Kernel.Firmament/Execution/SurfaceFeatureDryRunGenerator.cs` | `Aetheris.Kernel.Firmament.Execution` | Diagnostics | `Aetheris.Kernel.Firmament.Diagnostics` | Dry-run planning output. | Low | Moved in A0. |
| `Aetheris.Kernel.Firmament/Execution/SurfaceFamilyBoxCylinderPressureTest.cs` | `Aetheris.Kernel.Firmament.Execution` | Diagnostics | `Aetheris.Kernel.Firmament.Diagnostics` | Pressure-test harness/reporting. | Low | Moved in A0. |
| `Aetheris.Kernel.Firmament/Execution/SurfaceRestrictedField.cs` | `Aetheris.Kernel.Firmament.Execution` | LeaveInPlace (Ambiguous) | Unchanged in A0 | Could be CirCore-style field primitive but currently co-located with Firmament execution and stitch flow. | High | Defer to A1 to avoid dependency churn. |

## A0 result

### New namespace/folder boundaries
- `Aetheris.Kernel.Firmament.Materializer`
- `Aetheris.Kernel.Firmament.Diagnostics`

### Files moved in A0
- Materializer: `CirBrepMaterializer`, `CirBoxCylinderRecognizer`, `NativeGeometryRematerializer`.
- Diagnostics: readiness/dry-run/pairing/stitch/pressure/scaffold files listed above.

### Intentionally left in place
- `Aetheris.Kernel.Core/Cir/*` is already the practical **CirCore** boundary in this repo.
- Most `Aetheris.Kernel.Firmament/Execution/*` remain in place for A0 to avoid broad namespace blast radius.
- `SurfaceRestrictedField` and related restricted-field helpers are deferred as ambiguous/high-risk ownership candidates.

## Dependency guidance
- **CirCore** (`Aetheris.Kernel.Core.Cir`) should stay free of Firmament materialization policy and test-diagnostic pressure paths.
- **Materializer** may depend on CirCore + BRep + Firmament replay/materialization state.
- **Diagnostics** may depend on CirCore, Materializer, and Firmament execution evidence; production paths should not take required dependencies on diagnostics unless documented.

## Follow-up for CIR-5S-A1
- Re-evaluate remaining `Execution/*` files for further partitioning (especially `SurfaceRestrictedField`, stitch executors/planners, and mixed execution-policy files).
- Consider introducing a dedicated `Aetheris.Kernel.Core/CirCore` folder alias only if team wants explicit folder branding; functionally the existing `Core/Cir` boundary already serves CirCore.
