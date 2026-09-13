# FEA-SHELL-X1 — native sheet-metal authority and thin-wall conditioning

## Executive verdict

**Accepted for the explicitly experimental lane.** `ExperimentalShell` now consumes authored native `SheetMetalPartIr`, projects a read-only analytic midsurface patch graph with finite-radius bends, shares conforming C0 displacement modes across patch interfaces, and solves the formerly failing `L/t=200` system in 1,592 iterations. It remains labeled experimental and does not change or replace production solid FEA.

The real investor bracket was attempted without duplicating its geometry. It fails closed because its rounded bend terminations create partial-edge interfaces that X1's conforming full-edge coupling cannot represent. This is a precise X2 nonconforming/subdivision requirement, not a reason to weaken native authority.

## Frozen X0 baseline

The X0 mechanics are unchanged: the same three-displacement 3D strain operator, isotropic constitutive law, thin-wall map equation, hierarchical integrated-Legendre basis, alpha semantics, and recursive cut quadrature remain authoritative.

| Witness | Frozen X0 result | X1 regression |
|---|---:|---:|
| Perforated plate, 2x2, p=1/2/3/4, scaled energy | 0.479279 / 0.585525 / 0.659484 / 0.689749 | focused regression passes |
| Perforated plate p=4 reference error | 1.77% vs screenshot-derived 0.7021812127 | unchanged |
| Flat cantilever p=3 | 1.21026 mm; 3.18% low | 1.21026 mm |
| Thickness sweep L/t=10/50/100 | 1.08% / 3.18% / 3.56% error | unchanged physical results |
| Alpha 1e-8/1e-10/1e-12 | relative energy spread below 1.1e-8 | focused regression passes |
| Production solid cantilever | 5.39309699e-5 m; 56 iterations | unchanged |

No X0 formulation defect was found or corrected in X1.

## Sheet-metal authority audit

| Question | Existing authority used by analysis |
|---|---|
| Thickness | `SheetMetalPartIr.Thickness`; converted once from mm to m |
| Flat panel reference surface | `SheetRegionIr.Plane` plus its exact semantic contour |
| Bend axis/radius/angle | `SheetBendIr` and the corresponding `SheetRegionIr.Cylinder` |
| Structural midsurface radius | `inside radius + thickness/2`, exposed as `GeometricMidRadius` |
| Adjacency | `SheetBendIr.AdjacentRegionA/B` plus semantic region correspondence |
| Openings | `SheetFeatureIr` ownership and analytic profile, lowered as trim exclusions |
| Formed versus flat relation | existing region/bend identities and SheetMetal flattening correspondence; the analysis consumes formed authority |
| Semantic versus reconstructed geometry | only authored, `Complete` native SheetMetal AIR is admitted; recovered/reconstructed BRep is rejected |

The structural surface is the geometric material midsurface. The manufacturing K-factor locates the neutral axis used for bend allowance and is deliberately not used as the structural midsurface. Native analysis never pairs STEP faces, measures opposite BRep faces, or infers adjacency by proximity.

## Analysis projection and coupling

`ThinWallAnalysisAuthority` projects, but does not own, product geometry:

`SheetMetalPartIr -> MidsurfacePatchGraph -> analytic patch maps/trims -> X0 finite cells -> shared global 3D displacement system`

Planar patches use deterministic local frames. Cylindrical patches use axial coordinate `r`, arc-length coordinate `s`, and normalized thickness coordinate `t` in `[-1,+1]`. Both sides of every semantic interface are mapped and compared within `1e-8 m`; mismatch fails with `thinwall-sheetmetal-interface-mismatch`.

Conforming edge modes are unioned before global numbering. Reversed parameter directions explicitly reverse vertex order and apply hierarchical-mode parity. Thus `u_A = u_B` by construction; no duplicate edge solutions are averaged. Material-normal consistency is independently checked. X1 intentionally rejects unequal edge spans/partitions, crossing openings, and nonmanifold ambiguity.

