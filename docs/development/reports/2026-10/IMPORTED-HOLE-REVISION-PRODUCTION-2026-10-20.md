# Imported through-hole revision: bounded production closeout

Outcome: **Success for the bounded geometry-only through-hole move contract**.
The original NIST CTC-01 source now builds through unified Firmament admission,
retains its residual BRep, and exports a moved semantic Hole with qualified STEP
roundtrip. This is feature qualification, not approval of CTC-01 for manufacture.
Original vendor PMI is expressly excluded with the user's approval; its safe
migration requires a separate design.

## Motivating witness

Source: `testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp`.
Working-source byte SHA256: `85a5752da05f53c456ca3a9e038c90358e1d5a3141d1f0d6e5f0970f2356e821`.
Repository LF / explicit textSha256: `341cea888fb316673ed683e59391c11b4f96cff2542d9387e135f06682dbd745`.

The [golden source](../../../../fixtures/Regression/InlineStep/production/ctc01-hole-move.firmament)
selects original STEP walls #764/#765 (sequential faces 1/2), proves their isolated
round through-hole, and moves its center from (-160,45,0) to (-130,45,0) mm.
Diameter remains 25 mm and depth 50 mm between Z=-50 and Z=0. Entry is sequential
face 98; exit is face 3. The authored envelope is min(-175,30,-50), max(-115,60,0) mm.

| Evidence | Result |
| --- | --- |
| `validate` | valid, validationLevel Built, parserRoute UnifiedMechanical |
| Normal `build` | Ctc01Revision; ThroughHoleSuppressionReinsertion |
| Source/successor topology | 117 faces, 318 edges, 206 vertices |
| Surviving identities/geometry | 115 faces, 312 edges; surviving vertices, trims and bindings unchanged |
| Retired/new wall IDs | 1/2 retired; 118/119 created |
| Unrelated boundary exclusion | 113 faces excluded from the stock envelope |
| STEP reimport | Qualified; 636 pcurves, zero missing |
| Measured maximum lift discrepancy | about 0.00335475 mm, under explicit 0.01 mm source budget |
| New semantic PMI | diameter 25 mm, depth 50 mm, associated to both replacement walls |
| Original STEP | bytes unchanged; source PMI excluded explicitly |

The comparison uses actual CLI wireframe projections of the original source and
compiled successor. Its outline is preserved; only the selected local hole moves.
The image and detailed diagnostics remain in ignored
`artifacts/local/imported-hole-production/` and are reproducible below.

## Root causes and repairs

The previous HostSize replacement reconstructed a Box instead of preserving the
imported residual body. That compatibility route remains clearly identified as
`residualSurgery: false`. Pinned revisions now have an exclusive bounded owner;
they cannot select that rebuild or silently ignore additional Modify operations.
The new Core operation proves constant stock, validates intermediate suppression,
materializes analytic hole geometry from authored dimensions, and reinserts it
with fresh topology identities. Input data is immutable throughout refusal/success.

Modern InlineStep/Recognize/Region/Replace fields now bind through the existing
unified AST and `FirmamentSyntaxFields`. Obsolete modern recognition/replacement
regex readers were removed. PascalCase compatibility fields and historical `With`
remain admitted; new examples use camelCase and lowercase `with`. Unknown fields,
malformed evidence and wrong object/header types reject with source locations.

The loader hashes the same immutable input it decodes/imports. A stale pin rejects
before using entity selectors. Raw `sha256` remains byte-exact. The portability
audit found that the current worktree is CRLF while Git stores this corpus as LF.
The golden fixture therefore uses explicit `textSha256`: decoded text encoded as
UTF-8, with CRLF/CR changed to LF and no other normalization. Both the pin kind and
actual input byte hash are reported; there is no implicit hash fallback and the
original STEP is not rewritten. Tests cover LF and CRLF resource snapshots, raw
byte pinning and conflicting modes. Browser resources use their UTF-8 snapshot
text, with no host-filesystem fallback.

CTC's 32 original missing pcurves had two causes. Four straight cone generators
encountered a singular U coordinate at their exact apex; recovery now uses the
one-sided interior angle and validates its surface lift. UV closure ignores the
undefined angle only at the exact apex, while keeping 3D endpoint checks.
The other 28 original polynomial curves actually depart from their supports by
about .0034 mm. They cannot satisfy .001 mm without changing source geometry.
The fixture therefore authors `importTolerance: 0.01mm`, matching the source's
own declared accuracy. Strict .001 mm still rejects. No curve/surface is refitted,
no preflight is disabled, and default import/recovery tolerances remain unchanged.
STEP export carries declared distance accuracy instead of claiming native precision.

Source annotations require `geometryOnly: true` in this first pinned revision
family, with a structured, source-located warning. This intentionally covers
vendor graphs unknown to the bounded PMI inspector. Normal unpinned compatibility
imports keep their existing boundary. No imported annotation passthrough is claimed.

The new Hole projects diameter/depth PMI on its fresh walls. Additional authored
PMI resolves through the admitted source-to-successor topology map rather than
stale STEP provenance on retired walls. Shared imported-PMI target validation
rejects missing/wrong-kind targets. Explicit lowerer-owned generated-PMI receipts
let the existing final parity check verify authored and generated records together,
including builds with no authored Pmi block. The regression caught and closed both
the count mismatch and the otherwise unassociated explicit dimension.

## Public contract and diagnostics

See [authoring](../../../public/firmament/imported-feature-recognition.md),
[syntax](../../../public/firmament/syntax.md), and
[ownership](../../architecture/imported-hole-revisions.md).
`importTolerance` is numerical qualification accuracy, not a manufacturing `tol`.

