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
