# Three Telos display

## Concept Struct wireframes

The **Concepts** button in Helios and Cadmata toggles gold construction wireframes.
It starts off and is disabled when the built model has no retained geometric
Concept values. In Cadmata, open the Firmament source rather than only its STEP:
compile-time scaffolds are erased from manufacturing output.

Box3 bounds, planes, axes, Region2 boundaries and resolved 3D points/point sets are
projected from the compiler's typed values. Assembly datum frames and published
geometric semantics also provide guides. They follow occurrence placement and
sampled motion, independently of shaded/wireframe material display. Plain Boolean
requirements, scalar members and types without a retained spatial value have no
wireframe. A local 2D point without a spatial frame is not silently lifted to XY.

The overlay is visible through material so interior layout remains legible.
WebGPU adds a subtle static gold halo; WebGL fallback draws the same crisp gold
guides. Infinite planes and axes use finite 40 mm guides, frames use 20 mm axes,
and point marks span 2 mm; these are display sizes, not dimensions of a part.
The guides do not intercept engineering picks, add bodies, alter STEP/STL output
or recompile when toggled. Failed builds retain the last valid model's guides.

The admitted example is
`fixtures/Regression/Visualization/concept-structs.firmament`.

## Shared renderer

Cadmata's model, assembly, authoring overlays and PMI presentation share the Three Telos WebGPU host with Helios. It owns the camera, grid, mesh drawing, semantic topology lines, GPU leaders, selection projection and GPU resources. A supported browser/GPU is required for that path.

The shared mesh and line shaders live in standalone `.wgsl` files, loaded through TypeScript during host initialization. Builds carry the shader assets with the package; WebGPU validates their code. CIR field shaders continue through Copeland's typed Visual TypeScript compiler.

When WebGPU is unavailable, both products label their WebGL fallback. Cadmata loads that renderer only at the unsupported-browser boundary. Semantic authoring or PMI never selects it in a supported browser.

Spatial rendering uses four-sample MSAA by default, with local coverage for topology lines. Experimental temporal AA remains disabled. Fit uses the viewport aspect ratio and product world-up; both products use Z-up engineering views. GPU initialization has visible feedback. A viewport failure offers Restart viewport while retaining the product model. Failed model builds retain the last valid geometry.

PMI panels remain React DOM buttons, projected through TelosCamera in CSS pixels. They preserve semantic targets, camera-facing text, category filters, deterministic collision layout, selection and presentation-only dragging. Their leaders use retained Telos GPU buffers. Selecting a face highlights its published related PMI without broadening the face selection.

Three Telos supports direct WGSL field artifacts. Normal product Auto now projects qualified primitive definitions into fields using compiler-generated artifacts, alongside mesh geometry and authoritative topology. The [binding report](../release/P4-01A3-CIR-SHADER-BINDING-CLOSEOUT.md) records product qualification and the remaining CSG source-route boundary. The [X0 substrate report](../release/THREE-TELOS-X0.md) and [X1 overlay migration report](../release/THREE-TELOS-X1.md) retain their historical witness evidence.

Part, Assembly and Scene source now reach shared compiler-owned display projection in Cadmata and Helios. The SDK accepts explicit project documents for Scene dependencies. Resolved authored colours, roughness, metallic, opacity and emissive values travel per occurrence; products offer the Scene's named cameras. Simple translucent panes use mesh blending and object-level ordering. The [transport report](../release/P4-01A2-DISPLAY-TRANSPORT-X0.md) records actual house, factory and guitar views and the bounded transparency limits.

Compiled definitions retain qualified CIR admission, structural identity and bounds alongside BRep. The managed provider generates each semantic artifact once at display projection, with bounded caching. Fields carry the same resolved material state as meshes and retain invisible engineering picking proxies. Module or pipeline rejection keeps the mesh visible; developer inspection distinguishes generation, transport and GPU failures. The [Preview 4 rerun](../release/P4-01A-DISPLAY-PROJECTION-CLOSEOUT.md) records the current verdict and development WASM limits.
