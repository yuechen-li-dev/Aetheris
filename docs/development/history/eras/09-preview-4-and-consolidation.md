# Era 9: Preview 4 and consolidation

**1 October 2026 – present · about 100 commits so far · Telos, P4-*, STEP-LEGACY, BUILD-PERF, FIRMAMENT-FRONTEND-X0–X4, AST migrations, Helios authoring**

## In one paragraph

The current era turns breadth into a product. The display layer was rebuilt
(Three Telos, with GPU rendering of analytic fields), Cadmata shipped as a
portable Windows application in Preview 4, and imported-STEP robustness got a
long closeout. Then the focus moved to the language itself. A Claude authoring
pass on 13 October wrote twelve ordinary parts from the public docs and found
that the compiler sometimes accepted source and quietly did something else. The
response, the FIRMAMENT-FRONTEND series, moved every domain onto one
source-located syntax tree and made "unknown source is rejected" true almost
everywhere. Helios gained real authoring: a sketcher, an AI author and visual
pattern editing.

The working reports from this era are still in the repository, in
`docs/development/reports/2026-10/`, because the work is ongoing. This page
summarizes them.

## What was built

**Display and Preview 4 (1–10 October).**

- PRESENTATION-3D, SCENE-AUTHORING and ARCHVIZ-HOUSE: metre-scale scenes with
  rooms, openings, assemblies, cameras and presentation export.
- THREE-TELOS X0/X1: a shared engineering display layer that "uses Three.js where
  Three.js is useful and replaces it where engineering visualization needs
  stronger guarantees", with WebGPU, temporal anti-aliasing and Cadmata
  authoring overlays and PMI.
- CIR-DISPLAY and P4-01A: qualified primitives render directly from compiled
  analytic fields (FieldPass), with mesh fallback, shared programs per
  definition and consistent picking. A separate display host was an honest stop.
- STEP-LEGACY X0 to X3, surface materialization and trim triangulation: a long
  pass over awkward real-world STEP, including sphere orientation ambiguity, gear
  sleeves and quad-cell trim triangulation.
- **Preview 4 (10 October)**: Cadmata as a portable Windows ZIP with a bundled
  runtime, WebView2, STEP import with PMI and canonical STEP round trip.

**Performance.** BUILD-PERF M0 to M5 set a baseline and ratchet for the
edit-build-see loop, then cut the rebuild floor, refined invalidation, added a
binary bridge to the browser and removed kernel hotspots.

**Compiler honesty (13–16 October).**

- The **Claude authoring pass** wrote twelve parts from one-line briefs using
  only `docs/public`, then traced every failure to the parser. Several "built"
  parts were wrong: a profile ignored its `on XZ` plane, a documented schema did
  not exist, a fillet could not be combined with holes.
- **FIRMAMENT-FRONTEND-X0** reproduced the findings and closed the worst gaps:
  no more parse-failed-but-valid reports, implicit V1 fallback, unknown
  statements or fields accepted, or ignored layout frames. Its verdict was
  "meaningful progression; not accepted", because the universal invariant was not
  yet proved.
- **X1** replaced shadow parsing with production admission through one
  source-located AST, with an explicit disposition for every corpus file.
  **X2** migrated production semantics, and **X4** reran the Claude parts and
  repaired what still failed.
- Language passes in the same week: typed tolerances, compile-time values
  through Record, Template and Feature, Concepts and 2D construction, directive
  admission and casing, the lowercase-AST migration, mechanical and other domain
  front ends, `Options`/`match` (the compile-time choices RFC), assembly reuse,
  Inventory Records and modules, shared sheet-metal Profiles, and declarative V8
  motion with keyframes.
- **Closeouts (17–19 October)**: corpus modernization, piping and every
  remaining domain on the shared AST, body-state debt, convex stock and exact
  ellipsoids, ordinary-CAD fillet and chamfer robustness, native exact face edits
  and explicit subdivision sculpting, drawing generation, and qualified
  assertions and measurement.

**Helios authoring (19–21 October).** Export picker, measurement queries, a
profile sketcher that edits ordinary source, an AI author connected through
OpenAI (and later Claude and Gemini), engineering capabilities run as Dominatus
async actions, visual pattern editing, equation-driven curves, and imported-part
work: PMI on imported parts, a Recognize/Replace audit, proved through-hole moves
and removals, and pinned imported resources with dependency locks.

**Documentation (23 October).** This history, the public lessons and the split
language reference replaced the old monolithic syntax page and about seven
hundred milestone reports.

## Decisions that stuck (so far)

- **The source-intent invariant**: a successful build means every authored request
  was consumed. Silent acceptance is a bug, and the corpus records a disposition
  for every file.
- **One AST for every domain.** Domains keep their own semantics, but parsing,
  source spans and diagnostics are shared.
- **camelCase fields, PascalCase constructs, lowercase linking words**, with
  aliases for older spellings and a formatter that never renames user symbols.
- **Agent authoring passes are regression tests**: their specimens are preserved
  and their failures become fixtures.

## Claude's commentary

An earlier Claude session wrote the authoring pass that started the frontend
series, so read this with that in mind. What impressed me in the record is the response. The findings were
not argued with or patched one by one; they were reproduced, classified
("reproduced", "partially reproduced", "not reproduced") and turned into an
invariant with a corpus behind it. The X0 report even refused to call itself
accepted, because closing the reproduced cases did not yet prove the general rule.

The other notable thing is that the editor work and the language work converged.
Helios's sketcher and pattern editor edit ordinary Firmament source through the
same compiler, so every language repair made the editor more trustworthy, and
every editor feature found new places where the language was inconsistent.

## Where the originals are

The October reports are kept as the active working set in
[`docs/development/reports/2026-10/`](../../reports/2026-10/). Start with the
[language decision register](../../reports/2026-10/FIRMAMENT-LANGUAGE-DECISIONS-2026-10.md),
the [syntax review](../../reports/2026-10/FIRMAMENT-SYNTAX-REVIEW-2026-10.md) and
the [Claude authoring pass](../../reports/2026-10/firmament-authoring-pass-2026-10.md).
They will be summarized here and removed when this era closes.

Previous: [Era 8](08-domain-expansion.md) · Back to [the history](../README.md)
