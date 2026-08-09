# Aetheris Continuum architecture

## Definition and authority

**CIR means Continuum Implicit Representation.** A CIR value represents an occupied continuum region, optionally partitioned into material or semantic regions, through spatial queries. Occupancy is fundamental. Signed distance, gradients, projection, and exact cell classification are optional capabilities.

CIR is not inherently an SDF, mesh, BRep, CSG tree, NURBS model, or topology graph. SDF is one CIR evaluation backend.

Authority is deliberately split:

| Representation | Owns | Does not silently own |
| --- | --- | --- |
| BRep | exact faces, edges, vertices, trims, adjacency, orientation, topology identity, STEP interchange, semantic boundary ownership, and provenance | occupied-field sampling or regular-cell state |
| CIR | occupied-space classification, continuum/material regions, implicit fields, cell occupancy, and geometry sampling | exact boundary topology or stable face/edge identity |
| SurfaceMeshIR | derived structured approximation of the BRep boundary for tessellation, DCC, STL, and possible candidate-cell acceleration | occupancy or exact-boundary authority |
| SDF backend | distance-like evaluation, analytic primitives, field composition, transforms, intervals, gradients where exposed, and sampling support | the definition of CIR |

Implicit representations remain because they are unusually strong for occupancy, material fields, distance/proximity cues, Boolean-like volumetric classification, regular lattices, and continuum analysis—not as a fallback attempt to replace exact BRep.

The dependency direction is `Aetheris.Continuum -> Aetheris.Kernel.Core`. Core supplies general math, tolerances, JudgmentEngine, and exact geometry/BRep types. Core must not reference Continuum.

## Pre-extraction audit (AETHERIS-CONTINUUM-M0)

This inventory was written before implementation movement. The eight files under `Aetheris.Kernel.Core/Cir` totaled 1,893 lines.

| Existing item | Location/capability | Class | M0 disposition |
| --- | --- | --- | --- |
| `CirNode`, `CirBounds`, point classification | `Core/Cir/CirNode.cs`, `CirAnalysis.cs` | A: CIR concept mixed with B: SDF contract | split broad CIR contracts from signed-distance backend; retain narrow legacy names only in the backend |
| analytic box, cylinder, cone, sphere, torus | `Core/Cir/CirNodes.cs` | B: SDF backend | move without rewriting |
| union, subtraction, intersection, transforms | `Core/Cir/CirNodes.cs` | B: SDF field/composition backend | move without rewriting |
| dense volume sampler | `CirNodes.cs` | A/B: continuum sampling tied to SDF nodes | move; new lattice sampling becomes representation-neutral |
| tape instructions, payloads, lowering, point evaluation | `Core/Cir/CirTape.cs` | B: SDF backend | move intact under the SDF backend |
| conservative field intervals and region classification | `CirTape.cs` | B with A-facing capability | move; expose through optional cell-bounds classification capability |
| adaptive volume estimator | `CirAdaptiveVolumeEstimator.cs` | D: continuum experiment tied to SDF tape | move to Continuum; preserve regression behavior |
| `CirRegionPlanner` using `JudgmentEngine` | `CirRegionPlanner.cs` | D: continuum strategy selection | move to Continuum; it correctly uses explicit admissibility/scoring |
| mirror admission/provenance metadata | `CirMirrorMetadata.cs` | D/E: AIR consumer bridge and experimental policy | move out of Core; preserve as internal compatibility surface |
| convex prismatic mirror and top-view map | `Cir/Mirrors/CirConvexPolyhedronMirror.cs` | D: AIR/prismatic consumer-specific implicit mirror | move out of Core; keep dependency on Core prismatic/math types |
| `AirCirMirrorAdapter` | `Core/Air/AirCirMirrorAdapter.cs` | D: misplaced AIR-to-CIR consumer adapter | move with Continuum so Core does not depend on Continuum |
| `Point3D`, `Vector3D`, `Transform3D` | `Core/Math` | C: general kernel utility | remain in Core |
| `ToleranceContext` | `Core/Numerics` | C: general kernel utility | remain in Core |
| `JudgmentEngine` | `Core/Judgment` | C: general bounded-strategy utility | remain in Core |
| Firmament CIR lowering | `Kernel.Firmament/Lowering/FirmamentCirLowerer.cs` | D: consumer/compiler bridge | remain in Firmament and reference Continuum |
| Firmament analysis/materializers/recovery | `Kernel.Firmament/Analysis`, `Materializer`, and related execution | D plus E: bounded consumers and historically overextended CIR-to-BRep experiments | remain consumers; generic rematerialization redesign is out of M0 |
| CLI | no direct CIR implementation dependency found; CIR data is exposed through Firmament/AIR summaries | D: transitive consumer | no new direct authority path |
| Core CIR tests | 11 focused files under `Kernel.Core.Tests/Cir` plus AIR mirror tests | tests owned by old location | move CIR/SDF regression ownership to `Aetheris.Continuum.Tests`; leave genuinely Core tests in Core |
| Firmament/FrictionLab tests | lowering, analysis, recovery, and experimental mirror coverage | consumer regression tests | retain in consumer test assemblies and update references |
| serialization/debug | no general CIR serializer; diagnostics are records/strings and CLI summaries | E: incomplete | add deterministic lattice JSON diagnostics in Continuum; do not invent persistence format |
| gradients/normals | no general public CIR gradient contract found; SDF tape provides values/intervals only | E: missing capability seam | add optional capability; finite-difference SDF adapter may implement it |
| generic BRep -> CIR | none; existing paths primarily lower constructive Firmament/AIR to CIR or narrowly rematerialize CIR to BRep | E: absent and major | explicitly not required for M0 |

