# Pattern authoring — compiler and visual editing audit

> Historical audit snapshot. Repairs and current qualification are tracked in
> [the implementation report](PATTERN-AUTHORING-IMPLEMENTATION-2026-10-20.md).

Date: 2026-10-20. Source baseline: Aetheris `ad6dc5ce`, HeliosCAD `f038b19`.

**Outcome: audit complete; broader Pattern exposure is blocked by a confirmed
silent geometry defect.** The equation engine is real, but Function parameter
shadowing can change admitted engineering geometry. A full visual Pattern editor
is not implemented in this pass. Thirty-two source specimens, seven canonical
controls and real browser interactions identify the required repairs.
No compiler, SDK or product implementation code changed during this audit.

## The language model to expose

```text
Function law + checked inputs
             ↓
Concept Points (Linear / Stations / Series), or keyed Set<Record>
             ↓
Pattern application → Feature construction OR Part/Subassembly occurrences
```

`Function` computes scalar values. `Series` generates a bounded collection of
Point2/Point3 values. `Feature` constructs feature intent. `Pattern` applies a
recipe to a finite source. These responsibilities already exist; no new generic
loop runtime, constraint solver or host-code evaluator is needed.

Linear and circular patterns are convenient layouts. They must not become the
editor's definition of Pattern. The guitar's fret law controls placement and
width; its inlays use independent named distances along a direction. Preserve
those laws and datums instead of turning them into editable coordinate dumps.

## Reproduced capabilities

| Lane | Witness and result |
| --- | --- |
| Nonlinear part pattern | `equation-plate`: Function x = -30mm + 5mm*n*n; four real through holes at X=-30,-25,-10,15mm. Normal validate and build accept. |
| Direct expression / Pow | Direct arithmetic in Series position and dimensionless Pow(n,2) both build. |
| Keyed part mapping | Changing pitch to 6mm changes coordinates, retaining the four Pattern keys/generated identities. |
| Irregular stations | Named distances 0,17,45mm build; reordering declarations retains each key's coordinates. |
| Function/Series assembly | Existing 12-fret witness builds and exports through the ordinary assembly pipeline. Resizing scale retains occurrence identities and changes transforms. |
| Growing Series | Count 12→13 retains the existing occurrence identities as a subset. Fixed Pad geometry hashes remain unchanged. |
| Point3 Series | Explicit Point3 values and frame-relative XYZ placement work; the twelfth member resolves to world Z=64mm. No implicit Point2 promotion. |
| Empty source | An empty typed Set yields zero mapped holes and builds stock. Zero-count Series is rejected under its existing 1–1024 contract. |
| Guitar | Actual root inspection accepts: 22 fret Parts; Concept collections contain six crossings, 22 fret sites and nine inlay stations. Current artifact has 99 material instances and 56 geometry definitions. |
| Other canonical owners | Record-array Hole and Slot, keyed Set→Feature, semantic Grid, partial radial feature symmetry and Scene Pattern all build/inspect successfully. Guitar also exercises SectionChain Patterns and repeated subassemblies. |

The nonlinear plate has exact compiler volume **39773.80532894153mm³** =
40000−72π and stock bounds [-50,-25,0] to [50,25,8]mm. This checks actual four-hole
geometry, not merely parser output. The assembly identity/hash comparison is
for the fixed Pad witness; changing fret-width arguments can intentionally
change the guitar's leaf geometry.

## Findings and ownership

### P01 — Part/assembly parameter admission differs (high priority)

**Observed:** part Series count/spacing literals build. A checked Static count
or spacing, at Model or module scope, is rejected. Equivalent assembly Static
count/scale references build. `ui-promoted-count` is the actual source downloaded
from Helios after its offered Make count a parameter action; it fails normally.
The working model is correctly retained, but the operation itself is not useful.

**Root cause:** Assembly's `CanonicalStaticAuthoring.ExpandAssemblyPatterns`
reads and substitutes checked scalar inputs before Concept points expansion.
Part `Expand` runs `ConceptPointAuthoring.Expand` without that same bound value
environment. The point owner expects literal integer bounds and scalar
expressions whose variables are only the Series index / Function parameters.

**Repair:** pass the existing checked lexical value environment to a shared
AST-based point/law binder. Resolve integer bounds as checked Int values before
limits are enforced. Resolve Length/Angle inputs through the existing scalar
owner. Do not add another UI evaluator or another global substitution pass.
Literal-only Series is not a sensible permanent engineering language rule.

