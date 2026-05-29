# AIR-CIR-A0 — AIR/CIR/BRep authority and mirror contract audit

## 1. Executive summary

AIR, CIR/FRep, BRep, and STEP are different representations of the same intended solid, but they do not have equal authority.

- **Firmament / semantic intent** owns user-, feature-, and manufacturing-facing intent.
- **AIR** is the constructive topology/intention MIR. It preserves the construction family, profile/section/correspondence choices, and feature lineage that should drive emitted topology.
- **BRep** is the explicit topology/export backend. It owns materialized faces, edges, loops, surface bindings, and STEP serialization.
- **CIR/FRep** is a lower-level implicit/evaluation IR. It owns field evaluation only for admitted analysis questions; it is not the default topology construction MIR.
- **STEP** is an external serialization/interchange artifact. It is not internal construction truth and should not be treated as having recovered AIR intent by default.

The corrected lowering shape is therefore:

```text
Firmament / semantic intent
  -> AIR constructive topology MIR
      -> BRep explicit topology / STEP
      -> CIR/FRep mirror / analysis runtime, optional and admitted
```

This audit defines representation authority, CIR mirror admission, provenance and diagnostics requirements, disagreement triage, feasible current mirror lanes, analyzer dispatch implications, and the next proof roadmap. It makes no implementation, production, analyzer, STEP, Boolean, topology, CIR-node, or corpus-gate behavior changes.

## 2. Representation definitions

### 2.1 Firmament / semantic intent

Firmament / semantic intent is the user-, feature-, or manufacturing-facing meaning of a model operation. It answers questions such as “what did the user ask for?” and “which manufacturing feature is intended?”

Examples include:

- box;
- hole;
- chamfer;
- fillet;
- profile transition;
- counterbore or blind pocket intent;
- local edge finishing request.

Firmament intent may be high-level and policy-rich. It is not required to already contain materialized topology, and it is not equivalent to a field evaluator.

### 2.2 AIR

AIR is Aetheris's constructive topology MIR. It is the internal layer that should preserve enough construction context to emit topology deliberately rather than rediscover it from already-materialized geometry.

AIR should preserve:

- construction family;
- profile and section definitions;
- section correspondence;
- feature/route lineage;
- intended split/merge topology;
- admissibility and rejection context.

Current examples from code and docs include:

- profile-authored chamfer, where a vertical-edge chamfer can be represented as a changed profile that is then extruded;
- prismatic section transition, where bottom/top sections and correspondence drive side-face emission;
- line/arc profile extrusion, where resolved profile loops lower directly to explicit BRep faces;
- no-history/local-edge `AirChamfer`, where the route is intentionally narrow and non-authoritative until separately admitted.

AIR is authoritative for construction family and topology intent. It is not itself a STEP artifact and should not be replaced by CIR/FRep decompilation.

### 2.3 BRep

BRep is explicit materialized topology: vertices, edges, loops, faces, shells, surface bindings, and curve bindings. It is authoritative for questions about emitted topology and exported analytic surface families.

BRep owns:

- which faces, edges, loops, and shells exist in the emitted body;
- which face binds to which plane/cylinder/sphere/cone/torus/other supported surface;
- which curve family is bound to an edge;
- STEP export/import substrate behavior;
- explicit topology comparison and artifact inspection.

BRep is not the preferred high-level construction language in V2. It is the explicit backend produced from constructive intent.

### 2.4 CIR/FRep

CIR/FRep is an implicit field/evaluation representation. Current CIR has primitive and CSG tree nodes, a semantic `Evaluate(Point3D)` API, and a tape runtime direction for denser evaluation workloads.

CIR/FRep is authoritative only for admitted field/evaluation questions, such as:

- point containment/classification;
- approximate volume;
- map occupancy or thickness sampling;
- section sampling when the requested output is explicitly field-derived;
- differential field analysis.

CIR/FRep is not topology authority. It does not preserve face identity, loop identity, split-face lineage, feature role labels, or exact exported boundary topology unless an explicit mirror contract says how those losses are handled for a bounded request.

