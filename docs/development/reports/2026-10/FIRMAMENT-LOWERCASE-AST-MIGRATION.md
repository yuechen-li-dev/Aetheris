# Firmament lowercase AST migration and spelling reconciliation

Date: 2026-10-14. This continues the production migration described in
[FIRMAMENT-DOMAIN-FRONTEND-MIGRATION](FIRMAMENT-DOMAIN-FRONTEND-MIGRATION.md).

## Production ownership

The shared recursive-descent parser now represents historical `units mm`,
`solid name: Primitive`, Box `with` derivation and selector alias associations.
The primitive and Hole binders are shared with native mechanical source. There
is no new whole-document regex dispatch or source-rewrite pass. The obsolete
solid scanner, direct/derived primitive parsers, exposure line parser and
Modify/Hole/region/cut regex readers are removed.

`UnifiedCompatibility` identifies documents whose primitive, derivation,
exposure, Hole and region/cut scopes have AST admission. Both normal validate and build
consume their resulting engineering records; unknown statements and fields fail.
Dimensional defaults are explicitly limited to the historical millimetre context.
Native declarations retain explicit length units. Unresolved face aliases retain
the existing semantic resolver and engineering diagnostics.

`CompatibilityExtensions` identifies documents containing existing typed let,
tolerance, PMI, manufacturing/feature concepts, process Templates, imported-face
recognition/replacement and lattice fill adapters. Their primitive
and Hole inputs now bind from AST, but their remaining semantic scopes are still
source-owned. They are not claimed to have whole-document AST admission. The
old `LegacyLowercase` route name is retired; explicit V1 serialization remains
separate. No legacy semantic owner is deleted before its consumers migrate.

## Spelling decisions

New authoring uses PascalCase constructs/types, camelCase built-in fields,
lowercase linking words (`using`, `as`, `on`, `over`, `with`) and preserved
engineering symbols. User identifiers, recipe parameter labels and published
members remain case-sensitive. PascalCase field and linking aliases remain
compatible at their established vocabulary positions; arbitrary case folding
is not introduced. Uppercase `MODIFY` remains a bounded existing compatibility
alias and still rejects unsupported imported-body edits.

`as` now works in the shared expression parser, Profile pipeline binder,
convention formatter and language-service token classification. Formatting finds
alias nodes in the shared AST; it does not guess from an identifier's spelling.
Profile source adapters retain bounded `As|as` recognition while their remaining
semantic grammar is migrated. `From`/`To`, `Reverse`, `Close` and `TraceLoop` are
unchanged; this does not invent a global lowercase operator grammar.

The public [syntax reference](../../../public/firmament/syntax.md) now begins with the
compile-time Model/Struct/Feature/Pattern mental model, vocabulary rules and a
preferred mechanical example. Historical grammar has a distinct compatibility
section. Feature delegation examples live with Feature. Existing source is not
mass-renamed, and legacy syntax is not presented as a second preferred style.

Generated Hole field metadata now includes counterbore diameter/depth and
countersink diameter/angle. This repairs missing formatting/help vocabulary in
the existing registry rather than adding a second field schema.

## Diagnostic changes

Migrated scopes close their fields and statements at the actual AST owner.
Syntax diagnostics keep their shared `firmament-syntax-*` codes and original
spans rather than being renamed to match old code prefixes. In particular,
`@front:` in a Box override is a located syntax error; this previously invalid
fixture remains invalid. Corpus checks must verify actual V2 disposition and
absence of V1 fallback, not assume every error starts with `firmament-v2-`.

## Qualification

The Release solution build completed with zero errors and 19 nullable,
analyzer and WebAssembly SQLite interop warnings in the current SDK environment.
Focused compiler tests pass **274/274**; focused CLI/corpus/documentation tests
pass **39/39**. The fast Core lane passes **1,068/1,068**. The full solution lane passes **4,745/4,745**, with zero failed, skipped,
error, timeout or aborted tests across 21 TRX projects (20 nonempty suites).

The full lane uses the serial xUnit runsettings described in the preceding
domain migration report. Commands were run sequentially, with node reuse disabled:

```powershell
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:MSBUILDDISABLENODEREUSE = '1'
dotnet build Aetheris.slnx -c Release --no-restore -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build -m:1 -nr:false --filter "Category!=SlowCorpus" --settings artifacts/local/mechanical-frontend-migration/serial.runsettings
dotnet test Aetheris.slnx -c Release --no-build -m:1 -nr:false --settings artifacts/local/mechanical-frontend-migration/serial.runsettings -- RunConfiguration.MaxCpuCount=1
```

Geometry and real-path evidence:

| Witness set | Observed result |
| --- | --- |
| Lowercase primitives, derivations, aliases, holes and region/cut | 40/40 outcomes match: 32 identical accepted STEP hashes, eight preserved capability rejections. |
| Earlier domain migration | 37/37 outcomes match: 35 identical STEP hashes, two preserved rejections. |
| Earlier mechanical migration | 29/29 identical STEP hashes; four validate/build agreement witnesses pass. |
| New CLI source witnesses | Four agree in normal validate/build: two successful Built reports, two source-located failures. |
| Real showcases | GuitarX0, Industrial ATLAS, WarmModernHouse, warehouse, FactoryX0 and executable-machine all inspect successfully through their real CLI owners. |

