# EDGE-A3 — Edge-finish selection taxonomy and support collapse audit

## 1. Executive summary

Edge finish support should be selection-pattern driven. The practical chamfer/fillet problem in Aetheris is not “support arbitrary mutation of arbitrary edge graphs”; it is to classify the user’s intent into bounded selection patterns, admit a uniform rule, and lower that intent through the most construction-aware route available.

Practical chamfer/fillet use collapses into three baseline selection patterns:

- **single edge**;
- **face-boundary loop**;
- **whole-body canonical edge set**.

Arbitrary edge graphs, mixed rules, unequal chamfer distances, and variable fillet radii are not baseline support. They may become future advanced features only when an explicit bounded policy defines admissibility, corner behavior, topology, artifact evidence, and fallback authority.

Aetheris should optimize first for bounded uniform-rule cases: symmetric chamfers with one distance and constant-radius fillets with one radius. This aligns edge-finish work with V2’s sweep-first doctrine: preserve construction intent, construct final topology when possible, and use no-history/local-edge AIR fallback only when construction history is unavailable or intentionally not relied upon.

## 2. Problem reframing

The old/general CAD framing treats chamfer and fillet as a broad post-hoc edge operation over already-materialized topology:

- arbitrary subset of arbitrary edges;
- arbitrary radii or distances;
- arbitrary corner interactions among selected and unselected edges;
- arbitrary adjacent surface families;
- variable radii, unequal-distance chamfers, and mixed per-edge rules in one command.

That framing maximizes generality but also maximizes uncontrolled behavior. It pushes the implementation toward edge-graph mutation, late trim discovery, corner-case explosion, and ambiguous fallback semantics. It also obscures the constructive cases where Aetheris can emit the finished body directly.

The Aetheris framing should be narrower and more explicit:

- common manufacturable selection patterns first;
- uniform rule first;
- construction-history-aware lowering where the source operation is known;
- no-history/local-edge fallback only when the constructive route is unavailable or intentionally out of scope.

This means a chamfer on a vertical extrusion edge is usually a profile-authoring problem, a chamfer on a top/bottom extrusion edge is often a prismatic section-transition problem, and a chamfer on an imported plane-plane edge may be a narrow AirChamfer/AirEdgeSweep problem. The same classification should also shape fillet architecture before implementation begins.

## 3. Selection-pattern taxonomy

### Class A — Single edge

Class A selects exactly one edge.

Examples:

- one outside box edge;
- one slot edge;
- one pocket/rim edge.

Lowering depends on construction context:

- **vertical extrusion edge** -> profile-authored chamfer/fillet candidate;
- **top/bottom/horizontal extrusion edge** -> prismatic section-transition candidate;
- **no-history/imported local edge** -> AirEdgeSweep/AirChamfer/AirFillet candidate.

Class A is the smallest practical support target. It isolates adjacent-face families, endpoint policy, rule admissibility, and topology counts before loop and body-wide coordination are attempted.

### Class B — Face-boundary loop / chain around one face

Class B selects a face boundary loop or a connected chain around one face, ordered by topology.

Examples:

- chamfer the top rim of a box;
- fillet the rim of a pocket;
- soften the boundary of a slot face.

Advantages:

- natural ordering;
- known corners;
- known adjacency;
- corner policy derived from loop structure.

Likely routes:

- profile loop modification;
- prismatic section transition;
- loop-level AirEdgeSweep only if no construction history is available or admitted.

AIR-A1 region note: future edge- or face-attached features may use AIR Regions when local frames, explicit yields, and parent integration contracts are needed. Current Class B top-face loop chamfer work does not require region machinery by default because its bounded selection and route can remain explicit without introducing a nested scoped construction island.

Class B should not be conflated with arbitrary edge chains. A face loop has topological order, repeated local context, and an implied corner sequence; an arbitrary chain may not.

### EDGE-LOOP-A0 Class B policy note

