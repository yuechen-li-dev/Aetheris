# Remaining Firmament domain frontends — 2026-10-18

## Purpose and qualification boundary

This follows the [Piping migration](FIRMAMENT-PIPING-AST-ADMISSION-2026-10.md).
The work replaces independent authoring scanners with production consumption of
`FirmamentSyntaxParser` trees. It is not another shadow milestone and does not
claim that all of X2's semantic design work is finished.

The organizing model remains: Model owns an artifact; Struct defines reusable
construction; Feature is a typed recipe; Pattern is finite compile-time
repetition. A shared syntax tree does not turn domain-specific engineering
operations into interchangeable types.

## Production ownership changes

| Family | Former authority | Current authority and preserved lowerer |
| --- | --- | --- |
| Structural | Whole-source block, Node/Path and Table regex readers | Direct AST scopes; shared Record/Static/Table catalogue; existing member AIR, joint resolution, interfaces, cut lists and AP242 assembly generation. |
| Sculpting / BodyState | Independent block/field/control-net scanner | AST state/operation/SurfacePatch/Station scopes and shared values; existing sculptors, construction-state replay, continuity judgments and STEP exporter. |
| Analysis | Regex Analysis blocks plus fallback Box/cut scanning after geometry parse failure | AST Analysis owner, shared values, normal native geometry admission and an explicit closed legacy production; existing CIR, material/selection binding and solver. |
| PlasticShell | Case-insensitive block/quantity scanner | AST Model/shell/manufacturing scopes and shared values; existing manufacturing policies, candidate judgment and exact realization. |
| Drawing | Regex Concept/Template/application/View readers; parameter text replacement; independent Static/with reader | AST declarations and expression substitution; shared typed Record catalogue; existing product compilation, projection, layout, PDF/SVG/PPTX generation. |
| Review | Independent Review/entry block scanner | AST Review/entry scopes; existing immutable ReviewIR and proposal semantics. |
| Recovered / evidence-linked reconstructed SheetMetal | Independent serialized-form scanners | Explicit AST productions, including quoted external identities and recovery tuples; unchanged recovery IR, evidence mapping and flattener. |
| SheetMetal enclosure product adapter | Regex schema stripping, Record projection and Extend extraction | AST declaration spans and existing typed host Record inspection; unchanged assembly/materialization behavior. |

`FirmamentSyntaxScope` is a per-compilation consumption cursor over existing
nodes and `FirmamentSyntaxFields`, not a parser, second schema or diagnostic
allowlist. Owners take actual fields/children and close the tree. Unexpected
headers, unknown fields/statements and duplicate aliases reject. Local readers
validate types, cardinality and capability before using defaults. There is no
new whole-document regex dispatch or source macro system.

The Surfacing and Collaboration libraries sit below Kernel.Firmament in the
project graph. Their Firmament authoring classes now live in Kernel.Firmament,
retaining their namespaces. Geometry algorithms and neutral result/ReviewIR
contracts stay below it. This preserves repository source consumers; compiled
external consumers of those moved classes must rebuild against Kernel.Firmament.
Binary type forwarding back to it would introduce a dependency cycle. PlasticShell
can reference Kernel.Firmament directly without a cycle.

## Honesty findings and repairs