The new CLI specimens live in `fixtures/Regression/FrontendMigration/`:
`lowercase-compatibility`, `preferred-profile-links`, `lowercase-unknown-field`
and `lowercase-ignored-fillet`. The misspelled field points to line 4, column 42;
the unsupported Fillet points to line 5, column 18, with a readable explanation.
Normal JSON diagnostics remain structured objects. The eight region/cut
rejections retain the same diagnostic codes, messages and stages as their
pre-migration builds; they are existing execution-capability limits, not new
syntax failures disguised as preserved outcomes.

The original STEP hashes and expected rejections are versioned in
[FIRMAMENT-LOWERCASE-AST-MIGRATION.evidence.json](../../../release/FIRMAMENT-LOWERCASE-AST-MIGRATION.evidence.json).
The existing qualification script can replay them without a new harness:

```powershell
./scripts/qualify-firmament-domain-frontend.ps1 `
  -EvidenceFile docs/release/FIRMAMENT-LOWERCASE-AST-MIGRATION.evidence.json `
  -OutputDirectory artifacts/local/lowercase-frontend-migration/qualification
```

Raw build/test logs, TRX files, CLI JSON, runtime snapshots and STEP files are
ignored under `artifacts/local/lowercase-frontend-migration/`. The earlier
mechanical script retains its default artifact location. The first broad run
exposed the depth-parameter shorthand regression and narrower diagnostics;
these were repaired before the final qualification run, rather than weakening
accepted source or deleting the motivating fixtures.

## Runtime resource checks

The interrupted work left no test hosts or CLI processes alive. Existing MSBuild
and compiler servers belonged to the open Visual Studio session and were left
untouched. Subsequent builds disable node reuse, MSBuild server reuse and shared
compilation. Test assemblies and collections run serially, with process/memory
snapshots between lanes. A prior one-off dotnet hostpolicy launch error did not
recur in the explicit-project retry; it is not attributed to the parser without
reproduction. No runtime memory leak has been established by these observations.
At final closeout, no dotnet, testhost, vstest, MSBuild, VBCSCompiler or aetheris
process remained. Approximately 18.7 GiB of RAM and 109.7 GiB of disk were free.
Sampled disk counters showed no paging or disk queue pressure; these are bounded
observations, not a diagnosis of the earlier system-wide spike. No global runtime
settings or unrelated application processes were changed.

## Code-quality audit

Primitive and Hole field binding have one execution owner shared by native and
compatibility spellings. Compatibility units are explicit binding context, not
source substitution. Unknown-field checks close the fields actually consumed by
each owner. Unsupported headers, named Hole variant arguments, ignored directives
and extra operations reject. Region/cut retains the existing route policy and
kernel operations. The `end: depth` shorthand is restricted to its grammar
position so ordinary `depth` parameters keep their meaning.

No new error-code fatality list, whole-document regex route, broad rewrite pass,
product-specific validator, global mutable parser state or geometry operation was
introduced. The Concept fallback now checks actual root declarations, so quoted
`Concept Struct` text cannot select it. Remaining regex helpers for imported-face
replacement and typed semantic applications retain their existing owners.
`git diff --check` is clean. GUI Helios interaction is not claimed; compiler,
metadata and language-service coverage is exercised by the .NET tests.

## Remaining boundary

The remaining lowercase families are the explicit compatibility extension
owners above; region/cut now binds its existing side-hole route policy from AST.
Native Concept/profile adapters, expansion, Wire/Sweep, Assembly/Scene and other domain frontends also
remain as listed in the preceding migration report. This is a production cutover
of the stated families, not whole-language migration completion.


The concrete next witness is
`fixtures/Regression/Language/valid/let-toleranced-values.valid.firmfixture`.
Normal production validation returns **valid / Built / CompatibilityExtensions**.
The existing developer syntax inspection rejects the first `tol 0.05mm` at
line 15, column 42 with `firmament-syntax-declaration-tail`. This debug check does
not grant or revoke engineering admission; it exposes the still-unmigrated
syntax boundary. The smallest next cutover is structured typed values with
bilateral/asymmetric tolerance syntax, bound into the existing let/tolerance IR,
followed by removal of its source reader. It does not require general Let,
new evaluation semantics or a shadow-only milestone.


## Executive verdict

**Meaningful progression for the whole-language migration.** The lowercase
primitive, immutable Box derivation, exposure, Hole and region/cut families now
have production AST admission and qualified geometry preservation. Preferred
using/as spelling and the documented mixed-case scheme are implemented at their
owners. All focused, fast and full .NET regressions pass. The remaining typed
value/tolerance and other extension/domain owners above prevent claiming that
every legacy frontend has been removed. The tolerance witness isolates the next
required production migration; it is not a request for another shadow milestone.
