# Firmament V2 L3 arithmetic expression graph

L3 implements pure arithmetic expressions for Firmament V2 scalar `let` declarations. The feature remains a typed manufacturing-intent data layer: expressions are immutable, side-effect-free, acyclic, and evaluated during binding.

## Supported syntax

Scalar lets may use literals, top-level let references, dotted record-field references, parentheses, and binary `+`, `-`, `*`, `/` operators:

```firmament
let diameter: length = 6.0mm
let radius: length = diameter / 2
let drill: length = diameter + 0.25mm
let scaled: length = diameter * 1.25
let exported: length = MountingPattern.holeDiameter / 2
```

Record fields remain literal-bound in L3. Same-record field expression binding is intentionally deferred to avoid broadening record scope before field dependency ordering is designed.

## Type rules

| Operator | Allowed result families |
| --- | --- |
| `+`, `-` | `int` with `int` -> `int`; `float` with `float` -> `float`; `int`/`float` mix -> `float`; `length` with `length` -> `length`; `angle` with `angle` -> `angle` |
| `*` | numeric with numeric -> `int` or `float`; `length`/`angle` multiplied by `int` or `float` -> same dimensional type |
| `/` | numeric divided by numeric -> `float`; `length`/`angle` divided by numeric -> same dimensional type; `length / length` and `angle / angle` -> `float` |

Rejected combinations include string or bool arithmetic, dimensional addition with unitless values, cross-dimensional arithmetic, `length * length`, `angle * angle`, and assignment of a `float` expression to a dimensional or `int` declaration.

## Unit rules

L3 reuses existing literal unit normalization: `length` literals require `mm`, and `angle` literals require `deg`. Unitless numerics are not implicitly assigned to dimensional declarations, and dimensional division by the same dimension yields an explicit `float` ratio.

## Acyclic graph behavior

Top-level scalar lets are resolved through a full-document dependency graph, so forward references are accepted when acyclic. Cycles are rejected deterministically with `firmament-v2-expression-cycle`. Bound lets carry the evaluated value and a dependency set for downstream inspection.

## Diagnostics

L3 adds expression diagnostics for unknown symbols, unknown records or fields, record/scalar misuse, declared-type mismatch, invalid operators, division by zero, cycles, and unsupported expression syntax. Legacy L1/L2 let diagnostics are still emitted where callers already assert them.

## Explicit non-scope

L3 does not implement tolerance syntax, tolerance propagation, PMI/Forge wiring, functions, conditionals, loops, mutation, user-defined operators, arrays, string concatenation, recursion, or area/volume dimensional exponents.
