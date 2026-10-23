# Firmament frontend X2 — production semantic migration

Date: 2026-10-15. Base: `e6468b03c6bd4a6b2bfd40329de1381c16968512`.

**Verdict: Meaningful progression. X2 is not Accepted.**

The later [Inventory module pass](FIRMAMENT-INVENTORY-MODULES.md) replaces regex
include discovery with the AST-owned dependency graph, adds typed module imports
and retains original imported syntax locations. Its fresh gates are recorded in
that report. The data-pass measurements below remain their original evidence.

Previous qualified continuation: Record, Static and Table names now bind through lexical
production AST identities and a shared typed data catalog. The Spec schema-capture
witness builds its intended 30 × 20 × 6 mm geometry. Final gates: **5,018 tests**,
**1,276 classified sources**, **26/26 byte-identical STEP baselines**. Complete
include-file provenance and remaining collection/domain body readers remain X2 work.

## Baseline and inherited guarantees

The X1 closeout, its complete evidence ledger, earlier handoff, October authoring
audit, language audits/register and current public authoring guide were checked
against the implementation. A fresh owner-aware production sweep reproduced
the X1 baseline before edits: **1,266 sources**, **327/327 canonical sources
structurally parsed**, 902 total structural successes, 504 standalone owner
admissions, and zero unresolved classification mismatches. The source inventory
hash remained `7CC4BFD666557BFA58F182FEF8042BFF51B576244A2E85C1CF09EF4D45B25D41`.
X1's 4,943 passing tests and 26 STEP hashes are inherited evidence, not newly
measured X2 results. Fresh qualification is recorded separately below.

The nine explicit canonical domain rejections remain inherited boundaries:
four figure-dependent Garments, one profile identity collision and four
SurfaceCoil material-owner combinations. Analysis compilation is distinct from
solving. Deliberately invalid, experimental, legacy serialization and consuming
modules retain their dispositions.

## Architecture and migrated owners

The existing shared lexer and recursive-descent parser remain the source grammar.
AST metadata retains explicit parameter-list presence and authored argument-value
boundaries so admission and serialization cannot lose those distinctions.
No shadow compiler, whole-document dispatch mechanism, alternate engineering IR
or geometry operation was introduced.

Feature definitions, parameters, local derivations, construction and terminal
returns now bind AST nodes. `FirmamentBindingScope` resolves Features through
finite lexical parents, using a separate callable namespace from construction
types. A Model-local Feature can shadow a document Feature; sibling declarations
cannot capture a call. Duplicate definitions in one scope reject. Delegated
calls resolve in the definition's scope. Unused definitions still have their
return kind/delegation and dependency cycles checked.

`FirmamentSyntaxSubstitution` transforms references and member paths. Declaration
names, labels and strings are separate syntax roles. Explicit private-construction
renaming replaces Feature's regex alpha-renaming. Nested parameters and mapping
binders shadow enclosing substitutions. Defaults bind typed dependencies;
cycles and excessive depth reject. Existing scalar/Record transport continues
to carry tolerances and explicit PMI receipts through the existing typed owner.
Invocation metadata retains both the original whole-Record argument and its
checked flattened member bindings, including nested Record tolerance receipts.

Feature expansion produces concrete AST declarations and a retained AST result.
The only production Feature implementation uses these nodes. The old regex
definition/call/return/local readers and private character-based arithmetic
parser were removed. Existing consumers still take serialized declarations:
`FirmamentSyntaxWriter` is an explicit adapter boundary, not a second parser or
evaluator. Subsequent legacy expansion stages still prevent handing one bound
document directly to every lowerer. This is a remaining X2 gap, not full pipeline
acceptance merely because an AST result exists.

Finite bounds reject rather than truncate: 64 nested calls/default dependencies,
4,096 Feature invocations, 65,536 generated declarations and 4 MiB of output.
Output is charged during expansion, before accumulating another large result.
No mutable compiler-global environment or specialization cache was added.

## Expressions and values

`FirmamentNumericEvaluation` evaluates the shared expression AST. It replaces
both the former Feature arithmetic parser and the duplicate syntax-field numeric
interpreter. Scalar dependency binding and Concept arithmetic use its operator
type policy; scalar computation delegates numeric operations to that service.
Panel arc radians retain their original numeric unit without degree conversion.
Mixed degree/radian arithmetic is not silently admitted. Existing Assembly
frames/ports, Scene dimensions, Garment dimensions, bounded
Concept point math, linear features and relevant profile materializer adapters
call the same service instead of reaching into Feature expansion.