EDGE-LOOP-A0 (`docs/development/milestones/general/edge-loop-a0-face-boundary-edge-finish-audit.md`) now defines the Class B face-boundary loop policy in detail. The important clarification is that a loop selection is selected by an owning face boundary and its ordered coedge sequence; it is not an arbitrary edge list that happens to form a cycle. First-scope Class B work should target a closed planar outer loop on a history-known profile/prismatic body with a uniform rule, preferably the top-face outer-loop chamfer of a rectangular prism via prismatic section transition. Inner loops, open chains, no-history/imported loops, non-planar loops, mixed-distance/radius loops, and arbitrary graphs remain deferred until separate admissibility and corner policies exist.

### Class C — Whole-body canonical edge set

Class C selects all or classed edges of a simple body.

Examples:

- chamfer all outside edges of a box;
- fillet all outside edges of a box;
- soften all vertical edges of a prism;
- manufacturing roundover for a simple bounded primitive.

This should be treated as a canonical operation over a body family, not as `N` unrelated edge selections. A box-wide chamfer or fillet has known edge classes, known valence, repeated symmetry, and predictable corner interactions. The operation should therefore be represented by a constructive manifest or body-family policy when possible.

Likely routes:

- constructive manifest;
- profile/prismatic generation;
- legacy bounded route while migration/parity is unproven.

### Class D — Arbitrary edge graph

Class D selects arbitrary edge subsets with no guaranteed face-loop order, no simple body-family guarantee, and no bounded corner topology.

Properties:

- no natural loop/body family guarantee;
- mixed corners and termination;
- selected and unselected incident edges can interact unpredictably;
- ordering may be ambiguous or irrelevant to final topology.

Class D should be advanced/deferred unless reducible to Class A, B, or C, or unless a future milestone defines an explicitly supported bounded graph with deterministic admissibility, rejection reasons, corner policy, artifacts, and route authority.

## 4. Rule taxonomy

### Uniform rule

A uniform rule applies the same chamfer distance or fillet radius across the entire admitted selection. This is the first-class support target for Aetheris edge finishes.

### Symmetric chamfer

A symmetric chamfer uses equal offsets or a single distance. It is the first-class chamfer rule because it is easiest to express in profile/prismatic construction, easiest to validate geometrically, and easiest to compare against legacy bounded behavior.

### Constant-radius fillet

A constant-radius fillet uses one radius. It is the first-class fillet rule because it keeps cylindrical patch construction and corner policy bounded for plane-plane and simple prismatic cases.

### Unequal-distance chamfer

An unequal-distance chamfer uses two distances or an asymmetric offset rule. This is deferred/advanced because it increases adjacent-face trim policy and route-specific parameter interpretation.

### Variable-radius fillet

A variable-radius fillet changes radius along an edge or chain. This is deferred/advanced because it moves beyond the baseline cylindrical-patch model and complicates corner and STEP topology evidence.

### Mixed per-edge rules

Mixed per-edge rules assign different radii or distances to different edges in one selection. This is deferred/advanced unless a bounded policy admits it for a known body family or loop form.

Aetheris baseline edge-finish support should prioritize uniform symmetric rules: one chamfer distance or one fillet radius for the selected Class A/B/C target.

## 5. Lowering strategy matrix