Admission requires one qualified closed oriented body, a complete inward coaxial
wall group, two circular inner rims, two parallel planar supports, unchanged
size/depth, a Point3 on the selected support and a clear typed `within` prism.
Conservative bounds include the numerical uncertainty budget. Unknown bounding
families or possible intersections fail; they do not invoke arbitrary Booleans.

The shared validate/build path retains code, severity, message and AST location.
Important code prefixes include `firmament-inline-step-source-pin-mismatch`,
`firmament-inline-step-revision-required`,
`firmament-imported-hole-source-semantics-unsupported`,
`firmament-imported-hole-import-unqualified`,
`firmament-imported-hole-evidence-mismatch`,
`firmament-imported-hole-revision-rejected`, and
`firmament-imported-hole-roundtrip-unqualified`.
`firmament-inline-step-source-semantics-excluded` is a warning, not a waiver.

Build JSON exposes `importedHoleRevision`: strategy, source pin and hash kind, actual byte hash, exclusion choice,
accuracy, envelope, retired/new walls, support faces, dimensions, retained counts
and face correspondence. The existing migration report now includes actual
imported topology counts when the admitted runtime body is available. The summary
names the enclosing Model and counts the imported revision as one feature.

## Qualification

| Closing gate | Evidence |
| --- | --- |
| Release solution build | Passed, zero errors |
| Core fast lane | 1,087 passed |
| Complete solution lane | 5,838 passed across 20 test assemblies; zero failed/skipped tests |
| Focused imported frontend/compatibility | 77 passed (23 new, 54 existing) |
| Focused Core revision cases | 7 passed |
| Production kernel harness | 8/8 expected outcomes |
| Real CLI golden validate/build/analyze | Passed; one feature, qualified successor, semantic size/depth PMI |
| Real CLI strict .001 mm validate/build | Both rejected; no STEP emitted |
| Source integrity | Working byte hash remains unchanged; LF/CRLF text-pin and strict byte-pin tests pass |
| Whitespace/content checks | git diff --check and public documentation link qualification passed |

Existing nullable-analysis and WASM SQLite-varargs build warnings remain; this
feature does not claim to repair those unrelated warnings. FrictionLab's test
project currently discovers no tests; the 20 assemblies above are those with
executed tests. Existing V8 and other solution regression projects passed.

Raw evidence is under `artifacts/local/imported-hole-production/`:
`closing-solution-build.log`, `closing-fast-tests.log`, `closing-full-tests.log`,
`pin-focused-tests.log`, `closing-tool-tests.log`, `kernel-cases/cases.json`,
`validate-final.json`, `build.json`, `analyze.json`, `strict-validate.json`,
`strict-build.json`, `qualification-summary.json`, `ctc01-moved.step`,
`before.svg`, `after.svg`, and `comparison.png`. The source file is unchanged.
The intermediate doc-link failure was corrected by completing the linked closeout
before the closing full run; it is not waived.

Focused tests cover real source geometry, strict/explicit accuracy, 1025-sample
trim checks, unchanged source and residual identities/geometry/pcurves, fresh wall
IDs, semantic PMI associations, explicit PMI remapping, qualified STEP roundtrip,
validate/build agreement and source-located failures. All 54 pre-existing InlineStep
cases remain part of the focused compatibility lane.

The fixture harness now consumes the production Core owner rather than retaining
another edit engine. Its eight cases are: three accepted (30 mm move, overlapping
5 mm move and no-op), five rejected (collision, breakout, non-hole, blind and lobe).
Every accepted case must roundtrip as qualified; every case checks immutable input.
Original strategy-comparison reports remain historical investigation evidence.

No new parser route, global source rewrite, diagnostic fatality allowlist or
Boolean framework was introduced. There is one deterministic qualified surgery
strategy. The source pin, annotation policy and import tolerance cannot be bypassed
by standalone pinned re-export. The code-quality pass also checked original-source
provenance, map inverses, shared geometry, stale construction caches and generated
PMI accounting. Full interchange qualification is required because the apex and
export accuracy fixes are shared Core behavior.

### Reproduce

From the repository root in PowerShell:

```powershell
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus" -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1
dotnet test Aetheris.slnx -c Release --no-build -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1

dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate fixtures/Regression/InlineStep/production/ctc01-hole-move.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Regression/InlineStep/production/ctc01-hole-move.firmament --output artifacts/local/imported-hole-production/ctc01-moved.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll analyze artifacts/local/imported-hole-production/ctc01-moved.step --json

dotnet build tools/Aetheris.ImportedHoleExperiment -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet tools/Aetheris.ImportedHoleExperiment/bin/Release/net10.0/Aetheris.ImportedHoleExperiment.dll testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp qualify artifacts/local/imported-hole-production/kernel-cases
```

The harness README documents inventory, single moves and explicit import budgets.
The CLI `wireframe` command produces the before/after SVGs for visual inspection.

## Remaining limits

This release does not admit non-Z frames, resizing, blind holes, breakouts,
intersections, threads, blended entries, counterbores/countersinks or multiple
revisions in one Model. Source PMI migration and a recognition/editing UI need
separate designs. Conservative whole-support bounds may reject safe moves in
other shapes. CTC is a real complex golden witness, not a claim that every STEP
producer or arbitrary local surgery is supported.

The legacy HostSize prototype is not upgraded into this preservation contract.
Use the pinned `within` form for the qualified imported-body operation.
