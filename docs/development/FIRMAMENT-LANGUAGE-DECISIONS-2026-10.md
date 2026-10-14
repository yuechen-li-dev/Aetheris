# October 2026 Firmament language decision register

Source: [Claude's original authoring audit](firmament-authoring-pass-2026-10.md).
This register preserves its 24 recommendations and their rationale. A future
milestone assignment is a disposition, not an implemented feature. X0 remains
partially qualified across the whole language; the [production mechanical migration](../release/FIRMAMENT-MECHANICAL-FRONTEND-MIGRATION.md) closes the named mechanical admission defects. Historical X0/X1 classifications below remain evidence of their original observations.

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

The September [profile-pipeline audit](firmament-v2-language-audit.md) and earlier
[reconciliation register](firmament-language-inconsistencies.md) are historical
evidence, not proof that October's route-dependent behavior is resolved. Their
previous resolved classifications must be read with this qualification.

## X1 differential decisions (2026-10-14)

See the [X1 qualification](../release/FIRMAMENT-FRONTEND-X1.md). Categories A-E
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
