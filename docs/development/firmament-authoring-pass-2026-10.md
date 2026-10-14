# Firmament V2: authoring pass and language tuning list

Date: 2026-10-13. Method: Claude wrote twelve ordinary parts from a one-line engineering
brief each, using only `docs/public/firmament`. It ran `aetheris validate --json` and
`aetheris build`, checked the STEP bounds and face counts, and then read the parser to
find the cause of every failure. The parser is the source of truth for this document.
Earlier audits ([language audit](firmament-v2-language-audit.md), which covers profile
pipelines, and the [reconciliation register](firmament-language-inconsistencies.md))
are referenced where this pass contradicts them.

## 1. Scoreboard

| # | Part | First try | Outcome |
|---|---|---|---|
| 1 | Plate with holes and a fillet | ✗ | Built after 10 variants; the fillet could not be combined with holes |
| 2 | L-bracket from a point outline | ✗ | Built, **in the wrong plane** (`On XZ` silently ignored) |
| 3 | Revolved bushing | ✗ | `schema Revolve` is documented but does not exist; worked under `Mechanical` |
| 4 | Spacer family (Record + Template) | ✓ | |
| 5 | Plate with a 3×2 hole grid | ✗ | Worked only as a bare `Struct`; wrapping it in `Model` breaks it |
| 6 | Enclosure tray (Compose + Pocket) | ✓ | |
| 7 | Two-part assembly with datum ports | ✓ | |
| 8 | Spring and hook (WireForm) | ✓ | |
| 9 | NEMA17 plate (Static + Feature + Pattern) | ✗ | Worked only inside `Model` and only with literal coordinates; the opposite of #5 |
| 10 | Flange with a radial bolt circle | ✓ | |
| 11 | Bent tube (Sweep) | ✓ | |
| 12 | Slot block: slot, counterbores, chamfer | ✗ | Built after 9 variants |

6 of 12 parts built on the first try, and 11 of 12 built correct geometry in the end.
**The failures are not spread evenly.** Every domain with a single owner worked on the
first try: WireForm, Sweep, Revolve, Record/Template, Assembly and radial Pattern. Every
failure is in the general mechanical core: Box/Hole/Modify/Compose/Pattern/EdgeFinish.
Those are the constructs a new user writes first, and the core is where several
parsers overlap.

## 2. Why: what the parser does

`FirmamentV2Parser.ParseUncached` is not a grammar. It works like this:

1. **Five text-to-text rewrites** run before anything is parsed: spelling
   normalization, template imports, `ClosedBoundary2Authoring` (which runs twice),
   Template expansion, Feature expansion, and `CanonicalStaticAuthoring`. Each one is a
   regex pass over the source string.
2. **A whole-document regex dispatch** then picks one of nine document parsers:
   Panel, CanonicalAdvanced, Canonical, PrimitiveConstructionPolicy, RoundedBox,
   StandaloneProfile, ConceptModeling, Phase3, and the legacy lowercase route
   (`FirmamentV2Parser.cs:419-474`). The choice depends on *which other constructs
   appear anywhere in the file*.
3. The front end has **869 `Regex` call sites** (323 in `FirmamentV2Parser.cs` alone).
   A real lexer already exists (`FirmamentLanguageAnalysisService.Lex`, used by the
   spelling normalizer and the formatter), but the parser does not use it.

Most of the findings follow directly from this design:

