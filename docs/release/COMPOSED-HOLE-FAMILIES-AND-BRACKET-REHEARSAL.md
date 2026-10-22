# Composed hole families and local bracket rehearsal

Date: 2026-10-21. Starting revision: `1933f805`.

## Scope and outcome

The bounded production path is a single XY profile-composed part with disjoint
normal through shaft, counterbore or countersink holes. Countersinks now lower to
exact conical walls in the existing section-stack planner. Counterbores and
countersinks resolve their actual local material support; another boss cannot
move their entries into empty space. Unsupported intent still rejects.

The local rehearsal builds two specifications of one L-bracket Template, using
two Rect2 guides, a six-edge trace loop and extrusion. It exports STEP AP242,
GLB and authored PDF/SVG/PPTX drawings. A PDF-only reader independently rebuilds
both parts using Path construction and compares their actual STEP geometry.
No demo repository was modified and no hosted Actions run was performed.

Final gate: **Success for the bounded composed-hole family and local rehearsal.**
General composed-body fillets and the other boundaries below are not claimed as
implemented.

## Reproductions and repairs

| Finding | Starting behavior | Repair / qualified behavior |
| --- | --- | --- |
| Countersink on traced L / Compose | `compose-hole-unsupported-variant-or-end` | Typed circular cavity, exact cone/cylinder walls and circular trims; no Boolean fallback. |
| Plain composed counterbore | Already worked on constant-height L stock | Retained; full STEP output matches the starting revision byte for byte on the existing witness. |
| Counterbore beside taller boss, A4 mounting block | Recess operations were placed at the body's global maximum Z, above their local plate. The successful STEP omitted both recesses. | Normalize actual host material before drilling; resolve a contiguous local interval and exposed entry footprint. A4 recesses now enter at Z=12, independently of the Z=18 boss. |
| Counterbore/countersink on pocket floor | No reliable local support admission | Fit the complete entry disk in the exposed floor. A small floor rejects; a sufficiently large floor admits. |
| Named base perimeter beside boss | Chamfer planner selected the globally highest slab | Resolve the named Profile's support. Preserve continuing boss material and subtract it from the exposed cap. Reject a corridor intersecting continuing material. |
| Missing materialized feature walls | Correspondence diagnostic could coexist with an emitted body | The feature owner now rejects a missing shaft, recess or slot wall immediately. |
| Nearby pockets, slots, cavities | Base-outline checks did not prove the actual cavity corridor | Reuse exact section disk containment throughout the cavity interval; reject collisions, tangencies, disconnected spans and unsupported boundary-distance proofs. |
| Wrong-variant relief fields | Compose owner could consume irrelevant fields | Use shared schema variant applicability; an inapplicable field cannot disappear. |
| Normal Compose export evidence | Result could omit its existing runtime body/correspondence | Retain both there, report actual surface counts and correct stack kind. |
| Shaft diameter PMI | A composed feature's larger recess/cone could join its diameter target | Follow semantic shaft-wall ownership, preserving all shaft patches and their associations. Contradictory values keep their targets for geometry/requirement diagnostics. |
| GLB PMI for partitioned shaft | Multi-face presentation rejected even one physical shaft | Admit a connected set on one analytic cylinder; highlight every patch and retain every face association. Different axes/supports still reject. |
| Semicolon PMI fields | Punctuation reached the line/regex value reader | Existing structural parser owns field spans; no new parser route. |
| `PlusMinus(0.1mm)` in direct PMI | Only the two-argument call was recognized | Structurally read one/two length arguments; retain finite, nonnegative, unit and arity checks. |
| Authored `Wireframe` | Accepted but mapped to `VisibleOnly` | Preserve the requested enum. Suppress analytic patch seams rather than manufacturing edges in drawing projection. |
| Six-face display qualification test | A time-bounded partial API sometimes returned five faces under load | Use complete deterministic tessellation for the complete-display assertion; partial-display policy is unchanged. |

## Geometry and ownership

The existing parser owner binds hole declarations into typed composition metadata.
The section-stack owner proves host/material admission, plans exact walls and
trims, publishes semantic descendants and materializes one closed body. The
materializer does not choose a second interpretation. The conical entry's extra
void volume is integrated analytically. Linear outer chamfer transport uses its
exact quadratic section-area integral, retaining disjoint cavity volumes.

No new whole-document dispatch, source macro system, field schema, diagnostic
allowlist or global mutable parser state was added. PMI field parsing removes an
old regex consumer in favor of existing token spans. New regex use is confined
to the independent PDF/STEP verification script, not the compiler.

The GLB change is presentation over AP242 associations. It does not move body
vertices. The authored drawing helper supplies this family's dimensions and
normal-hole hidden contours through the shared Drawing IR; PDF, SVG and PPTX
use the same sheet. The automatic drawing recognition family is unchanged.

## Compatibility and intentional correction

An isolated build of the starting revision's Firmament sources was compared
with the repaired implementation using the same unchanged dependencies.

These five complete STEP artifacts were byte-identical:

1. `profile-compose-l-bracket.firmament`.
2. `profile-compose-l-bracket-external-modify-counterbore.firmament`.
3. `profile-compose-reflex-chamfer-with-counterbore.firmament`.
4. The canonical standalone single-edge fillet.
5. The canonical primitive counterbore.

