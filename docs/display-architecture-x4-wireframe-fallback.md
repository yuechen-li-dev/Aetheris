# DISPLAY-ARCH-X4 — Wireframe fallback for unsupported faces

## 1. Purpose and scope

DISPLAY-ARCH-X4 makes DisplayIR degradation more useful when a BRep face cannot be filled by analytic or bounded mesh materialization. A failed fill can now degrade to a `WireframeOnly` face with a `WirePatch` sourced from existing BRep loops/coedges/edges, so the viewer receives visible diagnostic boundary geometry instead of only a diagnostic record.

The scope is intentionally limited to the BRep display/view path, DisplayIR response shape, server-side edge polyline extraction, and minimal frontend acceptance/status reporting.

## 2. Relationship to A0/X1/X2/X3

- A0 identified that BRep owns geometric/topological source authority while DisplayIR owns display authority.
- X1 introduced partial DisplayIR response semantics and per-face display records.
- X2 made legacy tessellation explicit as the bounded `BoundedMesh` lane rather than hidden fallback authority.
- X3 improved planar loop classification and planar-with-holes behavior, especially FTC-07 boundedness.
- X4 adds the next display degradation step after failed fill: `WireframeOnly`/`WirePatch`.

## 3. Display degradation ladder

The intended per-face display ladder is now:

1. `AnalyticPatch`
2. `BoundedMesh`
3. `WireframeOnly`
4. `DiagnosticOnly`

X4 does not implement a general proxy patch system.

## 4. Wireframe DisplayIR contract

A wire fallback face is emitted as:

- `status = WireframeOnly`;
- `patchKind = WirePatch`;
- `materializationLane = WirePatch`;
- `sourceAuthority = BRep` at the response level;
- `displayAuthority = DisplayIR` at the response and lane levels;
- a `wirePatch` containing preview loops and edge polylines.

The `WirePatch` DTO contains `kind`, `source = BRepEdges`, `quality = PreviewPolyline`, and loop records. Each loop carries a loop id, a best-effort role (`Outer` for first loop, `Inner` for later loops), and edge records. Each edge record carries edge id, sampled points, source curve kind, sample count, and edge-local diagnostics.

## 5. Server wire extraction behavior

Server extraction is bounded and topology-driven:

- iterate face loops;
- iterate loop coedges;
- resolve coedge edge ids;
- sample line curves at trim interval endpoints;
- sample circle curves with a capped preview count;
- fall back to vertex endpoint points when available;
- report unsupported curve kinds as `Viewer.Wireframe.UnsupportedCurve` without blocking the response.

The extraction is a display approximation only; it does not change BRep topology or curve semantics.

## 6. Frontend behavior

The client API type now accepts `wirePatch` on `DisplayFaceDto`. The app inspector distinguishes wire-only and diagnostic-only face counts and reports partial display as a display degradation, not as an import failure.

X4 keeps filled face rendering unchanged. Full Three.js typed rendering cleanup remains a follow-up milestone.

## 7. Diagnostics

X4 adds/stabilizes these display diagnostics:

- `Viewer.Display.WireframeOnly` when a failed fill is represented by boundary wireframe DisplayIR.
- `Viewer.Wireframe.UnsupportedCurve` when an edge curve cannot be sampled by the conservative preview sampler.

Existing fill diagnostics, including `Viewer.Tessellation.Timeout`, `Viewer.PlanarTriangulation.*`, and `Viewer.Display.FaceMaterializationFailed`, remain attached and are not replaced by wireframe diagnostics.

## 8. FTC-07 status after X4

FTC-07 display preparation remains a bounded DisplayIR operation. If a face cannot fill but has BRep boundary edges, the response can surface it as `WireframeOnly`; otherwise it remains diagnostic-only. Import/export success remains separate from display partial status.

## 9. What did not change

X4 does not change:

- STEP import/export semantics;
- AP242 importer/exporter behavior;
- BRep topology;
- tessellator algorithms;
- CIR authority;
- Firmament V2 language/lowering;
- AIR Region route policy;
- Firmasm;
- CAD feature behavior.

## 10. Tests run

Validation for this branch includes solution restore/build, targeted DisplayIR/tessellation tests, server DisplayIR tests, client App tests, FTC-06 regression, and FTC-07 CLI smoke commands. Any local MSBuild file-lock failures should be treated as build hygiene rather than a DisplayIR semantic failure when a serialized rebuild passes.

## 11. Next milestone recommendation

Recommended next milestone:

```text
DISPLAY-ARCH-X5 — Three.js DisplayIR renderer cleanup
```

Goal: make the frontend renderer consume typed DisplayIR records directly (`AnalyticPatch`, `BoundedMesh`, `WireframeOnly`, `DiagnosticOnly`) instead of treating every successful display as mesh scene data.

If wire extraction becomes the bottleneck, use the alternate milestone:

```text
DISPLAY-ARCH-X5 — BRep edge polyline sampler hardening
```

## X5 follow-up

DISPLAY-ARCH-X5 moved the frontend toward typed DisplayIR rendering. `WirePatch` records are now consumed as their own renderable kind by `AetherisViewport` instead of being treated as exceptions to a mesh-scene model.
