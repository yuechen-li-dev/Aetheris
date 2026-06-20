# DISPLAY-QA-X1: FTC-06 / FTC-07 local visual pass

## 1. Purpose and scope

This pass validates the current local viewer behavior for FTC-06 and FTC-07 after the DisplayIR architecture milestones, with emphasis on:

- whether UI import succeeds;
- whether `/display/prepare` succeeds, partially succeeds, or fails;
- whether the UI presentation matches backend reality;
- which remaining defects are most likely kernel/import, DisplayIR preparation, fallback lane, frontend renderer, camera/framing, or local build/runtime issues.

This milestone did not attempt broad fixes, tessellator rewrites, legacy mesh-scene removal, STEP semantic changes, or topology changes.

## 2. Environment

- OS: Microsoft Windows 11 Pro, version `10.0.26200`, build `26200`
- Command shell: PowerShell
- `dotnet --version`: `10.0.301`
- `node --version`: `v26.2.0`
- `npm --version`: `11.13.0`
- Browser used for UI QA: Microsoft Edge (`C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`) driven headlessly by Playwright `1.61.0`
- In-app browser was also used for inspection, but not for the final import flow because its exposed runtime here could not populate the native file input.

## 3. Exact files tested

- FTC-06: `testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp`
- FTC-07: `testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp`

## 4. Exact commands used

Build / validation:

```powershell
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1
dotnet test Aetheris.Kernel.Core.Tests\Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "DisplayIR|BoundedMesh|WirePatch|DisplayPrepare|ViewMaterialization|FTC07|Ftc07|Tessellation|FTC06|Ftc06" --logger "console;verbosity=minimal"
dotnet test Aetheris.Server.Tests\Aetheris.Server.Tests.csproj -f net10.0 --no-build --filter "DisplayIR|BoundedMesh|WirePatch|DisplayPrepare|FTC07|Ftc07|FTC06|Ftc06|KernelApi|ViewMaterialization|Tessellate|Tessellation|StepIo" --logger "console;verbosity=minimal"
dotnet test Aetheris.Kernel.Core.Tests\Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter "Step242Ftc06SameSenseRegressionTests|FTC06|Ftc06|same_sense" --logger "console;verbosity=minimal"
cd aetheris.client
npm test -- --run App.test.tsx displayRenderables.test.ts AetherisViewport.test.ts displaySceneBuilder.test.ts
npm run build
```

CLI analysis / canonicalization:

```powershell
dotnet run --project Aetheris.CLI -f net10.0 --no-build -- analyze testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp --json
dotnet run --project Aetheris.CLI -f net10.0 --no-build -- analyze testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp --json
dotnet run --project Aetheris.CLI -f net10.0 --no-build -- canon testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp --out artifacts/display-qa-x1/nist_ftc_06_canonical.step --json
dotnet run --project Aetheris.CLI -f net10.0 --no-build -- canon testdata/step242/nist/FTC/nist_ftc_07_asme1_ap242-e2.stp --out artifacts/display-qa-x1/nist_ftc_07_canonical.step --json
```

Server / client startup:

```powershell
dotnet run --project Aetheris.Server --launch-profile http
$env:ASPNETCORE_URLS='http://localhost:5142'
npm.cmd run dev -- --host 127.0.0.1
```

Direct API capture:

```powershell
node <inline script posting to /api/v1/documents, /import/step, and /display/prepare for FTC-06 and FTC-07>
```

UI automation:

```powershell
npm init -y                                # in artifacts/display-qa-x1/playwright-env
npm install playwright --no-save           # in artifacts/display-qa-x1/playwright-env
$env:AETHERIS_PLAYWRIGHT_MODULE='C:\Users\yuech\source\repos\Aetheris\artifacts\display-qa-x1\playwright-env\node_modules\playwright\index.mjs'
node scripts\display-qa-x1-playwright.mjs
```

Focused FTC-06 camera experiment:

