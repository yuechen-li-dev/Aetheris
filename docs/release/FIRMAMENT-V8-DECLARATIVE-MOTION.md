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
Ordinary validate/build and the SDK also classify an Assembly inherited through
include via that same module graph, rather than misrouting the clip as a Part.

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

Evidence is generated under ignored `artifacts/local/`. The build uses serial
MSBuild with node reuse and shared compilation disabled. Qualification covers
the real CLI, native session runtime, public browser SDK and compiled WASM.
No whole-V8 declarative-motion claim is implied by the rigid-joint clip witness.

- Release solution build: passed. Existing analyzer, trimming and WebAssembly
  interop warnings remain; they are not represented as a warning-free build.
- Core fast lane: 1,076 passed. Focused articulated-assembly and Motion tests:
  28 passed. SDK Node tests: 13 passed.
- Final complete solution gate, including Helios feedback: **5,198 passed across 20 test assemblies,
  zero failures**. The final focused integration rerun passed 50 tests,
  including the imported-root project and structural-editing cases.
- Native session and real browser WASM proofs passed at seven times, including
  reverse/non-key queries. All 1,232 compared matrix components matched exactly.
  Explicit endpoint sampling retained 720deg; shared definitions and the
  last-valid graph survived sampling and failed edits.
- Both include-root and Inventory-root Motion fixtures passed ordinary CLI
  validate and build. Validation reports `validationLevel: Built` and no errors;
  the builds produced STEP artifacts.
- Claude FrontendX4 corpus: 32/32 passed without changing cases or recording
  new expected geometry.
- Frozen STEP replay: 26/26 byte-identical artifacts. Scoped FrontendX2
  qualification: all nine expected validate/build outcomes held.
- Full editable V8 demo generation completed for Stages A/B/C, baseline and
  the +6mm stroke revision. Each final configuration checked 5,768 cylinder
  samples and 11,536 spring samples. Maximum rod-closure residual was
  `8.527e-14mm`; spring seat distance and drift were zero. This retains the
  existing bounded interference scope, not a general collision certificate.

The V8 comparison retains 171 physical Parts, all 21 definition STEP hashes
and occurrence identities. Maximum world-matrix and measured metric difference
is `2.842170943040401e-14`, below the existing `1e-9` qualification threshold.
No reference tolerance was widened. The resulting graph contains 193
occurrences, 140 retained mechanical relationships and 43 separate frame
dependencies; increased retained relationship counts are graph fidelity, not
new geometry.

Local evidence: `v8-relations-fast.log`, `v8-motion-focused.log`,
`v8-motion-sdk-tests.log`, `v8-relations-x4.log`, `v8-relations-demo.log`,
`v8-relations/v8-parity.json` and `v8-relations/parity/geometry-parity.json`,
all beneath `artifacts/local/`.

Final integration also tests a Motion document whose Assembly comes from an
include. Root classification and Assembly-profile admission resolve that
include through the existing module graph. The browser witness supplies real
project documents rather than concatenating their text. The concurrent
structural-editing update is retained, including its request fields alongside
the restored Motion query fields.
Its six Windows-only assertion failures came from a raw-string test specimen
inheriting CRLF while assertions require LF. The specimen now explicitly uses
LF, independent of checkout settings. Production editing and geometry logic
were not changed for those failures; the initial failing full-run log is retained
as `v8-relations-full-before-newline-fix.log`.
The final rebuilt native runtime and WASM harness passed after exposing raw
schema applicability. No task-owned dotnet, testhost or aetheris hosts remained
at closeout. Consolidated local evidence is
`artifacts/local/v8-relations/qualification.json`.

The code-quality review kept variant rules in the existing schema metadata,
source alias matching in its existing owner, and diagnostic presentation in the
compiler presenter. The only added regex vocabulary is Hole in the existing
bounded completion-context scanner; no execution parser route, global rewrite,
diagnostic-fatality allowlist or geometry workaround was added.

Reproduction commands (from the repository root):

```powershell
$env:MSBUILDDISABLENODEREUSE = '1'
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false --no-restore
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build -m:1 -nr:false --filter 'Category!=SlowCorpus'
dotnet test Aetheris.slnx -c Release --no-build -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1
dotnet build Aetheris.Web.Perf -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet Aetheris.Web.Perf/bin/Release/net10.0/Aetheris.Web.Perf.dll verify-motion fixtures/Canonical/AssemblyInterfaces/articulated-motion.firmament
node scripts/qualify-assembly-motion-browser.mts <absolute-path-to-playwright-index.mjs>
node --test Aetheris.Web.Runtime/sdk/tests/*.test.mjs
python fixtures/Regression/FrontendX4/run.py
dotnet demos/Aetheris.EditableV8/bin/Release/net10.0/Aetheris.EditableV8.dll artifacts/local/v8-relations/demo
```

## Helios authoring feedback follow-up

The follow-up screenshots identified three compiler/language-service defects.
Hole field metadata now carries variant applicability in the generated schema;
completion and semantic field projection use that same metadata. A Shaft hole
no longer advertises counterbore or countersink fields. Those fields remain
errors when authored on the wrong variant.
The raw SDK schema also exposes additive `variants` metadata. A Helios control
using that union schema must filter by the active variant, or request compiler
completion; the compiler does not mutate the union schema per document.

The parser failure producer no longer appends a locationless generic malformed
Model error when a precise error already explains rejection. Structural failures
without a more specific diagnostic retain the generic fallback.
The build failure adapter treats `parse-failed` as a fallback result status,
not an extra diagnostic alongside an explanation. Source lookup for legacy
unknown-field diagnostics uses the existing accepted camelCase/PascalCase
aliases and requires an actual field-name token. The browser Part path now
uses the compiler's shared presenter rather than flattening errors to
`ValidationFailed` with invented `1:1` locations.

An empty Modify in a primitive Model binds its target and becomes a no-op before
material feature lowering. Tests compare its STEP text exactly with unmodified
stock and retain rejection for an unresolved target. The specimens live in
`fixtures/Regression/LanguageService/`. No separate Helios validator or
renderer fix is introduced; the viewport error pictured is outside this compiler
qualification.

Focused qualification: 24 tests passed, including all three Hole variants and
the five new feedback regressions. The real public-SDK/WASM browser harness
also passed all three variants, field completion/projection agreement, the
precise wrong-field diagnostic, and empty-Modify compilation. It simultaneously
retained the seven-time Motion proof and its 1,232 exact native/WASM component
comparisons. Evidence: `helios-authoring-focused.log` and
`helios-authoring-browser.log` beneath `artifacts/local/`.
The full follow-up gate passed all 5,198 tests; the fast lane passed 1,076.
Ordinary CLI validate and build reject both wrong-variant fields at their
actual field spans, without the generic Model or parse-status errors. Empty
Modify validates at `Built` level and exports successfully. The STEP identity
test verifies that accepting this no-op does not alter its stock geometry.

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
