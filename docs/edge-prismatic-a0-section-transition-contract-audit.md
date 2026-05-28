# EDGE-PRISMATIC-A0 — Prismatic section-transition contract audit

## 1. Executive summary

EDGE-PROFILE-X2 proved, in FrictionLab scope, that a history-known top-edge chamfer can be represented as a **prismatic section transition** instead of a local edge edit. Its successful witness modeled a rectangular prism as explicit Z-stacked sections: a full rectangle at the lower/stable interval, a changed top rectangle at the chamfer boundary, vertex/edge correspondence between sections, ruled planar transition faces, and direct BRep emission. The witness exported through the existing STEP path and did not use AirEdgeSweep, `BrepBoundedChamfer`, topology grafting, or 3D Boolean fallback.

The current production/profile-stack machinery is directionally aligned with axis-stacked profile thinking, but it is not yet a general polygon/profile transition emitter. `ProfileStackExtrudeExecutor` currently consumes rectangular host extents plus layer intervals with optional inner circular cut radii, lowers those intervals through the safe box/cylinder composition route, and rejects stacks with no cut intervals. That is useful evidence for interval validation and diagnostics, but not a contract for arbitrary profile-section evolution.

A first-class prismatic section-transition contract is therefore needed before any production-adjacent `PrismaticSectionTransitionEmitter` or `AirPrismaticTransition` lane. The contract must make profile sections, orientation, correspondence, transition-face classification, admissibility, and rejection reasons explicit. Without that contract, top/bottom/horizontal chamfer work risks becoming either an accidental rewrite of the current circular-hole profile-stack executor or an AirEdgeSweep-shaped local edit under another name.

This milestone is documentation/design only. It defines the contract, admissibility boundary, code evidence, blockers, case classification, and lab sequence. It makes no production route changes, no public API changes, no STEP/Boolean changes, no AirEdgeSweep changes, no geometry implementation, and no test weakening.

## 2. Definition

A **prismatic section transition** is an axis-stacked sequence of resolved 2D profile sections, each placed at a known parameter/height along an extrusion axis, with explicit correspondence between profile boundary elements and deterministic transition-face emission between sections.

The essential ingredients are:

1. a single stacking axis, initially Z;
2. two or more resolved 2D sections in a known frame;
3. deterministic section order by axis parameter;
4. one or more stable or transition intervals between adjacent sections;
5. explicit vertex-to-vertex and edge-to-edge correspondence;
6. transition-face emission from corresponding boundary elements rather than from post-hoc edge discovery.

Examples:

- **Full rectangle -> inset rectangle over a transition interval:** a lower rectangle at `z1`, an inset rectangle at `z2`, and four trapezoid/ruled side faces between corresponding rectangle edges.
- **Rectangle -> chamfered rectangle over a top chamfer interval:** a full rectangle at the end of the stable body interval, a changed top rectangle at the cap, and the side transition face for the affected edge classified as the chamfer face.
- **Polygon -> scaled polygon:** an N-gon at `z1`, a scaled N-gon at `z2`, index-stable correspondence, and N transition faces.
- **Line/arc profile -> line/arc profile:** admissible later if each line or arc element has explicit correspondence and the output surface family is deterministic.

A prismatic transition is not merely “a loft.” It is a constrained, manufacturable, axis-stacked construction whose topology is determined from section evidence and correspondence.

## 3. Relationship to nearby concepts

### 3.1 Profile extrusion

Profile extrusion uses the **same profile along the extrusion axis**. The profile may already encode a chamfered vertical edge, but the cross-section does not change as it moves along the axis.

Examples:

- `rectangle -> box`;
- `pentagon -> vertical-edge chamfered box`, where a rectangle corner has been replaced by a bevel segment before extrusion.

EDGE-PROFILE-X1 and EDGE-PROFILE-V1 belong here: the vertical edge chamfer is a 2D profile-shape operation followed by normal profile extrusion.

### 3.2 Prismatic section transition

A prismatic section transition uses **profiles that change along the axis**. The transition itself is the interval between unlike, explicitly corresponding sections.

Example:

