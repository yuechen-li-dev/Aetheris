# EDGE-A1 — Chamfer/fillet support compatibility matrix

## 1. Executive summary

This document defines what “full chamfer/fillet support” means for Aetheris within its bounded analytic modeling scope. It is a compatibility and readiness matrix, not an implementation plan that changes behavior.

The current AirChamfer golden path is strong but narrow: controlled convex planar single-edge chamfers now have Judgment-backed policy, topology planning, geometry artifacts, closed witnesses, controlled topology graft evidence, a production-adjacent prototype, feature-recognition parity probes, a non-authoritative shadow route, CLI STEP artifacts/corpus coverage, gated corpus stability evidence, and one internal/test-only gated Firmament opt-in route for the controlled CH-03 fixture.

Legacy bounded chamfer/fillet behavior remains authoritative wherever production behavior exists. In particular, `BrepBoundedChamfer` and `BrepBoundedFillet` are still the production routes for the currently supported bounded cases, and AirEdgeSweep/AirChamfer evidence must not be read as route replacement.

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
| CH-03 | convex/replacement | plane-plane, orthogonal controlled body | single edge | constant chamfer distance | no corner | plane | `production-legacy-supported` for legacy bounded box/simple contexts | `production-adjacent-air-prototype`; `shadow-supported`; `artifact-corpus-supported`; `closed-witness-supported`; `gated-opt-in-route-supported` | EDGE-X3 through EDGE-X13, EDGE-V1/V2/V3/V4, CLI tests, FrictionLab AirChamfer tests, Firmament shadow-diagnostics and opt-in route tests | default production authority, arbitrary body mutation beyond controlled fixture | Best current AirChamfer row; EDGE-V4 adds gated internal/test-only controlled route evidence, still not default production-authoritative | EDGE-V5 one additional controlled opt-in fixture or EDGE-V4.1 fallback injection hardening |
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
2. **EDGE-A2 / EDGE-FILLET-A0**: AirFillet architecture audit focused on bounded constant-radius analytic scope.
3. **EDGE-X14**: plane-plane constant-radius AirFillet cylindrical closed witness lab.
4. **EDGE-X15**: convex AirChamfer controlled body mutation hardening.
5. **EDGE-X16**: AirChamfer edge-chain policy lab.
6. **EDGE-X17**: AirChamfer three-edge corner patch audit/lab.
7. **EDGE-V4**: controlled opt-in AirChamfer route only if shadow diagnostics remain stable and the relevant production-readiness gates are satisfied for the selected row.

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
