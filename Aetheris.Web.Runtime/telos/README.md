# Three Telos

Three Telos is an engineering display layer that uses Three.js where Three.js is useful and replaces it where engineering visualization requires stronger guarantees.

Reuse upstream data structures. Own downstream GPU behavior.

Mesh and line shaders are standalone `src/shaders/*.wgsl` source files. The TypeScript loader fetches their static asset URLs before requesting a GPU device; load failures fail initialization with `telos-shader-load`. Package and native browser witness builds copy these files alongside the emitted modules. Vite rewrites the same URLs for production assets. WebGPU validates WGSL during module/pipeline creation. CIR field shaders continue through Copeland's typed VTS compiler.

This package owns the device, attachments, camera, ray query, raw mesh/field/line submission and lifetime. Three 0.183.2 supplies math and input adapters, never a GPU renderer. See `docs/release/THREE-TELOS-X0.md` and `docs/release/THREE-TELOS-X1.md` in the repository for qualification and product boundaries.

`fromDisplayMesh` projects compiler-qualified definitions with matching generated
shader artifacts into normal fields. It retains authoritative bounds, occurrence
transforms, resolved material and an invisible BRep picking proxy. A visual mesh
stays active while Telos validates the module and pipeline, and on rejection.
Telos compiles WGSL for the GPU; it never compiles CIR or generates shader source.
The shared typed field source lives in `Aetheris.Kernel.Firmament/Display/telos-field.v.ts`.

`telos-field-rays/2` adds metallic at vertex location 6; tint/opacity and roughness
keep the canonical 32-byte material uniform. The prior rays/1 substrate ABI remains
supported. Field lighting is a bounded diffuse/specular approximation, rather than
pixel-identical mesh shading or a PBR qualification. Rigid occurrences are admitted;
non-rigid product occurrences retain their mesh fallback.

`host.inspectDisplay()` and the canvas `data-telos-display`/`data-telos-field-draws`
attributes expose definition/binding state, actual fields and mesh surfaces,
separate picking-proxy counts, submitted fields and GPU cache counts. Shader source
is not dumped. Obsolete field program references and draw buffers are released
when geometry changes; unchanged occurrences/materials reuse program state.

World-space meshes and lines accept explicit `depthMode`: `depth-tested`, `depth-biased`, or `always-on-top`. Overlay meshes can be unlit with alpha blending and do not write depth. Line widths are CSS pixels. Products may project DOM text with `camera.project`/`unproject`; `beforeFrame` supplies the host-owned projection cadence after camera matrices update and before GPU drawing. No React or font engine lives here.

`setDynamicLines` updates a bounded set of world-space lines identified by stable IDs. An unchanged ID, segment count and depth state retain vertex and uniform buffers. Call it within `beforeFrame`, or request `invalidate()` after an external update. Removing an ID releases its resources. `setScene` similarly retains unchanged draw/proxy resources across selection and filter updates. Immutable geometry arrays remain the definition identity contract.

`overlayId` carries product-owned pick identity. DOM buttons receive their own DOM events; GPU picking prioritizes always-on-top authoring meshes, dynamic overlay leaders, construction lines/surfaces and then model geometry. Hidden or depth-occluded overlays do not steal geometry picks. This supplies graphics interaction identity, not CAD selector authority.

## Native presentation

`setPresentation(TelosPresentation)` supplies a static procedural backdrop,
key/fill/hemisphere mesh lighting, roughness highlights, coloured rim light,
selection accent and physically spaced minor/major reference-grid palettes.
Backdrop/grid colours are display encoded; lighting colours are linear.
`background.wgsl` and the mesh shader are real packaged source assets. One
fullscreen triangle renders the backdrop without writing authoritative depth;
one pipeline per sample/attachment configuration serves every preset.
Presentation never changes camera, topology or source selection. An update
resets optional temporal history and requests an on-demand frame. Unthemed
callers retain the prior mesh lighting. CIR field shaders keep their existing ABI.
No HDR bloom, shadow maps or physically simulated atmosphere are claimed.
See `docs/public/firmament/viewport-themes.md` for the product choices.

## Surface inspection

`inspectSurfaces(scene, mode, isolatedFace?)` is the shared Cadmata/Helios inspection
projection. Modes are `normal`, `surfaces`, `wire`, `overlay` and `patches`.
`surfaceInspectionFaces(scene)` enumerates source faces from individual meshes,
packed definition ranges and retained field proxies. Isolation keys contain both
occurrence and face identity. Projection never modifies the camera or source scene.
Packed faces share complete immutable geometry and GPU buffers; `triangleRange`
restricts drawing and picking while retaining original definition triangle indices.

Whole-model views keep actual fields. Per-face field inspection uses the retained
BRep proxy explicitly; products must label that distinction. Source edge IDs stay
authoritative. If face-edge adjacency is absent, isolated wire shows occurrence
edges rather than guessing adjacency from triangles. See the repository's
`docs/public/firmament/surface-inspection.md` for human controls and capture usage.

## Experimental temporal AA

`SpatialOnly` is the production default: four-sample MSAA shares one multisampled color/depth pair across surfaces, topology and overlays, then resolves once to the canvas. Lines also retain derivative coverage. Field shaders execute their existing center-ray ABI; MSAA does not qualify analytic field silhouette coverage. `None` uses one sample. `host.setAA("TAA")` enables a single-sample fixed-weight temporal experiment; `host.setAA("TAAUtility")` enables its generated Copeland utility policy. Both remain disabled by default. See `docs/release/TELOS-TAA-X0.md` for the temporal silhouette blocker and `docs/release/VIEWPORT-FINISH-X0.md` for product qualification.

Optional debug values are `policy`, `confidence`, `motion` and `depth`; seed a color view first. Debug reads frozen color history and never accumulates its visualization. The shared host owns eight bounded physical-pixel Halton samples, surface color/depth history, reprojection and invalidation. Picking, PMI and overlay projection use the logical camera. Scene/appearance/occurrence updates through `setScene` reset history; object-transform reprojection is not claimed. Topology and authoring overlays render after resolve. Temporal textures remain allocated after switching back to spatial mode until resize/disposal.

The actual decision source is `fixtures/three-telos/temporal-policy.v.ts`. Regenerate the packaged policy using `scripts/prepare-telos-temporal-policy.ps1` with an explicit Copeland checkout; this compiler is an offline build input, not a browser dependency. Missing optional temporal assets or pipeline support preserve spatial startup. Timestamp-query timing is optional. Qualification must submit `readPixels()` immediately after `render()` before yielding a browser frame, because the canvas swap texture can expire.