1. full rectangle section at `z1`;
2. inset or chamfered rectangle section at `z2`;
3. transition faces between corresponding edges;
4. cap faces at first/last sections as required by the solid.

Top and bottom chamfers on a history-known prism are natural candidates for this lane because the chamfer is an axis-local section evolution, not a mutation of an already-authored sharp body.

### 3.3 Current ProfileStackExtrudeExecutor

The current `ProfileStackExtrudeExecutor` is **not** a general prismatic section-transition emitter. Its current contract is better described as a rectangular host with axial circular cut intervals:

- `ProfileStackExtrudeSpec` stores host `Width`, `Depth`, `ZMin`, `ZMax`, and ordered `ProfileStackLayer` entries.
- `ProfileStackLayer` stores `ZMin`, `ZMax`, an optional `InnerCircleRadius`, role, and diagnostics.
- execution rejects stacks that provide no circular cut intervals;
- execution rejects non-positive circular radii;
- successful execution converts circular intervals to `SupportedBooleanHole` entries and invokes the box/cylinder safe-composition builder.

That means it can support through/stepped cylindrical hole interval scenarios, but it does not provide:

- a general polygon section profile type;
- arbitrary outer profile evolution;
- explicit profile correspondence;
- ruled polygon-to-polygon transition-face emission;
- direct cap/transition construction for changing outer boundaries.

This is the exact reason EDGE-PROFILE-X2 Route A was blocked while its lab-only Route B witness succeeded.

### 3.4 AirRuledTransition

`AirRuledTransition` is the more general conceptual neighborhood: profile-to-profile transition by ruled faces, not necessarily limited to a single axis-stacked, prismatic model. A prismatic transition may use ruled faces, but the prismatic contract is stricter:

- sections are ordered along one axis;
- each section has a frame/origin and axis parameter;
- the transition interval is bounded by adjacent sections;
- correspondence is part of the construction data;
- first-scope output is planar line-to-line faces.

If a future case needs arbitrary placement, non-parallel section frames, square-to-round adaptation, or unconstrained loft-like behavior, it should graduate toward `AirRuledTransition` rather than expanding prismatic first scope.

### 3.5 AirEdgeSweep

`AirEdgeSweep` is a no-history/local-edge operation. It remains valuable when the system has an already-built body and a selected edge, but lacks trustworthy construction history or profile-stack intent.

It is not preferred when construction history can emit final topology directly. For a history-known top chamfer on an extrusion, the intended solid can be emitted as section evolution; using AirEdgeSweep there would turn known constructive intent into local BRep surgery and would blur route authority.

### 3.6 Legacy BRep modification

Legacy BRep modification is a post-emission edit/mutation fallback or legacy route. It may remain authoritative for existing production cases until replacement parity is proven, but it is not the preferred representation when prismatic construction intent is known up front.

For prismatic transitions, the original sharp cap/side edge need not be emitted and then cut away. The final section profiles, transition edges, and transition faces are authored directly.

## 4. Required data model

This section names conceptual data shapes for a future production-adjacent lane. The names are recommendations, not API changes in this milestone.

Suggested names:

- `AirPrismaticSection`
- `AirPrismaticProfile`
- `AirPrismaticTransition`
- `PrismaticSectionTransition`
- `PrismaticSectionTransitionEmitter`
- `PrismaticCorrespondenceMap`

### Section

`AirPrismaticSection` / `PrismaticSection` should carry:

- axis parameter / Z value;
- resolved profile;
- section frame/origin;
- deterministic section orientation;
- role:
  - cap section;
  - stable interval section;
  - transition boundary section;
- diagnostics/provenance explaining whether the section came from a profile, a chamfer rule, a taper rule, or a lab fixture.

The axis parameter must be finite, sortable, and unique within one transition contract. Adjacent sections define positive intervals.

### Profile

`AirPrismaticProfile` should initially carry:

- one outer loop;
- line-only boundary elements;
- deterministic loop orientation;
- finite coordinates in the section frame;
- a resolved, already-normalized boundary suitable for BRep emission.

