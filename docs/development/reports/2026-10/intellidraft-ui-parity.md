# IntelliDraft: construction-to-part UI qualification

Date: 2026-10-25. Verdict: **Success for the bounded construction workflows below.** This is UI-only authoring evidence, not a claim that a fresh human usability study has taken place.

## What was missing

The former Sketch workspace could resize closed shapes and add circular openings. Its new-part action immediately created a rectangle, Profile and extrusion. It had no controls for named Point2/Line2 construction, circle spans, ordered Path steps or assembling a trace. It therefore could not reproduce the basic code-first construction workflow.

Three reference programs were authored and validated before UI implementation:

| Reference | Construction | Qualified STEP |
|---|---|---|
| [L plate](../../../../fixtures/Regression/IntelliDraft/named-lines-l.firmament) | Six named points, six Line2 guides, ordered outer trace | 8 faces, 18 edges, 12 vertices; bounds (0,0,0)–(60,40,6) mm; nominal volume 4,416 mm³ |
| [D plate](../../../../fixtures/Regression/IntelliDraft/arc-chord-d.firmament) | Circle2 span between named points, plus chord | 4 faces, 6 edges, 4 vertices; bounds (0,-15,0)–(15,15,6) mm; exact semicircular cylinder wall |
| [Rounded outline](../../../../fixtures/Regression/IntelliDraft/line-arc-path.firmament) | Named Line/Arc/Close Path steps, then Profile from Path | 7 faces, 15 edges, 10 vertices; bounds (0,0,0)–(40,30,2) mm; exact quarter-circle cylinder wall |

Reported nominal coordinates above omit insignificant floating-point residuals. The source specimens are unchanged comparison references, not fixtures rewritten to match UI output.

## Repairs and ownership

- `FirmamentIntelliDraft` prepares bounded source operations through the existing structural writer: layout, point, line, circle, Path, step, trace, extrusion, point move and literal dimension edit.
- `ProfileAuthoringParser.InspectConstruction` queries the existing point/guide/Path binders. No second geometry evaluator, constraint solver, parser route, whole-document rewrite or field schema was introduced.
- `FirmamentSketchService` exposes an additive `construction` projection and edit request through the existing browser SDK transport. Normal Profile projection and closed-shape dimension edits remain compatible.
- IntelliDraft in Helios stages source text locally. Open construction is explicitly not a built part. Rejected edits retain the preceding candidate. A complete candidate must pass the real compiler before commit; temporary model handles are disposed. Source revision checks protect newer work.
- Existing-part updates use the normal graph transaction and undo path. New-part replacement retains the existing unsaved-work decision. The old closed-shape dimension workspace remains available through Sketch.
- Compiler curves retain stable guide names, authored object spans and resolver provenance. Point moves update their declaration while Line2 references continue to name those points. Driven expressions are not flattened into coordinates.

The structural writer exposed a real separator issue when appending an extrusion after a bodyless `Profile ... from ...` declaration. IntelliDraft emits the explicit separator; the structural parser still validates the resulting source.

## Friction observed while operating the UI

1. The old viewport-sized overlay was too narrow for both controls and construction. IntelliDraft now uses a larger workspace.
2. Underlying controls remained exposed to accessibility navigation. A native modal dialog now confines interaction and focus; a manual accessibility inspection contains only the IntelliDraft controls. The browser test also checks native `:modal` state. DOM automation scopes selectors to the dialog; a DOM role query is not an accessibility-tree test.
3. Parser category order made guide choices surprising. The UI now presents authored object order.
4. Sequential line and trace entry required repetitive picks. Defaults advance to the preceding endpoint and next guide. Names and explicit reference choices remain editable.
5. Construction vanished after tracing. Gold guides remain visible behind the dark material boundary.
6. Coincident semantic endpoint aliases produced overlapping labels. Their display labels are grouped, with all names retained in the tooltip and source.
7. Path step entry showed a redundant form and offered appending after Close. The step form is now separate, and invalid closed/empty Path actions are disabled.
8. Upstream point and literal dimension changes were unavailable. They are now exposed in the object list, with staged Undo/Redo and full-build admission.

The L, D and rounded Path were also operated manually through browser controls without editing source or injecting geometry. The Path run changed a driving length, undid it, traced and extruded the outline.

## Qualification evidence

- Full Release solution build: passed.
- Fast kernel lane: 1,110 passed.
- Full serial .NET regression: **6,043 passed, 2 existing Drawing Notes renderer skips, 0 failed**.
- Focused sketch/construction lane: 26 passed. Four new construction tests cover source behavior, refused edits, analytic geometry parity, upstream changes and XZ placement.
- Helios: **156 unit tests passed**; production TypeScript/Vite build passed.
- SDK transport tests: 19 passed. Telos tests: 19 passed.
- Production AOT browser qualification: **3 passed**. Each part is authored through controls, built, exported to STEP and compared with its code reference using the real CLI. Checks cover manifold status, body/shell/face/edge/vertex counts, bounds and analytic curve/surface families. No spline surfaces appear in these parts.
- Independent backend comparisons import the emitted STEP and compare complete analytic curve/surface records and vertex coordinates against the references.
- XZ witness: the asymmetric L has world bounds (0,-6,0)–(60,0,40) mm, rather than the XY variant. Normal is world -Y.
- Broken trace, negative dimension and invalid name cases reject without changing the preceding source. The browser test additionally verifies a rejected trace, point edit, Undo/Redo and re-opening the constructed part.
- No engineering materializer or exporter behavior was changed. Full existing corpus regressions passed.

Native/CLI logs are under `Aetheris/artifacts/local/intellidraft/`. Browser-authored source, STEP, reference STEP, structured CLI analyses and screenshots are under `HeliosCAD/artifacts/local/intellidraft/`. Production build and browser logs are under `HeliosCAD/artifacts/local/intellidraft-*.log`.

The isolated browser suite does not start Leviathan. Its optional AI-status requests log connection refusals; AI is not used for the modeling or qualification. The exact CLI volume query still rejects curved trimmed shells, so curved cases are qualified by their analytic geometry, topology and bounds rather than claiming a volume result that is unavailable.

## Remaining limits

This UI creates one authored Model with unambiguous construction names, principal-plane layouts, named Point2/Line2/Circle2 guides, XY line/arc Paths, one outer trace and a positive extrusion. It is not UI parity with every existing Profile language feature.

General semantic snaps, expression builders, new inner-loop assembly, boundary edits, arbitrary trim/extend and freeform curve authoring remain outside this pass. Existing code capabilities and the prior shape dimension/opening editor remain available. Included/generated construction is not made writable by flattening expanded geometry.

No new kernel capability or parser migration is required for the qualified workflows. The next ergonomic expansion should reuse the same compiler query/edit seam for named snaps and further boundary operations, rather than introducing a browser-side sketch model.
