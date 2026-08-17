# PREVIEW3-HARDEN-A4

## Executive verdict

Yes. Preview 3 behaves as one coherent release candidate across the qualified Windows x64 surface. Packaged Firmament, STEP AP242, Sheet Metal, semantic PMI, Cadmata, Standard Library materials, native and `inlineSTEP` FEA, and the NativeAOT Forge Host were exercised through reused artifacts rather than isolated fixtures. All A4 ReleaseBlocker and MustFix findings were fixed and rerun through the packaged path that exposed them.

The release boundary remains bounded and explicit. Ordinary prismatic STEP does not persist a general solid-material designation; material identity is qualified for authored Sheet Metal and Firmament FEA. This is documented rather than widened during freeze.

## Integrated workflow matrix

| Workflow | Packaged path | Result | Findings | Release status |
| -------- | ------------- | ------ | -------- | -------------- |
| A — machined mounting block | ZIP CLI: Firmament -> BRep -> AP242 -> reimport -> Cadmata | 6 truthful features; 64 faces; enclosed manifold; 1 datum and 2 dimensions; byte-stable STEP | EdgeFinish was absent from structured inventory; fixed. General CAD material persistence is not supported and is now documented. | Pass with documented boundary |
| B — Sheet Metal bracket | ZIP CLI: authored bracket -> formed STEP + flat STEP + SVG -> reimport | 3 regions, 1 bend, 1 circular opening; 5052-H32 identity; all DFM rules pass | Material was absent from CLI summaries and help showed a stale invocation; fixed. | Pass |
| C — manufacturing PMI | packaged CLI + production Cadmata: CTC-03 AP242 | 3 datums, 13 dimensions, 5 position controls, 8 annotations, 23 face-associated PMI items | One internal materializer class name leaked through a consumer label; fixed. | Pass |
| D — native FEA | ZIP CLI + shipped SQLite catalog | ASTM A36, 500 N load, converged displacement/stress/reaction results, equilibrium residual below 1e-9 N | JSON failures previously went only to stderr; fixed. | Pass |
| E — generated STEP `inlineSTEP` FEA | ZIP CLI Firmament STEP -> generated analysis source -> FEA | imported face selection, Al 6061-T6, converged solve, equilibrium residual below 1e-8 N, repeatable Abaqus identity | Unknown face produced an internal nullable exception; fixed with selector inventory. | Pass |
| F — Forge foreign-language generation | published NativeAOT host + shipped Python and Go clients | Protocol v1 discovery/invoke; equivalent artifacts byte-identical; generated STEP reimports enclosed | Error categories all returned exit 4; fixed to request 2, missing template 3, invocation 4. | Pass |
| Release ZIP | clean extraction in a path containing spaces | CLI, examples, docs, licenses, materials, Forge, Cadmata, and FEA work without checkout | Public docs/examples/licenses were missing; PS 5.1 release script APIs failed; incremental compiler state changed ZIP bytes. All fixed. | Pass |
| External NuGet consumer | fresh project, generated packages only | versions load, SQLite material resolves, direct C# Forge API emits reimportable STEP | Smoke consumer needed an explicit Standard Library package reference for its direct API use; fixed. | Pass |

## Bugs found and fixed

### ReleaseBlocker

- The candidate ZIP omitted public documentation, executable examples, foreign-language Forge clients, `LICENSE`, and `THIRD_PARTY_NOTICES.md`. The release composition now includes the complete public walkthrough surface and its exact dependencies.
- Consecutive release builds could inherit shared managed compiler output from a prior NativeAOT publish. The resulting ZIPs differed in 15 managed assemblies. The release script now cleans the Release graph before publishing; two independent builds subsequently contained 672 files each with zero hash differences.

### MustFix

- Windows PowerShell 5.1 could not call `Path.GetRelativePath`, and pipeline binding to `Get-FileHash` failed. Both release-script paths are now PS 5.1 compatible.
- Authored `EdgeFinish` was missing from the engineering feature inventory even when the emitted chamfer was present. The report now retains stable EdgeFinish identity for admitted composed and external modification routes.
- Sheet Metal build/flatten output omitted authored material identity and manufacturing specification. JSON and human output now report them.
- FEA `--json` failures emitted plain stderr or an internal nullable exception for a missing imported face. Failures now emit structured diagnostics on stdout, including available imported face selectors.
- Forge Host invocation failures did not preserve the documented protocol exit classes. Packaged smoke now proves 2/3/4/0 for bad request/missing template/invocation failure/subsequent valid invocation.
- Top-level CLI help showed a stale Sheet Metal flatten form. It now uses the supported Firmament/flat STEP/SVG arguments.
- Cadmata fixture metadata exposed `AirHoleSimpleShaftMaterializer`. The public consumer label is now semantic and implementation-neutral.
- The initial release-bundle walkthrough used nonexistent `firmament validate/build` command prefixes. The clean-install commands now match the shipped CLI.

