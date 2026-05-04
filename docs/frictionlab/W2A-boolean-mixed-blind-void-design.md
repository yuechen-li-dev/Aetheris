# Boolean Wave 2a — bounded mixed blind-void representation design

Date: 2026-04-20 (UTC)

## 1) Outcome

**Outcome: B (uncertain but promising).**

A bounded deterministic model appears feasible **if and only if** Wave 2a is scoped to one conservative interaction class: a single axis-aligned blind prismatic pocket that does **not** volumetrically overlap prior analytic-hole volume. This keeps reconstruction inside current safe-family architecture and avoids general face-splitting/Boolean escalation.

If overlap between the blind pocket and analytic hole is required in Wave 2a, the current representation is insufficient without introducing a new interior-opening analytic span class and significantly broader rebuild logic.

---

## 2) Current representation audit

### 2.1 Box-root geometry

`SafeBooleanComposition` stores:

- `OuterBox` (root extents),
- `RootDescriptor` (box/cylinder/polygonal root kind),
- optional `OccupiedCells` for orthogonal additive/subtractive cell unions.

This is the core bounded safe-composition state carrier. It already supports representing pure box roots and orthogonal cell-decomposed states.【F:Aetheris.Kernel.Core/Brep/Boolean/BrepBooleanSafeComposition.cs†L7-L16】【F:Aetheris.Kernel.Core/Brep/Boolean/BrepBooleanSafeComposition.cs†L69-L103】

### 2.2 Analytic holes (cylinder/cone)

Analytic subtract history is stored in `Holes` as `SupportedBooleanHole` with axis, radii, centers, and span kind (`Through`, `BlindFromTop`, `BlindFromBottom`, `Contained`).【F:Aetheris.Kernel.Core/Brep/Boolean/BrepBooleanSafeComposition.cs†L135-L172】

Reconstruction path is centralized in `BrepBooleanBoxCylinderHoleBuilder.BuildComposition`, which selects bounded families (through hole chains, blind/coaxial special cases, sphere-only branches, etc.).【F:Aetheris.Kernel.Core/Brep/Boolean/BrepBooleanBoxCylinderHoleBuilder.cs†L25-L117】

### 2.3 Orthogonal pockets

Orthogonal subtract classification computes split planes, removes tool-overlap cells, and validates connectivity. Result is a bounded `OccupiedCells` set for one-body reconstruction via `BrepBooleanOrthogonalUnionBuilder.BuildFromCells`.【F:Aetheris.Kernel.Core/Brep/Boolean/BrepBoolean.cs†L1756-L1808】【F:Aetheris.Kernel.Core/Brep/Boolean/BrepBoolean.cs†L990-L1002】

### 2.4 Prismatic through-cuts

Prismatic continuation classification currently enforces through-cut span for prior-history chains and rejects blind continuation with an explicit diagnostic that analytic+orthogonal reconstruction is missing.【F:Aetheris.Kernel.Core/Brep/Boolean/BrepBoolean.cs†L1838-L1845】

For successful through-cut continuation, state is converted to `ThroughVoids` metadata, and rebuild dispatches either to the mixed-through builder (single-hole case) or pure prism through-cut builders.【F:Aetheris.Kernel.Core/Brep/Boolean/BrepBoolean.cs†L767-L785】【F:Aetheris.Kernel.Core/Brep/Boolean/BrepBoolean.cs†L1004-L1020】

### 2.5 Mixed-through-void reconstruction

`BrepBooleanBoxMixedThroughVoidBuilder` is intentionally narrow:

- box root only,
- no open-slot history,
- exactly one analytic hole,
- axis-aligned cylinder/cone,
- containment-only interaction class,
- strict anti-grazing margin.

Then it rebuilds as pure prismatic through-cut and clears analytic-hole history (`Holes = []`), retaining only prismatic through-void metadata.【F:Aetheris.Kernel.Core/Brep/Boolean/BrepBooleanBoxMixedThroughVoidBuilder.cs†L17-L79】

### 2.6 Where compatible vs divergent

Compatible today:

