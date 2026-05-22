# CIR-F8.4: bounded subtract retention classification for face patch dry-run candidates

## Purpose

CIR-F8.4 adds bounded **retention classification** on top of CIR-F8.3 dry-run candidates for subtract trees.

For `A - B`, candidate classification now models the generic subtract-boundary rule:

1. portions of `A` boundary retained where `B` is outside,
2. portions of `B` boundary retained where `B` lies inside `A` (tool-cavity side, reversed orientation).

This milestone remains descriptor/diagnostic-only. It does not emit BRep topology.

## Retention model

Each dry-run `FacePatchCandidate` now includes:

- `RetentionRole`
  - `BaseBoundaryRetainedOutsideTool`
  - `ToolBoundaryRetainedInsideBase`
  - `NotApplicable` / `Unsupported` / deferred variants
- `RetentionStatus`
  - `KnownTrimmedSurface`
  - `KnownWholeSurface` (reserved for future conservative proofs)
  - `Deferred`
  - `Unsupported`
- `RetentionReason` string
- `OppositeFamilies` snapshot (opposite-side source surface families used for trim-capability context)

`FacePatchCandidateReadiness` remains trim-capability readiness (`ExactReady`, `TrimDeferred`, `Unsupported`) and is not replaced by retention semantics.

## Subtract-only behavior

Retention classification applies only to `CirSubtractNode` roots in CIR-F8.4.

- subtract root: base/tool candidates receive explicit subtract retention roles.
- non-subtract root: candidates are marked `NotApplicable` + deferred retention diagnostic.

## Example outcomes

### subtract(box, cylinder)

- box planar source candidates -> `BaseBoundaryRetainedOutsideTool`
- tool cylindrical source candidate -> `ToolBoundaryRetainedInsideBase` with reversed patch orientation role
- planar/cylindrical trim capability remains `SpecialCaseOnly` (exact-ready scaffold), with retained-loop assembly deferred

### subtract(box, sphere)

- spherical tool source candidate -> `ToolBoundaryRetainedInsideBase`
- planar/spherical trim capability -> `ExactSupported` with circle family
- diagnostics indicate retention region loops/topology not assembled yet

### subtract(box, torus)

- toroidal tool source candidate -> `ToolBoundaryRetainedInsideBase`
- planar/toroidal remains deferred
- readiness -> `TrimDeferred`
- retention status -> `Deferred` with explicit matrix reason (quartic/algebraic)

## Diagnostics added

CIR-F8.4 distinguishes:

- role known + trim capability available + retained-loop assembly deferred,
- role known + trim policy deferred,
- subtract retention not applicable (non-subtract roots),
- topology assembly not implemented.

This avoids overloading everything into generic unsupported.

## Still deferred (intentional)

- trim loop computation and boundary graphing
- BRep face/edge/coedge/vertex emission
- topology assembly
- pair-specific subtract materializers
- generated topology naming (SEM-A0 preserved)

## Recommended next step (CIR-F8.5)

Implement bounded retained-region loop scaffolding from trim-capability-ready pairs (starting planar/spherical and constrained planar/cylindrical), while still deferring full BRep emission.
