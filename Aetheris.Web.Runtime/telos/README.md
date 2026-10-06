# Three Telos

Three Telos is an engineering display layer that uses Three.js where Three.js is useful and replaces it where engineering visualization requires stronger guarantees.

Reuse upstream data structures. Own downstream GPU behavior.

Mesh and line shaders are standalone `src/shaders/*.wgsl` source files. The TypeScript loader fetches their static asset URLs before requesting a GPU device; load failures fail initialization with `telos-shader-load`. Package and native browser witness builds copy these files alongside the emitted modules. Vite rewrites the same URLs for production assets. WebGPU validates WGSL during module/pipeline creation. CIR field shaders continue through Copeland's typed VTS compiler.

This package owns the device, attachments, camera, ray query, raw mesh/field/line submission and lifetime. Three 0.183.2 supplies math and input adapters, never a GPU renderer. See `docs/release/THREE-TELOS-X0.md` and `docs/release/THREE-TELOS-X1.md` in the repository for qualification and product boundaries.

World-space meshes and lines accept explicit `depthMode`: `depth-tested`, `depth-biased`, or `always-on-top`. Overlay meshes can be unlit with alpha blending and do not write depth. Line widths are CSS pixels. Products may project DOM text with `camera.project`/`unproject`; `beforeFrame` supplies the host-owned projection cadence after camera matrices update and before GPU drawing. No React or font engine lives here.

`setDynamicLines` updates a bounded set of world-space lines identified by stable IDs. An unchanged ID, segment count and depth state retain vertex and uniform buffers. Call it within `beforeFrame`, or request `invalidate()` after an external update. Removing an ID releases its resources. `setScene` similarly retains unchanged draw/proxy resources across selection and filter updates. Immutable geometry arrays remain the definition identity contract.

`overlayId` carries product-owned pick identity. DOM buttons receive their own DOM events; GPU picking prioritizes always-on-top authoring meshes, dynamic overlay leaders, construction lines/surfaces and then model geometry. Hidden or depth-occluded overlays do not steal geometry picks. This supplies graphics interaction identity, not CAD selector authority.

## Experimental temporal AA

`host.setAA("TAA")` enables a fixed-weight, depth-rejected, neighborhood-clamped baseline; `host.setAA("TAAUtility")` enables the generated Copeland utility policy. `None`/`SpatialOnly` retain local primitive/line coverage without temporal jitter or resolve. Spatial AA is still the default: X0's supersampled witness found both temporal modes worse than spatial-only at silhouettes. See `docs/release/TELOS-TAA-X0.md` for the exact bounded blocker and evidence.

Optional debug values are `policy`, `confidence`, `motion` and `depth`; seed a color view first. Debug reads frozen color history and never accumulates its visualization. The shared host owns eight bounded physical-pixel Halton samples, surface color/depth history, reprojection and invalidation. Picking, PMI and overlay projection use the logical camera. Scene/appearance/occurrence updates through `setScene` reset history; object-transform reprojection is not claimed. Topology and authoring overlays render after resolve. Temporal textures remain allocated after switching back to spatial mode until resize/disposal.

The actual decision source is `fixtures/three-telos/temporal-policy.v.ts`. Regenerate the packaged policy using `scripts/prepare-telos-temporal-policy.ps1` with an explicit Copeland checkout; this compiler is an offline build input, not a browser dependency. Missing optional temporal assets or pipeline support preserve spatial startup. Timestamp-query timing is optional. Qualification must submit `readPixels()` immediately after `render()` before yielding a browser frame, because the canvas swap texture can expire.
