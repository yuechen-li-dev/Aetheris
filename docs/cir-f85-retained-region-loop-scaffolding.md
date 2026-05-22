# CIR-F8.5: retained-region loop scaffolding for trim-ready face patch candidates

## Purpose

CIR-F8.5 adds a dry-run-only descriptor layer between CIR-F8.4 retention classification and future topology assembly.

For subtract candidates whose retention role is known, the generator now emits **retained-region loop descriptors** that state what loop families would be required to bound retained patches.

No BRep topology is created.

## Descriptor model

Each `FacePatchCandidate` now carries:

- `RetainedRegionLoops` (`RetainedRegionLoopDescriptor[]`)
- `LoopReadiness` (`ExactReady`, `SpecialCaseReady`, `Deferred`, `Unsupported`)
- `LoopDiagnostic` (candidate-level summary)

`RetainedRegionLoopDescriptor` contains:

- `LoopKind`
- `TrimCurveFamily`
- `TrimCapability`
- `SourceSurfaceFamily`
- `OppositeSurfaceFamily`
- `OrientationHint`
- `RetentionRole`
- `Status`
- `Diagnostic`

## Loop kinds and statuses

Loop kinds are bounded to scaffold vocabulary:

- `OuterBoundary`
- `InnerTrim`
- `MouthTrim`
- `CapTrim`
- `SeamTrim`
- `Deferred`
- `Unsupported`

Status reflects trim capability policy:

- `ExactReady` for `ExactSupported`
- `SpecialCaseReady` for `SpecialCaseOnly`
- `Deferred` for matrix-deferred entries
- `Unsupported` for unsupported entries

## Behavior by scenario

### subtract(box, cylinder)

- Base planar retained candidates emit `InnerTrim` loop descriptors against planar/cylindrical pairings.
- Cylindrical tool retained candidates emit `MouthTrim` loop descriptors against base planar families.
- Status surfaces as `SpecialCaseReady` (planar/cylindrical matrix policy) and can include exact-ready descriptors for planar/planar pairings.

### subtract(box, sphere)

- Base planar retained candidates emit circular loop descriptors from planar/spherical exact trim capability.
- Spherical tool retained candidates emit circular `MouthTrim` loop descriptors with `ExactReady` status.

### subtract(box, torus)

- Toroidal candidates emit deferred loop descriptors with matrix reason.
- Diagnostics preserve explicit quartic/algebraic deferred reason from planar/toroidal policy.

## Non-subtract behavior

For non-subtract roots, retention remains `NotApplicable`; loop descriptors are intentionally empty and diagnostics explain the bounded scope.

## Still deferred (intentional)

- Any BRep loop/coedge/edge/vertex emission
- Exact trim curve parameter solving
- Topology assembly and adjacency graphing
- Pair-specific subtract materializers
- Generated topology naming (SEM-A0 preserved)

## Recommended next step (CIR-F8.6)

Introduce bounded retained-loop grouping and canonical ordering per retained candidate (outer vs inner grouping and orientation contracts) while still avoiding BRep emission; use this to define a stable handoff contract for future topology assembly.
