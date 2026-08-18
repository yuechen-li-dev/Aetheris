# Firmament V2 L1 `let` primitive literals

Milestone: **V2-PHASE1-L1**

Firmament V2 L1 supports immutable top-level primitive `let` declarations as typed manufacturing-intent data. These declarations are parsed into the V2 document and bound into a semantic let list for later milestones to reference.

## Supported syntax

```firmament
let name: type = literal
```

Supported primitive type names are lowercase:

- `int`
- `float`
- `length`
- `angle`
- `string`
- `bool`

## Literal rules

- `int`: integer literal only, with no decimal point and no unit.
- `float`: numeric literal with no unit; integer-looking values are allowed.
- `length`: numeric literal with the currently supported length unit, `mm`.
- `angle`: numeric literal with the currently supported angle unit, `deg`.
- `string`: quoted literal.
- `bool`: `true` or `false`.

The bound value preserves the declared primitive type, the canonical value, and the unit where applicable.

## Diagnostics

L1 let validation reports deterministic diagnostics for unsupported or invalid input:

- `firmament-v2-let-duplicate-name`
- `firmament-v2-let-unknown-type`
- `firmament-v2-let-type-mismatch`
- `firmament-v2-let-invalid-literal`
- `firmament-v2-let-unit-mismatch`
- `firmament-v2-let-literal-only`

## Explicit non-scope

This milestone intentionally does **not** implement:

- records or grouped `let` blocks;
- references between `let` declarations;
- arithmetic or expression graphs;
- tolerance syntax;
- conditionals, loops, user functions, mutation, hidden behavior, or logic.

`let` declarations are not wired into STEP/AP242 behavior, PMI output, Forge descriptors, demo packets, or modeling behavior in L1.
