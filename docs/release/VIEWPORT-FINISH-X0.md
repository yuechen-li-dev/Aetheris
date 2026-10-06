# VIEWPORT-FINISH-X0 — Preview 4 product audit

Date: 2026-10-05. Verdict: **Honest stop**.

Cadmata and Helios have a more reliable shared viewport, but this milestone is **not accepted for Preview 4**. Actual product use exposed missing display contracts, not optional aesthetic refinements: neither product supplies authored CIR fields; Scene sources cannot reach their display path; authored assembly finishes are omitted; the threaded showcase has 37 wire-only faces. A complete, presentation-worthy five-model screenshot set cannot be produced from these products yet.

The current stable mesh viewport is preserved. No geometry language, IR, GPU compiler, second renderer, or new AA framework was introduced. Completing these gates requires the existing engineering/display owners to supply their missing projection, followed by product qualification. Cosmetic shaders cannot reconstruct that authority.

## Real product audit and provenance

The audit used Microsoft Edge with the real NVIDIA Ampere WebGPU adapter, actual application navigation, STEP file input, product startup-file claims, the Cadmata API, and Helios's WASM worker. It did not inject synthetic display packets into the products.

- Cadmata: built Release server, production Vite bundle copied into its `wwwroot`, real browser at localhost. Earlier baseline/dev-server captures also exposed shader asset admission and React StrictMode initialization failures.
- Helios: actual `/local` application, installed WASM SDK and worker, Monaco/source/file/build/pick controls, and a successful development bundle build. Its production build refuses the currently installed non-AOT SDK: `Production requires the AOT SDK package. Run npm run sdk:install:production.` Production Helios packaging is not qualified by this audit.
- Canonical captures use a 1600 × 1000 CSS viewport at DPR 2. Lifecycle checks also use 1150 × 850 and DPR 3. Fallback captures deliberately remove `navigator.gpu`; this tests the explicit boundary, not a second physical hardware platform.
- Source and generated evidence are separated. Raw screenshots, JSON, meshes, CLI outputs, and logs remain ignored under `artifacts/local/viewport-finish-x0/`.

