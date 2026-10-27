# Engineering edge finishes and Helios previews

2026-10-26. Qualification evidence: `artifacts/local/rim-finish/` in Aetheris;
`artifacts/local/rim-*` and `artifacts/local/graph-authoring/` in HeliosCAD.

## What changed

- Box whole-face Chamfer/Fillet now admits all six principal faces. Bottom and
  side plans reuse the qualified +Z construction under proper rigid signed
  permutations. Existing +Z constructions retain their original path.
- A native-XY, constant-stock Profile can retain disjoint through shaft,
  counterbore and countersink cavities while finishing either cap. The adapter
  retains the source-planned cavity graph and analytic surfaces; it does not
  Boolean-reconstruct the body or replace it with plain stock. Entry footprints
  must clear the entire rim corridor. Explicit planar-cap loop roles are emitted
  by the section-stack owner and retained by its materializer.
- `Boundary` binds to the unique outer stock Profile, including rounded stock and
  an L-bracket. It does not override an authored Selection named Boundary.
  Top/Bottom are local cap names; signed world axes must align with the actual
  extrusion normal. Generic EdgeFinish spelling and field aliases share binding.
- Removed two stale parser checks that classified non-four-segment Profiles as
  syntax failures. Geometry admission stays in the owning planner/materializer.
  Open-chain collision checks and their existing rejection diagnostics remain.
- Stable cavity identities, correspondence, original source spans and explicit
  AP242 PMI survive the retained-cavity operation. The new failure path supplies
  an enclosing EdgeFinish span; presentation can locate a named colliding Hole
  more precisely.
- The shared display tessellator now clips cylindrical rolls at their elliptical
  miter boundaries, including bodies that do not yet carry pcurves. Previously
  their rectangular UV envelope produced corner spikes. Periodic UV unwrapping
  uses the existing trimmed-surface owner. Qualification also exposed a fixed
  45-degree ellipse sampler that ignored caller tolerances. Ellipse sampling
  now honors angular/chord and segment budgets, with major radius bounding sag
  and axis ratio bounding tangent turning. Existing numerical volume checks
  retain their thresholds. This changes display tessellation,
  not exact BRep, STEP supports or authored dimensions.
- Helios rim previews retain the accepted solid and camera and overlay gold
  candidate boundary polylines. The guides reuse the Concept wireframe style in
  WebGPU and WebGL, are not selectable, and disappear on Cancel/Apply. They are
  projected BRep edges, not fabricated Concept declarations. Other feature
  previews retain their existing behavior. Palette descriptions now say face
  rim, and the build timer resets at the beginning of each build.

## Reproduction and regression evidence

Plain rounded Profile top/bottom finishes already worked. The failures were the
native Boundary selector, composed cavities, and Box face restrictions. These
were not evidence that a general BRep blend engine already existed.

`EngineeringRimFinishTests` exercises twelve Box face/kind combinations, the four
cap/kind combinations with all three through-hole families and explicit PMI,
invalid/interfering requests, and the actual live Codex source. It checks the
selected cap's inset, bounds, exact retained cavity surfaces, stable semantic
identities, manifold/preflight admission, orientation, analytic rim-volume loss,
STEP round-trip and validate/build agreement. Five display witnesses failed
before the elliptical trim fix and pass afterward, on both runtime and imported
BReps. They check that display vertices on the finished cap do not extend to the
old sharp corners; a topology count alone would not detect this defect.

The old invalid L-bracket specimen was preserved verbatim and moved to
`fixtures/Regression/EdgeFinishRobustness/l-bracket-host-boundary-chamfer.firmament`.
Its test now proves the admitted chamfer's volume. The old CLI -Z rejection case
now rejects an oversized finish instead; valid bottom finishes are covered by
the face matrix. Historical closeout manifests retain their original paths and
outcomes as historical evidence.