| Selection class | Rule class | Construction history known? | Preferred route | Fallback route | Current evidence | Missing evidence | Production readiness |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Class A: single vertical extrusion edge | Uniform symmetric chamfer | Yes | `ProfileVertexChamferExtrudeEmitter` | Legacy bounded route if production case exists; no-history AirChamfer only when history is unavailable | EDGE-PROFILE-X1 lab; EDGE-PROFILE-V1 internal production-adjacent emitter | Production authority, selector integration, parity against legacy route | Not production-authoritative |
| Class A: single top/horizontal extrusion edge | Uniform symmetric chamfer | Yes | `PrismaticTopEdgeChamferPrototype` / `PrismaticSectionTransitionEmitter` | Legacy bounded route if production case exists; controlled no-history AirChamfer only if admitted | EDGE-PRISMATIC-X1/X2/X3/V1/V2, X5 corpus, X6 analyzer confirmation, X8/X9 map evidence | Production selector policy, broader edge classes, loop/corner policy | Internal controlled route / not production-authoritative |
| Class A: single no-history plane-plane edge | Uniform symmetric chamfer | No | AirChamfer / AirEdgeSweep | Legacy bounded route where existing production behavior already owns the case | EDGE-X AirChamfer policy, topology, artifact, corpus, shadow diagnostics | Production route replacement gates, broader plane-plane cases, fallback authority | Experimental/non-authoritative except legacy-owned cases |
| Class B: face-boundary loop on profile/extruded face | Uniform symmetric chamfer | Yes | Profile loop modification or prismatic transition sequence | Legacy bounded route if it already owns the case | Profile/prismatic single-edge evidence implies likely route family | Loop admissibility, ordered corner policy, topology/corpus evidence | Deferred; next audit target |
| Class B: face-boundary loop no-history | Uniform symmetric chamfer | No | Future AirEdgeSweep loop policy | Legacy bounded route if existing behavior owns a narrow case | AirChamfer single-edge evidence only | Loop-level topology plan, corner policy, artifacts, rejection diagnostics | Deferred/future |
| Class C: whole-body box/prism | Uniform symmetric chamfer | Usually yes for generated primitives; no for imported bodies unless recognized | Constructive manifest / future profile-prismatic synthesis | Legacy bounded route while migration/parity is unproven | EDGE-A2 corner-manifest framing; legacy bounded chamfer exists for current supported cases; prismatic/profile evidence | Body-family manifest, selector classing, corner/corpus parity | Legacy remains authoritative where supported; constructive route deferred |
| Class A: single plane-plane edge | Uniform constant-radius fillet | Yes or no | Future AirFillet, or profile/prismatic lowering where history is known | Legacy bounded fillet where production behavior exists | EDGE-A1 scoped fillet rows; no EDGE-A3 implementation | AIR-FILLET/EDGE-FILLET architecture, cylindrical witness, STEP/corpus evidence | Deferred except legacy-owned cases |
| Class B: face-boundary loop | Uniform constant-radius fillet | Prefer yes; no-history possible later | Future loop-level AirFillet with corner policy, or construction-history loop rewrite | Legacy bounded fillet where existing behavior owns a narrow case | Taxonomy only | Ordered cylinders, corner patches, loop artifacts, analyzer/mirror evidence as applicable | Deferred |
| Class C: whole-body box | Uniform constant-radius fillet | Usually yes for generated primitives | Legacy bounded route / future constructive manifest | Legacy bounded fillet where supported | Existing bounded legacy behavior; taxonomy only for future constructive route | Structured corner patches, manifest, corpus, parity | Legacy remains authoritative where supported; constructive route deferred |
| Class D: arbitrary edge graph | Mixed chamfer/fillet | Irrelevant or mixed | Deferred | Deterministic rejection / split into Classes A/B/C where possible | EDGE-A1 marks broad graph support as gap | Explicit bounded graph policy if ever pursued | Not baseline support |
| Any class | Unequal-distance chamfer | Yes/no | Deferred/advanced | Deterministic rejection or legacy-only if existing route owns a specific case | EDGE-A1 rule taxonomy | Asymmetric rule semantics, per-route topology, artifacts | Not baseline support |
| Any class | Variable-radius fillet | Yes/no | Deferred/advanced | Deterministic rejection or legacy-only if existing route owns a specific case | EDGE-A1 rule taxonomy | Non-constant radius geometry, corner policy, STEP evidence | Not baseline support |

## 6. Support tiers

### Tier 1

- single-edge;
- uniform rule;
- plane-plane or simple profile/prismatic context.

### Tier 2

