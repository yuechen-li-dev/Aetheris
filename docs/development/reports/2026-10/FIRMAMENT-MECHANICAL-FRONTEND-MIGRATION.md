# Mechanical frontend production migration

Date: 2026-10-14. This supersedes the production-status statements in the
[X1 foundation qualification](FIRMAMENT-FRONTEND-X1.md). That report remains a
historical record of the earlier shadow-only implementation.

## Production change

Normal mechanical `Model` and material-bearing `Struct` documents now enter the
shared lexer and recursive-descent syntax tree, then bind into existing Firmament
engineering records. `validate` and `build` use this admission path. The route
metadata is `UnifiedMechanical`; it is an implementation detail.

The mechanical frontend no longer chooses Canonical versus CanonicalAdvanced
grammar because a source happens to contain a Profile, Concept or Slot2. It selects
an existing engineering lowerer from the actual material declaration nodes. An
unused guide cannot change Box/Hole syntax. Competing material families are rejected instead of silently choosing one.
Multiple explicit XY extrusions bind to the existing section-stack composition
IR, retaining each material request.

Primitive dimensions (including Concept-valued Box field consumption),
Hole/EdgeFinish/Perforation fields, bounded dimensional
arithmetic, closed-boundary fields, ordinary profile guides and extrusion headers
bind from AST nodes. Compose Base/Add/Remove, Boss/Pocket and Placement records
also consume AST fields. Compose Hole/Slot record boundaries and field consumption
use AST nodes while retaining their established bounded engineering value readers.
Unknown Compose statements are errors at the Compose owner. Unknown fields are
reported by the owner after its actual field reads; there is no duplicate global
field schema or error-code fatality allowlist.

Profile and Compose admission retains the bound profile/composition engineering
inputs for execution. Build cannot bypass failed mechanical admission by entering
a source parser later. The exporter no longer repeats the top-level frontend expansion pipeline;
it consumes the admitted expansion and engineering inputs. Existing
kernel, topology, feature identities, materialization and STEP exporters remain
the engineering authority.

Deleted debt includes the old canonical root scanner and primitive/Modify
declaration parsing, the unused canonical Hole/EdgeFinish parsers, whole-document
advanced-keyword selection, ordinary guide/header recognition regexes, and the
separate profile character-based measure parser. The STEP matcher also passes
original source to the shared compiler instead of stripping Match bodies and
leaving damaged syntax behind.

## Motivating reproductions

| Source evidence | Before migration | Required production outcome |
| --- | --- | --- |
| `FrontendX1/slot-route-perturbation.firmament` | Box/Hole succeeds without the unused Slot2; adding Slot2 selects CanonicalAdvanced and rejects | Both forms admitted; identical STEP text and unchanged topology |
| `FrontendX0/unknown-compose-statement.firmament` | Fillet LostIntent disappears while the pocket body builds | Parser, normal validate and build reject `Compose:Fillet`, located at the original Fillet token |
| `FrontendX0/unknown-boundary-field.firmament` | Rect2 heigth disappears during boundary lowering | Validate/build reject `Rect2:heigth`, located at the original field |
| `CanonicalGeometry/profile-chamfer-reflex-junction-top.firmament` | Generic validation rejects an admitted member-set selection while build succeeds through its profile bypass | Selection member-set is bound; validate/build both admit the existing geometry |

The source specimens remain in `fixtures/Regression/`. Tests also exercise
duplicate dimensions, malformed tokens inside a rewritten boundary, mixed
material bodies, ordinary Struct/Model equivalence, unrelated valid construction
siblings, original syntax locations after comments and existing static Match
syntax before its unchanged evaluator runs.

## Diagnostics and compatibility

The existing structured CLI contract remains: code, severity, explanatory
message, file, span/line/column where available, stage/context and a disclosed
location limitation otherwise. Syntax diagnostics retain their explanatory text
through build as well as validate. Transformed-source offsets are never presented
as precise original offsets; the original AST supplies the location when possible.
Existing semantic-source messages and capability diagnostics remain intact.

`valid` with validation level `Built` includes normal engineering compilation in
memory. `--parse-only` and external descriptor-pack analysis remain explicitly
`ParsedNotBuilt`. Static admission does not prove kernel execution cannot fail.
Warnings retain their existing severity. V1 serialization compatibility keeps its
explicit version boundary and does not become fallback for recognized V2 errors.

PascalCase and existing camelCase field aliases are preserved. Primitive Hole
centers retain the existing Point2 requirement; this migration does not redesign
point syntax. Identical repeated `Units: mm` declarations emitted by existing
module composition remain admitted; conflicting units fail. Historical
`Role: ProfileGuide` boundary annotations remain admitted. Typed Concept/Expose
and named construction-plane semantics retain their established semantic owner.
Profile selections support the already-built member-set form without requiring
the unrelated Target field, and repeated Source entries are retained and checked.

The optional `frontend-shadow` command remains a developer evidence tool over the
same syntax parser. Structural success alone grants no engineering admission.
There is no new shadow-only runtime or migration milestone.

