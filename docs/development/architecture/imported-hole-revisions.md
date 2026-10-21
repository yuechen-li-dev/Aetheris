# Bounded imported through-hole revisions

Implemented production contract for one isolated round Z-axis through-hole move.
The [authoring guide](../../public/firmament/imported-feature-recognition.md) and
[golden fixture](../../../fixtures/Regression/InlineStep/production/ctc01-hole-move.firmament)
are the executable surface; the [closeout](../../release/IMPORTED-HOLE-REVISION-PRODUCTION-2026-10-20.md)
records qualification. Original experiments remain historical evidence.

## Ownership

The unified structural parser admits `InlineStep`, `Recognize`/`Region`, and
`Replace ... with Hole<Shaft>`. `FirmamentV2ImportedEditingBinding` consumes their
fields through the existing `FirmamentSyntaxFields` owner. Unknown fields and
statements reject. There is no new whole-document parser or source rewrite.

The loader reads one immutable byte snapshot and imports at the authored numerical
budget. Exactly one pin is required: `sha256` checks those same bytes; explicit
`textSha256` checks decoded UTF-8/LF text, changing line endings only. Browser
snapshots use their UTF-8 resource text as the byte representation. There is no
automatic fallback between modes; the report records the pin kind and input byte
hash. The LF text mode makes Git-normalized corpus files portable without rewriting
the original STEP. Raw `stepFaceEntities` and sequential `faces`
retain distinct meanings through the imported topology map.

Pinned imports select the bounded revision owner exclusively. They cannot fall
into the historical HostSize/Box reconstruction route or ignore additional Modify
operations. The current release admits one revision in a single-body Model.
Historical unpinned inputs retain their existing compatibility boundary; the old
rebuild route explicitly reports that residual surgery was not performed.

`FirmamentImportedHoleRevision` binds the selected walls and entry support, checks
recognized evidence, invokes Core, generates semantic diameter/depth PMI and
performs qualified STEP export/reimport. Validate executes the same build admission.
It retains the revised admission document and source-to-successor face map for
subsequent semantic projections. Source file bytes remain untouched.

## Geometric certificate and transaction

`BrepThroughHoleRevision` proves a single closed oriented manifold, qualified
trims, inward connected coaxial cylindrical walls, complete circular inner rims,
two distinct Z-normal planar supports and full same-depth coverage. The selected
set must equal the entire proved wall group. Axis/frame limitations reject rather
than project onto XY. Authored confidence never overrides geometric proof.

The destination is a world Point3 on the selected support. Diameter is unchanged;
ThroughAll resolves against that support pair, not global body bounds. The typed
`within` prism spans both supports and contains both complete old/new holes with
positive clearance. Every unrelated boundary face must be excluded using
conservative plane/cylinder/cone support bounds, finite line/circle/B-spline curve
hulls and the import uncertainty budget. Unsupported bounds reject. This can
refuse physically possible edits; it cannot choose a permissive fallback.

One atomic operation:

1. Prove source topology, recognition and stock exclusion.
2. Suppress the old walls and their two opening loops in a successor BRep.
3. Qualify the filled successor.
4. Materialize circles, seam lines, cylinders and vertices from the authored
   center/diameter, reusing the proved split-wall connectivity template.
5. Insert fresh topology identities into the retained residual body.
6. Qualify the result, export with enforced preflight and reimport as qualified.

Surviving face/edge/vertex identities, geometry objects, bindings and unrelated
loops/pcurves remain unchanged. Only the two support trim lists and shell wall
membership change among surviving topology. New walls clear original STEP entity
identity; retired-to-new correspondence records history separately. Shared geometry
outside the edit region rejects rather than overwriting another binding. Cached
Boolean/construction decomposition is cleared. Rejection never mutates the input.

There is one deterministic qualified strategy. No strategy framework or
JudgmentEngine dispatch is needed; future competing strategies would require
explicit admissibility and scoring in the existing JudgmentEngine.

## Accuracy and annotation boundaries

`importTolerance` is numerical admission accuracy, not manufacturing tolerance.
It defaults to .001 mm and must be explicitly pinned if overridden (positive,
finite, at most .1 mm). CTC-01's original curves differ from support surfaces by
about .0034 mm and its file declares .01 mm. The golden source explicitly uses
.01 mm; strict .001 mm still rejects. The cone-apex generator repair treats the
singular chart without modifying curves or surfaces. All 636 trims now qualify
at the explicit source budget. Export carries at least the original declared
accuracy and requested budget; it does not assert native precision.

This first release requires `geometryOnly: true` for pinned revisions, with a
source-located warning. It excludes the entire original annotation layer,
including vendor semantics unknown to the bounded inspector. The user expressly
approved this for CTC-01. Generic PMI passthrough/remapping needs a separate design.
New Hole PMI and additional supported authored Firmament PMI bind to revised
geometry; they do not represent inherited vendor requirements.

## Diagnostics and qualification

Syntax/binding errors retain AST spans. Kernel refusal uses
`firmament-imported-hole-revision-rejected` with the failing geometric explanation;
frontend source-pin, annotation, import qualification, evidence and roundtrip
failures have separate stable prefixes. Build JSON exposes `importedHoleRevision`,
including strategy, source pin, accuracy, annotation exclusion, retired/new face
IDs, preserved counts, envelope and face correspondence. The existing migration
report records residual surgery and actual imported topology counts.

Regression tests exercise real CTC-01 source, strict/explicit accuracy, unchanged
residual geometry and identities, semantic PMI associations, STEP roundtrip,
validate/build agreement and located refusals. The local tool delegates to Core
and checks three accepted moves and five refusals. Full solution qualification
remains mandatory because trim/export fixes affect interchange beyond this part.

Deferred: arbitrary frames, hole resizing, counterbores/countersinks, blind,
intersecting, threaded or blended holes, broader stock bounds, multiple edits per
Model, generic source PMI migration, and UI recognition/editing affordances.
Independent STEP producers beyond the admitted geometric family need their own
qualification; CTC-01 is a golden witness, not universal import certification.
