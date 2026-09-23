# FIRMAMENT-SCHEMA-X1 — Authored field projection and safe source rewrite

## Executive verdict: Accepted

Aetheris now projects typed semantic values, authored source text, origin, and exact parser-owned spans through the generated schema. Helios uses the Web SDK to show those values and to change an authored canonical Hole Diameter by replacing only its Firmament literal, then rebuilding normally. The browser witness changed 10 mm to 12 mm, retained the adjacent comment, refreshed the Inspector and Hole wall selector, passed Monaco undo/redo, and persisted through Telos save/reopen.

## Compiler and Web contract

`FirmamentFieldProjector` exposes version `firmament-field-projection/1`. Each construct projection has a semantic ID, source document and hash, build revision, construct span, and schema-ordered fields. Each field contains effective typed value, authored text, origin, declaration/value spans, editability, and a read-only reason. Values travel as explicit kind, text, optional number/components, and unit rather than arbitrary CLR objects. Source ranges are recorded while the canonical Box/Hole and WireForm/Loft authoring parsers bind declarations. The lexical range scanner skips comments and nested delimiters; no post-build text search chooses a rewrite target.

The generated registry now emits a typed `ProjectHoleDiameter(FirmamentV2SemanticHoleDecl)` getter. A declaration advertising `SourceEditable` without a public static numeric projector raises `FMS001`. Only Hole Diameter advertises direct editing. The Web SDK adds `describeConstruct` and `rewriteField`; the latter checks source hash, build revision, semantic ID, field ID, the old literal, and a positive finite Length in mm. Refusals return stable codes including `stale_revision`, `field_readonly`, and `invalid_value`. It returns a Monaco replacement and new source but never changes compiled state itself.

| Construct | X1 value coverage | Editing |
| --- | --- | --- |
| Box | Bound Size vector, authored aggregate and exact range; Bounds unavailable when omitted | Source-only aggregate |
| Hole | Bound On, Center, Diameter, End; exact authored ranges; Wall remains a separate semantic output | Authored literal Diameter in mm |
| Helix | Typed Radius, Turns, Pitch, Height, Handedness, StartPhase from `WireAxisCoilAir`; omitted Height derived, Handedness/StartPhase defaulted | Source-only; expressions retain authored text |
| Loft | Bound profile/frame names, Rule, Correspondence, Reference, Twist; omitted Twist defaulted | Source-only complex fields |
| Concept | No fixed fields: members and their types are authored per Concept declaration | No fixed-schema projection |

The Inspector maps generated schema IDs to projection fields without Box/Hole-specific field rendering. It displays effective value, origin, type/unit, authored text, source link, editability, and refusal reason. Stale source disables the editor. The current experimental effective override section is a pre-existing separate UI; the X1 path uses only source rewrite and normal rebuild.

## Entry discovery and parser convergence

Generated schema entries now hold buildable full-document Helix and Loft templates. Bare Helix/Loft LX searches return those entries and legal owner context. Helios palette commands open the same schema entries. The canonical Hole parser takes common legal field names from generated schema, leaving variant-only additions local. This removes one duplicate common-field name list. Box recognition, Hole variant fields, cross-field rules, variant validation, and legacy syntax remain parser/binder-owned; the generator does not emit a parser. Binder values and schema descriptors still meet in the bounded static projection switch; documentation examples still spell source field names. Helios has no separate Box/Hole field table for these Inspector values.

## Qualification

- Focused generator, schema, and projection tests: 15 passed. They cover typed generated getter emission, invalid editable declarations, Box/Hole/Helix/Loft values, default/derived origins, expression preservation, exact rewrite, stale/invalid/read-only refusal, and buildable entry templates.
- Aetheris solution build passed with 7 existing Web/WASM warnings and no errors. The complete solution test run passed serially (`dotnet test Aetheris.slnx --no-build --no-restore -m:1 -v:q`), including Firmament 1,614, Kernel.Core 1,099, Server 59, SheetMetal 99, FEA 43, and EditableV8 3 tests.
- The final Web SDK was rebuilt and installed into Helios. Helios production build and 27 unit tests passed. The full Chrome Playwright suite passed serially, 7/7 tests, including Hole Inspector rewrite with undo/redo, save/reopen and 12 mm Hole wall reference; Box/Hole selector and PMI correspondence; buildable Loft/Helix palette entries; LX completion; and Telos reopen/STEP export.
- An independent UI-only pass selected a Hole and read its authored fields, edited 10 mm to 12 mm in Inspector with zero rebuild diagnostics, saved/reopened the result, opened the schema Helix and Loft templates, observed derived Helix Height and defaulted Loft Twist, and stopped its temporary servers without code changes.
- Aetheris CLI `inspect` recognized the canonical Hole and `build` produced STEP for both 10 mm and 12 mm sources under ignored `artifacts/local/schema-x1/`. The compiled feature reports changed from cylindrical shaft radius 5 mm to 6 mm; both STEP files reimported successfully and have different SHA-256 hashes.
- Parallel test runs showed resource contention in the .NET solution. The first full parallel browser run also exposed three older ambiguous `Go to Source` test locators after field source links were added; those tests now target the Inspector action button, and the complete serial browser run passes.

## Limits

Only the canonical Hole Diameter mm literal has a direct edit operation. Expressions, aggregate vectors, selectors, strings, profiles, frames, omitted/default insertion, and arbitrary units remain source-only. Helix and Loft values are projected but not edited from Inspector. Concept requires a separate member-schema design before generic field projection is meaningful. Parser/schema duplication remains in syntax recognition and semantic cross-field checks. The Helix entry compiled in 51.3 seconds during the independent UI pass, leaving the page temporarily unresponsive; this is a remaining UX/performance gap. The Helix and Loft palette entries replace the current single source buffer and mark it unsaved. The static schema/projector/rewrite path has no reflection; the broader Web JSON host still emits existing IL2026 trimming warnings and WASM sqlite varargs warnings, so this release does not claim full Web NativeAOT cleanliness.
