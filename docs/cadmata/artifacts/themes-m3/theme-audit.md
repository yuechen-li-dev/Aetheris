# P2-CADMATA-THEMES-M3 theme audit

## Previous architecture

- `App.tsx` owned an in-memory `"atelier" | "monument"` theme ID and rendered one segmented button per theme. Selection, document, assembly, Mate, PMI, and display state were independent React state.
- `viewportTheme.ts` already provided a useful typed descriptor for background color, standard material, selected material, line edges, logarithmic grid, two directional lights plus ambient/hemisphere light, shadows, exposure, fog, camera presentation, axes, and annotations.
- `AetherisViewport.tsx` was the React/R3F boundary. It created one orthographic `Canvas`, fitted the camera only when scene/assembly bounds changed, and created `MeshStandardMaterial` presentation instances without mutating display geometry.
- Renderer color management was explicit: `SRGBColorSpace`, `ACESFilmicToneMapping`, and per-theme exposure. The canvas was opaque with a Three.js scene background.
- Face material and line-edge treatment were shared infrastructure. Theme-specific data lived in two descriptors; renderer wiring was shared.
- The adaptive logarithmic grid was shared and camera-aware, but it could only be recolored—not disabled per theme.
- CAD edges used drei `Line`; selected faces swapped presentation material; selected edges swapped width/color. Assembly occurrences reused definition face patches under immutable world matrices.
- PMI used Drei `Html` callouts and leader lines with descriptor colors. Compiler overlays used a separate hardcoded `CADMATA_PALETTE`, so they did not adapt to light/cosmic worlds.
- There was no post-processing dependency, environment map, HDRI, texture-backed background, shader seam, registry map, persistence, or reduced-motion concern. Performance instrumentation already exposed frame time, renderer calls/triangles/memory, and logarithmic-grid allocations under `?perf`.

## Extraction decisions

- Retain the single descriptor and existing renderer boundary; expand it with metadata, background kind, rim light, grid enablement, effects intent, and semantic-overlay palette.
- Add one bounded full-screen procedural background component rather than scattering per-theme scene switches or introducing a shader graph.
- Replace the theme button expansion point with a registry-driven native dropdown.
- Reuse existing selection/material/PMI paths and make their colors theme-aware. Do not touch display packet, tessellation, assembly, semantic, export, or camera-fit code.
- Keep post-processing dependency-free. Background-only vignette/glow supplies composition without affecting CAD depth, picking, or annotation sharpness.
