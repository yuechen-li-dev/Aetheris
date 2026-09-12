# LANG-BURN-X1 — Firmament language burn-in

Date: 2026-09-12. Verdict: **Meaningful progression**. This is an authoring audit, not release acceptance.

## Executive verdict

A frontier agent can author useful, nontrivial parts in the qualified lanes, but cannot yet treat the stack as one coherent language without learning command routing, reference wrappers, and compatibility boundaries. Geometry must be inspected independently: adding a counterbore changes the stock's world Z placement, and one mixed-case compatibility program originally exported an unchanged imported Box while claiming success after an authored Hole. The latter now fails closed. The placement issue remains a dedicated compatibility/authority milestone.

The burn-in began with 34 local programs: 28 built through ordinary `build`, 29 passed generic `validate`, and 23 passed generic `inspect`. These counts include deliberate invalid/reference probes and command-routing failures; they are **not geometry acceptance rates**. Follow-ups brought the preserved corpus to [45 sources](../../fixtures/Regression/LanguageBurnIn/README.md), with first attempts retained. Nothing was added to Canonical. Public-doc authoring, parameter variations of canonical fixtures, and later implementation-guided probes are identified in the [manifest](../../fixtures/Regression/LanguageBurnIn/manifest.json). These were simulated fresh-authoring subruns within one agent, not independent blind agents.

Local evidence is under `artifacts/local/lang-burn-x1/`: per-case source, build/inspect/validate JSON, STEP, analysis, verification, wireframe SVG, and browser screenshots; `results.json` retains initial exits/timings. `compact.json`, `determinism.json`, and `browser-reviews.json` summarize observations. Large artifacts remain ignored. Reproduce structured evidence with [Invoke-FirmamentBurnIn.ps1](../../scripts/Invoke-FirmamentBurnIn.ps1). The report records observed behavior rather than converting every successful build into an acceptance claim.

## Fixed during burn-in

| Fix | Root cause and old behavior | New behavior | Regression/evidence |
|---|---|---|---|
| Generic Sheet Metal inspection | `inspect` omitted the existing Sheet Metal dispatcher; a valid bent/tabbed part built but failed generic semantic inspection | Delegates to the existing `sheetmetal inspect` path, preserving its JSON schema and manufacturing evidence | `SheetMetalCliTests.GenericInspect_UsesAuthoredSheetMetalInspection`: one bend, one cut, 1.5 mm stock; local `sheet-tab/inspect-fixed.json` and `sheet-bracket/inspect-fixed.json` |
| Mixed-case compatibility Modify cannot disappear | Compatibility `ModifyHeaderRegex` matched only lowercase `modify`; uppercase `Modify` on imported stock was not consumed | Case-insensitive keyword matching reaches existing target validation and rejects unsupported imported modification with `firmament-v2-modify-target-not-solid` | `FirmamentV2InlineStepTests.InlineStep_UnsupportedModification_IsRejectedRegardlessOfKeywordCase`: `modify`, `Modify`, `MODIFY`, both parser and export; `import-modify/fixed.json` |
| PowerShell Forge sample | Example assigned to read-only automatic `$Host` | Example uses `$forgeHost` | Public Markdown link checks; executable Forge protocol subruns use the same process interface |
| Public command/default guidance | Standalone SectionChain command distinction and partial-record behavior were easy to miss | Added concrete source build command, construction alias requirement, and explicit whole-record default behavior | Successful `handle-g1/domain-build.json`; NativeAOT partial/default/complete Paperclip requests |
| Placement warning | Public geometry guide did not expose the route-dependent Box origin | Records the observed discrepancy and links this repro | Stock/counterbore STEP bounds below |

No new geometry kernel, reference framework, renderer, or arbitrary imported editing was introduced. The only runtime changes are existing-dispatch reuse and keyword recognition; geometry authority is unchanged.

## Friction table and ranked top ten

Rows B01–B10 are ranked by practical value. Frequency is observed scope, not a population estimate.

