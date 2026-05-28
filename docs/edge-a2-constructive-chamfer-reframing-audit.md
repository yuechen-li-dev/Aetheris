# EDGE-A2 — Constructive chamfer reframing audit

## 1. Executive summary

Existing EDGE work is valid and should be preserved. EDGE-X3 through EDGE-V4 established a narrow but useful AirChamfer lane: policy admission, topology counts, geometry artifacts, closed witnesses, real-body probes, shadow routing, CLI artifacts, corpus evidence, and gated opt-in seams for controlled no-history/local-edge cases. The audit finding is not that this work is wrong; it is that some language and some implied execution assumptions are too shaped by post-hoc BRep editing.

The new theory is probable in the current codebase: many chamfers can and should be emitted as final constructive topology directly when construction context is known. A vertical edge of an extruded profile is usually a profile-shape question. A top or bottom edge of a prismatic extrusion is often a profile-stack or section-transition question. A simple convex polyhedral corner can often be emitted from final planes and vertices without discovering trims. In those cases, the BRep is an output representation, not the place where the operation should be authored by cutting an already-built sharp body.

Consequently, “trim” should often be interpreted as “construct the replacement adjacent face with its final boundary,” not “mutate the old face by cutting.” Likewise, a topology plan should be treated as a construction manifest when it names final output faces, offset boundaries, transition edges, and the omitted original sharp edge.

This audit concludes:

- the constructive reframing is plausible and already partially reflected by code;
- `BrepBoundedChamfer.CreateSingleCornerPlanarChamferBody` is already a direct constructive corner emitter for its bounded box corner case;
- `BrepExtrude`, `BrepPrimitives.CreateBox`, and `LineArcProfileExtrudeEmitter` already support line-only profile extrusion evidence suitable for a vertical-edge profile-chamfer lab;
- the current `ProfileStackExtrudeExecutor` validates the profile-stack idea but is not yet a general ruled section-transition emitter, because it currently lowers rectangular hosts with circular cut intervals through a bounded composition builder;
- `AirChamferTopologyPlan` already carries enough policy/count/intent information to be reinterpreted as a manifest, but it does not yet carry all explicit final-boundary coordinates needed for a general construction builder;
- future docs should avoid presenting AirEdgeSweep as the universal chamfer strategy.

The recommended convergence state is **success** for a docs/design audit: no production route changed, the theory is classified, and the smallest falsifiable lab sequence is now explicit.

EDGE-PROFILE-X1 follow-up: `ProfileChamferExtrudeLab` now exercises that smallest vertical-edge profile-chamfer sequence. It builds a chamfered rectangle/pentagon, emits it with `BrepExtrude.Create`, exports it through `Step242Exporter`, and records `10` vertices, `15` edges, `7` planar faces, `0` cylindrical faces, and one bevel side face without AirEdgeSweep, `BrepBoundedChamfer`, topology grafting, or 3D Boolean fallback. See `docs/frictionlab/edge-profile-x1-vertical-edge-chamfer-profile-extrude-lab.md`.

EDGE-PROFILE-V1 packaging note: `ProfileVertexChamferExtrudeEmitter` moves the same proof into an internal production-adjacent seam with deterministic admissibility, topology, STEP, and no-legacy-route diagnostics. It remains non-authoritative and does not replace production chamfer, fillet, primitive, STEP, Boolean, or AirEdgeSweep behavior. See `docs/edge-profile-v1-profile-authored-chamfer-emitter.md`.

EDGE-PROFILE-X2 follow-up: `ProfileStackChamferLab` now tests the top/horizontal-edge profile-stack theory. Route A records exact blockers in the current profile-stack model (no arbitrary polygon section profile contract and no ruled polygon-to-polygon transition emitter). Route B succeeds as a lab-only polygon section-transition witness for a top +X side chamfer on a 10 x 8 x 6 prism with chamfer distance 1, producing a closed all-planar STEP-exportable body without AirEdgeSweep, `BrepBoundedChamfer`, topology grafting, or 3D Boolean fallback. See `docs/frictionlab/edge-profile-x2-top-edge-chamfer-profile-stack-lab.md`.