- **The same statement means different things on different routes.**
  - `Hole<Shaft> { On: +Z; Center: Point2(0mm,0mm) ... }` builds under `Model` (#9).
  - Under a top-level `Struct`, the ConceptModeling route rejects `On: +Z` with
    "only planar +Z placement is admitted" (`ConceptIr.cs:428-434`). That route admits
    only `face(+Z)` or `Body.Top`. It then rejects `Point2(...)`, because it admits only
    `Center: Inst.Member[i]` (`ConceptIr.cs:437`).
  - Adding a `Slot2`, a top-level `Static`, or a grid Pattern inside `Model` moves the
    whole file to the Phase3 route. That route reports `edge-finish-syntax-invalid`
    for files that contain no EdgeFinish (#1, #5, #12).
- **Macro semantics leak.**
  - The concept-route Pattern substitutes the item name textually:
    `Regex.Replace(featureBody, \bItem\b, Source[i])` (`ConceptIr.cs:493`). That is why
    the documented form is `Hole<Shaft> Item { Center: Item }`, where `Item` is both the
    instance name and the loop variable.
  - Arithmetic is legal only in fields that some rewrite stage happens to evaluate.
    `Point2(spacing / 2, ...)` in a `Set<Point2>` is a type mismatch (#9). Boundary
    fields accept `Spec.Width / 2`, but only after Template specialization
    (`boundaries.md`).
- **Casing is a lookup table per owner** (`FirmamentSourceSpelling.Fields`). Lowercase
  `on:` therefore works on Hole, while `face:` fails on EdgeFinish (#1).
- **"Fatal" is an allowlist.** The build fails on a V2 diagnostic only if
  `IsFatalDiagnosticCode` lists it. That is a single expression with 26 prefix checks
  and 160 exact codes (`FirmamentV2Parser.cs:2349`, used at
  `FirmamentBuildAndExport.cs:647`). Any other V2
  failure falls through toward V1, and the user sees "must be valid canonical
  TOON-style text or JSON" for a file that was never V1 (#1).

## 3. Recommendations, ranked

Each item names the findings it fixes. Findings F1–F23 are listed in the appendix.

### P0: the toolchain must not lie (small, high value; do first)

1. **`validate` returns `valid` only when parse *and* bind succeed.** Today it says
   `valid` in all of these cases:
   - alongside `parse-failed` (F2, F18);
   - before a build that fails at capability checks (F5);
   - before a build that fails at selection binding (F22);
   - for a file the build routes differently (F10).

   Run the same front end, through semantic binding, in both commands, or report a
   distinct `parsed-not-bound` status.
2. **Every V2 failure is fatal.** Invert `IsFatalDiagnosticCode`: once a document is
   recognized as V2, any error stops the build. Reach V1 only through an explicit
   version header. The code comment above the fallback already states that intent.
   Fixes F2 and F6.
3. **Unknown statements and unknown fields are errors.** `Fillet corners { ... }` in a
   Model is silently dropped, and the part builds without the fillet (F3). Give every
   block a closed set of statements and fields, and report the nearest valid spelling.
4. **Every diagnostic has a span and a sentence, and the CLI prints both.**
   - `canonical-primitive-malformed` has no field or line (F1).
   - `SemanticSourceNotFound` drops its message ("No provenance source for: …") (F22).
   - JSON diagnostics are objects in some cases and bare strings in others (F14).
5. **No silent frame or placement drops.** `Concept Struct … On XZ` built in XY (F9).
   If a frame is unsupported on a route, fail.
6. **Small fixes:**
   - The build summary should name the Model, not the last feature (F8).
   - A pocket at through depth should suggest the through-cut spelling (F21).

### P1: one grammar (the structural fix)

7. **Parse once, into one AST, with the existing lexer.** Routes stay, but as lowering
   choices per body, not as document parsers chosen by regex. How to get there without
   breaking anything:
   1. Make the current route explicit: `validate --json` reports `route`, and any
      diagnostic that depends on the route names it.
   2. Write a recursive-descent parser over `Lex` that produces today's
      `FirmamentV2Document`. Run it in shadow mode against the 281-file corpus that the
      [language audit](firmament-v2-language-audit.md) already uses as a parse-identity
      oracle.
   3. Switch over construct family by construct family once parse identity holds.

   This removes route-dependent meaning (F6, F10, F12, F16, F19, F20) and produces
   spans for free (item 4).
8. **Turn the expansion stages into AST transforms.** Template, Feature, Static,
   ClosedBoundary and Pattern currently rewrite text. As tree transforms, binding is
   by scope, not by `\bName\b` replacement (F17). The two `ClosedBoundary2Authoring`
   passes also collapse into one.
9. **One expression evaluator, everywhere a value is expected.**
   - Length, angle and Point2 arithmetic should work in Set entries, Hole fields,
     Static initializers and Model scope (F1, F18).
   - The evaluator already exists for profiles and boundaries; make it the only one.

### P2: cut or merge (language surface)

10. **One point literal.** Hole `Center` accepts `Point2(x,y)` in Modify, `[x,y]` in
    Compose, and only `Inst.Member[i]` on the concept route (F23). Pick `[x, y]` (it is
    shorter and already used by every boundary type), and keep `Point2(...)` as the
    typed constructor that means the same thing.
11. **One variant mechanism.**
    - Holes use a generic argument (`Hole<Counterbore>`).
    - EdgeFinish uses a field (`Kind: Chamfer`), and `EdgeFinish<Chamfer>` validates
      but fails at build (F22).
    - Polygons use both (`Polygon2<Rhombus>` next to `Slot2` and `RegularPolygon2<6>`).

    Rule: a type variant is a generic argument. Make it `EdgeFinish<Chamfer|Fillet>`
    and keep `Kind:` as an alias.
12. **One Pattern construct.** Five spellings exist today:
    - `Pattern X Over Records { Template<Current> }`
    - `Pattern X { Source: S; Hole<Shaft> Item {...} }`
    - `Pattern X Over Set { p => F(p) }`
    - `Radial Pattern X { Source; About; Count }`
    - `Mirrored Feature X From Y { Across }`

    Proposal: `Pattern Name over <source> { item => <feature expression> }`, where
    `<source>` is a Set, a Table, `Grid(...)`, `Radial(axis, count, angle)` or
    `Mirror(plane)`. Keep Mirror as sugar if it reads better. Fixes F15 and F17.
13. **One cut vocabulary in Compose.**
    - `Boss`/`Pocket` and `Add`/`Remove` cover the same intents (2 canonical fixtures
      use Add/Remove).
    - A through-slot needs `Remove { ...; Role: ThroughCut }` (F21), while Hole uses
      `End: ThroughAll`.

    Give Pocket `End: ThroughAll | Depth: d`, matching Hole, and make Add/Remove
    aliases. The [register](firmament-language-inconsistencies.md) classifies this as
    "RetainedCompatibility", but in practice Remove is the *only* through-cut.
14. **One document root rule.** Today `Model` is required by some constructs and
    forbidden by others (F16 versus #9). Either `Model` is always optional, with
    top-level declarations forming an implicit model, or it is always required.
15. **Drop documented constructs that do not exist.**
    - Remove `schema Revolve` from `syntax.md`, or add it (F13).
    - Remove the `Feature` + `Pattern` example that only works in one nesting (F15).
16. **One separator rule.** Docs mix `{ A: 1; B: 2 }` and `{ A: 1 B: 2 }`, and some
    grammars (Function, Points, PMI, assemblies) require `;`. Make both a newline and
    `;` separate statements everywhere.
17. **One casing rule, applied in the lexer.**
    - Case-fold field labels globally, or not at all. Never per construct (F4).
    - The style guide (prefers camelCase) and `syntax.md` (says PascalCase is
      canonical) contradict each other. Pick one.
    - Also fix `Pmi` versus `PMI`.
18. **Retire one-off constructs, or justify them.** Usage across
    `fixtures/Canonical`, the showcase projects included, by number of files:

    | Construct | Files |
    |---|---|
    | `RoundedBox` | 1 |
    | `Plateau` | 1 |
    | `Polygon2<Parallelogram>` | 1 |
    | `Polygon2<Trapezoid>` | 1 |
    | `Polygon2<Rhombus>` | 2 |
    | `SmoothRoundedRect2` | 2 |
    | `Span` | 2 |
    | `Slot` (feature) | 2 |
    | `Add` / `Remove` | 2 / 2 |
    | `Match` | 0 (5 regression only) |

    Each of these brings its own route, admission regex and alias table. Candidates:
    - Fold the Polygon2 variants into `Polygon2<Explicit>` plus helper Features.
    - Fold `RoundedBox` into `Box` + `EdgeFinish`.
    - Keep the ones a showcase project needs, and make them library Features rather
      than grammar.

### P3: add

19. **Local named values in any scope**, with arithmetic: `Let pitch = 31mm` inside
    `Model`/`Struct` (F1). Today, parameterizing a plate requires Record + Static +
    Template.
20. **Inline vertex lists.**
    - `Polygon2 Outline { Points: [[0,0],[60,0],[60,5],...] }` (F11). A 6-vertex
      outline currently needs 7 constructs.
    - `Revolve` from a point list (F13). Today it needs a `Point2` + `Line2` per vertex.
21. **Uniform face references.** `+Z`, `face(+Z)`, `Body.Top` and `Top` should all be
    accepted wherever a planar face is expected (F19).
22. **`validate --explain`**: print the route, the lowered source after expansion, and
    the resolved Pattern instances. This helps a model and a human equally, and makes
    item 7 easy to test.
23. **A generated grammar reference** (EBNF, or a table produced from the AST), so the
    documentation cannot drift from the parser.
24. **Capability gaps, not grammar** (kernel work, listed for completeness):
    - fillet combined with holes in one Modify (F5);
    - shell/hollow for a box;
    - frames other than XY for Compose (F9).

### Suggested order

1. P0 items 1–6: a few days, no language change. They turn every silent or misleading
   case found in this pass into a precise error.
2. Items 22, 10, 11 and 15: cheap, and they make the language learnable.
3. Item 7 in shadow mode, then 8 and 9 on the new AST.
4. Items 12–14 and 16–18 once there is one parser, since they become edits to one
   grammar.
5. P3 additions.

## Appendix A: findings

| # | Finding |
|---|---|
| F1 | `let`/`Static` constants inside `Model` → `canonical-primitive-malformed`, with no field or line. |
| F2 | `validate` says `valid` alongside `parse-failed`; `build` then reports a V1 TOON error. |
| F3 | Unknown statement `Fillet corners {...}` is silently ignored. |
| F4 | Lowercase fields are accepted on Hole but rejected on EdgeFinish. |
| F5 | Fillet + Hole in one Modify: validate says valid, build fails ("CombinedHoleEdgeFinish requires Chamfer"). |
| F6 | A top-level `Static` before `Model` → Phase3 route → `edge-finish-syntax-invalid` with no EdgeFinish in the file. |
| F7 | EdgeFinish has two selector grammars: `Face: +Z; Target: Boundary` and `Target: Profile.Outer; On: Top`. |
| F8 | The build summary "Model:" names the last feature or profile. |
| F9 | `Concept Struct … On XZ` is ignored; the part is built in XY. |
| F10 | `Extrude` at Model scope: unknown to validate, built through another route by build. |
| F11 | `Segment` endpoints must be named Point2s; there is no inline-coordinate polyline. |
| F12 | Box Z placement depends on the route (documented in geometry.md). |
| F13 | `schema Revolve` is documented but unknown; Revolve needs a Point2 + Line2 per vertex. |
| F14 | `validate --json` diagnostics are objects or bare strings, depending on the failure. |
| F15 | The syntax.md Feature + Pattern example is not admitted at Model scope. |
| F16 | The same body fails when wrapped in `Model` and works as a bare `Struct` (#5); the reverse holds for #9. |
| F17 | `Item` is both the Pattern instance name and the loop variable (textual substitution). |
| F18 | `Point2(spacing / 2, …)` in `Set<Point2>` → `static-set-entry-type-mismatch`. |
| F19 | Concept route: `On: +Z` is rejected as "only +Z admitted"; `Center` must be `Inst.Member[i]`. |
| F20 | `Slot2` at Model scope moves the file to the Phase3 route. |
| F21 | There is no through-Pocket; a through-cut is `Remove {…; Role: ThroughCut}`, with no hint. |
| F22 | `EdgeFinish<Chamfer>` validates, then fails at build with `SemanticSourceNotFound`, message dropped. |
| F23 | Hole `Center`: `Point2(x,y)`, `[x,y]` or `Inst.Member[i]`, depending on the route. |

## Appendix B: forms that worked first try

These are the shapes the corpus should hold up as canonical, and the shapes a
recursive-descent grammar should preserve exactly:

- WireForm `Straight`/`Helix`/`Bend`.
- `Concept Path` + `Sweep`.
- `Axis` + `Revolve { Profile; About; Angle: full }`.
- `Record` + `Static` + `Template<Spec: T> Struct` + `Struct Part = T<Spec: S>`.
- Assembly with `expose { Semantic … DatumFrame … }` and `Interface<Fixed>`.
- `Model` + `Box` + `Modify { Hole<Shaft> …; Radial Pattern … }`.
- Compose `Base` + `Pocket` with `RoundedRect2` profiles.
