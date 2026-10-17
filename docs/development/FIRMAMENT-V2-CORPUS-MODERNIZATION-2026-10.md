# Firmament V2 corpus modernization — 2026-10-17

## Result and scope

**Meaningful progression.** The current cookbook has been mechanically
modernized and four compiler blockers repaired. Full language consistency is
not claimed: the pass isolated a concrete remaining Piping admission defect.
The complete fixture collection was measured with its actual production
owners, including legacy, negative, experimental and module inputs.

Baseline revision: `a3b6e9549e47767659ec1fa702f851f59c17efec`.
The initial working tree was clean. The source sweep changes 295 current
canonical files, preserves authored/public identities, and moves four original
centerline-only SurfaceCoil specimens unchanged into regression evidence.
There are now 369 canonical `.firmament`/`.firmasm` files.

Historical negative inputs, compatibility sources, Claude's X4 cases/references,
geometry goldens, and speculative language inputs were not style-swept.
One stale negative was corrected with its original source preserved separately.
The full corpus collection is broader than the cookbook; structural acceptance
and domain inspection are not universal engineering-build qualification.

## Plan executed

1. Inventory the entire fixture collection and capture production-owner outcomes
   and existing canonical artifacts before source changes.
2. Modernize admitted vocabulary through the existing lexer/AST-aware formatter;
   use AST spans for bounded construction-alias and sheet Profile source edits.
3. Repair genuine compiler blockers in their existing owners; preserve original
   evidence and classify fixture/context errors separately.
4. Extend the existing canonical qualification manifest and runner to cover every
   current source with the correct owner or consuming product.
5. Rebuild, run focused/fast/full regressions, recollect production outcomes and
   compare STEP, identities and world transforms. Record unresolved seams.

## Source decisions

| Area | Applied decision |
| --- | --- |
| Built-in vocabulary | camelCase fields and lowercase admitted linking words; existing PascalCase aliases remain supported. |
| Public identities | Preserve Model/Struct names, Record fields, library parameter names, semantic ports, occurrence paths and Set keys. No global symbol renaming. |
| Construction declarations | Prefer bare `Path` and 2D shapes; remove redundant `Concept` modifiers from those declarations. Preserve requirement Concepts and `Concept Struct` layouts. |
| Sheet bases | Named shared `Profile = Rect2` with the same minimum corner and dimensions; implicit `base: blankProfile` preserves the historical Base identity family, while named Bases retain their names. |
| Sheet rectangular openings | Named shared Rect2 Profiles; preserve legacy rectangular Slot geometry rather than silently rounding it. |
| Sheet points/anchors | Prefer `Point2(...)` and `CenteredAt(...)`; preserve dimension expressions, product policies and public schema member casing. |
| Coil spelling | Prefer Helix in ordinary recipes; retain the explicitly named AxisCoil compatibility witness. |
| Raw placement matrices | Replace canonical LegacyExplicit declarations with readable parent-relative frame placements, preserving translation and rotation. Retain explicit-matrix compatibility coverage in the precision test. |
| Legacy sheet Pattern | Preserve the semantic-panel witness's `PanelLayout.MountingHoles[index]` identities. Replacing its Count/Pitch effect bundle with keyed Feature expansion is a separate public-identity migration. |
| Unmigrated domain properties | Retain their actually supported spelling. Do not pretend the formatter's metadata covers independently owned Piping/FEA/surfacing vocabularies. |

Sheet Slot dimensions were read from the owning lowering: direct legacy cuts
are rectangles with Length along local U and Width along V. The migration uses
that exact Rect2 size. Legacy layout Pattern Slots are a different capsule
contract and were not mechanically converted.

## Findings and repairs

### Implicit sheet Base spelling owns historical identities

Full regression caught a genuine identity change: rewriting `Base: Rectangle`
as `Base Base { profile: blank }` changed edge-based legacy region/bend IDs.
The parser used the presence of the implicit Base field to select that identity
family. Changing the disconnected-graph test's expected ID would have hidden
the migration defect.

The existing implicit Base field now accepts a shared Profile through the same
`SheetMetalProfileBinding.Base` capability authority as named Bases. The four
modern sources use `base: blankProfile`; no identity override or new parser
route was added. Four unchanged original sources are retained under
`Compatibility/FirmamentV2/SheetMetal`. Tests require exact formed STEP, region,
bend and feature IDs, and deterministic flat hashes to match.

