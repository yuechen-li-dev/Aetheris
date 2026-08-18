# EDGE-X7 Controlled real-body AirChamfer probe lab

## Purpose and scope
EDGE-X7 adds a **lab-only** controlled real-body probe that selects one convex planar edge from a deterministic synthetic body, invokes `AirChamferConvexPlanarPrototype` (EDGE-V1), and emits a non-authoritative replacement-body artifact with deterministic diagnostics.

This milestone keeps legacy `BrepBoundedChamfer` authoritative and does not change production chamfer routing.

## References
- `docs/development/milestones/general/edge-a0-air-edge-sweep-fillet-chamfer-audit.md`
- `docs/development/milestones/general/edge-v1-convex-planar-air-chamfer-prototype.md`
- `docs/development/milestones/frictionlab/edge-x6-convex-planar-air-chamfer-closed-witness-lab.md`

## Why this follows EDGE-X6
EDGE-X6 validated closed synthetic witness emission and STEP smoke. EDGE-X7 extends confidence by adding controlled body creation plus deterministic target-edge/adjacent-face selection before invoking EDGE-V1.

## Controlled body model and selection
The probe constructs a deterministic box-like body and selects one explicit convex edge (`(5,4,-3)->(5,4,3)`) with two adjacent planar face normals.

A non-orthogonal variant uses a normalized `(1,1,0)` second normal to exercise existing EDGE-V1 non-orthogonal support.

## Prototype invocation and replacement artifact
The lab converts selection into `AirChamferConvexPlanarPrototypeRequest` and invokes EDGE-V1.

Current EDGE-X7 outcome is **artifact-first**:
- topology plan + geometry artifact + optional closed witness are consumed,
- candidate replacement body mutation is deferred with blocker:
  - `body-mutation-not-implemented;using-closed-witness-artifact`.

## Topology and STEP findings
For accepted controlled cases, the emitted closed witness satisfies smoke markers:
- includes `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `PLANE`,
- excludes `CYLINDRICAL_SURFACE`, `BREP_WITH_VOIDS`.

The probe records deterministic expected topology contract from the witness summary (vertex/edge/face/planar-face counts).

## Invalid/deferred cases
The lab includes deterministic rejected/deferred fixtures for invalid distance, invalid edge, missing adjacent face, non-planar marker, edge-chain, corner-chain, and legacy-dependent fallback.

## Guarantees
- Legacy authority preserved.
- No production route replacement.
- No 3D Boolean usage.
- No production chamfer/fillet behavior changes.
- No STEP exporter/importer changes.
- No Boolean core changes.

## Non-goals
- Production behavior and mutation.
- Replacing legacy chamfer.
- Fillet support.
- Chain/corner implementation.
- STEP or Boolean architecture changes.

## Recommended next milestone
- EDGE-X8 body-mutation hardening to transition from replacement artifact to controlled replacement body construction.


## EDGE-X8 follow-on
EDGE-X8 extends this probe with a lab-only controlled local topology graft attempt that tries to move from witness-only artifacts to a synthetic candidate mutated body under the same bounded convex planar single-edge constraints.
