# THREE-TELOS-X0

Verdict: **Meaningful progression**, 2026-10-05. The external-attachment blocker is removed by an owned raw WebGPU substrate, implemented in strict TypeScript. Real Edge browser tests qualify a direct-WGSL CIR cylinder, an ordinary mesh, shared depth in both occlusion directions, thick topology lines, resource reuse and recreation. Cadmata's normal DisplayScene/assembly path and Helios use the same package. Full acceptance is withheld: Cadmata's semantic authoring overlays and PMI still require the explicitly transitional R3F viewport.

## Audit before implementation

The primary Helios application is the sibling `HeliosCAD` repository, not only the older `demos/Aetherion.Helios` example. Both were inspected and integrated. This mission explicitly includes Helios; no Copeland source, kernel semantics, or unrelated repository was changed.

| Area | Cadmata before | Actual Helios before | Telos owner / retained dependency |
| --- | --- | --- | --- |
| Three | installed 0.183.2; caret range | installed 0.186.0; `latest` | all three clients and Telos pinned to 0.183.2; Helios types also pinned |
| Renderer / loop | R3F Canvas and useFrame | WebGLRenderer and dirty/damping RAF | TelosDevice, TelosHost invalidation, no Three GPU renderer |
| Background / grid | ShaderMaterial background; adaptive orthographic grid | scene color, GridHelper rotated to XY | solid clear and camera-ray reference grid; animated themes remain a legacy feature |
| Mesh / appearance | per-face MeshStandardMaterial; assembly shared definitions | BufferGeometry shared per definition; MeshStandardMaterial | immutable definition buffers and occurrence uniforms; small TelosMaterial |
| BRep edges | Drei Line / WebGL LineMaterial and artifact overlays | SDK edge polylines; LineSegments; selected TubeGeometry | screen-space segment quads; SDK edges retain their IDs |
| Pick / highlight | camera Raycaster ray to server BRep query; R3F occurrence events | CPU Raycaster, SDK describeSelection/describeEdgeSelection | Telos worldRay and TelosPick; server/SDK semantic selection authority retained |
| Camera / navigation | R3F authoritative orthographic camera and OrbitControls | perspective/orthographic cameras and OrbitControls | TelosCamera and direct input navigation; output-only Three camera adapter |
| Scene ownership | R3F hierarchy including authoring/PMI layers | Three Group built from WASM mesh snapshots | plain typed TelosScene, independent input adapters |
| Lifetime / resize | R3F lifetime plus material effects | renderer, observer, RAF, geometries, materials in wrapper | Telos resources, depth recreation, navigation listeners and observer disposal |
| WASM | server display packets | ModelSession typed-array snapshot, SDK selection resolution | unchanged; graphics does not interpret Firmament/CIR semantics |

Duplicated product code included camera creation, ray construction, resize, render scheduling, geometry upload, material conversion and edge rendering. Those graphics responsibilities now reside in Telos. Product wrappers still interpret their own packet and selection contracts and route DOM events. Legacy implementations are retained in files named `Legacy*`, with a visible transitional diagnostic.

Three remains useful for matrices, vectors, quaternion math, geometry containers, CPU triangle queries and loaders via the Three scene adapter. The DISPLAY-HOST-X0 retained probe documents external attachment format metadata and lifetime limitations in the installed Three WebGPU renderer. Cadmata's WebGL thick lines/background and Text/Html layer dependencies also block a renderer-only substitution. Telos avoids the attachment limitation entirely by submitting raw WebGPU passes. No backend maps, renderer private state, monkey-patches or Three fork are used.

## Ownership and contracts

Installed dependency inspection also finds Three 0.170.0 nested under Cadmata's Drei `stats-gl` dependency. This is a bounded legacy profiler dependency, not the version used by Telos or either product's main Three import. Direct product/Telos versions are unified at 0.183.2. Forcing an unrelated profiler upgrade was not required for the shared host; the nested version remains documented until the R3F compatibility dependency is removed.

The standalone package is `Aetheris.Web.Runtime/telos`, named `@aetheris/three-telos`. It builds TypeScript with strict checking, emits declarations, and can be packed independently. Its browser implementation has no dependency on the Aetheris WASM runtime, Firmament, CIR parsing or a Copeland checkout.

`TelosDevice` owns adapter/device/context, the preferred canvas format, actual device features/limits, validation diagnostics and loss handling. One device is created per viewport through this owner; product wrappers never request devices. Multiple simultaneous viewport device sharing is not an X0 capability.