- face-boundary loop;
- uniform rule;
- construction-history known or explicitly recoverable.

### Tier 3

- whole-body canonical edge set;
- simple primitives/prisms;
- uniform rule.

### Tier 4

- arbitrary edge graph;
- mixed rules;
- variable radius;
- unequal-distance.

### Tier 5

- non-analytic/freeform/surfacing-heavy edge finishes.

Aetheris should target Tier 1–3 first. Tier 4–5 remain deferred unless bounded by explicit policy.

## 7. Chamfer implications

Chamfer support collapses into a small route decision tree:

- vertical history-known edge -> profile-authored chamfer;
- top/bottom history-known edge -> prismatic transition;
- face loop -> profile/prismatic loop rewrite;
- whole-body simple box/prism -> constructive manifest or legacy bounded route while parity is unproven;
- no-history edge -> AirChamfer;
- arbitrary graph or mixed/unequal distances -> deferred.

This is the direct continuation of EDGE-A2, EDGE-PROFILE, and EDGE-PRISMATIC results. EDGE-A2 reframed many chamfers as construction of final topology rather than mutation of an already-sharp BRep. EDGE-PROFILE work demonstrates the vertical profile-authored branch. EDGE-PRISMATIC work demonstrates the top/horizontal section-transition branch. AirChamfer remains valuable, but its baseline role is no-history/local-edge fallback, not universal chamfer authority.

The important collapse is conceptual: the question is no longer “can the chamfer engine trim arbitrary edges?” The first question is “which selection class and uniform rule did the author ask for, and which construction-aware route can emit the final topology directly?”

## 8. Fillet implications

Fillet support should use the same selection taxonomy before adding geometry:

- single plane-plane edge -> cylindrical patch / future AirFillet;
- face-boundary loop -> ordered cylinders plus corner policy;
- whole-body box -> cylinders plus corner patches in a structured pattern;
- uniform radius only for baseline;
- mixed/variable radius deferred.

This should become input to a future `EDGE-FILLET-A0` or `AIR-FILLET-A0`. The future fillet audit should avoid starting from universal arbitrary edge graphs. Instead, it should classify fillet selection intent by Class A/B/C/D, admit constant-radius plane-plane or simple prismatic cases first, and document deterministic rejection for variable/mixed/freeform cases until explicitly bounded.

## 9. Corner policy simplification

Corners are much easier when the selection class and uniform rule are known.

Face loops provide an ordered corner sequence. The implementation can reason over adjacent selected edges, loop orientation, face ownership, and repeated endpoint transitions rather than discovering arbitrary graph neighborhoods.

Whole-body canonical sets provide known corner valence and symmetry. A box has structured three-edge convex corners; a simple prism has repeated top/bottom/vertical edge classes. A canonical manifest can name these interactions directly instead of pretending the command is just many unrelated single-edge operations.

Arbitrary graphs create uncontrolled corner policy: mixed selected/unselected incident edges, ambiguous termination, non-uniform rules, and topology that may depend on graph traversal order. Such support should be deferred unless a future milestone admits one bounded graph family with explicit policy.

## 10. Production-readiness implications

Readiness gates should be selection-class specific.

### Single-edge gate

A Class A route must define:

- admissibility policy for adjacent face family, edge orientation, rule size, and endpoint context;
- constructive route preference: profile, prismatic, AirChamfer/AirFillet, or legacy;
- explicit BRep topology and STEP expectations;
- feature-recognition parity where imported/no-history cases are claimed;
- fallback/legacy authority when the new route is not authoritative;
- artifact/corpus evidence for representative admitted and rejected cases;
- CIR mirror/analyzer evidence only where relevant to analysis claims, not as topology authority.

### Face-loop gate

