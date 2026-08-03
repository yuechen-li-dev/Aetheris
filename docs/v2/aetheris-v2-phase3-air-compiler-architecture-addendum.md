# PHASE3-ARCH-A0 — AIR compiler architecture investigation addendum

Status: investigation result; not production doctrine  
Date: 2026-08-03

This addendum tests, rather than assumes, the proposed pipeline:

```text
Firmament V2 -> Feature AIR -> Construction AIR -> BRep -> STEP AP242
```

No parser, production AIR, BRep, Boolean, STEP, PMI, or Forge behavior is changed by this milestone.

## 1. Executive finding

**Verdict: architecture mostly confirmed with corrections.**

The separation of source intent, semantic feature intent, constructive geometry, realized topology, and serialization is the right direction. Existing code provides strong evidence for it:

- parser-backed trace code already distinguishes `CreateBox` Feature AIR from rectangle-profile extrusion Construction AIR;
- `AirHoleFeature` preserves semantic hole intent before bounded profile-stack materialization;
- `BrepExtrude`, `BrepRevolve`, `LineArcProfileExtrudeEmitter`, and `PrismaticSectionTransitionEmitter` are reusable constructive emitters;
- `PrismaticTopFaceLoopChamferPrototype` lowers a bounded semantic-looking loop request to a three-section prismatic construction and emits a genuinely changed BRep;
- `Step242Exporter` serializes a completed `BrepBody` and does not need feature semantics.

The current implementation, however, does **not** yet implement that pipeline consistently. The normal compiler path lowers to `FirmamentPrimitiveLoweringPlan`, whose `FirmamentLoweredBooleanKind` mixes Boolean operations with chamfer, fillet, and draft, and `FirmamentPrimitiveExecutor` immediately constructs or transforms `BrepBody` instances. Most `Aetheris.Kernel.Core.Air` nodes and `AirBRepPlan` are trace/lab envelopes, not the production authority. `AirBRepPlan` is currently planned beside emission, not consumed by the emitter.

Three corrections are required:

1. Insert an explicit **BRepPlan/topology-realization plan** between Construction AIR and `BrepBody`. Construction dependencies and deterministic topology identity are different concerns.
2. Make Construction AIR a small geometry/region language, not a second feature catalog. In particular, `TopFaceLoopChamfer` is feature intent or a route specialization; it is not a geometric opcode.
3. Do not make offset/intersection/trim the universal chamfer lowering. For history-known prismatic geometry, profile editing or section transition is smaller, more exact, and already proven. Offset/intersection/trim is a useful dependency model for no-history support-surface cases, but its current prototype is not a valid body materializer.

The recommended high-level architecture is therefore:

```text
Firmament V2
  -> Feature AIR
  -> Construction AIR dependency DAG
  -> BRepPlan (deterministic topology roles, identity, orientation, tolerances)
  -> BRep
  -> STEP AP242

Optional side channels:
  Construction AIR/BRepPlan -> admitted CIR evaluation mirror
  imported STEP -> BRep -> explicitly admitted recovered feature/construction intent
```

## 2. Current pipeline as implemented

### 2.1 Normal Firmament compiler path

The production path is currently:

```text
Firmament source
  -> FirmamentTopLevelParser.Parse
  -> validators
  -> FirmamentPrimitiveLowerer.Lower
  -> FirmamentPrimitiveLoweringPlan
  -> FirmamentPrimitiveExecutor.Execute
       -> BrepPrimitives / BrepExtrude / BrepRevolve
       -> bounded BrepBoolean builders
       -> BrepBoundedChamfer / BrepBoundedFillet / BrepBoundedDraft
  -> FirmamentCompilationArtifact
  -> FirmamentStepExporter.SelectExportBody
  -> Step242Exporter.ExportBody
  -> STEP text
```

The call path is implemented by:

- `Aetheris.Kernel.Firmament/FirmamentCompiler.cs` — parse, validate, lower, execute, and package the compilation artifact;
- `Aetheris.Kernel.Firmament/Lowering/FirmamentPrimitiveLowerer.cs` and `FirmamentPrimitiveLoweringPlan.cs` — lower primitives and operations;
- `Aetheris.Kernel.Firmament/Execution/FirmamentPrimitiveExecutor.cs` — construct and compose BReps;
- `Aetheris.Kernel.Firmament/FirmamentStepExporter.cs` — choose the last executed geometric body and call the STEP backend;
- `Aetheris.Kernel.Core/Step242/Step242Exporter.cs` — serialize topology, geometry, and PMI.

`FirmamentPrimitiveLoweringPlan` is the closest production IR, but it is neither a clean Feature AIR nor Construction AIR. Its primitive records retain semantic-ish kinds such as box and cylinder, while `FirmamentLoweredBooleanKind` contains `Add`, `Subtract`, and `Intersect` beside `Draft`, `Chamfer`, and `Fillet`. Its tool payload remains `RawFields`/`RawValue`. The executor stores completed bodies by feature ID and uses those bodies as the operands for later operations.

This means the normal path currently uses `BrepBody` as modeling state. That is understandable for the existing bounded implementation, but it violates the proposed separation when treated as the future general architecture.

### 2.2 Firmament V2 build/export path

`FirmamentBuildAndExport.ExportSource` first invokes `FirmamentV2Parser`. It then selects among several specialized paths:

```text
FirmamentV2Parser
  -> DFM enforcement
  -> one of:
       semantic-hole direct materialization
       controlled side-hole materialization
       inline-STEP replacement/import path
       primitive bridge to FirmamentPrimitiveExecutor
  -> Step242Exporter
```