EDGE-PRISMATIC-A0 follow-up: `docs/edge-prismatic-a0-section-transition-contract-audit.md` defines the prismatic section-transition contract requested by EDGE-PROFILE-X2. It names the future `AirPrismaticTransition` / `PrismaticSectionTransitionEmitter` lane, fixes first-scope admissibility to line-only explicit-correspondence Z-stacked sections, and records why current circular-hole profile-stack machinery should not be generalized in-place for top/horizontal chamfers.

## 2. Theory under audit

The audit tests a V2-compatible chamfer decomposition: do not trim if construction history can emit the final body directly.

### 2.1 Profile chamfer

For edges parallel to an extrusion or sweep axis, chamfer can be a 2D profile modification.

Example:

1. A box can be represented as `Extrude(rectangle)`.
2. Chamfering one vertical edge can be represented as replacing the rectangle corner with a bevel segment, producing a pentagon.
3. The result is `Extrude(chamfered pentagon)`.
4. The chamfer face emerges naturally as the side face associated with the new bevel segment.

No local BRep surgery is required in this framing. The original sharp vertical edge was never emitted.

### 2.2 Profile-stack chamfer

For edges perpendicular to the extrusion axis, chamfer may be a section transition or profile-stack operation.

Example:

1. Lower interval: full rectangle.
2. Transition interval: rectangle changing to a chamfered rectangle.
3. Upper interval: chamfered rectangle, or the reverse for a bottom-edge chamfer.
4. The chamfer face is the ruled surface connecting corresponding profile-section boundary elements.

This is still constructive emission. It requires profile correspondence and ruled transition surfaces, not a generic trim-first execution path.

### 2.3 Constructive corner chamfer

For simple convex polyhedral corners, final chamfer planes and vertices are often directly known from face normals, distances, and bounded corner context.

For equal-distance box-like corners, the replacement topology can be emitted from:

- retained original vertices not removed by the cut;
- three new vertices on incident edges;
- clipped adjacent faces;
- one planar triangular chamfer face;
- explicit edge and face bindings.

The key point is that this route does not need rolling-ball discovery, generic surface/surface intersection, or Boolean trimming. It can be a manifest-driven direct BRep constructor.

### 2.4 AirEdgeSweep remainder

AirEdgeSweep remains valuable for cases that cannot be naturally expressed as profile or profile-stack construction but still have explicit edge/faces intent. Examples include no-history imported bodies, selected local edges on bodies whose constructive profile lineage is unavailable, and future bounded surface-family cases where the operation is genuinely edge-local.

AirEdgeSweep should therefore be a remainder lane and a no-history/local-edge lane, not the universal chamfer implementation strategy.

## 3. Existing code evidence

### 3.1 `BrepBoundedChamfer.CreateSingleCornerPlanarChamferBody` is already constructive

`BrepBoundedChamfer.ChamferAxisAlignedBoxSingleCorner` and `ChamferTrustedPolyhedralSingleCorner` select a corner candidate with `JudgmentEngine` before reaching the bounded constructor. In the orthogonal trusted route, `CreateSingleCornerPlanarChamferBody` directly emits topology and geometry.

Evidence from the constructor:

- It creates a `TopologyBuilder` and adds vertices explicitly.
- It creates **10 vertices**:
  - seven retained box vertices (`v000`, `v100`, `v110`, `v010`, `v001`, `v101`, `v011`);
  - three chamfer-cut vertices (`vX`, `vY`, `vZ`).
- It creates **15 edges**:
  - retained original box-boundary edges where still present;
  - shortened/clipped edges on affected faces;
  - three edges of the triangular chamfer patch.
- It creates **7 faces**:
  - bottom face;
  - clipped top face;
  - two unmodified side faces;
  - two clipped adjacent side faces;
  - one triangular chamfer face.
- The chamfer face is `faces[6]`, bound to a plane with normal `(1, 1, 1)` and u-axis `(1, -1, 0)`.
- It adds explicit line curves for every edge and explicit planar surface bindings for every face.
- It validates bindings with `BrepBindingValidator.Validate`.
- It does not call the Boolean core, does not compute surface/surface intersections, and does not mutate an existing BRep face in place.

This is direct final-topology emission. It uses clipped/replacement adjacent face boundaries, but the code path is constructive rather than trim-discovery based.

There is a separate trusted-polyhedral path for non-orthogonal bodies, `CreateTrustedPolyhedralSingleCornerPlanarChamferBody`, reached when the trusted body is not the orthogonal constructor case. That path should be audited separately in a future corner-manifest lab, but the existing orthogonal route is already enough to prove the constructive framing is not foreign to the codebase.