| ID | Area | Task | Friction / agent pain and frequency | Severity | Fixed? | Evidence | Recommendation |
|---|---|---|---|---|---|---|---|
| B01 | Placement/lowering | Add counterbore to Box | Same stock moves from Z 0..12 to -6..6; reproduced with and without chamfer | Major | No | `stock-block/analyze.json`, `mount-counterbore/analyze.json` | Establish one stock-frame law with compatibility migration |
| B02 | Parser/artifact | Modify imported stock | Mixed-case compatibility Modify was silently omitted; successful STEP lacked hole | Major | Yes: reject | `import-modify/retry.json`, `analyze.json`, `fixed.json` | Keep unsupported edits fatal; audit other compatibility blocks separately |
| B03 | Admission consistency | Boss stack | Build/validate succeed while inspect rejects Profile/Boss name collisions; three sources | Major | No | `boss-stack/inspect.json`: `firmament-v2-symbol-duplicate:Crown:Profile:Boss` and Pad equivalent | Unify admission before route selection; do not relax names in one command |
| B04 | Verification | Accept successful STEP | `verify` exits zero with unavailable mass properties or external inspection pending; several feature families | Major | No | Counterbore, chamfer, sheet and molded-shell `verify.json` | Publish machine-readable acceptance policy separate from command execution |
| B05 | Domain commands | Build smooth handle / analyze beam | Generic commands miss standalone SectionChain and FEA; domain commands succeed | Moderate | Docs | `handle-g1/build.json` versus `domain-build.json`; `steel-beam/fea.json` | Shared domain discovery and actionable dispatch guidance |
| B06 | Concept/frame | Reuse section datum | Four sections require four Concept planes plus four Construction aliases; direct reference rejected | Architectural | No | `direct-concept-frame/domain-build.json`: `section-chain-frame-missing:S0` | Shared oriented frame binding with consumer constraints |
| B07 | Forge defaults | Change two paperclip lengths | Schema fields claim defaults/optional; partial P fails for four missing fields | Moderate | Docs | `native-paperclip.json` versus complete/default requests | Clarify schema projection versus accepted partial record contract |
| B08 | Presentation | Inspect exported assembly | Single-part upload/analyze/wireframe reject Structural and Piping roots; extraction succeeds | Moderate | No | `frame/browser-failure.png`; assembly import counts 4/10/12 | Discoverable assembly viewer/import handoff |
| B09 | Diagnostics/selectors | Invalid hole; Bottom/Side | Generic validate accepts probes; build falls through to unrelated TOON/JSON error | Moderate | No | `invalid-negative-hole`, `bottom-hole`, `side-hole` | Preserve V2 failure ownership and expected selector vocabulary |
| B10 | Geometry capability | Angled drilling | Authored frame parses and inspects but materializer permits signed permutations only | Moderate | No | `angled-drill/build.json`: `HoleConstructionPlaneOrientationUnsupported` | Advertise admissible orientation in discovery; oblique drilling is a separate capability |
| B11 | Wireframe | Preview sphere | One-face closed sphere has no renderable bound curves/trimmed supports; shaded sphere works | Minor | No | `ball/wireframe.json`, `ball/browser.png` | Bounded untrimmed closed-surface preview support |
| B12 | Display | Cylinder / spline tube | Cylinder looks uncapped from initial view; coil tubes have flat/ribbon-like lighting despite closed reimport | Moderate | No | `turned-spacer/browser.png`, coil screenshots and surface/topology reports | Isolate cap sidedness/tessellation and spline normals; do not change STEP from screenshot alone |
| B13 | Authoring vocabulary | Add Boss to primitive | Must author Profile/Compose stock rather than reopen direct Box | Moderate | No | Public geometry guide; successful composed witnesses | Shared stock support contract before primitive feature expansion |
| B14 | Standard products | Find screw family | Forge list has Paperclip/sheet products but no HexBolt; bolt is a separate compatibility template | Moderate | No | `forge-list.json`, `hexbolt` source/build | Publish supported family discovery without claiming modeled threads |
| B15 | Import syntax | Re-export native stock | Guessed `Solid Body: InlineStep` failed; compatibility lowercase source worked; canonical form is `InlineStep Body` | Minor | Docs below | Retained `*.first-attempt.txt`; parser canonical InlineStep declaration | Add complete public import/replacement recipe |

