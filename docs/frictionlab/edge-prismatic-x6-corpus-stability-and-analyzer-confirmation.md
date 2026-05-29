# EDGE-PRISMATIC-X6 corpus stability and analyzer confirmation

## 1. Purpose and scope

EDGE-PRISMATIC-X6 adds an explicitly gated/manual stability check for the EDGE-PRISMATIC-X5 split-preserving prismatic artifact corpus. The gate exists to answer two repeatability questions before any broader production-route discussion resumes:

1. Does the corpus produce the same stable JSON/topology/marker/diagnostic projection across repeated runs?
2. Can existing CLI analyzer commands consume selected successful STEP artifacts in a bounded, deterministic way?

The check is intentionally test-only and lab-only. It does not change production chamfer/fillet behavior, production route selection, `ProfileStackExtrudeExecutor`, STEP exporter/importer behavior, Boolean core behavior, AirEdgeSweep behavior, `BrepBoundedChamfer`, coplanar merge policy, triangle migration, sketch solving, clipping, or NURBS/freeform support.

## 2. Relationship to EDGE-PRISMATIC-X5

EDGE-PRISMATIC-X5 introduced:

```bash
aetheris experimental prismatic-corpus --out-dir <dir> [--json]
```

That command writes split-preserving prismatic STEP artifacts and an `edge-prismatic-x5-corpus.json` summary. X6 does not replace or broaden the X5 route. Instead, X6 repeatedly invokes the existing X5 route in temporary directories and compares stable projections from each run.

Manual corpus generation remains:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental prismatic-corpus --out-dir "$tmp/edge-prismatic-x5" --json
```

## 3. Gating mechanism

The X6 stability/analyzer test lives in `Aetheris.CLI.Tests` and is explicitly gated by all of the following:

- xUnit trait/category: `Category=ArtifactCorpus`
- test-name filter target: `PrismaticCorpusStability`
- environment guard: `AETHERIS_RUN_ARTIFACT_CORPUS_TESTS=1`

Without the environment variable, the test returns successfully after writing a clear no-op message. The heavy corpus generation and analyzer body only runs when the environment variable is set.

Exact command for the no-op gate check:

```bash
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "PrismaticCorpusStability"
```

Exact command for the explicit stability/analyzer run:

```bash
AETHERIS_RUN_ARTIFACT_CORPUS_TESTS=1 dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "PrismaticCorpusStability"
```

The broader artifact-corpus category can also be run explicitly with:

```bash
AETHERIS_RUN_ARTIFACT_CORPUS_TESTS=1 dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "Category=ArtifactCorpus"
```

## 4. Corpus cases compared

The stable projection includes all X5 corpus rows:

Successful STEP-producing cases:

- `rectangle-inset`
- `top-edge-chamfer`
- `pentagon-scaled`
- `hexagon-scaled`
- `pentagon-asymmetric`

JSON-only rejected/deferred cases:

- `mismatched-vertex-count`
- `non-increasing-sections`
- `invalid-self-intersecting-profile`
- `holes-deferred`
- `arcs-deferred`
- `multiple-loops-deferred`
- `missing-correspondence`
- `non-identity-correspondence`

## 5. JSON fields compared

The stability projection intentionally removes run-local paths such as output directory and artifact path, then compares these stable fields:

- milestone (`EDGE-PRISMATIC-X5`)
- corpus version, if/when the corpus summary emits one
- route (`experimental`)
- transition route (`prismatic-section-transition`)
- emitter component name (`PrismaticSectionTransitionEmitter`)
- split policy (`preserve-section-splits`)
- sorted case names
- case statuses
- artifact filenames
- per-case route/emitter/split-policy fields
- topology summary JSON for successful cases
- STEP marker summary JSON for successful cases
- per-case diagnostics and errors
- top-level diagnostics and errors
- guarantee booleans:
  - no production route replacement
  - no AirEdgeSweep
  - no BrepBoundedChamfer
  - no topology graft/body mutation
  - no 3D Boolean
  - no coplanar merge

The test also writes X6-scoped diagnostics to test output, including repeated-run completion, JSON stability success, STEP hash stability success, normalized STEP stability success, analyzer section stability success, analyzer map stability comparison success, and the no-production/no-legacy-route guarantee confirmations.

## 6. STEP comparison mode

Current X5 STEP artifacts are deterministic for the selected corpus rows, so X6 compares raw SHA256 hashes for every successful artifact file:

- `edge-prismatic-x5-rectangle-inset.step`
- `edge-prismatic-x5-top-edge-chamfer.step`
- `edge-prismatic-x5-pentagon-scaled.step`
- `edge-prismatic-x5-hexagon-scaled.step`
- `edge-prismatic-x5-pentagon-asymmetric.step`

X6 also compares a normalized STEP-adjacent summary for each successful artifact: case status, artifact filename, topology summary, and marker summary. The normalized comparison remains useful if a future exporter change introduces benign run-specific text instability, but this milestone does **not** change the STEP exporter to chase hash stability.

## 7. Analyzer commands used

X6 runs existing analyzer commands against selected generated STEP artifacts. The test calls the in-process CLI runner, equivalent to these manual commands:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- analyze section <artifact.step> --xy --offset 0.5 --json
```

for:

- `edge-prismatic-x5-rectangle-inset.step`
- `edge-prismatic-x5-hexagon-scaled.step`

and:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- analyze section <edge-prismatic-x5-top-edge-chamfer.step> --xy --offset 5.5 --json
```

for the top-edge chamfer transition interval.

The map analyzer is invoked with:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- analyze map <artifact.step> --top --rows 16 --cols 16 --json
```

for:

- `edge-prismatic-x5-top-edge-chamfer.step`
- `edge-prismatic-x5-hexagon-scaled.step`

## 8. Analyzer assertions and current map limitation

`analyze section` succeeds for the selected prismatic artifacts. X6 compares a path-free projection containing:

- plane family;
- offset;
- offset axis;
- section axes;
- 3D bounding box;
- loop count;
- closed-loop count;
- line/arc/unsupported segment counts;
- section 2D bounding box.

The section assertions require non-empty closed line-loop geometry and zero unsupported section segments, then compare the stable projection across both corpus runs.

`analyze map` is also invoked, but current orthographic map analysis cannot raycast these imported/generated prismatic BReps. The stable JSON failure is treated as a bounded analyzer integration blocker rather than a corpus failure. The compared projection records the deterministic failure kind/message:

- `success = false`
- `errorKind = analysis-failure`
- message beginning with `Orthographic map v1 currently supports bodies accepted by BrepSpatialQueries.Raycast`

This documents that richer map support for non-primitive imported/generated prismatic bodies is a future analyzer milestone. X6 does not broaden production geometry, Boolean, STEP, or raycast behavior to make the map pass.

## 9. Why this does not run by default

The test writes real artifact directories, hashes STEP files, and runs analyzer commands. It is intended for manual milestone validation and future diff investigations, not for normal unit-test latency. The default/no-env behavior is therefore a no-op pass with a clear diagnostic message.

## 10. Non-goals

EDGE-PRISMATIC-X6 does not add or change:

- production chamfer/fillet behavior;
- production route replacement;
- default production routing;
- current `ProfileStackExtrudeExecutor` behavior;
- STEP exporter/importer behavior;
- Boolean core behavior;
- AirEdgeSweep behavior;
- `BrepBoundedChamfer` behavior;
- topology graft/body mutation;
- 3D Boolean fallback;
- coplanar merge mode;
- arbitrary edge selection;
- triangle migration;
- sketch solver or clipping engine behavior;
- NURBS/freeform support.

## 11. Tests run for this milestone

Focused X6 validation:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- --help
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental prismatic-corpus --out-dir "$tmp/edge-prismatic-x5" --json
dotnet run --project Aetheris.CLI --framework net10.0 -- analyze section "$tmp/edge-prismatic-x5/edge-prismatic-x5-rectangle-inset.step" --xy --offset 0.5 --json
dotnet run --project Aetheris.CLI --framework net10.0 -- analyze section "$tmp/edge-prismatic-x5/edge-prismatic-x5-top-edge-chamfer.step" --xy --offset 5.5 --json
dotnet run --project Aetheris.CLI --framework net10.0 -- analyze map "$tmp/edge-prismatic-x5/edge-prismatic-x5-top-edge-chamfer.step" --top --rows 16 --cols 16 --json
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "PrismaticCorpusStability"
AETHERIS_RUN_ARTIFACT_CORPUS_TESTS=1 dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "PrismaticCorpusStability"
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "Prismatic|AirChamfer|Experimental|Lab|Step|CliBaseline|Export|Corpus"
dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "PrismaticSectionTransition|PrismaticTopEdgeChamfer|ProfileStackChamfer|ProfileChamfer|ProfileStack|LineArcProfileExtrude|Profile2D|AirChamfer|EdgeSweep|CIRLab"
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "Chamfer|Fillet|Corner|BrepPrimitives|BrepExtrude|Step242|Primitive|Extrude|Boolean|SafeComposition"
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "Chamfer|Fillet|Corner|ProfileStack|LineArcProfileExtrude|FirmamentPrimitive|FirmamentStepExporter|SemanticRecovery|FrepMaterializer|Rematerialize"
```

## 12. Next recommended milestone

Recommended next milestone: **EDGE-PRISMATIC-X7 analyzer map support audit for generated/imported prismatic BReps**. The bounded blocker isolated by X6 is that `analyze section` can confirm non-empty deterministic sections for the prismatic STEP artifacts, while `analyze map` currently reports the known primitive-raycast limitation. X7 should decide whether to extend analyzer/raycast support in a lab route or keep map confirmation deferred until a broader analyzer architecture milestone.

X7 is now recorded in `docs/edge-prismatic-x7-analyze-map-cir-frep-audit.md`. Its conclusion is that map analysis should move toward hybrid representation dispatch: use CIR/FRep/tape evaluation for generated AIR bodies only when an admitted mirror exists, retain the current BRep raycast path for bodies accepted by `BrepSpatialQueries.Raycast`, and keep deterministic unsupported diagnostics for STEP/imported prismatic bodies until either a CIR mirror or broader raycast support is deliberately admitted. X7 makes no implementation, production, STEP, Boolean, topology, prismatic-emitter, AirEdgeSweep, analyzer-behavior, or gated-test-default changes.

Optional later milestones remain:

- optional coplanar merge proof lab, still gated and still not a default route;
- controlled production-adjacent route admission only after stability, analyzer, recognition, and fallback authority evidence are sufficient;
- return to chamfer/fillet production-route hardening without changing the split-preserving prismatic contract.

## 11. EDGE-PRISMATIC-X8 generated-source map follow-up

EDGE-PRISMATIC-X8 addresses the X6 `analyze map` limitation only for generated prismatic sources that still have admissible AIR/prismatic section data. The X8 lab dispatcher builds an admitted `CirConvexPolyhedronMirror` for `rectangle-inset` and `top-edge-chamfer` and produces deterministic CIR occupancy/thickness summaries. It does not change imported STEP-only artifact behavior: X5/X6 corpus STEP files still do not cause mirror inference, and normal production `aetheris analyze map` remains unchanged.
