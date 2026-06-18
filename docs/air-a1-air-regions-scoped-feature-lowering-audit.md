# AIR-A1 — AIR Regions and scoped feature lowering audit

## 1. Executive summary

AIR Regions are proposed as scoped construction islands inside AIR. They allow local construction in a local frame without immediate global BRep mutation, and they make the boundary between nested construction intent and parent topology explicit.

Core doctrine:

- AIR Regions are scoped construction islands inside AIR.
- Regions allow local construction in a local frame without immediate global BRep mutation.
- Region effects escape only through explicit yields.
- Parent integration is explicit and route-selected.
- Boolean is not the region model; Boolean is one possible backend integration route for a region yield.
- AIR Regions are proposed to solve multi-axis and different-frame features without making global Boolean the default.

Slogan:

```text
AIR Regions preserve local construction intent.
Boolean is only one possible integration backend.
```

AIR-A1 is documentation/design only. It defines the constitutional target for future scoped/local construction in AIR and does not implement AIR Regions, change production routing, change BRepPlan semantics, change geometry, change Firmament grammar, change CIR mirror behavior, or change Boolean/BRep/STEP behavior.

## 2. Problem statement: multi-axis feature lowering

Aetheris has repeatedly encountered the same architectural pressure: a body may be authored primarily along one construction axis, while later features are authored from another axis or attached to another face. A Z-extruded base body can legitimately receive an X-directed side hole, a side pocket in a face-local frame, or a boss grown from a side face. Treating those features as immediate global Booleans is too blunt for the compiler pipeline.

The old failure mode is:

- the base body is constructed in one axis or profile/extrusion context;
- a later feature is naturally described in a different frame;
- the implementation constructs a 3D feature volume;
- the implementation immediately intersects/unions/subtracts that volume against the parent;
- feature identity, frame identity, profile intent, boundary roles, and topology intent are erased before AIR can select a constructive route.

That is unsuitable for Aetheris V2 because:

- immediate Boolean loses feature/topology intent;
- direct BRep mutation is too low-level for source-preserving compiler IR;
- CIR can represent field subtraction, but cannot author topology or own face/loop identity;
- imported/no-history topology recovery is decompilation, not native lowering;
- route selection cannot make a decision-grade choice if the feature has already been collapsed into raw geometry.

Examples that require scoped/local construction include:

- side hole through a Z-extruded box along X;
- side pocket on a side face;
- boss extruded from a side face;
- slot authored in a face-local coordinate system;
- mirror or patterned local features;
- shell and local offset contexts;
- future semantic recovery candidates that must be admitted as recognized construction rather than treated as native intent by default.

AIR Regions address this by making the different-axis feature a local scoped AIR Region with an explicit local frame, explicit yield, and explicit parent integration route.

## 3. Compiler-region analogy

AIR Regions are inspired by compiler IR region concepts such as MLIR regions, but mapped into Aetheris's geometry compiler pipeline.

In an MLIR-style region:

- a region contains scoped operations;
- local names and values are scoped;
- values and effects escape only through explicit yield/result operations;
- the parent sees only yielded values and declared effects.

In an AIR Region:

- a region contains scoped local construction operations;
- local frames, local selections, and local values are scoped;
- topology intent and geometric effects escape only through explicit yields;
- the parent sees only yielded values, boundary contracts, declared effects, and route-selection diagnostics.

Mapping:

| Compiler-region concept | AIR Region concept |
| --- | --- |
| local operations | local features/constructive operations |
| local SSA values/names | local frames, selections, profiles, bodies, patches |
| explicit yield | explicit geometric/topological yield |
| region result type | yield kind and boundary contract |
| effect boundary | declared parent integration effect |

An AIR Region is not merely a folder, grouping, or display hierarchy. It is a scoping and effect boundary. If an operation inside a region modifies parent topology, that modification must be represented as a yielded value plus a selected/admitted parent integration route; invisible parent mutation is invalid.

## 4. AIR Region definition

The proposed region shape is:

