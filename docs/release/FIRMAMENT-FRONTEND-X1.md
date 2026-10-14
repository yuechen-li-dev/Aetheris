# FIRMAMENT-FRONTEND-X1 — unified structural parser, shadow qualification

Historical foundation report: subsequent [production mechanical migration](FIRMAMENT-MECHANICAL-FRONTEND-MIGRATION.md) supersedes the production-status statements below.

Date: 2026-10-14. Verdict: **Meaningful progression; not Accepted.**

Original source now has a lexer-driven, typed structural representation before
production expansion can erase intent. The shadow tooling exposes the omitted
Compose operation, preserves the unknown boundary field, and separates structural
parsing from actual production validation. No construct family has migrated.
Remaining admitted syntax and incomplete semantic/source-consumption comparison
prevent an X1 acceptance claim.

## Inputs and ownership audit

Read the [October authoring audit](../development/firmament-authoring-pass-2026-10.md),
[September profile audit](../development/firmament-v2-language-audit.md),
[reconciliation register](../development/firmament-language-inconsistencies.md),
[X0 qualification](FIRMAMENT-FRONTEND-X0.md),
[24-recommendation decision register](../development/FIRMAMENT-LANGUAGE-DECISIONS-2026-10.md),
public syntax and the active grammar inventory. The historical 281-file audit is
not the current corpus size.

The inspected production chain remains: source spelling/import resolution,
ClosedBoundary2 expansion, Template specialization, Feature expansion, boundary
expansion again, static/Pattern expansion, then whole-document parser selection.
`FirmamentV2Parser.ParseUncached` still selects Panel, CanonicalAdvanced,
Canonical, PrimitiveConstructionPolicy, RoundedBox, StandaloneProfile,
ConceptModeling, Phase3 or LegacyLowercase. Serialization-shaped inputs retain
their X0 compatibility boundary. The existing cache remains production-only.

`ClosedBoundary2Authoring` erases boundaries into Profile text. Template IR retains
typed parameters but carries body text; Feature expansion retains return/local
construction text; `CanonicalStaticAuthoring` and Concept IR expand finite patterns
through source substitution. Generic document AIR is a lossy post-expansion
projection. It cannot prove original field consumption or recover every scope.

`ProfileAuthoringParser`/`ClosedBoundary2Authoring`, prismatic composition and the
existing Mechanical, SheetMetal, WireForm, Surfacing, Assembly, Scene and PMI owners
remain the engineering authorities. Formatter and language-service paths still
use the original lexer and production diagnostic owners. No Helios validator or
new domain dispatch was introduced.

## Reproduction evidence

The first two cases were replayed through the repository CLI before implementation,
producing successful STEP builds. They were replayed after the changes as well.

| Source | Current production behavior | Shadow evidence / disposition |
| --- | --- | --- |
| FrontendX0/unknown-compose-statement | Validate/build succeed; CanonicalAdvanced omits Fillet LostIntent | Typed declaration and original span survive. Compose operation receipt comparison identifies LostIntent as absent. Existing parser defect (B), not repaired in production. |
| FrontendX0/unknown-boundary-field | Validate/build succeed despite Rect2 heigth | AST retains the field, its literal and original token span. Field comparison explicitly remains unqualified; no duplicated field schema was invented. Existing parser defect (B). |
| CanonicalGeometry/profile-chamfer-reflex-junction-top | Generic CanonicalAdvanced parse/validate reject; actual build succeeds | Member-set selector Bracket.Outer.[Inner, Upright] is represented structurally. Existing admission defect (B); lowerer semantics must govern migration. |
| ProfileComposition/valid/semantic-split-compose-chamfer | Generic LegacyLowercase parse and normal build both reject in this checkout; build reports EdgeFinishProfileComposeBoundaryUnsupported | Original profiles, aliases and repeated Source fields parse structurally. Historical acceptance must not be inferred from the directory name or X0 report. |
| Added Static/Slot2/Pattern/Profile siblings | Old document route remains production metadata, independent of shadow | Metamorphic tests compare every original body statement, excluding shifted spans; existing AST interpretation is unchanged. This proves structural invariance only. |
| FrontendX1/slot-route-perturbation | Hole plate builds on Canonical after removing the Slot2 sibling; including the correctly spelled unused Slot2 selects CanonicalAdvanced and rejects the build | Both sources parse structurally with identical original subtrees. Concrete route-dependent admission defect (B) remains in production. |
| Unsupported-XZ L bracket | Normal build rejects as established in X0 | AST retains literal XZ rather than substituting XY. Existing X0 asymmetric world-bounds regression remains active. |

Fixtures were preserved; no accepted source was edited to fit the new grammar.
The original twelve complete authoring programs were not supplied by the October
audit. X0 reconstructed witnesses remain identified as such; no X4 retest claim.

