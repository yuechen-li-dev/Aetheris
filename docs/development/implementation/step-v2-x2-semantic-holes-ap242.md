# STEP-V2-X2 semantic holes AP242 verification

STEP-V2-X2 promotes the already-supported Firmament V2 semantic hole source forms from feature-air coverage to AP242 `step-verified` fixture coverage. It does not introduce new hole syntax or new hole semantics.

## Fixtures

The MVP fixtures live under `fixtures/Hole/valid/`:

- `feature-v2-shaft-hole-through-step-verified.valid.firmfixture`
- `feature-v2-shaft-hole-blind-step-verified.valid.firmfixture`
- `feature-v2-counterbore-step-verified.valid.firmfixture`
- `feature-v2-countersink-step-verified.valid.firmfixture`

Each fixture uses a `10 x 8 x 6 mm` V2 `Box` host and a single real `modify base { hole<...> ... }` declaration from the HOLE-X4 source hook.

## Command path

The integration proof invokes the production CLI build path:

```bash
aetheris build <fixture> --out <path> --json
```

In tests this is executed through `Aetheris.CLI.CliRunner.Run(["build", fixture, "--out", stepPath, "--json"], ...)`. The build path lowers the V2 semantic hole to the existing `AirHoleFeature` / profile-stack materialization route, produces a real `BrepBody`, and exports through `Step242Exporter.ExportBody`. The fixtures do not use hardcoded STEP templates and are not trace-only artifacts.

## AP242 verification checks

For every fixture, `FirmamentV2SemanticHoleStepPipelineTests` verifies:

1. the real build command exits successfully;
2. an AP242 STEP file is emitted on disk;
3. the file contains `ISO-10303-21`, `ADVANCED_FACE`, and `VERTEX_POINT` topology markers;
4. trace-only and controlled-fixture sentinel text is absent;
5. `Step242Importer.ImportBody` reimports the emitted STEP;
6. imported topology contains faces and vertices;
7. stable semantic/topological evidence is present via imported analytic surface families;
8. exact volume analysis matches independent hand formulas.

## Volume formulas

All expected volumes are computed from fixture dimensions, not copied from implementation output:

- shaft through: `480 - pi * 1^2 * 6`
- shaft blind: `480 - pi * 1^2 * 3`
- counterbore: `480 - (pi * 1^2 * 6 + pi * (2^2 - 1^2) * 1)`
- countersink: with shaft radius `1`, entry radius `2`, included angle `90`, derived sink depth `1`: `480 - (pi * 1^2 * 6 + (pi * 1 / 3) * (2^2 + 2 * 1 + 1^2) - pi * 1^2 * 1)`

The CLI volume analyzer now recognizes this bounded class as `analytic-box-minus-z-hole`: an axis-aligned rectangular box with supported +Z semantic cylindrical, counterbore, or countersink intervals.

## Topology and semantic evidence

Exact topology counts are intentionally not part of the AP242 contract for these hole fixtures because STEP import can assign canonical IDs differently as the exporter/importer evolves. The stable evidence is instead:

- all variants: `ADVANCED_FACE > 0`, `VERTEX_POINT > 0`, reimport succeeds;
- shaft through/blind: one cylindrical wall face;
- counterbore: two cylindrical wall faces, one for the through shaft and one for the wider entry bore;
- countersink: one cylindrical shaft wall and one conical entry face.

## Relationship to MVP readiness and HOLE-X1-X4

HOLE-X1 introduced semantic `AirHoleFeature`; HOLE-X2 materialized simple shaft holes; HOLE-X3 added counterbore/countersink stack components; HOLE-X4 added the Firmament V2 source hook. STEP-V2-X2 proves that these existing semantics now travel through the real build/export/reimport path and satisfy the MVP readiness contract for the four bounded top-face hole variants above.

## Deferred

The milestone still defers standards/fit libraries, M-size tables, ISO/ASME semantics, threads/taps, drill tips, hole groups, patterns, `upToFace`, `upToNext`, arbitrary datum placement, non-planar entry faces, multi-body propagation, raw 3D boolean authoring, side-hole reroute, chamfer/fillet/draft, PMI, and DFM enforcement.
