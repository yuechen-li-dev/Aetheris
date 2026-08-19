# STEP-V2-X4 — multi-feature composition AP242 verification

STEP-V2-X4 promotes the first bounded Firmament V2 multi-feature composition cases to AP242 `step-verified` evidence. The production path remains semantic source → `AirHoleFeature` lowering → bounded safe BRep composition → `Step242Exporter`; no hardcoded STEP templates, trace-only output, patterns, hole groups, or new hole semantics are introduced.

## Fixtures

Valid Tier 4 fixtures:

- `fixtures/Regression/Composite/valid/composite-v2-two-independent-holes-step-verified.valid.firmfixture`
- `fixtures/Regression/Composite/valid/composite-v2-adjacent-non-overlapping-holes-step-verified.valid.firmfixture`

Invalid Tier 4 fixture:

- `fixtures/Compatibility/LegacyAliases/Invalid/Composite/composite-v2-overlapping-holes-rejected-with-clear-diagnostic.invalid.firmfixture`

The optional derived-variant plus hole case was deferred from X4 and is promoted separately by STEP-V2-X5; see `docs/development/implementation/step-v2-x5-derived-variant-plus-hole-ap242.md`. X4 intentionally proved multi-hole composition on one selected Box first.

## Command path and verification

The integration tests execute the real CLI build path:

```bash
aetheris build <fixture> --out <temp.step> --json
```

For valid fixtures the emitted AP242 is checked for `ISO-10303-21`, `ADVANCED_FACE`, and `VERTEX_POINT`, reimported with `Step242Importer`, inspected for two cylindrical wall faces, and analyzed with exact volume analysis. This exercises `FirmamentBuildAndExport`, semantic `AirHoleFeature` lowering, `AirHoleCompositeMaterializer`, `BrepBooleanSafeCompositionGraphValidator`, `BrepBooleanBoxCylinderHoleBuilder`, and `Step242Exporter.ExportBody`.

## Volume formulas

Both valid fixtures use a `10 x 8 x 6` Box:

```text
base volume = 480
hole radius = 1
through depth = 6
expected volume = 480 - 2 * pi * 1^2 * 6 = 442.3008881569225
```

The adjacent fixture places centers 2.5 model units apart, so the two radius-1 holes are close but non-overlapping.

## Overlap rejection policy

The invalid fixture uses two same-face, same-axis, circular shaft holes whose centers are 1.5 model units apart with radius 1. That is narrower than `r1 + r2`, so the build is rejected before successful AP242 emission. The stable diagnostic prefix is:

```text
firmament-v2-semantic-hole-overlap
```

This is deliberately narrow: same planar entry face, same world-Z axis/direction family, same through-all end condition family, circular shaft holes. The implementation does not resolve, merge, or reroute overlaps and does not attempt general Boolean conflict resolution.

## MVP readiness relationship

These fixtures satisfy the MVP readiness contract for this bounded multi-feature area because `current-stage: step-verified` is claimed only where real AP242 is emitted from a real `BrepBody`, reimported, topology/evidence checked, and volume verified. The rejected fixture is marked deterministic rejection and must not produce misleading success AP242.

## Deferred

- General feature conflict resolution.
- Patterns, hole groups, new hole variants, threads/taps, up-to-face/up-to-next, side-hole reroute, chamfer/fillet/draft, PMI, and DFM enforcement.
- Derived variant plus hole AP242 verification was promoted by STEP-V2-X5; remaining deferred items stay limited to broader semantics and unsupported feature families.
