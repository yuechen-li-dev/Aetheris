# PREVIEW3-HARDEN-A5e — fixture corpus qualification

## Executive verdict

Yes. A fresh human or LLM can use `fixtures/Canonical/` as a reliable Firmament V2 cookbook. The directory now contains 64 current, executable `.firmament` sources organized by engineering intent, an “I want to…” index, an operation coverage manifest, and an action manifest that qualifies every source through its real CLI path. Historical, invalid, regression, and speculative material no longer competes with current examples.

The conclusion is bounded to Preview 3's documented feature set. Loft, helix, arbitrary Boolean authoring, and freeform surface features remain unsupported and are linked from the canonical index rather than simulated with misleading examples.

## Before/after structure

Before A5e, the 767-file corpus mixed milestone folders, current examples, diagnostics, legacy V1 syntax, speculative `.firmfixture` material, and bug witnesses. Content-aware inventory classified only 4 files as immediately canonical, 69 as current but awkward, 444 as compatibility, 115 as invalid diagnostics, 47 as historical regressions, 43 as speculative, and 36 as requiring review.

The tracked fixture roots are now:

| Root | Purpose | Files after A5e |
|---|---|---:|
| `Canonical/` | Current Firmament V2 cookbook and its manifests | 67 |
| `Invalid/` | Minimal current one-failure diagnostics | 52 |
| `Compatibility/` | Legacy V1, `.firmasm`, and other retained compatibility inputs | 515 |
| `Regression/` | Historical and bug-specific witnesses | 88 |
| `Speculative/` | `.firmfixture` experiments and metadata | 44 |
| root `README.md` | Corpus contract and discovery entry point | 1 |

The post-move inventory has no unclassified source: the sole `UnknownNeedsReview` row is the root support README. Inline STEP payloads moved to `testdata/firmament/inline-step`; generated outputs remain under ignored `artifacts/local/`.

## Canonical coverage matrix

| Operation family | Minimal/current example | Practical or integration example | Invalid/regression evidence |
|---|---|---|---|
| Box, cylinder, analytic primitives | `Basics/box.firmament`, `Basics/cylinder.firmament`, `Primitives/` | primitive composition fixtures | canonical coverage guard |
| Concept Path and Profile | `Profiles/concept-path-line-arc-profile.firmament` | `Profiles/profile-delta-recess-extrusion.firmament` | parser/binder profile tests |
| Boss | `Features/Boss/rectangular-boss.firmament` | `Features/Boss/circular-boss-through-hole.firmament` | current Boss diagnostic corpus |
| Pocket | `Features/Pocket/rectangular-pocket.firmament` | `Integration/machined-mounting-block.firmament` | through-depth, insufficient-floor, and invalid-profile fixtures |
| Hole, counterbore, countersink | `Features/Holes/` | machined mounting block and PMI examples | current hole diagnostics and regressions |
| Slot/opening | `Features/Slots/straight-slot.firmament` | manufacturing integrations | slot parser/binder tests |
| Chamfer and fillet | `Features/EdgeFinish/` | mounting block and PMI example | EdgeFinish regressions |
| Pattern | `Patterns/record-array-hole-pattern.firmament` | four-hole mounting patterns | pattern diagnostics |
| Record, Static, Table, `with` | `Profiles/profile-delta-recess-extrusion.firmament` | Sheet Metal tab family and CNC policy | semantic profile parser tests |
| Template and DFM | `Templates/generic-mounting-plate.firmament` | `Templates/cnc-dfm-policy.firmament` and table-driven mounting plate | Template requirement tests |
| Materials and FEA | `Materials/material-catalog-coupon.firmament`, `FEA/cantilever.firmament` | `FEA/material-resolved-cantilever.firmament`, inline STEP cantilever | FEA/material diagnostic corpus |
| PMI/AP242 | `PMI/hole-dimension.firmament` | `PMI/multiple-hole-dimensions-with-chamfer.firmament` | CLI STEP round-trip tests |
| Sheet Metal | `SheetMetal/l-bracket-with-hole.firmament` | multi-hole bracket and `profile-delta-tab-family.firmament` | dedicated Sheet Metal invalid corpus |
| Assembly and drawing | `Assembly/bearing-module.firmament`, current drawing fixtures | nested assembly and drawing compile cases | active assembly/drawing suites |

