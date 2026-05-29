# EDGE-LOOP-A0 — Face-boundary loop edge-finish audit

## 1. Executive summary

Face-boundary loops are **Class B edge-finish selections** from EDGE-A3. They are ordered, face-owned selections, not arbitrary edge graphs. Aetheris should treat them as first-class practical targets for uniform chamfer and uniform fillet support because they match common CAD intent: chamfer or fillet the rim of a face, pocket, slot, hole, or top cap.

This audit defines the decision-grade policy for representing, admitting, and lowering face-boundary loop edge-finish requests. It establishes:

- admissibility for face-owned loop selections;
- why Class B loop support does not imply Class D arbitrary graph support;
- loop subtype taxonomy and recommended first scope;
- uniform chamfer and fillet rule policy;
- route selection among profile rewrite, prismatic section transition, constructive manifest, AirEdgeSweep/AirChamfer, AirFillet, and legacy bounded routes;
- loop corner policy;
- deterministic rejection/deferred cases;
- the smallest follow-on labs.

No production behavior, public API, STEP exporter/importer, Boolean core, BRep topology, AIR emitter behavior, CIR analyzer behavior, chamfer/fillet route, geometry implementation, tests, triangle migration, or NURBS/freeform support changes in this milestone.

## 2. Definition of face-boundary loop selection

A **face-boundary loop edge-finish selection** is an edge-finish request derived from a boundary of one owning face.

Required selection data:

- **Owning face**: the face whose boundary supplies the selection context. The face provides local surface family, local normal/orientation if planar, and a bounded domain for interpreting the loop.
- **Outer loop vs inner loop**: the selected loop may be the owning face's exterior boundary or one of its interior trim boundaries. The first implementation scope may choose only one of these, but the representation should distinguish them explicitly.
- **Ordered boundary edges/coedges**: the selection is not merely a set of edge IDs. It is the ordered coedge sequence in the face loop, including each coedge's edge reference, direction, previous coedge, and next coedge.
- **Closed loop vs open chain around one face**: the baseline Class B target is a closed loop. A connected open chain along one face is also Class B-adjacent, but it has endpoint policy and partial-application questions and should be treated as a separate later scope.
- **Loop orientation**: orientation is inherited from the owning face loop/coedge order. For planar faces it determines inside/outside relative to the face normal and therefore affects inset/outset construction policy.
- **Adjacent faces across each boundary edge**: each selected boundary edge has the owning face on one side and, for a closed solid shell, an adjacent face on the other side. Route selection and convexity diagnostics need this relationship.
- **Loop vertices/corners**: the corners are the ordered vertices between adjacent selected coedges. Each corner has an incoming selected edge and outgoing selected edge, which makes loop corner policy bounded.

Clarification: a loop selection is selected **by face boundary**, not by an arbitrary edge list that happens to form a cycle. A loop may be an outer boundary or inner boundary. First scope should admit only the loop subtype whose route and diagnostics are proven; it should reject or defer the rest with deterministic reasons.

## 3. Difference from arbitrary edge graph

A face-boundary loop has bounded structure that Aetheris can exploit:

- natural order from the owning loop;
- face ownership;
- known local normal or surface context;
- known adjacent edge sequence;
- predictable corner sequence;
- uniform rule context for every selected edge and corner;
- clear all-or-nothing semantics for the whole loop.

An arbitrary edge graph does not provide those guarantees:

- no guaranteed order;
- ambiguous termination for chains and branches;
- mixed selected and unselected incident edges at vertices;
- uncontrolled corner valence;
- ambiguous adjacent-face ownership when the graph spans multiple faces;
- harder fallback semantics if one edge or corner fails;
- greater risk of silently partial results.

Conclusion: **Class B loop support must not be read as Class D arbitrary graph support**. Aetheris should ship loop support only under a face-boundary selection contract and should keep arbitrary graphs deferred until a separate graph admissibility and corner-patch policy exists.

## 4. Loop taxonomy

### Loop B1 — Outer loop of planar cap face

