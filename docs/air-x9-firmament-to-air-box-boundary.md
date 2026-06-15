# AIR-X9 — Firmament-to-AIR box boundary

## Purpose and scope

AIR-X9 connects the first parser-backed `.firmfixture` source form to an AIR-facing summary. It is a frontend boundary milestone, not a geometry milestone: one parsed Firmament primitive box operation becomes one deterministic Feature AIR summary in `aetheris trace`.

## Relationship to AIR-X8

AIR-X8 proved that `fixtures/Firmament/Primitive/valid/box.valid.firmfixture` could load as a parser-backed fixture, invoke `FirmamentTopLevelParser`, and truthfully stop at `parsed`. AIR-X9 advances that same fixture from `parsed` to `feature-air` by recognizing the parsed box op and creating a trace-only Feature AIR summary.

## Existing Firmament syntax used

AIR-X9 uses the existing TOON-style Firmament primitive syntax; it does not add grammar:

```text
ops[1]:
  -
    op: box
    id: base
    size[3]:
      10
      8
      6
```

## Parser-backed fixture path

```text
fixtures/Firmament/Primitive/valid/box.valid.firmfixture
```

The fixture metadata now expects `feature-air` and `expected-feature-air: CreateBox`.

## Frontend path

```text
box.valid.firmfixture
  -> fixture metadata/source-body split
  -> FirmamentTopLevelParser
  -> parsed Firmament op: box
  -> Feature AIR trace summary: CreateBox
```

Constructive AIR is deliberately deferred in AIR-X9. The trace does not claim BRepPlan, BRep emission, STEP smoke, CIR mirror success, or production route selection for the parser-backed box fixture.

## Stage reached and why

The actual stage reached is `feature-air`. This is the furthest stable truthful boundary because the parser exposes a recognized box op and the fixture source provides deterministic `width=10`, `depth=8`, and `height=6`, but there is no narrow dependency-clean production lowering from this parsed source fixture to an existing Constructive AIR node in this milestone.

## Feature AIR summary fields

Text output includes a `Feature AIR` section with:

- source op kind: `box`;
- node kind: `CreateBox`;
- dimensions: `width=10`, `depth=8`, `height=6`;
- construction intent: `box / rectangular prism`;
- stage reached: `feature-air`;
- deterministic AIR-X9 diagnostics.

JSON output includes stable `featureAir` fields: `parserBacked`, `sourceOpKind`, `nodeKind`, `dimensions`, `constructionIntent`, `stageReached`, `diagnostics`, and `guarantees`.

## Dimension extraction status

Dimensions are reported for the AIR-X8/X9 box fixture. The adapter first checks parsed raw fields and then uses a narrow trace-only source-body extraction for the existing `size[3]` box form. This keeps parser behavior unchanged while making the trace summary deterministic.

## What remains metadata-driven

The AIR-X7 Chamfer fixtures remain metadata-driven and continue to exercise the existing AIR-X2/AIR-X6 trace paths. AIR-X9 does not convert them to parser-backed Firmament syntax.

## Non-goals

- no Firmament grammar expansion;
- no full Firmament-to-AIR migration;
- no production route replacement;
- no geometry changes;
- no BRepPlan, BRep, STEP, or CIR requirement for the parser-backed fixture;
- no route-selection or JudgmentUtility behavior changes.

## Tests run

The implementation was validated with the AIR-X9 parser-backed box trace commands, the metadata-driven Chamfer fixture compatibility checks, focused CLI tests, and the required filtered .NET build/test commands recorded in the PR summary.

## Recommended next milestone

Recommended next milestone: **AIR-X10 — Box Feature AIR to Constructive AIR/ProfileExtrude trace**. AIR-X9 found the Feature AIR boundary clean and dimension-bearing; the next narrow step is to decide whether a parser-backed box `CreateBox` can canonicalize to an `AirProfileExtrude` rectangle summary without creating dependency cycles or replacing production routes.

## AIR-X10 advancement

AIR-X10 advances the same parser-backed box fixture from `feature-air` to `constructive-air`. The trace now canonicalizes Feature AIR `CreateBox` to Constructive AIR `AirProfileExtrude` with canonical form `rectangle-profile-extrude`, using `size[0]` as width, `size[1]` as depth, and `size[2]` as height. The profile wrapper/emitter remains deferred, and BRepPlan/emission/STEP/CIR sections remain truthful non-success placeholders.


## AIR-X11 advancement note

The parser-backed box boundary now reaches existing profile extrusion emission evidence after Feature AIR and Constructive AIR. The source syntax and parser boundary are unchanged; the trace bridge reuses the existing profile extrusion wrapper and does not expand production Firmament behavior.
