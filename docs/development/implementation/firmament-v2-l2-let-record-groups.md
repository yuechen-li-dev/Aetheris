# Firmament V2 L2 grouped `let` records

Milestone: **V2-PHASE1-L2**

L2 implements the next narrow Firmament V2 language slice after primitive typed `let` declarations. The implementation remains a pure manufacturing-intent data feature and does not broaden Phase 1 into CAD modeling or executable scripting.

## Supported syntax

Top-level grouped `let` records are accepted with one level of primitive typed fields:

```firmament
let MountingPattern {
    holeDiameter: length = 6.0mm
    holeSpacingX: length = 80.0mm
    holeSpacingY: length = 40.0mm
    holeCount: int = 4
    label: string = "M6 mount group"
    inspectionRequired: bool = true
}
```

Supported field types are the L1 primitives: `int`, `float`, `length`, `angle`, `string`, and `bool`.

## Model and binding

The parser exposes record declarations as `FirmamentV2LetRecordDeclaration` values containing `FirmamentV2LetRecordField` entries. The bound document exposes `FirmamentV2BoundLetRecord` values whose fields are bound `FirmamentV2BoundLet` entries keyed by field name.

Scalar L1 lets remain exposed through `Lets` and `BoundLets`; L2 records are exposed through `LetRecords` and `BoundLetRecords`.

## Dotted references

A scalar `let` value may be exactly a dotted reference to a grouped-let field:

```firmament
let MountingPattern {
    holeDiameter: length = 6.0mm
}

let exportedHoleDiameter: length = MountingPattern.holeDiameter
```

The reference resolves to the referenced field's already-bound primitive value. The scalar declaration's type must match the referenced field type. L2 does not add arithmetic or reference chaining; this is only the minimal binder path needed to establish record-field references for later milestones.

## Deterministic diagnostics

L2 adds deterministic diagnostics for grouped-let and dotted-reference failures:

- `firmament-v2-let-record-duplicate-name`
- `firmament-v2-let-record-duplicate-field`
- `firmament-v2-let-reference-unknown-record`
- `firmament-v2-let-reference-unknown-field`
- `firmament-v2-let-reference-non-record`
- `firmament-v2-let-reference-record-used-as-value`

Existing L1 diagnostics continue to apply for unknown primitive types, primitive type mismatches, invalid literals, unit mismatches, duplicate scalar lets, and non-literal arithmetic attempts.

## Explicit non-scope

L2 deliberately does not implement:

- arithmetic expressions;
- tolerance syntax;
- nested records;
- top-level dotted field assignment sugar such as `let Group.field: length = 1.0mm`;
- PMI or Forge wiring for records;
- conditionals, loops, user functions, mutation, hidden behavior, or any Turing-complete language feature.
