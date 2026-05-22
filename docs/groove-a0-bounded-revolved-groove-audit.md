# GROOVE-A0: bounded toroidal / revolved groove feature audit

## Outcome

**Outcome A — clear bounded groove plan.**

Aetheris can plausibly support practical torus-derived groove features through a bounded semantic groove family layered on existing analytic surfaces + BSpline curve support, while keeping generic torus booleans explicitly unsupported for exact CIR→BRep materialization.

---

## 1) Existing groove-like feature audit

### Firmament vocabulary and lowering reality

Current known operation vocabulary includes `torus`, `straight_slot`, `slot_cut`, `chamfer`, and `fillet` in addition to baseline primitives/booleans. This confirms there is already groove-adjacent language, but not a dedicated revolved groove intent op.

- `straight_slot` and `slot_cut` are primitive ops.
- `chamfer` and `fillet` are boolean ops with bounded manufacturing execution paths.
- `torus` is available as primitive and as boolean tool op.
- No dedicated ring groove / revolved groove op is present.

### Existing capability status by family

- **Straight slot / slot-cut:** implemented and executable in Firmament primitive execution paths.
- **Keyway subset:** implemented as bounded canonical success family on cylinder roots (via bounded subset logic/tests), but not as a generic revolved groove abstraction.
- **Counterbore/countersink:** present primarily as recognition/PMI exportable feature stack patterns, not as dedicated standalone groove family.
- **Chamfer/fillet:** bounded manufacturing boolean families exist.
- **Torus boolean subtract/add/intersect:** accepted as intent, analyzable in CIR, but exact CIR→BRep torus boolean materialization remains explicitly unsupported.

### CIR + materializer state

- CIR has `CirTorusNode` representation and tape lowering payload for torus.
- Replay-guided materializer registry intentionally recognizes `subtract(box,torus)` then returns explicit unsupported materialization diagnostic.
- Source surface extraction and dry-run scaffolds include toroidal descriptors; downstream trim/materialization for toroidal subtract interactions remains deferred.

**Conclusion:** Aetheris already supports torus as analyzable intent and toroidal surfaces as first-class geometry, but does not support exact generic torus boolean materialization. Groove-like support exists in straight/prismatic families, not revolved toroidal groove semantics.

---

## 2) Candidate bounded groove families

### A. Planar round groove (on plane)

**Most viable modeling:** semantic revolved groove descriptor (not generic torus subtract as public abstraction).

- Canonical construction can be interpreted as a circle/arc profile revolved around axis normal to plane.
- If bounded to axis-normal planar alignment and concentric constraints, topology can be kept predictable (annular loops, periodic seam handling on toroidal patch where applicable).
- Required trims are often circular in symmetric cases; off-canonical boundaries may require deterministic BSpline trim approximations.

**Feasibility:** high for bounded subset, provided materializer explicitly rejects non-canonical orientations/depths that imply general quartic trimming.

### B. Cylindrical circumferential groove (coaxial)

**Most viable modeling:** semantic revolved groove descriptor with explicit coaxiality constraints.

- Reduces naturally to radial/axial section profile about cylinder axis.
- Resulting geometry can involve cylindrical retained surfaces plus toroidal blend-like groove wall segments (depending on profile).
- With strict coaxial/concentric constraints, many boundary curves remain circle/line families; harder cases can use deterministic BSpline trims if policy allows approximation.

**Feasibility:** high-medium for narrow coaxial subset; good manufacturing relevance.

### C. Spherical latitude groove (concentric)

**Most viable modeling:** semantic revolved groove with strict concentric/latitude constraints.

- Can be parameterized by sphere center + groove latitude frame.
- Likely introduces toroidal/local blend-like surfaces plus spherical retained regions.
- Trim behavior is more complex than planar/cylindrical families; approximation reliance likely higher.

**Feasibility:** medium; likely useful but lower priority than planar/cylindrical due to higher complexity and weaker immediate ROI.

### D. Generic torus subtract

