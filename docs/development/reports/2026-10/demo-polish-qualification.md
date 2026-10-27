# Demo polish qualification — 2026-10-26

Verdict: **Meaningful progression**. The compiler-honesty, diagnostic and local
package changes are implemented. The requested twofold cold-validation speedup
is not achieved; the unsafe temptation to weaken STEP qualification was rejected.
No packages or releases were published in this pass.

Baseline: Aetheris `1547048f`; paired HeliosCAD `85947e6`. Claude's brief reports
373 canonical sources; the current checkout contains **380**. Older release
blocker claims are historical observations, not the current baseline.

## Disposition of the seven requested items

| Item | Current finding and disposition |
|---|---|
| 1. Materialized feature accounting | Fixed. Successful mechanical routes now issue typed `(host, kind, name)` receipts before source-derived display reports are attached. A mandatory guard compares admitted Modify/Compose operations and concrete Pattern instances against them. Missing features reject normal validation and build. |
| 2. Field closure | Required Semantic, Assembly/Subassembly/expose and SectionChain owners already reject the reported fields in this checkout. Wrong-unit Point2 also already rejects. Preserve those fixes; improve unit messages and locations. The mutation probe below shows no new acceptance. |
| 3. WebGL fallback | Already implemented upstream. Sanity-qualified in the real Helios browser; no production viewport rewrite. See the paired Helios report. |
| 4. Fresh-clone SDK | Fixed in Helios: directory link, reproducible Telos prerequisite build, coherent Vite dependency resolution. Aetheris marks its intentionally dynamic runtime import for Vite. |
| 5. Cold-validation performance | **Honest stop for this item.** A bounded optimization experiment failed the target and was reverted. Retain only opt-in performance phase instrumentation. See the measured prerequisite below. |
| 6. CLI and diagnostics | Fixed remaining wrong-unit messages, field locations, structured SectionChain build diagnostics, specialized Assembly failure context and nine short help commands. Existing missing-file and lowercase-model fixes are retained and regression-tested. |
| 7. Release lane | The old preview-only/version/dependency findings were already repaired upstream. Repack the dependency-closed 25-library graph and CLI at 2.0.0, test a genuinely isolated consumer with nuget.org enabled, install the local tool, and run all six Learn lessons from an unrelated working directory. Local candidate source mapping prevents an existing remote 2.0.0 from hiding a broken candidate. |

## Compiler accounting contract

The admitted expansion supplies expected operations; the materializer supplies
receipts only after its geometry route succeeds. Ordinary section-stack operations
use the slabs' active operation identities. Semantic holes, slots and blind drills
also require realized topology correspondence. Profile-boundary finishes report
their actual host rather than receiving an exemption. Imported geometry and PMI
do not invent material operations. A successful Pattern needs every unique
expanded instance and the admitted expansion count.

`Features`/`EngineeringFeatures` remain useful display metadata, but cannot prove
that a feature was built: some are attached by reading source. `build --json`
therefore exposes the independent `materializedFeatures` ledger. Errors retain
`firmament-feature-not-materialized:Kind:Name` in the diagnostic message and use
the common structured diagnostic presentation. Original declaration spans are
preferred; unavailable expansion provenance is disclosed at the enclosing owner.

The guard is not a second geometry engine. It proves that the owning route
consumed the admitted operation, while existing geometric and interchange checks
prove that route's output. Future routes must report their actual executions.

Qualification found and repaired two accounting mistakes before acceptance:

- Two different feature kinds can legitimately share a name on one host. A Hole
  and EdgeFinish named Mount now require separate typed receipts; either missing
  receipt rejects.
- `Remove` with `role: ThroughCut` remains a Remove operation. It must not be
  relabeled Pocket merely because it shares that semantic role. Both existing
  Pipeline fixtures are explicit regression cases. Through-Pocket lowering now
  retains its existing SemanticFeatureKind metadata independently of that role.
  Admitted lowercase construct aliases compare as the same kind while host and
  feature names remain case-sensitive; one omitted feature yields one error.

