# SURFACE-FEATURE-A0: First-class surface feature architecture audit

Date: 2026-05-11  
Status: **Outcome A — clear surface-feature plan**  
Scope: design/audit only; no production behavior changes.

## 1) Existing feature classification (code + docs audit)

### Current authored families in Aetheris

- **Volumetric primitives**: `box`, `cylinder`, `sphere`, `torus` in Firmament lowering map directly to CIR primitive nodes (`CirBoxNode`, `CirCylinderNode`, `CirSphereNode`, `CirTorusNode`).
- **Boolean volume operations**: `add`, `subtract`, `intersect` lower to `CirUnionNode`, `CirSubtractNode`, `CirIntersectNode`.
- **Library/Forge-backed primitives**: `rounded_corner_box` and `slot_cut` are produced by `ForgeAtomics.RoundedRectangle(...)` + `ForgeAtomics.ExtrudeCentered(...)` in `StandardLibraryPrimitives`.
- **Reusable library part**: `cube_with_cylindrical_hole` in `StandardLibraryReusableParts` is implemented as `BrepBoolean.Subtract(cube, cylinder)`.
- **Edge features (bounded manufacturing style)**: chamfer/fillet are validated and preflighted as explicit bounded edge-finishing families (not generic freeform edge modification).
- **Semantic manufacturing features**: hole/counterbore/countersink/keyway/slot usage appears in Firmament fixtures/tests and PMI flows as operation-intent patterns, often currently lowered through bounded subtract families.

### Classification table (what they really are)

- `box`, `cylinder`, `sphere`, `cone`, `torus`: **Volumetric primitive**.
- `add/subtract/intersect`: **Boolean volume operation**.
- `chamfer`, `fillet` (bounded internal edges): **Edge feature**.
- `holes/counterbore/countersink` (current implementation path): **Semantic manufacturing feature lowered into constrained Boolean families**.
- `slot`, `keyway`: currently **semantic feature intent lowered as constrained subtract/tool paths**; functionally close to surface/feature intent but executed as bounded Boolean today.
- `rounded_corner_box`, `slot_cut` primitive families: **Library/Forge feature materialized as volumetric body generation**.
- Pattern-like entities (thread/knurl style): **not first-class yet** (deferred/nonexistent in current core code path).

### Which should remain booleans vs move toward surface features

- Keep true booleans for body-composition intent (`union/add`, gross subtract, intersect, multi-body composition).
- Keep bounded edge finishing as edge-feature subsystem (chamfer/fillet).
- Move groove/ridge/bead/thread-like local modifications toward **surface-feature intent**, because they are host-surface constrained and path/profile governed, not arbitrary volume CSG.

---

## 2) Surface feature definition in Aetheris terms

### Boolean
Combine/subtract/intersect volumes, with result primarily defined by volumetric set operation.

### Surface feature
A localized host-surface modification defined by:
- host surface reference + supported surface family,
- path/rule on or relative to host,
- profile + sign (add/remove),
- bounded effect region,
- materialization capability class and diagnostics.

Result is not “arbitrary tool-body CSG”; it is a constrained replacement/augmentation of host patches with predictable alignment constraints.

### Edge feature
Modification anchored to an existing edge/adjacent-face relationship (e.g., bounded fillet/chamfer families), typically not requiring a general host-surface path framework.

### Semantic manufacturing feature
High-level user intent (hole/countersink/slot/keyway/thread/etc.) that may lower into boolean, edge-feature, or surface-feature routes depending on admissibility.

---

## 3) Candidate surface-feature family analysis

## A) Round groove
- **Usefulness**: high (shafts, seals, retaining grooves).
- **Best early hosts**: planar circular groove and cylindrical circumferential groove.
- **Representation**: circular profile + closed path constrained to host frame.
- **Difficulty**: moderate if constrained; high if arbitrary path/orientation.
- **Placement**: Core for constrained variants.

## B) Ridge / bead
- **Usefulness**: high (stiffening, decorative and retention beads).
- **Representation**: groove dual (positive height instead of depth).
- **Difficulty**: similar to groove; can reuse same descriptor/materializer skeleton.
- **Placement**: Core for planar/cylindrical constrained forms.