EDGE-LOOP-X1 evidence note: `PrismaticTopFaceLoopChamferPrototype` / `TopFaceLoopChamferPrismaticLab` now provide the first constructive Class B proof for the history-known top-cap outer loop of a rectangular prism. The proof admits one owning top face, one outer closed ordered four-coedge loop, and one uniform symmetric chamfer rule, then lowers the request through a single prismatic section stack. It records `edge-loop-x1-class-b-loop-route` and `edge-loop-x1-not-four-independent-single-edge-chamfers`, so the evidence is explicitly not a collection of four unrelated Class A edge operations.

A Class B route must define:

- loop selection contract and ordered topology;
- admissibility for open chains vs closed loops;
- uniform rule policy;
- corner policy for every loop vertex;
- constructive lowering route and fallback route;
- BRep topology/STEP artifacts for successful loops;
- deterministic rejection diagnostics for unsupported loop forms;
- feature-recognition parity if STEP/imported loops are admitted;
- CIR/analyzer evidence where occupancy or containment is claimed.

### Whole-body gate

A Class C route must define:

- body-family recognition or construction manifest;
- canonical edge classes such as outside, vertical, top, bottom, rim, or primitive-specific classes;
- uniform rule policy;
- structured corner manifest;
- BRep topology and STEP artifact expectations;
- parity against legacy bounded behavior where legacy owns existing production support;
- fallback/legacy authority during migration;
- artifact/corpus coverage for each admitted body family;
- CIR mirror/analyzer evidence only for admitted mirror requests.

### Arbitrary graph gate

A Class D route is not a baseline goal. If pursued, it must define:

- graph admissibility and reduction to A/B/C where possible;
- deterministic traversal or graph-independent semantics;
- mixed selected/unselected endpoint behavior;
- corner and termination policy for all valence patterns;
- rule compatibility policy, including whether mixed rules are admitted;
- BRep topology/STEP artifacts and rejection fixtures;
- fallback authority and no-silent-partial-success behavior.

Until these gates are proven, arbitrary graph support should be documented as deferred rather than implied by single-edge or loop evidence.

## 11. Updates to EDGE-A1 matrix

EDGE-A1 rows should be interpreted through this taxonomy:

- CH-03 is a single-edge case whose route is either controlled/history-known or no-history depending on the path under test.
- Top/horizontal chamfer belongs to the prismatic section-transition route when construction history is available.
- Chain rows should be split into face-boundary loop/chain around one face versus arbitrary edge graph.
- Whole-body simple box/prism rows should be added or called out as canonical body-family operations rather than `N` unrelated edge selections.
- Constant-distance chamfer and constant-radius fillet rows are baseline rule candidates; unequal-distance, mixed-rule, and variable-radius rows are deferred/advanced.

The EDGE-A1 compatibility matrix now has an EDGE-A3 selection taxonomy update note to make this interpretation explicit.

## 12. Recommended roadmap

Recommended next milestones:

1. `EDGE-LOOP-A0` — face-boundary loop edge-finish audit.
2. `EDGE-FILLET-A0` or `AIR-FILLET-A0` — fillet architecture audit using selection taxonomy.
3. `EDGE-CORNER-X1` — constructive manifest for whole-body/simple corner chamfer/fillet cases.
4. `EDGE-LOOP-X1` — completed first-scope top-face outer-loop chamfer prismatic lab; next promote to `EDGE-LOOP-X2` artifact/corpus evidence.
5. `EDGE-FILLET-X1` — single-edge plane-plane constant-radius fillet witness.
6. Optional: whole-body box uniform chamfer/fillet corpus.

Arbitrary edge graph support should not be the next target. The next work should prove Tier 1–3 support gates, especially face-boundary loops and constant-radius fillet architecture, before broad graph features are considered.

## 13. Relationship to CIR/AIR/BRep authority

The selection taxonomy is AIR/Firmament intent. It names what the author meant to select and which rule class applies before a topology route is chosen.

BRep emits explicit topology once the route is selected. BRep is authoritative for the resulting vertices, edges, loops, faces, surface bindings, curve bindings, and STEP-exportable explicit topology, but it should not be treated as the default place to recover high-level edge-finish selection intent.

