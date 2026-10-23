# Drawing generation integration

Date: 2026-10-18.

> Subsequent engineering review rejected the specimen as a manufacturing drawing:
> it omitted hole coordinates and controlling stock/thickness requirements. The
> success below qualifies integration and presentation only. See
> [dimension completeness qualification](DRAWING-DEFINITION-QUALIFICATION.md) for
> the replacement gate and corrected drawing. The original findings are retained.


## Existing owners and observed gaps

The repository already contained a Drawing IR, authored Part/Assembly drawing
compiler, bounded occlusion projection, annotation candidates, embedded Inter font,
vector PDF, SVG, editable PPTX and React projection. Reuse those owners.

Observed gaps:

- No automatic drawing entry point from a part or AP242 PMI.
- Generic model-center anchors did not communicate actual PMI face associations.
- Circle/ellipse projection sampled entire curves instead of their bound intervals.
- Label occupancy persisted between independent pages.
- Text width was capped at 58mm even when glyph advances required more space.
- Leaders were not obstacles for other labels, and PPTX arrows faced the label.
- Datum PMI in the V2 export bridge had no geometric association even where an
  existing axis-face selector could resolve uniquely.
- Explicit HoleDiameter authoring names were lost during STEP emission/readback.
- Named STEP dimensions did not retain their diameter kind in the inspector.

## Repairs and policy

`drawing generate` compiles parts or imports one BRep, reads semantic PMI, selects
orthographic view candidates with JudgmentEngine, projects real face-bound anchors,
and paginates six PMI entries per enlarged view. A compact overview combines three
orthographic views at a shared scale and an isometric surface wireframe.
Authored Drawing compilation remains the existing owner and shares artifact writing.

`BrepWireframeSvgRenderer.SampleGeometry` exposes the existing world-space samples,
edge/face identities and evidence. Both SVG previews and drawing projection use it.
No SVG parsing, triangle-edge substitution or independent curve evaluator was added.
Wireframe deliberately retains rear edges. Existing bounded HLR remains available.

Semantic export retains optional authored hole names in property metadata, preserving
the established diameter/target representation keys. Unique axis-face datum ports
resolve through the existing selector owner to geometric face associations. Ambiguous
planes stay unlocated. Engineering geometry is unchanged; affected STEP PMI metadata
is intentionally more complete, so those files need not retain whole-file hashes.

The existing annotation owner uses actual Inter advances, page-local keepouts,
text/leader rejection and deterministic Judgment utility. Reference envelope dimensions
are labeled approximate `REF`, never controlling tolerances. Shared paper-space strokes
include leaders, witness lines and arrowheads for PDF/SVG/PPTX/React.

## Honest admission boundaries

Unlocated PMI, unsupported AP242 semantic families, foreign PMI units and impossible
layout reject. No center-anchor fallback is admitted by automatic generation. Generic
linear face associations do not prove measurement endpoints. Current V2 export already
rejects unsupported authored PMI families; generation also verifies that expected
authored PMI survives exchange. Geometry-only sheets explicitly disclose absent PMI.

Section/detail views, automatic assembly drawings, general standards compliance,
universal third-party semantic PMI and DXF remain separate qualification work.
Existing authored Assembly drawings and BOMs are regression gates.
Authored drawings retain their existing semantic-center anchors where measurement
endpoints are unavailable; those legacy linear callouts are not new endpoint-based
dimension witnesses. The automatic path requires geometric face associations and
labels its sampled envelope dimensions as reference only.

## Syntax inconsistency surfaced

The new specimen initially used semicolon-separated inline PMI fields. Current
validation rejected it with `firmament-v2-pmi-diameter-invalid`, invalid target and
tolerance diagnostics. The same fields on separate lines validate/build. The specimen
now uses the documented accepted multiline form. This is recorded as frontend debt;
this pass does not add source rewrites or a second PMI parser.

## Labeling references