Lengths and angles remain distinct; invalid dimensional products, mismatched
addition, unknown references, division by zero and nonfinite results reject.
Existing compile-time tolerance semantics remain: nominal arithmetic does not
implicitly propagate a tolerance. Explicit requirements and PMI retain their
own existing authority. This is numeric consolidation, not completion of typed
Records, Sets, Tables, enum values, selectors and structural predicates in one
scoped value environment.

## Patterns and finite feature Templates

Keyed Set and record-array Pattern headers and mapping bodies now use AST nodes.
The existing finite feature-Template output form specializes a checked declaration
through AST member substitution. Assembly/Scene occurrence Patterns also consume
AST headers and mapping bodies, including the legitimate form where one item
maps to several occurrences. Existing generated wrappers, keys, source order,
instance identities and per-family limits are preserved.

The historical semantic point Pattern adapter now substitutes expression nodes
instead of replacing every occurrence of the item spelling in its body. A binder
named `Diameter` cannot overwrite the `Diameter:` label. The new keyed Pattern
witness deliberately gives the binder and Hole declaration the same name and
compares real STEP output with a differently named binder/declaration.

This does not complete Pattern consolidation: Set/array collection interpretation,
point recipes, radial/mirror/linear expansion and several shared source adapters
remain. No new Pattern spelling or unsupported nesting was added.

## Includes, provenance and the next concrete blocker

Feature receipts distinguish definition and invocation, retain the lexical scope
and ordered delegation chain, and expose their actual available spans. Retained
nodes are matched to authored AST locations by lexical declaration/mapping
ownership and occurrence identity; offsets are not reused after spelling changes.
Generated nodes without an authored counterpart remain explicitly unmapped.
Virtual input locations carry a limitation note rather than claiming to be
original include-file positions.

The product Template registry has now moved to the production AST. Declaration
and application recognition, parameter lists/default spans, forward registration,
nearest-scope lookup and cycle dependencies use structured nodes. A Model-local
Template shadows its document counterpart; sibling-private Templates reject.
Nested specialization resolves from the definition scope. Same-scope duplicates,
unknown applications and ignored header/placement clauses reject. Scope receipts
are additive debug metadata. Original unambiguous specialization identities are
retained; newly admitted same-named lexical families receive distinct identities.

The former pending
[`template-shadowing.firmament`](../../../../fixtures/Regression/FrontendX2/template-shadowing.firmament)
is now an admitted positive: the local default produces a 30 × 20 × 6 mm body,
and its STEP matches concrete source. A nested definition-scope witness matches
the 10 mm outer family despite a 30 mm caller-local family. The sibling-negative
fixture retains the authored application span and actionable message in CLI JSON.

Host discovery/invocation uses the same lexical index. Document exports and the
established single-Model module container remain admitted; private definitions
from sibling Models are never pooled. Qualification exposed two genuine structural
productions used by host clients outside the fixture inventory: qualified
`Construct Company.Capability name` and lowercase `analysis Kind name` headers.
They now have explicit productions in the existing recursive-descent parser.
Parameterized force quantities such as `AppliedForce N` are also represented
before specialization; unit/capability meaning remains with the existing owner.
The file-build path also avoids re-expanding serialized V1 data as V2: the existing
NotRecognized boundary delegates to the original explicit version-1 reader and
retains its version and in-memory compatibility checks.

The server Paperclip demo previously passed the X0 example facade, which includes
a default application, into host specialization. After removing the Template
definition, that left a dangling default application that old discovery skipped.
The stricter binder rejects it. The endpoint now uses the existing declaration-only
library API; its real HTTP/STEP integration test remains the acceptance path.
Malformed Template prefixes cannot disappear if syntax recovery fails to produce
a declaration node: the existing lexer/token stream retains the failed prefix.

