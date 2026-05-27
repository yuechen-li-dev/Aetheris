# V2-V2: bounded ProfileHoleExtrude integration for through-hole route

## Purpose and scope
V2-V2 integrates the existing bounded `ProfileHoleExtrudeEmitter` into production execution for a narrow through-hole subset only, while preserving legacy fallback behavior.

References:
- `docs/aetheris-v2-sweep-first-architecture.md`
- `docs/aetheris-v2-a1-resolved-profile2d-contract.md`
- `docs/frictionlab/v2-x1-resolved-profile2d-lab.md`
- `docs/frictionlab/v2-x3-profile-with-hole-extrude-lab.md`
- `docs/v2-v1-profile-hole-extrude-production-evaluation.md`

## Integration seam chosen
Integration is implemented in `HoleRecoveryExecutor.Execute(HoleRecoveryPlan)` as the narrow through-hole execution seam before AIR/legacy routing.

## Exact admissibility boundary
Accepted only when all hold:
- host is rectangular box,
- axis is Z,
- hole kind is through,
- depth kind is through,
- entry/exit are plain,
- finite host/tool placement,
- exactly one through cylindrical segment (`IsThrough=true`, `AnchorSide=Through`),
- mapped local hole center and radius passes emitter validation (inside rectangle, non-touching).

All other cases deterministically reject and fallback.

## No-Boolean-subtract guarantee (accepted route)
For accepted V2-V2 route, hole body is emitted by `ProfileHoleExtrudeEmitter` and translated; no `BrepBoolean.Subtract` call is used in this path.

## Fallback behavior
On reject or emitter failure, diagnostics record rejection/failure and executor continues with legacy through-hole route (`ThroughHoleRecoveryExecutor`) unchanged.

## Diagnostics contract
Success:
- `v2-v2-profile-hole-extrude-attempted`
- `v2-v2-profile-hole-extrude-accepted`
- `v2-v2-profile-hole-extrude-no-3d-boolean-subtract`
- `v2-v2-profile-hole-extrude-succeeded`

Reject/fallback includes:
- `v2-v2-profile-hole-extrude-rejected:<reason>`
- `v2-v2-fallback-legacy-through-hole`

Failure/fallback includes:
- `v2-v2-profile-hole-extrude-failed-fallback`
- `v2-v2-fallback-legacy-through-hole`

## Topology/STEP parity
For accepted centered and off-center single-hole cases:
- planar faces = 6,
- cylindrical faces = 1,
- STEP smoke markers pass (`ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `PLANE`, `CYLINDRICAL_SURFACE`) and no `BREP_WITH_VOIDS`.

## Invalid/rejected cases
Rejected for unsupported host/axis/hole/depth/relief semantics, invalid placement, unsupported multi-profile semantics, and emitter validation failures.

## Tests run
See milestone run commands and focused `ProfileStackExtrudeHoleFamilyMigrationTests` coverage.

## Remaining limitations
- Multi-hole through plans are not currently representable at this seam via a single `HoleRecoveryPlan` execution unit.
- Blind/counterbore/stepped/countersink/chamfer/cross-axis and broader profile normalization remain out of scope.

## Next recommended milestone
Extend semantic planning/adapter contracts to represent bounded multi-hole through plans in a single admissible executable plan, then reuse this route without broadening emitter geometry scope.


## V2-V3 chaining note
A lab-only V2-V3 pipeline now demonstrates compile-time profile Boolean expression normalization feeding bounded emitter input (rectangle minus circles) and STEP smoke validation, with no 3D Boolean in the candidate path.


Update note (V2-V3): no change to V2-V2 accepted through-hole production route; new profile-expression front door is internal production-adjacent only.
