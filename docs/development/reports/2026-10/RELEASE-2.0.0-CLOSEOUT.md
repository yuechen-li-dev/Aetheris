# Aetheris 2.0.0 release qualification

**Accepted: the desktop, canonical-sample hotfix and exact distribution are qualified.**

Shipping product source: `c279d98d44701f2bbfa78622d0a99023dde44db8`.
[Producer and full regressions](https://github.com/yuechen-li-dev/Aetheris/actions/runs/40132493203); [complete Windows/Linux artifact requalification](https://github.com/yuechen-li-dev/Aetheris/actions/runs/40135607129).
Subsequent commits change test/CI qualification and this report only. No stable Aetheris tag or
package has been published by this pass.

## Desktop and canonical-sample hotfix

| Reproduction | Repair and evidence |
|---|---|
| The user-extracted ZIP opened no window. It shipped Aetheris.Server as Cadmata.exe; no-argument startup hosts a headless service on port 5000. The previous HTTP smoke did not prove a desktop. | Publish the existing Aetheris.Cadmata.Desktop WinForms/WebView2 host as root Cadmata.exe. Preserve explicit Cadmata.Server.exe for headless use. The real desktop owns an authenticated ephemeral loopback service and closes it with its window. |
| Separate CLI/server publishes duplicated 215 DLL names (110,931,664 bytes). | Merge into one root. Identical shared files occupy one path; SDK WindowsDesktop runtime-pack metadata selects required framework facades. Other differing shared files fail packaging. Vendor browser architecture files, localized resources and SDK WebView2Loader locations are retained. |
| The restored desktop also requires its production frontend and offline browser, which the headless ZIP omitted. | Include the pinned fixed WebView2 runtime, compiled assets, sample, complete notice audit and corresponding-source notice. The standalone CLI NuGet tool does not acquire the desktop browser. Staging/CAB/expanded work stays outside the uploaded publication directory. |
| Canonical through-hole STEP imports with orientation/trim warnings. | Correct four records: cavity same_sense, seam endpoint references and swapped top/bottom circular edge uses. Current generation already emits the quiet positive control. No importer warning is suppressed. |
| Hosted desktop test times out although its native log shows backend ready and UI visible. | The test incorrectly required a DevTools connection for its launch-only level. Inspect the actual app's PID-bound native window, WebView navigation log, unauthenticated 403 and native WM_CLOSE/service shutdown. Full rendered/interactive testing remains a separate real-app CDP path. |
| Temporary CI tool checkout makes clean source appear modified. | Put TSPack under ignored artifacts/local. Hosted publication rejects dirty source before expensive desktop publishing, with exact unexpected paths; artifact consumers reject modifiedSource metadata. |

The repaired stored STEP remains a 10x8x6 mm body with a 2 mm through bore.
All face entity IDs survive, including #191 used by PMI. Referencing the actual
circular boundary vertices welds the seam: topology is seven faces, fifteen edges
and ten vertices instead of twelve. Trim loops close, the shell is enclosed,
pcurves qualify and all seven source face senses agree with derived orientation.
Export/reimport also qualifies without diagnostics. These STEP bytes and seam
vertex identities intentionally change; this is not a blanket claim of byte
parity for outputs involving the formerly malformed fixture.

The new corpus regression uses Step242Corpus and checks acceptance, zero
messages, qualification, connected/enclosed topology, bounds, radius and PMI
face identity. All 63 focused InlineStep/PMI cases pass. Hostile-source tests
retain the importer's diagnostic behavior.

## Current qualification evidence

| Gate | Observed result |
|---|---|
| Clean-source full Linux regression | 6,002 passed, 7 optional 3DM skips, 0 failed; 20 suites and 6,009 tracked cases. Fast Core lane: 1,088 passed. |
| Clean-source full Windows regression | 6,000 passed, 9 optional skips, 0 failed; 20 suites and 6,009 tracked cases. Seven need external 3DM inputs; two need the external PDF renderer, which runs on Linux. |
| Complete clean-source producer | Passed: clean frontend check/typecheck, 96 tests, production build and lint; CLI, NativeAOT Forge, actual desktop/offline browser, VSIX (13 tests) and 25 libraries. Source identity is clean. |
| Exact immutable artifact consumers | Passed on both OSes in run 40135607129: all 28 receipts, portable tool, public API consumers and 25-package closure. Windows also passes actual native launch/close, production assets/import, all 15 native templates and advertised exports, assembly inspection and PDF/PPTX generation. |
| Actual downloaded desktop | The final clean hosted ZIP passes real native-window/WebGPU sample rendering, orbit/selection, its bundled repaired STEP import/export/reimport, invalid-file retention and native close/service shutdown on Windows 11/NVIDIA Ampere; no uncaught browser errors. Sample visible in 3.2 seconds. |
| Native hosted launch control | [Final hosted native qualification](https://github.com/yuechen-li-dev/Aetheris/actions/runs/40135607129) passes window ownership, browser navigation, 403 and close/shutdown. Its no-GPU telos-adapter-unavailable diagnostic is preserved. This does not certify rendered geometry on GitHub's server VM. |
| Negative provenance control | [Requalification control](https://github.com/yuechen-li-dev/Aetheris/actions/runs/40133808301) rejects the older modifiedSource ZIP; it is not relabelled as a successful release. |
| Engineering witness | CLI through-hole STEP remains SHA256 5999f127e5a1477e5825afa656dceb7f89241d8f85312b0e6bcd793639a0f548 with the shared runtime. No geometry algorithm, planner, tessellator or orientation resolver changed. |

Local evidence is under artifacts/local/release-2-readiness/desktop-repair.
The initial full local suite exceeded an existing imported-rod tessellation
budget while GPU/UI testing ran concurrently. The exact test and full suite both
pass when rerun without concurrent UI load: 6,007 passed, two optional PDF-renderer
skips, zero failed before adding the canonical fixture regression. No budget was
increased. Historical source/count receipts remain distinct below.

Artifact requalification uses the existing release-smoke matrix against the
producer's immutable staged ZIP/packages. It verifies both original full
regression jobs succeeded, checks producer source identity, reads version from
the actual NuGet manifest, and verifies all checksums and dependency closure.
It never publishes. This permits a corrected test harness to qualify the same
bytes without pretending a rebuild or skipped regression was a full run.

The final artifact is ID 12576947757 from producer run 40132493203. Its
BUILD-METADATA identifies c279d98d and modifiedSource=false. The actual desktop
apphost SHA256 is e3dde19afe2566fdf1b9ee0e2c9dc61a85fe275c9dd53c800aa9c9f15cbf35ed;
this matches the separately downloaded hosted native qualification receipt.
The notice audit is complete; the source notice and one fixed browser tree are
present, with no obsolete cadmata dependency directory.

| Corrected hosted artifact | Bytes | SHA256 |
|---|---:|---|
| `Aetheris-2.0.0-win-x64.zip` | 416022516 | `7160f36a9cecacd68c3dff06cfb326fc88502f9d0ca10c97e81fc68bc6a033fc` |
| `packages/Aetheris.CLI.2.0.0.nupkg` | 18002334 | `ed0c9338f5e3dbb30db80ad01343c2fe5c1bbcc82f9e4f7a798a56d0d28a4c31` |
| `aetheris-firmament-0.3.0-preview.3.vsix` | 13191 | `cae2cb93c955d93e7e030e6bc4ea9598ab65bcdba15afc4ca9551910ee9a2eed` |

The 416 MB ZIP now includes the actual offline desktop/browser. The core CLI
remains about 18 MB. This does not add the browser to its NuGet payload.

Final receipts: desktop-repair/final-clean-distribution-receipts.json,
final-clean-hosted-ui/qualification.json, final-canonical-reexport-analysis.json,
clean-hosted-native-evidence/qualification.json, hosted-clean-linux-summary.json,
hosted-clean-windows-summary.json and clean-requalification-run.json.
The final exported canonical box is again an enclosed manifold, seven faces,
fifteen edges, ten vertices, unchanged bounds and zero source mismatches/reasons.

The producer's original Windows consumer used the earlier DevTools-dependent
harness and failed that connection; its full regression/build gates passed.
That failure is preserved, not relabelled. The corrected harness then qualifies
both OS consumers of the exact immutable producer artifacts in the separate
green run linked above. No shipping C# or model source changed after c279d98d;
only test/CI qualification and this report did.

## Current executive verdict

**Accepted for the tested release scope.** The public executable is the actual
desktop, the redundant server dependency tree is gone, the canonical box is
quiet and Qualified, source identity is clean, both full regression lanes pass
and the exact distribution passes both consumer lanes plus local GPU interaction.
Geometry algorithms are unchanged; the malformed stored STEP is explicitly
repaired with stable nominal geometry and PMI face identity.

Qualification remains bounded: Windows 11/NVIDIA Ampere supplies the actual
rendered/interactive proof; GitHub's server VM supplies native host/navigation
proof and explicitly reports no GPU adapter. No human SmartScreen/title-bar
session, macOS desktop or broad hardware certification is claimed. Aetheris
publication remains separate; this pass has not created a stable tag/package.

Quality pass: shared-file conflicts fail rather than bind ambiguously; facade
ownership comes from SDK metadata; temporary tools/staging are ignored and kept
out of publication; diagnostic logs survive test failures. The failed candidate
is preserved. Its verified orphan headless process 42152 was stopped, and the
qualification app sessions close their own services. No background service,
startup hook or scheduled task was installed.

## Historical readiness qualification

The following receipts belong to source 69bcc6bb and its stated automated paths.
They preserve earlier frontend, package-graph and engineering-parity evidence.
They did not establish desktop acceptance: the headless-only Windows packaging
and its 133 MB ZIP are superseded by the repair above. Their hashes and counts
must not be assigned to the corrected distribution.

Distribution candidate: `69bcc6bbd839fe2ce2a4e1bbd9231f28cd719286`.
Baseline: `b05cca40ec3efd7540c647398892953299d2c136`.
TSPack: `01051b791cf83757a1c419ef7bc276d9de787a9d`.
The [complete distribution run](https://github.com/yuechen-li-dev/Aetheris/actions/runs/40121433111)
passed on October 24, 2026 and is the authority for hosted qualification.
The later closeout commit changes this report only. No Aetheris stable tag or package
has been published by this pass.

## Original audit and disposition

This follows [Claude's blocker audit](RELEASE-2.0.0-BLOCKERS.md). Its original
findings and decisions remain preserved there.

| Finding | Current reproduction and disposition |
|---|---|
| B1: incomplete public dependency graph | Confirmed. The sixteen-package list omitted Fields, Cloth3D, Humanoid and PlasticShell. Public products now derive their ProjectReference closure: 25 packages with aligned versions and topological publication order. The manifest gate rejects missing dependencies; an empty-cache consumer restores and emits reimportable AP242. |
| B2: third-party native CLI footprint | Already repaired before this pass. Retain resolved-graph and actual-payload guards. The core CLI excludes SQLite, Rhino, PDFium and Skia; optional interoperability remains separate. Drawing Notes use pinned PDF.js browser assets. |
| B3: unpublished/prerelease shader graph | Confirmed and repaired. Seven managed Copeland compiler dependencies are publicly released at 0.1.0. Aetheris pins that public version; normal restore requires no sibling checkout or private feed. |
| B4: preview-only/incomplete release lane | Confirmed. Version metadata and documentation are 2.0.0. The complete Windows bundle, CLI, VSIX and public libraries are staged together. Publication requires complete Windows/Linux regression and both consumers of the exact staged bytes. Final result is recorded below. |
| S1: ignored authoring intent | Confirmed in Extrude, Assembly/Semantic, WireForm, SectionChain, value objects and instantiated recipes. Checks now belong to their actual AST/value readers, using existing field/spatial metadata before erasure or geometry work. Wrong-dimensional coordinates reject. |
| S2: validate/build routing disagreement | Confirmed for lowercase native Model. Both commands now use one domain selector and native-root spelling normalization. Legacy serialized/solid inputs keep their explicit boundary. Enclosing model identity survives to the summary. |
| S3: weak/lost diagnostics | Reproduced missing-file raw errors, wrong-unit profile coordinates and vague extreme-size failures. Errors now retain structured codes, severity, messages, stages and available source locations. Precision-incompatible dimensions reject at their owner. |
| S4: expensive engineering preflight | Retained as a documented performance limit. Orientation, pcurve and import safety are not skipped. Parse-only validation remains explicitly limited. A faster witness needs a measured kernel change. |

Compatibility decisions:

- D1: publish the complete existing graph at product versions. Cloth3D/Humanoid
  remain experimental; package alignment does not claim production maturity.
- D2: keep core execution managed and optional native interoperability separate.
- D3: use the released Copeland 0.1.0 family.
- D4: qualify Windows x64 and Linux x64 CLI use. The desktop bundle is Windows
  x64; macOS is not claimed as qualified.
- D5: retain bare millimetres only in already admitted compatibility positions.
  Angles cannot silently become lengths. Wire directions are dimensionless;
  historical bare Origin components retain millimetres.
- D6: prefer PascalCase constructs, camelCase built-in fields and lowercase
  connectives. Normalize admitted aliases at syntax positions, preserving domain data.
- D7: check materialized intent before execution. Named data publications are
  not fixed built-in schemas; unused generic recipes defer material capability
  checks until concrete specialization.

## Frontend and geometry integrity

The first broad admission probe covered 3,317 declaration bodies; the expanded
probe covered 3,752 declaration/object bodies. Eleven additional instantiated
recipe gaps were repaired at ConceptIR, profile and linear-pattern ownership.
A replay of seventeen accepted non-data cases leaves six deliberate admissions:
one named Stations entry and five edits in an unselected Plate recipe. The
Stations witness adds an actual fifth occurrence with its stable name. Five
regressions select the Plate recipe and prove each inserted field then rejects
both validate and build. No specimen was edited to disguise a failure.

Stricter checks preserve legitimate provenance metadata, direct WireForm frame
fields, Close steps, nested Struct templates, inline Profile boundaries and
typed assembly exposure aliases. Reverse as a standalone value remains
distinct from unary Reverse. Offset aliases retain their admitted meanings,
reject conflicts and require whole-field consumption.

WireForm consumes existing AST root/ordered child spans. `StartFrame Empty;`
rejects instead of crashing; `Straight Ignored;` rejects instead of disappearing.
Comments contribute no operations or provenance. No geometry algorithm,
fillet planner, tessellator or STEP orientation resolver changed.

All 380 corpus validate/build outcomes match the original baseline. The 297
successful STEP builds and seven structural cut lists remain byte-identical.
The other 83 sources are libraries/inspection inputs, garments needing body
input, or non-STEP products; they are not called STEP regressions. All 23 CRLF
specimens also retain their outcomes and geometry after source-span repair.

Diagnostics carry code, severity, explanation, source file, stage and available
span. Missing files have no invented location. Expanded/legacy producers still
disclose enclosing locations where exact provenance is unavailable; complete
expansion source maps are not claimed.

## Compiler publication and bundle repairs

Copeland source `70b6cbe742ecfb2947c9c6a31cfa8e1d3a89067b` was
[published successfully](https://github.com/yuechen-li-dev/Copeland/actions/runs/40065426723)
with NuGet trusted publishing. All 1,394 compiler tests passed. Windows produced
the seven packages and Linux consumed those same bytes. The public-only consumer
checks deterministic WGSL, changed-source semantics, graphics/depth contracts
and invalid-source rejection. Final Aetheris checks use a fresh cache after one
stale local 0.1.0 compiler DLL was discovered; no user cache was deleted.

The real complete packager exposed these handoff/runtime failures:

| Failure | Owning repair |
|---|---|
| Telos exports absent on a fresh checkout | Build the local Telos producer before path-dependency hydration. Regenerate the missing lock entry through normal TSPack update. |
| VSIX pickup read an obsolete dist path; clean runners lacked its output parent | Consume the actual artifacts/local producer output, create its parent before invoking vsce, and require a newly created package. The missing-parent case was reproduced locally. |
| Cadmata --no-browser consumed --urls | Remove the host-owned valueless switch before .NET configuration parsing. Both orders have regression coverage. |
| Schema generator inherited PublishAot | Its own project keeps this Roslyn compiler tool managed. Forge/runtime remain NativeAOT. |
| Native templates crashed on string JSON | Existing language readers/writers use explicit generated primitive metadata. |
| Native structural cut-list JSON required reflection | Its owner uses a typed generated contract; all seven managed cut-list bytes match the baseline. |
| Measurement wrapper violated React lint rules | Render the tool through state and update current props in an effect. No engineering geometry change. |

`Build-CadmataFrontend.ps1` owns the existing producer/check commands. Packaging
reuses it and CI runs it before expensive .NET work. Windows and Linux jobs are
independent; both remain required for publication. Exact-byte smoke starts the
packaged Cadmata host, fetches production assets and imports STEP, then kills
its owned process. This proves host/API wiring, not manual browser/GPU rendering.

## TSPack root causes and bounded compatibility

[Owner PR 208](https://github.com/yuechen-li-dev/tspack/pull/208) repairs:

1. npm's mixed peer range `>=3.0.0 || insiders`: registry selection and
   classification share loose-OR handling, preserving valid SemVer alternatives
   and authored evidence. All-invalid ranges reject; JSR stays strict.
2. Explicit tools bypassing the requirement tape: TypeScript ~5.9.3 now retains
   authored precedence over transitive peer requests for another major version.
3. Runner/materializer disagreement: selected-package tool directories lead
   executable lookup and child PATH, including workspace-cwd targets.
4. Dropped local package requirements: path/Git package.json runtime, optional
   and peer requirements use the existing normalized registry frontier and
   instance graph. Aliases survive; producer devDependencies stay excluded.
5. Shared-instance visibility: directly declared type providers are visible
   from instance real paths. Runtime roots retain consumer ownership and also
   project at the compatibility root only when their identity is unambiguous.
   Transitive-only packages and private tools are not promoted; conflicting
   consumer versions keep their bindings without an arbitrary shared winner.

The last rule supports application-supplied providers through ordinary Node
lookup, including tunnel-rat 0.1.2, which imports React but declares it only as
a devDependency. It does not fabricate dependency edges or rewrite upstream
metadata. Planner and writer share the placement rule. Pure/filesystem tests
prove aliases, exclusions and two distinct consumer versions.

The client fixes authored install aliases to the names imported by its configs:
tailwindcss and typescript-eslint. Locks were regenerated normally, never
hand-edited. The original full refresh changed 177/173 realizations within
authored ranges; the later local-requirement/alias repair changed 12/12.

Nested source-only rehearsals were insufficient: TypeScript tracing and Node
hooks did not intercept Rollup's independent filesystem lookup. A separate
temporary workspace with no ancestor node_modules and NODE_PATH cleared now
provides the counterfactual control. The old tool reproduces the React failure;
the repaired tool passes the real producer: check, typecheck, 96 tests, build
and lint. Only the content-addressed artifact cache was reused. Twelve emitted
payload files agree after documented checkout whitespace/cache-URL normalization;
raw differing hashes remain recorded. No byte-identical UI claim is made.

All 43 Go test packages and broad vet pass. TSPack's own existing generated CI
references a missing @v1 setup-action tag and stops before testing. Aetheris
builds the pinned source directly. Likewise, Copeland PR 95 has an unrelated
TinyFarm/sibling-Dominatus bootstrap failure; the managed compiler publication
and package consumers passed independently. Neither unrelated check is hidden
or reported as successful.

## Qualification evidence

| Gate | Observed result |
|---|---|
| Final full Linux regression | 6,001 passed, 7 skipped, 0 failed; 20 suites. Portable CLI package guard passed. |
| Final full Windows regression | 5,999 passed, 9 skipped, 0 failed; 20 suites. |
| Complete Windows producer | Clean frontend check/typecheck, 96 tests, production build and lint passed. CLI, NativeAOT Forge, Cadmata, VSIX and all 25 libraries packaged successfully. |
| Staged Windows distribution | Extracted CLI validate/build/inspect/verify, Cadmata production assets and STEP import, all 15 native templates and their advertised exports, invalid-standoff refusal, assembly inspection and PDF/PPTX drawing generation passed. |
| Same staged packages on Windows and Linux | Portable tool installation and validate/build/verify passed. All 28 asset checksums and the 25-package graph passed. Empty-cache API consumers resolved public dependencies, loaded the packaged material catalog and emitted reimportable AP242. |
| Local full regression | 6,009 passed, 0 skipped, 0 failed; 20 suites, with the scratch-test qualification below. |
| Source/geometry parity | All 380 corpus outcomes preserved, 297 STEP artifacts and 7 cut lists byte-identical. |
| Tooling owner | All 43 Go test packages and broad vet passed. The isolated frontend counterfactual and fresh-directory VSIX producer passed. All 13 extension tests passed in the final hosted producer. |
| Publication | Intentionally skipped: workflow dispatch used publish=false. Copeland compiler 0.1.0 publication is already complete. |

Managed code and project definitions are unchanged from 03ff13d0, where the
local full-suite and corpus-parity receipts were captured. Later changes repair
frontend/tooling and distribution handoffs; the final hosted run qualifies them.

The tracked suite contains 6,008 cases. Local 6,009 includes one pre-existing
ignored TessellationHoleDiagnosticsTests.cs scratch test. It was neither added
to the release nor deleted from the user's checkout. Hosted counts qualify
tracked source; all optional witnesses pass locally. The seven common hosted
skips need local 3DM assets. Windows additionally skips two external-PDF-renderer
witnesses; those run on Linux and locally.

Historical hosted failures are classified, not relabelled as success:

| Run/source | Observed stop |
|---|---|
| 40082869172 / 03ff13d0 | Complete Windows/Linux .NET suites pass; clean packaging cannot find package-local tsc. |
| 40087147044 / 80a437a7 | Complete Windows/Linux suites and frontend typecheck pass; runtime/config imports fail. |
| 40115512913 / 69a2204d | Linux full suite and Windows frontend typecheck/96 tests pass; production build refuses undeclared React. |
| 40118578194 / d53be5a4 | Both full suites, frontend and native publishing pass; VSIX production fails because its output parent does not exist. |

Raw evidence is under ignored artifacts/local/release-2-readiness. Key receipts:
final-test-summary.json; final-geometry-parity.json; cut-list-parity.json;
hosted-qualified-linux-summary.json; hosted-qualified-windows-summary.json;
hosted-qualified-smoke-linux.log; hosted-qualified-smoke-windows.log;
hosted-qualified-run.json; hosted-qualified-distribution-receipts.json;
isolated-before-build.log;
isolated-after-producer.log; isolated-frontend-normalized-parity.json;
closed-distribution-receipts.json; closed-native-templates; closed-drawing-smoke;
closed-public-consumer.log.

The downloaded final hosted artifact (ID 12572147935) independently passes all
28 recorded checksums and the core CLI payload guard. Its publication receipts are:

| Hosted artifact | Bytes | SHA256 |
|---|---:|---|
| Aetheris-2.0.0-win-x64.zip | 133,069,488 | 525900e7c00f89831249fa3cc30485544fdc1b058da7bb2163395e45bac3a174 |
| Aetheris.CLI.2.0.0.nupkg | 18,002,229 | ab578809a0c9ccc3e11004da50ff788fecd35d5c962c80c5ffd3ef715ae2e068 |
| aetheris-firmament-0.3.0-preview.3.vsix | 13,191 | cae2cb93c955d93e7e030e6bc4ea9598ab65bcdba15afc4ca9551910ee9a2eed |

The earlier local candidate 69a2204d has separate receipts in
closed-distribution-receipts.json. Extracted and fresh installed local tools emit
through-hole STEP SHA256 5999f127e5a1477e5825afa656dceb7f89241d8f85312b0e6bcd793639a0f548.
Local archive hashes are not assigned to the hosted build.

No local .NET/Cadmata hosts remain. One obsolete generated client dependency
junction was removed without deleting its target, then normal sync regenerated
the layout. No outside user files were deleted. Evidence-directory cleanup
scripts preview the exact temporary qualification copy and ten earlier native
crash minidumps; deletion requires the user's explicit -Apply invocation.

## Historical executive verdict

**Superseded automated-candidate verdict; desktop release blocked.** The four original
packaging blockers are closed, the reproduced admission/diagnostic defects are
repaired, and the complete distribution passes its real hosted production path.
Engineering parity holds for the qualified corpus.

The remaining limits are explicit: expensive engineering preflight, incomplete
expansion source maps, unqualified macOS, optional external interoperability
witnesses and NativeAOT coverage bounded to the exercised public catalog.
The original audit's help-text/output-location cleanup remains nonblocking;
this pass adds no claim of manual desktop/GPU rendering qualification.

Aetheris publication remains a separate production-gated action. No stable
Aetheris tag or package was published by this pass. The managed Copeland 0.1.0
compiler packages are published and consumed by the qualified artifacts.
