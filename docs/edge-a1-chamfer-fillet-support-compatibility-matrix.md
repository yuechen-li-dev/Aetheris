# EDGE-A1 — Chamfer/fillet support compatibility matrix

## 1. Executive summary

This document defines what “full chamfer/fillet support” means for Aetheris within its bounded analytic modeling scope. It is a compatibility and readiness matrix, not an implementation plan that changes behavior.

The current AirChamfer golden path is strong but narrow: controlled convex planar single-edge chamfers now have Judgment-backed policy, topology planning, geometry artifacts, closed witnesses, controlled topology graft evidence, a production-adjacent prototype, feature-recognition parity probes, a non-authoritative shadow route, CLI STEP artifacts/corpus coverage, gated corpus stability evidence, and one internal/test-only gated Firmament opt-in route for the controlled CH-03 fixture.

Legacy bounded chamfer/fillet behavior remains authoritative wherever production behavior exists. In particular, `BrepBoundedChamfer` and `BrepBoundedFillet` are still the production routes for the currently supported bounded cases, and AirEdgeSweep/AirChamfer evidence must not be read as route replacement.

AIR-CIR-A0 adds an analyzer caveat for these lanes: map/volume/containment support should be read as backend- and mirror-scoped, not as general chamfer/fillet topology proof. `analyze map` may use BRep raycast where the explicit BRep query backend admits the body, or CIR/FRep only when an admitted mirror exists; AirChamfer and fillet routes remain `mirror-unavailable` by default until a separate mirror milestone proves a bounded reduction.

EDGE-A3 adds a selection-pattern taxonomy over the matrix: baseline edge-finish support should be interpreted as selection-pattern driven, with Tier 1 single-edge, Tier 2 face-boundary loop, and Tier 3 whole-body canonical edge-set cases prioritized under uniform symmetric chamfer or constant-radius fillet rules. Arbitrary edge graphs, mixed per-edge rules, unequal-distance chamfers, and variable-radius fillets remain deferred/advanced unless a future bounded policy admits them. See `docs/edge-a3-edge-finish-selection-taxonomy.md`.

EDGE-A2 reclassifies some chamfer cases as profile/profile-stack candidates rather than AirEdgeSweep candidates: vertical edges of history-known extrusions should be tested as profile modifications, and top/bottom extrusion edges should be tested as profile-stack or section-transition constructions. EDGE-PROFILE-X1 now provides lab evidence for the vertical-edge/profile-authored subset, EDGE-PROFILE-V1 packages that proof into an internal production-adjacent emitter, and EDGE-PROFILE-X2 provides lab-only section-transition evidence for one top horizontal +X side chamfer while documenting current profile-stack generalization blockers. EDGE-PRISMATIC-A0 classifies those top/bottom/horizontal history-known chamfers under the **prismatic section-transition** roadmap, EDGE-PRISMATIC-X1 introduces the first reusable lab-only prismatic emitter, EDGE-PRISMATIC-X2 re-expresses the top +X witness through that emitter, EDGE-PRISMATIC-X3 proves equal-count line-only generic polygon cases, EDGE-PRISMATIC-V1 packages that capability behind an internal production-adjacent seam, and EDGE-PRISMATIC-V2 adds an internal controlled top `+X` horizontal chamfer route that consumes that seam for the rectangular-prism case. Production statuses remain unchanged until a separately gated production route is admitted.

The matrix separates five concerns that were previously easy to conflate:

- current legacy support;
- AirEdgeSweep/AirChamfer progress;
- missing labs and proof artifacts;
- production readiness;
- remaining gaps before Aetheris can claim full support.

## 2. Support taxonomy

Support is classified across the following dimensions.

### Feature type

- **Chamfer**: a straight bevel or edge replacement profile, usually producing a planar or ruled replacement surface in the current bounded analytic scope.
- **Fillet**: a rounded edge replacement or internal manufacturing radius, usually producing cylindrical, spherical, toroidal, or other analytic transition surfaces depending on adjacent faces and corner policy.

### Material/topology role

- **Concave/additive**: the operation adds or smooths material in an internal edge/valley context, such as an internal manufacturing fillet or concave chamfer patch.
- **Convex/replacement**: the operation removes/replaces a sharp outside edge or corner with a transition surface and updated adjacent-face trims.

### Selection scope

- **Single edge**: exactly one bounded edge or edge-like selector.
- **Edge chain**: a connected sequence of edges with a common rule and termination behavior.
- **Corner chain**: multiple incident or sequential corners requiring transition patches.
- **Full-body edge set**: a broad body-level selection, such as all admissible outside edges or all edges matching a rule.

### Adjacent face families

