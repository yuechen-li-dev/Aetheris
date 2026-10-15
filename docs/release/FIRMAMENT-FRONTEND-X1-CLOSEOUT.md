# Firmament frontend X1 — final closeout for reviewerGPT

Date: 2026-10-14  
Implementation base: `0bf9f7da0ee4da2142f95325c415ec03902747bf`, plus the X1 closeout working-tree changes.  
Verdict: **Accepted for X1.**

## Executive finding

The specific blockers in the previous reviewer handoff are closed. Required canonical syntax is represented by the shared source-located AST, the migrated Assembly contracts use production admission, every current corpus outcome has an explicit disposition, and the final regression/artifact gate passes. This accepts X1 structural coverage and the qualified production migrations; it does not claim that X2 semantic consolidation or every remaining source adapter has been completed.

The original shadow-first proposal was superseded by the user's instruction to
migrate production admission. This closeout does that: mechanical compilation
uses its production AST binders, recognized V2 documents receive shared
structural admission before ordinary validate/build domain dispatch, and
Assembly contract/mate admission binds shared syntax into the existing domain
IR. The retained developer command's name does not make it an admission authority.
No new shadow-only milestone, whole-document regex dispatcher or geometry kernel
operation was introduced.

## The five requested closeout items

| Request | Result |
| --- | --- |
| Migrate Assembly/Interface roles and mate clauses | Role capabilities, atomic relationships, Fit/clearance/variation policies, free-motion clauses, numeric requirements, contract options, datum contracts and named role assignments bind AST nodes. Their old regex readers were removed. Existing placement/capability lowerers remain authoritative. |
| Run the current complete corpus with correct owners | 1,266 sources collected with original-source hashes and AST fingerprints; Assembly, Scene, Garment, Drawing, Analysis and Gear use their domain owners. Other sources use normal validation's existing domain dispatch. Analysis compilation is explicitly distinguished from solving. |
| Classify every mismatch | Every source has a recorded disposition. Successful Assembly dependency receipts qualify declaration modules in actual consuming contexts; standalone failures remain visible. Three accepted files under Invalid have separate measured downstream/geometry explanations. No unresolved mismatch remains in the ledger. |
| Close canonical structural gaps in X1 scope | All **327/327 Canonical sources** structurally parse. The previous Role/Lower/Mate gaps and additional canonical spelling gaps are closed. An external legacy SheetMetal test source was copied into fixtures and qualified with explicit profile target syntax. |
| Repeat regression and geometry qualification | 4,943 full-suite passes across 20 executing assemblies, zero failures/skips; 26 byte-identical STEP baseline comparisons. Additional real-path tests compare modern and compatibility contracts, instance identities and world transforms. |

## Production ownership and compatibility

The original release blocker was this legitimate canonical declaration:

```firmament
Interface<Axial> InstallCell { Role A requires AxisCapable; Role B requires AxisCapable; }
```

Previously the Assembly owner accepted it while the structural parser failed at
`requires`, on line 3, columns 39 and 68 of `executable-machine.firmament`.
It now parses and compiles through the ordinary Assembly path.

For new authoring, prefer a compiler-owned contract and a named application:

```firmament
Interface<Fixed> Install { }
// Inside the consuming Assembly:
Mate Installation: Install { A: Pair.Fixed.Mount; B: Pair.Moving.Mount; }
```

Typed families provide their normal capability requirements and lowering.
Inline typed Interface applications remain supported. Historical Role/Lower/Fit/
Allow forms are bounded compatibility and specialist contracts; they do not
define a second introductory syntax. `Interface<Custom>` remains the explicit
atomic escape hatch. No placement solver was replaced.

The new real-path fixture places the moving occurrence at **X = 15 mm**. Modern
defaults and explicit compatibility roles/lowering export identical AP242,
retain instance identities and produce the same resolved transform. Unknown
contract statements/fields, duplicate assignments, unresolved roles, malformed
capability lists, invalid option units and unsupported `on XZ` header clauses
reject before geometry can be emitted.

The structural parser also now represents constrained Template type parameters,
legacy typed parameter spelling, adjacent record literals in finite collections,
manufacturing/replacement forms, semantic-version literals, STEP entity
references, hyphenated review identities, SheetMetal extensions and explicit
CornerProfile/EdgeProfile target paths. These are local grammar additions to the
existing recursive-descent parser, not additional parser routes.

Nine canonical Sculpting files had a stray leading `+` that their old source
reader ignored. The canonical source was corrected; the original failing source
is retained as a negative regression. Normal validate/build now reject it at
line 1, column 1 and write no STEP. The nine corrected sources retain identical
engineering artifacts.

