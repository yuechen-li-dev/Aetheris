# EDGE-FILLET-A0 — Fillet architecture audit using selection taxonomy

## 1. Executive summary

Fillet support should follow the EDGE-A3 selection taxonomy instead of beginning from a universal arbitrary edge-graph operation. The practical baseline is to classify the requested selection, admit a uniform constant-radius rule, and lower through the most construction-aware route available.

Baseline fillet support should prioritize constant-radius, uniform-rule cases:

- **Class A**: one selected edge;
- **Class B**: one face-boundary loop;
- **Class C**: a whole-body canonical edge set for a bounded simple body family.

Aetheris should not start fillet work from arbitrary edge graphs, mixed-radius selections, or variable-radius fillets. Those cases require graph-wide termination policy, corner patches, mixed selected/unselected incident-edge semantics, radius interpolation, and no-partial-success behavior that are not justified as first-scope work.

Existing legacy bounded fillet behavior remains authoritative wherever production behavior exists. In particular, `BrepBoundedFillet` owns the current bounded manufacturing fillet path for recognized orthogonal roots and emits cylindrical fillet faces for admitted constant-radius internal concave planar-planar scopes. This audit does not replace that route.

This milestone defines practical fillet lanes and the next proof milestones. It is documentation/design only: no implementation, production route replacement, STEP/Boolean/CIR/AIR changes, or new fillet geometry are introduced.

## 2. Problem framing

Fillets are harder than chamfers because a fillet is not just a planar bevel replacement. Even for bounded analytic CAD, a fillet route must reason about:

- **curved transition surfaces** rather than only planes or ruled bevels;
- **cylindrical or rolling-ball-like patches** for plane-plane constant-radius cases;
- **corner patches** where two or three filleted edges meet;
- **tangent continuity** to adjacent faces, including deterministic orientation and trim boundaries;
- **radius admissibility** against local feature sizes, adjacent-face extents, and self-intersection risk;
- **endpoint and chain behavior** where a fillet terminates against selected or unselected edges;
- **STEP analytic surface classification** so emitted faces remain recognizable as cylinders, spheres, tori, or explicitly unsupported families rather than anonymous freeform output.

The EDGE-A3 selection taxonomy collapses the problem into bounded decision classes:

- **Class A — single edge**: isolate one local edge, adjacent face pair, radius rule, and endpoint policy.
- **Class B — face-boundary loop**: use an owning face and ordered coedge loop to define corner sequence and whole-loop success/rejection.
- **Class C — whole-body canonical edge set**: treat simple body roundovers as body-family manifests rather than many unrelated edge picks.
- **Class D — arbitrary edge graph**: defer until graph-level corner, termination, mixed-rule, and no-partial-success semantics are explicitly designed.

This reframing lets Aetheris prove the smallest useful fillet families first and avoid confusing bounded manufacturing support with general rolling-ball filleting.

## 3. Rule taxonomy for fillets

### Constant-radius fillet

A constant-radius fillet uses one finite positive radius for the selected edge or admitted selection. This is the first-class baseline support rule for Aetheris fillets.

### Uniform loop/body fillet

A uniform loop/body fillet uses the same radius across every selected edge in a Class B face-boundary loop or Class C canonical body set. It should become first-class for Class B/C only after Class A single-edge evidence proves surface emission, radius policy, and endpoint behavior.

### Variable-radius fillet

A variable-radius fillet changes radius along an edge or chain. It requires interpolation policy, endpoint constraints, corner compatibility, and surface families beyond the baseline cylindrical witness. It is deferred/advanced.

### Mixed-radius selection

A mixed-radius selection applies different radii to different edges in one selection. It requires graph-level interaction policy and corner resolution between unlike radii. It is deferred/advanced.

### Mixed chamfer/fillet selection

A mixed chamfer/fillet selection combines finish types in one selected set. It is deferred unless a bounded body-family manifest explicitly admits the combination, orders the construction, defines corner interactions, and provides artifact evidence.