### 3.2 Existing extrusion code supports the profile-chamfer theory

`BrepPrimitives.CreateBox` already builds a rectangle profile and lowers it through `BrepExtrude.Create`. `BrepPrimitives.CreateTriangularPrism` and `CreateHexagonalPrism` likewise create polyline profiles and lower through `BrepExtrude.Create`.

`BrepExtrude.Create` is a direct polyline-profile prism emitter:

- it allocates bottom and top vertices for each 2D profile vertex;
- it allocates bottom, top, and side edges;
- it emits bottom and top cap faces;
- it emits one side face for each profile segment;
- it binds line curves and planar surfaces deterministically.

Therefore, a chamfered rectangle represented as a five-segment line profile should naturally emit a pentagonal prism with one bevel side face. That is exactly the vertical-edge chamfer proof needed for a bounded lab.

`LineArcProfileExtrudeEmitter` gives an additional V2-facing profile lane. It accepts line segments, circular arcs, and full circles, validates one outer loop, emits cap faces, emits side faces per curve segment, and records diagnostics including `v2-v4-no-3d-boolean-used`. For a line-only chamfered rectangle/pentagon, it should be able to emit the vertical-edge chamfered prism without trim or Boolean work.

### 3.3 Profile-stack support is directionally aligned but not yet the full top-edge chamfer executor

`ProfileStackExtrudeExecutor` demonstrates an AIR/profile-stack style route with explicit layers, validation for contiguous z intervals, and diagnostics asserting a profile-stack executor route. However, current execution is specialized:

- `ProfileStackLayer` currently carries `InnerCircleRadius`, not arbitrary per-layer polygon loops.
- The executor rejects stacks with no cut intervals.
- It builds a rectangular host and recognized cylindrical hole intervals.
- It lowers through `SafeBooleanComposition` and `BrepBooleanBoxCylinderHoleBuilder.BuildComposition`, while recording that no 3D subtract route is used.

That means the top-edge chamfer theory is plausible architecturally, but the current executor likely cannot yet express “full rectangle to chamfered rectangle” as a general ruled polygon transition. The smallest useful lab may first prove the missing capability precisely rather than force an implementation.

### 3.4 `AirChamferTopologyPlan` is close to a construction manifest

`AirChamferTopologyPlan` already records:

- target edge start/end;
- edge direction;
- adjacent face normals;
- chamfer distance;
- convexity classification;
- adjacent face descriptors;
- original target edge count;
- offset curve count;
- new chamfer face count;
- transition edge count;
- affected adjacent face count;
- corner patch count;
- original edge replacement marker;
- trim-planned booleans;
- whether geometry emission was performed.

The plan therefore contains enough high-level intent and count data to function as a construction manifest for the controlled convex planar lab. It does not yet contain a complete general manifest for production emission because it lacks explicit final adjacent-face loops, stable identity mapping, complete corner/endpoint policy, and final-boundary coordinate lists for arbitrary bodies.

The important reframing is that `FaceATrimPlanned=true` and `FaceBTrimPlanned=true` can be read as “construct final adjacent-face replacement boundaries for face A and face B,” not as a requirement to mutate existing face topology by cutting it.

### 3.5 Fillet contrast

`BrepBoundedFillet` remains a useful contrast case. Fillet geometry usually involves circular/cylindrical/spherical/toroidal transition families and stronger corner policy. The constructive reframing still applies where a fillet can be profile-authored, but the immediate EDGE-A2 result is stronger for chamfers because planar chamfer faces and polyline profile segments are already well represented by current code.

## 4. Current terminology audit

