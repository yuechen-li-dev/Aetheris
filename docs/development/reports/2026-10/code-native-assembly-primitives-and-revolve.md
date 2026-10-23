# Code-native assembly: primitive construction and bounded solid Revolve

2026-10-23. Follow-up to the [initial rehearsal](code-native-assembly-rehearsal.md), on base `5e4c90b3` plus the local changes. No demo-repository push or PR is included.

## What changed

The showcase fastener now follows the direct construction strategy used for the McMaster bolt: compose named analytic masses, then finish an edge. [`fastener-library.firmament`](../../../../fixtures/Regression/CodeNativeAssembly/fastener-library.firmament) declares a reusable Record/Template with a Cylinder shaft, a Frustum attached to `shaft.Top`, and a 0.5 mm equal-distance Chamfer on `shaft.Bottom`. No authored Profile, Revolve or SectionChain is needed. The shared assembly still places the fastener through exposed Concept frames and the named Mate.

The nominal geometry is unchanged: Ø5.7 shaft, Ø12 × 90° head, 16 mm overall length, flat tip Ø4.7, 0.5 mm × 45° lead-in. The fastener has five analytic faces (two cones, one cylinder, two planes), seven edges and four vertices. The former four-quadrant SectionChain representation has fourteen faces; this is an intentional representation simplification, not a nominal shape change. The old source remains as `fastener-sections.firmament` for parity evidence.

The existing public Compose owner previously supported Profile operations only. It now binds a bounded Cylinder/Frustum production over the unified AST, reuses the existing primitive field binders and checked scalar values, and carries a typed admitted construction to execution. Spans are connected in order by explicit named top supports and equal joining radii. Terminal Cylinder chamfers rewrite only that span's end. The existing `RevolvedProfileStackEmitter` constructs the result directly; no source-to-source sketch rewrite, Boolean union or new whole-document parser route is involved. Validation remains `Built` / `UnifiedMechanical` through the normal CLI.

The same emitter closes the solid-axis Revolve gap for one safe family: full turns of flat-ended, strictly monotone piecewise-linear meridians with positive outer radii, one axis closure and two radial cap segments. The axis closure emits no material face. Caps and outer spans preserve source-semantic face associations. Both sweep signs and different authored world frames are tested. The former rejected source is now `fixtures/Regression/CodeNativeAssembly/solid-fastener-revolve.firmament` and builds a solid, not a hollow approximation.

## PMI ownership decision

Part PMI is **not inherited or promoted** by assembly export. This is intended document ownership, not a missing exporter behavior. Declare assembly-owned PMI explicitly in the root `.firmasm`; release metadata is likewise explicitly selected there. Mates express placement, not inferred manufacturing tolerances or a second constraint solver.

The public PMI and assembly guides, fixture README and initial report now say this accurately. Regression coverage verifies that the bracket's Part Datum and diameter are absent from assembly-owned PMI, while an explicitly authored assembly Note survives AP242 and leaves the geometry hash unchanged. The assembly's current admitted annotation forms are Notes and release metadata; this pass does not claim new assembly dimension/GD&T support or annotation reconstruction on package import.

## Qualification

The repeatable CLI rehearsal is `python fixtures/Regression/CodeNativeAssembly/rehearse.py --preview`; previews require numpy/matplotlib. Evidence is ignored under `artifacts/local/code-native-assembly/primitive-rehearsal/`, with build/test logs in its parent.

- Normal validate/build admits the primitive fastener through unified mechanical admission.
- Primitive Compose, solid Revolve and retained SectionChain satisfy the same independent checks of actual circular stations, cone angles, cylinder radii, enclosure, export preflight and bounded numerical mass against the analytic cylinder/frusta formula.
- The bracket's actual bore radius and countersink angle/apex are checked against the fastener's world carriers. Its head remains nominally flush and shaft clearance remains 0.3 mm diametral. Strength, retention and manufacturing angular/location stacks are not qualified; the model remains a threadless fastener blank without a drive recess.
- Moving the hole 16 mm in X moves the fastener through the Mate and retains its definition STEP hash. Both assemblies remain two-definition/two-occurrence AP242 and GLB products.
- STEP import/reexport qualifies topology, component counts, transforms and bounds. GLB preview checks the actual exported component bounds before drawing.
- Unsupported joining radii, placements, unknown fields, internal-rim finishes, oversized chamfers and Fillet reject in validate and build. Wrong dimensions and unsupported typed tolerances cannot disappear into nominal geometry. Unconsumed root material/PMI operations reject; ordinary `Require` remains owned by the shared compile-time binder.

| Gate | Result |
| --- | --- |
| Serial Release solution build, shared compilation disabled | Passed; 0 errors, 29 warnings |
| Focused composition, Revolve, SectionChain/guitar and PMI tests | 83 passed |
| Core fast lane (`Category!=SlowCorpus`) | 1,088 passed |
| Complete solution suite, serial, including SlowCorpus | **5,949 passed; 0 failed; 0 skipped** |
| Real CLI rehearsal and GLB visual review | Passed |
| `git diff --check` | Passed |

Full qualification includes 3,177 Firmament tests, 1,243 Core tests, 641 CLI tests and seven editable V8 tests. `Aetheris.FrictionLab.Tests` has no discoverable tests and is not counted as coverage. The warnings are existing compiler/analyzer/WebAssembly SQLite warnings; no zero-warning claim is made. No .NET test, compiler or MSBuild hosts remained after completion. Detailed logs are `revision-solution-build.log`, `revision-focused-tests.log`, `revision-fast-tests.log`, `revision-full-tests.log` and `revision-rehearsal-run.log` under the evidence parent directory.

## Boundaries and review

Coaxial Compose admits positive-radius Cylinder/Frustum spans along local +Z with matching shared rims. It rejects arbitrary placements, radial shoulders, mixed Profile operations, holes, internal-rim finishes and unsupported Part PMI/tolerance consumers. Solid-axis Revolve still rejects partial turns, pointed zero-radius tips, nonmonotone chains, radial shoulders and curved outer spans outside the existing sphere case. Inner loops and arbitrary Booleans remain outside this pass.

Code review found no new global diagnostic allowlist, whole-document regex dispatch, mutable parser state, solver or shadow execution path. Existing primitive field metadata and the existing analytic materializer remain the owners. The stack emitter's added face handles are metadata only for existing consumers. STEP preflight is enforced for the new Compose production. Public docs describe current admitted syntax and its limits.

The next showcase PR should use the primitive Template and pin a published Aetheris commit containing the repairs. Existing standalone bracket PDF and its drawing checks remain separate artifacts. This pass stays local.

## Verdict

**Success.** The motivating fastener is now authored as connected primitives and a real named chamfer, with preserved nominal geometry and semantic assembly placement. The bounded solid-axis Revolve case works through the existing analytic kernel path, and more complex cases remain explicit rejections. Assembly PMI stays explicitly owned by its assembly source. Full local regression and artifact qualification pass; no hosted CI or independent CAD qualification is claimed.
