# Structured surface recovery

`Aetheris.Reconstruction` treats a triangle surface as sampled geometric evidence, not as future semantic topology.

```text
TriangleSurfaceMesh
        ↓ validation and proximity evidence
Adaptive Surface Analysis
        ↓ local differential estimates
Unoriented Tangent Cross Field
        ↓ discrete quarter-turn winding
Cross-field Singularities
        ↓ deterministic field-aligned layout candidates
Separatrix / Quad Layout Graph
        ↓ four ordered seams and corners
QuadAtlas
        ↓ transfinite [0,1]² parameterization
Strict BoundaryPatch Panel Network
        ↓ shared seam sampling
SurfaceMeshIR
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

M2 also uses `JudgmentEngine` when one triangle has multiple admissible dual-graph routes into a four-sided chart. Hard rejection establishes two distinct faces, four distinct corners, a closed disk boundary, finite positive area, and a non-folded center before utility is considered. Admissible routes use dimensionless cross-field alignment (0.50), boundary shape quality (0.35), and source-normal compatibility (0.15). Boundary-loop preservation, seam incidence, four-side closure, and foldover rejection remain deterministic invariants; scoring cannot override them.

## Segmentation is not an atlas

> Geometric chart segmentation and quadrilateral surface parameterization are separate problems. M0 solved the former approximately; M2 introduces the latter.

The 333 Bunny M0 regions answer which triangles share a geometric fit. Only 78 have four connected boundary-side components; the others cannot truthfully populate `South/East/North/West`. `QuadAtlas` instead owns four ordered seam uses, four corners, disk topology, a non-folded rectangular parameterization, singularity evidence, and source-boundary correspondence. `PanelIr` is unchanged: an unresolved triangle remains a typed transition rather than an arbitrary N-sided Panel.

## M2 convergence status

M2 recovers discrete cross-field singularity evidence by quarter-turn winding around ordered incident-face loops and consolidates adjacent same-sign candidates through source-edge adjacency. A deterministic field-scored matching of the triangle dual graph creates genuine four-sided charts; bounded alternating paths improve coverage without making a false maximum-matching claim. Straight source-edge authorities feed a Coons boundary patch, which reduces to a bilinear non-rational support and maps coherently to `[0,1]²` without foldovers.

On the canonical Bunny, this produces 34,692 strict four-boundary `PanelIr` objects and a 99.807% quad mesh. The complete mixed lowering has zero internal cracks, preserves all five intentional holes, has zero non-manifold edges, and passes `SurfaceMeshIR` validation. Sixty-seven source triangles remain explicit residual transitions. Consequently the milestone is **Meaningful Progression**, not full success: the canonical document still includes those transitions and is not yet exclusively `QuadAtlas → strict Panel network → SurfaceMeshIR`.

The next blocker is a globally complete cross-field layout optimizer (in matching terms, blossom-capable/global rerouting rather than longer local augmenting paths), followed by a richer `SurfaceMeshIR` support carrier for non-planar `BoundaryPatch` geometry. Current `SurfaceMeshIR` stores a plane support proxy for each non-planar strict Panel cell and records that approximation in `PlanarPlannerPath`; the strict `PanelIr` remains the geometry authority.

Canonical compact evidence is in [the Bunny M2 artifact directory](artifacts/bunny-m2/README.md).

## M1 convergence status

M1 removes the M0 seam-data loss: all 845 Bunny chart adjacencies now resolve to authoritative ordered seam objects (858 connected traces after branch/component splitting), with shared junctions and canonical sampling. The resulting `SurfaceMeshDocument` has zero internal crack groups, five intentional open loops, and passes validation.

Strict jointly constrained `PanelIr` construction is not yet complete. The M0 chart decomposition produces general multi-sided chart boundaries rather than a quadrilateral atlas. Existing `BoundaryPatch` can enforce four recovered curves exactly, but applying it to these charts would require inventing or discarding sides. The next architectural step is therefore boundary-aligned quadrilateral chart topology and parameterization, not welding or a richer surface representation.

Canonical M1 evidence is in [the bunny M1 artifact directory](artifacts/bunny-m1/README.md).

## M0 baseline

The fitted chart interiors work, but chart boundaries are rectangular bounding domains rather than jointly optimized trim curves. The experimental structured output is therefore all quads but has one unreconciled crack group per chart seam and does not yet pass through canonical crack-free `SurfaceMeshDocument` validation. The next implementation must jointly parameterize/reconcile shared chart boundaries while preserving the Stanford Bunny's five open source loops. No implicit `FillHole`, `BridgeBoundary`, or `InferMissingSurface` policy is applied.

Canonical evidence is in [the bunny M0 artifact directory](artifacts/bunny-m0/README.md).
