# AETHERIS-CONTINUUM-M1: fixed-lattice boundary offset maps and MSAA geometry sampling

## Outcome

Yes. On the fixed `16x16x4` cylindrical-hole lattice, the best M1 strategy reduces volume error from the M0 medium result of `0.235506%` to `0.018660%` and estimates cylindrical-wall area within `0.025611%`. It also beats the M0 `32x32x8` volume error of `0.083611%` while retaining one eighth as many volume cells (`1,024` versus `8,192`). The gain comes from local Cut-cell boundary work; no lattice cell is subdivided and no AMR exists.

M1 is continuum boundary discretization infrastructure. It does not implement FEA, solver quadrature, stiffness integration, tiny-cut-cell stabilization, or any physics solver.

## M0 cylindrical-hole audit

The exact fixture is a `4 x 4 x 1` axis-aligned block, `[-2,2] x [-2,2] x [-0.5,0.5]`, minus a radius-1 through cylinder centered on the Z axis. `BlockWithCylindricalHoleRegion` is the analytic CIR occupancy authority.

- M0 medium lattice: `16x16x4`, `1,024` regular cubic cells of side `0.25`.
- Exact bounds classification: `784 Inside`, `128 Outside`, `112 Cut`.
- M0 geometry plan: 4 deterministic subcell-center locations on each axis, or 64 point classifications per Cut cell and `7,168` total geometry samples.
- Occupancy estimator: `(inside samples + 0.5 * boundary samples) / 64`; total volume is full Inside-cell volume plus Cut-cell coverage times cell volume.
- Exact volume: `16 - pi = 12.858407346410207`.
- Exact cylindrical wall area: `2*pi*r*h = 2*pi = 6.283185307179586`.
- Existing exact total block-with-hole boundary area: `48`; M1 wall-area comparisons intentionally use only the curved cylindrical wall.
- Exact material-side cylinder normal: `(x-cx, y-cy, 0) / r`. The outward normal of the material domain on the hole wall is its negative; M1 names and stores the material-side convention explicitly.

The M0 volume error is not a cell-classification error. Bounds classification correctly localizes the exact intersected cells. Error arises inside Cut cells because binary point classifications are reduced to a 64-level count with no proximity, reconstructed arc, or local support. The fixed midpoint layout can alias a curved cut, boundary hits only receive a fixed half weight, and identical XY evidence is recomputed in every Z layer. M0 also had no boundary-area or position/normal-fidelity estimator. More samples help, but representation, placement, and the estimator are distinct sources of error.

## Fixed primary lattice and authority

All main M1 rows use the same `16x16x4` lattice and the same `784/128/112` Inside/Outside/Cut classification. The authority split is:

- analytic CIR (and eventually BRep): exact/source geometry;
- `BoundaryOffsetMap`: derived, validated local approximation;
- `GeometrySamplePlan`: deterministic geometry-only occupancy evidence;
- lattice: unchanged regular volume carrier.

`CutCell` can carry derived `IBoundaryOffsetMap` instances, but a map never replaces its `BoundaryReference` or exact CIR authority. The analytic fixture attaches the stable semantic reference `m1-cylindrical-hole:cylindrical-hole` / `cylindrical-hole-wall`. A fabricated BRep face id was deliberately not added.

## BoundaryOffsetMap V1

V1 records the `CellIndex`, source `BoundaryReference`, orthonormal local frame, rectangular `(u,v)` domain, sampled offsets and normals, interpolation method, resolution, independent validation counts/errors, and policy acceptance.

For a cylinder Cut cell, the exact support constructs a deterministic frame:

- `P0`: projection of the cell center to the exact cylinder at the cell-center Z;
- `N`: radial material-side unit normal at `P0`;
- `U = (-Ny, Nx, 0)`: circumferential tangent;
- `V = (0,0,1)`: exact cylinder axis.

The local graph is

```text
P(u,v) = P0 + u U + v V + h(u,v) N
h(u,v) = sqrt(r^2 - u^2) - r
```

The map domain is the projection of the cell's XY corners onto `U` plus its Z interval on `V`. V1 samples this exact support at uniform `2x2`, `4x4`, or `8x8` map nodes. It then uses bilinear offset interpolation and linear normal interpolation followed by renormalization. It stores no triangles and requires no closed-form offset contract from future backends.

Construction validates nonempty boundary identity, finite coordinates/offsets/normals, an ordered domain, orthonormal frame orientation, unit normals, matching sample dimensions, and finite bounded error metadata. Independent half-stride validation locations never coincide with the map's own nodes.

The M1 fidelity policy accepts a map when maximum positional error is at most `0.0015` and maximum angular normal error is at most `0.25 degrees`, with resolution capped at 8. On the cylinder, 32 maps pass at `4x4`; 80 fail position fidelity and selectively rebuild at `8x8`.

## Geometry sample strategies and estimators

