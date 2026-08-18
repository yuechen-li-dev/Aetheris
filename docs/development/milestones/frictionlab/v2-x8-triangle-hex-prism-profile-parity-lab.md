# V2-X8 — Triangle/hex prism line-profile extrusion parity lab

## Purpose and scope
Lab-only parity probe compares existing triangle/hex prism baseline constructors against candidate emission through `LineArcProfileExtrudeEmitter` using resolved line-only loops. No production routing is changed.

## Doctrine and prior milestone references
- V2 doctrine: `docs/development/milestones/general/aetheris-v2-sweep-first-architecture.md`
- V2-A1: `docs/development/milestones/general/aetheris-v2-a1-resolved-profile2d-contract.md`
- V2-X7: `docs/development/milestones/frictionlab/v2-x7-line-arc-profile-extrude-lab.md`
- V2-V4: `docs/development/milestones/general/v2-v4-line-arc-profile-extrude-production-evaluation.md`
- V2-A2: `docs/development/milestones/general/v2-a2-prismatic-operation-migration-audit.md`

Triangle then hex were chosen because both are line-only outer loops with existing fixture/test evidence and bounded planar topology expectations.

## Baseline and candidate paths
- Baseline triangle: `BrepPrimitives.CreateTriangularPrism(baseWidth, baseDepth, height)`.
- Baseline hex: `BrepPrimitives.CreateHexagonalPrism(acrossFlats, height)`.
- Candidate path: adapt same profile convention into one non-hole `LineArcProfileLoop2D`, then call `LineArcProfileExtrudeEmitter.TryEmit`.

## Coordinate convention findings
- Triangle: `( -w/2,-d/2 ) -> ( +w/2,-d/2 ) -> ( 0,+d/2 )`, centered along Z via symmetric height.
- Hex: regular hex with circumradius `acrossFlats/sqrt(3)` and angles `k*pi/3`, centered at origin and symmetric in Z.

## Test cases and results
Cases include two valid triangle dimensions, two valid hex dimensions, and invalid height/size/non-finite rows. Lab rows are deterministic across repeat runs and include deterministic diagnostics.

## Topology parity findings
For valid rows, baseline and candidate topology summaries are compared on:
- body produced,
- vertex/edge/face counts,
- planar/cylindrical face counts,
- loop/coedge counts,
- extents.

If mismatch occurs, diagnostic `v2-x8-topology-parity-mismatch:<case>` is emitted.

## STEP smoke findings
Candidate bodies are STEP-smoke checked for:
- required markers: `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `PLANE`;
- exclusion markers: `CYLINDRICAL_SURFACE`, `BREP_WITH_VOIDS`.

## Invalid/deferred handling
Invalid numeric inputs reject deterministically and emit `v2-x8-invalid-input-rejected` with recommendation `prism-profile-invalid-rejected`.

## Migration readiness statement
Current lab rows are designed to provide bounded parity evidence only. Production migration decision should use this parity output plus existing fixture/build-export coverage.

## Non-goals
- no production routing migration,
- no slot migration,
- no full clipping engine,
- no STEP exporter change,
- no Boolean core change.

## Recommended next step
- If parity rows remain fully green: proceed to V2-V5 triangle production migration candidate.
- Then V2-V6 hex production migration candidate.
- If mismatch appears: harden emitter/convention mapping before production migration.


## Update note (consumed by V2-V5)

Triangle parity evidence from this lab was evaluated in V2-V5; production triangle migration was reverted after downstream Firmament chamfer parity regression, so lab evidence remains preparatory rather than consumed for final routing change. Hex parity rows remain continuity evidence for a future bounded migration milestone.

## Update note (V2-X8.1)

Feature-recognition parity for triangle prism chamfer/corner behavior is now tracked separately in `docs/development/milestones/frictionlab/v2-x8-1-triangle-chamfer-adjacency-parity-lab.md` because V2-X8 summary topology parity was not sufficient for V2-V5 production migration readiness.