A legacy composed rectangular-boundary box rebuild cannot preserve additional
cavities or height-changing operations. It now rejects that combination with
`ComposedBoundaryRebuildUnsupported` and directs authors to the section-stack
Top/Bottom EdgeFinish route. It does not silently rebuild only the stock box.

The reversion witness constructs the old bare Extrude output, attaches even the
full source-derived display reports, and submits it against source declaring a
hole. The guard rejects it at the original Mount declaration, including when a
library Use precedes the source. Separate tests cover wrong hosts, missing Pattern
instances, Box, Extrude, Compose, coaxial Compose, profile finish, hollow
perforation and imported STEP/PMI routes. These are behavior/geometry tests, not
exact-sentence snapshots.

## Field and diagnostic reproduction

The old Learn/06 examples now reject unknown Extrude fields, Semantic members,
Assembly root fields, SectionChain Point2 fields and explicit `deg` in a Point2
length vector. Field rejection precedes geometry execution. Normal lowercase V2
`model` validates and builds through its actual domain owner.

For explicit unit failures the diagnostic names construct, declaration, field,
expected unit and received unit. Extrude `to: 8deg` and SectionChain
`position: [2mm, -2deg]` are tested through both real CLI commands, including the
field location. Unitless values retain only the aliases/implicit-unit rules
already admitted by their owner. Ambiguous or generated locations are not invented.

Build failures for SectionChain now use structured objects equivalent to validate;
Assembly specialization preserves the original diagnostic and message, marking
expanded-definition offsets explicitly. Missing-file text is short and exits 1;
JSON remains structured. Help now supplies purpose, usage, options and an example
for format, mesh, lock, measure, inspect-3dm, recover-3dm, fea, drawing and scene.
View help describes both Firmament builds and existing STEP.

### Unknown-field probe

`scripts/qualify-unknown-fields.py` mutates actual brace token boundaries outside
comments/strings and invokes the real CLI. It copies only repository-contained
relative resources into ignored evidence directories. A failing control never
counts as successful field rejection.

All **322** baseline-valid seeds have passing resource-complete controls. Both
before and after runs contain **3,449** mutations, **66** accepted mutations and
no timeouts. All final accepted cases were rechecked after the typed-ledger fix:
66 accepted, no outcome changes.

| Runtime owner | Before accepted | After accepted |
|---|---:|---:|
| UnifiedMechanical | 42 | 42 |
| Assembly | 5 | 5 |
| UnifiedSectionChain | 16 | 16 |
| Garment | 1 | 1 |
| UnifiedRevolve | 1 | 1 |
| WireForm | 1 | 1 |

These are not 66 ignored built-in fields: **60** add legitimate named Concept
Struct publications, **one** adds a legitimate named station, and **five** occur
inside an unused Plate recipe while the source instantiates Disc. Recipe material
checks are specialization-dependent. Required closed field owners have no
accepted mutation in this run.

The token probe does not exhaust value-object expression syntax. A separate
minimal witness, `fixtures/Regression/FeatureAccounting/value-object-plane-known-gap.firmament`,
confirms that an extra field in an unused published inline Plane value still
accepts. This is an explicitly excluded value-object closure gap, documented in
`docs/public/reference/language/compatibility.md`. Existing WireForm operation
field closure is stronger than the old brief; it was not rewritten here.

## Performance: measured limit, no weakened verification

Fresh-process observations below were collected during qualification and are
single samples under competing load, not a controlled speed benchmark:

| Input | Baseline seconds | Experimental candidate seconds |
|---|---:|---:|
| WireForm/axis-coil validate | 10.422 | 8.375 |
| WireForm/helix validate | 10.109 | 7.797 |
| Assembly/coil-on-stem validate | 9.203 | 11.328 |
| GuitarX0 cold build | 12.906 | 9.078 |

The candidate used bounded coarse orientation witnesses only on the immediate
exporter-owned roundtrip, with disagreement falling back to full qualification.
It did not meet the requested 2× reduction, and the assembly sample regressed.
**All candidate behavior changes were reverted.** These are not final shipping
performance gains.