Examples:

- top face of a box;
- top face of a prism;
- cap face of a profile extrusion.

This is the strongest first-scope candidate. For history-known extrusions and prismatic bodies, the loop usually maps to profile or section-stack construction rather than generic edge mutation.

### Loop B2 — Inner loop of planar face

Examples:

- through-hole rim;
- pocket rim;
- slot rim.

This is an important practical target. It may lower through profile-hole/profile-loop infrastructure or prismatic transitions when the source construction is known. It should not be claimed until inner-loop orientation, hole-vs-island semantics, and adjacent cylindrical/planar wall policy are explicit.

### Loop B3 — Loop on side face / vertical wall

Examples:

- rectangular side face boundary;
- slot wall boundary.

This subtype is bounded but route-sensitive. Some side-face loops map to profile-boundary edits; others may imply top/bottom cap changes, wall-boundary trims, or no-history local lanes. It should follow after B1/B2 evidence.

### Loop B4 — Non-planar face loop

Examples:

- cylindrical face boundary;
- conical face boundary.

Likely deferred. Non-planar loops need surface-family-specific offset/sweep rules, seam handling, and corner behavior that should not be folded into the first planar loop scope.

### Loop B5 — Open chain around one face

Examples:

- selected partial rim along one face;
- three sides of a rectangular top face;
- a pocket edge chain with endpoints.

Defer or split into a separate audit. Open chains require endpoint caps, partial-corner policy, and mixed selected/unselected incident edge diagnostics.

Expected first scope:

- **Admit first**: closed planar outer loop of a history-known profile/prismatic body.
- **Possible early extension**: closed planar inner loop for hole/slot rims if existing profile-hole infrastructure makes it low-risk.
- **Defer**: non-planar face loops, open chains, imported/no-history loops, mixed-radius loops, mixed-distance loops, variable-radius loops, and arbitrary graphs.

## 5. Rule taxonomy for loops

### Uniform symmetric chamfer loop

A uniform symmetric chamfer loop applies one chamfer distance across all selected loop edges. It is the baseline chamfer loop target because one distance yields a consistent section inset/outset and a deterministic corner policy.

### Uniform constant-radius fillet loop

A uniform constant-radius fillet loop applies one radius across all selected loop edges. It is the baseline fillet loop target, but fillet route evidence should lag chamfer evidence because fillets require curved transition surfaces and corner patches.

Deferred rule families:

- unequal-distance chamfer loops;
- variable-radius fillet loops;
- mixed chamfer and fillet rules in the same loop;
- per-edge rule overrides;
- per-corner custom behavior;
- partial success where a subset of loop edges receives the finish.

## 6. Lowering strategy by loop type