| Finding | Reproduction / disposition |
| --- | --- |
| Canonical FEA solves via `fea` but normal `validate` rejects it through the mechanical owner | Confirmed before migration. Normal validation now uses UnifiedAnalysis and reports BoundNotSolved; normal build uses the same compiler and existing solver. |
| Native Analysis could recover from failed V2 geometry parsing by scanning a Box/radius out of source | Removed. Failed V2 geometry is not admitted by a text fallback. The legacy AST production closes the Model, solid, modify, region and cut scopes and validates the opposed Z faces. Imported-resource analyses close their own document when no native body is declared. |
| Structural tables had their own string-based schema reader | Replaced by the shared checked catalogue and scoped data identities. Missing/mistyped/extra columns and unconsumed role columns reject; duplicate role aliases reject. Invalid tables stop before indexing columns. |
| Unknown model/domain statements or fields could disappear | Closed local AST scopes reject them. Focused tests assert rejection and absence of output, not just error sentences. |
| Drawing enums could silently choose defaults for unknown values | Explicit Orientation, Projection, HiddenLines, BOM and nonzero finite direction validation. Invalid values reject before drawing artifacts. An explicit Orthographic projection now controls the mode; an absent projection retains the established direction-based inference. |
| A semicolon inside quoted Review text truncated the remaining message | Confirmed by the canonical drawing's Resolution text. AST string reading preserves the complete authored message. This is an intentional ReviewIR content repair; the production drawing geometry/PDF/PPTX remain unchanged. |
| Drawing specialization used word replacement and did not check arguments | Bound AST expression substitution; unknown, duplicate, missing and mistyped arguments reject. Shared Record checking preserves explanatory schema failures. |
| Duplicate singleton declarations could throw through SingleOrDefault | Explicit cardinality diagnostics for the migrated owners. |
| Invalid optional recovery values could become defaults | Authored malformed values retain an error; defaults apply only when absent. Duplicate recovery/evidence mappings reject. |
| AutoRib unrecognized keepOut identities had no effect | Candidate owner validates names against the actual generated candidate set. Legacy unquoted hyphenated identities remain admitted; quote them in new source. |
| Forge workbench disappeared under nested Model/Struct wrappers | A single wrapper chain is consumed structurally; material declarations belong to its innermost product. Sibling products are not pooled. Shared data still binds lexically. |
| A surfacing negative fixture starts with `+Model` | Classified as malformed source, not a lost geometry capability. Preserve the original. The regression asserts its syntax rejection and separately tests the intended G2 degree gate after removing the accidental marker in memory. The locality fixture with the same prefix remains invalid. |
| Native reconstructed SheetMetal and evidence-linked reconstruction both use intent: Reconstructed | Disambiguate by the existing EvidenceSource field in the AST. Native authored reconstruction still uses its established owner. |

## Admission and diagnostics

- Structural and Sculpting validation report the actual UnifiedStructural /
  UnifiedSculpting route and run their engineering owner. Analysis reports
  UnifiedAnalysis with `status: bound-not-solved` and `validationLevel:
  BoundNotSolved`; successful binding is not a solver-convergence claim.
- Drawing validation/build also select its actual product owner. Validation
  exercises drawing compilation in an isolated generated-output directory and
  removes that directory afterward. `drawing compile` remains available.
- Analysis/Drawing build output is an artifact directory. A `.step` output path
  rejects explicitly; export a separate geometry source when STEP is wanted.
- Domain diagnostic strings remain on existing .NET result contracts where
  necessary. Structured frontend diagnostics preserve code, message, owner,
  source file and original AST spans. The shared adapter projects them through
  the existing CLI/SDK presentation contract. Legacy lowerer errors without
  exact provenance keep the disclosed enclosing-document location. Drawing
  metadata errors now retain the shared `firmament-template-record-*` /
  `firmament-template-with-*` codes when Record admission rejects before Drawing
  policy checks. The old Drawing-only codes still describe inline metadata
  checks; a Record failure is not relabeled or suppressed to satisfy a legacy
  message assertion.
- The language service consumes Structural binding without materialization.
  Sculpting, FEA and PlasticShell editor analysis reports shared syntax and an
  explicit compile-context requirement. It never builds sculpt geometry on
  keystrokes or claims that syntax proves engineering admission. Their build /
  validate owners supply the semantic/capability diagnostics. Assembly and Scene
  retain routing precedence. No Helios-only validator was introduced.
- Optional schemas added to the existing selector: Structural, Sculpting,
  PlasticShell and Analysis. Explicit incompatible schemas reject.

## Spelling and compatibility decisions

PascalCase names constructs/types; camelCase names compiler fields. Acronym
fields prefer `pmi`, `bom`, `uvBounds`, rather than first-letter-only `pMI`,
`bOM`, `uVBounds`. The old acronym spellings remain aliases in the common field
reader. Authored Record members, named generic arguments, published symbols,
external identities and enum choices keep their declared case.

Existing canonical domain fixtures retain their historical field aliases in
this pass; the syntax guide and introductory domain examples use modern fields.
Some formatter owner vocabularies still need consolidation. Formatting is not
admission and must not rename user Record members. No geometry baseline was
regenerated merely to obtain a passing comparison.

The Drawing adapter retains its historical `Product:` application argument as
an alias for the unique `Item: Product` parameter. New code should name the
actual declared parameter. Drawing's bounded prototype surface still rejects
unused/ambiguous policy or prototype declarations; it is not a general-purpose
module Template binder.

## Remaining language inconsistencies and decisions

