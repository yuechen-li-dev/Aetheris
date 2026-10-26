# IntelliDraft: mixed shape lists and visibility

## Changes

- Replaced the two-column shape cards with a searchable, independently scrollable
  list. Selection follows the canvas/inspector and scrolls its row into view
  without scrolling the whole sidebar. The list is also available during Trace.
- Each row has a Show checkbox. Hidden construction is removed from the canvas,
  picking and alignment targets. Selecting a hidden row does not reveal it;
  Show all restores visibility. These are local workspace display preferences,
  not source deletion or conditional engineering suppression. Existing Profiles,
  source and exports remain unchanged; visibility resets when the workspace closes.
- Added direct drawing and exact-field creation for Circle2, Ellipse2, Slot2 and
  RoundedRect2 alongside Rect2. They use the existing closed-boundary compiler
  owners and dimension-edit transactions. Vertical slots retain their orientation.
  Whole-outline tracing now accepts each projected closed shape.
- Promoted the existing lowerer's member map into construction inspection, so
  generated Curve/Path names project as authored shape members with original
  source provenance. No second field schema, expression evaluator or parser
  route was introduced. Existing rectangle expressions and advanced point/Path
  workflows remain supported.
- Moved the palette above the list, compacted shorter windows and reduced canvas
  name labels to the selected shape for large layouts. Full names remain in the
  list. Runtime failure details now survive the IntelliDraft error presenter.

## Ellipse display gap found through actual use

Ellipse profiles already compiled and exported exact geometry, but the browser
could not display their LinearExtrusion side surface. The retained draft exposed
this as a generic internal error. The diagnostic presenter now includes the
runtime's specific explanation.

The shared BRep display tessellator now handles an elliptic directrix by sampling
the exact surface through its existing periodic trim and trimmed-surface owners.
It does not modify the BRep, replace the surface with a spline or display an
untrimmed substitute. Missing/degenerate trims and empty triangle output reject
explicitly. Surface normals use the exact tangent crossed with the extrusion
vector, normalized before the existing canonical face-orientation projection.

A half-ellipse strip test verifies the actual trim, unit normals and unchanged
STEP bytes. The five shape tests also require a nonempty patch for every BRep
face. The partial strip must not become a full tube.

## Qualification evidence

- Manual UI dogfood: created a rectangle, circle and slot; selected rows; hid the
  circle; confirmed it disappeared; restored it with Show all. No Source editing.
- Browser tests draw each of the five shape types with the mouse, move it, trace
  its outline, extrude, build and export. A separate case verifies a vertically
  drawn slot. Exported STEP bounds, topology and analytic families are compared
  against independently authored fixtures under `fixtures/Regression/IntelliDraft/`.
- A 16-shape drawing verifies independent list scrolling, filtering, selection,
  per-row visibility and Show all. Hiding a shape and rebuilding preserves saved
  source byte-for-byte and matches the same reference STEP geometry.
- Existing point/line L, arc/chord D, ordered Path, whole rectangle, explicit
  rectangle dependency and independent-drag workflows remain in the browser lane.
- Focused compiler/sketch tests: 36 passed. Elliptic extrusion tests: 2 passed.
  Frontend unit suite: 161 passed. Production AOT SDK install and frontend build
  succeeded. Final full-solution and production-browser gate results follow below.

The first development-browser run found two failures: the legacy circle-span
test needed to explicitly open Manual guide trace after closed-circle tracing
became available, and ellipse display hit the missing surface dispatch described
above. Geometry assertions were retained; the latter was repaired in the shared
tessellator rather than hidden by the UI.

## Limits

This is a five-type closed-shape palette, plus existing advanced point/line/circle
guides and Paths. It does not add arbitrary intersections, freeform trimming or
a simultaneous constraint solver. Explicit expression-driven centers remain
protected from literal dragging.

The existing measurement query does not report analytic volume for the generic
full-circle/ellipse Profile extrusion lane. Exact BRep records and exported
geometry qualify these cases; no approximate volume is presented as exact.

Logs are under ignored `artifacts/local/intellidraft-shape-list-*` in Aetheris
and HeliosCAD. Browser source, STEP, comparison JSON and screenshots are under
`HeliosCAD/artifacts/local/intellidraft/`, including `shape-list-many.png`,
`shape-list-hidden.png` and each `draw-*-built.png`.

## Final gate

**Success.** Release solution build and production AOT/browser builds succeeded.
Fast Core lane: 1111 passed. Full serial solution regression: **6054 passed,
2 existing Drawing Notes skips**, across 20 projects. Frontend unit suite:
**161 passed**. Final production browser lane: **13 passed**, including the
selected-row scroll check, all five shape types, vertical slots and unchanged
source/export behavior after construction visibility toggles. `git diff --check`
passed in both repositories. Temporary dogfood hosts were stopped; no .NET
build/test or WebAssembly optimizer processes remained after qualification.