- shared safe-composition carrier (`SafeBooleanComposition`) can hold multiple bounded descriptors at once (holes, cells, through-void tags).

Divergent today:

- analytic rebuild path consumes `Holes` semantics;
- orthogonal rebuild path consumes `OccupiedCells` boundary extraction;
- mixed-through path sidesteps coexistence by collapsing to one surviving representation (`ThroughVoids`) and dropping analytic descriptors.

This is exactly why mixed-through works while mixed-blind does not.

---

## 3) Root cause of current failure

Wave 2a target fails today due to a **combined representational + procedural gap** (not primarily topological at baseline scope):

1. **Representational gap for mixed blind state**
   - There is no canonical “analytic holes + one blind prismatic void” reconstruction contract.
   - Current mixed-through path explicitly erases analytic holes and emits only prismatic through-void metadata, which is invalid for blind continuation because blind cuts do not consume full height and may leave analytic remnants above/below floor depending on interaction class.【F:Aetheris.Kernel.Core/Brep/Boolean/BrepBooleanBoxMixedThroughVoidBuilder.cs†L64-L70】

2. **Procedural rebuild gap**
   - No builder currently takes both analytic-hole history and blind prismatic pocket history and deterministically materializes one manifold body.
   - The classifier therefore rejects the scenario with explicit defer diagnostic.【F:Aetheris.Kernel.Core/Brep/Boolean/BrepBoolean.cs†L1841-L1845】

3. **Topological risk appears only when overlap is allowed**
   - If pocket volume intersects analytic volume, interior openings and face/loop splitting become required.
   - That case tends toward new span semantics and broader face decomposition; not required for a conservative non-overlap Wave 2a scope.

---

## 4) Evaluation of candidate strategies

## Option A — Extend orthogonal cell model (baseline)

### Concept
Keep analytic holes as analytic descriptors, add a single blind prismatic pocket as bounded orthogonal removal (`OccupiedCells`), and rebuild from a unified mixed state.

### Pros

- Reuses existing bounded primitives (`SafeBooleanComposition`, cell connectivity checks, orthogonal builder).
- Keeps deterministic family framing.
- Natural place for blind pocket depth constraints (cell split along pocket floor Z).

### Gaps / required additions

- Add a dedicated mixed rebuild route that understands both `OccupiedCells` and `Holes` together; neither existing builder currently does that end-to-end.
- Enforce strict interaction constraints (recommended: no analytic-pocket volumetric overlap in Wave 2a).

### Verdict
**Most compatible with current architecture** if constrained to non-overlap mixed state.

---

## Option B — Extend mixed-through representation to support depth

### Concept
Generalize `BrepBooleanBoxMixedThroughVoidBuilder` to accept blind-depth prism tools.

### Blocking assumptions in current code

- Through-oriented identity: builder reconstructs full prism through-cut and wipes analytic-hole state.
- Interaction model is only “analytic contained in prism footprint” for through-void collapse semantics.

These assumptions are safe for through-cuts but do not preserve blind residual geometry/state.

### Complexity risk

Relaxing through assumptions without a new representation effectively forces implicit partial-volume CSG semantics. That is the beginning of general-Boolean creep.

### Verdict
**Not recommended for Wave 2a** as primary route.

---

## Option C — Layered reconstruction (compositional)

### Concept
Rebuild stage 1: box + analytic holes. Stage 2: apply blind pocket as separate bounded operation.

### Pros

- Clear separation of concerns.
- Deterministic execution order.

### Risks

- If stage-2 operation interacts with analytic faces, deterministic bounded behavior requires new clipping/trim contracts.
- Without strict guardrails, can diverge into dual-representation drift (analytic-first BRep vs metadata-first state).

### Verdict
**Viable only with strict non-overlap interaction class** (or with additional representation work that exceeds Wave 2a bounds).

---

## 5) Interaction constraints (Wave 2a bounded target)

## Allowed

1. Box-root safe composition with supported analytic history (cylinder/cone, current bounded rules).
2. Exactly one axis-aligned blind prismatic pocket continuation.
3. Pocket fully contained in root XY footprint and opening on exactly one exterior root face (existing pocket-family invariants).
4. **No volumetric overlap between pocket volume and analytic-hole volume** (strict positive clearance).