| Term | Current risk | Classification | Recommended interpretation or future term |
|---|---|---|---|
| `trim planned` / `FaceATrimPlanned` / `FaceBTrimPlanned` | Sounds like mutating existing faces by cutting. | Reinterpret in docs; rename later only if code churn is justified. | `adjacent face final boundary planned`, `replacement boundary planned`. |
| `body mutation` | Implies editing an already-authored BRep is the goal. | Avoid in future labs except when intentionally testing mutation. | `final-topology emission`, `construction from manifest`. |
| `topology graft` | Useful for EDGE-X8 historical evidence but suggests patching onto an old shell. | Keep for historical docs; avoid as universal term. | `controlled local construction witness`, `manifest attachment proof`. |
| `replacement` | Mostly correct: the sharp edge is omitted/replaced by a chamfer face. | Keep, but define as output topology replacement, not in-place mutation. | `original edge omitted`, `constructed chamfer face`. |
| `adjacent face affected` | Correct but vague. | Reinterpret in docs. | `adjacent face receives final output boundary`. |
| `trimmed adjacent face patch` | Accurate for a local artifact, but too edit-shaped if generalized. | Reinterpret in docs; avoid in profile/profile-stack labs. | `replacement adjacent face patch`, `final adjacent face patch`. |
| `convex replacement geometry` | Acceptable for no-history/local-edge AirChamfer. | Keep with scope note. | `convex local construction geometry`. |
| `AirEdgeSweep universal chamfer route` | Misleading if implied. | Avoid in future labs. | `AirEdgeSweep remainder/no-history route`. |

Better terms for new docs and labs:

- construction manifest;
- output face boundary;
- final-topology emission;
- adjacent face replacement boundary;
- original edge omitted;
- constructed chamfer face;
- profile-authored chamfer;
- profile-stack transition chamfer;
- no-history/local-edge AirChamfer.

No code renames are recommended in EDGE-A2. Existing names are stable test/documentation anchors and should not be churned without an implementation milestone that changes the manifest contract.

## 5. Case classification matrix

| Case | Edge orientation/context | Best representation | Current supporting code | Is theory probable? | Smallest lab proof | Production implication |
|---|---|---|---|---|---|---|
| Vertical edge of extruded rectangle/box | Edge parallel to extrusion axis; source construction context known. | Profile chamfer: rectangle becomes pentagon; extrude final profile. | `BrepPrimitives.CreateBox`, `BrepExtrude.Create`, `LineArcProfileExtrudeEmitter`. | Yes, high. | Build pentagon profile, emit via `LineArcProfileExtrudeEmitter`, compare topology counts/STEP smoke to bounded legacy box chamfer where comparable. | Adds future constructive lane; no production behavior change until separately gated. |
| Vertical edge of arbitrary line-only prism | Edge parallel to extrusion axis; profile is line-only polygon. | Profile modification: replace selected profile vertex with bevel segment. | `BrepExtrude.Create`; `LineArcProfileExtrudeEmitter` for line loops. | Yes, high for simple non-self-intersecting polygons. | Triangle/hex/polygon profile with one beveled vertex; verify face count increases by one and no Boolean diagnostics appear. | Future replacement for history-known prism chamfers; must not retry triangle migration until recognition contract is handled. |
| Horizontal/top edge of extruded rectangle/box | Edge perpendicular to extrusion axis at cap/side boundary. | Profile-stack/ruled section transition. | Profile-stack docs and executor validate interval framing; current executor is cylindrical-hole specialized. | Medium: architecture likely, current executor incomplete. | Try full rectangle to chamfered rectangle stack; document exact missing polygon-section/ruled-transition capability if rejected. | Requires new lab executor capability before production consideration. |
| Single convex planar edge with no construction history | Local edge selected on BRep/imported/no lineage body. | AirEdgeSweep / construction manifest. | EDGE-X3-X6 labs, EDGE-V1/V2/V3/V4 docs/code, `AirChamferTopologyPlan`, `AirChamferGeometryArtifact`. | Yes, but scoped. | Continue current controlled AirChamfer shadow/opt-in fixture with manifest language and no universal claim. | Preserve as no-history/local-edge route; not universal. |
| Box corner with three chamfered edges | Three incident convex box edges. | Direct constructive corner topology / manifest-driven corner builder. | `BrepBoundedChamfer.CreateSingleCornerPlanarChamferBody` already hand-builds direct topology for bounded single-corner cut. | Yes, high for equal-distance polyhedral cases. | Reproduce the existing 10-vertex/15-edge/7-face body from a manifest data object and compare topology, STEP, feature recognition. | Future corner-manifest lab; legacy remains authoritative. |
| Triangle prism chamfer-sensitive route | Prism migration/recognition-sensitive; topology contracts load-bearing. | Legacy fallback until feature-recognition parity and adjacency contracts are solved. | `BrepPrimitives.CreateTriangularPrism`, `LineArcProfileExtrudeEmitter`, V2-A3 docs, triangle adjacency audit lab. | Mixed: representable geometrically, risky semantically. | Non-production parity fixture that proves profile chamfer does not break triangle recognition/adjoining corner contracts. | No triangle migration retry in this milestone. |
| Non-planar edge family | Plane-cylinder, cylinder-cylinder, cone, sphere/torus/freeform adjacency. | Future AirEdgeSweep/surface family or profile-authored only where construction context makes it trivial. | Fillet/chamfer matrices; no general implementation. | Low for immediate chamfer profile proof; future bounded analytic work. | One family-specific lab after planar cases stabilize. | Deferred; no NURBS/freeform expansion. |

