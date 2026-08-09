# M5B semantic boundary quadrature

## Status

M5B reaches **meaningful progression**, not full milestone closure. Exact planar semantic-face loads now lower and integrate correctly under arbitrary rigid orientation. A baseline, a 45-degree single-axis case, and a compound 90/0/45-degree case solve with credible displacement/reaction behavior. A deliberately generic 15/20/45-degree probe proves that the remaining failure is no longer boundary integration: its force and moment remain exact while tiny occupied supports make the unstabilized immersed mechanics result unusable.

The next blocker is narrow and evidenced: arbitrary thickness tilts need a principled Cut-cell basis aggregation/ghost stabilization and an embedded Dirichlet treatment. A trial diagonal stiffness floor was rejected because it did not produce credible convergence.

## M5 audit and exact failure

M5 resolved `plate.face(+X)` or an imported recognized face to semantic identity, but mechanics then reparsed the path as one of six global `+/-X/Y/Z` tokens. Loads selected an outer lattice layer, used the corresponding lattice-face area, and constraints selected nodes by global coordinate equality. Pressure used an axis normal. No exact BRep plane or trim loop entered mechanics. Rotation therefore either selected no face or loaded/constrained the wrong generated support.

That path has been replaced; AnalysisIR did not gain orientation-specific fields.

## Boundary-lowering design

```text
semantic region / exact BRep face ID
  -> CIR-associated exact PlanarBoundaryDomain
  -> deterministic local frame P0,U,V,N
  -> existing planar triangulation for concavity and holes
  -> clip each local triangle by Cartesian cell-box half-spaces
  -> half-open fragment ownership by lexicographic (K,J,I)
  -> degree-3 triangle integration of restricted Q1 shape functions
  -> sparse nodal load contributions
```

`N` is not accepted from `SameSense`. The exact support normal is checked on both sides by CIR classification and flipped when occupied material requires it. `U` and `V` are exact-plane unit axes and remain stable under rigid transforms. Polygon shoelace area is exact for planar linear trims. Concave loops and holes use the existing deterministic planar triangulator only as local domain decomposition; no `SurfaceMeshIR` triangle is an authority.

`MechanicsBoundaryQuadraturePlan` records semantic path, boundary and exact BRep face IDs, frame, exact area, owned cell fragments, polygon vertices, Q1 quadrature points/weights, outward normals, ownership rule, material-side evidence, and provenance. Constant planar loads use a degree-three four-point triangle rule, which exactly integrates the Q1 trace. A resultant is converted to uniform traction by exact area and uses the identical path.

Loads own positive-area fragments exactly once. Dirichlet constraints intentionally use a related rule: every cell with an owned face fragment contributes its nearest face-side basis vertices, so integration half-openness cannot omit a constraint node. This remains a strong immersed-node approximation and is part of the generic-tilt limitation.

## Orientation evidence

All cases use byte-identical persisted AnalysisIR. Transformed studies preserve nominal source cell volume in backend lattice selection; this is not analysis intent.

| case | rotation X/Y/Z | DOFs | NNZ | Cut | PCG | max u | max cell-center VM | Kt | equilibrium | force error | moment error |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| baseline | none | 1,377 | 77,175 | 8 | 116 | 10.187 um | 16.168 MPa | 1.617 | 3.57e-7 N | 1.82e-12 N | 0 N m |
| single axis | 0/0/45 | 2,961 | 167,895 | 1,152 | 140 | 10.688 um | 33.764 MPa | 3.376 | 2.20e-7 N | 1.03e-11 N | 3.46e-13 N m |
| bounded compound | 90/0/45 | 3,024 | 166,392 | 3,179 | 117 | 11.646 um | 29.064 MPa | 2.906 | 2.88e-7 N | 6.43e-12 N | 3.42e-13 N m |
| generic failure probe | 15/20/45 | 3,948 | 223,110 | 6,072 | 869 | 2,985.7 um | 18,624.7 MPa | 1,862.5 | 3.03e-7 N | 1.90e-11 N | 9.52e-13 N m |