- **Plane-plane**: both adjacent faces are planar.
- **Plane-cylinder**: one planar and one cylindrical adjacent face.
- **Cylinder-cylinder**: two cylindrical adjacent faces.
- **Plane-cone**: one planar and one conical adjacent face.
- **Cone-cylinder**: one conical and one cylindrical adjacent face.
- **Sphere/torus adjacency**: spherical, toroidal, or corner-transition-adjacent surfaces participate in the edge definition.
- **Unsupported/freeform**: NURBS/freeform or unclassified surfaces outside the bounded analytic scope.

### Profile rule

- **Constant chamfer distance**: one uniform distance or equivalent symmetric bevel rule.
- **Unequal distance chamfer**: two distances or offsets, one per adjacent face.
- **Constant fillet radius**: one uniform radius.
- **Variable radius fillet**: radius varies along an edge or chain.

### Corner policy

- **No corner**: the selected edge does not require endpoint transition logic beyond ordinary trims.
- **Single endpoint transition**: one endpoint intersects another modified or terminating edge.
- **Two endpoint transitions**: both endpoints require transition decisions.
- **Three-edge convex corner**: a box-like outside corner where three convex edges meet.
- **Concave corner**: an internal corner requiring additive/smoothing transition policy.
- **Mixed-radius corner**: incident fillets/chamfers do not share one distance or radius.
- **Chain termination**: explicit end treatment for chains and partial-body selections.

### Output surface family

- **Plane**: planar chamfer face or planar patch.
- **Cylinder**: constant-radius fillet face along a straight edge.
- **Cone**: conical or tapered analytic transition.
- **Sphere patch**: equal-radius corner blend patch or related analytic corner surface.
- **Torus**: rolling-ball style transition in admitted analytic cases.
- **Ruled surface**: straight-line swept transition surface, including some unequal/tapered cases if admitted later.
- **Deferred/unsupported**: any surface family not currently represented or validated.

### Execution mode

- **Legacy `BrepBoundedChamfer/Fillet`**: production-authoritative bounded BRep route.
- **AirEdgeSweep lab**: lab-only edge-sweep architecture, policy, geometry, or witness proof.
- **AirChamfer prototype**: production-adjacent but non-authoritative AirChamfer path.
- **Shadow route**: non-authoritative evaluation beside legacy behavior.
- **Production-authoritative**: the route that is allowed to determine user-visible production output.

### Validation

- **Topology**: topology plan, graft, adjacency, trim, and identity evidence.
- **STEP**: export smoke or artifact checks for emitted bodies.
- **Feature-recognition parity**: recovered feature/topology semantics match the legacy-sensitive contract, not just the geometry.
- **CLI/artifact corpus**: repeatable user- or developer-facing artifact generation.
- **Gated stability**: explicit opt-in checks proving corpus and artifact determinism across repeated runs.

## 3. Status vocabulary

The matrix uses the following stable status values.

| Status | Meaning |
|---|---|
| `production-legacy-supported` | Supported by the legacy production route today. |
| `production-air-supported` | Supported by an AirEdgeSweep/AirChamfer/AirFillet route that is production-authoritative. No current row has this status. |
| `production-adjacent-air-prototype` | Has a real-body or production-adjacent Air prototype but is not authoritative. |
| `shadow-supported` | Evaluated by a non-authoritative shadow path with legacy authority preserved. |
| `lab-supported` | Supported in a bounded lab/proof fixture only. |
| `policy-supported` | Has explicit admissibility/scoring/rejection policy evidence. |
| `topology-plan-supported` | Has topology-plan evidence but not necessarily full body mutation. |
| `geometry-artifact-supported` | Has geometry artifact evidence. |
| `closed-witness-supported` | Has a closed-shell/body witness in lab scope. |
| `artifact-corpus-supported` | Has CLI/artifact corpus evidence. |
| `deferred` | Intentionally out of current scope, with no attempt to implement in this milestone. |
| `unsupported` | Not supported and not merely waiting for a proof upgrade. |
| `unknown/no-test` | Plausible or legacy-sensitive behavior has no explicit test/evidence row found. |
| `blocked-by-legacy-topology-contract` | Cannot migrate until legacy topology/identity/adjacency contracts are proven. |
| `blocked-by-corner-policy` | Requires corner or chain transition policy that does not yet exist. |
| `blocked-by-surface-family` | Requires analytic surface classification/generation not yet admitted. |
| `blocked-by-body-mutation` | Requires arbitrary or broader real-body topology mutation/grafting beyond controlled cases. |
| `blocked-by-feature-recognition-parity` | Geometry may be possible, but feature-recognition parity is missing or legacy-sensitive. |

## 4. Current golden path summary

The strongest current AirChamfer path is:

1. convex plane-plane single-edge chamfer;
2. controlled/simple body only;
3. JudgmentEngine-backed admissibility and scoring policy;
4. topology plan evidence;
5. geometry artifact evidence;
6. closed witness evidence;
7. controlled local topology graft evidence;
8. production-adjacent real-body prototype evidence;
9. feature-recognition parity probe evidence;
10. non-authoritative shadow route evidence;
11. CLI STEP artifact and corpus evidence;
12. gated corpus stability evidence.

