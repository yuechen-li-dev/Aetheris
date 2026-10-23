# Firmament frontend X1 — reviewer handoff and gate assessment

Historical assessment before the final migration/qualification pass. The
[final X1 closeout](FIRMAMENT-FRONTEND-X1-CLOSEOUT.md) supersedes this verdict and
records the repaired blockers, complete corpus ledger and passing final gate.

Date: 2026-10-14  
Reviewed implementation: `0bf9f7da0ee4da2142f95325c415ec03902747bf`  
Verdict: **Meaningful progression. The regression gate passes; full X1 acceptance remains open.**

## Executive assessment

The unified lexer/AST frontend now admits production mechanical source and several
domain families. Slot2 route perturbation, ignored Compose Fillet and unknown
boundary fields have concrete regression witnesses. Later passes extend that
production path through compatibility spellings, schema/include admission, typed
tolerances, compile-time value transfer, Concepts and closed 2D shapes. Existing
qualified geometry and explicit PMI artifacts remain stable.

This is substantial production migration, but it does not establish that all
required canonical V2 forms have a complete structural representation and shared
admission path. A fresh check finds a legitimate canonical Assembly/Interface
document accepted by its production owner but rejected by the unified structural
parser. Current whole-corpus structural coverage and semantic comparison have
also not been requalified after the subsequent migrations.

Therefore the answer to “is X1 Accepted?” is **not yet**. The bounded migrations
can be reviewed as successful work without declaring the remaining frontend
migration complete. No architecture-wide honest stop is necessary: the next
missing construct and its existing domain IR owner are identifiable.

## Scope and authority

The original X1 proposal emphasized shadow qualification. The user's subsequent
instruction supersedes shadow-only delivery: migrate production admission into
the unified AST, preserve legitimate compatibility and geometry, and retire each
obsolete reader after its family is qualified. This report assesses that actual
implementation direction. It does not recommend another shadow-only milestone.

The existing lexer, structural parser and domain lowerers remain the ownership
chain. The retained `frontend-shadow` command is useful for inspecting the shared
structural parser; its partial comparison is not a second admission authority or
proof of full semantic parity. Domain-specific commands remain necessary when
checking actual Assembly or Scene compilation.

The original X1 gate permits bounded adapters and leaves full expansion and
semantic consolidation to X2. Remaining regex readers are not, by their mere
existence, proof that X1 fails. The concrete unsupported canonical syntax and
incomplete current qualification are the gate issues here. Completing the user's
broader production migration objective also requires the remaining domain owners.

## Implemented and qualified work

| Area | Production result and qualification boundary | Detailed report |
| --- | --- | --- |
| Mechanical admission | Unified mechanical AST admission; Slot2 no longer changes parser behavior when a sibling construct is present; omitted Fillet and unknown fields reject. Four obsolete mechanical routes retired. | [Mechanical migration](FIRMAMENT-MECHANICAL-FRONTEND-MIGRATION.md) |
| Domain fields | AST-owned Gear, Revolve/Axis, Panel, Plateau and Loft/SectionChain record fields reuse existing engineering lowerers. Other domain source readers remain. | [Domain migration](FIRMAMENT-DOMAIN-FRONTEND-MIGRATION.md) |
| Compatibility and casing | Lowercase mechanical compatibility shares AST binders. Preferred spelling is PascalCase constructs/types, camelCase built-in fields and lowercase linking words; user/domain symbols retain their spelling. No global case folding. | [Lowercase migration](FIRMAMENT-LOWERCASE-AST-MIGRATION.md), [syntax reference](../../../public/firmament/syntax.md) |
| Includes and schema | Previously ignored directives and schema bypasses have explicit admission checks and regression coverage. This does not claim every domain has one directive owner. | [Syntax review](FIRMAMENT-SYNTAX-REVIEW-2026-10.md), [decision register](FIRMAMENT-LANGUAGE-DECISIONS-2026-10.md) |
| Typed tolerances and paths | Compile-time tolerance values reach explicit supported PMI projections. Bare Path and Concept Path share an owner; Sweep field syntax migrated to AST. Manual path operations remain bounded by their existing owner. | [Typed tolerances](FIRMAMENT-TYPED-TOLERANCES.md) |
| Compile-time values | Record, Template and Feature transfer typed tolerance values, defaults, overrides and provenance into nominal geometry and explicit PMI. Complete AST expansion is not claimed. | [Value binding](FIRMAMENT-COMPILE-TIME-VALUES.md) |
| Concepts and 2D | Structural contracts, scalar predicates, labeled bundles and structural composition use production admission. Plain and Concept-prefixed closed shapes agree; unknown fields and unsupported explicit rotation reject. | [Concepts and 2D](FIRMAMENT-CONCEPTS-AND-2D.md) |

