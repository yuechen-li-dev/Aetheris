# Drawing M0

Drawing M0 compiles one manually specified machined-part drawing into deterministic DrawingIR, SVG, and a vector A4 PDF.

```powershell
aetheris drawing compile fixtures/DrawingM0/bearing-block-drawing.firmament --out-dir docs/drawing/artifacts/m0 --json
```

The canonical fixture proves a single lifecycle: one Static Table describes a bearing-block family; the selected product compiles as exact BRep with semantic hole diameter and datum PMI; a normal Drawing Template specializes for that product; two manual views project the BRep; PMI is explicitly assigned to the front view; candidate layout finds collision-free exterior lanes; the table flows to a second A4 page; and the vector PDF retains text and paths.

Supported in M0:

- ISO A4 portrait or landscape only;
- multiple A4 pages, with design tables flowing to their own readable page;
- orthographic and isometric direction vectors;
- `VisibleOnly` and parsed `VisibleAndHidden` policy metadata;
- datum, diameter, linear, and existing feature-control PMI presentation;
- bilateral/asymmetric tolerance formatting from bound PMI;
- real Static Table data and React `<table>` rendering;
- deterministic specialization, layout, DrawingIR, SVG, and PDF;
- typed unknown-PMI, missing-view, failed-Concept, and impossible-layout diagnostics.

Current boundaries:

- exact face-occlusion HLR is not implemented; hidden-line policy is recorded but hidden segment classification is not yet complete;
- AssemblyIR occurrence projection/BOM is not admitted by this first part-only compiler;
- section/detail views, radius PMI, note-to-feature leaders, GD&T beyond existing Firmament records, and automatic view/PMI selection are not implemented;
- the React/MachinaLayout preview and native vector PDF backend consume the same DrawingIR, but PDF is not produced by browser-printing the React tree;
- table overflow currently allocates a dedicated page but does not yet split one very large table across several pages.

These limits are explicit so M0 is not mistaken for a complete ISO/ASME drafting implementation.
