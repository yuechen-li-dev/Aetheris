# AIR-V5 — Frustum ruled-transition production migration

## Context and lineage
- V2 doctrine: `docs/development/milestones/general/aetheris-v2-sweep-first-architecture.md` defines bounded transition/sweep-first policy and keeps analytic admissibility explicit.
- AIR-X5 matrix: `docs/development/milestones/frictionlab/air-x5-remaining-primitives-air-matrix.md` marked cone/frustum as viable but conservative for production migration.
- AIR-X6 lab: `docs/development/milestones/frictionlab/air-x6-frustum-ruled-transition-lab.md` proved non-apex frustum parity through a ruled-transition representation with conical analytic classification.

## What changed in production
- The production cone/frustum execution route now explicitly branches by admissibility policy:
  - apex cones (`topRadius == 0` or `bottomRadius == 0`) defer to revolve,
  - equal-radius cone inputs defer to existing revolve/cylinder-like semantics,
  - positive unequal-radius frustums route through a dedicated internal ruled-transition classification seam.
- The ruled-transition seam is intentionally bounded to coaxial circular frustums only and still classifies into existing analytic conical topology/STEP outcomes.

## Admissibility boundary (AIR-V5)
- **Included in ruled-transition production route:**
  - `bottomRadius > 0`
  - `topRadius > 0`
  - `bottomRadius != topRadius`
  - `height > 0`
- **Explicitly excluded from ruled-transition production route:**
  - apex cones (singularity policy): revolve-only
  - equal-radius cylinder-like cases: remain in existing cylinder/revolve semantics

## Topology and STEP parity contract
For migrated non-apex frustums, production behavior remains parity-aligned with existing observable contracts:
- one conical side face,
- planar caps as expected,
- closed manifold BRep,
- STEP smoke markers remain present:
  - `ISO-10303-21`
  - `MANIFOLD_SOLID_BREP`
  - `ADVANCED_FACE`
  - `CONICAL_SURFACE`
  - `PLANE`
- `BREP_WITH_VOIDS` remains absent for the standalone frustum primitive.

## Non-goals (unchanged)
- no generic loft,
- no square-to-round transition,
- no NURBS/freeform expansion,
- no STEP `RULED_SURFACE` exporter work,
- no sphere/torus migration changes.

## Tests run
- Focused core/firmament/frictionlab test filters for cone/frustum/revolve/STEP and AIR ruled-transition lab continuity.

## Known limitations
- Ruled-transition production seam is intentionally narrow and internal; it does not yet generalize to mixed-profile adapters.
- Apex singularity handling remains revolve-policy bounded.

## Next recommended milestone
- Bounded square-to-round adapter lab with explicit admissibility, scoring, and topology/STEP parity gates before any production routing.

## AIR-V5.1 follow-up
- CLI external validation coverage for ruled-transition frustum routing, apex cone routing, and equal-radius cone/frustum semantics was added in `docs/development/milestones/general/air-v5-1-cli-validation.md`.