## C) Thread
- **Usefulness**: very high.
- **Representation**: helical path + profile on cylindrical/conical host.
- **Difficulty**: high (long helical trims, dense topology, export burden).
- **Placement**: Forge/deferred first-class design path, not initial Core.

## D) Emboss/deboss
- **Usefulness**: medium-high.
- **Representation**: planar host + path/profile or text/profile projection.
- **Difficulty**: medium-high; quickly drifts into arbitrary sketch/sweep complexity.
- **Placement**: start in Forge or deferred core-experimental.

## E) Knurl / repeated texture
- **Usefulness**: medium-high manufacturing utility.
- **Representation**: patterned crossed helical/angled features on cylinder.
- **Difficulty**: very high (patterning, large topology counts).
- **Placement**: Forge/deferred.

## F) Dimple / indentation
- **Usefulness**: medium.
- **Representation**: localized depression on planar/cylindrical/spherical host.
- **Difficulty**: medium; depends on profile model and trim support.
- **Placement**: deferred after groove/ridge infrastructure proves out.

---

## 4) Core vs Forge recommendation

### Core built-ins (first wave)
- `RoundGrooveFeature` (planar circular + cylindrical circumferential constrained forms).
- `RidgeFeature`/`BeadFeature` for same constrained host/path families.

### StandardLibrary surface-feature wrappers
- ergonomic constructors/templates for common retention groove/bead parameter sets.

### Forge/deferred
- thread families,
- knurl patterns,
- emboss/deboss with complex sketch paths,
- broad freeform sweep-based local features.

### Criteria used
- high common CAD utility,
- analytic tractability under constrained alignments,
- compatibility with current curve/surface export lanes,
- reuse of existing source-surface and patch descriptor architecture,
- bounded complexity/risk.

---

## 5) Grooves: torus subtract vs semantic feature

## Option A — `Subtract(base, Torus)`
- **Pros**: minimal new language concept.
- **Cons**: re-enters generic boolean + quartic trim complexity; weak intent semantics; difficult diagnostics and selector/PMI provenance; conflicts with existing torus subtract deferral direction.

## Option B — `RoundGrooveFeature`
- **Pros**: matches design intent; enables host/path/profile admissibility checks; bounded strategy selection; cleaner diagnostics; better future PMI semantic expression.
- **Cons**: needs new descriptor/representation path.

## Option C — annotated subtract-torus (intent tag)
- **Pros**: transitional compatibility with existing author patterns.
- **Cons**: still semantically tied to boolean shape; risks ambiguous dual semantics.

### Recommendation
Adopt **Option B** as primary architecture. Option C may be transitional parsing sugar. Keep generic torus boolean materialization unsupported/secondary.

---

## 6) Proposed descriptor model

```text
SurfaceFeatureDescriptor
  FeatureId
  FeatureKind                 (RoundGroove | Ridge | Thread | ...)
  HostSurfaceRef              (semantic selector/provenance token, not raw topology id)
  HostSurfaceFamily           (Planar|Cylindrical|Conical|Spherical|Toroidal|BSpline)
  PathKind                    (CircleOnHost | HelixOnAxis | CurveOnSurface ...)
  PathParameters
  ProfileKind                 (CircularArc | VProfile | Trapezoid | ...)
  ProfileParameters
  DirectionSign               (Add | Remove)
  Extent                      (Depth/Height/Width)
  AlignmentConstraints        (coaxial/concentric/normal/clocking)
  CapabilityTarget            (CoreExact | CoreSplineApprox | Forge | Deferred | Unsupported)
  Diagnostics[]
```

Bounded concrete subtypes:
- `RoundGrooveFeatureDescriptor`
- `RidgeFeatureDescriptor`
- `ThreadFeatureDescriptor`

No generalized unconstrained sweep descriptor in A0.

---

## 7) CIR/native-state representation recommendation

Recommended: **Option D hybrid**
- Keep CIR for constructive volume intent.
- Add a separate `SurfaceFeatureGraph` in native execution state/replay lineage.

Why:
- avoids polluting current CIR boolean semantics,
- prevents “boolean swamp” for feature-like operations,
- enables feature-specific admissibility/materialization diagnostics,
- coexists with replay-driven auditability.

