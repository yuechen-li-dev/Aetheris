# CIR-BREP-X4: recognizer source-shape contract lab

## 1) Setup verification
- Verified existing X3/X3.1 artifacts exist in `Aetheris.Firmament.FrictionLab/CIRLab/`, `Aetheris.FrictionLab.Tests/CIRLab/`, and docs reports.
- Verified previous CIRLab tests pass on net10.0 baseline before X4 changes.
- Kept all experiment code within FrictionLab-only folders.

## 2) Source APIs inspected
- CIR nodes: `CirNode`, `CirSubtractNode`, `CirTransformNode`, `CirBoxNode`, `CirCylinderNode`.
- Lowering/execution: `FirmamentPrimitiveLoweringPlan`, `FirmamentPrimitiveExecutor`, `FirmamentPrimitiveExecutionResult`, `FirmamentCirLowerer`.
- Native state: `NativeGeometryState`, `NativeGeometryReplayLog`, `NativeGeometryReplayOperation`, `NativeGeometryCirMirrorState`.
- Analysis/readiness: `CirNativeAnalysisService`, `MaterializationReadinessAnalyzer`.

## 3) Experiment 1 source inventory table
Fixture: `testdata/firmament/examples/boolean_box_cylinder_hole.firmament`.

| Source | Available at recognizer call site | Geometry tree | Dimensions/transforms | Provenance ids/tool/source | Sufficient alone | Diagnostic value |
|---|---|---:|---:|---:|---:|---:|
| Raw lowered `CirNode` root | Yes (via lowering) | Yes | Yes | No | Yes (v1 geometry) | Medium |
| `FirmamentPrimitiveLoweringPlan` | Yes | Indirect (needs lower) | Yes (parametric intent) | Feature-level intent | No | Medium |
| `FirmamentPrimitiveExecutionResult` | Yes | Indirect (via state only) | Partial | Yes | No | High |
| `NativeGeometryState` | Yes | No traversable root (only root reference string) | No direct tree | Yes | No | High |
| `NativeGeometryReplayLog` | Yes | No | No direct dimensions | Yes (best provenance) | No | High |
| `CirMirror` summary | Yes | No | Bounds + approximate volume only | No | No | Low/Medium |

## 4) Experiment 2 node-only envelope findings
- `FromNode(CirNode)` successfully recognizes canonical direct and translation-wrapped forms.
- Unsupported transforms still reject via existing recognizer behavior.
- Missing provenance is explicit: no op/feature/tool diagnostics without replay.
- Conclusion: node-only is sufficient for v1 **geometry classification**, not sufficient for richer diagnostics.

## 5) Experiment 3 node+replay findings
- `FromNode(CirNode, NativeGeometryReplayLog?)` preserves recognition parity with node-only.
- When replay exists, diagnostics can include subtract op / feature / source feature / tool kind context.
- Recognition still works when replay is absent.
- Mismatch policy validated: replay contradiction is surfaced as mismatch diagnostic; it does not override node geometry.
- Conclusion: replay should be optional in v1 API and treated as provenance/diagnostic layer.

## 6) Experiment 4 NativeGeometryState findings
- `NativeGeometryState` alone cannot provide traversable CIR root; only `CirIntentRootReference` string is available.
- Therefore state-alone recognition is insufficient.
- A convenience adapter can accept `NativeGeometryState` only when caller also provides explicit `CirNode root`.

## 7) Experiment 5 lowering plan / execution result findings
- Plan/execution expose strong intent/provenance context and are stable upstream entrypoints.
- Neither is a direct substitute for geometric root inspection.
- Recommended usage: higher layer extracts/retains lowered `CirNode`, then attaches plan/result-derived diagnostics.

## 8) Experiment 6 mismatch / precedence policy
- Authoritative geometry source: lowered `CirNode`.
- Diagnostic-only sources: replay log, native state, plan, execution result.
- Precedence:
  1. Node geometry decides recognition success/failure.
  2. Replay/native/plan can enrich diagnostics and mismatch evidence.
  3. Replay contradictions must never force a geometry-positive result.

## 9) Experiment 7 recommended API shape
Lab prototype:

```csharp
public sealed record CirBoxCylinderRecognizerInput(
    CirNode Root,
    NativeGeometryReplayLog? ReplayLog = null,
    NativeGeometryState? NativeState = null,
    FirmamentPrimitiveLoweringPlan? LoweringPlan = null,
    FirmamentPrimitiveExecutionResult? ExecutionResult = null,
    string? SourceLabel = null);
```

Constructors/adapters validated:
- `FromNode(root)`
- `FromNode(root, replay)`
- `FromNativeState(state, root)` (requires explicit root)

## 10) Final v1 recognizer source contract
**Recommended v1 production contract:**
- Required: `CirNode Root`
- Optional: `NativeGeometryReplayLog? ReplayLog`

Rationale: `CirNode` is only source that consistently carries exact subtract topology + primitive dimensions + transform wrappers needed for canonical recognition. Replay is a high-value optional provenance channel.

## 11) Recommended v2 adapters
- Adapter from `FirmamentPrimitiveExecutionResult` -> `CirBoxCylinderRecognizerInput` requiring explicit root retention.
- Adapter from `(FirmamentPrimitiveLoweringPlan + lowered root + optional state)` -> input envelope.
- Optional policy flag for mismatch severity (warn vs reject for specific contradiction classes), while preserving node authority.

## 12) Production implementation guidance
- Implement recognizer API as node-first with optional replay.
- Keep geometry matching isolated from provenance matching.
- Add stable mismatch diagnostics schema (opIndex/featureId/toolKind/sourceFeatureId).
- Do not add recognizer dependency on BRep/native body objects.

## 13) Confidence ratings
- Node-only geometric sufficiency: **High**.
- Replay optionality and value: **High**.
- Native-state-alone insufficiency: **High**.
- Plan/result as primary geometry source: **High (negative)**.
- Mismatch precedence policy robustness: **Medium-High** (more exotic fixtures can further stress).
