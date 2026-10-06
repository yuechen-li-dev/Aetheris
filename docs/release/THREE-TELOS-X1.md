# THREE-TELOS-X1 — Cadmata authoring overlays and PMI

## Result: Success

Cadmata's supported-browser model, assembly, construction overlays and semantic PMI now use one Three Telos graphics authority. The presence of an authoring artifact no longer selects WebGL. React retains application state, semantic correspondence, widgets and callout layout. Telos retains camera matrices, GPU passes, depth, picking and resource lifetime. All new implementation and qualification code is TypeScript. Zero Three patches or forks were added.

The motivating case is the original CADMATA-PMI-X1 CTC-03 manufacturing AP242 STEP, loaded through the actual application import UI and server bridge. Its 173 published entities include 29 semantic callouts: 3 datums, 13 dimensions/diameters, 5 position controls and 8 notes. Default presentation shows 8 datum/position panels. The all-category witness preserves all 29 objects; the unchanged dense-layout policy displays 26 and hides 3 low-priority notes in the measured viewport.

## Remaining WebGL surface audit

The audit found no existing transform gizmo, snap tool, dashed GPU leader or arrowhead to migrate. Whole-part notes use dashed **CSS borders**, not stippled GPU lines. New arrow/dash/font frameworks would therefore invent product behavior.

| Original component / exact owner | Current user-facing purpose | Shared? | Telos equivalent / migration |
| --- | --- | --- | --- |
| `aetheris.client/src/viewer/CadmataOverlay.tsx`, `OverlayEntity` | Compiler construction/reference points, circles, planes, guide/profile/region polylines; selectable authoring preview | Cadmata only | Removed. `cadmataOverlays.ts` submits cached sphere/plane mesh data and existing Telos thick lines. Layer contract lives in `cadmataLayers.ts`. Rendering-only fallback copy is `LegacyCadmataOverlay.tsx`. |
| `aetheris.client/src/viewer/PmiAnnotationLayer.tsx` | Semantic datum, dimension, position and annotation panels, leaders, layout, dragging | Cadmata only; layout reused by fallback | Replaced with DOM buttons projected by TelosCamera and retained Telos leaders. Semantic helpers/content live once in `pmiPresentation.tsx`. Fallback rendering is `LegacyPmiAnnotationLayer.tsx`. |
| `aetheris.client/src/viewer/LegacyAetherisViewport.tsx`, `AxisGuide` | Reference axes and X/Y/Z text | Cadmata only | Existing Telos axis lines plus projected DOM labels; no Drei text authority in normal mode. |
| `LegacyAetherisViewport.tsx`, `PickRayCapture`, `CameraFit`, `OrbitControls` | Input rays, fitting, navigation | Cadmata only | Canonical TelosCamera/worldRay and existing Telos navigation; overlay IDs are explicitly dispatched before the server BRep query. |
| `LegacyAetherisViewport.tsx`, `FaceMesh`, `EdgeLine`, `AssemblyMeshes`, `AdaptiveLogGrid` | Model, topology, occurrence selection, grid | Cadmata only | Already migrated by X0. X1 extends retained draw/proxy updates and preserves these paths. |
| `aetheris.client/src/viewer/ThemeBackground.tsx` | Shader-driven aesthetic backgrounds in WebGL | Cadmata fallback only | Normal Telos uses X0's theme clear/reference grid. Animated shader background parity remains an inherited X0 aesthetic limit; no authoring/PMI coupling. |
| Legacy viewport performance globals | Debug-only old-renderer counters | Cadmata fallback only | Telos host metrics and browser qualification JSON; no hidden debug renderer. |
| `Aetheris.Web.Runtime/telos/src/{host,pick,camera,pipelines}.ts` | Graphics authority | Cadmata and both Helios consumers | Only generic graphics contracts were extended; no CAD models or React state entered Telos. |

## Small shared changes

Existing `TelosMesh` and `TelosLine` cover the graphics inventory. No new primitive family was necessary. Their contracts now expose `depthMode`; meshes may be unlit, and `TelosIdentity.overlayId` carries an opaque product-owned interaction ID.

