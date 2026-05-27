# AIR-V4 — Cylinder as AirRevolve production migration

## Context
AIR-V4 follows the Aetheris V2 sweep-first doctrine (`docs/aetheris-v2-sweep-first-architecture.md`) and the primitive-as-AIR normalization direction from AIR-X3 (`docs/air-x3-primitive-as-air-normalization-audit.md`).

AIR-X5 evidence (`docs/frictionlab/air-x5-remaining-primitives-air-matrix.md`) established that:
- cylinder `candidate:AirExtrude` remains blocked by `air-x5-air-candidate-unavailable:no-circular-profile-extrude-api`,
- cylinder `candidate:AirRevolve` achieved topology parity + STEP smoke parity,
- recommendation: `ready-for-production-migration`.

## Why AirRevolve (not AirExtrude)
Cylinder production routing now uses the already-proven two-point line-segment revolve lane. Circular-profile extrude is still intentionally deferred because the circular-profile extrude API is not yet present.

## Production change
`BrepPrimitives.CreateCylinder(radius, height)` now validates inputs as before, then delegates internal construction to `BrepRevolve.Create(...)` using:
- profile segment: `(radius, -height/2)` -> `(radius, +height/2)`,
- axis: world Z through origin,
- full-turn revolve.

Public API signature and centered output semantics are unchanged.

## Topology and STEP parity contract
AIR-V4 preserves the observable cylinder contract:
- 3 faces total,
- 1 cylindrical side face,
- 2 planar caps,
- seam-based side loop behavior compatible with existing tests,
- closed manifold body with deterministic bindings,
- STEP smoke markers remain: `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `CYLINDRICAL_SURFACE`, `PLANE`, and no `BREP_WITH_VOIDS` for primitive cylinder export.

## Non-goals
- no cone/frustum migration,
- no sphere/torus migration,
- no circular-profile extrude implementation,
- no STEP exporter/importer behavior changes,
- no Boolean core behavior changes,
- no general AirRevolve framework expansion beyond cylinder routing.

## Tests run
- `dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "BrepPrimitives|CreateCylinder|Step242|Primitive|BrepBoolean|SafeComposition|Cylinder"`
- `dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "AirPrimitiveMatrix|AirBoxExtrude|AirProfileStack|ProfileStackExtrude|RecoveryPolicy|CIRLab"`
- `dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "FirmamentPrimitive|FirmamentStepExporter|SemanticRecovery|FrepMaterializer|Rematerialize"`
- `./scripts/test-all.sh`

## Known limitations
- Circular-profile AIR extrude remains blocked (same AIR-X5 blocker).
- Sphere/torus still require broader revolve profile support and singularity/seam parity hardening.

## Next recommended primitive migration step
Given AIR-X5 readiness, next bounded candidate remains cone/frustum production-route hardening through existing revolve lane while preserving emitter/topology parity constraints.

## AIR-V5.1 follow-up
- CLI external validation coverage for AIR-routed cylinder behavior was added in `docs/air-v5-1-cli-validation.md`.
