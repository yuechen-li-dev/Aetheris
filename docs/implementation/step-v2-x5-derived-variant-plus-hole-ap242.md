# STEP-V2-X5 — derived variant plus semantic hole AP242 verification

STEP-V2-X5 closes the deferred Tier 4 composition case `composite-v2-hole-plus-derived-variant-step-verified`. It proves that a Firmament V2 `with`-derived selected Box can consume an existing semantic `hole<shaft>` and reach AP242 `step-verified` through the real build/export path.

## Fixture

- `fixtures/FirmamentV2/Composite/valid/composite-v2-hole-plus-derived-variant-step-verified.valid.firmfixture`

The source composition is intentionally narrow:

1. `solid base: Box { size: [10, 8, 6] }`
2. `solid wider: base with { size: [12, 8, 6] }`
3. `modify wider { hole<shaft> mount { on: face(+Z); center: [0, 0]; radius: 1; end: throughAll } }`

No new hole families, parser syntax, pattern/group semantics, side-hole reroute, or exporter behavior are introduced.

## Command path

The verification path is the real CLI path:

```bash
aetheris build fixtures/FirmamentV2/Composite/valid/composite-v2-hole-plus-derived-variant-step-verified.valid.firmfixture --out <temp>/composite-v2-hole-plus-derived-variant.step --json
```

The implementation path is:

```text
FirmamentV2Parser
  -> selected with-derived Box record (`wider`, 12 x 8 x 6)
  -> semantic hole<shaft> on `modify wider`
  -> FirmamentV2SemanticHoleLowering
  -> AirHoleFeature(SimpleShaft)
  -> AirHoleSimpleShaftMaterializer
  -> real BrepBody
  -> Step242Exporter.ExportBody
```

The output is not a hardcoded STEP template and is not trace-only output.

## AP242 verification checks

The integration test requires all of the following before the fixture can honestly claim `current-stage: step-verified`:

- the real `aetheris build` path succeeds;
- an AP242 file containing `ISO-10303-21` is emitted;
- `ADVANCED_FACE` count is greater than zero;
- `VERTEX_POINT` count is greater than zero;
- trace-only markers are absent;
- `Step242Importer` reimports the emitted file;
- topology evidence includes one cylindrical wall face for the shaft;
- exact volume analysis succeeds with method `analytic-box-minus-z-hole`.

## Volume formula and stale-state guard

The independent expected volume is based on the derived selected solid, not the base solid:

```text
derived box volume = 12 * 8 * 6 = 576
shaft removed volume = pi * 1^2 * 6 = 6pi
expected volume = 576 - 6pi = 557.1504440784612
```

The test also computes the stale-base alternative:

```text
10 * 8 * 6 - 6pi
```

and asserts the analyzed AP242 volume is not that value. This catches regressions where the semantic hole materializer accidentally uses the base `10 x 8 x 6` record after the fixture selected `wider`.

## Relationship to MVP readiness, X3, and X4

STEP-V2-X3 proved `with` derivation, chained derivation, and semantic face aliases. STEP-V2-X4 proved bounded multi-hole composition and deterministic overlap rejection. X5 composes the already-supported X3 derivation selection with the already-supported X2/X4 semantic shaft hole path, satisfying the remaining Tier 4 derived-variant-plus-hole MVP checklist item.

## Deferred

Still deferred: new hole semantics, counterbore/countersink expansion beyond existing paths, hole groups/patterns, threads/taps, drill tips, up-to-face/up-to-next, arbitrary datum placement, non-planar entry faces, side-hole reroute, chamfer/fillet/draft, PMI, DFM enforcement, generic 3D Boolean authoring, generic conflict resolution, new exporter behavior, or a V2-only exporter.
