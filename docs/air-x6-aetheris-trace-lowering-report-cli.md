# AIR-X6 — `aetheris trace` lowering report CLI

## Purpose and scope

AIR-X6 adds the first explicit top-level lowering trace command:

```bash
aetheris trace --case <name> [--json] [--out-dir <dir>]
```

`trace` is a compiler flight recorder for known built-in AIR/Firmament-derived cases. It reports how an authored feature is represented as AIR, admitted by route selection, summarized as BRepPlan, checked through existing BRep/STEP smoke, and mirrored into CIR when admitted.

## `trace` vs `analyze`

* `aetheris trace` inspects compiler lowering: AIR, route decision, BRepPlan, emitted BRep/STEP smoke, CIR mirror, diagnostics, guarantees, and losses.
* `aetheris analyze` inspects existing geometry/artifacts: STEP/BRep topology, faces, edges, maps, sections, and volume.
* `trace` accepts no arbitrary STEP input. STEP files remain the job of `aetheris analyze ...`.

## Supported cases

* `prismatic-section-transition`
* `top-face-loop-chamfer`

Optional aliases are accepted for convenience:

* `prismatic`
* `loop-chamfer`

## Output behavior

Text output is the default because the command exists to help humans and LLMs inspect what Aetheris is doing. `--json` emits deterministic machine-readable JSON.

Examples:

```bash
aetheris trace --case prismatic-section-transition
aetheris trace --case top-face-loop-chamfer
aetheris trace --case prismatic-section-transition --json
aetheris trace --case top-face-loop-chamfer --json
```

With `--out-dir`, text mode writes a `.txt` report and prints the written path. JSON mode writes the `.json` report and also emits the JSON to stdout for parser-friendly CLI use.

Preferred artifact names are used:

* `air-x6-prismatic-section-transition-trace.txt`
* `air-x6-prismatic-section-transition-trace.json`
* `air-x6-top-face-loop-chamfer-trace.txt`
* `air-x6-top-face-loop-chamfer-trace.json`

## Report contents

Each successful report includes:

* input/source case identity;
* AIR node and provenance summary;
* route-selection summary;
* BRepPlan counts, split policy, bounds, and semantic role counts;
* existing emitted BRep / STEP smoke summary;
* CIR mirror admission, backend, capabilities, provenance, and known losses;
* deterministic diagnostics;
* guarantees and unchanged behavior.

## Future AIR Region trace reporting

AIR-A1 identifies future trace needs for scoped AIR Regions: region tree, local frames, region effects, explicit yields, boundary contracts, parent integration routes, BRepPlan region roles, CIR mirror losses, and deterministic fallback/rejection diagnostics. AIR-X6 does not implement these fields; the note records the expected trace surface for later region milestones without changing current command behavior.

## Text output example

```text
Aetheris trace — AIR-X6 lowering report
Case: top-face-loop-chamfer
Trace kind: lowering
Input kind: built-in-case

AIR
  Node: TopFaceLoopChamfer
  Route: TopFaceLoopChamferPrismatic
  Selection class: FaceBoundaryLoop
  Rule: UniformChamfer

Route decision
  Mode: SwitchMatch
  Selected route: TopFaceLoopChamferPrismatic

BRepPlan
  Plan kind: PrismaticSectionTransition
  Vertices: 12
  Edges: 20
  Faces: 10
  Loops: 10
  Coedges: 40
  Cap faces: 2
  Chamfer faces: 4

Emission / STEP
  STEP smoke: succeeded

CIR mirror
  Status: mirror-admitted-exact
  Backend: cir-convex-polyhedron
```

## JSON output example

```json
{
  "milestone": "AIR-X6",
  "command": "trace",
  "traceKind": "lowering",
  "inputKind": "built-in-case",
  "caseName": "top-face-loop-chamfer",
  "succeeded": true,
  "air": { "node": "TopFaceLoopChamfer" },
  "routeDecision": { "mode": "SwitchMatch" },
  "bRepPlan": { "vertices": 12, "edges": 20, "faces": 10, "chamferFaces": 4 },
  "emission": {},
  "stepSmoke": {},
  "cirMirror": {},
  "capabilities": [],
  "knownLosses": [],
  "diagnostics": [],
  "guarantees": []
}
```

## Invalid usage behavior

* Missing `--case` returns nonzero, reports that `--case` is required, and lists supported cases.
* Unknown cases return nonzero and list supported cases.
* Positional STEP-like input returns nonzero with: `trace does not analyze STEP files; use \`aetheris analyze ...\`.`
* Unsupported options follow existing CLI error style.

## Non-goals

AIR-X6 does not change production geometry or topology behavior. It specifically does not add STEP input to trace, replace production routes, alter production analyzer behavior, change STEP exporter/importer behavior, change BRep topology behavior, add geometry implementation, add import/recovery, add arbitrary graph support, change route-selection/JudgmentUtility semantics, change Firmament lowering, change Boolean behavior, change CIR evaluator/tape behavior, or expand NURBS/freeform support.

## Tests run

Focused AIR-X6 validation used CLI help, text output, JSON parsing/determinism, `--out-dir` writing, and invalid usage tests. Broader gates should include CLI/core/Firmament/FrictionLab AIR, CIR, BRepPlan, STEP, prismatic, and chamfer filters.

## Recommended next milestone