| Loop subtype | Rule | Construction history known? | Preferred route | Fallback route | Current evidence | Missing evidence | Recommended next lab |
|---|---|---:|---|---|---|---|---|
| Outer loop of top cap | Uniform chamfer | Yes | Prismatic section transition with full lower section, full pre-chamfer section, and inset top section | Profile-loop rewrite when the cap loop maps more naturally to a 2D profile | `PrismaticSectionTransitionEmitter` exists for section stacks; `PrismaticTopEdgeChamferPrototype` proves a single top edge can be represented by a section transition | Whole-loop selection/admissibility, all-four-edge inset validation, corner diagnostics, corpus artifact | **EDGE-LOOP-X1** top-face outer-loop chamfer via prismatic transition |
| Outer loop of top cap | Uniform fillet | Yes | Future prismatic/profile fillet route using curved transition sections or authored arc profile | Future AirFillet if no constructive route applies | Profile/prismatic architecture supports constructive lanes conceptually | Fillet surface family, radius validation, corner blend patches, STEP/analyzer evidence | EDGE-FILLET-A0 architecture audit before implementation |
| Inner loop of through-hole rim | Uniform chamfer | Yes | Profile modification / profile-hole or prismatic transition candidate | AirEdgeSweep/AirChamfer only after loop policy exists | Profile-hole emitters and line/arc profile routes provide precedent for inner loops | Inner-loop orientation, adjacent cylinder/plane policy, self-intersection/inset checks | EDGE-LOOP-X2 or later inner-loop chamfer corpus |
| Inner loop of through-hole rim | Uniform fillet | Yes | Future profile-hole fillet route | Future AirFillet loop route | Hole recovery/profile-hole lanes identify practical source families | Cylindrical fillet patches, entry/exit loop policy, corner-free circular loop handling | EDGE-FILLET-A0 then inner-loop witness |
| Slot rim loop | Uniform chamfer | Yes | Line/arc profile modification candidate | Prismatic transition if slot is represented as stacked sections | Line/arc profile extrusion can emit line and arc loops | Slot-specific loop ownership and concave/convex corner classification | Slot-rim loop chamfer lab after top-loop evidence |
| No-history/imported planar face outer loop | Uniform chamfer | No | None for first scope | Future AirEdgeSweep/AirChamfer loop policy | AirChamfer/AirEdgeSweep labs show local no-history direction for narrower cases | Loop-level admission, corner policy, topology graft/patch rules, fallback authority | EDGE-LOOP-X3 no-history rejection diagnostics first |
| No-history/imported planar face outer loop | Uniform fillet | No | None for first scope | Future AirFillet loop policy | Fillet is architectural only for this class | Cylindrical patches, corner patches, topology insertion, artifact evidence | EDGE-FILLET-X1 single-edge fillet before loop fillet |
| Non-planar loop | Uniform chamfer or fillet | Either | Deferred | Deferred | Surface-family docs and analyzers identify non-planar faces but no loop finish route is admitted | Surface-specific sweep/offset policy, seam handling, corner rules | Separate non-planar loop audit |
| Open chain | Uniform chamfer or fillet | Either | Deferred or separate audit | Future chain-specific local lane | EDGE-A3 identifies chains as Class B-adjacent | Endpoint policy and mixed selected/unselected corner policy | EDGE-CHAIN-A0 if needed |
| Arbitrary graph | Any | Either | Out of scope | Out of scope | EDGE-A3 explicitly does not make arbitrary graphs baseline | Graph admissibility, valence policy, partial failure semantics | Class D audit only after Class B/C mature |

## 7. Corner policy for loops

Corners are easier for loops than for arbitrary graphs because:

- the loop gives an ordered sequence of vertices;
- every vertex has exactly one previous selected edge and one next selected edge in the selected loop;
- a uniform rule simplifies the local corner patch or profile inset construction;
- the owning face normal and adjacent-face relationships are available for convexity and orientation diagnostics;
- all-or-nothing loop semantics prevent silent partial application.

Corner classes:

- **Convex loop corner**: the finish removes material from an exterior corner under the selected rule.
- **Concave loop corner**: the finish rounds or bevels an interior/pocket/hole corner; inset/outset direction may invert relative to an outer loop.
- **Mixed convex/concave corner**: the adjacent faces or loop orientation disagree, often indicating a more complex trim or construction history.
- **Degenerate/tangent/short-edge corner**: the corner is tangent, nearly collinear, zero-length, below tolerance, or too short for the requested distance/radius.
- **Loop around hole versus outer rim**: inner loops invert intuitive inside/outside semantics and require explicit orientation handling.

First-scope corner policy:

- admit uniform rule only;
- reject edges or corner legs shorter than the required chamfer distance or fillet radius envelope;
- reject ambiguous, tangent, degenerate, or tolerance-dominated corners;
- reject self-intersecting inset/outset profiles;
- preserve deterministic corner diagnostics with edge/coedge/corner identity;
- do not silently partially apply a loop finish;
- do not merge away diagnostic section splits in the prismatic route by default.

## 8. Constructive routes for loop chamfers

### 8.1 Profile-loop rewrite

For history-known extrusion cap or side loops, Aetheris can often modify the 2D profile loop before extrusion. This is most useful when the requested loop finish maps to the profile boundary: for example, a vertical side-edge loop effect, a rectangular/capsule slot rim, or a hole profile whose rim can be represented by line/arc profile edits.

