# October 2026 Firmament language decision register

Sketcher / principal-plane amendment (2026-10-19): `Concept Struct on XZ`
was an admission limitation, not a language prohibition worth preserving.
For a local 2D layout consumed by `Profile ... using Layout`, admit XY, XZ and YZ
through the existing ConstructionPlane owner. XZ has local axes +X/+Z and normal
-Y; YZ has local axes +Y/+Z and normal +X. Keep default/explicit XY byte-compatible.
This is a frame selection for local 2D guides, not an implicit transform of typed
3D semantic values or unrelated consumers. Mixed spatial layouts and unsupported
material consumers still need explicit admission. The original X0/X4 XZ specimens
are retained; their rejection assertions must become orientation/geometry checks
after qualification, with an explicit traced-plane reference for X4 f01.
No specimen is rewritten to disguise the original requested plane.
Compose now preserves the inherited Base Profile frame and requires matching
frames for authored Base/Add/Remove operands. Existing world-coordinate support
adapters reject oriented Boss/Pocket/Hole/Slot/EdgeFinish combinations explicitly.
The X4 f01 qualification includes absolute world bounds, because comparing two
routes that both dropped the frame originally gave false parity.

Shared Profile amendment (2026-10-16):
[mechanical / sheet-metal reconciliation](FIRMAMENT-SHEET-METAL-SYNTAX-2026-10.md)
admits compact `Profile name = Rect2/Circle2/Slot2 { ... }` through the existing
boundary owner and lets native sheet Base/Cut consume named ordinary Profiles.
Canonical camelCase fields retain PascalCase compatibility. Legacy sheet Slot
cuts preserve rectangular geometry; modern Slot2 uses overall capsule length.
Nonrectangular bases, non-XY sheet profiles and unsupported cut curves reject.
Semantic-layout/manufacturing productions and the overloaded Intent compatibility
field remain explicitly bounded follow-up work; no kernel capability is implied.

Compile-time choices amendment (2026-10-16):
[implementation and qualification](FIRMAMENT-COMPILE-TIME-CHOICES-RFC.md) supersedes
the earlier enum-Static/total-Match deferral for the qualified engineering value
consumers. `Options` aliases `Enum`; preferred `match` retains `Match` compatibility.
Ordinary Enum/Bool value matches require complete unique cases and compatible
arm types before selected-only evaluation. Scalar Static, Record, Template,
Feature and Concept consumers share that admission and preserve selected
requirements into explicit PMI. The existing numeric Pattern-source selector
remains a bounded compatibility construct; this does not introduce general Int
matching, statement-level conditional construction or a new geometry capability.
Fine-grained expansion source mapping and all-domain expression consolidation
remain separate work, as described in the report.

X4 authoring retest amendment (2026-10-15): [repairs and final qualification](FIRMAMENT-FRONTEND-X4.md) close all 32 checks in Claude's current corpus. The decisions at the end of this register supersede only their qualified portions of the historical dispositions below.

X2 production binding amendment (2026-10-15):
[current semantic migration report](FIRMAMENT-FRONTEND-X2.md) migrates
Feature definitions/calls, private construction renaming and defaults to lexical
AST binding. Shared numeric AST evaluation replaces Feature's private arithmetic
parser and the separate syntax-field arithmetic implementation. Static scalar
binding and Concept arithmetic share the numerical operator/type policy. Keyed
Set, record-array and Assembly/Scene occurrence Pattern headers/mappings use AST
nodes; semantic point Pattern references no longer replace field labels.
Product Template lookup and Record/Static/Table lexical data ownership now use
the AST. The shared typed data catalog replaces independent canonical readers.
Set/array collections, Template body transport, several domain readers and complete
include/expansion provenance remain unfinished. X2 is **Meaningful progression**,
not Accepted; the original findings/rationale and all 24 recommendations below
remain tracked.

Concept/2D amendment (2026-10-14):
[production qualification](FIRMAMENT-CONCEPTS-AND-2D.md) adds named
scalar predicates and record-shaped labeled requirement bundles. Structural
Concepts compose named contracts and check actual semantic members. Plain 2D
shape names are canonical; Concept-prefixed closed families share their existing
geometry owner. Nonzero rotation of explicit Triangle2/Polygon2 now rejects
instead of disappearing. These decisions extend #1–#4, #9 and #23 within the
qualified mechanical lane; the original 24 recommendations and other domain
dispositions below remain in force.

| Decision | Disposition | Boundary |
| --- | --- | --- |
| Named predicates and labeled Boolean bundles | Implemented in production AST admission | Compile-time scalar parameters; Require applies a check. A bundle groups requirements, not data. |
| Structural contract composition | Implemented in production AST admission | Named `: A && B` composition, inherited type conflicts/cycles rejected; no material class inheritance or new namespaces. |
| Claimed Concept Struct conformance | Checked through the existing semantic value resolver | Unknown/missing/wrong members reject even without material use; underlying binding diagnostics survive. |
| 2D spelling consistency | Plain shape declarations preferred; legitimate Concept prefixes preserved | Same existing curve/Profile/AIR/BRep/STEP owners and fields; no new 2D solver or Boolean engine. |
| Explicit-shape rotation | Reject nonzero rotation, preserve zero rotation | Author rotated points or use Curve2 placement; do not silently change previously emitted coordinates. |
| Predicate casing | PascalCase names/types; camelCase parameters and labels recommended | User symbols retain case. Published structural member names retain their existing identity. |
| Whole Record/shape/frame/selector predicate inputs | Deferred to X2 semantic consolidation | Their value owners are not yet one shared expression environment; no string-based substitute is added. |
| Legacy dimensional Require / projected PMI | Existing owner preserved | Its block form is not reinterpreted as a Boolean bundle. |

