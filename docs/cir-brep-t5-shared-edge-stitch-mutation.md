# CIR-BREP-T5: first bounded shared-edge stitch mutation

## Outcome in this milestone

**Meaningful progression**.

T5 now executes a stricter mutation gate and attempts one deterministic ready `SharedTrimIdentity` operation, but reports a precise blocker when topology mutation is reached.

## Topology/remap diagnosis

1. Emitted planar and cylindrical patches currently materialize as **independent `BrepBody` instances**.
2. Shared-edge mutation requires a combined-body remap pass that can:
   - copy topology ids into one merged `TopologyModel`,
   - remap `face/loop/edge/coedge/vertex` references,
   - preserve geometry bindings in `BrepBindingModel`,
   - and then rewrite one candidate coedge to use a canonical edge id.
3. Current executor path has no safe internal utility for this combined remap+rewrite operation, so T5 must stop before fake stitching.

## Executor behavior

`SurfaceFamilyStitchExecutor.TryExecute(...)` now:

- requires readiness=`Ready`, kind=`SharedTrimIdentity`,
- requires token equality between entry A/B,
- requires compatible role pair (`InnerCircularTrim` with cylindrical top/bottom boundary),
- requires orientation policy compatibility,
- requires concrete topology refs including face+loop+edge+coedge,
- processes at most one ready candidate deterministically (ordered by `OrderingKey`),
- emits explicit blocker diagnostics when mutation is reached.

## Safety guardrails

- No coordinate-coincidence merging.
- No full shell claim (`FullShellClaimed=false`).
- No STEP export attempt (`StepExportAttempted=false`).
- No boolean behavior expansion.

## Next milestone

Implement a bounded, validator-backed combined-body remap utility for emitted patch bodies, then apply one real coedge->canonical-edge rewrite with invariant checks.
