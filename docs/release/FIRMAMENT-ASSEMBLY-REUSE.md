# Firmament assembly reuse and public contracts

Date: 2026-10-15. Implementation base: `96aa41ca58f4facd46bb7e8afa93f18524dc068d`.

## Result and scope

This pass implements the approved two-part Editable V8 design through production
compilation: keyed typed Subassembly reuse, and Concept-owned layout with
Interface-owned installation/fit relationships. Motion language design remains
separate. **Verdict: Success for this approved slice; X2 remains open.**

The engine imports one `Template<config: CylinderConfig> Subassembly CylinderUnit`.
A finite keyed Set uses immutable `with` derivations and nested `CylinderRest`
Records. One Pattern creates the eight units and their installation/journal-fit
Mates. The Cylinder definition owns sixteen physical Parts, its local layout,
Fixed interfaces, seating Mates and pin/spring fits. The engine installs the
complete public mount once per key.

The eight copied cylinder sources and all eighteen pose/matrix data files are
removed. Crankshaft, crankcase, cam banks and the smaller staged witnesses use
scalar expressions. C# retains the existing prescribed mechanism and writes only
engineering dimensions plus compact scalar rest inputs; it does not generate the
Firmament product tree or replacement meshes.

Read [the engine](../../fixtures/Canonical/EditableV8/engine.firmasm),
[the single Cylinder](../../fixtures/Canonical/EditableV8/assemblies/cylinder.firmament)
and [the authoring guide](../public/firmament/editable-mechanisms.md).

## Compiler ownership and repairs

- Inventory retains typed Subassembly exports in their definition modules and
  specializes them with the existing Template/Record binder. Private supporting
  declarations stay private. Nested checked host Record views become ordinary
  nested Record bindings, rather than type-marker strings masquerading as values.
- Set entry boundaries come from mapping AST nodes. Literal Records, aliases and
  immutable `with` values share the Record owner and its field/type checks. The
  old literal-only Set field checker is removed.
- Mapping blocks are represented in the unified AST, traversal, rewriter, writer,
  lexical data/Template binding and source-origin indexing. Assembly Pattern
  expansion admits occurrence/Concept/Interface/Mate declarations locally,
  qualifies occurrence references and gives local declarations hygienic names.
  Unknown body intent and duplicate declarations reject. Key order does not own
  occurrence identity.
- Imported assembly identities retain expression token boundaries: whitespace
  normalization must not turn `standard with` into `standardwith`. The existing
  legacy one-parameter Assembly compatibility form keeps its normalization.
- Nested visible Axis members can seat a complete unit through its published
  endpoint. Private descendants stay inaccessible. Scalar vector/station values
  use the existing dimension-checked evaluator. Ordinary seating and dimensional
  fit admission have separate responsibilities; fits do not add another driver.
- Published aliases bind against the solved local root path, rather than an
  unrelated private definition name. Occurrence and solved-definition identities
  agree, including AP242 definition-owned child usages.
- Definition-owned Record leaves reach semantic ports. WireForm winding ports are
  obtained from the same AIR/datum factory used after materialization. CamelCase
  wire fields use the existing spelling metadata. The spring seats its actual
  winding endpoint; the starting tangent is not used as a guessed coil axis.
- SheetMetal resolves its existing product/profile library imports before shared
  Record admission. Both normal part compilation and enclosure assembly entry
  points use the same domain-owned resolver. CLI inspection, validation, STEP, USD
  and GLB entry points also use that prepared domain entry instead of providing
  a geometry delegate to an unprepared parser. The low-level parser Concept test
  now supplies that domain context, checks a valid control, then removes the Lid
  member and checks the same specific Concept diagnostic before geometry.

There is no new whole-document parser dispatcher, parallel schema checker,
source-macro system, diagnostic fatality allowlist or global parser state. The
SheetMetal change consolidates its existing import transformations; it does not
add another import spelling or library definition.

## Physical endpoints and identity

The crankshaft publishes its real crankpin ports. The parent compares those
journals with the Cylinder's published rod big-end bore. The local wrist-pin fit
uses the rod's actual small-end port. Spring fits use WireForm clear diameter and
the stem's definition-owned diameter. The former virtual `SeatRodN` and
`SeatWristPinN` copies are gone from the V8.

A physical path is now `EditableV8.Cylinders.C1.Cylinder.Piston`, with logical
motion labels retained only as explicit binding inputs. Baseline/revised sources
retain keyed IDs. This intentionally changes the old flat occurrence hierarchy;
it does not claim byte identity of the V8's complete STEP product structure.
An obsolete concrete compressed-spring module is also removed; pitch specialization owns that variant.
The full V8 has 171 physical Parts, 21 geometry definitions, and 193 total tree
occurrences including the root and compiler-owned Pattern groups.

## Diagnostics and compatibility boundaries