### 2.5 STEP

STEP is the external serialization/interchange representation. It is the format external consumers receive and the artifact used for interchange and roundtrip inspection.

STEP is not internal construction truth. Importing STEP can recover explicit BRep topology and surface bindings, but it must not silently invent AIR feature intent or CIR mirror provenance. Any STEP-to-AIR or STEP-to-CIR recovery must be done through explicit recognizers and mirror-admission milestones.

## 3. Current code evidence

### 3.1 CIR is a primitive/CSG field tree with semantic evaluation

`CirNodeKind` currently includes `Box`, `Cylinder`, `Sphere`, `Torus`, `Cone`, `Union`, `Subtract`, `Intersect`, and `Transform`. `CirNode` exposes `Bounds` and an abstract `Evaluate(Point3D)` evaluator. Primitive nodes implement signed-field style evaluators: boxes use axis-aligned distance-to-box math, cylinders use radial/height distance, spheres evaluate radius distance, cones evaluate a finite cone field, and CSG nodes compose children with min/max/negation semantics.

This is field/evaluation evidence, not topology evidence: a CIR node can answer signed-value queries, but it does not carry emitted face IDs, edge IDs, loop IDs, STEP entity identities, or profile-stack correspondence.

### 3.2 CIR tape/runtime is evaluation-directed

`CirTape` lowers CIR evaluation into linear instructions for point evaluation, interval evaluation, and region classification. The E0 runtime design explicitly frames the tape as a point-evaluation-first runtime for map/section/volume-heavy analysis paths, with interval classifications as conservative signals.

This direction reinforces that CIR is an analysis runtime and mirror candidate, not the default place to reconstruct explicit topology.

### 3.3 Existing Firmament-to-CIR paths are limited lowerers and recognizers

`FirmamentCirLowerer` lowers primitive Firmament plans to CIR nodes for boxes, cylinders, spheres, toruses, cones, transforms, and supported boolean composition. `CirNativeAnalysisService` can analyze CIR nodes/tapes and Firmament plans for point classifications and volume estimates. `CirBrepMaterializer` and `CirBoxCylinderRecognizer` are bounded materialization/recognition work, including canonical box-minus-cylinder through-hole handling.

Those paths are useful evidence of CIR/BRep differential pressure, but they should be classified as bounded adapters or analysis services, not as a general CIR-to-BRep construction architecture.

### 3.4 BRep emitters already produce explicit topology from constructive intent

Current BRep lanes construct explicit topology directly from constructive inputs:

- `BrepPrimitives` creates primitive BReps.
- `BrepExtrude` extrudes profile loops into faces, edges, and loops.
- `LineArcProfileExtrudeEmitter` emits explicit line/arc profile extrusion topology and analytic side surfaces.
- `ProfileVertexChamferExtrudeEmitter` packages the profile-authored vertical chamfer lane.
- `PrismaticSectionTransitionEmitter` emits explicit transition BReps from bottom/top sections and correspondence.
- `BrepBoundedChamfer` and `BrepBoundedFillet` remain legacy bounded explicit topology routes where production behavior exists.

These lanes show the intended V2 pattern: preserve construction intent, then emit explicit topology directly, rather than derive topology from an implicit field.

### 3.5 Analyzer map/section contrast from EDGE-PRISMATIC-X7

`StepAnalyzer.AnalyzeMap` imports STEP to a `BrepBody`, computes a projection grid, and relies on `BrepSpatialQueries.Raycast` for every sample. `BrepSpatialQueries.Raycast` is intentionally a v1 primitive query layer focused on primitive outputs from `BrepPrimitives.CreateBox`, `CreateCylinder`, and `CreateSphere`; unsupported layouts return diagnostics.

`AnalyzeSection`, by contrast, is BRep/contour oriented and has been able to confirm selected prismatic generated STEP artifacts through explicit section geometry. EDGE-PRISMATIC-X7 therefore recommends hybrid map dispatch: CIR/tape for generated AIR bodies with admitted mirrors, BRep raycast for explicitly supported BRep topology, and deterministic unsupported diagnostics otherwise.

