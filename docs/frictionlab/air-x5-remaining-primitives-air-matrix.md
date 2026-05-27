# AIR-X5 — Remaining primitives as AIR lab matrix

## Purpose and scope
Lab-only evidence matrix for cylinder, cone/frustum, sphere, and torus comparing current primitive constructors against AIR-style candidates (extrude/revolve) where supported.

Non-goals (at AIR-X5 time): no production routing changes, no public API changes, no STEP import/export changes, no Boolean core changes, no NURBS/freeform work.

Status note after AIR-V4: cylinder has now been production-routed via the proven AirRevolve lane (`docs/air-v4-cylinder-as-air-revolve-production.md`). Circular-profile AirExtrude remains blocked.

## References
- AIR-X3: `docs/air-x3-primitive-as-air-normalization-audit.md`
- AIR-X4: `docs/frictionlab/air-x4-box-as-air-extrude-lab.md`
- AIR-V3: `docs/air-v3-box-as-air-extrude-production.md`

## Matrix overview
Implemented in `Aetheris.Firmament.FrictionLab/CIRLab/AirPrimitiveMatrixLab.cs` and validated in `Aetheris.FrictionLab.Tests/CIRLab/AirPrimitiveMatrixLabTests.cs`.

Rows include baseline plus candidate rows with deterministic diagnostics:
- `air-x5-primitive-matrix-lab-started`
- `air-x5-baseline-created`
- `air-x5-air-candidate-created`
- `air-x5-air-candidate-unavailable:<reason>`
- `air-x5-topology-parity-succeeded|mismatch`
- `air-x5-step-smoke-succeeded|failed`
- `air-x5-recommendation:<value>`

## Per-primitive findings
- Cylinder: baseline + AIR extrude candidate + AIR revolve candidate evaluated. Extrude path is representable; readiness depends on strict topology and STEP parity.
- Cone/frustum: baseline uses canonical revolve lane; AIR revolve candidate evaluated for frustum and apex-cone.
- Sphere: baseline direct constructor validated. AIR revolve candidate is explicitly unavailable with current revolve lab support (two-point line-segment only).
- Torus: baseline direct constructor validated. AIR revolve candidate is explicitly unavailable with current revolve lab support (no offset circle profile support).

## Topology parity findings
Parity is measured against each case baseline and recorded per row, including vertex/edge/face and analytic family counts plus loops/coedges.

## STEP smoke findings
Each row exports STEP and checks required markers by family:
- Cylinder: `CYLINDRICAL_SURFACE`, `PLANE`, `MANIFOLD_SOLID_BREP`, no `BREP_WITH_VOIDS`
- Cone/frustum: `CONICAL_SURFACE`, `MANIFOLD_SOLID_BREP`, no `BREP_WITH_VOIDS`
- Sphere: `SPHERICAL_SURFACE`, `MANIFOLD_SOLID_BREP`, no `BREP_WITH_VOIDS`
- Torus: `TOROIDAL_SURFACE`, `MANIFOLD_SOLID_BREP`, no `BREP_WITH_VOIDS`

## Candidate readiness ranking
Allowed recommendation vocabulary:
- `ready-for-production-migration`
- `needs-emitter-parity-work`
- `needs-air-revolve-lab-support`
- `keep-direct-constructor-for-now`

## Recommended production migration order
Evidence-driven provisional order:
1. Cylinder (AIR extrude/revolve candidates available; parity measurable now)
2. Cone/frustum (revolve policy and parity lane present)
3. Sphere (hold direct constructor pending seam/pole parity lab support)
4. Torus (hold direct constructor pending seam/pole + profile support parity)

## Explicit blockers
- Missing AIR revolve lab support for sphere/torus profile classes.
- Seam/pole parity risk for sphere/torus representations.
- Candidate topology mismatches must be resolved before migration recommendations.

## Test commands run
- `dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "AirPrimitiveMatrix|AirBoxExtrude|AirProfileStack|ProfileStackExtrude|RecoveryPolicy|CIRLab"`
- `dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "BrepPrimitives|CreateCylinder|CreateCone|CreateSphere|CreateTorus|BrepRevolve|Step242|Primitive|Conical|Torus|Sphere"`


## Explicit per-row results (current AirPrimitiveMatrixLab)