This path is not production-authoritative. It does not support chains, corners, fillets, arbitrary selections, arbitrary production bodies, or production route replacement. It also does not authorize a triangle migration retry or any STEP exporter/importer, Boolean core, sketch solver, clipping engine, NURBS, or freeform expansion.

## 5. Chamfer compatibility matrix

| Case ID | Convex/concave | Adjacent face family | Selection scope | Distance/rule | Corner policy | Expected output surface | Current legacy status | Current AirEdgeSweep/AirChamfer status | Evidence docs/tests | Missing evidence | Production readiness | Next milestone |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| CH-01 | concave/additive | plane-plane, orthogonal | single edge | constant chamfer distance | no corner | plane | `production-legacy-supported` where bounded internal concave route applies | `lab-supported`; `policy-supported` | EDGE-X1 inventory; EDGE-X2 concave planar patch proof; EDGE-X2.1 policy scaffold | production Air route, shadow parity, corpus | Legacy production only; Air is lab/policy evidence | Preserve legacy; use as concave AirChamfer baseline |
| CH-02 | concave/additive | plane-plane, non-orthogonal | single edge | constant chamfer distance | no corner | plane | `unknown/no-test` to `production-legacy-supported` only where existing bounded trusted contexts admit it | `lab-supported`; `policy-supported` | EDGE-X2.2 non-orthogonal concave planar policy+patch | production body mutation, feature-recognition parity, corpus | Not production Air-ready | Add parity/shadow fixture after convex route stabilizes |
| CH-03 | convex/replacement | plane-plane, orthogonal controlled body | single edge | constant chamfer distance | no corner | plane | `production-legacy-supported` for legacy bounded box/simple contexts | `production-adjacent-air-prototype`; `shadow-supported`; `artifact-corpus-supported`; `closed-witness-supported`; `gated-opt-in-route-supported`; `profile-authored-lab-supported`; `profile-authored-production-adjacent-emitter-supported` for history-known vertical extrusion edges; `profile-stack-section-transition-lab-supported`, `prismatic-emitter-lab-supported`, `prismatic-controlled-route-supported`, and `prismatic-artifact-corpus-supported` for split-preserving first-scope section transitions and one history-known top +X horizontal edge fixture | EDGE-X3 through EDGE-X13, EDGE-V1/V2/V3/V4, EDGE-PROFILE-X1, EDGE-PROFILE-V1, EDGE-PROFILE-X2, EDGE-PRISMATIC-A0, EDGE-PRISMATIC-X1, EDGE-PRISMATIC-X2, EDGE-PRISMATIC-X3, EDGE-PRISMATIC-V1, EDGE-PRISMATIC-V2, EDGE-PRISMATIC-X4, EDGE-PRISMATIC-X5, CLI tests, FrictionLab AirChamfer tests, ProfileChamfer tests, ProfileStackChamfer tests, ProfileVertexChamfer tests, Firmament shadow-diagnostics and opt-in route tests | default production authority, arbitrary body mutation beyond controlled fixture, production-adjacent profile chamfer admission, production-authoritative route admission, validation hardening, and recognition parity | Best current AirChamfer row for no-history/local-edge fixtures; EDGE-PROFILE-X1 proves history-known vertical-edge chamfer can be emitted as profile extrusion; EDGE-PROFILE-V1 packages that path as an internal production-adjacent emitter, still not production-authoritative; EDGE-PROFILE-X2 proves one top horizontal +X side chamfer can be emitted as a lab-only all-planar section-transition witness and records current profile-stack polygon/ruled-transition blockers; EDGE-PRISMATIC-A0 classifies top/bottom/horizontal history-known chamfers under the prismatic section-transition roadmap; EDGE-PRISMATIC-X1 supplies the reusable lab emitter; EDGE-PRISMATIC-X2 confirms the top-edge witness through that emitter; EDGE-PRISMATIC-V1 packages the internal emitter seam; EDGE-PRISMATIC-V2 consumes it in a controlled internal top-edge chamfer route with matching topology and STEP smoke; EDGE-PRISMATIC-X4 fixes split preservation as the default policy; EDGE-PRISMATIC-X5 adds deterministic split-preserving STEP/JSON corpus evidence | Run EDGE-PRISMATIC-X6 gated corpus stability before any production-authoritative prismatic top-edge chamfer route admission |
| CH-04 | convex/replacement | plane-plane, non-orthogonal controlled body | single edge | constant chamfer distance | no corner | plane | `production-legacy-supported` only for admitted trusted/triangle-like bounded contexts; otherwise `unknown/no-test` | `shadow-supported`; `artifact-corpus-supported`; `production-adjacent-air-prototype` for safe controlled case | EDGE-X11 non-orthogonal corpus row; EDGE-X12 stability; EDGE-V3 shadow route | broader topology mutation, richer feature-recognition parity, production fallback | Non-authoritative controlled evidence only | Harden controlled non-orthogonal shadow diagnostics |
| CH-05 | convex/replacement | plane-plane | single edge on arbitrary production body | constant chamfer distance | no corner or endpoint transition as needed | plane | `production-legacy-supported` only for legacy recognized bounded cases | `deferred`; `blocked-by-body-mutation`; partial `production-adjacent-air-prototype` for controlled body | EDGE-V2/V3 controlled prototype and shadow docs | arbitrary edge selection, grafting, identity, regression corpus | Not production Air-ready | EDGE-X15 convex AirChamfer controlled body mutation hardening |
| CH-06 | convex/replacement | plane-plane | edge chain | constant chamfer distance | chain termination; endpoint transitions | plane sequence plus transition patches | legacy support is bounded/contextual; generic status `unknown/no-test` | `deferred`; `blocked-by-corner-policy`; `blocked-by-body-mutation` | EDGE-V3 deferred edge-chain diagnostics; EDGE-X1 notes | chain policy, termination policy, topology plan, feature-recognition parity | Not ready | EDGE-X16 AirChamfer edge-chain policy lab |
| CH-07 | convex/replacement | plane-plane | box corner / three-edge corner | constant chamfer distance | three-edge convex corner | plane tri-corner patch or multiple planes | `production-legacy-supported` for bounded box/corner route | `deferred`; `blocked-by-corner-policy`; `blocked-by-feature-recognition-parity` | EDGE-X1 bounded corner inventory; V2-A3 topology contract docs | Air corner policy, topology graft, recognition parity | Legacy authoritative only | EDGE-X17 three-edge corner patch audit/lab |
| CH-08 | concave/additive | plane-plane | edge chain | constant chamfer distance | chain termination; concave corner policy | plane sequence plus transition handling | `production-legacy-supported` only for known bounded pair/interaction fixtures; generic status `unknown/no-test` | `deferred`; `blocked-by-corner-policy` | EDGE-X1 two-edge concave interaction inventory | generic chain policy, real-body mutation, parity | Legacy contextual support only | Follow CH-06 chain policy after single-edge concave parity |
| CH-09 | mixed | plane-cylinder | single edge | constant chamfer distance | no corner or endpoint transition | plane, cone, or ruled surface depending on definition | `unknown/no-test` | `deferred`; `blocked-by-surface-family` | EDGE-A0 and EDGE-X1 identify non-planar as future scope | analytic classification, surface generation, STEP/recognition parity | Not ready | Future analytic surface-family audit |
| CH-10 | mixed | cylinder-cylinder | single edge | constant chamfer distance | no corner or endpoint transition | cone/ruled/deferred analytic surface | `unknown/no-test` | `deferred`; `blocked-by-surface-family` | EDGE-A0 non-planar gap | admitted output surface rule, topology, exporter smoke | Not ready | Future cylinder-cylinder edge-sweep lab |
| CH-11 | mixed | plane-plane initially; extensible later | single edge or chain | unequal distance chamfer | endpoint transitions if chain | plane or ruled/tapered surface | `unknown/no-test` | `deferred`; `blocked-by-surface-family`; `blocked-by-corner-policy` where chained | EDGE-V3 limitations list variable/unequal cases as deferred | profile-rule policy, geometry artifact, recognizer parity | Not ready | Unequal-distance policy lab after constant-distance route |
| CH-12 | convex/replacement | triangle-prism / non-orthogonal planar legacy case | corner/single-corner legacy selector | bounded chamfer distance | legacy-sensitive corner policy | plane | `production-legacy-supported` and load-bearing | `deferred`; `blocked-by-legacy-topology-contract`; `blocked-by-feature-recognition-parity` | V2-V5, V2-X8.1, V2-X8.2, EDGE-A0, EDGE-X1 | replacement topology contract, recognizer parity, no triangle migration retry | Legacy remains authoritative | Do not migrate until row-specific parity contract is proven |
| CH-13 | concave or convex depending on profile | slot/capsule/prismatic profile | edge set from profile/extrude result | constant chamfer distance if present | chain termination likely | plane | `unknown/no-test` unless covered by existing bounded edge-finish fixtures | `deferred`; likely `blocked-by-body-mutation` and `blocked-by-corner-policy` | V2 slot/capsule/profile docs are adjacent context, not chamfer support proof | explicit fixture, selection mapping, topology/parity evidence | Not ready | Add inventory row when a motivating fixture exists |
| CH-14 | recognition-only | import/reconstruction chamfer recognition | reconstructed feature edge(s) | detected chamfer rule | reconstructed corner/chain semantics | recognized plane chamfer face | legacy-sensitive; `unknown/no-test` outside known recovery tests | `blocked-by-feature-recognition-parity`; partial shadow parity for controlled AirChamfer | EDGE-X9 parity probe; EDGE-V3 shadow route | importer/reconstruction mapping, corpus rows, divergence diagnostics | Not production support by itself | Recognition parity hardening milestone when import route is targeted |
| CH-15 | convex/replacement | plane-plane rectangular prism top cap | face-boundary outer loop (Class B) | uniform symmetric chamfer distance | four ordered loop corners, all-or-nothing whole-loop policy | four planar chamfer transition faces | no production route change; legacy remains authoritative where applicable | `lab-supported`; `prismatic-controlled-route-supported` for lab-only history-known top-face loop; no AirEdgeSweep/BrepBoundedChamfer/graft/Boolean/merge | EDGE-LOOP-X1; `PrismaticTopFaceLoopChamferPrototype`; `TopFaceLoopChamferPrismaticLab`; FrictionLab loop chamfer tests | artifact/corpus promotion, no-history/imported rejection hardening, inner/open/side-loop policy, production admission | Lab-only proof; not production-authoritative | EDGE-LOOP-X2 corpus, then EDGE-LOOP-X3 no-history rejection diagnostics |