### 3.6 Existing differential docs/tests already point at mirror discipline

CIR differential docs and tests cover Firmament-to-CIR lowerers, native analysis, CIR/BRep materializer recognition, and primitive/boolean parity cases. These are valuable but should be interpreted as bounded equivalence or recognition evidence. They are not proof that arbitrary CIR/FRep can be decompiled to authoritative BRep topology.

## 4. Authority matrix

| Question | Firmament authority? | AIR authority? | BRep authority? | CIR authority? | STEP authority? | Notes |
|---|---:|---:|---:|---:|---:|---|
| 1. What feature did the user ask for? | Yes | Carries lowered lineage | No | No | No | Firmament owns user/feature/manufacturing intent. STEP import must not invent this intent. |
| 2. What construction family is intended? | Yes, at feature level | Yes | Evidence only | No | Evidence only | AIR owns resolved construction family: extrusion, profile chamfer, prismatic transition, revolve, ruled transition, local edge route. |
| 3. What topology should be emitted? | Policy input | Yes | Materialized result | No | External result | AIR is topology intent; BRep is the emitted topology. CIR has no face/edge/loop authority. |
| 4. Which faces/edges/loops exist? | No | Intended lineage | Yes | No | Imported/exported evidence | BRep owns explicit topology IDs and shells. |
| 5. Which surface family is exported? | No | Intended analytic family | Yes | No | External artifact evidence | BRep/STEP own exported surface bindings. AIR can specify intended families. |
| 6. Is this point inside? | No | Chooses eligible mirror/backend | Yes, for supported BRep query paths | Yes, only when mirror admitted for containment | No, except via imported BRep backend | Authority depends on admitted backend and tolerance. |
| 7. What is approximate volume? | No | Chooses eligible mirror/backend | Yes, for supported exact/voxel paths | Yes, when mirror admitted for volume | No, except via imported BRep backend | CIR/tape is natural for admitted field volume; BRep remains needed for topology/export parity. |
| 8. What is map occupancy? | No | Chooses eligible mirror/backend | Yes, for supported raycast bodies | Yes, when mirror admitted for map sampling | No, except via imported BRep backend | `analyze map` should be representation-polymorphic, not CIR-first by default. |
| 9. What are section contours? | No | May define intended section family | Yes, for contour-oriented output | Only for explicitly field-sampled output | No, except via imported BRep backend | Current section analysis is BRep/contour-oriented. CIR section sampling is a different output contract. |
| 10. What feature-recognition/parity signals exist? | Source truth when available | Yes, for route lineage | Yes, for explicit topology signals | Yes, for admitted field parity signals | Evidence only | Parity signals must name the compared representations and losses. |
| 11. What should external consumers receive? | No | No | Yes, via export | No | Yes, as serialized interchange | BRep/STEP own external exchange. |
| 12. What should be used for regression/artifact comparison? | Expected intent labels | AIR route/provenance | Yes, for topology/STEP artifacts | Yes, for admitted field maps/volumes | Yes, for external artifact snapshots | Use comparison that matches authority. Do not use FRep-only output as topology proof. |

## 5. Mirror contract

An admitted CIR mirror is an explicit, scoped analysis artifact that says: “for this source object, this CIR/FRep representation may answer these analysis questions under these tolerances and known losses.” It is not an assumed equivalent body.

A CIR mirror must declare:

- **source**:
  - source AIR atom/route where available; or
  - BRep source when the mirror is explicitly BRep-derived by a bounded recognizer;
- **mirror type**:
  - `exact` — field semantics are expected to match the source solid within declared tolerance for supported field queries;
  - `conservative` — field answers are one-sided or bounded, with explicit unknown/mixed handling;
  - `approximate` — sampled/approximated field suitable only for explicitly approximate outputs;
  - `unsupported` — no mirror is admitted;
