# Single-cut carved electric guitar

The current source also qualifies [Concept-directed Fixed seating](../firmament/datum-seating.md):
the planar body crown, pickups and bridge conform to one authored 53mm hardware deck.

**Verdict: accepted for GUITAR-SURFACING-X0.** This is good enough as a presentation/dunk witness: a coherent surfaced guitar built from code, exact STEP export, a clean compiled USD display path and slide-ready Cycles images. The result demonstrates Aetheris's pipeline; the comparison does not claim a measured speed ratio against Zoo.

GUITAR-SURFACING-X0 is an original single-cut guitar presentation witness inspired by the Les Paul family. The supplied photographs guided proportions and finish; no photograph, logo, downloaded guitar model, or external product mesh is bundled. Aetheris constructs every guitar part. Blender imports the actual OpenUSD export and adds downstream appearance, studio lighting, floor, and camera.

The [authored assembly](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm) has a cubic single-cut outline, mahogany back, cream edge band, a five-section carved maple top with a 13 mm rise, a six-section D-profile G1 neck, tapered fretboard, tilted headstock, 22 frets, inlays, two pickups, bridge, stop tailpiece, four controls, selector, tuner placeholders, and six continuous formed strings. See [the multi-file subassembly layout](guitar-subassemblies.md) and [functional pickup construction](../firmament/functional-pickup.md) for the current source. Pickup consolidation changes current counts to 91 visible parts and 53 definitions; performance and render evidence below describe the earlier witness snapshots.

## Reproduce

From the repository root, using .NET 10 selected by `global.json`:

```powershell
pwsh -File scripts/qualify-guitar-x0.ps1 -Render -Viewport
```

`-Render` requires Blender with Cycles; `-Blender` supplies another executable. `-Viewport` uses the locally installed OpenUSD 25.08 SDK from the existing USD qualification workflow; `-UsdRoot` supplies its directory. Neither dependency is installed by this script. Without those flags, it builds, benchmarks, exports geometry and checks display mesh closure. Output defaults to ignored `artifacts/local/guitar-x0/`.

The fixtures are ordinary authored Firmament. `python scripts/create-guitar-x0.py`
is now a read-only CLI reproduction harness: it exports artifacts and verifies
that source hashes did not change. Body/neck Profile recipes, keyed sections,
endpoint bindings and physical-material appearance defaults are source-owned.
Root-aware `aetheris build` and `inspect` delegate to the existing geometry and
assembly owners; `asm export-usd` remains the presentation export command. See
[the qualified owl authoring slice](../firmament/source-owned-guitar-authoring.md).

For source changes, run both test lanes after the solution build:

```powershell
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
```

## Authority and deliberate simplifications

The silhouette consists of authored polynomial cubic spans. Back, edge band and carved top are closed exact BReps with planar caps and non-rational polynomial surfaces, built by the existing G0/G1 SectionChain materializer. The top uses matching smaller outlines at heights 40, 43, 48, 52 and 53 mm. Its small final crown is planar and meets the surface at G0; this is a practical crowned top, without a G2 or luthier-perfect curvature claim. Adjacent transition surfaces use the existing G1 policy and Judgment Engine qualification.

The guitar is a placed assembly of separate exact parts, rather than a fused manufacturing body. There is no routed pickup/control cavity, truss rod, fret-slot cut, neck-angle setup, or joinery qualification. The back covers indicate a control layout. The pickup cream seats are solid decorative plates beneath the bobbins. Tuners, knobs, bridge, saddles and inlays are simplified exact geometry. Strings reuse WireForm Straight/Bend/Straight with exact cylinders and tori, a 3 mm nut-break radius and varying diameters; they do not simulate vibration/tension or model wound-string microgeometry and post wraps. The declared stainless catalog material is a geometric-demo stock choice, not a claim about commercial guitar alloys.

USD contains the production display mesh, shared definitions, occurrence placement and simple preview colors. The full edge-distance cherry sunburst, mild procedural grain and glossy lacquer live in `scripts/render-guitar-x0.py` and the saved `guitar-studio.blend`. Color changes do not replace or deform product geometry. STEP carries geometry; neither STEP nor the simple USD preview claims to carry the Cycles procedural shader.

## Artifacts and evidence