Recommended AIR-X7: **unified trace artifact corpus / golden summaries**. AIR-X6 showed that deterministic reports can be composed from existing AIR-X1 through AIR-X5 summary surfaces without replacing production behavior; golden trace artifacts would lock the text/JSON contracts before adding source-file trace input.

## AIR-X7 fixture input extension

AIR-X7 extends `aetheris trace` with `--fixture <path>` while keeping AIR-X6 built-in `--case` traces intact. Fixture paths must end in `.valid.firmfixture` or `.invalid.firmfixture`; `--case` and `--fixture` are mutually exclusive. Text remains default, JSON is emitted only with `--json`, and `--out-dir` writes the corresponding `.txt` or `.json` report. Fixture trace reports add fixture expectation, expected/actual lowering stage, expected route/reason, expectation satisfaction, and deterministic fixture diagnostics. Arbitrary STEP input remains rejected by `trace`; use `analyze` for STEP geometry.

## AIR-X8 parser-backed fixture frontend section

For `.firmfixture` inputs with `// parser-backed: true`, `aetheris trace --fixture` now includes a `Frontend` section in text output and a `frontend` object in JSON. The fields record whether parser-backed mode was requested, the parser/frontend name, parse success, deterministic parse diagnostics, and the truthful frontend stage reached. The first parser-backed primitive box fixture stops at `parsed` because Firmament-to-AIR fixture lowering is not wired in AIR-X8.

## AIR-X9 Feature AIR section for parser-backed fixtures

For the parser-backed box fixture, `aetheris trace --fixture` now includes a `Feature AIR` text section and a `featureAir` JSON object. The section reports source op `box`, node `CreateBox`, dimensions when extracted, construction intent, deterministic diagnostics, and the truthful stage `feature-air`. Constructive AIR, BRepPlan, BRep/STEP, and CIR mirror sections remain unavailable/not-requested for this fixture unless a later milestone wires those boundaries.

## AIR-X10 Constructive AIR fixture section

For parser-backed Firmament primitive fixtures, `aetheris trace --fixture` can now include both `Feature AIR` and `Constructive AIR` sections. The AIR-X10 box fixture reports source op `box`, Feature AIR `CreateBox`, Constructive AIR `AirProfileExtrude`, canonical form `rectangle-profile-extrude`, rectangle width/depth, extrusion height, and truthful deferred BRepPlan/emission/STEP/CIR status.


## AIR-X11 profile emission section

For parser-backed Firmament box fixtures, `aetheris trace --fixture` may include a `Profile extrusion emission` text section and a `profileEmission` JSON object. These fields report wrapper invocation, emitter name, propagated dimensions, topology summary when exposed by the existing wrapper, STEP smoke availability, diagnostics, and guarantees. BRepPlan and CIR are still reported separately and remain deferred/not-requested unless actually wired for the lane.

## AIR-REGION-X1 status note

AIR-REGION-X1 adds a trace-only AIR Region skeleton: parser-backed box fixtures report a `RootRegion`, region fixtures can report metadata-driven `FaceAttachedRegion` yields with deferred integration, and no Boolean, geometry emission, production route replacement, grammar expansion, BRepPlan semantics, or CIR behavior is changed. See `docs/air-region-x1-region-model-skeleton-trace-fixtures.md`.

## AIR-REGION-X2 region yield contract trace note

For the side-hole region fixture, `aetheris trace` now renders a `Region yield` block in text and a structured `regions.regions[].yield` object in JSON. The fields are stable trace summaries for attachment, profile, direction, affected scope, boundary intent, integration status, diagnostics, known losses, and guarantees. The side-hole route remains trace-only and deferred.


## AIR-REGION-X3 trace note

`aetheris trace --fixture fixtures/Firmament/Region/valid/side-hole-face-attached-region.valid.firmfixture` now prints a `Region CIR mirror` section for the side-hole `FaceAttachedRegion`. The JSON region entry includes `cirMirror` with stable status, backend, fields, capabilities, losses, diagnostics, and guarantees.

## AIR-REGION-X4 region BRep boundary trace note

For `fixtures/Firmament/Region/valid/side-hole-face-attached-region.valid.firmfixture`, `aetheris trace` now reports a `Region BRepPlan boundary` text section and a structured `regions.regions[].brepBoundary` JSON object. The summary is contract-only: it records affected parent face, circular entry intent, deferred exit intent, deferred cylindrical cut-wall intent, planned role strings, losses, and guarantees while keeping integration deferred and materializing no topology.

## AIR-REGION-X5 note

AIR-REGION-X5 adds a trace-only side-hole integration route decision scaffold. The side-hole `FaceAttachedRegion` now reports deterministic candidate statuses, selects `DeferredIntegration`, rejects Boolean fallback as not admitted, keeps the CIR mirror analysis-only, and keeps the BRepPlan boundary contract as topology-side intent without materialization.

## AIR-REGION-X6 region placeholder trace note

Region fixture traces can now include a `Region BRepPlan placeholders` text section and a JSON `regions.regions[].brepPlaceholders` object. For the side-hole FaceAttachedRegion this section reports `PlaceholderOnly`, five deterministic placeholder elements, zero materialized elements, and guarantees that no parent topology mutation, BRepPlan materialization, BRep emission, STEP smoke, or Boolean invocation occurred.
