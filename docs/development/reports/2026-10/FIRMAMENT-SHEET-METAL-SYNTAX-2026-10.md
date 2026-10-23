# Firmament shared mechanical / sheet-metal Profile pass

Date: 2026-10-16. Starting revision: `9c14bfbb`.

## Decisions and result

A Profile is a reusable exact material boundary, independent of the consumer.
The modern compact spelling is `Profile blank = Rect2 { center: ...; size: ... }`.
Existing explicit loops, named segments, pipelines and immutable boundary edits
remain available. `Profile copy = blank` reuses the ordinary derived Profile owner.
The compact form delegates to the existing closed-boundary field schema and
Profile binder at their established AST-to-domain lowering seam; it does not add
a new document parser route, numeric evaluator, sketch system or materializer.

Sheet `Base deck { profile: blank }` and `Cut vent { profile: opening; on: deck;
at: Point2(...) }` now resolve through the same named Profile API as mechanical
Extrude. Base capability remains a single axis-aligned rectangle in local XY.
Cut capability admits one exact line/arc loop or a circle. Unsupported frames,
curves, inner islands and nonrectangular bases reject rather than becoming bounds
or a default rectangle. Base minimum coordinates become physical origin; Cut
coordinates are local to the named region and `at` adds a translation.

New authoring uses PascalCase constructs/types/domain symbols, camelCase fields
and authored names. PascalCase field aliases remain admitted. Base, Flange, Hole
and Cut read fields from the shared AST and dimensional binder; their former
regex scalar/tuple/field readers were removed. Newlines or semicolons separate
these fields. Unknown fields and unsupported header placements close through the
same field-consumption owner. Unknown sheet-body statements reject while known
semantic-layout operations are delegated to their actual domain owner.

The XZ witness uncovered a mechanical casing defect: `Construction Plane` tracing
only read `Trace` with a regex. Its owner now reads `trace`/`Trace` through shared
AST fields and rejects duplicates/unknown fields. A non-XY Profile remains a valid
mechanical construction possibility, but cannot silently become an XY sheet base.

## Compatibility evidence and decisions

- Legacy rectangular bases and Circle/Rectangle/Slot cuts preserve interpretation.
  The modern/legacy bracket pair has byte-identical formed STEP and matching flat
  area/bounds. No geometry reference was rewritten.
- Legacy sheet Cut `Slot` is rectangular. It is retained deliberately. Modern
  `Slot2.length` means overall capsule length, matching mechanical construction;
  rounded ends remain exact arcs in formed/flat geometry. Semantic-layout Slot
  Patterns already had their own exact-capsule contract and keep it.
- Named Profile declarations bind before legacy shape tags: a Profile named
  Circle/Slot/Rectangle is a real named boundary, not an implicit old cut recipe.
  Mixed legacy/named Base declarations and irrelevant dimensions on a Circle cut
  reject instead of discarding one request.
- `Extend SheetMetal` is an extension owner, not a second concrete document.
  Existing product-family assemblies, manufacturing releases, attachments and
  corner profiles remain part of the regression gate.
- Reusing one Profile in two cuts gives each cut distinct boundary identities,
  while tracing provenance back to the original Profile segments.
- `Intent` historically accepts Authored/Reconstructed/Manufacturing tags or a
  declared Concept Struct layout. Those legitimate meanings remain admitted;
  unknown references reject. This pass does not redesign that overloaded field.
- The kernel and the existing scalar evaluator are unchanged. Domain lowering
  now transports the resolved exact contour into the already-existing sheet cut
  path, instead of reconstructing a rounded shape from rectangular dimensions.

## Qualification

Focused sheet-metal suite: 119 passed, including 20 added profile tests. Tests
cover formed STEP equality, exact rounded area, legacy Slot preservation, origin,
repeated-cut identities/provenance, Template specialization, mechanical compact
Circle2/Slot2/RoundedRect2, unknown fields, unresolved names, unsupported curves,
header placement, conflicting Base declarations, irrelevant Circle dimensions,
legacy-tag name collisions and the actual XZ frame witness. The existing six
cubic-profile tests also pass: a full-suite failure exposed compact lowering
intercepting Profile Template specialization, repaired by leaving specialization
and historical call productions with the existing Template owner.

The final CLI gate passes 26/26 cases: all 19 current canonical SheetMetal
sources, five regression sources and both complete documentation examples. Every
case has validate/build agreement; admitted sheet parts also flatten/export. The
modern/legacy pair has identical formed STEP bytes and flat area/bounds;
`cli/results.json`. The named Profile alias also builds through the CLI.

The SDK build and all 14 transport tests pass. A real headless Edge WebAssembly
worker builds a compact mechanical Profile, rejects its misspelled field with a
structured source-located diagnostic, retains byte-identical last-valid STEP,
and rebuilds valid source; `browser.json`. This does not claim a new Helios UI
acceptance run or browser-native SheetMetal capability.

The original FrontendX4 authoring gate passes 32/32, including the chimera and
recorded geometry. The compile-time choices gate passes 57/57, with validate/build
agreement and 29 byte-identical accepted/reference STEP pairs. No existing source
specimen or geometry golden was rewritten.

Final Release solution build: zero errors, 11 warnings; `final-build.log`.
Fast Core lane: 1,076 passed; `fast.log`. Complete solution lane: 5,329 passed
across 20 test projects, zero failures or skips; `full.log`. This includes all
2,817 Firmament tests, 119 sheet-metal tests and seven Editable V8 tests. The
FrictionLab project reports no tests available and is not counted as a passing
project. No-build tests ran against the integrated Release build, serially with
MSBuild node reuse/shared compilation disabled. Final process inventory has zero
dotnet/testhost processes.

Diff whitespace, changed-file UTF-8/newline and repository content-location checks
pass. Review confirms no new document dispatch, regex parser, diagnostic-code
allowlist or independent numeric evaluator. The new Profile production is a
bounded AST lowering within the existing closed-boundary owner; specialization
and call syntax remains with the Template owner. Generated logs, reports and STEP remain under ignored
`artifacts/local/sheetmetal-syntax/`. Source witnesses and the CLI runner live in
`fixtures/Regression/SheetMetalSyntax/`.

## Remaining bounded work

This is a closed-profile reconciliation pass, not a claim that every SheetMetal
production is now a unified semantic frontend. Semantic-layout Pattern/Tab,
AttachmentPath, CornerProfile, ProfileDelta, manufacturing/PMI and recovery
adapters retain their existing domain-specific productions and terminators.
Their deeper syntax migration should reuse their semantic owners, not broaden
this pass into a second global rewrite. In particular, unification of arbitrary
Profile islands or spline cuts requires capability qualification beyond syntax.

SheetMetalDiagnostic still has the module's existing code/severity/message shape;
complete original-source span projection through all older manufacturing and
semantic adapters remains a separate diagnostic task. No new exact-location
claim is made for those adapters. The build/validate gate uses the same module
compiler, and the new capability errors identify the affected named construct.

No additional user decision is required for the qualified surface.

## Verdict

**Success for this shared-profile pass.** Both engineering domains consume the
same exact Profile authority; the modern compact syntax improves ordinary
mechanical authoring, and native sheet Base/Cut admission preserves qualified
geometry and compatibility. The limits above remain explicit follow-up work,
not claims of an entirely migrated SheetMetal frontend.
