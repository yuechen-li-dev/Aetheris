# Pattern authoring — binding repair and visual editor

Date: 2026-10-20. Baselines: Aetheris `ad6dc5ce`, HeliosCAD `f038b19`.

This report supersedes the current-status statements in the preserved
[original audit](PATTERN-AUTHORING-AUDIT-2026-10-20.md).

## Repairs

| Finding | Repair and ownership |
| --- | --- |
| P01: part/assembly Static admission mismatch | Point recipes consume the existing checked compile-time environment, including Record leaves and Template specializations. Both owners use the AST numeric evaluator. |
| P02: same-named Static captures Function parameter | Expression-bodied declarations receive a parameter scope; initializers bind in it. The existing value-substitution owner respects expression-body parameter spans. Assembly resolves point/law expressions before transporting remaining frame values. |
| P03: generated source names/provenance | Part Pattern sources and association sources expose `Layout.Sites`, matching assembly. Keyed point IR retains optional original collection/member spans and recipe kind. Existing generated identities are unchanged. Unmapped expanded locations remain unavailable rather than invented. |
| P04: generic point/law errors | The AST owner emits readable, source-located diagnostics for invalid expressions, units, bounds, unknown fields, duplicate keys, cycles and budgets. Assembly preserves the existing outer diagnostic code and now transports the inner explanation and original location when available. |
| P05: disconnected Pattern UI | Compiler-owned `language.patterns` projection and a Layout / Recipe / Instances editor reopen existing applications and create new ones. Preview/Apply uses the existing transaction and ModelSession lifecycle. |
| P06: incompatible Keys suggestion | Completion and inspector omit Keys for Series/Stations, while retaining Keys for equal-step Linear layouts. Completion checks actual AST ownership so an `over Points` reference cannot masquerade as a Points declaration. |

The point/law owner now reads unified AST declarations, fields, calls and spans.
It does not add a parser route, regex dispatcher, field schema or JavaScript
expression evaluator. Existing Set/Pattern construction expansion and domain
geometry owners remain authoritative. AST spans delimit the remaining transport
to those owners. Functions remain pure scalar expressions, with existing typed
units, acyclic dependencies and finite evaluation budgets.

## Editor behavior

- Existing Series, named Stations, explicit points and keyed Sets remain their
  authored forms. Keyed Set entries have distinct authored-value controls; their
  evaluated rows remain read-only. Expression/reference edits do not flatten
  values into lists.
- New applications can use an existing collection, a Point2 equation Series or
  named Point2 Stations. A Feature selector seeds named, typed argument bindings;
  the keyed mapping is editable Firmament source. A component selector reuses
  existing definition expressions; explicit published source/target frames and
  a coordinate law generate ordinary component/Placement source.
- Read-only rows show keys, compiler values and expansion associations. The gold
  point plot is local 2D data. It does not guess a world plane or implicit Point3
  lift. Point3 values retain their type and coordinates in the table.
- Shared consumers are listed. Changing first, keyPrefix or explicit keys warns
  about identity changes. Imported law/recipe definitions stay in their owning
  document; the panel edits one document per transaction.
- Includes resolve through the existing module graph. Preview compiles the
  complete project snapshot; it is the admission gate. The layout projection
  reports `LayoutResolvedNotBuilt`, not a built solid.
- The existing grid helper is separately named **Grid seed hole…**, preserving
  its original external seed and count semantics.
- Legacy domain Pattern forms remain supported. Forms without a qualified finite
  editor adapter have an explicit read-only explanation.

Typed tolerance evidence is not silently reduced to a nominal point input.
Unsupported tolerance-bearing layout consumers reject. Material-feature and
explicit PMI tolerance handling remains in its existing owner.

## Qualification

- 38 source specimens: 28 admitted and ten rejected, with validate/build/export
  agreement. Invalid cases retain explicit failures; none were made green by
  changing their requested geometry.
- Both shadowing witnesses produce Fret12 local `[0,-314]`mm and world
  `[0,344,62]`mm, identical occurrence transforms and leaf STEP hashes.
- Nonlinear plate: four holes at X = -30,-25,-10,15mm; exact volume
  39773.80532894153mm³ = 40000−72π; unchanged stock bounds. Pitch edits retain keys.
- Named station reorder retains each keyed coordinate. Growing assembly count
  retains prior identities; changing scale moves occurrences with unchanged
  fixed Pad leaf geometry. Point3 witness retains world Z = 64mm.