Aetheris baseline fillet support should be **constant-radius only**.

## 4. Selection classes for fillets

### Class A — Single-edge fillet

Examples:

- one plane-plane outside edge;
- one internal pocket edge;
- one imported or no-history local edge.

Routes:

- history-known profile/prismatic route where the source construction makes the rounded result cheaper to author than to patch;
- future **AirFillet** for no-history/local edges after an explicit cylindrical witness and topology policy exist;
- legacy bounded route where production behavior already exists.

Class A is the correct first proof scope because it isolates the adjacent face pair and radius policy before loop and body-wide corner interactions are attempted.

### Class B — Face-boundary loop fillet

Examples:

- top rim fillet;
- pocket rim fillet;
- hole rim fillet;
- slot rim fillet.

Routes:

- profile-authored rounded loop if construction history is known;
- prismatic or section-stack curved transition if the selected loop is a top/bottom loop of a prism;
- future AirFillet loop route if no construction history exists and a loop-level no-history policy is admitted.

Class B must remain face-owned and ordered by coedges. It is not an arbitrary list of edges that happens to close.

### Class C — Whole-body canonical fillet

Examples:

- fillet all outside edges of a box;
- round over a simple prism;
- bounded manufacturing roundover for a simple body family.

Routes:

- legacy bounded route where existing behavior owns support;
- future constructive manifest for simple body families;
- profile/prismatic synthesis where the finished body can be emitted directly from construction history.

Class C should be represented by a body-family policy or constructive manifest, not as N independent Class A operations.

### Class D — Arbitrary edge graph

Class D is deferred. It requires graph-level corner and termination policy, selected/unselected incident-edge semantics, mixed local face families, and no-partial-success behavior. It should not be used as the baseline definition of Aetheris fillet support.

## 5. Surface family taxonomy

Likely analytic output families are:

- **Plane faces retained/truncated**: original planar faces survive with new trim boundaries.
- **Cylindrical fillet faces**: plane-plane constant-radius single-edge cases should produce cylindrical transition surfaces.
- **Spherical or sphere-like corner patches**: uniform-radius three-plane box corners may require spherical patches or an explicitly equivalent bounded constructive representation.
- **Toroidal patches**: plane/curved or curved/curved adjacency may create toroidal transition families; these are likely deferred until specific body-family policies exist.
- **Ruled/transition surfaces**: construction history may admit profile/section representations whose emitted surfaces are derived from line/arc profiles or section transitions.
- **Unsupported/freeform/NURBS**: deferred and outside baseline scope.

First fillet support should target **plane-plane constant-radius cases with cylindrical transition surfaces**. Sphere-like and toroidal corner/adjacency families should remain roadmap topics until the cylindrical witness and corner policy are proven.

## 6. Lowering strategy matrix

