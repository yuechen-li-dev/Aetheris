# Vector A4 PDF

Drawing M0 uses a small deterministic PDF 1.4 backend. BRep projections, leaders, borders, and table rules remain PDF paths; metadata, labels, PMI, notes, and table values remain PDF text. No image XObjects or WebGL screenshots are emitted.

Every page has an explicit `/MediaBox`:

- landscape: `841.89 × 595.276 pt` = `297 × 210 mm`;
- portrait: `595.276 × 841.89 pt` = `210 × 297 mm`.

The PDF includes title, product subject, Aetheris author/creator/producer, fixed deterministic timestamps, page count, and actual-size footer text. The canonical PDF was checked with Poppler `pdfinfo` and rendered with `pdftoppm`; both pages are unclipped and readable at normal A4 scale. PDF hashes are stable because volatile performance timings are stored in validation evidence rather than semantic IR or PDF metadata.

The React preview uses the same DrawingIR and real `<table>` document structure. PDF lowering is deliberately native rather than a browser automation dependency; this preserves deterministic physical sizing while the React/browser printing seam matures.
