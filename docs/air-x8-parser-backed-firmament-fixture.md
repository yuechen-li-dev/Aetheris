# AIR-X8 — Parser-backed Firmament fixture

## Purpose and scope

AIR-X8 anchors the `.firmfixture` corpus to real Firmament frontend behavior for one smallest source form. The new fixture proves that `aetheris trace --fixture` can load fixture metadata, extract the source body, invoke the existing Firmament parser, record frontend fields, and satisfy or fail the fixture expectation from the parser result.

## Relationship to AIR-X7

AIR-X7 introduced source-level `.valid.firmfixture` and `.invalid.firmfixture` contracts, but the first Chamfer fixtures were metadata-driven lowering contracts. AIR-X8 keeps that behavior and adds an opt-in parser-backed mode.

## Metadata-driven vs parser-backed fixtures

Metadata-driven fixtures remain the default when `parser-backed: true` is absent. Parser-backed fixtures add:

```text
// parser-backed: true
```

When present, trace invokes the existing Firmament frontend on the non-metadata body.

## First parser-backed fixture

The first parser-backed fixture is:

```text
fixtures/Firmament/Primitive/valid/box.valid.firmfixture
```

It uses existing TOON-style Firmament syntax already present in Firmament tests:

```text
firmament:
  version: 1

model:
  name: air-x8-box
  units: mm

ops[1]:
  -
    op: box
    id: base
    size[3]:
      10
      8
      6
```

This is intentionally a primitive box document rather than a new call-expression syntax. No grammar was expanded for AIR-X8.

## Frontend trace fields

Parser-backed reports include frontend fields:

- `parserBacked`
- `parserName`
- `parseSucceeded`
- `parseDiagnostics`
- `frontendStageReached`
- `frontendSummary`
- `expectationSatisfied`

Text output includes a `Frontend` section. JSON output includes a stable `frontend` object.

## Stage truthfulness

The AIR-X8 fixture reaches `parsed`. Trace does not claim Firmament-to-AIR, BRepPlan, CIR mirror, or STEP smoke for this parser-backed fixture because that lowering bridge is not wired into fixture trace yet. The report records `air-x8-air-lowering-not-wired-for-parser-backed-fixture` and stops at the truthful frontend boundary.

## Existing Chamfer fixtures

The AIR-X7 Chamfer fixtures remain metadata-driven. They continue to map to existing AIR route-selection/lowering trace behavior and are not converted to parser-backed mode.

## Invalid parser-backed behavior

A `.valid.firmfixture` with `parser-backed: true` and invalid source fails the fixture contract, emits parser failure diagnostics, and returns nonzero. An optional `.invalid.firmfixture` can later be added when the corpus needs a stable parser-rejection contract.

## Non-goals

- no production Firmament grammar expansion;
- no full Firmament-to-AIR migration;
- no geometry changes;
- no route replacement;
- no BRepPlan semantic changes;
- no CIR evaluator/tape behavior changes;
- no STEP exporter/importer changes;
- no BRep topology changes;
- no Boolean, chamfer, fillet, shell, AirEdgeSweep, BrepBoundedChamfer, or BrepBoundedFillet behavior changes;
- no arbitrary graph, import/recovery, triangle migration, or NURBS/freeform expansion.

## Tests run

AIR-X8 validation used CLI help, parser-backed text/JSON trace, existing metadata-driven Chamfer trace, and focused `dotnet test` filters for CLI/Firmament/Core/FrictionLab where available.

## Recommended next milestone

Recommended AIR-X9: **Firmament-to-AIR frontend boundary for parsed box primitive**. AIR-X8 proves parser invocation and isolates the next blocker: parsed primitive source is not yet wired into AIR trace lowering.

## AIR-X9 advancement

AIR-X9 advances the same parser-backed box fixture from `parsed` to `feature-air`. The syntax remains the existing TOON-style `op: box` with `size[3]`; the parser is still the real Firmament parser; and the new trace-only boundary creates a Feature AIR `CreateBox` summary with width/depth/height dimensions. Constructive AIR remains deferred and is not reported as reached.
