# SEM-A0 — Firmament semantic topology naming and selector provenance audit

Date: 2026-05-05
Status: Outcome A — clear audit + recommended model
Scope: design/audit/documentation only (no selector resolver refactor, no placement behavior changes, no generated-edge implementation)

## A) Current selector reality (code-first audit)

### Selector shape and resolution

- Runtime selector shape is `feature.port` only; the resolver splits on the first `.` and fails non-shaped selectors. No selector chaining/multi-hop exists in runtime resolution.
- Selector resolution is topology-count oriented, not identity-preserving. Resolver outputs `(featureId, port, resultKind, count)` and does not return stable topology element identities.

### Implemented semantic port contracts today

Selector contracts are declared in `FirmamentSelectorContracts` and enforced via feature kind:

- `box`: `top_face`, `bottom_face`, `side_faces`, `edges`, `vertices`.
- `cylinder`: `top_face`, `bottom_face`, `side_face`, `circular_edges`, `edges`, `vertices`.
- `cone`: `top_face`, `bottom_face`, `side_face`, `circular_edges`, `edges`, `vertices`.
- `sphere`: `surface` only.
- `torus`: `surface`, `edges`, `vertices`.
- Boolean-like feature kinds (`add`, `subtract`, `intersect`, and currently draft/chamfer/fillet contract family): `top_face`, `bottom_face`, `side_faces`, `edges`, `vertices`.
- `triangular_prism`, `hexagonal_prism`, `straight_slot`, `rounded_corner_box`, `slot_cut`, `library_part` currently map to box-like contracts.

### Placement semantics tied to selector contracts

- Placement anchor extraction checks selector contract result kind, then computes an anchor via representative-point centroid:
  - face/face-set ports -> centroid of representative face points,
  - edge-set -> centroid of edge endpoint points,
  - vertex-set -> centroid of vertex points.
- `around_axis` is bounded to `side_face` resolving cylindrical/conical axis semantics.

### Canonical formatting behavior

- Formatter preserves selector tokens, does not rename/selectively canonicalize port names.
- Placement alias normalization is parser/lowering behavior; docs/tests indicate `centered_on` compatibility alias is normalized to `on_face` in canonical output expectations.

### PMI selector/semantic reference behavior

- PMI planar face references require selector-shaped input and store selector text directly (`PmiPlanarFaceReference.FromSelector`), with no topology-stable provenance layer.
- PMI semantic references are currently feature/selector semantic tokens (or datum IDs), not persistent topology graph identities.

### CIR-M2 placement-anchor behavior

- CIR lowerer supports only `.top_face` placement selector anchors for CIR-M2 and derives anchor from CIR node AABB centerXY + maxZ.
- Non-`.top_face` selector anchors are explicitly rejected in CIR lowering diagnostics.

## B) Current gaps and friction

1. **Contract names exceed semantic specificity for booleans.**
   Boolean contracts expose broad `edges`/`vertices`/`side_faces`, but runtime meaning is aggregate count/centroid over whatever topology exists post-boolean, not authored semantic features.

2. **No stable generated-topology identity model.**
   Runtime resolver can count/select generated edges/vertices sets, but there is no deterministic provenance identity for a specific generated edge/vertex.

3. **Topology consumption is only weakly represented.**
   When boolean input topology is consumed/trimmed/replaced, selectors on resulting feature IDs can resolve empty or different counts; diagnostics are count/empty oriented, not provenance-diff oriented.

4. **PMI uses selector tokens without topology lineage.**
   Planar PMI references rely on selector strings; no system exists to persist/verify a generated-topology semantic lineage across boolean evolution.

5. **CIR and production placement are intentionally bounded but asymmetric.**
   CIR-M2 top-face-only anchor approximation avoids drift today, but broader selector naming without shared provenance rules would create CIR/BRep semantic divergence.

## C) Naming taxonomy (recommended terminology)

### 1) Primitive-authored semantic ports

Ports authored by primitive contract and intended for semantic use:

- Box-like: `top_face`, `bottom_face`, `side_faces`.
- Cylinder/cone: plus `side_face`, `circular_edges`.
- Sphere/torus: `surface`.

### 2) Operation-authored semantic ports

Ports explicitly authored by an operation contract (current booleans expose generic set ports):

- Boolean result contract currently: `top_face`, `bottom_face`, `side_faces`, `edges`, `vertices`.
- Important: these are currently *set selectors over runtime topology*, not stable authored feature identities.

### 3) Generated topology (not yet semantic by default)

Edges/vertices/faces created incidentally by boolean/intersection/trimming/cell decomposition.

- Current system can expose aggregate sets (`edges`, `vertices`) but does not assign stable semantic per-element names.

### 4) Raw topology IDs (non-semantic)

Kernel `FaceId`/`EdgeId`/`VertexId` and topology ordering are implementation artifacts and must remain non-user semantic identifiers.

## D) Proposed future model: semantic provenance for generated topology

Principle: generated topology should become selector-addressable only when **deterministic provenance and role** exist.

Define generated identity key as:

```text
generated identity =
  authoring operation id
  + source semantic references
  + geometric role label
  + stable deterministic ordering key
```

### Required gates before exposing generated topology as semantic

Expose generated edge/vertex semantic names only if all are true:

