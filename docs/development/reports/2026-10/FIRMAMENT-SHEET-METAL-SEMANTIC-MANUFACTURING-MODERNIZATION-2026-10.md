# Sheet-metal semantic layout and manufacturing modernization

Implementation: 2026-10-16/17. Basis:
[the source-backed audit](FIRMAMENT-SHEET-METAL-SEMANTIC-MANUFACTURING-AUDIT-2026-10.md)
at `ba81ffda0041b312465d2e2310f99f936616e890`.

## Result and ownership

The authored sheet frontend now consumes the production AST for semantic layout,
structural contracts, manufacturing/PMI and ProfileDelta programs. The previous
layout block/field scans, manufacturing parser scans, and line-regex structural
Concept checker have been removed. Base/Flange/Hole/Cut and the formed/flat and
STEP lowerers remain in place.

`FirmamentDomainBinding` exposes the existing Template/Record/Static scalar
binder, Concept checker, terminal-return Feature specialization and finite Set
Pattern materializer to the sheet adapter. It retains checked tolerance values
for explicit PMI rather than reducing them to nominal strings. It is an executed
production path used by both validate and build, not a shadow parser or separate
validator. CanonicalStaticAuthoring's existing Set-entry binder and Pattern
materializer serve the sheet consumer; no new repetition evaluator was added.

The module resolves its recognized library directives from AST spans. Comment
masking uses lexer tokens, preserving quoted `//` and `#` and source offsets.
Unresolved include/Use directives reject rather than disappearing. Recovery and
reconstruction keep their existing domain authority and lowerers.

Ordinary Point2/2D boundary declarations inside Concept Structs delegate to the
existing construction-guide and closed-boundary owners; the sheet adapter does
not copy their schemas. Shared Profile resolution receives the same bound scalar
environment through derived Profiles and Templates. Lowercase `on XY` uses the
profile owner's AST frame check. Unknown fields in Extend SheetMetal scopes are
also closed, rather than bypassing root admission.

The sheet module closes its top-level statements as well. Known compile-time
data delegates to its owner; unknown material statements cannot disappear outside
the SheetMetal braces. Legacy module-level cuts/semantic operations retain the
single concrete sheet as their explicit compatibility owner, so narrowing scope
does not silently drop previously constructed geometry.

Record/Static/Table ownership comes directly from the shared data binder's
namespace classification. Qualification caught and repaired an adapter omission
for the canonical Static Table tab family; scoped table selections now have their
own geometry witness as well.

## Audit dispositions

| Audit finding | Implemented outcome |
| --- | --- |
| L01 Point2 rejected | Shared dimensional Point2/Vector2 expressions admitted; legacy tuples remain |
| L02/L03 unknown Pattern/feature fields | Field-consumption errors; no geometry |
| L04/L05 XZ silently defaults and changes identities | Unsupported placement rejects |
| L06 ignored Fillet | Unknown layout statement rejects |
| L07 false Boolean Require ignored | Existing Concept/Boolean checker rejects false requirements |
| L08/L09 semicolon bypasses structural Concept | Common typed member contract checked independent of formatting |
| L10 suffix-first Datum capture | Lexical local/exact lookup; foreign layout does not move holes |
| L11 required field terminators | Shared AST separators; newlines admitted |
| L12 extra implicit material bundle | Multiple legacy effect bundles reject as ambiguous |
| L13/L14 Slot/Rectangle capsule mismatch | Explicit geometry compatibility decision; no automatic shape reinterpretation |
| L15 casing differences | camelCase fields plus existing PascalCase aliases; duplicate aliases reject |
| M01 unnamed PMI omitted | One owning unnamed or legacy named Pmi binds and exports all records |
| M02 unknown PMI kind omitted | Unknown record rejects with source context |
| M03 misspelled release field omitted | Unknown field rejects |
| M04 negative tolerance magnitudes | Rejects; no malformed AP242 allowance emitted |
| M05 zero quantity | Rejects; quantity must be a positive integer |
| M06 arbitrary dimension Kind | Thickness/Linear/Diameter admission; unsupported kind rejects |
| M07 later PMI block ignored | Multiple PMI blocks reject |
| M08 comment markers inside strings | Quoted text preserved, including process strings and notes |
| M09 authored Hole absent from PMI targets | Bound authored cuts and generated feature identities admitted as targets |
| M10 typed dimension unsupported | Bound toleranced Length projects nominal, allowances and origins |