Every selected end face has exact area 0.001 m2. Area errors are 4.34e-19 m2 or less in the three report runs. Force/moment errors stay at floating summation scale even in the failed mechanics probe.

Single-axis displacement differs by 4.9% and the bounded compound proof by 14.3% from baseline. Cell-center stress is more orientation-sensitive because recovery samples different distances from the exact hole.

The canonical convergence rerun through exact Q1 boundary integration is:

| lattice | DOFs | PCG | max u | max cell-center VM | Kt |
|---|---:|---:|---:|---:|---:|
| 8x4x1 | 270 | 54 | 10.028 um | 11.086 MPa | 1.109 |
| 16x8x2 | 1,377 | 116 | 10.187 um | 16.168 MPa | 1.617 |
| 24x12x2 | 2,925 | 141 | 10.341 um | 32.903 MPa | 3.290 |

Displacement, equilibrium, and the Kirsch-scale fine stress trend remain credible. The change from M5's 31.549 MPa (Kt 3.155) is the expected effect of replacing the old nodal-style resultant distribution with exact Q1 trace integration; volume assembly and stress recovery are unchanged.

## Error accounting

- Geometry/domain integration: occupied-subcell midpoint error, unchanged from M5 and dominant when many cells are Cut.
- Boundary integration: independently measured exact area, resultant, and moment; about 1e-18 m2, 1e-11 N, and 1e-12 N m or better.
- Mechanics discretization: Q1 background basis, nearest-support strong Dirichlet selection, and cell-center recovery.
- Sparse solver: residual history and reaction equilibrium are separate; equilibrium stays around 1e-7 N.
- Tiny support: the generic failure has minimum fraction 3.05e-5 with 124/178/228 cells below 1/5/10%; this is the isolated blocker.

## Imported STEP, Forge, and Abaqus

The InlineStep test imports the canonical 10x8x6 mm six-planar-face STEP body, retains imported face IDs, applies 17 degrees Z plus 9 degrees X, fixes `imported.face(-X)`, applies a 100 N resultant to `imported.face(+X)`, and invokes `ForgeInvocation.Analyze()`. The native solve converges and the conventional imported-box Abaqus deck is nonempty. STEP lengths normalize from millimetres to metres.

The baseline native-plate deck remains deterministic. Rotated native-plate cells are conventional Cut candidates and are deliberately omitted rather than represented falsely as C3D8. No Abaqus installation or external solve is claimed.

## Stress, SPD, performance, and determinism

Kt is the maximum cell-center recovered von Mises value, not exact-hole-boundary stress. Its sampling distance changes with orientation. A small 270-DOF constrained case passes independent dense Cholesky in addition to symmetric assembly and positive-curvature PCG.

Baseline semantic resolution/projection/load/constraint timings were 1.67/18.49/35.06/4.17 ms; single-axis 2.95/17.94/35.35/4.61 ms; compound 2.71/17.52/34.89/4.50 ms. Volume quadrature and sparse assembly dominate rotation cost. Timings are not deterministic hashes.

Repeated plan construction preserves frame, fragments, ownership, area, and quadrature. The same AnalysisIR hash occurs in every orientation. Tests also assert deterministic Abaqus output and unique ownership.

## Remaining limitations and recommendation

Curved-face pressure, general imported BRep-to-CIR admission, and conforming rotated native-plate Abaqus meshing remain out of scope. Generic compound orientation is not yet a trustworthy mechanics solve because M5's unstabilized basis and strong node constraint policy are not robust to very small supports.

The first post-M5 milestone should implement deterministic small-support aggregation or a variationally justified ghost penalty, plus Nitsche or explicit multipoint enforcement for exact planar Dirichlet faces. The 15/20/45 probe is its motivating regression; the exact boundary force/moment path should remain unchanged.