Complete provenance and value scoping still have concrete blockers.
The previous `AssemblyM0Parser.LoadProjectSource` and `LoadSourceGraph` returned
concatenated strings without per-declaration file identity. The Inventory pass
removes those readers in favor of the module graph. Legacy include transport now
has source segments; typed imports keep their defining modules separate. Template bodies still cross the
existing Match/PMI and domain lowerers as source slices. No claim is made
that the Template body substitution or every domain reader has migrated.

The Spec schema-capture witness is now repaired:
[`record-shadowing.firmament`](../../../../fixtures/Regression/FrontendX2/record-shadowing.firmament)
is an admitted positive whose local 30 × 20 × 6 mm product matches concrete STEP.
The outer Spec(width) and local Spec(width, thickness) have distinct nominal type
identities. The outer value is checked against its own schema and cannot be
passed as an instance of the local type merely because their names match.

`FirmamentDataBinding` registers Record/Enum types and Static/Table values in
lexical AST scopes. Parameters, mapping binders and type parameters protect their
own namespaces. It binds type/value references before typed evaluation. Existing
unique spellings are preserved; collisions lower to deterministic private symbol
identities only at the established source boundary. This is AST identifier
lowering, not a new source-text macro, expression engine or parser route. Strings,
field labels, member labels and Table key fields are never renamed as references.
Explain metadata exposes original names, scopes, identities and available spans.

Record schemas, Static Record literals, checked with derivation, Table rows and
typed aliases now use one AST-derived catalog. `CanonicalStaticAuthoring` consumes
that catalog rather than reinterpreting another set of Record/Table declarations.
Their fields and scalar leaves use the established typed dependency graph.
Specialized data is admitted before Feature arguments. Duplicate data declarations,
private sibling references, nominal mismatches, cycles, unknown fields and ignored
headers reject. Typed tolerances retain their explicit PMI projection and authored
requirement locations; toleranced Table/collection cells still reject.

Host discovery selects visible schemas from its existing export scope, preserving
document and single-Model container compatibility. Public Record/type names remain
authored names; nested host Record markers resolve the selected nominal schema.
Unambiguous existing engineering identities remain unchanged.

The remaining X2 boundary is complete generated-source provenance and the
collection, expansion and domain source adapters. Inventory now owns module lookup;
legacy includes retain an explicit mapped declaration-composition adapter. Record/data lexical lookup no longer
requires that handoff to be flattened into an unqualified registry, but this pass
does not claim that all original include locations or domain bodies have migrated.

## Domain reader inventory

Classification is per source owner, not a claim that a structural gate migrates
all downstream semantics.

| Family / current owner | Current classification | Remaining source responsibility |
| --- | --- | --- |
| Feature / `FirmamentV2FeatureExpansion` | AST-owned binding/expansion with explicit serialization boundary | Downstream consumers still require text; complete per-node include/Template maps remain. |
| Product Template / `FirmamentTemplateSyntax`, `FirmamentV2TemplateExpansion` | AST-owned declarations, parameters, lexical lookup and dependency edges | Body substitution, Match/Require/PMI and domain transport remain explicit adapters; typed data uses the shared AST catalog. |
| Record/Static/Table / `FirmamentDataBinding`, shared `ReadDataCatalog` | AST lexical identities and one checked data catalog | Existing downstream source transport remains; Set/array interpretation and complete module maps are separate. |
| Gear / `GearAuthoring.Bind` | AST-owned source fields | Generic product specialization remains upstream. |
| Mechanical / unified binders | AST admission with bounded semantic adapters | Profile, Compose, selection/PMI and compatibility readers remain in parts of the lowerer chain. |
| Panel, Plateau, Revolve, Loft/SectionChain | AST admission with bounded semantic adapters | Profile resolution, preprocessing and specialized source projections remain. |
| Assembly contracts / `AssemblyInterfaceSyntax` | AST-owned migrated contracts/mates | Existing engineering placement and relationship lowerers preserved. |
| Assembly tree / `AssemblyM0Parser` | AST module graph and structural admission; existing product lowerers retained | Product tree, frames, appearances, ports and remaining source specialization. Includes use the module graph's mapped compatibility projection. |
| Scene / `SceneAuthoring.Reader` | Legacy source reader still authoritative | Room/opening/camera/placement binding and module interpretation; numeric service and occurrence mapping now shared. |
| SheetMetal / SheetMetalFirmament, AuthoredSheetMetalCompiler | Legacy source reader still authoritative after structural admission | Manufacturing source fields and expansion remain to migrate. |
| WireForm / `WireFormAuthoring` | Legacy source reader still authoritative | Field/route interpretation and source-based specialization remain. |
| Drawing / `FirmamentDrawingCompiler` | Legacy source reader still authoritative | Drawing Templates, metadata and drawing fields remain independent source readers. |
| Analysis / `FirmamentAnalysisCompiler` | Legacy source reader still authoritative | Analysis fields, native/import references and geometry adapters remain; solving is separate. |
| Garment / `GarmentAuthoring` | Legacy block reader after structural admission | Garment fields and figure/posing/surface source adapters; Feature specialization now shares AST owner. |
| Sculpting / Aetheris.Surfacing.SculptingAuthoring | Existing source adapter retained | Source interpretation and artifact capability remain separate from this migrated Feature family. |
| STEP/AP242, JSON serialization | Intentional external/import-format parser | Not Firmament V2 source semantics; not candidates for deleting external format readers. |