Forward Datum references bind before consumers; cycles and unresolved references
reject. Local declarations shadow outer declarations even before their values
have resolved. EqualPitch/Mirror cannot compare coordinates from different region
frames. A Datum-based Mirror can explicitly select `axis: Y`; legacy implicit X
meaning remains compatible.

## Syntax and compatibility decisions

- PascalCase constructs/types/domain symbols; camelCase fields and authored value
  names; common lowercase connective keywords. PascalCase field aliases remain.
- Layout Datum remains a region-local construction point. PMI DatumFeature
  remains a manufactured reference feature. Neither becomes an arbitrary 3D frame.
- New material effects belong under SheetMetal, referring to reusable layout data.
  One legacy effect-containing Concept Struct remains compatible with its old
  identities. Multiple implicit bundles reject; `intent:` is not a selector
  between multiple legacy bundles. This deliberately avoids a new layout loader.
- Ordinary keyed Set/Pattern/Feature authoring supports sheet Hole and Cut result
  consumers. Names such as `mounting.left` survive entry reordering. The old
  centered Count/Pitch Pattern retains its original placement and index identity.
- Legacy semantic Pattern Rectangle/Slot remains its straight-run capsule;
  legacy direct Cut Slot remains rectangular. New Rect2/Slot2 Profiles retain
  their established geometry and overall-length contract. No baseline was rerecorded.
- ProfileDelta prefers `anchor: CenteredAt(50mm)` and equivalent FromStart/FromEnd
  calls. The old whitespace anchor has one local compatibility production in the
  shared parser and produces the same call AST.
- Manufacturing is release/process metadata and allowance policy. A selected
  named release uses `manufacturing: release`; one legacy release is implicit.
  Other reusable release declarations are still field/type checked.
- One root-owned Pmi is canonical. A single named top-level Pmi remains compatible.
  A Manufacturing block is no longer a prerequisite for valid explicit PMI.
- `dimension:` consumes a checked toleranced Length from Static/Record binding.
  Geometry consumes its nominal. Legacy Value/PlusMinus remains supported;
  conflicting literal and typed projections reject. General process allowances
  remain policies rather than invented zero-valued engineering dimensions.
- Note is preferred; Annotation remains an admitted sheet alias. Existing
  DatumFeature/Position export, datum references, quantities and face associations
  are retained. Model-domain Hole<Shaft>/axis-face selectors remain unsupported
  sheet operations and receive concrete repairs using the author's names.

## Diagnostics and provenance

SheetMetalDiagnostic retains its existing positional contract and adds optional
sourceFile, sourceSpan, context and locationNote properties. CLI JSON continues
to serialize structured records. CLI text now presents source file/location,
severity, code and explanation for sheet validate/build/inspection failures.
It uses the existing diagnostic location presenter, not another validator.

AST field closure supplies unknown/duplicate field diagnostics. Shared Concept
binding preserves requirement explanations. Expanded-source diagnostics map back
to original enclosing declarations where possible. Unavailable mappings remain
unavailable with an explicit note; no synthetic 1:1 location is invented. Bound
dimensions retain value-origin receipts; imported origins without a mapping are
identified as expanded input rather than mislabeled original source.

Qualification caught a provenance serialization defect during migration:
interpolating IdentifierSyntax into ProfileDelta receipts embedded source offsets.
The implementation now writes the authored name text, preserving the original
receipt identity and optional-semicolon parity. Two existing CLI diagnostic
tests also caught lost concrete repair examples; those examples were restored.

## Qualification

Tracked evidence is the new `fixtures/Regression/SheetMetalSemantics` corpus,
`SheetMetalSemanticSyntaxTests`, and the source-located CLI test. Its runner calls
the actual repository CLI for validate/build/inspect and reinspects exported STEP.
Generated evidence belongs under ignored `artifacts/local/`.