```powershell
node <inline Playwright script importing FTC-06, then repeatedly wheel-zooming the viewport and saving comparison screenshots>
```

Notes:

- `npm test -- --run ... AetherisViewport.test.ts displayRenderables.test.ts ...` only matched existing `App.test.tsx` and `displaySceneBuilder.test.ts`; those two files passed.
- `npm run build` failed locally in [aetheris.client/src/App.tsx](C:/Users/yuech/source/repos/Aetheris/aetheris.client/src/App.tsx:393) and [aetheris.client/src/App.tsx](C:/Users/yuech/source/repos/Aetheris/aetheris.client/src/App.tsx:394) with TypeScript `never` narrowing errors.
- The in-app browser could inspect the app but could not populate the native STEP file input through its exposed runtime here, so final screenshot capture used standalone Playwright.

## 5. FTC-06 backend facts

- `analyze`: succeeded
- `canon`: succeeded
- API/UI import: succeeded (`200`)
- `/display/prepare`: succeeded (`200`)
- DisplayIR status: `mixed-fallback`
- Lanes emitted: analytic plus fallback
- Analytic faces: `161`
- Fallback faces: `26`
- Fallback reason breakdown: `UnsupportedTrim = 26`
- Diagnostic faces count: `0` observed as emitted diagnostic-only faces
- Wireframe-only faces count: `0` observed as explicit wireframe-only emission
- Bounded mesh faces count: `26` inferred
  Reason: all 26 fallback faces were present in `tessellationFallback.facePatches`, all patch `source` values were `Tessellator`, and all `scaffoldRejectionReason` values were `None`
- Analytic faces count: `161`
- Raw artifacts:
  - `artifacts/display-qa-x1/ftc06-analyze.json`
  - `artifacts/display-qa-x1/ftc06-canon.json`
  - `artifacts/display-qa-x1/ftc06-import.json`
  - `artifacts/display-qa-x1/ftc06-display-prepare.json`
  - `artifacts/display-qa-x1/ftc06-api-summary.json`

Important limitation:

- The current API contract does not expose first-class `WirePatch`, `WireframeOnly`, or `DiagnosticOnly` counters in `DisplayPreparationResponseDto`; those counts above are based on emitted payload evidence, not explicit server fields.

## 6. FTC-06 UI facts

- Screenshot paths:
  - `artifacts/display-qa-x1/ftc06-imported-display.png`
  - `artifacts/display-qa-x1/ftc06-display-status.png`
  - `artifacts/display-qa-x1/ftc06-imported-display-baseline.png`
  - `artifacts/display-qa-x1/ftc06-imported-display-zoomed-out.png`
  - `artifacts/display-qa-x1/ftc06-imported-display-max-zoomout.png`
- Whether body visible:
  - At default post-import view: effectively no; the viewport shows only the drafting grid
  - After aggressive wheel zoom-out: yes, but only as a heavily clipped partial body entering from the upper-right/top edge
- Whether visual defects remain: yes
  - The default camera framing fails badly for the imported model
  - Even after manual zoom-out, the model is still clipped and not fit to the viewport
- Whether UI status matches backend status:
  - Partially yes
  - Inspector correctly reports `Display lane: mixed-fallback`, `Render path: mixed-fallback`, `Analytic faces: 161`, `Fallback faces: 26`, `Face count: 187`, `Edge count: 476`
  - Import status only says `Import complete.` and does not explicitly communicate that this is a partial analytic+fallback display case

## 7. FTC-07 backend facts

- `analyze`: succeeded
- `canon`: succeeded
- API/UI import: succeeded (`200`)
- `/display/prepare`: failed (`422`)
- DisplayIR status: not emitted; display preparation aborted before a usable packet was returned
- Lanes emitted: none in the failed response
- Diagnostic faces count: unavailable because no display packet was emitted
- Wireframe-only faces count: unavailable because no display packet was emitted
- Bounded mesh faces count: unavailable because no display packet was emitted
- Analytic faces count: unavailable because no display packet was emitted
- Failure diagnostic:
  - `ValidationFailed`
  - `Viewer.Tessellation.Timeout`
  - `Display tessellation exceeded the bounded execution budget after ~5s while processing face 9 on surface 'Plane' during phase 'PlanarTriangulationWithHoles'.`
