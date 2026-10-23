# Firmament production domain migration

Date: 2026-10-14. Baseline: `83855e91`. This extends the
[mechanical migration](FIRMAMENT-MECHANICAL-FRONTEND-MIGRATION.md), using the
production recursive-descent AST and established engineering lowerers. It is not
a shadow qualification milestone or a claim that all remaining frontends have
been migrated.

## Production changes

Gear families and `Interface<Gear>`, Revolve and its Axis, Panel surfaces and
boundaries, Plateau, Loft, and SectionChain records now consume shared AST nodes. Unknown
fields, duplicate fields, unknown statements, malformed values and unsupported
headers fail at the owning binder. Gear and Revolve retain their bound
engineering records for build; build does not reparse them through another
domain grammar. SectionChain's bound chain can likewise execute through the
ordinary in-memory compiler. Dedicated section-chain commands retain their
inspection/export metadata and use the same AST binder.

The Panel character expression parser is removed. Its shared expression syntax
binds to the existing surface expression algebra; geometry, dimensional checking,
surface factories, tessellation and BRep emission retain their existing owners.
Named `Line S0 { ... }` objects in existing surface lists retain their identity.
Incomplete blocks retain their enclosing AST nodes for editor completion while
remaining invalid for compilation.

The legacy `Model Name mm` header now binds an implicit file body in the shared
grammar, with its original unit-token provenance. Wrapper-free Profile/Extrude
and Compose construction projects its statements into the mechanical scope
without rewriting source. The obsolete Phase3, RoundedBox,
PrimitiveConstructionPolicy and StandaloneProfile document parsers are removed.
Existing geometry lowerers and explicit V1 serialization compatibility remain.

Parser-route names (`UnifiedMechanical`, `UnifiedGear`, `UnifiedRevolve`,
`UnifiedPanel`, `UnifiedSectionChain`) are debug metadata. Structural admission
ownership, rather than a list of route strings, controls the build gate.
Generic traversal now belongs to `FirmamentSyntaxTraversal`, separate from
developer comparison/evidence tooling.

Plateau is an additive feature on an admitted extrusion. Its AST request and
bound base profile now drive execution; the ordinary base cannot execute alone
when Plateau admission fails. Existing geometry tests detect omission through
face counts and exact surface/pcurve checks. The canonical Plateau STEP hash is
unchanged. Source heuristics for unmigrated Wire/Sweep cannot reroute an admitted
mechanical object because of construct-shaped comment text; explicit schema
selection retains its authority.

## Compatibility and diagnostics

Wrapper-free Revolve retains implicit millimetres. Assembly gear definitions can
use top-level Units and repeated identical millimetre declarations. Conflicting
units fail. Existing camelCase/PascalCase field aliases bind through the same
field consumer; authored identities retain their case.

Inline SectionChain frames/profile specializations retain the existing bounded
expansion and generated names. AST spans now own its extraction boundaries. The
generated construction declarations inside the chain are bound through the
existing construction owner; they are neither rejected as unknown material nor
silently discarded. This is not an AST replacement of Template/Pattern expansion.

Gear and Revolve normal CLI validation use the common structured report and
normal engineering preflight. A semantically compatible gear train remains
inspectable, but cannot report normal single-body build admission: build requires
an Assembly for multiple gears. SectionChain validation likewise returns
structured diagnostics and explicit Built/ParsedNotBuilt metadata. Unambiguous
field tokens retain precise locations; old semantic producers without spans
receive a disclosed document anchor. Messages are preserved.

## Qualification

The durable baseline and reproduction inputs are in
[the evidence record](../../../release/FIRMAMENT-DOMAIN-FRONTEND-MIGRATION.evidence.json).
Run `scripts/qualify-firmament-domain-frontend.ps1` after a serial Release build.
It checks 15 gear/revolve sources, 11 section-chain/loft sources, 10 historical
unit-header sources and one Plateau source. Thirty-five baselines produced STEP;
two were already rejected. The script checks acceptance and exact STEP SHA-256 values, and writes
detailed output only under ignored `artifacts/local/`.

Geometry qualification passed: all 37 domain/header outcomes matched, including
35 identical STEP hashes and two preserved rejections. The previous mechanical
corpus also matched all 29 STEP hashes and all four validate/build witnesses.
Sixty focused domain/Plateau checks and 50 focused CLI checks pass. Panel equation
coverage checks nine world-space points and the existing reversed boundary order
for Back orientation. Comment perturbation cases prove WireForm/Sweep/Plateau
text cannot reroute an admitted Box or change its STEP output.

Real CLI inspection passes for guitar, Industrial ATLAS, WarmModernHouse,
warehouse, FactoryX0 and executable-machine. Inspection is not a new claim of
full assembly export, interactive Helios qualification, or collision certification.
The final serial Release build completed with zero errors and nine warnings.
The fast Core lane passed 1,068 tests. The full .NET regression passed **4,721
of 4,721 tests**, with zero failed, skipped, timed out or aborted tests, across
20 nonempty projects (21 result files; FrictionLab discovers zero tests). This
includes the full slow Core corpus, all 2,278 Firmament tests and all 491 CLI
tests. Tests ran with project concurrency one and xUnit assembly/collection
parallelism disabled; no execution budgets or test exclusions were changed.
Earlier failing runs remain under ignored artifacts: specific EdgeFinish messages,
inline SectionChain construction ownership, editor recovery and Plateau omission
were repaired and exercised again through the full suite.

