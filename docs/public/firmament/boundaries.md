# Closed 2D boundaries

`ClosedBoundary2` is the semantic contract for one deterministic, oriented, non-self-intersecting 2D boundary. It is not a public inheritance hierarchy or a sketch solver. Canonical declarations lower through the existing `Point2`, `Line2`, circular/elliptical curve, `Profile`, AIR, and STEP path.

Use the most semantic available noun:

| Type | Intended use | Primary dimensions | Stable guides |
| --- | --- | --- | --- |
| `Rect2` | rectangle | `Center`, `Size: [width, height]` | `Bottom`, `Right`, `Top`, `Left`; four corners |
| `Square2` | structurally equal sides | `Center`, scalar `Size` | same edge/corner vocabulary as `Rect2` |
| `Triangle2<Equilateral>` | equal-sided triangle | `Center`, `Side` | `A`, `B`, `C`; `AB`, `BC`, `CA` |
| `Triangle2<Isosceles>` | centered isosceles triangle | `Center`, `Base`, `Height` | `A`, `B`, `C`; `AB`, `BC`, `CA` |
| `Triangle2<Right>` | right triangle | right-angle `Origin`, `Legs` | `A`, `B`, `C`; `AB`, `BC`, `CA` |
| `Triangle2<Explicit>` | directly named triangle | `A`, `B`, `C` | `A`, `B`, `C`; `AB`, `BC`, `CA` |
| `Circle2` | circular boundary | `Center`, exactly one of `Diameter` or `Radius` | `Center`, one analytic `Boundary` |
| `Ellipse2` | elliptical boundary | `Center`, full `AxisLengths` | center/extrema and one analytic `Boundary` |
| `Slot2` | straight engineering slot | `Center`, overall `Length`, `Width` | `Top`, `EndArc`, `Bottom`, `StartArc` |
| `RoundedRect2` | rounded rectangular region | `Center`, `Size`, `Radius` | four lines and four named corner arcs |
| `Polygon2<Rhombus>` | diagonal-defined rhombus | `Center`, full `Diagonals` | four compass vertices and derived edges |
| `Polygon2<Parallelogram>` | sheared parallel-sided boundary | `Center`, `Base`, `Height`, `Shear` | four corners and derived edges |
| `Polygon2<Trapezoid>` | centered parallel-sided taper | `Center`, `BottomWidth`, `TopWidth`, `Height` | four corners and derived edges |
| `Polygon2<Explicit>` | arbitrary linear boundary | named `Set<Point2>` | authored vertex names and derived `A_B` edges |
| `RegularPolygon2<N>` | regular polygon | `Center`, `Circumradius` or `AcrossFlats` | `Vertex0...`, `Edge0...` |

Every form is consumed identically inside a profile:

```firmament
RoundedRect2 Panel { Center: [0mm, 0mm] Size: [100mm, 60mm] Radius: 8mm }
Profile PanelProfile { Loop Outer { Panel |> TraceLoop } }
```

Compact declarations for the complete family:

```firmament
Rect2 Plate { Center: [0mm,0mm] Size: [100mm,60mm] }
Square2 Pad { Center: [0mm,0mm] Size: 40mm }
Triangle2<Equilateral> TriEq { Center: [0mm,0mm] Side: 40mm }
Triangle2<Isosceles> TriIso { Center: [0mm,0mm] Base: 40mm Height: 30mm }
Triangle2<Right> TriRight { Origin: [0mm,0mm] Legs: [30mm,40mm] }
Triangle2<Explicit> TriAny { A: [0mm,0mm] B: [30mm,0mm] C: [0mm,20mm] }
Circle2 HoleBoundary { Center: [0mm,0mm] Diameter: 20mm }
Ellipse2 Opening { Center: [0mm,0mm] AxisLengths: [40mm,20mm] }
Slot2 CableSlot { Center: [0mm,0mm] Length: 50mm Width: 12mm }
RoundedRect2 Panel { Center: [0mm,0mm] Size: [100mm,60mm] Radius: 8mm }
Polygon2<Rhombus> Diamond { Center: [0mm,0mm] Diagonals: [50mm,32mm] }
Polygon2<Parallelogram> Leaning { Center: [0mm,0mm] Base: 40mm Height: 24mm Shear: 10mm }
Polygon2<Trapezoid> Tapered { Center: [0mm,0mm] BottomWidth: 44mm TopWidth: 28mm Height: 24mm }
Static Vertices: Set<Point2> { A => Point2(0mm,0mm) B => Point2(30mm,0mm) C => Point2(20mm,20mm) D => Point2(0mm,15mm) }
Polygon2<Explicit> Outline { Vertices: Vertices }
RegularPolygon2<6> Hex { Center: [0mm,0mm] AcrossFlats: 32mm Rotation: 30deg }
```

`Rotation: Angle` rotates a shape in its local XY plane. Placement remains owned by the existing `Concept Struct ... On XY` / `Profile ... Using ...` frame architecture; the shape declarations do not introduce a second frame system.

`Slot2.Length` is the overall end-to-end length and must be at least `Width`; equality lowers deterministically to one circular boundary. `Ellipse2.AxisLengths` are full lengths, not semi-axes. `RoundedRect2` accepts radius zero as rectangle-equivalent geometry. All sizes must be finite and positive, and rounded radius cannot exceed half the smaller size.

`RegularPolygon2<N>` is a bounded built-in structural parameter (`3 <= N <= 1024`), not general const generics. `Polygon2<T>` admits only the documented constrained variants. Add another named polygon variant only when it supplies meaningful geometric constraints or substantially better engineering dimensions than `Polygon2<Explicit>`.

`Polygon2<Explicit>` preserves the source order and names of its `Set<Point2>` entries, rejects duplicates, degenerate area, and self-intersection, and derives stable edge identities from adjacent names.
