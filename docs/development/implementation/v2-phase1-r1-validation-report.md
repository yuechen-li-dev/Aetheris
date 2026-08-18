# V2 Phase 1 R1 validation report

R1 adds a structured validation/report integration layer for the Firmament V2 Phase 1 manufacturing-intent stack. It does not add authoring syntax, geometry behavior, DFM execution, graphical PMI, or new AP242 lowering.

## Purpose

The report makes the existing parser/binder output visible and auditable for humans, LLMs, demo packets, and future AP242 export verification. Given a parser-backed Firmament V2 file or fixture, it answers:

- what scalar and record-field `let` values are declared;
- which values carry explicit tolerance evidence;
- which Forge concept applications bind against descriptors;
- which PMI records bind;
- which PMI dimensions are toleranced;
- which PMI records are currently export-supported versus export-deferred;
- which diagnostics are fatal and which are warnings.

## CLI exposure

The report is exposed through:

```bash
dotnet run --project Aetheris.CLI -- validate <firmament-or-firmfixture> --json
```

The JSON root is `firmamentV2Validation`.

## Schema summary

The top-level report shape is:

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

Each `let` row includes a dotted name for record fields, declared type, evaluated nominal value, optional tolerance, source (`let` or `let-record`), dependency names when available, and item diagnostics.

Each concept row includes kind (`manufacturing` or `feature`), optional feature name, family, concept, status, `dfmStatus: not-run`, bound fields, field type/kind, tolerance presence, field source, and diagnostics.

Each PMI row includes kind, name, status, export support, targets, datum refs, optional dimension/tolerance, deferred-export reason where applicable, and diagnostics.

## Status semantics

Report status is deterministic:

- `invalid`: at least one fatal diagnostic is present.
- `valid-with-deferred-export`: no fatal diagnostics are present, but one or more authoring-valid PMI records have deferred AP242 export support.
- `valid`: no fatal diagnostics and no deferred PMI records.

Per-item status values are:

- `valid`: item binds and is not export-deferred;
- `invalid`: item has fatal diagnostics;
- `export-deferred`: item is valid authoring data but AP242 lowering is deferred.

Warnings do not make the report invalid. The L4 tolerance-dropped-through-arithmetic warning remains visible as a non-fatal diagnostic so nominal-only arithmetic cannot silently erase tolerance evidence.

## Export support matrix

R1 reports the current P1 state without adding AP242 export behavior:

| PMI kind | R1 export support |
| --- | --- |
| `datum` | `supported` when the existing semantic datum export path has a bound target; otherwise `supported-when-target-resolves` |
| `diameter` | `supported` when the existing semantic diameter export path has a bound target and dimension; otherwise `supported-when-target-resolves` |
| `distance` | `deferred` |
| `flatness` | `deferred` |
| `parallel` | `deferred` |
| `perpendicular` | `deferred` |
| `coplanar` | `deferred` |

Deferred export is explicit in both per-record rows and the summary; records are not silently ignored.

## Example excerpt

```json
{
  "firmamentV2Validation": {
    "status": "valid-with-deferred-export",
    "summary": {
      "letCount": 7,
      "tolerancedLetCount": 4,
      "conceptCount": 2,
      "validConceptCount": 2,
      "pmiRecordCount": 5,
      "exportSupportedPmiCount": 2,
      "exportDeferredPmiCount": 3,
      "fatalDiagnosticCount": 0,
      "warningDiagnosticCount": 2
    }
  }
}
```

## Diagnostics policy

The report reuses Firmament V2 parser diagnostic codes and classifies them through the parser fatal-diagnostic table. Fatal diagnostics make the report `invalid`; warnings remain visible but non-blocking. Concept, PMI, tolerance, expression, and let-binding diagnostics are not hidden.

## Non-scope

R1 intentionally does not add:

- new Firmament syntax;
- new AP242 lowering or export behavior;
- broad DFM execution (`dfmStatus` is `not-run`);
- geometry/modeling behavior;
- graphical PMI/layout behavior.