## Additional canonical-source defect exposed

FactoryX0's Bin template declared two Extrudes (`bottom` and `sides`). The old
profile route selected only the first; the unused rim profile also had colliding
inner/outer segment names. AST consumption exposed both problems. Multiple XY
extrusions now bind into the existing section-stack lowerer, and the canonical
Bin rim explicitly names its inner segments with existing `As` aliases. This is
an intentional correction to previously omitted geometry: bins now contain their
requested side walls. It is not claimed as unchanged historical Bin geometry.
The focused witness proves a second extrusion reaches Z=8 rather than stopping
at the first extrusion's Z=2; factory checks exercise the real canonical source.
No new kernel operation, parser route or source macro implements this repair.

## Remaining boundaries

This is production mechanical migration, not replacement of every Firmament
domain. Dedicated Revolve, WireForm/WireRoute, Sweep, SectionChain, Panel, Assembly,
Scene, SheetMetal and other domain owners retain their existing semantic and
geometry paths. AST material declarations identify the boundary; source-wide
incidental guide keywords do not select mechanical grammar. A complete language
frontend replacement must migrate those owners deliberately.

Template/Feature/Pattern and some profile/path derivation transforms still use
existing source expansion. Their complete scope/provenance migration and semantic
expression consolidation remain separate work. Some semantic adapters still read
bounded source values. Legacy lowercase, old no-brace headers and bare standalone
Profile document forms also retain compatibility admission; this does not claim
that every historical mechanical spelling has migrated. This report does not claim universal field-consumption
coverage across those unmigrated domains or all 1,232 historical/speculative
sources. General non-XY support, Fillet+Hole capability and syntax reconciliation
have not been added. All 24 recommendations retain an explicit disposition in the
[language decision register](FIRMAMENT-LANGUAGE-DECISIONS-2026-10.md).

## Qualification

**Accepted for the production mechanical Model/Struct migration.** This is not
whole-language X0 acceptance: the compatibility and dedicated-domain boundaries
above remain explicit. The requested production path and motivating defects are
repaired without a shadow-only admission layer.

- Release solution build: zero errors (11 existing warnings in the qualification
  build).
- Fast Core lane: 1,068 passed.
- Focused complete Firmament project: 2,236 passed.
- Full solution, including the slow corpus: **4,675 passed, zero failed or skipped,
  across 20 nonempty test projects**. FrictionLab's existing empty test assembly
  discovers zero tests.
- Real CLI admission witnesses: 4/4 have the expected, consistent validate/build
  outcome. Both rejected intent-loss sources retain actionable original-token
  diagnostics; Slot2 and the existing profile member-set selection are admitted.
- Same-checkout pre-change STEP comparisons: **29/29 byte-identical**. The set
  covers primitives, holes, edge finishes, Boss/Pocket, slots, profiles,
  closed boundaries, PMI and the threaded bolt. Five additional historical X0
  showcase comparisons also match (two overlap this set).
- Ten real showcase CLI checks pass: threaded bolt, PMI, sheet metal, templates,
  radial pattern (build/STEP); guitar, industrial ATLAS (assembly inspection);
  warm-modern house, warehouse and factory (scene inspection). Inspection is not
  being represented as an additional STEP export or interactive Helios session.

The normal full run passed all CLI and then-current Firmament tests but hit three existing
Core native display execution budgets. The final full lane uses the existing X0
serial xUnit settings (`ParallelizeTestCollections=false`, `MaxParallelThreads=1`)
and `RunConfiguration.MaxCpuCount=1`; all 4,675 tests pass without excluding tests,
changing execution budgets or modifying kernel code. Raw failed and final runs
remain separate in the ignored evidence directory.

The dedicated quality pass checked ownership of field reads, duplicate and
unconsumed fields, unsupported headers/variants, full consumption of bounded
feature values, severity, V1 fallback, source locations, message propagation,
parser/build duplication and global state. The existing parse cache is reused;
no new global parser state, broad regex dispatch, global rewrite system or
fatality allowlist was added. `git diff --check` passes. Existing tests asserting
the old broken Slot2/Fillet behavior now assert actual production acceptance,
rejection and geometry; tests do not require exact diagnostic sentences.

The compact [evidence record](../../../release/FIRMAMENT-MECHANICAL-FRONTEND-MIGRATION.evidence.json)
contains the per-source hashes, outcome witnesses, showcase commands and complete
project test totals. Raw output is under ignored
`artifacts/local/mechanical-frontend-migration/`. The geometry/admission script
uses the retained pre-change manifest or, when those ignored artifacts are absent,
the recorded release hashes:

```powershell
dotnet build Aetheris.slnx -c Release -m:1 --no-restore
./scripts/qualify-firmament-mechanical-frontend.ps1
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 --settings artifacts/local/mechanical-frontend-migration/serial.runsettings -- RunConfiguration.MaxCpuCount=1
```

For a fresh checkout, create the ignored `serial.runsettings` used above with:

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
