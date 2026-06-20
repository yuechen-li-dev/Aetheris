# DISPLAY-QUALITY-X1 — FTC-06 / FTC-07 post-DisplayScene QA

## 1. Purpose and scope

This report records a post-`DisplayScene` frontend stabilization QA pass for FTC-06 and FTC-07 after `DISPLAY-ARCH-X6` retired the legacy frontend mesh-scene contract.

This milestone was treated as a stabilization and evidence-gathering pass only. No backend semantic changes were intended, and no product behavior was intentionally changed during this milestone.

Follow-up note: `CLIENT-BUILD-FIX-X1` resolved the frontend merge-marker blocker found during this pass and restored a clean production client build from the same branch line.

## 2. Environment

- OS: Microsoft Windows 11 Pro 64-bit, version `10.0.26200`, build `26200`
- Shell: PowerShell `7.6.2`
- .NET SDK: `10.0.301`
- Node: `v26.2.0`
- npm: `11.13.0`
- Browser used for blocker verification: Microsoft Edge `149.0.4022.69`
- Standalone Playwright script: not run as a grounded UI pass because the current client worktree did not compile cleanly

## 3. Exact files tested

- `testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp`
- `testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp`

## 4. Build/test baseline

Commands run:

```bash
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1
cd aetheris.client
npm run build
npm test -- --run App.test.tsx displayRenderables.test.ts AetherisViewport.test.tsx displaySceneBuilder.test.ts displaySceneBounds.test.ts
```

Results:

- `dotnet restore Aetheris.slnx`: passed
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: passed
- `npm run build`: failed in the current worktree because `aetheris.client/src/App.tsx`, `src/viewer/AetherisViewport.tsx`, `src/viewer/displaySceneBounds.ts`, and `src/__tests__/displaySceneBounds.test.ts` still contain merge conflict markers
- `npm test -- --run ...`: failed for the same reason; `displayRenderables.test.ts` and `displaySceneBuilder.test.ts` passed, while `App.test.tsx`, `AetherisViewport.test.tsx`, and `displaySceneBounds.test.ts` failed during transform on merge markers

Focused display/server test commands:

```bash
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "DisplayIR|BoundedMesh|WirePatch|DisplayPrepare|ViewMaterialization|FTC07|Ftc07|Tessellation|FTC06|Ftc06" --logger "console;verbosity=minimal"

dotnet test Aetheris.Server.Tests/Aetheris.Server.Tests.csproj -f net10.0 --no-build --filter "DisplayIR|BoundedMesh|WirePatch|DisplayPrepare|FTC07|Ftc07|FTC06|Ftc06|KernelApi|ViewMaterialization|Tessellate|Tessellation|StepIo" --logger "console;verbosity=minimal"
```

Results:

- `Aetheris.Kernel.Core.Tests`: passed, `91/91`
- `Aetheris.Server.Tests`: passed, `33/33`

Additional regression check:

```bash
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "Step242Ftc06SameSenseRegressionTests|FTC06|Ftc06|same_sense" --logger "console;verbosity=minimal"
```

- FTC-06 regression slice: passed, `6/6`

## 5. Backend facts: FTC-06

CLI / API artifacts:

- Analyze JSON: [ftc06-analyze.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc06-analyze.json)
- Canon JSON: [ftc06-canon.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc06-canon.json)
- API import JSON: [ftc06-api-import.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc06-api-import.json)
- API display/prepare JSON: [ftc06-api-display-prepare.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc06-api-display-prepare.json)
- Compact backend summary: [ftc06-backend-summary.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc06-backend-summary.json)

Observed facts:

- Analyze result: success
- Canon result: success
- UI/API import result: success
- `display/prepare` status: success, `Complete`
- DisplayIR status: emitted
- `sourceAuthority`: `BRep`
- `displayAuthority`: `DisplayIR`
- Display lanes: `AnalyticPatch`, `BoundedMesh`
- Renderable counts by kind:
  - `AnalyticPatch`: `161`
  - `MeshPatch / BoundedMesh`: `26`
  - `WirePatch`: `0`
  - `DiagnosticPatch`: `0`