A parity run caught a concrete transport failure: adding spaces to a guitar
Panel specialization changed its Assembly definition identity and STEP ordering,
even though the Cartesian-coordinate multiset matched. The serializer now retains
authored argument-value spans and preserves application punctuation/trivia while
emitting the bound AST values. A focused source/AST test and the original guitar
STEP hash qualify that repair. No numerical tolerance or reordered-geometry excuse
was used to waive byte identity. The removal criterion for this transport adapter
is the typed module/specialization handoff described above, with explicit stable
identity compatibility; a general source rewrite is not the replacement.

Feature definition/invocation headers are closed, including empty generic/parameter
lists, quoted type names, named return constructors and unsupported invocation
placements. An otherwise buildable drilled plate rejects when `on XZ` is added
to its named Feature invocation. Value parameters cannot capture type/variant
names such as `Profile`, `Boss` or `Shaft`.

No obsolete Feature implementation remains as a competing authority. Unmigrated
Template body/value and domain readers are explicitly outstanding, not justified as
permanent alternate semantic frontends.

## Diagnostics, tooling and compatibility

Feature binding preserves the established blocking `fatal` severity, stable
diagnostic families, human messages, stage and available authored locations.
Unsupported general control flow now comes from the shared structural parser's
located diagnostic. Two component expectations were updated for that earlier
owner. A whitespace-only generated-source assertion was replaced with an AST
result assertion while retaining its real build/private-identity checks.

`validate --json --explain` exposes Feature ownership, definitions, actual bound
invocations, scopes, expansion counts and definition/use receipts. It explicitly
also exposes product Template owner, bound arguments and definition/application
scopes. Data metadata adds original names, lexical scopes and lowered identities
under `dataOwner` / `dataBindings`, while labeling remaining body/provenance adapters. Ordinary JSON retains
its existing report shape. `inspect --json` adds the same Feature receipt fields.
Normal validate/build still use the same production admission, not explain data.
No Helios parser or validator was introduced. Existing language-service compiler
diagnostics/navigation tests are part of the full gate; no fresh browser session
or universal domain marker/source-map qualification is claimed.

PascalCase constructs/types, camelCase built-in fields, lowercase links and
preserved user/domain symbols remain the spelling decision. Legitimate aliases,
typed Interface families/named Mates, multi-occurrence Pattern mappings, material
overrides and rotation fields are preserved. Assembly adapters still require
semicolons in some generated forms; the serializer supplies them rather than
changing accepted source meaning or rewriting that grammar during this pass.

## Dogfood and X3 handoff

Two fresh ordinary parts were authored from the current guide: a small drilled
plate using a typed/defaulted local Feature and a two-hole keyed mounting plate.
The latter intentionally collides the element name with a field/declaration name.
Their real validation/build and STEP inspection receipts are retained locally.
A separate Panel arc witness checks retained radian surface coordinates and
explicitly retains the existing panel-only STEP capability rejection.
Invalid sibling calls/data references are explicit rejection witnesses; the former
Record scope boundary is now a positive geometry and nominal-type qualification.

Observed friction: semantic Grid derivations still require the existing field
separators; the initial compact test spelling was invalid in that owner and was
corrected to documented multiline fields. This did not justify adding another
Grid grammar. Pattern families retain different human-facing binders, point
aliases and material/domain scopes. X3 owns their eventual surface reconciliation;
X4 still owns the independent original twelve-part authoring retest.