### DocsFix

- Forge interoperability docs now use the shipped NativeAOT host instead of directing release users to build from source.
- Release notes and known issues now state the Windows x64, imported-unit, imported-containment, mass-verifier, Sheet Metal opening, and general CAD material-persistence boundaries.

No open ReleaseBlocker or MustFix remains.

## Cross-subsystem evidence

- Firmament machined STEP -> packaged CLI reimport -> Cadmata: SHA-256 `19CC6E25BB31C563E078EF5A2F5699D6296CE4D16A788BB7084BB358115DB98A`; 64 faces; enclosed manifold; Boss, Pocket, EdgeFinish, shaft hole, and two counterbores reported as 6 features.
- Firmament through-hole STEP -> `inlineSTEP` FEA: geometry identity `0f572512346ec480aee64b2e84cb4898eb2136df017cfbb99674ae0f29ea5823`; repeat Abaqus SHA-256 `CAF0772E625E024E159CEB31F04F6A5C118BABFBB536A7081242B8355E7FF5A4`.
- Sheet Metal bracket -> formed/flat/reimport: formed STEP `347A5D2968A01D2C94F439516A775262C3FC553EACD103CB1A7998A1476340FC`; flat STEP `7DABD84D3D9089055DAB5205A6270F571F8B820FD86ED989046901B43614C0A4`; SVG `0E10E0ECA045927A4FABD17DD4B98B5EBF846A79C84E98CFCB0C9551E5CF3336`.
- Manufacturing AP242 -> packaged CLI -> Cadmata: 129-face enclosed manifold, millimetre public basis, 3 datums, 8 diameters, 5 linear dimensions, 5 positions, 8 engineering annotations, and 23 geometrically associated items.
- Forge Python/Go -> packaged CLI: STEP `114CD7C0C6A8A364B2943CC955A12D8A96B576A187DFC1957EA9F769296872BE`, flat STEP `88C437373FE4FDF91E8F0A5B5E0E5C135B290F0DFE18449EC3EE7C0970C1D075`, SVG `1657E3BBC3EF418617B45C5D9AB76A96D70B0BA6356C0E88A7BA07EDC18B6519`; zero cross-language artifact differences; generated STEP reimported as a 46-face enclosed manifold.
- External NuGet consumer -> Standard Library -> direct Forge API -> STEP importer: pass without project references or source-checkout runtime files.

## Cadmata whole-product dogfood

The production bundle loaded a small PMI witness, full CTC-03 manufacturing AP242, a plain Boss/Pocket part, a Sheet Metal part, and CTC-03 again. Orbit, pan, zoom, reset, selection/deselection, viewport resize, PMI filters, datum selection, feature inspection, geometry-to-PMI discovery, and model switching all remained usable.

CTC-03 displayed 3 datums, 13 dimensions, 5 position controls, and 8 annotations. Face 123 discovered its diameter, position, and engineering note. Dragging the `FrontMountPosition` callout changed presentation position while semantic identity `step-pmi:4030` remained unchanged. The note `MountHoleRework` remained associated with feature `FrontMountHoles` and faces 123/124. Switching PMI-heavy -> plain -> Sheet Metal -> PMI-heavy cleared selection, callouts, names, and filters without cross-model contamination. One non-fatal Three.js `Clock` deprecation warning remains documented.

The final clean ZIP Cadmata executable served its production UI with HTTP 200 and exited cleanly.

## Error recovery and unsupported boundaries

- Unknown material -> `firmament-material-unknown` -> corrected native FEA succeeds.
- 40 mm Pocket in 12 mm stock -> `firmament-pocket-through-depth` with remaining floor `-28mm` and required floor `3mm` -> corrected part succeeds.
- Imported `body.face(#9999)` -> `firmament-analysis-inline-step-face-missing` plus supported selectors -> corrected imported analysis succeeds.
- Forge bad protocol, missing template, extra argument, and failed template requirement -> structured diagnostics and exit 2/3/2/4 -> valid invocation afterward exits 0.
- Packaged CLI probes reject general loft, helix, and arbitrary public solid Boolean records with fatal `firmament-v2-unknown-record-type`; reject a spindle/generalized torus with `firmament-v2-primitive-field-invalid`; and reject Sheet Metal Counterbore/Countersink with the actionable `sheetmetal-hole-domain-syntax` boundary. The serial FEA corpus verifies `firmament-analysis-inline-step-containment-unsupported`. No crash, silent omission, or new feature implementation was introduced.