- Analyze face count: `187`
- Analyze edge count: `476`
- Diagnostic count: `0`
- Lane summary:
  - `AnalyticPatch`: `Complete`, `161` faces
  - `BoundedMesh`: `Complete`, `26` faces

Interpretation:

- FTC-06 backend still lands in the healthy `mixed-fallback` path observed after X2.
- No backend diagnostic evidence suggests clipping, omission, or display degradation for FTC-06.

## 6. UI facts: FTC-06

Expected screenshot targets:

- `artifacts/display-quality-x1/ftc06-default-view.png`
- `artifacts/display-quality-x1/ftc06-status-inspector.png`
- `artifacts/display-quality-x1/ftc06-angle-1.png`
- `artifacts/display-quality-x1/ftc06-angle-2.png`

Current result:

- A grounded fresh-client FTC-06 UI pass could not be completed.
- A fresh Vite instance on `https://127.0.0.1:43117/` hit a compile-time overlay before the app shell rendered because the current worktree contains unresolved merge markers in `aetheris.client/src/App.tsx` and related DisplayScene viewer files.
- Existing listeners on `5173` were present before this pass and were treated as stale local runtime state, not acceptable evidence for post-X6 UI QA.

Available UI blocker artifact:

- Fresh-client Vite overlay screenshot: [client-43117-vite-overlay.png](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/client-43117-vite-overlay.png)

What could not be verified from a fresh client build:

- whether FTC-06 is visible by default
- whether camera framing remains fixed
- whether any geometry is clipped
- whether shading/materials look sane
- whether fallback faces are visibly different
- whether wire/diagnostic state is visible
- whether UI status matches backend facts

Best grounded statement for this milestone:

- FTC-06 backend evidence remains healthy and consistent with X2/X6 expectations, but the current frontend worktree was not in a runnable state for a fresh visual confirmation.

## 7. Backend facts: FTC-07

CLI / API artifacts:

- Analyze JSON: [ftc07-analyze.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc07-analyze.json)
- Canon JSON: [ftc07-canon.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc07-canon.json)
- API import JSON: [ftc07-api-import.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc07-api-import.json)
- API display/prepare JSON: [ftc07-api-display-prepare.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc07-api-display-prepare.json)
- Compact backend summary: [ftc07-backend-summary.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc07-backend-summary.json)

Observed facts:

- Analyze result: success
- Canon result: success
- UI/API import result: success
- `display/prepare` status: success, `Partial`
- DisplayIR status: emitted
- `sourceAuthority`: `BRep`
- `displayAuthority`: `DisplayIR`
- Display lanes: `AnalyticPatch`, `BoundedMesh`, `WirePatch`
- Renderable counts by kind:
  - `AnalyticPatch`: `96`
  - `MeshPatch / BoundedMesh`: `0`
  - `WirePatch`: `210`
  - `DiagnosticPatch`: `0`
- Analyze face count: `306`
- Analyze edge count: `871`
- Diagnostic count: `299`
- Lane summary:
  - `AnalyticPatch`: `Complete`, `96` faces
  - `BoundedMesh`: `Failed`, `0` faces
  - `WirePatch`: `Partial`, `210` faces

Key backend diagnostic evidence:

- Top-level partial-display diagnostic: `Viewer.Display.Partial`
- First blocking face still includes the known planar hole path:
  - code: `Viewer.Tessellation.Timeout`
  - face: `9`
  - surface: `Plane`
  - phase: `PlanarTriangulationWithHoles`
- Subsequent degraded faces continue through `FaceDispatch`
- Representative wire-only faces include spheres, cylinders, torus, and BSpline faces downgraded to `WirePatch`

Interpretation:

