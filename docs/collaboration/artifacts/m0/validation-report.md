# M0 validation report

## Backend and package

- Toolchain: direct deterministic OPC/Open XML generation using the .NET base class library; no Office installation, commercial library, or cloud dependency.
- Coordinate mapping: exact 36,000 EMU/mm. A4 landscape is 10,692,000 x 7,560,000 EMU; DFM 16:9 is 12,192,001 x 6,858,000 EMU (decimal 338.6667 mm width rounded once).
- Native content: PowerPoint lines, dashed hidden lines, text boxes, semantic groups, and DrawingML tables. No `p:pic`, raster media, full-page SVG, or screenshot.
- Curves: current projected point sequences lower to grouped editable line segments; analytic curve identity is not yet present in DrawingIR.
- Inter: requested in every text run and theme; not embedded in PPTX M0, so local substitution occurs if Inter is unavailable.

## Collaboration and authority

- ReviewIR preserves stable target, source path, current engineering display, capabilities, typed entry kind/status, author identity, authored date, proposal data, rationale, and authored order.
- The canonical tolerance proposal is `PlusMinus(0.005mm)` -> `PlusMinus(0.010mm)`. Tests prove the supplied current authoritative value remains unchanged.
- Production PPTX tests prove review groups are absent; Review PPTX tests prove `Review.DFM-004.Callout` is present and semantically anchored to `MountDiameter`.

## Validation and QA

- Focused collaboration tests: 5 passed.
- Focused Drawing tests: 13 passed; focused CLI Drawing tests: 2 passed. Coverage includes exact dimensions, native tables/text/shapes/groups, stable names, no raster picture, clean-vs-review behavior, 16:9 DFM output, and byte-identical repeated package hash.
- Open XML SDK validation reports zero schema errors for Production, Review, and DFM presentations.
- Existing Drawing M0/M0B tests remained green.
- LibreOffice headless imported and rendered all four canonical PPTX files. Visual inspection found legible geometry, dimensions, metadata, zone labels, BOM/design-table continuation pages, review callout, and two DFM slides without clipping or overlap.
- Editability is structurally proven by native object types and successful editor import. Interactive drag/edit operations were not automated.

## Performance and determinism

- Canonical Bearing Block: Drawing PPTX 23.16 ms, Review projection 7.24 ms, DFM deck 9.72 ms.
- Canonical Machine Assembly: Drawing PPTX 13.44 ms.
- A clean repeat produced byte-identical SHA-256 hashes for DrawingIR, PDF, Production PPTX, Review PPTX, and DFM PPTX. Package entry ordering, timestamps, slide order, shape names, tables, Review IDs, and callout placement are normalized.

## Microsoft 365 audit

Dominatus contains a Semantic Kernel capability-profile/allowlist abstraction and fake Outlook mail/calendar sample. It deliberately has no `Microsoft.Graph`, Azure Identity, MSAL, live Graph authentication, OneDrive upload, SharePoint handling, or PowerPoint delivery dependency. Nothing was copied. Adding an Aetheris adapter now would create a transport implementation rather than reuse one, so M0 leaves the clean seam documented and keeps compilation fully offline.

## Remaining bounded limitations

- PPTX font embedding is not implemented.
- Analytic arcs/splines lower as editable segmented lines because DrawingIR M0B exposes projected point sequences, not analytic curve records.
- Callout collision avoidance is a deterministic bounded offset/clamp, not a global optimization pass.
- No PPTX redline import, Graph transport, PowerPoint comments API, or source mutation.
