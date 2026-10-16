# V8 declarative assembly and motion implementation checkpoint

Date: 2026-10-16. Starting checkout: `9cf7b4af`.

## Verdict

**Meaningful progression.** Shared datum composition, articulated reusable
Subassemblies, moving exposed geometric ports and typed finite Motion clips now
run through the production compiler and occurrence evaluator. The complete V8
migration is not accepted: public external relationship roles, SliderCrank
closure and exact axial spring baking remain unimplemented. The V8 retains its
qualified mechanism adapter and authored rest configuration until those owners
are admitted together.

This is executable compiler work, not a shadow frontend. The approved broader
design is [the relations and motion proposal](../development/architecture/FIRMAMENT-V8-RELATIONS-AND-MOTION-PROPOSAL.md).

## Repairs and authority

- Derived assembly `DatumFrame` fields bind shared AST expressions and compose
  through the existing frame owner. `from`, `translateLocal` and `rotateLocal`
  eliminate authored basis constants for the V8's common bank/cam frames.
  Translation follows the referenced basis, independently of a sibling rotation.
  Existing Plane-relative `on`/`at`/`X` frames remain admitted.
- Reusable definitions retain their local joints alongside solved zero frames.
  Instancing rebases occurrence and semantic IDs; nested graphs are instantiated
  once, and each repeated occurrence owns independent realized Mate coordinates.
- Source mounting frames synthesized during local compilation remain available
  in the instantiated semantic tree. They are not dropped when solved placement
  replaces authored placement.
- One graph admission check covers joint drivers and rigid ancestry. Pose
  evaluation resolves both dependencies together rather than updating rigid
  descendants after all joints have already read their parents.
- Plain Placement retains a separate frame-dependency collection, consumed by
  the same pose evaluator and retained through instancing. A moving target
  therefore drives its relative placements, without exporting a fabricated
  physical Fixed joint.
- Exposed geometric semantics retain their actual internal occurrence. World
  queries and relationships from those ports follow its current pose. Fixed
  seating gap and clocking survive that rebinding.
- An internally moving public port cannot serve as the complete Subassembly's
  rigid installation mount. Both typed-Mate and Placement paths reject it with
  `assembly-kinematic-moving-mount-unsupported` instead of freezing its rest frame.
- Fixed local joints remain visible in IR. The route-binding guard now checks
  actual degrees of freedom, so retaining Fixed relationships does not
  incorrectly classify a static routed assembly as moving.
- Unsupported public Interface exports fail explicitly instead of being
  dropped by the legacy geometric exposure adapter. Realized Mate aliases are
  published explicitly and can be re-exported through nested Subassemblies. Unknown local datum sources
  also reject when the derived datum is unused.

`Time` joins the existing typed scalar and arithmetic owner. Seconds and
milliseconds normalize to seconds; existing enum ordinals remain unchanged.
Record-supplied durations use this binding path. Time cannot replace engineering
Length or Angle values.

`Motion`, `Track`, `Keyframe` and `Keyframes<Domain,Value>` bind the unified AST.
Domains are Time/Angle; values are Angle/Length. Curves admit explicit Linear,
Hold or Cosine interpolation. Angular values remain unwrapped. Periodic profiles
require explicit, matching endpoints; Loop clips also check physical pose
continuity. Input expressions reuse the existing typed expression evaluator.
Local templated Parts in Inventory modules now keep declaration identity separate
from invocation arguments, fixing doubled specializations during module binding.
The imported two-occurrence witness qualifies public channels and geometry sharing.

The compiler orders coordinate writers once; duplicate writers and dependency
cycles reject before sampling.

Motion targets resolve to actual Mate stable IDs, with qualified realized names
for repeated Subassemblies. Sampling is a pure time-to-state/pose query; it does
not rebuild BRep, mutate shared geometry or accumulate prior transforms. There
is no backend playback state machine to synchronize. Transport controls belong
to the consumer; adding an agent/state-machine dependency for the pure sampler
would add no authority.

## Consumers and diagnostics

`aetheris asm motion <file> --clip Cycle --time 1.25 --json` admits the real
assembly and samples its clip. Inspection includes compiled Motion metadata.
JSON diagnostics remain structured. Text diagnostics preserve file, source
extent, severity, code and explanation.

The existing web runtime retains the admitted Assembly IR per successful
session revision. The SDK's `model.sampleMotion` requests only occurrence
transforms and scalar joint state; meshes remain shared. Failed source rebuilds
retain the prior geometry and its matching graph. Disposal releases both.

Motion spans map through the existing syntax-origin and module-projection
owners. The SDK Assembly error projection preserves those file/span/line
locations instead of substituting the whole root document. Unknown provenance
is left unavailable rather than fabricated.

Motion sampling does not change the default engineering configuration or its
STEP export. This checkpoint does not advertise clip-to-STEP deformation baking,
USD clip timecode conversion, GLB animation baking or V8 browser migration.
Existing direct joint-state export APIs remain their current owners.

## Source witnesses

- `fixtures/Canonical/AssemblyInterfaces/derived-datum-frames.firmament`:
  a shared bank frame places a Cam at measurably oblique world coordinates.
- `articulated-subassemblies.firmament` in the same directory: repeated local
  joint graphs, a rigid intermediate node below a moving parent, and an external
  component following an exposed moving port.
- `articulated-motion.firmament`: a typed Record duration, unwrapped two-turn
  rotation, a dependent half-speed joint and a cosine lift profile.
- `fixtures/Regression/AssemblyMotion/slider-crank.pending.firmament`: an
  explicitly pending design witness, not a must-build regression. Current
  admission rejects its unimplemented Interface family.

Tests assert admission/rejection, actual world transforms, zero-pose parity,
shared definitions, arbitrary/reverse/non-key time queries, final endpoint
sampling, published-port behavior and source evidence. They do not only match
error sentences.

## Qualification

Evidence is generated under ignored `artifacts/local/`. Final results are
recorded below after qualification. No whole-V8 declarative-motion claim is
implied by successful rigid-joint clip qualification.

## Exact remaining ownership boundary

`AssemblyM0Parser.InstantiateAssemblyDefinition` still locally compiles a
reusable definition before its parent realizes it. Missing roles and unresolved
local placements therefore reject before a parent can supply an external
crankpin. The public exposure owner exports geometric semantics and dimensional
relations; it does not yet export a partially bound Interface contract with external
roles. Source-level aliases for already realized Mate channels are implemented.

The next implementation must retain that open, typed contract in the reusable
definition, instantiate its roles and driver identities per occurrence, then
complete it in the parent. Add SliderCrank as a bounded closure node using
`Core.Mechanisms.SliderCrank`, with actual port-derived geometry/domain checks.
Do not relax general cycle rejection or independently keyframe the piston.

`AssemblyM1Pipeline.CompileParsed` also currently materializes Part definitions
before engineering mate compilation. Exact default-pose spring baking needs a
bounded admission/materialization ordering that can derive its pitch first.

An axial spring also changes geometry, rather than just rigid occurrence pose.
Its selected/default engineering configuration must materialize the existing
WireForm with derived pitch. The already-compressed phase-zero coils cannot be
replaced by free coils or a scaled display mesh in STEP.

Once those owners exist, regroup piston/valve/crank-throw components, replace
CylinderRest and per-Part motion bindings, expand datum/Motion Patterns through
the existing finite owner, and qualify the baseline/+6mm-stroke sweeps and all
advertised exports. The current V8 source deliberately retains its working
mechanism adapter instead of presenting incomplete crank-only motion as a
complete engine.
