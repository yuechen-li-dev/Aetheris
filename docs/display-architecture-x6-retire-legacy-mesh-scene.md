# DISPLAY-ARCH-X6 — Retire Legacy Mesh-Scene Assumptions from the Frontend Viewer

## 1. Purpose and scope

DISPLAY-ARCH-X6 makes `DisplayScene` / `DisplayRenderable` the primary frontend scene contract. The work is intentionally frontend-scoped: API DTOs are mapped into typed renderables before reaching `AetherisViewport`, and any older tessellation fallback payload is treated as compatibility input only.

## 2. Relationship to X5 and DISPLAY-QA-X2

DISPLAY-ARCH-X5 introduced typed DisplayIR records and renamed the viewport to `AetherisViewport`. DISPLAY-QA-X2 then fixed imported-body framing/status behavior by fitting to typed DisplayIR renderables. X6 follows those milestones by removing the remaining frontend habit of treating legacy mesh scene data as an equal source of truth.

## 3. What legacy mesh-scene assumptions remained

The remaining assumptions were:

- `buildDisplaySceneData` returned both typed `DisplayScene` and legacy `RenderSceneData` as parallel render contracts.
- `AetherisViewport` accepted legacy scene data as a required prop and rendered it when no typed scene was present.
- App inspector counts still used tessellation fallback face/edge counts.
- Tessellation fallback conversion lived in the generic scene builder rather than in a named compatibility adapter.

## 4. What was removed

- The app no longer stores tessellation fallback state as the active viewer scene.
- `AetherisViewport` no longer requires legacy `RenderSceneData` to render.
- The primary build result no longer exposes `sceneData` as a peer scene contract.
- Inspector mesh counts now derive from `DisplayScene` renderables first.

## 5. What remains as compatibility adapter

`legacyTessellationToDisplayScene` and `legacyFacePatchToMeshRenderable` remain as explicit compatibility adapters for old `/tessellate`-style or `tessellationFallback` payloads that have no typed DisplayIR faces. They convert legacy face patches into typed `MeshPatch` renderables and tag the scene with `legacyCompatibility` metadata.

## 6. New frontend scene flow

```text
API DTOs -> DisplayScene -> DisplayRenderable[] -> AetherisViewport -> Three.js primitives
```

Typed DisplayIR faces win whenever present. Legacy tessellation fallback is only used when no typed faces are available.

## 7. AetherisViewport responsibilities

`AetherisViewport` is now a dumb renderer for typed renderables:

- `AnalyticPatch` renders through its preview mesh.
- `MeshPatch` / `BoundedMesh` renders mesh arrays.
- `WirePatch` renders edge polylines.
- `DiagnosticPatch` is accepted without inventing geometry.

It does not decide display success from mesh patch counts and does not infer fallback semantics from old lane strings.

## 8. Three.js backend role

Three.js remains the backend primitive renderer. It receives already-classified renderables and is not responsible for scene authority, display lane selection, or fallback policy.

## 9. Tests run

- `npm test -- --run displayRenderables.test.ts AetherisViewport.test.tsx displaySceneBuilder.test.ts App.test.tsx` after dependency installation.
- `npm run build`.

Full .NET validation and CLI smoke were also run for this change set when preparing the PR.

## 10. What did not change

- STEP import/export semantics.
- BRep topology.
- Server DisplayIR authority.
- Tessellator algorithms.
- CIR authority.
- Firmament V2 language/lowering.
- AIR Region route policy.
- Firmasm.
- FTC-07 planar triangulation.
- CAD feature behavior.

## 11. Next milestone recommendation

Recommended next milestone: `DISPLAY-ARCH-X7 — remove deprecated tessellationFallback frontend compatibility once server clients are migrated`.
