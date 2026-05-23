# AIR-A0 architecture probe report

## 1) Executive summary

Outcome: **Success (design/audit milestone)**.

Evidence indicates Aetheris already contains multiple AIR-like lanes (especially hole-family semantic plans + profile-stack execution + surface-feature descriptor planning), while bounded Boolean infrastructure remains necessary but should be treated as fallback for irreducible interactions.

Best immediate next lab: **AIR-X1 minimal Profile IR + AirProfileStackExtrude spec extraction from existing hole-family plans**, because it has highest leverage, strongest existing evidence, and lowest architecture risk.

## 2) Source code/docs inspected

### Core code inspected
- Semantic recovery/materializer:
  - `FrepMaterializerPolicyCatalog`
  - `HoleRecoveryPolicy`
  - `HoleRecoveryPlan` / `HoleProfileSegment`
  - `HoleRecoveryExecutor`
  - `ProfileStackExtrudeSpec` / `ProfileStackLayer`
  - `ProfileStackExtrudeExecutor`
  - `ProfileStackExtrudePlanAdapter`
  - `FrepSemanticRecoveryRematerializer`
- CIR/FRep:
  - `CirNode` / `CirNodes`
  - primitive nodes: box, cylinder, cone, sphere, torus
  - boolean nodes: union/subtract/intersect
  - `CirTape` and inline `CirTapeLowerer`
  - `FirmamentCirLowerer`
- BRep:
  - `BrepPrimitives` (`CreateBox/CreateCylinder/CreateCone/CreateSphere/CreateTorus` and extrusion helper usage)
  - `BrepBoolean`
  - `BrepBooleanBoxCylinderHoleBuilder`
  - `SafeBooleanComposition` / `BrepBooleanSafeComposition`
- STEP:
  - `Step242Exporter`
- Surface-feature lane:
  - `SurfaceFeatureDescriptors`
  - `SurfaceFeaturePlanningBridge`
  - `SurfaceFeatureDryRunGenerator`
  - `SurfaceFeatureEvidenceGenerator`

### Docs inspected
- `docs/frictionlab/cir-sweep-x0-profile-stack-extrude-lab.md`
- `docs/cir-sweep-v1-profile-stack-extrude-executor.md`
- `docs/cir-sweep-v2-cylindrical-hole-profile-stack-migration.md`
- `docs/cir-recovery-v19-hole-family-capability-manifest.md`
- `docs/brep-boolean-stack-a0-stepped-root-cause.md`
- `docs/brep-boolean-stack-a1-nlevel-coaxial-validator.md`
- `docs/brep-boolean-stack-a2-stepped-downstream-blocker.md`
- `docs/brep-boolean-stack-a4-stepped-repeated-subtract-productionization.md`
- `docs/surface-feature-a0-architecture-audit.md`
- `docs/surface-feature-a1-descriptors.md`
- `docs/surface-feature-a2-planning-bridge.md`
- `docs/surface-feature-a3-planar-groove-dry-run.md`
- `docs/surface-feature-a4-planar-groove-evidence.md`
- `docs/step-void-a0-brep-with-voids-audit.md`
- `docs/step-void-a1-brep-with-voids-import.md`
- `docs/step-void-a2-solid-root-export-planner.md`
- `docs/firmament-semantic-topology-naming.md` (SEM-A0)
- `docs/groove-a0-bounded-revolved-groove-audit.md`

## 3) AIR concept definition

**AIR — Atomic Intermediate Representation** is the bounded constructive intent layer between CIR/FRep semantics and BRep topology emission.

ABC model:
- **AIR** = atomic constructive intent.
- **BRep** = boundary topology.
- **CIR** = evaluable constructive/field representation.

Phrase contract:
- AIR describes construction.
- BRep describes boundary.
- CIR describes volume/evaluation.

AIR is additive architecture; it does not replace CIR or BRep.

## 4) Current code inventory

