# Three Telos display

Cadmata's model, assembly, authoring overlays and PMI presentation share the Three Telos WebGPU host with Helios. It owns the camera, grid, mesh drawing, semantic topology lines, GPU leaders, selection projection and GPU resources. A supported browser/GPU is required for that path.

When WebGPU is unavailable, both products label their WebGL fallback. Cadmata loads that renderer only at the unsupported-browser boundary. Semantic authoring or PMI never selects it in a supported browser.

PMI panels remain React DOM buttons, projected through TelosCamera in CSS pixels. They preserve semantic targets, camera-facing text, category filters, deterministic collision layout, selection and presentation-only dragging. Their leaders use retained Telos GPU buffers. Selecting a face highlights its published related PMI without broadening the face selection.

Three Telos supports direct WGSL field artifacts, with real CIR mixed-depth and CIR-plus-PMI browser witnesses. Automatic CIR product packet construction and default field rendering are future CIR-DISPLAY-X1B work. The [X0 substrate report](../release/THREE-TELOS-X0.md) and [X1 overlay migration report](../release/THREE-TELOS-X1.md) record qualification and reproduction commands.
