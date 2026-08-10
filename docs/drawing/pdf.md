# Vector A4 PDF

M0B embeds a fixed Inter TrueType resource as a Type0 Identity-H font with `FontFile2` and `ToUnicode`. Output text is searchable, and annotation layout uses the same Inter advance widths. The font resource is internal to the compiler and is not delivered as a separate artifact.

Drawing M0B uses a deterministic native PDF 1.4 backend. BRep projections, hidden intervals, leaders, A4 zone borders/ticks, information-block rules, and table rules remain PDF paths; metadata, labels, PMI, notes, BOM, and table values remain PDF text. No image XObjects, React rasterization, or WebGL screenshots are emitted.

Every page has an explicit `/MediaBox`:

- landscape: `841.89 × 595.276 pt` = `297 × 210 mm`;
- portrait: `595.276 × 841.89 pt` = `210 × 297 mm`.

The PDF includes title, product subject, Aetheris author/creator/producer, fixed deterministic timestamps, page count, and actual-size footer text. The canonical PDF was checked with Poppler `pdfinfo` and rendered with `pdftoppm`; both pages are unclipped and readable at normal A4 scale. PDF hashes are stable because volatile performance timings are stored in validation evidence rather than semantic IR or PDF metadata.

The React preview uses the same DrawingIR and real `<table>` document structure. PDF lowering is deliberately native rather than a browser automation dependency; this preserves deterministic physical sizing while the React/browser printing seam matures.
