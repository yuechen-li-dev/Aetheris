# 2. Profiles

Most real parts are not boxes. The usual way to make one is to draw its outline
in 2D and give it thickness. In Firmament that takes three steps:

1. **Construction geometry**: points, lines, rectangles and circles that describe
   the shape. They are guides only and create no material.
2. **A Profile**: closed loops traced from those guides, with a definite inside
   and outside. Holes are inner loops.
3. **A material operation** such as `Extrude`, which turns the Profile into a
   solid.

There is no sketch solver. You state dimensions and positions directly, so the
shape is always exactly what the numbers say.

## The source

[`02-bracket-profile.firmament`](../../../fixtures/Canonical/Learn/02-bracket-profile.firmament)
makes a flat L-shaped bracket with a mounting hole in its upright leg:

```firmament
// Lesson 2: draw a closed 2D boundary, then extrude it.
Model BracketPlate {
  units: mm

  // Construction geometry only: nothing here is material yet.
  Concept Struct layout on XY {
    Polygon2<Explicit> outline {
      points: [[0mm, 0mm], [60mm, 0mm], [60mm, 15mm], [15mm, 15mm], [15mm, 40mm], [0mm, 40mm]]
    }
    Circle2 bore { center: [7.5mm, 30mm]; diameter: 6mm }
  }

  // A Profile turns guides into closed loops with an inside and an outside.
  Profile section using layout {
    Loop Outer { outline |> TraceLoop }
    Loop Inner { bore |> TraceLoop }
  }

  // Extrude gives the Profile thickness, from Z = 0 to Z = 6mm.
  Extrude bracket { profile: section; from: 0mm; to: 6mm }
}
```

- `Concept Struct layout on XY` groups construction geometry on the XY plane.
  `Concept` marks it as design data: it never becomes material by itself.
- `Polygon2<Explicit>` is a polygon from listed corner points. The compiler
  checks winding, repeated points, self-intersection and zero area.
- `Profile section using layout` reads guides from `layout`.
  `outline |> TraceLoop` copies a whole closed guide into a loop. The `Inner`
  loop becomes a hole through the part.
- `Extrude bracket` makes the solid. Its result is one body with nine faces: six
  sides, top, bottom and the bore wall.

## Simple shapes in one line

A rectangle, circle or slot can be a Profile directly:

```firmament
Profile stockOutline = Rect2 { center: [0mm, 0mm]; size: [70mm, 44mm] }
```

This uses the same exact boundary as the longer form. Lesson 3 uses it for every
outline.

## Tracing edge by edge

When a loop should follow selected edges, or you want to name the edges for
later reference, trace them in order with `|>`:

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

Each stage continues from where the previous one ended; `as` names the resulting
edge, and `Close` checks that the loop really closes. It never adds a missing
segment for you. A gap or an ambiguous continuation is an error.

## Shapes you can use

| Shape | Main fields |
|---|---|
| `Rect2`, `Square2` | `center`, `size` |
| `RoundedRect2` | `center`, `size`, `radius` |
| `Circle2` | `center` and one of `radius` or `diameter` |
| `Ellipse2` | `center`, `axisLengths` (full lengths) |
| `Slot2` | `center`, `length` (overall, including both round ends), `width` |
| `Triangle2<...>` | `Equilateral`, `Isosceles`, `Right` or `Explicit` |
| `Polygon2<...>`, `RegularPolygon2<N>` | named polygon variants or an N-sided regular polygon |
| `Point2`, `Line2`, `CubicBezier2` | open guides for tracing |

The full field list is in [profiles and 2D construction](../reference/language/profiles.md).

## Other planes

Change `on XY` to `on XZ` or `on YZ` to draw on another principal plane. On XZ
the bracket stands upright and extrudes along −Y; try it and check the new
bounds with `aetheris analyze`.

## Try it

- Add a second `Circle2` and a second `Inner` loop.
- Replace the polygon with a `RoundedRect2` and compare the face count.
- Swap `[60mm, 0mm]` and `[60mm, 15mm]` so two edges cross, and read the
  `firmament-boundary2-explicit-self-intersection` diagnostic.

For curves defined by equations, revolved profiles and sweeps along a path, see
[equation curves](../firmament/equation-curves.md), [Revolve](../firmament/revolve.md)
and [Sweep](../firmament/sweep.md).

Next: [3. Features](03-features.md).
