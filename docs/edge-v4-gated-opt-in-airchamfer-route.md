# EDGE-V4 — gated opt-in Firmament AirChamfer route

## Purpose and scope

EDGE-V4 adds a deliberately narrow, internal/test-only Firmament execution seam that can select an AirChamfer candidate **only** for the known controlled CH-03-style convex plane-plane single-edge fixture proven by EDGE-X13. The route is off by default and does not change normal Firmament chamfer execution.

Reference trail:

- EDGE-A1 compatibility matrix: `docs/edge-a1-chamfer-fillet-support-compatibility-matrix.md`
- EDGE-X13 Firmament shadow diagnostics: `docs/edge-x13-firmament-airchamfer-shadow-diagnostics.md`
- EDGE-V3 shadow route: `docs/edge-v3-air-chamfer-shadow-route.md`
- EDGE-V2 real-body prototype evidence: `docs/frictionlab/edge-x8-controlled-local-air-chamfer-topology-graft-lab.md` and subsequent AirChamfer corpus docs

## Opt-in flag and seam

The seam is `FirmamentPrimitiveExecutor.Execute(loweringPlan, FirmamentAirChamferExperimentalOptions)`.

The opt-in switch is `EnableAirChamferExperimentalRoute`. `FirmamentAirChamferExperimentalOptions.Disabled` is the default path used by existing production-facing `FirmamentPrimitiveExecutor.Execute(loweringPlan)` and by `FirmamentCompiler`.

The candidate path is injected through `CandidateProvider`, keeping the production Firmament assembly independent of FrictionLab while allowing tests to invoke `AirChamferShadowRoute->AirChamferRealBodyPrototype` as ground-truth evidence.

## Default behavior guarantee

Default execution records disabled/default diagnostics and returns the legacy `BrepBoundedChamfer` body. AirChamfer cannot become the result unless the internal/test-only options object explicitly enables the experimental route and supplies a successful candidate provider.

## Supported controlled case

The accepted shape guard is intentionally exact:

- fixture feature id `edge_x13_legacy_edge_break` from source feature `base`,
- source state is `BoxRoot`,
- chamfer distance is positive and finite,
- selection is exactly `x_max_y_max`,
- adjacent face family is planar,
- no edge chain,
- no corner chain,
- no legacy-dependent topology marker,
- candidate evidence reports topology, STEP smoke, recognition parity, and no 3D Boolean use.

Everything else remains legacy/deferred: normal Firmament chamfers, arbitrary model edge selection, multiple edges, corners/chains, non-planar/cylindrical adjacent faces, fillets, variable distance, triangle migration, sketch solving, clipping, NURBS/freeform, and arbitrary BRep mutation.

## Fallback policy

The route computes the legacy chamfer first. If opt-in is disabled, the guard rejects, the candidate provider is missing, the provider throws, the candidate rejects/defers/fails, topology evidence is missing, STEP smoke fails, recognition parity fails, or 3D Boolean use is reported, execution returns the already-computed legacy body and emits fallback diagnostics.

Legacy `BrepBoundedChamfer` remains default-authoritative. EDGE-V4 is not public stable API and is not a production default route replacement.

## Diagnostics contract

Deterministic EDGE-V4 diagnostics include:

- `edge-v4-air-chamfer-opt-in-route-started`
- `edge-v4-air-chamfer-opt-in-enabled`
- `edge-v4-air-chamfer-opt-in-disabled`
- `edge-v4-supported-case-accepted`
- `edge-v4-supported-case-rejected:<reason>`
- `edge-v4-air-chamfer-shadow-route-invoked`
- `edge-v4-air-chamfer-candidate-selected`
- `edge-v4-air-chamfer-candidate-rejected:<reason>`
- `edge-v4-air-chamfer-candidate-failed-fallback`
- `edge-v4-legacy-fallback-used`
- `edge-v4-legacy-default-route-used`
- `edge-v4-production-default-unchanged`
- `edge-v4-no-3d-boolean-used`

The selected test provider also preserves EDGE-V3 shadow diagnostics such as STEP smoke and recognition parity success.

## Tests and results

Focused tests live in `Aetheris.Kernel.Firmament.Tests/AirChamferFirmamentOptInRouteTests.cs`:

- default disabled route uses the unchanged legacy result and does not select AirChamfer,
- opt-in supported controlled case invokes the shadow route and selects the AirChamfer candidate,
- unsupported non-planar face-family, edge-chain, corner-chain, and legacy-dependent triangle-style cases fallback to legacy with deterministic rejection diagnostics,
- injected candidate failure falls back to legacy.

## No-3D-Boolean guarantee

EDGE-V4 does not call the 3D Boolean core in the AirChamfer candidate path. The executor emits `edge-v4-no-3d-boolean-used`, and the candidate must affirm no 3D Boolean use before selection.

## Non-goals and limitations

EDGE-V4 does not change STEP exporter/importer behavior, Boolean core behavior, fillet geometry, public stable APIs, arbitrary edge selection, edge/corner chains, triangle migration retry, sketch solving, clipping, NURBS, or freeform support. It does not make AirChamfer the default production chamfer route.

## Next recommended milestone

Recommended next work is **EDGE-V4.1 fallback/failure injection hardening** if route isolation needs more negative evidence, or **EDGE-V5** to add exactly one more controlled opt-in fixture after this route remains stable. If the focus shifts to fillets, begin with **EDGE-X14 AirFillet architecture audit**. For broader body mutation, use **EDGE-X15 controlled body mutation hardening** before widening Firmament selection.
