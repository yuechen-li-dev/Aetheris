# CIR-F8.6: retained-loop grouping + canonical ordering/orientation contracts

CIR-F8.6 adds a dry-run grouping layer on top of CIR-F8.5 retained-loop descriptors.

## Purpose

For each `FacePatchCandidate`, loop descriptors are now grouped into deterministic loop groups that carry:

- group kind,
- group readiness (`ExactReady` / `SpecialCaseReady` / `Deferred` / `Unsupported` / `NotApplicable`),
- orientation policy (`UseCandidateOrientation` / `ReverseForToolCavity` / `Deferred` / `Unsupported`),
- stable ordering key,
- per-group diagnostics.

No BRep loop/edge/coedge/face emission is introduced.

## Deterministic ordering

Within a candidate, loop descriptors are ordered by:

1. source family,
2. opposite family,
3. loop kind,
4. loop status,
5. trim curve family.

Groups are then ordered by ordinal string key:

`{sourceFamily}|{retentionRole}|{groupKind}|{readiness}|{orientation}|{count}`

## JudgmentEngine decision

`JudgmentEngine` was **not** introduced in this milestone because group selection is a direct monotonic reduction from loop descriptor statuses (no competing candidates requiring scored tie-breaks).

## Example outcomes

- `box - cylinder`: base and tool candidates emit trim groups; tool-side exact/special groups use `ReverseForToolCavity`.
- `box - sphere`: spherical tool candidate emits exact-ready circular trim groups with `ReverseForToolCavity`.
- `box - torus`: toroidal groups are deferred and diagnostics keep matrix rationale (`quartic/algebraic`).

## Deferred work

- No curve parameter solving.
- No topology assembly.
- No generated topology naming (SEM-A0 preserved).

## Recommended CIR-F8.7 next step

Consume `RetainedRegionLoopGroup` contracts in a topology-assembly planner that maps group kinds to future BRep loop intents while preserving deterministic ordering and explicit rejection diagnostics.
