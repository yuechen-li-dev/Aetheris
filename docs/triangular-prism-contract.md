# `triangular_prism` contract (P2 hardening)

This document makes the current `triangular_prism` primitive explicit so future work does not need to infer its geometry.

## Primitive shape

`triangular_prism` is a **centered isosceles triangular prism**, not a right-triangular prism.

Given:
- `base_width = w`
- `base_depth = d`
- `height = h`

the local 2D profile in XY is exactly:
1. `(-w/2, -d/2)`
2. `( w/2, -d/2)`
3. `( 0,    d/2)`

Extrusion is along local `+Z` for distance `h`.

## Local frame / origin semantics

Kernel primitive construction produces a legacy body centered on `Z` in `[-h/2, +h/2]`.

Firmament primitive publishing applies the standard prism default-frame translation of `+h/2` in `Z`, so published primitive coordinates become `Z ∈ [0, h]` before any user `placement` offset/selector transform.

## Bounding box contract

For the published primitive (before extra placement offsets), expected axis-aligned bounds are:

- `X ∈ [-w/2, +w/2]`
- `Y ∈ [-d/2, +d/2]`
- `Z ∈ [0, h]`

Example (`w=20, d=12, h=10`) yields bbox min `(-10, -6, 0)` max `(10, 6, 10)`.

## Canonical example

- `testdata/firmament/examples/triangular_prism_basic.firmament`

This example is used by tests that assert both topology counts and analyze JSON bounding-box facts.

## Scope boundary

This clarification pass does **not** introduce arbitrary triangle profiles, prism DSL changes, or generalized prism-family redesign.