## Release ZIP walkthrough

The final ZIP was extracted under `%TEMP%` to a directory whose name contains spaces. No command used the repository checkout for runtime dependencies.

1. Top-level help returned exit 0 and exposed validate, build, inspect/analyze, Sheet Metal, FEA, and related command discovery.
2. The bundled A4 machined source validated in 188 ms, built in 459 ms, and reimported in 201 ms.
3. The bundled Standard Library catalog resolved ASTM A36 and Al 6061-T6 during native/imported FEA.
4. The Sheet Metal bracket produced formed STEP, flat STEP, and SVG with material, bend, cut, and DFM evidence.
5. Native and generated-STEP FEA solved with typed SI-unit results and equilibrium evidence.
6. NativeAOT Forge `info`, `list`, `describe`, and `invoke` succeeded; Python and Go clients emitted identical artifacts.
7. A Forge-generated STEP was inspected by the packaged CLI.
8. The shipped Cadmata host served the production bundle.

The ZIP root now contains `README.md`, `LICENSE`, `THIRD_PARTY_NOTICES.md`, `docs/public`, tracked examples and their dependencies, `Materials`, the CLI, Cadmata, Forge Host, and all four foreign-language client sources.

## Performance sanity

Representative wall-clock observations on the qualification machine:

| Operation | Approximate time |
| --------- | ---------------- |
| Machined validate | 0.19 s |
| Machined build | 0.46 s |
| Machined STEP reimport | 0.20 s |
| Sheet Metal formed build | 0.27 s |
| Sheet Metal flatten + STEP + SVG | 0.39 s |
| Forge host invocation | 16–20 ms template execution; 0.11 s Python process roundtrip |
| Go client | 9.44 s including `go run` compilation |
| Native ASTM A36 FEA | 0.70 s |
| Imported STEP FEA | 1.49 s |

No severe performance regression was observed. Twenty packaged Forge invocations completed in 676 ms, twenty STEP reimports in 3.85 s, and twenty material-backed native FEA runs in 12.58 s, all with zero failures or obvious handle/process leakage.

## FEA evidence and units

- Native ASTM A36 cantilever: 500 N load, maximum displacement `2.50619032107412e-5 m`, maximum von Mises stress `9.90827051725881e6 Pa`, reaction magnitude approximately 500 N, equilibrium residual `8.911281496689226e-10 N`.
- Generated imported STEP: Al 6061-T6, maximum displacement `1.26136842667411e-5 m`, maximum von Mises stress `4.65430702810233e6 Pa`, equilibrium residual `9.93853727025691e-9 N`.
- Firmament and STEP public geometry are millimetre-first; Standard Library elasticity values are Pa; loads and reactions are N; FEA displacement is reported in m. AP242 inspection reports mm and explicitly labels the imported-unit preservation limitation.

## Determinism and provenance

After adding a clean managed build boundary to the release script, two complete release builds each contained 672 files and produced zero file/hash differences. The final release ZIP SHA-256 is `88B0A38A14654C96CE9188D8EFED6085EB0EAC5B2FB515EA28A5F8E8F827DF12`; the VSIX is `DEF44BC6A83AED90A67505987EC36D00AA134D900FAB734C48B83D0AEB59F361`; and the CLI NuGet tool package is `1D1233B72E64B92662B437B46C7BF53CA0AE09A79D9DD18ADBF038AE074FCD43`.

Representative model and Forge hashes are listed in Cross-subsystem evidence. Timing fields vary as documented; semantic identities and artifact bytes do not. Output contains release version/protocol identity and no developer-machine paths or random GUID-based product identity.

## Known issues

Public release limitations are maintained in [`docs/public/reference/known-issues.md`](../public/reference/known-issues.md). The release-relevant set is:

- qualified release binaries are Windows x64 only;
- `inlineSTEP` is a bounded single-body/face-identity class and does not promise arbitrary imported containment;
- the generic tessellated mass verifier can be unavailable for some valid combined analytic bodies;
- Compose-host Countersink and Sheet Metal Counterbore/Countersink remain outside their admitted domains;
- imported STEP topology does not preserve source unit metadata and the qualified input is millimetre AP242;
- ordinary prismatic STEP does not persist a general solid-material designation;
- one harmless Three.js clock deprecation warning can appear in developer tools.

