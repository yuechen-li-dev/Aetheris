# EDGE-PRISMATIC-X8 — Hybrid map dispatch using admitted prismatic CIR mirrors

## 1. Purpose and scope

EDGE-PRISMATIC-X8 adds a focused lab/test-only hybrid map dispatch prototype for generated prismatic bodies whose AIR/prismatic source sections can be admitted as `CirConvexPolyhedronMirror` mirrors. The prototype proves that generated prismatic source data can select a CIR mirror backend for occupancy/thickness mapping without changing production `analyze map` behavior.

The implementation lives in `Aetheris.Kernel.Core.Tests.Cir` as `PrismaticHybridMapDispatchLab` and supporting test-only records. It consumes generated prismatic source data directly, builds the existing internal `CirConvexPolyhedronMirror`, and reports deterministic map summaries and diagnostics. It does not infer mirrors from imported STEP and does not claim face or topology identity from CIR-backed maps.

## 2. References

This milestone follows these prior contracts and proofs:

- AIR/CIR/BRep authority and mirror contract: `docs/development/milestones/general/air-cir-a0-authority-and-mirror-contract.md`.
- AIR/CIR mirror metadata: `docs/development/milestones/general/air-cir-x1-mirror-metadata-prototype.md`.
- CIR map prototype: `docs/development/milestones/general/cir-map-x1-primitive-map-prototype.md`.
- Mirror-aware primitive map dispatch: `docs/development/milestones/general/cir-map-x2-mirror-aware-primitive-map-dispatch.md`.
- Prismatic mirror feasibility: `docs/development/milestones/general/cir-prismatic-x1-prismatic-mirror-feasibility.md`.
- Convex polyhedron mirror implementation: `docs/development/milestones/general/cir-prismatic-x2-convex-polyhedron-mirror.md`.
- Analyze-map audit recommending hybrid dispatch: `docs/development/milestones/general/edge-prismatic-x7-analyze-map-cir-frep-audit.md`.
- X5/X6 corpus and analyzer evidence: `docs/development/milestones/frictionlab/edge-prismatic-x5-section-transition-artifact-corpus.md` and `docs/development/milestones/frictionlab/edge-prismatic-x6-corpus-stability-and-analyzer-confirmation.md`.

## 3. Dispatcher/API shape

The test-only prototype introduces these internal test types:

- `PrismaticHybridMapDispatchLab` — dispatcher entry point and fixture source factory.
- `GeneratedPrismaticMapSource` — generated prismatic source sections plus optional correspondence.
- `HybridMapDispatchResult` — selected backend, admission status, requested use, summary, known losses, diagnostics, and recommendation.
- `HybridMapSummary` — stable map summary fields.
- `HybridMapBackendKind` — `CirConvexPolyhedron`, `SdfTape`, `BrepRaycast`, or `Unsupported`.

The summary reports:

- backend selected: `cir-convex-polyhedron`, `cir-tape`, `brep-raycast`, or `unsupported`;
- mirror status;
- requested use;
- view;
- rows and columns;
- occupied/hit count;
- empty count;
- minimum, maximum, and average thickness;
- bounds;
- diagnostics;
- known losses.

## 4. Backend selection policy

The X8 dispatcher policy is intentionally narrow:

1. For a generated AIR/prismatic source, attempt to build/admit a `CirConvexPolyhedronMirror` from the source sections.
2. If the mirror status is `mirror-admitted-exact` and the request is map occupancy, select `cir-convex-polyhedron`.
3. If the request is face identity or topology parity, reject as `mirror-rejected-lossy-for-request` and return no map summary.
4. If no admitted mirror is available, return `unsupported` with deterministic mirror-unavailable diagnostics.
5. For a primitive box baseline, reuse the CIR-MAP-X2 primitive CIR tape path and compare BRep raycast baseline when supplied.
6. Do not infer a CIR mirror from arbitrary imported STEP.
7. Do not return face IDs, loop IDs, split-face lineage, feature role labels, or topology parity from CIR-backed maps.

## 5. Prismatic cases

### `rectangle-inset`

The generated source uses the X2-style two-section rectangle/inset profile stack. X8 admits it as an exact convex polyhedron mirror for map occupancy and selects the `cir-convex-polyhedron` backend.

### `top-edge-chamfer`

The generated source uses `PrismaticTopEdgeChamferPrototype.CreateSectionStack` for the controlled top-edge chamfer case. X8 admits it as an exact convex polyhedron mirror for map occupancy and selects the `cir-convex-polyhedron` backend.

### Optional cases

The X8 implementation keeps optional polygon-scaled corpus cases out of scope. They remain candidates for a future mirror-hardening milestone after the generated-source dispatch seam is promoted beyond the focused rectangle/chamfer proof.

## 6. Map summary findings

X8 uses the existing `CirConvexPolyhedronMirror.CreateTopViewSummary` sampling policy for prismatic CIR maps. Stable 16x16 top-view findings in the focused tests are:

| Case | Backend | Grid | Occupied | Empty | Thickness min | Thickness max | Thickness avg |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: |
| `rectangle-inset` | `cir-convex-polyhedron` | 16x16 top | 256 | 0 | approximately 0.75 | 4.0 | approximately 3.05 |
| `top-edge-chamfer` | `cir-convex-polyhedron` | 16x16 top | 256 | 0 | approximately 3.3125 | 4.0 | approximately 3.9531 |
| primitive box | `cir-tape` with optional `brep-raycast` baseline | 16x16 top | 256 | 0 | 4.0 | 4.0 | 4.0 |

These are CIR/lab summaries for generated-source mirrors, not production `analyze map` results.

## 7. Lossy request behavior

`top-edge-chamfer` face-identity and topology-parity requests reject as lossy with no substituted occupancy map. The stable recommendation is `prismatic-hybrid-map-lossy-request-rejected`.

Machine-checkable diagnostics include:

- `edge-prismatic-x8-mirror-rejected-lossy-for-request:face-identity`;
- `edge-prismatic-x8-mirror-rejected-lossy-for-request:topology-parity`;
- `edge-prismatic-x8-backend-selected:unsupported`.

## 8. Imported STEP/no-mirror behavior

Imported STEP-only prismatic bodies remain unsupported by this prototype. X8 deliberately does not reconstruct mirror authority from STEP/BRep topology. The imported-step test returns no map summary, selects `unsupported`, and reports:

- `edge-prismatic-x8-imported-step-no-mirror`;
- `edge-prismatic-x8-mirror-unavailable:<source>`;
- `edge-prismatic-x8-backend-selected:unsupported`.

## 9. Known losses

Every CIR-backed prismatic map summary includes these known losses:

- face identity lost;
- loop identity lost;
- split-face lineage lost;
- feature role labels lost;
- topology parity unavailable.

The prototype also emits X8-scoped loss diagnostics, including `edge-prismatic-x8-loss-face-identity`, `edge-prismatic-x8-loss-split-face-lineage`, and `edge-prismatic-x8-loss-topology-parity`.

## 10. Relationship to production `analyze map`

Production `StepAnalyzer.AnalyzeMap` and normal CLI `aetheris analyze map` are unchanged. X8 adds no production analyzer route, no default CLI route, and no experimental CLI command. The focused tests include explicit diagnostics:

- `edge-prismatic-x8-no-production-analyzer-behavior-changed`;
- `edge-prismatic-x8-no-default-cli-behavior-changed`;
- `edge-prismatic-x8-no-cir-to-brep-extraction`.

A future milestone can add a clean explicit route such as `aetheris experimental prismatic-map --case <case> --rows <n> --cols <n> --json`, but X8 intentionally stops at Core test scope.

## 11. Non-goals

X8 does not change or claim:

- production analyzer behavior;
- default CLI behavior;
- public APIs;
- STEP exporter/importer behavior;
- Boolean core behavior;
- BRep topology behavior;
- AIR emitter behavior;
- production prismatic route behavior;
- CIR-to-BRep extraction;
- topology identity, face IDs, loop IDs, or split-face lineage from CIR maps;
- imported STEP mirror inference;
- coplanar merge policy;
- gated artifact corpus test defaults.

## 12. Tests run

Focused validation:

```bash
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "EDGE-PRISMATIC-X8|PrismaticHybridMap"
```

Required regression sweeps for this milestone were run with the requested filters:

```bash
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "EDGE-PRISMATIC-X8|PrismaticHybridMap|CirPrismatic|CirMirror|CirMap|CIR|Cir|BrepPrimitives|BrepSpatialQueries|Raycast|Step242|Primitive|Boolean|SafeComposition"
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "Analyze|Map|CliBaseline|Step|Prismatic|AirChamfer|Experimental|Corpus"
dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "PrismaticSectionTransition|PrismaticTopEdgeChamfer|ProfileStackChamfer|ProfileChamfer|ProfileStack|LineArcProfileExtrude|Profile2D|AirChamfer|EdgeSweep|CIRLab"
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "FirmamentPrimitive|FirmamentStepExporter|SemanticRecovery|FrepMaterializer|Rematerialize|Chamfer|Fillet|Corner|ProfileStack|LineArcProfileExtrude"
```

The gated X6 artifact corpus stability body remains opt-in and is not made default by X8.

## 13. Recommended next milestone

Recommended next steps, in priority order:

1. **EDGE-PRISMATIC-X9 experimental CLI prismatic-map route** — expose this generated-source-only path under an explicit experimental command, preserving normal `analyze map` behavior.
2. **AIR-CIR-A1 mirror drift/parity policy** — define stale mirror detection and generated-source provenance checks before broader use.
3. **Chamfer/fillet selection taxonomy return** — use the mirror evidence to refine when prismatic section transitions, AIR edge sweeps, or legacy BRep routes should compete.

## EDGE-PRISMATIC-X9 CLI exposure note

EDGE-PRISMATIC-X9 exposes the X8 generated-source-only hybrid map proof through `aetheris experimental prismatic-map --case <case> --rows <n> --cols <n> --json`. The route is experimental, accepts only the generated `rectangle-inset` and `top-edge-chamfer` cases, rejects positional STEP input, and preserves the X8 authority boundary: normal `aetheris analyze map` remains unchanged, imported STEP bodies do not infer CIR mirrors, and CIR map summaries make no topology or face-identity claims.
