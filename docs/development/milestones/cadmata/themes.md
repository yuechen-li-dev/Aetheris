# Cadmata viewport themes

Cadmata viewport themes are presentation-only renderer configurations. Switching a theme does not rebuild display packets, tessellation, assembly occurrences, semantic selections, Mates, PMI, transforms, or camera fit. `App` owns the selected theme ID; `AetherisViewport` consumes the resolved descriptor.

## Registry and descriptors

`aetheris.client/src/viewer/viewportTheme.ts` defines `ViewportTheme`, the ordered `VIEWPORT_THEMES` gallery, and the deterministic `VIEWPORT_THEME_REGISTRY`. A descriptor owns:

- metadata (`id`, label, description, category, animation, performance class);
- renderer background fallback, explicit ACES exposure, fog, and restrained effects recipe;
- object and selected material response;
- edge, adaptive logarithmic grid, axes, selection overlay, and PMI colors;
- ambient, hemisphere, key, fill, and rim lighting.

The six curated themes are:

| Theme | Intent | Background | Presentation |
| --- | --- | --- | --- |
| Atelier | Industrial engineering workbench | Graphite clear | Neutral task light, matte stock, crisp dark edges |
| Monument | Architectural gallery model | Pale stone clear | Pastel material, soft warm light, quiet grid |
| Mars | Oxidized planetary survey plate | Analytic dust horizon, low sun, strata, sparse particulate field | Burnt iron, grazing orange key, deep red shadow |
| Sirius | Cold-spectrum stellar product render | Deterministic starfield, blue-white halo, diffraction rays, orbital trace | Metallic precision finish, hard white key, blue rim |
| Singularity | Severe cosmic graphics plate | Off-axis event horizon, accretion ring, logarithmic polar bands, lens rings | High-contrast metal, warm energy rim, grid suppressed |
| Aeons | Ancient-future observatory | Violet star haze, immense concentric arcs, mathematical spokes | Muted gold metal, monument-scale rim, faint technical grid |

No generated images, HDRIs, stock textures, or external art packs are used.

## Procedural background seam

`ThemeBackground.tsx` is a bounded, non-selectable full-screen shader layer. One deterministic WebGL-compatible fragment program contains the four curated procedural worlds and is selected by a numeric theme mode. It uses analytic gradients, hashes, interpolated noise, polar distance fields, logarithmic bands, diffraction lines, and concentric-ring SDFs. The shader writes no depth, cannot intercept raycasts, renders before CAD, and leaves `scene.background` as a failure fallback.

Effects described as bloom or vignette are deliberately implemented inside the background composition. Cadmata has no post-processing dependency and this milestone does not add a full-frame effect chain that could soften edges, alter picking, or obscure PMI. The geometry remains under normal Three.js depth testing and ACES filmic tone mapping.

All themes are static by default. This makes still frames deterministic, avoids gratuitous motion during inspection, and inherently honors reduced-motion preferences. If animation is added later, gate the time uniform with `prefers-reduced-motion` and keep the static shader path authoritative.

## Theme selector and persistence

The viewport toolbar contains a labeled native `<select>`, populated directly from the registry. It exposes the current selection, switches immediately without a scene reload, supports normal keyboard navigation and focus-visible styling, and stores the ID under `cadmata.viewport-theme` in `localStorage`. A valid `?theme=<id>` query overrides storage for repeatable gallery capture; invalid IDs fall back to Atelier.

MachinaLayout continues to own shell/workspace regions. The selector remains in the existing Machina viewport region; Three.js/R3F remains the 3D scene authority.

## Adding a theme

1. Add a stable lowercase ID to `VIEWPORT_THEME_IDS`.
2. Add one complete descriptor to `VIEWPORT_THEMES`. Reuse a nearby theme only as a base and explicitly override its visual identity.
3. If a flat background is insufficient, add one bounded mode to `ThemeBackground`; do not add selectable scene geometry or a generic shader graph.
4. Give selected edges, assembly selection, semantic overlays, datum labels, and PMI callouts explicit high-contrast colors.
5. Decide whether the adaptive grid belongs in the composition; set `gridStyle.enabled` accordingly.
6. Add registry/fallback tests and capture the same model/camera gallery frame with `?perf&theme=<id>`.

## Performance and fallback

The four procedural themes add one full-screen draw call, two triangles, one geometry, and normally one shader program. Singularity disables the adaptive grid and is therefore cheaper in draw calls despite its denser fragment work. The renderer keeps an opaque clear fallback color behind every shader. A shader compile/runtime failure therefore removes atmosphere, not CAD geometry.

Representative measurements and screenshots live in `docs/development/milestones/cadmata/artifacts/themes-m3/`.