The main benefits are that the final BRep is emitted directly from construction intent and that topology is not mutated after the fact. The route must still validate loop orientation, profile self-intersection, minimum edge lengths, and whether the loop is outer or inner.

### 8.2 Prismatic transition

For top and bottom face loops, the natural constructive route is a prismatic section transition. A uniform top-face outer-loop chamfer can be represented as:

- lower full rectangle at `z = 0`;
- full rectangle at `z = height - d`;
- inset rectangle at `z = height`.

This route expresses the finish as a section-stack transition, not as AirEdgeSweep, `BrepBoundedChamfer`, topology grafting, trim/clipping, or a 3D Boolean. Split-preserving topology should remain the default because section boundaries are semantic evidence.

### 8.3 Constructive manifest

For whole-loop/corner combinations on simple body families, a constructive manifest can describe the intended finished topology without exposing a generic mutation API. This may be useful when profile or prismatic rewrite is awkward but the topology and body family are still known, such as rectangular-prism whole-top-loop or whole-body corner manifests.

### 8.4 AirEdgeSweep loop fallback

For no-history or imported loops, the eventual fallback is a loop-level AirEdgeSweep/AirChamfer route. It should not be used as a vague escape hatch. It requires explicit loop admission, adjacent-face family support, corner patch policy, topology insertion/graft policy, deterministic diagnostics, and artifact evidence.

## 9. Constructive routes for loop fillets

### 9.1 Profile-authored fillet

If the loop maps cleanly to line/arc profile modification, a profile-authored fillet can be represented as rounded profile corners before extrusion. This may produce cylindrical, ruled, or arc-derived side surfaces depending on the profile and extrusion family.

### 9.2 Prismatic fillet transition

A top or bottom loop fillet may need multiple sections, an analytic curved transition surface family, or an explicit fillet transition representation rather than a single planar chamfer interval. This is harder than chamfer and should not borrow chamfer readiness claims.

### 9.3 AirFillet loop route

The no-history/local loop case eventually belongs to an AirFillet loop route. It requires cylindrical or rolling-ball-like patches, corner patch policy, trim boundaries, and topology update semantics. Aetheris should prove a single-edge constant-radius plane-plane fillet before claiming loop fillets.

### 9.4 Legacy bounded route

Legacy bounded routes remain authoritative where current production behavior exists. Loop support should not replace production chamfer/fillet routes until the loop route has stronger route admission, topology, STEP, artifact, and analyzer evidence.

## 10. First-scope recommended target

Preferred first loop target:

**`EDGE-LOOP-X1 — Uniform chamfer around top face outer loop of rectangular prism via prismatic section transition`**

Candidate body and rule:

- rectangular prism width `10`, depth `8`, height `6`;
- selected loop: top face outer loop;
- rule: uniform symmetric chamfer distance `1`;
- section stack:
  - `z0 = 0`: lower full rectangle;
  - `z1 = height - d = 5`: full rectangle;
  - `z2 = height = 6`: inset rectangle;
- semantic result: all four top edges chamfered uniformly;
- topology policy: split-preserving by default;
- route exclusions: no AirEdgeSweep, no `BrepBoundedChamfer`, no 3D Boolean, no topology graft, no production route replacement.

This is preferred over a selection/admissibility-only lab because the single-edge prismatic top chamfer and section-transition emitter already provide route evidence. EDGE-LOOP-X1 should still include explicit selection/admissibility diagnostics as part of the lab. If the whole-loop inset/corner validation proves premature, the fallback lab should be **`EDGE-LOOP-X1 — face-loop selection/admissibility policy lab`**, with geometry deferred and no partial implementation.

## 11. Production-readiness gates for loop support

A loop route cannot be production-ready until it passes all relevant gates:

