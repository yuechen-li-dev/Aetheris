# FORGE-CS-A3 runtime concept validation

Milestone: **FORGE-CS-A3**

Phase 2 doctrine remains:

```text
Firmament remains data.
C# owns logic.
Forge is the interop boundary.
```

## Purpose

FORGE-CS-A3 wires the built-in C# Forge runtime concept validators into the existing Firmament V2 validation/report path used by:

```bash
dotnet run --project Aetheris.CLI -- validate <file.firmament|file.firmfixture> --json
```

The goal is narrow Phase 2 behavior change:

- keep the current parser/binder schema validation;
- run built-in C# concept validators after parse/bind;
- surface structured runtime concept diagnostics in the existing R1 validation report;
- keep AP242 export, PMI generation obligations, parser syntax, and concept-pack loading unchanged.

## Invocation flow

The runtime concept validation flow is now:

```text
FirmamentV2Parser.Parse
  -> bound FirmamentV2Document
  -> existing parser/binder schema diagnostics
  -> FirmamentV2VariablesAdapter
  -> FirmamentV2ConceptApplicationAdapter
  -> built-in ForgeConceptRegistry populated by StandardForgeRuntimeConceptPack
  -> IForgeConcept.Validate(ConceptValidationContext)
  -> structured FirmamentDiagnostic results
  -> FirmamentV2ValidationReportBuilder
  -> firmamentV2Validation JSON
```

Only the built-in in-process runtime pack is used:

```text
StandardForgeRuntimeConceptPack
```

No external pack loading, plugin loading, Roslyn compilation, or parser-registry replacement is introduced.

## Built-in checks

Implemented A3 runtime checks:

- `process<CNC>`
  - `minimumToolRadius` must be greater than zero.
  - `material` must be present and non-empty if runtime validation sees it.
- `hole<Shaft>`
  - `diameter` must be greater than zero.
  - `target` must be present.
  - missing diameter tolerance emits a warning recommendation.
- `hole<Counterbore>`
  - `diameter`, `counterboreDiameter`, and `counterboreDepth` must be greater than zero.
  - `counterboreDiameter` must be greater than or equal to `diameter`.
  - `target` must be present.
  - missing diameter/counterbore-diameter tolerance emits warning recommendations.
- `hole<Countersink>`
  - `diameter` and `countersinkDiameter` must be greater than zero.
  - `countersinkDiameter` must be greater than `diameter`.
  - `angle` must be greater than `0` and less than `180`.
  - `target` must be present.
  - missing diameter/countersink-diameter tolerance emits warning recommendations.

## Diagnostic codes and severity policy

Implemented runtime diagnostic codes:

- `forge.process.cnc.minimum-tool-radius-positive` (`fatal`)
- `forge.process.cnc.material-required` (`fatal`)
- `forge.hole.shaft.diameter-positive` (`fatal`)
- `forge.hole.shaft.target-required` (`fatal`)
- `forge.hole.shaft.diameter-tolerance-recommended` (`warning`)
- `forge.hole.counterbore.diameter-positive` (`fatal`)
- `forge.hole.counterbore.counterbore-diameter-positive` (`fatal`)
- `forge.hole.counterbore.counterbore-depth-positive` (`fatal`)
- `forge.hole.counterbore.diameter-order` (`fatal`)
- `forge.hole.counterbore.target-required` (`fatal`)
- `forge.hole.counterbore.diameter-tolerance-recommended` (`warning`)
- `forge.hole.countersink.diameter-positive` (`fatal`)
- `forge.hole.countersink.countersink-diameter-positive` (`fatal`)
- `forge.hole.countersink.diameter-order` (`fatal`)
- `forge.hole.countersink.angle-range` (`fatal`)
- `forge.hole.countersink.target-required` (`fatal`)
- `forge.hole.countersink.diameter-tolerance-recommended` (`warning`)

Policy:

- parser/binder still owns missing-field, unknown-field, duplicate-field, and type-mismatch diagnostics;
- runtime C# validators own semantic checks over already-bound values;
- warnings stay visible and do not make the report invalid;
- fatal runtime diagnostics make the top-level report status `invalid`.

Because parser-required fields already fail deterministically, the runtime validators are intentionally biased toward semantic checks rather than repeating every parser-required-field error.

## Report integration

`FirmamentV2ValidationReport` keeps the same root shape:

```text
firmamentV2Validation
  source
  status
  lets[]
  concepts[]
  pmi[]
  exportSupport
  diagnostics[]
  summary
```

A3 extends the report minimally:

- runtime concept diagnostics are appended to the existing top-level `diagnostics[]`;
- concept rows now include `runtimeValidation` with:
  - `provider`
  - `status`
  - `diagnostics[]`
- runtime diagnostic rows preserve `code`, `severity`, `message`, and optional `fieldName`/`target`.

Status policy remains:

- any fatal parser or runtime concept diagnostic => `invalid`
- otherwise any export-deferred PMI => `valid-with-deferred-export`
- otherwise => `valid`

## CLI behavior

`aetheris validate ... --json` now reports built-in runtime concept validation results through the existing `firmamentV2Validation` payload.

Warning-only runtime findings still return CLI exit code `0`.
Fatal runtime findings return CLI exit code `1` because the report status becomes `invalid`.

No `--forge-pack` option was added.

## Tests and fixtures

Focused coverage was added for:

- existing valid built-in concept fixture remains valid;
- countersink diameter-order failure;
- countersink angle-range failure;
- counterbore diameter-order failure;
- positive-value checks;
- shaft missing-tolerance warning;
- `process<CNC>.minimumToolRadius` failure;
- CLI invalid/warning exit behavior;
- explicit confirmation that validation uses `Aetheris.Standard` built-ins and does not advertise external pack loading.

Added fixtures:

- `fixtures/Language/invalid/concept-countersink-diameter-order.invalid.firmfixture`
- `fixtures/Language/invalid/concept-countersink-angle-range.invalid.firmfixture`
- `fixtures/Language/invalid/concept-counterbore-diameter-order.invalid.firmfixture`
- `fixtures/Language/invalid/concept-cnc-minimum-tool-radius.invalid.firmfixture`
- `fixtures/Language/valid/concept-shaft-missing-tolerance-warning.valid.firmfixture`

Forward link: [`forge-cs-a4-concept-pmi-obligations.md`](forge-cs-a4-concept-pmi-obligations.md) extends the same runtime concept path with report-only PMI obligation rows and warning-only missing-obligation findings.

## Explicit non-scope

FORGE-CS-A3 does not add:

- external concept-pack loading;
- plugin discovery;
- Roslyn compilation;
- template generation;
- PMI obligation generation;
- AP242 export changes;
- parser registry replacement;
- Firmament syntax changes;
- document mutation;
- opaque validator log output.