Compile-time value-binding amendment:
[qualification report](FIRMAMENT-COMPILE-TIME-VALUES.md) carries
typed Record requirements through scalar/whole-Record Template and Feature
parameters, defaults, nested specialization and local aliases into admitted
geometry and explicit PMI. It adds original requirement/binding receipts and
repairs value substitution of labels/shadowed locals under #1–#4, #8, #9 and
#19. The shared scalar graph remains authoritative; full scoped AST expansion,
all-domain expression consolidation, enum Static and total Match checking are
not declared complete. This supersedes the earlier Record/Template propagation
deferral only for the report's qualified lane.

Typed-tolerance amendment: [qualification report](FIRMAMENT-TYPED-TOLERANCES.md)
adds typed scalar Static requirements to unified mechanical admission, retains
nominal geometry, and explicitly projects selected Length requirements through
native PMI to AP242. Diagnostic severity and original-source-span preservation
gain coverage under #1–#4. Path spelling is reconciled under #17; Semantic,
Region, Enum/Match and Set roles are now distinguished in the authoring guide.
At that pass, Template/Record/domain-wide tolerance propagation remained a technical follow-up,
not a completed semantic consolidation or an implicit change to units doctrine.

Directive/casing follow-up: [implementation and qualification](FIRMAMENT-DIRECTIVES-AND-CASING.md)
closes the syntax review's ignored-include and schema-formatting witnesses.
Recommendations #1–#4 gain directive admission and located structured CLI errors;
#17 gains AST-bound symmetry field aliases and lowercase from/to links. The
24 original recommendations and broader deferred scopes remain preserved below.
This does not claim a universal module system or completed syntax reconciliation.

The October 14 [syntax review](FIRMAMENT-SYNTAX-REVIEW-2026-10.md) supplements
this register with twenty language/teaching seams and exact execution witnesses.
Its initial documentation-only pass left include/schema findings open; the
implementation follow-up above supersedes that status. Documentation changes
alone did not implement recommendations #1–#4, #7 or #23.
All 24 original recommendations and their historical dispositions are preserved
below. Later production amendments supersede only the scopes they qualify.

