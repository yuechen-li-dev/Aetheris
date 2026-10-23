# Era 5: AIR and sweep-first

**23 May – 17 June 2026 · about 110 commits · AIR-A0/X/V, V2-A/X/V, EDGE-*, AIR-CIR-A0, AIR-REGION**

## In one paragraph

The pivot. Instead of discovering topology by colliding surfaces at run time,
Aetheris would declare it from construction intent. A box is a rectangle
extruded; a counterbored hole is a stack of profile intervals; a chamfer on an
extruded edge is a different 2D profile. The new middle layer, AIR (Atomic
Intermediate Representation), carries that intent down to an explicit topology
plan, and B-rep is emitted already correct. On 14 June a long document called
the "AIR-A0 constitution" fixed the architecture that every later era builds
on: Firmament → AIR → BRep → STEP, with CIR as an optional analysis mirror.

## What was built

- **The doctrine.** `aetheris-v2-sweep-first-architecture.md` (23 May) put it in
  three lines: *Boolean union composes. Boolean subtraction discovers. Aetheris
  prefers declared topology over discovered topology.* Primitives were
  reclassified as conveniences over constructive families (box = rectangle
  extrude, cylinder = circle extrude or revolve, sphere = revolved arc). The kernel
  foundation became the **resolved 2D profile**, not a sketch solver.
- **Production migrations (AIR-V1 to V5).** Profile-stack extrusion for
  cylindrical holes, blind holes and counterbores as interval semantics, then Box
  as `AirExtrude`, Cylinder as `AirRevolve` and Frustum as a ruled transition, all
  with topology and STEP parity against the old routes.
- **Profiles (V2-A1, V2-X1 to X8, V2-V1 to V5).** A resolved `Profile2D` contract;
  bounded 2D Boolean normalization (2D Booleans resolve point intersections and
  winding, which is far cheaper and more predictable than 3D); slot and capsule
  profiles; line/arc loop extrusion; triangle and hexagonal prism parity; and the
  first production route, `ProfileHoleExtrude`, for through holes.
- **Chamfers, reframed (EDGE-X1 to X13, EDGE-A0 to A3, EDGE-PROFILE,
  EDGE-PRISMATIC, EDGE-LOOP).** First an `AirChamfer` lane that patched sharp
  bodies, with policy, topology plans, real-body probes, shadow routes and STEP
  corpora. Then EDGE-A2's reframing: *do not trim if construction history can emit
  the final body directly.* A vertical-edge chamfer is a pentagon instead of a
  rectangle; a top-edge chamfer is a section transition. EDGE-A3 added a selection
  taxonomy (single edge, face-boundary loop, whole body) that still defines what
  edge finishes support.
- **Authority (AIR-CIR-A0, CIR-MAP, CIR-PRISMATIC).** AIR owns construction; B-rep
  owns emitted topology and STEP; CIR is a mirror for analysis where one is
  admitted; STEP import is not recovered AIR unless a recognizer says so.
- **The constitution (AIR-A0, 14 June).** The canonical pipeline Firmament →
  Feature AIR → Constructive AIR → BRepPlan → BRep → STEP, and an explicit
  statement that Firmament → CIR → BRep must not be the default.
- **Regions and tracing (AIR-A1, AIR-REGION X1–X13, AIR-X6 to X11).** Scoped local
  construction for features on other axes, worked through on a side hole from
  yield contract to face splitting to shell closure, and `aetheris trace`, which
  shows how a fixture lowers stage by stage.

## Decisions that stuck

- **Firmament → AIR → BRep → STEP.** This is still the architecture.
- **Sweep-first, analytic-first.** Prefer extrude, revolve, sweep and transitions
  over 3D subtraction; use 3D Booleans only when intent cannot be declared.
- **No sketch solver in the kernel.** Constraints, if any, are resolved before the
  kernel sees a profile.
- **Edge finishes are constructions, not mutations**, organized by selection
  pattern.
- **CIR is never the topology authority.**

## Claude's commentary

This is the most important document trail in the repository. What makes it
unusual is how directly it follows from the failures before it. The safe Boolean
families of era 3 were already construction recipes; profile-stack execution in
era 4 was already declared topology. The sweep-first document says so in its own
words: "This is not a failure narrative." It names the earlier work as the
evidence that exposed the leverage.

It is also where Aetheris stopped being a kernel with a language on top and
became a compiler. Once intent is preserved down to a topology plan, the usual
compiler ideas apply: intermediate representations with clear owners, lowering
stages you can trace, admission checks at each stage, and diagnostics that point
back at source. Most of the vocabulary used today, admission, lowering, authority,
golden path, comes from these weeks.

## What survives

- `Aetheris.Kernel.Core/Air` and `Aetheris.Kernel.Firmament` lowering, including
  `aetheris trace`
- The profile-stack and section-transition emitters behind Compose, holes and
  edge finishes
- The sweep-first and AIR constitution documents, summarized in
  [DECISIONS.md](../DECISIONS.md)

Previous: [Era 4](04-cir-and-the-recovery-wall.md) · Next: [Era 6: Firmament V2 and the AP242 MVP](06-firmament-v2-and-the-ap242-mvp.md)