The public syntax document was maintained during these passes. The original
24 authoring recommendations retain explicit dispositions in the decision
register; a tracked disposition does not mean the recommendation is implemented.

## Fresh gate witnesses

These checks were replayed against the reviewed revision during this report
pass, using the repository CLI DLL rather than an installed executable.

| Source and command | Exit | Observed result |
| --- | --- | --- |
| `fixtures/Regression/FrontendX1/slot-route-perturbation.firmament`, `validate --json --explain` | 0 | Valid, `Built`, `UnifiedMechanical`; no diagnostics. |
| `fixtures/Regression/FrontendX0/unknown-compose-statement.firmament`, `validate --json` | 1 | Invalid, `ParsedNotBuilt`; `firmament-v2-canonical-declaration-unknown:Compose:Fillet` survives. |
| `fixtures/Regression/FrontendX0/unknown-boundary-field.firmament`, `validate --json` | 1 | Invalid, `ParsedNotBuilt`; `firmament-v2-canonical-field-unknown:Rect2:heigth` survives. Route is null because rejection precedes route selection. |
| `fixtures/Canonical/AssemblyInterfaces/executable-machine.firmament`, `asm inspect --json` | 0 | Actual Assembly production owner accepts the document. |
| Same canonical Assembly source, `frontend-shadow --json` | 1 | Unified structural parser rejects two Role capability requirements on original source line 3. |

The canonical Interface declaration is:

```firmament
Interface<Axial> InstallCell { Role A requires AxisCapable; Role B requires AxisCapable; }
```

Both failures have code `firmament-syntax-declaration-tail`. At line 3, column 39,
the parser reports `Unexpected 'requires' after Role A; expected a body, initializer or separator.`
The corresponding Role B failure is at column 68. The debug comparison reports
`semanticParityQualified: false`, zero compared facts and 12 uncompared declarations.

This is a **canonical structural-coverage and admission-adapter gap**, not evidence
of a newly discovered ordinary validate/build contradiction. The debug envelope's
generic production projection invokes the standalone part owner, which rejects
the document's include as unconsumed. That projection is not Assembly authority;
the successful `asm inspect` result above is the relevant production check.

Relevant existing owners are
[FirmamentSyntaxParser](../../../../Aetheris.Kernel.Firmament/FirmamentV2/Syntax/FirmamentSyntaxParser.cs)
and [AssemblyM0Parser](../../../../Aetheris.Kernel.Firmament/Assembly/AssemblyM0Parser.cs).
The latter still reads Interface roles, capability requirements and mate clauses
before producing the existing Assembly/Interface IR.

## Corpus accounting

A fresh filesystem inventory finds **1,263** fixture source files, including
**327 Canonical** files. It includes `.firmament`, `.firmfixture` and `.firmasm`,
case-insensitively. These are inventory counts, not successful parse counts:

| Fixture group | Sources |
| --- | ---: |
| Canonical | 327 |
| Compatibility | 417 |
| DifferenceEngine | 2 |
| Experiments | 8 |
| Invalid | 203 |
| InvestorPitch | 15 |
| Regression | 241 |
| Speculative | 45 |
| Thread | 4 |
| three-telos | 1 |

The generated source inventory records each source's SHA-256. Its manifest hash
is `D60E72438B523B7BE5BF036892BCF474DE546B966E7383A28D259DE8FD3AA992`.