Later scopes may add line+arc profiles and holes. Holes should not be smuggled into first scope because they create inner-loop correspondence, void shell, and cap-loop policy questions that are separate from the top-edge chamfer motivating case.

### Correspondence

`PrismaticCorrespondenceMap` should carry:

- vertex-to-vertex mapping between adjacent sections;
- edge-to-edge mapping between adjacent sections;
- transition-face classification for every mapped edge pair;
- explicit edge role:
  - ordinary side;
  - chamfer transition;
  - taper transition;
  - unchanged/coplanar split;
  - deferred;
- rejection diagnostics for unmapped, ambiguous, reversed, or self-intersecting correspondences.

First scope should require explicit by-index correspondence and equal vertex/edge counts. Inferred correspondence is a later research problem, not a default convenience.

### Transition interval

`AirPrismaticTransition` / `PrismaticSectionTransition` should carry:

- start section;
- end section;
- axis span;
- transition mode:
  - linear ruled;
  - planar if line-to-line and coplanar vertices define a plane;
  - cylindrical if arc correspondence later permits it;
  - deferred;
- transition-face role/classification;
- policy for preserving or merging coplanar splits.

The interval must have positive axis span and finite geometry. If a face cannot be classified in first scope as planar line-to-line, the first-scope emitter should reject rather than approximate.

## 5. First-scope admissibility

The minimal first scope should be deliberately narrow:

- two or three sections along Z;
- line-only outer profiles;
- equal vertex/edge count between corresponding sections;
- explicit correspondence by index;
- no holes;
- no arcs;
- no multiple outer loops;
- no self-intersection;
- all transition faces planar;
- all dimensions finite;
- positive axis interval;
- deterministic orientation.

This first scope is sufficient for the motivating safe labs: rectangle-to-inset rectangle, top +X side chamfer reproduction, and equal-count polygon-to-polygon taper.

Explicitly deferred:

- line+arc profiles;
- holes;
- changing vertex count across sections;
- inferred correspondence;
- profile Boolean normalization;
- rounded/curved transitions;
- non-Z arbitrary axes unless trivial frame mapping is proven;
- edge/corner chains;
- square-to-round adapters;
- NURBS/freeform surfaces.

The admissibility result should be explicit and diagnostic-rich. If multiple bounded strategies later exist, JudgmentEngine scoring is appropriate for selecting among admissible prismatic/profile/AirEdgeSweep lanes. It should not be used for deterministic validation inside one already-selected prismatic emitter.

## 6. Emission contract

A first-scope `PrismaticSectionTransitionEmitter` should work from resolved sections and correspondence, not from a sharp source body.

Required emission steps:

1. create section vertices for each profile at each Z;
2. create profile edges within each section;
3. create transition edges between corresponding vertices across adjacent sections;
4. create cap faces at the first and last sections;
5. create transition faces between corresponding edges;
6. classify transition faces as planar and bind plane surfaces;
7. preserve explicit split faces even when coplanar if section-boundary evidence matters;
8. optionally allow a later, separate coplanar merge policy, but do not merge in the first lab.

For a stable interval followed by a transition interval, the emitter may have three sections: bottom/full, upper-stable/full, and top/changed. The middle section exists to preserve the stable side faces below the chamfer and the transition faces above it.

For the EDGE-PROFILE-X2 style top-edge chamfer:

- full rectangle section at `z0` / bottom cap;
- full rectangle section at `z1 = height - chamferDistance` / stable-to-transition boundary;
- inset or chamfered rectangle section at `z2 = height` / top cap;
- transition interval `z1 -> z2`;
- the `+X` side transition face is classified as the chamfer face;
- other transition faces may be ordinary, taper, or unchanged/coplanar split depending on the exact profile change.

The contract should also require topology summaries in labs: vertex count, edge count, face count, planar/cylindrical counts, loop/coedge counts, cap face count, transition face count, and role-specific face counts.

## 7. Current code evidence and blockers

### 7.1 ProfileStackExtrudeExecutor evidence

`ProfileStackExtrudeExecutor` supplies useful current evidence:

- it validates non-empty, contiguous, positive Z layers inside a global span;
- it emits deterministic diagnostics for layer count, roles, bounds, and radii;
- it represents axial intervals explicitly;
- it already carries the phrase “profile-stack executor route” in execution diagnostics;
- it can produce successful bodies for supported circular interval cases through existing safe composition.

But it is also the main current blocker for top-edge prismatic transition if reused directly:

- it rejects profiles with no cut interval;
- its only per-layer changing shape datum is `InnerCircleRadius`;
- it converts intervals into `SupportedBooleanHole` entries;
- it invokes `BrepBooleanBoxCylinderHoleBuilder.BuildComposition`;
- its host outer shape is a rectangular box, not a changing arbitrary outer profile.

Therefore it should not be broadened in-place during the first prismatic labs. A separate prismatic contract/emitter is cleaner and safer.

### 7.2 ProfileStackExtrudePlanAdapter and AirProfileStackExtrude

`ProfileStackExtrudePlanAdapter` confirms the current production-adjacent stack lane is hole-family oriented. It accepts a narrow through cylindrical stack, has a bounded stepped-hole branch, and explicitly defers countersink/chamfered-entry/conical plans plus blind/counterbore cases that need unsupported interval semantics.

The FrictionLab `AirProfileStackExtrude` model maps AIR-like layers back into `ProfileStackExtrudeSpec` using an outer rectangle and optional centered circle loop. That is useful as a historical bridge, but it is not a general `AirPrismaticTransition`: it lacks arbitrary outer profile sections and correspondence.

### 7.3 LineArcProfileExtrudeEmitter contribution

`LineArcProfileExtrudeEmitter` contributes implementation evidence for direct topology emission from resolved profile boundary elements:

- it validates a profile/extrude request before geometry emission;
- it supports one outer loop and can represent holes in its own profile-extrude lane;
- line edges become planar side faces;
- arc/full-circle edges become cylindrical side faces;
- it builds cap loops, side loops, edge curves, face surfaces, and a `BrepBody` directly;
- it records diagnostics that no 3D Boolean was used.

For prismatic first scope, the reusable idea is the direct profile-edge-to-face emission pattern, not the exact same-profile extrusion assumption. Line+arc prismatic transitions should be deferred until correspondence and surface-family rules for arcs are defined.

### 7.4 BrepExtrude, BrepPrimitives, and topology utilities

`BrepExtrude.Create` can already extrude a single `PolylineProfile2D` into cap and side faces. `BrepPrimitives.CreateBox` already emits simple rectangular prism topology. These are evidence that direct constructive BRep emission is a supported code style.

The lab-only EDGE-PROFILE-X2 witness uses `TopologyBuilder`, `BrepGeometryStore`, `BrepBindingModel`, line curves, plane surfaces, and `BrepBody` construction directly. That proves the low-level topology/geometry utilities can represent the needed first-scope output: closed all-planar bodies with explicit split transition faces.

However, there is no reusable production-adjacent helper today that takes a list of arbitrary sections and correspondence and emits transition faces. That missing helper is the primary implementation gap after the contract is accepted.

### 7.5 Step242Exporter evidence

`Step242Exporter.ExportBody` already exports single-body manifold BReps and supports plane/cylinder surface output used by adjacent labs. EDGE-PROFILE-X2’s witness STEP smoke passed with required STEP markers and without cylindrical surfaces or `BREP_WITH_VOIDS`. No STEP exporter/importer change is needed for first-scope all-planar prismatic witnesses.

### 7.6 Expected blockers

Expected blockers before production-adjacent prismatic work:

- no general polygon section profile type for prismatic stacks;
- no profile correspondence contract;
- no general transition-face emitter;
- current stack layers tied to inner circular cut intervals;
- current executor requires cut intervals;
- no coplanar split/merge policy for section-boundary evidence;
- no line+arc transition surface-family policy;
- no holes/inner-loop correspondence policy.

## 8. Case classification

