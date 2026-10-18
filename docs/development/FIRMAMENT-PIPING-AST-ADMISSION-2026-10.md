# Piping AST admission — 2026-10-17

**Success for this pass.** The two original Piping honesty failures are closed,
production admission uses the unified AST, compatibility is qualified, and all
required local gates pass. This does not declare X2 complete.

## Scope and decisions

This follows the corpus modernization pass and closes its CORPUS-09 blocker.
Piping's existing production owner now binds the unified structural AST directly;
the regex block scanner and semicolon field reader have been removed. This is
production admission, not a shadow validator. The planner, geometry materializers,
STEP exporter, stable identities, BOM and cut-list algorithms are retained.

Use PascalCase constructs, camelCase fields and case-sensitive domain symbols.
Historical PascalCase field names remain compatibility aliases of the same shared
field reader. Owner field enums supply both binding names and formatter vocabulary;
the formatter has no independent Piping field schema. Unknown fields are rejected,
not normalized into accepted intent. No global text rewrite or new regex dispatch
was introduced.

One `PipingSystem` owns direct `PipePolicy`, `KeepOut`, `Equipment`, `Port`,
`Connection`, `RouteSpace`, `Route`, `RouteRequest` and `LocalReroute` children.
Nested declarations cannot leak into its sibling symbol tables. Each field owner
uses `FirmamentSyntaxFields` and closes its scope. Model/Struct wrappers close
their own scopes as well. A document with additional material operations rejects;
an unrelated operation is never omitted from a successful piping artifact.

Lengths retain the existing mm quantities/bare-mm-number convention. Historical
omitted document units mean mm; conflicting explicit units reject. Directions
are dimensionless. Required values must be present; absent optional values keep
the existing defaults. Invalid authored values, duplicate aliases, unknown enum
symbols, invalid indices and unsupported header placements reject. Multiple
RouteSpace defaults reject instead of selecting the first. Duplicate names are
diagnosed before symbol lookup can throw.

An explicitly authored zero system clearance now remains zero. The old reader
overwrote it with the 25 mm absent-field default. This is an intentional repair
of discarded intent, rather than a geometry-parity promise for that defective
spelling. The eight existing canonical sources do not exercise this old fallback.

`schema Piping` is optional and uses the existing schema selector. An explicitly
different domain rejects. Recognition uses AST declarations; comments or string
contents containing `PipingSystem` cannot claim the source.

The eighth canonical source, `tee.firmament`, binds Standard catalogue aliases
alongside an explicit straight route. The same existing catalogue now validates
its `Use` and `Let` references. Such references do not instantiate a tee. They are
not an exception allowing arbitrary statements to be skipped. Forge specializes
PumpSkid beside Record/Static configuration declarations. These
now use the existing shared domain value/expansion binder, including scoped field
lookup and schema checking. No Piping-specific scalar evaluator is introduced.

Normal compilation requires at least one explicit or accepted route. A proposal-
only source now rejects with `piping-route-required` instead of reaching empty
geometry reductions. A proposal can still coexist with other built routes.

## Original witnesses and current behavior

| Witness | Before | Repaired outcome |
| --- | --- | --- |
| `piping-field-casing.pending.firmament` | Validate/build succeeded but `connectionType: Weld` produced Generic ports. | Promoted unchanged to [accepted witness](../../fixtures/Regression/FrontendX2/piping-field-casing.firmament). Weld is bound on both ports; exact canonical STEP is retained. |
| `piping-field-unknown.pending.firmament` | Validate/build succeeded and `unexpectedIntent` disappeared. | Promoted unchanged to [invalid witness](../../fixtures/Invalid/Piping/unknown-port-field.firmament). Validate, inspect and build reject at line 6; no STEP is written. |
| Historical field spellings | Independent PascalCase field parsing. | Eight original sources retained byte-for-byte under [compatibility fixtures](../../fixtures/Compatibility/FirmamentV2/Piping); modern canonical sources use camelCase. |
| Explicit incompatible schema | The schema detector did not identify Piping as a distinct domain. | Mismatch rejects at the schema header before execution. |
| Inspect schema rejection | Bare diagnostic strings bypassed the common presentation contract. | Inspect uses the same structured rejection presenter as validate/build. |

The preserved pending-source comments describe the original bugs intentionally;
their regression status is recorded here and in the fixture README.

## Diagnostic contract