## Complete corpus accounting

| Disposition | Sources |
| --- | ---: |
| Admitted | 487 |
| AdmittedGeometryWithDfmFindings | 1 |
| AdmittedInConsumerContext | 34 |
| DeclarationModuleRequiresConsumer | 5 |
| ExpectedNegativeRejection | 282 |
| ExperimentalAdmitted | 9 |
| ExperimentalRejected | 31 |
| LegacySerializationAdmitted | 5 |
| LegacySerializationRejected | 324 |
| MalformedOrUnsupportedSourceRejected | 6 |
| MisfiledPositiveFixture | 1 |
| RejectedAtSolveStage | 1 |
| SourceAdmissionRejected | 80 |

Raw structural outcomes: **902 parsed, 364 rejected**. Raw standalone/domain-owner outcomes: **504 accepted, 762 rejected**. These totals include legacy serialization, negative and experimental fixtures; they are not a V2 success rate.

Inventory covers `.firmament`, `.firmfixture` and `.firmasm`, case-insensitively,
under the complete current `fixtures/` tree. The checked-in compact evidence
record lists every source, hash, structural outcome, owner outcome and disposition.
Full diagnostics and production payloads remain in ignored local evidence.

| Disposition | Sources |
| --- | ---: |
| Admitted | 285 |
| AdmittedInConsumerContext | 33 |
| SourceAdmissionRejected | 9 |

A successful dependency receipt proves that exact source hash in the recorded
consumer. It does not prove every possible Template specialization. Likewise,
structural success is syntax coverage, not proof of successful geometry execution.
Universal semantic parity remains explicitly unqualified.

The nine standalone canonical rejections outside consuming-module coverage are:

- Four Garment examples require an explicit figure/body binding. Their domain
  diagnostic is `garment-fit-body-required`; existing fitting tests supply bodies.
- `Canonical/Pipeline/profile-with-inner-loop.firmament` has colliding segment
  identities between its inner and outer loops. The compiler identifies the
  collision and requests aliases or renamed guide/stage identities.
- Four WireForm SurfaceCoil examples reach the current material-owner admission
  boundary. Their source combines a material carrier primitive and WireForm;
  the ordinary build rejects rather than silently omitting material. This
  closeout does not implement new combined-material/kernel behavior.

These are represented syntax with explicit domain admission outcomes, not
unclassified structural regressions. The corpus does not become “all builds
succeed” by reclassifying invalid, speculative or context-dependent input.

### The three accepted negative-path fixtures

| Fixture | Real witness and disposition |
| --- | --- |
| `Invalid/FEA/ExperimentalShell/singular-map.firmament` | Analysis compilation succeeds at its stated level; ordinary `fea` rejects with `thinwall-map-singular` because the cylindrical span is inconsistent. |
| `Invalid/SheetMetal/tray-dfm-violation.firmament` | Exact geometry compiles. `sheetmetal inspect` reports DFM **Warning**, specifically insufficient corner relief and its repair. This is not a frontend syntax negative. |
| `Invalid/Pipeline/traceloop-non-loop.firmament` | Misfiled positive source: real build and STEP reimport produce one enclosed body, six faces, bounds `(-5,-2,0)..(5,2,1)` mm. |

Those dispositions are evidence records bound to exact source hashes. They are
not compiler exceptions or a diagnostic fatality allowlist. A changed or newly
accepted negative source without matching reviewed evidence fails classification.

## Regression and artifact evidence

| Qualification | Result |
| --- | --- |
| Release solution build | Zero errors; 11 existing warnings in the final incremental solution build (the earlier full build also reported existing WASM warnings). |
| Required Core fast lane | 1,068 passed. |
| Firmament fast lane before final provenance test | 2,457 passed; the final full suite also includes the later header/provenance coverage. |
| Final focused Assembly AST/admission tests | 27 passed. |
| Full solution including SlowCorpus | **4,943 passed across 20 executing assemblies; zero failed/skipped.** |
| STEP baseline comparisons | 26 byte-identical: 13 retained earlier baselines, four freshly captured Assembly exports and nine freshly captured Sculpting exports. |
| Modern versus compatibility contract | Real M1 construction, AP242 byte equality, stable identities and X = 15 mm placement. |
| Existing plain versus Concept-prefixed shapes | 17 STEP comparisons rerun within the full suite. |

The existing FrictionLab project discovers no tests and is not counted as an
executing test assembly. Narrow lanes are not summed into the full total. Earlier
failed runs remain local reproduction evidence; the qualified final log is
`full-closeout.log`. No remote CI or new live Helios/browser interaction is claimed.

