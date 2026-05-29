# EDGE-PRISMATIC-X5 — Prismatic section-transition artifact corpus

## 1. Purpose and scope

EDGE-PRISMATIC-X5 adds a deterministic artifact corpus for the split-preserving prismatic section-transition lane. The corpus is evidence-producing only: it writes repeatable STEP artifacts for successful first-scope prismatic transition cases and writes JSON-only rows for invalid or deferred cases.

This milestone does **not** production-route prismatic chamfer behavior. It exercises the internal/lab `PrismaticSectionTransitionEmitter` path and the existing STEP exporter as a smoke oracle, while preserving the EDGE-PRISMATIC-X4 rule that section-boundary split faces are semantic output.

## 2. References

- `docs/aetheris-v2-sweep-first-architecture.md` — V2 architecture doctrine.
- `docs/aetheris-v2-a1-resolved-profile2d-contract.md` — resolved `Profile2D` contract.
- `docs/edge-a2-constructive-chamfer-reframing-audit.md` — constructive chamfer reframing audit.
- `docs/edge-prismatic-a0-section-transition-contract-audit.md` — prismatic section-transition contract/audit.
- `docs/frictionlab/edge-prismatic-x1-section-transition-emitter-lab.md` — first lab emitter proof.
- `docs/frictionlab/edge-prismatic-x2-top-edge-chamfer-through-prismatic-emitter-lab.md` — top `+X` edge chamfer proof.
- `docs/frictionlab/edge-prismatic-x3-generic-line-profile-transition-lab.md` — generic equal-count polygon proof.
- `docs/edge-prismatic-v1-section-transition-emitter.md` — internal emitter seam.
- `docs/edge-prismatic-v2-controlled-top-edge-chamfer-route.md` — controlled top-edge chamfer route.
- `docs/edge-prismatic-x4-coplanar-split-merge-policy-audit.md` — split/merge policy; split preservation remains the default.

## 3. Corpus command

The chosen route is an experimental CLI hook:

```bash
aetheris experimental prismatic-corpus --out-dir <dir> [--json]
```

The command is implemented as a lab-only artifact writer behind the CLI. It is intentionally not a production build/analyze/chamfer route.

## 4. Artifact filenames and path convention

The command writes all artifacts under the supplied output directory. Successful cases write STEP files plus the shared JSON summary. Invalid or deferred cases write only JSON rows.

Successful STEP filenames:

- `edge-prismatic-x5-rectangle-inset.step`
- `edge-prismatic-x5-top-edge-chamfer.step`
- `edge-prismatic-x5-pentagon-scaled.step`
- `edge-prismatic-x5-hexagon-scaled.step`
- `edge-prismatic-x5-pentagon-asymmetric.step`

Shared JSON summary filename:

- `edge-prismatic-x5-corpus.json`

## 5. Corpus case list

Successful artifact cases:

1. `rectangle-inset` — rectangle to centered inset rectangle.
2. `top-edge-chamfer` — controlled top `+X` edge chamfer represented as a three-section prismatic transition.
3. `pentagon-scaled` — regular pentagon to scaled regular pentagon.
4. `hexagon-scaled` — regular hexagon to scaled regular hexagon.
5. `pentagon-asymmetric` — translated asymmetric pentagon from the X3 stable case.

JSON-only diagnostic cases:

- `mismatched-vertex-count` — rejected.
- `non-increasing-sections` — rejected.
- `invalid-self-intersecting-profile` — rejected.
- `holes-deferred` — deferred.
- `arcs-deferred` — deferred.
- `multiple-loops-deferred` — deferred.
- `missing-correspondence` — rejected.
- `non-identity-correspondence` — rejected until correspondence hardening exists.

## 6. JSON summary schema

The JSON summary contains:

- `milestone`: `EDGE-PRISMATIC-X5`.
- `outputDirectory` and `summaryPath`.
- `route`: `experimental`.
- `transitionRoute`: `prismatic-section-transition`.
- `emitterComponentName`: `PrismaticSectionTransitionEmitter`.
- `splitPolicy`: `preserve-section-splits`.
- `cases`: one row per corpus case.
- root `diagnostics` and root `errors`.
- root `guarantees`.

Each case row contains:

- `caseName`.
- `status`: `succeeded`, `rejected`, `deferred`, or `failed`.
- `artifactPath` and `artifactFileName` when a STEP file is written; `null` for JSON-only diagnostic rows.
- `route`, `transitionRoute`, `emitterComponentName`, and `splitPolicy`.
- `topologySummary` with section, vertex, edge, face, planar-face, cylindrical-face, transition-face, cap-face, loop, coedge, and bounds fields. The top-edge chamfer row also records lower prism side faces and chamfer transition faces.
- `stepMarkerSummary` for successful STEP rows, including required-present and forbidden-absent marker checks.
- deterministic `diagnostics`.
- case-local `errors`.
- case-local `guarantees`.

## 7. Split-preserving topology contracts

The corpus asserts the current split-preserving topology, not a future merged topology.

