# EDGE-X3 — Convex planar AirChamfer Judgment policy lab

## Purpose and scope
EDGE-X3 adds a **lab-only** policy evaluator for convex planar single-edge chamfer requests using `Aetheris.Kernel.Core.Judgment.JudgmentEngine<TContext>`. It classifies requests into deterministic accept/defer/reject/fallback outcomes and emits machine-checkable diagnostics. No convex replacement geometry is emitted in this milestone.

## References
- EDGE-A0 audit: `docs/edge-a0-air-edge-sweep-fillet-chamfer-audit.md`
- EDGE-X1 matrix: `docs/frictionlab/edge-x1-chamfer-fillet-capability-matrix.md`
- EDGE-X2 patch proof: `docs/frictionlab/edge-x2-concave-planar-chamfer-patch-lab.md`
- EDGE-X2.1 scaffold: `docs/frictionlab/edge-x2-1-air-chamfer-policy-scaffold-lab.md`
- EDGE-X2.2 non-orthogonal concave: `docs/frictionlab/edge-x2-2-nonorthogonal-concave-air-chamfer-policy-patch-lab.md`

## Why JudgmentEngine
EDGE-X3 is a bounded strategy-selection problem. `JudgmentEngine` already provides admissibility, deterministic score ordering, tie-break behavior, and rejection data; EDGE-X3 uses that substrate directly (no parallel utility framework).

## Judgment infrastructure touched
- `Aetheris.Kernel.Core.Judgment.JudgmentEngine<TContext>`
- `JudgmentCandidate<TContext>`
- `JudgmentResult<TContext>`

## Request/candidate/decision model
`AirChamferPolicyLab` now builds a judgment context with consideration metrics and evaluates a finite candidate set including:
- `accept-air-chamfer-patch`
- convex deferrals: `defer-convex-replacement-policy`, `defer-convex-replacement-geometry`
- chain/corner/legacy deferrals/fallback
- invalid and unsafe rejects

## Considerations and score model
Deterministic considerations:
- `geometry-support`
- `offset-stability`
- `corner-policy`
- `legacy-readiness`

Diagnostics include:
- `edge-x3-judgment-engine-used`
- `edge-x3-judgment-candidate-created:<name>`
- `edge-x3-judgment-score:<candidate>:<score>`
- `edge-x3-judgment-consideration:<name>:<value>`
- `edge-x3-decision:<decision>`

## Fixture outcomes
- Valid-looking convex planar single-edge: `defer-convex-replacement-geometry`
- Unsafe convex envelope: `reject-unsafe-offset-envelope`
- Canonical concave planar: accepted
- Safe non-orthogonal concave planar: accepted
- Edge/corner chain: deferred
- Legacy-dependent triangle/chamfer fixture: fallback/defer legacy
- Invalid distance/edge/adjacency/classification: rejected

## Non-goals
No production route changes, no public API changes, no STEP/Boolean changes, no convex replacement geometry, no fillet emission, no chain/corner implementation, no triangle migration retries.

## Next milestone
Implement bounded convex replacement topology planning lane (still policy-gated), then wire geometry emission only after admissibility and replacement-plan diagnostics are stable.