- **supported analyzer uses**:
  - point containment;
  - approximate volume;
  - map occupancy;
  - section sampling;
  - explicitly **not** face identity;
  - explicitly **not** topology parity;
- **tolerance policy**:
  - distance tolerance;
  - boundary classification policy;
  - map voxel/grid policy;
  - volume resolution/adaptive policy;
  - handling of unknown/mixed regions;
- **known losses**:
  - face identity;
  - loop identity;
  - split-face lineage;
  - feature role labels;
  - boundary precision;
  - exact trim/loop topology;
- **provenance**:
  - AIR node/atom id;
  - construction route id/name;
  - BRep body id or hash if available;
  - CIR mirror id/version;
  - emitter/mirror version;
  - source corpus/artifact case id when applicable;
- **diagnostics**:
  - unavailable/inapplicable reason;
  - stale/mismatched source reason;
  - lossy-for-request reason;
  - unsupported atom/operation reason;
  - selected backend and rejected backend candidates.

Stable mirror statuses:

- `mirror-unavailable`
- `mirror-admitted-exact`
- `mirror-admitted-conservative`
- `mirror-admitted-approximate`
- `mirror-rejected-unsupported-atom`
- `mirror-rejected-lossy-for-request`
- `mirror-rejected-stale-or-mismatched`

The status must be visible in analyzer diagnostics whenever CIR/FRep is considered or expected. Silent fallback between BRep and CIR is not acceptable for comparison or corpus artifacts.

## 6. Lowering policy

Canonical lowering shape:

```text
Firmament -> AIR -> BRep
Firmament -> AIR -> CIR mirror, optional
```

Policy:

- AIR-to-BRep is authoritative for exported topology.
- AIR-to-CIR is optional and analysis-scoped.
- CIR-to-BRep is not the default path.
- CIR-to-BRep is allowed only for explicitly bounded reconstruction/materialization adapters with their own gates, diagnostics, and non-default scope.
- STEP-to-CIR is not allowed by default.
- STEP import may recover CIR mirrors only through explicit recognizers and mirror-admission milestones.
- STEP import may recover BRep topology, but that does not recover AIR feature intent unless a separate recognizer admits it.

This prevents the old “CIR/FRep -> recover/patch/trim -> BRep” direction from becoming the normal topology construction path.

## 7. Disagreement policy

BRep and CIR can disagree because they answer different questions under different contracts. Triage must start with authority, not with a global assumption that one representation is “truth.”

Decision tree:

1. **Was a CIR mirror admitted for this AIR atom/source and requested analysis?**
   - No: CIR has no authority. Report `mirror-unavailable` or a specific rejection status.
   - Yes: continue.
2. **Does the requested output require topology, face identity, edge identity, loop identity, or exported surface identity?**
   - Yes: use AIR/BRep/STEP as appropriate, not CIR.
   - No: continue.
3. **Does BRep output violate AIR construction intent?**
   - If yes, suspect an AIR-to-BRep emitter bug or an AIR route/admissibility bug.
4. **Does CIR result disagree with BRep on an admitted field query within tolerance?**
   - Possible causes: AIR-to-CIR mirror bug, AIR-to-BRep emitter bug, stale provenance, or tolerance mismatch.
   - The comparison artifact must report source ids, mirror status, tolerance context, and backend outputs.
5. **Does only STEP roundtrip disagree?**
   - Suspect STEP export/import, entity recovery, or downstream recognizer behavior before suspecting AIR intent.
6. **Does a generated AIR body compare differently from an imported STEP-only body?**
   - Generated AIR may carry admitted mirror provenance; imported STEP normally does not. Do not infer equivalent analyzer authority from identical-looking geometry alone.

Do not assume either BRep or CIR is globally “truth.” Authority depends on the question and the admitted scope.

## 8. Current AIR atom mirror feasibility

