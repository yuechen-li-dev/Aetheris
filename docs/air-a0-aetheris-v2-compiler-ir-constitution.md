# AIR-A0 — Aetheris V2 compiler IR constitution

## 1. Executive summary

Aetheris is a compiler for BRep.

The constitutional representation boundaries for Aetheris V2 are:

- **Firmament is source / authoring intent.** It is the user-facing semantic language and feature layer.
- **AIR is the constructive geometry MIR.** It must preserve topology-generating construction intent before explicit topology emission.
- **BRep is the explicit topology backend and STEP/export authority.** It owns emitted faces, loops, coedges, edges, vertices, curve bindings, surface bindings, shells, bodies, and STEP serialization behavior.
- **CIR is an admitted field/evaluation side-channel.** It is useful for occupancy, map, containment, volume, differential checks, bounds, and mirrors, but it is not the topology authority.
- **STEP is serialization, not construction truth.** STEP is the external interchange artifact produced from or imported into explicit topology; it is not recovered construction intent unless a recognizer explicitly admits that recovery.

The default lowering must **not** be:

```text
Firmament -> CIR -> BRep
```

That path asks an implicit/evaluation representation to become the topology-generating source of truth. It turns normal compilation into decompilation/materialization and reintroduces runtime discovery failure modes.

The preferred lowering is:

```text
Firmament -> AIR -> BRep
```

with optional AIR-derived mirrors:

```text
AIR -> CIR mirror
```

The canonical Aetheris V2 compilation pipeline is:

```text
Firmament / semantic source
  -> Feature AIR
  -> Constructive AIR
  -> BRepPlan
  -> BRep
  -> STEP
```

The admitted analysis side channel is:

```text
Constructive AIR
  -> admitted CIR mirror
  -> analysis / map / containment / volume / differential checks
```

This document is documentation/design only. It does not implement AIR, change production routes, change BRep topology, change STEP import/export, change Boolean behavior, change CIR evaluation/analyzers, change Firmament behavior, or alter chamfer/fillet/shell geometry.

AIR-A1 extends this constitution with **AIR Regions**: scoped construction islands inside AIR for local-frame features whose effects escape only through explicit yields and route-selected parent integration. The doctrine is that different-axis features such as side holes, side pockets, bosses, pattern elements, and future shell/local offset contexts should be represented as scoped local AIR intent first; Boolean remains only one possible admitted backend integration route, not the default representation. See `docs/air-a1-air-regions-scoped-feature-lowering-audit.md`.

## 2. Compiler analogy

Aetheris V2 should be reasoned about as a compiler pipeline, not as a collection of interchangeable geometry encodings.

| Aetheris concept | Compiler analogue | Constitutional meaning |
| --- | --- | --- |
| Firmament | Source language / frontend syntax | User-authored semantic program. |
| Feature intent | Typed AST / high-level IR | Feature-level meaning after parsing, binding, and semantic checks. |
| Feature AIR | High-level typed IR | Source-preserving feature operations, selections, rules, and provenance. |
| Constructive AIR | MIR | Canonical constructive operations that preserve topology-generating intent and have known emission, explicit unsupported status, or bounded fallback. |
| BRepPlan | Backend instruction selection / emission plan | Explicit planned topology with roles and provenance before materialization. |
| BRep | Backend object representation | Concrete topology and geometry objects. |
| STEP | Serialized ABI/interchange artifact | External representation of emitted BRep, not internal construction truth. |
| CIR tree/tape | Evaluator IR / field bytecode side-channel | Field/evaluation program for analysis, not topology construction authority. |
| Analyzers | Validation/runtime tools | Consumers of BRep and admitted CIR mirrors. |
| Artifact corpus | Compiler test corpus / golden IR-output fixtures | Evidence that selected lowering routes preserve expected topology/artifacts. |
| Route selection | Compiler lowering/optimization pass | Enumerates admissible routes, scores them, selects deterministically, and records rejection reasons. |
| Feature recovery from imported BRep | Decompilation/recognition | Explicit recognition of intent from topology; not normal compilation. |

CIR-to-BRep by default is decompilation because CIR is an evaluator representation: it can answer field questions, but it does not inherently carry face identity, loop identity, edge identity, feature lineage, split policy, or construction history. Asking CIR to produce BRep as the normal route means reconstructing topology from an implicit field after source intent has already been erased. That is appropriate only for explicitly admitted materialization/recovery lanes, not for native/generated Aetheris compilation.

