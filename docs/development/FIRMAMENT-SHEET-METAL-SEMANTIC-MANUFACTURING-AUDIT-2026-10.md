# Sheet-metal semantic layout and manufacturing syntax audit

Date: 2026-10-16. Audited revision: `ba81ffda0041b312465d2e2310f99f936616e890`.
Status: audit complete; proposals below are **not implemented**.

This is the original audit snapshot. Subsequent implementation and current
dispositions are recorded in the
[modernization report](FIRMAMENT-SHEET-METAL-SEMANTIC-MANUFACTURING-MODERNIZATION-2026-10.md).

## Conclusion

Converge these frontends through the existing syntax tree and compile-time binder,
while preserving sheet-region semantics and the existing formed/flat/AP242
lowerers. A capitalization pass alone would leave serious admission defects.
Fix ownership, complete consumption and scoped binding before changing examples
to preferred spellings. Do not add another parser, rewrite pass or shadow mode.

This audit changes documentation only. Existing source fixtures, compiler code,
geometry references and public syntax contracts have not been migrated.

## What the language currently means

| Surface | Current owner and meaning | Convergence decision |
| --- | --- | --- |
| Base, Flange, Hole, Cut | Shared AST fields and dimensional values feeding native sheet lowerers | Keep; extend the established seam |
| Named Profile | Shared exact closed-boundary authoring | Keep Rect2/Circle2/Slot2 and exact loops as the boundary vocabulary |
| Layout Datum | Region-local **2D construction point**, not a manufacturing datum | Preserve its role; admit ordinary Point2 expressions through the shared binder |
| Concept Struct | Layout parser scans contained operations globally; containment mainly supplies a name prefix | Make construction data scoped; material effects must belong to a consuming sheet |
| AttachmentPath | Bounded edge segment with inset/span and explicit carrier release | Keep this domain operation; it is not a generic curve or assembly mate |
| Tab / SteppedNotch / CornerProfile | Material edge edits, sometimes written inside a Concept Struct | Keep lowerers; give every effect an explicit sheet owner |
| ProfileDelta | Finite edge program with levels, spans, transitions and exposed capabilities | Keep program/identity model; move syntax and values to shared owners |
| Layout Pattern | Separate centered Count/Pitch feature generator | Preserve legacy geometry; converge repetition onto ordinary Pattern/Feature binding |
| Layout Require | Brace-shaped RequiredMembers/EqualSize/EqualPitch/Mirror checks | Common Boolean Require semantics, with typed domain predicates |
| Structural Concept | Separate line-sensitive sheet contract checker | Common structural-contract binder, supplied with sheet semantic members |
| Manufacturing | First globally found named release/process block | Explicit owning-sheet selection; metadata/default policies remain distinct from geometry |
| Sheet Pmi | First globally found named block; DatumFeature/Dimension/Position/Annotation | Explicit projection bound to owner symbols and admitted export capabilities |

The primary source owners are
`Aetheris.SheetMetal/SheetMetalSemanticLayout.cs`,
`SheetMetalConceptContracts.cs`, `SheetMetalManufacturingDefinition.cs` and
`SheetMetalFirmament.cs`; ProfileDelta parsing lives in
`Aetheris.Kernel.Firmament/FirmamentV2/SemanticProfileDeltaParser.cs`.
The sheet diagnostic record is in `Aetheris.SheetMetal/SheetMetalIr.cs`.

## Executed evidence

Used the repository Release CLI, not an installed global executable. Each of 27
source variants ran through `validate --json`, `build --json`, and
`sheetmetal inspect --json`: 81 invocations. Twenty-one variants were admitted
and six rejected. Validate/build acceptance agreed for all 27. **That agreement
does not prove soundness:** both paths admit the silent omissions below.

Baselines:

- `fixtures/Canonical/SheetMetal/semantic-panel.firmament`: four cuts, three
  regions, one bend, flat material area 7800.022098417826 mm².
- `fixtures/InvestorPitch/sheet-metal-bracket.firmament`: four cuts, three regions,
  one bend, flat material area 13440.493767226479 mm², 13 semantic PMI records.