## 6. Fillet compatibility matrix

| Case ID | Convex/concave | Adjacent face family | Selection scope | Radius/rule | Corner policy | Expected output surface | Current legacy status | Current AirEdgeSweep/AirFillet status | Evidence docs/tests | Missing evidence | Production readiness | Next milestone |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| FI-01 | concave/additive | plane-plane | single edge | constant fillet radius | no corner | cylinder | `production-legacy-supported` where bounded internal manufacturing fillet route applies | `deferred`; no AirFillet implementation | EDGE-X1 fillet inventory; Firmament/Core fillet tests | AirFillet policy, cylindrical geometry artifact, closed witness, shadow route | Legacy production only | EDGE-FILLET-A0 then EDGE-X14 |
| FI-02 | convex/replacement | plane-plane | single edge | constant fillet radius | no corner | cylinder | `unknown/no-test` unless a bounded legacy fixture exists | `deferred`; no AirFillet implementation | EDGE-A0 identifies AirFillet as future scope | policy, topology plan, cylinder artifact, body mutation, parity | Not ready | Plane-plane constant-radius AirFillet architecture target |
| FI-03 | mixed | plane-plane | edge chain | constant fillet radius | chain termination | cylinders plus transition surfaces | legacy bounded internal chained behavior exists only in specific manufacturing contexts; generic `unknown/no-test` | `deferred`; `blocked-by-corner-policy`; no AirFillet | EDGE-X1 notes chained same-radius/termination legacy candidates | chain policy, transition artifacts, parity | Legacy contextual support only | AirFillet chain policy after single-edge proof |
| FI-04 | convex/replacement | plane-plane | three-edge corner | equal constant radius | three-edge convex corner | sphere patch plus cylinders or admitted corner blend | `unknown/no-test` | `deferred`; `blocked-by-corner-policy`; `blocked-by-surface-family` | EDGE-A0/EDGE-X1 identify corner gap | spherical corner patch policy, topology, exporter/recognizer parity | Not ready | Fillet corner audit after AirChamfer corner lessons |
| FI-05 | mixed | plane-plane | corner | unequal or mixed radius | mixed-radius corner | sphere/torus/ruled/deferred blend | `unsupported` or `unknown/no-test` | `deferred`; `blocked-by-corner-policy`; `blocked-by-surface-family` | No current AirFillet evidence | mixed-radius policy and analytic surface admission | Not ready | Defer until equal-radius corner support exists |
| FI-06 | mixed | plane-cylinder | single edge | constant fillet radius | no corner or endpoint transition | cylinder, torus, or other analytic blend | `unknown/no-test` | `deferred`; `blocked-by-surface-family` | EDGE-A0 surface-family gap | analytic classification, geometry artifact, STEP/recognition parity | Not ready | Future plane-cylinder analytic fillet lab |
| FI-07 | mixed | cylinder-cylinder | single edge | constant fillet radius | no corner or endpoint transition | torus/rolling analytic blend or deferred | `unknown/no-test` | `deferred`; `blocked-by-surface-family` | EDGE-A0 surface-family gap | toroidal/surface-family policy, topology, exporter/recognizer parity | Not ready | Future cylinder-cylinder analytic fillet lab |
| FI-08 | mixed | simple edge initially plane-plane | single edge | variable radius fillet | endpoint transitions even for single edge | variable cylindrical/conical/ruled/deferred | `unsupported` or `unknown/no-test` | `deferred`; `blocked-by-surface-family` | No AirFillet evidence; variable rules out of current scope | variable-radius profile model, surface generation, recognition parity | Not ready | Explicitly defer beyond bounded constant-radius scope |
| FI-09 | concave/additive | internal manufacturing fillet, commonly plane-plane | single edge or bounded interaction | constant radius | no corner or bounded chain termination | cylinder | `production-legacy-supported` where tests exist | `deferred`; no AirFillet replacement | EDGE-X1 inventory; Firmament manufacturing fillet tests | AirFillet parity and fallback policy | Legacy production only | Preserve legacy authority until AirFillet proves parity |
| FI-10 | mixed | plane-plane first; broader later | edge chain termination | constant or mixed radius | chain termination | cylinders plus transition/deferred surface | contextual legacy support only; generic `unknown/no-test` | `deferred`; `blocked-by-corner-policy` | EDGE-X1 termination candidate inventory | explicit termination policy and diagnostics | Not ready | AirFillet termination policy lab |
| FI-11 | recognition-only | import/reconstruction fillet recognition | reconstructed edge(s) | detected radius/rule | reconstructed chain/corner semantics | recognized cylinder/sphere/torus | `unknown/no-test` outside existing semantic recovery lanes | `deferred`; `blocked-by-feature-recognition-parity`; no AirFillet | semantic recovery docs are adjacent but not AirFillet proof | importer mapping, recognizer parity, corpus rows | Not production support by itself | Add recognition audit once AirFillet artifacts exist |

