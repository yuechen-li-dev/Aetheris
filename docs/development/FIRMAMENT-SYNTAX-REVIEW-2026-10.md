# Firmament syntax review — 2026-10-14

Concept/2D follow-up:
[implementation and qualification](../release/FIRMAMENT-CONCEPTS-AND-2D.md)
reconciles S16's vocabulary in production. Concept names a requirement: a
structural contract, scalar Boolean predicate or labeled requirement bundle.
Concept Struct supplies non-materialized construction. Plain shape names are
preferred; the compatibility Concept prefix shares closed-shape binding rather
than selecting a different interpretation. Definitions, conformance claims and
applied checks are distinguished in the rewritten syntax reference. Unconsumed
guides still bind, and explicit boundary rotation can no longer disappear.
Record/shape predicate parameters, namespaces and complete AST expansion remain
separate semantic/frontend work. Historical findings below are preserved.

Compile-time binding follow-up:
[implementation and qualification](../release/FIRMAMENT-COMPILE-TIME-VALUES.md)
extends S17 with typed Record members, Template/Feature inputs, defaults and
aliases sharing the scalar binder and retaining requirements/provenance. It
also repairs Template value substitution of labels and shadowed Feature
parameters. This does not close every expression position, numeric-policy
seam, Pattern binder or domain grammar listed below.

Implementation follow-up: the [directive/casing pass](../release/FIRMAMENT-DIRECTIVES-AND-CASING.md)
repairs S01 (symmetry fields), S02 (Profile/mirror links), S14 and S15.
It also repairs S13's dense mirror/override witness; specialized semicolon rules
elsewhere are unchanged. Original observations below remain pre-repair evidence.

Typed-tolerance follow-up: [implementation and qualification](../release/FIRMAMENT-TYPED-TOLERANCES.md)
adds AST-owned dimensional Static values and explicit native PMI projection.
It clarifies S16/S17's value/interface roles: Semantic publishes typed
engineering interfaces, Path is a non-materialized ordered guide, and
Enum/Match choose while Set/Pattern enumerate. Bare Path and Concept Path now
share the same owner. This does not close the wider expression-position or
domain-grammar differences recorded below.

## Purpose and boundary

This review accompanies the rewrite of [Writing Firmament](../public/firmament/syntax.md).
The question is what a human or agent can infer from the language's surface,
and where that inference differs from current admission. This pass changes
Markdown only. It does not implement a new grammar, extend compatibility,
change geometry or close frontend milestones.

The working model remains: Model is a class-like artifact owner, Struct is
reusable construction, Feature is function-like and Pattern is finite repetition.
These are organizational analogies, not an implementation of runtime OOP.

Evidence is from checkout `d4032b00` and its existing Release CLI assemblies.
The Firmament assembly used for probes was built on October 14 at 14:28 local
time; no rebuild was performed during this documentation-only pass. Source
inspection corroborates the defects below. In-memory probes call the existing
`FirmamentBuildAndExport.CompileSource`; fixture checks use the repository CLI's
normal `validate --json`, not an installed CLI or a shadow parser.

The [original October audit](FIRMAMENT-LANGUAGE-DECISIONS-2026-10.md) and
[earlier inconsistency register](firmament-language-inconsistencies.md) remain
historical evidence. Their resolved/retained labels are not proof that every
current owner shares the same syntax. Earlier reports must not be silently
rewritten as if later repairs had already existed.

## Findings and decisions still needed

“Observed” below means executed in this pass. “Inspected” means checked against
the current source and named guides/tests; it does not claim a new exhaustive
runtime qualification. An open question is not an approved syntax change.

