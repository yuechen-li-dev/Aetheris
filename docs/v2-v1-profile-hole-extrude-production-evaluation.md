# V2-V1 Production Evaluation — ProfileHoleExtrude through-hole emission

## Purpose and scope
This milestone evaluates V2-X3's profile-topology-first through-hole extrusion as a bounded production-adjacent emitter for rectangular outer loops with circular through-holes, without `BrepBoolean.Subtract`.

## Architectural references
- V2 doctrine: `docs/aetheris-v2-sweep-first-architecture.md`
- Resolved profile contract (V2-A1): `docs/aetheris-v2-a1-resolved-profile2d-contract.md`
- V2-X1 lab: `docs/frictionlab/v2-x1-resolved-profile2d-lab.md`
- V2-X3 lab evidence: `docs/frictionlab/v2-x3-profile-with-hole-extrude-lab.md`

## Adoption decision
Decision: **production-adjacent emitter added, production route integration deferred**.

Added internal component:
- `Aetheris.Kernel.Firmament/Materializer/ProfileHoleExtrudeEmitter.cs`

Not changed in this milestone:
- no STEP exporter/importer behavior changes,
- no Boolean core behavior changes,
- no broad executor/materializer routing replacement,
- no blind/counterbore/stepped-hole support added.

## Admissibility boundary (exact)
Accepted input boundary in `ProfileHoleExtrudeEmitter`:
- rectangular prism outer profile (`Width`, `Depth` > 0),
- one or more circular holes (full circles only),
- full-height linear extrusion (`Height` > 0),
- hole loops strictly inside boundary,
- non-overlapping hole loops,
- through-hole only.

Rejected/deferred:
- holes outside/touching boundary,
- overlapping holes,
- non-positive radius,
- non-positive height,
- non-rectangular or arbitrary curve loop shapes (not represented by this emitter input type),
- blind/counterbore/stepped holes.

## No-Boolean-subtract guarantee
The candidate path emits BRep topology directly (builder + explicit topology/geometry binding) and does not call `BrepBoolean.Subtract`.
Diagnostics include:
- `v2-v1-profile-hole-extrude-attempted`
- `v2-v1-profile-hole-extrude-accepted`
- `v2-v1-profile-hole-extrude-succeeded`
- `v2-v1-profile-hole-extrude-rejected:<reason>`
- `v2-v1-profile-hole-extrude-no-3d-boolean-subtract`

## Topology/STEP parity findings
Validated cases preserve the expected V2-X3 shape contract:
- planar face count = 6,
- cylindrical face count = hole count,
- STEP smoke contains `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `PLANE`, `CYLINDRICAL_SURFACE`,
- STEP smoke does not contain `BREP_WITH_VOIDS`.

## Fallback behavior
No production executor integration in V2-V1, therefore existing recovery/materializer paths and fallbacks remain unchanged.

## Tests run
- Added focused emitter tests in `Aetheris.Kernel.Firmament.Tests/Integration/ProfileHoleExtrudeEmitterTests.cs` for required valid and invalid cases.
- Existing focused suite execution is retained and reported in milestone run output.

## Remaining limitations
- Emitter currently uses a narrow internal request model, not direct `ResolvedProfile2D` wiring.
- Multiple-outer-loop and open-profile rejection are handled upstream/lab contract today, not by this emitter's request type.
- Integration diagnostics for fallback routes (`...-failed-fallback`, `...-legacy-through-hole`) are deferred until routing integration.

## Next recommended milestone
V2-V2:
1. add guarded integration attempt in through-hole rectangular-box Z-axis routes,
2. keep strict fallback to legacy route,
3. emit route-level fallback diagnostics,
4. compare semantic parity against existing routes with CLI-level coverage.


## V2-V2 note
V2-V2 integrates the bounded plain rectangular Z-axis through-hole subset into production `HoleRecoveryExecutor` routing using `ProfileHoleExtrudeEmitter` with explicit diagnostics and legacy fallback preservation.
