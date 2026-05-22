# CIR-M2 differential CIR-vs-BRep analysis

CIR-M2 extends the experimental Firmament-to-CIR lowerer with a bounded selector-aware placement subset and adds differential checks against the production BRep execution/analyzer path.

## Supported placement subset (CIR-M2)

- `place.offset[3]` (existing CIR-M1 behavior, preserved).
- `place.on_face` lowered when canonicalized to a selector anchor with `.top_face` port.
- Supported selector lowering shape: `<featureId>.top_face`, where `<featureId>` is already lowered in-sequence.
- Anchor lowering: top-face anchor maps to `(centerX, centerY, maxZ)` from the referenced feature's CIR bounds, then optional `offset[3]` is added and emitted as a CIR `Transform` translation.

This is intentionally bounded to avoid semantic drift.

## Explicitly unsupported/deferred in CIR-M2

- `around_axis`, `radial_offset`, `angle_degrees` placement semantics.
- non-`.top_face` selector ports (edge/vertex/side faces/etc.).
- assembly placement, library-part placement semantics, custom op placement forms.
- generalized selector evaluation and runtime topology-dependent selector semantics.

Unsupported forms fail with explicit lowering diagnostics.

## Differential checks (CIR vs BRep)

For shared Firmament fixtures, tests compare:

1. Approximate volume
   - CIR: `CirAnalyzer.EstimateVolume`.
   - BRep: voxelized `BrepSpatialQueries.ClassifyPoint` over body vertex bounds.
2. Point classification
   - CIR: `CirAnalyzer.ClassifyPoint`.
   - BRep: `BrepSpatialQueries.ClassifyPoint` for selected probe points.
3. Bounds
   - CIR: `CirNode.Bounds`.
   - BRep: bounds reconstructed from body vertices.

## What mismatches mean

A mismatch indicates either:

- placement anchor mismatch (feature anchor point or offset semantics differ),
- primitive local-frame convention mismatch,
- estimator-resolution sensitivity.

Given CIR-M2 scope, failures should be interpreted as semantic drift signals, not proof that one kernel is globally incorrect.

## Dual-kernel drift risks

- CIR currently approximates selector-anchor semantics via CIR bounds for a narrow anchor class.
- Production placement resolves anchors from runtime BRep topology/surfaces.
- Any expansion beyond bounded `.top_face` should either share resolver math/helpers or explicitly document divergence risk.

## Recommended CIR-M3

- Introduce a small shared placement-anchor semantic helper usable by both production execution and CIR lowering for face-anchor classes.
- Add bounded support for one additional deterministic face anchor class (`bottom_face`) with parity tests.
- Expand differential fixtures to include sphere-on-box semantic placement and explicit unknown-classification handling policy.
