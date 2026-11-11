# PMI presentation styles: Markers and Drawing

Phase L4 of HeliosCAD's Label design. Helios side: HeliosCAD `docs/development/reports/2026-11/pmi-styles.md`.

- The web runtime's display mesh now carries, per annotation, the ASME drawing presentation the annotated GLB exporter
  uses (`drawing`: drafting lines, filled glyph triangles, backing panels). These were `[JsonIgnore]` on
  `PmiDisplayAnnotation` and never reached the browser.
- `@aetheris/three-telos` `pmiLines` / `pmiMeshes` / `pmiGroup` take a `PmiStyle`:
  - `markers` — each record's target patch and outline in its own Okabe–Ito colour (`pmiMarkerColor`); no 3D glyphs,
    labels are left to the host. `pmiLabelLines` keeps the old glyph polylines for line-only consumers.
  - `drawing` — the drafting lines, opaque white panels and dark filled text, depth-tested, as exported.
- `PmiDisplayProjection.FromStep` defaults to the viewport tessellation so target witnesses lie on displayed faces.