| Case | Best representation | First-scope status | Needed contract | Current evidence | Next lab |
|---|---|---:|---|---|---|
| 1. top +X side chamfer on rectangle | Prismatic section transition | In first scope | Three Z sections, equal 4-edge correspondence, `+X` transition face role = chamfer | EDGE-PROFILE-X2 Route B closed all-planar STEP witness | EDGE-PRISMATIC-X2 |
| 2. top corner chamfer on rectangle | Prismatic section transition if same-count section can encode it; otherwise constructive corner manifest | Partial/deferred | Corner role policy and possibly changed vertex count or explicit corner patch classification | Constructive corner audit adjacent; no prismatic corner contract yet | After X2, decide prismatic vs corner-manifest fixture |
| 3. bottom edge chamfer | Prismatic section transition | In first scope if mirror of top side chamfer | Same as top chamfer with reversed interval/cap role | EDGE-PROFILE-X2 topology is mirrorable by concept, not yet witnessed | Add as X2 variant only after first emitter exists |
| 4. symmetric taper / inset rectangle | Prismatic section transition | In first scope | Two sections, equal rectangle correspondence, taper roles | Low-level topology supports planar transition quads | EDGE-PRISMATIC-X1 |
| 5. scaled polygon transition | Prismatic section transition | In first scope for line-only equal-count polygons | N-section profile, by-index correspondence, planar face validation | `BrepExtrude`/line profile evidence plus X2 direct witness style | EDGE-PRISMATIC-X3 |
| 6. rectangle-to-chamfered polygon with same vertex count | Prismatic section transition | In first scope only if equal count is explicit and no inference is needed | Explicit equal-count section authoring and face roles | X2 demonstrates changed rectangle with equal edge count for one side chamfer | EDGE-PRISMATIC-X2/X3 variant |
| 7. polygon-to-polygon with different vertex count | Usually `AirRuledTransition` or later prismatic with split correspondence | Deferred | Split/merge correspondence, fan faces, rejection policy | No current contract | Future correspondence audit |
| 8. line+arc profile transition | Later prismatic or `AirRuledTransition` depending placement | Deferred | Arc-to-arc correspondence and planar/cylindrical/ruled surface-family rules | `LineArcProfileExtrudeEmitter` supports same-profile extrusion side surfaces | EDGE-PRISMATIC-X5 audit |
| 9. profile with holes | Later prismatic profile-stack or separate hole-family lane | Deferred | Inner-loop correspondence, void/cap policy, shell/STEP expectations | Current profile-stack hole lane is circular interval specialized | Separate hole prismatic audit after outer-loop success |
| 10. square-to-round adapter | `AirRuledTransition` first; prismatic only with explicit advanced correspondence | Deferred / out of first scope | Different-count and line-to-arc/surface transition policy | Existing ruled/frustum labs are adjacent, not enough | Future `AirRuledTransition` lab, not X1-X3 |

The classification rule is: first-scope prismatic owns axis-stacked, line-only, equal-count, explicit-correspondence cases. `AirRuledTransition` owns broader loft/adaptation behavior. AirEdgeSweep owns no-history/local-edge editing behavior.

## 9. Testability plan

### EDGE-PRISMATIC-X1

**Purpose:** Build the smallest production-adjacent or lab-only first-scope emitter around the contract.

