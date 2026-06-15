# AIR-X11 — Box Constructive AIR to profile emission trace

## Purpose and scope

AIR-X11 advances the parser-backed box fixture from AIR-X10 Constructive AIR evidence to existing profile extrusion wrapper/emitter evidence. This is a trace integration milestone, not a geometry milestone: it reuses the already-existing AIR-X1 `AirProfileExtrudeWrapper` and `LineArcProfileExtrudeEmitter` path and reports only what that path truthfully exposes.

AIR-A1 intentionally pauses before treating multi-axis features as the next backend continuation. The region audit defines scoped AIR Regions for side holes, side pockets, bosses, patterns, shell/local offset contexts, and other local-frame features so future work does not collapse different-axis construction directly into global Boolean or ad hoc BRep mutation.

## Relationship to AIR-X10

AIR-X10 proved `fixtures/Firmament/Primitive/valid/box.valid.firmfixture` can be loaded, parsed by `FirmamentTopLevelParser`, recognized as `op: box`, surfaced as Feature AIR `CreateBox`, and canonicalized as Constructive AIR `AirProfileExtrude` with canonical form `rectangle-profile-extrude`. AIR-X11 starts from that same Constructive AIR summary and invokes a narrow trace probe for profile emission evidence.

## Existing Firmament syntax used

The fixture continues to use the existing TOON-style Firmament syntax:

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

No grammar production or source syntax changes were introduced.

## Parser-backed fixture path

```text
fixtures/Firmament/Primitive/valid/box.valid.firmfixture
```

The fixture now expects `emitted-brep` because the reused profile extrusion emitter returns a nonzero BRep topology summary.

## Pipeline

```text
fixture
  -> FirmFixtureLoader
  -> FirmamentTopLevelParser
  -> Feature AIR CreateBox summary
  -> Constructive AIR AirProfileExtrude summary
  -> BoxConstructiveAirToProfileEmissionTraceProbe
  -> AirProfileExtrudeWrapper
  -> LineArcProfileExtrudeEmitter
  -> emitted BRep topology summary
```

STEP smoke remains unavailable for this lane because the wrapper exposes `AirStepSmokeSummary.NotChecked`.

## Dimension propagation

The trace keeps the AIR-X10 convention:

- `size[0]` maps to rectangle width;
- `size[1]` maps to rectangle depth;
- `size[2]` maps to extrusion height.

For the committed fixture, Feature AIR, Constructive AIR, and profile emission all report `width=10`, `depth=8`, and `height=6`. The existing emitter convention centers the extrusion about `Z=0`, producing bounds `[-5,-4,-3]..[5,4,3]` for the fixture.

## Emission summary fields

The trace report adds `profileEmission` with stable fields:

- `wrapperInvoked`;
- `emitterName`;
- `succeeded`;
- `width`, `depth`, `height`;
- `stageReached`;
- `topologySummary` when available;
- `stepSmoke`;
- `diagnostics`;
- `guarantees`.

## Stage reached and why

The actual stage is `emitted-brep`. The existing wrapper invokes `LineArcProfileExtrudeEmitter`, the emitter succeeds, and the wrapper returns a deterministic nonzero topology summary for the rectangular prism. AIR-X11 does not claim `step-smoke` because no STEP smoke check is performed by this wrapper path.

## BRepPlan and CIR status

BRepPlan remains deferred for parser-backed profile extrusion in this milestone; no BRepPlan semantics were added or changed. CIR mirror remains `not-requested`; no CIR evaluator, tape, or mirror behavior changed.

## What remains metadata-driven

The AIR-X7 chamfer fixtures remain metadata-driven and continue through their established AIR-X2/AIR-X6 trace paths. AIR-X11 changes only the parser-backed box lane.

## Non-goals

- no Firmament grammar expansion;
- no full Firmament-to-AIR migration;
- no production route replacement;
- no geometry changes;
- no profile emitter rewrite;
- no BRepPlan/CIR requirement;
- no STEP exporter/importer changes;
- no BRep topology behavior changes;
- no route-selection/JudgmentUtility behavior changes;
- no arbitrary graph, import/recovery, triangle migration, or NURBS/freeform expansion.

## Tests run

Validation included CLI build/help/trace commands, parser-backed box text and JSON traces, metadata-driven chamfer fixture trace, and focused filtered test suites. See the PR summary for exact commands and results.

## Recommended next milestone

Recommended next milestone: **AIR-X12 — ProfileExtrude BRepPlan for parser-backed box**. AIR-X11 shows the existing emitter can produce BRep evidence from parser-backed Constructive AIR, while BRepPlan remains the next explicitly deferred topology-planning boundary.