```text
AirRegion
  RegionId
  RegionKind
  ParentRegionId
  ParentBody
  LocalFrame
  EntryInterface
  Operations
  VisibleSelections
  YieldValues
  EffectKind
  BoundaryContract
  IntegrationPolicy
  Provenance
  Diagnostics
  KnownLosses
  Guarantees
```

Field definitions:

- `RegionId`: stable identifier for the scoped construction island, used in trace, diagnostics, BRepPlan provenance, and future fixture expectations.
- `RegionKind`: taxonomy value such as `RootRegion`, `FaceAttachedRegion`, or `PatternElementRegion` that communicates why the scope exists.
- `ParentRegionId`: optional parent region identifier. Root regions have no parent; nested features must identify the parent region.
- `ParentBody`: the body or body-like value the region is attached to or will integrate with. This is not permission for implicit mutation.
- `LocalFrame`: the coordinate frame in which scoped operations are authored and validated.
- `EntryInterface`: the declared inputs from the parent, such as attachment face, selected loop, edge, profile reference, pattern transform, or recovery candidate evidence.
- `Operations`: local AIR operations scoped to the region. These may be Feature AIR-like or Constructive AIR-like depending on the phase.
- `VisibleSelections`: parent selections intentionally visible inside the region, such as an attachment face or boundary loop. Parent topology is not globally visible by default.
- `YieldValues`: explicit values the region exports to the parent, such as a subtractive volume, additive body, replacement patch, or selection.
- `EffectKind`: declared effect category describing how the yield intends to interact with the parent.
- `BoundaryContract`: contract describing what escapes, what it may affect, admissible integration routes, and known losses.
- `IntegrationPolicy`: route-selection policy and admissibility constraints for consuming the region yield.
- `Provenance`: source feature id/span, construction lineage, frame derivation, and parent/child region lineage.
- `Diagnostics`: deterministic traceable events and rejection/fallback reasons.
- `KnownLosses`: explicit losses when lowering to a less expressive representation, especially Boolean or CIR mirror.
- `Guarantees`: affirmative statements the region makes, such as no implicit parent mutation, orthonormal frame validation, or topology authority remaining outside CIR.

## 5. Local frame model

The proposed local frame shape is:

```text
AirLocalFrame
  Origin
  XAxis
  YAxis
  ZAxis
  Handedness
  ParentTransform
  AttachmentSource
  SourceFaceOrNormal
  TangentOrReferenceDirection
```

Required concepts:

- `Origin`: local construction origin in parent coordinates or in a derivable parent-relative transform.
- `XAxis`, `YAxis`, `ZAxis`: basis vectors for local construction.
- `Handedness`: explicit right-handed or reflected/mirrored status, especially important for mirror-derived frames.
- `ParentTransform`: transform from local coordinates to parent coordinates.
- `AttachmentSource`: source of the frame, such as root world, profile extrusion, face attachment, edge attachment, user construction plane, pattern element, mirror transform, or recovery recognizer.
- `SourceFaceOrNormal`: face identity and/or normal when the frame is face-attached.
- `TangentOrReferenceDirection`: tangent/reference direction when a face or edge has multiple possible in-plane orientations.

Initial frame kinds:

- world/root frame;
- profile/extrusion frame;
- face-attached frame;
- edge-attached frame;
- arbitrary user/construction frame;
- pattern/mirror-derived frame;
- recovered/imported candidate frame.

The important doctrine is that local frames solve “different axis” features by making local construction explicit instead of forcing every operation into the parent construction axis. A side feature is not a corruption of the parent Z-extrusion frame; it is a nested construction scope with its own frame and an explicit contract for how its result integrates.

## 6. Region kinds

Initial taxonomy:

### `RootRegion`

Owns top-level body construction. A simple box or primary profile extrusion may live entirely in a root region without a nested region.

### `LocalFeatureRegion`

Generic nested scoped feature when no more precise kind is available. This should be a transitional category, not a dumping ground for arbitrary graph support.