## 7. Production-readiness gates

A matrix row can be upgraded to production support only after the relevant gates below are satisfied or explicitly ruled non-applicable with evidence:

1. Judgment/admissibility policy with deterministic rejection reasons.
2. Topology plan showing intended faces, edges, trims, identity, and adjacency.
3. Geometry artifact proving the intended analytic surface family.
4. Closed witness proving a valid bounded body/shell in lab scope.
5. Real-body controlled prototype proving the path can mutate or construct an actual body in a constrained case.
6. Feature-recognition parity proving recovered feature semantics, adjacency, and topology-sensitive contracts, not merely similar geometry.
7. Shadow route or other non-authoritative evaluation preserving legacy authority and production output.
8. CLI/artifact generation where useful for inspection and repeatable developer evidence.
9. Regression corpus/stability where useful for repeatability and future diff detection.
10. Production route integration with fallback/legacy-authority policy.
11. STEP/export smoke for user-visible or exported bodies.
12. CLI/example fixture coverage if the capability is user-facing.

V2-A3 remains the governing doctrine for migration: geometry parity is not feature parity, and STEP parity is not recognizer parity. A row that exports plausible STEP geometry still cannot replace a legacy route until the topology and recognition contracts are proven for the motivating case.

## 8. Full-support definition

### Full chamfer support