| Witness | Patches | Interfaces/orientation | Openings | Shared displacement DOFs | DOFs | Iterations | Max displacement | Equilibrium residual |
|---|---:|---|---:|---:|---:|---:|---:|---:|
| Native flat | 1 panel | none | 0 | 0 | 624 | 154 | 1.21026155e-3 m | 3.55e-7 N |
| Native L bracket | 2 panels + 1 bend | Same, Same | 0 | 90 | 585 | 260 | 1.82293756e-3 m | 6.57e-7 N |
| Native U channel | 3 panels + 2 bends | Same, Same, Reversed, Same | 2 | 180 | 945 | 1,519 | 4.25714580e-4 m | 3.61e-8 N |

The native-flat displacement differs from the synthetic X0 flat result by only `2.79e-8` relative. Shared identity makes interface displacement jump numerically zero. Stress is retained per patch and sampled at `t=-1,0,+1`; no bend-gradient averaging or classical shell resultant is introduced.

## Conditioning evidence

The `L/t` sweep keeps the X0 grid at 4 cells, p=3, and 624 DOFs. Matrix metrics, residual history, scaling range, IC shift/retries, and timing are emitted in JSON. The X1 default is symmetric diagonal equilibration followed by shifted IC(0).

| L/t | Policy | Iterations | Final solver residual | Displacement | Reference error | Equilibrium | Verdict |
|---:|---|---:|---:|---:|---:|---:|---|
| 50 | Jacobi, unscaled | 1,385 | 5.06e-8 | 1.21026158e-3 m | 3.18% | 5.25e-8 N | baseline passes |
| 50 | equilibration + IC(0) | 1,009 | 7.58e-13 | 1.21026158e-3 m | 3.18% | 5.89e-8 N | passes |
| 100 | Jacobi, unscaled | 4,916 | 4.52e-8 | 9.64360013e-3 m | 3.56% | 1.78e-7 N | baseline passes |
| 100 | equilibration + IC(0) | 3,216 | 4.83e-13 | 9.64360010e-3 m | 3.56% | 1.27e-7 N | passes |
| 200 | Jacobi, unscaled | 10,000 | 2.29e-4 | none | — | — | stalls |
| 200 | equilibration + IC(0) | **1,592** | 3.52e-13 | 7.70232642e-2 m | 3.72% | 7.99e-6 N | **qualifies** |
| 500 | Jacobi, unscaled | 10,000 | 1.83e2 | none | — | — | stalls |
| 500 | equilibration + IC(0) | 6,729 | 2.44e-13 | 1.20278659 m | 3.78% | 6.74e-4 N | algebraic solve only; equilibrium not qualified |

At L/t=200, identity, Jacobi, equilibration alone, equilibration+Jacobi, and equilibration+3x3 block Jacobi all reach the 10,000-iteration cap. Block Jacobi is useful evidence, not the winner: at L/t=100 it takes 6,073 iterations versus 6,612 for equilibration+Jacobi and 3,216 for equilibration+IC(0).

IC stabilization is a bounded policy decision. Six fixed candidates use shifts `[0, 1e-8, 1e-6, 1e-4, 1e-2, 1] * max(diagonal)`. Every candidate is factored deterministically; admissibility is successful positive-pivot construction. `JudgmentEngine` selects the smallest admissible shift by utility and the report records the selected utility, failed candidates, chosen shift, and retry count. It chooses policy only; it never changes geometry, mechanics, or engineering intent.

The p=2..6 L/t=100 study converges in 324 / 3,216 / 764 / 842 / 910 iterations, with displacements 9.36861 / 9.64360 / 9.73460 / 9.76148 / 9.77806 mm. The p=3 shift requires the largest bounded stabilization in this set; this non-monotonic conditioning is exactly why selection is measured rather than inferred from p alone.

## Curved trim and body force