The A4 mounting block intentionally changes: its old cylinder inventory contained
only radius 3 mm; the corrected body also contains the requested radius 5.5 mm
recesses. Its named base chamfer now acts on that base's support. Preserving the
old omitted/wrongly placed features would preserve a defect, not source intent.
The new L countersink is rejected by the reference revision and accepted now.
No fixture was edited to turn a failing geometry comparison green.

## Rehearsal and drawing evidence

| Specification | Nominal | Resized variant |
| --- | ---: | ---: |
| Overall X/Y/Z, mm | 80 / 60 / 12 | 100 / 75 / 15 |
| Leg widths, mm | 20 / 20 | 25 / 24 |
| H1 X/Y from O, mm | 60 / 10 | 70 / 12 |
| Shaft / entry diameter, mm | 6 / 12 | 8 / 14 |
| Included cone angle | 90° | 90° |

Five controlling dimensions, marked O/X0/Y0, independent coordinate limits,
through diameter, countersink entry diameter/angle, entry plane A and hole-axis
orientation define the nominal part. Limits are ±0.1 mm and ±0.5°. The local
screen covers Cartesian/size limits and axis lean on ideal orthogonal stock;
machine/process capability, stock form and general angular stacks remain
unqualified. Checked/approved signoffs are blank.

For each final PDF, the independent reader freezes a fresh Path-based source
before reading any reference geometry. Both reconstructions match bounds,
topology counts, all topology vertices, analytic cylinder/cone supports and
the independently evaluated reference volume. Nine incomplete-document
mutations reject for each size: missing dimension, origin, coordinate limits,
hole row, countersink angle limits/value, through extent, axis orientation or
outline angle definition.

Exact construction volume and numerical trimmed-face mass estimates are reported
separately. A tessellation estimate is never labeled exact. The final PDFs were
rendered and visually inspected. AP242/GLB preserve supported datum and shaft-size
PMI; additional drawing requirements are not claimed as unsupported AP242 records.

## Qualification

Qualified on Windows with .NET SDK 10.0.401, through the real compiler/export path:

| Lane | Result |
| --- | --- |
| Release solution build, serial / no shared compilation | Pass, 0 errors |
| Focused composed-hole, support, drawing and round-trip tests | 91 passed |
| Fast Core lane (`Category!=SlowCorpus`) | 1,088 passed |
| Full solution lane, including SlowCorpus | 5,914 passed, 20 test assemblies, 0 failures, 0 skipped |
| STEP compatibility controls | Five byte-identical outputs |
| Nominal + resized PDF-only reconstruction | Both pass; nine negative mutations each |
| Final PDF rendering and inspection | Both sheets inspected |
| Code-quality / whitespace review | Complete; `git diff --check` passes |

The final code/test/fixture/runner hashes are recorded with the ignored evidence.
The first full run exposed the real A4 defect and the timing-sensitive display
assertion; both were repaired and the full gate was rerun successfully. No
geometry baseline was rewritten. The final complete rerun also covers the last
chamfer-volume and authored hidden-contour changes.

Builds use `-m:1 -nr:false -p:UseSharedCompilation=false`; test lanes run serially.
Task-owned reusable MSBuild hosts from an early launcher invocation were reaped.
Normal qualification hosts exit when their lanes finish; no background service
or startup hook was created.

Raw evidence defaults to ignored `artifacts/local/compose-hole-rehearsal/`;
final user artifacts are under `artifacts/local/demos/code-native-bracket/`.
Automatic approval review blocked recursive deletion of the private comparison
builds/cache, stating only “blocked by policy.” Those approximately 230 MiB remain
local. `artifacts/local/compose-hole-rehearsal/Cleanup-ComparisonBuilds.ps1` is a
reviewable cleanup script; its default invocation only previews verified paths.
STEP/hash evidence and deliverables are outside its cleanup targets.

Reproduction commands and the runner are documented in
[the public rehearsal guide](../public/code-native-bracket.md).

## Explicit remaining boundaries

- Composed-body fillets still require an adapter that retains existing material
  intervals, caps and cavities. The standalone Profile fillet planner constructs
  a fresh extrusion; removing its Compose guard would lose requested geometry.
  The rejection now explains this precise coupling. Existing primitive and
  standalone Profile fillet capabilities remain unchanged.
- Oriented Compose hole supports, blind counterbore/countersink ends, entry
  breakouts/tangencies and intersecting cavities are not admitted here.
- A perimeter-chamfer corridor cannot cross a shaft/recess or shaft/cone station.
- Exact disk containment remains bounded to line/circular-arc boundaries; an
  unavailable distance proof rejects instead of treating NaN as “inside.”
- Compose retains its existing expansion/source-span representation; this pass
  does not claim a new general expansion source map or complete PMI vocabulary.
- Generic automatic drawings and general surface-silhouette extraction are not
  expanded. This is a qualified authored bracket drawing, not a claim that all
  imported parts can be fully dimensioned automatically.
- Hosted GitHub Actions qualification is the next deployment/rehearsal boundary.
  The target demo repository has deliberately remained untouched.
