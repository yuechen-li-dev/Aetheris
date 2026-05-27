# V2-X3 — Profile-with-hole AirExtrude lab

## Purpose and scope
Lab-only experiment proving V2 thesis for through-holes: represent subtractive intent as profile void loops and emit BRep directly from profile topology (rectangle outer + circular hole loop(s)) without runtime 3D boolean subtract.

## Architecture references
- V2 doctrine: `docs/aetheris-v2-sweep-first-architecture.md`
- V2-A1 profile contract: `docs/aetheris-v2-a1-resolved-profile2d-contract.md`
- V2-X1 precursor: `docs/frictionlab/v2-x1-resolved-profile2d-lab.md`

## Original V2 thesis statement
- Hole intent represented as 2D profile voids.
- 3D topology emitted from declared profile topology.
- Candidate path does **not** call `BrepBoolean.Subtract`.

## Candidate construction
`ProfileHoleExtrudeLab` validates a `LabResolvedProfile2D` via V2-X1 validator, then builds topology directly:
- top and bottom planar cap faces each with one outer rectangular loop and N inner circular loops,
- four rectangular side faces,
- one cylindrical side face per circular hole loop,
- STEP smoke through `Step242Exporter` and marker checks.

## Test cases and results
Covered cases:
- valid centered one-hole,
- valid off-center one-hole,
- valid two-hole,
- reversed-loop orientation normalization,
- invalid: outside/touching/overlapping holes, invalid height/radius, open outer loop,
- deferred: multiple outer loops.

Observed topology contract on valid runs:
- planar faces: 6,
- cylindrical faces: equals hole count.

## STEP smoke findings
Successful runs include markers:
- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `PLANE`
- `CYLINDRICAL_SURFACE`

And exclude `BREP_WITH_VOIDS`.

## Invalid/deferred behavior
Invalid profiles are rejected before BRep emission with diagnostic `v2-x3-invalid-profile-rejected`.
Deferred profile topologies map to recommendation `profile-hole-extrude-deferred-topology`.

## Readiness
Current lab recommendation for successful valid cases: `profile-hole-extrude-ready-for-production-evaluation`.

## Non-goals
- no production AIR routing changes,
- no 2D boolean normalization,
- no sketch solver,
- no blind/counterbore/stepped holes,
- no STEP exporter changes,
- no Boolean core changes.

## Recommended next step
Proceed to production evaluation focused on emitter parity/hardening and contract migration from lab profile model to production profile contract.

## V2-V1 follow-up note
V2-V1 consumed this lab evidence by adding a bounded production-adjacent internal emitter (`ProfileHoleExtrudeEmitter`) with parity tests; executor integration remained deferred behind existing fallbacks.


## V2-V2 note
V2-V2 promotes the bounded through-hole subset from lab/adjacent status into the production through-hole seam with deterministic admissibility and fallback diagnostics.
