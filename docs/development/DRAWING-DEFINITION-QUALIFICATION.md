# Drawing definition and PDF reconstruction qualification

## Engineering review and replacement acceptance gate

The earlier drawing integration preserved diameter PMI and produced readable
vectors, but its example did not define hole locations or a controlling thickness.
Approximate REF envelopes did not specify stock acceptance. Zero annotation
collisions and successful export were insufficient acceptance tests. The original
integration report is retained with a superseding review note.

The replacement gate asks whether a reader can reconstruct the nominal part from
the PDF alone, then distinguishes specified limits from actual process capability
and engineering approval. A 3D nominal model is not evidence of manufacturing
capability. No source geometry was changed for this correction.

## Ownership and implementation

`BrepRectangularPlateQuery` is a read-only kernel query. It admits six stock planes
with exact rectangular boundaries and separated, inward-oriented circular
Z-through-hole walls. Every face, outer loop, opening, seam and edge count is
accounted for. Bound straight curves agree with their vertices; full circle trims
and axis directions are checked. Imported shell orientation must be qualified.
No sampled display bounds, source-name heuristic, history reconstruction or new
geometry operation establishes completeness.

The drawing dimension planner consumes those facts and associated PMI. It emits
controlling stock/thickness dimensions, an entry-face coordinate schedule from
finished edges, hole IDs/center marks and grouped size/limit/THRU callouts. Source
PMI identities survive grouping. An associated plane datum is shown in a useful
view, rather than attached ambiguously to a plan-view corner. The side view uses
qualified internal cylinder silhouettes as dashed lines, excluding parameter
seams from the exterior outline.

Both generation modes share numeric, kind and face-association PMI admission.
The default path does not execute the old overlay planner first. Reference-only
views remain an explicit visualization mode marked incomplete/not for manufacture.
Unhandled geometry or additional PMI families reject the default complete-plan
path; there is no implicit success fallback. Source Drawing declarations still
delegate to their authored owner and have no automatic completeness assessment.

PDF/SVG/PPTX/React project shared IR. The title/signoff table is retained, and hole
schedules are native editable tables. Notes, tables, dimensions, labels and leaders
participate in layout keepouts/audits. Long table values and impossible allocations
reject instead of truncating data. Round-trip numeric text avoids rounding small
tolerances to zero.

## Requirements and bounded tolerance screen

The demonstration supplies explicit drawing inputs: 6061-T6 aluminium, bilateral
stock/coordinate tolerance 0.1 mm and general angular tolerance 0.5 degrees.
These are example authoring choices, not inferred source facts. The original
diameter requirement remains 4 +0.05/-0.02 mm (3.98-4.05 mm); it is not advertised
as a clearance fit for a 4 mm shaft. AP242 body geometry and original PMI are not
rewritten with the additional drawing inputs.

The compiler records nominal completeness, specified dimensional tolerances,
material, missing requirements and process capability separately. Missing linear,
angular, material or hole-size requirements stay visible. Checked/Approved names
and dates remain blank. The source-record and CLI signoff paths remain compatible.

The arithmetic screen checks positive stock/diameter intervals and hole web limits
for independent Cartesian entry-center limits and cylinder-axis lean on ideal
orthogonal planar stock. Projected bore radius accounts for lean. Its assumptions
are explicit in IR. It does not qualify stock form, general angular stacks,
surface finish, a functional fit, measurement uncertainty or machining capability.
Diameter/depth/quantity contradictions and zero-width diameter limits reject.