**Recommendation:** keep deferred/unsupported for exact materialization.

- Reintroduces quartic/general algebraic curve burden.
- Blurs feature intent and weakens diagnostics.
- Contradicts current CURVE-A0/CIR-F8 trajectory.

---

## 3) Torus subtract vs semantic groove feature

### Recommendation: Option B (`RevolvedGroove` / `RoundGroove` semantic feature)

Semantic groove intent is the right abstraction for bounded manufacturable families.

Why:

1. Encodes admissibility constraints (coaxiality, concentricity, profile family) directly.
2. Supports precise rejection diagnostics when users leave bounded subset.
3. Avoids accidental commitment to generalized torus boolean kernel behavior.
4. Aligns with existing bounded-family strategy used by chamfer/fillet/keyway-like subsets.

`Subtract(base, Torus)` may still remain a legal lower-level expression in CIR, but should not be the primary feature-language abstraction for new groove family rollout.

---

## 4) CIR representation recommendation

### Preferred shape: dedicated `CirRevolvedGrooveNode` (or equivalent intent node)

Preferred over plain `CirSubtractNode(CirTorusNode)` for this feature family.

- Preserves explicit feature intent and constraints for analysis/materialization.
- Keeps fallback path analyzable even when materialization unavailable.
- Prevents overgeneralization pressure from arbitrary torus booleans.

### Transitional fallback option

If introducing a new node is too large for A1/A2, attach bounded groove intent metadata to subtract+torus lowering contracts, but this is second-best because intent becomes indirect.

---

## 5) BRep representation requirements by feasible family

## Shared surface/curve requirements

- Surface families: planar, cylindrical, spherical, toroidal (already present), optional BSpline surface generally not required for first bounded subsets.
- Trim curve families: line/circle/ellipse where exact; BSpline3 where deterministic approximation is acceptable and explicitly labeled non-exact.
- Topology: robust loop handling for annular/ring loops, inner loops, and periodic seam handling (especially toroidal patches).

### Planar round groove

- Expected surface participants: planar retained face + toroidal groove wall region (or equivalent revolved patch decomposition).
- Likely loop pattern: outer retained planar loop + inner annular groove loop(s).
- Seam: toroidal periodic seam logic needed where toroidal patch participates.

### Cylindrical circumferential groove

- Expected participants: cylindrical retained surface segments + toroidal groove patch(es).
- Loops may include circular edges around axis and possibly seam-coincident edges on periodic surfaces.
- Requires robust periodic loop normalization across cylindrical and toroidal domains.

### Spherical latitude groove

- Expected participants: spherical retained region + toroidal/revolved groove patch family.
- Higher risk of complex loop routing and seam interactions; likely needs stronger dry-run evidence before production materialization.

---

## 6) BSpline trim feasibility judgment

BSpline trim support appears viable as a bounded approximation lane when exact elementary representation is unavailable.

- Aetheris already has `BSpline3Curve` in core geometry families and STEP importer/exporter coverage for BSpline entities.
- Deterministic STEP round-trip infrastructure and tests exist for BSpline surfaces/curves in the current stack.
- Therefore deterministic internal BSpline generation for bounded groove trims is plausible.

Policy requirement:

- Never label spline approximations as exact analytic intersections.
- Diagnostics must state whether a trim is exact elementary (line/circle/ellipse) vs deterministic spline approximation.

---

## 7) Constraints that avoid quartic/general algebraic curve burden

For bounded groove families, enforce all of the following:

1. **Coaxial/concentric alignment** with base analytic surface frame (plane normal axis, cylinder axis, or sphere center relation).
2. **Profile in controlled section plane** (radial/axial section), revolved with known axis.
3. **Restricted profile kind** (round/semicircular only for initial families).
4. **No arbitrary torus orientation** relative to base body.
5. **No arbitrary base-surface intersections** beyond known bounded family mappings.
6. **Admissible depth/radius limits** to avoid self-intersection or ambiguous topology.

Reject cases requiring arbitrary plane×torus quartic/algebraic intersection extraction outside bounded reducible subset.