### `FaceAttachedRegion`

Local feature attached to a face. Useful for side holes, side pockets, side bosses, slots, and other face-local construction. This is likely the first valuable nested region kind after `RootRegion`.

### `EdgeAttachedRegion`

Future edge/loop-local construction. This may become relevant for edge-attached grooves, local edge finishes, or loop-aware scoped construction, but should not replace existing bounded chamfer/fillet routes where region machinery is unnecessary.

### `PatternElementRegion`

Repeated child feature in a transformed local frame. Each repetition has its own scoped frame and yield; the parent pattern operation collects and integrates yielded effects explicitly.

### `MirrorElementRegion`

Mirrored child feature in a reflected local frame. Handedness and orientation losses must be explicit.

### `ShellRegion`

Future scoped inner-offset/rim construction. Shelling should not be modeled as an unstructured global offset/trim mutation; it needs explicit yields for replacement/constructed body and rim connections.

### `RecoveryCandidateRegion`

Imported/recovered topology candidate. This is explicitly not native construction unless a recognizer admits it with evidence and known losses.

The first implementation should likely start with `RootRegion` and `FaceAttachedRegion`, with trace-only summaries and frame validation before any production integration behavior is changed.

## 7. Region effect model

Effects must be explicit. No AIR Region may invisibly mutate its parent.

Initial effect kinds:

- `PureConstruction`: builds a local body/value without modifying the parent. The parent may later consume the yield, but construction itself has no parent effect.
- `Additive`: yields an additive body, boss, attachment, or union-like intent.
- `Subtractive`: yields a cut volume, pocket, hole, or removal intent.
- `Replacement`: yields a patch or rewritten boundary that replaces part of parent topology.
- `SelectionOnly`: yields a selection or local reference without geometry/topology construction.
- `AnnotationOnly`: yields metadata, provenance, or diagnostics only.
- `Unsupported`: declares that the requested local construction cannot currently be represented or integrated.

The effect kind is a compiler contract, not a geometry operation by itself. A `Subtractive` effect may be consumed by a high-level side-hole constructive insertion, a BRepPlan patch, an admitted Boolean fallback, or a deterministic reject/defer route depending on route selection.

## 8. Region yield model

A region may build rich local AIR internally, but the parent consumes only yielded values. Initial yield/value kinds:

- `YieldBody`: exports a local body-like value without declaring additive/subtractive intent by itself.
- `YieldAdditiveBody`: exports a body intended to add material to the parent.
- `YieldSubtractiveVolume`: exports a volume intended to remove material from the parent.
- `YieldReplacementPatch`: exports replacement topology or planned replacement boundary.
- `YieldProfileBoundary`: exports a resolved profile boundary for profile-domain integration.
- `YieldSectionStack`: exports a section stack for prismatic/transition-style integration.
- `YieldFaceLoopRewrite`: exports a loop rewrite around a face boundary.
- `YieldAttachmentInterface`: exports attachment/rim/interface information, such as a boss base rim or pocket entry loop.
- `YieldSelection`: exports a selection/reference for later operations.
- `YieldUnsupported`: explicitly marks unsupported or deferred construction.

Yield values should be typed strongly enough that parent integration does not rediscover intent from raw geometry. For example, a side pocket should not merely yield “some body”; it should yield a subtractive volume plus entry loop and blind-bottom intent where available.

## 9. Boundary contracts

Boundary contracts define what escapes the region and how the parent may consume it. Initial contracts:

- `DoesNotEscape`: no local value/effect escapes.
- `YieldsBody`: a body escapes without direct parent mutation.
- `YieldsCutVolume`: a subtractive volume escapes.
- `YieldsPatch`: a replacement or insertion patch escapes.
- `YieldsLoopRewrite`: a face/loop rewrite escapes.
- `YieldsAttachmentInterface`: an attachment/rim/interface escapes.
- `YieldsSelection`: a selection/reference escapes.
- `RejectedOrDeferred`: the boundary cannot be honored in this milestone/route.