For the example's 4 mm bore through 8 mm stock, a finishing/inspection operation
would need to be selected and qualified. The 0.07 mm size band is not a guarantee
for ordinary drilling. The [Protolabs material/process capability table](https://www.protolabs.com/services/cnc-machining/precision-machining-tolerances/)
distinguishes ordinary holes from specified bore tolerances. That is feasibility
context, not evidence that this part has been made or inspected. Functional fit
and release remain engineering decisions; `processCapabilityVerified` is false.

## Independent PDF reconstruction witness

`fixtures/Regression/DrawingGeneration/reconstruct_from_pdf.py` receives only a
PDF, output directory and optional CLI executable. It reads visible dimension
text and coordinate/type tables, then writes and compiles a fresh Firmament part.
It does not access the original source, Drawing IR or embedded attachments. The
reader convention is bounded to the documented dimensioned plate layout; it is
not general-purpose drawing OCR.

`qualify_pdf_roundtrip.py` freezes that independent reconstruction before reading
the reference STEP. It then uses the real CLI to compare topology, normalized
bounds, surface families and volume. A verification-only adapter independently
reads canonical STEP cylinder placements/radii, checking every hole's center and
size, so misplaced holes cannot pass merely by preserving volume and counts.
Global rigid placement is irrelevant to the local part definition.

Qualification runs the original ten-hole plate and a non-grid variant with one
center changed to (-34.125, -17.25) mm. Four incomplete PDF text variants must be
rejected: absent stock size, absent coordinate row, missing extent, and missing
coordinate tolerance. No fixture expectations or original part dimensions are
hard-coded into the independent reader.

```powershell
python fixtures/Regression/DrawingGeneration/qualify_pdf_roundtrip.py --pdf artifacts/local/drawing-definition/final/InspectionPlateDrawing.pdf --reference-step artifacts/local/drawing-definition/final/InspectionPlate.step --out-dir artifacts/local/drawing-definition/roundtrip --cli Aetheris.CLI/bin/Release/net10.0/aetheris.exe
```

## Qualification results

Raw evidence belongs in ignored `artifacts/local/drawing-definition/`; the
independent readers are durable fixture support. Public usage and boundaries are
maintained in `docs/public/drawings.md`.

| Gate | Result |
| --- | --- |
| Full solution Release build | `build-full.log`: zero errors, 30 pre-existing warning occurrences |
| Focused Firmament drawings | `focused-final.log`: 49 passed, including Open XML validation |
| Focused CLI drawings | `cli-final.log`: 7 passed |
| Core fast lane | `fast-final.log`: 1,087 passed |
| Complete solution regression | `full-final.log`: 5,660 passed, zero failures/skips; FrictionLab reports no discovered tests |
| Client TypeScript / complete Vitest | Typecheck passed; 19 files / 91 tests passed |
| Existing FrontendX4 corpus | `corpus-final.log`: 32/32 passed; no baseline recording |
| Independent reconstruction | Original and non-grid PDF-only reconstructions compile and match all cylinder axes/radii, topology, bounds and reported volume |
| Missing-data mutation checks | All four rejected for each of the two PDFs |
| Geometry preservation | Original 414 STEP geometric entities byte-identical to pre-refactor baseline; SHA256 `c7ec5a43100cdf5368016315bbd02bbca59c82855f68cb50737f8845a73efe9c` |
| Part / STEP presentation parity | PDF, SVG and PPTX bytes identical |
| Final visual inspection | Original and non-grid variants each fit one A4 landscape page; both PDFs rendered and inspected |
| Layout evidence | Zero text/model, text/text or text/stroke collisions and zero failed annotations for both examples |

`git diff --check` passes. The solution lanes ran serially with
`MSBUILDDISABLENODEREUSE=1`, `DOTNET_PROCESSOR_COUNT=2`, `-m:1`,
`-nr:false`, `UseSharedCompilation=false` and the prescribed full solution test
command. At closeout no dotnet, testhost, MSBuild, VBCSCompiler or aetheris runtime
hosts remain. Skill frontmatter/UI metadata validation also passes.

The CLI mass-property volume is explicitly `NumericalWithBound` with a triangulated
boundary integral, not an authoritative exact volume assertion. Its agreement is
a parity check. Independent analytic volumes from the printed stock/hole recipe
and reference primitive parameters are also compared; the plate's analytic nominal
volume is approximately 54,994.6903508513 mm3. This drawing pass does not change the
verification engine's numerical integration policy.

Final example files are under `final/InspectionPlateDrawing.*`. The frozen
reconstructions, PDF-only extracted definitions and comparison evidence are under
`roundtrip/` and `asymmetric-roundtrip/`. No original geometry expectations or source
part fixtures were rewritten. PowerPoint desktop rendering/editing was not tested;
native package validity, PDF rendering and shared presentation data were checked.

The focused gate includes successful geometry/coordinate coverage and native PPTX
validation, perturbed non-grid centers, unsupported sphere/blind geometry, missing
requirements, invalid tolerances, edge breakout, contradictory diameter/depth/
quantity, reference-only admission and preservation of previous drawing behavior.

## Remaining limits and code-quality review

The complete-plan family is rectangular stock with disjoint Z-through holes.
Other outlines, blends, blind/counterbored/countersunk holes, oblique holes,
sections and automatic Assembly planning require their own qualified production
owners. The current layout supports four distinct hole size/limit groups and
paginates coordinate schedules after twelve rows; dense labels can reject.
These are named stops, not evidence that an incomplete drawing defines a part.

This pass introduces no DSL parser, rewrite, diagnostic allowlist, geometry solver
or UI-side dimension inference. Kernel queries own geometric facts; the drawing
planner owns coverage/presentation; backends consume shared data. Authored drawing
and old reference-view tests remain, with changed automatic expectations reflecting
the new one-sheet complete definition rather than duplicate diameter sheets.

A reusable `mechanical-drawing-review` skill was created and validated in the
user's Codex skill directory. It requires reconstruction from the exported artifact,
function-driven limits, process/inspection distinction and truthful signoff. It
supports review rather than becoming a parallel compiler validator. Layout choices
follow [MIT's completeness/nonredundancy guidance](https://ocw.mit.edu/courses/2-007-design-and-manufacturing-i-spring-2009/pages/related-resources/drawing_and_sketching/).

## Verdict

**Success for the qualified plate/through-hole family.** The corrected one-page
PDF contains enough visible information to reconstruct the nominal part, with
explicit dimensional requirements and material rather than inferred defaults.
An independent PDF-only reader reconstructs both the original and irregular
variants, and geometry comparisons pass. The source body's exact geometry is
unchanged; unsupported automatic families stop explicitly.

This is not a general manufacturing-release or process-capability certification.
Qualified shop execution, stock form/finish, functional fit, inspection uncertainty
and engineering signoff remain distinct from the successful drawing-definition
gate. The implementation and public documentation preserve that distinction.
