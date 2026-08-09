# M5 linear elasticity

## Formulation

Displacement DOFs live at admitted regular-lattice vertices, with three translational DOFs per node. Each active cell uses the trilinear eight-node hexahedral (Q1) basis. Strain uses small-strain kinematics with engineering shear in the internal six-vector; reported `SymmetricTensor` shear entries are tensor shear. Stress is isotropic Cauchy stress in the global Cartesian frame. Stress and strain are recovered at cell centers; no nodal stress projection is presented as primary truth.

Full cells use standard 2×2×2 Gauss integration and share the same regular-cell rule. Cut cells retain the regular basis and integrate only occupied CIR material using a distinct `MechanicsQuadraturePlan` (deterministic 4×4×4 occupied subcell midpoint rule). `GeometrySamplePlan` remains geometry-only and is never reused as solver quadrature.

The sparse representation is deterministic sorted-row storage. Assembly is symmetric. Dirichlet elimination preserves symmetry. The solver is conventional PCG with Jacobi preconditioning, a relative residual tolerance, deterministic iteration limit, and recorded residual history. Reactions are computed from the unmodified `K u - f` at constrained DOFs.

No stabilization was added. The fine canonical run measured eight cells at 6.25% active fraction, none below 5%, and converged reliably. This is evidence for this benchmark only, not a general tiny-cell stability claim.

## Canonical benchmark

The plate is 200 mm × 100 mm × 10 mm with a centered 20 mm circular through-hole, steel `E=200 GPa`, `nu=0.3`, the `-X` end fixed, and a +10 kN resultant at `+X`. Nominal gross-section stress is 10 MPa. A uniform unperforated bar would extend 10 µm. The infinite-plate Kirsch peak is approximately 30 MPa (factor 3); finite width and fully fixed end conditions mean it is a reference scale, not an exact solution for this finite model.

| Lattice | DOFs | NNZ | PCG iters | max displacement | max cell-center von Mises | factor / 10 MPa | equilibrium residual |
|---|---:|---:|---:|---:|---:|---:|---:|
| 8×4×1 | 270 | 11,700 | 54 | 10.158 µm | 11.899 MPa | 1.190 | 1.29e-7 N |
| 16×8×2 | 1,377 | 77,175 | 116 | 10.162 µm | 15.683 MPa | 1.568 | 3.78e-7 N |
| 24×12×2 | 2,925 | 170,163 | 141 | 10.341 µm | 31.549 MPa | 3.155 | 2.91e-7 N |

Stress convergence is non-monotone at coarse resolution because center recovery and Cut-cell occupancy do not sample the exact hole boundary peak. The fine result reaches the Kirsch reference scale, but three levels do not establish an asymptotic rate.

The current runtime separates domain setup, mechanics quadrature, assembly, boundary assembly, solve, and recovery. For 16×8×2 the measured sparse storage was 931,612 bytes and result storage 56,636 bytes. Timing values are persisted in `native-results.json` and are machine/run dependent.

## Known limitations

The requested rotated-lattice orientation study is not implemented: exact arbitrarily oriented boundary load integration is the next isolated blocker. The current semantic plane integrator is axis aligned. General imported STEP CIR recognition, curved pressure integration, stronger rigid-mode rank checks, proof of SPD beyond converged positive-curvature PCG behavior, boundary/quadrature-point stress reporting, and a trustworthy conventional conforming control mesh also remain.
