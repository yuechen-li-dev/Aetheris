# CIR-MAP-X1 — CIR-backed primitive map prototype

Status: **Lab/test-only prototype** (no production analyzer or CLI behavior change).

## 1. Purpose and scope

CIR-MAP-X1 proves that CIR/FRep can serve as an orthographic map backend for simple primitive mirrors whose field semantics are obvious and explicitly admitted:

1. box;
2. cylinder;
3. sphere.

The prototype is intentionally kept in focused test/lab code under `Aetheris.Kernel.Core.Tests`. It does not alter the production `analyze map` command, `StepAnalyzer`, STEP import/export, Boolean code, BRep topology, AIR emitters, CIR node kinds, or CIR-to-BRep extraction. AIR-CIR-X1 builds on this by adding internal admission metadata/status diagnostics that future mirror-aware dispatch can consume; CIR-MAP-X1 behavior itself remains lab-only and unchanged.

## 2. Authority contract reference

This milestone follows the AIR-CIR-A0 authority split:

- AIR remains authoritative for construction intent.
- BRep remains authoritative for explicit topology and STEP export.
- CIR/FRep is authoritative only for admitted field/evaluation questions.

The map prototype treats primitive CIR mirrors as explicit, scoped, and diagnostic field mirrors. It does not infer CIR mirrors from arbitrary STEP imports.

## 3. Why primitive mirrors first

Box, cylinder, and sphere are first because all three already exist as CIR primitive fields and as `BrepPrimitives` bodies accepted by `BrepSpatialQueries.Raycast`. That gives a bounded parity target:

- CIR can answer field sign questions along sample rays.
- BRep raycast can provide the current explicit-topology baseline for the same sample grid.
- The comparison can avoid prismatic feature reconstruction, STEP intent inference, and general planar-shell raycast expansion.

Prismatic mirrors are deliberately excluded from X1.

## 4. CIR map algorithm

### 4.1 Backend choice: tape first, node as lowering source

The lab evaluator accepts a `CirNode` by lowering it through `CirTapeLowerer.Lower(...)`, and also accepts an already-lowered `CirTape` with explicit bounds. X1 therefore exercises the tape runtime for map sampling while preserving `CirNode` as the semantic builder/oracle surface.

Diagnostic emitted by the test prototype:

- `cir-map-x1-backend-selected:cir-tape`

### 4.2 Grid and view policy

The prototype supports orthographic top, bottom, front, back, left, and right views over explicit CIR bounds. For each row/column sample, it uses the same center-of-cell policy as the production BRep map path:

```text
u = minU + (col + 0.5) / cols * (maxU - minU)
v = minV + (row + 0.5) / rows * (maxV - minV)
```

The ray starts on the near side of the primitive bounds and advances to the far side in the selected view direction.

### 4.3 Sampling, root, and tolerance policy

For each sample ray, the evaluator:

1. samples the CIR field at a deterministic number of evenly spaced depths;
2. treats field values `<= tolerance` as inside;
3. detects outside-to-inside and inside-to-outside sign transitions;
4. refines transition depths by fixed-iteration bisection;
5. reports occupancy and approximate thickness as `exitDepth - entryDepth`.

Current X1 test policy:

- `SamplesPerRay = 384` for parity tests;
- `RootRefinementIterations = 32`;
- `Tolerance = 1e-7`;
- BRep/CIR thickness summary tolerance: `0.075` model units.

This is intentionally deterministic and correctness-first rather than an optimized production map implementation.

## 5. Primitive cases tested

Required X1 cases covered by tests:

- box top view;
- box front view;
- cylinder top view;
- cylinder front view;
- sphere top view;
- sphere front view.

A separate box top test asserts exact full occupancy and exact constant thickness for an axis-aligned box sample grid.

## 6. BRep raycast baseline comparison

For each primitive/view case, the test builds the matching `BrepPrimitives` body and runs `BrepSpatialQueries.Raycast` over the same bounds, view, rows, cols, and center-sample grid. The baseline uses the same near-side epsilon convention as the CLI map path and compares stable summary fields rather than requiring every curved-boundary depth to be bit-for-bit identical.

Compared fields:

- total sample count;
- hit/empty sample count;
- minimum thickness;
- maximum thickness;
- average thickness.

Box occupancy is exact for the selected grids. Cylinder and sphere occupancy also match the BRep baseline for the selected deterministic grids; thickness is compared within the documented tolerance because curved boundaries and BRep/CIR entry conventions are independent implementations.

## 7. Mirror statuses and diagnostics

The prototype uses AIR-CIR-A0 vocabulary and deterministic diagnostics.

Mirror status used for admitted primitives:

- `mirror-admitted-exact`

Unsupported/prismatic diagnostic surface:

- `mirror-unavailable`
- `mirror-rejected-lossy-for-request`

X1 diagnostics include:

- `cir-map-x1-lab-started`
- `cir-map-x1-mirror-admitted-exact:<primitive>`
- `cir-map-x1-backend-selected:cir-tape`
- `cir-map-x1-brep-raycast-baseline-created:<primitive>`
- `cir-map-x1-cir-map-created:<primitive>`
- `cir-map-x1-map-parity-succeeded:<primitive>`
- `cir-map-x1-map-parity-warning:<primitive>:<reason>` for unsupported mirrors
- `cir-map-x1-no-prismatic-mirror-used`
- `cir-map-x1-no-production-analyzer-behavior-changed`

## 8. Limitations

CIR-MAP-X1 does **not** provide:

- prismatic mirror support;
- arbitrary STEP-to-CIR map support;
- production analyzer dispatch changes;
- default CLI behavior changes;
- public API changes;
- STEP exporter/importer changes;
- Boolean core changes;
- BRep topology changes;
- AIR emitter behavior changes;
- new CIR node kinds;
- CIR-to-BRep extraction.

The prototype is a proof that admitted primitive CIR mirrors can answer map-like field questions and can be checked against the existing primitive BRep raycast path.

## 9. Recommended next milestones

- **CIR-MAP-X2:** harden the tape-backed map evaluator, move toward a non-test internal lab package if needed, and evaluate interval/early-out acceleration.
- **AIR-CIR-X1:** prototype explicit mirror metadata so generated AIR bodies can declare admitted CIR mirrors without STEP inference.
- **CIR-PRISMATIC-X1:** investigate whether prismatic field mirrors are feasible without losing construction/topology authority or making unsupported map claims.