### Rectangle to inset rectangle

- section count = `2`
- vertices = `8`
- edges = `12`
- faces = `6`
- planar faces = `6`
- cylindrical faces = `0`
- transition faces = `4`
- cap faces = `2`
- loops = `6`
- coedges = `24`

### Top `+X` edge chamfer

- section count = `3`
- vertices = `12`
- edges = `20`
- faces = `10`
- planar faces = `10`
- cylindrical faces = `0`
- lower prism side faces = `4`
- transition faces = `4`
- chamfer transition faces = `1`
- cap faces = `2`
- loops = `10`
- coedges = `40`

### Pentagon to scaled pentagon

- section count = `2`
- vertices = `10`
- edges = `15`
- faces = `7`
- planar faces = `7`
- cylindrical faces = `0`
- transition faces = `5`
- cap faces = `2`
- loops = `7`
- coedges = `30`

### Hexagon to scaled hexagon

- section count = `2`
- vertices = `12`
- edges = `18`
- faces = `8`
- planar faces = `8`
- cylindrical faces = `0`
- transition faces = `6`
- cap faces = `2`
- loops = `8`
- coedges = `36`

### Asymmetric pentagon

The asymmetric pentagon follows the same `n = 5` formula as the scaled pentagon: `10` vertices, `15` edges, `7` faces, `5` transition faces, `2` cap faces, `7` loops, and `30` coedges.

## 8. STEP marker expectations

Successful STEP artifacts must contain:

- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `PLANE`

Successful STEP artifacts must not contain:

- `CYLINDRICAL_SURFACE`
- `BREP_WITH_VOIDS`

These are smoke checks only. EDGE-PRISMATIC-X5 does not change STEP exporter/importer behavior.

## 9. Invalid/deferred behavior

Invalid and deferred rows are machine-checkable JSON diagnostics. They do not write STEP files, even if an output directory exists. Rejected rows prove bounded validation failure. Deferred rows preserve explicit future-work boundaries for holes, arcs, and multiple loops.

## 10. Guarantees

The X5 corpus route guarantees:

- no production route replacement;
- no AirEdgeSweep use;
- no BrepBoundedChamfer use;
- no topology graft/body mutation;
- no 3D Boolean fallback;
- no coplanar merge;
- no trim engine, sketch solver, clipping engine, NURBS/freeform support, or triangle migration retry.

## 11. Non-goals

EDGE-PRISMATIC-X5 intentionally does not add:

- production routing;
- merge mode or coplanar simplification;
- STEP exporter/importer changes;
- Boolean core changes;
- current `ProfileStackExtrudeExecutor` behavior changes;
- AirEdgeSweep behavior changes;
- BrepBoundedChamfer behavior changes;
- public API changes.

## 12. Tests run

Focused validation for this milestone:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- --help
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental prismatic-corpus --out-dir "$tmp/edge-prismatic-x5" --json
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "Prismatic|AirChamfer|Experimental|Lab|Step|CliBaseline|Export|Corpus"
dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "PrismaticSectionTransition|PrismaticTopEdgeChamfer|ProfileStackChamfer|ProfileChamfer|ProfileStack|LineArcProfileExtrude|Profile2D|AirChamfer|EdgeSweep|CIRLab"
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "Chamfer|Fillet|Corner|BrepPrimitives|BrepExtrude|Step242|Primitive|Extrude|Boolean|SafeComposition"
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "Chamfer|Fillet|Corner|ProfileStack|LineArcProfileExtrude|FirmamentPrimitive|FirmamentStepExporter|SemanticRecovery|FrepMaterializer|Rematerialize"
```

## 13. Recommended next milestone

Recommended next milestone: **EDGE-PRISMATIC-X6 gated corpus stability check**. That gate should run repeated corpus generations, compare JSON-significant fields and STEP hashes, and keep the split-preserving artifact contract stable before any production-route discussion resumes.

Alternative future milestones remain valid after X6:

- **EDGE-PRISMATIC-X7 optional coplanar merge proof lab**, if a gated simplification lane is desired.
- Return to chamfer/fillet production-route work after the corpus is stable and route admission criteria are explicit.

## 14. EDGE-PRISMATIC-X6 gated stability/analyzer note

EDGE-PRISMATIC-X6 adds an explicitly gated/manual stability and analyzer confirmation check for this X5 corpus. See `docs/frictionlab/edge-prismatic-x6-corpus-stability-and-analyzer-confirmation.md` for the `Category=ArtifactCorpus` trait, `AETHERIS_RUN_ARTIFACT_CORPUS_TESTS=1` guard, `PrismaticCorpusStability` filter, repeated-run JSON/STEP hash/normalized-summary comparisons, `analyze section` confirmation, and current `analyze map` primitive-raycast integration blocker. The X6 gate is not part of normal unit-test execution unless explicitly requested, and it does not change production chamfer/fillet behavior, production route authority, ProfileStack behavior, STEP exporter/importer behavior, Boolean core behavior, AirEdgeSweep behavior, or coplanar merge policy.