### Nested assembly placement falsely conflicts with child mates

Replacing `fixed-nested-usd`'s relative matrix exposed
`assembly-placement-authority-conflict`: the frame owner treated a mate between
children as a driver of the containing assembly. The same overly broad prefix
test also prevented an explicitly placed grouping assembly from containing
externally mated child parts.

`AssemblyFrameAuthoring` now checks which occurrence owns the moving endpoint.
A child's driver does not compete with its container's placement; mates wholly
inside an assembly do not drive its placement either. Existing competing
authorities on the same occurrence continue to reject. A focused regression
checks the nested rotated parent's world coordinates and AP242 export; existing
USD tests retain the complete joint families and executable pose path.

Relative matrices require `to: Parent`. Rewriting them as `to: World` can reset
nested placements. Identity placements are also needed for otherwise disconnected
members; omitting them can leave a material occurrence unresolved. The accepted
source explicitly preserves these authorities.

### Sculpting casing produces a runtime exception

The shared formatter changed Model units and nested Hole fields, but the separate
surfacing owner read only PascalCase properties. Missing centers/diameters left
an empty mounting-hole list which reached base construction and threw an index
exception instead of returning the field diagnostics.

The existing surfacing field readers now accept the first-character casing
aliases of their own requested fields. Authored values and semantic identifiers
remain case-sensitive. Base construction stops when its field/hole reads fail.
Tests compare state identity and exact STEP between spellings, then require
malformed mounting data to return diagnostics and no constructed states.

This is a bounded repair in the existing owner, not a claim that surfacing's
independent text reader has completed AST migration.

### Embedded construction chains steal the BodyState document owner

The ordinary CLI gate exposed a pre-existing validate/build contradiction for
all six BodyState cookbook sources. Validation used Sculpting; build and inspect
claimed their embedded SectionChain declarations and sent the document to the
ordinary Section/Loft frontend. They then rejected the BodyState, SculptState
and Station productions.

The existing AST-based SectionChain recognition predicate now leaves the
Station-shaped construction chains in a concrete BodyState/SculptState document
with their surfacing owner. Ordinary Section/Loft productions still claim mixed
documents and reject unsupported material. The existing schema-family guard
recognizes BodyState/SculptState as Mechanical, so an explicit SectionChain
schema cannot bypass admission. CLI tests run validate, inspect and build on
representative X3b/X4 bodies, then require mismatched schema rejection without
emitting STEP. No additional route or text rewrite was introduced.

### The inner-loop cookbook recipe is invalid

`Pipeline/profile-with-inner-loop` reused Bottom/Left/Top/Right identities across
its outer and inner loops. Production rejected it with segment-identity
collisions. Explicit `as InnerBottom` and corresponding aliases repair the
recipe. A real build/reimport test checks enclosure and the analytic volume
`(40 * 30 - 16 * 10) * 4 = 4160 mm³`. There was no admitted baseline geometry
for this previously invalid source to preserve.

### Four SurfaceCoil examples overstate qualification

The original analytic-support sources are accepted by `WireFormAuthoring.Parse`
and its centerline tests, but ordinary Model admission rejects the additional
material supports; BlenderBall additionally declares two material bodies.
The original files were moved byte-for-byte to
`fixtures/Regression/WireFormSurfaceSupport`, and the owning tests and public
guide now describe the actual centerline-only boundary.

The next capability needs explicit nonmaterial analytic supports with scoped AST
binding. Silently erasing declared support solids is not an acceptable repair.

### Qualification manifest and negative-fixture classifications

The previous canonical runner omitted newer families, tried reusable modules as
standalone products, sent some ordinary part files to assembly inspection, sent
multi-gear interface witnesses to normal single-body validation, and rejected
the preferred `units:` spelling with its old style regex.

The existing manifest now records owner operations, 69 module consumers, and four
explicit external-body boundaries. Every module's named consumer must qualify;
dependency reachability was checked against includes and typed resource imports.
The runner includes `.firmasm`, preserves per-product JSON, writes a qualification
index under ignored artifacts, and reports all failures instead of terminating
while printing its first error. Expected rejection is a separate level, never
counted as a successful product.

Three initially accepted `Invalid/` inputs were investigated:

