# AIR-X7 — Firmament fixture trace corpus

## Purpose and scope

AIR-X7 introduces source-level Firmament fixture trace inputs for `aetheris trace`. A `.firmfixture` is a language/lowering contract fixture: valid fixtures describe accepted Firmament feature programs, invalid fixtures describe rejected or deferred programs, and trace reports record the lowering stage reached and why.

AIR-X7 extends AIR-X6 without changing production geometry, route behavior, STEP import/export, BRep topology, CIR evaluator/tape behavior, Boolean behavior, or Firmament grammar.

## Relationship to AIR-X6 trace

AIR-X6 added built-in trace cases. AIR-X7 keeps those cases and adds fixture input:

```bash
aetheris trace --fixture fixtures/Firmament/Chamfer/valid/top-face-loop-chamfer.valid.firmfixture
aetheris trace --fixture fixtures/Firmament/Chamfer/valid/top-face-loop-chamfer.valid.firmfixture --json
aetheris trace --fixture fixtures/Firmament/Chamfer/valid/top-face-loop-chamfer.valid.firmfixture --out-dir artifacts/air-x7
aetheris trace --fixture fixtures/Firmament/Chamfer/valid/top-face-loop-chamfer.valid.firmfixture --out-dir artifacts/air-x7 --json
```

Text output remains the default. JSON is emitted only when `--json` is present. `--case` and `--fixture` are mutually exclusive, and one of them is required.

## Trace vs analyze

`aetheris trace` is the compiler lowering flight recorder for AIR/Firmament-derived inputs. `aetheris analyze` is geometric forensics for existing STEP/BRep artifacts. AIR-X7 deliberately does not accept arbitrary STEP files in `trace`; STEP remains the job of `analyze`.

## Fixture convention and directory structure

AIR-X7 adds two source fixture extensions:

- `*.valid.firmfixture` for accepted source/lowering contracts.
- `*.invalid.firmfixture` for expected rejection or deferral contracts.

The first corpus is committed under:

```text
fixtures/Firmament/Chamfer/
  valid/top-face-loop-chamfer.valid.firmfixture
  invalid/arbitrary-graph-chamfer.invalid.firmfixture
  invalid/non-uniform-loop-chamfer.invalid.firmfixture
  invalid/loop-fillet-deferred.invalid.firmfixture
```

## Fixture metadata format

A fixture may start with leading `// key: value` metadata comments. AIR-X7 recognizes:

- `case`
- `expected`
- `expected-stage`
- `expected-route`
- `expected-reason`
- `description`

The extension is the source of truth for valid/invalid expectation. If `// expected:` conflicts with the extension, the loader rejects the fixture.

## Fixture source text role

The current first-scope implementation treats fixture bodies as Firmament-like language-contract text plus metadata-driven trace mapping. It does **not** expand production Firmament syntax to make these examples parse. As Firmament syntax matures, these same `.firmfixture` files should become parser-backed frontend/lowering fixtures.

## Lowering stage model

Trace reports include fixture fields such as `inputKind = firmfixture`, fixture path, expectation, case name, expected stage, actual stage reached, expected route/reason, expectation satisfaction, and deterministic fixture diagnostics.

The stage vocabulary includes `fixture-loaded`, `parsed`, `bound`, `feature-air`, `route-selection`, `constructive-air`, `brep-plan`, `emitted-brep`, `step-smoke`, `cir-mirror`, `rejected`, `deferred`, and `unsupported`. AIR-X7 derives the first corpus stages from fixture metadata and existing AIR-X6/AIR-X2 trace results.

## Valid fixture behavior

`top-face-loop-chamfer.valid.firmfixture` maps to the existing top-face loop chamfer trace. It reaches `cir-mirror`, selects `TopFaceLoopChamferPrismatic` by `SwitchMatch`, reports AIR node `TopFaceLoopChamfer`, selection class `FaceBoundaryLoop`, rule `UniformChamfer`, and BRepPlan chamfer face count `4`.

## Invalid and deferred fixture behavior

Invalid fixtures succeed at the CLI level when the expected invalid/deferred behavior is explained deterministically. They stop at route selection/rejected/deferred stages and do not emit geometry, BRepPlan topology, STEP smoke, or CIR mirror success.

The first invalid corpus maps to existing AIR-X2 admissibility diagnostics:

- arbitrary graph chamfer: `arbitrary-graph-unsupported`;
- non-uniform loop chamfer: `non-uniform-rule-unsupported`;
- loop fillet: `loop-fillet-deferred-until-single-edge-fillet-evidence`.