| ID | Area and current behavior | Classification | Decision or next evidence needed |
| --- | --- | --- | --- |
| S01 | camelCase built-in fields work in ordinary Box/Hole authoring, but symmetry still requires `Across`, `Source`, `About`, `Count`, `Angle`. Changing only those labels rejects admitted fixtures. Observed. | Active casing inconsistency. | Apply the established casing policy through the symmetry owner, with alias/geometry parity, rather than tell authors to lowercase unsupported fields. |
| S02 | `using/as/on/over` have lowercase link forms; pipeline endpoint `From/To` and mirror-header `From` remain PascalCase. Inspected. | Incomplete linking-word policy. | Decide which words are structural operators and which are lowercase links. Do not infer `from` support from the field `from:`. |
| S03 | Direct Hole accepts `Point2(0mm, 0mm)` and rejects `[0mm, 0mm]`; Rect2 accepts a list. The composed Hole reader contains both list and Point2 forms. Observed direct pair; inspected composed owner. | Same conceptual value, inconsistent admission. | Choose a point-construction policy with explicit context conversions; keep Point2, Point3 and vector types distinct. The old “Compose only accepts arrays” claim is stale. |
| S04 | Native fields require dimensional lengths; historical primitive source can inherit mm. A Model without `units` rejects; a Struct containing the same dimensional Box builds without it. Observed. | Compatibility plus root-dependent units policy. | Specify the purpose and scope of a root units declaration separately from literal typing. No change to engineering units doctrine is proposed. |
| S05 | Model, material-bearing Struct, wrapper-free construction and library-only documents have different entry rules. A bare Box fails in-memory compilation; the same Box inside Struct builds. Observed pair; other entry forms inspected. | Root/scope contract incomplete. | Define legal source-unit kinds and contexts. “Everything is an optional wrapper” would be false. |
| S06 | Feature definitions, named Feature invocations, finite feature Templates and scalar Functions all resemble functions but have different bodies/results/scopes. Inspected. | Overloaded surface and missing teaching distinction. | Keep semantic geometry recipes distinct from scalar expressions; decide whether the two Template recipe forms need one declaration convention later. |
| S07 | Pattern maps a Set with an explicit binder, maps semantic points with `Source`/`Item`, or maps record arrays through Template/`Current`; radial Pattern is another owner. Placing the beginner Pattern inside Modify builds. | Active repetition inconsistency. | Define one mental model for element binding and scope before changing spellings. `Item` and `Current` are not interchangeable aliases. This pass does not replace expansion. |
| S08 | Product specialization uses `Template<...> Struct Name`; finite feature Templates use `Template<spec: Type> Name`; call-shaped feature Template aliases also remain. Inspected. | Multiple declaration shapes plus compatibility. | Decide which family authors should learn first and how each names its output. Preserve admitted alias intent until replacement is qualified. |
| S09 | `<...>` denotes closed variants (`Hole<Shaft>`), types (`Set<Point2>`), counts (`RegularPolygon2<6>`), Interface families and actual Template arguments. Direct EdgeFinish instead uses `kind: Chamfer`; `EdgeFinish<Chamfer>` rejects. Observed finish pair. | Variant/type/configuration distinction obscured. | Document these meanings now; decide whether feature variants deserve consistent notation later. No universal generic-programming contract exists. |
| S10 | Pocket uses `depth` and must leave a floor; through-cut Remove uses `Role: ThroughCut`; Hole uses `end`; Slot uses `extent` and `role`. Inspected. | Engineering differences with vocabulary friction. | Preserve blind/through semantics while deciding whether termination vocabulary can be shared. Missing geometry combinations are separate from this syntax question. |
| S11 | `on XY`, `on: +Z`, a Plane/Frame and an Assembly attachment are differently typed, context-owned references. Inspected. | Legitimate type distinctions, overloaded placement word. | Publish a consumer-by-reference matrix before promising general plane/frame substitution. The historical ignored-XZ defect is covered by existing tests, not a reason to offer silent defaults. |
| S12 | Primitive EdgeFinish uses face + Boundary target; composed finishing uses a Profile boundary/support. Native semantic faces and imported `#face` references have different identities. Inspected. | Typed selector differences, uneven surface. | Explain selector types and target ownership. Do not merge imported entity IDs with native semantic identities or invent a universal string selector. |
| S13 | Newline/semicolon fields coexist with mandatory semicolons in Function, Points, PMI Note and Assembly provenance grammars. Inspected. A dense Hole seed without field separators fails with mirror + override, while the semicolon-separated version builds. Observed. | Active lexical/termination inconsistency and rewrite sensitivity. | Decide one general separator rule and list unavoidable domain exceptions. Do not make whitespace changes a semantic migration by accident. |
| S14 | `/* ... */` before schema rejects as late; `//` succeeds. A selector followed by a trailing comment, or a selector sharing a line with Model, may be ignored. Observed; exact witnesses below. | Confirmed frontend correctness defect. | Consume schema through the same token boundary as admission, or explicitly reject malformed placement. Unrecognized names must not bypass selection because of formatting. |
| S15 | Both `include` and `Include` of a nonexistent file before a plain mechanical Model compile successfully. Other project/include consumers have fail-closed tests. Observed mechanical case; inspected project tests. | Confirmed ignored intent and inconsistent module contract. | Each directive must be consumed by a real owner or rejected. Define supported mechanical module consumption before advertising universal include. No parallel loader should be added just to hide this gap. |
| S16 | Record is data; Struct is construction; Concept Struct is non-materialized construction; Concept can name a contract or a typed geometric family. Earlier prose grouped Record/Struct together. | Documentation ambiguity plus overloaded vocabulary. | The rewrite separates their roles. Whether Concept declarations should become more visibly distinct remains a language decision. |
| S17 | AST expression syntax does not establish one evaluator in every position. Static values, Feature-local `let`, Template arguments and scalar Functions retain bounded owners. Inspected. | Semantic consolidation still incomplete. | Inventory admitted expression positions/types before promising general local values. No general Let, runtime control flow or new evaluator is introduced here. |
| S18 | `=>` means a Set association, Pattern binder, Require condition or compatibility exposure alias. `with` derives Records, features or historical solids in different contexts. Inspected. | Contextual operator overlap. | Keep a role-specific reference. Decide which overlaps are useful consistency and which hide distinct operations; do not declare global equivalence. |
| S19 | Public schema names are Mechanical, WireForm, Sweep, SectionChain, Garment; these are not all product roots. Garment admits bounded Feature Profile use, contradicting the old page's Mechanical-only claim. | Documentation errors and non-universal domain grammar. | Correct teaching now. Decide separately whether all roots need an explicit document-kind model; do not invent schema names. |
| S20 | The old entry page mixed complete files, rootless fragments, undefined names, an unowned Pattern and migration commentary. Its Polygon2 “Rhombus only” statement and geometry's stock-placement warning were stale. | Documentation defects. | Repaired in this pass; complete examples are compiled, fragments identify prerequisites. A generated whole-language grammar still requires complete owner coverage. |