The October decision register retains all original recommendations and rationale,
with current owner/compatibility/X3/X4 dispositions appended. No syntax decision
is being withheld for user approval. The outstanding work is implementation and
qualification of the module/Template semantic contract described above.


## Final qualification

The final code gate is `closeout-build.log` / `closeout-full.log`, not the intermediate
runs that exposed severity, serializer identity and header-consumption issues.
Final qualification commands ran serially with build-node reuse/shared compilation disabled.
The last CLI review removed a duplicate data error caused by appending its message
to the code string. Structured callers now retain one stable, source-located
binding diagnostic; string-only compatibility APIs retain readable explanations.

| Check | Final result |
| --- | --- |
| Release solution build | Zero errors; 19 existing warnings in the final gate build. |
| Focused scoped-data/frontend/Template/Record tests | 117 passed in the final focused gate; Feature, CLI and Paperclip HTTP tests also pass in the full gate. |
| Public documentation qualification | 13 passed; relative links and real CLI examples qualified. |
| Required Core fast lane | 1,068 passed. |
| Complete serial solution suite, including SlowCorpus | **5,018 passed across 20 executing assemblies; zero failures/skips.** Firmament: 2,548; CLI: 518; Core: 1,201. |
| Complete current source corpus | **1,276 sources**, all classified; **327/327 canonical sources structurally parsed**; zero unresolved classification mismatches. |
| Inherited source comparison | All 1,266 original source hashes, structural outcomes, production owners/levels/exits and final classifications match the fresh baseline. One speculative AST fingerprint change is reviewed below. |
| Diagnostic family changes | Two control-flow negatives use the shared structural parser; a structural-only nested-generics witness reports its unsupported parameter and undeclared Product separately. All remain rejected. Data-only diagnostic-code differences are retained explicitly. |
| STEP/AP242 parity | **26/26 byte-identical**, including guitar, assemblies, symmetry, Sculpting, typed tolerances and Record/Template/Feature explicit PMI. |
| Fresh valid sources | Six real CLI validate/build successes, with STEP reimport inspection; three sibling negatives reject in both commands and emit no STEP. |
| Panel radian compatibility | Real production parse produces Panel AIR and the expected sampled surface coordinates; solid build retains its explicit preexisting Panel-only capability rejection. |
| Runtime snapshot | **0 remaining dotnet/test/build hosts**, 13.19 GiB free RAM and 91.82 GiB free disk. |

The existing FrictionLab project discovers no tests and is not counted as an
executing test assembly. Focused/fast totals are not added to the full total.
There is no remote CI or new live Helios/browser claim. The runtime result is a
post-run snapshot plus bounded retained-session checks, not a long-duration soak.

### Corpus dispositions

| Disposition | Sources |
| --- | ---: |
| Admitted | 493 |
| AdmittedGeometryWithDfmFindings | 1 |
| AdmittedInConsumerContext | 34 |
| DeclarationModuleRequiresConsumer | 5 |
| ExpectedNegativeRejection | 285 |
| ExperimentalAdmitted | 9 |
| ExperimentalRejected | 31 |
| LegacySerializationAdmitted | 5 |
| LegacySerializationRejected | 324 |
| MalformedOrUnsupportedSourceRejected | 6 |
| MisfiledPositiveFixture | 1 |
| RejectedAtSolveStage | 1 |
| SourceAdmissionRejected | 81 |

Raw structural outcomes: 912 parsed / 364 rejected. Raw standalone/domain owner
outcomes: 510 accepted / 766 rejected. These totals are not a V2 success rate.
Canonical dispositions remain 285 direct admissions, 33 actual consumer contexts
and nine explicit domain admission limits. No previously admitted fixture was
reclassified to conceal a regression. The Panel-only witness remains an existing
capability rejection. The former pending Record is explicitly promoted to a positive.

The current inventory hash is
`3FE41317FF888DC74EB9A1FF6A95761E4950B7F88301976B5EB4BC8960F24CF0`.
The compact evidence record references the exact X1 complete ledger by hash,
records verification of every inherited source, adds all ten current new source rows
and all observed diagnostic/fingerprint differences, and retains all 26 artifact comparisons.
Detailed current owner outputs/messages remain in the ignored complete report.
Universal semantic parity remains explicitly false.