## Artifact truth and wrong-artifact bugs

### B01: a feature changes stock placement

The sources use identical `Box Body { Size: [60mm,40mm,12mm] }`. The only added operation in `mount-counterbore` is a centered 6 mm through shaft with a 12 mm counterbore, 4 mm deep.

| Witness | Reimported bounds | Bodies/faces | Assessment |
|---|---|---|---|
| stock-block | X -30..30, Y -20..20, Z 0..12 | 1 / 6 | enclosed-manifold |
| mount-counterbore | X -30..30, Y -20..20, Z -6..6 | 1 / 9 | enclosed-manifold |
| hole-finish / finish-hole | same centered Z -6..6 | 1 / 13 | enclosed-manifold |
| explicit Compose boss-stack | Z 0..21 | 1 / 16 | enclosed-manifold |

This is a wrong-placement issue even though each individual BRep is closed. `AirHoleSimpleShaftMaterializer` and combined export paths explicitly choose centered bounds when `document.ConceptIr is null`, while Concept-backed hosts use 0..height. The primitive lowering path produces 0..height. Multiple callers encode this distinction. Changing one exporter offset would leave semantic selection, feature placement, and compatibility fixtures inconsistent. Defer the repair rather than compensating geometry after export.

### B02: imported Hole absent despite success — fixed to fail closed

The initial guessed PascalCase `Solid Body: InlineStep` failed for missing solid. Retrying the existing compatibility `solid Body: InlineStep` form with uppercase `Modify` produced success and an unchanged six-plane Box, zero cylinder surfaces, twelve edges, eight vertices. The case-sensitive compatibility Modify recognizer ignored the block. The case-insensitive recognizer now reaches existing unsupported-target validation; the fixed run emits no new STEP. The old successful artifact remains local only as pre-fix evidence, not as a current output.

No other missing-feature STEP bug was established. The cylinder cap and spline lighting observations are **display suspects**, not proven STEP defects: the independent reimport reports closed topology, and cylinder mass evaluation is `NumericalWithBound`. Fixing these requires inspection of display triangle orientation/coverage, not speculative STEP surgery.

`verify` is evidence, not a pass bit. For example, hole-finish reports `Unavailable` because face 12 produced no verification triangles, even though STEP reimport is valid. The report truthfully exposes this; process exit zero does not certify volume. Six baseline parts have unavailable mass evaluation: mount-counterbore, both counterbore/chamfer orders, both sheet parts, and molded-enclosure. No external CAD Assistant was invoked; `ExternalInspectionPending` must not be called external certification.

## Domain and composition coverage

