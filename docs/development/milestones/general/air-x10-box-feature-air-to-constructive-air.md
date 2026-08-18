# AIR-X10 — Box Feature AIR to Constructive AIR/ProfileExtrude trace

## Purpose and scope

AIR-X10 advances the first parser-backed Firmament fixture from a Feature AIR-only trace summary to a Constructive AIR trace summary. It proves one parsed primitive source form can cross the frontend-to-constructive-MIR boundary without expanding Firmament grammar, replacing production routes, or requiring BRepPlan/BRep/STEP/CIR output.

## Relationship to AIR-X9

AIR-X9 proved that `fixtures/Primitive/valid/box.valid.firmfixture` loads through `FirmFixtureLoader`, invokes `FirmamentTopLevelParser`, recognizes `op: box`, extracts `size[3]`, and reports Feature AIR `CreateBox`. AIR-X10 keeps that path and adds a narrow trace-only canonicalizer from that Feature AIR box summary to Constructive AIR `AirProfileExtrude`.

## Existing Firmament syntax used

AIR-X10 uses the existing TOON-style primitive syntax:

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

No new syntax or grammar production is introduced.

## Parser-backed fixture path

```text
fixtures/Primitive/valid/box.valid.firmfixture
```

The fixture now expects `constructive-air`, `expected-feature-air: CreateBox`, and `expected-constructive-air: AirProfileExtrude`.

## Frontend path

```text
box.valid.firmfixture
  -> fixture metadata/source-body split
  -> FirmamentTopLevelParser
  -> parsed Firmament op: box
  -> Feature AIR trace summary: CreateBox
  -> Constructive AIR trace summary: AirProfileExtrude
```

## Dimension mapping

The trace adapter maps the existing `size[3]` tuple deterministically:

- `size[0]` -> width;
- `size[1]` -> depth;
- `size[2]` -> height.

For the committed fixture this yields `width=10`, `depth=8`, and `height=6`. The rectangle profile uses width/depth. The extrusion uses height along the reported `Z` axis.

## Constructive AIR summary fields

The JSON/text trace surface reports:

- node kind: `AirProfileExtrude`;
- canonical form: `rectangle-profile-extrude`;
- source Feature AIR node kind: `CreateBox`;
- profile kind: `Rectangle`;
- width/depth/height;
- extrusion axis: `Z`;
- construction intent: `Box`;
- route kind: `ProfileExtrude` as a canonical route label only;
- diagnostics and guarantees.

## Stage reached and why

The actual stage is `constructive-air` because the parsed box has all bounded data required to summarize a canonical rectangular profile extrusion. The adapter does not claim backend topology emission.

## Profile wrapper/emitter status

AIR-X10 does **not** invoke the existing profile extrusion wrapper or `LineArcProfileExtrudeEmitter`. It creates a Constructive AIR summary only. BRepPlan, emission, STEP smoke, and CIR mirror remain deferred with explicit diagnostics. A future milestone can invoke the wrapper if dependency direction and trace semantics remain clean.

## What remains metadata-driven

The AIR-X7 Chamfer fixture corpus remains metadata-driven and continues to exercise the existing AIR-X2/AIR-X6 trace paths. AIR-X10 does not migrate those fixtures to parser-backed Firmament syntax.

## Non-goals

- no grammar expansion;
- no full Firmament-to-AIR migration;
- no production route replacement;
- no geometry changes;
- no BRepPlan/CIR requirement;
- no STEP exporter/importer changes;
- no BRep topology changes;
- no route-selection/JudgmentUtility behavior changes.

## Tests run

Implementation validation includes the focused CLI build/help/trace commands and filtered CLI, Firmament, Core, and FrictionLab test commands listed in the PR summary.

## Recommended next milestone

Recommended next milestone: **AIR-X11 — Box Constructive AIR to existing profile extrusion wrapper/emission trace**. AIR-X10 found the parser-backed source-to-Constructive-AIR bridge clean, while wrapper/emitter invocation remains deliberately deferred to avoid overstating BRepPlan, STEP, or CIR success.

## AIR-X11 advancement note

AIR-X11 now consumes this Constructive AIR `AirProfileExtrude` summary through a narrow parser-backed box profile emission trace probe. The box fixture advances from `constructive-air` to `emitted-brep` only because the existing AIR-X1 profile extrusion wrapper invokes `LineArcProfileExtrudeEmitter` and returns deterministic BRep topology evidence; BRepPlan and CIR remain deferred.