- Seven canonical controls passed: Record-array Hole/Slot, keyed Feature mapping,
  semantic Grid, radial symmetry, Scene and full guitar. Guitar's 56 leaf STEP
  hashes and occurrence transforms match the pre-repair audit baseline; 22 fret
  Parts, six crossings and nine inlay stations remain present.
- Browser dogfood: reopen/edit law and count, refuse excessive count, Cancel,
  Apply and single-step Undo/Redo; create/reopen a second Series Pattern and a
  named-Stations Pattern. Actual saved browser sources produce five and seven
  holes while preserving their earlier named holes.
- Helios: 145 tests passed in 34 files; TypeScript check and development-mode bundle
  passed. SDK transport/selection/measurement tests passed (19). Production AOT
  publishing and a packaged Cadmata desktop run are not claimed.

- Final Release solution build: zero errors (existing build warnings remain).
- Focused binding/point/Stations/language-service checks: 82 passed.
- Fast kernel lane: 1087 passed. Full serial solution gate: **5777 passed,
  zero failed/skipped across 20 projects with discovered tests**. FrictionLab.Tests
  reports no discovered tests and is not counted as a passing project.
- The completed documentation link check passed separately after the SDK API
  reference update.
- Actual browser Set edit retained First/Second keys. The browser-created assembly
  Pattern exported through normal AP242 build: 32 tree occurrences, including two
  added material components. All previous 27 occurrences retain their transforms,
  both leaf STEP hashes remain unchanged, and the new world origins are
  `[0,20,0]` and `[10,20,0]`mm. Component definitions are shared.
- Browser-created five/seven-hole parts have authoritative exact volumes
  39717.25666117692mm³ and 39604.159325647684mm³ respectively, matching stock minus
  five/seven diameter-3mm through holes.

The final 38-source CLI rerun and all seven canonical controls passed their
classified outcomes against the final build. The guitar still matches the frozen
audit baseline. No production AOT publish or packaged native desktop execution
is implied by the web/native compiler checks. An intermediate run found
an empty-Stations rejection missing during migration; that check was restored.
Another run read a report link before its new target had been written; the final
qualification runs against the completed documentation tree.

Evidence remains ignored under `artifacts/local/`: `pattern-*-*.log`, the
`pattern-authoring-audit/` CLI JSON/STEP results, saved `pattern-editor-*.firmament`
and screenshots. Durable source specimens live in
`fixtures/Regression/PatternAuthoringAudit/`; regression tests are in
`FirmamentPatternBindingTests`, `ConceptPointAuthoringTests` and
`LinearStationAuthoringTests`.

[Pattern instance view](../../artifacts/local/pattern-editor-stations.png),
[assembly recipe view](../../artifacts/local/pattern-editor-assembly.png).
Temporary browser tabs, preview ModelSessions and task-owned Vite hosts were
closed. No .NET/testhost/MSBuild/compiler-server processes remained at the end.
A [review-only cleanup script](../../artifacts/local/pattern-cleanup-downloads.ps1)
can remove unchanged test downloads. No test downloads were deleted by this task.

The code-quality pass also restored rejection of empty Stations and rejects
unconsumed placement/contract headers on point-collection owners. The AST reader
must not broaden recognition and then erase a requested frame. This restriction
is local to Points recipes; geometric Concept Struct frames retain their owner.

## Deliberate limits

No new trigonometric library, implicit global Function capture, named/default
Function arguments, sparse-exclusion language, general loop runtime or constraint
solver is introduced. The initial new-layout controls author Point2; existing
Point3 layouts can be edited and mapped. Component frames and coordinate laws remain explicit source expressions; the
editor does not add another mate/placement solver. A layout's
local plot is deliberately separate from physical world placement.


## Verdict

**Success.** The reproduced parameter capture and part/assembly binding mismatch
are repaired through the existing AST/value owners. Patterns can be authored and
edited without baking their laws or keyed layouts, and actual compiler Preview
admits their geometry before one source transaction is applied. Geometry,
identity and provenance controls pass, including the original wrong-geometry
witness and frozen guitar comparison.

No user decision is required for this pass. The bounded limits above remain
explicit; this does not claim a broader math library, implicit frame inference,
Template-specialization debugger or a new geometry capability.
