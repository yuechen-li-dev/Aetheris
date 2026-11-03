# Language-service editor polish — 2026-11-02

## Changes

Extended the existing shared-syntax/schema projection rather than adding another
Firmament parser. Completion now distinguishes owners, fields, values, comments
and strings; covers registered owner fields; supplies scoped local symbols;
replaces complete partial identifiers; and prepares context-specific insertions
and name-tab-stop snippets. Existing complete entry examples retain their API
meaning. Added metadata for Model, Modify, Cylinder, Cone, Frustum, Sphere, Torus
and RoundedBox, all backed by existing compiler behavior.

Updated keyword classification, owner-specific hover and bounded parameter and
Feature navigation. Web/SDK definition requests retain explicit project snapshots
and locate targets using their own document text. Thread invalid-field messages
are readable and precisely located when unambiguous. Formatter preserves unit
adjacency and specialization; valid shared-syntax modules and assemblies dispatch
to the conservative spelling pass.

Helios uses an isolated language Worker, guarded Monaco providers and standard
format/find/comment/problem/snippet contributions. Its source bridge handles stale
React updates and document EOL normalization. The sibling HeliosCAD report
`docs/development/reports/2026-11/language-polish.md` covers UI evidence.

## Evidence

- Release full solution build: 0 warnings, 0 errors.
- Core fast lane: 1,168 passed.
- Full solution: 6,371 passed, 0 failed, 2 existing Drawing tests skipped across
  20 test assemblies. Firmament: 3,393 passed.
- Focused language/schema/scene-authoring feedback: 98 passed.
- SDK transport: 8 passed. Helios units: 238 passed. Language browser cases:
  6 passed, plus three successful repeated fast-snippet workflows.
- `aetheris validate fixtures/Language/Editor/parameterized-plate.firmament --json`
  reports valid. Six primitive templates compile, and formatting five canonical
  Learn parts plus the Feature fixture preserves exact STEP output.

Logs are ignored `artifacts/local/language-polish-*.log` and
`language-polish-cli-validation.json`. Initial full-suite failures were old Scene
completion and Hole-field expectations; updated them to assert scope-aware
completion and suppression of already-authored fields, then reran the complete
suite successfully. Public style and architecture docs match the current service.

## Remaining boundaries

Definition lookup is lexical and unique-name based, with bounded scoped parameter
support; it is not a general binding/rename service. Some admission diagnostics
still disclose document-level anchors. Conservative formatting does not validate
an imported module's consuming project. Production AOT publish was not rerun.

The UI audit isolated an existing included-Feature compiler failure: splitting
`fixtures/Language/Editor/parameterized-plate.firmament` before `Model editorPart`,
placing the prefix in `parts/library.firmament`, and compiling an include plus the
Model reports Web SDK `internal-error` with details
`ArgumentOutOfRange_IndexLength Arg_ParamName_Name, length`. The same source in one
file validates/builds. Included Learn 06 assembly definitions compile and navigate
correctly. File-local Feature semantics were not broadened in this editor pass;
the sibling Helios ignored `artifacts/local/language-polish/project-audit.*` retain
the exact reproduction. This is a compiler diagnostic/provenance follow-up.