| Output under `artifacts/local/guitar-x0/` | Purpose |
| --- | --- |
| `guitar.step` | Exact AP242 assembly, with shared part definitions |
| `carved-maple.step`, `carve-build.json` | Standalone G1 top, continuity/pcurve/topology and STEP reimport evidence |
| `guitar.usda` | Real Aetheris production display export |
| `neck.step`, `neck-build.json` | Standalone closed neck, G1 qualification and STEP reimport evidence |
| `neck-side.png`, `neck-back.png` | Side and rear stabilization views of the actual exported neck |
| `guitar-studio.blend` | Portable Cycles scene with procedural finish and studio |
| `hero.png`, `carve-detail.png`, `shaded-isometric.png` | Full guitar beauty, carved-top detail and shaded isometric |
| `display.json`, `mesh-check.json` | Exported geometry evidence and shared-edge closure checks |
| `string-low-e.step`, `string-build.json` | Ordinary WireForm CLI construction and STEP reimport evidence |
| `timings.json` | Cold first compile and warm same-process recompile, separately measured display preparation and serialization |
| `viewport/viewer.json`, `viewport/orbit-*.png` | Real usdview/Hydra camera orbit and unchanged-geometry evidence |
| `render.json`, `usd-validation.json` | Renderer metadata and independent OpenUSD verification |

The benchmark excludes `dotnet build` and process startup. Cold includes JIT and first-use initialization; warm recompiles the same source without a persistent model cache. Camera orbit is measured in standalone usdview, whose compiled USD scene has no Firmament compiler connection. The evidence compares product geometry hashes before/after 36 rotations and records stage-change notices. It establishes the downstream compiled-display path, without claiming that the Cadmata browser was tested in this milestone.

This witness can support a visual comparison with another guitar demo. It does not establish a numerical speedup over Zoo: Zoo's example was not run or benchmarked here. The next useful refinement would be more sculpted tuner buttons and a shaped pickguard, rather than a new geometry engine.

## X0 measurements

Measured locally on 2026-09-30 with .NET SDK 10.0.401, OpenUSD 25.08, Blender 5.2.2 LTS and an RTX 3070. The source compiles to 55 shared definitions and 107 guitar part occurrences. The display export contains 114,040 triangles across shared definitions.

| Stage | Cold | Warm |
| --- | ---: | ---: |
| Compile exact model | 7.474 s | 4.020 s |
| Prepare display meshes | 0.450 s | 0.344 s |
| Serialize USDA | 0.098 s | 0.060 s |
| Combined in-process pipeline | 8.022 s | 4.424 s |

Independent OpenUSD opening took 0.284 s. All 55 display definitions passed closed-edge validation, and the four surfaced parts plus six strings also passed outward-normal checks. Aetheris/USD world-transform disagreement was below `7e-16`, and world-bounds disagreement was below `0.000004 mm`. The real Storm/Hydra viewport completed 36 orbit steps at a mean 14.70 ms including framebuffer readback (maximum 22.98 ms). Product geometry hashes remained equal and there were zero stage-change notices. These measurements are a named local witness, not a hardware-independent performance guarantee.

The standalone maple top exports 74 faces, 162 edges, 90 vertices and 324 pcurves. Its independently checked pcurve reconstruction deviation is below `0.00000001 mm`, and STEP reimport succeeds. The source keeps G1 qualification distinct from sampled curvature observations; neither the planar crown nor the entire guitar is advertised as G2.

Final Cycles renders use GPU/OptiX, 64 samples and denoising. The 1800×2200 hero took 24.62 s, the 2200×1600 carved-top detail 16.86 s, and the 1800×2200 isometric 23.23 s. Blender USD import took 0.130 s. The saved report checks that shading and camera changes preserved imported product topology, and records the exact input USD SHA-256.

The work reused existing geometry paths and removed three concrete display/validation gaps: concave cap triangulation now retains shared spline boundaries; adjacent WireForm clearance now samples straight intervals consistently; swept-torus chart winding now agrees with the exact support normal before applying BRep orientation. The assembly routes the analytic formed-wire family through its existing shared-boundary SurfaceMeshIR mesher. No new surfacing kernel or string solver was added.

One full test run during Cycles rendering hit the existing bounded display timeout in the imported-slider CLI test at 6.108 s. Its isolated no-render run passed in 0.326 s; the later serial lane passed without increasing budgets or changing the test. The failed-run log is retained as `full-tests.log` under local output.

Final validation: solution Release build succeeded with zero errors; Core fast lane passed 1,005 tests; the focused guitar/WireForm/USD lane passed 36 tests; the final serial full solution lane passed all 4,012 tests across 20 projects with zero failures and zero skips. OpenUSD `usdchecker` and independent instance/transform/bounds/mesh validation succeeded. Repository layout and diff whitespace checks passed. The exact final commands and logs are under local output as `build.log`, `fast-tests.log`, `focused-tests.log`, `full-tests-final.log`, and `test-summary.json`.

## Neck stabilization