- [MIT engineering drawing guide](https://ocw.mit.edu/courses/2-007-design-and-manufacturing-i-spring-2009/pages/related-resources/drawing_and_sketching/):
  informative views, readable dimensions and nonredundant manufacturing information.
- [Engineering Working Drawings Basics, NASA-hosted lecture notes](https://s3vi.ndc.nasa.gov/ssri-kb/static/resources/Engineering%2BWorking%2BDrawing%2BBasics.pdf):
  grouped/spaced dimensions, outside placement where possible, true-shape views and
  avoiding dimensioning hidden features. Wireframe presentation is explicitly named
  rather than misrepresented as conventional HLR.

## Qualification

Generated evidence belongs under ignored `artifacts/local/drawing-generation/`.

| Check | Evidence |
| --- | --- |
| Solution Release build | `frozen-build.log`: zero errors; 22 existing nullable/xUnit/WebAssembly interop warnings |
| Firmament drawing focus | `frozen-focused.log`: 36 passed |
| CLI drawing focus | `frozen-cli.log`: 7 passed |
| Core fast lane | `frozen-fast.log`: 1,087 passed |
| Complete solution | `frozen-full.log`: 5,647 passed, zero failures/skips; includes 1,220 Core and 2,929 Firmament tests. FrictionLab reports no discovered tests. |
| Client TypeScript and full Vitest | Typecheck succeeded; 19 files / 91 tests passed (`client-typecheck.log`, `client-full.log`) |
| Existing FrontendX4 geometry corpus | `frozen-corpus.log`: 32/32 passed, including chimera/reference geometry; no baseline recording used |
| Real CLI | Part build; generation from part and STEP; authored bearing-block and Assembly drawings all succeeded |
| Presentation parity | Part and its STEP produce byte-identical PDF, SVG and PPTX |
| Body geometry parity | First 414 geometric entities match the pre-change STEP byte-for-byte; SHA256 `c7ec5a43100cdf5368016315bbd02bbca59c82855f68cb50737f8845a73efe9c` |
| Final generated example | Three A4 landscape pages; all ten named toleranced diameters present exactly once; located datum; zero text/model, text/text or text/stroke collisions; zero failed annotations |
| PDF inspection | Searchable text, embedded Inter, no image objects; correct author and signoff dates |
| Native PPTX | Three native title tables, six columns/five rows with merges; existing Open XML validator tests pass; no raster replacement |
| Visual review | All seven rendered pages inspected: generated plate (3), authored part (2), authored Assembly/BOM (2) |

Final example: `final/InspectionPlateDrawing.pdf`, `.svg`, `.pptx`, `.drawing.json`
and `.validation.json`. `artifact-qualification.json` records cross-format hashes,
geometry parity and render paths. No canonical source or geometry baseline was
rewritten. PowerPoint desktop rendering/editing was not exercised; PDF rendering,
package structure and Open XML validity are the qualification performed here.

Negative regressions cover missing geometric association, foreign PMI units,
missing nominal values, unqualified dimension kinds, and incomplete/invalid
signoff dates. Positive regressions cover source immutability, deterministic
artifacts, retained authored names/depth, measured long labels, a true projected
diameter witness, native tables, and authored checker/date metadata.

The final lanes ran serially with `MSBUILDDISABLENODEREUSE=1` and
`DOTNET_PROCESSOR_COUNT=2`:

```powershell
dotnet build Aetheris.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Firmament.Tests -c Release --no-build -m:1 --filter Drawing
dotnet test Aetheris.CLI.Tests -c Release --no-build -m:1 --filter Drawing
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build -m:1 --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
python fixtures/Regression/FrontendX4/run.py
```

`git diff --check` passes. At closeout there are no remaining dotnet, testhost,
MSBuild or VBCSCompiler processes. Superseded task-owned qualification process trees
were stopped before replacement runs; no unrelated runtime hosts were terminated.

## Title table and signoff follow-up

The user's review requested an actual title table and Drawn/Checked/Approved
traceability. The shared IR now contains a six-column table with merged cells,
measured text lines and paper-space cell bounds. PDF/SVG project it directly;
PPTX emits a native DrawingML table and React an HTML table. Legacy Fields and the
two-argument Compile API remain available for compatibility.

Role names and ISO dates are explicit paired inputs. The published specimen credits
`Codex (AI)` with drawn date `2026-10-18`; checked and approved remain blank. Authored
metadata can supply role pairs and keeps Author/Date fallback. PDF and PPTX document
metadata also retain the provided preparer/date; the previous unrelated hardcoded
PDF/PPTX creation dates are removed. This is traceability, not signature validation.

The PDF backend previously wrote numeric millimetre line weights directly as PDF
points. Conversion is now explicit, matching the SVG/PPTX paper units. Surface grids
are lighter, primary boundaries stronger and title cells use muted label text with
a pale header band. Small holes use a rim leader without redundant interior arrowheads;
a sufficiently large face-on diameter may carry a measured two-arrow witness.

## Code-quality review

The existing semantic exporter/selector owns PMI associations, the shared BRep
sampler owns curve evaluation, the existing drawing compiler owns artifact writing,
and the Drawing IR owns table cells and annotation strokes. There is no new parser,
source rewrite, geometry solver, renderer-side PMI interpreter or global mutable
layout state. The original two-argument Compile entry point and inspector Name
representation remain compatible; new metadata is additive. Drawing presentation
changes do not mutate source or BRep geometry.

## Verdict

**Success for the bounded integration and title-table/signoff pass.** The real CLI
generates searchable vector PDF, SVG and native editable PPTX from a part or its
supported AP242 PMI. The motivating dense plate retains all ten named toleranced
diameters, source-associated anchors and body geometry, with readable sheets and
explicit preparation credit. Existing authored part/Assembly drawings, tables,
BOMs and review packages remain qualified by the regression suite.

This is not a claim of complete manufacturing dimension coverage or standards
certification. Broader V2 distance/control export, endpoint-based linear dimensions,
automatic section/detail/assembly view selection, general third-party PMI adapters
and DXF remain explicit follow-up work. Unsupported intent rejects instead of
receiving guessed units, anchors, dimensions or approvals.
