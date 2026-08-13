# M2 API architecture

Public ownership remains in `Aetheris.Geometry`: `CurveJet2`, `PatchJet2`, `DifferentialPolicy`, `CurveCurvatureResult`, `PatchCurvatureResult`, `NormalCurvatureResult`, and `CurvatureQuery`. The bounded curve/patch objects expose `SupportsSecondJet` and `EvaluateJet2`; unavailable procedural capability is explicit.

`PredicateEvidenceKind` is reused. Successful local floating-point curvature is `ToleranceBounded`; deterministic whole-seam sampling is `Sampled`; singular or unavailable states are `Unknown`. Representation kind never upgrades evidence.

Surfacing owns only correspondence: it maps semantic Panel edges to patch boundaries, aligns tangent planes/orientations, selects geometric transverse directions, and consumes public normal curvature. It does not implement fundamental-form math or compare raw parameter-grid derivatives.

No topology is authored, no generic intersection/contact-order service is introduced, and no theorem-specific semantics enter the dependency graph.
