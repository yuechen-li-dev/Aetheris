# Profiles and 2D construction

Most parts start as a closed 2D boundary. The pattern is always the same:

1. declare **construction geometry** (guides) that creates no material;
2. trace guides into a **Profile**, closed loops with an inside;
3. give the Profile to a **material consumer** such as `Extrude`, `Compose`,
   `Revolve` or a sheet-metal `Base`.

There is no sketch solver. Positions and sizes are stated, so the boundary is
exactly what the numbers say.

Helios's [IntelliDraft workspace](../../firmament/sketcher.md) follows this same
construction → trace → material workflow. It writes named points, guides and
Path steps into ordinary Firmament source and uses the compiler to resolve them.

## One-line Profiles

A single closed shape can be a Profile directly:

```firmament
Model Plate {
  units: mm
  Profile outline = Rect2 { center: [0mm, 0mm]; size: [80mm, 50mm] }
  Extrude stock { profile: outline; from: 0mm; to: 2mm }
}
```

This uses the same fields and the same exact boundary as a named `Rect2`,
`Circle2`, `Slot2` or other closed shape, including variant shapes such as
`Profile hex = RegularPolygon2<6> { center: [0mm, 0mm]; acrossFlats: 24mm }`.
`Profile copy = outline` derives an immutable copy of a named Profile or closed
shape.

A Path's final `Close name` step draws the straight edge back to the start. If
the steps before it already end at the start (for example, the outline ends with
an arc into its start point), `Close` adds no edge and only marks the Path
closed. In that case it names no edge either.

[Sheet metal](../../firmament/sheet-metal.md) consumes the same named Profiles
for its `Base` and `Cut` operations, within its own limits (a rectangular base,
line and arc cuts).

## Layouts and loops

Use the loop form when a Profile has inner loops, combines several guides, or
needs named edges:

For a shape-driven example, [this independently placed L plate](../../../../fixtures/Regression/IntelliDraft/independent-rectangles-l.firmament)
retains two `Rect2` declarations with their own centers and dimensions. Their
placement makes named corners available for tracing; it does not impose a
persistent relationship between the rectangles. A closed Profile still requires
the selected spans to meet.

For optional explicit dependencies, [this L plate](../../../../fixtures/Regression/IntelliDraft/rectangles-l.firmament)
retains two `Rect2` declarations: an envelope and an inset rectangle whose top
and right edges follow it through shared dimensions. The Profile traces selected
edge spans between their named corners. Changing the envelope or shared wall
input updates the construction and boundary together, without independently
moving six vertices. [Its revision](../../../../fixtures/Regression/IntelliDraft/rectangles-l-revised.firmament)
changes only driving dimensions.

```firmament
Model ProfilePlate {
  units: mm

  Concept Struct layout on XY {
    Rect2 boundary { center: [0mm, 0mm]; size: [30mm, 24mm] }
  }

  Profile outline using layout {
    Loop Outer {
      boundary.Bottom as south
      |> boundary.Right as east
      |> boundary.Top as north
      |> boundary.Left as west
      |> Close
    }
  }

  Extrude stock { profile: outline; from: 0mm; to: 12mm }
}
```

`Loop Outer` is the outside boundary; additional loops such as `Loop Inner` are
holes. [Lesson 2](../../learn/02-profiles.md) shows an outer
polygon with an inner circle.

### Tracing with `|>`

`|>` joins guide segments in source order:

- The first stage sets its own direction; each later stage orients itself to
  continue from the current endpoint. A gap, or a continuation that could go two
  ways, is an error.
- `Reverse` asks for the opposite direction, falling back to whichever direction
  connects after the first stage.
- `Close` checks that the loop closes. It never adds a missing segment.
- `as name` names the resulting edge. Edge names are unique across the whole
  Profile, not per loop; use `as` to tell apart edges that would otherwise share
  a name.
- `guide |> TraceLoop` copies a whole closed guide and fixes its winding for the
  loop it is in. It works for `Rect2`, `Square2`, `RoundedRect2`, `Polygon2`,
  `RegularPolygon2`, closed Paths, `Circle2` and `Ellipse2`. A full circle or
  ellipse stays one boundary segment named after the guide.

To trace only part of a guide, select endpoints by name:

```text
guide from startPoint to endPoint as selectedEdge
```

After the first stage, `guide to endPoint` starts from the previous endpoint.
`guide from startPoint` runs from `startPoint` to the guide's own end (its start
under `Reverse`), so the point where a `RoundedRect2` side meets its corner never
needs to be placed by hand. Endpoints must be named points on that guide.
Circular spans work the same way, and `Reverse` takes the other way round.

### Rounding a corner with `Round(r)`

`|> Round(r)` between two stages replaces the corner where they meet with an
arc of radius `r` tangent to both. Both neighbours are trimmed back to the
tangent points; the arc bends into whichever side the corner turns, so the same
stage rounds convex and concave corners. Line and arc neighbours are both
supported. Before `Close`, `Round(r)` rounds the corner between the last stage
and the first.

