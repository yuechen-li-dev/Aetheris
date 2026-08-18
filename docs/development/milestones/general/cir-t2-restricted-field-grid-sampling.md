# CIR-T2: restricted-field grid sampling + cell classification

## Purpose

CIR-T2 adds deterministic grid sampling over a `SurfaceRestrictedField` and cell classification over the planar `[0,1] x [0,1]` parameter domain.

This milestone answers where sign changes happen and where cells are inside/outside/boundary/mixed/unknown, while deliberately not extracting contours.

## Scope

Supported now:

- source restricted fields with planar rectangular parameterization from T1,
- regular deterministic grid sampling,
- corner-based cell classification,
- count + diagnostics reporting.

Deferred:

- marching squares contour extraction (CIR-T3),
- adaptive 2D subdivision,
- non-rectangular/non-planar source domains,
- topology, export, materialization behavior changes.

## Sampling policy

For options `(ResolutionU, ResolutionV)`:

- require both resolutions `>= 2`,
- sample in row-major order (`j` outer, `i` inner),
- `u = i / (ResolutionU - 1)`,
- `v = j / (ResolutionV - 1)`,
- evaluate each corner through `SurfaceRestrictedField.Evaluate(u,v)`.

## Cell classification policy

Each cell uses 4 corners: `(i,j)`, `(i+1,j)`, `(i,j+1)`, `(i+1,j+1)`.

- any unknown/unavailable corner => `Unknown`,
- both inside + outside present => `Mixed`,
- any boundary corner without inside/outside disagreement => `Boundary`,
- all inside => `Inside`,
- all outside => `Outside`.

This is deterministic and designed for CIR-T3 contour extraction input.

## Diagnostics

Grid result diagnostics include:

- sampler start marker,
- resolution marker,
- explicit classification policy marker,
- contour extraction not implemented,
- export/materialization unchanged.

## Geometry evidence expectation

For box-face source against opposite cylinder/sphere/torus:

- sampling is deterministic,
- counts are deterministic for a fixed resolution,
- mixed/boundary cells indicate likely trim transitions.

## JudgmentEngine decision

Not used in CIR-T2. This milestone is deterministic transformation/classification with no competing strategy selection.

## SEM-A0 status

CIR-T2 introduces no generated topology naming/provenance changes. SEM-A0 guardrails remain preserved.

## Next (CIR-T3)

Consume T2 sample/cell data with deterministic marching squares contour extraction and contour diagnostics, still without changing export/materialization behavior.