| ID | Evidence / inconsistency | Disposition / next owner |
| --- | --- | --- |
| DOMAIN-01 | Compact BodyState SectionChain uses Station/origin/size and implicit frame axes; standalone SectionChain uses explicit Section/Profile/Loft frames. | AST migration complete for both. Design a shared section representation before merging semantic lowering. Do not rename one grammar into the other or change existing frames. |
| DOMAIN-02 | BodyState sounds general, but its constructor is the qualified housing-with-holes witness. | Separate capability boundary. Document explicitly; define a generic body-state import/constructor contract before broadening. |
| DOMAIN-03 | Concept Drawing contains a finite list of policy requirements; ordinary Concept is a predicate/structural contract. | Preserve and validate the bounded policy vocabulary. A unified policy-as-predicate design is semantic consolidation work. |
| DOMAIN-04 | Drawing Template has Product/Metadata parameters and a historical Product argument alias, unlike ordinary product Templates. | Explicit compatibility adapter over AST with nominal Record checks. Consolidate with the general Template binder before promising general defaults, nesting or arbitrary constraints here. |
| DOMAIN-05 | StructuralMemberRow / StructuralJointRow / StructuralWeldRow names select catalogue roles. Paths and Node references also use legacy annotation syntax. | Preserve qualified role contracts and identities. A typed collection/role declaration needs a reviewed surface; no speculative new syntax in this pass. |
| DOMAIN-06 | Structural Node positions accept the established implicit-mm list convention; most modern geometry fields require explicit lengths. FEA lowers to SI and native analysis uses its established [0,size] frame. | Preserve geometry and document owner rules. Do not silently recenter physics or extend unit algebra. |
| DOMAIN-07 | Shared scalar algebra covers lengths/angles/time; FEA has physical literal conversions for stress, density, force and acceleration. Recovery also serializes area evidence. | AST owns expression boundaries; existing physical converters retain unit policy. General derived-dimensional algebra is a separate semantic change. |
| DOMAIN-08 | Review proposal units still use bounded legacy comparison; Review does not mutate authoritative PMI. | Preserve contract. A typed proposal value model is a semantic follow-up, not a license to claim general dimensional checking. |
| DOMAIN-09 | Complete source maps through include/Template expansion remain incomplete. | Preserve available original spans; otherwise disclose enclosing/generated context. Do not claim exact locations where unavailable. |
| DOMAIN-10 | Some older Template/profile rendering still transports admitted bodies as text and has source-based local-name rewriting. Serialized V1 remains explicit compatibility. | These are not a new frontend route. Scope-aware AST expansion and removal of the remaining transport rewrites still belong to X2; parser migration alone does not close that debt. |
| DOMAIN-11 | Formatter/completion metadata coverage differs across domain owners; Sculpting/FEA/PlasticShell language service needs compile context. | Track metadata/host integration separately. Compiler admission remains authority; no inferred UI validation. |
| DOMAIN-12 | Different product kinds produce different artifacts and validation levels. Some module-specific validation branches still run their owner even when the common --parse-only flag is present. | Explicit CLI dispatch/levels above report work actually performed; no unqualified parsed-is-valid claim. Drawing/Analysis --out means directory, not STEP. A uniform per-owner parse-only option contract remains CLI work; the current Built/BoundNotSolved reports are not a promise of a cheap syntax-only pass. |
| DOMAIN-13 | Shared Record/Static/Table binding is available in the migrated domains, but common standalone Concept/Require statements are not admitted at every Structural/Sculpting/PlasticShell product scope. | They reject explicitly rather than disappearing. A consistent requirement/contract projection across domain owners still needs semantic consolidation; do not infer it from shared syntax. |

No decision requires a user choice to finish this bounded migration. The listed
semantic reconciliations need designs and qualification; none is represented as
already implemented. Existing geometry/kernel capability limits remain limits.

## Qualification evidence

Evidence is under ignored `artifacts/local/remaining-domain-ast/`.
The initial clean source revision was `d20df8ed268ff71518bb7e4a4aa581e45556eb5e`.
Before-source copies and artifacts were captured before their owners changed:
3 FEA cases, 7 Structural cases, 10 Sculpting cases, 6 BodyState cases, 7
PlasticShell cases and the canonical production drawing.

