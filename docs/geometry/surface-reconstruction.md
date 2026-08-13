# Structured surface recovery

`Aetheris.Reconstruction` treats a triangle surface as sampled geometric evidence, not as future semantic topology.

```text
TriangleSurfaceMesh
        ↓ validation and proximity evidence
Adaptive Surface Analysis
        ↓ local differential estimates
Unoriented Tangent Cross Field
        ↓ geometric objective
Chart Network
        ↓ bounded fitting
Recovered PanelIr Network
        ↓ shared-seam tessellation (incomplete in M0)
SurfaceMeshIR
```

This path is distinct from `TriangleMesh → Continuum CutCells → FEA`. CutCells describe geometry embedded in a computational volume; reconstruction recovers structure on the surface itself.

## Authority and evidence

`TriangleSurfaceMesh` permits open and defective inputs. Its validator records non-finite vertices, invalid indices, degeneracy, duplicates, non-manifold edges, orientation disagreement, connected components, and open boundary loops without repairing them. Source connectivity supports local neighborhoods and provenance only. Charts are selected from normal, cross-field, and spatial evidence, and recovered patch fits carry sampled residuals.

The M0 patch fallback is a non-rational quadratic expression. It exposes exact first and second jets for the fitted expression, while its relationship to the input remains `SampledApproximation`. `PanelFactory.FromRecoveredQuadratic` lowers that expression exactly to a degree-2 non-rational Bezier support and creates a normal `PanelIr`; this does not upgrade the fit to source authority.

## Determinism

Load order, BVH splitting, lattice traversal, four-fold field transport, chart growth, fitting pivots, IDs, and tessellation order use stable ordering. Performance observations are inherently nondeterministic and are marked separately from deterministic geometry hashes in the artifact manifest.

## M0 limitation

The fitted chart interiors work, but chart boundaries are rectangular bounding domains rather than jointly optimized trim curves. The experimental structured output is therefore all quads but has one unreconciled crack group per chart seam and does not yet pass through canonical crack-free `SurfaceMeshDocument` validation. The next implementation must jointly parameterize/reconcile shared chart boundaries while preserving the Stanford Bunny's five open source loops. No implicit `FillHole`, `BridgeBoundary`, or `InferMissingSurface` policy is applied.

Canonical evidence is in [the bunny M0 artifact directory](artifacts/bunny-m0/README.md).