| AIR/emitter lane | BRep status | CIR mirror status today | Plausible mirror strategy | Allowed analyzer uses | Blocker | Recommended milestone |
|---|---|---|---|---|---|---|
| 1. Box primitive | Supported by `BrepPrimitives`; raycast/containment support exists for recognized primitive layouts | Existing `CirBoxNode` can represent simple box fields; mirror metadata not standardized | Exact axis-aligned/placed box mirror from AIR primitive | Point containment, approximate volume, map occupancy | Need explicit mirror admission/provenance and dispatch diagnostics | CIR-MAP-X1, AIR-CIR-X1 |
| 2. Cylinder primitive | Supported by `BrepPrimitives`; raycast/containment support exists for recognized cylinder layouts | Existing `CirCylinderNode`; mirror metadata not standardized | Exact finite cylinder mirror for supported placement/axis policy | Point containment, approximate volume, map occupancy | Placement/axis/tolerance provenance and backend dispatch | CIR-MAP-X1, AIR-CIR-X1 |
| 3. Sphere primitive | Supported by `BrepPrimitives`; raycast/containment support exists for recognized sphere layouts | Existing `CirSphereNode`; mirror metadata not standardized | Exact sphere mirror | Point containment, approximate volume, map occupancy | Mirror admission/provenance and analyzer selection | CIR-MAP-X1, AIR-CIR-X1 |
| 4. Cone/frustum | BRep support exists in primitive/emitter lanes where present, but map raycast primitive gate is narrower than all analytic surfaces | Existing `CirConeNode`; no general analyzer mirror contract | Exact finite cone/frustum mirror for bounded primitive cases | Point containment, approximate volume, later map occupancy | BRep map support and CIR mirror capability diagnostics are not admitted for general cones | AIR-CIR-X1, later CIR-MAP primitive expansion |
| 5. Torus | BRep analytic surface support exists in topology/export contexts where present; current BRep raycast primitive map gate does not admit torus | Existing `CirTorusNode`; no admitted map mirror contract | Exact torus field for standalone primitive or bounded analytic uses | Point containment/volume/map only after explicit mirror admission | Topology/export vs field semantics and unsupported map backend today | AIR-CIR-X1, later CIR-MAP primitive expansion |
| 6. Profile extrusion / line-only polygon prism | `BrepExtrude` and line/arc profile extrusion emit explicit topology | No current generic CIR mirror | Convex polyhedron/half-space evaluator for line-only convex profiles; generalized polygon winding/extrusion evaluator later | Containment, approximate volume, map occupancy after admission | No half-space/poly-prism CIR node/evaluator or mirror contract today | CIR-PRISMATIC-X1 |
| 7. Profile-authored vertical chamfer | Production-adjacent internal emitter packages profile-chamfer-as-extrude for bounded cases | No current admitted mirror distinct from profile extrusion | Mirror the resulting changed profile prism, likely half-space/poly-prism for line-only bounded cases | Same as profile extrusion after admission | Needs profile-prism mirror strategy and provenance from chamfered profile atom | CIR-PRISMATIC-X1 |
| 8. Prismatic section transition | `PrismaticSectionTransitionEmitter` emits explicit transition BReps for admitted section/correspondence cases | No current admitted CIR mirror | Section-stack implicit evaluator, ruled side half-spaces for simple equal-count line-only cases, or deferred | Approximate map/volume/containment only after admission; not section contour topology | No section-stack/transition field evaluator and no mirror metadata | CIR-PRISMATIC-X1, EDGE-PRISMATIC-X8 |
| 9. Profile-hole rectangle minus circles | BRep paths and bounded recognizers exist for selected box-minus-cylinder style cases | CIR can represent box subtract cylinder in bounded canonical cases; mirror admission not generalized | Exact CSG `box - cylinder(s)` for through holes that satisfy bounded recognizer constraints | Containment, approximate volume, map occupancy after admission | Multiple holes, non-through/blind holes, tangent/grazing, and profile provenance need explicit gates | AIR-CIR-X1, CIR-MAP-X1 for single through-hole subset |
| 10. Ruled transition/frustum | BRep transition/frustum lanes exist where constructed explicitly | Cone/frustum can mirror only rotational/conical subset today | Use `CirConeNode` for true cone/frustum; section-stack evaluator for non-conical ruled transitions | Field analysis for admitted conical subset | General ruled surfaces do not map to current CIR primitives | CIR-PRISMATIC-X1 or a ruled-transition mirror milestone |
| 11. AirChamfer no-history/local-edge | Narrow experimental/prototype/shadow route evidence exists; legacy bounded routes remain authoritative where production exists | `mirror-unavailable` today for general local-edge AirChamfer | Only reducible planar half-space cuts/simple cylinders should be considered; otherwise deferred | None by default; field uses only after specific reduction admits mirror | Loss of face identity, local topology graft semantics, and no general half-space mirror | AIR-CIR-A1 after prismatic half-space proof |
| 12. Fillet legacy/manufacturing fillet | `BrepBoundedFillet` remains production route for current bounded cases | `mirror-unavailable` today except trivial cylindrical/spherical/torus-like reductions if separately proven | Analytic SDF blends or primitive torus/cylinder/sphere subsets, but not general fillet topology | None by default | Topological split/trim identity and blend semantics are not captured by current CIR mirror contract | Separate fillet-mirror audit after chamfer/prismatic proof |
| 13. Boolean composition fallback | BRep/STEP Boolean fallback exists where routed; Firmament-to-CIR supports selected primitive booleans | CIR supports primitive CSG node composition but not exported topology authority | Use CIR CSG only for admitted primitive compositions with declared losses | Containment, approximate volume, map occupancy; not topology parity | General Boolean BRep topology and trims cannot be recovered from CIR by default | AIR-CIR-X1 for primitive CSG mirrors; no default CIR-to-BRep extraction |