CLI misuse, missing fixture files, invalid extensions, unknown fixture cases, and expectation mismatches return nonzero.

## Golden report policy

AIR-X7 keeps JSON deterministic enough for normalized comparison in tests. Text is human/LLM-facing, so tests assert key sections and phrases rather than exact full text. Stable generated golden report files are deferred; the committed source `.firmfixture` corpus is the authoritative first artifact.

## Non-goals

AIR-X7 does not change production Firmament syntax or behavior, production routes, route-selection/JudgmentUtility behavior, geometry implementation, BRepPlan semantics, CIR evaluator/tape behavior, STEP exporter/importer behavior, BRep topology behavior, production analyzer behavior, Boolean behavior, AirEdgeSweep behavior, BrepBoundedChamfer/BrepBoundedFillet behavior, chamfer/fillet/shell geometry, arbitrary graph support, import/recovery, triangle migration, or NURBS/freeform behavior.

## Tests run

- `dotnet run --project Aetheris.CLI -f net10.0 -- --help`
- `dotnet run --project Aetheris.CLI -f net10.0 -- trace --help`
- `dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/Firmament/Chamfer/valid/top-face-loop-chamfer.valid.firmfixture`
- `dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/Firmament/Chamfer/valid/top-face-loop-chamfer.valid.firmfixture --json`
- `dotnet run --project Aetheris.CLI -f net10.0 -- trace --fixture fixtures/Firmament/Chamfer/invalid/arbitrary-graph-chamfer.invalid.firmfixture`
- Focused .NET CLI/core/Firmament/FrictionLab test filters for Trace/Fixture/AIR/CIR/BRepPlan/etc.

## Recommended next milestone

Recommended AIR-X8: **Firmament parser-backed trace fixture for one real source form**. AIR-X7 found that metadata-driven fixture contracts are sufficient for stable trace semantics, but the next convergence blocker is tying at least one `.firmfixture` body to real frontend parsing without prematurely expanding unsupported language forms.

## AIR-X8 parser-backed fixture mode

AIR-X8 adds opt-in parser-backed fixture mode with `// parser-backed: true`. Metadata-driven AIR-X7 fixtures remain the default. Parser-backed fixtures extract the non-metadata body, invoke the existing Firmament frontend, report parse success/failure and `frontendStageReached`, and satisfy the fixture expectation from the truthful frontend stage reached.

The first parser-backed fixture is `fixtures/Firmament/Primitive/valid/box.valid.firmfixture`. It reaches `parsed` and records the explicit AIR-X8 boundary diagnostic that AIR lowering is not wired for parser-backed fixtures yet.

## AIR-X9 parser-backed primitive stage

The parser-backed primitive fixture `fixtures/Firmament/Primitive/valid/box.valid.firmfixture` now reaches `feature-air` through the real Firmament parser and a narrow trace summary adapter. The AIR-X7 Chamfer fixtures remain metadata-driven and continue to use their existing route-selection/lowering trace mappings.

## AIR-X10 parser-backed fixture advancement

The primitive parser-backed fixture `fixtures/Firmament/Primitive/valid/box.valid.firmfixture` now expects and reaches `constructive-air`. It remains parser-backed while the Chamfer corpus remains metadata-driven. The box trace reports Feature AIR `CreateBox`, Constructive AIR `AirProfileExtrude`, canonical form `rectangle-profile-extrude`, and deferred BRepPlan/emission/CIR status.


## AIR-X11 corpus note

The parser-backed primitive box fixture advances to existing profile extrusion emission evidence with expected stage `emitted-brep`. The AIR-X7 chamfer fixtures remain metadata-driven and continue to validate their previous valid/deferred/rejected paths.

## AIR-REGION-X1 status note

AIR-REGION-X1 adds a trace-only AIR Region skeleton: parser-backed box fixtures report a `RootRegion`, region fixtures can report metadata-driven `FaceAttachedRegion` yields with deferred integration, and no Boolean, geometry emission, production route replacement, grammar expansion, BRepPlan semantics, or CIR behavior is changed. See `docs/air-region-x1-region-model-skeleton-trace-fixtures.md`.

## AIR-REGION-X2 side-hole fixture note

The region fixture corpus now uses `fixtures/Firmament/Region/valid/side-hole-face-attached-region.valid.firmfixture` to validate a side-hole `FaceAttachedRegion` yield contract. Metadata records the expected side-hole feature, circle profile, radius, attachment face, direction, boundary kind, and affected scope without requiring production Firmament grammar support.
