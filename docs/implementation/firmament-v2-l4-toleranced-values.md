# Firmament V2 L4 toleranced let values

V2-PHASE1-L4 adds narrow tolerance syntax to Firmament V2 `let` declarations while keeping Firmament pure data: immutable, typed, acyclic, and without functions, conditionals, loops, mutation, hidden state machines, PMI wiring, or tolerance arithmetic.

## Supported syntax

A tolerance may appear after the nominal value expression of a dimensional `let`:

```firmament
let holeDiameter: length = 6.0mm tol 0.05mm
let slotWidth: length = 12.0mm tol +0.10mm -0.05mm
let draftAngle: angle = 3deg tol 0.5deg
```

The same syntax is available on one-level record fields:

```firmament
let MountingPattern {
    holeDiameter: length = 6.0mm tol 0.05mm
    holeSpacingX: length = 80.0mm tol +0.10mm -0.05mm
    holeCount: int = 4
}
```

Tolerance remains declaration-level syntax. It is not valid inside arbitrary subexpressions such as `(6.0mm tol 0.05mm) + 1.0mm`.

## Forms and storage

`tol 0.05mm` is bilateral tolerance. It is stored as `FirmamentV2ToleranceKind.Bilateral` with positive magnitudes: `plus = 0.05`, `minus = 0.05`, `unit = mm`, and the dimensional primitive type.

`tol +0.10mm -0.05mm` is asymmetric tolerance. It is stored as `FirmamentV2ToleranceKind.Asymmetric` with positive magnitudes: `plus = 0.10`, `minus = 0.05`, `unit = mm`, and the dimensional primitive type. The source signs are syntax only.

Bound scalar lets and bound record fields expose the parsed tolerance through `FirmamentV2BoundLet.Tolerance`; expression binding also carries alias tolerance metadata so future consumers can identify toleranced values without PMI coupling.

## Type and unit rules

L4 supports tolerance only for dimensional primitive types:

- `length` with `mm` tolerances;
- `angle` with `deg` tolerances.

Tolerance on `int`, `float`, `string`, or `bool` is rejected with `firmament-v2-tolerance-invalid-type`. Unit mismatch is rejected with `firmament-v2-tolerance-unit-mismatch`.

## Alias and arithmetic behavior

Exact scalar aliases preserve tolerance:

```firmament
let holeDiameter: length = 6.0mm tol 0.05mm
let exportedHoleDiameter: length = holeDiameter
```

Exact dotted aliases also preserve tolerance:

```firmament
let MountingPattern { holeDiameter: length = 6.0mm tol 0.05mm }
let exportedHoleDiameter: length = MountingPattern.holeDiameter
```

Arithmetic remains nominal-only. If arithmetic uses a toleranced input and the result has no explicit tolerance, the result binds without tolerance and the parser emits the non-fatal diagnostic `firmament-v2-tolerance-dropped-through-arithmetic`:

```firmament
let holeDiameter: length = 6.0mm tol 0.05mm
let radius: length = holeDiameter / 2
```

If the arithmetic result has an explicit tolerance, that explicit tolerance is used and no propagation is inferred:

```firmament
let radius: length = holeDiameter / 2 tol 0.025mm
```

## Diagnostics

L4 adds deterministic tolerance diagnostics:

- `firmament-v2-tolerance-invalid-type`
- `firmament-v2-tolerance-unit-mismatch`
- `firmament-v2-tolerance-invalid-literal`
- `firmament-v2-tolerance-negative-bilateral`
- `firmament-v2-tolerance-missing-minus`
- `firmament-v2-tolerance-missing-plus`
- `firmament-v2-tolerance-dropped-through-arithmetic`
- `firmament-v2-tolerance-unsupported`

All malformed tolerance forms fail binding except `firmament-v2-tolerance-dropped-through-arithmetic`, which is intentionally non-fatal because the result is a valid nominal-only value.

## Explicit non-scope

L4 does not implement PMI wiring, automatic tolerance propagation, tolerance arithmetic, GD&T/full PMI controls, functions, conditionals, loops, mutation, or changes to STEP/AP242/analyze behavior.