Bad Record overrides retain their explanatory field/type messages. Assembly
value-expansion failures identify the original enclosing Assembly where available
and disclose that the expander supplied no expression-level span. Specialized
module errors retain their defining/export context. This is not a claim of full
expansion source maps.

Imported Part Templates admit scalar/enum inputs. Caller Record/type arguments
are explicitly rejected at that deferred materialization boundary, preventing a
same-named private Record from supplying different geometry. Typed Subassemblies
carry checked Record values, including nested rest bundles. Caller-defined type
arguments and typed Assembly/Scene imports still require definition-module
specialization followed by a concrete export.

The existing legacy Assembly Template route remains a compatibility consumer;
this pass does not remove it before a separate family migration. Template-local
Static Records forwarded to nested product Templates remain a known X2 binding
boundary; this pass does not claim that every such combination is qualified.

No new motion/animation syntax, mechanism solver, kernel operation or Helios
validator is added. The browser evaluator remains the existing prescribed motion
projection. Sheet-metal display export can still reject an unsupported trimmed surface with
`assembly-mesh-definition-failed`; its valid STEP geometry is not silently
substituted. This is a separate existing tessellation capability.
A fresh interactive browser/rendering audit is outside this pass;
actual exported geometry and the real JavaScript evaluator are qualified.

## Qualification

The final release solution build passed with two existing warnings. The complete
serial solution gate passed **5,070 tests in 20 executing assemblies, zero failed
and zero skipped**. The focused compiler lane passed 96 tests, the focused
SheetMetal compatibility lane passed eight, and the Core fast lane passed 1,068. Two CLI domain-import regressions additionally
exercise inspection, full validation, real STEP export and truthful display-mesh
capability rejection for the existing sheet-metal compatibility products.
The full Core lane, including SlowCorpus, passed 1,201; the fast lane is not used
as a substitute for the full gate.

All five original/ported variant comparisons pass. Every compared local position,
normal and index array is exactly equal. Maximum world-matrix differences are:

| Variant | Physical Parts | Geometry definitions | Maximum matrix difference |
| --- | ---: | ---: | ---: |
| Stage A | 11 | 8 | 0 |
| Stage B | 20 | 14 | 3.56e-15 |
| Four-cylinder bank | 98 | 21 | 1.71e-13 |
| Baseline V8 | 171 | 21 | 1.71e-13 |
| Revised V8 | 171 | 21 | 2.28e-13 |

Baseline and revised kinematic/clearance and spring-axis sweeps pass. The real
JavaScript evaluator matches 27,360 scalar matrix components per variant against
C# at the exported sample angles, with maximum error 2.85e-14. CLI validation of
the checked-in engine reports `success: true`, `validationLevel: Built`, and no
diagnostics. The static Scene exports GLB with 21 definitions and 194 tree
occurrences. This does not add runtime motion to the Scene.

The complete current corpus contains **1,326 sources**, with all **368 canonical
sources structurally represented** and **zero unresolved mismatches**. All 41 new
V8 source modules are admitted standalone or through exact successful consumer
receipts. Every pre-existing source retains its previous classified outcome,
including the two sheet-metal compatibility products. All **26 showcase STEP
baselines are byte-identical**. This is a measured corpus qualification, not a
claim of universal semantic parity for every possible program.

The dedicated code-quality pass checked parser ownership, shared Record/schema
binding, declaration hygiene, diagnostic propagation, V1 compatibility, source
provenance limits, cache/identity consistency and absence of new global parser
state. `git diff --check` is clean. The final artifact run uses a fresh directory
so stale removed modules cannot participate in qualification.

Final evidence is collected under ignored `artifacts/local/editable-v8-modules/`.
The [adjacent compact JSON](FIRMAMENT-ASSEMBLY-REUSE.evidence.json) records final counts, corpus dispositions, artifact
parity and source hashes. Intermediate failing runs are not final acceptance.

The durable `geometry-reference.expected.json` fixture captures the original
baseline at the implementation-base commit: 171 logical Part bindings, local
mesh fingerprints and solved world matrices. The adjacent compressed numeric
golden now permits cross-platform comparisons: its bytes must match those original
fingerprints, while live positions/normals use a 1e-9 component tolerance and
indices remain exact. World matrices retain a 1e-9 tolerance. See the
[fixture contract](../../fixtures/Canonical/EditableV8/README.md) for format,
Windows/Linux measurements and recovery instructions. The JSON retains the original
generated-source hash and provenance. Per-run mesh exports stay ignored.

Reproduce the build/test lanes from the repository root:

```powershell
$env:MSBUILDDISABLENODEREUSE = '1'
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1
./demos/Aetheris.EditableV8/Run.ps1 -Output artifacts/local/demos/editable-v8 -NoServe
```

The Run script uses a serial build with node reuse/shared compilation disabled,
then executes the produced DLL. The post-qualification process snapshot contains
no .NET build/test hosts. It preserves the existing foreground preview
behavior. No background service or startup task is created.
