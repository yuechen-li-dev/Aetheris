# IntelliDraft: retained construction shapes

IntelliDraft now leads with **Shapes → Trace → Part**. The motivating L retains
two `Rect2` constructions and a shared wall input. Selecting six boundary spans
produces a Profile without authoring six points or six line declarations. The
rectangles supply their intrinsic right angles and parallel, equal opposite
sides; ordinary compile-time expressions supply the dependence between them.

## Changes and ownership

- The compiler projects retained shapes, writable driving dimensions, derived
  expressions and selectable spans between existing named landmarks. The UI
  does not solve constraints, evaluate source expressions or create intersection
  coordinates. Advanced points, lines, circles and ordered Paths remain available.
- A related-rectangle preset creates a top-right aligned inset in the same
  Concept Struct. Width, height and center remain expressions of the reference
  dimensions and a shared wall or separate insets. Derived fields are read-only;
  independent inputs use explicit Apply/Discard transactions.
- Trace selection preserves named references and stable edge aliases. Connected
  spans are oriented by the existing compiler geometry owner. Existing Profiles
  can be retraced while retaining the material consumer's Profile reference.
- Construction inspection and mechanical profile adaptation now carry the
  existing compile-time binding environment. Previously, a correctly resolved
  rectangle expression could lose its bindings at the public Profile façade or
  Model-stripping adapter and fail normal compilation.
- Public profile/sketcher documentation and the Helios IntelliDraft guide now
  lead with retained rectangles. The SDK exposes additive shape/span metadata
  and source-edit operations through its existing transport.

## Dogfood findings

The manual production-browser run created a Layout, the outer rectangle, the
related rectangle, six selected spans, the Profile and a 6 mm extrusion, then
committed a solid showing **Ready / No problems**, without opening Source.

Actual browser use exposed and corrected inspector scroll position after tool
changes, competing pending dimension forms, overlapping landmark labels and
horizontal/vertical SVG targets with zero-area accessibility bounds. Trace
selection stays visible when switching rectangles. A final visual check caught
a browser focus outline that scaled into a thick black bar; SVG targets now use
a thin, non-scaling focus highlight.

## Evidence

- `fixtures/Regression/IntelliDraft/rectangles-l.firmament`: bounds
  `(-30,-20,0)..(30,20,6)` mm, volume 4416 mm³, 8 faces / 18 edges / 12 vertices.
- `rectangles-l-revised.firmament`: width 80 mm and wall 10 mm, bounds
  `(-40,-20,0)..(40,20,6)` mm, volume 6600 mm³, unchanged topology counts.
- Compiler tests verify retained `Rect2` source, no authored Point2/Line2 outline,
  dependent dimensions after revision, stable curve identities, rejection of
  an oversized wall without candidate mutation, retracing and an XZ world-bounds
  witness. Expanded focused lane: **93 passed**.
- Full Release solution build succeeded. Fast Core lane: **1110 passed**. Full
  serial solution regression: **6046 passed, 2 existing Drawing Notes skips**.
  An initial full run caught nine regressions in an attempted façade
  consolidation. Restoring the existing Span and open-Path inspection contracts,
  while retaining the shared binder fix, closed them without relaxing tests.
- Helios unit suite: **157 passed**. Production AOT SDK install and production
  frontend build succeeded. All **5 production browser workflows passed** after
  the final SVG focus refinement. Browser qualification covers the retained-rectangle
  L, width/wall revisions, invalid-wall refusal, whole-rectangle TraceLoop, and
  the existing point, arc/chord and ordered-Path workflows. Each exported STEP
  is compared against an independently built fixture for bounds, topology and
  analytic surface/curve families; source is validated through the actual CLI.
- The first production publish referenced a removed .NET 10.0.13 runtime file
  after native compilation. A fresh-host retry using the installed 10.0.14
  runtime succeeded; no runtime pin or source workaround was introduced.

Logs are under ignored `artifacts/local/intellidraft-shapes/` in Aetheris and
`artifacts/local/intellidraft-shapes-*` in HeliosCAD. Generated browser source,
STEP, comparison JSON and screenshots are under
`HeliosCAD/artifacts/local/intellidraft/`.

## Boundaries and verdict

**Success for the retained-rectangle workflow.** This is a constructive editor,
not a simultaneous constraint solver. The current relationship preset requires
unrotated rectangles in one Concept Struct and shares the top-right corner.
Arbitrary intersection creation, general geometric snaps, arbitrary trim/extend,
and general multi-loop drafting remain outside this pass. Existing code-authored
geometry continues through its normal compiler owners.
