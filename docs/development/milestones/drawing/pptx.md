# Native editable PowerPoint drawings

Aetheris lowers the same normalized `DrawingIr` used by SVG and vector PDF into a deterministic offline `.pptx`. One Drawing page becomes one slide with the exact A4 physical dimensions: 210 x 297 mm portrait or 297 x 210 mm landscape. Millimetres map to Open XML EMUs at exactly 36,000 EMU/mm.

> **PowerPoint is an editor for the compiled drawing artifact, not the authoritative CAD model.**

The backend emits PowerPoint lines for projected edges, dashed lines for hidden edges, text boxes for notes and PMI, grouped lines/text for annotations, native tables for Design Tables and BOMs, and editable text/rules for zones and DrawingInfo. It does not insert a full-page image or SVG. Stable object names begin with `View.`, `PMI.`, `Table.`, `Metadata.`, `Zone.`, or `Review.`. View geometry and each annotation are separate groups, so moving a dimension does not require ungrouping the view.

Projected curves currently arrive in DrawingIR as deterministic point sequences. The PPTX backend emits their segments as editable PowerPoint lines inside the semantic view group. This preserves editability and avoids rasterization; native arc/freeform lowering can be added when DrawingIR exposes analytic curve identity without changing placement authority.

Inter is the canonical typeface in generated runs and the presentation theme. Unlike PDF, PPTX does not embed the bundled Inter font in M0. Office or LibreOffice substitutes a locally available font when Inter is absent, which can slightly change text metrics; install Inter for canonical appearance.

Production output omits collaboration overlays. Review output uses the identical page geometry plus editable callout groups. Manual edits to either file remain downstream edits and are never read back into Firmament in M0.

```text
Firmament / Assembly / SemanticValue / PMI
                     |
                 DrawingIR
              /      |      \
           SVG      PDF     PPTX
```

Generate the canonical evidence with:

```powershell
dotnet run --project Aetheris.CLI -- drawing compile fixtures/Canonical/Drawings/bearing-block-production-drawing.firmament --out-dir docs/development/milestones/drawing/artifacts/pptx-m1 --json
dotnet run --project Aetheris.CLI -- drawing compile fixtures/Regression/Drawing/machine-assembly-production-drawing-legacy-placement.firmament --out-dir docs/development/milestones/drawing/artifacts/pptx-m1 --json
```
