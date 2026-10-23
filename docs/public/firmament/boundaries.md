# Closed 2D boundaries

`ClosedBoundary2` is the semantic contract for one deterministic, oriented, non-self-intersecting 2D boundary. It is not a public inheritance hierarchy or a sketch solver. Canonical declarations lower through the existing `Point2`, `Line2`, circular/elliptical and polynomial curve, `Profile`, AIR, and STEP path.

Use the most semantic available noun:

| Type | Intended use | Primary dimensions | Stable guides |
| --- | --- | --- | --- |
| `Rect2` | rectangle | `center`, `size: [width, height]` | `Bottom`, `Right`, `Top`, `Left`; four corners |
| `Square2` | structurally equal sides | `center`, scalar `size` | same edge/corner vocabulary as `Rect2` |
| `Triangle2<Equilateral>` | equal-sided triangle | `center`, `side` | `A`, `B`, `C`; `AB`, `BC`, `CA` |
| `Triangle2<Isosceles>` | centered isosceles triangle | `center`, `base`, `height` | `A`, `B`, `C`; `AB`, `BC`, `CA` |
| `Triangle2<Right>` | right triangle | right-angle `origin`, `legs` | `A`, `B`, `C`; `AB`, `BC`, `CA` |
| `Triangle2<Explicit>` | directly named triangle | `A`, `B`, `C` | `A`, `B`, `C`; `AB`, `BC`, `CA` |
| `Circle2` | circular boundary | `center`, exactly one of `diameter` or `radius` | `center`, one analytic `Boundary` |
| `Ellipse2` | elliptical boundary | `center`, full `axisLengths` | center/extrema and one analytic `Boundary` |
| `Slot2` | straight engineering slot | `center`, overall `length`, `width` | `Top`, `EndArc`, `Bottom`, `StartArc` |
| `RoundedRect2` | rounded rectangular region | `center`, `size`, `radius` | four lines and four named corner arcs |
| `SmoothRoundedRect2` | squircle-style corners with straight sides | `center`, `size`, `cornerExtent` | four sides; `BottomRightCornerA/B`, `TopRightCornerA/B`, `TopLeftCornerA/B`, `BottomLeftCornerA/B` |
| `Polygon2<Rhombus>` | diagonal-defined rhombus | `center`, full `diagonals` | four compass vertices and derived edges |
| `Polygon2<Parallelogram>` | sheared parallel-sided boundary | `center`, `base`, `height`, `shear` | four corners and derived edges |
| `Polygon2<Trapezoid>` | centered parallel-sided taper | `center`, `bottomWidth`, `topWidth`, `height` | four corners and derived edges |
| `Polygon2<Explicit>` | arbitrary linear boundary | named `Set<Point2>` | authored vertex names and derived `A_B` edges |
| `RegularPolygon2<N>` | regular polygon | `center`, `circumradius` or `acrossFlats` | `Vertex0...`, `Edge0...` |