## 6. Testability assessment

### Lab A: Profile chamfer extrusion

**Purpose:** prove vertical-edge chamfer as profile extrusion.

**Expected inputs:**

- line-only chamfered rectangle profile, e.g. rectangle with one corner replaced by two points and a bevel segment;
- extrusion height;
- optional legacy box-corner/edge chamfer comparator if topology can be normalized.

**Expected outputs:**

- one closed BRep prism;
- two cap faces;
- one side face per profile segment, including the chamfer face;
- line curves only;
- planar faces only;
- deterministic diagnostics showing no 3D Boolean.

**Exact tests:**

- a FrictionLab test invoking `LineArcProfileExtrudeEmitter.TryEmit` with a pentagon outer loop;
- topology count assertion: five profile sides produce five side faces plus two caps;
- surface-family assertion: all faces planar;
- STEP smoke assertion using existing STEP exporter route;
- optional comparison against a bounded legacy chamfered box in a normalized topology summary.

**Likely blockers:**

- `LineArcProfileExtrudeEmitter` is internal to Firmament materializer tests/labs, so the lab should live where internals are already visible;
- comparator topology may differ from legacy bounded edge/corner chamfer conventions even when geometry is equivalent;
- feature-recognition parity may require a separate contract.

**Recommended sequence:** first lab after EDGE-A2: **EDGE-PROFILE-X1**.

### Lab B: Profile-stack top-edge chamfer

**Purpose:** prove or falsify horizontal/top-edge chamfer as section transition.

**Expected inputs:**

- lower full rectangle section;
- upper chamfered rectangle section;
- transition interval height equal to chamfer distance or other explicit rule;
- profile-vertex correspondence map.

**Expected outputs:**

- one closed BRep with ruled transition faces;
- cap/side faces for stable intervals;
- chamfer transition face(s) from corresponding section edges or vertices;
- no Boolean trim/discovery.

**Exact tests:**

- start with a capability test against current `ProfileStackExtrudeExecutor` to document whether polygon section transitions are representable;
- if not representable, assert `UnsupportedProfileShape` with diagnostics naming the missing polygon-section/ruled-transition capability;
- once a lab executor exists, assert topology counts, ruled/planar face families, and STEP smoke.

**Likely blockers:**

- current `ProfileStackLayer` only models optional circular inner radii;
- current executor requires at least one cut interval;
- current executor builds rectangular host plus cylindrical hole intervals, not arbitrary polygon profiles;
- no profile correspondence contract exists for polygon-to-polygon transition.

**Recommended sequence:** **EDGE-PROFILE-X2** after Lab A.

### Lab C: Constructive corner manifest

**Purpose:** convert the already-constructive corner route into a manifest-driven proof without changing production behavior.

**Expected inputs:**

- box extents;
- corner token;
- chamfer distance;
- generated manifest listing retained vertices, new cut vertices, final face loops, edge curves, and face planes.

**Expected outputs:**

- topology equivalent to current `CreateSingleCornerPlanarChamferBody` for the bounded fixture;
- **10 vertices**, **15 edges**, **7 faces**;
- one triangular planar chamfer face;
- STEP smoke and feature-recognition parity at least equal to current legacy route.

**Exact tests:**

- lab-only builder emits from manifest;
- compare topology summary against current legacy single-corner chamfer;
- compare STEP smoke markers;
- run feature-recognition parity for the bounded corner route if existing recognition supports it.

**Likely blockers:**

- current constructor is hard-coded rather than data-manifest driven;
- stable identity naming for manifest items may need a lab-only schema;
- trusted non-orthogonal corner route should not be mixed into the first proof.

