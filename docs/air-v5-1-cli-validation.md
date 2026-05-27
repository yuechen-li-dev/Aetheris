# AIR-V5.1 CLI validation

## Purpose and scope
This milestone validates that AIR-routed production changes for box (AIR-V3), cylinder (AIR-V4), and cone/frustum routing (AIR-V5) preserve externally visible Aetheris CLI behavior.

Validation is intentionally CLI-first: build through `aetheris build`, inspect through `aetheris analyze --json`, and verify generated STEP text markers.

## CLI commands/features inspected
- `dotnet run --project Aetheris.CLI --framework net10.0 -- --help`
- `aetheris build <fixture.firmament> --out <shape.step>`
- `aetheris analyze <shape.step> --json`

Checked contracts:
- build success/failure behavior,
- summary/topology envelope (`bodyCount`, `structuralAssessment`),
- analytic surface-family reporting (`plane`, `cylinder`, `cone`),
- STEP marker presence/absence (`ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, analytic surface markers, no `BREP_WITH_VOIDS`).

## Cases validated
- Box: `10,10,10` and `12,8,6`.
- Cylinder: `r=5,h=10` and `r=3,h=12`.
- Non-apex frustum: `(5,2,10)`, `(3,1,12)`, and inverted taper `(2,5,10)`.
- Apex cones: `(5,0,10)` and `(0,5,10)`.
- Equal-radius cone/frustum: `(4,4,10)`.
- Invalid input probes: zero/negative dimensions and heights, negative radii.

## Expected marker/contracts asserted
- Boxes export planar faces and `PLANE` markers.
- Cylinders export one cylindrical family + two planar caps and include `CYLINDRICAL_SURFACE` and `PLANE`.
- Non-apex and apex cones/frusta export `CONICAL_SURFACE` with planar caps where expected.
- Equal-radius cone/frustum remains cylinder-like (`CYLINDRICAL_SURFACE`, not `CONICAL_SURFACE`).
- Valid solids include `MANIFOLD_SOLID_BREP`; all validated cases exclude `BREP_WITH_VOIDS`.

## Baseline impact
No external fixture/baseline files were regenerated. Validation coverage was added as deterministic CLI tests using ephemeral per-test temporary fixtures/STEP outputs.

## Tests run
- Focused CLI and kernel/firmament test filters for primitive and STEP behavior.
- Full repo gate via `./scripts/test-all.sh` after focused checks.

## Remaining limitations
- Validation remains external/black-box at CLI level and does not assert internal AIR route-selection traces directly.
- Assertions intentionally avoid face/edge hard-coded IDs for routed primitives; analytic-family and marker contracts are used instead.