A separate real compiler profile measured 8.609 s total: 1.891 s in orientation
qualification, 5.323 s in pcurve recovery/qualification, and 7.647 s in STEP body
import overall. Even deleting orientation entirely cannot meet the target.
Existing recovery already caches seeds; dense spline lift qualification still
performs 8,193 samples per coedge. Added BuildPerfTrace scopes expose the two
costs without changing admission or STEP output.

The smallest justified next performance project is proof-preserving pcurve
qualification optimization or producer-owned trim/coedge witnesses across the
export/reimport boundary. Skipping external STEP checks, weakening tolerances or
adding a public trusted-import switch is not a demo-polish fix.

## Final qualification

Final results and evidence are recorded under ignored
`artifacts/local/demo-polish/`; see the completion addendum below. Earlier
interrupted or intermediate runs are not the acceptance evidence. The full
corpus compares **both** validate/build exit status and successful STEP SHA-256,
not just compilation success. Existing recipe fragments, domain-specific exports
and known invalid/speculative sources retain their baseline classification. The
25 baseline-valid sources declined by this STEP-output command are seven Scenes,
seven Garments, and eleven Drawing/Analysis artifact-directory products. Their
diagnostics request the correct export command or output directory; they are not
frontend admission contradictions.

No source migration, kernel construction change, new parser route, global error
allowlist or global rewrite was introduced. A dedicated diff review checked
receipt provenance, diagnostic constructors/locations, schema duplication,
source/build ownership and process cleanup.

### Completion evidence

- `final4-build.log`: full Release solution build, zero errors.
- `final4-focused.log`: 59 accounting/X4 tests passed; `final4-concept-cli.log`:
  four real CLI semantic-hole tests passed.
- `final4-fast.log`: 1,111 Core tests passed.
- `final4-full.log`: **6,099 passed, zero failed, two existing Drawing skips**,
  across 20 nonempty test projects. The intentionally empty FrictionLab project
  remains empty. The full test command exited 0.
- `final4-corpus/summary.json` and `differences.json`: **380 sources**, 322
  validate successfully, 297 STEP builds succeed, **zero exit/hash differences**
  against the pristine baseline; no timeouts. Intermediate failed/superseded
  qualification runs are not counted as final evidence.
- The separate through-Pocket regression's STEP is byte-identical between
  `baseline-cli` and `final4-cli`, despite its newly retained kind metadata.
- `fields-before`, `fields-after`, `fields-qualified-accepted.json`: the full
  before/after 3,449-case probe and final-binary recheck of all 66 accepted cases.
- `packages-qualified.log`: 25 dependency-closed public libraries at 2.0.0.
  `consumer-qualified.log`: fresh isolated restore with candidate/nuget.org source
  mapping, build and execution succeed, including packaged material catalog and
  a reimportable STEP API result.
- `tool-install-qualified.log` and `qualified-lessons/summary.json`: the locally
  installed 2.0.0 tool validates/builds all six Learn lessons from the system temp
  working directory; all six STEP hashes match the pristine corpus. The installed
  Firmament assembly matches the candidate tool ZIP, excluding stale-cache success.
- `clean-bootstrap-qualified.log` / `clean-browser-qualified.log`: source trees
  refreshed with the final patch, generated SDK/Telos dist and JavaScript
  dependency trees removed, README bootstrap succeeds, and all three real `/local`
  WebGL fallback tests pass. **This is clean Windows source-tree qualification,
  not the requested clean Linux container.** Docker's daemon was unavailable and
  the installed WSL environment had no Linux Node/.NET toolchain; that platform
  acceptance remains unqualified.
- Paired frontend/backend logs are summarized in their own reports. Test hosts
  are temporary process-owned hosts; no service/startup hook was installed.

Remaining work is bounded and explicit: the unachieved cold-validation target,
the excluded nested value-object closure family, and Linux-container bootstrap
qualification. Existing accepted engineering geometry is preserved in the tested
corpus; this report does not claim static validation prevents every runtime
geometry failure.
