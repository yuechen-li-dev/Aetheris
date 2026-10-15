# Firmament Inventory Records — module binding first pass

Date: 2026-10-15. Implementation base: `263f0128`.

**Success for the Inventory module first pass. X2 remains open for the stated
semantic and source-adapter boundaries.**

## Authoring decisions

`Inventory Record Components { bracket: Part = Firmament("parts/bracket").Bracket }`
provides qualified, checked names for concrete Part, Subassembly, Assembly and Scene
modules. Inventory availability creates no occurrence or quantity. Existing
`include` remains a short unqualified composition, including concrete Parts and
legacy Template applications. There is no enforced catalog-size threshold.

Constructs, types, adapters and catalog names use PascalCase; catalog members use
camelCase. Exported/domain/user names keep their spelling. Locators normally omit
extensions: Firmament resolves `.firmament` / `.firmasm`, Step resolves `.step` /
`.stp`. Ambiguity rejects; explicit extensions remain the repair. Paths resolve
relative to their declaring document. The established snapshot-only root-relative
include spelling remains a fallback when the document-relative target is absent.

Entries import concrete modules. Templates specialize within their defining source
and export the resulting product. Cross-module Template value arguments are not
admitted by this first pass, preventing caller values from being captured by a
private Static in the imported module. Source snapshots still contain source only:
STEP/InlineStep assets reject, and concrete Part resource adapters without a
project-aware context reject explicitly rather than accessing the filesystem.

## Ownership and repairs

`FirmamentModuleGraph` uses the existing recursive-descent AST for dependencies,
Inventory fields, export-kind checks, lexical catalogs and occurrence reference
binding. Each original source has its own node. Mapping bodies bind module
references before Pattern expansion; iteration symbols and parameters shadow outer
catalogs. The existing already-resolved parser API retains its supplied project
context, including source-backed published-port dependencies. Inventory imports do not inject
foreign declarations into a global Record/Template catalog. Legacy includes retain
an explicitly source-mapped declaration projection into their consuming scope.
The two regex include-discovery implementations are removed.

Native Parts use the existing Firmament build and exact AP242 importer. Imported
Assemblies/Subassemblies use the existing local solve, published ports, Interface
and Mate owners. Scene composition reuses those engineering products and composes
nested Scene geometry, hierarchy, appearances and frames. Child cameras remain
private. No geometry operation or placement solver was added.

The shared grammar now recognizes the Inventory modifier and a product-tree tag
following a newline-terminated field. The `.firmasm` profile counts actual root
AST declarations instead of guessing from line starts. Inventory is registered
with the existing generated schema/completion metadata.

Module identities derive from project-relative paths, kind and export. Native file
and source-snapshot builds retain the same definition identities, definition STEP
hashes and world transforms. Their overall artifact digest can differ because it
also contains intentionally different original-source provenance.

`validate` uses ordinary Assembly/Scene engineering admission unless parse-only is
requested. Parse-only reports `ParsedNotBuilt`; it does not promise geometry.
Unknown adapters/members, invalid headers, duplicate names, private-scope references,
kind mismatches, missing exports/resources, cycles and ambiguous locators reject.
No fallback frontend interprets those failures as a different language.

## Diagnostics and provenance

Inventory/resource/syntax diagnostics retain their original source file and span.
The included-syntax regression now checks the original `bad.firmament` offset rather
than accepting a synthetic `#expanded-includes` location. Source projections map
only diagnostics belonging to their own producer document; imported diagnostics
are not remapped through the caller's offsets.

Engineering lowerers retain bounded source adapters. A lowerer that cannot provide
a precise expression span reports the enclosing exported Part and discloses that
limitation. Complete per-node generated-source and expansion maps remain X2 work.
Ordinary Assembly IR and the compiled Scene source model retain Inventory binding
receipts and source dependency hashes. CLI Scene inspection exposes the composed
nodes and their source anchors; its existing report does not yet project those
additional source-model receipt collections. No product-specific validator was introduced.

## Regression and geometry qualification

The final serial solution gate passed **5,051 tests across 20 executing
assemblies**, with zero failures/skips. The release solution build has zero errors
and 10 existing warnings. Focused compiler tests: 76; focused CLI
tests: 4; required Core fast lane: 1,068; public documentation
qualification: 13. Focused/fast counts are not added to the full total.

The complete corpus contains **1,285 sources**, including **327/327 canonical
structural successes**, with zero unresolved classification mismatches. All 1,276
inherited sources retain their hashes, AST fingerprints, owners, production
levels/exits, diagnostic codes and dispositions. Nine new module sources account
for the entire inventory change. Six are directly admitted, two declaration modules
are proved in actual consuming Assemblies, and the explicit negative rejects.
The standalone Subassembly's module-context diagnostic does not claim it is a
runnable mechanical data Record. Canonical classifications remain 285 direct
admissions, 33 actual consumer contexts and nine existing domain limitations.