The side/rear review exposed a shape defect: the original rounded rectangular neck ended in a deep flat cap behind the headstock. It was already a closed solid, rather than an accidentally open surface or a missing material. The replacement uses the existing SectionChain path with six corresponding D-shaped cubic profiles. A flat seat stays at z=55 mm beneath the existing fretboard; the rounded back transitions from a deeper body heel through a straight lengthwise shaft tapering from 13 mm to a 7 mm nut section, whose back reaches the headstock base at z=48 mm. Width follows the existing fretboard taper. The headstock and all other product parts retain their authored construction and placement.

This is a deliberately simplified solid with sharp seat edges hidden by the fretboard and a G0 capped interface into the separate headstock. It does not claim a fused G2 neck/headstock transition or manufacturing joinery. No kernel changes or new Firmament features were needed for this stabilization. The neck contains 17 exact faces (15 spline surfaces and two planar caps), with zero faceted fallback; CLI validation and STEP reimport pass. Pcurve reconstruction deviation is below 0.000000006 mm. Regression coverage checks the closed outward-oriented mesh, flat fretboard clearance, nut attachment bounds and body heel presence.

Dedicated side and back Cycles views expose these previously hidden areas. The rear view hides the presentation floor and uses a rear studio light; neither change modifies product geometry. Updated timings and orbit evidence above refer to the stabilized source. Current render durations and input hashes are in `render.json`.

Stabilization validation: Release solution build passed with zero errors; the focused neck regression passed; the Core fast lane passed 1,005 tests; the serial full solution lane passed all 4,012 tests across 20 projects with no failures or skips. Logs are `stabilization-build.log`, `stabilization-focused.log`, `stabilization-fast.log`, `stabilization-full.log`, and `stabilization-test-summary.json`. All five final Cycles images match the final USD input hash, and product topology remains unchanged by shading.

### Straight shaft refinement

The follow-up side review requested a flatter lengthwise back. Shaft stations now lie on one straight taper from back z=42 mm at y=240 mm to z=48 mm at the nut (y=660 mm). An extra station before the shaft isolates the heel fairing from the shaft tangent; the rounded D cross-section remains. This removes the visible longitudinal belly without changing the fretboard, headstock or other product parts. A regression checks that shaft samples never protrude behind the straight taper and that centerline samples remain on the back or flat seat. The rear studio light is enabled only for neck inspection views, preserving the front studio lighting.

Straight shaft validation: Release solution build and focused geometry regression passed; Core fast lane passed 1,005 tests; serial full solution lane passed 4,012 tests with zero failures or skips. STEP reimport, OpenUSD validation, closed outward-oriented meshes and unchanged viewport geometry pass. Logs use the `straight-neck-` prefix under local output; `straight-neck-test-summary.json` records the totals.

## Source-authoring owl closeout, 2026-10-01

The [updated checklist](../firmament/authoring-composition-proposal.md) and
[implemented syntax](../firmament/source-owned-guitar-authoring.md) record this slice.
Profile/section recipes are source-owned; fixed public endpoints drive strings;
physical Material supplies Appearance defaults with explicit with overrides.
Rendering consumes authored identities instead of matching occurrence names.

Release solution build passed. Focused qualification passed 95 Firmament and 27
CLI tests; fast Core passed 1,004. The full serial gate passed 4,225 tests with
zero failures and seven existing skips across 21 projects. Cold compilation was
11.105s, warm uncached 5.108s, and retained-session 0.887s with all 53 definitions
reused. Display preparation was 0.218–0.398s; these local measurements include
first-use JIT in cold, exclude process startup/build, and are not a Zoo speed ratio.
Current cached/uncached USD serializations match.

Fresh outputs are under ignored artifacts/local/owl: guitar.step, guitar.usda,
report.md/report.json, inspect.json, timings.json, display.json and render/.
Six full-resolution Cycles images and guitar-studio.blend use the final USD hash,
with 91 imported meshes, nine pearl assignments and two pickup treatments;
shading/camera changes preserve product topology. Blender import took 0.198s;
hero took 42.79s and shaded isometric 41.72s at 1800x2200, GPU/OptiX, 64 samples.
Earlier native Storm orbit measurements above remain historical; this slice
qualifies the compiled Cycles scene and does not claim a refreshed native orbit run.

## Connection-detail polish, 2026-10-01

The reusable tuner now includes one lateral steel stem joining its button to the
headstock, automatically shared by all six patterned occurrences. An exact
semicircular rail sits on the tailpiece's top face. Its crown supplies the public
string datum at z=65mm, replacing the previous 5mm free offset above the flat
tailpiece. The six existing string routes retain their actual geometry.

The neck uses ten G1 sections, with the existing straight shaft taper isolated
from the short headstock transition. Its D-shaped cross-section is split exactly
into six corresponding spans, then blends into the rectangular headstock root.
The slab begins 16mm behind its original base datum; the transition's terminal cap
is buried inside it at 18mm. A small solid scarf under the veneer closes the front
of the angled join. This is a presentation assembly of separate exact solids;
it does not claim fused joinery, a continuous G2 interface to the slab, tuner
internals, string holes or mechanically functional string retention.