A clean checkout can reproduce the serial settings by writing this generated
configuration under `artifacts/local/` before the full test command:

```xml
<RunSettings>
  <RunConfiguration><MaxCpuCount>1</MaxCpuCount></RunConfiguration>
  <xUnit>
    <ParallelizeAssembly>false</ParallelizeAssembly>
    <ParallelizeTestCollections>false</ParallelizeTestCollections>
    <MaxParallelThreads>1</MaxParallelThreads>
  </xUnit>
</RunSettings>
```

Then run `dotnet test Aetheris.slnx -c Release --no-build -m:1 --settings
<saved.runsettings> -- RunConfiguration.MaxCpuCount=1`. Geometry reproduction
requires the built repository CLI DLL, not an independently installed binary.

## Code-quality review

The migrated Gear, Revolve, Panel, Plateau, Loft and SectionChain files contain no regex
parsers. No diagnostic-code fatality list, whole-document keyword dispatch,
second field schema or new global mutable parser state was introduced. Build
admission ownership is structural; debug route names do not decide execution.
Existing feature admission owns nonpositive EdgeFinish radius/distance errors,
preserving its specific engineering diagnostics during normal preflight.

Gear definition extraction is an Assembly extension point, not validation of an
entire Assembly. It consumes gear declarations and syntax diagnostics within
those spans; the enclosing Assembly owner remains responsible for its own
capability requirements and mates. Tests cover this ownership boundary, preceding
Concept definitions, model summary identity, invalid optional values, and
unchanged accepted geometry. Language-service context tests cover incomplete
Loft blocks; they do not claim interactive Helios qualification.

## Remaining production owners

| Owner | Actual remaining dependency before deletion |
| --- | --- |
| Lowercase V2 | The [next production cutover](FIRMAMENT-LOWERCASE-AST-MIGRATION.md) migrates primitive solids, Box derivation, exposure aliases, Hole modifications and region/cut to AST binding. Imported-face recognition/replacement, let/PMI/concept applications and lattice fill still retain semantic adapters. This is distinct from explicit version-1 serialization. |
| Concept / Interface / profile derivation | Existing semantic resolution and several pipeline/path/Span readers remain source-based. Migrated record boundaries do not imply these evaluators have been consolidated. |
| Template / Feature / Pattern | Existing finite specialization and expansion still produce source. Replacing them requires scoped AST substitution with identity/provenance parity, rather than removing their current owner behind an AST gate. |
| Wire / Sweep | WireForm, WireRoute, coils, knots and circular sweeps retain their existing source binders and state replay. |
| Assembly / Scene | Product containment, capability requirements, mating, published ports, appearance and spatial compilation retain their existing owners. |
| Other domains | SheetMetal, Piping, Structural, PlasticShell, Sculpting and Garment have separate semantic/compiler entry points that have not been replaced by this change. |

These routes are deliberately retained until their real consumers are migrated
and qualified. No compiler-wide completion or universal rejection guarantee is
claimed. The next cutover must carry the existing domain records and their
binding/expansion semantics into the shared AST, then remove each source reader;
a structural parse gate alone is insufficient.

The compile-time object-construction mental model is documented in the public
[overview](../../../public/reference/language/README.md). It does not introduce runtime
objects, inheritance, general loops or new copy/reference semantics.


## Concrete next boundary

`fixtures/Canonical/AssemblyInterfaces/executable-machine.firmament`, line 3,
contains `Role A requires AxisCapable` and `Role B requires AxisCapable`. The
ordinary `asm inspect` exits 0, while the shared syntax parser reports `firmament-syntax-declaration-tail` at columns 39
and 68: `Unexpected 'requires' after Role A/B`. Its existing owner is
`AssemblyM0Parser.ParseInterfaces`; mate and requirement binding continues into
`InterfaceDefinition`, `MateSource` and the existing Assembly compiler.

The smallest Assembly cutover prerequisite is representation of capability
requirements and mate relations in syntax, followed by binding those nodes into
that existing IR. Merely adding a syntax gate would reject legitimate legacy
assembly source; merely adding a `requires` token without its binder would not
admit or check the requested relationship. The old Assembly parser is therefore
retained pending that migration and qualification.


## Executive verdict

**Meaningful progression.** The named production families and four removed
compatibility routes are qualified against actual compilation and geometry.
The requested whole-language migration is **not complete**: lowercase V2,
Wire/Sweep, Assembly/Scene and the other listed domain owners remain. The
capability-role witness above identifies a specific unsupported AST/binding
boundary that prevents safe deletion of the Assembly reader. No new shadow-only
milestone or parallel validator is proposed; the remaining work is direct syntax
and binding migration into the existing engineering IR.