`TelosFrame` owns the presented color texture/view and one `depth32float` attachment. Canvas color is acquired per frame; depth is retained until dimensions change. Depth clears to 1, mesh/field compare `less` and write depth, topology compares `less-equal` without writing, and overlays compare `always` without writing. Passes load/store the same attachments. Single-sample only; MSAA is deferred. Resize uses CSS size/DPR, clamps texture dimensions to device limits, destroys the old depth texture and increments an observable generation. Pass viewport/scissor defaults cover those attachment dimensions.

The explicit order is background clear, optional reference grid, opaque mesh, field, topology and overlay, then submission/presentation. Selection tint is incorporated in mesh/field/line inputs. Pipeline construction is separated in `pipelines.ts`; pass kinds determine formats, vertex layouts, bindings, depth behavior and cache identity. This is a bounded pass sequence, not a general render graph.

`TelosCamera` owns position, target/up, perspective/orthographic projection, aspect, span, near/far and view/projection inverses. Matrices are column-major, the world is right-handed, clip depth is WebGPU [0,1], and screen coordinates are CSS pixels. Project/unproject and canonical near-plane world rays are shared by picking, fields, grids and navigation. `toThree()` returns compatibility state; mutating it cannot change Telos. The reference grid intersects these rays with XY/XZ planes and bounds its generated segment count, including near-horizon views. It has no orthographic-only camera cast.

The mesh path uploads positions, normals and uint32 indices per immutable definition ID. Occurrences carry separate model/inverse-transpose normal transforms and material uniforms. Base color, opacity, roughness and metallic drive a small directional shading model, not full MeshStandardMaterial/ACES/PBR parity. Transparency uses ordinary blending without sorting; advanced transparency and shadow parity are unqualified. Geometry IDs must identify immutable geometry within a scene. Assembly selection updates retain mapped arrays through a WeakMap packet cache.

Topology lines use one instanced quad per segment. Width is in CSS pixels and is independent of zoom/DPR. Near-plane crossings are clipped before division by w. The documented normalized-depth bias is 2e-6; lines do not alter authoritative surface depth. This is display bias, not engineering geometry. Hidden-edge occlusion is checked in the browser witness. Field overlays use supplied BRep data, never inferred tessellation diagonals.

`TelosIdentity` carries occurrence/body/definition/face/edge identities from the source. `TelosPick` exposes a CPU implementation behind a typed hit contract, uses Telos rays, resolves mesh face ranges, transforms world normals, and admits engineering mesh proxies for field picking. Edge queries use projected pixel distance and nearest-surface rejection. Direct field face semantics remain deferred. Helios resolves hits through the SDK; Cadmata's server BRep query receives the same canonical ray. Graphics does not invent source selectors.

`fromDisplayMesh` adapts SDK definitions, occurrences, transforms, ranges and kernel edge polylines. Cadmata adapts its DisplayScene/assembly packets separately. `fromThree` is a one-way data adapter from hierarchy/BufferGeometry, including GLTF-loader results; Telos does not store Object3D scene state. Multiple Three materials/interleaved attributes are not qualified by this adapter. No USD loader or additional scene ecosystem is introduced.

## Direct WGSL / CIR field witness

The retained compiler qualification path is:

```text
existing CIR SdfCylinderNode(0.8, 2)
  -> CirVisualTsLowerer (existing CIR tape)
  -> Field + typed Aetheris.Kernel.Firmament/Display/telos-field.v.ts
  -> Copeland parser/binder -> VD-MIR -> WgslGraphicsBackend
  -> artifact -> Telos Field pass -> real Edge WebGPU
```

The artifact includes semantic shader hash, WGSL, vertex/fragment entrypoints, canonical binding metadata, capabilities, CIR source identity and compiler source mappings. The browser consumes WGSL without DXC, Naga, native compiler processes or CIR interpretation. The qualification script takes an explicit Copeland checkout only as an offline compiler input, and changes no files there.

The direct compiler currently permits named canonical material shapes rather than arbitrary uniform records. An initial depth-row uniform record was rejected with `COPE-GPU-MATERIAL-0003`. X0 therefore uses the supported 32-byte tint/roughness material binding at group 0/binding 0. Camera-local near/far ray data, local-to-clip Z/W rows and scale-relative epsilon use typed vertex attributes in the documented 72-byte field ABI. These six vertices are updated per occurrence/frame; WGSL and pipelines are never generated per camera or occurrence. Field tint/opacity are consumed; roughness is transported but the smoke shader does not implement a full material model.

The cylinder shader conservatively steps the existing qualified distance function along the local ray, computes a hit normal, and writes `clipZ / clipW` at the actual hit point. Mesh and field thus use the same camera and depth convention. Rigid occurrence transforms are admitted; affine scale/shear is explicitly rejected with `telos-field-transform-not-rigid`. Bounds and a mesh proxy are part of the field contract. Bounds currently control epsilon and fitting, not ray-box acceleration. The smoke shader has a fixed 512-step budget. Full CIR-DISPLAY-X1B packet/admission/field default qualification is not claimed.

