# Drawing M0 evidence

Canonical source: `fixtures/Canonical/Drawings/bearing-block-production-drawing.firmament`.

Artifacts:

- `BearingBlockProduction.drawing.json`: normalized DrawingIR with product/template/Table provenance, pages, projected primitives, annotation candidates, collision evidence, and lane occupancy;
- `BearingBlockProduction.svg`: structured vector preview;
- `BearingBlockProduction.pdf`: deterministic two-page vector A4 compilation target;
- `BearingBlockProduction-page-1.png`: Poppler-rendered visual QA of the first page;
- `BearingBlockProduction.validation.json`: collision, page-size, vector, hash, and measured performance evidence;
- `pdf-proof.json`: independent Poppler page-size/metadata inspection;
- `determinism.json`: repeated-compile hash proof.

The design-table proof is page 2 and `pages[1].tables[0]` in DrawingIR. Every row comes directly from `Static Table BearingStandards`; the Drawing declaration contains no duplicated table cells.
