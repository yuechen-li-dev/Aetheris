# CIR-DISPLAY-X1 — product integration boundary

Status: **Honest stop**, 2026-10-05. Cadmata and Helios cannot yet use CIR as
their default visual path. X0 remains qualified as a standalone GPU witness;
there is no X1 product-rendering qualification.

The audit found the render-graph migration described by the mission's honest-stop
criterion. Neither product currently supplies a shared WebGPU attachment/pass
owner. Adding a second canvas would leave CIR and mesh with independent depth;
switching Cadmata's renderer alone would break existing topology overlays and
background materials. No such workaround was installed, and Auto was not enabled.

## Reproducible evidence

Run from the repository root, after installing the client dependencies and
generating the X0 artifacts:

```powershell
node scripts/audit-cir-display-x1.mjs <HeliosCAD-root> artifacts/local/cir-display-x0/Sphere-fragment.wgsl
```

The explicit Helios path identifies the actual external product checkout rather
than substituting `demos/Aetherion.Helios`. The audit only reads that checkout.
Its JSON output defaults to ignored
`artifacts/local/cir-display-x1/compatibility-audit.json`.

This is a CPU compatibility probe against the installed Three implementation,
not a browser render, a mixed-depth test, or a production API dependency. The
probe imports private Three parsers solely to expose their actual behavior.

Observed with Three **0.183.2**:

| Existing input | WebGPU node conversion |
| --- | --- |
| `MeshStandardMaterial` | `MeshStandardNodeMaterial` |
| `LineBasicMaterial` | `LineBasicNodeMaterial` |
| `ShaderMaterial` | `null` |
| Drei's installed `LineMaterial` | `null` |

The actual generated `Sphere-fragment.wgsl` fails `WGSLNodeFunction` construction
with `FunctionNode: Function is not a WGSL code.` A standalone control function
parses successfully. Naga emits a complete module with structures, module state,
helpers and an annotated fragment entry point. Three's `wgslFn` interface expects
a function declaration; it is not a complete-module pipeline integration API.
This does **not** invalidate the X0 WGSL, which already ran on WebGPU.