| Input | Actual finding and disposition |
| --- | --- |
| `Pipeline/traceloop-non-loop` | A Rect2 is closed and now legitimately supports TraceLoop. Preserve that original source in `Regression/FrontendX2/traceloop-rect2`; the negative now contains an actually open Line2. Tests check parse/build outcomes for both. |
| `FEA/ExperimentalShell/singular-map` | Compile succeeds; the owning solver test requires `thinwall-map-singular` from `LinearElasticSolver.Solve`. A compile-only corpus result is not solver acceptance. |
| `SheetMetal/tray-dfm-violation` | Geometry deliberately builds. `SheetMetalDfm.Evaluate` reports Warning with an actionable corner-resolution finding; the repaired tray reports Pass. This is a quality-policy witness, not invalid language. |

Four FigureFit garment sources correctly require an external body. Standalone
validation's `garment-fit-body-required` is recorded as `ExternalBodyRequired`,
separately from successful products. No fit certification is implied.

## Remaining blocker: Piping loses optional field intent

Minimal reproductions are committed under `Regression/FrontendX2`:

- `piping-field-casing.pending`: requests `connectionType: Weld`; validate
  accepts and inspection reports `Generic` for both ports.
- `piping-field-unknown.pending`: supplies `unexpectedIntent: Weld`; the unknown
  field is ignored and validation accepts.

The ordinary `ConnectionType: Weld` reference reports Weld for both ports.
These are known failing compiler-honesty witnesses, not expected-success tests
or qualified cookbook examples. The public syntax/piping guides disclose the
current admission defect.

The exact coupling is `PipingAuthoring.Parse` calling its `Field(string body,
string name, ...)` helper over independently extracted brace text. It discards
field ownership and consumption, and optional fields substitute defaults when
the requested spelling is absent. Adding one alias would leave unknown fields
silently accepted. Adding a second schema or whole-source rewrite would repeat
the debt this pass is removing.

The smallest next prerequisite is to project Piping's existing declaration
nodes into its current semantic records and use shared `FirmamentSyntaxFields`
consumption at each existing construct owner. Preserve its route planner,
geometry lowerers, BOM and equipment/nozzle semantics. Only then migrate all
piping field spellings and require both pending witnesses to become correct
admission/rejection tests. The subsequent domain sweep must inspect FEA and the
independent surfacing reader as well; they are not qualified by structural
parse success alone.

## Qualification evidence

Raw local evidence belongs under `artifacts/local/corpus-modernization/`.
The initial complete collection contains 1,481 sources, all 373 initial canonical
sources structurally parsed, and no production-owner exceptions. The baseline
classification counts are retained in its report; they do not grant universal
semantic parity.

Final qualification against the completed implementation:

| Check | Observed result |
| --- | --- |
| Release solution build, serial, compiler sharing disabled | Succeeded; 0 errors, 30 existing warnings |
| Fast kernel lane | 1,076 passed |
| Full serial solution lane, including corpus tests | **5,379 passed across 21 test assemblies; 0 failed/skipped** |
| Focused frontend/assembly pass before the final owner repair | 490 passed |
| Focused final CLI owner/schema/public-link checks | 12 passed |
| Focused sheet-metal pass, including four original identity references | 161 passed |
| Current cookbook | **369/369 classified: 296 successful owner operations, 69 consumer-qualified modules, 4 explicit external-body boundaries** |
| Complete structural/production-owner collection | 1,488 sources; 1,124 structurally parsed, 364 rejected; all 369 canonical sources structurally parsed; no owner exceptions |
| Existing STEP-producing canonical operations with pre-change artifacts | 182 pairs; 180 byte-identical; the two differing flat exports have equivalent geometry as detailed below |
| All canonical sheet-metal formed exports against original source | **19/19 byte-identical STEP** |
| Before/after assembly evidence | **52/52** retain public instance/definition identities, exact part STEP hashes/metrics and resolved world matrices within 1e-9 |
| Before/after Scene inspection evidence | **7/7** retain counts, bounds, public nodes and layout facts after excluding source spans/placement-authority provenance |
| Original domain-migration hash references | **37/37 matched** |
| Claude's unchanged X4 CLI/geometry corpus | **32/32 passed**; no recording/rebaselining |
| Shared-profile CLI corpus and documentation specimens | 26/26 passed; formed/flat reference parity retained |
| Semantic/manufacturing CLI corpus | 29/29 passed |

