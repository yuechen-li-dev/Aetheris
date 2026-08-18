# Firmament V2 casing

Firmament generally uses PascalCase for semantic constructs and camelCase for ordinary data slots. Casing is a readability convention, not a semantic ceremony. The parser may accept legacy casing where it is unambiguous, and no style-only warnings are emitted. New V2 language vocabulary does not use snake_case; imported names, user identifiers, and engineering designations such as `5052_H32` retain their source spelling.

The bounded FEA-PROD-X1 audit used three decisions: **canonicalize** first-party output, **allow both** for low-cost migration aliases, and **doesn't matter** for identifiers whose spelling carries no language meaning.

| Token/family | Category | Existing spellings | Canonical spelling | Decision | Parser policy |
| --- | --- | --- | --- | --- | --- |
| `Model`, `Template`, `Record`, `Static`, `Profile`, `Selection` | declaration | primarily PascalCase | PascalCase | canonicalize | CanonicalOnly in the central V2 grammar |
| `Analysis` | semantic declaration | `analysis`, `Analysis` | `Analysis` | allow both | CanonicalPreferredAcceptAliases |
| `LinearElastic` | analysis/type value | `LinearElastic` | `LinearElastic` | canonicalize | CasePermissive inside analysis declarations |
| `Fixed`, `Force`, `Traction`, `Pressure` | analysis semantic construct | lowercase and PascalCase | PascalCase | allow both | CanonicalPreferredAcceptAliases |
| `InlineStep` declaration | imported-body declaration | `InlineStep` | `InlineStep` | canonicalize | CanonicalOnly in the central V2 grammar |
| `inlineSTEP(path)` analysis expression | built-in compatibility function | `inlineSTEP`, `InlineStep` | `inlineSTEP` | allow both | CanonicalPreferredAcceptAliases |
| `Recognize`, `Replace`, `Modify`, `Match`, `Require`, `Assert`, `Pmi` | semantic/DSL construct | PascalCase in canonical grammar; some legacy lowercase routes | PascalCase | canonicalize | CanonicalOnly where ambiguity or route selection matters |
| `body`, `bodyResource`, `material`, `region`, `components`, `vector`, `results`, `lattice` | ordinary property | camelCase | camelCase | canonicalize documentation | CasePermissive in the analysis block |
| `Displacement`, `Strain`, `Stress`, `ReactionForce`; `X`, `Y`, `Z` | enum-like value | PascalCase/uppercase | shown spelling | allow both | CasePermissive in analysis lists |
| `face(...)`, user function/member names | member/function-like | camelCase or authored identifier | camelCase for first-party members | doesn't matter for user members | identifier semantics apply |
| model, body, region, constraint, load names | user identifier | authored spelling, including underscores | author choice | doesn't matter | case-sensitive identity |
| STEP entity references, recognized external names, material designation tokens | external/imported identifier | source spelling such as `#141`, `5052_H32` | source spelling | doesn't matter | exact external identity |

This is deliberately not a global case-insensitive rewrite. The central V2 grammar retains canonical-only spellings where token case participates in deterministic route selection. The analysis compiler accepts the harmless legacy aliases listed above so existing M5 files remain viable while first-party FEA fixtures use the preferred form.
