# M5C immersed-basis robustness

M5C reaches the core mechanics goal: the 15/20/45-degree case is usable on the unchanged lattice without AMR, diagonal floors, damping, or physical-stiffness scaling. Numerical authority is derived compiler metadata. It controls whether a Q1 coefficient remains independent and which admitted boundary formulation is assembled; it is not a PDE field.

## Untouched M5B reproduction

The pre-change CLI path reproduced 3,948 DOFs, 223,110 NNZ, 6,072 Cut cells, minimum cell fraction 3.0517578125e-5, diagonal ratio 2.8627e10, 869 Jacobi-PCG iterations, 2.9857 mm maximum displacement, 18.6247 GPa maximum recovered stress, and 2.65e-7 N equilibrium residual. Exact boundary integration remained accurate: 1.90e-11 N force and 9.52e-13 N m moment residual. This separates the basis/constraint failure from geometry authority and boundary integration.

## Basis authority and aggregation

For every active lattice node, `BasisSupportEvidence` integrates `N_i²` over physical quadrature and normalizes it by the nominal incident Cartesian support. A cell with at least 50% occupied quadrature is an admissible root. Root-cell nodes remain independent. A non-root basis below the frozen 2% support score is expressed by evaluating the root cell's Q1 basis at the source-node position.

The extension weights sum to one and reproduce the source position. Consequently constants and every affine displacement—including rigid translations, infinitesimal rigid rotations, and constant-strain patch fields—are reproduced algebraically. Assembly applies `Pᵀ K P` and `Pᵀ f`; no strategy can mutate arbitrary matrix entries. Roots are selected by bounded Cartesian distance, then `(K,J,I)` order. The three-cell search and same active connected lattice neighborhood prevent remote or nondeterministic associations.

Admission precedes utility. Ordinary Q1 is always admitted. Aggregation is admitted only when a well-supported root exists and the source is not itself in a root cell. The score is the frozen two-feature model:

`ordinary = normalizedSupport`

`aggregate = 0.03 - 0.01 setupCost = 0.02`

This exactly matches the fixed 2% threshold control. Because utility did not outperform the three-line fixed policy, the fixed crossing is retained and JudgmentEngine supplies explicit admission, tie-breaking, rejection, and provenance rather than pretending a more complex fitted model is justified.

## Boundary enforcement

Strong nearest-node enforcement remains the control. Exact-boundary maximum/RMS violations are evaluated at `MechanicsBoundaryQuadraturePlan` points, so zero constrained-node displacement is not misreported as zero physical-boundary error.

The alternative is symmetric Nitsche linear elasticity:

`a(u,v) - <sigma(u)n,v> - <sigma(v)n,u-g> + <gamma E/h (u-g),v>`.

It uses exact semantic face identity, material-side normal, face quadrature, Q1 trace, constitutive matrix, and local cell size. The admitted nominal tier is `gamma=100`; `gamma=20` produced non-positive PCG curvature and was rejected. Nitsche requires a complete vector constraint, exact quadrature, and minimum active fraction at least 1e-4. More severe trace configurations fall back to strong enforcement because the bounded penalty tiers do not establish coercivity there. Weak reactions use the consistent numerical flux `sigma(u)n - gamma E/h (u-g)`.

The basis and boundary decisions are separate. The canonical severe compound case selects aggregation plus strong enforcement; X90/Z45 and the held-out Z31 case select aggregation plus Nitsche. No AnalysisIR or Forge API field exposes either choice.

## Orientation evidence

| case | raw/effective DOFs | aggregated bases | NNZ | min fraction | <.01% / <.1% / <1% / <5% / <10% cells | BC | PCG | max u | max VM | energy J | equilibrium N | BC max/RMS m |
|---|---:|---:|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|
| Baseline | 1377/1377 | 0 | 77,175 | 4.722e-1 | 0/0/0/0/0 | Strong | 116 | 10.19 um | 16.17 MPa | .05087 | 3.57e-7 | 0/0 |
| Z45 | 2961/2952 | 3 | 167,832 | 8.333e-2 | 0/0/0/0/8 | Strong | 224 | 10.68 um | 35.35 MPa | .05214 | 3.14e-7 | 3.36e-7/2.04e-7 |
| X90/Z45 | 3024/1860 | 388 | 117,216 | 9.259e-3 | 0/0/4/4/4 | Nitsche | 507 | 11.30 um | 27.78 MPa | .05542 | 5.43e-6 | 1.27e-8/4.62e-9 |
| X15/Y20/Z45 | 3948/2196 | 584 | 150,912 | 3.052e-5 | 8/28/124/178/228 | Strong | 519 | 14.05 um | 34.72 MPa | .05107 | 5.65e-7 | 6.54e-7/3.27e-7 |
| held-out Z31 | 3078/2682 | 132 | 161,910 | 9.766e-4 | 0/4/16/24/44 | Nitsche | 810 | 10.95 um | 24.30 MPa | .05166 | 3.09e-6 | 1.11e-8/2.39e-9 |

All algebraic/integrated continuum energy comparisons agree within 6.5e-14 relative. The compound diagonal and row-norm ratios improve from 2.86e10 and 1.85e7 to 50.0 and 23.3. Its displacement is within 38% of the coarse baseline rather than 293x larger.

## Stress probes and Kirsch comparison

Validation-only probes evaluate the solved Q1 derivative at the exact local top and right hole points. For nominal 10 MPa remote stress, the infinite-plate references are +30 MPa and -10 MPa. Baseline gives +12.99/+2.64 MPa; Z45 +34.71/-0.90 MPa; X90/Z45 +27.93/-7.19 MPa; compound +27.37/-5.68 MPa; held-out Z31 +16.89/-5.12 MPa. The compound errors are 2.63 and 4.32 MPa. These are finite-width, coarse-Q1 comparisons, not claims that the benchmark is an exact infinite plate.

## Determinism, cost, and limits

The strategy map records every support, treatment, root, extension weights, both judgments, exact-boundary errors, reactions, and a SHA-256 policy evidence hash. Sorted nodes, root selection, sparse rows, and JudgmentEngine tie-breaking make the structure deterministic. Matrix storage falls from the M5B compound 2.69 MB to approximately 1.82 MB by the existing CSR estimate; the transformation storage is bounded at eight weights per aggregated basis.

Quadrature remains the dominant cost (roughly 30 seconds for the severe CLI run); aggregation/scoring is small relative to assembly. Jacobi-PCG is unchanged. Abaqus remains a conventional independent lowering with the same rotated deck hash and no custom aggregation/Nitsche elements.

Ghost penalty and a separate boundary MPC were not retained: affine aggregation solved the tiny-support blocker, and symmetric Nitsche supplies the exact weak path. Curved-face weak constraints, higher-order recovery, automatic local generalized-eigenvalue penalty bounds, nonlinear mechanics, contact, and AMR remain future work.