The semantic-hole lane is the strongest production-adjacent Feature AIR evidence:

```text
FirmamentV2SemanticHoleDecl
  -> FirmamentV2SemanticHoleLowering
  -> AirHoleFeature
  -> AirHoleSimpleShaftMaterializer / AirHoleCompositeMaterializer
  -> profile-stack/bounded BRep composition
  -> BrepBody
  -> Step242Exporter
```

This path preserves semantic intent longer than the legacy lowering plan, but the materializers still use specialized profile-stack and bounded BRep composition rather than a shared Construction AIR graph.

### 2.3 Trace/lab AIR path

The `Aetheris.Kernel.Core/Air` model is currently an internal evidence envelope:

- `AirNodeKind` contains `ProfileExtrude`, `PrismaticSectionTransition`, and `TopFaceLoopChamfer`;
- `AirWrappers` invokes existing emitters and summarizes results;
- `AirRouteSelector` classifies bounded routes;
- `AirBRepPlan` predicts stable topology elements and roles;
- `AirCirMirrorAdapter` optionally creates an analysis mirror;
- `aetheris trace` composes these independently for reporting.

For the top-face loop trace, `AirTraceReportBuilder.BuildTopFaceLoopChamfer` first invokes `AirTopFaceLoopChamferWrapper`, which already emits the BRep, and then separately invokes `AirTopFaceLoopChamferBRepPlanner`. Thus the current plan validates parity with the emitter but does not drive it. This is valuable evidence, not yet a compiler backend pipeline.

The enum itself exposes the present layer leak: `ProfileExtrude` and `PrismaticSectionTransition` are construction forms, while `TopFaceLoopChamfer` is semantic feature intent.

### 2.4 Where BRep is output versus workspace

Acceptable BRep emission paths:

- `BrepExtrude.Create` builds final topology from a 2D polyline and frame;
- `BrepRevolve.Create` builds final analytic topology from a line profile and axis;
- `LineArcProfileExtrudeEmitter` emits planes/cylinders and loops from profile curves;
- `PrismaticSectionTransitionEmitter` emits final split-preserving topology from ordered polygon sections and correspondence;
- `PrismaticTopFaceLoopChamferPrototype` constructs its final body from sections without first emitting a sharp body.

Paths where BRep is still the modeling workspace:

- `FirmamentPrimitiveExecutor` feeds completed `BrepBody` operands to `BrepBoolean.Union`, `Subtract`, and `Intersect`;
- production chamfer calls recognize a completed source BRep and route to `BrepBoundedChamfer`;
- trusted polyhedral chamfer paths read completed topology, rebuild replacement face cycles, then create a new BRep;
- side-hole experimental integration models face splits and attachments against parent BRep state.

Direct BRep construction is not itself a violation. The concern is that upstream semantic operations have no reusable construction representation before they reach those fixture-bounded BRep algorithms.

## 3. Recommended compiler pipeline

### Firmament V2 — auditable source language

Owns authored modeling/manufacturing intent, names, units, process concepts, semantic references, and PMI declarations.

Must not expose entity numbers, `FaceId`, `EdgeId`, coedge order, STEP IDs, trim-curve IDs, or materializer-specific topology tokens.

### Feature AIR — semantic HIR

Owns bodies, features, semantic selections, feature composition, end conditions, manufacturing meaning, and provenance. Examples supported by current evidence include `CreateBox`, semantic hole families, chamfer intent, and semantic Boolean intent.

Feature AIR may say `Subtract(base, hole)` or `Chamfer(body, FaceBoundary(top), EqualDistance(d))`. It must not prescribe loop/coedge creation or STEP ordering. A selection may refer to stable semantic roles or construction-history outputs, not arbitrary emitted topology IDs.

### Construction AIR — immutable geometric construction DAG

Owns profiles, frames, extents, sweeps/revolutions, ordered section transitions, support surfaces, derived intersection curves, trimmed regions, and geometric region composition. Nodes should reference their dependencies explicitly and immutably.

“Decorator” is not the preferred vocabulary. `OffsetSurface`, `SurfaceIntersection`, and `TrimmedRegion` are dependency nodes. An offset is not merely presentation wrapped around a surface; intersections depend on two support surfaces and tolerances, and trims depend on support-surface parameterization plus derived curves and region sense.

Construction AIR must not contain `TopFaceLoopChamfer`, `Countersink`, or other growing feature names merely to dispatch specialized code. Feature lowering should choose reusable construction forms such as profile rewrite, section transition, revolve, or an explicit support-surface dependency graph.

### BRepPlan — topology realization plan

Owns deterministic planned IDs, element roles, adjacency, loop/coedge ordering, face orientation, split/merge policy, seam policy, tolerance use, and expected topology counts. It is the bridge from geometric regions to topology.

The existing `AirBRepPlan` is good evidence for this layer, but it must eventually become an emitter input or be generated by the same authoritative lowering used by the emitter. Parallel prediction is insufficient because the plan and emitted body can diverge.

### BRep — realized LIR

Owns vertices, edges, curves, parameter intervals, faces, support surfaces, loops, coedges, shells, bodies, adjacency, and orientation. BRep-level recognition, intersection, and stitching remain legitimate backend algorithms. BRep should not be the only durable representation of feature intent.

### STEP AP242 — serialized target

Owns deterministic entity serialization and AP242 representation/PMI mapping. Boolean or chamfer semantics must not be rediscovered in the writer. `Step242Exporter.ExportBody` is already close to the recommended boundary.

