# Aetheris finite-element architecture

M5 keeps engineering intent and numerical execution separate:

```text
Firmament analysis declaration
  -> typed, SI-normalized AnalysisIR
  -> exact BRep identity + CIR occupancy
  -> fixed Continuum lattice
  -> mechanics quadrature and Q1 displacement basis
  -> sparse K u = f / PCG
  -> displacement, strain, stress, reactions

same AnalysisIR
  -> conventional full-cell C3D8 verification mesh
  -> Abaqus .inp
```

Firmament owns body, homogeneous material assignment, semantic regions, constraints, loads, analysis kind, and requested result fields. Forge owns typed Template invocation and imported-resource binding. BRep owns exact face/topology identity. CIR owns occupied material. Continuum owns the fixed regular computational domain. `Aetheris.FEA` owns mechanics execution. Abaqus export is an interoperability backend, not the native Cut-cell formulation.

AnalysisIR contains no node, element, DOF, or sparse-matrix indices. `SemanticRegionBinding` preserves the source path, optional exact BRep face IDs, recognized imported faces, and declaration provenance. Solver indices first appear during mechanics discretization.

M5 supports native Box and centered box-minus-through-cylinder material domains. InlineStep Templates support canonical six-planar-face boxes through `ImportedStep`; unsupported imported topology fails with `firmament-analysis-inline-step-cir-unsupported` instead of silently approximating arbitrary BRep solids.

## Boundary ownership

An exterior exact fragment belongs to its sole material-side cell. If an exact fragment coincides with an interior lattice plane, the cell with the lexicographically smaller `(K,J,I)` index owns it. The comparison does not use the surface normal, so reversing orientation cannot change ownership. This provides one owner, no duplicate integration, and deterministic assembly.

M5B generalizes this rule to arbitrary-oriented exact planar fragments. The backend obtains `PlanarBoundaryDomain`, establishes deterministic `(P0,U,V,N)` coordinates, checks material outward direction by CIR classification, decomposes concave/holed trims with the existing planar utility, and clips local pieces against regular cells. `MechanicsBoundaryQuadraturePlan` is distinct from geometry and volume sampling.

The architecture deliberately does **not** reconstruct an arbitrary BRep as one magical global FRep/SDF. BRep remains exact boundary/topology authority, CIR remains occupied-material authority, regular cells provide computational support, and only the local portion needed by a cell is represented. `SurfaceMeshIR` is not mechanics boundary authority.

Curved-face pressure remains future work. Generic compound rotations can create extremely small supports; boundary integration stays exact, but robust immersed basis/Dirichlet treatment is the next mechanics blocker.