## 3. Representation authority doctrine

Authority must be local to the question being asked:

- **Firmament owns user/feature/source intent.** It answers what the user requested and what semantic feature was authored.
- **AIR owns construction/topology-generating intent.** It answers which constructive family, selection, rule, correspondence, split policy, and route should emit the final topology.
- **BRep owns explicit emitted topology and STEP/export behavior.** It answers what topology actually exists after emission and what STEP serialization will contain.
- **CIR owns field/evaluation semantics only inside admitted mirror scope.** It answers admitted field questions such as occupancy or approximate comparison, with declared losses.
- **STEP owns external serialization only.** It is an interchange artifact over explicit topology, not a source of recovered feature history.
- **Imported BRep/STEP does not automatically recover AIR intent.** Recovery requires explicit recognizers and diagnostics.
- **If BRep and CIR disagree, investigate mirror scope, BRep emitter correctness, and comparison semantics rather than assuming one global truth.** A mismatch may indicate stale mirror metadata, approximate sampling limits, missing CIR atoms, BRep emission bugs, topology comparison bugs, or an invalid question for the admitted mirror.

CIR may validate occupancy or approximate analysis when admitted. CIR must not claim face identity, loop identity, edge identity, feature identity, or topology parity unless that capability is explicitly admitted. In most mirror cases, topology parity should be unavailable.

## 4. Proposed IR stack

### 4.1 Firmament / source intent

Role:

- user-facing authoring layer;
- feature syntax;
- semantic operations;
- source diagnostics;
- not responsible for BRep topology details.

Examples:

- box;
- cylinder;
- profile extrude;
- hole;
- chamfer;
- fillet;
- shell;
- pattern;
- mirror.

Firmament should compile semantic feature requests into Feature AIR rather than directly forcing CIR materialization or post-hoc BRep mutation.

### 4.2 Feature AIR

Feature AIR is a typed high-level feature IR. It remains semantic, but is now inside the compiler pipeline rather than the source language surface.

Examples:

- `CreateBox`
- `CreateProfileExtrude`
- `CreateThroughHole`
- `ChamferEdge`
- `ChamferLoop`
- `FilletEdge`
- `Shell`
- `Pattern`
- `Mirror`

Feature AIR should preserve:

- feature id;
- source provenance;
- construction history;
- selection kind;
- rule kind;
- requested operation;
- diagnostic context.

Feature AIR is where source-level convenience operations remain visible before being canonicalized into lower constructive families. For example, `CreateBox` may remain a feature operation even if its Constructive AIR lowering is rectangle-profile extrusion.

### 4.3 Constructive AIR

Constructive AIR is the canonical MIR with deterministic lowering contracts. It should only contain operations that have one of the following statuses:

- known BRep emission strategy;
- explicit unsupported/deferred status;
- bounded legacy fallback with recorded authority and losses.

Examples:

- `AirProfileExtrude`
- `AirProfileExtrudeWithLoops`
- `AirProfileVertexChamferExtrude`
- `AirPrismaticSectionTransition`
- `AirRuledTransition`
- `AirRevolve`
- `AirSweep`
- `AirEdgeSweep`
- `AirConstructiveShell`
- `AirCornerManifest`
- `AirLegacyBRepRoute`
- `AirUnsupported`

Each Constructive AIR node should define:

- admissibility rules;
- expected topology contract, where possible;
- BRep emission route;
- optional CIR mirror route;
- known losses;
- diagnostics;
- rejection/deferred reasons;
- provenance.

Constructive AIR is the right place to ask: “What is the highest-level constructive representation that can emit the final topology directly?”

### 4.4 BRepPlan

BRepPlan is an explicit topology emission plan before concrete BRep materialization. It is lower-level than Constructive AIR but richer than raw BRep because it carries roles, stable planned identities, and provenance.

Proposed contents:

- planned vertices;
- planned curves;
- planned edges;
- planned coedges;
- planned loops;
- planned surfaces;
- planned faces;
- planned shells;
- planned bodies;
- topology roles;
- source/AIR provenance;
- expected counts;
- validation hooks.

BRepPlan is useful because it provides:

- stable IDs;
- debuggable topology;
- feature roles;
- artifact summaries;
- validation before materialization;
- easier LLM/Codex reasoning;
- a way to avoid silent topology mutation.

BRepPlan should not replace AIR. It is the backend plan selected from AIR.

### 4.5 BRep

BRep is the explicit topology representation. It owns:

- materialized faces;
- loops and coedges;
- edges and vertices;
- surface/curve bindings;
- shell/body closure;
- STEP/export behavior;
- explicit topology truth after emission.

Once a Constructive AIR node has emitted a BRep body, BRep is authoritative for the emitted topology. AIR remains authoritative for construction intent and provenance, but the actual backend object is the BRep body.

### 4.6 CIR side-channel

CIR is an admitted evaluation/mirror IR. It is useful for:

- occupancy;
- map;
- containment;
- volume;
- differential checks;
- bounds;
- approximate or exact field comparison where admitted.

CIR is not a topology source. CIR mirrors should be generated from AIR or BRep with explicit admission metadata, capabilities, and losses.

## 5. AIR value model

Recommended AIR typed values:

- `AirBody`
- `AirProfile2D`
- `AirLoop2D`
- `AirCurve2D`
- `AirSection`
- `AirSectionStack`
- `AirCorrespondenceMap`
- `AirSelection`
- `AirEdgeSelection`
- `AirLoopSelection`
- `AirBodyEdgeClassSelection`
- `AirRule`
- `AirChamferRule`
- `AirFilletRule`
- `AirShellRule`
- `AirFrame`
- `AirTransform`
- `AirBRepPlan`
- `AirCirMirrorAdmission`

AIR should be SSA-like and immutable:

- AIR body values are immutable.
- Feature operations consume an `AirBody` and produce a new `AirBody`.
- In-place BRep mutation should not be the normal lowering model.
- Stable node IDs and provenance should be attached to every node and value.

Example source-shaped AIR:

```text
%profile0 = Profile.Rectangle(width=10, depth=8)
%body0 = ProfileExtrude(%profile0, height=6)
%loop0 = Select.TopOuterLoop(%body0)
%body1 = ChamferLoop(%body0, %loop0, distance=1)
```

Route selection may lower `%body1` to constructive section-stack AIR:

```text
%sec0 = Section(z=0, profile=fullRectangle)
%sec1 = Section(z=5, profile=fullRectangle)
%sec2 = Section(z=6, profile=insetRectangle)
%body1 = PrismaticSectionTransition(%sec0, %sec1, %sec2)
```

This preserves the source feature request while selecting a constructive topology-emitting representation.

## 6. AIR dialects / node families

### Profiles

Profile AIR covers:

- profile loops;
- line/arc/circle curves;
- outer/inner loops;
- orientation;
- profile validation.

Profiles are foundational because V2 is resolved-profile-first rather than sketch-solver-first.

### Sections / prismatic

Section/prismatic AIR covers:

- section stack;
- z/order/axis;
- profile correspondence;
- split policy;
- transition faces.

This family captures extrusions, stepped profiles, tapered/frustum-like transitions, and top-edge/top-loop chamfer constructions where the finished topology is best emitted directly from sections.

### Body constructors

Constructive body constructors include:

- profile extrusion;
- prismatic section transition;
- revolve;
- ruled transition;
- sweep;
- primitive aliases.

Primitive aliases are convenience front doors. For example, a box can lower to rectangle extrusion; a cylinder can lower to circle extrusion or revolve depending on selected route.

### Feature selections

Selection AIR should model:

- single edge;
- face-boundary loop;
- whole-body canonical edge set;
- arbitrary graph as explicit unsupported/deferred;
- face selection;
- removed-face selection for shell.

Selections need provenance and class because selection class determines admissible route families.

### Edge finishes

Edge-finish AIR should include:

- profile chamfer;
- prismatic chamfer;
- `AirEdgeSweep` chamfer;
- profile fillet candidate;
- prismatic fillet candidate;
- `AirFillet` candidate;
- corner manifest.

Chamfer and fillet should not begin as arbitrary BRep graph mutation. They should begin as typed requests whose selected route depends on construction history and selection class.

### Shell/thicken

Shell/thicken AIR should include:

- inward shell;
- open-face shell;
- inner profile/body derivation;
- rim connections;
- thicken deferred or separate.