The [historical X1 foundation report](FIRMAMENT-FRONTEND-X1.md) recorded 1,232
sources: 741 structurally parsed and 491 rejected; its Canonical subset was
300 parsed and 23 rejected out of 323. It also recorded 57 generic-production-
accepted/structurally-rejected cases. Those are historical measurements, not
current failure totals. The source population and implementation have changed.
No new full structural-coverage collection was run for this report.

The historical differential comparison covered selected facts, including model
identity, primitive identity/type, literal-mm Box sizes and direct Compose
receipts. It did not prove complete field, reference, expansion or domain parity.
A current acceptance percentage cannot be inferred from it.

## Regression and geometry evidence

The latest implementation qualification was retained under
`artifacts/local/firmament-concepts/`. This report checked its final logs and
machine-readable summaries against the current committed implementation. The
full suite was **not rerun during this documentation-only pass**.

| Check | Final recorded result |
| --- | --- |
| Release solution build | Zero errors; 19 existing nullable/analyzer/platform/WASM warnings. |
| Firmament component lane excluding SlowCorpus | 2,436 passed. |
| Focused CLI lane | 35 passed. |
| Required Core fast lane | 1,068 passed. |
| Full solution lane, including corpus tests | **4,914 passed across 20 executing assemblies; zero failures or skips.** Includes 2,450 Firmament and 512 CLI cases. |
| Domain-appropriate CLI witnesses | 21 expected outcomes; invalid builds emitted no STEP file. |
| Complete public examples | Nine compiled through the real compiler. |
| Saved existing STEP baselines | 13 byte-identical outputs, including typed tolerances and Record/Template/Feature explicit PMI. |
| Plain versus Concept-prefixed closed shapes | 17 byte-identical STEP comparisons in regression tests. |
| Slot physical witness | One body, six faces, world bounds `(-20,-6,0)..(20,6,5)` mm. |

The full command was:

```powershell
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
```

Builds used `-m:1 -nr:false -p:UseSharedCompilation=false`; tests ran serially.
The existing FrictionLab project discovers no tests and is excluded from the
20 executing-assembly count. Narrow lanes are not added to the full total.

Representative checks cover the threaded bolt, guitar, industrial ATLAS,
warm-modern house, factory scene, sheet-metal Template, existing PMI and a generic
mounting plate. These are representative compatibility checks, not byte identity
for every showcase artifact. Earlier migration parity counts overlap later
baselines and must not be summed as independent coverage.

The language-service regression checks projection of the compiler's failed
requirement message and original Require span. No fresh live Helios interaction,
remote CI run or universal cross-domain diagnostic parity is claimed. The prior
qualification's final runtime snapshot recorded zero remaining dotnet/test hosts
or Visual Studio build nodes; that is a recorded snapshot, not continuous monitoring.

## X1 acceptance checklist

“Qualified” below describes the stated boundary, not universal frontend coverage.