- **Loop selection/admissibility**: owning face, loop identity, loop kind, closure, rule kind, and construction history are explicit.
- **Ordered topology extraction**: coedge order, edge orientation, vertex sequence, adjacent faces, and face surface family are deterministic.
- **Uniform rule validation**: one distance/radius, finite positive value, minimum edge/corner clearances, and no mixed per-edge overrides.
- **Corner policy**: convex/concave/degenerate/tangent/short-edge cases produce deterministic success or rejection.
- **Route selection**: profile/prismatic/manifest/Air/legacy route choice is explicit and scored/admitted when multiple bounded strategies compete.
- **BRep topology/STEP**: emitted topology, loop/coedge counts, surfaces, bounds, and STEP smoke are stable for the claimed route.
- **Feature-recognition parity**: recognizers/analyzers understand the result or explicitly report unsupported diagnostics.
- **Artifact/corpus**: deterministic STEP/JSON summaries exist for admitted and rejected cases.
- **Analyzer/CIR mirror**: only claim CIR or analyzer parity after an admitted mirror or map route exists.
- **Fallback/legacy authority**: existing production behavior remains authoritative unless explicitly replaced by a proven route.
- **No partial success**: loop finish either applies to the whole admitted loop or rejects/defer with diagnostics.

## 12. Rejection/deferred policy

First-scope loop support should reject or defer deterministically for:

- non-closed loop when first scope is closed-loop only;
- non-planar owning face;
- unsupported loop kind, such as inner loop when only outer loop is admitted;
- non-uniform rule;
- mixed radius/distance;
- variable radius;
- unequal-distance chamfer;
- edge or corner leg too short for the requested distance/radius;
- self-intersecting inset/outset profile;
- ambiguous convexity;
- tangent, degenerate, zero-length, duplicate, or tolerance-dominated corners;
- unsupported adjacent face family;
- imported/no-history loop without admitted AirEdgeSweep/AirChamfer/AirFillet loop policy;
- open partial-loop chain when not in scope;
- arbitrary edge graph;
- any case requiring STEP, Boolean, BRep topology, CIR, AIR emitter, triangle migration, or production-route behavior changes outside the scoped lab.

## 13. Relationship to AIR/CIR/BRep authority

- The face-boundary loop selection is Firmament/AIR intent: it describes what the user selected and what uniform rule should be applied.
- BRep owns emitted topology: faces, loops, coedges, edges, vertices, surface bindings, and STEP emission are the result of an admitted lowering route, not proof that original selection intent can be recovered later.
- CIR mirror may validate occupancy, map behavior, or analyzer claims only after an admitted mirror exists for the constructed body family.
- STEP import does not recover loop selection intent by default. Imported topology can expose face loops, but that is not equivalent to a history-known face-loop finish request.
- AIR/CIR/BRep authority remains layered: intent and construction plan first, BRep emission second, CIR analysis only where admitted.

## 14. Roadmap after EDGE-LOOP-A0

1. **EDGE-LOOP-X1**: uniform top-face outer-loop chamfer via prismatic transition.
2. **EDGE-LOOP-X2**: artifact/corpus for loop chamfer, including admitted top-loop output and deterministic rejected cases.
3. **EDGE-LOOP-X3**: loop-selection diagnostics and no-history rejection policy; prove imported/no-history loops do not silently fall into unsupported mutation.
4. **EDGE-FILLET-A0**: fillet architecture audit using Class A/B/C taxonomy before implementing fillet geometry.
5. **EDGE-FILLET-X1**: single-edge constant-radius plane-plane fillet witness.
6. **EDGE-CORNER-X1**: whole-body/simple corner manifest audit/proof for bounded body families.

## 15. Non-goals

This milestone explicitly does not include:

- implementation;
- production behavior changes;
- public API changes;
- STEP exporter/importer changes;
- Boolean core changes;
- BRep topology changes;
- AIR emitter behavior changes;
- CIR node or analyzer behavior changes;
- production chamfer/fillet route replacement;
- new chamfer or fillet geometry;
- arbitrary graph support;
- test weakening;
- triangle migration retry;
- NURBS/freeform expansion;
- gated artifact corpus stability requirements by default.
