# Generate dimensioned engineering drawings

A drawing must define the nominal part without requiring its CAD file or measuring
its picture. The compiler distinguishes nominal geometry coverage, specified
limits, material and actual process qualification. Searchable PDF text and a clean
layout are presentation checks, not manufacturing approval.

Helios exposes PDF, SVG and PPTX in **Export** for supported parts, using this same
dimension planner. Selecting several formats downloads a ZIP. The browser projects
the current compiled STEP and checks that authored PMI has not disappeared; it does
not invent material, tolerances or signoffs. Use the authoring workflow below to
supply those requirements. Assembly/Scene automatic drawings are not exposed there.

## Generate a dimensioned plate

```powershell
aetheris drawing generate fixtures/Regression/DrawingGeneration/inspection-plate.firmament --out-dir artifacts/local/drawing/inspection-plate --linear-tolerance 0.1 --angular-tolerance 0.5 --material "6061-T6 aluminium" --drawn-by "Codex (AI)" --date 2026-10-18 --json
```

`generate` also accepts `.step` / `.stp`. The default output directory is
`artifacts/local/drawing/<source-name>`. PDF, SVG, editable PPTX, Drawing IR and a
validation report share the same dimension plan. The compiler preserves source and
body geometry. Generation options are explicit drawing authoring inputs; they do
not rewrite the part or add these requirements to its AP242 PMI.

The current complete-definition family is an axis-aligned rectangular plate/box
with disjoint circular Z-through holes. Recognition accounts for every face,
rectangular stock boundary, full circular opening and cylinder seam. Fillets,
chamfers, blind holes, counterbores, nonrectangular outlines, oblique holes and other
families require an authored complete drawing until their dimension planners are
qualified. They fail automatic generation with `drawing-definition-incomplete`;
a sampled bounding box is never used to claim complete dimensions.

The [code-native bracket rehearsal](code-native-bracket.md) demonstrates the
authored route for a traced L outline with a countersunk through hole. Its final
PDF is independently reconstructed and compared with STEP for two parameter sets.
That bounded authoring helper does not extend the automatic recognition family.

A qualified plate receives:

- Exact controlling width and height dimensions, and thickness on an aligned edge
  view. These values come from stock planes, not sampled display bounds.
- Hole center marks and IDs, a coordinate schedule from the left and bottom
  finished edges, entry-face/orientation information, and explicit THRU extents.
  Visible X0 and Y0 leaders identify those zero edges; `(0,0)` marks their
  intersection on the top face. The table's X and Y headers each state their
  bilateral tolerance, for example `X ±0.1` and `Y ±0.1` (mm).
- Grouped diameter/limit callouts only when size, limits and extent agree.
  Original PMI names, targets and face identities remain in provenance.
- A datum indication on the actual associated plane in a useful view. Coordinate
  edge references do not silently create extra GD&T datums.
- Light internal hole-wall lines in the side view, rather than treating a
  cylinder's parameter seam as an exterior edge.

Coordinates are independent baseline dimensions. No row/column pattern is guessed
from an apparently regular picture. Irregular coordinates remain individual values.
Hole schedules paginate beyond twelve rows; more than four distinct size/limit
groups currently requires authored sheet allocation. Crowded labels or oversized
values reject instead of clipping, shrinking or omitting engineering information.

## Tolerances and manufacturing authority

`--linear-tolerance <mm>` explicitly specifies bilateral stock-size and X/Y
coordinate limits. `--angular-tolerance <degrees>` specifies the general angular
allowance. `--material <name>` supplies the material for the drawing. These are
not defaults and never override existing diameter PMI.

Each table coordinate is measured from its marked zero edge with its own ± limit;
the two limits define independent Cartesian intervals, not a circular GD&T position
zone. X0/Y0 are drawing coordinate references, distinct from authored datum PMI
such as A. They do not silently add B/C datum requirements to the source or AP242.
An unspecified coordinate tolerance prints `TOL?` in each header and remains an
incomplete requirement.

Without these inputs, nominal dimensions are still complete for a recognized part,
but the sheet prominently identifies missing requirements and is not for
manufacture. Missing hole diameter tolerances remain missing as well. JSON reports
`nominalGeometryComplete`, `dimensionalTolerancesComplete`, `materialSpecified`,
`processCapabilityVerified` and `missingRequirements` independently. Authored
Drawing compilation has no automatic coverage assessment; its evidence remains
unassessed rather than implicitly complete.