### 4.1 Already AIR-like
- `HoleRecoveryPlan` + `HoleProfileSegment` explicit intent schema (host/axis/depth/entry/exit/profile stack/placement).
- `HoleRecoveryPolicy` variant admissibility + scoring via `JudgmentEngine`.
- `ProfileStackExtrudeSpec/ProfileStackLayer/ProfileStackExtrudeExecutor` explicit stack construction lane.
- `ProfileStackExtrudePlanAdapter` intent-preserving conversion from semantic plan.
- `FrepSemanticRecoveryRematerializer` planner->policy->plan->executor orchestration.
- Surface-feature descriptor/planning/dry-run/evidence pipeline (non-emitting but intent-atomic).

### 4.2 Partially AIR-like
- `BrepPrimitives`: some direct primitive topology constructors (not intent-atomic), but also uses extrude-style helpers for prism-like families.
- `BrepBooleanBoxCylinderHoleBuilder.BuildComposition`: composition from recognized analytic intent; still under Boolean namespace.
- `Step242Exporter` root planner: strong policy architecture, but post-BRep/export layer.

### 4.3 Boolean-first but could become AIR-fronted
- Box-cylinder/cone/safe subtract families where intent is known as holes/stacks before 3D boolean operations.
- Some primitive factories (cylinder/cone/sphere/torus) can be represented as revolve/extrude atoms with bounded emitters.
- Groove/surface-feature families currently deferred/planning-only could become explicit `AirSurfaceFeature` materialization lanes.

### 4.4 Not AIR / fallback territory
- General unsupported boolean cases and non-sweepable interactions in `BrepBoolean` execution classes.
- Arbitrary mixed interactions that violate bounded coaxial/profile constraints.
- Freeform/high-order surfacing not expressed by bounded descriptors.

## 5) Primitive-as-sweep/revolve analysis

| Primitive | Extrude/Revolve/Ruled form? | Current impl trend | What needed for AIR emitter |
|---|---|---|---|
| Box | Yes (rect profile extrude) | direct topology constructor | `AirExtrude` with rectangle profile canonicalization + exact planar/cyl side policies |
| Cylinder | Yes (circle extrude or line revolve) | direct constructor; also reused in hole tools | choose canonical `AirRevolve` or `AirExtrude` form + seam/cap emission policy |
| Cone/Frustum | Yes (revolve of line segment) + ruled form | constructor + conical tool paths | `AirRevolve` + `AirRuledTransition` bounded contracts and radius-order guards |
| Sphere | Yes (semicircle revolve) | direct constructor | `AirRevolve` bounded to full/partial sphere with seam/pole policy |
| Torus | Yes (circle revolve around axis) | direct constructor, generic torus boolean often unsupported in feature lanes | constrained `AirRevolve` torus contract + explicit fallback for unsupported interactions |

Observation: all canonical primitives are representable via sweep/revolve/ruled atomics; the key work is deterministic emitter policy, not representability.

## 6) Candidate AIR node set

Recommended A0 names:

- `AirExtrude`
- `AirProfileStackExtrude`
- `AirRevolve`
- `AirRuledTransition`
- `AirEdgeSweep`
- `AirSurfaceFeature`
- `AirBooleanFallback`

Justification:
- Prefix groups by layer and distinguishes from CIR/BRep nodes.
- Matches existing evidence lanes (profile stack, surface-feature planning).
- Preserves explicit fallback semantics rather than implicit boolean defaulting.

## 7) Feature-to-AIR mapping table