### Old architecture problems

The public `CirNode.Evaluate` contract made CIR look synonymous with a scalar signed-distance evaluator. Continuum implementation, SDF bytecode, adaptive sampling policy, AIR mirror provenance, and prismatic consumer experiments all lived in Core. This made Core appear to own an implicit-modeling authority while Firmament also used CIR in the opposite direction for narrow BRep recovery. The assembly boundary did not communicate the now-established authority split.

### BRep and boundary identity

A cut cell may carry a `BoundaryReference` with source representation, stable source identifier, optional exact BRep face identifier, and semantic region. This is an attachment hook, not inferred correspondence. M0 analytic fixtures use analytic boundary identifiers. A future bounded BRep-to-CIR bridge may attach exact face references only when it can prove the correspondence.

No generic BRep-to-CIR conversion is required in M0. Imported STEP/BRep bodies cannot acquire an implicit mirror merely because sampling would be convenient.

### Geometry sampling versus solver quadrature

`GeometrySamplePlan` answers what portion of a cell is occupied and which boundaries are candidates. Its deterministic subcell patterns may later grow from 2x2x2 to 4x4x4 or hierarchical reuse.

Solver quadrature is a future, separate artifact that will choose integration locations and weights for PDE terms. Geometry samples are not quadrature points and M0 introduces no solver state.

### Boundary offset map seam

A future `BoundaryOffsetMap` is a compact local, derived cache for a cut cell: source boundary reference, local frame, offset/normal samples, parameter bounds, and approximation/error metadata. It is analogous to a rendering displacement/normal map only as a storage intuition. It never becomes exact geometry authority; BRep and/or CIR remain authoritative.

### SurfaceMeshIR relationship

SurfaceMeshIR may later accelerate candidate boundary-cell detection, provide structured samples and normal/parameter hints, or propagate semantic regions. Every result must still be confirmed against occupancy authority. SurfaceMeshIR does not classify material by itself.

## Oct Continuum Boundary Experiment audit

Local evidence was available in `C:/Users/yuech/source/repos/oct/Experiments/ContinuumComputabilityBoundary` and was read without modifying that repository.

Ideas that transfer cleanly:

- M4's plain Cartesian specification and deterministic row-major indexing;
- M11's fixed, deterministic MSAA-style coverage samples;
- M12's separation of a cheap base pattern from later selective higher-rate boundary sampling;
- M15's conclusion that coverage and signed-distance narrow bands are related but distinct capabilities;
- explicit metrics and fixed ordering for reproducibility.

M1 source review corrected an important nuance: M12's quarter-point 2x2 and eighth-offset 4x4 patterns were disjoint, so the Oct experiment recomputed its upgraded cells and did not demonstrate hierarchical sample reuse. Aetheris retains those regular layouts for comparison and adds a separate nested base whose coordinates are a subset of regular 4x4.

Ideas redesigned for Aetheris:

- Oct's site/cell masks become typed 3D `CellIndex`, bounds, and `Inside/Outside/Cut` records;
- sampled coverage is used only in Cut cells after analytic/interval classification where available;
- SDF is an optional backend/cue rather than the continuum definition;
- refinement selection and solver transfer/flux structures are deferred.

Ideas made obsolete or inappropriate by Aetheris authority:

- sampled masks must not stand in for exact BRep boundary identity;
- coverage gradients need not pretend to be exact normals when CIR/SDF or BRep can supply them;
- solver coupling, flux, and balance records from later Oct milestones do not belong in this geometry-only substrate.

## Deferred numerical distinctions

Geometric refinement means more deterministic samples, improved offset maps, or a higher MSAA rate without changing lattice cells. Physical refinement means smaller cells or AMR. Boundary curvature alone does not force AMR.

Tiny active fractions will eventually require numerical policy such as cell aggregation, basis aggregation, redistribution, or a minimum-active-fraction rule. That is solver stabilization, not M0 geometry, and no such policy is embedded in `CutCell`.
