# FEA-SHELL-X0 — experimental full-3D thin-wall finite cells

## Executive verdict

**Meaningful progression.** Aetheris can now solve flat thin bodies through an explicit shell-aligned, high-order finite-cell discretization while retaining full three-dimensional elasticity and a cell count independent of thickness. The real CLI path passes a flat cantilever through `L/t=100`, shows p-convergence on a trimmed plate, preserves equilibrium, and emits deterministic JSON/SVG evidence. It is not accepted for production: `L/t=200` exposes PCG conditioning, native SheetMetal authority is not connected, and the modified Scordelis–Lo benchmark is deferred.

This is always labeled `ExperimentalShell`; it neither replaces nor silently falls back to the production solid solver.

## Implemented path

`Analysis -> ExperimentalShell -> ThinWallGeometryMap -> 2D master grid x t -> hierarchical tensor p-basis -> FCM quadrature/alpha -> existing sparse solve -> continuum results`

The mode exposes thickness, flat/cylindrical map, grid, p order, alpha, quadrature order, subdivision depth, cell classification, Jacobian extrema, DOFs, nonzeros, residual, equilibrium, through-thickness stress locations, and a deterministic discretization hash. Imported STEP shell inference fails closed. Abaqus export is deliberately omitted because its existing Q1 export does not represent this basis.

## Basic perforated finite-cell benchmark

The committed fixture is a 4 m x 4 m x 1 m solid with a central 1 m-radius through-hole, `E=206.9 GPa`, `nu=0.29`, 100 Pa top traction, a 2 x 2 master grid, depth 5, and default alpha. The supplied reference `0.7021812127` is screenshot-derived without a verified unit convention. The table therefore compares the explicitly labeled display normalization `10^6 U_SI`; the raw SI energy is that value times `10^-6 J`.

| p | cells | DOFs | `10^6 U_SI` | reference error | PCG residual | equilibrium residual (N) |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 4 | 54 | 0.479279 | 31.74% | 3.96e-9 | 1.44e-9 |
| 2 | 4 | 225 | 0.585525 | 16.61% | 1.13e-6 | 1.94e-7 |
| 3 | 4 | 588 | 0.659484 | 6.08% | 1.65e-6 | 2.68e-7 |
| 4 | 4 | 1215 | 0.689749 | 1.77% | 2.19e-6 | 3.68e-7 |

The fixed grid exhibits strong measured p-convergence; no exponential-rate claim is made. All four master cells are cut. At depth 5 they produce 124 terminal cut subcells and an estimated minimum physical fraction of 0.8034.

## Flat thin-wall cantilever and thinness sweep

The cantilever is 100 mm x 20 mm, steel (`E=200 GPa`, `nu=0.3`), fixed at `-X`, and loaded by a 10 N `-Z` resultant at `+X`. The reference is Euler–Bernoulli tip displacement `F L^3/(3 E I)`, with `I=b t^3/12`. The predeclared acceptance is less than 5% through the successful sweep.

| L/t | cells | p | DOFs | displacement (m) | reference (m) | error | equilibrium residual (N) |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 10 | 4 | 3 | 624 | 9.89163e-6 | 1.00000e-5 | 1.08% | 6.01e-9 |
| 50 | 4 | 3 | 624 | 1.21026e-3 | 1.25000e-3 | 3.18% | 4.60e-8 |
| 100 | 4 | 3 | 624 | 9.64360e-3 | 1.00000e-2 | 3.56% | 2.12e-7 |
| 200 | 4 | 3 | 624 | no result | 8.00000e-2 | — | PCG stopped at 10,000 iterations, residual 5.24e-5 |

The number of cells and DOFs does not grow with thinness. The `L/t=200` failure is a conditioning limit in the current diagonal-preconditioned iterative solve, not geometric thickness under-resolution. It remains a typed `thinwall-linear-solve-failed` result.

For the default 2 mm case, p=2 gives 1.18185 mm (5.45% low) and p=3 gives 1.21026 mm (3.18% low), establishing refinement toward the 1.25 mm reference.

## Numerical integration studies

At p=2 on the perforated plate:

| alpha | depth | Gauss order | cut leaves | strain energy (J) | iterations |
|---:|---:|---:|---:|---:|---:|
| 1e-8 | 5 | 3 | 124 | 5.85525480e-7 | 169 |
| 1e-10 | 5 | 3 | 124 | 5.85525486e-7 | 168 |
| 1e-12 | 5 | 3 | 124 | 5.85525486e-7 | 169 |
| 1e-12 | 3 | 3 | 28 | 5.90202802e-7 | 168 |
| 1e-12 | 4 | 3 | 60 | 5.84874884e-7 | 169 |
| 1e-12 | 5 | 4 | 124 | 5.84963716e-7 | 169 |

Alpha sensitivity is below `1.1e-8` relative across the requested range. Depth 3 versus 5 changes energy by 0.80%; Gauss order 3 versus 4 at depth 5 changes it by 0.096%. The default remains 1e-12 without post-hoc tuning.

