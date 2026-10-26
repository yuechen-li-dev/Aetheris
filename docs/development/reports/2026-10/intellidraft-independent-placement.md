# IntelliDraft: independent drawing and soft alignment

## Change

The primary workflow is now **draw rectangle → enter exact dimensions → drag
to place → repeat → trace → extrude**. Each rectangle retains its own placement
and dimension inputs. A rectangle's intrinsic geometry supplies its right angles
and parallel, equal opposite sides. Different rectangles do not automatically
acquire persistent relationships.

The canvas offers temporary edge/center alignment guides within seven screen
pixels, on both axes. This permits all four corner alignments and center
alignment. Releasing a drag submits the selected shape's center coordinates to
the existing compiler dimension-edit operation. Alt bypasses alignment. Escape
and pointer cancellation abandon the preview. Undo/Redo use the existing staged
source history. Fit, zoom buttons and middle-drag control the view without editing
source; the camera stays stable during placement.

Independent drawing and unsnapped dragging use 0.1 mm authoring increments.
Alignment uses projected target coordinates, and numeric fields admit finer
precision. This is an authoring resolution, not a manufacturing tolerance or a
change to kernel mathematics.

Explicit dimension-driven inset relationships remain under **Advanced** for
legitimate existing parametric source. They are never introduced by snapping.
Expression-driven placements remain protected against literal replacement.

## Ownership and limits

Production compiler, SDK transport and engineering geometry code are unchanged.
The browser samples already resolved construction for display and hit testing;
its alignment helper chooses a disposable coordinate offset, not an engineering
constraint. It does not evaluate Firmament expressions. Draw and drag commit
through the existing compiler-owned source transactions and preflight checks.

An edit that breaks an existing traced Profile is refused, preserving the last
accepted candidate and reporting the compiler diagnostic. No neighboring shape
is moved and no missing boundary is manufactured. The open construction stage
remains free to position shapes before tracing.

Direct pointer drawing currently creates rectangles. Existing point/line/circle
guides and ordered line/arc Paths remain available under Advanced. Movement
requires independently writable projected center controls. Alignment considers
projected bounds of shapes in the same layout. This pass does not add arbitrary
intersection creation, curve editing, simultaneous constraints or inverse
expression solving.

## Qualification

- Manual production-browser dogfood: drew two rectangles, sized them to
  60 × 40 and 52 × 32 mm, dragged the second into alignment, confirmed center
  (4,4) mm through the visible fields, selected six spans and built a 6 mm
  extrusion. The UI showed **Ready / No problems** without opening Source.
- Added `fixtures/Regression/IntelliDraft/independent-rectangles-l.firmament`.
  The UI-generated source retains two Rect2s and separate center/size parameters,
  with no Point2/Line2 outline or implicit expression relationship.
- Production browser tests exercise drawing, exact sizing, dragging to every
  corner alignment, disappearing guides, Alt bypass, Escape cancellation and
  Undo. Moving the envelope to (7,3) mm leaves the notch at (4,4) mm. Its restored
  source then traces and builds normally.
- Exported STEP matches the independently built reference: one enclosed
  manifold body, 8 faces / 18 edges / 12 vertices, bounds
  (-30,-20,0)..(30,20,6) mm, planar surfaces and analytic curves. CLI validation
  reports `valid`. Compiler tests compare resolved geometry and prove that
  moving/resizing one independent rectangle does not change the other. An
  invalid later Profile placement is rejected without source mutation.
- Focused IntelliDraft compiler tests: **8 passed**. Release solution build:
  succeeded. Fast Core tests: **1110 passed**. Full serial solution regression:
  **6047 passed, 2 existing Drawing Notes skips** across 20 projects.
- Frontend unit suite: **159 passed**. Production frontend build succeeded;
  **6 production browser workflows passed**, including the existing explicit
  relationship, point, arc/chord, whole-rectangle and ordered-Path workflows.

Code review confirmed no new parser route, schema, source-expression evaluator,
global mutable geometry state or hidden persistent snapping relationship. Public
IntelliDraft/profile documentation now leads with independent placement and
distinguishes optional explicit expressions from UI alignment.

Logs: ignored `Aetheris/artifacts/local/intellidraft-placement-*` and
`HeliosCAD/artifacts/local/intellidraft-placement-*`. Generated source, STEP,
comparison JSON and screenshots: `HeliosCAD/artifacts/local/intellidraft/`,
including `soft-alignment-guides.png` and `independent-rectangles-l-built.png`.

## Verdict

**Success for independent rectangle drawing, exact sizing, soft placement and
tracing.** The top-right dependency preset is no longer the primary construction
model. The full kernel suite and actual browser/export path remain qualified.