### P02 — Unrelated Static captures a Function parameter (P0: wrong geometry)

**Observed:** `Distance(n: Int, scale: Length)` computes the fret law and is
called with `Distance(n,628mm)`. The baseline twelfth fret is [0,-314]mm local,
[0,344,62]mm world. Prepending `Static scale: Length = 100mm;` still validates,
builds and exports successfully, but changes that fret to [0,-50]mm local and
[0,608,62]mm world. Renaming only that unrelated Static to `catalogScale`
restores the correct result. This is accepted **wrong geometry**, not merely a
poor diagnostic or an invalid fixture.

`function-parameter-shadow-assembly` and its `-control` preserve the pair.
The Function argument remains 628mm in both. The module Static's 100mm has been
substituted into the Function body in place of its own bound parameter.

A related part symptom appears when a Model Static is named `pitch`: a module
Function's own `pitch` parameter use fails with
`firmament-binding-name-not-visible:pitch`. A differently named Static avoids
that visibility diagnostic, but still hits P01 when passed into a part Series.

**Root cause:** `Syntax/FirmamentDataBinding.Collect` creates parameter scopes
only for declarations with block bodies. An expression-bodied Function has no
block body. `Rewrite` visits its initializer in the outer scope rather than a
Function parameter scope. This can misclassify an ordinary bound parameter as
an inaccessible or captured compile-time value. In assembly, subsequent scalar
substitution replaces the misbound reference with the unrelated Static value;
Function type checking cannot recover the lost parameter binding.

**Repair:** establish parameter scope for expression-bodied declarations and
bind their initializer in that scope. This is lexical binding repair, not a
proposal to add closures or implicit global captures.

### P03 — Part inspection loses the author's collection name/provenance

**Observed:** the equation plate inspection names its Pattern source
`__ConceptPoints_6_Layout5_Sites`. Assembly expansion already maps generated Set
names back to the logical collection name. Concept point IR carries Recipe and
string provenance, but no original per-field source spans. Some displayed
association spans refer to rewritten/generated source.

**Repair:** retain original AST collection/law/application identities, original
source file/revision/spans and index/key provenance through the existing
expansion owner. Apply the logical-source projection consistently in part and
assembly inspection. Keep existing exported instance IDs; add explicit mappings
rather than renaming identities to make the UI look uniform.

### P04 — Rejections are honest but explanations are weak

**Observed:** invalid units, unknown `heigth`, zero/excessive count, unsupported
calls and recursive Function dependencies all reject. Part failures often lead
with `collection-syntax-invalid`, followed by `series-fields-invalid:Layout.Sites`
at the Model's document anchor. That does not say which bound/expression/field
failed. A known Function cycle code survives, but precise law/field location is
still lost. No validate/build admission disagreement appeared in the 32 probes. P02 shows
why agreement alone is insufficient: both paths admit the same wrong geometry.

**Repair:** produce structured diagnostics at the Function/Points/Series AST
owner, with the offending expression/field and enclosing collection. Preserve
existing stable codes where useful; transport a readable reason, expected type,
actual type and checked limit. Avoid cascading collection syntax errors after a
specific failed point recipe. Disclose generated-source provenance limits.

### P05 — Graph navigation exists; Pattern editing is incomplete

Real browser sequence:

1. Open equation plate: Ready, four holes, Function→Layout→Modify dependencies.
2. Select Pattern Drills: its inspector offers Source/Delete/+Field, but no
   layout selector, mapped recipe, instance count or key table. Right Inspector
   lands on the first generated Hole; all generated fields are read-only.
3. Navigate Layout→Points Sites→Series: literal count 4→3 builds three holes.
4. Make count a parameter: rejected source, last valid model retained. Revert
   restores the accepted source.
5. Select Function siteX: its generic Value expression is editable. Changing
   -30mm to -25mm builds; saved source retains the Series and Pattern.

Thus the graph is not wholly incapable of editing laws. It requires knowing
source structure, hides the Pattern's real dependencies, and has no joined
source-to-instance explanation. Function parameter names/types and calls are
not presented together with the editable law inputs.

**Repair:** a compiler-owned Pattern description projected into one inspector,
with Layout, Recipe and Instances sections and direct Edit source-owner links.
Opening an existing Pattern is as important as creating one.