```text
Blank.Bottom as Bottom
|> Round(5mm)
|> Blank.Right as Right
|> Round(5mm) as BottomRightRound
```

The arc is named by `as`, or otherwise `{previousEdge}Round{n}`, where `n` counts
the rounds in the loop.
[`round-stages.firmament`](../../../../fixtures/Regression/AuthoringConvenience/round-stages.firmament)
builds the same solid as a `RoundedRect2`. `Round(r)` is rejected when its
neighbours already meet tangentially, when they do not meet, when `r` is not a
positive length, and when the arc would not fit on either neighbour.

`|>` is only for tracing. It is not a general map, filter or function
composition operator. Manual `Segment` declarations remain available for
low-level control.

## 2D shapes

| Shape | Fields |
| --- | --- |
| `Point2` | `position` |
| `Line2` | named `from` and `to` points |
| `Arc2` | named `center`, `from` and `to` points; the counter-clockwise arc from `from` to `to`. `to` must lie on the circle through `from`; it is rejected otherwise |
| `CubicBezier2` | named endpoints plus `control1`, `control2` |
| `Rect2` | `center`, `size` (two full lengths) |
| `Square2` | `center`, `size` (one length) |
| `RoundedRect2` | `center`, `size`, `radius` |
| `SmoothRoundedRect2` | `center`, `size`, `cornerExtent`; polynomial corners, not circular fillets |
| `Circle2` | `center` and exactly one of `radius` or `diameter` |
| `Ellipse2` | `center`, `axisLengths` (two full lengths; `axes` also accepted) |
| `Slot2` | `center`, `length`, `width`; `length` is overall, including both round ends, and at least `width` |
| `Triangle2<Equilateral \| Isosceles \| Right \| Explicit>` | variant-specific dimensions |
| `Polygon2<Rhombus \| Parallelogram \| Trapezoid \| Explicit>` | variant-specific dimensions |
| `RegularPolygon2<N>` | regular polygon with a bounded vertex count |

`Point2`, `Line2`, `Arc2` and `CubicBezier2` are open guides; the rest are closed.
Named points on an `Arc2` split it into spans, as on a `Line2`.
Parametric shapes accept `rotation` in degrees.

`Polygon2<Explicit>` takes its corners inline,
`Polygon2 outline { points: [[0mm, 0mm], [40mm, 0mm], [0mm, 20mm]] }`, or from a
named point set with `vertices: points`. Coordinates may use Static arithmetic.
Both forms check winding, duplicate points, self-intersection and non-zero area.
Explicit triangles and polygons do not accept a rotation; rotate the points, or
place a derived Profile with `Curve2`.

Prefer the shape that says what you mean: a `Rect2` for a rectangle, a
`Triangle2` for a triangle, a named polygon variant before a list of points.
`Polygon2<Rhombus>`, for example, takes two full diagonals:

```text
Polygon2<Rhombus> diamond { center: [0mm, 0mm]; diagonals: [90mm, 64mm] }
```

There are no arbitrary 2D Boolean operations. Shape families and variant fields
are listed in full in [closed 2D boundaries](../../firmament/boundaries.md);
[equation curves](../../firmament/equation-curves.md) covers `Curve2` and
`Curve3`; [boundary edits](../../firmament/profile-boundary-edits.md) covers
named tabs and smooth replacements on an existing Profile.

## Planes

A layout's header picks its plane: `on XY` (the default), `on XZ` or `on YZ`.

| Layout | Local axes | Normal |
| --- | --- | --- |
| `on XY` | world X, Y | +Z |
| `on XZ` | world X, Z | −Y |
| `on YZ` | world Y, Z | +X |

`Extrude` follows the layout's plane. `Compose` takes its frame from its Base
Profile and requires every Base/Add/Remove operand to share it. On a plane other
than XY, Compose currently rejects Boss, Pocket, Hole, Slot and EdgeFinish,
because their supports are defined in world XY.

The layout plane applies only to that layout's 2D guides. It does not move
unrelated 3D members, and it does not change any other consumer's support.

## Frames and selectors

`XY`, an axis such as `+Z`, a `Plane`, a `DatumFrame` and a published face are
different kinds of reference, and `on` means something different in each place it
appears: a layout plane, a hole's support face, a section frame or an assembly
attachment. Each consumer admits specific reference types. An unsupported
placement rejects; it is never replaced with XY.

Faces have three distinct naming schemes: native named faces, signed primitive
faces such as `face(+Z)`, and imported `#face` references from STEP. They are not
interchangeable. See [STEP import](../../firmament/step-import.md),
[datum seating](../../firmament/datum-seating.md) and
[Concept derivation](../../firmament/concept-derivation.md).

More construction forms: [geometry](../../firmament/geometry.md),
[spans](../../firmament/spans.md), [Revolve](../../firmament/revolve.md).