## 9. Analyzer dispatch implications

### analyze section

`analyze section` is currently BRep/contour-oriented. It imports or receives a BRep, intersects a section plane with explicit face/edge geometry, and reports contour-oriented results.

It may remain BRep-first because section contours require explicit face/edge geometry and boundary topology semantics. CIR-backed section sampling is possible later, but it is a different output contract: field-sampled occupancy/zero-set approximation rather than authoritative BRep contour topology. If added, diagnostics must label it as CIR-backed sampling and report mirror status.

### analyze map

`analyze map` should become representation-polymorphic, not permanently BRep-raycast-only and not unconditionally CIR-first.

Candidate backends:

- **BRep raycast** for explicit-topology bodies accepted by `BrepSpatialQueries.Raycast` or future supported BRep raycast providers;
- **CIR/tape field sampler** for generated AIR bodies with admitted CIR/FRep mirrors;
- **future hybrid** backends that combine BRep boundary evidence with CIR field acceleration or conservative tile pruning.

Dispatch rules:

- Prefer CIR only when a mirror is admitted for the requested map output.
- Retain BRep raycast for bodies already supported by explicit BRep query paths.
- Do not infer a CIR mirror from arbitrary STEP import.
- Emit backend diagnostics: selected backend, mirror status, tolerance/grid policy, and rejected candidates.
- Prevent silent output changes when a body moves from BRep raycast to CIR/tape sampling.

### analyze volume / containment

CIR/tape is a natural backend for approximate volume and point containment when a mirror is admitted, especially for dense sampling or adaptive interval workloads.

BRep remains required for:

- exact topology/export parity;
- face/edge/loop identity;
- STEP artifact comparison;
- exact analytic BRep volume paths where they exist;
- unsupported or stale mirror cases.

## 10. Metadata/provenance requirements

Future implementation should carry at least:

- AIR node/atom id;
- construction route name;
- emitter version;
- BRep body id/hash/topology summary;
- CIR mirror id/version;
- tolerance context;
- mirror capability flags;
- diagnostics/rejection reasons;
- source artifact/corpus case id;
- source representation kind (`Firmament`, `AIR`, `BRep`, `STEP`, `CIR`);
- backend dispatch decision and rejected candidates;
- known-loss flags, especially face/loop/split-lineage loss;
- stale-source detection inputs, such as route version plus topology summary/hash.

