# DISPLAY-QA-X2: imported-body framing and display status

## 1. Purpose and scope

DISPLAY-QA-X2 fixes the frontend/viewer regression identified in DISPLAY-QA-X1 where FTC-06 imported and prepared display data successfully, but the default viewport framing was so wrong that the model was effectively off-screen. This milestone also fixes the local client production build break in `aetheris.client/src/App.tsx` and promotes mixed/partial display status into the main visible UI.

Scope stayed intentionally narrow:

- frontend DisplayIR-aware bounds and camera fit;
- frontend import/display status messaging;
- frontend TypeScript build repair;
- local FTC-06 / FTC-07 visual QA rerun.

This milestone did not change STEP import/export semantics, BRep topology, DisplayIR server authority, tessellator algorithms, Firmament V2 language/lowering, AIR Region route policy, CIR authority, Firmasm, or CAD feature behavior.

## 2. Relationship to DISPLAY-QA-X1

DISPLAY-QA-X1 established that FTC-06 was not an import-loss problem:

- FTC-06 import succeeded.
- `/display/prepare` succeeded as `mixed-fallback`.
- Backend facts were `161` analytic faces plus `26` fallback faces, `187` total.
- The UI still showed only the drafting grid until extreme manual zoom-out, and even then the model was clipped and offset.

DISPLAY-QA-X2 resolves that frontend framing defect and reruns the local pass with the same FTC corpus files.

## 3. Root cause of FTC-06 bad framing

The root cause was the viewport camera path, not the importer or kernel:

- `aetheris.client/src/viewer/AetherisViewport.tsx` mounted a fixed orthographic camera at `[6, 6, 6]` with a fixed zoom and no fit-to-scene pass.
- The typed DisplayIR path in `displaySceneBuilder.ts` preferred `DisplayScene` renderables and often left legacy `sceneData` null.
- Because the viewport never computed bounds from typed DisplayIR renderables, imported bodies could exist correctly in scene space while the camera target and zoom remained pinned to legacy assumptions around the origin.

In practice, FTC-06 geometry was present, but the initial camera target/zoom was not derived from the imported model's actual bounds.

## 4. Fix

### DisplayIR-aware bounds

Added `aetheris.client/src/viewer/displaySceneBounds.ts` with:

- `computeDisplaySceneBounds(displayScene, sceneData)`
- `computeOrthographicCameraFit(bounds, frustumWidth, frustumHeight, viewDirection)`

Bounds now consider typed DisplayIR geometry first:

- `AnalyticPatch`: analytic preview mesh vertex positions
- `MeshPatch`: bounded-mesh vertex positions
- `WirePatch`: wire polyline points
- `DiagnosticPatch`: ignored unless geometry exists elsewhere

If typed renderables have no geometry, the code falls back to legacy `RenderSceneData`.

### Camera fit

`AetherisViewport.tsx` now mounts a `FitCameraToScene` helper that:

- recomputes bounds from typed DisplayIR renderables or legacy scene fallback;
- centers the orbit target on the bounds center;
- repositions the orthographic camera along the existing isometric viewing direction;
- computes a conservative orthographic zoom from projected bounds size;
- updates `near` / `far` to avoid clipping the imported body.

This is intentionally conservative and does not redesign the viewer camera model.

### Status UI

`App.tsx` now emits first-class import/display summaries:

- mixed fallback: `Import complete. Display: mixed analytic + bounded mesh fallback.`
- partial display: `Import complete. Display partial: N wire-only face(s), M diagnostic-only face(s).`
- materialization failure remains separate from import failure.

Mixed fallback is treated as success, not failure.

## 5. Client build break fix

The local production build failure in `aetheris.client/src/App.tsx` around the old lines `393/394` came from the import flow's mutable `displayError` control flow and TypeScript narrowing.

The import flow was simplified so it now:

- performs the import path directly inside `handleImportStep`;
- uses a structured `RefreshDisplayResult` from `refreshSummaryAndActiveTessellation`;
- sets import success, partial-display, and materialization-failure messaging explicitly.

`npm run build` now passes.

## 6. FTC-06 before/after

