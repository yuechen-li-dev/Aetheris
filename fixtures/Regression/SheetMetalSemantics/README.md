# Sheet-metal semantic/manufacturing admission corpus

These specimens preserve the 2026-10-16 audit's failing intent. Do not edit a
negative case to make it pass. The expected change is in its production owner.
The audit report under `docs/development/` records the original outcomes.

Run `python fixtures/Regression/SheetMetalSemantics/run.py` after building the
Release CLI. It checks validate/build agreement, structured errors, inspection,
and exported AP242 records. Output goes to ignored `artifacts/local/`.

Negative cases retain ignored fields/operations, unsupported XZ placement, a false
Require, missing structural members (with and without semicolons), ambiguous
legacy layout effects, unknown PMI, misspelled release fields, negative allowances,
zero quantities, unknown dimension kinds and duplicate PMI blocks.

Positive compatibility cases retain original geometry for Point2/tuple spellings,
newline fields, lexical Datum resolution, unnamed PMI and legacy Slot/Rectangle
capsules. Former manufacturing limitations now admit quoted comment markers,
authored-hole PMI targets and typed dimension binding.

`modern-layout-release.firmament` demonstrates scoped layout, Record tolerances,
Concept checks, process metadata and explicit PMI. `keyed-features.firmament` and
`keyed-profile-cuts.firmament` exercise the ordinary Set/Feature/Pattern owners
feeding the existing sheet Hole/Cut consumers. The companion .NET tests assert
geometry, centers, stable IDs, exact area, binding origins and AP242 associations.

`shared-construction-layout.firmament` checks that ordinary Point2/Rect2 design
data and a lowercase XY frame retain the shared Profile owner and scalar values.

Multiple implicit legacy effect bundles reject; selecting one with `intent:` is
not implemented. Put new material operations in the consuming SheetMetal body.
The misleading legacy Pattern Rectangle alias remains a capsule for geometry
compatibility. It is not a spelling recommendation for new rectangular cuts.
