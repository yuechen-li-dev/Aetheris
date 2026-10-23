# Firmament body-state and semantic debt closeout

This pass follows the [remaining-domain AST migration](FIRMAMENT-REMAINING-DOMAIN-AST-2026-10-18.md).
The priority is BodyState correctness. The constraint is debt repayment, with no
new geometry operations, language control flow or parallel admission system.

## Repairs and actual ownership

| Finding | Repair / qualification |
| --- | --- |
| BodyState always called SculptedHousingFactory.CreateBase. A cylinder or existing feature body could not be retained as itself. | `input` references the Model's admitted engineering product. The AST separates state declarations from engineering construction, then the existing compiler admits/materializes that construction. Its exact body/STEP and available semantic correspondence are retained. No bounding-box fit or housing conversion occurs. A conflicting export-name override rejects rather than silently ignoring the requested name or losing PMI associations. |
| Native Gear/Sweep/Revolve/SectionChain producers did not consistently expose their authored body identity through the primitive bridge. | Retain RuntimeBodySourceName on the existing materializer result. BodyState consumes producer metadata, without parsing diagnostic feature IDs or maintaining a second construct list. Cone/sphere/torus/rounded-box/Gear/Sweep/Revolve adoption and replay are compared to their unchanged canonical source. |
| Domain Pattern expansion claimed Patterns belonging to mechanical Compose/Modify. | Expand only direct children of the actual domain owner. Mechanical patterns stay with their engineering owner; unsupported placements remain visible to AST scope closure. A patterned/explicit-PMI engineering product is retained byte-for-byte. |
| Authoring required a mounting hole even though the housing builder supports zero. Its PMI code indexed the first hole unconditionally. | Holes are optional. A hole-free housing has no hole pattern or hole PMI. A real crown edit and typed replay qualify this path. |
| A second HoleFeature after AddSectionChain could drop the chain: retention looked only at the immediately preceding operation. | Retain the typed additive construction from the full predecessor history. Two subsequent holes are compared against independently ordered construction with both bores present before attachment. |
| Other whole-housing reconstructions could erase a retained attachment/duct, and a valid semantic name did not prove an operation supported that region. | Check the actual construction/history/support capability before rebuilding. Unsupported combinations reject atomically, without publishing a successor. Offset/Replace targets and the Blend shoulder are checked explicitly. |
| Mixed-diameter housings emitted the first hole's diameter for the whole pattern. | Group hole PMI by authored diameter and the constructed bore faces' exact analytic radii. The legacy equal-diameter product stays unchanged. No nearest-face or guessed semantic rebinding is introduced. |
| Replacement-patch hashes omitted control points, while SectionChain hashes omitted frames and spline knots. Different shapes could share a state/geometry fingerprint. | Include the complete typed support and chain recipe. Regression checks change an interior control point, a frame and a knot vector while keeping names/counts fixed. |
| Abstract patch payloads could be written without a readable typed patch constructor. | The existing construction-state serializer now persists/reconstructs BSpline patch data explicitly, retaining domain, correspondence, orientation and surface class. Opaque evaluator delegates reject as replay authority. |
| The direct imported-face adapter inherited an unrelated native housing replay recipe and hashed only face count. | Clear that false authority, fingerprint the retained geometry, include patch support in graft identities, and reject whole-housing reconstruction that would discard imported face provenance. The bounded graft is not claimed as a durable imported-source program. |
| Blank/duplicate hole IDs could collapse distinct geometric bores into one semantic inventory entry. | Reject missing or duplicate hole identities before constructing a body. |
| CLI state presentation dereferenced HousingConstruction for every state. | Housing evidence is optional. Build/inspect can present engineering-source states without a null-reference failure. |
| Profile Template specialization renamed every matching word, including strings, fields and unrelated members, then scanned braces again to hoist guides. | Use the existing AST rewriter, lexical binding scopes, construction ownership and syntax writer. Preserve the established specialization prefixes and colliding Segment identities. Rewrite references to bound local guides; preserve unrelated external members, field labels and strings. |
| Template Record member validation searched source text, including string contents. | Inspect MemberSyntax and actual nested parameter/mapping shadow boundaries. A string mentioning `spec.ThisIsTextAndNotAField` no longer creates a phantom reference; a real missing member still rejects. |
| Structural/Sculpting/PlasticShell rejected common scalar requirements even though they already shared the value binder. | Admit common Concept declarations and Require checks through FirmamentDomainBinding and its existing Concept evaluator at product scopes. This does not invent scalar properties for domain geometry. |
| Expanded diagnostic anchors omitted field names/values. A failed state also generated an unnecessary unresolved-output error at the Model line. | Extend the existing origin index with field/name/value anchors. Preserve actual diagnostic codes/messages. Suppress an output cascade when the selected declaration has already failed, retaining the primary source-located cause. |
| Some owners silently ignored --parse-only and materialized geometry. | Owners without a static admission path reject the option with `firmament-validation-option-unsupported`, `status: not-validated`, and `validationLevel: NotValidated`. Reuse the existing Structural/Garment static analyzers; Mechanical/Assembly/Scene/SectionChain/WireForm/Sweep static paths remain with their owners. PlasticShell's normal report now also identifies its route and build level. |

