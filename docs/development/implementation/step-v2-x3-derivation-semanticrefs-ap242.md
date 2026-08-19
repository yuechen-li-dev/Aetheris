# STEP-V2-X3 — derivation and semantic references AP242 verification

STEP-V2-X3 promotes Tier 3 Firmament V2 source-composition fixtures from parser/source intent to AP242 `step-verified` evidence through the production build path.

## Fixture paths

- `fixtures/Regression/RecordDerivation/valid/derivation-v2-with-size-override-step-verified.valid.firmfixture`
- `fixtures/Regression/RecordDerivation/valid/derivation-v2-with-chained-twice-step-verified.valid.firmfixture`
- `fixtures/Regression/SemanticRefs/valid/semanticref-v2-expose-face-alias-resolves-in-step.valid.firmfixture`

The repository's existing V2 derivation convention is `RecordDerivation`, so X3 keeps the new derivation fixtures there instead of creating a parallel `Derivation` tree.

## Command path

Each non-blocked fixture is verified by the real CLI path:

```bash
aetheris build <fixture> --out <path> --json
```

In local validation this is run via:

```bash
dotnet run --project Aetheris.CLI -- build <fixture> --out <path> --json
```

The build path parses Firmament V2, lowers through the production bridge/materializer, exports with `Step242Exporter.ExportBody`, and writes real AP242. No hardcoded STEP templates or trace-only output are used.

## Verified source composition features

### `with` size override

The size override fixture derives `wider` from `base` and changes only `size` from `10 x 8 x 6` to `12 x 8 x 6`. The final emitted solid is the derived record selected by the normal V2 document convention (`Document.Solid` is the last solid). Expected volume:

```text
12 * 8 * 6 = 576 mm^3
```

Expected topology after AP242 reimport is the canonical single box: 6 faces, 8 vertices, and 12 edges.

### chained `with` twice

The chained fixture derives `wider` from `base`, then `taller` from `wider`, proving the second derivation uses the immediate prior normalized record and does not accumulate stale dimensions. Expected final dimensions and volume:

```text
12 * 8 * 7 = 672 mm^3
```

Expected topology after AP242 reimport is again the canonical single box: 6 faces, 8 vertices, and 12 edges.

### semantic face alias consumed by a semantic hole

The semantic reference fixture exposes `face(+Z) => top`, then uses `on: top` in a later `hole<shaft>` feature. Parser/lowering tests assert the semantic hole entry face source is the alias `top`, while the resolved selector is `face(+Z)`. STEP verification then checks the same production AP242 evidence as the direct-face shaft-hole fixture: topology markers, successful reimport, cylindrical wall evidence, and analytic volume:

```text
480 - pi * 1^2 * 6 mm^3
```

## AP242 verification checks

The X3 integration tests assert:

- the real `build` command accepts the fixture;
- the output `.step` file exists;
- the file contains `ISO-10303-21`, `ADVANCED_FACE`, and `VERTEX_POINT` markers;
- trace-only markers such as `trace` and fixture-control text are absent;
- `Step242Importer.ImportBody` succeeds;
- derivation fixtures reimport as canonical box topology (6 faces / 8 vertices / 12 edges);
- semantic alias fixture reimports with cylindrical wall evidence;
- `StepAnalyzer.AnalyzeVolume` returns exact analytic volumes matching the hand-computed formulas.

## MVP readiness relationship

These fixtures satisfy the MVP readiness contract for Tier 3 V2 source composition because `current-stage: step-verified` is only claimed where the real production build/export path emits AP242, reimports it, and passes topology/semantic and volume evidence checks.

## Deferred item: edge-loop alias

Edge-loop alias remains deferred for X3. The parser can represent an exposure such as `face(+Z).outerLoop => topRim`, but there is no production operation in this milestone scope that consumes an edge-loop alias into AP242. Therefore no edge-loop alias fixture is marked `step-verified` here.