### P06 — Generic field metadata offers invalid recipe combinations

Points with a Series child currently offers `Keys` as `Vector3`, even though
Keys belongs to the alternate equal-pitch recipe. Count offers parameter
promotion despite P01. Series index/keyPrefix are identifiers, count/first are
checked integers, and position is a typed point expression; generic text/scalar
controls cannot fully express those rules.

**Repair:** conditional metadata comes from the owning parsed/bound recipe.
Reuse existing schema metadata; add missing value distinctions at that owner.
Do not maintain a separate Helios list of valid recipe combinations.

### P07 — Layout preview is missing for this working Concept point source

The equation plate has a valid Layout point collection, but its Concepts toggle
is disabled. The current retained geometric Concept visualization does not
expose these evaluated point recipes as an editable layout preview.

**Repair:** project compiler-resolved keyed points in their explicit frame using
the existing gold Concept overlay. Hover/select shows key and source owner.
For Point2, use the selected application's resolved support/target frame; if
none is available, show a local 2D layout instead of inventing world XY. A
collection can have consumers in different frames. Avoid 1024 permanent labels;
use selection/hover. A connecting guide, if shown,
is a construction aid, not an inferred manufacturing curve. No JavaScript law
evaluation or viewport-derived coordinate authoring.

### P08 — The current grid helper is a creation adapter

It preserves a field-based Hole seed, emits a Feature and explicit keyed Set,
and maps the remaining copies. Its grid controls count the seed; the Pattern
it emits has one fewer mapped entry. It does not retain an authored grid recipe
that can simply be reopened with row/pitch controls.

**Repair boundary:** preserve that accepted source and identities. Open it as
an explicit keyed layout unless real recipe provenance exists. Never infer a
grid from approximate floating-point coordinates or turn an arbitrary Series
into a grid. A reusable parametric 2D grid recipe with durable row/column keys
is a separate bounded language decision; not a prerequisite for exposing the
already implemented equation/station capabilities.

## Proposed implementation

### Compiler / SDK

Extend the existing language-service/compiler projection with a Pattern
description joined from the original AST and admitted IR:

- Application ID, source file/revision, owner/target, producer kind and spans.
- Logical collection identity, source kind and recipe kind; checked field/input
  controls with reference-versus-literal state and explicit read-only reasons.
- Function references/signatures, positional call inputs and expression span.
- Typed Feature or Part/Subassembly recipe reference and authored argument spans.
- Generated keys/IDs, ordinals, values and frame, plus key→instance provenance.
- Count/identity policy, source consumers, diagnostics and admission level.

This is a projection over current owners, not another validator or expansion
engine. AST source structure alone must say ParsedNotBuilt; a built preview may
report admitted expansion/geometry facts. Compilation still checks geometry
capabilities and engineering conflicts. Keep existing tolerance/provenance
admission; do not erase typed tolerances or imply nonlinear uncertainty
propagation has been implemented.

Consume existing Function/Points/Series AST nodes in their binder and reuse
`FirmamentNumericEvaluation`'s AST evaluator. The current structural admission
and Pattern header parsing already use AST; ConceptPointAuthoring still
extracts Functions/point recipes with regex and rewrites source strings. Migrate
that bounded owner rather than installing another parser or rewrite layer.
Existing Feature, assembly, Scene and SectionChain lowerers stay authoritative.

### Helios workflow

**Pattern…** opens a pending transaction with three sections:

- **Layout:** use existing collection, explicit keyed values, equal spacing,
  named stations or equation Series where the source owner admits it. Show
  dimensionality, support frame, bound inputs and evaluated gold point preview.
  Preserve expressions/reference bindings. An advanced expression editor is
  appropriate; do not create a graph node for every multiplication.
- **Recipe:** choose an existing Feature for a part or a shared component
  definition for an assembly. Bind element fields to typed arguments and
  placement references. Changing a shared layout/Function shows its consumers.
- **Instances:** a read-only key/value/instance table. Selecting a generated
  feature leads back to Edit layout / Edit recipe / Edit law input. Changing
  first/keyPrefix warns of changed identities. Distinguish mapped count from a
  helper's external seed and from produced physical bodies.

Preview compiles the entire candidate project; Apply commits one source edit
transaction. Cancel/refused preview retain working source and geometry. Use the
existing ModelSession lifecycle, project context, compiler edits and Monaco
Undo/Redo. No hidden per-instance position overrides or baked coordinate caches.