The control arithmetic needed for splitting the cubic profile exposed a narrow
authoring omission: Point2 controls accepted only literals after Template
substitution. The profile binder now delegates finite Length arithmetic to the
existing bounded scalar evaluator. The regression fixture checks real exact
extrusion and rejects wrong units and nonfinite coordinates. No new compiler or
geometry engine was introduced.

Compared with the preceding owl export, all 91 existing part placements remain
unchanged and 89 keep identical display geometry within 1e-9. Only the neck and
headstock solid change; eight added occurrences provide six stems, the rail and
the scarf. The result has 99 visible parts and 56 shared geometry definitions.
Reproduction, comparison, mesh checks and fresh presentation outputs live under
ignored `artifacts/local/guitar-polish/`. The renderer adds front, rear and side
headstock close-ups so the three repairs can be inspected directly.

The standalone neck CLI build and STEP reimport pass: one closed shell, 56 exact
faces (54 polynomial spline faces and two planar caps), zero rational product
surfaces and zero faceted fallback. Pcurve reconstruction deviation is below
0.000000007mm; loop closure and orientation checks pass. The admitted triangle
proxy self-intersection check passes; it is validation evidence, not a global
analytic intersection proof.

Fresh local benchmark: cold compile 12.848s, warm uncached compile 6.324s,
retained-session compile 0.993s with all 56 definitions reused. Mesh preparation
takes 0.224–0.437s and USD serialization 0.075–0.109s. All three serialized scenes
match. As before, cold includes first-use JIT, process startup/build are excluded,
and the measurements do not establish a numerical speedup over Zoo.

Polish validation: Release solution build passes with zero errors and four
existing WASM interop warnings. Core fast lane passes 1,004 tests. Final full serial
gate passes 4,229 tests with zero failures and seven existing local-data skips
across 21 projects. The first full run caught three stale fixture assertions for
the section/span layout and shared-definition reuse count; their updated checks
pass in the final run. Both logs and all TRX results remain in local output.

Nine full-resolution Cycles views and the saved studio scene use the final USD
hash. The 1800x2200 hero takes 11.81s and isometric 11.21s at 64 GPU/OptiX samples;
USD import takes 0.142s. All 99 product meshes survive shading and camera changes
without topology edits or compiler invocation. Closed-edge/outward-normal checks
pass for the four surfaced parts, six strings, pickup and three new shared detail
definitions. `report.md`/`report.json` link source, STEP, USD, Blender scene,
images, timings, comparisons and test evidence. **This is ready as a presentation
witness**; hardware internals and fused manufacturing joinery remain simplified.

## Authored provenance and PMI, 2026-10-01

The composition root now declares a typed `GuitarReleaseInfo` Record and immutable
`GuitarRelease` value, selected by `Provenance: GuitarRelease;`. Version **0.1.0**
credits **GPT 6.1 Sol Codex**, explicitly identifies the creator as an AI agent
working under human direction, and records the release date, milestone, nominal
units, authoring tolerance, and presentation-only status.

Eight named product notes cover design intent, the 0.1 mm authoring convention,
carved body, neck/head joint, electronics, bridge, strings/tuners, and finish.
Together with ten release fields they export as **18 semantic AP242 annotations**.
The STEP header also carries the author, date, description, and version. These
notes are design information; they do not claim measured acceptance, newly
qualified GD&T, or a manufactured instrument. The authored release date becomes
a fixed midnight timestamp, independent of when the exporter runs.

Reinspect with the assembly-aware route:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm --output artifacts/local/guitar-provenance/guitar.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll analyze assembly artifacts/local/guitar-provenance/guitar.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm export-usd fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm artifacts/local/guitar-provenance/guitar.usda --evidence artifacts/local/guitar-provenance/display.json --json
```

The real STEP reinspection recovers all 18 annotations with their intended
occurrence targets. All existing STEP geometry/product/placement entities are
byte-identical to the connection-polish baseline; only the header and 36 note
entities are added. The 99 body occurrences and 56 shared geometry definitions
are preserved. USD carries the same release fields and notes as custom
attributes, with unchanged meshes and transforms; the existing beauty images
continue to represent the current geometry. Metadata edits reuse geometry, as
qualified through a retained compilation session regression.

Current outputs and detailed evidence live in `artifacts/local/guitar-provenance/`.
Provenance closeout: Release build and repository layout/diff checks pass; the
Core fast lane passes 1,004 tests and the full serial gate passes **4,243 tests**
with zero failures and seven existing local-data skips across 21 projects.
See [assembly PMI syntax and limits](../firmament/pmi.md#assembly-release-records-and-design-notes).