Shell should be modeled as constructive topology where possible, not as generic offset/trim discovery.

### Fallback/legacy

Fallback AIR should include:

- legacy BRep route;
- BRep Boolean fallback;
- unsupported/deferred node.

Fallback nodes must record why a higher-level route was unavailable and what authority/losses the fallback carries.

## 7. Route selection and lowering passes

Every feature lowering should ask:

> What is the highest-level constructive representation that can emit the final topology directly?

Preferred route order:

1. profile rewrite / profile-authored construction;
2. prismatic/section-stack construction;
3. revolve/ruled/sweep construction;
4. constructive manifest for known body families;
5. `AirEdgeSweep`/`AirFillet` local no-history route;
6. legacy bounded BRep route where currently authoritative;
7. BRep Boolean fallback only when explicitly admitted;
8. deterministic reject/defer.

Aetheris should not use generic BRep mutation as the default. It should not use CIR-to-BRep as the default. It should not silently fall back.

Route selection is a compiler pass with:

- candidate route enumeration;
- admissibility checks;
- utility/scoring;
- deterministic tie-breaking;
- selected route;
- rejected route diagnostics;
- fallback authority;
- provenance.

Aetheris already has `JudgmentEngine`-style policy logic in several areas, including materializer planning, restricted contour snap selection, Boolean policy, BRep spatial containment, bounded chamfer/fillet decisions, STEP root export planning/import recovery, and AirChamfer friction-lab policy. AIR route selection should reuse that pattern when the subsystem selects among multiple bounded strategies. It should not be used for simple deterministic rewrites or ordinary enum dispatch.

## 8. Provenance and roles

Required provenance metadata:

- source feature id;
- Firmament source location/span if available;
- AIR node id;
- selected route;
- selection class;
- rule class;
- construction history kind;
- diagnostics;
- known losses;
- route exclusions.

BRep role metadata that AIR/BRepPlan should preserve where possible:

- cap face;
- side face;
- transition face;
- chamfer face;
- fillet face;
- shell inner face;
- shell outer face;
- rim face;
- hole wall;
- profile-loop face;
- prismatic transition face;
- legacy face;
- unknown/recovered face.

Geometry parity is not feature parity. A face that is geometrically planar still needs role/provenance when used for diagnostics, artifacts, and feature recognition. Without role metadata, later analyzers and corpus comparisons can prove geometry similarity but cannot prove that construction intent survived.

## 9. CIR mirror contract from AIR

AIR nodes may optionally provide a CIR mirror. A mirror must declare status:

- unavailable;
- admitted exact;
- admitted conservative;
- admitted approximate;
- rejected unsupported atom;
- rejected lossy for request;
- rejected stale/mismatched.

A mirror must declare capabilities:

- occupancy;
- map;
- containment;
- volume estimate;
- differential comparison;
- bounds.

A mirror must declare losses:

- no face identity;
- no loop identity;
- no topology parity;
- no feature labels;
- approximate only.

Examples:

- Box -> exact CIR primitive.
- Cylinder -> exact CIR primitive.
- Profile hole extrusion -> possible primitive CSG mirror if admitted.
- Prismatic convex body -> convex polyhedron mirror if admitted.
- Top-face loop chamfer -> convex polyhedron mirror if all-planar/convex.
- Shell -> future outer-minus-inner mirror.
- Fillet -> mirror deferred unless exact analytic mirror exists.

The mirror contract exists to make analysis useful without allowing analysis IR to steal topology authority.

## 10. Existing lane mapping