**All 26 established STEP/AP242 baselines are byte-identical.** The replay checks
source hashes before comparing artifacts, including guitar, assemblies, symmetry,
Sculpting and typed-tolerance/PMI examples. The second retained guitar compile
reuses its tracked definitions; detailed counts are retained in the compact record.

Real CLI replay validates/builds/reimports assembly STEP for the machine and short-include
Assembly, validates/inspects/exports GLB for the factory and child Scene, and proves
that unknown members fail both validate and build without a STEP artifact. Native
component tests also compare source-snapshot definition identities, STEP definition
hashes and world transforms against disk compilation.

Post-run snapshot: **0 remaining .NET/test/build hosts**, 11.03 GiB free RAM,
91.93 GiB free disk. This is not a long-duration leak or browser-interaction claim.

Current source inventory SHA-256: `8F76A0EE96B4912DA5CD6DE8AAA11358DF2378800DFCCF1A7CE8CB00A327925F`.

Raw outputs stay under ignored `artifacts/local/firmament-inventory/`. Final gates
are `final-build.log`, `final-focused.log`, `final-cli.log`, `final-fast.log`,
`final-full.log` and `final-docs.log`; complete corpus output is `corpus/report.json`
and `classified.json`. STEP replay is `geometry-parity.json`, module replay is
`module-replay.json`, and host state is `runtime-summary.json`. Earlier intermediate
runs remain reproduction evidence and are not substituted for the final gate.

Portable record: [Inventory compact evidence](FIRMAMENT-INVENTORY-MODULES.evidence.json).


The complete fixtures are [the machine](../../fixtures/Regression/Inventory/machine.firmasm),
[the factory](../../fixtures/Regression/Inventory/factory.firmament),
[short includes](../../fixtures/Regression/Inventory/include-simple.firmament) and
[missing-member rejection](../../fixtures/Regression/Inventory/missing-member.invalid.firmament).
Component tests cover private schemas, catalog/Pattern shadowing, source/snapshot geometry,
retained cache reuse/invalidation, STEP import, published ports through the real
Mate solver, source-only resource rejection and original syntax locations.

## Code-quality review and remaining boundaries

The modified seam is a module-binding owner, not another whole-document parser,
expression evaluator or text macro system. Private transport identifiers replace
only AST-bound occurrence reference spans. Original engineering lowerers remain the
source of geometry and capability admission. There is no compiler-global mutable
module registry or background service. Graph traversal and retained sessions have
explicit finite bounds; cycles reject rather than truncating expansion.

This pass does not complete all X2 semantic migration. Remaining collection/domain
readers, generated-source maps and the general typed cross-module specialization
contract remain explicit work. Unused Inventory entries resolve and kind-check;
placed occurrences request engineering materialization. STEP supplies one Part
through the existing strict importer; importing STEP assemblies as Inventory
Assemblies is not introduced here.

## Replay

```powershell
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate fixtures/Regression/Inventory/machine.firmasm --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm export-ap242 fixtures/Regression/Inventory/machine.firmasm --out artifacts/local/firmament-inventory/machine.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll scene inspect fixtures/Regression/Inventory/factory.firmament --json
```

Language reference: [syntax.md](../public/firmament/syntax.md),
[Inventory guide](../public/firmament/inventory.md),
[decision register](../development/FIRMAMENT-LANGUAGE-DECISIONS-2026-10.md).

## Measured artifact witnesses

All bounds are in millimetres. Occurrence totals include product-tree containers;
body totals count physical Parts.

| Fixture | Export and reimport | Physical bodies | World minimum | World maximum |
| --- | --- | ---: | --- | --- |
| Machine | AP242 assembly, reimported | 4 | `[-15, -10, 0]` | `[155, 10, 9]` |
| Short includes | AP242 assembly, reimported | 2 | `[-15, -10, 0]` | `[32, 10, 8]` |
| Factory | GLB, Scene inspection | — | `[-15, -10, 0]` | `[515, 10, 9]` |
| Workcell | GLB, Scene inspection | — | `[5, -10, 0]` | `[35, 10, 6]` |

The native Assembly instance bounds and body counts match the exported STEP
reimport. Scene bounds come from ordinary composed Scene inspection, with a
successful GLB export. The compact record retains definition/occurrence counts,
artifact hashes and retained-session reuse measurements.