| Feature family | Primary AIR atom | Notes |
|---|---|---|
| through-hole | `AirProfileStackExtrude` | already close to V2 cylindrical stack route |
| blind-hole | `AirProfileStackExtrude` | needs bounded blind stack/entry handling |
| counterbore | `AirProfileStackExtrude` | explicit layered cylindrical spans |
| countersink | `AirProfileStackExtrude` or `AirRevolve` | conical entry + cylindrical continuation |
| chamfered-entry | `AirProfileStackExtrude` or `AirRuledTransition` | bounded chamfer cone semantics |
| stepped-hole | `AirProfileStackExtrude` | strongest existing AIR-like evidence |
| fillet | `AirSurfaceFeature` (deferred) | bounded host/path/profile contracts required |
| chamfer | `AirSurfaceFeature` / `AirRuledTransition` | deterministic edge/face constraints needed |
| groove/ridge | `AirSurfaceFeature` | aligns with A0-A4 lane |
| shell | `AirBooleanFallback` (initially) | topology mutation-heavy; keep fallback first |
| emboss/deboss | `AirSurfaceFeature` | descriptor-first path |
| thread/deferred | `AirSurfaceFeature` or deferred | maintain deferred/forge policy initially |
| slot/keyway | `AirExtrude` / `AirEdgeSweep` | depends on host/path boundedness |
| lattices | fallback/deferred | outside first bounded AIR scope |
| torus-like features | `AirRevolve` / `AirSurfaceFeature` | bounded torus contracts only |

## 8) Boolean fallback boundary

Boolean remains necessary for:
- cross-axis holes and oblique intersections,
- unrelated solid composition/assembly-like boolean composition,
- non-sweepable interactions,
- multi-feature interference not representable as one bounded profile stack,
- cases where admissibility checks fail for AIR atoms.

Policy: boolean is explicit fallback atom (`AirBooleanFallback`) rather than hidden default foundation.

## 9) No-NURBS / ruled-transition policy

- AIR initial scope is bounded analytic.
- No arbitrary NURBS loft primitive in AIR-A0/AIR-X1.
- Ruled transitions allowed only with deterministic bounded contracts.
- Generic loft/freeform remains out-of-scope until explicit constrained design exists.

## 10) Risks and guardrails

### Risks
1. Over-broad AIR scope could recreate a sketch kernel prematurely.
2. Conflating AIR with BRep may erase intent diagnostics.
3. Boolean fallback might regress if AIR admissibility is too aggressive.
4. Surface-feature lane may overclaim exactness before emitters exist.

### Guardrails
1. Keep AIR atoms bounded + policy-scored via JudgmentEngine.
2. Require explicit admissibility/rejection diagnostics per atom.
3. Preserve existing Boolean and STEP behavior unchanged while probing.
4. Preserve SEM-A0 identity boundaries (no generated topology naming expansion).

## 11) Recommended AIR project ladder

1. **AIR-X1**: minimal Profile IR + `AirProfileStackExtrude` extraction from current hole-family plans.
2. **AIR-X2**: primitive-as-AIR emitters audit/prototype for box/cylinder/cone/sphere/torus.
3. **AIR-X3**: ruled-transition bounded feasibility (frustum/adapter family).
4. **AIR-X4**: surface-feature AIR lane mapping from existing A0-A4 descriptor/evidence pipeline.
5. **AIR-V1**: orchestrated AIR planner with explicit BooleanFallback and no public API changes.

## 12) Recommended immediate next milestone

**Recommended next milestone: AIR-X1**.

Why AIR-X1 first:
- strongest present evidence (`HoleRecoveryPlan` + profile-stack executor already active),
- lowest risk (bounded cylindrical families, existing tests/docs),
- direct value (formalizes first AIR atom without touching public behavior),
- prepares AIR-X2/X3 by defining profile primitives and admissibility contracts.

## 13) Confidence ratings

| Topic | Confidence | Evidence quality |
|---|---|---|
| AIR-like status of hole-family/profile-stack lane | High | direct code + test/documentation continuity |
| Primitive representability as extrude/revolve/ruled | High | geometric equivalence + current helper patterns |
| SurfaceFeature as future AIR lane | Medium-High | strong planning/evidence docs, emission still deferred |
| Boolean fallback boundary recommendations | High | BREP-BOOLEAN-STACK + bounded boolean diagnostics |
| Minimal Profile IR proposal | Medium | architectural inference from current bounded schemas |

## Required diagrams

### Pipeline diagram

```text
Firmament/CIR/FRep
  -> Semantic Recovery
  -> AIR
  -> BRep
  -> STEP
```

### ABC list

```text
AIR  = atomic constructive intent
BRep = boundary topology
CIR  = evaluable constructive/field representation
```