### Reviewed continuation differences

All **1,272** source paths common to the preceding 1,273-source X2 pass retain
exact hashes, AST fingerprints, structural outcomes, production owners/levels/exits,
diagnostic codes and dispositions. The pending Record file is promoted/renamed to
`record-shadowing.firmament`: its former missing-thickness rejection is replaced
by a correctly admitted local product. Three fixtures are added: scoped Table
selection, scoped Record tolerance/PMI and a sibling-private data rejection.
These changes account for the complete current 1,276-source inventory.

The original 1,266-source comparison remains unchanged. Earlier Template-pass
history is retained in the compact evidence rather than reported as a new data-pass
difference. No invalid/speculative fixture was reclassified to hide a regression.

One speculative material fixture changes AST fingerprint: spaced `200 GPa` and
`180 GPa` now form Quantity nodes. Its unsupported material/density syntax still
rejects structurally and in production; its disposition is unchanged. This is
neither an admitted material capability nor a geometry regression.

`Regression/FrontendX1/nested-generics.firmament` is explicitly structural-only:
its nested Set parameter and undeclared `Product` remain semantically rejected.
The parameter diagnostic names `T`, and lookup reports `Product` separately.
Compared with the pre-X2 baseline, two control-flow fixtures changed diagnostic
owner, one Feature cycle no longer repeats the same cycle from a second starting
node, and two Feature type errors carry shorter code suffixes. All six complete
code differences are retained in evidence; no accepted/rejected status changes.

### Physical and identity witnesses

| Witness | Measured result |
| --- | --- |
| Scoped drilled plate | One enclosed manifold body, seven faces, bounds `(-15,-10,0)..(15,10,8)` mm; the local recipe produces the checked 6 mm hole. STEP matches the equivalent concrete program in the component test. |
| Keyed mounting plate | One enclosed manifold body, eight faces, bounds `(-20,-12,0)..(20,12,6)` mm; two 6 mm holes retain `holes.left/right` identities. STEP matches the renamed-binder form, including a binder named Shaft. |
| Semantic point Pattern | Four checked 6 mm holes; a binder named Diameter retains the same STEP as the Item spelling. |
| Local product Template | One enclosed manifold body, six faces, bounds `(-15,-10,0)..(15,10,6)` mm; local width is 30 mm and STEP matches concrete source. |
| Scoped schema capture / Table row | Each is one enclosed manifold six-face body, bounds `(-15,-10,0)..(15,10,6)` mm. Component tests compare local Record, scalar, Table and with derivation against concrete STEP. |
| Scoped Record tolerance / explicit PMI | One seven-face body, bounds `(-20,-15,0)..(20,15,8)` mm; 8 mm hole with +0.10/-0.05 mm requirement, AP242 face associations and the authored tolerance span checked in the component test. |
| Guitar | Original STEP hash `469E42D07EC1BB3FFDB88D5B0149DA50C21F39631EDC7441C2B3DCF11C41681C`; 170 occurrences / 66 definitions. |
| Radian Panel | Sampled endpoints `(10*cos(0.25),10*sin(0.25),0)` and `(10*cos(0.75),10*sin(0.75),5)` mm, with no round-trip unit conversion. |

Representative real ATLAS, house/factory Scene, sheet-metal, WireForm, Sweep,
Revolve and PMI cases also run through their proper corpus owners and regression
paths. Exact pre/post artifact equivalence is claimed for the measured STEP
baselines and explicit equivalent-program witnesses, not every conceivable
Scene/GLB export or every specialization.

## Performance evidence

Single-run end-to-end CLI measurements include process startup and the existing
engineering execution, not just AST evaluation:

| Case | Elapsed |
| --- | ---: |
| Small mechanical / scoped Feature | 512 ms |
| Record/Table Template | 450 ms |
| Keyed Pattern | 546 ms |
| Guitar, two retained-session compiles | 10,384 ms |
| Small Assembly, two retained-session compiles | 595 ms |
| House Scene inspection | 2,274 ms |

