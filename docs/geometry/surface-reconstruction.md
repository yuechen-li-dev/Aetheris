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
        ↓ ordered chart-transition traces
Recovered Seam + Junction Network
        ↓ fixed boundary authorities
Boundary-constrained Panel Network
        ↓ sample every seam once
Canonical SurfaceMeshDocument
```

This path is distinct from `TriangleMesh → Continuum CutCells → FEA`. CutCells describe geometry embedded in a computational volume; reconstruction recovers structure on the surface itself.

## Authority and evidence

`TriangleSurfaceMesh` permits open and defective inputs. Its validator records non-finite vertices, invalid indices, degeneracy, duplicates, non-manifold edges, orientation disagreement, connected components, and open boundary loops without repairing them. Source connectivity supports local neighborhoods and provenance only. Charts are selected from normal, cross-field, and spatial evidence, and recovered patch fits carry sampled residuals.

The M0 patch fallback is a non-rational quadratic expression. It exposes exact first and second jets for the fitted expression, while its relationship to the input remains `SampledApproximation`. `PanelFactory.FromRecoveredQuadratic` lowers that expression exactly to a degree-2 non-rational Bezier support and creates a normal `PanelIr`; this does not upgrade the fit to source authority.

## Determinism

Load order, BVH splitting, lattice traversal, four-fold field transport, chart growth, fitting pivots, IDs, and tessellation order use stable ordering. Performance observations are inherently nondeterministic and are marked separately from deterministic geometry hashes in the artifact manifest.

## Seam authority law

> A chart adjacency has one reconstructed seam authority. Neighboring Panels reference that seam; they do not independently fit duplicate boundaries and weld them later.

`RecoveredSeam` distinguishes internal seams (exactly two chart sides) from source-open-boundary seams (one chart side). Its stable identity is derived from canonical chart ordering and ordered source provenance. It owns a normalized `[0,1]` parameter domain, an explicit direction mapping for each side, one recovered curve, and one sample/vertex identity sequence. `RecoveredJunction` similarly owns the point shared by all incident seam endpoints. The source transition edges provide ordered geometric and topological evidence; they do not become semantic CAD edges.

The canonical topology-conforming lowering evaluates interior vertices on the recovered chart support and seam vertices on `RecoveredSeam` exactly once. Both adjacent patches then refer to the same `SurfaceMeshVertex` IDs and `SharedEdgeSamplePlan`. This is construction-time sharing, not post-hoc positional welding. Source-open seam chains retain a single `FaceBoundaryUse`, and source loop correspondence remains explicit.

## Judgment use

Seam representation selection is a bounded `JudgmentEngine` decision between a line and a non-rational degree-one B-spline fallback. Before utility scoring, candidates must be finite, preserve authoritative endpoints, and (for a line) remain within `1e-4` of the source bounding-box diagonal. Utilities are:

```text
line   = -(position residual / tolerance) - 0.001
spline = -0.002 * source sample count
```

This makes the residual/complexity trade explicit and deterministic. Candidate scores, rejections, winners, and weights are persisted in `seam-fit-candidates.json` and `judgment-traces.json`. Exact trace ordering, junction identity, same/reversed side orientation, shared sampling, and source boundary classification use deterministic invariants instead of Judgment because no competing interpretation is needed.

## M1 convergence status

M1 removes the M0 seam-data loss: all 845 Bunny chart adjacencies now resolve to authoritative ordered seam objects (858 connected traces after branch/component splitting), with shared junctions and canonical sampling. The resulting `SurfaceMeshDocument` has zero internal crack groups, five intentional open loops, and passes validation.

Strict jointly constrained `PanelIr` construction is not yet complete. The M0 chart decomposition produces general multi-sided chart boundaries rather than a quadrilateral atlas. Existing `BoundaryPatch` can enforce four recovered curves exactly, but applying it to these charts would require inventing or discarding sides. The next architectural step is therefore boundary-aligned quadrilateral chart topology and parameterization, not welding or a richer surface representation.

Canonical M1 evidence is in [the bunny M1 artifact directory](artifacts/bunny-m1/README.md).

## M0 baseline

The fitted chart interiors work, but chart boundaries are rectangular bounding domains rather than jointly optimized trim curves. The experimental structured output is therefore all quads but has one unreconciled crack group per chart seam and does not yet pass through canonical crack-free `SurfaceMeshDocument` validation. The next implementation must jointly parameterize/reconcile shared chart boundaries while preserving the Stanford Bunny's five open source loops. No implicit `FillHole`, `BridgeBoundary`, or `InferMissingSurface` policy is applied.

Canonical evidence is in [the bunny M0 artifact directory](artifacts/bunny-m0/README.md).