### CIR — optional evaluation mirror, not a pipeline stage

Existing AIR-to-CIR adapters explicitly disclaim face identity, loop identity, topology parity, and BRepPlan-role parity. Keep CIR as an admitted evaluation/analysis side channel unless a separate exact materializer is proven.

## 4. Boolean placement

### Recommendation

- **Semantic Boolean intent:** Feature AIR.
- **Geometric composition:** Construction AIR region combinators where the operation remains the clearest exact representation.
- **Boundary realization:** BRepPlan/materializer, potentially using bounded BRep intersection/classification algorithms internally.
- **Serialization:** STEP receives only the resolved BRep and semantic PMI associations.

Boolean nodes should survive into Construction AIR when they still represent real unresolved region composition. They should be normalized earlier only when a more specific exact construction is known. Examples:

- a box is better lowered directly to rectangle extrusion than retained as six half-space intersections;
- a history-known through-hole may lower to an outer profile with an inner circular loop or a bounded profile stack, avoiding an unnecessary general 3D Boolean;
- independent or interacting features may legitimately remain `RegionDifference` until a bounded materializer resolves them.

Fully evaluating Booleans in Feature AIR would force the semantic layer to understand tolerances, surface intersections, classification, and topology. Fully deferring semantic meaning to `BrepBoolean` loses construction provenance and makes the completed BRep the primary feature graph. The split above avoids both failures.

### Current match

The current implementation only partially matches. `FirmamentPrimitiveLoweringPlan` preserves `Add`/`Subtract`/`Intersect`, but `FirmamentPrimitiveExecutor` resolves them against completed BRep bodies. `SafeBooleanComposition` preserves some bounded analytic provenance, and hole materializers can use profile-stack specialization, but there is no general Construction AIR region graph. No general Boolean support should be claimed.

## 5. Construction AIR vocabulary

The smallest useful families supported by current code or directly required by the bounded chamfer are:

| Family | Minimal form | Evidence and boundary |
|---|---|---|
| Profiles | `PlanarProfile(outerLoop, innerLoops, frame)` | `PolylineProfile2D`, line/arc profile loops, and profile-stack regions exist. “RectangleProfile” and “CircleProfile” are conveniences, not foundational opcodes. |
| Linear construction | `LinearSweep(profile, vector/extent)` | `BrepExtrude` and `LineArcProfileExtrudeEmitter` prove polygon and line/arc extrusion. The emitter may choose exact plane/cylinder surfaces rather than a generic STEP linear-extrusion surface. |
| Revolved construction | `RevolutionSweep(profile, axis, angle)` | `BrepRevolve` exactly handles its bounded full-revolution two-point profile subset. Partial and general profiles remain unsupported. |
| Section construction | `SectionTransition(orderedSections, correspondence, splitPolicy)` | `PrismaticSectionTransitionEmitter` is the strongest reusable 3D construction primitive for current chamfer work. Calling this only `RuledSurface` would lose whole-body section and correspondence semantics. |
| Support surfaces | analytic plane/cylinder/cone/sphere/torus and supported swept/spline surfaces | `SurfaceGeometry` already stores these exact families. A generic `PlaneSurface` construction node is useful only where support-surface derivation is actually required. |
| Surface derivation | `OffsetSurface(source, signedDistance)`, `SurfaceIntersection(a, b, tolerance)` | Required for a later no-history surface route; not currently implemented as construction nodes. Analytic plane offsets preserve exactness. General offsets need family-specific exactness/admissibility. |
| Trimmed regions | `TrimmedRegion(supportSurface, boundaryCurves, sense)` | Prefer “region” over `TrimSurface`: the support surface remains unbounded and topology bounds it. Existing BRep binds faces to support surfaces and edges to 3D curves, but has no general per-face pcurve dependency model. |
| Region composition | `RegionUnion`, `RegionDifference`, `RegionIntersection` | Needed as an explicit unresolved geometric composition family; current code only has production BRep Boolean execution and bounded safe-composition metadata. |
| Topology realization | `BRepPlan`/`ShellAssembly` | Shell/loop/coedge identity, orientation, and ordering belong in the realization plan, not in general geometric nodes. |

For the first top-face-loop chamfer, the minimal instruction set is smaller than the candidate list in the milestone prompt:

```text
PlanarProfile
SectionTransition(three sections, explicit correspondence, preserve splits)
BRepPlan / ShellAssembly
```

`PlaneSurface`, `OffsetSurface`, `SurfaceIntersection`, `TrimmedRegion`, `RegionDifference`, and a standalone `RuledSurface` are not required for that construction-history-aware route. The emitter derives planar support faces from section quads.

For a future no-history straight planar single-edge route, the likely minimum adds analytic support-surface references, signed offsets, plane/plane intersections, final face-region boundaries, and endpoint/corner resolution. That is a different lowering strategy selected from the same Feature AIR chamfer intent.

### Sweep and ruled-surface conclusion

Sweeps and section transitions are useful foundational construction bytecode, but “sweeps and ruled surfaces” is too narrow as the entire bytecode thesis.

