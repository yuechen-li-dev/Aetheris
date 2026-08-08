# P2 Cadmata M1 visual and performance evidence

The `before-*` images were captured from `HEAD` before the refactor, served from
an isolated Git archive with the dependencies materialized by TSPack. The
`after-*` images use the refactored application through the real Vite proxy and
`Aetheris.Server` API. No screenshot framework or mocked renderer was used.

- `before-empty-shell.png`, `before-simple-part.png`,
  `before-complex-part.png`, and `before-selected-geometry.png` record the
  Preview 1 shell, box, L-bracket, and selected L-bracket face.
- `after-empty-shell.png`, `after-simple-atelier.png`,
  `after-complex-atelier.png`, and `after-selected-geometry.png` record the new
  shell, direct Profile fixture, CTC-01 X4 fixture, and semantic Profile
  selection.
- `after-simple-monument.png` proves that renderer presentation changes through
  a theme choice without changing the renderer path.
- `after-small-desktop.png` is retained as an additional viewport-layout
  observation; the selected browser backend remained at 1440x900 CSS pixels,
  so it is not claimed as a 1024px breakpoint proof.
- `performance.json` contains the structural grid comparison and live renderer
  counters. Frame time is the browser presentation interval; GPU timestamp
  queries were not introduced into the product renderer.