The normal compiler retains its admitted document and native producer identity
on the existing in-process export result. The state adopter consumes that
binding evidence instead of running a separate semantic parse after compilation.
These fields do not change normal CLI/SDK JSON.

The engineering base is a versioned `EngineeringSourceConstruction`, alongside
the existing `HousingBaseConstruction`. It retains source, source file and the
selected product name. Replay recompiles that recipe through the same compiler;
the retained STEP is evidence, not the replay authority. External resources are
resolved relative to the retained source file, and are not silently frozen into
the state. Different dependency contents can produce a different state identity.

`ConstructionStateReplayer` now lives beside Firmament authoring in
`Aetheris.Kernel.Firmament`, retaining its namespace and source API. This avoids a
Surfacing-to-frontend dependency cycle. External precompiled .NET consumers must rebuild after the class move or
BodyState constructor change. Replayer consumers reference Kernel.Firmament;
BodyState-only geometry consumers still reference Surfacing. No binary forwarding
or universal binary compatibility is claimed. Construction-state serialization
and geometry algorithms remain in Surfacing.

## Compatibility decisions

Patch/SectionChain-derived state fingerprints intentionally change because the
old preimages could not distinguish different engineering inputs. Authored
semantic names and materialized geometry are preserved. Base housing IDs and
unaffected operation IDs retain their existing construction. Old source recipes
can be replayed to current realized identities; this is not a promise that a
previously colliding fingerprint remains a unique identity.

The previous DOMAIN register remains historical evidence. This table is its
current disposition under the no-new-features constraint; it does not erase the
original findings.

| Item | Current decision |
| --- | --- |
| DOMAIN-01: two SectionChain forms | Retain the compact Station form as compatibility, including its +X frame and G0 default. Both forms already lower to SectionChain IR; do not silently change geometry by imposing the standalone form's frames/defaults. A new unified authoring surface is outside this pass. |
| DOMAIN-02: housing-only BodyState | General engineering-product construction and source replay are repaired. Existing sculpt materializers remain explicit housing capabilities. Bounded replacement still needs the shared semantic-face/PMI adapter described below; broader offset/blend/composition also need kernel work. |
| DOMAIN-03: Drawing policy Concepts | Retain the bounded Drawing policy contract as a distinct documented domain form. Do not claim it is a general predicate evaluator or silently reinterpret its policies. |
| DOMAIN-04: Drawing Template adapter | Preserve its typed Product/Metadata compatibility adapter. General nested product Template semantics remain open; they require owner consolidation, not another source rewrite. |
| DOMAIN-05: structural Table roles | Preserve the existing named row contracts and public IDs. Direct Node/Path/Member authoring remains the ordinary alternative. A new typed role-collection syntax would be a feature and is not introduced. |
| DOMAIN-06: points/units/frames | Preserve explicit owner conventions and physical placement. No recentering, default-unit change or geometry migration. |
| DOMAIN-07: derived dimensions | Preserve existing finite physical literal converters and common dimensional algebra. General derived-dimensional computation is a separate feature. |
| DOMAIN-08: Review values | Preserve bounded proposal comparison and the separation from authoritative PMI. A new typed proposal model is not introduced. |
| DOMAIN-09: expansion provenance | Field/name/value source anchors are repaired. Complete include/expansion source maps remain incomplete; enclosing/generated locations must still disclose that limit. |
| DOMAIN-10: Template text transport | Remove the global Profile rename/hoist scans and Record member regex admission. Existing lowerer string transport remains; this is not a claim of fully AST-native expansion. |
| DOMAIN-11: editor coverage | Keep compiler authority and the existing explicit compile-context notice for expensive domains. No geometry construction on each keystroke and no parallel editor validator. Complete domain completion/formatting metadata remains host integration work. |
| DOMAIN-12: validation levels | Ignored static-only options now reject explicitly; actual owner validation levels remain distinct. No parsed-is-valid claim. |
| DOMAIN-13: requirements | Common compile-time data predicates/contracts now bind in Structural/Sculpting/PlasticShell product scopes. Automatic semantic-value projection of domain entities remains unsupported and must reject, rather than fabricate properties. |

