# Drawing architecture

> **The 3D semantic model is the product definition. A Drawing is a printable projection of that definition, not a parallel source of engineering truth.**

Drawing M0 is a bounded compiler lane over Firmament V2. `Concept Drawing`, `Template ... Drawing`, and `Drawing` specialization declarations are normalized and erased before the remaining module travels through the ordinary exact `FirmamentBuildAndExport` route. The resulting AP242 is reimported as BRep, so the drawing projector consumes the same exact body used by STEP rather than an independently authored outline.

```text
Firmament module
  ├─ Drawing language normalization -> concept/template/source/provenance
  └─ ordinary product compilation -> exact BRep + semantic PMI + Static Tables
                                         │
                                         v
DrawingIR -> projected edge polylines -> bounded PMI candidates -> A4 pages
          -> React/MachinaLayout preview + SVG
          -> deterministic vector PDF
```

`DrawingIr`, `DrawingPageIr`, `DrawingViewIr`, and `DrawingAnnotationIr` are backend-independent records in `Aetheris.Kernel.Firmament/Drawing/DrawingIr.cs`. React state is not authoritative. Every annotation retains a semantic reference and display value copied at compile time from bound Firmament PMI; there is no drawing-owned editable dimension.

M0 supports literal drawings and ordinary Drawing-returning Templates through the same declaration syntax. The bounded parser is structured like the existing Analysis and Assembly compilers because the central parser remains deliberately split. It accepts one Drawing specialization per compile.

## Projection boundary

Projection evaluates exact BRep vertices and analytic circle/ellipse curves, selects a stable view basis, and emits structured 2D polylines. Coincident projections and coplanar internal seams are removed. M0 does **not** implement exact face-occlusion hidden-line removal. `VisibleOnly` therefore means clean boundary/feature edge projection, not a full drafting HLR proof. Three.js is not used by the authoritative path; it remains available for future interactive preview only.

## Prior-art audit

Machina Canvas demonstrated useful A4 constants, millimetre SVG sizing, explicit render/export boundaries, and semantic sidecar inspection. Drawing M0 reuses those ideas. It intentionally does not reuse the Canvas scene/document coupling, mutable tool state, raster export path, hand-built title-block geometry, or large mechanical-annotation sidecar architecture. Aetheris uses typed compiler IR, React document elements, and a narrow vector backend instead.