---

## 8) Generic torus boolean diagnostic policy

Keep current policy direction and make it more explicit:

- `subtract(box, torus)` generic: analyzable in CIR, exact materialization unsupported with explicit reason.
- Misaligned torus “groove-like” input: reject as outside bounded groove admissibility (axis/coaxial constraints violated).
- Unsupported base surface family: reject with bounded-family list.
- Arbitrary torus orientation: reject with explicit “would require generalized quartic/algebraic trim handling”.
- No silent tessellation fallback in STEP exact export lanes.

Diagnostic taxonomy should separate:

1. **Intent recognized but outside bounded subset**;
2. **Subset recognized but exact trim unavailable**;
3. **Requires unsupported algebraic intersection family**.

---

## 9) Proposed bounded feature/materializer architecture

## GrooveFeatureDescriptor (concept)

Recommended descriptor fields:

- `GrooveKind` (PlanarRound, CylindricalCircumferential, SphericalLatitude)
- `BaseSurfaceFamily` (Plane/Cylinder/Sphere)
- `Axis`
- `ProfileKind` (Semicircle/Round)
- `ProfileRadius`
- `CenterlineRadius` (major-like radius for revolved section)
- `Depth`
- `Width` (derived or explicit depending on profile convention)
- `AlignmentConstraint` (coaxial/concentric/normal)
- `MaterializationCapability` (ExactReady / SplineApproxReady / Deferred / Unsupported)
- `Diagnostics`

## CIR representation

Preferred: `CirRevolvedGrooveNode` carrying descriptor + provenance handles.

Alternative (transitional): annotated `CirSubtractNode(CirTorusNode)` with groove-intent metadata sidecar; acceptable short-term but weaker long-term.

## Materializer strategy

1. Admissibility gate: strict constraint validation.
2. Candidate route selection: if multiple bounded routes exist (exact elementary trims vs deterministic spline route), use **JudgmentEngine** utility scoring with explicit admissibility + rejection reasons.
3. Emit only truthful capability tier:
   - exact analytic trim route when available,
   - deterministic spline trim route when approved,
   - otherwise explicit unsupported/deferred.

## Export policy

- Prefer exact elementary STEP curve entities whenever exact trims exist.
- Allow deterministic BSpline curve export for approved approximation lanes, explicitly marked as approximated in diagnostics/metadata where applicable.
- Keep CIR-only fallback when exact/export-safe route unavailable.
- No tessellation masquerading as analytic STEP output.

---

## 10) Smallest safe implementation ladder

1. **GROOVE-A1 — semantic descriptor + validation contracts (no emission).**
   - Add `GrooveFeatureDescriptor` and admissibility rule set.
   - Add diagnostics taxonomy.

2. **GROOVE-A2 — CIR intent representation.**
   - Introduce `CirRevolvedGrooveNode` (or transitional annotation path).
   - Keep materialization no-op/deferred, analysis-only.

3. **GROOVE-A3 — dry-run surface/loop/topology planner for bounded planar+cylindrical subsets.**
   - Reuse CIR-F8 descriptor scaffolds.
   - Produce readiness evidence only.

4. **GROOVE-A4 — deterministic BSpline trim generation prototype + policy hooks.**
   - Deterministic construction tests + STEP roundtrip checks.

5. **GROOVE-A5 — first production bounded materializer (planar round groove).**
   - Exact-first; spline fallback only when policy explicitly allows.

6. **GROOVE-A6 — cylindrical circumferential groove production path.**

7. **GROOVE-A7 — spherical latitude groove feasibility re-evaluation.**
   - Proceed only if A6 evidence shows robust seam/loop stability.

---

## 11) Recommended immediate next milestone

**Recommended next step: GROOVE-A1.**

Rationale:

- It captures feature intent and constraints before geometry emission.
- It prevents architecture drift into generic torus boolean expectations.
- It creates the contract needed for admissibility diagnostics and later JudgmentEngine route selection.