Housing OffsetRegion still expresses the recipe's crown height above its base
plane. It is not a generic displacement operator over an arbitrary successor
surface. This boundary is explicit in the public operation table. The old
housing adapter's datum/tolerance annotations are preserved for compatibility;
ordinary engineering inputs retain their own explicit PMI.

## Evidence and qualification

Source witnesses and transformation-only fragments live under
`fixtures/Regression/BodyStateDebt/`. The fragments are syntax-transform test
inputs, not speculative products counted as successful builds. Existing
Canonical and Claude X4 sources/goldens are not rewritten to manufacture passes.

Run evidence is local and ignored under `artifacts/local/body-state-debt/`.
The initial historical domain gate was 37/37 before implementation.
The broader adoption tests wrap existing canonical products; they do not edit
those fixtures. An early generated wrapper retained `schema Mechanical` while
adding a Sculpting output: that mismatch is correctly rejected, not classified
as a regression. The wrapper now explicitly selects Sculpting, while its
reference still uses the original Mechanical selector. Separate failures for
real native producer names were actual metadata gaps and were repaired in their
owning materializers. The
additional Profile Template baseline is `template-before.step`. The final
qualification results are recorded below.

Projection preserves the original engineering statement text and offsets,
omitting only AST-owned state/output spans. A rich PMI witness exposed that
reserializing the whole body changed literal/call spelling consumed by an older
lowerer. That path was repaired locally; it does not introduce a global rewrite
or pretend the older lowerer is AST-native. The original failing iteration is
retained in `pattern-qualified.log`.

The generic mass evaluator explicitly labels nonanalytic trimmed-face results
as tessellated sanity estimates. An early repeated-hole test incorrectly used
one as an exact small-hole volume oracle. Its failed measurement is retained in
`modules-focused.log`; the repaired test uses exact STEP equality to an
independently ordered source, plus bore topology, semantic inventory and replay.
No kernel volume code or expected geometry was changed to accommodate it.

## Final qualification

Checkout HEAD is `7051f060c4dccfe64ef7974f645285c57abd8c3d` with this pass's
reviewable working-tree changes. Qualification used .NET SDK 10.0.401 on Windows.

| Gate | Result | Local evidence |
| --- | --- | --- |
| Release solution build | 0 errors; 30 existing warnings retained | `release-build.log` |
| Focused state/surfacing/normalization, CLI and Template checks | 92 + 17 + 10 = 119 passed | `modules-qualified.log`, `cli-qualified.log`, `template-qualified.log` |
| Fast kernel lane | 1,076 passed | `fast.log` |
| Full configured solution suite, including SlowCorpus | 5,508 passed, 0 failed, 0 skipped; 20 nonempty assemblies | `full.log`, `test-summary.json` |
| Canonical owner/consumer gate | 296 owner operations + 69 consumed modules + 4 expected external-body boundaries = 369 sources | `canonical/qualification.json`, `canonical.log` |
| Historical frontend gate | 37/37 matched: 35 exact STEP hashes and 2 expected rejections | `after/comparison.json` |
| Recent Structural/Sculpting/BodyState/PlasticShell baseline | 30/30 source texts unchanged; baseline artifact SHA verified against the previous qualified parity record; 28 exact STEP files | `domain-current.json`, `domain-current.log` |
| Two changed STEP files | Intentional mixed-diameter PMI correction; reimport and annotation-free re-export have identical geometry hashes and topology | `geometry-comparison.json` |
| Profile Template control arithmetic | Before/after exact STEP SHA-256 `6dd25297e4d52687998c4546076ea3ee886295fa7cdce152088876a08e5eaad4` | `template-before.step`, `template-after.step` |
| Claude X4 | 32/32 passed, cases/goldens untouched | `x4.log` |
| Sheet profile corpus | 26/26; formed STEP/flat parity true | `sheet-syntax.log` |
| Sheet semantic corpus | 29/29 | `sheet-semantics.log` |
| Final public documentation and assembly documentation contracts | 14/14 passed | `docs-qualified.log` |
| Runtime/storage inventory | No dotnet/testhost/VBCSCompiler/aetheris host remained; this evidence tree 144.3 MiB; C: free 200.3 GiB at sample | Final inventory |