New authoring uses the plain shape names. The compatibility prefix `Concept`
is also admitted for every closed family above, including generic Triangle2,
Polygon2 and RegularPolygon2 forms. Both spellings use the same AST field binder,
reject unknown fields and unsupported header placements, and emit the same
geometry. `Concept Circle2` is a construction guide, not a Boolean requirement
named Circle2. The [syntax guide](../reference/language/requirements.md#conditions-concept-and-require)
separates requirements, requirement bundles and construction values.

Every form is consumed identically inside a profile:

```firmament
RoundedRect2 Panel { center: [0mm, 0mm] size: [100mm, 60mm] radius: 8mm }
Profile PanelProfile { Loop Outer { Panel |> TraceLoop } }
```

Compact declarations for the complete family:

```firmament
Rect2 Plate { center: [0mm,0mm] size: [100mm,60mm] }
Square2 Pad { center: [0mm,0mm] size: 40mm }
Triangle2<Equilateral> TriEq { center: [0mm,0mm] side: 40mm }
Triangle2<Isosceles> TriIso { center: [0mm,0mm] base: 40mm height: 30mm }
Triangle2<Right> TriRight { origin: [0mm,0mm] legs: [30mm,40mm] }
Triangle2<Explicit> TriAny { A: [0mm,0mm] B: [30mm,0mm] C: [0mm,20mm] }
Circle2 HoleBoundary { center: [0mm,0mm] diameter: 20mm }
Ellipse2 Opening { center: [0mm,0mm] axisLengths: [40mm,20mm] }
Slot2 CableSlot { center: [0mm,0mm] length: 50mm width: 12mm }
RoundedRect2 Panel { center: [0mm,0mm] size: [100mm,60mm] radius: 8mm }
Polygon2<Rhombus> Diamond { center: [0mm,0mm] diagonals: [50mm,32mm] }
Polygon2<Parallelogram> Leaning { center: [0mm,0mm] base: 40mm height: 24mm shear: 10mm }
Polygon2<Trapezoid> Tapered { center: [0mm,0mm] bottomWidth: 44mm topWidth: 28mm height: 24mm }
Static vertices: Set<Point2> { A => Point2(0mm,0mm) B => Point2(30mm,0mm) C => Point2(20mm,20mm) D => Point2(0mm,15mm) }
Polygon2<Explicit> Outline { vertices: vertices }
RegularPolygon2<6> Hex { center: [0mm,0mm] acrossFlats: 32mm rotation: 30deg }
```

`rotation: Angle` rotates a shape in its local 2D coordinates. Placement remains owned by `Concept Struct ... on XY/XZ/YZ` and `Profile ... using ...`, or by a traced Construction Plane; the shape declarations do not introduce a second frame system. XZ uses world +X/+Z with normal -Y; YZ uses +Y/+Z with normal +X. A plane-scoped 2D layout does not implicitly transform spatial semantic members. See [the sketch workspace](sketcher.md#planes).

Explicit Triangle2 and Polygon2 boundaries use their authored point coordinates.
A nonzero `rotation` on those variants is rejected; it previously disappeared.
Rotate the authored points or use the existing Curve2 placement of a Profile.
A zero rotation remains compatible. Other shape variants retain their admitted
local rotation.

After Template specialization, boundary coordinate and length fields accept bounded dimensional arithmetic through the existing Profile evaluator, for example `center: [Spec.Width / 2, -Spec.Length / 2]` and `radius: 2mm + 1mm`. Extrusion `from`/`to` intervals use the same evaluator. Expressions must consume the entire field and yield a length; incompatible units, unknown names, and division by zero fail rather than accepting a numeric prefix. This does not add arbitrary functions or a new expression language.

`Slot2.Length` is the overall end-to-end length and must be at least `width`; equality lowers deterministically to one circular boundary. `Ellipse2.AxisLengths` are full lengths, not semi-axes. `RoundedRect2` accepts radius zero as rectangle-equivalent geometry. All sizes must be finite and positive, and rounded radius cannot exceed half the smaller size.

`RegularPolygon2<N>` is a bounded built-in structural parameter (`3 <= N <= 1024`), not general const generics. `Polygon2<T>` admits only the documented constrained variants. Add another named polygon variant only when it supplies meaningful geometric constraints or substantially better engineering dimensions than `Polygon2<Explicit>`.

`Polygon2<Explicit>` preserves the source order and names of its `Set<Point2>` entries, rejects duplicates, degenerate area, and self-intersection, and derives stable edge identities from adjacent names.

## Smooth polynomial corners

```firmament
SmoothRoundedRect2 Outline {
    center: [0mm,0mm]
    size: [77.98mm,163.43mm]
    cornerExtent: 19.43mm
}
Profile Section { Loop Outer { Outline |> TraceLoop } }
```

This bounded primitive provides four straight sides and eight cubic Bézier spans. `cornerExtent` is the distance from the sharp envelope corner to each side tangency point, not a circular radius. It must be strictly between zero and half the smaller size. Rotation and dimensional arithmetic work as for the other boundaries. Closure and orientation are constructed internally; users do not author control points.

The fixed corner law is polynomial, inspired by a fourth-power superellipse; it is not an exact Lamé curve or a certified Apple contour. In a unit corner from (0,0) to (1,1), let `b = 2^(-1/4)`, `q = 1-b`, and `a = b-1/2`. The first cubic has controls `(0,0), (a,0), (2a,0), (b,q)`. Reflect it across the corner bisector and reverse traversal for the second half. Endpoint speed is nonzero, collinear endpoint controls give zero side curvature, and reflection gives matching tangent and signed curvature at the midpoint. Monotone convex corner spans remain inside their envelope. Area is integrated exactly; the inspection perimeter is a fixed Simpson quadrature estimate.

The existing Profile binder owns the resolved curves, and ordinary extrusion and SectionChain consume them as polynomial B-splines. No guide body is exported. [The SectionChain witness](../../../fixtures/Canonical/SectionChain/smooth-corner-sections.firmament) checks realized surface position, normals, and geometric curvature at every span and section join. Its straight extrusion has G2 side joins; its planar caps meet the sides with sharp edges. General `Continuity: G2` surface construction and plateau-to-body support matching remain separate work.

Use a smooth boundary as the footprint of a [G2 planar plateau](plateaus.md) to blend a raised region into a planar base.

`Polygon2 outline { points: [[0mm, 0mm], [30mm, 0mm], [20mm, 20mm], [0mm, 15mm]] }`
is the inline explicit form. Both `Point2(x, y)` and `[x, y]` entries use Length
expressions, including scoped Static arithmetic. Inline vertex identities are
`Vertex0`, `Vertex1`, and so on; use a named Set when names must survive a reorder.
The duplicate, area, self-intersection and explicit-rotation checks are shared.
A Revolve may name this closed polygon directly as its `profile`; it is converted
to the same closed Profile before the existing revolve binder runs.