- A box maps naturally to a planar rectangular profile plus linear sweep. Parser-backed trace code already describes this, and `BrepExtrude`/`LineArcProfileExtrudeEmitter` prove emission.
- A cylindrical through-hole can map to a swept circular inner loop or a bounded region difference. Current semantic-hole/profile-stack paths support this for bounded box hosts; they do not prove general region difference.
- Existing revolution artifacts map cleanly to a bounded `RevolutionSweep`, although `BrepRevolve` only admits full revolution of a two-point line profile today.
- `SurfaceGeometry` and STEP import/export support exact `LinearExtrusionSurface` and `SurfaceOfRevolutionSurface` artifacts.
- Degree-1 B-spline classification recognizes exact ruled candidates for import/export, but it is classification, not a reusable ruled-surface constructor.
- `AirRuledTransitionFrustumLab` does not implement a ruled emitter: `CreateRuledTransitionCandidate` calls the same `BrepRevolve.Create` as its baseline.
- `PrismaticSectionTransitionEmitter` creates planar quad faces directly. It is a reusable bounded section-transition constructor, not a general rail-to-rail ruled-surface implementation.

Therefore use `LinearSweep`, `RevolutionSweep`, and `SectionTransition` as the current proven families. Add a generic `RuledSurface(railA, railB)` only when an actual constructor, parameterization contract, trimming contract, and exactness tests exist.

## 6. Chamfer lowering result

### 6.1 Top-face boundary-loop prototype

`PrismaticTopFaceLoopChamferPrototype` is best classified as:

- a **bounded reusable chamfer materializer** for a strict rectangular-prism/top-cap domain;
- a **specialized lowering of a general section-transition recipe**;
- evidence for Construction AIR profile/section operations;
- not evidence that offset/intersection/trim is required for all chamfers.

Its exact path is:

```text
FaceLoopChamferSelection + dimensions + equal distance
  -> validate top cap / outer / closed / ordered four-edge loop
  -> sections at z=0, z=height-distance, z=height
  -> inset the top rectangle by distance on all four sides
  -> identity correspondence
  -> PrismaticSectionTransitionEmitter
  -> split-preserving planar BRep
  -> optional STEP smoke
```

This is constructive final-boundary emission. It does not consume a source `BrepBody`, discover actual adjacent support surfaces, offset them, intersect them, trim old faces, or mutate topology.

The current exact domain is:

```text
axis-aligned rectangular prismatic body
history-known planar top cap
outer closed ordered four-edge loop
uniform symmetric equal-distance chamfer
distance < width/2, depth/2, and height
```

That is the correct first production chamfer domain because it has real changed topology, bounded selection semantics, invalid/deferred cases, deterministic section correspondence, parameter variations, STEP evidence, and no dependence on arbitrary BRep surgery. The production milestone should describe it exactly; it must not claim arbitrary rectangular prisms with unknown history, arbitrary planar face loops, or general edge chains.

### 6.2 Single-edge AirChamfer prototype

`AirChamferGeometryArtifactLab` is evidence for a local dependency sketch, not a body materializer. It computes two line segments by adding normalized face normals times distance to the target-edge endpoints, joins their endpoints, and records a planar four-vertex strip. It does not:

- derive signed inward directions from body orientation;
- compute the required original-plane/offset-plane intersections;
- consume the source BRep geometry;
- trim the adjacent face regions;
- resolve endpoint/corner patches;
- assemble the artifact into a shell.

The subsequent `AirChamferClosedWitnessLab.BuildWitness` ignores both `plan` geometry (except edge length) and `artifact`, and calls `BrepPrimitives.CreateBox(8, 8, edgeLength)`. `AirChamferControlledTopologyGraftLab` and `AirChamferRealBodyPrototype` then report that ordinary box as if it contained one chamfer face, two trimmed faces, and two transition edges. The `SourceBody` input to `AirChamferRealBodyPrototype` is not used to build the candidate.

The controlled CLI experiment confirms the mismatch:

| Evidence | Claimed summary | Ground-truth `aetheris analyze` |
|---|---|---|
| `experimental airchamfer-cube` | 6 faces, 12 edges, 8 vertices, **1 chamfer face**, original edge replaced | enclosed `8 x 8 x 6` box, 6 planar faces, 12 edges, 8 vertices |
| production `m5a_chamfer_box_edge_basic.firmament` | production bounded route | enclosed body with 7 planar faces, 15 edges, 10 vertices |

Accordingly `AirChamferRealBodyPrototype`, `AirChamferStepArtifactLab.WriteControlledCubeOneEdgeStep`, and the EDGE-X11 hash tests prove deterministic emission of the placeholder box and deterministic metadata. They do **not** prove chamfer topology, trimming, or feature-recognition parity.

### 6.3 Correct support-surface dependency for the planar case

The prompt’s conceptual sequence—offset both support surfaces, intersect the two offsets, then derive trim curves—is incomplete for a distance-distance chamfer.

For support planes `S1` and `S2`, the two boundary lines of the inserted planar strip are generally derived from cross intersections:

```text
C1 = Intersect(S1, Offset(S2, signedAmountForRule))
C2 = Intersect(S2, Offset(S1, signedAmountForRule))
ChamferSupport = plane/span through C1 and C2
```

The intersection of the two offset planes is useful construction evidence but is not itself either trim boundary. The offset magnitude and sign depend on whether the rule is equal setback, equal normal offset, distance-angle, or another convention and on material-side orientation. For a 90-degree equal-distance convex edge these distinctions collapse conveniently; they do not for arbitrary dihedral angles.

Thus a future no-history exact route needs explicit rule semantics, signed material side, support-surface identity, cross-intersection dependencies, endpoint/corner policy, and shell validation. The current normal-vector translation artifact is not sufficient, especially for its claimed non-orthogonal case.

### 6.4 Exactness boundary