## Geometry, results, and failure boundaries

- The flat and analytic cylindrical maps have independent positive-Jacobian tests at `t=-1,0,+1`. Zero, negative, inconsistent, or nonfinite mappings have typed failures.
- Stress and von Mises are ordinary continuum results evaluated at `t=-1,0,+1`; they are not membrane or bending resultants.
- Algebraic strain energy is checked against independent continuum reintegration. The flat p=3 relative discrepancy is 3.69e-9.
- Only exact master faces support strong constraints and surface loads. No node snapping or whole-edge expansion is performed.
- Debug output includes `parameter-domain-grid.svg`, `mapped-deformed-cells.svg`, and structured JSON under caller-selected output directories.

## Paper-derived method versus Aetheris adaptation

| Topic | Source-derived method | Aetheris X0 implementation | Status |
|---|---|---|---|
| 3D displacement formulation | Revised directive: full 3D displacement elasticity | Existing isotropic `C`, physical-gradient 3D `B`, three displacement components | Implemented |
| Alpha method | Fictitious-domain constitutive scaling | Pointwise `alpha C` | Implemented |
| `alpha=1e-12` | Revised directive default | Explicit setting and evidence; never auto-tuned | Implemented |
| Hierarchical integrated-Legendre basis | High-order hierarchical p-space | Conforming tensor hexahedral space, p=1..6 | Aetheris adaptation |
| p-refinement | Increase order on a fixed grid | Deterministic shared entity keys and fixed ordering | Implemented |
| `M(r,s,t)` | Thin-wall coordinate map | Flat and analytic cylindrical constant-thickness maps | Implemented subset |
| 2D parameter grid | Cells in the geometry parameter plane | Regular rectangular grid, one normalized thickness span | Implemented |
| Interior quadrature | Tensor Gaussian | Default `(p+1)^3` | Implemented |
| Cut integration | Resolve indicator discontinuity | Recursive `(r,s)` subcells plus tensor Gauss points | Aetheris adaptation |
| Geometric classification | Trimmed parameter domain | Exact analytic bounds and point containment | Implemented subset |
| Dirichlet enforcement | Paper details unverified | Strong elimination on exact master faces | X0 simplification |
| Sparse solver | Not source-prescriptive | Existing sparse symmetric matrix and diagonal PCG | Aetheris adaptation |

The bibliographic and abstract-level claims are supported by the [TUM publication record](https://portal.fis.tum.de/en/publications/shell-finite-cell-method-a-high-order-fictitious-domain-approach-/) and [DOI record](https://doi.org/10.1016/j.cma.2011.06.005). Full-text equation fidelity is not claimed; see the development notes.

## Deferred qualification

| Requested case | Status | Evidence / next blocker |
|---|---|---|
| Modified Scordelis–Lo | Deferred | Body-force loading and curved parameter-space hole classification are not present; symmetry coordinates require full benchmark verification |
| Native planar SheetMetal panel | Deferred | Current analysis compiler has no semantic midsurface/thickness adapter from `Aetheris.SheetMetal` |
| Simple bend / patch coupling | Out of X0 core | Requires explicit multipatch continuity; no smoothing approximation was introduced |
| Investor bracket | Not attempted | Benchmark qualification is incomplete, and arbitrary STEP midsurface inference remains forbidden |
| Grid-offset/tiny-cut sweep | Deferred | Current native through-hole fixture is centered; tiny-cut conditioning needs a dedicated semantic-offset fixture |

## Reproduction and validation

Run:

```powershell
dotnet build Aetheris.slnx -c Release -m:1
dotnet test Aetheris.FEA.Tests/Aetheris.FEA.Tests.csproj -c Release
dotnet run --project Aetheris.CLI -c Release -- fea fixtures/Canonical/FEA/ExperimentalShell/flat-cantilever.firmament --out-dir artifacts/local/fea-shell-x0/flat
dotnet run --project Aetheris.CLI -c Release -- fea fixtures/Canonical/FEA/ExperimentalShell/cut-boundary-plate.firmament --out-dir artifacts/local/fea-shell-x0/cut
```

Raw per-run evidence belongs in ignored `artifacts/local/fea-shell-x0/`; this document is the compact durable record.

Validation completed on 2026-09-12:

- Release solution build: passed with zero warnings and zero errors.
- Broad solution tests excluding the tagged slow corpus: passed; focused FEA suite passed 35/35 and focused FEA CLI tests passed 4/4.
- Canonical qualification: passed 163/163 fixtures, including both ExperimentalShell fixtures and the production cantilever.
- Production cantilever CLI witness: `ProductionSolid`, 56 iterations, 5.39309699e-5 m maximum displacement, 3.22e-9 N equilibrium residual, valid Abaqus package.
- Packaged CLI: `artifacts/local/fea-shell-x0/package/Aetheris.CLI.2.0.0-preview.3.nupkg`.
- Repository layout guard and `git diff --check`: passed.