- Expected body: two-section rectangle -> inset rectangle transition.
- Expected topology: 8 vertices, rectangle cap faces, 4 transition side faces for the pure two-section case; if a stable interval is included, topology should explicitly count stable and transition faces separately.
- STEP smoke: required `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `PLANE`; no `CYLINDRICAL_SURFACE`, no `BREP_WITH_VOIDS`.
- Blockers to expose: invalid orientation, non-positive interval, non-planar transition quad, ambiguous correspondence.

### EDGE-PRISMATIC-X2

**Purpose:** Reproduce EDGE-PROFILE-X2 top-edge chamfer through the new prismatic transition emitter instead of a one-off lab witness.

- Expected body: 10 x 8 x 6 rectangular prism with a top `+X` side chamfer distance 1, using full rectangle section(s) and a changed top section.
- Expected topology: same contract-level counts as X2 witness unless the emitter intentionally records equivalent split policy; 12 vertices, 20 edges, 10 planar faces, 0 cylindrical faces is the current witness baseline.
- STEP smoke: same all-planar manifold smoke as X1.
- Blockers to expose: role classification of the chamfer transition face, preserving stable/transition split faces, correspondence diagnostics.

### EDGE-PRISMATIC-X3

**Purpose:** Prove the emitter is not hard-coded to rectangles or the top +X chamfer fixture.

- Expected body: equal vertex-count line-only polygon -> polygon transition, such as hexagon -> scaled/skewed hexagon.
- Expected topology: section/cap/transition counts scale deterministically with vertex count.
- STEP smoke: all-planar manifold BRep.
- Blockers to expose: self-intersection checks, orientation normalization, by-index correspondence robustness.

### EDGE-PRISMATIC-X4

**Purpose:** Audit coplanar split policy.

- Expected body: a transition where one or more corresponding edges are unchanged/coplanar, so the stable face and transition face could be merged geometrically but carry distinct section-boundary evidence.
- Expected topology: first result preserves split faces; optional comparison may show what a later merge would change.
- STEP smoke: both preserved-split and optional merged variants should export if represented.
- Blockers to expose: semantic naming/identity impact, face count expectations, downstream recognizer assumptions.

### EDGE-PRISMATIC-X5

**Purpose:** Audit line+arc transition readiness without implementing it by default.

- Expected body: none required unless the audit finds a safe line+arc subset.
- Expected topology: documented expected surface families for arc-to-arc and line-to-arc cases.
- STEP smoke: only if a bounded all-analytic witness is produced.
- Blockers to expose: cylindrical/conical/ruled surface classification, arc parameter correspondence, hole/slot interactions.

The lab sequence should stop if convergence fails: a failed lab must leave behind narrower blockers and stronger evidence rather than accumulating brittle geometry patches.

## 10. Relationship to chamfer roadmap

The chamfer roadmap should split by constructive intent:

- **EDGE-PROFILE-X1 / EDGE-PROFILE-V1:** vertical-edge/profile chamfers are profile modifications followed by extrusion.
- **Prismatic section transition:** top/bottom/horizontal edge chamfers on history-known prismatic bodies are section evolution along the extrusion axis.
- **AirEdgeSweep:** no-history or local-edge cases where construction history/profile stack is unavailable or intentionally bypassed.
- **Constructive corner manifest:** bounded polyhedral corner chamfers where final corner planes/vertices are known directly.
- **Legacy BRep modification:** existing production fallback/authority until row-specific parity is proven.

This split prevents AirEdgeSweep from becoming a universal chamfer hammer. It also prevents the current circular-hole profile-stack executor from being stretched into an unrelated polygon transition engine without an explicit contract.

## 11. Naming recommendation

Recommended stable names:

- `AirPrismaticTransition` for the conceptual AIR atom/lane;
- `PrismaticSectionTransitionEmitter` for the emitter;
- `PrismaticSection`;
- `PrismaticCorrespondenceMap`;
- `PrismaticTransitionFace`;
- `PrismaticTransitionRole`.

“Prismatic” is preferred over “polygon” because:

- it aligns with manufacturable/CNC/prism context;
- it includes line-only profiles now and line+arc profiles later;
- it focuses on axis-stacked section evolution rather than only loop shape;
- it leaves room for rectangle, polygon, and analytic profile sections under one constrained construction model.

Avoid names that imply unconstrained lofting. If a case is not axis-stacked with explicit section correspondence, it should probably be named under ruled transition or another broader concept.

## 12. Non-goals

This milestone explicitly does **not** include:

- implementation;
- production route changes;
- public API changes;
- broad profile-stack rewrite;
- current profile-stack production behavior changes;
- STEP exporter/importer changes;
- Boolean core changes;
- AirEdgeSweep changes;
- production chamfer/fillet behavior changes;
- new geometry implementation;
- test weakening;
- triangle migration retry;
- sketch solver;
- clipping engine;
- NURBS/freeform support;
- holes or line+arc prismatic emission in first scope;
- inferred profile correspondence.
