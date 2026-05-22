# CIR-RECOVERY-V12: hole-family coverage matrix + diagnostics hardening

## Summary
V12 hardens production `HoleRecoveryPolicy` coverage and diagnostics for the current hole-family variants:

- `ThroughHoleVariant`
- `CounterboreVariant`
- `BlindHoleVariant`
- `CountersinkVariant`

This milestone adds matrix-style test coverage and diagnostics trace assertions. It does **not** add variants, change executor semantics, or change STEP exporter behavior.

## Coverage matrix

### Supported exact rows
- ThroughHole -> `ThroughHoleVariant`, `HoleKind.Through`, executable.
- Counterbore -> `CounterboreVariant`, `HoleKind.Counterbore`, executable.
- BlindHoleTop -> `BlindHoleVariant`, `HoleKind.Blind`, executable.
- BlindHoleBottom -> `BlindHoleVariant`, `HoleKind.Blind`, executable.
- Countersink -> `CountersinkVariant`, `HoleKind.Countersink`, executable.

### Unsupported/fallback rows
- BoxSphere -> no variant admits; fallback/reject path.
- TangentCylinder -> no variant admits; fallback/reject path.
- UnsupportedTransform -> no variant admits; fallback/reject path.
- NonCoaxialCounterboreOrCountersink -> no variant admits; fallback/reject path.

## Cross-steal policy
Each supported case is asserted to:
- select the expected variant, and
- produce rejection evidence from non-selected variants.

This guards against silent over-admission and variant stealing.

## Diagnostics hardening
`HoleRecoveryPolicy` diagnostics now include stable matrix-debug details:
- policy evaluated,
- variant count,
- per-variant considered/admissible lines,
- per-variant rejection reason lines,
- selected variant,
- produced plan kind,
- profile stack summary,
- fallback selection when no variant admits.

## Non-goals preserved
- no new hole recovery variants,
- no executor architecture changes,
- no STEP exporter behavior changes,
- no public CLI/API expansion,
- no generic boolean recovery.

## Recommended next family expansion
If/when expansion resumes, keep the same pattern:
1. bounded admissibility first,
2. explicit rejection diagnostics for all peer variants,
3. matrix row + cross-steal + manifold STEP sanity tests with each new variant.
