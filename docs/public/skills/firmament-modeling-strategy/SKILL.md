---
name: firmament-modeling-strategy
description: "Plan, author, revise, and review Firmament CAD models using stable Concept scaffolding, functional features, reusable components, profiles, section chains, and semantic routes. Use for CAD construction strategy, reference-based reconstruction, or improving model organization and physical coherence. Identify compiler capability gaps with reproducible evidence and recommended permanent fixes; do not turn tooling workarounds into modeling doctrine."
---

# Firmament modeling strategy

Construct an editable explanation of the object. Final faces describe what
exists; source should explain its shape, references, repetition, and connections.
This skill distills the Aetheris CAD strategy notes and the guitar authoring
session of 2026-09-30 through 2026-10-01. It is strategy guidance, not a language
specification or authorization to implement compiler extensions.

## Establish the modeling contract

Before choosing features, identify the intended deliverable: presentation
witness, editable engineering model, reconstruction, or manufacturing release.
For a small task, this can be a few sentences, not a separate planning ceremony.

- Identify the shape-defining silhouette, major dimensions, coordinate frame,
  units, and functional attachment points.
- Separate supplied dimensions and inspected geometry from visual estimates.
  A photograph supplies proportions, not hidden construction or exact scale.
- State what must be geometric, what may be simplified, and what belongs only
  to rendering. A presentation witness still needs coherent physical connections.
- For reconstruction, separate measured facts, candidate construction strategy,
  confidence, and alternatives. Equivalent geometry does not prove original history.

Resolve repository paths below against the active Aetheris checkout. Read its
`AGENTS.md` and relevant current public guides. The textbook contains explicitly
speculative syntax; copy admitted recipes from current guides and qualified
fixtures instead. If those sources are unavailable, use the strategy but do not
invent syntax or assert a capability.

## Choose a shape spine before details

Choose a small dependency graph, then serialize it into features:

```text
design datums and dimensions
  -> primary mass / silhouette
  -> major additions, removals, and structural transitions
  -> functional details and repetition
  -> edge finish and appearance
```

This is a reasoning diagram, not Firmament syntax. Order follows dependencies;
it is not a universal requirement to finish every additive feature before any cut.

| Design intent | First representation to consider |
| --- | --- |
| Prismatic housing, bracket, fixture | Simple stock, bosses, pockets, semantic holes and slots |
| Shape defined by its planar silhouette | Closed Profile with named constructive edits |
| Carved top, tapered shaft, transition between cross-sections | Corresponding profiles on explicit SectionChain frames |
| Axisymmetric component | Revolve when its meridian expresses the design; cylindrical extrusions can also be appropriate |
| Formed wire or tube | Its existing semantic route/sweep family, with endpoints and bend intent |
| Repeated complete component | Shared definition and keyed assembly occurrences |

A guitar outline genuinely needs profile and surface expressiveness; forcing it
into boxes would obscure the design. Conversely, do not hide a bracket's holes,
reliefs, and mounting pads inside one elaborate outer sketch.

Preserve hole intent: entry support, axis, diameter or fit, end condition, and
any axial stack. A cylinder subtraction may produce the right geometry while
losing the fastening or locating relationship. Use the supported semantic form;
report missing semantics rather than asserting an unsupported standard or thread.

Keep small 3D fillets and chamfers late and local. Rounded profile corners define
the silhouette before extrusion. A broad neck-to-head transition connects masses
and belongs to the shape spine; it is not automatically a finishing fillet.

## Build Concept scaffolding that owns placement

Use named planes, axes, frames, and point collections to express the design
layout. Concepts are construction authority and are erased from material output;
they are not extra parts. They may be idealized independently of the final body.
Materialized geometry must still pass its own validity and contact checks.

- Prefer design-owned datums when they express independent layout intent.
  Derive a Concept from a physical definition's published geometry when that
  geometry legitimately owns the reference. Keep the dependency one-way.
- Publish semantic ports from the definition that knows their meaning. Consumers
  reference its mounting seat or string entry, rather than rediscovering an edge
  or traversing private children.
- Declare the common Concept datum, its Interface members, and each Mate's
  realization. This directly expresses a relationship to known scaffolding;
  do not invent a simultaneous pairwise constraint-solving problem.
- Give each occurrence one placement authority. Do not add a Placement and a
  competing datum Mate to compensate for uncertainty.
- A plane supplies a normal but does not fully locate and clock a rigid member.
  Supply a complete target frame. A spatial coaxial relationship likewise needs
  an axial station and a radial clocking reference; use a Revolute relationship
  only when its remaining angle is intentional. Planar `Axis2` and spatial
  `Axis` are different concepts.
- Express meaningful offsets in the owning local frame. Absolute coordinates
  are legitimate at layout roots; copying their values into every child makes
  the relationship fragile.

For curved surfaces, separate the desired seat from the overall surface shape.
A carved body may have a deliberately planar hardware crown; multiple components
can conform to its derived datum. An arbitrary tangent seat needs a supported
surface evaluation/attachment contract. A published headstock frame does not
prove arbitrary non-planar placement is implemented.