The implemented matrix is:

1. `m0-baseline-4x4x4`: the unchanged M0 subcell count.
2. `regular-2x2x2`: regular quarter-point samples.
3. `regular-4x4x4`: regular eighth-offset subcell centers (identical to M0 for this fixture, retained as an explicit standardized row).
4. `offset-map-2x2`: piecewise-linear local graph from a 2x2 map.
5. `offset-map-4x4`: piecewise-linear local graph from a 4x4 map.
6. `offset-map-selective-msaa`: 4x4 maps everywhere, selective 8x8 maps, and a nested geometry-sample confidence pass.

Regular strategies estimate occupancy by explicit point count. Their first wall-area estimator constructs the deterministic binary sample field and measures its marching-squares contour, extruded through the fixture height. It is deliberately distinct from occupancy and has visible error.

Map strategies transform the XY cell rectangle into local `(u,N)` coordinates. Each adjacent map-node pair defines a linear boundary segment. Deterministic half-plane clipping integrates the material-side polygon area for occupancy; clipped segment length times cell height estimates wall area. This is a map-assisted integration rule, not a hidden correction factor and not solver quadrature. Segment first moments and length-weighted normals also produce boundary centroid and normal aggregates.

## Hierarchy, selective trigger, and caches

The Oct M11 code used deterministic quarter-point `2x2` coverage. M12 used the local trigger `0 < coarseCoverage < 1` and recomputed regular `4x4` only on that band; M15 demonstrated that fractional coverage and signed-distance proximity/normals are related but not interchangeable.

Aetheris reuses those ideas but changes one important limitation: Oct's `2x2` and `4x4` coordinates were disjoint, so Oct did not reuse samples. M1 keeps regular 2/4 rows for direct comparison and adds a nested selective hierarchy:

```text
base axis coordinates:       [3/8, 5/8]
regular 4x4 axis coordinates:[1/8, 3/8, 5/8, 7/8]
```

Thus all 8 base 3D samples are byte-identical members of the 64-sample refinement. The selective trigger upgrades a Cut cell when its map fails the explicit fidelity policy, or when nested-base occupancy is mixed and differs from map occupancy by at least `0.02`. It upgrades 80/112 cylinder Cut cells and 0/64 plane Cut cells. It never changes lattice topology.

Stable point keys use the IEEE-754 coordinate bits. Boundary-support keys use source id, radial frame bits, and local `u`; cylinder evaluations therefore reuse identical values across V nodes and Z-layer cells. In the best cylinder row:

- geometry cache hit rate: `10.6383%` (640 hierarchy hits among 6,016 requested geometry samples);
- boundary-map construction cache hit rate: `96.6435%`;
- all sample/evaluation reuse: `7,320 / 45,120 = 16.2234%`.

The raw count includes `32,192` independent validation points, `6,912` map nodes, and `6,016` geometry samples. Validation dominates because this milestone proves fidelity independently; production policy can later reduce validation cost without changing the measured approximation.

## Fixed-lattice cylindrical-hole benchmark

All runtimes are means of 20 Release runs after 5 warmups on the validation machine. `Exact queries` includes independent analytic validation queries. Dashes mean that a point-count strategy does not claim a continuous position/normal map.

| Strategy | Cells | Cut | Map nodes | Validation | Geometry samples | Raw | Exact queries | Reuse | Volume error | Wall-area error | Max / RMS position | Max / RMS normal | Total ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| regular 2x2x2 | 1,024 | 112 | 0 | 0 | 896 | 896 | 896 | 0% | 0.843085% | 6.347023% | - | - | 1.463 |
| M0 baseline 4x4x4 | 1,024 | 112 | 0 | 0 | 7,168 | 7,168 | 7,168 | 0% | 0.235506% | 5.181638% | - | - | 2.569 |
| regular 4x4x4 | 1,024 | 112 | 0 | 0 | 7,168 | 7,168 | 7,168 | 0% | 0.235506% | 5.181638% | - | - | 2.569 |
| offset map 2x2 | 1,024 | 112 | 448 | 2,800 | 0 | 3,248 | 2,856 | 12.069% | 0.515843% | 0.937715% | 0.015749 / 0.009921 | 0.054383° / 0.036784° | 2.032 |
| offset map 4x4 | 1,024 | 112 | 1,792 | 9,072 | 0 | 10,864 | 9,184 | 15.464% | 0.046930% | 0.233887% | 0.001775 / 0.001114 | 0.012004° / 0.006038° | 2.536 |
| offset map + selective MSAA | 1,024 | 112 | 6,912 | 32,192 | 6,016 | 45,120 | 37,800 | 16.223% | **0.018660%** | **0.025611%** | 0.001127 / 0.000335 | 0.006093° / 0.001695° | 6.274 |

The global cylindrical-wall centroid residual is `1.07e-17`, consistent with the exact origin. The global area-weighted normal is zero by cylindrical symmetry; per-cell diagnostics retain nonzero local aggregate evidence.

