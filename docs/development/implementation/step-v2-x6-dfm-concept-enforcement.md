# STEP-V2-X6 — minimal DFM/concept build enforcement

STEP-V2-X6 promotes a narrow Tier 5 slice of Firmament V2 template/concept metadata from parse/storage evidence to real build-time rejection. This is not a general DFM framework and does not execute Forge packages or plugins.

## Fixtures

Added build-verified invalid fixtures:

- `fixtures/Templates/invalid/template-v2-cnc-min-tool-radius-enforced.invalid.firmfixture`
- `fixtures/Templates/invalid/template-v2-concept-unit-mismatch-rejected-at-build.invalid.firmfixture`

Both fixtures use the real `aetheris build` path and are classified as Tier 5 deterministic rejections.

## Supported enforcement scope

The only enforced process/template shape in this milestone is:

- `template<CNC> <Name> { concept minimumToolRadius: <number> <unit> }`

The only feature family checked by the rule is the already-supported Firmament V2 semantic hole family inside `modify` blocks:

- `hole<shaft>` shaft radius;
- `hole<counterbore>` shaft and counterbore radii;
- `hole<countersink>` shaft and countersink entry radii.

The source document must still use the existing model-level `units mm` path. The minimum tool radius concept must use the same length unit as the model (`mm` today). A concept unit such as `deg` is parsed as template metadata, then rejected by build enforcement.

## Diagnostics

Stable diagnostics introduced for the enforced MVP slice:

- `firmament-v2-dfm-minimum-tool-radius-violation`
- `firmament-v2-dfm-concept-unit-mismatch`

The diagnostic messages include the template name, concept name, expected/actual unit or minimum value, and the offending semantic hole radius when applicable.

## Real command path

The rejection path is:

```text
Firmament V2 fixture
  -> FirmamentV2Parser.Parse
  -> FirmamentV2DfmEnforcement.Validate
  -> FirmamentBuildAndExport.ExportSource
  -> CLI build failure before STEP/AP242 write
```

Direct CLI probes:

```bash
dotnet run --project Aetheris.CLI -- build fixtures/Templates/invalid/template-v2-cnc-min-tool-radius-enforced.invalid.firmfixture --out "$tmp/template-v2-cnc-min-tool-radius.step" --json
```

```bash
dotnet run --project Aetheris.CLI -- build fixtures/Templates/invalid/template-v2-concept-unit-mismatch-rejected-at-build.invalid.firmfixture --out "$tmp/template-v2-concept-unit-mismatch.step" --json
```

Both commands are expected to return a failing build result and must not leave a successful AP242 output file.

## What remains metadata-only

All other template/process/concept shapes remain metadata-only or fixture-doctrine examples, including non-CNC processes, `minimumWallThickness`, `minimumHoleDiameter`, cost/process selection, materials, standards tables, and PMI lowering.

## Relationship to the MVP contract

This closes the Tier 5 MVP contract items:

- `template-v2-cnc-min-tool-radius-enforced`
- `template-v2-concept-unit-mismatch-rejected-at-build`

The implementation deliberately proves only that parsed Firmament V2 DFM/concept metadata can block the real build path with deterministic diagnostics.

## Relationship to FORGE-X1/X2

FORGE-X1/X2 provide descriptor scaffolding and the Aetheris.Standard concept pack direction. STEP-V2-X6 does not load descriptors dynamically, execute Forge plugins, or read external package standards. The current enforcement is built into the Firmament V2 build path as an MVP bridge until a fuller Forge runtime exists.

## Deferred capabilities

Deferred on purpose:

- dynamic Forge package loading;
- plugin execution;
- standards/fit/thread/tap tables;
- broad CNC manufacturability analysis;
- process strategy scoring or cost modeling;
- new primitive, hole, pattern, PMI, STEP, DisplayIR, frontend, or product behavior.