Coincident frames alone do not prove physical support. Inspect the material at
the seat, clearance, and the intended footprint. A point-contact check does not
prove full-footprint support or fastening. In the guitar, a common hardware deck
made the bridge's separate floating height visibly wrong and easy to remove.

## Separate component structure from feature construction

Use a part for a coherent body built from features. Use a subassembly when
separate bodies have meaningful component identity, materials, interfaces, or
motion. Choose from product intent, not from a count of visible primitives.

```text
Pickup part definition
  housing -> two coil features -> patterned pole feature
Guitar assembly
  neck pickup occurrence
  bridge pickup occurrence

Tuner subassembly definition
  washer + post + stem + button, with a public mounting frame
Headstock assembly
  keyed occurrences of the complete tuner
```

These are construction/product trees, not literal source. The pickup witness is
a deliberately simplified compound part; it does not establish that a real
pickup's coils and magnets are one manufactured material body.

Reusable functional `Feature` constructors return construction intent. Keep
their parameters and local references explicit. Instantiate variants with
Template/Record parameters and immutable `with` values where admitted.

Pattern a feature inside the owning part when it is repeated construction.
Pattern complete occurrences when it is repeated equipment. Avoid global groups
of all washers, all posts, and all buttons: they hide which tuner owns what.
Group related recipes in `.firmament` modules with the product composition in
the root `.firmasm`. Share definitions; preserve stable keys for occurrences.

Mirroring mounting sites does not automatically mirror component handedness or
orientation. Set clocking deliberately; use another definition for genuinely
handed hardware. Inspect one seed component before expanding its pattern.

## Author profiles and surfaces for controlled edits

Start a silhouette from a closed primitive or a simple named scaffold. Replace
bounded spans or apply admitted constructive modifications. Name landmarks such
as waist, horn, shoulder, and neck seat; keep shape decisions local to them.
Use explicit boundary chains when the shape actually requires that control,
not as a substitute for deciding what the outline means.

Inherited endpoints reduce closure bookkeeping; they do not prove that a curve
cannot cross itself. Check orientation, closure, cusps, and intersections after
each meaningful profile change. Interpolation passes through points but can
overshoot between them. Do not assume a fairness optimizer exists.

For a section chain:

1. Decide section stations, local frames, orientation, seam, and span correspondence.
2. Establish width/depth envelopes and endpoint/contact conditions first.
3. Keep matching spans meaningful across sections; changing order can twist the surface.
4. Use a few purposeful sections, adding isolation stations where a local
   transition must stop influencing a long shaft or broad top.
5. Inspect side/back silhouettes and sections, not only the attractive front view.

A rounded neck cross-section need not bulge along its length. The guitar kept
its D-shaped sections while giving the shaft a nearly straight taper and isolating
the short end transition. That is a local shape correction, not a new surface engine.

Distinguish exact representation, design accuracy, and continuity. An exact
polynomial surface can exactly represent an approximate design. A closed solid
does not prove G1/G2 continuity, manufacturability, or good proportions. Claim
only the continuity and interchange properties actually qualified.

## Choose the right series and route references

- **Equal spacing:** keyed linear points or sites, such as nut crossings or tuner rows.
- **A design law:** a bounded pure `Function` evaluated during compilation, such
  as fret spacing. Functions are already compile-time; no `Comptime` keyword.
- **Irregular but aligned locations:** explicit keyed stations along a named
  direction, such as inlays. Do not force arbitrary stations into a fitted equation.

Use stable semantic keys to preserve identity when pitch or dimensions change.
Lift local Point2 coordinates through an explicit frame before spatial routing.

Bind routes to published terminal geometry. A string's nut crossing, tailpiece
seat, and tuner entry should share the product's placement references. Specify
diameter, bend radius, and the intended role of intermediate points. A bend's
virtual corner is not necessarily a point the realized wire passes through.
Use a guide's finite interval and traversal direction only in an admitted lane;
do not assume arbitrary spline following or obstacle autorouting from a simpler
point-route example. Check endpoint contact and bend feasibility in the result.

For this project's ordinary manually authored engineering stations, 0.1mm
resolution is the default convention unless the task states a tighter need.
Distinguish nominal rounding from a declared manufacturing tolerance. Preserve
derived curve controls, direction vectors, and evaluated math; do not globally
round compiler intermediates or change kernel/mesh tolerances.

## Stabilize geometry before presentation

Inspect a neutral shaded isometric plus orthographic side and rear views.
Look specifically for detached components, floating wire ends, implausible
thickness, abrupt caps, interferences, and missing connecting material. A valid
assembly can still be an unconvincing object. Tuner stems, tailpiece contact,
and the head/neck transition were small guitar edits with large visual impact.

Attach appearance to physical Material identity and override finish with `with`
where needed. A steel part remains steel when painted. Distinguish nominal
material names from certified properties. Downstream USD/Cycles shaders,
lighting, and cameras may make the object look expensive; they must consume
the compiled geometry and must not conceal or repair missing CAD geometry.
Disclose where procedural finish lives.

