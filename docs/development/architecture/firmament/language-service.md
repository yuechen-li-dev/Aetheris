# Firmament language service

Firmament language intelligence is owned by `Aetheris.Kernel.Firmament`.
`FirmamentLanguageService` supplies schema-backed completion;
`FirmamentLanguageAnalysisService` supplies token spans, parser diagnostics,
hover, bounded source definitions, and formatting. `Aetheris.Web.Runtime`
exposes these through `languageComplete`, `languageAnalyze`, `languageHover`,
`languageDefinition`, and `languageFormat`; the npm SDK forwards their contracts.
Helios runs language queries in a dedicated WebAssembly Worker separate from
geometry builds. Queries do not materialize BRep. Document identity, revision,
offsets, Monaco model version and cancellation guard asynchronous editor results.

## Completion and source navigation

Completion gets owners and value positions from the shared syntax tree, with
shared tokens recovering incomplete `field:` expressions while typing. Construct
schemas supply field descriptions, required fields, choices, preferred spellings,
and entry examples. Scope projection excludes unrelated construct families.
Fields already authored after the caret are excluded too; the current field is
replaceable across its complete identifier. Strings and comments suppress
completion. Lexical value symbols and scoped parameters supplement schema choices.

An entry's `Source` remains the complete reference example. `InsertText` unwraps
the declaration appropriate to the current owner; `Snippet` adds an editable name
tab stop. The client does not infer nesting or author a second catalog. Compiled
selector candidates still come from semantic identities, excluding RuntimeOnly
topology.

Hover gets field help from the owning schema and exposes local declaration and
parameter signatures. Definitions resolve unique lexical declaration names and
scoped parameters; the project overload searches the explicit source snapshot.
The SDK transports that snapshot and the Web adapter computes positions using the
target document. Helios opens the file only when Monaco requests navigation,
without changing files during hover or definition-provider lookup. This is bounded
source navigation, not type-aware member binding, rename, or find-all-references.

## Analysis and formatting

Live analysis uses the existing parser and admission owners, without geometry
builds. Thread field errors identify the field when it is unique; ambiguous or
unmapped admission failures retain their disclosed document-level anchor. Assembly
analysis remains project-aware but some diagnostics are still anchored at the
document header. Precise location coverage is not universal.

`Format` arranges part layout and prefers schema-owned vocabulary. It requires
parser admission before and after and preserves tokens, comments, numbers, unit
adjacency, authored names, and specialization syntax such as `Hole<Shaft>`.
Scene and Garment use their existing owners. Valid shared-syntax modules and
assemblies use conservative `FormatConventions` spelling changes with layout
preserved. Formatting does not prove successful project compilation; unsupported
or malformed source is refused. Helios reports the reason, applies accepted edits
as an undoable operation, and leaves format-on-build disabled.

Coverage lives in `FirmamentEditorExperienceTests`, the language-service and
semantic-schema suites, SDK transport tests, and Helios unit/browser tests.
Executable templates compile through the real geometry pipeline; five canonical
Learn parts retain identical STEP output after formatting. The current work report
records full-suite evidence and remaining boundaries.