A boundary contract should declare:

- what escapes;
- what parent topology it may affect;
- what selection, face, edge, loop, or body it is attached to;
- what integration routes are admissible;
- what topology/provenance losses are known;
- whether CIR mirroring is analysis-only;
- whether Boolean fallback is admitted, rejected, or not considered.

The boundary contract is where “local construction island” becomes compiler-visible parent behavior.

## 10. Parent integration routes

Integration route taxonomy:

- profile rewrite / profile-domain integration;
- prismatic/section-stack integration;
- face-attached constructive insertion;
- local BRepPlan patch/insertion;
- constructive manifest;
- AirEdgeSweep/AirFillet local route;
- BRep Boolean fallback;
- CIR-only analysis mirror;
- deterministic reject/defer.

Route selection rule:

```text
Use the highest-level constructive integration route that can emit final topology directly.
```

This is the same constitutional preference expressed elsewhere in Aetheris V2: preserve construction intent as long as possible, lower through explicit topology-generating plans when admitted, and avoid turning normal compilation into decompilation.

Boolean fallback policy:

- allowed only when explicitly admitted;
- must record provenance loss and topology role loss;
- must not erase the region's local construction intent from AIR trace/reporting;
- must not become the default representation for different-axis features;
- must be route-selected with rejection reasons for higher-level routes.

## 11. Relationship to Boolean

AIR Region is not Boolean. Boolean is one possible integration backend for a yielded region effect.

Bad default:

```text
construct side feature volume
Boolean subtract immediately
discard local feature/frame/profile intent
```

Good AIR model:

```text
create face-attached region
build local profile/extrusion in local frame
yield subtractive volume plus boundary/attachment contract
route select integration:
  profile/prismatic/patch if admitted
  Boolean fallback if necessary and admitted
  reject/defer otherwise
```

This distinction is the central reason AIR-A1 exists. The different-axis feature should remain a compiler-native feature with scoped intent, not be demoted to global Boolean merely because its axis differs from the base body's axis.

## 12. Relationship to CIR

CIR may mirror region effects for occupancy/analysis if admitted. CIR can represent additive/subtractive fields conveniently, but it cannot own topology.

A region-to-CIR mirror should declare losses such as:

- no face identity;
- no boundary loop identity;
- no patch identity;
- no topology parity;
- no parent integration topology;
- no STEP/export authority.

CIR mirror must not be the default path to BRep. For example, a side hole region may mirror as `parent field subtract cylinder` for analysis, containment, volume, or differential checks. The emitted BRep topology should still come from AIR, route selection, BRepPlan, and explicit topology integration.

## 13. Relationship to BRepPlan

BRepPlan may eventually represent region boundaries and integration patches. Region yields should eventually map to BRepPlan elements such as:

- local bodies;
- cut boundaries;
- rim/attachment loops;
- replacement patches;
- integration faces;
- entry loops and blind bottoms;
- generated side walls and transition faces.

BRepPlan must preserve region provenance and local frame. It should not infer region intent from raw geometry after the fact.

Potential BRepPlan additions for future milestones:

- region id on plan elements;
- parent/child region provenance;
- attachment interface roles;
- cut boundary roles;
- generated patch roles;
- integration route diagnostics;
- local frame metadata where needed for auditability.

AIR-A1 does not make any BRepPlan semantic change. It only defines what future region-aware BRepPlan work should preserve.

## 14. Relationship to Feature AIR and Constructive AIR

Regions may appear at both Feature AIR and Constructive AIR levels, but with different roles:

- Feature AIR may contain user-level feature regions or feature requests that canonicalize into regions.
- Constructive AIR may contain normalized regions with local frames and explicit yields.
- Regions are not necessarily needed for simple global profile extrusion.
- Regions are needed when scoping, local frame, visible parent selections, effects, and parent integration matter.

Examples:

- Box primitive does not need a nested region beyond the root.
- Top-face loop chamfer may be a simple feature selection rather than a region, unless local scoped construction becomes useful.
- Side pocket should likely be a `FaceAttachedRegion`.
- Pattern element should likely be a `PatternElementRegion`.
- Shell may become a `ShellRegion`.

Feature AIR should preserve source-facing intent; Constructive AIR should normalize the region into a topology-generating form that can either emit directly, route to an admitted plan, mirror to CIR for analysis, or reject/defer deterministically.

## 15. Example lowering sketches

### 15.1 Root box

Firmament:

- box size `10, 8, 6`.

AIR:

- `RootRegion`;
- Feature AIR `CreateBox`;
- Constructive AIR `AirProfileExtrude`.

No nested region is required because the root construction frame and the constructive profile/extrusion frame are sufficient. Existing AIR-X10/AIR-X11 box trace evidence should continue without region implementation changes.

### 15.2 Side through-hole along X

Firmament intent:

- box;
- hole attached to `+X` face;
- circular profile;
- through depth along local face normal / X axis.

AIR sketch:

- `RootRegion` builds the box.
- `FaceAttachedRegion` attaches to the `+X` face:
  - local frame origin on the face;
  - local `Z` or normal axis aligned to the inward/outward cut direction;
  - local circle profile;
  - local extrusion/cut operation;
  - yield `YieldSubtractiveVolume` plus attachment boundary contract.

Integration candidates:

- side-hole constructive insertion;
- BRepPlan patch/insertion;
- BRep Boolean fallback if explicitly admitted;
- reject/defer.

CIR mirror:

- parent minus cylinder may be valid for analysis;
- CIR is not topology authority and does not provide face/loop identity for the emitted BRep.

### 15.3 Side pocket

A side pocket is similar to a side through-hole, but with blind depth and a pocket bottom.

AIR sketch:

- `FaceAttachedRegion` on a side face;
- local profile in face-local coordinates;
- local cut depth into the parent;
- yield `YieldSubtractiveVolume`;
- boundary contract includes entry loop, side walls, and pocket bottom intent.

Integration candidates should prefer constructive insertion or BRepPlan patching over Boolean fallback when admitted.

### 15.4 Side boss

A side boss is additive rather than subtractive.

AIR sketch:

- `FaceAttachedRegion` on a side face;
- local profile in face-local coordinates;
- local extrusion outward or along declared normal;
- yield `YieldAdditiveBody`;
- yield `YieldAttachmentInterface` for the base/rim where the boss meets the parent.

Integration candidates include face-attached constructive insertion, local BRepPlan insertion, admitted Boolean union fallback, or reject/defer.

### 15.5 Patterned feature

The parent region owns the pattern operation. Each repetition is a `PatternElementRegion` with a transformed frame. Each element yields its local effect explicitly; the parent collects yields and integrates them via route selection.

This avoids treating patterning as a pre-expanded unstructured list of global Booleans. It also gives future diagnostics a natural place to report per-element rejection, collision, or fallback decisions.

### 15.6 Shell/open-face shell

A future `ShellRegion` should derive inner offset body and rim connections in a scoped construction context.

The shell should yield replacement/constructed body intent, rim loops, and boundary contracts rather than immediately performing an unstructured offset/trim mutation. Open-face shell cases should explicitly identify removed/open faces, inner wall construction, and rim integration.

## 16. Region admissibility and rejection policy

Deterministic rejection/deferred cases include:

- missing local frame;
- non-orthonormal or invalid local frame;
- ambiguous face attachment;
- unsupported source face family;
- unsupported effect kind;
- unsupported yield kind;
- unsupported integration route;
- region yield requests topology identity from CIR;
- parent/child frame mismatch;
- region affects parent without explicit yield;
- arbitrary imported topology without recognized recovery;
- Boolean fallback requested but not admitted;
- overlapping sibling regions without merge policy;
- patterns with unsupported collisions;
- shell/open-face shell requests without bounded policy;
- arbitrary graph support requests without a defined region contract.

