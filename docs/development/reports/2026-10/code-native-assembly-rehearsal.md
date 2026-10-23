# Code Native Design: local bracket assembly rehearsal

This records the **initial** SectionChain rehearsal. The [primitive composition and Revolve follow-up](code-native-assembly-primitives-and-revolve.md) supersedes its authoring choice and Revolve limitation. Assembly PMI noninheritance is intentional document ownership, not an exporter defect.

Date: 2026-10-23. Base: `5e4c90b3`, with the local changes in this pass. No demo-repository changes, push, showcase PR or hosted CI run are included.

## Demonstrated workflow

The [fixture README](../../../../fixtures/Regression/CodeNativeAssembly/README.md) contains the commands and scope. Firmament source declares a reusable L-bracket Template and a separate circular SectionChain fastener. A named `Interface<Fixed>` / `Mate InstallFastener` binds their exposed semantic seating frames. No hand-authored fastener Placement is present.

The nominal bracket is 96 × 60 × 12 mm with 20 mm legs, a Ø6 through bore, and a Ø12 × 90° countersink. Its hole center is (76, 10) mm. The separate fastener has a Ø5.7 shaft, Ø12 × 90° head, 16 mm overall length and **0.5 mm × 45° tip chamfer**. The flat tip diameter is 4.7 mm. It is intentionally threadless and has no drive recess; the artifact is a fastener blank, not a released screw specification.

The second source changes only the hole X coordinate from 76 to 60 mm. The fastener follows from translation (76, 10, -4) to (60, 10, -4) mm. Its occurrence identity and definition STEP hash remain unchanged. Both Mate position and angular residuals are zero for the nominal cases.

## Engineering checks

The tests inspect actual materialized circle, cone and cylinder supports rather than relying only on the Mate residual:

- Fastener stations are (radius, local Z) = (2.35, 0), (2.85, 0.5), (2.85, 12.85), (6, 16) mm.
- Both tip and head conical carriers have a 45° semi-angle. The tip chamfer is a real conical face, not a display bevel.
- The world-space head cone and host countersink share apex (76, 10, 6) mm. The head top is flush at Z=12 mm and the tip extends to Z=-4 mm.
- Nominal shaft radial clearance is 0.15 mm. Piecewise-linear radius checks at the critical axial stations verify nominal coaxial noninterference and the intended conical contact.
- Independent proposed ±0.1 mm shaft/bore size limits yield 0.1–0.5 mm diametral clearance. They do **not** qualify angular/location stacks or conical seating under manufacturing variation. Retention, strength and a driving feature are outside this rehearsal.
- The solid is enclosed, orientation-consistent and export-preflight valid. A refined numerical mass estimate is checked against the analytic cylinder-plus-frusta volume within its reported bound; numerical mass is not labeled exact.

The fastener has 14 faces, 28 edges and 16 vertices: eight conical quadrant faces, four cylindrical quadrant faces, and two planar caps. The bracket has 22 faces, 50 edges and 30 vertices. The assembly bounds are (0, 0, -4) through (96, 60, 12) mm.

Both cases export AP242 and GLB. The AP242 importer qualifies two geometry definitions and two occurrences. Importing to a local component package and reexporting retains component count, world transforms, topology and local/world bounds. The imported package retains interchange-owned placements; it does not reconstruct the source's semantic Mate.

The optional PNG renderer consumes the actual GLB node matrices and mesh data, checks each component's world bounds, and shows a seated top view, exploded view and fastener detail. Explosion is presentation only. It is not a drawing or geometry authority.

## Gaps found and local repairs

1. **Checked dimensions did not reach SectionChain consumers.** The SectionChain owner now shares the existing compile-time value binder with boundary, Static and Template expansion, projects bound nominal scalars into the existing consumers and masks only their checked declaration spans for legacy readers. Unsupported manufacturing-tolerance consumers still reject explicitly.
2. **A full Circle2 profile could not enter the SectionChain span adapter.** It now becomes four exact deterministic circular quadrant spans. Explicitly segmented existing profiles retain their identities. This is not polygonal approximation.
3. **Ruled analytic recognition handled full circles but missed corresponding circular arcs.** Matching phase/sweep, coaxial arc spans now reuse the existing analytic cylinder/cone recognizer. Original arc evaluators and trims remain authoritative. Different phase/sweep must not claim exact conical representation.

