# CIR-M4 differential matrix for CIR-vs-BRep semantic alignment

Status: Experimental validation harness (not production behavior change).

## Coverage

The matrix compares CIR against BRep analyzer behavior only for currently supported CIR subset fixtures:

- Primitives: `box_basic`, `cylinder_basic`, `sphere_basic`
- Booleans: `boolean_box_cylinder_hole`, `boolean_subtract_basic`, `boolean_add_basic`, `boolean_intersect_basic`
- Placement: `placed_primitive`, `w2_cylinder_root_blind_bore_semantic`

Explicit unsupported coverage check:

- `rounded_corner_box_basic` must fail CIR lowering clearly as unsupported.

## What is compared

For each supported fixture case:

1. **Lowering status**: CIR lower success/failure and diagnostics.
2. **Bounds**: CIR AABB vs BRep vertex-derived AABB (`min/max/extents` deltas).
3. **Approximate volume**:
   - CIR: `CirAnalyzer.EstimateVolume(root, resolution)`
   - BRep: voxel estimate using `BrepSpatialQueries.ClassifyPoint` over BRep AABB at identical resolution.
4. **Probe classifications**: fixed semantic probe points compared between CIR and BRep point classification.

## Tolerances and resolution

- Shared volume resolution: `72`.
- Bounds tolerances are case-specific (`0.001` to `0.02`) to allow finite numeric drift while catching semantic frame drift.
- Volume relative tolerance is case-specific (`0.02` to `0.20`) with larger tolerance on placement/boolean cases where classification and voxel discretization amplify uncertainty.

## BRep analyzer uncertainty policy

BRep point classification uncertainty is surfaced, not hidden:

- Approximate volume path tracks `unknownCount` and `sampleCount`.
- Probe points marked as certainty-required fail if BRep returns `Unknown`/failure.
- Unknown behavior is reported as analyzer certainty limitation class rather than silent agreement.

## Mismatch classes used in diagnostics

Harness failure messages classify likely drift as one of:

- placement drift
- primitive convention drift
- boolean semantic drift
- analyzer uncertainty / unsupported BRep analyzer certainty
- unsupported CIR subset

## Known limitations

- BRep volume is currently approximate via voxel classification, not analytic solid volume integration.
- Boundary-proximal probes are intentionally minimized to avoid overfitting classification convention differences.
- Unsupported selector and generated-topology semantics remain guarded by SEM-A0 constraints.

## What mismatches imply

- Bounds mismatch usually signals placement/local-frame convention drift.
- Volume mismatch with aligned bounds often signals boolean semantic drift or sampling uncertainty.
- Probe mismatch isolates local semantic disagreements better than aggregate volume alone.

## Recommended CIR-M5 next step

Add a small structured differential report artifact (JSON) per matrix run capturing per-fixture metrics, unknown ratios, and mismatch classes so trend regression can be tracked over time without parsing test logs.