- FTC-07 is no longer an import failure and no longer a total display-prepare failure.
- FTC-07 currently materializes as a partial DisplayIR result with `96` filled analytic faces and `210` wire-only faces.
- The dominant remaining backend/display issue is still bounded-mesh fill recovery, not import, not canon, and not DisplayIR authority.

## 8. UI facts: FTC-07

Expected screenshot targets:

- `artifacts/display-quality-x1/ftc07-default-view.png`
- `artifacts/display-quality-x1/ftc07-status-inspector.png`
- `artifacts/display-quality-x1/ftc07-angle-1.png`
- `artifacts/display-quality-x1/ftc07-angle-2.png`

Current result:

- A grounded fresh-client FTC-07 UI pass could not be completed for the same reason as FTC-06: the current DisplayScene frontend worktree did not compile to a runnable app shell.
- Because a fresh client could not render, the pass could not verify body visibility, partial-display visuals, wire-only face prominence, diagnostic marker visibility, or UI/backend status agreement.

What can still be stated from backend facts:

- FTC-07 backend state is `Partial`, not failed import.
- The partial result is strongly wire-heavy (`210` `WirePatch` faces).
- The degraded lane is specifically the `BoundedMesh` path, which reported `Failed`.

## 9. Visual issue classification

### FTC-06 visual artifacts

Issue:
  title: FTC-06 fresh visual confirmation blocked by unresolved frontend merge state
  model: FTC-06
  screenshot: [client-43117-vite-overlay.png](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/client-43117-vite-overlay.png)
  suspected layer:
    - local runtime/build
  evidence: fresh Vite boot on alternate port `43117` rendered a compile overlay before the application shell; current source still contains merge markers in `aetheris.client/src/App.tsx` and related DisplayScene files
  severity: high for QA confidence, low for backend display correctness
  recommended follow-up: resolve the worktree merge state before claiming a new visual pass; then rerun FTC-06 screenshots against a fresh client instance

### FTC-07 partial/wire/diagnostic display issues

Issue:
  title: FTC-07 remains predominantly wire-only after DisplayIR partial recovery
  model: FTC-07
  screenshot: none captured from a fresh client build
  suspected layer:
    - BoundedMesh tessellation
    - DisplayIR mapping
  evidence: live `display/prepare` returned `Partial`; `AnalyticPatch=96`, `BoundedMesh=0`, `WirePatch=210`; `BoundedMesh` lane status was `Failed`; first timeout remained `face 9 / Plane / PlanarTriangulationWithHoles`
  severity: high
  recommended follow-up: `DISPLAY-BACKEND-X1 — FTC-07 wire-only-to-fill recovery triage`

Issue:
  title: FTC-07 timeout fan-out extends beyond the initial planar face
  model: FTC-07
  screenshot: none captured from a fresh client build
  suspected layer:
    - BoundedMesh tessellation
    - AnalyticPatch sampling
  evidence: after the initial face-9 planar timeout, additional faces degraded during `FaceDispatch`; representative wire-only faces include `Sphere`, `Cylinder`, `Torus`, and `BSplineSurfaceWithKnots`
  severity: medium
  recommended follow-up: isolate why one stalled bounded-mesh path causes broad wire-only fallback instead of preserving more fill coverage

### Status UI issues

Issue:
  title: UI/backend status agreement could not be revalidated post-X6 from a fresh build
  model: FTC-06 and FTC-07
  screenshot: [client-43117-vite-overlay.png](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/client-43117-vite-overlay.png)
  suspected layer:
    - frontend DisplayIR status/inspector polish
    - local runtime/build
  evidence: the client never reached the app shell, so no status panel or inspector state was visible; existing `5173` listeners were treated as stale runtime and not accepted as grounded evidence
  severity: medium
  recommended follow-up: after resolving the merge state, rerun a fresh-client UI pass before making any new frontend status-polish claims

### Build/test issues

