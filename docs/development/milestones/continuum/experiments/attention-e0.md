# AETHERIS-CONTINUUM-ATTENTION-E0

Status: completed CPU experiment; mixed numerical result, strongest structural result is equivalence to known sparse/coarse mechanisms.

## Hypothesis and falsification rule

The exact hypothesis tested was:

> An analytic, deterministic, SPD local/global/hierarchical residual interaction can reduce the work of an exact-residual Poisson PCG solve at competitive total CPU cost.

The experiment did not assume neural computation, training, Q/K/V matrices, softmax, or replacement of sparse physics. A candidate was useful only if `||Ku-f||` converged and its interaction cost did not erase its iteration reduction. A result in which hierarchy merely restated coarse correction, or nonlocal work dominated total time, counted against a novel attention mechanism.

## Problem and reference

The domain is the unit cube. There are `n^3` interior nodal unknowns and homogeneous Dirichlet values on all six faces. With `h=1/(n+1)`, the matrix-free seven-point operator is

```text
(Ku)i,j,k = [6u(i,j,k) - u(i-1,j,k) - u(i+1,j,k)
                         - u(i,j-1,k) - u(i,j+1,k)
                         - u(i,j,k-1) - u(i,j,k+1)] / h^2.
```

Missing neighbors are the explicit zero boundary values. `K` is symmetric positive definite. Its structural nonzero count is `N + 6(n-1)n^2`. The manufactured continuum solution is

```text
u = x(1-x)y(1-y)z(1-z)
f = 2[ y(1-y)z(1-z) + x(1-x)z(1-z) + x(1-x)y(1-y) ].
```

This polynomial is an independent analytic reference. Centered second differences reproduce each quadratic second derivative exactly, so the sampled analytic solution is also the exact discrete solution up to floating-point error. This is useful here: solution errors measure solver correctness without mixing in discretization error. The forcing is not a single eigenmode, avoiding an artificially one-step CG baseline.

## Token analogy and K versus K^-1

“Token” is only an interpretation layer. One token is one interior lattice unknown. Available state is its lattice/physical position, scalar iterate, residual, forcing, boundary proximity, and stencil coefficients. Production Continuum terminology is unchanged.

The evidence makes the mapping unambiguous:

- `K` is the local seven-point interaction and is already represented optimally as a sparse stencil.
- Global influence belongs to `K^-1`, an approximate inverse, residual propagator, or coarse correction—not to a replacement for `K`.
- Every experimental path therefore applies `z=P(r)` as a PCG preconditioner while the conventional sparse `K` supplies every residual and convergence decision.

## Operators

All applications are deterministic and CPU-only.

| Operator | Form and cost | Mathematical contract |
|---|---|---|
| CG | Identity preconditioner | SPD |
| Jacobi | `(1/diag(K))I`; on this uniform problem it is only a scalar rescaling | SPD |
| Compact symmetric | `D^-1(I + 0.12 A_neighbor)`, one-edge support, `O(N)` | SPD because `|beta| < 1/6` |
| Screened symmetric | Separable `exp(-ManhattanDistance/2)` plus Jacobi, calibrated to the `(1,1,1)` inverse response, applied by line recurrences | Positive symmetric kernel, `O(N)` |
| Truncated Green | `D^-1 + sum(q q^T)(1/lambda - D^-1)` for the eight `(1..2)^3` Dirichlet sine modes | Positive rank-8 spectral factorization, `O(8N)` |
| Hierarchical screened | Average each `2x2x2` block, screened interaction on macro tokens, constant scatter, plus fine Jacobi | `R=(1/8)P^T`, SPD macro kernel |
| Two-level control | Same transfer, eight weighted-Jacobi steps on the Galerkin coarse stencil, scatter, plus fine Jacobi | Positive coarse inverse polynomial |

The screened kernel is a distance-decay control, not claimed to be a Green function. Only the sine expansion is Green-function-inspired: it is a literal truncation of the discrete Dirichlet inverse eigen-expansion. The compact operator is a signed sparse residual propagator. The hierarchy is a local-plus-global/multi-channel analogue: fine Jacobi and a separate macro interaction contribute additively.