### Per-stage timings for the best row

| Stage | ms |
| --- | ---: |
| cell classification | 0.621 |
| boundary-map construction and validation | 2.949 |
| geometry sampling/cache | 1.090 |
| aggregation | 1.614 |
| end-to-end measured path | 6.274 |

`Total` is measured directly and includes classification; small differences from the stage sum are timer/rounding overhead.

## Oblique-plane control

The `8x8x8` plane fixture has 512 cells and 64 Cut cells. Its map uses the exact plane projection, material-side normal, a deterministic in-plane `U`, Z-axis `V`, and `h=0`.

| Strategy | Geometry/map/validation requests | Volume error | Boundary-area error | Max position | Max normal | Refined cells |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| regular 2x2x2 | 512 | 0 | 9.375% | - | - | 0 |
| regular 4x4x4 | 4,096 | 0 | 4.6875% | - | - | 0 |
| offset map 2x2 | 1,856 | 0 | `1.26e-13%` | 0 | 0° | 0 |
| offset map + selective MSAA | 6,720 | 0 | `1.26e-13%` | 0 | `1.21e-6°` floating-point maximum | 0 |

The map adds no geometric error and the policy requests no denser representation. The small selective normal angle is the `acos` sensitivity of a renormalized dot product at machine precision, not plane curvature.

## Fixed medium versus M0 refinement

| Result | Grid | Cells | Cut | Geometry samples | Volume error | Wall-area error |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| M0 coarse | 8x8x2 | 128 | 24 | 1,536 | 0.843085% | not implemented |
| M0 medium | 16x16x4 | 1,024 | 112 | 7,168 | 0.235506% | not implemented |
| M0 fine | 32x32x8 | 8,192 | 480 | 30,720 | 0.083611% | not implemented |
| M1 best, fixed medium | 16x16x4 | 1,024 | 112 | 6,016 geometry + 6,912 map | **0.018660%** | **0.025611%** |

M1 uses 12.5% of the fine lattice cells and reduces fine-grid volume error by 77.68%. Its warmed end-to-end mean is 6.27 ms; the checked M0 fine result was approximately 13.01 ms, though the historical timing should be treated as contextual rather than a controlled cross-build speedup claim.

Cost normalization remains plural rather than collapsed into a score:

- absolute volume error per 1,000 cells: `0.002343` (M1 best) versus `0.001312` (M0 fine); this normalization penalizes the smaller carrier and is not independently a quality score;
- absolute volume error per exact query: `6.35e-8` (M1 best, validation included) versus `3.50e-7` (M0 fine point queries);
- absolute wall-area error per 1,000 cells: `0.001571` for M1 best;
- runtime per Cut cell: `0.0560 ms` for M1 best;
- reuse: `16.223%` overall, `96.644%` for boundary-map construction, and `10.638%` for geometry hierarchy queries.

## Determinism and evidence

The generator runs 5 warmups and 20 measured passes. Cell classification, frames, map nodes, hierarchy coordinates, policy decisions, cache keys, estimators, per-cell metrics, and JSON ordering are deterministic. Runtime is explicitly excluded from the deterministic projection. All 20 projections were byte-identical; SHA-256 is recorded in `deterministic-hashes.json`.

Artifacts under `docs/development/milestones/continuum/artifacts/m1/` are generated by:

```powershell
dotnet run --project tools/Aetheris.Continuum.M1/Aetheris.Continuum.M1.csproj -c Release
```

They include the benchmark summary, cylinder and plane results, per-cell diagnostics, fixed-versus-fine comparison, deterministic projection/hash, query/cache counts, active volume fractions, and stage timings.

## Oct ideas: reused, modified, rejected

- Reused: M11 deterministic fractional coverage; M12 boundary-local selective escalation; M15 separation of coverage, proximity, and orientation.
- Modified: Aetheris samples only exact-classified Cut cells; adds a truly nested hierarchy and stable caches; uses exact CIR support for frames/validation; estimates a 3D extruded wall rather than a 2D ownership field.
- Rejected: Oct's later AMR/patch hierarchy, flux/transfer, diffusion-like consequences, and treating coverage gradients as geometry normals. Those belong neither to fixed-lattice M1 nor to geometry-only `GeometrySamplePlan`.

## Limits and recommended M2

V1 map integration is deliberately bounded to boundaries extruded along lattice Z, which covers the cylinder and plane controls without hardcoding cylinder logic into the integrator. M2 should generalize local-domain clipping and area/first-moment integration for arbitrary oriented analytic patches, reduce independent-validation cost through deterministic stratified policy or certified support bounds, and add a bounded exact-BRep face-backed `BoundaryReference` fixture. It should still avoid AMR until geometry-local methods are tested on a non-extruded curved surface such as a sphere or torus patch.