Source: [Claude's original authoring audit](firmament-authoring-pass-2026-10.md).
This register preserves its 24 recommendations and their rationale. A future
milestone assignment is a disposition, not an implemented feature. X0 remains
partially qualified across the whole language; the [production mechanical migration](FIRMAMENT-MECHANICAL-FRONTEND-MIGRATION.md) closes the named mechanical admission defects. Historical X0/X1 classifications below remain evidence of their original observations.

| # | Original recommendation and rationale | Disposition | Decision / remaining boundary |
| --- | --- | --- | --- |
| 1 | Validate through parsing, binding and capability admission; F2/F5/F10/F18/F22 contradicted builds | Fixed in X0, bounded | Generic mechanical validation invokes existing CompileSource; static/external-pack analysis is explicitly ParsedNotBuilt. Specialized domains and source consumption remain unqualified. |
| 2 | Every V2 error stops compilation; no fatal-code allowlist or accidental V1 fallback | Fixed in X0 for fallback; X1 unified parser for profile admission | Shared severity adapter defaults legacy diagnostic strings to errors; explicit traces/information and the existing tolerance warning remain nonfatal. The earlier universal gate exposed legitimate junction/derived-profile admission missing from the generic parser. Production mechanical AST binding now admits those existing selectors and gates Profile/Compose execution; dedicated non-mechanical domain migration remains separate. Serialization-shaped roots remain with the legacy reader even when data values are V2 keywords; that reader must prove explicit version 1. |
| 3 | Unknown statements/fields must error; never silently omit Fillet | Fixed for named production mechanical cases; remaining X1/X2 | Canonical root/Modify block checks use lexer boundaries; Box fields reuse generated metadata. The production mechanical migration now rejects the Compose Fillet and erased Rect2 field witnesses through AST-owned consumption. Other domain owners still require migration. |
| 4 | Every error has a sentence, span and consistent JSON; preserve SemanticSourceNotFound text | X1 unified parser, partially fixed in X0 | Generic build/report share an object shape and original token spans where identifiable. Enclosing locations disclose lost provenance. Legacy/domain producers still need typed located diagnostics. |
| 5 | Reject ignored frames such as On XZ | Fixed in X0, bounded | Profile layout frame resolution rejects unsupported XZ and unresolved Using frames. Asymmetric bounds witness retained. This does not qualify every attachment/frame owner. |
| 6 | Correct Model summary and suggest through-cut vocabulary for Pocket | Fixed in X0 | Document owner is projected from existing semantic parse metadata; Pocket through-depth error suggests Remove with Role: ThroughCut. |
| 7 | Parse once into an AST over Lex, migrate construct families in shadow mode | Production mechanical migration; remaining X1 unified parser | The shared recursive-descent AST over the existing lexer is now used by production mechanical admission. Existing engineering lowerers are retained. Other domain syntax coverage and complete AST expansion remain tracked work, not a shadow-only prerequisite for this mechanical repair. |
| 8 | Replace text rewrites with AST transforms; lexical Pattern substitution leaks scope | X1 structure; X2 semantic consolidation | Original Template/Feature/Pattern bodies are now structured in shadow mode. Production rewrites, expansion origin receipts and scoped AST transforms remain unimplemented; they require X2 binding. |
| 9 | One expression evaluator in every value position | X2 semantic consolidation | Do not expand arithmetic admission during X0. Existing owner-specific evaluators remain. |
| 10 | One point literal: arrays and typed Point2 constructor mean the same thing | X3 syntax reconciliation | Preserve existing route-dependent aliases; do not broaden syntax by accidental regex acceptance. |
| 11 | One generic variant mechanism; EdgeFinish<Chamfer/Fillet>, retaining Kind alias | X3 syntax reconciliation | Generic EdgeFinish is not implemented. Its failed build is now rejected by normal validation. |
| 12 | Unify five Pattern spellings around finite sources and feature expressions | X3 syntax reconciliation | Binding and source provenance prerequisites belong to X1/X2; no Pattern rewrite in X0. |
| 13 | One cut vocabulary; through-Pocket and Add/Remove aliases | X3 syntax reconciliation | Existing finite Pocket versus Remove ThroughCut distinction is retained and diagnosed. Adding termination semantics is not an X0 repair. |
| 14 | One rule for optional/required Model roots | X3 syntax reconciliation | Preserve current roots until route identity and consumed-source equivalence are established in X1. |
| 15 | Remove nonexistent documented schema and invalid nesting examples | Fixed in X0 in part; X4 authoring retest | Removed nonexistent schema Revolve and reconciled casing prose. Complete Feature/Pattern nesting retest remains X4. |
| 16 | Newline and semicolon separators everywhere | X3 syntax reconciliation | Existing separators preserved; root and Modify unknown checks no longer depend on line-start regex matching. |
| 17 | One casing doctrine, with preserved domain symbols; reconcile Pmi/PMI | X3 syntax reconciliation | Documentation now states PascalCase constructs, camelCase fields, accepted owner-specific aliases. Global normalization and Pmi reconciliation deferred. |
| 18 | Retire or justify rare one-off constructs; consider library Features | Rejected/deferred with reason | Frequency alone is insufficient evidence to delete admitted engineering constructs or churn stable identities. Review after X1 corpus equivalence. |
| 19 | Scope-local named values with arithmetic | X2 semantic consolidation | General Let remains a non-goal for X0; decide syntax only after binding ownership is unified. |
| 20 | Inline vertex lists for polygon/revolve outlines | X3 syntax reconciliation | No new polygon/revolve grammar during X0. Existing named-point diagnostics retained. |
| 21 | Uniform planar-face references (+Z, face(+Z), Body.Top, Top) | X2 semantic consolidation | Typed target binding must precede alias expansion; current owner-specific constraints remain explicit. |
| 22 | Explain route, expansion and bound Pattern instances | Fixed in X0, bounded; X1 shadow evidence | --shadow --explain separates original structural status/summary, actual generic production route and normal production validation. --ast exposes original bodies/trivia. Bound expansion explanation remains X2; no invented expansion provenance. |
| 23 | Generate a grammar reference from the AST | X1 unified parser, deferred pending coverage | A compact AST now exists, but remaining Interface/domain syntax gaps and absent binding mean it cannot yet generate the authoritative complete grammar. Existing field metadata remains intentionally incomplete. |
| 24 | Fillet+Hole, box shell/hollow, non-XY Compose capability gaps | Separate geometry/kernel feature | Improve rejection and preserve existing admitted frame mechanisms. No new kernel feature or general plane capability added. |

The September [profile-pipeline audit](../../history/eras/08-domain-expansion.md) and earlier
[reconciliation register](../../history/eras/07-previews-1-to-3.md) are historical
evidence, not proof that October's route-dependent behavior is resolved. Their
previous resolved classifications must be read with this qualification.

## X1 differential decisions (2026-10-14)

See the [X1 qualification](FIRMAMENT-FRONTEND-X1.md). Categories A-E
classify observed differences, not automatic permission to change production.
Machine reports deliberately retain `Unclassified` where source receipts cannot
justify a stronger conclusion. This table records the **historical shadow-only foundation** before production
mechanical migration. Current outcomes for the named defects are below. Recommendations not amended above retain their
original X0 dispositions, including all syntax design and kernel deferrals.

| Audit finding / recommendation | Old behavior | Shadow interpretation / category | Authority and next decision |
| --- | --- | --- | --- |
| F3 / #3 unknown Compose Fillet | Builds while Compose AIR has no LostIntent operation | AST retains Fillet LostIntent and exact span; receipt comparison flags the omission. **B: existing parser defect** for this witness. | Production remains authoritative but defective here. Complete Compose operation admission over AST before migration; X2 capability ownership must reject unsupported Fillet. |
| F3 / #3 erased boundary field | Rect2 RecessGuide builds with heigth discarded | Original field/value/span survive in AST. Field-consumption comparison is explicitly unqualified. **B: existing parser defect** for the witness. | Reuse boundary owner's field schema/consumption receipts in an AST adapter; do not add another field vocabulary. Not fixed by structural acceptance. |
| F10 / #1/#2 profile admission | ReflexNotch generic parsing rejects; actual build succeeds | AST retains .[Inner, Upright] as member-set syntax. **B: generic admission defect**. | Existing profile lowerer remains geometry authority; migration requires its actual selectors and admission semantics, not generic-parser success. |
| F6/F16/F20 / #7/#14 | Unrelated declarations can affect whole-document production route | Box/Hole/Modify/EdgeFinish subtrees remain identical with Static, Slot2, Pattern, Profile siblings and admitted structural wrappers. The SlotRoutePerturbation witness builds without the unused Slot2, then rejects on CanonicalAdvanced when it is added. **B** route dependence; structural invariance is proved, semantic invariance is not. | No new root rule. Production root admission remains X2/X3 work; do not infer semantic parity from AST identity. |
| F15/F17 / #8/#12 | Pattern/Template bodies are rewritten as text | Keyed mappings, typed arguments, generic invocations and occurrence bodies are structured. **B** scope/provenance gaps remain in production expansion. | No text expansion replacement in X1; X2 owns scoped binding and expansion receipts. Pattern syntax reconciliation stays X3. |
| F4/F23 / #10/#17 | Owner-specific case and point aliases | AST preserves authored spelling and distinct array/constructor forms. **D: compatibility aliases**, only where existing owners admit them. | No global normalizer or equivalence assertion. Binding normalization in X2; language reconciliation X3. |
| #7 Interface/domain coverage | Role requires, Mate relations and imported #face selectors have existing owners | These forms currently receive located shadow syntax errors. **A: missing new-parser coverage** for admitted fixtures. | Complete these structural forms before claiming X1 Accepted; do not weaken the production owners. |
| #7/#16 specialized shorthand | Blind 10mm, CenteredAt 50mm and some Extend forms remain domain syntax | Shadow rejects these forms rather than consuming opaque header text. **A: missing coverage**. | Add bounded typed syntax and source tests within X1 follow-up; preserve lowerers. |
| #7 Sculpting +Model sources | Canonical fixtures contain leading plus characters | Shadow reports the actual plus token. **C: language ambiguity** pending owning frontend audit. | Do not strip punctuation globally or silently bless an accidental source spelling. |
| #9/#19 expressions | Existing evaluators admit different value positions | Structural precedence/member/index/call syntax is represented; values remain unbound. **C** where grammar/generic-spacing is ambiguous. | No evaluator replacement. X2 owns dimension/type/name checks; X3 owns grammar reconciliation. |
| #22 explanation | Production route and limited counts only | Separate shadow/production results plus explicit coverage limits are available. | Expanded/generated spans and bound instances remain unavailable when old rewrites lose provenance; X2 prerequisite. |
| #23 generated grammar | No complete unified grammar | AST is a partial structural contract, not complete language authority. | Generate a reference only after all admitted source forms and context admission have qualified. |


## Production mechanical dispositions (2026-10-14)

The user authorized direct production migration instead of another shadow-only
milestone. The earlier findings and rationale above are preserved, not rewritten
as if they had never existed. Current mechanical outcomes supersede the old
production-status statements:

| Recommendations | Current disposition | Evidence / remaining ownership |
| --- | --- | --- |
| #1, #2 | Fixed for the migrated mechanical path | Shared AST admission, retained bound engineering inputs and authoritative build gate; member-set profile witness agrees in validate/build. Dedicated domain owners remain outside this cutover. |
| #3 | Named mechanical P0 cases fixed | Compose Fillet rejects; erased Rect2 heigth rejects; actual AST field readers close their scopes. No universal claim for every old path/derivation/domain adapter. |
| #4 | Improved in production migration | Syntax messages and original spans survive validate/build; generated-source provenance limitations remain disclosed. |
| #7, #14 | Production migration, remaining X1/X3 work | Model and material-bearing Struct use UnifiedMechanical. Unused Slot2 no longer changes grammar or STEP. Existing V1 and dedicated material-domain paths remain explicit compatibility boundaries. |
| #8, #12 | X2/X3 remain | Existing expansion is reused once for mechanical execution; AST expansion and Pattern reconciliation are not implemented. |
| #9 | Bounded mechanical consolidation; remainder X2 | Profile measure parsing uses AST expressions; no general Let or cross-domain evaluator replacement. |
| #22 | Production route visibility plus optional evidence tooling | Normal explain reports UnifiedMechanical. Developer AST/comparison tooling is retained without controlling engineering admission. |
| #23 | Remaining X1 coverage prerequisite | The production mechanical AST does not yet define every other domain's grammar. |
| #5, #6, #10, #11, #13, #15-#21, #24 | Original dispositions retained | No new frame/kernel capability or global syntax reconciliation is claimed by the mechanical migration. |


## Continuing production domain migration (2026-10-14)

The [domain migration report](FIRMAMENT-DOMAIN-FRONTEND-MIGRATION.md)
extends production ownership; it is not another shadow-only milestone. Original
recommendations and rationale above remain preserved. Whole-language completion
is withheld until the remaining domain consumers have actually migrated.

| Recommendations | Current change | Remaining qualification / ownership |
| --- | --- | --- |
| #1, #2, #3, #4 | Gear/Revolve validation uses common build preflight and structured diagnostics. Gear, Revolve/Axis, Panel, Plateau, Loft and SectionChain record fields bind from AST and close their scopes. | Source-based Concept/profile adapters, lowercase V2 and other domain consumers remain. No universal claim for unknown intent in those owners. |
| #7, #14 | Legacy unit-bearing headers and wrapper-free profile construction share mechanical AST binding. Phase3, RoundedBox, primitive-policy and standalone-profile document parsers are deleted. | Lowercase V2, Assembly/Scene, Wire/Sweep and the other domain compilers retain their existing owners pending migration. Explicit V1 serialization remains deliberate compatibility. |
| #9 | Panel surface equations and Revolve angle aliases use shared expression syntax. | Existing semantic algebras/lowerers retain their domain-specific dimensional and geometry contracts. |
| #8, #12 | SectionChain inline frame/profile extraction uses AST spans and preserves existing generated identities. | Scoped AST specialization/substitution with expansion provenance remains required before deleting Template/Feature/Pattern source expansion. |
| #22, #23 | Migrated domain routes are inspectable; incomplete blocks remain in AST for language-service context while compilation fails. | A complete grammar/reference must await remaining domain coverage; developer comparison tooling grants no admission. |
| All other recommendations | Prior dispositions retained. | This change introduces no kernel capability, runtime execution model, general Let, inheritance or new value/reference semantics. |


## Lowercase compatibility and preferred spelling (2026-10-14)

The [lowercase production cutover](FIRMAMENT-LOWERCASE-AST-MIGRATION.md)
continues direct migration. Historical spellings are compatibility input, not a
second preferred syntax. Recommendations and their original rationale above
remain intact; this amendment records only implemented changes.

| Recommendations | Implemented disposition | Remaining ownership |
| --- | --- | --- |
| #1-#4, #7, #14 | Primitive solids, immutable Box derivation, exposure aliases, Hole modifications and region/cut in lowercase V2 now consume shared AST nodes and reuse native primitive/Hole binding. Unknown intent in these scopes rejects; old solid/field/Modify/Hole/region/cut regex readers are removed. | Explicit CompatibilityExtensions owners retain typed lets/tolerances, PMI, manufacturing concepts, process Templates, recognition/replacement and lattice grammars. No claim of universal AST admission. |
| #22, #23 | Explain identifies UnifiedCompatibility versus remaining CompatibilityExtensions ownership. | Assembly/Scene, Wire/Sweep, expansion and other domain consumers still require direct migration and geometry qualification. |
| Casing / syntax reconciliation | Prefer PascalCase constructs/types, camelCase built-in fields, lowercase using/as links and preserved engineering symbols. Shared parsing, Profile binding, formatting and language-service token classification admit as. The syntax reference is reorganized and examples updated in place. | User-defined names and published members are not globally folded or renamed. Legacy aliases remain only as bounded compatibility; this is not a promise to perpetuate every V1-to-V2 grammar detail. |
| Invalid selector override fixture | Still invalid, now with a precise firmament-syntax-statement at @ rather than a later record-binding error. | Original failing source remains unchanged; fixture diagnostic metadata follows its migrated owner. |
| All other recommendations | Prior disposition retained. | No kernel, units doctrine, runtime model, general computation or new shadow-only milestone is introduced. |

## X1 closeout amendment — Assembly contract admission

The preferred relationship syntax is a reusable `Interface<Family>` declaration
plus a named `Mate` application. Family requirements and normal atomic lowering
are compiler owned. Inline typed Interface applications remain admitted.
Historical Role/Lower/Fit/Allow clauses bind shared AST nodes into the existing
Interface/Assembly IR; they are bounded compatibility and specialist contracts,
not a second preferred introductory syntax. Custom atomic relationships remain
explicitly available through `Interface<Custom>`.

This closes the identified canonical Role-requirement structural gap and adds
closed checking of the migrated contract and role-assignment bodies. It does not
claim a new Assembly solver, universal AST expansion, a single evaluator across
all domains, or complete include/expansion source maps. Source locations in
expanded Assembly scopes are labeled as such. The closeout report and corpus
ledger record qualification separately from these language decisions.

Named Mate assignments retain their declared role spelling. Compact inline typed
applications retain the existing `a`/`b` aliases for predefined `A`/`B` slots;
these are documented compatibility aliases, not global case folding. New examples
use `A`/`B`. Built-in contract options consume camelCase and their existing
PascalCase aliases through shared field matching.

The [X1 closeout](FIRMAMENT-FRONTEND-X1-CLOSEOUT.md) qualifies all
327 canonical sources structurally, classifies the complete 1,266-source corpus,
and records 4,943 passing full-suite tests plus 26 byte-identical STEP baselines.
This closes the structural migration gate, not X2 semantic consolidation or the
remaining domain source adapters. The nine explicit canonical admission limits
and downstream-only negative expectations remain visible in the closeout ledger.


## X2 current owner and handoff matrix

This supplements the original recommendation/rationale table; it does not replace
its history. “Retained” means the existing admitted semantics remain, not that the
complete language surface is reconciled. X2 remains Meaningful progression.

| # | Current implementation / semantic owner | Compatibility impact | X3 decision / X4 evidence still needed |
| --- | --- | --- | --- |
| 1 | Shared production admission; Feature/Template/data binding failures reach validate/build | Existing valid sources retained; lexical sibling capture rejects | X4 repeat all twelve sources through both commands. |
| 2 | Shared V2 disposition/severity and explicit serialized V1 boundary retained | Feature fatal severity retained; shared syntax owns control-flow rejection | X4 ensure every failure remains V2 and actionable. |
| 3 | Mechanical AST field/statement owners; Feature/Pattern bodies and Record/Table fields consumed | Labels cannot be rewritten as iteration references; other domains still pending | Complete domain source consumption in X2, then X4 omission witnesses. |
| 4 | Compiler diagnostic records; original Feature definition/use anchors added | Existing codes/severity/report retained; explain adds receipts | Complete module/Template maps in X2; X4 diagnostic repair effort. |
| 5 | Existing placement/frame admission retained | No new plane fallback or kernel placement behavior | X4 asymmetric XZ and attachment witnesses. |
| 6 | Existing document summary/Pocket capability messages retained | No semantic change | X4 check model identity and through-cut guidance. |
| 7 | X1 lexer/recursive-descent AST remains structural authority | No replacement parser or new dispatch | X3 root/scope teaching; X4 documented first-try paths. |
| 8 | Feature AST expansion, Template lexical lookup, scoped data identities and Pattern mapping hygiene migrated | Stable names/keys retained; product Template body/value adapters remain X2 | Finish module/Template AST handoff before X3 syntax changes. |
| 9 | Shared numeric AST evaluator/type policy used by fields, scalar graph, Feature and Concept arithmetic | Units preserved; Record/Static/Table scope and nominal schemas migrated; collection/domain value ownership remains partial | Finish typed value consolidation in X2; X4 arithmetic in ordinary parts. |
| 10 | Existing point/vector owners retained; Feature components use shared numeric AST values | Both established point spellings retained where admitted | X3 choose canonical literal and alias policy; X4 point ergonomics. |
| 11 | Existing Hole/EdgeFinish variant owners retained | No new generic EdgeFinish form | X3 reconcile variants; X4 exact repair diagnostic. |
| 12 | Set/array/occurrence AST mappings; semantic point substitution fixed | Human spellings unchanged; radial/mirror/linear owners still separate | X3 unify surface forms after remaining X2 semantics; X4 Pattern authoring. |
| 13 | Existing Compose and materializer authority retained | ThroughCut versus Pocket unchanged | X3 termination vocabulary; X4 enclosure/slot tasks. |
| 14 | Existing document/domain root contracts retained | No new optional-root rule | X3 decide and document root policy; X4 wrapping stability. |
| 15 | Public syntax updated for actual Feature and product Template scope/defaults | No speculative syntax published as admitted | X4 repeat complete public examples and original corpus. |
| 16 | Existing separators retained; serializer meets Assembly adapter requirements | No broad terminator grammar change | X3 reconcile separators, including Grid derivation friction. |
| 17 | Existing spelling metadata/AST normalization retained | Pascal/camel aliases preserved; user symbols retain case | X3 remaining owner exceptions and PMI spelling; X4 examples. |
| 18 | Existing one-off geometry owners retained | No deletion based on frequency | X3 justify library/construct boundaries after semantic qualification. |
| 19 | Feature defaults plus Record/Static/Table references use lexical AST identities and the common typed catalog | No general Let or runtime variables | Finish lexical value ownership in X2; X3 surface choice; X4 parameterization effort. |
| 20 | Existing named outline/profile construction retained | No new inline polygon/revolve grammar | X3 vertex-list policy; X4 L-bracket/revolve friction. |
| 21 | Existing typed face/selection owners retained | No global face alias expansion | X2 typed selector binding; X3 canonical spelling; X4 selection repairs. |
| 22 | Existing inspect and validate --explain expose Feature/Template scopes/arguments; validate also exposes data identities and available origins | Ordinary report shape unchanged; developer metadata additive | Complete Template/Pattern/module explanation in X2; X4 diagnosis usefulness. |
| 23 | AST remains incomplete as an authoritative semantic grammar generator | No aspirational grammar publication | Finish source owner migration, then X3 generated reference and X4 doc qualification. |
| 24 | Existing kernel/material/capability owners retained | No Fillet+Hole, shell or general plane implementation | Separate engineering capability work; X4 accurate unsupported diagnostics. |


### X2 continuation: lexical product Templates

Product Template declarations, parameters, applications and dependency edges now
come from the shared AST. Local shadowing, sibling privacy and definition-scope
nested lookup are qualified through production compilation. Host exports retain
document and single-Model module-container compatibility. Qualified Construct
capability names and historical lowercase analysis headers have explicit shared
parser productions. Existing unique-name specialization identities are preserved.

Recommendations 3, 4, 8, 15, 19 and 22 advance with closed application headers,
source-located lookup errors, lexical receipts and the promoted Family witness.
At that pass they remained partial for scoped Record/Static/Table binding,
complete module maps and source-based body/domain lowerers. The following data
continuation closes the demonstrated schema-capture defect; module maps and
remaining body/domain adapters stay open. No syntax or kernel-policy decision is
waiting for the user. All 24 original dispositions/rationales above remain tracked.


### X2 continuation: scoped Record, Static and Table data

Record/type and Static/Table value references bind lexical AST identities before
typed evaluation. Same-named Records in different scopes remain nominally
distinct. Same-scope duplicates and sibling-private references reject; parameter
and mapping binders protect their own names. Existing unambiguous spellings,
field keys and Table key labels are retained. Internal collision identities are
compiler metadata, not new authoring syntax.

The Record/Table/with/row/alias catalog is shared by specialization and canonical
static erasure. Its declarations and field values come from the shared AST;
ordinary scalar evaluation still belongs to the existing typed graph. Table row
leaves and nested typed aliases enter that graph rather than a second evaluator.
The former Spec schema-capture witness is promoted to a positive geometry test.
A scoped requirement witness checks explicit AP242 PMI and authored tolerance
provenance. Recommendations 3, 4, 8, 9, 15, 19 and 22 advance accordingly.

Complete include-file/module provenance and remaining collection/domain source
adapters remain X2 work. No general Let, runtime scripting, new units doctrine or
geometry capability was introduced. All 24 original recommendations and rationale
remain in the register above; no syntax policy decision is awaiting the user.


### Inventory and module graph continuation

[Inventory Records](../../../public/firmament/inventory.md) bind typed references to
concrete Part, Subassembly, Assembly and Scene exports. PascalCase catalogs and
camelCase members retain the current casing policy. Adapter-typed locators normally
omit extensions; ambiguous matches reject, with explicit extensions as the repair.
`include` remains available for short unqualified compositions; no size threshold
is enforced. Both forms use the AST-owned dependency graph.

Inventory imports remain separate source modules. Existing include composition
uses a mapped compatibility projection. Resource, Inventory and original syntax
errors carry declaring-file spans; downstream generated/native errors still disclose
an enclosing-export limitation where precise producer locations are unavailable.
Definition identities use project-relative resource paths, not machine directories.
Source-only snapshots reject STEP dependencies explicitly. Templates specialize in
the defining module before Inventory export; general cross-module value arguments
are deferred to the remaining typed module contract, without implicit capture.

Recommendations 3, 4, 8, 15, 19, 22 and 23 advance. The full X2 domain/value/source-map
migration is still open; no new parser route, geometry engine or runtime module
system is introduced. Qualification is recorded in the Inventory release report.

## FrontendX4 retest decisions (2026-10-15)

- #19 / a01: `Static name = expression` is the Model-local compile-time value spelling, with optional scalar type inference. Model `let` is not a new alias; Feature-local `let` retains its existing meaning. Update the design specimen to `Static thickness = 6mm`; its geometry oracle is unchanged.
- #20 / f02, f04, g01: admit `Polygon2 Name { points: [[x, y], ...] }` as the inline form of the existing explicit boundary. Coordinates use typed Length expressions; winding, duplicate, intersection and area checks remain owned by the explicit-polygon lowerer. Named `vertices: Set<Point2>` stays supported. No general inline Segment endpoint syntax is added; f03 remains a located rejection.

- #10: planar Hole centers and `Set<Point2>` entries accept `[x, y]` as well as `Point2(x, y)`. Both retain Length checks and the shared numeric evaluator. Spatial Point3/Grid values retain their frame checks.
- #11: `EdgeFinish<Chamfer>` and `<Fillet>` select the existing variant, with `Kind` retained as a compatibility alias. Conflicting variants reject. Profile finish fields close through the existing AST field-consumption owner; extra misspelled fields cannot disappear.
- #12 / #15: a Model/Struct-level Pattern targets exactly one material body. Multiple candidate bodies require an explicit Modify. Source/Grid Patterns and Set/Feature Patterns keep their respective finite binders; generated features enter the target's Modify scope.
- #13: `Pocket { end: ThroughAll }` lowers to the existing Remove/ThroughCut operation, from stock bottom to selected top support. Explicit depth or floor policy conflicts with ThroughAll; an accidentally through-depth Pocket still rejects. No kernel capability is added.
- #21: `+Z`, `face(+Z)`, `Top`, and the active host's `.Top` share planar support binding; spelling alone cannot select Concept point interpretation.
- #4 / #24: the combined Fillet + Hole capability remains rejected, with its explanatory message and original EdgeFinish location. Inline Segment endpoints remain rejected at their Segment declarations. Causal profile errors precede and replace derivative missing-operand noise.

The a01 reference and chimera already use `Static`, so only its design specimen
changes. Other specimens, references and recorded geometry remain unchanged.
The [X4 qualification report](FIRMAMENT-FRONTEND-X4.md) records the
measured outcomes; these amendments supersede earlier deferrals only within
the cases and boundaries qualified there. They do not close every expression,
expansion-provenance or domain limitation tracked by the original 24 items.


## Engineering follow-up: Fillet + Hole (2026-10-15)

The user explicitly authorized the capability after X4. Recommendation #24 is
**partially implemented in a separate geometry feature**, not a new language
restriction or another parser milestone. One Box top `Boundary` Fillet can now
coexist with disjoint top ThroughAll shaft/counterbore holes. The same bounded
builder handles finish-only top-boundary fillets. Four exact cylindrical blends
join on elliptical direct-intersection seams; vertical corners remain sharp.
Original d05 source is retained and becomes a positive capability specimen.
Intersections, tangencies, other combined selectors, blind holes, countersinks,
Regions, mixed finishes and general feature chains remain explicit limitations.
Shell and unrelated geometry work from #24 is still deferred. This supersedes
the d05 rejection disposition above only for the newly qualified combination.
See [qualification and upstream reconciliation](FIRMAMENT-FILLET-HOLE.md).

## Sheet semantic layout and manufacturing follow-up (2026-10-17)

The [modernization report](FIRMAMENT-SHEET-METAL-SEMANTIC-MANUFACTURING-MODERNIZATION-2026-10.md)
records the production AST migration and every disposition from the preceding
sheet audit. This is an implementation follow-up, not another shadow milestone.

- Layout, structural contracts, manufacturing/PMI and ProfileDelta no longer use
  their old block/field regex readers. Unknown intent and unsupported placements
  reject. Validate and build execute the same sheet compiler.
- Shared scalar Record/Static binding and typed `dimension:` projection preserve
  nominal geometry, allowances and value provenance. Ordinary keyed Set/Pattern
  mapping reuses Feature specialization for sheet Hole/Cut consumers.
- Preserve one legacy layout effect bundle and its identities. Multiple implicit
  bundles reject; new effects belong under the consuming SheetMetal body.
- Preserve legacy capsule/rectangular Slot meanings explicitly; new shared
  Rect2/Slot2 Profiles have their established exact-boundary meanings.
- camelCase fields and PascalCase aliases are admitted by the owning field
  reader; conflicting aliases reject. Prefer call-shaped ProfileDelta anchors,
  unnamed owning Pmi and Note; old anchors, one named top-level Pmi and Annotation
  remain compatibility forms.
- Arbitrary-plane sheet layout, general GD&T, constraint solving and callable
  Boolean-composable domain EqualPitch/Mirror intrinsics remain separate work.
  Legacy domain checks are AST-owned; ordinary predicate/bundle Concepts use the
  common checker. These limits do not grant apparent successful admission.

## V2 corpus modernization follow-up (2026-10-17)

The [corpus report](FIRMAMENT-V2-CORPUS-MODERNIZATION-2026-10.md) records the
current-source sweep, original outcomes, qualification levels and remaining
compiler-honesty failure. This does not close X2.

| Decision | Disposition | Rationale |
| --- | --- | --- |
| CORPUS-01: Preferred vocabulary | Applied | Use admitted camelCase fields/lowercase links and bare construction Path/2D shapes. Keep PascalCase aliases and authored/public identities. |
| CORPUS-02: Sheet boundaries | Applied | Shared Rect2 Profiles replace legacy rectangular boundaries without changing Slot geometry. Points and anchors use ordinary constructor calls. |
| CORPUS-03: Implicit Base identity | Fixed | Permit `base: profile` through the shared Base Profile binder. Preserve implicit region/bend IDs; a named Base is a distinct identity choice. Original source/geometry witnesses remain under Compatibility. |
| CORPUS-04: Relative assembly placement | Fixed | Use Parent for relative placement. Child-owned mates do not drive their containing occurrence; retain competing-authority rejection on the actual owner. |
| CORPUS-05: Surfacing casing/crash | Fixed; production AST admission migrated 2026-10-18 | Existing owner reads first-character field aliases and stops before construction after bad mounting data. A shared structural parse is not proof this independent reader is migrated. |
| CORPUS-06: Inner-loop segment identity | Fixed source | Give inner segments explicit aliases; preserve 4160 mm3 intended volume. |
| CORPUS-07: SurfaceCoil supports | Separate semantic/capability prerequisite | Preserve centerline-only witnesses outside Canonical. Define nonmaterial analytic support binding before admitting support solids plus wire output; never erase material implicitly. |
| CORPUS-08: Fixture qualification levels | Applied | Qualify modules through real consumers; inspect gear interfaces; record missing garment bodies separately. Solver-invalid and DFM-warning specimens are not syntax failures. Preserve the now-valid Rect2 TraceLoop specimen and use an open Line2 for the actual negative. |
| CORPUS-09: Piping optional fields | Fixed, Piping AST admission pass | Original reproduction: lowercase connectionType became Generic and unknown fields disappeared. Direct AST production owners now consume shared fields; aliases retain Weld, unknown/duplicate/malformed fields reject. The unchanged source specimens were promoted to accepted/invalid regressions. See [qualification](FIRMAMENT-PIPING-AST-ADMISSION-2026-10.md). |
| CORPUS-10: Legacy semantic-panel Pattern | Deferred identity reconciliation | Keep existing PanelLayout.MountingHoles[index] public paths. Decide explicit identity projection when migrating this Count/Pitch bundle to keyed Feature expansion; do not change IDs for presentation alone. |
| CORPUS-11: Embedded BodyState chains | Fixed ownership | Station-shaped SectionChain construction data belongs to the containing surfacing document. Ordinary Section/Loft and explicit schema guards retain their own rejection behavior; validate/build/inspect must select the same owner. |

No additional fatality allowlist, source macro system or parallel frontend was
introduced. No decision above grants missing geometry or blanket X2 acceptance.


## Remaining-domain AST follow-up (2026-10-18)

The [production migration and inconsistency register](FIRMAMENT-REMAINING-DOMAIN-AST-2026-10-18.md)
records Structural, Sculpting/BodyState, Analysis, PlasticShell, Drawing/Review,
and recovered/evidence-linked SheetMetal ownership. This supersedes the
CORPUS-05 independent-reader qualification above. Unknown intent closes at the
actual AST owner; no shadow-only admission path was added. DOMAIN-01 through
DOMAIN-13 preserve the remaining semantic, expansion, units, provenance and
metadata decisions. This does not mark X2 or separate geometry work complete.


## Body-state and semantic debt follow-up (2026-10-18)

The [body-state debt report](FIRMAMENT-BODY-STATE-DEBT-2026-10-18.md) records
current dispositions for all thirteen DOMAIN items without erasing their
original findings. Engineering-product construction/replay now preserves the
normal compiler's exact output; hole-free housings work; repeated holes retain
an attachment; unsupported reconstruction histories reject atomically. Mixed
hole-diameter PMI is corrected. Profile guide hygiene and Record member checks
use AST ownership instead of whole-word source matching. Shared compile-time
Concept/Require checks are admitted at Structural/Sculpting/PlasticShell product
scopes, and owners no longer silently ignore static-only validation requests.

This is not general BRep operation grafting, derived-dimensional algebra,
a new structural collection language or complete expansion source maps. Those
remain explicit capability/consolidation boundaries under the no-new-features
constraint. See the report for qualification, compatibility changes and the
concrete remaining housing-materializer coupling.

## Exact face and subdivision representation follow-up (2026-10-18)

The [generic sculpting foundation](FIRMAMENT-GENERIC-SCULPTING-2026-10-18.md)
separates exact BRep successors from explicitly requested mesh successors.
This extends the bounded DOMAIN-02 capability disposition; it does not erase
the remaining imported-product/PMI projection prerequisite.

| Decision | Disposition |
| --- | --- |
| Native exact face editing | Reuse the shared bounded support graft with current face/edge bindings, a bicubic graph, preservation contracts and typed replay. No housing reconstruction. Unsupported metadata/continuity/intersection claims reject. |
| `MeshState` | Explicit box/sphere cage conversion and finite Catmull-Clark editing under the existing Sculpting AST owner. One operation per successor, stable parent-derived handles and protected vertices. |
| Output and precision | Mesh successors are STL/GLB artifacts, never implicit STEP. Validation reports `MeshBuilt` and unavailable STEP output. `maximumDeviation` bounds conversion and inherited output quantization; deformations remain explicit. |
| Future boundaries | Arbitrary BRep retopology, new cage constructors, full PMI/import metadata succession, global mesh self-intersection proof and interactive editing remain separate qualification work. No silent fallback or implied X2 closure. |

## Exact convex-stock primitive follow-up (2026-10-18)

| Decision | Disposition |
| --- | --- |
| `Polyhedron<N>` | N is the compile-time face count of a regular convex solid: 4, 6, 8, 12 or 20. Positive `edgeLength` supplies its size. Other face counts do not imply a unique regular construction and reject. |
| `Ellipsoid` | Three positive `radii` are local X/Y/Z semiaxes. The native authority is an exact ellipsoid quadric split into eight octants. Rational patches are an exact STEP projection and recover to native quadrics through the existing Judgment recovery owner. An asymmetric egg is a separate construction. |
| Whole-stock rounding | Reuse `EdgeFinish<Fillet>` with `target: AllEdges`, no face selector and a positive radius. Admit one operation on untouched direct size-based Box or regular Polyhedron stock. Realize all cylindrical rolls and spherical corners together from the inward-offset convex lattice. |
| Geometry meaning | Box stock retains its frame and outer dimensions. Polyhedra retain original support planes; vertex extrema can recede under rounding. Radius stays below the inradius, leaving planar faces and straight spans. Existing RoundedBox silhouette geometry is unchanged. |
| Capability boundary | Arbitrary-body all-edge blends, mixed edits, hollow stock, all-edge chamfer and semantic Bounds/Pattern construction adapters reject. General blending remains separate work. No implicit mesh conversion. |
| Presentation and interchange | Rounded Box must not reuse a sharp Box CIR mirror. Use its actual BRep mesh. Native ellipsoid angular pcurves require explicit STEP-domain remapping before export; the primitive emits exact 3D edge curves without pcurves. General ellipsoid editing/pcurve succession remains outside this pass. |

The STEP entity review confirmed that [`ELLIPSOID`](https://www.steptools.com/docs/stp_aim/html/t_ellipsoid.html) exists as a CSG primitive, while [`advanced_face_geometry_select`](https://www.steptools.com/docs/stp_aim/html/t_advanced_face_geometry_select.html) does not admit it as a BRep face surface. Direct CSG interchange needs separate qualification; this pass retains exact patch projection for BRep exchange. See [the qualification report](FIRMAMENT-CONVEX-STOCK-2026-10-18.md#step-entity-review-requested-during-qualification).