| Case | Current exact status / likely construction |
|---|---|
| Straight edge, two planes, bounded orthogonal prism, equal symmetric distance | Exact in the production bounded constructors and history-known profile/section routes. |
| Straight edge, two non-orthogonal planes, equal setback | Mathematically exact with signed plane offsets/cross intersections and endpoint resolution; not proven by the current AirChamfer artifact. Some trusted-polyhedral production corner cases exist but are separate bounded BRep rebuilds. |
| Curved edge between analytic surfaces | Not generally a single plane or simple two-rail ruled strip. Requires family-specific offset/intersection and chamfer-surface construction. |
| Constant-angle or asymmetric distance chamfer | Requires explicit rule parameters and different offset placement; not represented by current `UniformChamfer`/single distance metadata. |
| Corner/vertex resolution | Requires a junction policy and possibly additional faces; explicitly deferred by the AirChamfer artifact. |
| Multi-edge chains | Requires continuity, ordering, and corner interaction policy; current AIR route defers/rejects these. |
| General freeform support surfaces | Exact offsets may not remain in the current analytic family; spline construction or approximation policy is required. No general support is proven. |

The statement “all chamfers are ruled surfaces” is rejected. A planar strip is ruled, and many sweep-history cases admit ruled transition faces, but general chamfer support surfaces and corner patches do not reduce to one universal ruled-surface opcode.

## 7. Layer placement table

| Current type/node/path | Current layer | Recommended layer | Reason |
|---|---|---|---|
| `AirNodeKind.TopFaceLoopChamfer` | mixed Core AIR enum | Feature AIR semantic node or bounded lowering-pattern tag | It names the user-visible feature/selection, not a geometric constructor. |
| `AirNodeKind.ProfileExtrude` | mixed Core AIR enum | Construction AIR | Profile plus extent is construction geometry. |
| `AirNodeKind.PrismaticSectionTransition` | mixed Core AIR enum | Construction AIR | Ordered sections/correspondence are reusable geometric construction. |
| `FaceLoopChamferSelection` | BRep prismatic prototype | Feature AIR selection contract, resolved from semantic/history roles | It describes top-cap outer-loop meaning; its current booleans/counts are prototype-bounded. |
| `PrismaticTopEdgeChamferSelection` | BRep prismatic prototype enum | Feature AIR semantic selection/route input | `TopPositiveXSide` is feature selection, not BRep topology. |
| `PrismaticTopFaceLoopChamferPrototype` | Core BRep/Prismatic | bounded Feature-to-Construction lowering plus materializer seam | Split validation/lowering from reusable `SectionTransition` emission over time. |
| `PrismaticTopEdgeChamferPrototype` | Core BRep/Prismatic | bounded Feature-to-Construction lowering | It is axis/fixture-specific but proves section-transition lowering. |
| `PrismaticSectionTransitionEmitter` | Core BRep/Prismatic | Construction AIR materializer into BRepPlan/BRep | Reusable section/correspondence algorithm; direct topology creation is appropriate here. |
| `AirTopFaceLoopChamferBRepPlanner` | trace-only Core AIR | BRepPlan lowering | Roles/IDs/counts belong between construction and BRep; it must eventually drive or share authority with emission. |
| `AirChamferTopologyPlan` | FrictionLab | experimental Feature-to-BRepPlan sketch | Counts and affected roles are useful, but coordinates/dependencies are incomplete. |
| `AirChamferGeometryArtifact` | FrictionLab | experimental Construction AIR dependency evidence | Offset lines and strip are geometry evidence only; rename/rework before reuse because current “trimmed faces” are not regions. |
| `AirChamferClosedWitnessLab` | FrictionLab | remain lab; reject as chamfer materializer | It emits a fresh box and discards the artifact. |
| `AirChamferRealBodyPrototype` | FrictionLab | remain lab until replaced | Its candidate body is not chamfered and does not derive from `SourceBody`. |
| `SelectAirChamferExperimentalRouteOrLegacy` | production executor opt-in seam | temporary route gate around proven materializers | It is fixture-ID/selection hardcoded and can select the false-positive candidate when opt-in. Default remains legacy. |
| `BrepBoundedChamfer` | BRep edge-finishing production code | bounded legacy BRep materializer/backend fallback | Legitimate bounded rebuild/recognition logic, but not the general semantic IR. |
| `BrepExtrude`, `LineArcProfileExtrudeEmitter` | BRep/Materializer | Construction AIR materializers | Proven reusable linear construction with exact analytic surfaces in bounded families. |
| `BrepRevolve` | BRep feature builder | Construction AIR materializer | Proven bounded revolution construction. |
| `LinearExtrusionSurface`, `SurfaceOfRevolutionSurface` | Core geometry / STEP | BRep support geometry emitted from Construction AIR | Exact surface representations, not whole-solid feature nodes. |
| `Step242BsplineRuledClassifier` | STEP import classification | import/recovery analysis | Classification evidence must not be mistaken for a ruled constructor. |
| `OffsetSurface` / `SurfaceIntersection` | no production node; partial lab math only | future Construction AIR dependency nodes | Needed only after exact family/sign/tolerance semantics are specified. |
| `TieredTrimCurveRepresentation` | Firmament execution diagnostics | future curve/trim recovery input, not yet Construction AIR authority | Preserves analytic/numerical candidates and provenance but explicitly emits no BRep/STEP. |
| `FirmamentLoweredBoolean` | legacy lowering plan | split between Feature AIR boolean intent and Construction AIR region composition | Current type mixes region operations and edge-finishing features. |
| `BrepBoolean` and bounded Boolean builders | BRep backend | bounded region realization/fallback | Backend computation remains valid; it should consume explicit composition intent and preserve provenance. |
| `Step242Exporter` | Core STEP backend | STEP AP242 target backend | Correctly consumes realized BRep; keep feature geometry decisions out. |