Issue:
  title: DisplayScene frontend worktree contains unresolved merge markers
  model: repo-wide blocker affecting FTC UI QA
  screenshot: [client-43117-vite-overlay.png](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/client-43117-vite-overlay.png)
  suspected layer:
    - local runtime/build
  evidence: `npm run build` and `npm test -- --run ...` both fail on merge markers in `aetheris.client/src/App.tsx`, `src/viewer/AetherisViewport.tsx`, `src/viewer/displaySceneBounds.ts`, and `src/__tests__/displaySceneBounds.test.ts`; fresh Vite startup reproduced the same parse error
  severity: high
  recommended follow-up: resolve the local merge state first; do not interpret this as a product display regression

## 10. Screenshots / artifact index

Report artifact:

- Report: [display-quality-x1-ftc06-ftc07-post-displayscene-qa.md](/C:/Users/yuech/source/repos/Aetheris/docs/display-quality-x1-ftc06-ftc07-post-displayscene-qa.md) — intended to commit

Backend artifacts:

- [ftc06-backend-summary.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc06-backend-summary.json) — local-only
- [ftc07-backend-summary.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc07-backend-summary.json) — local-only
- [ftc06-api-display-prepare.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc06-api-display-prepare.json) — local-only, very large raw payload
- [ftc07-api-display-prepare.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc07-api-display-prepare.json) — local-only
- [ftc06-api-import.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc06-api-import.json) — local-only
- [ftc07-api-import.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc07-api-import.json) — local-only
- [ftc06-analyze.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc06-analyze.json) — local-only
- [ftc07-analyze.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc07-analyze.json) — local-only
- [ftc06-canon.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc06-canon.json) — local-only
- [ftc07-canon.json](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/ftc07-canon.json) — local-only
- [nist_ftc_06_canonical.step](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/nist_ftc_06_canonical.step) — local-only
- [nist_ftc_07_canonical.step](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/nist_ftc_07_canonical.step) — local-only

UI/blocker artifacts:

- [client-43117-vite-overlay.png](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/client-43117-vite-overlay.png) — local-only
- [client-dev-43117-stdout.log](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/client-dev-43117-stdout.log) — local-only
- [client-dev-43117-stderr.log](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/client-dev-43117-stderr.log) — local-only
- [server-stdout.log](/C:/Users/yuech/source/repos/Aetheris/artifacts/display-quality-x1/server-stdout.log) — local-only

Not generated:

- `ftc06-default-view.png`
- `ftc06-status-inspector.png`
- `ftc06-angle-1.png`
- `ftc06-angle-2.png`
- `ftc07-default-view.png`
- `ftc07-status-inspector.png`
- `ftc07-angle-1.png`
- `ftc07-angle-2.png`

Reason:

- the current frontend worktree did not compile into a fresh runnable UI

## 11. Recommended next milestone

Recommended next milestone:

`DISPLAY-BACKEND-X1 — FTC-07 wire-only-to-fill recovery triage`

Why this is the right next milestone:

- FTC-06 backend display is already healthy and complete
- FTC-07 import, analyze, and canon all succeed
- FTC-07 no longer presents as an import failure or a total display-prepare failure
- the remaining substantive product issue is that FTC-07 still collapses to `210` wire-only faces because the `BoundedMesh` lane fails and the first explicit timeout remains `face 9 / Plane / PlanarTriangulationWithHoles`
- the frontend blockage found during this pass is a local merge-state issue, not the next architecture milestone to optimize around

## 12. Non-goals

- no STEP import/export semantic changes
- no BRep topology changes
- no tessellator rewrite
- no Firmament V2 changes
- no AIR Region route policy changes
- no CIR authority changes
- no CAD feature changes

Additional explicit non-changes for this pass:

- no DisplayIR server authority changes
- no Firmasm changes
- no frontend typed renderable semantic changes
- no AP242 importer/exporter behavior changes