| Gate | Result | Evidence |
| --- | --- | --- |
| Release solution build, .NET SDK 10.0.401 | Passed; 0 errors, 30 warnings retained in log | `release-build.log` |
| Fast kernel lane | 1,076 passed | `qualified-fast.log` |
| Complete solution regression | 5,464 passed, 0 failed, 0 skipped across 20 nonempty assemblies | `release-full.log`, `test-summary.json` |
| Canonical owner/consumer qualification | 296 owner operations + 69 consumed modules + 4 expected external-body boundaries = 369 sources | `canonical/qualification.json` |
| Historical domain admission/STEP hashes | 37/37 matched | `domain-parity/comparison.json` |
| Claude X4 source corpus | 32/32 passed; no case/golden edits | `x4.log` |
| Sheet profile and semantic corpora | 26/26 and 29/29; formed/flat parity retained | `sheet-profile.log`, `sheet-semantic.log` |
| Fresh Structural/Sculpting/BodyState/PlasticShell STEP | 30/30 byte-identical | `parity.json` |
| FEA analysis IR and native numerical evidence | 3/3 semantically identical after excluding actual runtime/setup timing fields | `parity.json` |
| Production drawing PDF/SVG/PPTX | 3/3 byte-identical | `parity.json` |
| Drawing IR | All other fields identical after excluding timing and the two documented metadata/text repairs below | `parity.json` |
| Unknown AutoRib network | Validate/build both reject; text includes source, code and explanation; no STEP emitted | `text-diagnostic.json`, `text-diagnostic.txt` |
| Runtime hosts / storage | No dotnet, testhost, VBCSCompiler or aetheris process remained after checks; this task's evidence occupied 140.8 MiB, with 201.0 GiB free on C: at the final sample | Final process/disk inventory |

The solution also visits the FrictionLab test project, which currently discovers
no tests; it is not counted as a passing test assembly. Qualification is local
Windows/.NET/CLI evidence. No remote CI run or native/browser Helios interaction
is claimed. Frozen Zig was not run: neither its compiler nor its infrastructure
changed.

The shared Static receipt prefix is now `StaticRecord` rather than
`StaticRecord:AsterDrawingDefaults`; the named Static identity, derived-from
reference and overrides remain available. Separately, the original Resolution
string was truncated at its embedded semicolon by the old scanner. The complete
new Review text is checked against the unchanged authored source, not a newly
recorded golden. Review content is intentionally repaired; production drawing
geometry/layout and PDF/SVG/PPTX hashes stay exact. Comparison retains both old
and new receipts/text in `parity.json`. FEA comparisons exclude `runtime`,
`performance`, `authoritySetup` and `constraintAndStabilizationSetup`, all timing
measurements, and retain the analysis, materials, selections, lattice, loads,
solver convergence/residuals, equilibrium, stress/displacement, energy and Abaqus
checks.

Useful reproduction commands (run serially from the repository):

```powershell
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
./scripts/Test-CanonicalFixtures.ps1 -NoBuild
./scripts/qualify-firmament-domain-frontend.ps1
python fixtures/Regression/FrontendX4/run.py
python fixtures/Regression/SheetMetalSyntax/run.py
python fixtures/Regression/SheetMetalSemantics/run.py
```

## Code-quality review and next semantic work

The review checked scope closure, duplicate aliases, nested product ownership,
unknown enum/default handling, propagation of messages/spans, explicit legacy
geometry admission, shared data binding, V1 isolation and state lifetime. Piping
and Structural now share the diagnostic-string compatibility adapter rather
than duplicating it. New scope state is per compilation; no parser-global mutable
state, fatality-code allowlist, source macro system or new regex recognition was
added. Retained regex use in the migrated code is limited to existing physical
literal/STEP/identifier or presentation conversions, not independent Firmament
block/field readers. `git diff --check` passes.

A final runtime review removed a potential editor regression: Sculpting editor
analysis must not run full state compilation on every keystroke. Its language
service path now reports shared syntax and an explicit compile-context notice;
real build/validate returns the engineering diagnostics. Tests cover both this
boundary and malformed-source syntax markers.

The next semantic work is concrete:

1. Replace the remaining admitted-body text transport/local-name rewriting in
   `FirmamentV2TemplateExpansion.RenderOutput` with scope-aware AST expansion,
   preserving public member paths, source origins and existing specialization
   identities. This is expansion debt, not another parser/shadow milestone.
2. Define one domain semantic-value projection so common Concept/Require checks
   can bind domain entities consistently; Structural/Sculpting/PlasticShell
   currently reject unsupported requirement placements explicitly.
3. Reconcile the two section representations, typed structural collection roles
   and domain metadata/formatter coverage using those semantic contracts. Keep
   the unit and geometry boundaries in DOMAIN-01 through DOMAIN-13 explicit.

## Verdict

**Success for this production domain-reader migration.** The remaining audited
independent domain readers now consume the unified AST, known ignored intent and
fallback paths reject, normal validation/build use their actual owners, and the
qualified geometry remains unchanged. The canonical corpus, full regression and
parity gates pass.

**X2 semantic consolidation remains open.** The thirteen decisions above identify
specific language/expansion/metadata work; this report does not claim that a
shared parser alone makes every domain syntax or semantic contract uniform.
