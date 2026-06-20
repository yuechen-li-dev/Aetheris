# DISPLAY-ARCH-X5 — Three.js DisplayIR renderer cleanup

## 1. Purpose and scope

X5 cleans up the frontend viewer contract so the React viewport consumes typed DisplayIR renderables first and uses Three.js as a rendering adapter. The scope is limited to frontend DTO mapping, viewport component naming, DisplayIR render lanes, focused tests, and documentation.

## 2. Relationship to A0/X1/X2/X3/X4

- A0 identified the authority split: BRep owns geometry/topology truth, DisplayIR owns view records, and Three.js should only render those records.
- X1 introduced partial DisplayIR responses and per-face status/diagnostics.
- X2 made tessellated display an explicit bounded `BoundedMesh` lane.
- X3 narrowed planar-with-holes materialization without changing CAD truth.
- X4 added `WirePatch` / `WireframeOnly` degradation so failed filled faces can still show boundary wires.

X5 is the frontend counterpart: it stops treating a mesh scene as the only top-level display shape when typed DisplayIR records are present.

## 3. Why Three.js is a backend, not display authority

Three.js primitives are implementation details for browser rendering. A mesh, line, or empty diagnostic marker in Three.js does not decide CAD truth, source authority, display authority, or fallback status. Those decisions arrive from DisplayIR fields such as `sourceAuthority`, `displayAuthority`, `displayLanes`, per-face `patchKind`, per-face `status`, `materializationLane`, and diagnostics.

## 4. New frontend DisplayIR rendering flow

The frontend now maps:

```text
DisplayPreparationResponseDto
  -> DisplayScene
  -> DisplayRenderable[]
  -> AetherisViewport
  -> Three.js mesh/line/no-geometry adapters
```

Legacy `tessellationFallback` and `RenderSceneData` compatibility remain available when no typed face records are present. New typed face records are preferred.

## 5. Component/file rename

- Old name: `aetheris.client/src/viewer/ViewerViewport.tsx`
- New name: `aetheris.client/src/viewer/AetherisViewport.tsx`
- Rationale: the viewport is an Aetheris DisplayIR consumer, not a generic viewer or a Three.js-owned scene component. The exported props were similarly renamed from `ViewerViewportProps` to `AetherisViewportProps`.

## 6. Typed renderable kinds

- `AnalyticPatch`: preserved as an analytic DisplayIR lane and lowered through the existing preview sampler for Three.js.
- `MeshPatch` / `BoundedMesh`: rendered as straightforward Three.js `BufferGeometry` meshes, while preserving that this is a bounded display lane rather than geometry truth.
- `WirePatch`: rendered as line primitives from edge polyline samples and accepted without mesh arrays or normals.
- `DiagnosticPatch`: kept as a renderable/status record with diagnostics and no required geometry.

## 7. What still remains approximate

Analytic patches are still sampled into preview triangles for Three.js. X5 isolates that as an analytic preview rendering adapter; it does not implement a full analytic GPU renderer.

## 8. Tests run

Validation for this change includes client App tests, typed DisplayIR mapper tests, viewport render-path tests, solution restore/build, targeted Kernel/Core DisplayIR tests, targeted Server DisplayIR tests, FTC-06 regression tests, and FTC-07 CLI smoke commands.

## 9. What did not change

X5 did not change STEP import/export semantics, AP242 importer/exporter behavior, BRep topology, tessellator algorithms, CIR display or authority, Firmament V2 language/lowering, AIR Region route policy, Firmasm, DisplayIR server authority, or CAD feature behavior.

## Suggested next milestone

DISPLAY-ARCH-X6 — typed DisplayIR API cleanup / remove legacy render-scene assumptions.

## X5.1 follow-up

DISPLAY-ARCH-X5.1 resolved the imported-occurrence server validation issue by documenting and testing the distinction between direct bounded `/tessellate` mesh lowering and DisplayIR-authoritative `/display/prepare`. The frontend typed renderable architecture from X5 remains unchanged.