## Rejected

1. Any pocket/analytic volumetric overlap (including pocket floor cutting through analytic wall).
2. Tangent/edge-grazing contact between pocket boundary and analytic boundary.
3. Topology-splitting outcomes (multiple disjoint solids).
4. Non-manifold outcomes.
5. Partial side leaks / multi-mouth openings requiring general face splitting.
6. Any case requiring new interior-opening analytic span semantics in Wave 2a.

This rejection set keeps Wave 2a bounded and deterministic.

---

## 6) Minimal viable bounded model

### 6.1 Data to store

Extend safe-composition state with one explicit blind prismatic continuation descriptor, e.g.:

```csharp
record SupportedPrismaticBlindVoid(AxisAlignedBoxExtents Bounds, IReadOnlyList<(double X,double Y)> Footprint, BlindEntryFace EntryFace);
```

and store at most one instance (Wave 2a cap) alongside existing `Holes` and optional `OccupiedCells`.

(illustrative only; not implementation).

### 6.2 Builder contract

Add one bounded mixed blind builder lane:

- input: recognized box root composition + analytic-hole history + one blind prismatic descriptor,
- precondition: interaction class = strict non-overlap,
- reconstruction: deterministic single-body result,
- output: updated safe composition preserving analytic-hole history and blind-pocket descriptor.

### 6.3 Invariants

- Exactly one blind prismatic continuation descriptor.
- Analytic history limited to existing bounded cylinder/cone set.
- World-Z alignment rules unchanged.
- Positive clearance between analytic and pocket volumes.
- Result must remain a single connected manifold.

### 6.4 Explicitly out of scope

- Any overlap/trimmed analytic-pocket mixed geometry.
- Multi-pocket chains.
- Non-axis-aligned pocket tools.
- Open-slot + blind-pocket mixing.
- Non-box roots.
- General BRep Boolean fallback.

---

## 7) Diagnostics strategy

## Accept when

- Tool and history pass existing bounded-family checks,
- mixed interaction classifier proves strict non-overlap + positive margins,
- reconstruction validates manifold + connected-body invariants.

## Reject when

- mixed interaction class is overlap/tangent/ambiguous,
- any invariant above fails,
- reconstruction cannot guarantee one deterministic bounded output.

## Required diagnostic shapes

Use deterministic, family-specific messages (no silent fallback):

- `... bounded mixed blind-void continuation requires strict non-overlap between analytic history and blind prismatic pocket volume.`
- `... bounded mixed blind-void continuation supports exactly one blind prismatic pocket.`
- `... bounded mixed blind-void continuation rejected: tangent/edge-grazing analytic-pocket boundary contact.`
- `... bounded mixed blind-void reconstruction failed bounded manifold validation.`

All should carry explicit source tags similar to existing Boolean family diagnostics.

---

## 8) Final recommendation

**Recommendation: Proceed, but only with the conservative Wave 2a scope above (Outcome B).**

Rationale:

- A clean bounded path exists for non-overlap mixed blind continuation.
- Complexity remains controlled and architecture-aligned (no general Boolean engine work).
- Overlap support is a different milestone and should remain explicitly deferred.

---

## 9) High-level implementation plan (if proceeding)

1. Add explicit mixed blind descriptor to safe-composition metadata (single-entry cap).
2. Add bounded mixed blind interaction classifier (strict non-overlap only).
3. Add dedicated mixed blind rebuild lane (new builder) selected before generic fallback.
4. Preserve deterministic diagnostics for all rejected interaction classes.
5. Add focused tests:
   - positive non-overlap case,
   - tangent/grazing reject,
   - overlap reject,
   - disconnected-topology reject.
6. Update deferred/scope docs to encode the exact accepted class and explicit exclusions.

---

## 10) Architectural blocker (for broader scope)

If product requires blind-pocket/analytic overlap handling, blocker is:

> **missing bounded representation for interior-opening analytic remnants and associated face-loop splitting semantics**.

That is beyond Wave 2a conservative scope and should be treated as a separate architecture decision.
