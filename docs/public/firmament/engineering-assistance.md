# Engineering references and review packages

Helios AI Author can use attached specifications, datasheets, parts catalogs and
parameter sheets. Semantic Kernel exposes typed tool schemas and invokes the
registered functions. Dominatus retains effect execution; Aetheris remains the
authority for source admission, geometry and measurements.

## Attach evidence

Open **Files → Engineering references**, sign in through Leviathan, and add a TXT,
Markdown, text-bearing PDF, DOCX, CSV or XLSX file. Select the references the current
workspace should expose to the agent. Up to ten can be attached. Files are limited
to 4 MiB, PDFs to 100 pages, and tables to 2,000 data rows and 64 columns.

References stay in your server account, including their original bytes. Another
account cannot retrieve them by guessing an ID. Checkpoints preserve attachment
IDs alongside source; they are not independent backups of the server's originals.
Changing attachments clears stale agent context and unapplied candidates.

Reference tools return the document's SHA-256 and its page, paragraph, line or cell
location. PDF extraction requires selectable text; no OCR is performed. Complex
PDF reading order is not an interpretation guarantee. Tables are read from CSV
records or XLSX worksheets with a literal header row. Headers must be nonempty and
unique. Formulas are flagged; cached formula results are never admitted as typed
engineering parameters. Export formula-derived data as explicit values first.

## Ask the agent

Examples:

- “Read the attached motor datasheet and cite the mounting pitch before editing.”
- “Filter this bearing catalog for boreMm = 8 and outerDiameterMm ≤ 22. Show the
  source rows before choosing a candidate.”
- “Bind widthMm to width in mm and angleDeg to angle in degrees. Make the two
  variants from this parameter sheet, then build and compare their bounds.”
- “Prepare a review package against the attached requirements, noting unresolved
  tolerances and inspection requirements.”

Catalog filters are literal numeric comparisons against explicitly named columns.
They return candidates, not a fit certification or live price/availability quote.
No live supplier connection is implied. Units must come from the source or user.
Parameter projection requires explicit units: mm, cm, m, in, deg, rad or scalar.
It returns proposals with cell provenance; it does not bypass source editing or
compilation. The agent must still author valid Firmament and obtain build evidence.

## Review export

The agent first prepares a draft manifest with cited requirements/comments and
open issues. The manifest is tied to hashes of the current source documents.
After a successful candidate build, it can request export_review with admitted
formats, such as STEP and PDF when the compiler offers both for that model.

**Download draft review package** appears in AI Author when export succeeds.
The ZIP contains all candidate source documents, actual compiler exports, their
hashes, and review.json with cited evidence, source hashes, compiler bounds and
open issues. A stale manifest, failed build or failed exporter produces no new
package. The package describes a candidate and is explicitly not engineering
approval. It does not invent tolerances or sign drawn/checked/approved fields.

Source evidence, model inference and compiler measurements are different kinds of
evidence. Compilation alone does not prove manufacturability or requirements
compliance. Human review remains visible in the package's draft status.

This pass implements native Semantic Kernel tools. Live supplier APIs,
SharePoint/OneDrive delivery, OAuth connected accounts and external review/comment
synchronization are not implemented. Existing PDF/STEP/etc. exporters remain the
export authority; SK does not redraw or reconstruct them.

For the agent connection and checkpoint workflow, see [AI Author](ai-author.md).