The two flat STEP byte differences are the network-appliance and power-supply
enclosures. Shared rectangular Profiles serialize the same rectangle contours
with a different vertex/edge traversal order. Reusing the X4 inspection/verification
oracle found no differences in topology counts, surface families, structural
assessment, world bounds (1e-9) or volume (1e-9 relative). Coordinate-resolved
vertex and edge/curve-family inventories also match. All 19 **formed** exports
are byte-identical. No changed geometry golden was recorded.

Scene inspection does not expose every world occurrence matrix. Its 7/7 result
is limited to the stated facts; the 52 assembly comparisons independently check
their world transforms. This is not a claimed pre/post GLB byte comparison.

Among sources present both before and after, production admission changed only
for the repaired inner-loop cookbook recipe (rejected to accepted) and the stale
non-loop negative replaced with an actual open guide (accepted to rejected).
The original now-valid Rect2 specimen was preserved. Four added compatibility
references, that original Rect2 source, and two explicitly pending Piping defect
specimens account for the seven-source increase from 1,481 to 1,488.

The final complete collection's classifications are:

| Classification | Count | Interpretation |
| --- | ---: | --- |
| Admitted | 627 | The recorded production owner returned success at its stated level; this includes the two explicitly documented pending Piping semantic defects. It is not a universal correctness verdict. |
| BothRejected | 6 | Structural and production rejection retained as source evidence. |
| DeclarationModule | 42 | Collection recognizes the declaration-only shape; the cookbook manifest additionally qualifies all 69 actual contextual modules through consumers. |
| ExperimentalSource | 40 | Speculative/experimental sources, not language compatibility promises. |
| LegacyGrammarBoundary | 329 | Legacy/serialized leading-token inputs; measured separately from native V2 structural parsing. |
| NegativeFixtureAccepted | 2 | The solver-stage singular-map and DFM-warning witnesses explained above. |
| NegativeFixtureRejected | 287 | Explicit negative paths rejected by their recorded owner. |
| SemanticOrCapabilityRejection | 155 | Located outcomes retained for binding, domain/context and capability review, including contextual modules and external-body requirements. |

Reproduce the maintained qualification paths from the repository root:

```powershell
dotnet build Aetheris.slnx -c Release -m:1 -nr:false -p:UseSharedCompilation=false --no-restore
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
./scripts/Test-CanonicalFixtures.ps1 -NoBuild
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll frontend-shadow --corpus fixtures --production --output-dir artifacts/local/corpus-modernization/collection
python fixtures/Regression/FrontendX4/run.py
python fixtures/Regression/SheetMetalSyntax/run.py
python fixtures/Regression/SheetMetalSemantics/run.py
./scripts/qualify-firmament-domain-frontend.ps1 -OutputDirectory artifacts/local/corpus-modernization/domain-parity
```

The existing developer command name `frontend-shadow` collects both structural
and real production-owner results here; it is not a new shadow parser or an
engineering-admission bypass. Raw collection, STEP comparison and full test logs
are under the ignored task artifact directory. Frozen Zig checks were not run:
no Zig compiler/build infrastructure changed. Remote CI and a live Helios browser
workflow were not run or claimed.

## Code-quality boundary

No parser route, diagnostic-code fatality allowlist, kernel feature, editor
validator or runtime source-rewrite layer was introduced. The source migration
used the existing formatter and temporary AST-span tooling under ignored local
artifacts. The matrix conversion was a bounded one-time edit checked against
resolved world transforms. Existing domain algorithms and goldens were retained.

The surfacing change reuses each field reader's existing requested name; its
case-only regex fragment adds no independent field vocabulary or document
dispatch. The fixture runner's regex is a source-style guard, not compiler
admission. Legacy negative tests explicitly normalize their mutation inputs
where they intentionally inject PascalCase aliases; ordinary source reads and
the real CLI qualification still consume the modern files directly. Required
mutation helpers fail if their target fragment disappears.

No X2 closure, global language-consistency verdict, new geometry capability,
garment fit certification, or browser workflow certification is claimed.

The final public-Markdown link check and `git diff --check` pass; the modified
PowerShell runner parses without errors. Build/compiler node reuse was disabled,
and all task-owned .NET CLI/test/compiler hosts exited after qualification.
Automatic approval review rejected removal of disposable intermediate artifact
directories with the stated reason "blocked by policy". Those ignored files
remain alongside the retained before/after evidence; no cleanup was bypassed.