This metadata should be included in future analyzer artifacts whenever a comparison crosses AIR, BRep, CIR, or STEP boundaries.

## 11. Recommended proof roadmap

1. **CIR-MAP-X1 — CIR-backed map for existing primitives.**
   - Use existing Box/Cylinder/Sphere CIR nodes.
   - Compare with existing BRep primitive raycast map.
   - Do not add prismatic mirrors yet.
   - Emit backend/mirror diagnostics.

   X1 proof note (May 29, 2026): the primitive proof now exists as a lab/test-only CIR tape map prototype for admitted box, cylinder, and sphere mirrors. It compares deterministic occupancy/thickness summaries against the existing `BrepSpatialQueries.Raycast` primitive baseline and emits explicit `mirror-admitted-exact` diagnostics, without changing production analyzer dispatch, CLI behavior, STEP import/export, Boolean behavior, BRep topology, AIR emission, CIR node kinds, prismatic mirror support, or CIR-to-BRep extraction.

2. **AIR-CIR-X1 — AIR mirror metadata prototype.**
   - Attach mirror-availability diagnostics to a small generated body path.
   - Prove `mirror-admitted-*` and `mirror-rejected-*` statuses in artifacts.
   - Keep production analyzer behavior gated or unchanged until separately admitted.

3. **CIR-PRISMATIC-X1 — prismatic mirror feasibility.**
   - Decide between convex polyhedron/half-space CIR node/evaluator and section-stack implicit evaluator.
   - Start with rectangle-inset or controlled top-edge chamfer.
   - Prove known losses and tolerance policy before using analyzer outputs as regressions.

4. **EDGE-PRISMATIC-X8 — hybrid map dispatch prototype.**
   - Use CIR mirror when admitted.
   - Use BRep raycast otherwise.
   - Keep deterministic unsupported diagnostics for STEP/imported bodies without admitted mirrors.

5. **AIR-CIR-A1 — mirror drift / parity policy.**
   - Define artifact comparisons across AIR->BRep and AIR->CIR.
   - Standardize stale/mismatched provenance diagnostics.
   - Define when field disagreement blocks a route versus only warning on an approximate analysis artifact.

## 12. Risks and guardrails

Risks:

- **Dual-kernel drift**: AIR->BRep and AIR->CIR evolve independently and disagree without clear ownership.
- **False equivalence between BRep and CIR**: treating a field mirror as if it preserves topology.
- **Hidden loss of face identity**: map/volume results silently replace face/edge evidence.
- **Approximate maps pretending to be topology proof**: sampled occupancy is not explicit contour or face proof.
- **Tolerance mismatch**: BRep boundary, CIR zero-set, voxel, and STEP import tolerances diverge.
- **STEP import inventing intent**: recognizers over-claim AIR features from external topology.
- **Analyzer output changing silently by backend**: regressions become incomparable.
- **Performance illusions from recursive CIR instead of tape**: prototype field trees may look correct but not scale to map/section/volume workloads.

Guardrails:

- require explicit mirror admission;
- require explicit backend diagnostics;
- require stable mirror statuses;
- carry provenance and stale-source checks;
- do not infer default CIR mirrors from STEP;
- do not use CIR-to-BRep as the default production path;
- do not make topology claims from FRep-only outputs;
- keep BRep/STEP authority for exported topology;
- keep analyzer output contracts distinct when backend semantics differ.

## 13. Non-goals

This milestone does not do any of the following:

- no implementation;
- no new CIR nodes;
- no analyzer behavior change;
- no route replacement;
- no STEP exporter/importer changes;
- no Boolean core changes;
- no BRep topology changes;
- no AIR emitter behavior changes;
- no map/section analyzer behavior changes;
- no production behavior change;
- no test weakening;
- no default gated corpus test changes;
- no CIR-to-BRep extraction work;
- no NURBS/freeform scope.
