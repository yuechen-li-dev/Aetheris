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

This draws the PMI a file already carries on wireframe views. Sheets are marked
**REFERENCE ONLY / NOT FOR MANUFACTURE**, and nominal completeness is false.
Manufacturing generation options are rejected in this mode. Unsupported wireframe
geometry also reports a named error. A reference view is not a substitute for
complete dimensions. The part's sampled envelope is stated in the sheet notes.

The first sheet is an overview: TOP above FRONT and RIGHT beside FRONT, aligned in
third-angle projection at one standard scale (1:1, 1:2, 1:5, 1:10, ...) chosen as
the largest that fits the part above the title block. Hidden edges are thin, grey
and dashed; a view whose hidden edges would outweigh its visible ones shows visible
edges only. The isometric shows visible edges and the outlines of curved faces
only, at its own standard scale, in the top-right space or, when that is larger,
the full-height space right of RIGHT. Every view is labelled with its name and
scale as a ratio, such as `SCALE 1:10`.

Each requirement is a numbered balloon on the view that shows its faces best, and
one row of the **PMI SCHEDULE** sheets that follow: ID, characteristic,
requirement, datum reference frame and the STEP entity it came from. Datum features
keep their letter in a box. Reading requirements from a table rather than from
leaders that cross the part is deliberate, and it keeps every control's datums in
one column.

Balloons sit on rings around the view, never on the geometry. Each takes the free
place nearest the direction of its feature from the view's centre, so leaders stay
short and in order; a place whose leader would cross another leader or many model
edges costs more, and crossing pairs are swapped when that removes the crossing. A
leader to an edge or hole ends in an arrowhead; a leader to a surface seen face-on
ends in a dot inside that surface; a datum feature's leader ends in a filled
triangle. Each view sheet holds up to 18 balloons; more continue on another sheet
of the same view. A balloon with no free place rejects with
`drawing-layout-impossible`, naming the balloon, its anchor and the view.

AP242 files from other producers are read through the schema, so their PMI is
written in millimetres (angles in degrees) whatever unit the file uses: a
`0.005 in` flatness per `0.25 in` square reads `0.127 /6.35×6.35`. Projected
zones (`P 10.795`), unequally disposed profiles (`U 0.5`), per-unit controls
(`/15`, `/Ø25.4`), common datums (`A-B`), MAX/MIN/AVG value qualifiers and
symmetry and concentricity controls are written as such. Sizes use Ø, R, SØ and SR.
A size or location the file states without a value is listed with the model's
nominal in parentheses and `NO VALUE IN FILE`; the drawing does not present it as a
requirement.

Records attached to auxiliary geometry instead of faces (datum target points,
isolated centre lines) are ballooned at that geometry. A record with no location
on the part (no face, and no auxiliary geometry inside the part's envelope) is
listed in the schedule as `NOT LOCATED` and gets no balloon; it is never dropped.

### Placing balloons by hand

A `Label` in the model's `Pmi` block moves one balloon. It changes where the
balloon is drawn, never the requirement. From
[`fixtures/Canonical/PMI/nist-ctc01-drawing-labels.firmament`](../../fixtures/Canonical/PMI/nist-ctc01-drawing-labels.firmament):

```firmament
Pmi {
    Label TopFlatness {
        record: "#57"
        side: Above
    }
    Label DatumA {
        record: A
        at: [120mm, 34mm]
    }
    Label SlotLocation {
        record: "#24"
        view: Front
        side: Below
    }
}
```

- `record` names the PMI record: `"#N"` is entity N of an imported STEP file (the
  STEP column of the schedule), any other value is a record name, such as a datum
  letter or a record declared in this `Pmi` block.
- `view` is `Top`, `Front` or `Right`: the view sheet that shows the balloon.
- `side` is `Left`, `Right`, `Above` or `Below` its view; the balloon takes the
  best free place on that side.
- `at` is the balloon's centre in millimetres from the sheet's top-left corner;
  it is used as written.

A Label sets at least one of `view`, `side` and `at`. An unknown field, an invalid
value or a record the part does not have rejects with
`firmament-v2-pmi-label-invalid` and the reason; a record that does not survive
export rejects drawing generation with `drawing-label-record-unresolved`.

The same PMI becomes a 3D model with callouts:

```powershell
aetheris mesh nist_ctc_01_asme1_ap242-e1.stp --format glb --output artifacts/local/mesh/ctc01.glb --json
```

The GLB holds the part in millimetres and one callout per requirement, regenerated
from the semantic records: feature control frames, datum symbols, sizes with their
limits, angles in degrees, and the parameters above. The file's own presentation
glyphs are not reused. A requirement on several faces, or on a cone, torus or
freeform face, points at its largest face; a hole whose axis is not Z points at its
wall. A size on several alike holes that states no quantity is drawn as one `2X`
callout on all of them; one size on holes of different diameters rejects with
`pmi-display-target-multiple-unsupported`. Records with no face on the part are listed in the notes panel as
`NOT LOCATED ON PART`. On a crowded part a callout slides sideways along its face
before reaching further out; one with no free place rejects with
`pmi-display-layout-no-space`.

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
