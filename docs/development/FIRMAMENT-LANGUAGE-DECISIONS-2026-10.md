# October 2026 Firmament language decision register

Source: [Claude's original authoring audit](firmament-authoring-pass-2026-10.md).
This register preserves its 24 recommendations and their rationale. A future
milestone assignment is a disposition, not an implemented feature. X0 remains
partially qualified; see [the release report](../release/FIRMAMENT-FRONTEND-X0.md).

| # | Original recommendation and rationale | Disposition | Decision / remaining boundary |
| --- | --- | --- | --- |
| 1 | Validate through parsing, binding and capability admission; F2/F5/F10/F18/F22 contradicted builds | Fixed in X0, bounded | Generic mechanical validation invokes existing CompileSource; static/external-pack analysis is explicitly ParsedNotBuilt. Specialized domains and source consumption remain unqualified. |
| 2 | Every V2 error stops compilation; no fatal-code allowlist or accidental V1 fallback | Fixed in X0 for fallback; X1 unified parser for profile admission | Shared severity adapter defaults legacy diagnostic strings to errors; explicit traces/information and the existing tolerance warning remain nonfatal. The attempted universal gate exposed legitimate junction/derived-profile admission missing from the generic parser and was backed out; this remains an explicit gap. Serialization-shaped roots remain with the legacy reader even when data values are V2 keywords; that reader must prove explicit version 1. |
| 3 | Unknown statements/fields must error; never silently omit Fillet | X1 unified parser, partially fixed in X0 | Canonical root/Modify block checks use lexer boundaries; Box fields reuse generated metadata. Advanced Compose and erased boundary fields still need consumption receipts. |
| 4 | Every error has a sentence, span and consistent JSON; preserve SemanticSourceNotFound text | X1 unified parser, partially fixed in X0 | Generic build/report share an object shape and original token spans where identifiable. Enclosing locations disclose lost provenance. Legacy/domain producers still need typed located diagnostics. |
| 5 | Reject ignored frames such as On XZ | Fixed in X0, bounded | Profile layout frame resolution rejects unsupported XZ and unresolved Using frames. Asymmetric bounds witness retained. This does not qualify every attachment/frame owner. |
| 6 | Correct Model summary and suggest through-cut vocabulary for Pocket | Fixed in X0 | Document owner is projected from existing semantic parse metadata; Pocket through-depth error suggests Remove with Role: ThroughCut. |
| 7 | Parse once into an AST over Lex, migrate construct families in shadow mode | X1 unified parser | Route metadata now comes from actual branch selection. No AST or new route architecture added. |
| 8 | Replace text rewrites with AST transforms; lexical Pattern substitution leaks scope | X1 unified parser | First retain consumed spans and expansion origin receipts; then migrate transforms in shadow mode. |
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
| 22 | Explain route, expansion and bound Pattern instances | Fixed in X0, bounded; X1 unified parser for full expansion | --explain prints actual selected route, admission level and readily available template/solid counts. Complete expanded-source/source-map explanation deferred. |
| 23 | Generate a grammar reference from the AST | X1 unified parser | Existing opt-in semantic field metadata is not a complete grammar; do not present it as one. |
| 24 | Fillet+Hole, box shell/hollow, non-XY Compose capability gaps | Separate geometry/kernel feature | Improve rejection and preserve existing admitted frame mechanisms. No new kernel feature or general plane capability added. |

The September [profile-pipeline audit](firmament-v2-language-audit.md) and earlier
[reconciliation register](firmament-language-inconsistencies.md) are historical
evidence, not proof that October's route-dependent behavior is resolved. Their
previous resolved classifications must be read with this qualification.