Aetheris can claim full chamfer support within its bounded analytic scope when it supports:

- all bounded plane-plane convex and concave single-edge cases;
- edge chains with deterministic ordering and termination behavior;
- simple convex and concave corner policies;
- admitted non-planar analytic adjacent face families, such as plane-cylinder and cylinder-cylinder, once their output surface families are explicitly classified;
- deterministic policy/rejection for unsupported or ambiguous cases;
- production integration with fallback and legacy-authority policy;
- STEP/export smoke and feature-recognition parity for supported production rows.

### Full fillet support

Aetheris can claim full fillet support within its bounded analytic scope when it supports:

- constant-radius plane-plane convex and concave single-edge cases;
- chain and corner policies;
- analytic classifications for cylindrical fillets, spherical corner patches, toroidal/other admitted analytic blends, and any ruled/conical cases Aetheris chooses to admit;
- deterministic rejection for unsupported, mixed, or ambiguous cases;
- production integration with fallback and legacy-authority policy;
- STEP/export smoke and feature-recognition parity for supported production rows.

Aetheris does not need NURBS or freeform surface support to claim full support within this bounded analytic scope. It must, however, clearly reject or defer unsupported surface families and must make those rejections visible through policy diagnostics and documentation.

## 9. Known blockers

Known blockers before broader chamfer/fillet support include:

- production body mutation/grafting beyond controlled synthetic cases;
- edge-chain policy;
- corner patch policy;
- non-planar adjacent face families;
- AirFillet geometry;
- variable radius;
- feature-recognition parity for legacy-sensitive cases;
- arbitrary body edge selection;
- importer/reconstruction mapping;
- STEP export for future non-planar, ruled, toroidal, spherical, or other edge-sweep surfaces if needed.

## 10. Recommended roadmap

Recommended post-EDGE-X13 roadmap:

1. **EDGE-X13**: completed as a non-authoritative Firmament-facing test-only shadow diagnostics probe for CH-03, capturing production-adjacent diagnostics without changing route authority.
2. **EDGE-A2**: constructive chamfer reframing audit, reclassifying history-known chamfers into profile/profile-stack/corner-manifest candidates where evidence supports it.
3. **EDGE-PROFILE-X1**: vertical-edge chamfer as profile extrusion lab.
4. **EDGE-PROFILE-X2**: horizontal/top-edge chamfer as profile-stack or section-transition lab.
5. **EDGE-PRISMATIC-A0**: completed as the prismatic section-transition contract audit for top/bottom/horizontal history-known chamfer roadmap classification.
6. **EDGE-PRISMATIC-X1**: completed as a lab-only first-scope `PrismaticSectionTransitionEmitter` proof for two/three Z-stacked line-only outer sections with explicit identity correspondence, closed planar BRep emission, split transition intervals, STEP smoke, and deterministic invalid/deferred diagnostics. This upgrades the evidence base for prismatic section transitions but does not change production chamfer/fillet behavior, current `ProfileStackExtrudeExecutor` behavior, AirEdgeSweep, `BrepBoundedChamfer`, STEP exporter/importer code, Boolean core code, triangle migration, sketch solving, clipping, or NURBS/freeform support.
7. **EDGE-PRISMATIC-X2**: completed as a lab-only top `+X` horizontal edge chamfer proof through the X1 prismatic emitter, preserving the EDGE-PROFILE-X2 topology and STEP witness while replacing the one-off Route B proof path.
8. **EDGE-PRISMATIC-X3/V1/V2**: completed as generic line-only emitter evidence, internal emitter packaging, and a controlled top `+X` horizontal chamfer route through the packaged emitter.
9. **EDGE-PRISMATIC-X4/X5**: completed as the coplanar split/merge policy audit and deterministic split-preserving STEP/JSON artifact corpus. X5 records rectangle inset, top-edge chamfer, pentagon, hexagon, asymmetric pentagon, and JSON-only invalid/deferred diagnostics without production routing.
10. **EDGE-CORNER-X1**: manifest-driven single-corner chamfer reconstruction lab.
11. **EDGE-FILLET-A0**: AirFillet architecture audit focused on bounded constant-radius analytic scope.
12. **EDGE-X14**: plane-plane constant-radius AirFillet cylindrical closed witness lab.
13. **EDGE-X15+ / EDGE-V paths**: continue controlled no-history/local-edge AirChamfer hardening only where shadow diagnostics and production-readiness gates remain stable.

The roadmap should be adjusted by evidence. If a row reveals recognizer divergence, legacy topology mismatch, or body-mutation instability, the next milestone should narrow to that blocker rather than broadening support claims.

## 11. Compatibility matrix maintenance policy

Every future EDGE milestone should update this matrix when it changes support status, adds evidence, narrows a blocker, or introduces a new deferred row.

Every production migration must reference the relevant row IDs and identify which production-readiness gates have passed. A row status cannot be upgraded to `production-air-supported` without passing the applicable production-readiness gates and documenting fallback/legacy-authority policy.

Rejected or deferred behavior counts as support only when the rejection is deterministic, documented, and covered by an explicit policy/test or diagnostic fixture. Silent failure, accidental omission, or unknown behavior is not support.

## 12. Non-goals

This milestone does not include:

- implementation;
- production routing;
- NURBS/freeform support;
- STEP exporter/importer changes;
- Boolean core changes;
- public API changes;
- route replacement;
- new geometry implementation;
- test weakening;
- triangle migration retry;
- sketch solver or clipping engine work.



## EDGE-A3 selection taxonomy update

EDGE-A3 reframes the selection-scope rows in this matrix as practical support tiers rather than a single march toward arbitrary edge-graph mutation:

- **Class A / Tier 1 — single edge, uniform rule:** the immediate baseline for plane-plane or simple profile/prismatic contexts.
- **Class B / Tier 2 — face-boundary loop or ordered chain around one face, uniform rule:** the next bounded target because loop order and corner sequence are known.
- **Class C / Tier 3 — whole-body canonical edge set, uniform rule:** a body-family operation over simple boxes/prisms, not `N` unrelated edge selections.
- **Class D / Tier 4 — arbitrary edge graph, mixed rules, unequal-distance chamfer, or variable-radius fillet:** deferred/advanced unless reducible to Classes A/B/C or admitted by an explicit bounded policy.
- **Tier 5 — non-analytic/freeform/surfacing-heavy edge finishes:** deferred outside current bounded analytic scope.

Consequences for existing rows: CH-03 should be read as a single-edge case whose route authority depends on whether history is known; top/horizontal history-known chamfer belongs to the prismatic section-transition route; chain rows should distinguish face-boundary loops from arbitrary edge graphs; and whole-body box/prism uniform chamfer/fillet support should be called out as canonical body-family support rather than broad graph support. Baseline Aetheris work should prioritize uniform symmetric chamfer and constant-radius fillet rules.


## EDGE-PRISMATIC-V1 status note

The prismatic section-transition row is now **internal production-adjacent / not routed** for Z-axis two/three-section, line-only, one-outer-loop, no-hole, equal-count identity-correspondence transitions. Rectangle, pentagon, hexagon, asymmetric pentagon, and stable+transition rectangle cases validate the split-preserving topology formula and STEP smoke through the existing exporter. EDGE-PRISMATIC-X4 records split preservation as the current prismatic contract: coplanar section-boundary faces are semantic output by default, not merge candidates inside the emitter. EDGE-PRISMATIC-X5 adds artifact-corpus evidence for the same split-preserving contract by writing deterministic STEP files and JSON summaries for successful rectangle, polygon, asymmetric polygon, and top-edge chamfer cases plus JSON-only invalid/deferred diagnostics. No production chamfer/fillet behavior, ProfileStack behavior, STEP exporter/importer behavior, Boolean core behavior, AirEdgeSweep behavior, public API, triangle migration, sketch solver, clipping engine, or NURBS/freeform support changed.