`TelosHost.beforeFrame` provides one projection cadence after camera matrices update and before GPU drawing. `setDynamicLines` retains a vertex/uniform pair for each stable leader ID and segment count. Updates write those buffers; they do not create geometry or pipelines. Removing an ID releases it. Model scene updates also retain unchanged mesh uniforms, line buffers and CPU picking proxies. Semantic artifacts/geometry arrays remain immutable; genuinely rebuilt geometry replaces its buffers.

All line drawing uses the existing segment-quad shader. Pipeline cache keys normalize equivalent default/explicit depth states, and overlay/model variants share the same shader module. In the CTC-03 all-category measurement, 129 geometry definitions and 26 visible leaders use 574 tracked buffers, **3 pipelines and 2 shader modules**. There is no pipeline per label.

## Coordinates, depth and pass order

| Surface | Coordinates / size | Depth |
| --- | --- | --- |
| Construction point | World sphere, existing radius 1.8 | Always on top; unlit |
| Plane region | Published world origin and u/v basis | Depth tested, unlit alpha 0.12, no depth write |
| Construction circle/polyline | World coordinates; CSS-pixel line width | Depth biased, no depth write |
| PMI leader | Published world anchor to a camera-unprojected screen-layout endpoint | Always on top, no depth write |
| PMI DOM panel | World-anchored, screen-sized, CSS-pixel layout | Always on top; culled outside WebGPU clip depth [0,1] |
| Axis label | Projected world endpoint, screen-sized DOM text | Always on top; same clip policy |

Pass order is Background → ReferenceGrid → MeshOpaque → Field/CIR → Topology → Overlay → presentation. Overlay mesh drawing precedes authoring lines and dynamic PMI leaders. DOM panels composite above the canvas. There is one canvas renderer, device, camera and authoritative depth target. Simple alpha blending is sufficient; no order-independent transparency was introduced.

The plane adapter now honors the published u/v vectors. The old WebGL plane helper used their lengths while leaving every rectangle in XY. This is a local graphics adapter correction, with a focused non-XY basis test. Circle presentation keeps the current XY authoring convention.

## Text and semantics

The existing callouts were HTML buttons through Drei `Html`. They remain HTML buttons with the same content, categories, classes, theme colors, accessible inspect labels and selected state. This preserves the browser typography and pointer interaction without needing a GPU font renderer. Copeland's existing MSDF/font work is the reuse owner if GPU text becomes necessary; X1 neither reimplements nor unnecessarily imports it.

`semanticPmiItems` and `layoutPmiCallouts` were extracted without replacing their anchor or bounded greedy collision algorithms. Both normal mode and browser fallback consume those single product-owned implementations. Projection/unprojection uses TelosCamera directly; no `THREE.Camera`, R3F context or second RAF loop participates.

Dragging changes a per-session CSS-pixel offset only. It starts from the actual current displaced placement, avoiding the former first-drag jump from zero. Leaders update from the same semantic anchor. The real browser asserts identical GPU buffer and geometry objects across the drag. Offsets remain volatile presentation hints, as before.

Selecting PMI still uses `resolveCadmataSelection` to highlight its published target faces. Geometry-to-PMI visual emphasis uses `indexSemanticInspection`'s published owner/target relationships; it does **not** broaden the selected topology set. The original semantic inspector discovery path remains intact. Hidden category panels/leaders are absent from submission/picking.

CTC-03 anchors come from `CadmataStepSemanticBridge`, not display triangles. The authored projected-hole fixture's `pmi:datum:A`, target `face(+Z)`, published anchor `(20,20,6)` and Face 2 association survive a real fixture compiler rebuild. The migration consumes that existing anchor exactly; refining its geometric intent belongs to the semantic bridge, not the renderer.

## Picking and navigation

DOM labels receive DOM events and stop propagation; pointer capture keeps dragging out of navigation. GPU picks use explicit `overlayId` dispatch, without invisible R3F event geometry.