Reproduction entry point: `scripts/qualify-viewport-finish.mts <playwright-module-path> final <cadmata-url> 2`. Helios is expected at `http://127.0.0.1:4173/local`. The script opens fresh Cadmata processes for the guitar and robot because startup-file claims are one-shot. Use the real repository CLI DLL to generate the threaded STEP before the run:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Thread/hexbolt-threaded.firmament artifacts/local/viewport-finish-x0/threaded.step --json
node scripts/qualify-viewport-finish.mts <playwright-module-path> final http://127.0.0.1:5142 2
node scripts/qualify-viewport-finish.mts <playwright-module-path> themes http://127.0.0.1:5142 2
```

The CLI successfully compiles the threaded source and inspects both house and factory Scenes. That proves their source owners work; it does not prove these products display them.

## Changes and friction log

| Issue and user impact | Root owner | Repair or release boundary |
|---|---|---|
| Shader asset requests were refused; supported-browser viewport stayed blank | Product Vite asset admission | Admit the shared Telos package directory in both dev servers. Helios scripts explicitly select `vite.config.ts`, avoiding a stale emitted JS config. |
| Packaged temporal WGSL returned 404 even when its asset existed | Cadmata static file hosting | Explicit `.wgsl` → `text/plain` content type, verified by real HTTP 200 and product startup. Unknown file types remain restricted. |
| React StrictMode could dispose an older initialization after a new context was configured | Telos device and product initialization | Abort pending startup; destroy canceled devices before configuring the canvas; only the current context owner may unconfigure it. |
| `SpatialOnly` did not give mesh silhouettes multisample coverage | Telos frame/pipeline owner | Four-sample color and the single authoritative depth attachment, shared by drawing passes, with one final color resolve. |
| Wide-model framing clipped in narrow panes; initial fit ran before pane sizing | Shared camera/host | Size synchronously before fit; account for viewport aspect in both projection modes; respect product world-up. |
| Engineering views had inconsistent orientation and unnecessary depth range | Camera projection | Both products use Z-up, XY grid, restrained isometric first fit, and bounds-relative near/far planes. |
| Grid terminals and excess line density looked accidental | Telos grid/line shader | Bounded 1/2/5 decade spacing, at most 32 lines either side per axis, derivative coverage, and finite end fading. |
| Opening an assembly highlighted the whole model | Cadmata selection projection | Start with no assembly selection. Explicit user selection still uses the published occurrence tree. |
| Opaque bright face emphasis hid material/form | Shared material projection | Amber blended into original material; selected and hover strengths differ; preserve opacity. Both mesh and field selection use the same helper. |
| Hover was absent or kept redrawing while dragging | Product pointer projection | Local picking for hover; clear on leave, skip navigation drags, invalidate only when hover changes. Engineering click authority stays in its existing owner. |
| FIT SEL fitted the whole scene | Shared bounds fit / Helios control | Fit selected occurrence bounds; retain shared camera authority. |
| Empty/GPU initialization/error states could be silent or clipped | Product viewport UI | Useful empty guidance, quiet busy status, bounded diagnostic panel, and Restart viewport retaining the product model. |
| DPR changed without CSS resize or media-query notification | Shared host lifecycle | ResizeObserver, resize/media-query notifications, plus a 500 ms attached scalar DPR check. Unchanged DPR schedules no frame; disposal removes its timer/listeners. |
| Repeated edge definitions collided across occurrences | Telos adapter | Include occurrence identity in line identity; retain definition geometry reuse and kernel edge provenance. |
| File header reading left the previous STEP import enabled | Cadmata file selection | Clear admitted selection immediately; ignore stale async header completions. A UI regression test checks disabled import and out-of-order completion. |
| Renderer implementation rows and the template browser title leaked into normal UI | Cadmata product projection | Production inspector omits renderer counters/lanes; full import status avoids implementation labels; preserve partial-display warnings. Product title and neutral workspace caption replace template/milestone labels. |
| Both products made a missing default favicon request | Product HTML | Explicit empty favicon avoids console/network noise without shipping template branding. The asset audit attributes the earlier remaining 404s to `favicon.ico`, not a GPU shader failure. |
| Helios development fallback emitted `ResizeObserver loop completed with undelivered notifications` | Legacy fallback/layout boundary; exact observer not isolated | One legacy renderer mounts and the capture completes. The warning remains recorded; unsupported-browser resize behavior is not fully qualified and no hidden second graphics owner was introduced to mask it. |
| Authored analytic geometry still displays preview triangles | Product CIR artifact/provider seam | **Blocked:** missing qualified CIR artifact/admission projection; see below. |
| Guitar loses authored finishes; guitar/robot lack assembly topology lines | Assembly display projection | **Blocked:** DTO has face patches and transforms but omits authored appearance and engineering edge polylines. P4-01A confirms the robot fixture has no authored looks. Do not infer materials from names or edges from triangle diagonals. |
| House/factory cannot open as Scenes | Cadmata service / Web runtime build dispatch | **Blocked:** route to the existing Scene compilation owner and publish its occurrences/materials/cameras. |
| Threaded close-up has visibly missing shaded thread faces | Kernel display preparation | **Blocked:** real import and preparation succeed with `Partial`, 37 wire-only faces. Preserve the admission warning; fix the existing preparation/tessellation owner. |
| Guitar first display is slow; complete stutter comparison unavailable | Compile/serialize/upload product path | Recorded rather than claimed resolved. No speculative prewarm or performance rewrite. |
| Large generated mate identities clutter guitar inspector | Cadmata semantic inspector | Retained for diagnosis; presentation simplification remains unqualified. Do not alter semantic mate identity to beautify a screenshot. |
| Helios production bundle cannot use the installed SDK | SDK packaging | Separate AOT package prerequisite; development evidence is labeled accordingly. |

## CIR default and Auto policy

Production policy remains: **qualified engineering-supplied CIR artifact → CIR; otherwise → published mesh fallback**. Qualification must include field shader/ABI, bounds, transforms, material, engineering selection proxy and topology overlays. It cannot be inferred solely from a surface kind or an `analytic` display-lane label.

The current products cannot fulfill the first branch:

1. `aetheris.client/src/viewer/cadmataTelos.ts` draws analytic `previewMesh` data and returns `fields: []`.
2. `Aetheris.Server/Contracts/ApiContracts.cs` assembly display DTOs carry face patches, occurrence transforms and semantic selection members. They do not carry CIR artifacts/admission, authored appearance, topology lines, or cameras.
3. `Aetheris.Web.Runtime/sdk/src/index.d.ts` publishes mesh definitions/ranges/optional edges and occurrences. It has no CIR artifact/provider contract, appearance projection, or Scene camera projection.
4. `Aetheris.Server/Api/AssemblyDisplayService.cs` routes `.firmament` input to `AssemblyM1Pipeline`. House and factory requests both return HTTP 400, `assembly-parse-missing-root: Expected 'Assembly Name { ... }'.`
5. `Aetheris.Web.Runtime/Program.cs` dispatches only Assembly versus Part compilation. Opening the house in actual Helios fails while retaining the previous bracket and marking `MODEL OUT OF DATE`.

Existing sphere/cylinder/cone/torus/CSG qualification in `CIR-DISPLAY-X0` remains substrate evidence. This run rechecks a real GPU CIR-cylinder/mesh mixed-depth witness, transforms, perspective/orthographic projection, picking, and resource reuse. It does **not** relabel that witness as a sphere/cone/torus/CSG product sweep. Product assembly/Scene CIR occurrence and selection qualification remains blocked. No automatic CIR coverage percentage or completed Auto mode is claimed.

Smallest repair: extend the existing engineering-owned display contract/provider with qualified CIR artifacts and proxies; project them through the existing Telos adapters. Reuse the existing Scene session/compiler for Scene dispatch and preserve compiled occurrences, finishes, units, and cameras. Carry authored assembly appearance and kernel edge polylines through the existing DTO. Then re-run the requested primitive and showcase qualification. A renderer-only reconstruction would create a parallel authority and would not close these gates.

## AA, camera, grid, lighting and selection policies

Spatial AA is the production default. Meshes and thick lines use four-sample MSAA and one shared depth owner. Topology lines retain local derivative coverage; the GPU witness reads exactly four device pixels at DPR 1 and eight at DPR 2, at camera spans 3 and 10. Perspective/orthographic mixed depth and picking remain qualified in the shared witness.

`None`, `TAA`, and `TAAUtility` use one sample. Mode transitions rebuild the required attachments/pipeline bindings and pass real GPU readback without validation errors. Temporal modes remain opt-in developer experiments; no temporal mode is exposed in ordinary product controls or enabled by default. The existing center-ray CIR shader ABI does not gain analytic silhouette coverage simply by rendering to a multisampled attachment. Field-aware AA and coherent full CIR material response are not newly qualified here.

Fit uses authoritative display bounds, pane aspect, useful margin and product world-up. Camera tests contain every bounding-box corner in narrow and wide panes, both projection modes, at scales `1e-5`, `1`, and `1e6`. Actual orbit, wheel navigation, resize, named view, and face selection were exercised. Full pan/maximize/minimize/dock permutations and room/factory-scale navigation remain unqualified.

The shared neutral studio-like mesh shader remains restrained; selection blends amber rather than replacing all shading. Existing roughness/metallic mesh inputs remain intact. The default fallback material makes geometry legible, but uniform guitar appearance cannot substitute for its authored finishes. P4-01A confirms the robot fixture has no authored appearance bindings. Opacity and roughness/metallic parity across product CIR/mesh, house glass and mixed Scene materials are not qualified.

PMI remains CSS-pixel DOM text projected through the shared camera, with GPU leaders and published semantic targets. Slightly more line spacing improves readability. CTC03 datum selection passes without broadening engineering selection authority. Existing authoring overlays remain supported; this run does not qualify every construction-plane/preview case. The Helios face pick records its runtime face identity and explicitly does not assert a new compiler-owned source selector.

## Witnesses and visual review

| Real witness | Review and remaining limitation | Capture |
|---|---|---|
| Threaded hex bolt | Framed close view, curved thread silhouette and mesh edges visible. **Incomplete shading:** distal thread region becomes wire-only. Not presentation-worthy or CIR proof. | `final/mechanical-closeup.png` |
| CTC03 manufacturing AP242 | Thin sheet, openings and rounded bends readable; datum/GD&T panels have clear contrast and leaders. Selected Datum A remains selectable. Dense full PMI can still require category filters. | `final/cadmata-pmi.png`, `final/cadmata-selected.png` |
| GuitarX0 | Useful full-model framing; strings and composition visible; no initial all-model highlight. Authored woods/metal finishes and assembly edges absent; generated mate labels remain noisy. | `final/guitar.png` |
| Industrial Atlas | Useful Z-up robot pose; repeated occurrence hierarchy retained, recesses visible. Assembly topology lines absent. Subsequent P4-01A audit confirms this fixture has no authored appearance bindings; uniform material alone does not prove material loss. | `final/robot.png` |
| WarmModernHouse | Source compiles through CLI; Cadmata refuses Scene as Assembly; Helios retains last valid bracket with clear failed-build state. No actual house viewport exists to review glass/camera/materials. | `final/helios-house-refusal.png` is refusal evidence, **not a house showcase** |
| FactoryX0 | Source compiles through CLI; product display endpoint refuses Scene as Assembly. Factory presentation is unqualified. | `FactoryX0-packet.json` |
| Helios editable bracket | Hole form, topology outline, isometric fit, face selection and split view are legible. Failed rebuild retains display revision 0 while source revisions advance. | `final/helios-part.png`, `final/helios-selected.png`, `final/helios-last-valid.png` |

The supplementary `themes/` run checks Cadmata Atelier and light Monument rendering/PMI. Themes remain product presentation choices. The shared grid/lighting are intentionally simple; every legacy animated/theme lighting parameter is not projected by Telos.

The `before/` captures are genuine failed startup/asset-admission states. `final/` shows the repair, not a manufactured aesthetic comparison. There is no measured before/after silhouette-quality or performance comparison against a functioning pre-change product. The requested README/Release/VC screenshot set is **partial and not accepted**: the mechanical image is defect evidence, the guitar lacks authored finishes, and there is no house image. No screenshot was substituted from Blender, GLB, or a synthetic renderer. An earlier mistimed mechanical capture was discarded; the final script verifies the actual request name, successful display response and completed product loading before capture.

## Performance and resource evidence

`final/audit.json` contains measured browser instrumentation. These are end-to-end navigation/queue-completion timings, not GPU timestamp durations or a sustained FPS benchmark. Screenshot capture, browser scheduling and compilation can affect them.

| Measurement | Cadmata CTC03 | Helios bracket |
|---|---|---|
| Active render pipeline samples | 4 | 4 |
| Idle 500 ms | No new GPU submission | No new GPU submission |
| Resource inventory before/recovered | 543 buffers / 2 textures | 43 buffers / 2 textures |
| Live DPR change | 1506×1344 at DPR 2 → 2259×2016 at DPR 3 | 1036×1032 at DPR 2 → 1554×1548 at DPR 3 |

Twenty wheel-navigation steps measure Cadmata at 12.15 ms mean / 17.90 ms maximum and Helios at 6.73 ms mean / 8.60 ms maximum. The JSON also records CPU encode and queue completion distributions. Fresh guitar startup takes 17.59 seconds to first captured display and robot startup 6.31 seconds. These include browser/application load and capture overhead, and were measured while serial regression validation was active; they are intentionally not release performance budgets. The threaded import/export/display path also needs further performance qualification. A zero-idle-submit check and equal post-recovery inventory establish bounded lifecycle behavior, not a claim of leak-free arbitrary model switching.

The separate shared mixed-depth GPU witness passes with no GPU or console errors, retains one geometry definition across transformed occurrences, and releases buffers/textures on disposal/recreation. Full showcase camera-motion/stutter and Scene-switching qualification remain open.

## Validation and cleanup

- Release `.NET` solution build: passed, zero errors; two existing WASM SQLite warnings.
- Fast kernel lane: 1,005 passed.
- Full serial solution lane: **4,312 passed across 20 test projects; zero failed/skipped**.
- Telos build/typecheck and tests: **10 passed**, including aspect/scale framing and repeated-occurrence edge identity.
- Cadmata production build/typecheck and frontend suite: **86 passed across 18 files**, including the async STEP-selection regression.
- Helios development bundle/typecheck and frontend suite: **29 passed across 7 files**. Existing large Monaco bundle/CSS parser advisories remain; production AOT prerequisite is recorded separately.
- Shared real GPU qualification: passed, including four AA mode transitions, depth/readback, line DPR widths, picking, reuse and disposal.
- Actual product audit: orbit redraw, PMI/face selection, pane resize, live DPR change, zero idle submissions, forced device loss/restart, failed-build retention, explicit fallback and showcase startup all exercised.
- Final asset/theme smoke: both actual products start without console/page errors or missing assets; Cadmata's packaged WGSL responds HTTP 200 as `text/plain` (5,284 bytes). Repository layout guard and both repository diff whitespace checks pass.

Commands used include the required full gate:

```powershell
dotnet build Aetheris.slnx -c Release -m:1 --nologo -v:q
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
npm --prefix Aetheris.Web.Runtime/telos test
npm --prefix aetheris.client test
npm --prefix aetheris.client run build
npm --prefix ../HeliosCAD test
npm --prefix ../HeliosCAD run build:fast
npm --prefix Aetheris.Web.Runtime/telos run build:witness
node scripts/qualify-three-telos-browser.mts <playwright-module-path>
```

Cleanup retains one supported-browser Telos graphics/camera/depth authority, sample-specific pipeline caching, shader-module reuse and local resource ownership. The unsupported-browser renderer is lazy; forced R3F/Drei preload chunks were removed. Device recovery remounts the viewport rather than introducing a parallel loop. No background service, startup hook, renderer research, or new JudgmentEngine selector was added: these repairs are deterministic transformations/lifecycle fixes, and no competing qualified product CIR candidates exist to score.

**Executive answer:** the repaired shared mesh viewport is materially better, but automatic qualified CIR, complete canonical geometry, authored finishes and actual Scene presentation prevent saying that both products now have the requested finished Preview 4 viewport. Repair the display-provider prerequisites before aesthetic acceptance.