The following table gives exact edits to those baselines. Layout rows use the
first baseline; manufacturing rows use the second. A means admitted by all three
commands; R means rejected by all three. The two unchanged baselines also count
toward the 27 variants.

| ID | Source edit | Result | Observed behavior / classification |
| --- | --- | --- | --- |
| L01 | Datum `At: Point2(50mm, 30mm);` | R | `sheetmetal-semantic-datum-point`; shared point spelling not supported here |
| L02 | Add `Coutn: 9;` beside Pattern `Count: 4;` | A | Unknown field ignored; identical STEP |
| L03 | Add `Depth: 100mm;` in Pattern's Circle feature | A | Unsupported field ignored; identical STEP |
| L04 | `Concept Struct PanelLayout On XZ {` | A | Placement ignored; identical STEP; generated names lose `PanelLayout.` prefix |
| L05 | Same header using modern lowercase `on XZ` | A | Same placement and identity defect |
| L06 | Add `Fillet ignored { radius: 5mm; }` inside layout | A | Operation omitted; identical STEP |
| L07 | Add `Require impossible => false` inside SheetMetal body | A | False requirement omitted; identical STEP |
| L08 | Replace Concept member `Panel: SheetRegion` with `Missing: SheetRegion` | R | Correct `firmament-concept-missing-member` |
| L09 | Same missing requirement with trailing semicolon | A | Contract member disappears from checker; identical geometry |
| L10 | Prepend `Concept Struct Other { Datum MountingPatternCenter { On: Panel; At: (55mm, 30mm); } }` | A | Existing Pattern binds Other's short name: center moves from (50,30) to (55,30); STEP changes |
| L11 | Remove semicolons after Pattern Count and Pitch | R | Reports missing Count despite authored `Count: 4`; legacy field-reader mismatch |
| L12 | Add an unused Concept Struct with one Circle Pattern, diameter 2mm, center (50mm,40mm), Count 1, Pitch (10mm,0mm), On Panel | A | Extra cut materializes: five cuts; area decreases by π mm² |
| L13 | Replace Circle feature with `Slot { Width: 4mm; Length: 10mm; }`, Count 1 | A | Exact capsule, area 7860.553063332699 mm² |
| L14 | Same as L13 with `Rectangle` instead of `Slot` | A | Byte-identical capsule STEP; Rectangle does not mean rectangular opening here |
| L15 | Lowercase first letters of On/At/Feature/Diameter/Center/Count/Pitch fields | A | Identical STEP; those casing aliases already work |
| M01 | Change `Pmi InvestorBracketPmi {` to `Pmi {` | A | All 13 PMI records disappear, including from exported AP242 |
| M02 | Add `Flatness Ignored { Target: BasePlate; Tolerance: 0.1mm; }` to Pmi | A | Unknown record skipped; still only 13 PMI records |
| M03 | Misspell `GeneralCutTolerance` as `GeneralCutTolerence` | A | Policy becomes null; no unknown-field diagnostic |
| M04 | Replace `PlusMinus(0.12mm, 0.12mm)` with negative arguments | A | Negative allowances retained in inspection and exported AP242 |
| M05 | Replace `Quantity: 2;` with `Quantity: 0;` | A | Zero quantities retained in exported diameter and position records |
| M06 | Replace `Kind: Linear;` with `Kind: Banana;` | A | Arbitrary kind admitted; analyzer reports generic Dimension |
| M07 | Append second named Pmi containing one valid Annotation | A | Second block ignored; 13 records rather than 14 |
| M08 | Put `//` inside quoted BlankProcess text | R | Misleading `sheetmetal-firmament-invalid`: found zero SheetMetal documents |
| M09 | Add valid Hole Extra, then target it in BaseMountDiameter and BaseMountPosition | R | `sheetmetal-manufacturing-pmi-invalid`: unknown identity Extra |
| M10 | Declare `Static plateWidth: Length = 100mm tol 0.25mm`; replace BaseWidth Value/Tolerance with `dimension: plateWidth;` | R | Requires literal Value/PlusMinus; typed projection not admitted |