| Domain/task | Witnesses and observed result |
|---|---|
| Primitives | Box, Cylinder, Sphere, pointed Cone, Torus: all build/reimport; shaded review performed; Sphere wireframe failure retained |
| Profile / Path | Fresh closed four-span rectangular Paths reused by extrusion and Compose; profile-extrusion is 36x18x7; deterministic closing preserves floating-point endpoint residuals without author repairs |
| Boss → Boss / Hole | Fresh pad 24x20x5 and crown 12x10x4; current Top gives Z17 then Z21; through bore survives both bosses; unique-name retries make inspect succeed |
| Pocket → Hole | 16x12 pocket, 4 mm depth in 12 mm stock; eight-mm floor and central through bore are visible in wireframe and shaded view |
| Hole ↔ EdgeFinish | Counterbore before/after 1 mm outer chamfer: both openings and edge break visible; origin inconsistency persists |
| Pattern ↔ feature | Four-hole pattern before/after chamfer: four holes and outer edge break visible in both |
| Sheet Metal | Enlarged L-bracket and 18x8 tab with hole: formed STEP, flat STEP, flat SVG, manufacturing regions/area and bend evidence; generic inspect now works |
| WireForm | Fresh one-bend and two-plane three-dimensional forms; finite tangent lengths, circular section, exact bend surfaces, rotated-frame program semantics |
| Coil / SurfaceCoil | Three-turn axis coil and three-turn spherical winding; each reimports 386 faces in one closed body; polynomial tube wireframe reviewed, no support sphere product body expected |
| SectionChain | Two fresh four-section handles, default G1 and explicit G0; 14 faces each; G1 pcurve reconstruction maximum 7.09e-10 mm, tangent-plane error 4.49e-5 degrees; visible smooth/ruled difference |
| BodyState chains | Longer east-attached grip plus preserved holes; west/east subtractive duct; 24 faces each with openings and PMI visible; these remain separate Station/Size authoring from standalone Profile/Frame chains |
| Structural | 700x450 centerline four-member hollow frame, miter interfaces and cut list; assembly reimport extracts four definitions/occurrences. Overall shaded assembly review blocked by single-part uploader |
| Piping | Six-anchor obstacle route and local reroute at 28 mm clearance with locked segment; assembly reimport extracts ten and twelve occurrences respectively; overall shaded assembly review remains blocked |
| PlasticShell | Frustum enclosure, four 9 mm standoffs, core holes, ribs, gate/ejector intent: 36-face closed reimport; zero-draft warning is retained and documented behavior |
| Products | Paperclip leg parameter changes, 42 mm HexBolt compatibility template, NativeAOT default LBracket and complete/default Paperclip invocation; bolt threads remain metadata |
| Materials / FEA | A36 native beam under 350 N tip load and canonical imported through-hole body under 350 N load solve via `fea`; native load integration residual 5.69e-14 N; this is a solver smoke, not external FEA validation |
| Imports | Qualified native stock re-export and imported-face FEA; generic imported Hole now rejects. No arbitrary BRep edit expansion |
| Planes/selectors | Signed-permutation side drilling succeeds and entry is visible after rotation; oblique normal fails materialization; literal Bottom/Side probes expose poor routing diagnostics |

Canonical qualification additionally exercises the shipped six/eight-section and line/arc/profile families. It is regression breadth, not a claim that fresh authors successfully designed every possible mixed-profile transition. Revolve, arbitrary pattern Axis, arbitrary boss support, arbitrary oblique drilling, mixed-topology chains, and global plane interchange were not qualified as new capabilities. Their kernel/internal representations are audited below; no invented syntax is presented as supported.

## Concept model findings

| Reference | Already shared | Divergence observed | Recommendation |
|---|---|---|---|
| Plane | `ConceptIrPlaneValue`: stable ID, origin, normal, orientation hint; semantic `ExactPlaneBinding` | Section/hole consumers require `ConstructionPlane`, which also derives full orthonormal axes; PlasticShell PartingPlane has its own record | Share explicit oriented-plane/frame binding and provenance; keep tooling constraints local |
| Axis | `ConceptIrAxisValue` and `ExactAxisBinding` retain origin/direction identity | Coil is a stateful winding generator; hole has placement + end condition; Structural has endpoint path; Revolve has `RevolveAxis3D` internally | Reuse an axis binding where callers need an axis, without replacing route/path or feature extent semantics |
| Frame / DatumFrame | `ExactDatumFrameBinding` explicitly binds all six rigid placement freedoms with a right-handed basis | ConstructionPlane, Wire StartFrame(Tangent,Up), Section frames, Piping port direction, and Structural Orientation have separate input forms | Shared checked frame mathematics/conversion; require consumer-specific constraints rather than universal record parsing |
| Point | Concept Point3 and semantic ExactPoint; Point2 accepted for hole centers and Path start | Profile uses local 2D; Piping/Structural use world 3D; imported point identity is topology-derived | Preserve coordinate-space and source identity; do not silently coerce 2D/world values |
| Direction | Unit/validated kernel direction, Concept vector, frame hints | `Plane: Up` in Wire means local rotation axis, not a Plane reference; Structural Orientation is a projected hint | Name/document role at boundaries; do not make a vector masquerade as a plane |
| Profile | Reused by Path-derived extrusion, Boss/Pocket and standalone chain | SectionChain BodyState uses bounded Station Size instead of standalone profiles; circle authoring can require explicit cardinal endpoints | Keep closed oriented loop authority; shared profile consumers before new sugar |
| Path | Ordered line/arc Concept Path has stable spans and closure | Structural Path is a graph edge; Piping Through is route anchors; WireForm is a forming program | Distinct domain meanings should remain; share exact curve/frame results where useful |
| Construction | `ConstructionPlane.TryTrace` validates finite origin, nonzero normal, projected Up, handedness, source Concept identity | Authored alias is mandatory even when only a semantic reference is needed | Keep non-product helper meaning; remove redundant adapters only in a dedicated binding milestone |

