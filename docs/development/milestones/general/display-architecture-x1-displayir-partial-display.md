# DISPLAY-ARCH-X1 — DisplayIR partial display contract

## Purpose and scope

DISPLAY-ARCH-X1 introduces an explicit DisplayIR/ViewIR response contract for `display/prepare`. The goal is bounded partial display: a valid imported BRep body can still produce a usable display response when one face cannot be materialized by the bounded mesh lane.

This milestone does not rewrite the tessellator, implement CIR display, or add a new analytic GPU renderer. It wraps the existing analytic packet and mesh tessellation outputs as explicit DisplayIR lanes.

## Relationship to DISPLAY-ARCH-A0

DISPLAY-ARCH-A0 identified ambiguous display authority: BRep is geometry/topology truth, analytic display packets describe some faces, legacy tessellation acted as implicit fallback authority, and Three.js rendered whichever triangles were available.

X1 follows the A0 recommendation by making the server-emitted DisplayIR response the view authority while keeping BRep as source authority.

## Current problem

Before X1, `display/prepare` required fallback tessellation to succeed for mixed/fallback bodies. If one fallback face timed out, the endpoint returned a whole-body failure even though STEP import, BRep construction, and canonical export could be valid.

FTC-07 exposed this: bounded tessellation reported `Viewer.Tessellation.Timeout` for face materialization on a planar face with holes during `PlanarTriangulationWithHoles`, and that diagnostic became an HTTP 422 display preparation failure.

## DisplayIR contract

`DisplayPreparationResponseDto` now carries compatibility fields plus DisplayIR metadata:

- `sourceAuthority`: `BRep`.
- `displayAuthority`: `DisplayIR`.
- `status`: `Complete`, `Partial`, `DiagnosticOnly`, or `Failed`.
- `lanes`: emitted patch kinds such as `AnalyticPatch`, `MeshPatch`, and `DiagnosticPatch`.
- `faces`: per-face DisplayIR records.
- `diagnostics`: body/display-level diagnostics with stable face fields where available.

Each `DisplayFaceDto` records:

- `faceId`, optional `shellId`, and `surfaceKind`.
- `status`: `Analytic`, `Mesh`, `DiagnosticOnly`, or another explicit display status.
- `patchKind`: `AnalyticPatch`, `MeshPatch`, or `DiagnosticPatch` in X1.
- optional `analyticPatch` or `meshPatch` payload.
- per-face diagnostics.

Diagnostics include stable fields for code, message, face id, surface kind, phase, and suggested next action.

## Partial display semantics

`display/prepare` now returns success for a valid body when at least some display lanes materialize. Failed faces are represented explicitly as diagnostic faces instead of silently disappearing or failing the whole body.

If all faces fail, the response is still bounded and explicit (`DiagnosticOnly`) rather than an infinite spinner. X1 does not yet synthesize wireframe/proxy patches for all failed faces; `WireframeOnly` remains future work.

## FTC-07 behavior before/after

Before X1:

- STEP/AP242 import succeeded.
- Canonical export could succeed.
- Server import succeeded.
- `display/prepare` could return HTTP 422 because bounded fallback tessellation timed out on a single face.

After X1:

- Import remains success.
- `display/prepare` returns a DisplayIR response with BRep source authority and DisplayIR display authority.
- Materialized faces remain available.
- Timed-out faces are reported as `DiagnosticOnly` with `Viewer.Tessellation.Timeout`, face id, surface kind, and phase where available.

## API response changes

The legacy fields remain:

- `lane`
- `analyticPacket`
- `tessellationFallback`

New DisplayIR fields are additive:

- `status`
- `sourceAuthority`
- `displayAuthority`
- `lanes`
- `faces`
- `diagnostics`

## Frontend behavior changes

The client DTOs now accept the DisplayIR metadata and diagnostic-only faces. The viewer continues to render existing analytic preview sampling and mesh fallback data, but the UI can report import success separately from partial view materialization.

## What did not change

- BRep remains geometry/topology truth.
- STEP import/export semantics were not changed.
- Tessellator algorithms, including planar-with-holes triangulation, were not rewritten.
- Firmament V2 language/lowering was not changed.
- AIR Region route policy was not changed.
- CIR authority and CIR display were not changed.
- Firmasm and CAD feature behavior were not changed.

## Tests run

- `dotnet restore Aetheris.slnx`
- `dotnet build Aetheris.slnx -f net10.0 --no-restore`

## Next milestone recommendation

DISPLAY-ARCH-X2 should quarantine legacy tessellation behind an explicit bounded mesh lowering lane with quality/budget metadata and cleaner per-face cancellation semantics. If X2 absorbs most of that work quickly, DISPLAY-ARCH-X3 should focus on a robust planar face-with-holes display lane using FTC-07 face 9 as the motivating regression.

## X2 follow-up

DISPLAY-ARCH-X2 adds the explicit `BoundedMesh` DisplayIR lane for legacy BRep tessellation. The X1 compatibility fields remain, including `tessellationFallback`, but new lane metadata identifies mesh materialization as a bounded lowering from `BRep` into `DisplayIR` rather than a fallback display authority. See `docs/development/milestones/general/display-architecture-x2-bounded-mesh-lane.md`.