The planner checks positive stock/diameter intervals and screens remaining hole
webs against Cartesian entry-center limits and cylinder-axis lean on ideal
orthogonal stock. The evidence records that assumption. This bounded interference
screen does not qualify stock form, general angular stacks, surface finish,
inspection uncertainty, a manufacturing process or functional fit. It is not
permission to declare a part manufacture-ready.

For the example, the retained source diameter limits are 3.98-4.05 mm. Those are
source requirements, not a claim that ordinary drilling will meet them, nor an M4
clearance-fit designation. A suitable finishing/inspection process must be agreed.
See the material/process-dependent [Protolabs machining capability table](https://www.protolabs.com/services/cnc-machining/precision-machining-tolerances/).

Conflicting diameter, depth or quantity PMI rejects. Unknown dimension kinds,
unlocated PMI and unqualified AP242 families also reject; semantic requirements
cannot disappear because nominal geometry looks plausible. Automatic PMI readback
currently admits the existing Aetheris millimetre subset, not universal third-party
AP242 PMI. Use multiline fields inside source PMI declarations; some semicolon
forms remain frontend debt.

## Explicit reference visualization

```powershell
aetheris drawing generate model.step --reference-only --out-dir artifacts/local/drawing/reference --json
```

This retains the earlier PMI/wireframe review sheets with approximate `REF`
envelopes. Sheets are marked **REFERENCE ONLY / NOT FOR MANUFACTURE**, and nominal
completeness is false. Manufacturing generation options are rejected in this mode.
Unsupported wireframe geometry also reports a named error. A reference view is not
a substitute for complete dimensions.

## Authored drawings and formats

`aetheris drawing compile <drawing.firmament> --out-dir <directory>` retains explicit
Drawing/Templates, selected views, metadata Records, design tables, Assembly BOMs
and reviews. `generate` delegates a source's explicit Drawing declaration to that
owner. Automatic generation options cannot silently alter an authored drawing.

Within an authored View, `HiddenLines: Wireframe` uses the shared trimmed BRep
wireframe sampler. `VisibleOnly` and `VisibleAndHidden` retain existing bounded
occlusion behavior. Wireframe can show rear geometry but cannot replace dimensions
or supply an unstated hole depth.

PDF uses embedded Inter, searchable text and vector strokes. SVG, React and editable
PPTX consume the same annotation geometry and table data. PPTX requires Inter in the
editor for matching metrics. Text/leader/model and table keepouts are checked;
layout errors reject. DXF and general automatic section/detail/Assembly planning
remain separate work. No general ISO/ASME certification is claimed.

## Title table and traceability

Every sheet has a merged-cell title table for title/identity, material, part number,
revision, scale, units, sheet and status. Separate Drawn / Checked / Approved names
and dates are explicit paired inputs:

- `--drawn-by <name> --date <yyyy-MM-dd>`
- `--checked-by <name> --checked-date <yyyy-MM-dd>`
- `--approved-by <name> --approved-date <yyyy-MM-dd>`

These flags also work with `drawing compile`. The authored metadata Record can
supply `DrawnBy`/`DrawnDate`, `CheckedBy`/`CheckedDate` and
`ApprovedBy`/`ApprovedDate`; legacy `Author`/`Date` remain the Drawn fallback.
Unperformed reviews remain blank. Preparation credit, including AI assistance,
is traceability rather than a digital signature or automatic engineering approval.

## Reconstruction qualification

The regression reader `fixtures/Regression/DrawingGeneration/reconstruct_from_pdf.py`
reads only visible PDF dimensions and tables, writes a fresh Firmament source and
can compile it through the real CLI. It does not read the original source, Drawing
IR or embedded attachments. Compare that frozen reconstruction with the original
only afterward. The witness is bounded to this plate convention, not general OCR
or an automatic engineering reviewer.

The layout follows the completeness and nonredundancy principles in
[MIT's engineering drawing guide](https://ocw.mit.edu/courses/2-007-design-and-manufacturing-i-spring-2009/pages/related-resources/drawing_and_sketching/).