- Raw artifacts:
  - `artifacts/display-qa-x1/ftc07-analyze.json`
  - `artifacts/display-qa-x1/ftc07-canon.json`
  - `artifacts/display-qa-x1/ftc07-import.json`
  - `artifacts/display-qa-x1/ftc07-display-prepare.json`
  - `artifacts/display-qa-x1/ftc07-api-summary.json`
  - `artifacts/display-qa-x1/ftc07-display-prepare-ui-response.json`

## 8. FTC-07 UI facts

- Screenshot paths:
  - `artifacts/display-qa-x1/ftc07-imported-display.png`
  - `artifacts/display-qa-x1/ftc07-display-status.png`
- Whether body visible: no
- Whether partial display / wire / diagnostic status is visible:
  - The UI clearly shows `Import complete. View materialization failed.`
  - It does not show a partial-display state, because `/display/prepare` failed outright
  - It does not show wireframe-only or diagnostic-only face counts or labels
- Whether UI status matches backend status:
  - Yes for the important distinction between import success and display failure
  - Inspector falls back to `Display lane: None`, `Render path: fallback`, and zero counts, which is consistent with the absence of a successful display packet but not especially rich diagnostically

## 9. Visual bug classification

### Issue 1

- Title: FTC-06 imported body is not framed into the default view
- Model: FTC-06
- Screenshot path: `artifacts/display-qa-x1/ftc06-imported-display.png`
- Suspected layer: frontend camera / old mesh-scene compatibility assumptions
- Evidence:
  - backend import and `/display/prepare` both succeed
  - inspector shows `mixed-fallback` with `187` rendered face patches and `476` edges
  - default viewport still shows only the grid and axis guide
- Severity: High
- Suggested follow-up: remove or repair legacy camera/framing assumptions and add fit-to-imported-body behavior for DisplayIR-backed scenes

### Issue 2

- Title: FTC-06 geometry appears only after extreme manual zoom-out and remains clipped
- Model: FTC-06
- Screenshot path: `artifacts/display-qa-x1/ftc06-imported-display-max-zoomout.png`
- Suspected layer: frontend camera / Three.js viewport setup / old mesh-scene compatibility assumptions
- Evidence:
  - after repeated zoom-out, geometry becomes visible only as a clipped upper-right fragment
  - this strongly suggests the body exists in scene space but is not normalized/framed correctly for the imported bounding box
- Severity: High
- Suggested follow-up: implement imported-body fit/center logic against actual scene bounds before removing legacy mesh-scene assumptions

### Issue 3

- Title: FTC-07 display preparation still times out on planar multi-loop face 9
- Model: FTC-07
- Screenshot path: `artifacts/display-qa-x1/ftc07-display-status.png`
- Suspected layer: DisplayIR server preparation / bounded mesh tessellation path
- Evidence:
  - kernel analyze, canon, and import all succeed
  - `/display/prepare` returns `422`
  - diagnostic identifies face `9`, surface `Plane`, phase `PlanarTriangulationWithHoles`
  - UI correctly reports view materialization failure instead of import failure
- Severity: High
- Suggested follow-up: isolate and repair the bounded planar-with-holes triangulation failure for FTC-07 face 9 without broad tessellator rewrite

### Issue 4

- Title: FTC-06 partial-display state is present in backend/inspector but under-signaled in the main UI
- Model: FTC-06
- Screenshot path: `artifacts/display-qa-x1/ftc06-display-status.png`
- Suspected layer: frontend typed DisplayIR renderer / status presentation
- Evidence:
  - backend returns `mixed-fallback`
  - inspector exposes lane and counts
  - main import status says only `Import complete.` and does not highlight that the display is mixed analytic+fallback
