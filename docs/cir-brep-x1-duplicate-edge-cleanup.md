# CIR-BREP-X1 — bounded duplicate-edge cleanup contract after stitch rewrite

## Purpose
CIR-BREP-X1 adds an evidence-only duplicate-edge cleanup planner for the topology state after CIR-BREP-T7 shared-edge rewrite.

## Evidence-only duplicate classification
`DuplicateEdgeCleanupPlanner` classifies duplicate edges only from stitch evidence:
- `AppliedStitchOperation` canonical edge id,
- duplicate edge id,
- rewritten coedge id,
- token ordering key.

No duplicate inference by geometric or coordinate coincidence is permitted.

## Boundary edge classification policy
For each edge in combined/rewrite topology, classify by coedge use count:
- `0` + stitch duplicate evidence → `StitchDuplicateUnreferenced`.
- `1` → `ShellClosureBlocker`.
- `2` → `ExpectedOpenBoundary` (non-blocker in this bounded check).
- `>2` → `Ambiguous`.
- `0` without stitch relation → `Ambiguous`.

## Cleanup mutation status
CIR-BREP-X1 is planning-only:
- `CleanupMutationImplemented=false` always.
- Diagnostic emitted: `duplicate-edge-cleanup: candidate identified; mutation deferred`.

No topology mutation, no vertex merge, no shell closure claim, and no STEP behavior change.

## Pressure test integration
`SurfaceFamilyBoxCylinderPressureTest` now runs duplicate-edge cleanup planning and records:
- candidate/deferred counts,
- shell blocker count,
- ambiguous count,
- stage diagnostics proving planning-only behavior.

## Observed blocker priorities
Primary remaining blockers stay explicit:
- `duplicate-edge-cleanup-mutation-deferred`,
- `vertex-merge-needed`,
- `shell-not-proven-closed`,
- `edges-with-one-coedge`,
- `step-smoke-skipped-shell-not-closed`.

## JudgmentEngine decision
JudgmentEngine is not used in X1 because classification is deterministic map/reduce from stitch evidence + edge-use counts, with ambiguous cases explicitly marked instead of scored.

## Next milestone
CIR-BREP-X2 should add bounded, safe removal for stitch-linked zero-coedge duplicate edges only after validating topology and geometry-binding deletion contracts end-to-end.