CIR mirrors/analyzers may validate occupancy, containment, volume, or map behavior for admitted mirrors. They do not define selection topology. A prismatic or edge-finish CIR mirror can provide analysis evidence only under its own admission policy.

STEP does not recover selection intent by default. STEP import can provide BRep topology and, with separate recognizers, may recover some constructive semantics, but imported topology should not be assumed to carry Class A/B/C intent unless a recognizer admits that interpretation.

## 14. Non-goals

EDGE-A3 explicitly does not include:

- implementation;
- production route changes;
- public API changes;
- new fillet/chamfer geometry;
- broad arbitrary edge graph support;
- variable-radius or unequal-distance implementation;
- STEP exporter/importer changes;
- Boolean core changes;
- CIR node or analyzer behavior changes;
- AIR emitter behavior changes;
- BRep topology changes;
- production chamfer/fillet route replacement;
- test weakening;
- triangle migration retry;
- NURBS/freeform expansion.

## EDGE-LOOP-X2 Class B corpus evidence

EDGE-LOOP-X2 adds artifact-corpus evidence for the Class B top-face outer-loop chamfer route introduced by EDGE-LOOP-X1. The corpus command `aetheris experimental loop-chamfer-corpus --out-dir <dir> [--json]` writes canonical, larger valid, and non-square STEP artifacts, plus JSON-only rejected/deferred rows for invalid dimensions, invalid or too-large distances, non-uniform rules, arbitrary graphs, open chains, non-closed loops, non-outer loops, non-planar owning faces, and inset self-intersection risk. This is still lab-only, selection-pattern-driven evidence; it does not admit a production selector or broaden Class B support beyond the history-known top-cap outer loop.

## EDGE-FILLET-A0 application note

EDGE-FILLET-A0 (`docs/development/milestones/general/edge-fillet-a0-selection-taxonomy-architecture-audit.md`) applies this taxonomy to fillet support. The audit keeps baseline fillets constant-radius only, treats Class A single-edge cylindrical plane-plane evidence as the first proof target, defers Class B loop and Class C whole-body fillets until corner policy evidence exists, and keeps Class D arbitrary graphs, mixed-radius selections, variable-radius fillets, and mixed chamfer/fillet selections deferred unless a future bounded manifest admits them. Existing legacy bounded fillet behavior remains authoritative where production support already exists.

## AIR-A0 mapping note

AIR-A0 maps these selection classes into Feature AIR: Class A as `AirEdgeSelection`, Class B as `AirLoopSelection`, Class C as `AirBodyEdgeClassSelection`, and Class D as explicit unsupported/deferred arbitrary graph selection. Future chamfer/fillet lowering should select the highest construction-aware AIR route before using no-history local edge or legacy bounded BRep fallbacks. See `docs/development/milestones/general/air-a0-aetheris-v2-compiler-ir-constitution.md`.

## AIR-X1 Class B metadata note

AIR-X1 records the top-face outer-loop chamfer proof as internal AIR metadata with node kind `TopFaceLoopChamfer`, route `TopFaceLoopChamferPrismatic`, selection class `FaceBoundaryLoop`, and rule kind `UniformChamfer`. This note is descriptive metadata around the existing prismatic loop route only; it does not introduce route selection, JudgmentEngine policy, production replacement, or new chamfer/fillet geometry.

## AIR-X2 evidence note

AIR-X2 provides route-selection evidence for Class B face-boundary-loop uniform chamfer by admitting the `TopFaceLoopChamferPrismatic` AIR-X1 wrapper through switch/match classification. It also records stable rejection for arbitrary-graph uniform chamfer as unsupported in AIR-X2.


## AIR-X4 Class B planning note

For Class B top-face boundary-loop uniform chamfers, AIR-X4 preserves selection metadata into BRepPlan feature context and marks upper transition faces with chamfer semantic roles. This does not change edge-finish selection or production routing.