Softmax was rejected before solve testing. Row normalization is generally asymmetric, destroys the physical response scale required of an approximate inverse, and offers no SPD guarantee for PCG. A dense inverse-distance control was also rejected for the scaling path because its `O(N^2)` cost violates the experiment contract; the separable screened kernel supplies the scalable distance-decay control.

Numerical contract probes at `32^3` used two deterministic nontrivial vectors. Every tested preconditioner had positive observed energy. Relative symmetry defects ranged from `5.20e-18` to `3.32e-15`, below the `1e-11` test threshold.

## Measured scaling

Environment: Windows 11 Pro, AMD Ryzen 7 7700X (8 cores/16 logical processors), .NET 10 Release, single process. Times are medians of three warmed runs and are not cross-machine performance claims. Setup is recorded separately in the artifact; the table shows solve time. Tolerance is relative residual `1e-8`. Memory is deterministic solver-working-set plus preconditioner storage, not process RSS.

| Method | n | Unknowns | Nonzeros | Iterations | Final relative residual | Relative solution error | Solve ms | Memory bytes |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| CG | 8 | 512 | 3,200 | 16 | 3.20e-9 | 2.04e-10 | 0.73 | 20,480 |
| Jacobi-CG | 8 | 512 | 3,200 | 16 | 3.20e-9 | 2.04e-10 | 0.22 | 20,488 |
| Compact symmetric | 8 | 512 | 3,200 | 10 | 7.30e-9 | 6.98e-10 | 0.31 | 20,480 |
| Screened symmetric | 8 | 512 | 3,200 | 13 | 1.66e-9 | 1.35e-10 | 0.50 | 28,672 |
| Truncated Green rank 8 | 8 | 512 | 3,200 | 13 | 6.13e-9 | 5.00e-10 | 1.53 | 20,640 |
| Hierarchical screened | 8 | 512 | 3,200 | 17 | 3.86e-9 | 2.31e-10 | 0.43 | 22,528 |
| Two-level control | 8 | 512 | 3,200 | 17 | 3.11e-9 | 1.80e-10 | 0.76 | 22,528 |
| CG | 16 | 4,096 | 27,136 | 33 | 5.96e-9 | 1.53e-10 | 1.26 | 163,840 |
| Jacobi-CG | 16 | 4,096 | 27,136 | 33 | 5.96e-9 | 1.53e-10 | 1.21 | 163,848 |
| Compact symmetric | 16 | 4,096 | 27,136 | 20 | 4.19e-9 | 1.29e-10 | 1.11 | 163,840 |
| Screened symmetric | 16 | 4,096 | 27,136 | 15 | 6.52e-9 | 2.50e-10 | 1.69 | 229,376 |
| Truncated Green rank 8 | 16 | 4,096 | 27,136 | 27 | 7.28e-9 | 3.49e-10 | 12.73 | 164,000 |
| Hierarchical screened | 16 | 4,096 | 27,136 | 27 | 9.04e-9 | 3.27e-10 | 1.71 | 180,224 |
| Two-level control | 16 | 4,096 | 27,136 | 24 | 6.35e-9 | 2.41e-10 | 1.84 | 180,224 |
| CG | 32 | 32,768 | 223,232 | 65 | 7.72e-9 | 3.68e-10 | 12.16 | 1,310,720 |
| Jacobi-CG | 32 | 32,768 | 223,232 | 65 | 7.72e-9 | 3.68e-10 | 11.89 | 1,310,728 |
| Compact symmetric | 32 | 32,768 | 223,232 | 37 | 9.37e-9 | 4.48e-10 | 9.51 | 1,310,720 |
| Screened symmetric | 32 | 32,768 | 223,232 | 20 | 7.22e-9 | 6.61e-11 | 17.22 | 1,835,008 |
| Truncated Green rank 8 | 32 | 32,768 | 223,232 | 53 | 8.34e-9 | 2.85e-10 | 172.70 | 1,310,880 |
| Hierarchical screened | 32 | 32,768 | 223,232 | 33 | 8.35e-9 | 1.28e-10 | 10.81 | 1,441,792 |
| Two-level control | 32 | 32,768 | 223,232 | 32 | 7.10e-9 | 1.27e-10 | 10.38 | 1,441,792 |