The new tests assert actual rejection/no Part, geometry/flat material parity,
cut centers, stable identities under Set reordering, Record tolerance origins,
exact Profile cuts and AP242 nominal/allowance/quantity/datum/face associations.
Additional cases cover duplicate aliases, forward references/cycles, quoted
import-looking strings, unresolved directives and modern ProfileDelta anchors.

Final qualification on the completed implementation:

| Check | Result |
| --- | --- |
| Release solution build, serial, compiler server reuse disabled | Passed; 0 errors, 30 existing nullability/WASM SQLite warnings |
| Focused sheet suite | 157/157 |
| Core fast lane, Category != SlowCorpus | 1,076/1,076 |
| Complete solution suite, serial, including corpus tests | 5,368/5,368 across 20 test assemblies; 0 failed/skipped |
| Semantic/manufacturing CLI corpus | 29/29; validate/build/inspect agreement and exported PMI inspection |
| Shared-profile and canonical sheet CLI corpus | 26/26; modern/legacy formed STEP bytes and flat geometry match |
| FrontendX4 authoring corpus | 32/32; chimera/reference topology, bounds and volume qualification retained |
| Final process inventory | No dotnet, aetheris or testhost processes remaining |
| Diff and text checks | git diff --check clean; UTF-8 and newline checks passed |

The FrictionLab project still reports no discovered tests; the 20-assembly total
counts assemblies that executed tests. This pass did not perform a new Helios
browser workflow qualification. It changes no editor-side validator.

Commands used the repository Release CLI. Final logs are in
`artifacts/local/sheetmetal-semantics-audit/accepted-{build,focused,fast,full}.log`
and `accepted-{semantic-cli,profile-cli,x4}.log`. Per-case JSON/STEP evidence is
under `artifacts/local/sheetmetal-semantics/cli`, `sheetmetal-syntax/cli` and
`frontend-x4`. Earlier failing iteration logs remain distinct from these final
accepted runs. No reference geometry or expected.json was rerecorded.

## Code-quality review

- Removed the obsolete sheet structural-contract checker and the migrated
  block/field regex helpers. Admission follows AST ownership and field consumption.
- Retained only bounded semantic-path classification regexes in layout; they do
  not discover statements or decide parser fallback. No whole-document dispatch,
  source macro system or diagnostic-code fatality allowlist was introduced.
- The Hole/Cut result set describes domain capabilities for the existing Feature
  expander. It does not decide severity or create a second field schema.
- Kept mutable binding/consumption state per compilation. No global parser state,
  service, scheduler or persistent runtime host was added.
- Shared construction/profile data delegates to its existing binder and lowerer;
  its scalar environment now crosses the profile-consumer boundary explicitly.
- Validate/build use the same sheet compilation owner. Presentation adds source
  metadata without product-specific validation or changing geometry exporters.
- Documentation, source specimens and generated output follow repository locations.
  Existing geometry references and corpus goldens were not rewritten.

## Explicit remaining limits

This pass does not implement arbitrary-plane sheet bases/layouts, general GD&T,
or a geometric constraint solver. Sheet Feature results are bounded Hole/Cut
consumers; mechanical result capabilities do not automatically become sheet
operations. Modern finite mapping is rooted in the consuming SheetMetal body;
legacy Count/Pitch recipes remain a compatibility producer.

Explicit PMI nominals remain authored requirements. Target binding and exported
face association do not certify that every manually authored nominal equals a
measured BRep dimension. The direct-hole target specimen isolates identity
binding; it is not a manufacturing-release consistency certificate.

The four brace-shaped domain Require checks remain compatible AST-owned checks;
this pass has not introduced callable EqualPitch/Mirror Concept intrinsics for
arbitrary Boolean composition. Ordinary Boolean/predicate/bundle Concepts work
through the shared checker. These distinctions are documented rather than hidden
behind apparent syntax acceptance.

No user decision is needed for these limits. Further capability expansion should
have its own witnesses and lowerer qualification.

## Executive verdict

**Success for this modernization pass.** The audited sheet layout/manufacturing
omissions and contradictions are repaired or have an explicit geometry-preserving
compatibility disposition. The production path now shares AST/value/Concept and
finite Feature/Pattern owners, rejects unsupported intent, preserves diagnostics
and PMI provenance, and retains the qualified canonical geometry. The remaining
capabilities above are explicit limits, not silently admitted requests.