| # | Original criterion | Current assessment |
| --- | --- | --- |
| 1 | Reuse existing lexer | Implemented; structural parser uses the lossless lexer. |
| 2 | One unified structural V2 parser | Implemented and used by migrated production families. |
| 3 | Typed AST with spans | Implemented. |
| 4 | Parsing independent of whole-document regex routing | Established for the structural parser; remaining production owners still need migration. |
| 5 | Multiple families coexist in AST | Covered by focused structural and production cases. |
| 6 | Mechanical/profile families recognized | Qualified migrated families; original Slot/Fillet/field regressions covered. |
| 7 | Concept/Interface/Assembly/Scene represented | Partial. Canonical Interface Role requirements still fail structurally. |
| 8 | Template/Feature/Pattern structure retained | Structural foundation exists; complete AST expansion remains X2. |
| 9 | Finite expressions represented | Bounded expression/value paths covered; universal evaluator consolidation remains X2. |
| 10 | Unknown intent detectable without omission | Qualified migrated contexts; universal consumption across remaining domain owners unproven. |
| 11 | CanonicalAdvanced failures reproduced | Historical reproductions and mechanical production regressions retained. |
| 12 | Old behavior not treated as automatic truth | Ignored operations/fields reject; accepted compatibility has explicit witnesses. |
| 13 | Canonical corpus qualification | Open: current complete structural/admission collection has not been refreshed. |
| 14 | Differential classification | Historical partial comparison exists; complete current semantic comparison remains open. |
| 15 | Source spans and provenance | Authored spans and bounded expansion provenance covered. Enclosing anchors disclose limits; full expansion maps remain X2. |
| 16 | Route-independence tests | Focused mechanical witnesses pass; not established universally across domains. |
| 17 | Shadow tooling cannot alter production | Normal production admission does not depend on the debug command; user-directed production migration is explicit. |
| 18 | Migrated-family compatibility gates | Qualified within the family reports and retained full suite. |
| 19 | Showcase compatibility | Representative real sources pass; every current showcase export has not been freshly compared. |
| 20 | No new whole-document regex dispatch | No new dispatch architecture introduced by the reported passes. |
| 21 | No new broad source rewrite system | No new broad rewrite system; existing expansion debt is explicitly retained. |
| 22 | Tests/artifact qualification green | Latest full implementation gate passes, with bounded artifact evidence above. |
| 23 | Code-quality cleanup | Modified owners audited in individual pass reports; obsolete readers removed only for qualified families. |
| 24 | X2 work mapped | Decision register and pass limits identify remaining expansion/value/domain consolidation. |

## Smallest path to close the gate

1. Represent Interface Role capability requirements and relevant mate clauses in
   the shared structural parser, then bind them into existing `InterfaceDefinition`,
   `MateSource` and Assembly IR. Preserve name/capability checks and original spans.
   Merely accepting the token `requires` would not close admission ownership.
2. Qualify that family through actual Assembly compilation: accepted and invalid
   forms, complete source consumption, ports/instances/transforms and geometry or
   artifact parity. Retire its obsolete reader after those checks pass. Apply the
   same bounded process to the remaining required domain syntax uncovered by the
   corpus, without adding a parallel validator.
3. Refresh the current corpus using the correct domain owners. Classify legitimate
   V1 compatibility, intentional invalid/speculative cases, unsupported canonical
   syntax and actual regressions explicitly. Compare meaningful fields,
   expressions, identities and semantic receipts rather than acceptance alone.
4. After implementation changes, rerun the serial full regression gate and the
   affected artifact comparisons, then update this verdict from measured evidence.

Full scoped AST expansion, universal expression/value consolidation and complete
expansion source maps remain X2 work. Remaining deliberate syntax reconciliation
belongs to X3; the complete twelve-part authoring retest belongs to X4. Missing
kernel operations remain separate capability work. None should be smuggled into
X1 acceptance or used to excuse the canonical Interface gap. No user policy
decision is needed to undertake the next bounded migration.

## Reproduction and evidence locations

Run from the repository root after building the CLI:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm inspect fixtures/Canonical/AssemblyInterfaces/executable-machine.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll frontend-shadow fixtures/Canonical/AssemblyInterfaces/executable-machine.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate fixtures/Regression/FrontendX1/slot-route-perturbation.firmament --json --explain
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate fixtures/Regression/FrontendX0/unknown-compose-statement.firmament --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate fixtures/Regression/FrontendX0/unknown-boundary-field.firmament --json
```

Fresh local outputs: `artifacts/local/firmament-x1-review/` contains `recheck.json`,
`assembly-production.json`, `assembly-syntax.json`, the three validation outputs,
`source-inventory.json` and `inventory-summary.json`.
Prior full qualification: `artifacts/local/firmament-concepts/` contains
`build-final.log`, `full.log`, `test-summary.json`, `qualification.json`,
`step-parity.json`, `cli-results.json` and `runtime-summary.json`.
These generated directories are ignored local evidence, not promised contents of
a source checkout; this report preserves their material results and replay commands.

## Requested reviewer disposition

Accept the bounded production migrations and their recorded regression evidence.
Keep the overall X1 verdict at **Meaningful progression** until the canonical
Interface gap and current corpus qualification are closed. A green test suite
supports compatibility of tested behavior; it does not establish complete
frontend admission coverage.