Jacobi cannot change iteration count on a constant-diagonal operator. Its timing difference from CG is benchmark noise/code-path cost, not numerical value. At `32^3`, compact symmetric coupling reduces iterations by 43.1% and solve time by 21.8%. The screened kernel reduces iterations by 69.2%, but is 41.6% slower than CG because its 13.76 ms interaction cost dominates. Rank-8 Green interaction is 14.2 times slower than CG.

### Cost breakdown at 32^3

| Method | Sparse matvec ms | Preconditioner ms | Interaction ms | Hierarchy ms | Total ms |
|---|---:|---:|---:|---:|---:|
| CG | 4.80 | 1.05 | 0 | 0 | 12.16 |
| Jacobi | 4.63 | 0.95 | 0 | 0 | 11.89 |
| Compact | 2.71 | 3.21 | 3.20 | 0 | 9.51 |
| Screened | 1.41 | 13.77 | 13.76 | 0 | 17.22 |
| Truncated Green | 3.82 | 162.99 | 162.93 | 0 | 172.70 |
| Hierarchical screened | 2.33 | 5.29 | 2.60 | 2.68 | 10.81 |
| Two-level control | 2.31 | 4.86 | 0 | 4.86 | 10.38 |

## Residual and mode behavior

The residual is not required to decrease monotonically in CG/PCG. Early relative residuals at `32^3` show the different behavior:

| Method | Iter 1 | Iter 5 | Iter 10 | Iter 20 | Final |
|---|---:|---:|---:|---:|---:|
| CG / Jacobi | 3.082 | 9.47e-1 | 4.90e-1 | 7.16e-2 | 7.72e-9 |
| Compact | 2.830 | 7.82e-1 | 2.41e-1 | 8.79e-4 | 9.37e-9 |
| Screened | 2.646 | 6.25e-2 | 5.75e-5 | 7.22e-9 | 7.22e-9 |
| Truncated Green | 2.71e-1 | 1.57e-1 | 3.44e-2 | 1.01e-3 | 8.34e-9 |
| Hierarchical screened | 4.448 | 3.61e-1 | 2.65e-2 | 2.86e-5 | 8.35e-9 |
| Two-level control | 4.774 | 4.51e-1 | 2.40e-2 | 4.78e-5 | 7.10e-9 |

A separate unit-correction probe starts with a normalized exact sine error mode, forms `r=Ke`, applies `e <- e-P(r)`, and reports the remaining norm:

| Method | Low `(1,1,1)` | High `(n,n,n)` | Interpretation |
|---|---:|---:|---|
| Jacobi | 0.9955 | 0.9955 | Equal-magnitude poor extremes |
| Compact | 0.9922 | 0.4348 | High-frequency smoother |
| Screened | 0.0411 | 1.1001 | Strong low-mode correction, slight high-mode amplification |
| Truncated Green | 1.92e-14 | 0.9955 | Exact selected low mode, no benefit outside rank |
| Hierarchical screened | 0.4786 | 0.9955 | Coarse/low-frequency correction |
| Two-level control | 0.9494 | 0.9955 | Eight coarse smoothing steps only weakly invert the very lowest mode |

This is the clearest positive evidence for the interaction-space interpretation: analytic global propagation can target low-frequency error. It is not, by itself, an efficient preconditioner.

## Interaction visualization and Green behavior

For the center-adjacent token on `16^3`, the rank-8 Green factorization's largest weight is its diagonal/Jacobi contribution (`6.3227e-4`). The next seven strongest global-factor weights lie in the adjacent central `2x2x2` region and are positive (`5.52e-5` to `5.54e-5`); weights decay spatially and the truncated higher sine modes create signed lobes farther away. This is qualitatively consistent with a boundary-conditioned low-frequency influence expansion, but rank 8 is far too small for quantitative Green-function equivalence. Exact coordinates and the 32 strongest weights are persisted as JSON and CSV.

## Hierarchy versus multigrid

The macro experiment performs exactly the comparison the analogy requires:

```text
fine residual -> 2x2x2 average restriction -> macro interaction -> constant prolongation -> additive correction
```

