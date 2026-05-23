# AIR-A0 — Atomic Intermediate Representation kickoff

## 1) Purpose

**AIR = Atomic Intermediate Representation.**

AIR is the proposed constructive intent layer between CIR/FRep semantic recovery and BRep emission:

```text
Firmament / CIR / FRep
  -> semantic recovery
  -> AIR
  -> BRep
  -> STEP
```

AIR exists because many exact CAD feature families in current Aetheris are already naturally expressed as sweeps/profile stacks/surface features, while 3D boolean subtraction is often being used as a late-stage reconstruction mechanism. For admissible families, AIR should preserve intent explicitly before topology emission.

## 2) ABC model

### AIR (Atomic Intermediate Representation)
AIR captures **atomic constructive intent**: bounded operations such as extrude/revolve/profile-stack/surface-feature-style construction, including explicit admissibility and fallback markers.

### BRep (Boundary representation)
BRep captures **boundary topology and geometry bindings** (faces/edges/loops/coedges/curves/surfaces), i.e., what the solid boundary is after construction decisions have been made.

### CIR (Constructive/field IR)
CIR captures **evaluable constructive/field semantics** (node/tape evaluation, CSG-style combinations, transforms), i.e., a volume/evaluation representation useful for semantic reasoning and replay.

> AIR describes construction.  
> BRep describes boundary.  
> CIR describes volume/evaluation.

AIR does not replace CIR or BRep; it fills the missing intent-preserving constructive lane between them.

## 3) Why Boolean is fallback, not foundation

A0 hypothesis:

1. **3D boolean subtraction discovers topology after the fact.** It is often robust only within bounded families and can become brittle with stack depth/interference interactions.
2. **For sweep/profile-stack features, topology is known pre-emission.** Layered intent can be declared explicitly (z-spans, radii, entry side, continuity) before topology construction.
3. **2D/profile-driven construction is generally cheaper/more deterministic** than full 3D intersection-heavy routes for admissible families.
4. **Boolean remains necessary** for irreducible interactions (cross-axis intersections, unrelated solids, multi-feature interference outside one atomic lane).

Therefore AIR should be sweep-first/surface-feature-first, with Boolean retained as an explicit fallback/escape hatch.

## 4) AIR candidate atoms (A0)

### Extrude
- Represents: 2D closed profile extruded along an axis/frame.
- Examples: box-like prisms, polygonal prisms, straight-slot prisms.
- Expected analytic surfaces: planar caps + ruled/planar side faces (and circular/arc-derived cylindrical faces when profile contains arcs).
- Should not handle: arbitrary self-intersecting profiles, unconstrained profile-boolean kernels, freeform loft substitutions.

### ProfileStackExtrude
- Represents: ordered z-layers/intervals with profile/radius transitions across depth.
- Examples: through-hole, stepped-hole, bounded counterbore-like stacks.
- Expected analytic surfaces: cylindrical walls, planar annular transitions, bounded conical/chamfer transitions if explicitly admitted.
- Should not handle: arbitrary non-contiguous stacks, unconstrained cross-axis interference, implicit placement inference.

### Revolve
- Represents: profile revolved around an axis over bounded angle/range.
- Examples: cylinders, spheres (half-profile revolve), torus-like sections (bounded policy), rotational grooves.
- Expected analytic surfaces: cylindrical, conical, spherical, toroidal families; circular trims.
- Should not handle: arbitrary swept freeform blends or unconstrained generalized loft behavior.

### RuledTransition
- Represents: deterministic ruled transition between bounded profile sections.
- Examples: frustum/cone-like adapters, bounded taper transitions.
- Expected analytic surfaces: planes/cones/cylinders and explicitly bounded ruled surfaces.
- Should not handle: generic NURBS loft fallback, high-order continuity claims beyond bounded contract.

### EdgeSweep
- Represents: bounded profile sweep along a declared path/edge contract.
- Examples: keyway-like/slot-like feature sweeps on admissible host/path contracts.
- Expected analytic surfaces: path/profile-induced ruled/cylindrical/toroidal subsets under strict path/profile constraints.
- Should not handle: arbitrary 3D guide-curve networks or unconstrained Frenet-frame sweep complexity in A0.

### SurfaceFeature
- Represents: descriptor-driven surface-local features with explicit planning/evidence and deferred/materialized states.
- Examples: planar groove/ridge families, emboss/deboss-like bounded features.
- Expected analytic surfaces: host retained patches + bounded groove/profile patches with explicit trim expectations.
- Should not handle: generic torus boolean, broad freeform surfacing, topology naming generation.

### BooleanFallback
- Represents: explicit AIR escape hatch where atomic constructive intent is insufficient.
- Examples: cross-axis interacting voids, unrelated solid composition, multi-feature interference.
- Expected analytic surfaces: whatever bounded Boolean families can support.
- Should not handle: being treated as default primary primitive for sweepable/structured families.

## 5) Relationship to current code

- Current `HoleRecoveryPolicy` already decompiles CIR boolean shapes into explicit intent (`HoleRecoveryPlan`, `HoleProfileSegment`, placement metadata).
- `ProfileStackExtrudeSpec/ProfileStackLayer/ProfileStackExtrudeExecutor` are already AIR-like nucleus behavior for stacked cylindrical intent.
- `FrepSemanticRecoveryRematerializer` already routes semantic selection -> executable plan -> BRep output and fallback decisions.
- `BrepPrimitives` includes both direct topology constructors and sweep-like helpers (`BrepExtrude`, revolve helpers in bounded feature lanes), suggesting future primitive wrappers can thinly map to AIR emitters.
- `BrepBoolean` + safe composition family remain critical fallback and bounded composition mechanisms.
- Surface-feature A0-A4 already define a non-emitting descriptor/planning/evidence lane strongly aligned with a future `SurfaceFeature` AIR atom.

## 6) No-NURBS boundary policy

A0 boundary:

- AIR remains **bounded analytic** first.
- No generic NURBS/loft fallback as core AIR primitive.
- Ruled transitions are admissible when deterministic/manufacturable and explicitly bounded.
- Arbitrary loft is out of AIR scope unless a later constrained design explicitly defines admissibility, exactness contracts, and deterministic fallback behavior.

## 7) Open architecture questions

1. What minimal profile/sketch model is sufficient for AIR-X1 without importing a full sketch kernel?
2. Which current primitives should be re-expressed as AIR emitters first (box/cylinder/cone/sphere/torus ordering)?
3. Where should AIR admissibility/scoring live (reuse JudgmentEngine policies vs per-atom validators)?
4. How should AIR represent deferred/exactness states for SurfaceFeature lane without conflating with BRep availability?
5. How should BooleanFallback provenance be preserved for diagnostics and future replay?
6. How much transform/path generality is admissible in first EdgeSweep and Revolve labs?
7. What is the smallest viable AIR->BRep emitter interface that preserves diagnostics and STEP-root expectations?

## 8) Pipeline and ABC diagrams

### Pipeline

```text
Firmament/CIR/FRep
  -> Semantic Recovery
  -> AIR (Atomic Intermediate Representation)
  -> BRep
  -> STEP
```

### ABC conceptual triangle/list

```text
[AIR]  atomic constructive intent
[BRep] boundary topology + geometry bindings
[CIR]  evaluable constructive/field representation
```