**Recommended sequence:** **EDGE-CORNER-X1** after Lab A or in parallel with Lab B if scoped to docs/test-only manifest generation.

### Lab D: AirEdgeSweep remainder

**Purpose:** keep current EDGE-V prototype path for no-history/local-edge cases while reframing topology planning as constructive manifest work.

**Expected inputs:**

- target edge endpoints;
- adjacent face normals/descriptors;
- chamfer distance;
- convexity classification;
- safety envelope;
- no construction-history profile context.

**Expected outputs:**

- policy decision;
- manifest-like topology plan;
- geometry artifact or closed witness in lab/prototype contexts;
- no claim that this is the universal chamfer route.

**Exact tests:**

- keep existing AirChamfer tests and corpus routes;
- add docs/assertions that accepted routes are no-history/local-edge experiments;
- avoid adding new production routing.

**Likely blockers:**

- old terminology can still imply mutation/grafting as the primary architecture;
- arbitrary body identity and endpoint/corner policies remain unsolved.

**Recommended sequence:** continue only as no-history/local-edge hardening after profile-chamfer proof clarifies the split.

## 7. Implications for current EDGE roadmap

EDGE-X13/V4-style shadow/opt-in work should **continue only in its current narrow role**: no-history/local-edge AirChamfer experimentation, controlled fixture evidence, fallback hardening, and non-authoritative diagnostics. It should not be widened under the assumption that AirEdgeSweep is the universal chamfer strategy.

The next milestone should shift to a profile-chamfer lab:

- **EDGE-PROFILE-X1:** vertical-edge chamfer as profile extrusion lab.
- **EDGE-PROFILE-X2:** horizontal/top-edge chamfer as profile-stack transition lab.

EDGE-V prototype docs should be updated over time to say they represent the no-history/local-edge path, not the universal chamfer path.

Compatibility matrix row statuses should not change to production support as part of EDGE-A2. The matrix should gain a note that some chamfer cases are reclassified as profile/profile-stack candidates, and row statuses should change only after lab evidence exists.

Expected roadmap conclusion:

- preserve EDGE-V paths as useful for no-history/local-edge cases;
- add profile/profile-stack branches;
- do not treat AirEdgeSweep as the universal chamfer implementation;
- update compatibility status only after the first profile chamfer proof.

## 8. Relationship to V2 architecture

This reframing is a direct consequence of V2 doctrine:

- resolved profile first;
- sweep first;
- declared topology over discovered topology;
- BRep as output and validation substrate;
- 3D Boolean or post-hoc trim only as fallback when constructive intent cannot be expressed earlier.

The resulting chamfer doctrine is:

- **Do not trim if you can construct the final topology.**
- **Do not edge-sweep if the chamfer is really a profile modification.**
- **Do not mutate a BRep if construction history can emit the chamfered body from the start.**

This is consistent with boxes as rectangle extrusions, prism families as profile extrusions, profile-stack interval work, and the existing hand-built bounded corner chamfer.

## 9. Recommended revised roadmap

- **EDGE-A2:** this constructive chamfer reframing audit.
- **EDGE-PROFILE-X1:** vertical-edge chamfer as profile extrusion lab.
- **EDGE-PROFILE-X2:** horizontal/top-edge chamfer as profile-stack transition lab.
- **EDGE-CORNER-X1:** manifest-driven single-corner chamfer reconstruction lab.
- **EDGE-V paths:** continue only for no-history/local-edge cases, controlled fixtures, shadow diagnostics, fallback hardening, and corpus evidence.
- **EDGE-A1 compatibility matrix update:** after the first profile chamfer proof, update relevant rows with profile/profile-stack evidence rather than broadening AirEdgeSweep status.
- **Future AirEdgeSweep/surface-family work:** proceed after planar/profile cases clarify the boundary between construction-history chamfers and local-edge chamfers.

## 10. Non-goals

EDGE-A2 does not do any of the following:

- no implementation;
- no production route changes;
- no public API changes;
- no code renames;
- no STEP exporter/importer changes;
- no Boolean core changes;
- no new geometry path;
- no new production routing;
- no test weakening;
- no triangle migration retry;
- no sketch solver, clipping engine, NURBS, or freeform support;
- no modification to current AirChamfer behavior except documentation notes;
- no deprecating legacy chamfer route yet.
