# AIR-FIRMAMENT-X2 — Firmament V2 parser Box `with` derivation

## 1. Purpose and scope

AIR-FIRMAMENT-X2 implements the first parser-backed Firmament V2 `with` derivation slice for Box solid records. The milestone proves the configuration mechanism:

```text
records + with = model configuration without spreadsheets
```

The implementation is intentionally narrow: one or more model-scope Box solid bindings may be parsed, and a later Box solid may derive from a previously defined Box solid with a `size` override. The derived record is resolved and revalidated before Feature AIR lowering. No topology mutation, BRep editing, backend patching, control flow, or general record derivation is introduced.

## 2. Relationship to X1

X1 introduced the isolated Firmament V2 frontend and parser-backed Box source path for `model`, `units mm`, `solid name: Box`, and `size: [x, y, z]`, lowering the parsed Box to a Feature AIR `CreateBox` trace summary. X2 keeps that X1 path and extends only the V2 frontend slice needed to resolve an immutable Box record derivation before lowering.

## 3. V2 parser organization / V1 legacy separation

The X2 parser and AST changes remain under `Aetheris.Kernel.Firmament/FirmamentV2/`. The legacy V1 parser remains on the `FirmamentTopLevelParser` path and is not expanded with V2 `with` syntax. Trace routing continues to use fixture metadata (`syntax-version: FirmamentV2`, `implementation: parser-backed`) to invoke `FirmamentV2Parser` directly.

## 4. Supported X2 grammar

Supported parser-backed shape:

```firmament
model BoxVariant {
    units mm

    solid base: Box {
        size: [10, 8, 6]
    }

    solid tall: base with {
        size: [10, 8, 12]
    }
}
```

The accepted X2 subset is:

- inherited `units mm`;
- direct Box solid records with `size: [number, number, number]`;
- derived Box solid records of the form `solid derived: base with { size: [...] }`;
- same-model name resolution against previously parsed solid records;
- positive numeric size components.

## 5. `with` semantics

A `with` solid target must resolve to a previously defined source-level Box solid record. The derived solid receives a new identity and a resolved Box record. For X2, the only supported override field is `size`; the override replaces the inherited size wholesale.

## 6. Source-level immutability and identity

The base solid remains unchanged. The derived solid is a new source-level record binding with its own name and `derivedFrom` evidence. X2 derivation happens before Feature AIR lowering and does not mutate topology or backend geometry.

## 7. Revalidation/admissibility

The derived Box is validated exactly like a directly authored Box:

- size arity must be three;
- every size component must be greater than zero.

If the derived record is invalid, parsing reports stable diagnostics and does not lower to Feature AIR.

## 8. Unsupported constructs

X2 does not implement general `with`. Unsupported constructs include nested `with`, selectors as derivation targets, feature records, field overrides other than `size`, undefined or forward base references, partial updates such as `size.x`, arbitrary expressions, non-numeric arrays, templates, concepts, PMI, `where`, materials, patterns, regions, shell, fillet, chamfer, profiles, ruled surfaces, and explicit per-literal units beyond the existing X1 subset.

## 9. Fixture changes

`fixtures/FirmamentV2/RecordDerivation/valid/box-with-size-variant-v2.valid.firmfixture` advanced from A2.2 metadata-only design intent to `implementation: parser-backed` with `expected-stage: feature-air`, `expected-feature-air: CreateBox`, `expected-solid: tall`, `expected-size: [10, 8, 12]`, and `expected-derived-from: base`.

Invalid parser-backed fixtures now cover degenerate derived size, selector-like override fields, unknown fields, undefined bases, and duplicate solid names under `fixtures/FirmamentV2/RecordDerivation/invalid/`.

## 10. Diagnostics

Stable X2 diagnostics include:

- `firmament-v2-name-unresolved`;
- `firmament-v2-duplicate-name`;
- `firmament-v2-with-requires-record`;
- `firmament-v2-with-requires-box-record`;
- `firmament-v2-with-field-not-found`;
- `firmament-v2-with-field-type-mismatch`;
- `firmament-v2-with-forward-reference`;
- `firmament-v2-with-derived-record-invalid`;
- `firmament-degenerate-dimension`.

Existing X1 diagnostics remain in use for missing model, missing units, missing solid, unknown record type, missing size, and size arity.

## 11. Tests run

Validated with targeted CLI traces and focused .NET test filters for Firmament V2 parser, fixtures, trace, primitive, region, side-hole, parser-backed, and invalid fixture behavior. The required commands and results are recorded in the implementation PR summary.

## 12. Next milestone recommendation

The next milestone should keep V2 isolated and add one narrow capability at a time. Recommended next step: explicit derived-record trace/diagnostic polish for forward-reference detection or a second Box-only `with` fixture shape, before any attempt at general `with`, selectors, templates, concepts, PMI, `where`, materials, or topology-affecting behavior.
