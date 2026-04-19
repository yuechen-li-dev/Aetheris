# INV-B0 — suspicious blind-hole → enclosed-void diagnosis

Date: 2026-04-19 (UTC)

## Outcome

**Outcome A — resolved diagnosis.**

The FRIC-B0 suspicious `zone_b_blind_cyl` result is a **fixture/setup bug**, not a kernel blind-hole subtract bug and not an enclosed-void checker bug.

## 1) Exact suspicious case

- Fixture: `testdata/firmament/frictionlab/fric-b0/chimera_a_zone_b_blind_cyl.firmament`
- Operation: `subtract(box, cylinder)`
- Diagnostic observed under process `default`:
  - `[FIRM-SCHEMA-0006] ... fully enclosed internal void(s) ... not allowed for process 'default'`

## 2) Fixture geometry intent vs actual geometry

The fixture labels the operation as `zone_b_blind_cyl`, but it does **not** place the cylinder on an exterior face.

Source values:

- root box: `size[3]: [60, 40, 20]`
- subtract tool: cylinder `radius: 4`, `height: 8`
- no `place` block on the subtract op

Execution path facts:

- Booleans with `placement == null` resolve to zero translation.
- In that case, subtract executes with the raw primitive tool body.
- Core primitive constructors (`CreateBox`, `CreateCylinder`) are centered at the origin.

Therefore, this fixture produces:

- box extents: `x ±30`, `y ±20`, `z ±10`
- cylinder extents: `z ±4`

So the cylinder is fully internal, and subtracting it forms a sealed cavity (enclosed void), not an exterior-opening blind hole.

## 3) Built artifact inspection

Because default process rejects enclosed voids, the suspicious fixture cannot export STEP in default mode.
To inspect actual resulting topology, the same geometry was rebuilt with a temporary additive schema (no code changes).

Observed outputs:

- additive rebuild succeeds and exports STEP;
- resulting STEP uses `BREP_WITH_VOIDS` with an outer closed shell plus an oriented inner closed shell;
- the same file fails CLI import/analyze with `Missing MANIFOLD_SOLID_BREP root entity`, confirming current analyzer only imports manifold-root forms, not `BREP_WITH_VOIDS` roots.

As a baseline comparison:

- `chimera_a_box_root_baseline.firmament` (through-hole) exports `MANIFOLD_SOLID_BREP` and is analyzable;
- `Aetheris.Firmament.FrictionLab/Cases/blind-hole-mount-block/part.firmament` (explicit `place.on_face`) succeeds under default and is analyzable as a single-shell part.

## 4) Boolean result vs enclosed-void checker

Checker behavior is consistent with implementation:

- `FirmamentSchemaEnclosedVoidValidator` runs for non-additive/default processes.
- It calls `BrepEnclosedVoidAnalyzer.Analyze(...)` on the final body.
- Analyzer reports enclosed voids from `body.ShellRepresentation.InnerShellIds`.

Given the suspicious fixture constructs an actual inner shell, checker classification is expected and correct.

## 5) Final diagnosis

**Fixture/setup bug.**

The FRIC-B0 `zone_b_blind_cyl` case was treated as a blind hole in reporting, but its parameters/model-space placement produce a fully enclosed cavity.

No kernel blind-hole subtraction defect was required to explain the result.
No enclosed-void checker defect was required to explain the result.

## 6) Recommended follow-up

1. Reclassify FRIC-B0 `zone_b_blind_cyl` from “blind-hole” to “contained cavity” in pressure-test reporting.
2. Add a true exterior-opening blind-hole fixture variant for FRIC-B0 using explicit `place.on_face` semantics.
3. Keep `FIRM-SCHEMA-0006` expectation for the current fixture under `default` as a guardrail.
4. Optional tooling follow-up: extend `aetheris analyze` importer path to accept `BREP_WITH_VOIDS` roots so enclosed-void artifacts can be inspected directly via CLI.