## Architecture and AST

New code lives under `FirmamentV2/Syntax/`:

- `FirmamentSyntax`: compact immutable record model for declarations, contextual
  prefixes, type arguments, parameters/defaults, annotations, clauses, fields,
  directives, keyed mappings, occurrences and nested bodies.
- Expressions have typed literal, quantity, reference, member/member-set, index,
  call, specialization, sequence, object, immutable-with, unary/binary, frame,
  trace-endpoint and alias nodes. No body is an opaque source-text payload.
- `FirmamentSyntaxParser`: recursive descent statements/types and a precedence
  parser for expressions. Every recognized scope closes explicitly. Unconsumed
  tokens are parsed into nodes or produce a fatal located diagnostic. Recovery
  synchronizes at separators, lines and scope boundaries; both ordinary and
  occurrence loops guarantee progress. Nesting/expression chains are bounded.
- `FirmamentSyntaxEvidence`: normalized structural fingerprints and a deliberately
  partial comparison with production AIR. It performs literal-mm comparison only,
  not expression evaluation or engineering admission.

AST type names are open authored identifiers; node categories and operators are
typed. A syntactically shaped Fillet declaration is retained for a semantic owner
to reject. `Parsed` always means **StructuralOnlyUnbound**. It does not certify
names, types, dimensional legality, capability, placement, expansion or geometry.
Unknown fields/statements are not thereby admitted for execution.

Source offsets use original UTF-16 positions, matching existing editor spans.
Every node carries a span; the document retains all tokens, whitespace, comments
and EOF. Concatenating token text reproduces the original source exactly. No
spelling normalization or production expansion runs inside the structural parser.
There is no generated-source location to fabricate: this AST is original source.

The sole existing `FirmamentLanguageAnalysisService.Lex` scanner gained a lossless
view and unterminated token evidence. Its normal view still omits trivia/EOF as
before. Numeric scanning now distinguishes fractions, exponents and member dots;
domain symbols such as StainlessSteel.304_Annealed remain identifiers after a
member delimiter. No second lexer, global case normalizer, regex, source macro
pass or whole-document parser dispatch was added.

## Compatibility, expressions and remaining syntax

Existing casing and root spellings are preserved in the tree. No point constructor,
cut vocabulary, optional-root or generic-variant semantics changed. Adjacent angle
suffixes and explicitly named/empty spaced generic applications are represented;
other comparison/generic-spacing ambiguities remain a decision, not a new language
promise. Expression nodes retain precedence without evaluating engineering values.

Template/Feature definitions, defaults, nested generics, keyed Set/Pattern mappings,
radial/linear/mirror declarations, finite template invocations and XML-shaped
Part/Assembly bodies have structural coverage. Imported declarations are parsed in
their own inventoried files; this parser does not specialize or resolve includes.
There is no AST-to-AIR adapter disguised as a call back into regex syntax parsing.

Important outstanding forms include Interface Role/requires and Mate relations,
imported STEP #face selectors, specialized Blind/CenteredAt value shorthand,
Replace/Extend headers and drawing-specific metadata. Canonical Sculpting fixtures
with literal leading plus signs require an owner decision. These are located
shadow failures; the corpus does not hide them. Finishing these forms is bounded
X1 work. Unifying evaluators and scoped expansion remains X2.

## Diagnostics and developer tooling

Syntax errors reuse `FirmamentV2ValidationDiagnostic`: stable code, fatal severity,
sentence, original file/span, line/column, enclosing context where available and
stage Syntax. New codes are `firmament-syntax-statement`, `-expression`,
`-identifier`, `-expected-token`, `-declaration-tail`, `-tag-mismatch`, `-depth`
and `-unterminated-token`. Warnings were not globally promoted.

`frontend-shadow file --json` provides summary, structural outcome, actual generic
production route/diagnostics and partial comparison. `--ast` adds the full tree
only when requested. `--validate` invokes the unchanged normal validation command
and includes its separate JSON result/exit code. `validate file --shadow --explain
--json` exposes the same developer envelope. Ordinary validate/build output does
not run or include shadow analysis. Shadow failure returns failure on an individual
file even when the production projection accepted it.

Comparison checks original Model identity, unambiguous primitive name/type,
literal-mm Box dimensions and direct Compose operation receipts. Unmatched fields
and declarations are counted explicitly. It does **not** claim complete semantic
or geometry parity. Most legacy AIR fields/nesting/references no longer have the
original syntax needed for a trustworthy full differential. Diagnostics and source
spans from the production projection remain separately visible. A-E dispositions
for investigated cases are in the decision register; other machine differences
remain Unclassified rather than receiving invented automated explanations.

## Corpus, tests and geometry

