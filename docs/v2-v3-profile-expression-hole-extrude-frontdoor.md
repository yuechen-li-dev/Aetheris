# V2-V3: ProfileExpression-to-ProfileHoleExtrude internal front door

## Purpose and scope
This milestone introduces a **production-adjacent internal** front door named `ProfileExpressionHoleExtrudeEmitter` that accepts a bounded profile expression subset and emits through-hole prisms through `ProfileHoleExtrudeEmitter` (no 3D Boolean).

Scope is intentionally narrow:
- `Difference(Rectangle, Circle...)` only.
- Full-height extrusion only.
- Circle holes must be fully contained, non-overlapping, non-touching.

## References
- V2 doctrine: `docs/aetheris-v2-sweep-first-architecture.md`
- V2-A1: `docs/aetheris-v2-a1-resolved-profile2d-contract.md`
- V2-X1: `docs/frictionlab/v2-x1-resolved-profile2d-lab.md`
- V2-X3: `docs/frictionlab/v2-x3-profile-with-hole-extrude-lab.md`
- V2-X4: `docs/frictionlab/v2-x4-profile-boolean-normalization-lab.md`
- V2-V1: `docs/v2-v1-profile-hole-extrude-production-evaluation.md`
- V2-V2: `docs/v2-v2-profile-hole-extrude-through-hole-integration.md`
- V2-X5/V2-V3 lab chain: `docs/frictionlab/v2-v3-profile-boolean-to-extrude-pipeline.md`

## Why after lab chain
V2-X5 proved feasibility via lab chaining and internals exposure. This milestone captures that proven bounded route behind a cleaner **internal production-adjacent seam** so lab evidence can reuse production-adjacent orchestration without duplicating normalization/adaptation/emission flow.

## Internal design
`ProfileExpressionHoleExtrudeEmitter` (internal, Firmament) performs:
1. deterministic front-door diagnostics,
2. bounded normalization/validation,
3. adaptation to `ProfileHoleExtrudeRequest`,
4. call to `ProfileHoleExtrudeEmitter`.

No 3D Boolean operation is called in this route.

## Diagnostics contract
Emits deterministic route diagnostics:
- `v2-v3-profile-expression-frontdoor-attempted`
- `v2-v3-profile-expression-normalization-attempted`
- `v2-v3-profile-expression-normalized`
- `v2-v3-profile-expression-rejected:<reason>`
- `v2-v3-profile-expression-deferred:<reason>`
- `v2-v3-profile-expression-adapted-to-hole-emitter`
- `v2-v3-profile-hole-extrude-attempted`
- `v2-v3-profile-hole-extrude-succeeded`
- `v2-v3-profile-hole-extrude-failed:<reason>`
- `v2-v3-no-3d-boolean-used`

## Invalid/deferred behavior
Rejected reasons include invalid rectangle/circle, outside/touching boundary, overlap/touch, unsupported op/primitive, and emitter validation failure.
Deferred currently includes capsule and rectangle-right/topology cases.
Invalid/deferred paths stop before BRep emission.

## InternalsVisibleTo status
`InternalsVisibleTo("Aetheris.Firmament.FrictionLab")` is retained and intentionally bounded for lab evidence reuse of this internal seam; no public API expansion was introduced.

## Topology/STEP findings
Success cases preserve expected topology convention (6 planar faces + N cylindrical faces) and STEP smoke markers (`ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `PLANE`, `CYLINDRICAL_SURFACE`, no `BREP_WITH_VOIDS`).

## Production routing status
No production user-facing route broadening was introduced. Existing routes and fallback behavior remain unchanged.

## Remaining limitations
No sketch solver, no generalized 2D clipping, no 3D Boolean fallback, no blind/counterbore/stepped/cross-axis variants, no multiple-material-island support.

## Next milestone
Evaluate controlled production routing at a narrow internal seam once bounded expression admission policy and telemetry thresholds are agreed.