`ProfileDelta` is deliberately demonstrated as domain-neutral semantic programming. The ordinary extrusion example uses Concept Path, Enum, Record, Static Table lookup, `with`, a typed Template owner, and a recess delta. Sheet Metal uses the same data-oriented style for a reusable tab family; it is no longer the implied owner of the abstraction.

Semicolons are optional when a newline, block boundary, or following named field makes the boundary unambiguous. Dense one-line declarations may retain them for clarity. Parser tests prove punctuated and unpunctuated ProfileDelta forms bind identically.

## Rewritten examples

Twenty-three current fixtures were materially reauthored or created, rather than merely renamed: 18 promoted examples had historical scaffolding or awkward syntax removed, and 5 focused coverage fixtures were added (cone, rectangular boss, circular boss with through-hole, rectangular pocket, and the Sheet Metal profile-delta tab family). The Concept Path profile and domain-neutral ProfileDelta example were also promoted and rewritten into modern teaching examples.

Canonical names now describe intent, declarations use current A5c/A5d style, assertions state engineering expectations, and fixture comments explain the operation rather than milestone provenance. Canonical discovery is guarded against stale aliases and historical syntax.

## Compatibility/speculative quarantine

Legacy V1 sources and format-compatibility inputs live only under `Compatibility/`; retained `.firmasm` assemblies have their own compatibility subtree. Historical milestone witnesses and bug-specific cases live under `Regression/`. Experimental `.firmfixture` language and its metadata live under `Speculative/` and are excluded from canonical authoring discovery. Current failing behavior lives under `Invalid/` with intent-based names. Nothing in these roots is presented as current syntax.

Relative references embedded in moved `.firmasm` and inline STEP fixtures were repaired and remain exercised. Useful history was preserved; it was not rewritten into fake V2 source.

## Bugs/friction found

| Severity | Finding | Disposition |
|---|---|---|
| Must fix | Boss/Pocket semantic diagnostics were emitted as warnings, allowing `validate` to report valid source. | Fixed fatal diagnostic routing for `firmament-boss-*` and `firmament-pocket-*`; added minimal invalid Pocket cases and validation-report tests. |
| Must fix | Semantic ProfileDelta fields effectively required semicolons despite otherwise line-oriented syntax. | Parser now terminates fields at semicolon, newline/end, or the next `Name:` field; equivalence tests added. |
| Fixture design | Moving inline STEP and `.firmasm` cases exposed hidden relative-path coupling. | Payloads moved to the testdata policy location and all embedded/test references repaired. |
| Tooling | PowerShell inventory classification used case-insensitive `-match`, merging case-significant language signals. | Switched to `-cmatch`; regenerated the inventory. |
| Docs/style | ProfileDelta looked Sheet-Metal-specific and semicolon advice was inconsistent. | Added ordinary-CAD and Sheet-Metal examples, snippets, grammar coverage, and explicit utilitarian punctuation guidance. |
| Document for preview | Loft, helix, arbitrary Booleans, and freeform surfaces are outside the supported family set. | Recorded and linked from the canonical README and supported-features documentation; no fake coverage added. |

No unbounded workaround or new CAD feature family was introduced.

## Fresh-agent results

The policy for this run prohibited spawning an independent subagent, so this is accurately recorded as a corpus-only reconstruction and retrieval exercise by the implementing agent, not an independent-agent result. Only the canonical index, canonical fixtures, and public documentation were used for the exercise; implementation history was not needed.

