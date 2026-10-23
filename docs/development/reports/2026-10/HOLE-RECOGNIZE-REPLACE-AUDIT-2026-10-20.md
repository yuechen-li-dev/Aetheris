# Hole Recognize / Replace — current capability audit

This was an assessment, not implementation of a new recognition or editing feature.
Only documentation was changed. Source specimens, CLI JSON and STEP output are
retained under ignored artifacts/local/hole-recognize-probes/ and
artifacts/local/hole-recognize-current-*.

## Verified current behavior

- The canonical Integration/inline-step-recognize-replace.firmament source passes
  validate and build. Export reports one planned/verified/emitted replacement,
  holeShaft-bounded-rebuild, and residualSurgery=false.
- The 54 FirmamentV2InlineStepTests pass against the current Release binaries.
- Changing diameter from 2 mm to 3 mm rejects with
  firmament-inline-step-replacement-verification-failed. This is not a qualified
  imported-hole resize workflow.
- Changing only the recognized Faces list from [7] to [1] still builds. Input
  sequential face 1 is STEP #51 on a PLANE; face 7 is #191 on a
  CYLINDRICAL_SURFACE of radius 1. The authored hole kind is not fully proved.
- Changing HostSize from [10mm,8mm,6mm] to [20mm,8mm,6mm] still builds. Output
  bounds are X=-10..10, Y=-4..4, Z=0..6. The imported host was 10 mm wide.
  The report's verification does not establish host preservation.

The probes use the same relative import path as the canonical fixture, from an
artifact folder at the same depth. An initial attempt using an absolute InlineStep
path was rejected by the existing path policy; those path errors were not treated
as geometric capability evidence.

## Ownership and root cause

FirmamentV2Parser binds authored region kinds, face references and replacement
parameters. FirmamentSemanticValues attaches exact BRep face/region bindings and
imported entity provenance; it does not infer the complete manufacturing feature.

FirmamentBuildAndExport.TryExportV2InlineStepReplacementBody is a bounded rebuild
owner. It counts matching-radius cylindrical supports, creates a synthetic Box
from HostSize, constructs a +Z through-hole with the existing shaft materializer,
and exports that reconstructed body. Its volume check compares quantities derived
from the rebuilt host; it does not independently prove equality to the import.
It does not preserve arbitrary residual topology by surgery.

The proposal assist is review-only metadata. Its replacementReady flag is not
execution or geometric preservation proof. The older X4/X5/X7 reports describe
this prototype and should not be read as a general imported-hole editing contract.

## Suggested pre-launch slice

1. Close false verification first: verify the selected faces and the entire admitted
   host, derive host facts from the BRep, and reject inconsistent claims. Otherwise
   disable replacement outside a genuinely proved reconstruction class.
2. Recognize isolated analytic through-holes from actual imported BRep evidence:
   cylindrical wall, axis/radius, boundary loops, concave material relationship,
   compatible entry/exit supports and stable source associations. Explicitly reject
   ambiguous split/partial/intersecting cases until admitted.
3. Adopt a verified hole as named semantic geometry, attach PMI, and re-export
   without changing geometry. This is distinct from replacing/rematerializing it.
4. Permit regeneration/editing only for completely proved construction families
   with independent geometry/correspondence qualification. Arbitrary imported-body
   hole resizing, relocation, suppression or type conversion requires a separate
   topology-editing milestone.

No estimate in days is justified by this audit. Bounded recognition/adoption is a
reasonable next capability; general imported topology editing is materially larger
and should not be smuggled into launch polish. No full regression rerun was needed
for this documentation-only assessment; the focused tests and real CLI paths above
are the executed evidence, not a claim of new implementation qualification.