The two PMI changes are `BodyState/surf-x3b-multi-operation-replay` and
`BodyState/surf-x3b-safe-feature-after-add`. The former retains 22 faces / 48 edges /
28 vertices and normalized geometry SHA-256
`bc8fe7b67babdad6d88e0720ce72acc3915d2caf21aa8fc9a8a5614dbe026228`.
The latter retains 17 faces / 37 edges / 22 vertices and normalized geometry hash
`61795e22417d43db384fd54270fe97f711932fc6467871aeac38796a8ee960b9`.
The raw STEP metadata previously assigned diameter 4 to every bore; the corrected
PMI distinguishes the authored diameter groups and quantities, with exact face
associations. Geometry normalization removes annotations only for comparison;
shipping STEP retains the corrected PMI.

The 30-source comparison reuses the previous domain migration's retained
before/after snapshots, not a newly recorded golden. It verifies each baseline
STEP's recorded SHA and compares its saved source text with the unchanged
current source before rebuilding. The paired source-reference and independent
operation-order tests supply portable regression coverage as well.

The solution visits FrictionLab, which currently discovers no tests; it is not
counted as a passing test assembly. These are local compiler/CLI/STEP results,
not remote CI or interactive Helios certification. Frozen Zig was not run:
neither its compiler nor its infrastructure changed.

## Code-quality review and reproduction

Review covered direct statement ownership, closed field consumption, V1 isolation,
name resolution, duplicate semantic IDs, state/history identity, typed replay,
source anchors, warning/error boundaries, product identity, and runtime lifetime.
No new whole-document regex dispatch, diagnostic fatality allowlist, independent
field catalogue, source macro framework, global parser state, service or startup
hook was added. The projection omits only known AST state/output spans; guide
hygiene reuses the existing AST rewriter/binding scopes. Geometry builders are
reused; no kernel operation was added. `git diff --check` passes. Canonical and
Claude X4 fixtures/goldens have no working-tree changes.

Run serially from the repository:

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

## Remaining blocker and verdict

The next body-state blocker is concrete: `OffsetRegionSculptor`,
`ReplaceRegionSculptor`, `BlendBoundarySculptor`, and
`SectionChainHousingBrepBuilder.BuildAddEast/BuildRemoveThroughX` construct a
whole housing from HousingConstruction. They do not take an arbitrary input
BRep and emit a bounded topology graft with successor correspondence. Passing
a generic body through those builders would replace unrelated engineering
geometry. Capability checks now prevent that path.

A bounded support-surface graft already exists in ImportedFaceRegionReplacer;
it would be inaccurate to say every replacement lacks a kernel algorithm. That
adapter still computes a HousingConstruction successor and housing PMI/interfaces,
while the source-level ReplaceRegion owner resolves housing regions. An
engineering-source state currently retains complete original STEP/PMI and the
available correspondence, but has no common bound semantic-face/PMI projection
for editing and re-exporting an arbitrary successor. Removing only the guard
would discard or mislabel that authority.

The smallest remaining X2 prerequisite is to decouple the existing bounded graft
from housing bookkeeping and supply it with authoritative face selection, PMI
association succession and typed replay through the shared domain-value seam.
This is semantic adapter debt, not new syntax or proof that the existing kernel
is incapable. More general offsets, blends and SectionChain topology would also
need broader kernel materializers; those are outside the no-new-features round.
No fitted housing, new parser route or success flag is used to conceal either
boundary.

**Meaningful progression for the overall semantic closeout.** Construction,
history retention, unsafe text recognition, requirement admission and validation
option debts are repaired and separately qualified above. Full X2 semantic
consolidation and arbitrary-body editing are not declared complete. No further
user syntax decision was required for these repairs.