The compact record retains the Assembly owners' actual per-build phase timings
and reuse counts. The second guitar compile reuses all 56 definitions tracked by the ordinary
definition reuse counter; exported definition count remains 66. The second
small Assembly compile reuses its definition. These checks preserve retained
compilation behavior. They are neither a speedup claim against an old binary
nor a complete lex/bind/expand/lower phase benchmark. Dedicated uniform frontend
phase measurements remain part of unfinished X2 qualification.

## Code-quality closeout

The modified frontend was checked for competing Feature readers, duplicated
numeric interpretation, duplicate Record/Table readers, textual identifier
replacement, diagnostic construction,
header consumption, type/value namespace capture, provenance loss, mutable global
state, expansion growth and ordering. Obsolete Feature readers and arithmetic
code are removed, together with the independent canonical Record/Table readers
and their unused Table-column helper. Existing unmigrated readers are identified above; no new
fatal-code allowlist, whole-document parser route, generic source-reparse adapter
or kernel feature was added. Serializer source spans preserve transport spelling
only after AST binding; they do not select symbols or engineering meaning.

The build, focused/full tests, complete corpus disposition and STEP hashes verify
the final implementation. Public syntax and the 24-item decision register are
updated. Test expectations changed only for earlier control-flow ownership and
a whitespace-only assertion replaced by an AST identity assertion, plus the
promoted pending Template and Record witnesses; real build,
geometry and identity assertions remain. Generated logs, STEP and full reports
stay under ignored `artifacts/local/firmament-x2/` and the continuation directory
`artifacts/local/firmament-x2-scoped/`, with this pass in
`artifacts/local/firmament-x2-data/`. Earlier evidence remains intact; this pass
does not remove prior generated runs. Runtime hosts are checked after qualification.

## Replay and evidence

```powershell
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll frontend-shadow --corpus fixtures --production --output-dir artifacts/local/firmament-x2/replay-corpus
python scripts/classify-firmament-frontend-x1.py --report artifacts/local/firmament-x2/replay-corpus/report.json --downstream-evidence docs/release/FIRMAMENT-FRONTEND-X1-DOWNSTREAM.evidence.json --output artifacts/local/firmament-x2/replay-classified.json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate fixtures/Regression/FrontendX2/scoped-drill.firmament --json --explain
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm inspect fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm --json --profile --repeat 2
```

The inventory command retains its historical `frontend-shadow` name; it calls
the current structural parser and production domain owners. It does not provide
a shadow-only admission path.

Artifact replay: the adjacent evidence JSON's `geometryParity` entries contain
source hashes and expected STEP hashes. Build each ordinary source with
`build <source> --output <ignored-output.step> --json`; use
`asm export-ap242 <source> --out <ignored-output.step> --json` for Assembly sources.
Compare SHA-256 only after a successful command. The source hash must still match;
a changed source is not the same baseline.

Local final logs in `artifacts/local/firmament-x2-data/`: `closeout-build.log`,
`closeout-focused.log`, `closeout-cli.log`, `closeout-docs.log`, `closeout-fast.log`, `closeout-full.log`,
`corpus/report.json`, `classified.json`, `parity.log`, `geometry-parity.json`,
`dogfood.json`, `performance.json`, `reviewed-dispositions.json` and
`runtime-summary.json`. Earlier failed runs remain reproduction evidence; no
failed intermediate run is used as the final gate.

Portable record: [X2 compact evidence](../../../release/FIRMAMENT-FRONTEND-X2.evidence.json).
Baseline: [X1 closeout](FIRMAMENT-FRONTEND-X1-CLOSEOUT.md) and its complete ledger.
Current language: [syntax](../../../public/firmament/syntax.md) and
[decision register](FIRMAMENT-LANGUAGE-DECISIONS-2026-10.md).

## Executive verdict

**Success for the requested scoped-data binding pass. Meaningful progression
for X2 overall.** Record, Static and Table lookup and schema ownership now
use production AST identities and the common typed catalog. The schema-capture
witness builds its intended geometry, inaccessible/incorrect data rejects, and
scoped tolerance requirements project through the real AP242 path.

X2 remains open for complete generated-source provenance and remaining collection,
expansion and domain readers. The later Inventory module report records the new
module-binding and original syntax-location qualification. No language-policy decision is awaiting
the user. The next work is the module syntax/provenance handoff and removal of
those remaining source owners as each family is qualified.