S01–S19 remain implementation or design matters unless explicitly described as
a documentation correction. This review authorizes no compatibility removal.

## Exact executed witnesses

All probes used the existing normal engineering compilation entry point, not
just syntax acceptance. They return STEP in memory on success. No source fixture
or generated model file was added in this pass.

### S15 — ignored mechanical include

```firmament
include "__missing_syntax_review__.firmament";
Model Example {
  units: mm
  Box stock { size: [30mm, 20mm, 6mm] }
}
```

Observed: success, no diagnostics. `Include` behaves the same. The result was
repeated with the repository root supplied as `sourceDirectory`, so this is not
merely a missing base-directory test. The mechanical scope binder skips
`DirectiveSyntax`; it does not require a consumption receipt for the include.
The normal file export path also passes the source to this admission path.

This finding is bounded to an ordinary mechanical Model. Assembly project
loading, including file-backed Profile dependencies, has missing/cycle checks;
those consumers are not being declared broken by this witness.

### S14 — schema formatting changes admission

Append the same Model above to each prefix:

| Prefix | Observed normal compilation |
| --- | --- |
| `schema DoesNotExist` followed by newline | Rejects: `firmament-schema-unknown:DoesNotExist:available=Mechanical,WireForm,Sweep,SectionChain,Garment`. |
| `schema DoesNotExist ` followed immediately by Model | Builds, no diagnostics. |
| `schema DoesNotExist // selector` followed by newline | Builds, no diagnostics. |
| `schema WireForm` followed by newline | Rejects: `wireform-declaration-missing: No WireForm declaration was found.` |
| `schema WireForm ` followed immediately by Model | Builds, no diagnostics. |
| `/* author comment */`, newline, `schema Mechanical`, newline | Rejects: `firmament-schema-late`. |
| `// author comment`, newline, `schema Mechanical`, newline | Builds. |

The selector matches a whole line before shared parsing. Forms it misses can
become generic directives which the mechanical binder skips. This is an
admission gap, not merely a recommendation to put headers on separate lines.

### S01 — symmetry casing

The existing [radial bolt-circle](../../fixtures/Canonical/Symmetry/radial-bolt-circle.firmament)
and [mirrored-hole](../../fixtures/Canonical/Symmetry/mirrored-hole.firmament)
sources both compile in memory. Change only these labels:

| Change | Observed outcome |
| --- | --- |
| Radial `Source/About/Count/Angle` to `source/about/count/angle` | Rejects: `firmament-symmetry-radial-field-missing:BoltCircle`. |
| Mirror `Across` to `across` | Rejects: `firmament-symmetry-plane-unresolved:RightMount`. |

The mirror error describes resolution rather than the spelling failure. An
alias repair should also improve that diagnostic rather than erase evidence.

### S03/S09 — intuitive substitutions that fail

Inside `Modify stock` of the Model above:

```firmament
Hole<Shaft> bore {
  on: +Z
  center: Point2(0mm, 0mm)
  diameter: 4mm
  end: ThroughAll
}
```