- Severity: Medium
- Suggested follow-up: add a first-class viewer status pill/banner for partial display, separate from import success/failure

### Issue 5

- Title: Local client production build currently fails independently of the visual QA flow
- Model: repo-wide local runtime issue
- Screenshot path: n/a
- Suspected layer: local build/runtime issue
- Evidence:
  - `npm run build` fails with TypeScript errors at [aetheris.client/src/App.tsx](C:/Users/yuech/source/repos/Aetheris/aetheris.client/src/App.tsx:393) and [aetheris.client/src/App.tsx](C:/Users/yuech/source/repos/Aetheris/aetheris.client/src/App.tsx:394)
  - dev server still runs, so this did not block the QA pass
- Severity: Medium
- Suggested follow-up: fix the `displayError` narrowing/build break separately so future local UI verification does not depend on the dev server only

## 10. Recommended next milestones

1. Remove or repair legacy imported-scene camera/framing assumptions for DisplayIR-backed viewer scenes.
   Reason: FTC-06 already imports and prepares display data successfully; the strongest remaining visible defect is framing, not kernel loss.
2. Fix the FTC-07 planar-with-holes display materialization failure in the bounded fallback path.
   Reason: the remaining blocker is narrow and well-identified at face 9 / `PlanarTriangulationWithHoles`.
3. Promote partial-display status to a first-class UI signal.
   Reason: FTC-06 is a successful mixed-fallback case, but the main status line currently hides that fact unless the user inspects the side panel.
4. After the camera/framing fix, rerun the same local FTC-06 pass before removing old mesh-scene assumptions.
   Reason: the current screenshots show imported geometry is present but badly framed; that is exactly the evidence needed before touching the legacy scene path.

Recommended immediate next milestone name:

- `DISPLAY-QA-X2` if the next step is a narrow frontend framing/status follow-up
- `DISPLAY-ARCH-X6` if the team wants to frame it as the first legacy-camera/scene-assumption removal slice

## 11. Non-goals

- no kernel rewrite
- no STEP import/export semantic changes
- no AP242 importer/exporter behavior changes
- no BRep topology changes
- no broad tessellator rewrite
- no Firmament V2 language/lowering changes
- no AIR Region route policy changes
- no CIR authority changes
- no Firmasm changes
- no frontend typed renderables behavior changes beyond a lightweight local QA helper script

## Answers to the milestone questions

1. Can FTC-06 import through the UI?
   Yes.
2. Can FTC-07 import through the UI?
   Yes.
3. Does FTC-06 display with remaining obvious visual defects?
   Yes. The body is effectively off-screen by default and only becomes partially visible after extreme zoom-out.
4. Does FTC-07 display fully, partially, wireframe-only, or diagnostic-only?
   No usable display is emitted in the current pass; import succeeds, but display preparation fails with a bounded diagnostic.
5. Likely remaining layers:
   - FTC-06 primary issue: frontend camera / old mesh-scene compatibility assumptions
   - FTC-07 primary issue: DisplayIR server preparation / planar-with-holes fallback tessellation
   - local secondary issue: client `npm run build` break
6. Does the UI correctly distinguish import success, partial display, wireframe-only, diagnostic-only, and materialization failure?
   - import success vs materialization failure: yes
   - partial display: only indirectly through inspector lane/counts, not prominently
   - wireframe-only faces: not explicitly surfaced in this UI
   - diagnostic-only faces: not explicitly surfaced in this UI
7. What should the next follow-up be?
   Fix imported-body framing/camera behavior first, then rerun FTC-06; keep FTC-07 on a narrow display-preparation/tessellation follow-up rather than another importer investigation.

## Artifact note

Artifacts were generated locally under `artifacts/display-qa-x1/`. They were not committed in this pass. Notable large local-only artifacts include `ftc06-display-prepare.json`, which is large because it contains the full tessellation fallback payload.