Geometry preservation is claimed for the measured baselines and regression
witnesses, not every possible program or every showcase export. The complete
owner sweep additionally exercises real canonical assemblies and scenes,
including the guitar, industrial ATLAS, house and factory.

## Diagnostics, provenance and quality review

Syntax diagnostics retain stable codes, fatal severity, explanatory messages,
original spans, line/column and construct context. Contract/mate semantic errors
remain structured Assembly diagnostics. Unsupported placements and unknown
authoring intent are rejection outcomes, not warning-only recoveries.

Dimensional `from "source"` provenance has its own syntax node. This avoids a
spelling-normalization regression that capitalized provenance as though it were
a selector link and broke tolerance chains. The regression verifies the chain
and normal compiler outcome, not just an error string.

Exact original-file source maps for the complete expanded include/Template graph
remain X2 work. Expanded Assembly scope locations are identified as such.
Included syntax errors disclose `#expanded-includes` and a location note rather
than presenting merged offsets as exact root-file offsets. A real project-snapshot
test verifies that limitation is exposed without discarding the error message.

The quality pass checked source consumption in the migrated bodies, duplicate
validation/schema ownership, unsupported headers, casing, diagnostic loss,
provenance, hidden fallback, global mutable state and regex growth. The migrated
Interface and role-Mate readers no longer use regex extraction. Existing domain
lowerers and unqualified source adapters remain explicit; no claim is made that
every legacy reader has been deleted.

`syntax.md`, `assemblies.md` and the language decision register were updated.
Preferred spelling remains PascalCase constructs/types, camelCase built-in fields,
lowercase linking words and preserved user/domain symbols. Named Mate assignments
use the declared role spelling. Compact inline applications retain the existing
`a`/`b` aliases for predefined `A`/`B` slots; these bounded aliases do not introduce
global case folding. No unresolved user policy decision blocks closeout.

## Reproduction and retained evidence

```powershell
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll frontend-shadow --corpus fixtures --production --output-dir artifacts/local/firmament-x1-closeout/corpus-closeout
python scripts/classify-firmament-frontend-x1.py --report artifacts/local/firmament-x1-closeout/corpus-closeout/report.json --downstream-evidence docs/release/FIRMAMENT-FRONTEND-X1-DOWNSTREAM.evidence.json --output artifacts/local/firmament-x1-closeout/corpus-classified.json
```

The developer collection runs real domain owners and records their validation
levels. Collection completion alone is not a compiler or release success signal.
The classifier verifies the inventory and source hashes before assigning
dispositions. It cannot make a rejected build valid.

Local evidence: `artifacts/local/firmament-x1-closeout/` contains the qualified
build/full-test logs, full corpus payloads, geometry parity manifest and downstream
witnesses. Generated STEP, logs and diagnostic dumps remain ignored under the
repository artifact policy. The adjacent compact release records preserve the
material results and source-level qualification ledger without machine paths.

Final runtime snapshot: **0 remaining dotnet/test/build hosts**, 15.0 GiB free memory and 102.8 GiB free disk. Commands ran serially with build-node reuse/shared compilation disabled. This is a snapshot, not continuous runtime certification.

## Reviewer disposition and remaining ownership

**Accept X1.** The previously unsupported canonical Interface requirements now parse and compile, complete current corpus classification is retained, and regression plus geometry-parity qualification passes. Keep the nine explicit canonical domain admission limits and the five uninstantiated noncanonical declaration modules visible; do not turn them into a claim that every source is an executable valid part.

X2 still owns complete scoped AST expansion, semantic/value consolidation,
remaining Assembly/Scene/domain source adapters and complete expansion provenance.
Those migrations should continue directly through production admission and the
existing IR owners, not through a new shadow-only milestone. X3 retains remaining
intentional syntax reconciliation, X4 the complete twelve-part authoring retest,
and geometry/kernel capabilities remain separate. The original 24 recommendation
dispositions and rationale remain in the language decision register.

## Related records

- [Compact complete corpus and geometry evidence](FIRMAMENT-FRONTEND-X1-CLOSEOUT.evidence.json).
- [Reviewed downstream witnesses](FIRMAMENT-FRONTEND-X1-DOWNSTREAM.evidence.json).
- [Original foundation report](FIRMAMENT-FRONTEND-X1.md) and [previous gate assessment](FIRMAMENT-FRONTEND-X1-REVIEW-HANDOFF.md).
- [Language decision register](../development/FIRMAMENT-LANGUAGE-DECISIONS-2026-10.md), [syntax reference](../public/firmament/syntax.md) and [Assembly reference](../public/firmament/assemblies.md).