This is the same restriction/coarse-action/prolongation skeleton as two-level multigrid. At `32^3`, hierarchical screened PCG takes 33 iterations and 10.81 ms; the conventional coarse-stencil control takes 32 iterations and 10.38 ms. The screened hierarchy removes low-frequency error more strongly per unit correction, but that does not translate into a total-solve advantage. In E0, “hierarchical attention” rediscovered coarse correction and was slightly less effective than the classical representation.

Attention-space language did add one useful diagnostic framing: it made interaction range, factorization rank, channels, and per-token influence explicit in a common interface. It did not add a new numerical mechanism.

## Scope gates and result

Result classification: **mixed, with a strong equivalence to known numerical structure**.

- There is measurable numerical value in treating residual state as an interaction space: compact coupling improves this solve, screened propagation removes low modes, and hierarchy exposes coarse communication.
- The competitive compact method is a classical sparse polynomial/stencil preconditioner under different language.
- The global screened and truncated-Green methods reduce selected errors but lose on CPU interaction cost.
- The hierarchical method is essentially a slightly slower two-level coarse correction.

The variable-coefficient/material and geometry-aware extensions were not run. The promising runtime result was the compact local operator, whose mechanism is already classical; the attention-specific nonlocal and hierarchical hypotheses failed the competitive-cost gate. Adding material identity or boundary features now would risk rescuing a failed simple hypothesis with more degrees of freedom. Cut cells were likewise excluded as planned.

The low-rank experiment was run at rank 8 and decisively cost-dominated. Higher rank was not attempted because application cost already reached 162.93 ms at `32^3` while saving only 12 of 65 iterations.

GPU decision: **do not use GPU**. Although the screened and spectral interactions are parallelizable, the CPU evidence does not show competitive operator quality and the best runtime result is an `O(N)` local stencil. Neither Oct/Prometheus nor Copeland/Aurelian was touched.

## Reverse lessons: FEA/numerics to attention

- Preserve an exact sparse operator and use flexible interaction only for residual correction.
- Separate local smoothing from low-frequency/coarse correction.
- Enforce symmetry, positive energy, boundary conditions, and conservation/nullspace constraints structurally rather than hoping data recovers them.
- Hierarchy needs an operator-consistent restriction/coarse action/prolongation contract; token aggregation alone is not enough.
- Global influence is an inverse property. Confusing a local operator with its dense inverse leads to the wrong interaction target.
- Count interaction application cost, setup, and memory alongside iteration count.

Learning might later help choose anisotropic kernel parameters, coarse bases, or material-aware channel weights, but E0 provides no evidence that learning is needed. No dataset or training path was introduced.

## Reproduction, tests, and artifacts

Run the deterministic sweep with:

```powershell
dotnet run --project tools/Aetheris.Continuum.AttentionE0/Aetheris.Continuum.AttentionE0.csproj -c Release
```

Focused tests cover seven-point assembly, CG/Jacobi correctness, independent analytic reference agreement, deterministic screened application, numerical symmetry/positive energy, `2x2x2` hierarchy construction, and exact sparse residual convergence for experimental paths.

Artifacts under `docs/development/milestones/continuum/artifacts/attention-e0/` include benchmark JSON/CSV, full residual histories, operator configurations and rejection reasons, mode analysis, mathematical contracts, interaction weights, a timing-free deterministic result projection, and its SHA-256 hash. Runtime fields are deliberately excluded from the deterministic hash.

## Recommended next experiment

Do not add learned attention or GPU acceleration next. If this line continues, test a deliberately nonuniform, anisotropic scalar coefficient problem using two controls:

1. an operator-consistent classical two-level or incomplete-factorization preconditioner;
2. a strictly SPD coefficient-aware interaction with the same sparsity/cost budget.

The falsifiable question should be whether explicit material/directional interaction metadata beats the classical control—not whether a more elaborate attention vocabulary can reproduce it.

## Direct answer

Yes, the interaction-space view has measurable interpretive and limited numerical value: it cleanly identifies low-frequency global propagation and produced a 21.8% runtime improvement through compact coupling on the largest case. But E0 does **not** show a distinct attention-style global solver advantage. The useful mechanisms are captured more efficiently by classical sparse smoothing and coarse correction; the genuinely nonlocal attention-shaped operators reduce iterations but lose on total cost.