| Task | Selected guidance | Result |
|---|---|---|
| A — plate with four counterbores and chamfer | mounting-block integration, four-hole pattern, EdgeFinish | built successfully |
| B — block with boss, hole, and pocket | Boss/Pocket examples and mounting block | built successfully |
| C — typed Template with two configured variants | generic and table-driven mounting plate | built successfully |
| D — Sheet Metal bracket with holes | L-bracket and multi-hole Sheet Metal examples | flattened successfully |
| E — material-resolved FEA cantilever | material catalog and material-resolved cantilever | solved successfully |

The five retrieval prompts each have one obvious starting point: countersink → `Features/Holes/countersink.firmament`; Pocket → `Features/Pocket/rectangular-pocket.firmament`; CNC policy → `Templates/cnc-dfm-policy.firmament`; Sheet Metal hole → `SheetMetal/l-bracket-with-hole.firmament`; Force → `FEA/cantilever.firmament`.

## Test migration

All fixture consumers were migrated to the semantic roots, including literal paths, `Path.Combine` fragments, CLI/public-document examples, release capability manifests, embedded `.firmasm` references, and inline STEP support paths. A repository-wide stale-path scan is clean in active code and documentation.

`scripts/Test-CanonicalFixtures.ps1` enumerates every canonical source and dispatches its real action from `qualification.json`: STEP build, Sheet Metal flatten, FEA solve, assembly inspect, or drawing compile. It rejects warnings/errors, uncovered files, missing coverage entries, and stale syntax. All 64 canonical sources pass. The repository layout guard passes all 3,591 tracked files.

Validation completed:

- Release solution build: 0 warnings, 0 errors.
- Full active .NET suite: 3,017 passed, including Firmament parser/binder, invalid diagnostics, compatibility, Templates, Sheet Metal, FEA, PMI/AP242, assembly, drawing, CLI, Server, and Forge suites.
- Public documentation/example qualification passed as part of the CLI suite.
- VS Code extension: typecheck, 13 tests, build, and VSIX package passed.
- Client: typecheck, 81 tests, build, and lint passed during release packaging.
- Canonical qualification, coverage guard, repository layout guard, Markdown link qualification, and `git diff --check` passed.

TSPack reported acknowledged dependency-version conflicts and blocked lifecycle-script categories; these are policy notices, not failed checks.

## Release impact

The final Preview 3 publication set was regenerated through the production packaging path, including the CLI NuGet package, Windows x64 ZIP, NativeAOT Forge Host embedded in the ZIP, and Firmament VSIX. From a fresh ZIP extraction, packaged CLI help and ProfileDelta validation passed, and the NativeAOT Forge Host returned valid `info` and `list` protocol payloads. A second clean output directory produced an identical `SHA256SUMS.txt`, proving byte-for-byte reproducibility for all three publication artifacts.

First-run publication hashes:

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| `Aetheris-2.0.0-preview.3-win-x64.zip` | 107823395 | `e2b085cbd5d86e43873578a74425a550cb620f0ee73c90d7159457a0d5386d6a` |
| `aetheris-firmament-0.3.0-preview.3.vsix` | 12951 | `42eb40170a27ea8c635a9680b8d845917a8a99c6c07dec518b2a31b0b5179b7a` |
| `packages/Aetheris.CLI.2.0.0-preview.3.nupkg` | 33416841 | `850c568d840994654c1deaee6f79cf1b221fb4edc91fb21d6e212f88c8e6d488` |

The 16 public library packages remain owned by the separate `scripts/package-public-libraries.ps1` publication path and were not changed by fixture reauthoring.

## Feature freeze

Confirmed: A5e adds no new engineering feature family. It reauthors examples, generalizes the presentation of existing Concept Path/ProfileDelta semantics, makes punctuation consistently permissive, fixes two bounded validation/parser defects, repairs paths, and automates coverage. Unsupported families remain explicitly unsupported.