## 8. Architecture risks

- **Construction AIR becomes another feature catalog.** The current mixed `AirNodeKind` already shows this pressure. Require geometry-family nodes and keep feature names in Feature AIR or route provenance.
- **Feature AIR leaks topology IDs.** Semantic selections must use named roles, construction-history outputs, or resolvable predicates. Imported/no-history selections require explicit recovery/admission, not raw `EdgeId` in source.
- **BRep becomes mutable source state.** Existing executor and bounded feature paths do this today. Keep them as bounded backends while new routes carry semantic/construction state separately.
- **Over-generalizing from boxes.** Both prismatic chamfer prototypes are axis-aligned and history-known. Their exactness does not prove arbitrary planar loops or imported bodies.
- **Claiming all chamfers are ruled.** Planar strips are ruled; curved support combinations, distance-angle rules, and corner patches may need specialized analytic or spline surfaces.
- **Duplicating geometry algorithms.** Profile extrusion, section transitions, offsets, intersections, and trim derivation must live behind reusable materializers rather than being reimplemented per feature.
- **Hidden nondeterminism.** Stable output currently depends on ordered sections, identity correspondence, `TopologyBuilder` creation order, ordered diagnostics, stable selection order, preserved section splits, and STEP traversal/entity order. Semantic names must survive lowering; incidental emitted IDs must not become source contracts.
- **Boolean semantics leak into STEP emission.** Keep Boolean evaluation upstream. The STEP writer should serialize the chosen body and PMI only.
- **Offset/trim dependencies are implicit.** Signed material side, tolerance, surface inputs, derived curves, parameter-space representations, and region senses must be explicit DAG edges.
- **BRepPlan and emitter diverge.** Current trace constructs them separately. A production plan must be authoritative or mechanically derived from the same immutable construction data.
- **False-positive validation.** Marker smoke and metadata counts can pass for the wrong shape, as the single-edge AirChamfer artifact demonstrates. Ground-truth topology and geometric invariants are required.
- **Exact/approximate status is lost.** Every offset, intersection, recovered curve, and spline/ruled classification needs exactness and tolerance provenance; numerical contours must not silently become AP242 exact geometry.

## 9. Corrected Phase 3 milestone ladder

### PHASE3-ARCH-A0 — this investigation

Audit actual paths, expose the false single-edge witness, validate the prismatic route, and record the corrected architecture.

### AIR-CONSTRUCTION-A1 — formalize the proven construction kernel

Define, internally and without syntax changes, the minimum typed construction data for:

- planar line/arc profiles and frames;
- linear sweep;
- bounded full revolution;
- ordered section transition with explicit correspondence and split policy;
- geometric region composition as an explicit deferred/bounded family;
- exactness/tolerance/provenance metadata.

Do not add offset/intersection/trim nodes merely as placeholders. Specify those in a later design record after a correct no-history planar derivation is executable.

### AIR-BREPPLAN-A2 — make one plan authoritative

Change only the prismatic section-transition lab/internal path so a BRepPlan is generated from Construction AIR and consumed by, or shares a single authoritative topology specification with, the emitter. Prove plan/body parity and deterministic IDs across canonical, distance-2, and non-square cases.

### AIR-CHAMFER-A3 — semantic bounded loop chamfer lowering

Add an internal Feature AIR chamfer intent with a semantic face-boundary-loop selection and equal-distance rule. Lower the admitted rectangular-prism/top-cap case to `SectionTransition`. Keep `TopFaceLoopChamfer` as a bounded route/lowering specialization, not a Construction AIR opcode.

### AIR-CHAMFER-A4 — real materialization and artifact proof

Materialize through the authoritative BRepPlan, export STEP, reimport with `aetheris analyze`, validate geometric section measurements—not only counts/markers—and prove stable hashes/order. Keep legacy production authority until parity gates pass.

### AIR-CHAMFER-A5 — production route admission

Admit only the history-known rectangular-prism/top-cap outer-loop domain. Preserve explicit rejection/defer behavior for inner loops, open chains, imported/no-history bodies, nonuniform rules, and oversized distances.

### AIR-CHAMFER-A6 — Firmament V2 syntax and end-to-end demo

Only after Feature AIR, Construction AIR, BRepPlan, BRep, and STEP gates are proven should final Firmament syntax be selected.

### AIR-SURFACE-A0 — separate no-history planar-edge investigation

Correctly derive signed plane offsets, cross-intersection trim lines, chamfer support plane, and endpoint/corner resolution. Replace the false closed witness with a genuinely changed body before considering `OffsetSurface`, `SurfaceIntersection`, or `TrimmedRegion` production nodes.

This order puts implementation evidence before syntax and keeps the strong prismatic case independent from the currently invalid single-edge witness.

## 10. Decision record

**Keep:** the five conceptual compiler responsibilities; semantic selections; immutable construction intent; linear sweep/revolution/section transition as foundational families; BRep as topology authority; STEP as serialization; CIR as an explicitly admitted analysis mirror; bounded legacy BRep algorithms as backend/fallback evidence.

**Change:** add BRepPlan as an explicit realization layer; split mixed `AirNodeKind` responsibilities; make plan/emitter share one authority; model surface operations as dependency nodes rather than decorators; use profile/section lowering before generic offset/trim when construction history makes it exact; strengthen validation to inspect actual geometry.

