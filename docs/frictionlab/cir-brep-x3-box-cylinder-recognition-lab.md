# CIR-BREP-X3 — FrictionLab canonical box-cylinder recognition experiments

## Scope
Lab-only experiments in `Aetheris.Firmament.FrictionLab/CIRLab` and `Aetheris.FrictionLab.Tests/CIRLab`.

## 1) Exact CIR shape
- Hypothesis: recognize `CirSubtractNode(CirBoxNode, CirCylinderNode)` with optional pure translations.
- Experiments: direct node + translated wrappers tests.
- Evidence: direct and translated cases pass; non-subtract rejected.
- Conclusion: first production recognizer should require subtract(box,cylinder) after optional translation-unwrapping.
- Recommendation: accept only identity/pure-translation wrappers in v1.
- Confidence: High.

## 2) Box/Cylinder coordinate conventions
- Experiments: bounds/evaluate assertions.
- Evidence: box centered at origin with half extents; cylinder centered at origin, Z-axis, half-height in ±Z.
- Conclusion: CIR primitive conventions are origin-centered signed-distance primitives.
- Recommendation: normalize around these conventions, then apply extracted translations.
- Confidence: High.

## 3) Through-hole requirement
- Experiments: short cylinder rejection, spanning cylinder acceptance.
- Evidence: height/depth and end-cap coverage checks separate through vs blind.
- Conclusion: v1 should require full through depth with tolerance band.
- Recommendation: reject blind/partial/miss.
- Confidence: High.

## 4) Allowed axes
- Evidence: `CirCylinderNode` encodes only canonical Z axis; no intrinsic arbitrary-axis field.
- Conclusion: axis variation must come from transforms.
- Recommendation: v1 supports only native Z-axis cases (plus translation).
- Confidence: High.

## 5) Transforms
- Experiments: pure translation vs rotation wrappers.
- Evidence: translation unwrap is safe; rotation wrapper rejected.
- Conclusion: detect pure translation by transformed basis vectors.
- Recommendation: reject rotation/nonuniform/shear in v1 with explicit reason.
- Confidence: High.

## 6) Firmament lowering shape for `boolean_box_cylinder_hole`
- Evidence (source inspection): lowerer always emits primitive local-frame + placement via `CirTransformNode`, then boolean `CirSubtractNode`.
- Conclusion: real path is frequently subtract of transformed operands, not always literal raw primitives.
- Recommendation: recognizer should include wrapper normalization.
- Confidence: Medium-High.

## 7) Recognition entrypoint
- Evidence: CIR node tree has enough for canonical v1 (shape + transforms + dimensions).
- Conclusion: node-first recognizer is sufficient for first scope.
- Recommendation: implement node recognizer first; add plan/replay adapter only if needed.
- Confidence: Medium.

## 8) What counts as box
- Evidence: accepted only `CirBoxNode`; non-box lhs rejected in tests.
- Recommendation: strict `CirBoxNode` for v1.
- Confidence: High.

## 9) What counts as cylinder
- Evidence: accepted only `CirCylinderNode`; sphere tool rejected.
- Recommendation: strict finite circular `CirCylinderNode` only.
- Confidence: High.

## 10) Hole position constraints
- Experiments: centered, offset-valid, tangent, outside.
- Evidence: strict interior clearance inequalities reject tangent/grazing/outside.
- Recommendation: allow offsets when strict clearance holds; do not force centered-only.
- Confidence: High.

## 11) Tolerances
- Evidence: experiments use `ToleranceContext.Default.Linear` from kernel numerics.
- Recommendation: use shared tolerance context; classify near-boundary as tangent/unsupported conservatively.
- Confidence: High.

## 12) Recognizer return shape
- Evidence: lab result record carries success/reason/diagnostic, normalized primitives+translations, axis, through-length.
- Recommendation: production result should be structured, not bool.
- Confidence: High.

## 13) Recognition vs builder coupling
- Evidence: lab recognizes and returns normalized parameters without building BRep.
- Conclusion: separation is practical.
- Recommendation: recognizer returns normalized params; builder consumes them.
- Confidence: High.

## 14) Unsupported variants behavior
- Evidence: unsupported test matrix exercises stable reason codes.
- Recommendation: no fallback/guess; return explicit unsupported reason vocabulary.
- Confidence: High.

## 15) Production test needs
- Recommendation matrix:
  - success: direct subtract, translated subtract
  - rejects: non-subtract, lhs non-box, rhs non-cylinder, unsupported transform, blind/not-through, tangent/outside
  - tolerances: near-boundary around clearance threshold
- Confidence: High.

## Recommended production recognizer policy
Recognize only canonical `subtract(box,cylinder)` after unwrapping identity/pure-translation transforms; require positive dimensions, Z-axis cylinder convention, strict through-hole depth coverage, and strict XY interior clearance with shared tolerance.

## Recommended production recognizer result shape
`Success`, `ReasonCode`, `Diagnostic`, normalized `Box` dimensions, normalized `Cylinder` radius/height, `BoxTranslation`, `CylinderTranslation`, `Axis`, `ThroughLength`, optional source-node references.

## Recommended production test matrix
Copy/adapt all CIRLab tests under `Aetheris.FrictionLab.Tests/CIRLab` into production recognizer suite once production implementation starts.

## Known deferred cases
- Rotated cylinders/boxes.
- Nonuniform-scaled/sheared transforms.
- Lowering-plan/native replay metadata adapters.
- Multi-hole/composite boolean chains.