For existing source, keep its actual layout form. Named stations stay stations;
Series stays a law; explicit Set stays explicit data. Legacy radial/linear
feature forms remain supported via their production owners and receive honest
read-only explanations where a visual adapter is not yet qualified.

### Recommended sequence / acceptance gate

1. Close P02 first: its paired witnesses must both yield -314mm local / 344mm
   world for the twelfth fret. Then close P01 with shared lexical value binding
   and original AST provenance.
   Test literal, Static, scoped Record/Template inputs and shadowing. Add P04
   precise diagnostics at that same owner.
2. Expose the Pattern description; close part/assembly inspection mismatch P03.
3. Implement reopen/edit of existing Set, Series and station applications and
   compiler-evaluated layout preview. Repair conditional controls P06.
4. Add create-from-existing-collection and new Series/station workflows, using
   pending Preview/Apply; preserve the current grid helper's seed semantics.
5. Qualify the nonlinear plate, Point3 assembly, existing grid, record Hole/Slot,
   radial/linear controls and full guitar through code and browser editing.
   Compare keys, source bindings, world transforms, topology and STEP leaf
   geometry where edits are placement-only. Run the full regression gate.

No user decision blocks that sequence. Optional later decisions are a durable
2D grid recipe, broader math builtins (including angle-safe trig), named/default
Function arguments, and explicit sparse exclusions. They are not prerequisites
and are not silently included in this proposal. General Function-returned
collections, recursion and a higher-order collection language are unnecessary.

## Evidence and qualification

- 32 source probes: 18 admitted, 14 rejected; validate/build agreement in all 32.
  One admitted source produces confirmed wrong geometry (P02); it is **not** a
  passing geometry witness. The paired control produces the correct geometry.
  Cases are classified as correct controls, admitted geometry defects, rejected
  binding defects, expected invalid inputs, or outside the Function contract.
- Seven existing canonical controls accepted, including full guitar and Scene.
- 62 focused existing tests passed.
- Release solution build passed with zero errors (existing warnings remain).
- Fast kernel lane: 1087 passed. Full serial solution gate: 5754 passed, zero
  failed/skipped, across 20 projects with discovered tests. FrictionLab.Tests
  reports no discovered tests and is not counted as a passing project.
- The independent shadowing regression assertion **fails as expected**:
  expected local [0,-314]mm, observed [0,-50]mm while admitted=true. Its expected
  coordinates are recorded durably in the fixture `expectations.json`.
  The existing full suite passes because it does not cover this counterexample.
- This is an audit/docs/specimen change; no new implementation unit tests or
  production AOT publish are claimed. Source probes exercise the real CLI and
  browser path in addition to the existing regression suites.
- Temporary browser tab and task-owned Vite host were closed after the run;
  .NET execution used disabled node reuse and shared compilation.
- Geometry/identity checks ran against actual CLI JSON; the browser count/law
  edits and failed promotion were saved as source evidence.

Sources: `fixtures/Regression/PatternAuthoringAudit/`. Raw JSON, STEP, source
hashes, screenshots and read-only reproduction/qualification scripts remain
ignored under `artifacts/local/pattern-authoring-audit/`. The public Patterns
walkthrough, syntax entry and point/Function guide now describe current behavior.
This audit does not claim its identified implementation defects are repaired.

Reproduction commands (PowerShell, repository root):

```powershell
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus" -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1
dotnet test Aetheris.slnx -c Release --no-build -m:1 -nr:false -- RunConfiguration.MaxCpuCount=1
aetheris validate fixtures/Regression/PatternAuthoringAudit/equation-plate.firmament --json
aetheris build fixtures/Regression/PatternAuthoringAudit/equation-plate.firmament --output artifacts/local/pattern-authoring-audit/equation-plate.step --json
aetheris inspect fixtures/Regression/PatternAuthoringAudit/equation-plate.firmament --json
aetheris asm inspect fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm --json
```

If aetheris is not on PATH, substitute
`dotnet ./Aetheris.CLI/bin/Release/net10.0/aetheris.dll` for that executable.
`run.py` retains input hashes and validate/build results; `canonical.py` collects
canonical domain witnesses; `qualify.py` checks the recorded geometry/identity facts; shadowing is recorded
as a known defect, not passed as correct geometry. These read-only harnesses and raw output are local run evidence, not new
language processors.
