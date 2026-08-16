# FEA-PROD-X1 production boundary

The production path is:

```text
Firmament analysis intent
  -> resolved geometry source (native body or imported STEP)
  -> IContinuumRegion analysis domain in SI coordinates
  -> deterministic regular lattice and occupied cut-cell quadrature
  -> MAT-DB-X1 LinearElasticIsotropic material record
  -> exact semantic planar boundary/load lowering
  -> deterministic sparse Q1 assembly and PCG solve
  -> typed result fields and reproducibility report
```

`LinearElasticAnalysisIr` is the ownership boundary. It contains the analysis kind, domain reference, resolved material and stable ID, constraints, loads and explicit distribution policy, lattice, requested outputs, SI values, and provenance. The mechanics namespace does not parse Firmament or STEP and does not query the material database.

Native boxes and box-with-hole bodies and imported BReps converge at `IContinuumRegion`. Six-planar-face imported boxes retain an exact analytic fast path. Other closed imported BReps use kernel `BrepSpatialQueries` for occupancy and exact BRep topology/trim curves for planar boundary domains. STEP coordinates currently follow the repository's documented millimetre import assumption and are scaled to metres at the analysis-domain seam. Unsupported or ambiguous kernel containment produces an empty/unsupported domain diagnostic rather than fabricated material.

The direct bounded form is:

```firmament
Analysis LinearElastic ImportedThroughHole {
    body: inlineSTEP("part.step")
    material: Standard.Materials.Aluminum.6061_T6
    Fixed Mount {
        region: body.face(#170)
        components: [X, Y, Z]
    }
    Force Load {
        region: body.face(#141)
        vector: [0N, -500N, 0N]
    }
    results: [Displacement, Strain, Stress, ReactionForce]
    lattice: [6, 4, 3]
}
```

The existing canonical `InlineStep name { Path: ... }` plus `body: name` form and recognized semantic regions remain supported. `#170` denotes stable STEP `ADVANCED_FACE` identity and is lowered to the corresponding BRep face ID; sequential `face(6)`, recognized region names, and bounded directional selectors remain available. This is a bounded face-selection system, not a universal CAD query language.

## Load and result semantics

`Force` is a total resultant in newtons, distributed over the selected exact face by area while preserving resultant and first moment. `Traction` is a vector in Pa applied per unit area. `Pressure` is a scalar in Pa acting opposite the CIR-resolved outward normal. Reports state this as `TotalResultantOverSelectedArea`, `TractionPerUnitArea`, or `PressureNormalToSurface`.

| Result | Representation | Unit | Location/recovery |
| --- | --- | --- | --- |
| Displacement | vector | m | admitted lattice nodes; aggregated nodes are affine extensions |
| Strain | symmetric small-strain tensor | 1 | occupied cell center from the Q1 gradient |
| Stress | isotropic Cauchy tensor plus von Mises scalar | Pa | occupied cell center; no nodal stress interpolation |
| ReactionForce | vector per constraint | N | constrained residual or consistent Nitsche boundary integral |

Only requested result collections are exposed. Equilibrium evidence is always retained as a run-integrity metric. The CLI report includes geometry identity/hash, BRep body ID, stable material ID, lattice, constraints, load policy, solver settings, timings, sparse memory, residual history, result contracts, and `sum(reaction)+sum(load)`.

## X1 witnesses

- Catalog coupon: `fixtures/FirmamentV2/Materials/catalog-material-coupon.firmament` preserves the MAT-DB-X1 5052-H32 path.
- Cantilever: `ProductionFeaX1Tests` checks exact linear scaling (`u` doubles with load and halves with doubled `E`), three bounded lattice levels, increasing DOFs, finite displacement, and reaction equilibrium. For the 120x20x20 mm beam, 100 N transverse tip resultant, and 70 GPa modulus, slender Euler-Bernoulli theory gives 61.71 micrometres. The table trends toward that reference; the fine result is about 5.6% low. The beam is only six depths long and the load/support idealizations differ, so this is an empirical credibility check rather than an asymptotic or exact-agreement claim.

| Lattice | DOFs | max displacement | beam-reference error | equilibrium residual | PCG residual / iterations | assembly / solve | sparse bytes |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 6x2x2 | 189 | 42.803 micrometres | -30.6% | 1.27e-9 N | 3.21e-8 / 49 | 28 / 25 ms | 101,308 |
| 12x2x2 | 351 | 53.931 micrometres | -12.6% | 3.22e-9 N | 2.94e-8 / 56 | 54 / 46 ms | 197,212 |
| 18x3x3 | 912 | 58.270 micrometres | -5.6% | 1.19e-9 N | 2.66e-8 / 89 | 186 / 223 ms | 597,652 |

The CLI `--lattice nx,ny,nz` override produced these three runs from one canonical fixture, keeping analysis intent fixed while varying only discretization. Timings are machine/run dependent.
- Imported through-hole: `fixtures/FirmamentV2/FEA/inline-step-through-hole.firmament` imports a seven-face planar/cylindrical STEP body without rebuilding it. At 6x4x3 it produced 72 cut cells, 420 DOFs, 22,230 nonzeros, 45 PCG iterations, 14.445 micrometre maximum displacement, 44.945 MPa maximum cell-center von Mises stress, and an 8.43e-9 N equilibrium residual. The measured run spent about 0.17 s in domain setup, 0.83 s in cut quadrature, 0.46 s in assembly, 0.04 s in solve, and used about 268 kB of sparse storage; timings are machine-dependent.

The imported witness deliberately disables high-order rescue sampling for cells already empty at the bounded occupancy pass. This policy is reported as a solver setting. It avoids exponential work over generic BRep bounding-box voids; users must refine the lattice when features are thinner than the chosen occupancy sampling.

Curved-face loads, universal selection, adaptive refinement, nonlinear constitutive laws, contact, and formal convergence guarantees remain outside X1. The OCCT L-bracket was also audited: its imported analytic-ray containment currently returns `Unknown` for interior probes because of the kernel's bounded B-spline face-domain support. X1 does not paper over that kernel limitation with a mesh or ad-hoc STEP parser.