| Case | Candidate name | Body produced | Topology parity | STEP smoke | Recommendation | Blocker (if any) |
|---|---|---:|---|---|---|---|
| cyl-5x10 | baseline | yes | n/a | pass | keep-direct-constructor-for-now | none |
| cyl-5x10 | candidate:AirExtrude | no | mismatch | failed | needs-air-revolve-lab-support | `air-x5-air-candidate-unavailable:no-circular-profile-extrude-api` |
| cyl-5x10 | candidate:AirRevolve | yes | parity-succeeded | pass | ready-for-production-migration | none |
| cyl-3x12 | baseline | yes | n/a | pass | keep-direct-constructor-for-now | none |
| cyl-3x12 | candidate:AirExtrude | no | mismatch | failed | needs-air-revolve-lab-support | `air-x5-air-candidate-unavailable:no-circular-profile-extrude-api` |
| cyl-3x12 | candidate:AirRevolve | yes | parity-succeeded | pass | ready-for-production-migration | none |
| cyl-invalid | baseline | no | n/a | failed | keep-direct-constructor-for-now | invalid baseline input |
| cyl-invalid | candidate:AirExtrude | no | mismatch | failed | needs-air-revolve-lab-support | `air-x5-air-candidate-unavailable:no-circular-profile-extrude-api` |
| cyl-invalid | candidate:AirRevolve | no | mismatch | failed | keep-direct-constructor-for-now | `air-x5-air-candidate-unavailable:invalid-baseline-input` |
| frustum-5-2-10 | baseline | yes | n/a | pass | keep-direct-constructor-for-now | none |
| frustum-5-2-10 | candidate:AirRevolve | yes | parity-succeeded | pass | needs-emitter-parity-work | none |
| cone-apex-5-0-10 | baseline | yes | n/a | pass | keep-direct-constructor-for-now | none |
| cone-apex-5-0-10 | candidate:AirRevolve | yes | parity-succeeded | pass | needs-emitter-parity-work | none |
| cone-invalid | baseline | no | n/a | failed | keep-direct-constructor-for-now | invalid baseline input |
| cone-invalid | candidate:AirRevolve | no | mismatch | failed | keep-direct-constructor-for-now | `air-x5-air-candidate-unavailable:invalid-baseline-input` |
| sphere-5 | baseline | yes | n/a | pass | keep-direct-constructor-for-now | none |
| sphere-5 | candidate:AirRevolve | no | mismatch | failed | needs-air-revolve-lab-support | `air-x5-air-candidate-unavailable:current-brep-revolve-supports-only-two-point-line-segment-profile` |
| sphere-2.5 | baseline | yes | n/a | pass | keep-direct-constructor-for-now | none |
| sphere-2.5 | candidate:AirRevolve | no | mismatch | failed | needs-air-revolve-lab-support | `air-x5-air-candidate-unavailable:current-brep-revolve-supports-only-two-point-line-segment-profile` |
| sphere-invalid | baseline | no | n/a | failed | keep-direct-constructor-for-now | invalid baseline input |
| sphere-invalid | candidate:AirRevolve | no | mismatch | failed | needs-air-revolve-lab-support | `air-x5-air-candidate-unavailable:current-brep-revolve-supports-only-two-point-line-segment-profile` |
| torus-8-2 | baseline | yes | n/a | pass | keep-direct-constructor-for-now | none |
| torus-8-2 | candidate:AirRevolve | no | mismatch | failed | needs-air-revolve-lab-support | `air-x5-air-candidate-unavailable:current-brep-revolve-supports-only-two-point-line-segment-profile` |
| torus-5-1 | baseline | yes | n/a | pass | keep-direct-constructor-for-now | none |
| torus-5-1 | candidate:AirRevolve | no | mismatch | failed | needs-air-revolve-lab-support | `air-x5-air-candidate-unavailable:current-brep-revolve-supports-only-two-point-line-segment-profile` |
| torus-invalid-major | baseline | no | n/a | failed | keep-direct-constructor-for-now | invalid baseline input |
| torus-invalid-major | candidate:AirRevolve | no | mismatch | failed | needs-air-revolve-lab-support | `air-x5-air-candidate-unavailable:current-brep-revolve-supports-only-two-point-line-segment-profile` |
| torus-invalid-minor | baseline | no | n/a | failed | keep-direct-constructor-for-now | invalid baseline input |
| torus-invalid-minor | candidate:AirRevolve | no | mismatch | failed | needs-air-revolve-lab-support | `air-x5-air-candidate-unavailable:current-brep-revolve-supports-only-two-point-line-segment-profile` |
| torus-invalid-intersect | baseline | no | n/a | failed | keep-direct-constructor-for-now | invalid baseline input |
| torus-invalid-intersect | candidate:AirRevolve | no | mismatch | failed | needs-air-revolve-lab-support | `air-x5-air-candidate-unavailable:current-brep-revolve-supports-only-two-point-line-segment-profile` |

Notes:
- “STEP smoke pass” means export succeeded, required markers were present for that primitive family, and `BREP_WITH_VOIDS` was absent.
- For baseline rows, topology parity is not applicable because parity is only computed candidate-vs-baseline.