Live desktop dogfood used Helios in Edge and the installed Codex app-server
connection with its ChatGPT subscription. The real task produced a 40 × 30 × 8 mm
plate with R3 vertical corners, four Ø4 through holes at (±14, ±9), and an R1 top
rim. Helios applied it, offered Undo AI changes, and exported STEP. Saved source
compiled independently through CLI to the identical STEP bytes:
`724A426FDC653387D72BA436991E62CB3C21BBECC22D010D6D5585EC32CC4570`.
Inspection found one enclosed shell, 34 faces, bounds [-20,-15,0]–[20,15,8],
24 cylindrical and four toroidal surfaces, and no spline surfaces. The actual
agent source is retained as `helios-codex-rounded-plate.firmament` in that same
regression directory. Undo restored the Box. A second real task was cancelled;
its candidate was not applied and its Codex child process exited. Ordinary graph
controls also previewed and applied the bottom fillet without editing source.
Claude and Antigravity live clients were not qualified in this pass.

## Qualification

Geometry-only qualification passed 6,138 tests with two existing drawing skips.
After the display fix, final qualification passed **6,143 tests across 21 test
projects**, with the same two skips and no failures. The fast Core lane passed
1,127 tests. Build succeeded; existing compiler/analyzer warnings remain.

- Focused finish/countersink qualification: 41 passed, including exact support,
  volume, trim-containment and ellipse chord-budget checks.
- Helios: 192 unit tests; eight graph browser workflows; six author transaction
  browser workflows. The graph witness covers gold preview, unchanged source,
  Cancel disposal and right-face Apply/STEP export. Author client protocol
  fixtures are explicitly separate from the real Codex subscription run above.
- Shared Telos: 20 tests, including non-pickable gold operation guides,
  occurrence transforms, material/source immutability and disposal.
- SDK development WASM and Helios frontend builds passed. No production AOT
  bundle or release publication was performed in this pass.

The final frozen CLI under `candidate-qualified-cli` processed all **380
canonical sources**, retaining **322 validation acceptances, 297 STEP builds
and all 297 STEP hashes**, with **zero differences** from the pre-pass corpus.
Results are under `corpus-qualified`; `qualification.json` consolidates the
gate totals. Expected standalone/module/domain export refusals count as
outcomes, not broken parts that were made to appear successful.

The final manual desktop recheck of the updated gold preview could not be
completed: Computer Use stopped because it could not confidently determine the
current browser URL. No alternate UI automation was used to bypass that stop.
The automated gold-preview browser witness and its screenshot remain available.
Earlier live Codex, export, undo, cancellation and bottom-face graph operations
were observed successfully before that stop. All owned preview/agent hosts were
stopped. Concurrent Leviathan source edits were left untouched.

Automatic approval review refused deletion of redundant ignored build snapshots.
`artifacts/local/rim-finish/cleanup-redundant-snapshots.ps1` is a syntax-checked,
inspection-by-default script for the user to review and run; it archives summary
JSON and excludes the final snapshot/corpus and original baseline. It has not
been executed. No user files outside the workspace were removed.

## Deliberate bounds

This is not general edge blending. Multiple sequential Profile finishes remain
rejected. The new cavity-retention adapter rejects non-native frames, inner
profile loops, blind holes, slots and additional material operations. Existing
qualified Compose top-chamfer histories keep their older, broader interval
planner; open-chain composed fillets still reject after collision diagnosis.
Combined primitive Box holes/rims remain +Z-only. These boundaries are documented
in the public fillet guide; no intent is omitted to get plausible geometry.

No new whole-document parser route, source rewrite, diagnostic allowlist,
arbitrary Boolean surgery or freeform blend fallback was introduced. Existing
Judgment-based profile planners still select qualified analytic constructions;
rigid frame mapping and cavity retention are deterministic adapters, not new
competing geometry engines.

## Verdict

**Success** for the bounded engineering-finish and gold-preview paths qualified
above. The motivating rounded plate retains its holes and rim in the real
Helios/Codex build/export workflow; bottom and side finishes are admitted and
trimmed correctly in the qualified matrix. Existing canonical STEP geometry is
unchanged. The final manual desktop recheck is explicitly unverified because of
the computer-use guard; automated browser coverage and its captured gold-preview
image are available. This is not release-package or arbitrary-blend qualification.