1. Provenance deterministic (same authored inputs -> same source semantic references).
2. Role deterministic (edge/vertex function is semantically classifiable, e.g., hole mouth rim).
3. Ordering deterministic (if plural siblings, stable index key is explicit and deterministic).
4. Intentional exposure by op contract/library (not automatic for all generated topology).

If any gate fails: keep topology anonymous (debug/internal only), not selector contract surface.

## E) Rules for generated edges/vertices

### What should be allowed (future deterministic cases)

- Operation-authored, role-specific ports such as `hole.mouth_edge`, `slot.floor_face`, `pocket.axis` where op semantics define feature intent independent of incidental tessellation/order.
- Deterministic intersection-derived entities only when both parent semantic faces and ordering strategy are explicit and stable.

### What should remain disallowed

- Generic user-facing selectors like `feature.edge_42` derived from raw topology IDs.
- Auto-naming every generated edge/vertex from boolean decomposition.
- Treating topology list order as semantic identity.

### Example evaluation

- Edge from intersection of `base.top_face` and `slot.side_wall`: potentially nameable **if** operation contract explicitly exposes that seam role and deterministic pairing/order rule.
- Union seam between retained faces: usually **not nameable** unless explicitly modeled as op-authored feature with deterministic role.
- Vertex from three semantic faces intersection: nameable only if all contributing faces are semantic references and vertex role is contractually exposed.
- Arbitrary cell decomposition edge: non-semantic, keep anonymous/debug-only.

## F) Topology consumption behavior (boolean reality today)

Current behavior from runtime/tests:

- Boolean execution may fail entirely for unsupported/invalid tool/body combinations (diagnosed at compile/execution phase).
- Even when contracts permit selector ports, runtime selectability can resolve empty due to resulting topology realities (e.g., pointed-cone missing cap, boolean-derived selector mismatches).
- Diagnostics explain unknown root/invalid port/empty resolution/count mismatch, but do not provide consumed-topology lineage explanation.

Implication: current selectors are reliable as bounded contract+runtime checks, not as persistent topology identity references across consuming operations.

## G) CIR implications and CIR-M3 requirements

### CIR provenance opportunity

- CIR nodes already encode authored operation structure before BRep materialization; this is the right layer to attach semantic provenance metadata for future deterministic feature ports.
- CIR should preserve operation-authored feature identities (primitive ports and explicit op-authored ports) and pass those to downstream BRep materialization when deterministic.

### CIR-M3 must avoid semantic drift

Immediate rule for CIR-M3:

> CIR-M3 may share primitive/op-authored placement anchors that already exist in contracts, but must not invent generated topology names or per-edge/per-vertex semantic identities.

### What CIR-M3 should require

1. Shared interpretation for existing authored anchors (`top_face`, `bottom_face`, `side_face`, etc.) without broadening selector taxonomy.
2. No new selector namespace for generated topology in CIR-M3.
3. Any future generated-topology naming must be gated by explicit SEM milestone provenance rules and parity tests against production path.

## Required explicit answers

1. **What semantic names exist today?**
   Primitive and boolean contracts listed above; syntax is `feature.port`; placement supports semantic anchor selectors and bounded around-axis selector use; docs partially cover this (`firmament-selectors`, `firmament-placement-semantics`, `firmament-overview`).

2. **Authored vs generated classification?**
   - Primitive-authored: primitive port tables (`top_face`, `bottom_face`, `side_face`, `surface`, etc.).
   - Operation-authored: current boolean contract ports exist but are mostly broad topology set abstractions.
   - Generated topology: present in runtime results but not semantically identity-named by deterministic provenance.
   - Raw IDs: kernel topology ids/order only; non-semantic.

3. **Are generated edges/vertices named today?**
   - Specific generated edges/vertices: **No** stable semantic names.
   - Aggregate selectors `edges`/`vertices`: **Yes** as set ports for some feature kinds, but not stable per-element names.
   - Stability/docs: set-count behavior is tested/documented; per-element naming rules are absent.
   - Placement/PMI use: placement can anchor to aggregate edge/vertex sets via centroid; PMI planar datum path is selector-string based and does not expose generated-edge/vertex references.

4. **What happens when topology is consumed?**
   Input topology can disappear/trim/transform through booleans; selector results can become empty/mismatched. Diagnostics are explicit for unresolved root, illegal port, runtime-empty, and count mismatch; consumed-lineage explanations are not modeled.

5. **How should generated topology be named in future?**
   Use provenance key model above with strict gates; expose only deterministic operation-authored roles; keep incidental decomposition topology anonymous.

6. **How does CIR affect this?**
   CIR is the best layer to carry operation/feature provenance pre-BRep; CIR-M3 should share existing authored anchor semantics only and defer generated-topology semantic expansion to dedicated SEM milestones.

## Recommended milestones (post-SEM-A0)

- **SEM-M1**: Shared semantic port registry for primitives + StandardLibrary ops + boolean/op-authored ports with explicit result-kind semantics.
- **SEM-M2**: Provenance schema for operation-authored generated topology (face/edge/vertex candidates) with deterministic role and ordering requirements.
- **SEM-M3**: Limited generated edge/vertex selector support only for deterministic, explicitly exposed operation roles with CIR/BRep parity tests.

## Explicitly forbidden (carry-forward guardrails)

- Raw edge/vertex ID selectors as stable semantic references.
- Automatic naming of every generated topology element.
- User-facing selector identity based on BRep topology numbering or incidental iteration order.