| Existing lane | Current implementation shape | Proposed AIR node/lane | BRep emission authority | CIR mirror availability | Missing AIR migration work |
| --- | --- | --- | --- | --- | --- |
| Box/primitive normalization | Primitive constructors and production AIR box/primitive evidence normalize simple solids toward constructive families. | `CreateBox` -> `AirProfileExtrude` rectangle, or primitive alias over constructive AIR. | BRep primitive/extrude emitters and STEP exporter remain backend authority. | Exact primitive mirror for box where admitted. | Stable Feature AIR wrapper, Constructive AIR schema, provenance, route diagnostics. |
| Profile extrusion | Line/arc profile extrusion and profile-stack executors emit BRep from resolved profile/section data. | `AirProfileExtrude` / `AirProfileExtrudeWithLoops`. | Existing profile extrusion emitters. | Possible exact/CSG mirror for simple profiles; unavailable or approximate for unsupported loops. | Node schema, profile value model, BRepPlan bridge, corpus summaries. |
| Profile with hole extrusion | Through-hole/profile-stack lanes emit bodies from profile loops instead of late 3D subtract where admitted. | `AirProfileExtrudeWithLoops`. | Existing profile-stack/profile-hole BRep emission. | Possible primitive CSG mirror if the profile/hole family admits exact correspondence. | Explicit inner-loop AIR, loop provenance, mirror admission metadata. |
| Profile vertex chamfer extrusion | Profile vertex chamfer emitter rewrites profile geometry and extrudes the already-finished profile. | `AirProfileVertexChamferExtrude` or profile rewrite route under `ChamferEdge`. | Existing profile vertex chamfer extrusion emitter. | Usually profile-derived 2D/CSG mirror only if admitted. | Connect EDGE-A3 Class A vertical/history-known selection to Feature AIR route selection. |
| Prismatic section transition | Section transition emitter builds BRep from ordered sections and correspondence. | `AirPrismaticSectionTransition`. | Existing `PrismaticSectionTransitionEmitter`. | Convex polyhedron mirror for admitted convex/all-planar cases. | Make this the first BRepPlan candidate; encode section correspondence and split policy. |
| Top-edge chamfer prototype | Controlled top-edge chamfer lowers to a prismatic transition rather than generic edge mutation. | `ChamferEdge` -> `AirPrismaticSectionTransition` / future `AirTopEdgeChamfer`. | Existing prismatic top-edge prototype and section transition emitter. | Convex mirror if resulting body is admitted convex/all-planar. | Feature selection wrapper, route diagnostics, production/non-production boundary. |
| Top-face loop chamfer prototype/corpus | Class B top outer-loop chamfer corpus emits prismatic transition artifacts and rejected/deferred JSON rows. | `ChamferLoop` -> future `AirTopFaceLoopChamfer` -> `AirPrismaticSectionTransition`. | Existing lab prototype/corpus route, not normal production route. | Convex polyhedron mirror if all-planar/convex and admitted. | Promote corpus evidence into AIR wrapper tests without route replacement. |
| AirChamfer/AirEdgeSweep no-history lane | Friction-lab/shadow/opt-in route explores local no-history chamfer candidates with diagnostics and legacy fallback. | `AirEdgeSweep` / `AirChamfer` local route. | Experimental/lab route only unless explicitly admitted; legacy bounded BRep remains authoritative where production exists. | Usually unavailable unless a matching analytic mirror is defined. | Keep as lower-priority route after construction-history-aware candidates; define endpoint/corner manifest. |
| CIR prismatic convex mirror | CIR metadata and prismatic convex-polyhedron mirrors support admitted field analysis. | `AirCirMirrorAdmission` adapter from constructive prismatic AIR. | None; mirror is not BRep authority. | Admitted exact/conservative/approximate per case. | Tie mirror provenance to AIR nodes and BRepPlan roles. |
| Analyzer map/section relationship | Analyzer/map work can dispatch using BRep and admitted CIR mirrors. | Analyzer consumes BRep plus optional `AirCirMirrorAdmission`. | BRep remains topology authority. | Mirror-aware map only inside declared capability scope. | Avoid topology claims from field maps; record unavailable/lossy cases. |
| Legacy bounded chamfer route | `BrepBoundedChamfer` owns existing bounded production chamfer behavior and uses route/corner judgments. | `AirLegacyBRepRoute` for scopes where existing behavior is authoritative. | `BrepBoundedChamfer`. | Generally unavailable or approximate unless separately admitted. | Wrap as bounded fallback with route exclusions and losses; do not silently replace. |
| Legacy bounded fillet route | `BrepBoundedFillet` owns current bounded production fillet behavior for admitted constant-radius scopes. | `AirLegacyBRepRoute` initially; future `AirFillet` after witness evidence. | `BrepBoundedFillet`. | Deferred unless exact analytic fillet mirror exists. | Fillet AIR route selection after Class A cylindrical witness and corner evidence. |

## 11. Imported/recovered geometry policy

Native/generated bodies and imported/recovered bodies must not be treated the same.

Native/generated path:

```text
Firmament -> AIR -> BRep/CIR
```