**Defer:** generic offset surfaces; generic surface intersections; pcurve-aware trimming; imported/no-history selection recovery; curved-edge and analytic-surface chamfers; asymmetric/distance-angle rules; edge chains; corner patches; general region Booleans; generic ruled-surface construction; final Firmament syntax.

**Reject:** the current single-edge AirChamfer closed witness as proof of a chamfered BRep; `TopFaceLoopChamfer` as a Construction AIR opcode; the claim that all chamfers are ruled; the claim that the current implementation already treats BRep only as LIR; the claim that the frustum lab has a separate ruled emitter; universal offset/intersection/trim lowering.

**Next implementation milestone:** `AIR-CONSTRUCTION-A1`, followed by `AIR-BREPPLAN-A2`. Formalize only the construction forms already proven by real emitters, then make the prismatic topology plan authoritative before adding semantic chamfer production behavior.

## Investigation appendix A — implementation inventory

| Implementation | Inputs / selection | Outputs | Assumptions and algorithm | Failure modes / stability | Reusability and recommended placement |
|---|---|---|---|---|---|
| `AirChamferRealBodyPrototype` (`Aetheris.Firmament.FrictionLab/CIRLab/AirChamferRealBodyPrototype.cs`) | source BRep, explicit edge endpoints/normals, distance, policy flags | policy/plan/artifact plus candidate BRep and STEP summary | delegates through policy/artifact/closed-witness chain; does not use source body for emitted candidate | rejects/defer flags; deterministic tests pass, but topology contract is false-positive metadata over a box | lab-only negative evidence; replace, do not promote |
| `AirChamferStepArtifactLab.WriteControlledCubeOneEdgeStep` | hardcoded 10x8x6 source and +X/+Y vertical edge | STEP file, marker and metadata summaries | calls shadow route then serializes candidate | stable hash/marker corpus; geometric analysis exposes 8x8x6 plain box | lab artifact writer only |
| `PrismaticTopFaceLoopChamferPrototype` (`Aetheris.Kernel.Core/Brep/Prismatic/...`) | width/depth/height/distance plus bounded `FaceLoopChamferSelection` | 3-section changed BRep and optional STEP smoke | inset top rectangle; identity correspondence; section emitter | rejects invalid/nonuniform/arbitrary/too-large; defers open/inner/non-top/nonplanar; deterministic real topology | bounded Feature lowering + Construction materializer seam |
| `PrismaticTopEdgeChamferPrototype` | box dimensions, distance, `TopPositiveXSide` only | 3-section changed BRep | inset only top +X section vertices | other selection and oversized distance reject; distance 1/2 stable | bounded lowering evidence, not general selection |
| `PrismaticSectionTransitionEmitter` | 2–3 planar polygon sections, equal vertex counts, identity correspondence | split-preserving planar BRep/STEP | direct `TopologyBuilder`, line curves, plane support surfaces | holes/arcs/>3 sections/nonidentity/nonplanar quads deferred/rejected; deterministic formula | reusable Construction AIR materializer |
| `BrepBoundedChamfer` | recognized box or trusted polyhedral BRep plus bounded selection | rebuilt chamfered BRep | JudgmentEngine route selection, profile/replacement-face construction, direct topology rebuild | family- and token-specific; production tests cover bounded convex/concave/corner cases | bounded backend/fallback; not semantic IR |
| `FirmamentPrimitiveExecutor.SelectAirChamferExperimentalRouteOrLegacy` | lowered chamfer operation, source/legacy bodies, opt-in candidate provider | selected candidate or legacy BRep | accepts only hardcoded `edge_x13_legacy_edge_break`, base box, `XMaxYMax`, planar single edge | default disabled; fallback on any report failure; candidate contract does not detect the plain-box false positive | temporary route gate; do not make authoritative |
| `AirBRepPlan` planners | section request and optional feature context | planned IDs, roles, counts, diagnostics | deterministic plan built independently of emitter; chamfer planner overlays four roles | validates bounded context; stable IDs tested; cannot prevent emitter divergence | retain as basis for realization plan, then make authoritative |
| `BrepExtrude` / line-arc emitter | planar profile, frame, positive depth | direct planar/cylindrical BRep | direct final topology and analytic support surfaces | bounded profile validation; no general swept-surface node | proven Construction AIR materializers |
| `BrepRevolve` | two-point radial/axial line, frame/axis, full revolution | cylinder/cone BRep | exact analytic revolution | partial/general profiles not implemented | proven bounded `RevolutionSweep` materializer |
| STEP swept/ruled support | completed BRep surface geometry or imported STEP | exact linear-extrusion/revolution/spline surface round trips | backend serialization/classification | classification does not construct solids; the investigation detected and restored pre-existing probe-harness drift | backend support and import evidence only |

## Investigation appendix B — controlled experiments

### Experiment A/B — decomposition trace and representation comparison

`dotnet run --project Aetheris.CLI -- trace --case top-face-loop-chamfer --json` reported:

```text
Feature-shaped input: TopFaceLoopChamfer / FaceBoundaryLoop / UniformChamfer
Route: TopFaceLoopChamferPrismatic (SwitchMatch)
Construction: three prismatic sections + identity correspondence
BRepPlan: 12 vertices, 20 curves/edges, 10 faces, 40 coedges
Semantic role overlay: four upper transition faces marked ChamferFace
Emitter: PrismaticTopFaceLoopChamferPrototype / PrismaticSectionTransitionEmitter
STEP smoke: passed
CIR mirror: admitted for evaluation, explicitly lossy for topology/feature identity
```