Concepts are not all merely data Records. `ConceptIrSemanticMember` distinguishes compile-time values from materialized semantic references, with semantic phase, provenance, type, and stable identity. Ordinary Records remain appropriate for Paperclip dimensions and manufacturing policy data. `Aetheris.Semantics` already has Point/Axis/Plane/DatumFrame bindings and capabilities; build on these seams rather than introducing a second Concept universe.

**Construction recommendation: A + B.** Keep `Construction` for non-product helper geometry and explicit authoring frames; replace redundant mandatory wrapping at consumers with resolved Concept/frame references where identity and handedness can be preserved. Do not remove the keyword or treat a plane as a fully oriented frame without its orientation policy. A Plane supplies origin/normal; a frame additionally owns in-plane orientation. Tooling parting, port outward direction, and sheet bend adjacency retain domain authority.

## Authoring workarounds and setup cost

| Intended task | Required setup/workaround | Why / action |
|---|---|---|
| Top hole on stock | Box plus one Hole; no temporary points/plane required using coordinate center | Good default within admitted selector vocabulary; fix origin law |
| Boss on box-shaped stock | Closed Path + Profile + Struct/Compose/Base before Boss | Direct Box cannot be reopened as Compose; public guide correctly says so |
| Stacked bosses | Rename feature Pad/Crown to RaisedPad/RaisedCrown for inspect, retaining profile names | Global symbol binder rejects collision while other commands admit it; preserve original repro |
| Four-section smooth handle | Four Concept planes + four Construction aliases + four reusable Profile references | Eight frame-related declarations for four stations; runtime trace validation is useful, mandatory alias plumbing is friction |
| 3D formed wire | One StartFrame; each Bend says Up or Right | No per-bend Plane required; transported local frame is a successful derived-reference design |
| Angled hole | Concept Plane + Construction alias + local center still cannot admit arbitrary angle | Signed-permutation kernel boundary, not a missing syntax trick |
| Change two Forge record fields | Send all six Paperclip fields, or omit the whole P to get defaults | Current record input is replacement, not merge; schema needs clearer contract |
| STEP re-export | Canonical `InlineStep Body { Path: "..." }`; older `solid Body: InlineStep` is compatibility | First attempt used an unqualified hybrid spelling; docs need a complete recipe, not a V3 grammar |
| Inspect assembly STEP | `asm import-step ... --out ...` rather than single-part analyze/upload | Preserve multi-root occurrence semantics; no flattening into a fake single solid |

Derived current Top and transported Wire frames are good examples of the compiler doing deterministic work. Shared support frames, face-plane extraction, and cylindrical axes are plausible future affordances only when source identity and ambiguity checks remain explicit. No nearest-face guessing was added.

## Simulated fresh-authoring task outcomes

