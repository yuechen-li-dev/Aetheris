# Generate engineering drawings

Use the existing drawing compiler to produce searchable vector PDF, SVG and
editable PPTX from a Firmament part or supported AP242 semantic PMI:

```powershell
aetheris drawing generate fixtures/Regression/DrawingGeneration/inspection-plate.firmament --out-dir artifacts/local/drawing/inspection-plate --drawn-by "Codex (AI)" --date 2026-10-18 --json
aetheris drawing generate inspection-plate.step --out-dir artifacts/local/drawing/from-step --json
```

The default output directory is `artifacts/local/drawing/<source-name>`.
Generation preserves the source and writes a Drawing IR and validation evidence
beside the three presentation formats. PDF is a vector document with an embedded
Inter font and searchable dimension text. SVG and editable PPTX consume the same
positions and annotation strokes. Inter must be available in a PPTX editor to retain
matching text metrics. Changes in a presentation never change geometry
or PMI.

## What automatic generation does

An overview sheet combines top, front and right orthographic views with an isometric
wireframe. Its orthographic views share a scale. Enlarged PMI sheets follow only
for views with annotations; dense callouts paginate.
Surface lines come from the same BRep wireframe sampler as `aetheris wireframe`;
orthographic views retain topology edges without the denser surface grid. Rear
edges are intentionally visible. These are wireframe views, not a claim of complete
hidden-line removal or standards certification.

The generator reads the existing Aetheris AP242 semantic PMI subset: named
diameters with bilateral tolerances, datum features and supported face-associated
annotations/position controls. Datum and hole callouts retain source identities
and STEP entity/face references. Diameter callouts choose the orthographic view
closest to the cylindrical axis. A face-on circular diameter receives a two-arrow
diameter witness when its surface radius agrees with the semantic nominal.

The X/Y/Z envelope measurements prefixed `~` and marked `REF` describe sampled
projected curves. They are approximate reference measurements, with no implied
manufacturing tolerance. They do not replace explicit PMI. A linear PMI callout
can identify an associated face but does not invent measurement endpoints.

Labels use embedded-font widths. Layout reserves view titles, notes, information
blocks, geometry, other labels and leader clearances. Placement uses the existing
JudgmentEngine with deterministic costs and rejection reasons. Dense PMI is split
into batches of at most six per view sheet. Layout failure is an error, not
permission to shrink, clip or silently omit a dimension.

## Authored drawings

Keep using `aetheris drawing compile <drawing.firmament> --out-dir <directory>` for
explicit Drawing/Templates, selected views, engineering metadata, design tables,
assembly BOMs and reviews. `generate` honors a source's existing Drawing declaration
by using that compilation path. Assembly drawing authoring remains explicit.

Within an authored View, `HiddenLines: Wireframe` selects the shared wireframe
projection; `VisibleOnly` and `VisibleAndHidden` retain their existing bounded
occlusion behavior. This adds a presentation policy, not a new geometry frontend.

## Engineering authority and current boundaries

Generated sheets communicate available source facts and require engineering
review before manufacturing release. They do not infer material, missing dimensions,
datum schemes, fits, general tolerances or revision metadata. A geometry-only input
is explicitly labeled as having no supported semantic PMI.

Automatic generation currently admits one imported BRep body and millimetre PMI.
Foreign PMI units, unsupported standard AP242 dimension/tolerance families and
missing face associations produce named errors. Geometry-only imports can use the
existing unit normalization. The generator does not claim universal third-party
AP242 PMI interpretation. A datum axis-face port must resolve uniquely; multiple
same-facing planes remain ambiguous.

General section/detail views, automatic assembly annotation, standards certification
and DXF drawing export are not qualified by this pass. PDF, SVG and PPTX are the
implemented outputs.

Use multiline fields inside PMI declarations. The current PMI frontend still
rejects some otherwise equivalent semicolon-separated inline fields; generation
does not introduce a second parser to work around that inconsistency.

For labeling conventions, see MIT's
[Engineering Drawing and Sketching](https://ocw.mit.edu/courses/2-007-design-and-manufacturing-i-spring-2009/pages/related-resources/drawing_and_sketching/):
choose informative views, avoid redundant controlling dimensions, and place readable
dimension text and leaders near the relevant feature. These references guide layout;
they are not a claim of ISO/ASME compliance.

## Title table and traceability

Every sheet has a shared title table with merged title/identity cells, material,
part number, revision, scale, units, sheet number and status. Its final rows contain
separate **Drawn by / Checked by / Approved by** and corresponding date boxes.
PPTX exports a native editable table; the browser uses an HTML table. PDF and SVG
project the same measured cells and line hierarchy.

Names and dates must be supplied together. Missing review/approval roles remain
blank. For example, `--drawn-by "Codex (AI)" --date 2026-10-18` credits preparation
of this drawing without claiming that anyone checked or approved it. These flags
also work with authored `drawing compile`:

- `--drawn-by <name> --date <yyyy-MM-dd>`
- `--checked-by <name> --checked-date <yyyy-MM-dd>`
- `--approved-by <name> --approved-date <yyyy-MM-dd>`

The authored metadata Record can also supply `DrawnBy` / `DrawnDate`, `CheckedBy` /
`CheckedDate` and `ApprovedBy` / `ApprovedDate`; declare and populate both members
for each provided role. Existing `Author` and `Date` remain the fallback for Drawn.
These are traceability records, not digital signatures or an automatic manufacturing
release decision. Preparation credit does not replace the model's source provenance.

Text is measured before wrapping; a title-table or notes overflow rejects rather
than silently truncating a name, date or engineering note. Line weights use paper
millimetres consistently: heavier model boundaries, light surface grids, finer
dimension/leader strokes, muted field labels and a stronger outer table frame.