These are DocumentForPreview limitations, not unresolved ReleaseBlockers.

## Feature-freeze audit

Feature freeze status: **intact**.

The post-freeze capability exception remains only A3b first-class connected Boss and finite-depth Pocket semantics. A4 changes are correctness, diagnostics, structured-output truthfulness, packaging, release determinism, test coverage, and documentation corrections. No CAD/Sheet Metal/PMI/FEA/Forge/Cadmata feature family, protocol verb, physics mode, platform RID, C ABI, or architecture was added.

## Release notes draft

The factual public draft is [`docs/public/reference/release-notes.md`](../public/reference/release-notes.md). It covers Firmament V2, analytic/prismatic CAD, Boss/Pocket, Sheet Metal, AP242 and semantic PMI, Cadmata, materials, bounded linear-elastic FEA, NativeAOT Forge interoperability, and the Windows x64/bounded-feature limitations without expanding the release promise.

## Finding classification

- **ReleaseBlocker (resolved):** incomplete release ZIP; release ZIP nondeterminism from inherited publish state.
- **MustFix (resolved):** PS 5.1 packaging failures; untruthful EdgeFinish and Sheet Metal material summaries; FEA JSON and missing-face diagnostics; Forge exit mapping; stale CLI help; internal Cadmata label; invalid release walkthrough commands.
- **DocsFix (resolved):** shipped-host Forge instructions, units/material boundary, release notes, known issues.
- **DocumentForPreview:** Windows x64 qualification; bounded imported containment; imported STEP unit metadata; combined-body generic mass verifier; Compose/Sheet Metal opening boundaries; general CAD material persistence; Three.js warning.
- **DeferredPostPreview3:** new geometry/formed-feature families, arbitrary Boolean authoring, additional FEA physics, new protocol verbs, UI controls, and additional RIDs.

## Validation

- Release build: `dotnet build Aetheris.slnx -c Release -m:1 --no-restore` — pass, 0 warnings, 0 errors.
- Full serial .NET suite: 3,017 passed, 0 failed, 0 skipped. `Aetheris.FrictionLab.Tests` remains a pre-existing assembly with no discoverable tests.
- Cadmata: typecheck pass; 16 test files / 81 tests pass; production build pass; lint pass; packaged production-host and interactive dogfood pass.
- VS Code extension: 13 tests pass; typecheck/build/package pass.
- Public docs/examples: public qualification tests pass; bundled Getting Started, Boss/Pocket, material, PMI, Sheet Metal, native FEA, `inlineSTEP`, and Forge examples run through the packaged CLI/host.
- Public libraries: 16 Preview 3 packages generated; fresh external restore/run and direct Forge API/STEP reimport pass.
- NativeAOT Forge publish: pass with the existing audited trim/AOT warnings; packaged protocol and foreign-language smoke pass.
- Release ZIP: build pass; clean extraction walkthrough pass; required docs/examples/licenses present; no source checkout dependency.
- Forge protocol: Protocol v1 remains distinct from Aetheris `2.0.0-preview.3`; error recovery and valid invoke pass.
- STEP corpus: machined, Sheet Metal, Forge, primitive, and manufacturing PMI export/reimport tests pass.
- PMI/AP242/Cadmata: semantic counts, associations, filters, selection, dragging, and model-reset flow pass.
- Native FEA and generated-artifact `inlineSTEP` FEA: converged, equilibrated, typed, repeatable.
- Resource sanity: 20 Forge invokes, 20 STEP imports, and 20 material/FEA runs pass without obvious leakage.
- Release logging: ordinary successful CLI/host output contains no stack traces, parser traces, debug spam, developer paths, or internal materializer names.
- Stale-development leakage scan over public docs and packaged README/samples: clean for milestone/debug implementation names and developer paths.
- Deterministic reruns: representative STEP/flat STEP/SVG/Abaqus/Forge artifacts stable; two complete post-fix release trees match file-for-file.
- `git diff --check`: pass.

## Acceptance

The preserved A4 corpus covers the realistic machined part, practical Sheet Metal bracket, CTC-03 manufacturing PMI/Cadmata interaction, native material-backed FEA, generated-STEP `inlineSTEP` FEA, published Forge Host with Python/Go clients, clean release ZIP, and external NuGet consumer. All A4 ReleaseBlocker and MustFix findings are resolved and rerun through their original integrated workflows.