| Task | First attempt | Retries/workaround | Final artifact status |
|---|---|---|---|
| Mounting block with counterbore/chamfer | Builds | No syntax retry | Features present; **placement discrepancy**, volume unavailable |
| Stepped boss stack | Builds; inspect fails | One naming retry | Stages and bore correct under explicit Compose interval; original collision retained |
| Sheet bracket with tab | Builds/flat succeeds; generic inspect fails | Dispatcher fixed | Bent plate, tab and hole observed; mass evaluator unavailable |
| Smooth handle | Generic build fails | Use `section-chain build` | G1/G0 bodies and measured continuity admitted; shaded and wireframe review |
| 3D bent wire | Build/inspect succeeds | No syntax workaround | Two bend planes and mouth/caps observed; closed topology |
| Surface coil around sphere | Parameter variation succeeds | No syntax workaround | Three-turn varying-radius winding; display-normal quality unresolved |
| Structural frame | Source/build succeeds | Assembly extraction command | Four occurrences confirmed; overall browser view blocked |
| Pipe around obstacle | Source/build succeeds | Assembly extraction command | Route/fitting evidence and ten occurrences; overall browser view blocked |
| Two Paperclip fields through Forge | Fails | Supply complete P (one retry) | NativeAOT invocation and STEP succeed; omitted-P defaults also succeed |
| Imported native modification | Initial spelling fails; compatibility retry silently drops feature | Parser fixed to reject | Correct fail-closed outcome; editing capability intentionally not added |

Success rates should therefore be reported by phase. The first 34-source ordinary-build rate is 28/34 (82.4%), but it would be misleading to label that an engineering success rate. Some entries are deliberately invalid; two good SectionChains need their domain command; some successful outputs have placement or verification issues.

## Small follow-up tickets

1. B11: render an untrimmed one-face sphere through the general wireframe path, with full-sphere parameter-domain proof.
2. B07: distinguish record-default projection from required fields of an explicit record in Forge describe; acceptance is one partial-record request matching its advertised contract.
3. B09: route invalid V2 Hole/selector diagnostics consistently; include feature name and expected selector forms instead of TOON fallback.
4. B05: expose domain command hints in generic validate/inspect/build for SectionChain and FEA, reusing domain authority.
5. B14/B15: complete public fastener/import recipes and discovery links; no promise of modeled threads or arbitrary imported edits.

## Deferred architectural milestones

| Proposed milestone | Problem and why deferred | Bounded scope | Acceptance witness |
|---|---|---|---|
| LANG-STOCK-FRAME-X1 | B01 origins vary by Concept IR presence and materializer; a local translation breaks other contracts | Define stock-frame authority, version/compatibility behavior, then align primitive/hole/finish lowering | Box before/after shaft, counterbore and finish retains exact world bounds; Concept/no-Concept parity and existing placement fixtures |
| LANG-ADMISSION-X1 | B03 multiple parsers/materializers independently admit source | One admission result for existing native Profile/Compose routes; preserve symbol rules | Colliding Profile/Boss names have identical validate/inspect/build outcome; unique-name stack remains correct |
| LANG-CONCEPT-FRAME-X1 | B06 plane/frame aliases and consumers diverge | Reuse semantic Point/Axis/Plane/DatumFrame bindings; retain provenance and domain constraints; no global grammar rewrite | Same resolved frame used by section and hole without duplicate helper; wire/port orientation parity and invalid handedness rejection |
| ARTIFACT-VERIFY-X1 | B04 closed topology does not ensure complete mass tessellation | Bounded cap/trim coverage and explicit acceptance policy; separate external observation | Counterbore/chamfer and sheet examples produce complete independently checked mass or explicit nonacceptance |
| DISPLAY-ASSEMBLY-X1 | B08/B12 uploader cannot review assemblies; cap/spline display suspects | Assembly import/view handoff plus isolated primitive cap/spline-normal repros in production renderer | Four miter rails and obstacle pipe shown at occurrence placements; closed cylinder caps and tube highlights match underlying geometry |

Arbitrary oblique drilling, imported BRep surgery, topology-changing loft correspondence, and generalized feature Axis consumption need their own kernel/feature contracts. They are capability suggestions, not bugs patched in this pass.

## Suggested future language/features

These emerged from the authored witnesses and are separate from the bugs above:

1. A checked support-frame reference accepted by both Sections and construction-plane drilling, avoiding one alias per station while preserving orientation and provenance.
2. A semantic primitive-stock operand for existing finite Boss/Pocket composition, so box-shaped stock does not require a second profile authoring recipe. Admit it only after the stock-frame law is explicit.
3. A documented bounded imported replacement recipe through existing recognition/replacement authority, exposing the supported workflow without implying that arbitrary `Modify` is legal on imported stock.

Profile reuse, transported wire frames, named semantic Top and immutable `with` already exist; they should be made easier to discover rather than reintroduced as new features.

## Validation and limitations

- Release solution build completed with zero warnings/errors after runtime fixes.
- Full serial default .NET suite: **3,254 passed, zero failed, zero skipped across 17 test assemblies**, using `dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1` (`full-serial-idle.log`, exit 0). Repository-disabled legacy test sources were not enabled; this is the full default suite, not opt-in legacy qualification. A previous run concurrent with NativeAOT/replay failed the timing-only `ThroughHole_RecipeLayerHasNoMeaningfulRuntimeRegression` (34.090 ms legacy, 262.109 ms recipe); the complete rerun without those concurrent workloads passed, with no threshold changes.
- Canonical qualification after the final parser fix: **140 fixtures pass** (`canonical-final.log`).
- Focused Sheet Metal regression and three imported-modification casing regressions pass.
- Seven repeat STEP hashes match; six repeat SVG hashes match. Repeated complete inspection JSON and diagnostics also match for stock-block, invalid-wire-frame and invalid-negative-hole (`diagnostic-determinism.json`). No general claim of byte-identical path/timing-bearing JSON is made. Initial CLI build turnaround was 0.109–0.719 seconds per case on this machine, excluding solution build. No performance optimization was justified.
- Fresh final packed CLI 2.0.0-preview.3 installed into a new local tool directory and built stock, counterbore/chamfer, sheet tab, Paperclip and HexBolt from a non-root working directory. **All five STEP hashes match** repo CLI outputs; packed STEP inspection succeeds, generic Sheet Metal source inspection succeeds, and imported Modify rejects.
- NativeAOT Forge publish/list/describe/default and explicit invocation exercised. Paperclip complete record and default record, plus default LBracket (formed/flat STEP and SVG), succeed; partial record fails as documented. NativeAOT emits pre-existing dependency trim/AOT warnings; this is successful execution of the tested routes, not a warning-free AOT claim.
- Production Cadmata/Three.js import screenshots cover the successful single-body baseline and side-drill/import follow-ups. A general wireframe SVG contact sheet was inspected for holes, bosses, pocket floor, chains, sheet, coils and shell. Assembly uploads were blocked; no independent third-party CAD kernel was available through this run. The browser path uses Aetheris import plus Three.js display and is not an independent topology certificate.
- No private debug renderer was created. The temporary Cadmata process was stopped after review. An initial test run hit its file locks; subsequent runs ran with it closed. A temporary docs-link failure occurred before this report existed and is resolved by writing/checking the report.
- The final grammar/reference audit is evidence-backed but not exhaustive: no new Revolve, arbitrary mixed-profile G1, or global Axis authoring capability is claimed. The report stops at isolated architectural blockers rather than producing special-case geometry repairs.
- Full preserved-corpus replay: **45 cases, 38 domain executions succeed, seven reject** (`artifacts/local/lang-burn-x1-replay/results.json`). The successful executions include two FEA solves; this is not 38 visually certified solids. Rejections are angled-drill, bottom-hole, direct-concept-frame, import-modify, invalid-negative-hole, invalid-wire-frame, and side-hole.
- Public Markdown links pass through the CLI test suite; report/corpus relative links pass an additional file-resolution check. Repository layout guard and `git diff --check` pass. New content is confined to the existing docs/release, fixtures/Regression and scripts roots; generated outputs remain local.

The principle “the correct way should also be the easy way” holds locally for semantic Top, finite Pocket, transported WireForm, materials and manufacturing intent. It is violated at command routing, admission parity, default-record discovery, and stock-frame ownership. The next highest-value work is stock-frame consistency and shared admission, followed by frame-reference reuse.