This trace faithfully describes the section-transition algorithm, except that its BRepPlan is observational rather than emitter-driving.

The proposed offset/intersection/trim representation does **not** describe this actual algorithm. No support-surface offsets or intersections are computed. The final body is constructed from sections and correspondence.

For the single-edge experimental path, source inspection traced:

```text
policy/selection
  -> topology-count plan
  -> translated edge-line artifact and planar strip
  -> artifact discarded
  -> fresh 8x8xedgeLength box used as “closed witness”
  -> box relabeled as chamfer candidate
  -> STEP marker smoke
```

The CLI/analyzer experiment falsified the claimed chamfer realization.

### Experiment C — opcode sufficiency

For the admitted top-face-loop case:

- required: `PlanarProfile`, ordered `SectionTransition`, explicit correspondence/split policy, BRepPlan/shell realization;
- redundant for this route: separate `PlaneSurface`, `OffsetSurface`, `SurfaceIntersection`, `TrimmedSurface`, `RegionDifference`;
- incorrectly broad: `RuledSurface` as the whole-body instruction;
- incorrectly named: `TrimSurface` (prefer `TrimmedRegion` with support and boundary dependencies);
- wrong layer: `TopFaceLoopChamfer` in Construction AIR (belongs in Feature AIR/lowering route).

### Experiment D — topology determinism

Existing stability tests and repeated corpus generation show deterministic output under current fixtures. Determinism depends on:

- fixed ordered section vertices;
- identity correspondence;
- stable `TopologyBuilder` insertion order;
- preserved section splits and no coplanar merge;
- stable diagnostic/selection ordering;
- stable STEP traversal/entity ordering;
- hardcoded semantic recognition for the top cap/four-edge outer loop.

`AirChamferCorpusStabilityTests` proves repeated JSON, marker, topology-summary, and STEP-hash stability for the single-edge corpus, but because the body is a placeholder box this is not geometric correctness evidence.

### Experiment E — controlled variation

The loop-chamfer corpus was regenerated and analyzed:

| Case | Input variation | Topology | Bounds | SHA-256 |
|---|---|---|---|---|
| canonical | 10x8x6, distance 1 | 12 V / 20 E / 10 F | `[-5,-4,0]..[5,4,6]` | `3A7EDBC22E16693AD862E439679909634BF72AF3ECACA9A3B26851FBE1FFAD86` |
| larger distance | 10x8x6, distance 2 | 12 V / 20 E / 10 F | `[-5,-4,0]..[5,4,6]` | `997ECA8A0698CC84385D1D09D9DD29CF8A115C93411633605B9AD17B3381A3B3` |
| non-square | 12x5x7, distance 1 | 12 V / 20 E / 10 F | `[-6,-2.5,0]..[6,2.5,7]` | `72D92381B11AB70AABBDF16BD4E6C0DCE698E6825A8E05FE658A8CEB380C2680` |

All three reimport as enclosed manifolds with ten planar faces. The topology formula remains stable while geometry and hashes change, which is the expected behavior. This demonstrates parameterization beyond one cube fixture, but not beyond rectangular axis-aligned prisms and the top outer loop.

The single-edge experimental artifact hash was `BDE1ECD8E7936604BCB38D52727725C9E2F39BB0B56B5798D563C1DF2C00E8D4`; analysis found bounds `[-4,-4,-3]..[4,4,3]` and ordinary box topology. The production legacy example produced 7 faces/15 edges/10 vertices with hash `3ED853E16D3670D09E41A42D3A428C9D104469481738712FCE8021416DAB6382`.

## Investigation appendix C — validation evidence

Focused results during the investigation:

- `dotnet restore Aetheris.slnx`: **passed**.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: **passed**, 0 errors and 57 existing warnings.
- Requested Firmament test filter `Air|Chamfer|Friction|Ruled|Sweep|Brep|Step`: **228 passed**.
- FrictionLab legacy-gated filter `AirChamfer|TopFaceLoopChamfer|PrismaticTopEdgeChamfer|AirRuledTransitionFrustum`: **151 passed** with `AETHERIS_RUN_LEGACY_TESTS=1`.
- Core filter covering prismatic top-edge, AIR top-face BRepPlan, extrusion, revolution, linear-extrusion STEP, and ruled STEP tests: **48 passed**.
- The initial CLI filter covering AirChamfer stability/artifacts, loop corpus, top-face trace, and ruled probe found **10 passed, 1 failed**. The failure exposed that tracked `tools/Run-RuledStepProbe.ps1` and its documentation were truncated/corrupted and no longer described or implemented the `InlineStep` workflow.
- The tooling-only harness and documentation were restored without changing compiler behavior. Direct `RuledStepProbeHarnessTests` rerun: **2 passed**.
- The complete CLI filter covering AirChamfer stability/artifacts, loop corpus, top-face trace, and ruled probe then passed: **11 passed**.
- A direct restored-harness run reimported and analyzed the ellipse linear-extrusion probe successfully: 1 open face, 4 edges, 4 vertices, one exact `linear-extrusion` surface family, and one `SURFACE_OF_LINEAR_EXTRUSION` STEP entity. This is backend round-trip evidence, not a Construction AIR ruled-solid constructor.
- `dotnet run --project Aetheris.CLI -- --help`: **passed**.
- `git diff --check`: **passed**.