Shader modules reject an ID reused with different WGSL. Pipeline keys include semantic shader identity, pass state, color/depth formats and sample count. Immutable buffers, modules and pipelines belong to the device resource manager. Viewport disposal destroys buffers/textures and clears caches; device loss stops scheduling and emits a diagnostic. Remounting the viewport creates a new device/frame while product model state remains outside the graphics owner. Automatic loss recovery is deferred.

## Product integration and exact remaining blocker

Cadmata's `AetherisViewport.tsx` routes normal DisplayScene and assembly packets to Telos on WebGPU browsers. Grid, axis strokes, face/edge highlighting, orbit/pan/zoom, canonical server pick ray and occurrence picking use this host. The actual Helios wrapper and the older demo also use this package for compiled meshes, kernel edges, navigation, model refresh, hover, face/edge selection and a basic overlay slot. FRONT/TOP/RIGHT commands explicitly choose orthographic projection. Wireframe currently displays semantic topology strokes rather than tessellation diagonals. Screenshot capture remains a product canvas operation.

**The next isolated blocker is the Cadmata authoring/PMI scene adapter.** `CadmataOverlay.tsx` uses Drei Line and R3F mesh event handlers for concept points/planes/profile guides; `PmiAnnotationLayer.tsx` uses Drei Html/Line plus useThree/useFrame for screen label layout. Their existing stable entity selection, draggable callouts and visibility rules need a Telos overlay/DOM projection adapter using `camera.project`, canonical picking and shared line/mesh inputs. The new wrapper deliberately selects `LegacyAetherisViewport` when `cadmataArtifact` is supplied and labels this reason. This preserves working authoring views instead of dropping annotations or making a second new GPU renderer. Concrete dependency remains visible in these two modules; neither is accepted as migrated.

The smallest next repair is that adapter, including screen labels and authoring-entity hit routing, followed by the existing PMI/overlay tests and a real authored-model selection/label witness. Full product parity and removal of the authoring fallback remain required for Accepted. CIR smoke is qualified in the shared substrate witness; automatic product field packet construction/default selection remains the stated X1B work.

## Qualification

Evidence is generated under ignored `artifacts/local/three-telos/`, with build/unit/.NET logs in `artifacts/local/three-telos-*.log`.

| Gate | Evidence / limit |
| --- | --- |
| Shared camera | TypeScript tests: perspective/orthographic project-unproject, ray, aspect, scales 1e-5/1/1e6, output-only Three adapter |
| Mixed depth | Real Edge/NVIDIA Ampere: center-pixel readback matches field-only when field is ahead, mesh-only when mesh is ahead; both projections; mesh-mesh control passes. Bounded smoke, not full image/corpus qualification |
| Lines | 4 CSS px measures 4 device px at DPR 1 and 8 at DPR 2, at spans 3 and 10; transformed lines, hidden-line rejection, selected style |
| Reuse | Two mesh occurrences: one definition buffer set. Two fields: shared shader module/pipeline. Three total mesh/line/field modules and pipelines |
| Lifecycle | Depth generation increases on resize; disposal clears tracked buffers/textures; recreated viewport renders the same scene without captured GPU errors |
| Grid / camera stress | Orthographic span 0.01 and 10000 with reference grid; orbit-view matrix and unusual clipping; no captured validation error. This is not exhaustive large-coordinate visual qualification |
| Helios real wrapper | Browser WASM compiles fixture Box, renders through actual product Viewport, picks SDK `face(+Z)`, mouse orbit and wheel zoom redraw, rebuilds 40mm to 45mm, picks same selector at revision 2; no page errors/alerts |
| Cadmata real wrapper | Browser DisplayScene fixture renders mesh/edge, highlights and emits canonical pick ray; no errors/alerts. This does not prove full backend import UI or authoring/PMI |
| Unit regressions | Telos 4 passed; Cadmata 83 passed; actual Helios 29 passed; demo Helios 26 passed |
| Builds | Telos strict TypeScript, Cadmata production/lint, actual Helios and demo development bundles pass. Demo production now explicitly requires the existing AOT SDK; production AOT packaging is not claimed |
| .NET | Release solution build: zero errors; final incremental build retains two pre-existing WebAssembly SQLite warnings. Fast kernel 1,005 passed. Full serial lane: 4,312 passed across 20 suites, zero failed/skipped |

One full corpus test rewrote only line endings in a generated STEP fixture; the byte content was verified apart from line endings and restored. No kernel implementation changed.