No partial silent mutation is allowed. If a region cannot establish a valid frame, yield, boundary contract, and admitted integration route, the correct behavior is deterministic reject/defer with evidence.

## 17. Region provenance and diagnostics

Required provenance:

- source feature id/span if available;
- parent region id;
- local region id;
- local frame;
- attachment source;
- effect kind;
- yield kind;
- boundary contract;
- integration route;
- fallback status;
- known losses.

Diagnostics should include:

- region created;
- frame established;
- operation scoped;
- effect declared;
- yield produced;
- integration route selected;
- fallback/rejection reasons.

Suggested diagnostic code families:

- `air-region-created`
- `air-region-local-frame-established`
- `air-region-effect-declared`
- `air-region-yield-produced`
- `air-region-integration-route-selected`
- `air-region-boolean-fallback-admitted`
- `air-region-boolean-fallback-rejected`
- `air-region-invalid-frame-rejected`
- `air-region-implicit-parent-mutation-rejected`

Diagnostics should be designed for `aetheris trace` first. The goal is decision-grade visibility into why a region was admitted, mirrored, integrated, deferred, or rejected.

## 18. Trace and fixture implications

`aetheris trace` should eventually report regions with:

- region tree;
- local frames;
- region effects;
- yields;
- parent integration route;
- boundary contracts;
- BRepPlan region roles;
- CIR mirror losses;
- diagnostics and guarantees.

Future fixtures may include:

- side-hole valid fixture;
- side-pocket valid fixture;
- side-boss valid fixture;
- invalid implicit mutation fixture;
- invalid unsupported local frame fixture;
- invalid Boolean fallback not admitted fixture;
- invalid ambiguous face attachment fixture;
- pattern element collision/defer fixture.

Trace stages may include:

- `region-created`
- `region-yield`
- `region-integration`
- `region-rejected`

AIR-A1 does not implement any of these stages or fixtures. It documents the future need so AIR-X12 and later backend work can decide whether to continue profile-extrude BRepPlan depth first or pause for a trace-only region skeleton.

## 19. Implementation roadmap

Recommended future milestones:

1. `AIR-REGION-X1 — Region model skeleton and trace-only RootRegion/FaceAttachedRegion fixtures`
   - no geometry integration;
   - region tree summaries only;
   - local frame validation;
   - deterministic diagnostics;
   - no production route replacement.

2. `AIR-REGION-X2 — Side-hole FaceAttachedRegion mock yield`
   - metadata/fixture-driven;
   - yields subtractive volume;
   - integration deferred;
   - no BRep topology change.

3. `AIR-REGION-X3 — CIR mirror for side-hole subtractive region`
   - analysis only;
   - no topology authority;
   - explicit face/loop/patch identity losses.

4. `AIR-REGION-X4 — BRepPlan boundary contract for side-hole region`
   - planned boundary roles;
   - no production Boolean;
   - no direct BRep behavior change until route evidence exists.

5. `AIR-REGION-X5 — Admitted integration route for one side-hole fixture`
   - only after route is proven;
   - route-selected and diagnostically explainable;
   - fixture-backed topology evidence required.

AIR-X12 decision note: AIR-X12 can continue ProfileExtrude BRepPlan work if the goal is to deepen the current root-box lane. However, if the next motivating feature is side holes, side pockets, side bosses, shell, pattern, or face-local construction, the project should strongly consider pausing for `AIR-REGION-X1` first. Without a region skeleton, multi-axis work is likely to regress into immediate Boolean or ad hoc BRep mutation.

## 20. Non-goals

AIR-A1 explicitly does not include:

- no implementation;
- no AIR Region model code;
- no production route changes;
- no route-selection/JudgmentEngine behavior changes;
- no Boolean changes;
- no BRep topology changes;
- no STEP exporter/importer changes;
- no CIR evaluator or mirror behavior changes;
- no production analyzer/map behavior changes;
- no Firmament grammar changes;
- no geometry implementation;
- no side-hole implementation;
- no side-pocket implementation;
- no side-boss implementation;
- no shell implementation;
- no arbitrary graph support;
- no import/recovery implementation;
- no arbitrary STEP input to trace;
- no NURBS/freeform expansion;
- no triangle migration retry;
- no AirEdgeSweep route behavior changes;
- no BrepBoundedChamfer/BrepBoundedFillet behavior changes;
- no chamfer/fillet/shell geometry changes;
- no test weakening.