| Selection class | Rule | Construction history known? | Adjacent face family | Preferred route | Fallback route | Current evidence | Missing evidence | Recommended next lab |
|---|---|---:|---|---|---|---|---|---|
| Class A single plane-plane edge | Constant radius | No | Plane-plane | Future AirFillet single-edge cylindrical witness | Legacy bounded route only where current production scope already admits it | Existing bounded internal concave planar-planar route emits cylinders for recognized manufacturing cases; AirChamfer/AirEdgeSweep labs show local-edge policy patterns but not fillet geometry | No no-history AirFillet witness, no general outside-edge replacement policy, no endpoint/cap proof | **EDGE-FILLET-X1 — Single plane-plane constant-radius fillet cylindrical witness** |
| Class A single vertical extrusion edge | Constant radius | Yes | Plane-plane along vertical extrusion side faces | Profile-authored rounded profile/extrude candidate using line/arc profile geometry | Legacy bounded route only for existing bounded manufacturing cases | Profile-authored chamfer evidence proves constructive vertical-edge route shape for planar bevels; line/arc profile extrusion infrastructure exists | Rounded profile side-face topology and STEP cylinder/ruled classification evidence | EDGE-FILLET-PROFILE-X1 if profile route becomes lower-risk after X1 scoping |
| Class A single top/horizontal extrusion edge | Constant radius | Yes | Plane-plane top/side prism edge | Future prismatic curved section-transition candidate | Defer or legacy only where supported | Prismatic top-edge chamfer witness proves section-transition route for planar chamfer | Curved transition section model, cylindrical surface emission, endpoint policy | EDGE-FILLET-PRISMATIC-X1 after Class A cylinder evidence |
| Class B top face outer loop | Constant uniform radius | Yes | Planar top with planar side faces | Future profile/prismatic loop fillet | Defer until Class A and corner evidence | EDGE-LOOP-A0/X1/X2 prove top-loop chamfer route/corpus, not fillet | Loop corner patches, curved transition sections, whole-loop radius clearance | EDGE-LOOP-FILLET-X1 after EDGE-FILLET-X1 and corner audit |
| Class B hole/slot rim | Constant uniform radius | Usually yes for constructive profiles | Plane-plane or plane-cylinder depending on hole/slot | Future profile/line-arc loop fillet candidate | Defer | Line/arc profile and profile-stack lanes exist; current chamfer loop evidence is outer-loop only | Inner-loop semantics, cylinder adjacency for holes, loop corner policy, STEP/analyzer evidence | Later EDGE-FILLET-PROFILE-LOOP-X1 |
| Class C whole-body box | Uniform radius | Yes for primitive/profile-known boxes | Three-plane outside box edge/corner family | Legacy bounded route where existing behavior owns support; future constructive manifest | Defer outside legacy scope | Existing bounded fillet supports selected internal concave manufacturing edges/chains, not broad outside-box roundover; primitive STEP supports analytic cylinders/spheres/tori generally | Three-edge corner patch manifest, all-edge classing, no-partial-success policy | EDGE-CORNER-X1 constructive manifest audit |
| Plane-cylinder fillet | Constant radius | Varies | Plane-cylinder | Defer | None | Toroidal/cylindrical adjacency policy, trim math, STEP classification | Deferred |
| Cylinder-cylinder fillet | Constant radius | Varies | Cylinder-cylinder | Defer | None | Toroidal/freeform risk, self-intersection, face identity policy | Deferred |
| Variable-radius fillet | Variable radius | Varies | Any | Defer | None | Radius interpolation, non-cylindrical surfaces, corner compatibility | Deferred |
| Arbitrary graph/mixed-radius | Mixed/variable | Varies | Mixed | Defer | None | Graph-level termination/corner policy and no-partial-success semantics | Deferred |

## 7. Existing legacy fillet assessment

The current production fillet support is a bounded manufacturing lane, not a universal fillet kernel:

- Firmament routes admitted fillet booleans through `ExecuteBoundedManufacturingFilletOnRecognizedOrthogonalRoot`, resolves explicit bounded edge tokens, preflights recognized orthogonal safe-boolean compositions, and dispatches to `BrepBoundedFillet.FilletTrustedPolyhedralSingleInternalConcaveEdge`.
- `BrepBoundedFillet` is documented in code as an F0/F1 bounded constant-radius cylindrical builder for explicit internal concave planar-planar vertical edges. It uses `JudgmentEngine` candidates for single-edge cylindrical fillets, chained same-radius cylindrical fillets, and chained same-radius cylindrical termination contexts.
- The preflight accepts one or two explicit internal concave vertical edge tokens, requires distinct local concave corners for pairs, requires adjacent pair interaction for two-edge chains, and rejects radii that are not strictly smaller than the local bounded neighborhood extent.
- Core tests verify a canonical single internal concave edge emits a cylindrical face; non-planar source contexts reject through the judgment path; adjacent same-radius pairs emit multiple cylindrical faces; same-radius follow-on/termination behavior is admitted; mismatched-radius termination is rejected.
- CLI tests build bounded fillet Firmament fixtures, export STEP, analyze the result as an enclosed manifold, and confirm cylindrical surface families/radius markers for single-edge and chained fixtures.
- STEP/analyzer evidence already exists for primitive cylindrical, spherical, and toroidal surface classification, but that should be read as analytic surface/export capability, not proof of generalized fillet construction.