Priority is deterministic: DOM panels by DOM z-index; then always-on-top authoring meshes, dynamic PMI leaders, construction lines/surfaces, and model geometry. Within equal line priority the nearest ray distance wins; ties keep submission order. Hidden surfaces are excluded; depth-tested lines behind an opaque model face do not steal its pick. Existing thick-line CSS-pixel tolerances are reused. Model clicks retain the canonical world ray and server-owned BRep query, while assembly occurrence selection uses Telos identity.

Existing Telos orbit/pan/wheel navigation still drives the same TelosCamera. Tests cover perspective/orthographic projection, DPR 1/2, camera movement, resizing and a near plane crossing the target region.

## Fallback and dependencies

`AetherisViewport.tsx` has one unsupported-browser fallback boundary. Its legacy viewport is dynamically imported only when `navigator.gpu` is absent. Authoring artifacts and PMI never trigger that branch. Real normal-mode application resource traces contain zero R3F/Drei/legacy viewport requests. GPU initialization/loss errors remain explicit diagnostics rather than silently switching renderers.

R3F/Drei stay installed exclusively for the supported unsupported-browser fallback: `LegacyAetherisViewport.tsx`, `LegacyCadmataOverlay.tsx`, `LegacyPmiAnnotationLayer.tsx`, and the legacy-only `ThemeBackground.tsx`. None is a normal-mode renderer or hidden interaction utility. No non-authoritative R3F helper remains on the normal path. A structural test enforces that import fence. The existing nested `stats-gl` Three 0.170 dependency remains unused legacy dependency metadata; primary Three stays pinned at 0.183.2.

## Real-path qualification

| Check | Evidence |
| --- | --- |
| Actual Cadmata import UI | Original CTC-03 manufacturing STEP, server import/semantic bridge, actual App and viewport. Default 8 panels; DIM 13; NOTES 8; filter to exactly 8 notes; orbit/wheel; presentation drag moves exactly +74/-37 CSS pixels at both DPRs; refresh remains Telos. |
| Ordinary authoring UI | Actual Modeling Demo activation, Create Box 40×30×8, Apply Translation +5 X, server occurrence translation shown, Refresh Display Data. This is the existing experimental product workflow, not a new authoring tool. |
| Construction preview | Real `construction-plane-positive-x` compiler/API fixture: 10 published construction/profile/guide polylines submitted through the normal wrapper. The canonical fixture encodes its plane as wire geometry; no synthetic weaker replacement model was substituted. |
| Selection both directions | CTC-03 Datum A selects Face 1; real viewport pixel pick of Face 1 highlights the related Datum A panel through the normal server BRep pick path. |
| Authored rebuild | `pmi-projected-hole-diameter` fixture compiled twice through the real endpoint; `pmi:datum:A`, `face(+Z)`, anchor and selected Face 2 remain stable. |
| Resources/performance | Real 29-object PMI model, 20 static and 20 moving-camera GPU-submission-completed frames per DPR. Drag retains all buffer/geometry identities. |
| CIR plus PMI | X0's qualified direct-WGSL cylinder placed at the existing authored datum world anchor; mesh list is empty; actual DOM datum and retained GPU leader remain visible. This proves graphics independence, not automatic product CIR packet construction or field semantic picking. |
| Shared substrate regression | X0 browser witness rerun: both mixed mesh/field depth directions, perspective/orthographic, pixel-width lines, shared definitions/shaders, hidden topology picking, resize, disposal and recreate. |
| Actual Helios viewport | Real WASM compile, top `face(+Z)` selection, orbit, zoom, source rebuild 40→45, stable source selector and fresh SDK build revision; no browser errors/alerts. |

Screenshots are generated/ignored under `artifacts/local/three-telos/x1/`:

- [Actual normal authoring](../../artifacts/local/three-telos/x1/normal-authoring-dpr1.png)
- [Actual canonical PMI](../../artifacts/local/three-telos/x1/default.png)
- [Selected datum and face](../../artifacts/local/three-telos/x1/selected.png)
- [Dragged presentation](../../artifacts/local/three-telos/x1/dragged.png)
- [Filtered notes](../../artifacts/local/three-telos/x1/filtered.png)
- [DPR 2 orbit/close zoom](../../artifacts/local/three-telos/x1/zoom-dpr2.png)
- [29-object dense view](../../artifacts/local/three-telos/x1/all-pmi-dpr2.png)
- [Construction preview](../../artifacts/local/three-telos/x1/construction-dpr1.png)
- [Authored datum after rebuild](../../artifacts/local/three-telos/x1/authored-rebuild-dpr1.png)
- [CIR with semantic PMI](../../artifacts/local/three-telos/x1/cir-pmi-dpr1.png)

Reports: `application-dpr{1,2}.json`, `overlays-dpr{1,2}.json`, plus X0 `browser-report.json` and `helios-wrapper.json`. They distinguish actual App workflow from the real-wrapper/API instrumentation witness.

## Bounded performance

Installed Edge, headless WebGPU, reported adapter vendor `nvidia`, architecture `ampere`; 1100×800 CSS-pixel instrumentation viewport. Each number is from 20 frames including `queue.onSubmittedWorkDone`, not an isolated GPU timer.

| Measure | DPR 1 | DPR 2 |
| --- | ---: | ---: |
| Static PMI-heavy completed-frame mean | 3.465 ms | 3.420 ms |
| Static observed p95 (19th sorted sample) | 4.000 ms | 4.100 ms |
| Moving camera / relayout completed-frame mean | 3.670 ms | 4.450 ms |
| Moving projection/layout/submission CPU mean | 0.350 ms | 0.420 ms |
| Retained dynamic line write CPU mean | 0.050 ms | 0.085 ms |

The static layout signature skips recomputation; zero-duration timer samples mean below timer resolution, not free work. Camera movement and label dragging retain buffers. The all-category scene uses 158 draw submissions, 574 buffers, 3 pipelines and 2 modules. Dense labels keep the inherited collision/de-emphasis policy. No comparable timed old-WebGL capture was made, so these are qualification measurements rather than a claimed speedup.

## Validation

- Telos strict TS build and tests: **7 passed**.
- Cadmata: **85 passed**, lint clean, production TS/Vite build passed.
- Actual Helios: **29 passed**, development TS/Vite build passed.
- Aetherion Helios demo: **26 passed**, development TS/Vite build passed.
- Frontend/shared unit total: **147 passed**.
- .NET Release solution build passed; initial rebuild had 10 warnings in existing Firmament/test/WASM files outside this change, and the final incremental build had only 2 existing WASM SQLite warnings. Fast kernel lane: **1,005 passed**. Full serial solution lane: **4,312 passed in 20 suites, zero failed/skipped**.
- Repository CLI `analyze` ran on the canonical CTC-03 STEP; browser import used the same file and semantic bridge.
- Helios production AOT SDK bundling is not claimed; the real browser uses the existing development WASM SDK and compiles the real fixture.
- No kernel/compiler/Copeland/Helios source changes were needed. A corpus test changed only generated STEP line endings; verified and restored. All temporary foreground hosts were stopped after qualification.

## Cleanup and friction owners

The dedicated cleanup extracted shared product-only presentation contracts, removed the obsolete overlay component/barrel, sealed fallback rendering behind a lazy import, retained model/picking resources across props changes, normalized equivalent depth cache states, and reused shader modules across overlay/model passes. Camera projection, line rendering and semantic layout each have one normal-mode owner. No React state or product-specific primitive entered Telos.

