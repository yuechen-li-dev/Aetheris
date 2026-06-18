# AIR-FIRMAMENT-X1 — Tiny Firmament V2 parser for Box

## Purpose and scope

AIR-FIRMAMENT-X1 implements the first parser-backed Firmament V2 slice: `model`, inherited `units`, one typed `solid` binding, the `Box` record, and a `size` field with three positive numeric components. The milestone intentionally stops at Feature AIR `CreateBox` trace evidence; it does not add BRep, STEP, topology, Boolean, shell, fillet, chamfer, surfacing, pattern, material, FEA, or PMI behavior.

## V2 parser organization

The V2 frontend is isolated under `Aetheris.Kernel.Firmament/FirmamentV2/`. `FirmamentV2Parser` and `FirmamentV2Ast` are separate from the legacy `Parsing/FirmamentTopLevelParser` path. Trace dispatch uses fixture metadata (`syntax-version: FirmamentV2`, `implementation: parser-backed`) to invoke the V2 parser directly.

## V1 legacy separation policy

Firmament V1 remains the existing TOON/YAML-style compatibility surface in `fixtures/Firmament/` and continues through `FirmamentTopLevelParser`. X1 does not expand V1 syntax, migrate the V1 corpus, or treat V2 as a V1 compatibility mode. Unsupported V2 design fixtures from A2/A2.1/A2.2/A2.3 remain metadata-classified unless explicitly promoted.

## Supported X1 grammar

Canonical X1 syntax is typed-record style:

```firmament
model BoxExample {
    units mm

    solid base: Box {
        size: [10, 8, 6]
    }
}
```

Supported constructs are limited to:

- `model Name { ... }`;
- `units mm`;
- `solid name: Box { size: [x, y, z] }`;
- numeric size literals;
- inherited model units;
- one solid binding.

## Unsupported constructs

X1 rejects or leaves metadata-only all broader V2 constructs, including `with`, `=>`, `template<Process>`, `concept`, `PMI`, `where`, selectors, regions, cut/add, shell, fillet, chamfer, ruled surfaces, profiles, materials, patterns, multiple solids, expressions, arbitrary units, and parser recovery beyond basic diagnostics.

## Lowering path

The implemented path is:

```text
FirmamentV2 source
  -> FirmamentV2Ast
  -> FirmamentFrontendTraceProbe.ParseV2Only
  -> Feature AIR CreateBox trace summary
```

X1 intentionally stops at Feature AIR for V2. Existing V1 box trace behavior still reaches its established profile-emission evidence separately.

## Fixture changes

`fixtures/FirmamentV2/Primitive/valid/box-v2.valid.firmfixture` is promoted from metadata-only `not-implemented` design intent to `implementation: parser-backed`, `expected-stage: feature-air`, `expected-feature-air: CreateBox`, `dimensions: 10, 8, 6`, and `units: mm`.

New invalid parser-backed pilot fixtures cover missing units, negative size, wrong size arity, and unknown record type under `fixtures/FirmamentV2/Primitive/invalid/`.

## Diagnostics

Stable X1 diagnostics include:

- `firmament-v2-missing-model`;
- `firmament-v2-missing-units`;
- `firmament-v2-missing-solid`;
- `firmament-v2-unsupported-construct`;
- `firmament-v2-unknown-record-type`;
- `firmament-v2-box-missing-size`;
- `firmament-v2-box-size-arity`;
- `firmament-degenerate-dimension`.

## Tests run

Validation for this milestone includes CLI build/help, V2 text and JSON traces, one invalid V2 trace, V1 box and side-hole trace smoke, and focused CLI/kernel/friction-lab filtered test suites. See the PR summary for exact commands and results.

## Next milestone recommendation

Recommended AIR-FIRMAMENT-X2: add a small token/lexer layer and semantic-name checks for V2 while preserving the X1 Feature AIR boundary. Do not broaden into regions, selectors, record derivation, or geometry materialization until the tiny parser contract remains stable across the fixture corpus.


## X2 follow-on note

AIR-FIRMAMENT-X2 builds on this isolated V2 frontend by adding only Box `with` record derivation: a derived Box may override `size`, be revalidated, and lower to Feature AIR `CreateBox`. The V1 parser remains untouched and the V2 frontend remains under the `FirmamentV2` namespace.

## X3 exposure extension

The X1 Box record path remains the base V2 parser route. AIR-FIRMAMENT-X3 layers Box-only semantic exposure metadata on that route without changing `CreateBox` dimensions or geometry behavior.
