# IntelliDraft and Profile dimensions in Helios

The sketch workspace edits ordinary Firmament source. There is no separate saved
sketch model, browser constraint solver or mesh-to-profile conversion.

## Construction, trace, part

**IntelliDraft** in the viewport toolbar authors the same retained construction
used in Firmament code. A new draft opens ready to draw: the first shape creates
its layout (a Concept Struct on the plane chosen in the toolbar, XY by default).
`Rect2` supplies right angles and equal, parallel opposite sides. Its type and
dimensions remain authoritative when Trace selects boundary spans from its named
edges and corners.

The toolbar draws lines, arcs, rectangles, circles, ellipses, slots and rounded
rectangles. Keys: V select, L line, A arc, R rectangle, C circle, E ellipse,
S slot, U rounded, T trace, F fit, Delete removes the selection, Ctrl+Z / Ctrl+Y
undo and redo, Escape steps back. A tool stays active after drawing and proposes
the next free name (`rect1`, `rect2`, …). Points snap to a grid that follows the
zoom; Alt snaps to 0.1 mm.

- **Line** writes a `Line2` between named points. Ends that land on an existing
  named point (a rectangle corner, another line's end) reuse it; new ends are
  written as `Point2 <line>Start` / `<line>End`. The line menu also declares an
  `Axis2 { origin; direction }`: an unbounded reference direction that is shown
  on the canvas but never traced.
- **Arc** drags from the centre to the start, then clicks the end (15° steps;
  Alt is free). It writes a `Circle2` guide centred on a named point plus the
  named points on it. The arc itself is chosen when tracing: each pair of
  neighbouring points on a circle is a counter-clockwise span.
- The rounded menu also draws `SmoothRoundedRect2` (continuous-curvature
  corners) with its `cornerExtent`.

Selected shapes show their dimensions on the canvas; click one to type a value.
Values driven by an expression are dashed and read-only. Resizing a shape whose
edge is lined up with another shape's edge asks once whether to move that shape
too; nothing is stored, and drags never ask. Rename and Delete act on the selected
shape, line, arc or axis. A shape that is part of the traced outline is not
deleted until the outline is retraced without it.

The sidebar's searchable shape list selects retained construction. Its checkboxes
show or hide construction in the workspace; hidden shapes are excluded from
picking and alignment, but their source and any traced Profile remain intact.
Temporary alignment guides help placement while dragging; they do not link
shapes. Hold Alt to bypass alignment.

Choose **Trace** and click the edges of the outline on the canvas: rectangle
sides, lines and arcs between named points, across shapes. Click order and
direction do not matter; the outline is written counter-clockwise. The sidebar
shows whether the outline is closed. **Use this outline** writes the Profile;
**Use whole shape outline** traces one closed shape. Picks are also available
as a list under **Edges as a list**.

Explicit dimension-driven relationships remain optional under **Advanced**.
The top-right inset preset intentionally creates expressions and shared inputs;
it is not the default placement behavior. Existing expressions remain read-only
at their derived controls and are edited through their inputs, never flattened
by a drag.

Named **Point**, **Line**, **Circle** guides and Paths remain under **Advanced**,
with a manual guide trace. Alternatively, create a **Path**, then append named
Line or Arc steps with lengths, radii and signed turns. **Close Path with line**
explicitly adds a closing line. **Trace this Path** resolves that boundary to a
Profile. Trace's **Close** checks an already closed boundary and never supplies a
missing edge.

Complete examples:

- [L plate from independently placed rectangles](../../../fixtures/Regression/IntelliDraft/independent-rectangles-l.firmament).
- [L plate from explicitly related rectangles](../../../fixtures/Regression/IntelliDraft/rectangles-l.firmament)
  and [its dimension-driven revision](../../../fixtures/Regression/IntelliDraft/rectangles-l-revised.firmament).
- [Low-level L plate from named points and lines](../../../fixtures/Regression/IntelliDraft/named-lines-l.firmament).
- [D plate from a circle span and chord](../../../fixtures/Regression/IntelliDraft/arc-chord-d.firmament).
- [Rounded outline from ordered Path steps](../../../fixtures/Regression/IntelliDraft/line-arc-path.firmament).

Choose an extrusion depth, **Extrude Profile**, then **Build and use part**.
Incomplete construction is staged source, not a valid engineering part. Compiler
rejection, including a move that breaks an existing Profile, retains the preceding candidate and the current built model. Undo and
Redo navigate staged edits; Cancel discards them. A complete candidate must pass
the normal build before replacing the current document. Updating construction
in an existing part is one normal undoable source transaction.

The advanced objects list edits named points and literal driving dimensions. Point moves
retain connected Line2 references. Expression-driven positions and dimensions
are not replaced with coordinates. Canvas point placement rounds to a 0.1 mm
grid; explicit typed values can use finer precision. Paths created by the UI use
XY; the layout frame banner identifies local axes and the world-space normal.

## Closed-shape dimension workspace

Open **Command palette → New sketch part** to start a rectangular profile and a
6 mm extrusion. Circle, slot and rounded-rectangle sketch starters are also in the
command palette. For an existing part, use **Sketch** above the viewport, or
double-click its Profile/shape node in the graph. Choose the Profile when a guide
is shared by more than one Profile.

## Dimensions and navigation

The workspace uses the Profile's local coordinates. Its header identifies the
resolved plane, world origin and normal; the canvas shows local +X and +Y.
Scroll to zoom, middle-drag or Alt-drag to pan, and use **Fit** to see the outline.
Select a named shape in the inspector or click its center marker.

Type a dimension in the inspector and press Enter or leave the field. Drag a
gold dimension label for an approximate adjustment; type a number for the final
size. Keyboard arrows on a dimension label change it by one displayed unit.
Lengths display in millimetres and rotations in degrees. Slot **overall length**
includes both semicircular ends.

- A literal is a directly editable driving dimension.
- A directly referenced literal `Static` is edited at its declaration. The UI
  names that shared parameter, so other consumers changing is explicit.
- An expression is read-only here. **Edit source** navigates to it; edit its
  driving inputs in Parameters or Source. Dragging never replaces a formula or
  typed tolerance with a literal.

Drag previews resolve the Profile without constructing the solid. Releasing a
drag or entering a dimension prepares one source transaction and invokes the
normal engineering build. An invalid profile edit is refused without changing
source. A later solid-build failure retains the previous geometry and the
existing revertible draft. Source edits retain Monaco undo history.

`ProfileResolvedNotBuilt` describes the preview's validation level. It does not
mean an extrusion, revolve, loft or downstream feature has passed its build.

## Mounting openings that follow their dimensions

For a Profile whose explicit outer loop traces an unrotated Model-level Rect2,
**Circular profile opening** offers its center, corners and edge midpoints.
Choose the rectangle, anchor, inward X/Y insets and opening diameter, then
**Add opening**. Inapplicable insets are disabled. For a four-hole plate, add one
opening at each corner with the same insets.

The operation shares the rectangle's driving values through named `Static`
parameters. Literals/expressions are promoted when necessary; existing named
inputs are reused. For example, the resulting relationship can read:

```firmament
Static OutlineCenterX: Length = 0mm
Static OutlineCenterY: Length = 0mm
Static OutlineWidth: Length = 80mm
Static OutlineHeight: Length = 50mm
Rect2 Outline {
  center: [OutlineCenterX, OutlineCenterY]
  size: [OutlineWidth, OutlineHeight]
}
Circle2 MountOpening {
  center: [OutlineCenterX + (OutlineWidth / 2 - 8mm),
           OutlineCenterY + (OutlineHeight / 2 - 8mm)]
  diameter: 4mm
}
```

This is dimensional dependence through existing compile-time values. It does
not introduce an `Outline.Right.Offset(...)` expression or store sampled edge
coordinates. The opening becomes an explicitly named inner Profile loop. It is
a profile opening, rather than a drilled Hole feature with drill-point or
counterbore semantics. The compiler rejects overlapping/touching circular
openings and openings outside the rectangle. Complex inner-loop clearance and
other reference families are not offered yet.

## Planes

Local 2D Concept layouts admit these right-handed principal frames when consumed
by `Profile ... using Layout`:

| Layout | Local +X | Local +Y | Extrusion +normal |
| --- | --- | --- | --- |
| `on XY` | world +X | world +Y | world +Z |
| `on XZ` | world +X | world +Z | world -Y |
| `on YZ` | world +Y | world +Z | world +X |

Omitting `on` retains XY. Arbitrary origins/orientations use a traced
Construction Plane. A principal-plane layout is a frame for local 2D guides and
scalar data; it does not implicitly rotate typed 3D semantic members or override
the admission rules of Sheet Metal, Assembly mates or other consumers.

Extrude uses that resolved frame. Compose inherits its Base Profile frame and
requires all authored Base/Add/Remove Profiles to share it. Legacy explicit
Placement can transport unplaced XY Profiles; it cannot override a different
oriented Profile. Oriented Compose with Boss/Pocket/Hole/Slot/EdgeFinish is
explicitly rejected while those support adapters still assume world XY.

## Current boundary

The **Sketch** dimension workspace edits authored Rect2, Circle2, Ellipse2, Slot2, RoundedRect2 and SmoothRoundedRect2 fields
and displays the resolved line/arc/ellipse/cubic curves of other Profiles.
Generated Template/Feature bodies do not become writable source through their
expanded geometry. Profiles must have an unambiguous authored name in the
document; edits do not reach into included files.

IntelliDraft currently authors one unambiguous Model, retained closed shapes,
named Point2/Line2/Circle2 guides, Axis2 references, line/arc Paths, one outer
trace and a positive extrusion. Strokes snap to named points, not to arbitrary
intersections; an arc's circle stays a full construction circle. The optional Advanced related-rectangle preset shares a top-right corner with
an unrotated rectangle in the same Concept Struct. Its wall/inset values and the
rectangle dimensions remain compile-time source expressions. Trace selection
uses named landmarks on the compiler's bounded guides; it does not invent
arbitrary intersection points. Soft alignment currently uses shape bounds in
the same layout. General intersection/point snaps, arbitrary one-way geometric references, boundary-edit tools, arbitrary
trim/extend and simultaneous constraint solving are not implemented. Existing code-authored geometry
continues to use its normal compiler owners.
