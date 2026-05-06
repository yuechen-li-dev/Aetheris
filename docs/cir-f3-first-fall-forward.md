# CIR-F3: First bounded runtime fall-forward to `CirOnly`

## Scope

CIR-F3 adds one bounded runtime transition path from `BRepActive` to `CirOnly` for a proven unsupported BRep materialization case where intent is still valid and CIR lowering is supported.

Out of scope for F3:

- CIR→BRep materialization
- public CLI exposure
- broad boolean/materializer redesign
- generated topology naming

## Chosen F3 candidate

Fixture: `testdata/firmament/fixtures/m10l-unsupported-box-subtract-sphere-touching-boundary.firmament`.

Why this candidate:

- intent is valid,
- BRep path reports unsupported materialization,
- CIR lowering supports box/sphere/subtract,
- no generated topology selector dependency,
- CIR mirror analysis yields bounded/volume diagnostics.

## Eligibility rule used in F3

A small classification is used for fallback eligibility:

- `MaterializationUnsupported` (eligible): BRep diagnostics include `NotImplemented` for unsupported materialization.
- `InvalidIntent` (not eligible): validation/coherence failures (`ValidationFailed`).
- `AnalyzerUncertainty` (not a trigger): everything else.

Only `MaterializationUnsupported` currently triggers fall-forward.

## Transition semantics

When eligible unsupported BRep materialization occurs during boolean execution:

1. replay log is preserved,
2. CIR lowering is attempted from the full lowering plan,
3. if CIR lowering succeeds:
   - state transitions to `CirOnly`,
   - materialization authority becomes `CirIntentOnly`,
   - `MaterializedBody` is null,
   - a transition event records `BRepActive -> CirOnly` with reason `MaterializationUnsupported`.
4. if CIR lowering fails:
   - hard failure is returned with both BRep context and CIR fallback lowering diagnostics.

## CirOnly analysis behavior

For the F3 candidate, `NativeGeometryState.CirMirror` is `Available` with estimated volume and bounds, enabling ongoing CIR-side analysis after fall-forward.

## Export behavior in F3

`FirmamentStepExporter` now explicitly rejects `CirOnly` execution state with a clear diagnostic stating:

- model remains valid/analyzable in CIR,
- exact BRep/AP242 materialization is unavailable,
- CIR→BRep materializer is required.

No tessellation or partial export is attempted.

## Guardrails

- invalid-intent fixtures remain hard failures (no transition),
- supported BRep golden paths remain `BRepActive` and export as before,
- unsupported CIR lowering after eligible BRep failure remains hard failure (no fake `CirOnly`).

## Recommended F4

Implement a scoped CIR-only artifact boundary for export/interop diagnostics (e.g., structured `CirOnly` export contract and per-op blocker payload), then introduce a minimal CIR→BRep materializer for one narrow boolean family.