Observed: builds. Change only center to `[0mm, 0mm]`: rejects, including
`firmament-v2-canonical-point2-invalid`.

```firmament
EdgeFinish top {
  face: +Z
  target: Boundary
  kind: Chamfer
  distance: 0.5mm
}
```

Observed: builds. Replace the header with `EdgeFinish<Chamfer> top` and remove
`kind`: rejects as malformed Modify/unknown fields. The intended variant is not
recognized in that position, and the resulting field errors are less helpful
than a direct unsupported-variant diagnostic would be.

### S04/S05 — entry and units

| Source | Observed outcome |
| --- | --- |
| `Model Example { Box stock { size: [30mm,20mm,6mm] } }` | Rejects: `firmament-v2-canonical-units-invalid`. |
| `Struct Example { Box stock { size: [30mm,20mm,6mm] } }` | Builds. |
| `Box stock { size: [30mm,20mm,6mm] }` | Rejects with missing Model/solid/units and V2 source-required diagnostics. |

This does not assert that all wrapper-free profile/domain programs reject.
It shows why omitting the enclosing context from an example is misleading.

### S13 — dense fields interact with symmetry expansion

In the documented mirror/override context, these seed bodies are not equivalent
under current execution:

```text
// Rejects when mirrored and then derived with { Diameter: 8mm }:
Hole<Shaft> leftMount { on:+Z center:Point2(26mm,0mm) diameter:6mm end:ThroughAll }

// Builds in the same context:
Hole<Shaft> leftMount { on:+Z; center:Point2(26mm,0mm); diameter:6mm; end:ThroughAll }
```

The complete context is a 90 x 44 x 8mm Box, a center Plane with normal +X,
`Mirrored Feature rightMount From leftMount { Across: centerPlane }`, then
`Feature largerMount = rightMount with { Diameter: 8mm }` inside Modify.
The failing variant reports canonical document/Modify malformed diagnostics.
The existing symmetry override reader replaces field text up to a semicolon,
newline or closing brace. Recognition of adjacent fields by the shared parser
does not make that rewrite safe. The entry page consequently teaches newlines
or semicolons, not dense implicit field boundaries.

## Ownership map

| Concern | Current source / reference |
| --- | --- |
| Structural declarations, clauses, directives and endpoint spellings | [FirmamentSyntaxParser.cs](../../Aetheris.Kernel.Firmament/FirmamentV2/Syntax/FirmamentSyntaxParser.cs) |
| Field aliases, consumed fields and dimensional expressions | [FirmamentSyntaxValues.cs](../../Aetheris.Kernel.Firmament/FirmamentV2/Syntax/FirmamentSyntaxValues.cs) |
| Preferred spellings versus protected authored names | [FirmamentSourceSpelling.cs](../../Aetheris.Kernel.Firmament/FirmamentV2/FirmamentSourceSpelling.cs), [language style](../public/firmament/language-style.md) |
| Model/Struct units, direct Hole/finish and ignored directives | [FirmamentV2MechanicalBinding.cs](../../Aetheris.Kernel.Firmament/FirmamentV2/FirmamentV2MechanicalBinding.cs) |
| Schema recognition and domain restrictions | [FirmamentFrontendSchema.cs](../../Aetheris.Kernel.Firmament/FirmamentFrontendSchema.cs) |
| Mirror/radial spellings and overrides | [SemanticSymmetryAuthoring.cs](../../Aetheris.Kernel.Firmament/FirmamentV2/SemanticSymmetryAuthoring.cs) |
| Profile pipelines and identity | [ProfileAuthoringParser.cs](../../Aetheris.Kernel.Firmament/FirmamentV2/ProfileAuthoringParser.cs) |
| Point lists in composed features, feature support | [PrismaticProfileComposition.cs](../../Aetheris.Kernel.Firmament/Materializer/PrismaticProfileComposition.cs), [geometry](../public/firmament/geometry.md) |
| Polygon variants and closed boundaries | [ClosedBoundary2Authoring.cs](../../Aetheris.Kernel.Firmament/FirmamentV2/ClosedBoundary2Authoring.cs) |
| Specialization and Feature/Pattern ownership | [Template expansion](../../Aetheris.Kernel.Firmament/FirmamentV2/FirmamentV2TemplateExpansion.cs), [Feature expansion](../../Aetheris.Kernel.Firmament/FirmamentV2/FirmamentV2FeatureExpansion.cs), [Templates guide](../public/firmament/templates.md) |
| Include contracts that differ from the witness | [project snapshots](../public/firmament/project-snapshots.md), [ConceptDerivationTests.cs](../../Aetheris.Kernel.Firmament.Tests/ConceptDerivationTests.cs), [AssemblyInterfaceX1Tests.cs](../../Aetheris.Kernel.Firmament.Tests/Assembly/AssemblyInterfaceX1Tests.cs) |

