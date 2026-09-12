# FEA-SHELL-X0 source and implementation notes

## Source record

The cited source is E. Rank, S. Kollmannsberger, Ch. Sorger, and A. Düster, “Shell Finite Cell Method: A high order fictitious domain approach for thin-walled structures,” *Computer Methods in Applied Mechanics and Engineering* 200 (45–46), 3200–3209 (2011), [DOI 10.1016/j.cma.2011.06.005](https://doi.org/10.1016/j.cma.2011.06.005). The [TUM publication record](https://portal.fis.tum.de/en/publications/shell-finite-cell-method-a-high-order-fictitious-domain-approach-/) confirms the authors, bibliographic data, and abstract.

The full publisher text was not available in the implementation environment. Consequently, no equation numbers, exact trunk-space definition, or benchmark details are represented here as independently paper-verified. The revised FEA-SHELL-X0 directive supplies the governing full-3D formulation and benchmark parameters used by this implementation.

## PAPER — independently verified

The abstract establishes three claims:

- the method extends the Finite Cell Method to thin-walled structures;
- it combines a fictitious-domain method with high-order hierarchical Ansatz spaces from p-FEM;
- the finite cells occupy a two-dimensional master domain in the geometry parameter plane rather than a three-dimensional Cartesian embedding grid.

The abstract also mentions numerical benchmarks and potential application to trimmed NURBS surfaces. It does not expose enough detail to verify the paper's exact basis, integration algorithm, boundary enforcement, or benchmark numbers.

## SOURCE DIRECTIVE — governing X0 formulation

The revised directive defines a three-displacement, full-3D small-strain formulation. For a midsurface `m(r,s)`, unit normal `n(r,s)`, and half-thickness `k(r,s)`, the physical point is

`M(r,s,t) = m(r,s) + t k(r,s) n(r,s)`, for `-1 <= t <= 1`.

Each rectangular master cell in `(r,s)` spans the single normalized thickness interval. The reference-to-master affine Jacobian and the full derivatives `dM/dr`, `dM/ds`, and `dM/dt` form the physical Jacobian. Physical basis gradients produce the ordinary three-dimensional small-strain `B` operator; the existing isotropic constitutive relation remains `sigma = C epsilon`.

The extended material uses `alpha=1` in the physical region and an explicit default `alpha=1e-12` in the fictitious region. Cut cells are recursively divided in `(r,s)`, then integrated using tensor Gauss quadrature and semantic point containment. X0 strongly enforces only stable master-boundary Dirichlet data.

## AETHERIS ADAPTATION

| Topic | Source-derived method | Aetheris X0 implementation | Status |
|---|---|---|---|
| 3D displacement | Full continuum `ux,uy,uz` | Three translational coefficients per basis function; ordinary 3D strain and stress | Implemented |
| Thin-wall map | Parameter-plane cells mapped into a thin 3D body | Flat affine map and analytic cylindrical map | Implemented, constant thickness |
| 2D grid | Regular parameter/master domain | `MasterGrid: [r,s]`, with one `t` span | Implemented |
| Indicator | Physical/fictitious weighting | `1` / explicit `AlphaFictitious`, default `1e-12` | Implemented |
| Basis | High-order hierarchical p-space | Tensor hexahedral integrated-Legendre modal space, bounded to `1 <= p <= 6` | Adaptation; exact paper trunk space unverified |
| Interior quadrature | Tensor Gaussian integration | `(p+1)^3` by default | Implemented |
| Cut integration | Accurate discontinuous-indicator integration | Recursive quadtree subdivision in `(r,s)` plus tensor Gauss integration | Adaptation |
| Classification | Trimmed parameter domain | Exact `IBoundsClassificationCapability` and point containment; native through-hole is analytic | Implemented for box and one Z through-hole |
| Fictitious cells | Retain small stiffness | Points outside the physical trim remain assembled with alpha | Implemented |
| Dirichlet data | Embedded treatment is method-dependent | Strong elimination only on exact master-domain faces | X0 simplification |
| Sparse solution | Linear system | Existing deterministic sparse symmetric matrix and diagonal-PCG path | Adaptation |
| Energy | Continuum strain energy | Both `0.5 u^T K u` and independently reintegrated `0.5 epsilon^T C epsilon` | Implemented |

The integrated-Legendre internal mode of degree `n` is `(P_n-P_(n-2))/sqrt(4n-2)`; the two endpoint modes are linear. Shared entity keys provide conformity and deterministic ordering. This is an ordinary tensor p-space, not a claim of exact paper basis fidelity.

## DEFERRED / UNKNOWN

- Exact paper equation mapping, trunk-space pruning, stabilization, and weak essential-boundary procedure remain unknown without the full text.
- Native `Aetheris.SheetMetal` midsurface/thickness extraction is not connected to the analysis compiler. X0 accepts explicit native/synthetic box semantics and rejects imported STEP shell inference.
- Variable thickness, multiple surface patches, bend coupling, arbitrary embedded Dirichlet boundaries, and NURBS maps are deferred.
- The modified Scordelis–Lo case needs body-force loading, curved trim coordinates, and carefully verified symmetry axes. It was not approximated with invented data.
- The screenshot-derived perforated-plate energy `0.7021812127` has no verified unit convention. The release report compares it only to explicitly labeled `10^6 U_SI`; no solver parameter was tuned to match it.