## EDGE-PRISMATIC-V2 status note

The top/horizontal history-known chamfer row now has an **internal controlled route / not production-authoritative** witness for the rectangular-prism top `+X` side case. `PrismaticTopEdgeChamferPrototype` accepts controlled dimensions and chamfer distance, validates the supported selection, builds the canonical three-section stack, invokes `PrismaticSectionTransitionEmitter`, produces a closed all-planar BRep with the expected 12 vertices, 20 edges, 10 faces, 10 loops, and 40 coedges, passes STEP smoke through the existing exporter, and is now included in the EDGE-PRISMATIC-X5 corpus as `edge-prismatic-x5-top-edge-chamfer.step` with split-preserving topology asserted. No production chamfer/fillet behavior, production route replacement, current `ProfileStackExtrudeExecutor` behavior, STEP exporter/importer behavior, Boolean core behavior, AirEdgeSweep behavior, public API, triangle migration, sketch solver, clipping engine, or NURBS/freeform support changed.

## EDGE-PRISMATIC-X6 stability/analyzer evidence note

The prismatic section-transition evidence now includes an explicitly gated/manual corpus stability check: `AETHERIS_RUN_ARTIFACT_CORPUS_TESTS=1 dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "PrismaticCorpusStability"`. The gate compares repeated EDGE-PRISMATIC-X5 JSON summaries, topology summaries, STEP marker summaries, raw STEP SHA256 hashes, normalized STEP summaries, and invalid/deferred diagnostics for rectangle inset, top-edge chamfer, scaled pentagon, scaled hexagon, asymmetric pentagon, and JSON-only invalid/deferred cases. It also confirms deterministic `analyze section` geometry summaries for selected successful artifacts and records the current `analyze map` primitive-raycast limitation as analyzer integration evidence to address later. This improves repeatability evidence for the split-preserving prismatic lane but does not upgrade any chamfer/fillet row to production support and does not change production chamfer/fillet behavior, ProfileStack behavior, STEP exporter/importer behavior, Boolean core behavior, AirEdgeSweep behavior, coplanar merge policy, triangle migration, sketch solver, clipping engine, or NURBS/freeform support.

## EDGE-PRISMATIC-X7 map/CIR audit note

EDGE-PRISMATIC-X7 documents that `analyze map` remains a blocker/evidence item for prismatic rows because the current map backend depends on `BrepSpatialQueries.Raycast`, whose v1 acceptance is limited to primitive BReps. The recommended path is not to upgrade matrix support claims by broadening production geometry, but to prove hybrid map dispatch separately: admitted CIR/FRep/tape mirrors for generated AIR bodies, existing BRep raycast for supported explicit-topology bodies, and deterministic unsupported diagnostics for STEP/imported prismatic bodies without an admitted mirror. This note does not change row status, production readiness, analyzer behavior, STEP/export/import behavior, Boolean behavior, topology, prismatic emitter behavior, AirEdgeSweep behavior, or gated-test defaults.

## EDGE-LOOP-A0 loop-support status note

EDGE-LOOP-A0 adds a documentation-only Class B audit for face-boundary loop edge-finish selections. Loop support is now scoped as ordered, face-owned closed-loop intent rather than arbitrary graph mutation. The preferred first target is a uniform symmetric chamfer around the top face outer loop of a history-known rectangular prism via prismatic section transition, using the existing top-edge prismatic witness as a building block and preserving section splits by default. Uniform constant-radius loop fillets, inner-loop hole/slot rims, no-history/imported planar loops, non-planar loops, open chains, mixed rules, and arbitrary graphs remain unimplemented/deferred pending explicit route, corner, artifact, and analyzer evidence. No production chamfer/fillet behavior, public API, BRep topology, STEP exporter/importer, Boolean core, AIR emitter, CIR analyzer, triangle migration, or NURBS/freeform behavior changed.

## CH-15 EDGE-LOOP-X2 corpus evidence

CH-15 now has EDGE-LOOP-X2 artifact-corpus evidence. The explicit experimental route `aetheris experimental loop-chamfer-corpus --out-dir <dir> [--json]` writes the canonical, larger valid, and non-square top-face outer-loop chamfer STEP artifacts and records rejected/deferred JSON-only rows for out-of-scope selections and invalid rules. The evidence remains lab-only and preserves no production route replacement, no AirEdgeSweep, no BrepBoundedChamfer, no topology graft, no 3D Boolean, and no coplanar merge.