| Friction / exposed coupling | X1 repair or bounded disposition | Permanent owner |
| --- | --- | --- |
| `useThree` / `useFrame` bound callouts to the renderer camera | TelosCamera direct projection plus `beforeFrame`; no extra loop | Telos camera/frame cadence; Cadmata layout |
| Drei `Html` mixed DOM content and renderer placement | Keep semantic DOM content; replace only projection/placement | Cadmata presentation; browser typography |
| Potential new GPU text wheel | No GPU text needed; existing Copeland MSDF remains the future reuse seam | Copeland font/MSDF subsystem if requested |
| Pointer propagation through R3F groups/lines | Opaque `overlayId`, explicit hit priority and product callback dispatch | Telos graphics picking; Cadmata selection intent |
| `setScene` rebuilt draw uniforms/line buffers/proxies on ordinary props updates | Retain identity-matched draw resources and immutable CPU geometry | Telos resource lifetime |
| Callout endpoints move on every orbit/drag | Bounded retained leader buffers, updated on placement changes | Telos line buffers; Cadmata offsets/layout |
| First drag used zero instead of collision-displaced placement | Capture the actual placement-relative offset | Cadmata presentation |
| Geometry discovery did not visually emphasize related callouts | Use existing published owner/target index; topology selection stays exact | Cadmata semantic correspondence |
| Plane helper ignored u/v direction | Emit published world corners; non-XY unit check | Cadmata graphics adapter |
| Canonical construction fixture publishes wire planes | Test its actual polylines; plane-mesh adapter has focused basis coverage | Compiler fixture contract; no synthetic demo replacement |
| Repeated default/explicit overlay depth pipeline state | Normalize resolved depth and share mesh/line modules | Telos pipeline cache |
| Dense labels cannot all fit without overlap | Preserve existing bounded hiding of low-priority notes; use category filters | Cadmata layout policy, not GPU backend |
| Copied authoritative anchors may be coarse (authored datum z=6) | Consume exactly; no triangle-derived correction | Cadmata semantic bridge / compiler provenance |
| Suspense changed legacy test timing | Await fallback component loading in its existing wire test | Frontend fallback test |
| Async qualification loader lint heuristic | Defer asynchronous endpoint loading via promise callback; production path unaffected | Test entry point |
| Existing .NET WASM/nullable/analyzer and Helios jsdom CSS/chunk warnings | Recorded; no unrelated compiler/UI cleanup | Existing .NET and Helios build owners |
| Theme animation differs from legacy WebGL | Inherited X0 clear/grid policy, explicitly outside overlay migration | Future Telos aesthetics work if requested |

## Reproduction

From repository root, build Telos and the frontend before building the host. Logs and captures stay under ignored `artifacts/local/three-telos/`.

```powershell
npm --prefix Aetheris.Web.Runtime/telos test
npm --prefix Aetheris.Web.Runtime/telos run build:witness
npm --prefix aetheris.client test
npm --prefix aetheris.client run lint
npm --prefix aetheris.client run build
dotnet build Aetheris.slnx -c Release -m:1
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter 'Category!=SlowCorpus'
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
```

Use temporary **foreground** terminals for the server and Vite; stop each with Ctrl+C after testing:

```powershell
dotnet Aetheris.Server/bin/Release/net10.0/Cadmata.dll --urls https://localhost:7145
# In a separate foreground terminal, cwd=aetheris.client:
$env:AETHERIS_VITE_HTTP='1'
$env:AETHERIS_TELOS_WITNESS='1'
npm run dev -- --host 127.0.0.1
# For actual Helios, cwd=../HeliosCAD:
npm run dev -- --host 127.0.0.1
```

With the X0 qualified CIR artifact available (regenerate with `scripts/prepare-three-telos-cir.ps1` if needed):

```powershell
node scripts/qualify-three-telos-x1.mts ../HeliosCAD/node_modules/playwright/index.mjs
node scripts/qualify-three-telos-x1.mts ../HeliosCAD/node_modules/playwright/index.mjs 2
node scripts/qualify-three-telos-x1-overlays.mts ../HeliosCAD/node_modules/playwright/index.mjs
node scripts/qualify-three-telos-x1-overlays.mts ../HeliosCAD/node_modules/playwright/index.mjs 2
node scripts/qualify-three-telos-browser.mts ../HeliosCAD/node_modules/playwright/index.mjs
node scripts/qualify-three-telos-products.mts ../HeliosCAD/node_modules/playwright/index.mjs http://127.0.0.1:5173
```

X1 leaves no Cadmata renderer migration blocker for CIR-DISPLAY-X1B. That milestone still owns automatic authored-geometry CIR packets/dispatch, broader field qualification and semantic selection proxies. This report claims the existing overlay and PMI behavior over the qualified field, not those future capabilities.