Raw diagnostic strings remain on `PipingCompilationResult.Diagnostics` for .NET
compatibility. `FrontendDiagnostics` retains code, severity, message and original
AST span. The kernel adapter carries already-mapped authored spans separately
from expanded-stage offsets, so presentation does not reparse them through an
unrelated mechanical frontend. Semantic/kernel failures without precise spans
retain the existing disclosed enclosing-document location.

Validate, inspect and build present structured diagnostic objects. Text includes
file, location, severity, code and explanation. Validation reports
`parserRoute: UnifiedPiping` and `validationLevel: Built` or `BuildRejected`;
`valid` means the normal binding/routing/materialization/export path succeeded
without writing output files. No static proof of arbitrary future construction
success is claimed.

## Qualification

All results are local. Raw outputs remain under ignored
`artifacts/local/piping-ast/`. `final-full.log` and `final-tests/` are the
successful final .NET gate; earlier attempt logs are retained separately.

| Check | Result | Evidence |
| --- | --- | --- |
| Release solution build | Passed, 0 errors; 30 existing warnings | `build.log` |
| Fast Core lane | 1,076 passed | `fast.log` |
| Full solution lane | 5,423 passed in 21 assemblies; 0 failures/skips | `final-full.log`, `final-tests/`, `test-summary.json` |
| Focused Piping admission/compatibility | 62 passed | `piping-tests.log` |
| CLI structured agreement/location | 3 passed, including exact original field offsets/columns | `cli-tests.log` |
| Real Forge PumpSkid protocol | Passed | `forge-tests.log` |
| Existing Piping STEP | 8/8 byte-identical before/after | `geometry-parity.json` |
| Routing/BOM/cut-list sidecars | 24/24 byte-identical | `geometry-parity.json` |
| Preserved originals | Eight canonical and two pending sources byte-identical | `preserved-source.json`, `text-checks.json` |
| Explicit zero clearance | Verified 7.5 mm physical clearance admits with authored zero; absent-field 25 mm default rejects | `clearance-zero.json`, `clearance-absent.json` |
| Complete canonical owner gate | Passed: 296 owner operations, 69 consumed modules, 4 expected external-body boundaries (369 sources) | `canonical/qualification.json` |
| Historical domain hash gate | 37/37 hashes and admissions matched | `domain-parity/comparison.json` |
| FrontendX4 authoring corpus | 32/32 passed; source/reference goldens unchanged | `x4.log` |
| Sheet profile/semantic CLI corpora | 26/26 and 29/29 passed; formed STEP / flat parity retained | `sheet-profile.log`, `sheet-semantic.log` |

The task-owned .NET/aetheris/test/compiler hosts all exited after qualification.
The cleanup script parses without errors; preview mode was exercised and no
deletion was executed. Modified/new text, relative Markdown links and
`git diff --check` pass. No remote CI or browser execution is claimed.

The code-quality review checked statement/field closure, shared schema ownership,
source-span provenance, severity, duplicate-name failure, V1 isolation and
validate/build reuse. No new error-code fatality table, rewrite parser, alternate
geometry path, or global mutable parser state was introduced. The existing
Piping planning/materialization algorithms are unchanged. Legacy Zig checks are
outside this pass because no Zig/compiler build infrastructure changed.

Reproduce with:

```powershell
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false --no-restore
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
./scripts/Test-CanonicalFixtures.ps1 -NoBuild -OutputDirectory artifacts/local/piping-ast/canonical
./scripts/qualify-firmament-domain-frontend.ps1 -OutputDirectory artifacts/local/piping-ast/domain-parity
python fixtures/Regression/FrontendX4/run.py
```

## Cleanup and remaining work

[Clear-FirmamentCorpusIntermediates.ps1](../../scripts/Clear-FirmamentCorpusIntermediates.ps1)
previews seven explicitly listed old intermediate paths. `-Apply` is required for
deletion and `-Apply -WhatIf` previews the actual operations. It validates paths
inside the intended artifact directory and refuses junctions/symlinks in their
ancestry or contents. It retains final reports, baseline reports, qualification
outputs and source. Only preview mode was run during this pass; the user reviews
and executes deletion. Preview measured approximately 615 MiB of eligible data.

This completes the Piping admission pass, not all X2 work. Independent FEA and
surfacing readers still require their own AST ownership qualification. FEA's
`FirmamentAnalysisCompiler` still owns its regex Header/NestedAll/Scalar parsing;
its numerical and geometry lowering should remain intact when that reader is
migrated. Analytic SurfaceCoil support admission and public identity projection
for legacy sheet-layout Patterns retain the decisions in the earlier corpus
report. No kernel capability or Helios/browser qualification is claimed here.
