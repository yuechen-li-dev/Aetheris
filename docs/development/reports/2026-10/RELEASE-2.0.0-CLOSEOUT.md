# Aetheris 2.0.0 release qualification

Source baseline: `b05cca40ec3efd7540c647398892953299d2c136`.
This follows [Claude's blocker audit](RELEASE-2.0.0-BLOCKERS.md), preserving its
findings rather than replacing them with an assumed green status.

## Disposition and decisions

| Finding | Reproduction | Repair or decision |
|---|---|---|
| B1: incomplete public dependency graph | Confirmed: the fixed sixteen-package list omitted Fields, Cloth3D, Humanoid and PlasticShell. | Public products now derive their ProjectReference closure. A manifest gate checks identities, aligned versions, missing dependencies and publication order. The local rehearsal packed 25 packages; an empty-cache consumer restored and produced reimportable STEP. |
| B2: native CLI footprint | Already fixed before this pass. | Retain the native-asset build/pack guards. Drawing Notes use PDF.js; SQLite and 3DM are optional adapters. Do not reintroduce PDFium/Skia to support raster previews. |
| B3: unpublished/prerelease shader graph | Confirmed. | Publish all seven managed Copeland compiler dependencies at 0.1.0 and pin Firmament to that public version. Remove automatic private-feed injection and CI sibling checkout. |
| B4: preview-only release lane | Confirmed; full bundle remains blocked. | Product/SDK/Cadmata metadata is 2.0.0. The lane derives its version, checks tags and gates publication on exact staged-byte smoke tests. Windows full regression now passes, but a clean Cadmata producer exposes the TSPack lock/resolution blocker below. No stable release is claimed. |
| S1: ignored intent | Confirmed: Extrude, assembly root/Semantic, WireForm, SectionChain Point2/Line2/Path/Loop and wrong coordinate units. | Consumption checks belong to their actual readers, using shared AST fields and existing spatial metadata. Also close occurrence fields, standalone port publications, Span fields, Modify fields, prism offsets, semantic Require and projected PMI before their erasure. |
| S2: validate/build route split | Confirmed with lowercase native model: rejected validation, successful build and wrong summary owner. | One command-level domain selector; case-only native root normalization; existing lowerers retain execution authority. Legacy solid grammar remains separate. |
| S3: diagnostics | Reproduced missing-file raw errors, wrong units and vague scale failures. | Missing-file failures now use the structured frontend diagnostic shape. Extrude dimensions identify required lengths; precision-incompatible primitive dimensions reject at the field owner. Wire field errors include readable explanations. Capability errors remain distinct from syntax failures. |
| S4: expensive engineering preflight | Retained as a performance limitation. | Do not skip orientation, pcurve or import safety to shorten validation. A faster orientation witness needs its own measured kernel change after release. Parse-only validation remains explicitly limited. |

D1: publish the existing complete graph at product versions, with experimental
status retained in the Cloth3D/Humanoid documentation. Avoid a release-time module
split that would alter current compilation ownership. Fields and PlasticShell
retain their existing bounded contracts.

D2: keep the core managed and optional native interoperability separate. This
removes the reason to split the core tool into third-party-native RID packages.
D3: use the published stable Copeland 0.1.0 family, not the private preview feed.
D4: qualify Windows x64 and Linux x64 CLI use; the desktop ZIP targets Windows x64.
Do not claim macOS qualification from portability alone.

D5: retain bare millimetres only in already admitted compatibility positions;
explicit angles cannot become lengths. WireForm directions are dimensionless,
while historical bare Origin components retain millimetres. D6: PascalCase
constructs, camelCase built-in fields and lowercase connectives are preferred;
established aliases normalize at syntax positions without renaming domain data.
D7: close materialized built-in intent rather than disclaim silent geometry loss.
Unused reusable recipes still defer material capability checks until concrete
specialization; authored Record/Concept publications and named station keys are
not a fixed built-in field dictionary.

## Managed compiler publication

Copeland release source: `70b6cbe742ecfb2947c9c6a31cfa8e1d3a89067b`.
[Publication run](https://github.com/yuechen-li-dev/Copeland/actions/runs/40065426723)
built the package family on Windows, consumed the same package bytes on Linux,
and published with NuGet trusted publishing. All 1,394 compiler tests passed.
The isolated public-only consumer checked deterministic WGSL, changed-source
semantics, graphics stage/depth contracts and invalid-source rejection.

[Release tooling PR](https://github.com/yuechen-li-dev/Copeland/pull/95) is separate
from the already completed publication. Its unrelated TinyFarm check currently
fails because Aurelian.Combat references a sibling Dominatus checkout absent on
the runner; the managed compiler release jobs do not use that game graph. This
pass does not bypass that check or rewrite the game integration.

A local cache audit found one stale Copeland.TS 0.1.0 DLL. Final Aetheris
qualification therefore uses a fresh public NuGet cache, not the global cache.
No user cache files were deleted.

## Compatibility and diagnostic contract

Initial stricter checks exposed legitimate provenance metadata, direct WireForm
Origin/Tangent/Up fields, Close path steps, nested Struct wire templates, inline
Profile boundary declarations and typed assembly exposure aliases. Each remains
admitted by its actual owner. No fixture was edited to make a check pass.

The parser now distinguishes the standalone Reverse value from unary Reverse
with an operand. This preserves the orientation of geometric Spans when an AST
field reader replaces text extraction. Offset aliases retain Along/Axis/Direction
and Termination/Extent, reject conflicts, and require whole-field consumption.
No geometry algorithm, fillet planner, tessellator or STEP orientation resolver
was changed.

The final owner review also reproduced a malformed `StartFrame Empty;` crash.
It now rejects with `wireform-start-frame-invalid` in both real CLI commands,
retaining an explanation and producing no STEP. This is a reader guard, not a
new fallback or recovery interpretation.
The follow-up operation check also reproduced `Straight Ignored;` being omitted
when followed by a real operation. WireForm now consumes the existing AST's
root and ordered child spans, instead of rediscovering execution statements by
regex. Missing bodies, unsupported headers and multiple wire products reject.
A commented-out operation contributes neither geometry nor provenance, and its
accepted STEP is byte-identical to the control. The geometry state machine and
coil/knot construction algorithms remain unchanged.

CLI diagnostics carry code, severity, explanation, source file, stage and source
location when available. Missing-file diagnostics have no invented span. Legacy
and expanded producers still disclose enclosing locations where exact provenance
is unavailable; this release does not claim a complete expansion source map.

## Qualification evidence

Raw evidence defaults to ignored `artifacts/local/release-2-readiness/`.
The retained `qualify-release-corpus.py` runs real validate/build commands and
compares STEP hashes with a frozen baseline. It records target-specific refusal
rather than misclassifying Scene/Garment mesh workflows or Drawing/Analysis
artifact-directory requirements as STEP regressions.

`test-support/ReleaseAdmissionProbe` inserts fields at AST-owned declaration and
object boundaries, uses the real CLI, and deletes temporary sibling specimens in
finally blocks. Accepted probes require classification: authored publications,
named mappings and unused definitions are not automatically correctness defects.
Replay fails if it accidentally matches no specimens.

The first broad run covered 3,317 declaration bodies. An expanded run then
covered 3,752 declaration/object bodies. It caught eleven additional accepted
cases in instantiated recipes: semantic Struct roots and EdgeFinish, Feature
return holes, private Profiles and Axis2. Those are closed at ConceptIR,
profile-binding and linear-pattern ownership, before erasure or geometry work.

A replay of all seventeen accepted non-data cases now leaves six deliberate
admissions: one named Stations entry and five edits inside the unselected Plate
recipe in local-closed-boundaries. The Stations witness compiles both variants:
adding the named entry increases realized part occurrences from four to five,
with the new stable name retained. Five regression cases select that previously
unused Plate recipe and confirm each inserted field rejects validate and build.
These are explicit classifications, not a blanket exemption for Template bodies.

The final public-byte build passed 6,007 tests across twenty suites, with zero
failures and zero skips; the optional external PDF renderer was enabled. The
fast kernel lane passed 1,088 tests. Sixty focused CLI/edge-finish cases also
pass. Final hosted release qualification remains a gate. All completed
380-file comparisons retained identical exits and byte-identical STEP
for all 297 successful engineering builds. The remaining 83 files comprise
library/inspection sources, garment sources needing body input, and non-STEP
products; they are not relabeled as geometry regressions.
After the CRLF repair, all twenty-three CRLF corpus specimens were replayed
against the original baseline: identical command outcomes and STEP bytes.

The real product-path rehearsal also generated a production PDF/SVG/PPTX drawing,
a warehouse GLB and cantilever analysis artifacts. Raw evidence is retained
alongside the corresponding source and command results.

Kernel source qualification is complete at `c86282e5a15863203633d62b8960d111632e455c`.
No Aetheris stable tag or package was published by this pass. Full distribution
qualification remains blocked as described below.

| Lane | Result | Boundary |
|---|---|---|
| Local complete solution | 6,007 passed; zero failed/skipped | Includes optional PDF renderer and locally available 3DM witnesses. |
| Linux CI, final kernel source | 5,999 passed; seven skipped | Seven local 3DM fixtures unavailable on the runner; those pass locally. [Run](https://github.com/yuechen-li-dev/Aetheris/actions/runs/40074676010). |
| Windows CI, preceding kernel candidate | 5,993 passed; nine skipped | Before the final WireForm closure. Seven local 3DM fixtures and two optional external-renderer tests unavailable; all pass locally. The CRLF and timeout failures are closed. [Run](https://github.com/yuechen-li-dev/Aetheris/actions/runs/40072419493). |
| Canonical source replay | 380 matched outcomes; 297 byte-identical STEP builds | Final kernel source, compared with the original baseline. |
| Public graph and standalone consumer | 25 packages; empty-cache restore/build/API invocation passed | Repository MSBuild imports disabled; Forge output reimports as AP242. |
| Installed managed tool | Version 2.0.0; validate/build/verify passed | Fresh tool installation; 17,993,291-byte nupkg; no third-party native assets. Its through-hole STEP matches the baseline SHA256. |

The first Windows release rehearsal exposed CRLF assertion offsets being used
against original source after LF normalization, and a stable-key test mutation
that assumed LF. Compilation now preserves authored line endings instead of
discarding those source offsets; six explicit LF/CRLF assertion cases and a
byte-identical STEP witness cover the repair. The key-order test normalizes its
own mutation input. Hosted qualification also serializes xUnit collections (in
addition to test assemblies), so independent corpus tessellations do not contend
against one another's bounded execution budgets. No product budget is enlarged.

## Code-quality review

Consumption was checked at the existing field readers before their rewrites.
There is no new error-code fatality list, whole-document parser dispatch or
geometry strategy. Command routing is shared and delegates to existing owners;
spatial fields reuse existing metadata. Temporary probe files are cleaned in
finally blocks; parse state and product runtime state were not made global.

The package verifier now reads dependency nodes regardless of the NuGet XML
namespace. A real package manifest mutated to depend on `Aetheris.NotPublished`
is rejected; the real 25-package graph passes. The consumer smoke disables
repository Directory.Build props/targets, uses only staged packages plus
nuget.org and an empty cache, then invokes the public Forge API and reimports
its STEP. This prevents a repository-local build from masquerading as package
qualification. The desktop smoke is configured to invoke the actual NativeAOT
host, but that hosted gate has not run because the frontend producer is blocked.

## Cadmata's TSPack graph repair

The Windows rehearsal reached packaging after its complete .NET suite passed.
It stopped at `tspack sync` with `TSPACK_IR_INVALID_SOURCE`: the pinned 0.1.8
rejects the legitimate sibling `../Aetheris.Web.Runtime/telos` path. The build
prerequisite is now pinned to released 0.1.14-m80i.2, source
`c830bd678f3e74700f89851af4f394d5a4054012`, which accepts that bounded path.
Its Windows release archive was checked against SHA256
`2b242166f3a112e0cd254ef984844b3eb889124d7c0344462bb7a295e33a395b`.

Telos's ignored `dist` exports must also be built before hydration on a clean
clone. Its existing npm lock/build producer now runs explicitly with install
lifecycle scripts disabled. This does not introduce an npm lockfile for Cadmata.

That removes the old source-path stop and exposes the next, genuine problem:
the committed `ts-lock.toml` contains no Telos entry. Sync does not add the
missing dependency; Cadmata typechecking accurately refuses the missing module.
`tspack update @aetheris/three-telos` refuses because targeted update supports
npm sources only. A full update fails with `TSPACK_RESOLVE_NPM_INVALID_RANGE`
on `tailwindcss`'s `>=3.0.0 || insiders` peer range. npm's own semver control
accepts its `>=3.0.0` branch in loose mode and satisfies 3.4.17. The dry-run also
reports TypeScript 7.0.2 peer selections despite the declared ~5.9.3 tool; no
resulting lockfile or dependency upgrade was applied.

Raw evidence: `tspack-sync-new.log`, `cadmata-typecheck.log`,
`telos-lock-plan.json`, `cadmata-lock-plan.json`, `npm-semver-control.json` and
`hosted-windows.log` under the evidence root. The incomplete UI checks were not
removed or replaced by a hand-edited lockfile, npm overlay or placeholder module.

The user authorized repairing TSPack and retaining the full bundle. The owner
repair is [TSPack PR 208](https://github.com/yuechen-li-dev/tspack/pull/208),
source `e92af3f529e84d97fd0a26a52ec244bf0478bfee`. npm's mixed OR peer range
now keeps its valid SemVer alternatives in both version selection and
requirement classification. All-invalid ranges still reject, prerelease
exclusion remains, JSR stays strict, and authored range evidence survives.
Explicit tools now enter the existing requirement tape instead of bypassing
it; TypeScript ~5.9.3 controls its environment ahead of transitive peers.
There is no new resolver or dependency override layer.

All 43 Go test packages pass. Vet passes for the modified owners; broad vet
separately reports four pre-existing lock-copy diagnostics in materialize tests.
The manifest frontend and embedded executable build successfully. Aetheris's
workflow builds that exact source commit, identified with build metadata rather
than pretending this is a newly published TSPack release.

The lockfile was regenerated through normal `tspack update`, then hydrated with
`sync`. Targeted path update remains unsupported; it was not necessary to add
another update mode. The full refresh adds 177 and removes 173 registry/local
realizations, updates one existing package's metadata, and records requirement
and module-instance evidence. This includes dependency updates within the
authored ranges, not just Telos; no lock records were hand-edited. Cadmata's
typecheck, 96 tests, production build and lint all pass through the embedded
candidate. New lint checks exposed ref access during render in the measurement
wrapper; the rendered tool is now state and latest props update in an effect.

Raw continuation evidence: `cadmata-lock-plan-tools-fixed.json`,
`cadmata-lock-update.log`, `cadmata-sync-fixed.log` and
`cadmata-*-qualified.log`. The complete hosted bundle rehearsal is next.
Its extracted-byte smoke now starts Cadmata on a temporary loopback port,
checks production asset responses and imports the emitted STEP through its
actual document API, then terminates its owned process. This qualifies host
wiring; it does not claim browser interaction or GPU rendering from HTTP alone.

The first real-process rehearsal exposed `--no-browser` consuming the following
`--urls` option inside .NET's configuration parser. Cadmata now removes its own
valueless switch before passing arguments to that parser. Both switch orders
bind the requested ephemeral loopback port in regression tests (eight startup
tests pass). The rebuilt-host smoke serves three production assets and imports
the STEP witness. Final qualification still uses the actual extracted ZIP.

The extension's sync/check, typecheck, 13 tests, build and VSIX packaging also
pass with the pinned tool. Its producer writes to `artifacts/local`; the bundle
script's stale `dist` pickup was repaired to consume that actual artifact. A
fresh package is required before staging, so an old VSIX cannot hide failure.

## Executive verdict

**Meaningful progression.** The managed compiler family is publicly released;
the Aetheris public graph restores; known frontend honesty findings are closed;
full kernel tests and geometry parity pass. A full Aetheris 2.0.0 distribution is
not yet qualified: the TSPack blocker and local frontend checks are closed,
but final hosted Windows packaging and extracted-bundle smoke remain gates.
Keep stable publication closed until those exact staged bytes pass.
