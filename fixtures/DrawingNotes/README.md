# Drawing Notes regression sources

- `synthetic-engineering-drawing.pdf`: repository-owned engineering sheet;
  regenerate with `python fixtures/DrawingNotes/generate_synthetic.py`.
- `page-coordinates.pdf`: small deterministic PDF 1.7 witness with an inherited,
  offset CropBox, page rotations 0/90/180/270 and UserUnit 2. Regenerate with
  `python fixtures/DrawingNotes/generate_page_coordinates.py`. A red rectangle
  has known physical page-space bounds after each rotation; the browser test
  checks its actual rendered pixels, not just page metadata.

Run `node scripts/test-drawing-notes-browser.mjs` after building the Release CLI
to exercise the real server, bundled PDF.js, annotation save/reload and source-linked
PNG crops. Playwright and a browser are development prerequisites; neither ships
in the compiler. Output defaults to ignored `artifacts/local/drawing-browser/`.
Set `AETHERIS_PLAYWRIGHT_MODULE` / `AETHERIS_BROWSER_PATH` for local installations.

Managed inspection and notebook tests run without native libraries. To qualify
the explicitly external headless renderer as well, set `AETHERIS_TEST_PDFTOPPM`
to a Poppler pdftoppm executable and run `dotnet test Aetheris.Drawing.Tests`.