`scripts/qualify-firmament-frontend-x1.ps1` inventories every `.firmament`,
`.firmfixture` and `.firmasm` under fixtures, compares original syntax against the
generic production projection and writes machine JSON/Markdown under ignored
`artifacts/local/frontend-x1/corpus`. Compatibility, invalid and speculative files
are included. Collection exit zero means the report completed, not qualification;
`qualified: false` and `semanticParityQualified: false` are explicit.

Final counts, benchmark samples, test totals and geometry hash comparisons are in
the adjacent compact [evidence record](FIRMAMENT-FRONTEND-X1.evidence.json). Raw
diagnostics and per-file outcomes remain in the ignored report. The required
canonical showcase sources and their included modules are part of that inventory;
no failure list was silently excluded to improve a percentage.

Final collection: **1,232 sources, 741 structurally parsed, 491 rejected**;
Canonical contains 323 source files including legacy `.firmasm`, with 300 parsed
and 23 rejected. There are 57 generic-production-accepted/shadow-rejected files.
These are coverage gaps, not an authoring success rate.

Final validation: Release build succeeded with zero errors and seven existing
warnings; 27 parser tests, three CLI tests and 1,068 fast Core tests passed.
The normal full solution lane passed **4,652 tests across 20 projects**, zero
failed/skipped. All ten real showcase CLI checks passed. Both same-checkout
pre-X1 intent-loss STEP hashes and all five historical X0 showcase STEP hashes
match. The normal CLI hole-plate control also builds successfully after removing
only the unused Slot2 AST span; the source containing Slot2 rejects.

Focused coverage includes lossless lexing, nested generics/defaults, precedence,
member/index/with/frame/trace syntax, original source diagnostics, error recovery,
depth limits, occurrence progress, retained unknown intent, structural sibling/root
invariance, literal-dimension differential detection, unsupported-XZ preservation,
CLI JSON shape and separate production/shadow outcomes. Production geometry is
exercised through existing lowerers; no new geometry engine is involved.

The closeout full run initially caught a missing linked evidence file in this
report. The file was added, and the final full regression qualification was rerun
with it present. This was a documentation packaging error, not a kernel failure.

Reproduce with the repository SDK/CLI:

```powershell
dotnet build Aetheris.slnx -c Release -m:1
./scripts/qualify-firmament-frontend-x1.ps1
dotnet test Aetheris.Kernel.Firmament.Tests -c Release --no-build --filter FullyQualifiedName~FirmamentFrontendX1Tests
dotnet test Aetheris.CLI.Tests -c Release --no-build --filter FullyQualifiedName~FirmamentFrontendX1CliTests
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
```

## Performance and code-quality pass

`--benchmark` measures twenty warm parses after warm-up, with current-thread
allocated bytes. IO, production parsing, comparison, serialization and geometry
are excluded. The samples cover a Box, ordinary Compose part, nested Template/PMI,
guitar entry source, house entry source and largest current Firmament fixture
(FactoryX0 kit). These are per-file syntax measurements, not transitive whole
guitar/scene compilation or interactive-session throughput. No cache/session
redesign was introduced. Raw samples and exact sizes remain in evidence.

The review found and fixed an occurrence recovery progress hole, corrected frame
vector/body spans, preserved repeated fields, and made comparison coverage explicit.
New parsing uses no regex, no source slicing for grammar reconstruction, no mutable
global parser state, no field schema copy and no fatal-code allowlist. JSON options
are read-only configuration. No unrelated formatting or domain lowerer edits were
made. Native Helios interaction and remote CI are not claimed; existing diagnostic
consumers and all production authorities remain in place.

## Migration gates, prerequisites and verdict

No migration gate is waived. AST coverage and diagnostics have focused evidence;
complete structural/semantic parity, binding, original field consumption, expansion
provenance and family-specific AP242 parity are not established. Accepted geometry
must continue through existing owners until all gates pass.

The smallest next steps are:

1. Complete the named Interface/domain syntax gaps and classify every admitted
   source rejected by shadow. Preserve invalid/legacy classifications separately.
2. Build explicit typed adapters for a bounded family, projecting AST fields and
   references into its existing binder/lowerer with consumption receipts. Do not
   gate profiles on the lossy generic document parse.
3. Extend differential evidence to full scoped fields/references/expressions and
   actual domain admission. Then qualify geometry/STEP before any route retirement.
4. X2 may consolidate binding, dimension evaluation and finite scoped expansion
   once these syntax receipts exist. X3 syntax choices and X4 authoring retest
   remain separate milestones.

**Meaningful progression:** original-source structural loss is no longer a blocker
to inspecting and testing intent in shadow mode. Production honesty gaps remain;
the next blockers are explicit missing syntax forms and missing semantic/source
consumption adapters. X1 is not Accepted, and no production cutover is justified.
