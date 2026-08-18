# DISPLAY-ARCH-X2 — Explicit BoundedMesh DisplayIR lane

## 1. Purpose and scope

DISPLAY-ARCH-X2 quarantines legacy BRep display tessellation behind an explicit DisplayIR lowering lane named `BoundedMesh`. Tessellation remains available for rendering bounded faces, but it is no longer described by new DisplayIR metadata as implicit fallback authority or as a whole-body success/failure gate.

This milestone is intentionally additive: existing compatibility fields such as `tessellationFallback` remain available for old clients, while the authoritative DisplayIR metadata now exposes lane kind, source, implementation, quality, budget, status, and diagnostics.

## 2. Relationship to DISPLAY-ARCH-A0 and X1

DISPLAY-ARCH-A0 identified the display authority problem: BRep owns geometry/topology truth, DisplayIR should own view materialization, analytic DTOs are one display lane, and tessellation must not become hidden CAD truth.

DISPLAY-ARCH-X1 added partial DisplayIR semantics to `display/prepare`: response status, source/display authority fields, lane summaries, per-face records, stable diagnostics, and FTC-07 partial-display behavior.

X2 builds on X1 by making the mesh-producing path a named lowering lane: `BoundedMesh`.

## 3. Why tessellation is not fallback authority

Tessellation is a view materialization technique. It can approximate displayable face interiors, and it can fail per face because of bounded execution budgets or unsupported trim/surface cases. It does not replace BRep topology, STEP import/export, analytic display records, CIR, or any CAD feature authority.

Therefore `display/prepare` now reports tessellation as `BoundedMesh` with `source = BRep` and `displayAuthority = DisplayIR`. A failed mesh face becomes a diagnostic display face; it does not invalidate the imported body.

## 4. BoundedMesh lane contract

`BoundedMesh` means: lower BRep faces into bounded mesh patches for display only.

The contract is:

- source authority: `BRep`;
- display authority: `DisplayIR`;
- implementation: currently `BrepDisplayTessellator` through the existing display preparation mesh path;
- status: `Complete`, `Partial`, or `Failed` based on per-face materialization;
- success result: face mesh patches remain renderable;
- failure result: face diagnostics remain visible and attached to diagnostic-only face records;
- no single face failure may force whole-body display failure if other faces materialized.

## 5. Lane metadata

`DisplayPreparationResponseDto.lanes` remains the compatibility list of lane names. `DisplayPreparationResponseDto.displayLanes` carries structured lane metadata for the explicit DisplayIR lanes.

For `BoundedMesh`, the metadata includes:

- `kind`: `BoundedMesh`;
- `source`: `BRep`;
- `displayAuthority`: `DisplayIR`;
- `implementation`: `BrepDisplayTessellator`;
- `quality`: currently `Default`;
- `timeoutMs`: the current default bounded execution budget, when reported by the API;
- `faceCount`: successfully materialized mesh face count;
- `diagnosticCount`: lane diagnostics count.

Per-face `DisplayFaceDto.materializationLane` distinguishes `AnalyticPatch`, `BoundedMesh`, and `DiagnosticOnly` records.

## 6. FTC-07 behavior

FTC-07 remains a partial display case. The import succeeds, successful display faces remain present, and the timed-out planar face becomes a diagnostic-only face with:

- `materializationLane = BoundedMesh`;
- diagnostic code `Viewer.Tessellation.Timeout`;
- phase `PlanarTriangulationWithHoles` or the narrower phase reported by the tessellator.

This preserves X1's partial DisplayIR behavior while naming the failed lowering lane explicitly.

## 7. API/DTO changes

Additive DTO changes:

- `DisplayPreparationResponseDto.lanes` includes compatibility lane-name strings such as `AnalyticPatch`, `BoundedMesh`, and `DiagnosticOnly`.
- `DisplayPreparationResponseDto.displayLanes` contains structured `DisplayLaneDto` records.
- `DisplayLaneDto` records lane kind, status, source, display authority, implementation, quality, timeout, face count, and diagnostic count.
- `DisplayFaceDto.materializationLane` records which lane produced or attempted the face materialization.

Compatibility notes:

- `lane` remains the legacy coarse value (`analytic-only`, `mixed-fallback`, `fallback-only`) for old clients.
- `lanes` remains a string list for old clients, with `BoundedMesh` added when mesh lowering is used.
- `tessellationFallback` remains the mesh compatibility payload for old clients.
- New DisplayIR authority fields do not call tessellation the display authority; they keep `displayAuthority = DisplayIR`.

## 8. Frontend behavior

The client DTOs accept structured lane metadata and per-face materialization lanes. The inspector continues rendering existing analytic and mesh paths, but partial-display text now distinguishes import success from bounded mesh materialization failure.

Diagnostic-only faces from `BoundedMesh` are display diagnostics, not import failures.

## 9. What did not change

This milestone did not change:

- tessellation algorithms;
- `PlanarTriangulationWithHoles` behavior;
- STEP import/export semantics;
- BRep topology;
- Firmament V2 language or lowering;
- AIR Region route policy;
- CIR authority;
- Firmasm;
- CAD feature behavior.

## 10. Tests run

Validation for this milestone included solution restore/build, targeted kernel/server DisplayIR and tessellation tests, client App tests, FTC-06 same-sense regression tests, and FTC-07 CLI smoke commands.

## 11. Next milestone recommendation

Recommended next milestone:

`DISPLAY-ARCH-X3 — planar face with holes display lane rewrite using FTC-07 face 9`

If immediate user-facing resilience is preferred first, use:

`DISPLAY-ARCH-X3 — wireframe/edge-first diagnostic fallback for unsupported/timed-out faces`

## X3 follow-up

DISPLAY-ARCH-X3 narrows the planar multi-loop `BoundedMesh` lane by adding explicit planar loop classification before `PlanarTriangulationWithHoles`. Unsupported nesting and degenerate loops now produce stable face-local planar triangulation diagnostics while preserving the X2 DisplayIR authority split.