Three's `NodeBuilder.build()` reports an incompatible material and substitutes
a generic node material when conversion returns null. That substitution cannot
preserve the existing GLSL background or thick-line shader behavior. Three's
[official migration documentation](https://threejs.org/manual/pages/webgpurenderer.html)
also explicitly requires migrating `ShaderMaterial`, `RawShaderMaterial` and
`onBeforeCompile` customizations to node materials/TSL.

## Actual ownership and coupling

| Responsibility | Cadmata today | Actual HeliosCAD today |
| --- | --- | --- |
| Renderer/frame ownership | R3F `Canvas` in `aetheris.client/src/viewer/AetherisViewport.tsx`, default WebGL | `THREE.WebGLRenderer` in external `src/viewport/Viewport.tsx` |
| Display input | Server analytic face packets and assembly display packets | Public SDK `ModelSession.mesh` |
| Definition sharing | Assembly mesh definitions/occurrences | One `BufferGeometry` per SDK definition, meshes per occurrence |
| Camera | Product orthographic camera, orbit and zoom | Product perspective/orthographic cameras and OrbitControls |
| Topology overlay | Server BRep wire points rendered by Drei `Line` | SDK BRep edge polylines rendered as `LineSegments` |
| Picking | Existing part pick-ray path and assembly occurrence interaction | Raycaster plus SDK `describeSelection` / `describeEdgeSelection` |
| Highlight | Existing face/edge/occurrence overlays | Occurrence tint and face/edge mesh overlays |
| Presentation | Theme lights/materials, custom GLSL background, axis text | Standard mesh materials, theme/grid, selection tint |

Cadmata's `EdgeLine` and axis guide use Drei's thick `Line`; their installed
implementation constructs the unsupported `LineMaterial`. Its theme background
constructs `ShaderMaterial`. Axis/PMI text also needs migration qualification:
the installed Troika derived-material implementation customizes
`onBeforeCompile`, another unsupported WebGPU seam. Text was inspected in source,
not independently rendered in this audit.

The actual external Helios checkout is ahead of the in-repository demo in edge
ownership: it draws kernel-provided semantic edge polylines, not mesh triangle
adjacency. The repair must preserve those polylines and their SDK edge identities.
Picking is **not** the fundamental blocker found here: its mesh proxy and stable
SDK selection mapping are suitable to retain during a visual-path migration.

Existing WebGL depth attachments cannot be bound as WebGPU render-pass depth
attachments. Thus a separate CIR canvas or a final color overlay would not meet
mixed-depth acceptance. The products need one device and explicit shared
color/depth attachment ownership for opaque mesh and CIR passes.

Three's public [ExternalTexture API](https://threejs.org/docs/pages/ExternalTexture.html)
and render-target APIs are promising seams for externally owned GPU textures.
Their existence does not prove the required depth, resizing, disposal, MSAA,
tone-mapping or fallback behavior. This audit does not claim Three makes the
integration impossible. It identifies the larger shared-host migration needed
before a product-safe implementation can be qualified.

## Compiler-host boundary

`Aetheris.Web.Runtime` targets `browser-wasm`. Its public `DisplayMesh` contract
contains definitions, occurrences, transforms, triangle selection ranges and
optional semantic edges; it contains no CIR shader artifact or provider.

X0's `WebGpuGraphicsBackend` invokes DXC through the existing backend and Naga
through `System.Diagnostics.Process`, with temporary files. That is a native
artifact compiler, not an executable browser-WASM service. Referencing that
backend from the browser SDK would not make dynamic authored shader compilation
available there. The existing server may host the same compiler for connected
clients, but standalone Helios needs an explicit capability/provider contract
and deterministic mesh fallback when that provider is absent. Fixed primitive
shader bundles would not cover arbitrary structural hashes and authored CSG.

There is also no existing X0 production cache to reuse: X0 supplies the structural
key and compatibility seam, not a measured cross-window artifact cache. X1 must
add that cache at the native artifact-provider boundary and a device-specific
pipeline cache in the shared browser host.

## Smallest architectural repair

These are proposed boundaries, not implemented capabilities:

1. Establish one framework-neutral display host exported by the Aetheris SDK
   (a separate optional Three adapter export). It owns WebGPU initialization,
   device/attachments, resize, frame scheduling, disposal and the legacy mesh
   fallback. Cadmata's R3F wrapper and Helios's imperative wrapper supply their
   existing scene/camera/interaction state; neither owns CIR shader semantics.
   Migrate Cadmata's thick topology lines, theme background and text to compatible
   Three materials in that host integration before replacing its default renderer.
2. Qualify that host with the **existing X0-generated modules** and ordinary Three
   meshes sharing physical depth, first in both depth orders and overlapping
   ranges. Establish projection/depth conventions, resource lifetime and resize
   behavior before adding authored-model routing. Then integrate product-camera
   ray generation through the existing Visual TS → VD-MIR → DXC → Naga path.
   Do not strip or rewrite Naga output to fit `wgslFn`.
3. Add a shared definition/occurrence descriptor and native shader-artifact
   provider, consumed by the server and SDK projection adapters. Wire authored
   compiler output into it; do not recover CIR from tessellated triangles or
   renderer-side primitive guesses. Preserve BRep meshes as selection proxies,
   semantic edge polylines as overlays, and existing authored appearance authority.
   Only then qualify Auto/CIR/Mesh dispatch and enable Auto for admitted definitions.

The proposed pass sequence is background/clear, opaque mesh and opaque CIR using
the same depth attachment, topology overlays with depth testing, selection
overlays, and gizmos. Opaque correctness must not depend on mesh/CIR submission
order. Transparent content remains on the mesh path and requires a separately
qualified final transparent pass. No PBR engine or CIR interpreter is proposed.

## Architecture review

| Question | Decision for the repair |
| --- | --- |
| CIR packet owner | Shared engine display projection: authoritative authored CIR + engineering definition identity; server/SDK serialize the same model |
| Shader artifact cache | Native artifact provider, keyed by CIR structure plus full compiler/render compatibility identity |
| WebGPU pipelines | Shared browser display host, partitioned by device and render-target compatibility |
| Product sharing | SDK Three adapter with thin R3F/imperative wrappers |
| Visual versus selection authority | CIR draws opaque surfaces; invisible mesh proxies retain existing SDK/BRep selection; BRep polylines retain edge identity |
| Mesh fallback | Missing provider/WebGPU, unsupported CIR, compiler/pipeline failure, transparent or imported/recovered geometry |
| Shader invalidation | Field/structure or shader compatibility changes; not camera, rigid occurrence transform or material binding changes |

Proposed occurrence buffers invalidate on placement changes; appearance bindings
on authored appearance/highlight changes; bounds on geometry or placement changes;
picking proxies on BRep/mesh revision changes. Reloads should retain compatible
artifact keys while removing obsolete device resources. These rules remain
unqualified until implemented and tested; no cache-hit or 100-instance claim is made.

## Friction log

| Friction | Workaround accepted? | Correct owner / permanent repair |
| --- | --- | --- |
| Two WebGL frame owners, no shared GPU pass | No second CIR renderer/canvas | Shared SDK display host and product wrappers |
| Independent WebGL/WebGPU depth | No color-only composition or pass-order trick | One WebGPU device and shared attachments |
| Unsupported Cadmata background/thick lines/text | No generic-material substitution or disappearing overlays | Cadmata presentation adapters migrated to supported node materials |
| Full Naga module versus `wgslFn` | No output stripping or handwritten field evaluator | Custom compiled-module pass in the shared host |
| Browser-WASM cannot run native compiler subprocesses | No sibling executable assumption in SDK | Explicit native artifact provider; standalone mesh fallback |
| Definition packet/cache absent | No per-occurrence/per-frame compiler | Shared engine projection and versioned artifact/device cache |
| Transform/projection agreement | No X0 orthographic witness camera reused as product camera | Product camera/occurrence bindings, qualify near/far and extreme zoom |
| Appearance agreement | No CIR-only material semantics | Existing authored appearance projection; qualify shared lighting |
| Overlay alignment and picking | No new face identity system | Preserve existing BRep polylines/proxies; qualify after host migration |

## Validation and limitations

The compatibility probe completed and produced the findings above. The dedicated
quality audit found no production renderer duplication added by this work. The
diagnostic script has no production imports/callers; generated evidence stays
under ignored `artifacts/local/`. Existing X0 and unrelated factory-scene work
were preserved. No shader frontend/backend changes were needed for this audit.

Validation logs are under `artifacts/local/cir-display-x1/`:

- Release solution build: passed, zero errors, two warnings.
- Kernel fast lane: 1,005 passed, zero failed/skipped.
- Full serial solution lane: 4,312 passed across 20 projects, zero failed/skipped.
- Cadmata production build: passed; 82 tests passed across 16 files.
- External Helios development build: passed; 29 tests passed across 7 files.

No product CIR pixels, mixed-depth/selection/overlay captures, performance figures,
memory measurements, resource-reuse counts or browser X1 qualification were
produced. Prior X0 browser evidence is documented in `CIR-DISPLAY-X0.md`; it must
not be presented as X1 product evidence. Product acceptance criteria remain unmet.

Executive verdict: **Honest stop** at the shared render-host migration. The
shader chain is preserved, the exact existing material and host coupling is
reproducible, and the smallest repair is identified. A partial packet or a
second renderer would not satisfy the mission's meaningful-progression criteria.

## Subsequent compiler boundary repair

VD-WGSL-X0 subsequently added Copeland's managed `Copeland.TS.Backend.Wgsl`
assembly and demonstrated the real `.v.ts` frontend plus direct WGSL emission
inside browser WASM. All five X0 fields executed without native compiler tools.
See Copeland's `docs/release/VD-WGSL-X0.md` for qualification evidence.

The native artifact-provider requirement above describes the earlier X0 path;
it is no longer necessary for a browser host that adopts this direct backend.
The existing Aetheris SDK has not yet adopted it. Shared render-host/depth ownership
and compatible Cadmata overlays remain the X1 product integration boundary.
