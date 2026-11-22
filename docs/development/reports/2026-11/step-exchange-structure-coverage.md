# STEP exchange-structure and entity-form coverage

Reference: NIST STEP File Analyzer and Viewer (usnistgov/SFA). Its syntax checker delegates to
ST-Developer and cannot be reproduced, so the reference is what SFA checks itself
(`getSchemaFromFile`, `checkP21e3`, `\X2\` decoding) and its entity categories (`sfa-data.tcl`,
281 geometry/topology entities in `stepGEOM`). Evidence is two mutation corpora built from
`block-basic.step`: 60 malformed or variant files and 37 equivalent entity forms, run through
`Step242Importer.ImportBody`.

## What changed

Parser (`Step242SubsetParser`):
- Reads the exchange structure instead of scanning for `DATA;`: `ISO-10303-21;`, HEADER with
  FILE_SCHEMA, any number of DATA sections, edition 3 ANCHOR/REFERENCE/SIGNATURE (skipped),
  `END-ISO-10303-21;`, BOM. Structural refusals: not STEP, missing start, no DATA, unclosed DATA
  or missing end ("incomplete ... probably truncated"), IFC schema.
- Header warnings: no FILE_SCHEMA, unknown schema. Info: user-defined `!` entities.
- `&SCOPE ... ENDSCOPE`, user-defined constructors, binary literals.
- Nesting is bounded at 256 (the 200 000-deep case was a stack overflow).
- Strings decode `\\`, `\S\`, `\P?\`, `\X\`, `\X2\`, `\X4\` (PMI text such as `±` was raw before).
- Errors name entity, line and column, and say when the text ends at the error.

Normalization passes (geometry unchanged):
- `Step242ImplicitKnots`: BEZIER/QUASI_UNIFORM/UNIFORM curves and surfaces get their
  ISO 10303-42 knots. Before: refused as "BOUNDED_CURVE/BOUNDED_SURFACE unsupported".
- `Step242EquivalentForms`: INTERSECTION_CURVE→SURFACE_CURVE, FACE_SURFACE→ADVANCED_FACE,
  ORIENTED_FACE (incl. `.F.` with reversed bounds), SEAM_EDGE→ORIENTED_EDGE, SUBEDGE→EDGE_CURVE on
  the parent curve, RECTANGULAR_TRIMMED/CURVE_BOUNDED surfaces and zero offsets → basis, a
  SHELL_BASED_SURFACE_MODEL of one closed shell → solid (warning).

Importer:
- POLYLINE edge geometry → exact degree-1 B-spline; TRIMMED_CURVE with point trims → basis with
  edge vertices; single-segment COMPOSITE_CURVE → segment curve (sense respected); a surface in a
  SURFACE_CURVE's associated geometry is skipped, not a failure.
- Orphan planar-face adoption is now a disclosed repair (`step-import-unlisted-faces-adopted`).
- Missing root says what the file holds (surface model, faceted B-rep, CSG, wireframe, mapped
  items, tessellated).
- No length unit in a representation context → millimetres with a warning.
- Parse notes reach `ImportBody`, `ImportRigidRoots` and the orchestrator result.

## Evidence

| Corpus | Before | After |
|---|---|---|
| Malformed/variant (60) | 9 crashes or misleading messages (stack overflow, "Unable to recover statement boundary", "expected PCURVE"), header never checked, orphan faces silent | every case imports or is refused with a structural/located message; nesting bounded |
| Entity forms (37) | 16 refused | 33 import as the unit cube; refused: CLOTHOID, nonzero-offset types (by name), CSG (named), two solids (assembly route) |
| Units | metre/cm/inch/foot scaled (verified 1000, 10, 25.4, 304.8 mm edges) | plus missing-unit warning |

`Step242ExchangeStructureCoverageTests` pins 16 refusals with their message, 34 equivalent forms
(volume 1, closed, consistent), 4 unit scales, the missing-unit warning and string decoding.
Existing test snippets with an empty HEADER now carry a FILE_SCHEMA; the corpus audit reports the
first error, not a header warning, as what blocked a file.

Not covered (SFA `stepGEOM` entries the importer does not read): CSG and feature solids,
volumes, tessellated items outside the tessellated lane, swept surfaces other than those already
supported, replicas and transformation operators outside assemblies, locally refined splines,
clothoids and involutes, multi-segment composite edge curves, nonzero offsets. Each is refused by
name.

## Verdict

Success. Random STEP files now either open or fail with a message that says what the file is and
where it breaks; equivalent ISO forms no longer fail on their spelling.
