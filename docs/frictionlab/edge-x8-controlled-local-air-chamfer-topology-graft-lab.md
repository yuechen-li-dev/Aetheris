# EDGE-X8 Controlled local AirChamfer topology graft lab

## Purpose and scope
EDGE-X8 is a **lab-only** continuation of EDGE-X7 that attempts a controlled local topology graft for one deterministic convex planar single-edge case. It does not change production routes, public APIs, STEP import/export behavior, Boolean core behavior, or fillet/chamfer production behavior.

## References
- `docs/edge-a0-air-edge-sweep-fillet-chamfer-audit.md`
- `docs/aetheris-v2-sweep-first-architecture.md`
- `docs/v2-a3-legacy-topology-contracts-and-parallel-emitter-lanes.md`
- EDGE-X3 through EDGE-X7 friction labs
- `docs/edge-v1-convex-planar-air-chamfer-prototype.md`

## Why this follows EDGE-X7
EDGE-X7 proved deterministic controlled body selection and EDGE-V1 invocation, but deliberately deferred body mutation (`body-mutation-not-implemented;using-closed-witness-artifact`). EDGE-X8 takes the same bounded setup and performs a synthetic local replacement experiment.

## Controlled body model
The lab mirrors EDGE-X7 controlled body setup:
- deterministic box body fixture,
- deterministic selected convex target edge,
- deterministic adjacent planar face pair (orthogonal and non-orthogonal safe variant).

## Target edge selection
The target edge remains deterministic: `(5,4,-3)` to `(5,4,3)` for valid cases. Invalid fixture sets identical endpoints.

## EDGE-V1 invocation
EDGE-X8 builds `AirChamferConvexPlanarPrototypeRequest` and invokes `AirChamferConvexPlanarPrototype` before attempting graft.

## Graft operation model
For accepted bounded cases the lab records these machine-checkable operations:
1. original edge marked for replacement,
2. two adjacent planar faces marked trimmed to offset boundaries,
3. chamfer face inserted,
4. two transition edges inserted.

No general BRep editor is introduced; this is controlled synthetic grafting only.

## Candidate result and topology contract
For the canonical controlled case, the candidate contract is explicit and asserted:
- `faceCount = 6`
- `planarFaceCount = 6`
- `edgeCount = 12`
- `vertexCount = 8`
- `chamferFaceCount = 1`
- `trimmedAdjacentFaceCount = 2`
- `transitionEdgeCount = 2`

## Orientation and STEP smoke
When candidate body is created, lab requires:
- topology validation diagnostic,
- orientation validation diagnostic,
- STEP smoke markers:
  - contains `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `PLANE`
  - excludes `CYLINDRICAL_SURFACE`, `BREP_WITH_VOIDS`

## Invalid/deferred cases
Before graft attempt, fixtures are rejected/deferred for:
- invalid distance,
- invalid target edge,
- missing adjacent face,
- non-planar adjacent marker,
- edge chain,
- corner chain,
- legacy-dependent triangle/chamfer dependency.

## Guarantees
- Legacy `BrepBoundedChamfer` remains authoritative.
- No production route replacement.
- No 3D Boolean usage.

## Non-goals
- No production behavior changes.
- No arbitrary body mutation system.
- No legacy chamfer route replacement.
- No fillet geometry.
- No edge/corner chains.
- No STEP exporter/importer changes.
- No Boolean core changes.
- No sketch solver/clipping/NURBS/freeform scope.

## Recommended next milestone
- If stable: EDGE-V2 production-adjacent real-body AirChamfer prototype.
- If blockers remain: EDGE-X8.1 topology graft hardening.


## EDGE-V2 packaging note
EDGE-V2 now packages this controlled graft pipeline into `AirChamferRealBodyPrototype`, preserving non-authoritative status and unchanged production routes.

EDGE-X9 now consumes EDGE-V2 candidate outputs to validate feature-recognition/adjacency parity for controlled cases before any shadow-route experiment.
