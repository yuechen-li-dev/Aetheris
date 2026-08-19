# STEP-V2-X7 semantic PMI AP242 emission

STEP-V2-X7 adds the minimal Firmament V2 semantic PMI declarations needed to prove AP242 semantic PMI emission through the production build path. This is Tier 6 optional MVP scope and is not graphical PMI.

## Supported declarations

Only two declaration forms are accepted in a `pmi { ... }` block:

```firmament
pmi {
  diameter mountDiameter {
    target: mount
    value: 2mm
  }

  datum A {
    target: top
  }
}
```

* `diameter <name>` declares semantic hole-diameter PMI. The target must resolve to a V2 semantic hole name in a `modify` block and `value`/`diameter` must be a positive millimetre length.
* `datum <label>` declares a semantic planar datum. The target must be a stable face selector (`face(+Z)`) or a face alias exposed by a Box (`top`).

The declaration identity is the entry name (`mountDiameter`, `A`), the kind is encoded as `HoleDiameter` or `DatumPlane` in the V2 AST, the target is retained as authored, and the diameter value is retained for hole-diameter PMI.

## Fixture paths

* `fixtures/Regression/PMI/valid/pmi-v2-hole-diameter-callout-emits-in-step.valid.firmfixture`
* `fixtures/Regression/PMI/valid/pmi-v2-datum-plane-emits-in-step.valid.firmfixture`

Both fixtures are `tier: 6`, `current-stage: step-verified`, `semantic-pmi-required: true`, and `graphical-pmi-required: false`.

## Command path and exporter route

The fixtures are built through the real CLI path:

```bash
aetheris build fixtures/Regression/PMI/valid/pmi-v2-hole-diameter-callout-emits-in-step.valid.firmfixture --out <tmp>/pmi-v2-hole-diameter.step --json
aetheris build fixtures/Regression/PMI/valid/pmi-v2-datum-plane-emits-in-step.valid.firmfixture --out <tmp>/pmi-v2-datum-plane.step --json
```

The route remains:

```text
Firmament V2 source -> FirmamentV2Parser -> BrepBody (Box or AirHoleFeature materialization) -> Step242Exporter.ExportBody(body, semanticPmi)
```

No hardcoded STEP templates or trace-only outputs are used.

## AP242 semantic evidence

The existing Aetheris AP242 semantic PMI style is reused:

* hole diameter evidence: `SHAPE_ASPECT('firmament-feature:base.mount', ...)`, `PROPERTY_DEFINITION('diameter:base.mount', ...)`, and `SHAPE_DIMENSION_REPRESENTATION('diameter:base.mount', ...)`;
* datum evidence: `SHAPE_ASPECT('firmament-datum:A', ...)` and `PROPERTY_DEFINITION('datum:A:base', ...)`.

The tests also require topology markers (`ADVANCED_FACE`, `VERTEX_POINT`), STEP reimport through `Step242Importer`, exact volume analysis for the hole fixture (`480 - pi * 1^2 * 6`), and exact volume analysis for the datum box fixture (`480`).

## No graphical PMI

STEP-V2-X7 intentionally does not implement graphical PMI, drawing views, annotation layout, leaders, rendered callouts, annotation planes, dimension graphics, DisplayIR PMI, frontend PMI, or PMI roundtrip editing. The tests assert that graphical PMI is not required and reject graphical markers such as `DRAUGHTING_CALLOUT` and `ANNOTATION_PLANE`.

## Relationship to MVP readiness contract

This implements the optional Tier 6 entries in `docs/development/scope-contracts/mvp/firmament-v2-ap242-mvp-readiness-contract.md`: `pmi-v2-hole-diameter-callout-emits-in-step` and `pmi-v2-datum-plane-emits-in-step`. PMI remains non-MVP-required unless a pitch/demo explicitly requires annotated AP242 output.

## Relationship to existing AP242 PMI route

The implementation reuses the existing `Step242Exporter` semantic PMI payload records (`Step242SemanticPmiHole` and `Step242SemanticPmiDatum`) and the established property/shape-aspect style. It adds only a narrow V2 parser/lowering adapter that produces those existing records for semantic hole diameter and planar datum declarations.

## Deferred PMI features

Deferred: graphical PMI, GD&T feature-control frames, tolerance stacks, standards libraries, arbitrary edge/loop targets, multiple drawing views, annotation placement, leader geometry, dimension graphics, DisplayIR/frontend PMI, PMI roundtrip editing, full AP242 PMI coverage, and new STEP exporter architecture.