`BodyForce` now integrates `density * acceleration` using the same mapped volume quadrature, physical Jacobian, trim classification, and alpha-scaled fictitious contribution as stiffness assembly. The curved 40-degree, R=200 mm, t=2 mm perforated witness passes with finite continuum fields and reaction equilibrium. The complete modified Scordelis–Lo result is not claimed: a trustworthy paper comparison still requires verified symmetry-coordinate assignment and benchmark reference extraction beyond the supplied screenshot-level information.

## Investor bracket attempt

| Field | Result |
|---|---|
| Native sheet authority | yes; original authored fixture reused unchanged |
| Patch/DOF/solve results | unavailable because projection correctly fails before discretization |
| Blocker | `thinwall-sheetmetal-interface-mismatch` |
| Evidence | both bend-to-panel curves differ by 0.0020000000000000018 m |
| Cause | rounded bend terminations make the cylindrical interface a proper subset of the unsplit panel edge |
| Overall verdict | not qualified; needs semantic panel-edge subdivision or nonconforming coupling |

DFM status remains independent. No benchmark-only midsurface, nearest-edge match, or geometry averaging was added.

## CLI and artifacts

Ordinary authors select only `Mode: ExperimentalShell`; the qualified solver policy is automatic. Debug-only CLI switches are `--experimental-preconditioner identity|jacobi|block-jacobi|ic0` and `--experimental-equilibration true|false`.

```powershell
dotnet run --project Aetheris.CLI -c Release -- fea fixtures/Canonical/FEA/ExperimentalShell/native-l-bracket.firmament --out-dir artifacts/local/fea-shell-x1/native-l-bracket --json
powershell -ExecutionPolicy Bypass -File scripts/qualify-fea-shell-x1.ps1
powershell -ExecutionPolicy Bypass -File scripts/qualify-fea-shell-x1-investor.ps1
```

Per-run output includes `analysis-ir.json`, `midsurface-patch-graph.json`, solver/system/residual JSON, parameter-domain and mapped/deformed SVGs, boundary/body-force quadrature, and continuum result summaries. Generated evidence remains under ignored `artifacts/local/fea-shell-x1/`.

## Remaining evidence-driven limits

- X2: semantic edge subdivision or nonconforming coupling for partial-edge bend interfaces, proven by the investor bracket.
- X2: tiny-cut stabilization/aggregation only if a dedicated worsening cut-fraction sweep shows IC/equilibration is insufficient. The offset-cut regression remains finite; no ghost penalty was justified in X1.
- Complete modified Scordelis–Lo reference qualification after its exact symmetry and reference data are independently verified.
- General imported thin-wall recognition, nonlinear geometry, buckling, plasticity, and contact remain out of scope.

## Validation

Validation completed on 2026-09-12:

- Release solution build: passed with zero warnings and zero errors.
- Complete FEA regression: 43/43 passed, including all X0 cases, native flat parity, single/multi-bend coupling, alpha, offset cut, curved trim/body force, equilibrium, deterministic repeat, and typed invalid authority.
- Complete solution test run, serialized: 3,391 tests passed across the test-bearing projects; `Aetheris.FrictionLab.Tests` contains no discoverable tests. A prior parallel run transiently failed one unrelated tessellation determinism test, which passed immediately alone and again in the serialized 961-test Kernel.Core run.
- Production cantilever: unchanged at 56 iterations and 5.393096992e-5 m; equilibrium residual 2.30e-9 N.
- Canonical qualification: 167/167 fixtures passed.
- Fresh CLI publish outside the repository: native single bend passed in 260 iterations; L/t=200 passed in 1,592 iterations at 0.0770232642 m.
- Curved body-force CLI witness: 753 iterations, 9.57344e-5 m, 8.32342 N gravity resultant, and 2.88e-8 N equilibrium residual.
- Conditioning matrix, p study, investor attempt, native debug artifacts, and deterministic rerun: completed under ignored `artifacts/local/fea-shell-x1/`.
- Modified-document link resolution, repository layout guard (4,030 tracked files), and `git diff --check`: passed.