Localize a correction to the definition, layout, or interface that owns it.
Predict what should change before rebuilding: a placement edit should not
reshape unrelated definitions; an appearance edit should not deform meshes.
Inspect actual reuse/rebuild evidence instead of inferring cache success from
a fast render. Preserve ports and keyed identities during organizational edits.

## Verify through the real pipeline

Use Aetheris.CLI as inspection authority. Start with `aetheris --help` and the
relevant subcommand help; if absent from PATH, use the repository's dotnet CLI
path. Check the consuming root for imported modules: formatting a module is
not semantic validation, and source validation does not materialize geometry.

Scale verification to the claim:

- Source/semantic inspection: dependencies, shared definitions, expanded paths,
  interfaces, and resolved placements.
- Geometry: build the actual BRep, inspect topology and contact, and verify
  the relevant STEP export/reimport path.
- Presentation: inspect the actual exported scene and multiple views. Keep
  rendering edits separate from product geometry.
- Performance, when requested: state cold/warm/cache conditions; separate
  materialization, display preparation, export, startup, and render time. Check
  camera interaction in the actual viewer; do not infer no rebuild from cache reuse.

Run repository-required tests for compiler/kernel changes; use focused iteration
and the full gate for qualification. Do not invent a fresh test suite for a
cosmetic model edit. Keep generated artifacts in `artifacts/local/` by default.
Record source revision/input hashes so render and timing evidence match the model.

Close with artifact paths, exact versus simplified geometry, evidence, declared
tolerances, and limitations. Put author/date/version and design notes in authored
Record/PMI data when provenance is requested. A presentation witness is not a
manufacturing release; a speed comparison needs measurements of both products.

## Report tooling friction as an actionable compiler gap

Diagnose whether the problem is authored design, unsupported semantics, or a
defect in a supported operation. An unusual cap may be correct geometry with
bad design; a missing material is a different failure. Reduce the case before
choosing a remedy. Preserve the failed evidence.

For each real tooling gap, record:

```text
Desired modeling operation and why it belongs in the model:
Minimal source / consuming root, revision, CLI command and diagnostic:
Expected semantic result versus observed result:
Responsible layer: authoring/parser, binding/placement, AIR/kernel, export,
                   display, or inspection/source mapping:
Recommended permanent change, admitted scope, and explicit rejection boundary:
Acceptance test: motivating model, relevant invalid case, identity/provenance
                 and export behavior that must remain stable:
Temporary simplification, if used: artifact impact, disclosure, removal criterion:
```

If named ports are ignored, fix the binding/placement owner; do not teach
authors to copy world matrices. If a supported exact body displays incorrectly,
preserve the BRep and isolate the meshing problem; do not replace it with an
external mesh. If ordinary construction requires a Python source generator,
identify the missing authoring operation and recommend a compiler-owned form.
Read-only automation for invoking tools and collecting evidence remains useful.

A documented design simplification can be appropriate within the user's scope.
It must not silently claim the missing operation worked. Report concrete gaps
and recommended fixes in the modeling report without expanding into an unrequested
compiler project. When an authorized fix is needed, use its existing owner and
qualify the motivating case. Do not accumulate ad-hoc geometry compilers,
renderer-only repairs, or fallback logic that erases engineering intent.

## Read only the references needed for the current decision

All paths below are relative to the active Aetheris repository root. The reviewed
skill source is `docs/public/skills/firmament-modeling-strategy/SKILL.md`; an
installed copy is self-contained, and these checkout references are optional
further reading. Refresh that copy when updating the reviewed source.

- Strategy textbook: `docs/development/architecture/llm-cad-strategy/README.md`.
  Lessons 01/03 cover evidence and reconstruction; 05 covers dependency graphs;
  06/07/08 cover profiles, holes, and finish; 09 covers manufacturing assumptions
  and recognition versus clean redraw. Their proposal snippets are not syntax authority.
- Parts and components: `docs/public/firmament/functional-pickup.md`,
  `docs/public/firmament/assembly-components-and-sites.md`.
- Datums and ports: `docs/public/firmament/assembly-authoring-foundation.md`,
  `docs/public/firmament/datum-seating.md`,
  `docs/public/firmament/concept-axis-mating-proposal.md`,
  `docs/public/firmament/concept-derivation.md`.
- Shape authoring: `docs/public/firmament/profile-boundary-edits.md`,
  `docs/public/firmament/section-chains.md`.
- Series/routes: `docs/public/firmament/concept-points-and-functions.md`,
  `docs/public/firmament/wire-point-routes.md`,
  `docs/public/firmament/source-owned-guitar-authoring.md`.
- Presentation and evidence: `docs/public/firmament/materials.md`,
  `docs/public/firmament/pmi.md`, `docs/public/firmament/incremental-compilation.md`,
  `docs/public/firmament/language-style.md`.
- Concrete source to inspect, not a universal recipe:
  `fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm`.
