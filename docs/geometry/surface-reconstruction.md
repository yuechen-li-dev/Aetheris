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
        ↓ structural support + admitted offset/normal residual fields
Residual-aware bounded patch evaluation
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

M3 uses exact Edmonds blossom matching after those geometric hard filters. Judgment continues to rank geometric alternatives; it does not replace the graph algorithm. Structural merge representation is a separate Judgment decision between base support and a sampled scalar offset grid after closest-point residual decomposition. Manifoldness, source-loop preservation, foldovers, and seam incidence remain hard invariants.

## Segmentation is not an atlas

> Geometric chart segmentation and quadrilateral surface parameterization are separate problems. M0 solved the former approximately; M2 introduces the latter.

The 333 Bunny M0 regions answer which triangles share a geometric fit. Only 78 have four connected boundary-side components; the others cannot truthfully populate `South/East/North/West`. `QuadAtlas` instead owns four ordered seam uses, four corners, disk topology, a non-folded rectangular parameterization, singularity evidence, and source-boundary correspondence. `PanelIr` is unchanged: an unresolved triangle remains a typed transition rather than an arbitrary N-sided Panel.

## Structural support and residual detail

M3 introduces reconstruction-owned `SurfaceResidualField`. Its scalar `BilinearScalarField` stores bounded piecewise-bilinear displacement along the base unit normal and has positional authority for reconstructed geometry. `BilinearNormalField` stores explicit target normals with `DifferentialInterpretationOnly` authority and cannot move a point. Both carry an authored parameter domain, evidence kind, provenance, error statistics, and an explicit seam policy; neither uses an image texture as authority.

Residual extraction calls the bounded Geometry closest-point query, retains the base `(u,v)`, and decomposes the source residual into signed normal and explicit tangential components. Scalar offset admission is rejected when tangential residual is too large. Corrected geometric normals come from displaced surface derivatives; a normal-only correction remains a separate interpreted normal.

`SurfaceMeshIR` now has a domain-neutral `ISurfaceMeshBoundedPatch` first-jet adapter. Quad-atlas lowering carries the actual authored `BoundaryPatch` through that adapter instead of manufacturing a plane support. An optional residual identity is retained on `SurfacePatch`. Shared corrected corners must agree within an explicit tolerance or lowering rejects them; it does not weld disagreements.

## M3 convergence status

M3 removes two M2 blockers completely. Deterministic blossom matching reduces Bunny transitions from 67 to one; because the source contains 69,451 triangles, one unmatched face is the mathematical lower bound for pair-only matching. The canonical native-bounded-patch `SurfaceMeshDocument` has 34,725 quads, one typed transition, zero internal cracks, five intentional holes, zero non-manifold edges, and passes validation. All strict quad cells use the real bounded patch carrier; the plane-proxy count is zero.

The residual model, extraction semantics, corrected evaluation, zero/shared seam policies, normal-only authority separation, merge/crease fixtures, and residual-aware lowering are implemented and tested. A bounded Bunny merge-pressure audit exactly projects the 1,000 smoothest best-neighbor proposals. It admits 516 disjoint two-Panel unions, giving 34,209 structural candidates in this deliberately partial pass; 24,392 proposals are explicitly deferred. Forty-six admitted unions choose residual grids. This is evidence that coarsening works, not a claim that the final compact atlas has been recovered.

Cross-field relaxation preserved the detected global index but admitted no Bunny smoothing iteration because the first proposal did not reduce the 1,603 detected defects under the hard acceptance rule. Parameter distortion likewise remains essentially M2-level. Therefore M3 is **Meaningful Progression**, not Success: the coarsened candidates are not installed as the canonical atlas, and the singularity count has not improved.

The next blocker is batched/cached projection and general multi-Panel four-sided parameterization that constructs one authoritative global seam network, including reconciled residual boundary samples. Without that, repeatedly merging local pairs would either be prohibitively slow or risk inventing incompatible chart boundaries. Canonical compact evidence is in [the Bunny M3 artifact directory](artifacts/bunny-m3/README.md).

## M2 convergence status (historical)

M2 recovers discrete cross-field singularity evidence by quarter-turn winding around ordered incident-face loops and consolidates adjacent same-sign candidates through source-edge adjacency. A deterministic field-scored matching of the triangle dual graph creates genuine four-sided charts; bounded alternating paths improve coverage without making a false maximum-matching claim. Straight source-edge authorities feed a Coons boundary patch, which reduces to a bilinear non-rational support and maps coherently to `[0,1]²` without foldovers.

On the canonical Bunny, this produces 34,692 strict four-boundary `PanelIr` objects and a 99.807% quad mesh. The complete mixed lowering has zero internal cracks, preserves all five intentional holes, has zero non-manifold edges, and passes `SurfaceMeshIR` validation. Sixty-seven source triangles remain explicit residual transitions. Consequently the milestone is **Meaningful Progression**, not full success: the canonical document still includes those transitions and is not yet exclusively `QuadAtlas → strict Panel network → SurfaceMeshIR`.

At M2, the next blockers were a blossom-capable layout optimizer and a richer `SurfaceMeshIR` support carrier for non-planar `BoundaryPatch` geometry. M3 closes both: the statement below is retained only as historical context for the M2 artifact.

Canonical compact evidence is in [the Bunny M2 artifact directory](artifacts/bunny-m2/README.md).

## M1 convergence status

M1 removes the M0 seam-data loss: all 845 Bunny chart adjacencies now resolve to authoritative ordered seam objects (858 connected traces after branch/component splitting), with shared junctions and canonical sampling. The resulting `SurfaceMeshDocument` has zero internal crack groups, five intentional open loops, and passes validation.

Strict jointly constrained `PanelIr` construction is not yet complete. The M0 chart decomposition produces general multi-sided chart boundaries rather than a quadrilateral atlas. Existing `BoundaryPatch` can enforce four recovered curves exactly, but applying it to these charts would require inventing or discarding sides. The next architectural step is therefore boundary-aligned quadrilateral chart topology and parameterization, not welding or a richer surface representation.

Canonical M1 evidence is in [the bunny M1 artifact directory](artifacts/bunny-m1/README.md).

## M0 baseline

The fitted chart interiors work, but chart boundaries are rectangular bounding domains rather than jointly optimized trim curves. The experimental structured output is therefore all quads but has one unreconciled crack group per chart seam and does not yet pass through canonical crack-free `SurfaceMeshDocument` validation. The next implementation must jointly parameterize/reconcile shared chart boundaries while preserving the Stanford Bunny's five open source loops. No implicit `FillHole`, `BridgeBoundary`, or `InferMissingSurface` policy is applied.

Canonical evidence is in [the bunny M0 artifact directory](artifacts/bunny-m0/README.md).