Production behavior should therefore remain legacy-authoritative where this bounded route is currently admitted. It should not be silently replaced by AirFillet, profile, prismatic, or manifest routes until parity is proven for the exact affected scope. Current evidence supports constant-radius internal concave planar-planar cylindrical manufacturing fillets and same-radius bounded chained cases; it does not establish arbitrary outside-edge roundovers, whole-body box fillets, plane-cylinder/cylinder-cylinder fillets, variable-radius fillets, or arbitrary graph support.

## 8. First-scope recommended target

Preferred first target:

`EDGE-FILLET-X1 — Single plane-plane constant-radius fillet cylindrical witness`

Candidate scope:

- controlled rectangular prism or two-plane local fixture;
- exactly one selected edge;
- radius = `1`;
- produce one cylindrical fillet face between two planar faces;
- no general rolling-ball algorithm;
- no arbitrary edge graph;
- no variable radius;
- no production route replacement.

This is preferred because it proves the smallest missing fillet primitive: a constant-radius plane-plane cylindrical transition outside the current legacy manufacturing niche. It directly exercises the hardest first-order fillet requirements—tangent cylinder placement, adjacent planar face truncation, endpoint/cap policy, STEP cylinder emission, and analyzable topology—without requiring loop corners or whole-body manifests.

Alternative target:

`EDGE-FILLET-PROFILE-X1 — Profile-authored rounded vertical edge via line/arc profile extrusion`

This may be lower-risk if implementation evidence shows that line/arc profile extrusion can emit the rounded vertical-edge result with fewer topology-graft hazards. However, it is more construction-history-specific. It should be the backup or follow-on route, not the primary architecture proof, because Aetheris still needs a no-history/local Class A cylindrical witness to bound AirFillet semantics.

## 9. Corner policy for fillets

Corner categories:

- **No-corner/single-edge endpoint**: one selected edge terminates at unselected topology; first-scope work should avoid full corner patches where possible.
- **Face-loop ordered corner**: consecutive edges in a Class B loop meet with known order and a loop-level radius policy.
- **Three-edge box corner**: whole-body or multi-edge Class C selections meet at a three-plane corner and may require spherical or sphere-like patches.
- **Mixed radius corner**: incident selected edges have different radii; deferred.
- **Variable-radius corner**: one or more incident edges vary radius; deferred.
- **Non-planar/cylindrical adjacency corner**: cylindrical, toroidal, or curved adjacent faces participate; deferred.

First-scope EDGE-FILLET-X1 should use no corner patch if possible, or a tightly controlled endpoint/cap policy only. Whole-body/corner fillets should be deferred to a constructive manifest audit such as EDGE-CORNER-X1.

## 10. Radius admissibility policy

Baseline fillet admissibility should require:

- radius is greater than `0` and finite;
- radius is strictly smaller than adjacent face extents relevant to the local trim;
- radius is strictly smaller than local feature size and clearances;
- too-large radii reject with diagnostics rather than clamp or partially apply;
- degenerate, tangent, or zero-length selected edges reject;
- ambiguous convexity/concavity rejects unless a route explicitly supports both cases;
- self-intersecting offsets or transition surfaces reject;
- a uniform radius only.

This policy matches the legacy bounded route’s posture: admissibility is explicit, failures are diagnostic, and radius bounds are part of the selection contract.

## 11. Production-readiness gates

A fillet route is not production-ready until it passes the relevant gates:

- **Selection/admissibility**: selection class, edge/loop/body identity, adjacent faces, and route eligibility are explicit.
- **Radius policy**: finite positive constant radius; local extent checks; deterministic rejection for too-large or degenerate cases.
- **Surface family emission**: claimed analytic families are emitted intentionally, especially cylinders for plane-plane constant-radius cases.
- **BRep topology/STEP**: topology is manifold where claimed; face/loop/coedge counts are stable; STEP emits analytic surfaces with expected markers.
- **Corner policy**: endpoint, loop, and body corners succeed or reject under documented policy.
- **Feature-recognition parity**: existing recognizers/analyzers either understand the emitted result or report scoped unsupported diagnostics.
- **Artifact/corpus**: deterministic STEP/JSON artifacts cover admitted and rejected cases.
- **CIR/analyzer evidence where relevant**: CIR mirrors validate only occupancy/analysis for admitted mirrors and do not claim topology parity.
- **Fallback/legacy authority**: existing production legacy support remains authoritative until explicit parity and migration gates are met.
- **No partial success**: a selected edge/loop/body operation either applies to the whole admitted selection or rejects/defer with diagnostics.

## 12. Relationship to chamfer/profile/prismatic work

Chamfer profile/prismatic success does not automatically imply fillet readiness. Chamfers can often be represented as planar bevels or line-only section transitions; fillets require curved transitions, tangent placement, radius bounds, and potentially corner patches.

Fillets can share the selection taxonomy and construction-history routing strategy:

- profile-authored fillets can use line/arc profile loops where the source construction is known;
- prismatic fillets may use curved section transitions, not the same all-planar prismatic emitter proven for chamfers;
- loop fillets should inherit the face-owned ordered-loop policy from EDGE-LOOP-A0, but must add curved surface and corner evidence;
- AirFillet should remain the no-history/local-edge lane, not a universal route for all constructive and whole-body cases.

## 13. Relationship to CIR/AIR/BRep authority

- AIR owns fillet selection and radius intent: selected edge/loop/body class, rule, and construction history are source-level semantics.
- BRep owns emitted cylindrical, spherical, toroidal, ruled, and planar topology plus STEP export.
- CIR mirror can validate occupancy only after an admitted mirror exists for the constructed family.
- CIR cannot claim face identity or topology parity for fillet routes.
- STEP import does not recover fillet intent by default; imported analytic surfaces may be recognizable, but that is not equivalent to recovering the original AirFillet/profile/prismatic/manifest request.

## 14. Recommended roadmap

1. **EDGE-FILLET-X1**: single plane-plane constant-radius cylindrical witness.
2. **EDGE-FILLET-X2**: STEP/artifact corpus for X1, including admitted cylinder output and deterministic rejected radius/selection cases.
3. **EDGE-FILLET-A1**: loop/whole-body fillet corner policy audit.
4. **EDGE-CORNER-X1**: constructive manifest for whole-body box/chamfer/fillet corners.
5. **EDGE-LOOP-FILLET-X1**: face-boundary loop constant-radius fillet only after single-edge/corner policy evidence.
6. Keep variable-radius, mixed-radius, mixed chamfer/fillet selections, curved-surface adjacency, and arbitrary graphs deferred.

If X1 implementation scoping shows the no-history/local witness is higher risk than expected, run **EDGE-FILLET-PROFILE-X1** as a construction-history-specific parallel proof, but do not let it replace the need for a Class A plane-plane cylindrical AirFillet witness.

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
- new fillet/chamfer geometry implementation;
- arbitrary graph support;
- variable-radius implementation;
- mixed-radius implementation;
- NURBS/freeform expansion;
- test weakening;
- gated artifact corpus stability requirements by default.

## AIR-A0 route-selection note

AIR-A0 places future fillet support behind AIR route selection: constant-radius Class A evidence should lower through the highest construction-aware route available, then a future `AirFillet`/`AirEdgeSweep` local route only when no-history support is explicitly admitted, with legacy bounded BRep fillet behavior preserved as the current authoritative fallback. See `docs/air-a0-aetheris-v2-compiler-ir-constitution.md`.

## AIR-X2 route-policy note

AIR-X2 keeps face-boundary-loop constant-radius fillet deferred. The route selector requires Class A single-edge fillet and corner evidence before a loop fillet route can be admitted, and no fillet geometry behavior changes in AIR-X2.