Full construction provenance is available.

Imported path:

```text
STEP -> BRep
```

Optional recovery path:

```text
BRep -> recovered AIR
```

only if an explicit recognizer admits the recovery. Otherwise the body is BRep-only.

No silent inference:

- A STEP body that looks like a prismatic transition is not automatically an AIR prismatic transition.
- A loop in imported topology is not automatically a history-known face-loop feature.
- Feature recovery is decompilation/recognition and must be explicit.

Recovered AIR should be marked as recovered/recognized rather than native/source-authored, with recognizer diagnostics and confidence/admissibility metadata.

## 12. Boolean policy

Boolean is a route, not the default.

Preferred policy:

- use 2D/profile Boolean where source is profile-based;
- use profile-stack/prismatic construction where interval-like;
- use CIR mirror for analysis when admitted;
- use BRep Boolean fallback only when necessary and explicit;
- do not let general Boolean erase AIR provenance without recording loss.

This avoids the earlier CIR/BRep confusion and Boolean PTSD class of failures by keeping construction intent upstream. The compiler should avoid discovering topology from intersecting 3D surfaces when a profile, section stack, revolve, sweep, or manifest can directly emit the final topology.

## 13. Edge-finish policy under AIR

EDGE-A3 selection classes map directly into Feature AIR selections:

- **Class A single edge** -> `AirEdgeSelection`.
- **Class B face-boundary loop** -> `AirLoopSelection` with owning-face/loop provenance.
- **Class C whole-body canonical edge set** -> `AirBodyEdgeClassSelection` with body-family manifest.
- **Class D arbitrary graph** -> explicit unsupported/deferred selection until graph-level policy exists.

Chamfer policy:

- vertical/history-known -> profile rewrite;
- top/horizontal/history-known -> prismatic transition;
- top-face loop -> prismatic transition;
- no-history -> `AirEdgeSweep`/`AirChamfer`;
- arbitrary graph -> deferred.

Fillet policy:

- constant-radius only baseline;
- first proof target: single plane-plane cylindrical witness;
- loop/whole-body deferred until Class A/corner evidence;
- variable/mixed radius deferred.

Edge finishes should preserve selected route, rejected candidate routes, rule class, endpoint/corner policy, and known losses.

## 14. Shell/thicken policy under AIR

Shell is more important than thicken for near-term architecture. Baseline shell should be inward-only.

Shell policy:

- Do not model shell as offset toward an origin.
- Use an interior witness point only to orient inward normals.
- For history-known analytic bodies, shell should be constructive:
  - derive inner profile/body;
  - connect openings/rims;
  - emit final topology.
- Generic surface-offset/trim shell is deferred.
- Thicken is separate and lower priority.

Recommended future docs:

- `SHELL-A0 — Inward shell/thicken architecture audit`
- `SHELL-X1 — Open-top rectangular prism inward shell constructive witness`

## 15. BRepPlan proposal

BRepPlan should:

- describe explicit topology before materialization;
- carry roles/provenance;
- validate loops/coedges/faces before BRep creation;
- support artifact summaries;
- help future emitters be deterministic.

BRepPlan should not:

- replace AIR;
- become a public API immediately;
- require retrofitting every existing emitter at once;
- hide feature intent.

Gradual adoption path:

- new emitters produce BRepPlan first;
- old emitters can be wrapped;
- artifact/corpus writers consume BRepPlan summaries when available.

`AirPrismaticSectionTransition` is the best first BRepPlan candidate because it already has explicit section correspondence, predictable topology counts, role-rich transition faces, and artifact/corpus evidence.

## 16. Migration strategy

### AIR-X1

Wrap existing proven lanes in minimal AIR models:

- profile extrusion;
- prismatic section transition;
- top-face loop chamfer.

### AIR-X2

Add route-selection/admissibility framework:

- candidate routes;
- scoring;
- diagnostics;
- deterministic rejection.

### AIR-X3

Introduce BRepPlan for one new or existing emitter:

- prismatic section transition preferred;
- or shell/fillet witness later.

### AIR-X4

AIR-to-CIR mirror adapters:

- box/cylinder/sphere;
- prismatic convex mirror;
- loop chamfer convex mirror if admitted.

### AIR-X5

Firmament-to-AIR frontend boundary:

