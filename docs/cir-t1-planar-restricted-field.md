# CIR-T1: planar rectangular surface-restricted field evaluator

## Purpose

CIR-T1 introduces the first executable trim-oracle substrate: evaluate an opposite CIR field over a **source planar rectangular parameter domain**.

Given source surface `S(u,v)` and opposite field `F(x,y,z)`, T1 provides deterministic point samples of `g(u,v)=F(S(u,v))`.

## Parameterization contract

T1 supports only source descriptors where:

- `Family = Planar`
- `BoundedPlanarGeometry.Kind = Rectangle`
- corners are available as `Corner00/Corner10/Corner01/Corner11`

The evaluator constructs:

- `S(u,v) = Corner00 + u*(Corner10-Corner00) + v*(Corner01-Corner00)`
- domain `u,v ∈ [0,1]`

It rejects non-planar, missing bounded geometry, non-rectangle bounded geometry, and degenerate U/V vectors.

## Subtract opposite operand selection

For `CirSubtractNode(left,right)`, T1 uses explicit source side:

- source side `Left` => opposite node `right`
- source side `Right` => opposite node `left`

Opposite node is lowered via `CirTapeLowerer.Lower` and sampled via `CirTape.Evaluate`.

## Evaluation behavior

`Evaluate(u,v)`:

1. checks domain membership and records a diagnostic if outside;
2. maps `(u,v)` to world point through planar rectangle parameterization;
3. evaluates opposite tape at that point;
4. classifies sign with tolerance:
   - negative = inside opposite
   - positive = outside opposite
   - near-zero = boundary

## Exactness/export/materialization policy

T1 is evaluation-only. It does not alter:

- topology emission,
- STEP export behavior,
- Boolean behavior,
- surface-family materialization readiness decisions.

SEM-A0 guardrails remain preserved (no generated topology naming changes).

## Non-goals

Not included in T1:

- contour extraction,
- marching squares or cell interval refinement,
- analytic snap,
- trim curve/BRep integration,
- CLI exposure changes.

## Recommended T2

Add restricted-domain grid/cell sampling over this evaluator, then a deterministic marching-squares contour scaffold that consumes sampled signs/values without changing exporter/materializer behavior.