The first attempt reordered expansion too broadly and broke guitar profile recipes / named polygon sets. The full suite exposed this. That attempt was stopped and its failure log retained. The final ordering binds scalar values, resolves boundaries while named sets still exist, expands Static/Section values, then specializes Template profiles. Existing guitar section-family tests pass again. No test expectations or legacy fixture geometry were loosened to obtain that result.

## Explicit remaining limits

- The initial run rejected the solid axis-touching Revolve meridian with `firmament-revolve-axis-touch-materialization-not-supported`. The follow-up now admits that bounded flat-ended full-turn case; its preserved specimen has moved to `fixtures/Regression/CodeNativeAssembly/solid-fastener-revolve.firmament`. More general Revolve repair remains separate work.
- SectionChain's Concept plane resolver accepts field-shaped plane values (`tip: Plane { ... }`). A declaration-shaped `Plane tip { ... }` did not resolve through that consumer. The fixture and public SectionChain documentation use the admitted form; this pass does not claim global reconciliation of plane productions.
- Assembly AP242 export intentionally does **not** promote the bracket Template's part-level PMI into assembly requirements. An empty PMI set is correct because this assembly declares none. Assembly PMI must be explicit in the root assembly document; it is not inferred from parts or Mates.
- No new assembly manufacturing PDF was generated. The existing standalone bracket drawing belongs to the preceding single-part demo. A future showcase PR should retain it as a separate bracket artifact and distinguish it from assembly geometry.
- Qualification is local Windows/.NET evidence. Hosted Linux CI and independent third-party CAD import are not claimed here. The showcase must pin a published Aetheris commit containing these repairs, not the older demo pin or an uncommitted working tree.

## Evidence and gates

Generated evidence is ignored under `artifacts/local/code-native-assembly/`:

- `rehearsal/qualification.json`: CLI qualification and source/definition hashes.
- `rehearsal/assembly.step`, `assembly.glb`, and `relocated.*`: real exports.
- `rehearsal/*-analysis.json`, `*-roundtrip-analysis.json`, `*-roundtrip-inspection.json`: product structure and geometry metrics before/after import and reexport.
- `rehearsal/fastener-validation.json`: normal validation reports `Built`, `UnifiedSectionChain`, valid.
- `rehearsal/solid-revolve-rejection.json`: preserved truthful unsupported alternative.
- `rehearsal/preview.png`: actual GLB visual review.
- `solution-build.log`, `focused-tests.log`, `fast-tests.log`, `full-tests.log`: qualification gates.
- `full-tests-initial-ordering-regression.log`: rejected intermediate expansion ordering, not acceptance evidence.

Final gates:

| Gate | Result |
| --- | --- |
| Release solution build, serial, shared compilation disabled | Passed; 0 errors, 11 warnings |
| Focused fastener, SectionChain and guitar Section-family tests | 37 passed |
| Core fast lane (`Category!=SlowCorpus`) | 1,088 passed |
| Complete solution suite, serial, including SlowCorpus | **5,929 passed, 0 failed, 0 skipped** |
| Local CLI rehearsal, both source cases and import/reexport | Passed |
| GLB component bounds and rendered visual review | Passed |
| `git diff --check` | Passed |

The full suite includes 3,157 Firmament tests, 1,243 Core tests, 641 CLI tests and seven editable V8 tests. `Aetheris.FrictionLab.Tests` reports no discoverable tests; it is not counted as coverage. No .NET test, compiler or MSBuild hosts remained after qualification. The solution build warnings include existing WebAssembly SQLite varargs and compiler warnings; a zero-warning build is not claimed.

## Code review

Changes stay in the SectionChain consumer and existing ruled-surface recognition owner. No new parser route, regex dispatch, geometry fallback, solver, service or runtime state was added. Circle subdivision is analytic and deterministic. Scalar projection uses existing value substitution and AST declaration boundaries; unbound declarations remain subject to normal admission. Public SectionChain documentation describes the accepted syntax and tolerance boundary.

## Verdict

**Success for the local rehearsal.** The separate chamfered fastener is semantically placed, moving the hole carries it without manual bookkeeping, and the product-structured exports survive Aetheris import/reexport with the tested geometry and placement intact. Existing regression suites pass after restoring the correct expansion ordering. This is bounded corpus and artifact evidence, not a claim of byte-identical output for every possible legacy input.

The next showcase PR should use the follow-up's primitive Compose source after the Aetheris repairs have a published commit to pin. Keep the prior standalone bracket PDF and its drawing checks as separate artifacts; document explicit assembly PMI ownership. That PR and its hosted CI remain outside this local run.
