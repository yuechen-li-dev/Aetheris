# Drawing architecture

> **The 3D semantic model is the product definition. A Drawing is a printable projection of that definition, not a parallel source of engineering truth.**

Drawing M0B is a bounded compiler lane over Firmament V2. Drawing declarations are normalized and erased before the remaining module travels through the ordinary Part or Assembly M1 route. Part drawings reimport canonical AP242 BRep. Assembly drawings consume AssemblyIR plus its world-transformed occurrence BReps, preserving the product tree instead of Boolean-flattening it.

```text
Firmament module
  ├─ Drawing language normalization -> concept/template/source/provenance
  └─ ordinary product compilation -> exact Part BRep or AssemblyIR occurrences
                                         │
                                         v
DrawingIR -> exact edge intervals + bounded occlusion -> PMI candidates -> zoned A4 pages
          -> React/MachinaLayout preview + SVG
          -> deterministic vector PDF
```

`DrawingIr`, `DrawingPageIr`, `DrawingViewIr`, and `DrawingAnnotationIr` are backend-independent records in `Aetheris.Kernel.Firmament/Drawing/DrawingIr.cs`. React state is not authoritative. Every annotation retains a semantic reference and display value copied at compile time from bound Firmament PMI; there is no drawing-owned editable dimension.

M0 supports literal drawings and ordinary Drawing-returning Templates through the same declaration syntax. The bounded parser is structured like the existing Analysis and Assembly compilers because the central parser remains deliberately split. It accepts one Drawing specialization per compile.

## Projection boundary

Projection evaluates exact BRep edge geometry, selects a stable view basis, and splits each segment at occluding triangle boundaries. The existing bounded face tessellator supplies only the depth oracle; exact BRep edge identity remains authoritative. `VisibleOnly` omits classified hidden intervals and `VisibleAndHidden` emits dashed intervals. Each view records candidate, visible, hidden, split, triangle, unsupported-support, and hash evidence.

Assembly projection iterates AssemblyIR leaf Part occurrences. World transforms come from Assembly M1 execution, and each primitive carries occurrence path plus definition identity. The BOM is derived from that same occurrence list, so geometry and quantity evidence cannot silently diverge.

Text layout and PDF both use advance widths from the embedded fixed Inter resource. PDF uses Type0/Identity-H with `FontFile2` and `ToUnicode`; output remains searchable and contains no raster page images.

## Prior-art audit

Machina Canvas demonstrated useful A4 constants, millimetre SVG sizing, explicit render/export boundaries, and semantic sidecar inspection. Drawing M0 reuses those ideas. It intentionally does not reuse the Canvas scene/document coupling, mutable tool state, raster export path, hand-built title-block geometry, or large mechanical-annotation sidecar architecture. Aetheris uses typed compiler IR, React document elements, and a narrow vector backend instead.