Implementation shape (design only):
- Extend native-state lineage alongside `NativeGeometryReplayLog` with feature graph entries referencing authored feature IDs and host selectors.
- CIR nodes remain unchanged in A0; future A2 may introduce bridging nodes only if needed.

---

## 8) Materialization architecture proposal

1. **Host selection phase**
   - resolve authored host semantic reference,
   - validate supported host family.

2. **Feature strategy planning (JudgmentEngine where multiple bounded strategies compete)**
   - candidate strategies: exact elementary patch replacement, exact mixed patch family, spline approximation, reject/defer.
   - admissibility + utility scoring produce selected strategy + rejection reasons.

3. **Patch generation phase**
   - generate source/trim descriptors (reusing CIR-F8/CURVE-A0 descriptor direction),
   - derive replacement/augmentation patches.

4. **Curve/surface realization policy**
   - prefer line/circle/ellipse/elementary surfaces when exact,
   - permit BSpline only when policy allows approximation with explicit diagnostics.

5. **Readiness gate integration**
   - reuse readiness style gates: feature must satisfy host/path/profile/export capabilities before BRep emission.

6. **BRep assembly plug-in**
   - use existing generic topology containers (faces/loops/edges/coedges/shells),
   - do not alter core topology rules in A0.

---

## 9) Constraints to avoid generalized quartic/algebraic curve scope

Hard constraints for first-wave groove/ridge support:

- Host surface family limited to supported constrained families (initially planar/cylindrical).
- Path must be canonical/aligned (e.g., concentric circle on planar face; circumferential ring on cylinder).
- Profile must be bounded simple known profile (initially circular arc/round profile).
- Arbitrary torus orientation/tool-body subtraction rejected.
- Arbitrary swept volume/local freeform CSG rejected.
- Misaligned host/tool axis rejected.
- No promise of general torus boolean or generalized quartic intersection support.

These constraints are explicit contract, not temporary hidden behavior.

---

## 10) STEP / BSpline / export policy

- Exact export is allowed when resulting faces/edges map to existing supported elementary + current edge curve types (line/circle/ellipse and existing surface set including torus/BSpline where truly represented).
- BSpline is acceptable for approximation only when explicitly classified as approximate and reported in diagnostics/provenance.
- Approximate BSpline must never be labeled equivalent to exact analytic feature semantics.
- Current STEP lane is already explicit about supported curve/surface kinds and rejects unsupported curve/surface bindings; surface-feature routing should preserve this honesty.

---

## 11) Selector / PMI / provenance policy (SEM-A0 aligned)

- Feature host attachment must use authored semantic references (selector contracts), not raw topology ids.
- Do **not** invent generated edge/vertex names.
- Feature-level semantic references (feature ID + role ports) may be added only where deterministic and contract-authored.
- PMI should reference feature-intent objects/ports where supported; unresolved generated topology remains anonymous.
- Replay/native logs should capture feature operation identity, strategy selected, capability tier, and diagnostics.

---

## 12) Implementation ladder (recommended)

### SURFACE-FEATURE-A1
Descriptor + taxonomy + validation-only contracts for `RoundGrooveFeatureDescriptor`/`RidgeFeatureDescriptor`; no materialization behavior changes.

### SURFACE-FEATURE-A2
Introduce `SurfaceFeatureGraph` in native state/replay lineage; CIR remains unchanged.

### SURFACE-FEATURE-A3
Planar circular round-groove dry-run planning path (host resolution + strategy decision + diagnostics only).

### SURFACE-FEATURE-A4
Cylindrical circumferential groove dry-run planning path.

### SURFACE-FEATURE-A5
First bounded BRep emission for one constrained family (recommended: cylindrical circumferential groove or planar circular groove), with explicit readiness/export gating.

### FORGE-THREAD-A0
Thread descriptor/planner in Forge, deferred core materialization.

---

## 13) Recommended immediate next milestone

**SURFACE-FEATURE-A1: descriptor + capability taxonomy + validation-only planning contracts.**

Why this is smallest useful step:
- preserves existing CIR/BRep/STEP behavior,
- codifies intent and admissibility constraints up front,
- creates auditable path before geometry/topology risk,
- directly addresses repeated torus/swept-boolean category errors.
