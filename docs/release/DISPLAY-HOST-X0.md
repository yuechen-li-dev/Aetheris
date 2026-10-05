# DISPLAY-HOST-X0

Verdict: **Honest stop**. The requested shared production display host is not implemented. A real browser experiment isolated a dependency limitation before changing either working product.

## Ownership audit

Cadmata owns an R3F WebGL renderer in `aetheris.client/src/viewer/AetherisViewport.tsx`. Its background is a ShaderMaterial, its semantic edge path uses Drei's WebGL LineMaterial, and its text/PMI layers use material extensions. Its assembly packet has shared definitions and occurrence transforms, but its part path builds per-face meshes.

The actual external application is `C:/Users/yuech/source/repos/HeliosCAD`, specifically `src/viewport/Viewport.tsx`. It owns a separate WebGLRenderer, resize observer, geometry/material lifetimes, picking, and highlighting. It already shares mesh definitions among occurrences and draws SDK BRep polylines rather than inventing topology from tessellation. SDK selection resolution remains the semantic authority. Cadmata uses installed Three 0.183.2; Helios uses installed Three 0.186.0.

The SDK has no display host today. The managed direct WGSL backend is already qualified separately by VD-WGSL-X0; this task did not reopen that compiler or introduce DXC/Naga into browser execution.

## Attempt and exact blocker

The attempted architecture retained Three's existing mesh material implementation on an explicitly supplied GPUDevice, with host-created color/depth textures wrapped in the public ExternalTexture class and assigned to a public RenderTarget. The intended sequence was mesh submission, a raw WGSL pass loading the same color/depth attachments, and canvas presentation. No backend state mutation or private pass interception was used.

The minimal retained reproducer is `scripts/display-host-x0-attachment-probe.js` and its HTML page. Every run first renders a curved sphere with MeshStandardMaterial into ordinary Three-owned color/depth attachments on the same device, scene, and camera. This control succeeds with no captured validation errors. It then substitutes external attachments with explicit dimensions, Three format/type, and GPU render/texture/copy usage.

Real Edge browser results for **both installed versions**:

| Case | r183 / Cadmata | r186 / Helios |
| --- | --- | --- |
| Ordinary color + depth | Pass | Pass |
| External color + external depth | Required `GPUDepthStencilState.format` is undefined | Same |
| External color + ordinary depth | Required `GPUColorTargetState.format` is undefined | Same |

These are synchronous GPUDevice.createRenderPipeline descriptor errors, before successful mesh submission. They are not a WGSL compiler failure or lack of WebGPU on this machine.

In both dependencies, `src/renderers/webgpu/utils/WebGPUTextureUtils.js` takes an early return for `texture.isExternalTexture`, assigning `textureData.texture` and `initialized`, but omitting `textureData.format`. `WebGPUUtils.getTextureFormatGPU()` reads that backend metadata. Public `texture.format`, `type`, and `internalFormat` cannot populate the omitted metadata through this branch. The [current upstream source](https://github.com/mrdoob/three.js/blob/dev/src/renderers/webgpu/utils/WebGPUTextureUtils.js) inspected during qualification also retains the early return. The public [ExternalTexture documentation](https://threejs.org/docs/pages/ExternalTexture.html) does not establish external render-target compatibility.

There is also ownership drift: r183 destroys wrapped GPU textures during texture disposal; r186 excludes external textures from that destruction. Any eventual adapter must explicitly qualify texture ownership across its pinned dependency version.

## Decision and smallest repair

Do not ship a nominal shared renderer which still hides its real depth attachments inside Three. Do not monkeypatch private backend maps merely to make this experiment pass. Such a patch would also need to preserve metadata, attachment replacement, disposal, pipeline keys, and resize behavior across the two different installed versions.

The smallest concrete repair is a maintained, pinned Three dependency change establishing external render-target metadata and lifetime behavior, followed by overlap/load-store/resize/disposal browser tests. This is a dependency repair; a public API for externally owned render attachments would be preferable. Merely upgrading to the Helios installed version does not fix the demonstrated failure. This repair has not been implemented or qualified here.

The alternative is a shared host that owns raw mesh and semantic edge pipelines, retaining Three only for cameras, controls, and CPU raycasting. That requires replacing the current material/render submission boundary and qualifying appearance and product overlays; it is a larger renderer redesign, not a wrapper migration. The mission's honest-stop condition for Three limitations applies at this boundary.

Production Cadmata and Helios are preserved. No new SDK export, renderer fork, hidden WebGL fallback, or claimed CIR pass slot was installed. The discarded host prototype was reduced to an isolated diagnostic script.

## Reproduce

From the Aetheris repository root:

```powershell
./scripts/prepare-display-host-x0-probe.ps1 -HeliosRoot C:/Users/yuech/source/repos/HeliosCAD
python -m http.server 8766 --bind 127.0.0.1
```

Open these pages in a WebGPU-capable browser:

- `http://127.0.0.1:8766/scripts/display-host-x0-attachment-probe.html`
- `http://127.0.0.1:8766/scripts/display-host-x0-attachment-probe.html?ownedDepth`
- `http://127.0.0.1:8766/artifacts/local/display-host-x0/helios-probe.html`
- `http://127.0.0.1:8766/artifacts/local/display-host-x0/helios-probe.html?ownedDepth`

Preparation reads the explicit actual Helios installation and copies its installed Three build files to ignored `artifacts/local/display-host-x0/`; it does not change Helios. Browser reports and a screenshot are saved alongside dependency/build/test evidence there. No background service or startup hook is created.

## Qualification limits

The browser experiment qualifies the dependency failure and its successful ordinary-attachment control. It does **not** qualify either product on WebGPU, authored appearance parity, shared picking, semantic thick lines, a large scene, performance, shader caching, or a direct compiler artifact pass. No before/after product image or performance gain is claimed; those gates were not reached. All twenty acceptance requirements remain unaccepted as a production host capability.

Validation: JavaScript syntax check; all four real browser cases above with passing ordinary-attachment controls; Aetheris Release solution build; fast kernel lane **1,005 passed**; full serial solution lane **4,312 passed across 20 suites**, zero failed/skipped. Build retains two pre-existing WebAssembly SQLite warnings. The full corpus run rewrote only line endings in one generated STEP fixture; its content was checked and that test side effect restored.