M09 had an additional control build: the same Hole Extra with original PMI
targets builds successfully and reports five features. This isolates the missing
PMI target registration from hole construction.

Five exported STEP files were independently inspected with `analyze --json`:
manufacturing baseline, unnamed PMI, negative tolerances, zero quantities and
unknown dimension kind. Baseline exports two datums, five dimensions, two
positions and four annotations. The unnamed variant exports zero. Existing
region/pattern datum and position associations are real export capabilities and
must survive modernization; root-owned thickness/notes need not have individual
face associations.

The layout baseline STEP SHA-256 is
`baeea51b9aafa2b0cef29fddfa44934e7eb49e9f67fa8d1338b21e17ebccb4eb`.
L02/L03/L05/L06/L07/L15 produce exactly those same bytes. L10 produces
`f7ff208a9d1b958575bf605510106e3b9af4d5902689ed6fb413dac7d54dba37`.
Unchanged area/topology would not have detected that location change.

Local generated sources, scripts, command JSON and STEP evidence are under
`artifacts/local/sheetmetal-semantics-audit/`. They are ignored development
output, not a committed regression corpus. The table preserves reproduction
edits; implementation should promote the relevant specimens into `fixtures/`.
Typical reproduction commands:

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll validate INPUT --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build INPUT --output artifacts/local/audit.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll sheetmetal inspect INPUT --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll analyze artifacts/local/audit.step --json
```

## Root causes and additional source findings

1. Layout `Blocks` scans the whole text. Struct detection requires a bare name
   followed by a brace, so a placement header breaks containment without stopping
   discovery of inner effects. Unknown statements/fields are not consumed or
   rejected. This is a frontend correctness defect, not a missing kernel feature.
2. `TryPointExpression` resolves the first suffix-matching Datum; it does not bind
   the nearest lexical symbol. Datum declarations bind in source order. `TryAxis`
   treats a Datum reference as its X axis; no explicit Y choice is available there.
3. `SheetMetalConceptContracts.Validate` recognizes whole-line members without
   terminators. Ordinary semicolon formatting can bypass the contract.
4. Manufacturing `FindNamedBlock` chooses the first block globally. `ChildBlocks`
   only discovers four known record kinds, so the switch's unsupported-kind
   diagnostic cannot protect unknown kinds. Optional field parsers conflate
   malformed/misspelled fields with absence. Duplicate-field consumption is absent.
5. Manufacturing `BuildTargets` registers part, base, flanges, bends and layout
   Patterns/members; it does not receive ordinary authored cuts. Export association
   code already knows how to find features, but admission cannot reach them.
6. Manufacturing owns another literal-only tolerance/value parser. It does not
   consume the existing typed-tolerance binding/provenance contract. Its target
   matching is case-insensitive, unlike ordinary case-sensitive authored symbols.
7. `SheetMetalFirmament.Compile` strips comments with a string-unaware regex.
   It conditionally invokes shared Template expansion when `Template<...>` is
   present. The resulting direct/expanded-source admission difference requires
   qualification; this audit did not execute a paired Template-trigger witness.
8. `SemanticProfileDeltaParser` still owns case-sensitive PascalCase field readers
   and special whitespace anchors such as `FromStart 0mm`. It has not received
   the Base/Cut AST-field migration. Source inspection only in this audit.
9. Sheet diagnostics carry code/severity/message/source-face IDs, but lack authored
   file/span fields. Tested errors have explanatory messages but no precise
   authoring spans. Expanded-source provenance must be retained at the shared seam.

Source-only findings are not presented as executed regressions. Recovery and
reconstruction are separate evidence-bearing workflows; do not mechanically
reinterpret their source as ordinary authored sheet instructions.

## Proposed language decisions

### One spelling and value system

Use PascalCase constructs/types/domain symbols, camelCase fields and lowercase
connective keywords (`on`, `over`, `with`, `as`). Preserve authored names exactly.
PascalCase field aliases remain explicit compatibility aliases at the owning
schema; conflicting duplicate aliases reject. Semicolons and newlines follow
the common AST rules, never a domain-specific line regex.

Use the shared dimensional evaluator, scoped Record/Static/Table values and
typed tolerances. Admit `Point2(...)` and `Vector2(...)` expressions for layout
coordinates. Old tuples remain compatible where their meaning is unambiguous.
Region-local coordinates retain their owning region/frame; Point2 alone must not
authorize mixing base and flange coordinates. Unsupported `on XZ` rejects until
the domain lowerer can honor it. Do not silently rotate, flatten or default it.

Keep layout `Datum` as a compatibility construction-point declaration initially;
do not introduce a new Datum2 type merely to rename it. Document that it is
different from PMI `DatumFeature`, a manufactured reference feature, and from a
3D DatumFrame. Shared AST binding can distinguish those owners without conflating
their capabilities or inventing a second Point2 implementation.

### Construction data first, material operations under the consumer

A Concept Struct supplies named construction data and semantic paths. A
SheetMetal body owns material edits and repetition. New examples should put
Feature calls, Patterns, Tab/Notch/CornerProfile effects under that consuming
sheet, referring to layout members by normal scoped names. This avoids adding a
new mandatory `layout:` selection mechanism just to compensate for global scans.

Legacy layout-contained effects can remain compatible only through explicit AST
ownership: associate them with their uniquely resolved sheet regions and reject
ambiguous ownership. Merely importing a reusable data declaration must not cut
material. Do not remove this compatibility form before existing canonical
layouts, product families and provenance paths are qualified.

Reuse ordinary `Pattern name over values { value => Recipe(value) }`, named Sets
and named Feature calls; extend the sheet result consumer rather than building
another repeater. Sheet recipes return sheet operations with region targets,
not mechanical `Hole<Shaft>` with face selectors. Keyed values preserve instance
identity independently of declaration order. Legacy Count/Pitch remains a
compatibility producer with its existing centered placement and index identities.
No automatic conversion may recenter a previously accepted family.

Preserve AttachmentPath's span/inset/release safety and ProfileDelta's exposed
capabilities. Keep current CornerProfile target headers for the first migration;
changing their derived IDs is unnecessary. Normalize ProfileDelta anchor calls
to expressions such as `anchor: FromStart(0mm)` through its AST production,
with a local compatibility production for the old whitespace form. This is a
proposal, not currently documented working syntax.

Use the common structural Concept checker with sheet semantic member types.
Use common `Require name => predicate`; the existing four sheet checks become
typed domain predicates with explicit region/axis inputs. Legacy brace Require
records lower into the same checked requirement representation. Do not add a
general geometric constraint solver or silently treat a bare Datum as a Y axis.

### Preserve old shapes; use shared Profiles for new shapes

Never globally alias Rectangle to Slot2. L13/L14's old capsule removes
`10 * 4 + pi * 2² = 52.566370614359... mm²`: its long dimension describes the
straight run. A modern Slot2 with overall length 10 and width 4 removes
`(10 - 4) * 4 + pi * 2² = 36.566370614359... mm²`. A literal 10-by-4 rectangle
removes 40 mm². These are three different shapes.

Retain old semantic Pattern Rectangle/Slot geometry under explicit compatibility
ownership and diagnose the misleading alias as deprecated. New recipes use
named Rect2/Slot2 Profiles through the shared Cut boundary owner. Legacy direct
Cut Slot remains its already documented rectangular compatibility operation.

### Requirements, release policies and PMI have separate jobs

A toleranced compile-time value supplies a nominal to geometry. `Pmi` explicitly
projects that requirement. Manufacturing stores material/process release data
and default allowance policies; it must not silently export every field or turn
a descriptive process string into a dimensional requirement.

Proposed projection fragment, **not supported by today's sheet frontend**:

```firmament
Static plateWidth: Length = 100mm tol 0.25mm
// The sheet Base/Profile consumes plateWidth's nominal value.
Pmi {
    Dimension baseWidth { target: basePlate; kind: Linear; dimension: plateWidth }
}
```

Reuse native PMI's `dimension:` binding and original-value provenance. Retain
legacy Value/PlusMinus pairs with validated nonnegative allowance magnitudes.
Reject contradictory literal and bound-value fields. GeneralCutTolerance is an
allowance policy, not a fake nominal `0mm tol ...` dimension; keep that distinction.
Validate known kinds, units, finite values, positive quantities and datum labels.
Do not silently infer that an authored nominal matches a measurable feature:
target-specific checks must establish what is actually measured.

Make root-contained unnamed Pmi canonical, as in modern Model/Assembly authoring.
Support named top-level Pmi and Manufacturing declarations through explicit
owner binding (a proposed sheet `manufacturing: release` reference selects a
reusable release). Legacy single-root/single-release documents remain unambiguous
compatibility inputs. Duplicate or ambiguous blocks reject; first-global wins
must disappear. PMI should not require a process-release block just to project a
valid engineering requirement. Missing release data is a separate release policy.

Keep DatumFeature for manufactured sheet reference features; retain Position's
existing datum relationships and quantity. Prefer the common Note spelling for
new annotations, retaining Annotation as a local alias. Do not remove working
sheet Position export merely because native Model PMI has a smaller capability
set. A shared syntax does not grant unsupported GD&T across every domain.

Resolve targets from the actual bound domain symbol registry, including authored
cuts, rather than another hand-maintained name collection. Export must preserve
stable IDs and feature/region face associations. Capability rejection must name
the requested record and target at the original source span.

## Implementation order and qualification gate

1. **Honest AST admission:** migrate remaining layout, contract and manufacturing
   production owners to existing syntax nodes. Close unknown/duplicate fields,
   statements, placements and blocks; replace global scans and suffix capture.
   Fix string/comment handling through the existing lexer. Preserve lowerers.
2. **Shared values and projection:** use existing scoped binder for layout
   expressions, structural Concepts, Requirements and typed PMI dimensions;
   preserve source provenance and bind real target symbols. Qualify release/PMI
   ownership and remove the corresponding obsolete readers.
3. **Common repetition and authoring cleanup:** admit sheet Feature results through
   the shared Pattern expansion, preserving old centered/index identities until
   explicit migration. Modernize canonical examples and public syntax only after
   each spelling works. No shadow-only acceptance milestone.

Admission fixes deliberately reject previously ignored invalid intent. Valid
legacy source must preserve formed/flat geometry, cut centers, semantic IDs,
exact material area, bend correspondence and AP242 requirements/associations.
Require validate/build agreement but also assert requested effects and PMI
presence. Keep the negative specimens negative; do not merely match error text.

Promote L02-L10 and M01-M05/M07-M09 first. Add duplicate aliases, ambiguous
ownership, imported unused declarations, wrong units, cross-region references,
forward references and Template/non-Template paired cases. Qualify semantic-panel,
inset-partial-flange, ProfileDelta tab families, the investor manufacturing
bracket and SheetMetal.ProductFamilies, plus modern shared-profile examples.
Compare geometry independently of provenance changes to STEP metadata. Reinspect
AP242 after export. Then run focused suites and both prescribed .NET test lanes,
including the full solution corpus. No full .NET regression suite was rerun for
this documentation-only audit; the CLI probes above are the fresh evidence.

## Deferred scope and decisions

No user decision is required to start the admission repairs. The recommended
choices above are concrete enough for implementation. New arbitrary-plane sheet
layout, general GD&T, a constraint solver and recovered-source modernization
remain separate capability work. Shared Pattern consumption is a bounded
frontend extension, not permission to rebuild the geometry engine.

**Audit outcome: Success.** The implementation boundary is identified, current
failures are reproduced, and the proposed surface separates construction data,
material operations, typed requirements and manufacturing release information.
The existing semantic/manufacturing frontend is **not yet qualified as honest**;
the defects above remain open until implementation and regression qualification.