- lower selected existing Firmament primitives/features to Feature AIR.

### Later

- imported BRep feature recovery;
- shell;
- fillet;
- whole-body manifests;
- broader route-selection integration.

## 17. Production-readiness gates for AIR

AIR production readiness requires:

- node schema stability;
- provenance;
- deterministic route selection;
- diagnostics;
- BRep emission parity;
- STEP artifact stability;
- CIR mirror admission semantics;
- feature recognition/analyzer implications understood;
- no silent fallback;
- no production route replacement without explicit migration;
- corpus coverage.

A route should not be promoted merely because a geometry sample looks correct. It must declare authority, capabilities, losses, fallback behavior, and corpus evidence.

## 18. Non-goals

This milestone explicitly does not include:

- AIR implementation;
- public API commitment;
- production behavior changes;
- BRep topology changes;
- STEP changes;
- Boolean changes;
- CIR evaluator changes;
- Firmament behavior changes;
- chamfer/fillet/shell geometry changes;
- arbitrary graph support;
- import/recovery implementation;
- triangle migration retry;
- NURBS/freeform expansion;
- test weakening.

## 19. Recommended next milestone

Recommended next milestone:

```text
AIR-X1 — Minimal AIR wrappers for proven constructive lanes
```

Candidate scope:

- `AirProfileExtrude`;
- `AirPrismaticSectionTransition`;
- `AirTopFaceLoopChamfer`;
- common provenance/diagnostic envelope;
- no production route replacement;
- tests only around wrappers/roundtrip summaries.

Alternative smaller milestone:

```text
AIR-X1 — PrismaticSectionTransition as first AIR node
```

Recommendation: choose **AIR-X1 — Minimal AIR wrappers for proven constructive lanes**.

Reasoning:

- It is still documentation-compatible with the hard boundary of no production route replacement.
- It validates the AIR envelope across three distinct proven families: profile, prismatic, and loop-chamfer-as-prismatic.
- It prevents `AirPrismaticSectionTransition` from becoming an accidental one-off design.
- It gives route selection and BRepPlan later milestones enough variety to design stable provenance, diagnostics, and mirror admission metadata.
- It keeps imported recovery, shell, fillet, arbitrary graph support, Boolean replacement, and NURBS/freeform expansion out of scope.

## AIR-X1 status note

AIR-X1 adds a minimal internal AIR envelope and thin wrappers for the existing profile extrusion, prismatic section transition, and top-face loop chamfer constructive lanes. The wrappers validate provenance, diagnostics, route identity, topology summaries, and Class B loop metadata without introducing BRepPlan, route selection, production route replacement, CIR mirror changes, or STEP/BRep/Boolean behavior changes. See `docs/air-x1-minimal-air-wrappers-for-proven-lanes.md`.

## AIR-X2 status note

AIR-X2 adds an internal route-selection/admissibility layer ahead of AIR-X1 wrappers. It preserves the AIR-A0 authority split: Firmament remains source intent, AIR remains constructive MIR, BRep remains topology authority, CIR remains a mirror side-channel, and STEP remains serialization. AIR-X2 does not implement BRepPlan and does not replace production routes.

## AIR-X3 status note

AIR-X3 adds the first minimal internal BRepPlan proof for `AirPrismaticSectionTransition`. The plan sits between Constructive AIR and the existing prismatic BRep emitter, records deterministic planned IDs, topology roles, expected counts, split policy, provenance, diagnostics, and guarantees, and does not change production routes, BRep topology behavior, CIR authority, Firmament lowering, or STEP/export authority.


## AIR-X4 status note

AIR-X4 adds feature-role-aware BRepPlan evidence for the Class B top-face loop chamfer lane. It preserves AIR-A0 authority boundaries: AIR remains constructive MIR, BRepPlan remains a non-materializing backend plan, BRep remains explicit topology/export authority, CIR remains a side-channel, and STEP remains serialization.

## AIR-X5 status note

AIR-X5 adds an internal/test-visible AIR-to-CIR mirror adapter envelope for generated prismatic section transitions and top-face loop chamfers when existing convex polyhedron mirror evidence admits them. The adapter preserves AIR provenance metadata but explicitly denies CIR face identity, loop identity, topology parity, chamfer-face identity, feature labels, and BRepPlan role parity. It does not change production analyzer behavior, route selection, BRepPlan behavior, BRep topology, STEP import/export, CIR evaluator/tape behavior, Firmament lowering, Boolean behavior, or chamfer/fillet/shell geometry. See [AIR-X5 — AIR-to-CIR mirror adapter envelope](air-x5-air-to-cir-mirror-adapter-envelope.md).