### Backend facts

- Import: success
- `/display/prepare`: success
- Display lane: `mixed-fallback`
- Display status: `Complete`
- Analytic faces: `161`
- Fallback faces: `26`
- Face count: `187`
- Edge count: `476`

### UI facts after X2

- The body is visible in the default imported view.
- The body is centered and framed without extreme manual zoom-out.
- The main UI now says `Import complete. Display: mixed analytic + bounded mesh fallback.`
- The inspector still matches backend facts.

### Artifact paths

- Viewport screenshot: [ftc06-imported-display-fixed.png](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-qa-x2/ftc06-imported-display-fixed.png)
- Status screenshot: [ftc06-display-status-fixed.png](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-qa-x2/ftc06-display-status-fixed.png)
- Display response capture: [ftc06-display-prepare.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-qa-x2/ftc06-display-prepare.json)

## 7. FTC-07 smoke status

FTC-07 no longer reproduced the old DISPLAY-QA-X1 local outcome during this X2 rerun.

Observed local smoke result:

- Import: success
- `/display/prepare`: success (`200`)
- Display lane: `mixed-fallback`
- Display status: `Partial`
- Main UI message: `Import complete. Display partial: 210 wire-only face(s), 0 diagnostic-only face(s).`
- Analytic faces: `96`
- Fallback faces: `210`
- Inspector face count: `8`
- Edge count: `871`

This is a changed local smoke result, not a frontend framing change. X2 did not modify server-side display semantics or tessellation algorithms.

Artifact paths:

- Viewport screenshot: [ftc07-imported-display-smoke.png](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-qa-x2/ftc07-imported-display-smoke.png)
- Status screenshot: [ftc07-display-status-smoke.png](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-qa-x2/ftc07-display-status-smoke.png)
- Display response capture: [ftc07-display-prepare.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-qa-x2/ftc07-display-prepare.json)

## 8. Tests run

Frontend:

```powershell
cd aetheris.client
npm test -- --run src/__tests__/App.test.tsx src/__tests__/displayRenderables.test.ts src/__tests__/displaySceneBuilder.test.ts src/__tests__/AetherisViewport.test.tsx src/__tests__/displaySceneBounds.test.ts
npm run build
```

Solution / server-core smoke:

```powershell
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "DisplayIR|BoundedMesh|WirePatch|DisplayPrepare|ViewMaterialization|FTC07|Ftc07|Tessellation|FTC06|Ftc06" --logger "console;verbosity=minimal"
dotnet test Aetheris.Server.Tests/Aetheris.Server.Tests.csproj -f net10.0 --no-build --filter "DisplayIR|BoundedMesh|WirePatch|DisplayPrepare|FTC07|Ftc07|FTC06|Ftc06|KernelApi|ViewMaterialization|Tessellate|Tessellation|StepIo" --logger "console;verbosity=minimal"
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "Step242Ftc06SameSenseRegressionTests|FTC06|Ftc06|same_sense" --logger "console;verbosity=minimal"
dotnet run --project Aetheris.CLI -f net10.0 -- analyze testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp --json
dotnet run --project Aetheris.CLI -f net10.0 -- analyze testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp --json
```

Local visual QA:

```powershell
node scripts/display-qa-x2-playwright.mjs
```

The local rerun used the same script with `AETHERIS_QA_CASE=ftc06` and `AETHERIS_QA_CASE=ftc07` so each capture ran against a fresh server/client pair.

## 9. Remaining limitations

- Camera fit is still an orthographic, isometric, geometry-bounds fit; it is not a full viewer-camera redesign.
- Diagnostic-only faces without geometry remain intentionally excluded from bounds.
- FTC-07 changed locally from X1's materialization failure to a partial mixed-fallback result, but X2 does not explain or claim ownership of that backend behavior change.

## 10. Next milestone recommendation

Recommended next step: a focused FTC-07 follow-up that explains the changed local smoke result and stabilizes the intended server-side behavior for planar / wire degradation reporting.

If the team wants to stay in frontend scope first, the next small slice would be viewer polish around partial-display highlighting and optional explicit wireframe emphasis, not more camera work.