Representative browser measurement: device init 181 ms, first mesh+field submission completion 43 ms, mesh upload CPU 0.3 ms, field pipeline creation CPU below 0.1 ms timer resolution, steady submission completion mean 2.86 ms across 20 frames. These are one-machine smoke measurements at small resolution, not GPU timestamp profiling or product throughput claims. Line upload timing is collected separately during line witnesses; CPU timer granularity can report zero. No large house/warehouse qualification or performance gain is claimed.

## Friction log / permanent owners

| Friction | Workaround / Telos fix | Permanent owner / disposition |
| --- | --- | --- |
| Three external attachment format metadata | raw mesh/field/line pipelines avoid its GPU backend | Telos attachments; no upstream patch needed |
| WebGL LineMaterial/background | owned quads and solid clear/reference grid | Telos; animated background parity deferred |
| Camera/grid orthographic assumptions | one owned projection/ray API, bounded plane-ray grid | Telos; tests cover projections/scales |
| Direct compiler canonical-only material records | supported tint/roughness uniform, typed ray/depth vertex payload | general frame uniforms belong to Copeland; no compiler rewrite or unrelated material-shape abuse |
| Product authoring Text/Html/events | explicit legacy route preserves current behavior | Cadmata overlay/PMI adapter; next acceptance blocker |
| Resource lifetime / queued frame race | explicit render consumes pending invalidation; readback snapshots dimensions before awaiting mapping | Telos; resize/dispose/recreate browser witness |
| Selection reset TOP camera up vector | initialize product world-up only on viewport creation; selection updates preserve owned camera state | Helios wrapper; live selection/orbit/zoom/rebuild witness rerun |
| Helios demo stale local SDK tarball | refresh explicit local package after packing | SDK install workflow; `npm install` alone reused cached same-version tarball |
| Demo production build requirement | use development bundle for this renderer gate | existing SDK requires AOT for production; no false production claim |
| Cadmata fixture outside Vite root | opt-in `AETHERIS_TELOS_WITNESS=1` narrowly admits fixture directory; standard React dev preamble | qualification only; current fixture policy preserved |
| Thin-line / hidden-edge and transform uncertainty | pixel-width and occlusion readbacks; rigid field admission | Telos; extreme depth-bias/large model qualification remains bounded |
| Field selection semantics | typed hit abstraction accepts engineering mesh proxy | SDK/BRep correspondence; perfect field face picking deferred |

## Cleanup and fork decision

Cleanup separated pipeline descriptors from host sequencing, kept math/camera/rays in one module, moved product packet interpretation to adapters, retained immutable assembly geometry across selection changes, and centralized device/frame/resources/navigation. All new implementation and test logic is TypeScript; WGSL is shader source and C# is the offline compiler witness. HTML contains only normal browser module/development bootstrap.

**Is Three.js still helping more than it hurts? Yes, as a utility and ecosystem dependency.** Maintained Three patches: **0**. Patch invasiveness: **none**. Renderer internals depended upon by Telos: **0**. Upgrade risk is concentrated in public math/geometry/raycast/input adapter APIs, with one pinned version and camera/picking regressions. Legacy R3F views retain their existing dependencies until removed. A hard fork is not justified. Three can be reduced later without changing the plain scene/shader contracts or raw WebGPU passes.

Executive answer: **the shared, Three-renderer-independent WebGPU substrate now works, including direct-WGSL CIR and mixed depth; full Cadmata/Helios X0 product acceptance is not yet earned because Cadmata authoring/PMI still selects the legacy host.** This is Meaningful progression, not Accepted.

## Reproduce

```powershell
# Aetheris root
./scripts/prepare-three-telos-cir.ps1 -CopelandRoot ../Copeland
cd Aetheris.Web.Runtime/telos
npm ci
npm test
npm run build:witness
cd ../..
node scripts/qualify-three-telos-browser.mts ../HeliosCAD/node_modules/playwright/index.mjs

# Product witness: foreground terminals, stop after qualification
# HeliosCAD: npm run dev -- --host 127.0.0.1
# Aetheris/aetheris.client:
# $env:AETHERIS_VITE_HTTP='1'; $env:AETHERIS_TELOS_WITNESS='1'
# npm run dev -- --host 127.0.0.1
node scripts/qualify-three-telos-products.mts ../HeliosCAD/node_modules/playwright/index.mjs http://127.0.0.1:5173

dotnet build Aetheris.slnx -c Release -m:1
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
```

Build Telos before installing/building a consumer; npm file dependencies reference its compiled `dist` export. Fresh consumers need their ordinary dependencies and local SDK package installed. The browser script owns its temporary HTTP server and closes it in `finally`. No persistent/background service, scheduled task or startup hook is created.