## AIR-X6 trace status

AIR-X6 adds `aetheris trace` as a top-level compiler-lowering report command for built-in AIR cases. The command reports AIR, route selection, BRepPlan, emitted BRep/STEP smoke, CIR mirror admission, diagnostics, guarantees, and known losses without making STEP serialization or CIR evaluation topology authority.

## AIR-X7 fixture corpus note

Firmament `.valid.firmfixture` and `.invalid.firmfixture` files are source/lowering contract fixtures. They document accepted and rejected/deferred authoring-intent programs and let `aetheris trace --fixture` report compiler lowering stages without treating STEP as construction truth or changing production geometry behavior.

## AIR-X8 parser-backed fixture anchor

AIR-X8 anchors the Firmament fixture corpus to the real Firmament frontend for one minimal primitive source form. The primitive box `.valid.firmfixture` is parser-backed, reaches the truthful `parsed` frontend stage, and deliberately does not claim AIR/BRepPlan/CIR/STEP lowering until that bridge is explicitly wired. Metadata-driven AIR-X7 Chamfer fixtures remain valid lowering contracts.

## AIR-X9 parser-backed frontend boundary note

AIR-X9 creates the first parser-backed Firmament-to-AIR boundary for the existing TOON-style primitive box fixture. The path is `Firmament source -> FirmamentTopLevelParser -> parsed box op -> Feature AIR CreateBox trace summary`. This validates the constitutional distinction between Firmament source and Feature AIR without expanding grammar, replacing production routes, or claiming Constructive AIR/BRepPlan/CIR stages that are not wired.

## AIR-X10 parser-backed boundary note

AIR-X10 creates the first parser-backed Feature AIR to Constructive AIR boundary: the existing Firmament `op: box` / `size[3]` fixture now traces from parsed source to Feature AIR `CreateBox` and then to a Constructive AIR `AirProfileExtrude` rectangle-profile-extrude summary. This is still a trace-only frontend-to-MIR proof; it does not change production grammar, route selection, BRepPlan, BRep, STEP, CIR, Boolean, or geometry behavior.


## AIR-X11 parser-backed emission boundary note

AIR-X11 establishes the first parser-backed Constructive AIR to existing emission-evidence boundary: a Firmament `box` fixture reaches `AirProfileExtrude` and then the existing profile extrusion wrapper/emitter summary. The boundary is trace-only and does not alter grammar, production routes, geometry implementation, BRepPlan semantics, CIR behavior, STEP export/import, route selection, or topology behavior.

## AIR-REGION-X1 status note

AIR-REGION-X1 adds a trace-only AIR Region skeleton: parser-backed box fixtures report a `RootRegion`, region fixtures can report metadata-driven `FaceAttachedRegion` yields with deferred integration, and no Boolean, geometry emission, production route replacement, grammar expansion, BRepPlan semantics, or CIR behavior is changed. See `docs/air-region-x1-region-model-skeleton-trace-fixtures.md`.

## AIR-REGION-X2 constitution note

AIR-REGION-X2 reinforces that AIR Region yields carry construction intent across scoped boundaries. The side-hole `FaceAttachedRegion` yield is explicit, local, and deferred, allowing later lowering to choose an integration route without treating Boolean subtraction as the region model.


## AIR-REGION-X3 constitutional note

The AIR Region side-hole CIR mirror is a side-channel analysis summary only. It reinforces that CIR evaluation mirrors can describe field behavior while AIR/BRepPlan/BRep/STEP retain their separate authority boundaries.

## AIR-REGION-X4 side-hole BRep boundary contract note

AIR-REGION-X4 treats side-hole BRepPlan boundary information as a trace-only contract, not as topology materialization. The side-hole `FaceAttachedRegion` may report affected parent face intent, entry/exit boundary intent, cut-wall intent, planned role strings, deferred elements, losses, and guarantees while retaining deferred parent integration and denying Boolean, BRepPlan element, BRep, STEP, CIR topology authority, grammar, and route-selection behavior changes.