## Historical claims checked before rewriting

The existing [mechanical migration tests](../../Aetheris.Kernel.Firmament.Tests/FirmamentMechanicalFrontendMigrationTests.cs)
cover unused Slot2 preserving STEP, unknown Fillet and fields rejecting, and
Model/Struct using the same binder. The [X0 tests](../../Aetheris.Kernel.Firmament.Tests/FirmamentFrontendX0Tests.cs)
cover the asymmetric XY/XZ rejection witness. These were inspected, not rerun as
a full regression suite in this documentation-only pass. They prevent treating
the old audit's route behavior as an unqualified description of current source.

The old Box/Hole Z-placement warning is removed from the public geometry guide.
The [bottom-anchored stock regression](../../Aetheris.Kernel.Firmament.Tests/FirmamentV2SemanticHoleTests.cs)
checks host and materialized vertex bounds at 0..height. This is a specific
repaired route, not proof that every domain shares one placement convention.

The old “Feature is Mechanical-only” sentence is corrected: the schema owner
also permits Garment, whose documented Feature use is bounded Profile recipes.
The Polygon2 Rhombus-only statement is corrected against its current owner.

## Priorities for subsequent implementation

1. Close S14/S15 consumed-directive gaps. A schema or include cannot be admitted
   merely because its AST node is recognized. Preserve legitimate project
   consumers; fail closed where no consumer exists.
2. Reconcile the existing capitalization policy with S01/S02. Prove equivalent
   aliases preserve generated identities and geometry; avoid a global text fold.
3. Decide entry/scoping and finite Pattern binding (S05–S08), then point and
   expression rules (S03/S04/S17). These determine what authors can safely infer.
4. Specify termination and selector vocabularies with their engineering types
   (S09–S13/S18/S19). Similar-looking operations need not share an untyped API.

These are ordered proposals for the next code pass, not work implemented here.
Do not add another diagnostic-only parser to represent a grammar that execution
does not honor. The current compiler remains the admission authority.

## Documentation verification

The four complete files in the rewritten entry page compile through the existing
engineering entry point and return nonempty STEP. They cover a direct Hole,
layout/Profile extrusion, Feature + Set Pattern, and product Template.
The sphere example moved to the Revolve guide is also compiled in memory.
The Rhombus, mirror/override and radial fragments also build when wrapped in
their stated Model/material contexts, with semicolon-separated seed fields.
Endpoint selection is exercised by the complete sphere. Fragments remain
labelled and are not represented as standalone executable files.

Eight existing sources were checked with the real CLI's normal validation:
[through-hole](../../fixtures/Canonical/Features/Holes/through-hole.firmament),
[preferred-profile-links](../../fixtures/Regression/FrontendMigration/preferred-profile-links.firmament),
[simple-hole-feature](../../fixtures/Canonical/Feature/simple-hole-feature.firmament),
[pattern-feature-map](../../fixtures/Canonical/Set/pattern-feature-map.firmament),
[generic-mounting-plate](../../fixtures/Canonical/Templates/generic-mounting-plate.firmament),
[boss-pocket-block](../../fixtures/Canonical/Features/Boss/boss-pocket-block.firmament),
[boundary-chamfer](../../fixtures/Canonical/Features/EdgeFinish/boundary-chamfer.firmament)
and [straight-slot](../../fixtures/Canonical/Features/Slots/straight-slot.firmament).
All reported `valid` at level `Built`. This is example evidence, not a full corpus
retest or a new frontend acceptance verdict.

The existing `PublicMarkdownRelativeLinksResolveInsideRepository` test passed
(1 passed, 0 failed), run from Aetheris.CLI.Tests in Release with `--no-build`,
`-m:1`, `-nr:false` and one test CPU. Relative links in every changed Markdown
file, balanced code fences, and `git diff --check` also pass. Only Markdown files
changed. No .NET runtime/test/build hosts remained after the checks. No compiler,
fixture or geometry changes are made, and no full .NET regression or pre/post
geometry qualification is claimed for documentation edits.

## Verdict

The entry document now has one learning order, runnable starting examples,
explicit fragment contexts, and a distinction between preferred style, current
admission and compatibility. Twenty syntax/teaching seams have named ownership
and dispositions. The ignored include and schema-formatting cases remain real
correctness defects for the next implementation pass. A cleaner manual must
not be mistaken for a repaired compiler.