## AIR-REGION-X1 status note

AIR-REGION-X1 adds a trace-only AIR Region skeleton: parser-backed box fixtures report a `RootRegion`, region fixtures can report metadata-driven `FaceAttachedRegion` yields with deferred integration, and no Boolean, geometry emission, production route replacement, grammar expansion, BRepPlan semantics, or CIR behavior is changed. See `docs/air-region-x1-region-model-skeleton-trace-fixtures.md`.

## AIR-REGION-X2 status note

AIR-REGION-X2 implements a trace-only side-hole yield boundary contract for the metadata-driven `FaceAttachedRegion` fixture. The contract records `SideHole` feature intent, face attachment, circular profile radius, through/inward direction, parent-body-local affected scope, through-cut boundary intent, deferred exit boundary, and explicit-yield-only locality guarantees. Parent integration remains deferred and no production geometry, Boolean, BRepPlan, CIR, STEP, or grammar behavior changes.


## AIR-REGION-X3 status note

AIR-REGION-X3 demonstrates the A1 doctrine that region effects may be mirrored for analysis without transferring topology authority to CIR. The side-hole region reports an analysis-only parent-box-minus-cylinder mirror summary while BRepPlan, BRep, STEP, Boolean, and production integration remain deferred.

### AIR-REGION-X4 status note

AIR-REGION-X4 adds a trace-only BRepPlan boundary contract for the side-hole `FaceAttachedRegion`. The summary records the affected `+X` parent face, circular entry-loop intent, deferred opposite-side exit intent, deferred cylindrical cut-wall intent, planned semantic role strings, deferred elements, losses, and guarantees. It preserves A1 locality: no parent topology mutation, no BRepPlan materialization, no Boolean, no BRep emission, no STEP smoke, and no production route replacement.

## AIR-REGION-X5 note

AIR-REGION-X5 adds a trace-only side-hole integration route decision scaffold. The side-hole `FaceAttachedRegion` now reports deterministic candidate statuses, selects `DeferredIntegration`, rejects Boolean fallback as not admitted, keeps the CIR mirror analysis-only, and keeps the BRepPlan boundary contract as topology-side intent without materialization.

## AIR-REGION-X6 status note

AIR-REGION-X6 adds trace-only side-hole BRepPlan placeholder elements for the FaceAttachedRegion path. This is consistent with the AIR Region doctrine: parent integration remains explicit and deferred, Boolean remains rejected/not admitted, CIR remains analysis-only, and BRepPlan placeholders record future topology work without mutating parent topology or emitting BRep/STEP.

## AIR-REGION-X7 note

AIR-REGION-X7 consumes the controlled side-hole placeholder plan for the `+X` fixture and materializes standalone patch evidence for the entry loop, exit loop, and cylindrical cut wall. Parent BRep integration remains deferred; CIR remains analysis-only; Boolean is not generally admitted; no production route replacement or general side-hole support is introduced.


## AIR-REGION-X8 status note

AIR-REGION-X8 adds a controlled side-hole parent BRep integration attempt to the AIR Region trace. The attempt consumes the X2-X7 evidence chain and reports Outcome B: blocked at parent face splitting/loop insertion, with no fake integrated parent topology, no Boolean use, and no general side-hole support.

## AIR-REGION-X9 status note

AIR-REGION-X9 advances the side-hole parent integration evidence past the generic X8 `FaceSplitting` blocker. The controlled `+X` face now reports face-split and circular entry-loop evidence with `CutEntryLoop` consumed. Parent integration remains partial with `ExitLoopInsertion` as the next specific blocker; Boolean fallback and general side-hole support remain disallowed.
