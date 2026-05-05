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

## CIR-M5 structured differential report artifact

CIR-M5 adds a machine-readable JSON report writer to the differential harness so Codex and tooling can consume fixture-level outcomes without scraping assertion text.

### Generation path

Run the focused artifact test:

`dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter CIRvsBRep_DifferentialReportArtifact_IsGeneratedAndReadable`

The test executes the same case matrix path and emits:

`<test-bin>/artifacts/cir/differential-matrix/latest.json`

This location is deterministic per test output directory and is not committed to source control.

### Report shape (stable fields)

Top-level fields:

- `success`
- `generatedAtUtc`
- `matrixName` (`cir-vs-brep`)
- `resolution`
- `fixtureCount`
- `passedCount`
- `failedCount`
- `unsupportedCount`
- `fixtures`

Per fixture fields include:

- identity and expectation: `name`, `path`, `expectedSupport`, `status`, `mismatchClass`, `notes`
- CIR section: lowering success/diagnostics, bounds, volume metric
- BRep section: build success, bounds, approximate volume, unknown ratio
- comparison section: bounds/volume/probe pass-fail placeholders for deterministic schema consumption

### CIR-M5.1 report semantics and stability contract

- The artifact-generation test is **reporting-only** and must pass even when CIR-vs-BRep semantic drift exists.
- Drift belongs in fixture data (`status=failed`, `mismatchClass`, probe mismatches, volume deltas), not as an artifact-test failure.
- Matrix assertion tests remain drift-sensitive and continue to fail when unexpected disagreement appears.

### How Codex should use it

- Parse JSON directly from the deterministic artifact path.
- Use top-level counters for quick trend checks.
- Use per-fixture `status` and `mismatchClass` to distinguish unsupported fixtures from supported-but-drifted fixtures.
- For supported failed fixtures, read `comparisons.bounds`, `comparisons.volume`, and `comparisons.probes` first:
  - bounds: `maxAbsDelta`, `tolerance`, `passed`
  - volume: `absDelta`, `relativeDelta`, `tolerance`, `passed`, plus BRep `unknownCount`/`unknownRatio`
  - probes: `probeCount`, `mismatchCount`, structured mismatch entries with CIR/BRep classification and BRep-unknown indicator
- Use notes/diagnostics as secondary evidence when a fixture is failed/unsupported.

### Limitations

- BRep volume remains voxel-estimated and inherits analyzer uncertainty.
- `generatedAtUtc` is intentionally variable; tests validate presence/shape, not exact value.
- Comparison detail is now populated for bounds/volume/probes in CIR-M5.1; future work should focus on classification taxonomy tightening rather than placeholder fields.
