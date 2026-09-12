# GEOM-BOUNDARY2-X1 — canonical 2D boundary families

## Executive verdict

**Accepted.** Firmament can express the common closed 2D boundary families directly, expose stable semantic guides, and pass them through `TraceLoop` without manual point/line/arc reconstruction. The constructors erase into the existing Profile/AIR/BRep/STEP path; no second sketch kernel or profile representation was introduced.

## Audit

Before X1, the repository had three relevant but uneven surfaces:

- `Rect2` was a semantic construction helper with virtual corner and edge guides, but it did not itself satisfy `Shape |> TraceLoop`.
- `Circle2` was an analytic curve guide used by explicit profile segments; it was not a directly traceable closed boundary and required a separately authored center point.
- `Polygon2<Rhombus>` was the only retained closed-shape declaration. Its bounded source expansion produced four ordinary `Point2` and `Line2` guides and rewrote `TraceLoop` into the existing profile pipeline.
- `Concept Path` already owned exact connected line/arc construction, traversal identity, and provenance. `ProfileAuthoringParser` already owned winding, closure, `Span<T>`, and resolution to `ResolvedProfile2D`.
- The material AIR admitted line segments, circular arcs, and full circles. Core BRep/STEP already admitted `Ellipse3` and `LinearExtrusionSurface`, but the profile AIR had no full-ellipse carrier.

The narrow common contract is therefore retained semantic metadata plus exactly one deterministic closed oriented curve chain that can be erased into `ResolvedProfile2D`. `ClosedBoundary2` does not own feature semantics, compound profiles, booleans, arbitrary constraints, or materialization.

Existing frame authority remains `Concept Struct ... On XY`, `Construction Plane`, and `Profile ... Using ...`. Shapes add only a uniform local `Rotation`; they do not add an `On`/frame subsystem. Outer-loop traversal is counter-clockwise. `TraceLoop` retains generated guide names as profile span names and provenance.

## Qualified shape matrix

| Shape | Qualified | Exact carrier | Stable guides | `TraceLoop` | `Span<Plane>` | STEP |
| --- | --- | --- | --- | --- | --- | --- |
| `Rect2` | yes | lines | 4 edges + 4 corners | yes | profile boundary | analytic planar/linear |
| `Square2` | yes | lines | 4 edges + 4 corners | yes | profile boundary | analytic planar/linear |
| `Triangle2<Equilateral/Isosceles/Right/Explicit>` | yes | lines | `A/B/C`, `AB/BC/CA` | yes | profile boundary | analytic planar/linear |
| `Circle2` | yes | one full circle | `Center`, `Boundary` | yes | profile boundary | circle/cylinder |
| `Ellipse2` | yes | one full ellipse | center, four extrema, `Boundary` | yes | profile boundary | ellipse + linear extrusion |
| `Slot2` | yes | 2 lines + 2 circles | named lines/caps | yes | profile boundary | line/circle/cylinder |
| `RoundedRect2` | yes | 4 lines + 4 circles | named lines/corners | yes | profile boundary | line/circle/cylinder |
| `Polygon2<Rhombus>` | yes, compatible | lines | compass vertices + derived edges | yes | profile boundary | analytic planar/linear |
| `Polygon2<Parallelogram>` | yes | lines | corners + derived edges | yes | profile boundary | analytic planar/linear |
| `Polygon2<Trapezoid>` | yes | lines | corners + derived edges | yes | profile boundary | analytic planar/linear |
| `Polygon2<Explicit>` | yes | lines | Set names + derived edges | yes | profile boundary | analytic planar/linear |
| `RegularPolygon2<N>` | yes, `3..1024` | lines | ordinal vertices/edges | yes | profile boundary | analytic planar/linear |

`Polygon2<Kite>` was intentionally not admitted: the proposed diagonal/intersection parameterization did not justify another public variant in X1. A regular kite is better expressed by its more semantic family where one exists; an arbitrary one uses `Polygon2<Explicit>`.

## Taxonomy and parameter doctrine

Rectangle, square, circle, ellipse, slot, and rounded rectangle remain separate public nouns because each structurally encodes different invariants and engineering dimensions. `Polygon2<T>` is reserved for bounded polygon variants whose constraints or dimensions are materially better than an explicit vertex set. This prevents both a giant `Shape2<Kind>` field switch and a named-shape encyclopedia.

`RegularPolygon2<N>` is a compiler-known structural shape parameter. The binder admits an integer literal only, validates `3 <= N <= 1024`, generates a finite deterministic vertex/edge sequence, and erases it before AIR. It introduces no user-defined const parameter, arithmetic over type parameters, or general metaprogramming facility.

## Before / after

Rectangle:

```firmament
// Before: Bottom |> Right |> Top |> Left |> Close
Rect2 Plate { Center: [0mm,0mm] Size: [100mm,60mm] }
Profile PlateProfile { Loop Outer { Plate |> TraceLoop } }
```

Slot:

```firmament
// Before: two points/lines, two circle guides, four directed segments
Slot2 CableSlot { Center: [0mm,0mm] Length: 50mm Width: 12mm }
Profile SlotProfile { Loop Outer { CableSlot |> TraceLoop } }
```

Explicit polygon:

```firmament
Static Vertices: Set<Point2> { A => Point2(0mm,0mm) B => Point2(30mm,0mm) C => Point2(20mm,20mm) D => Point2(0mm,15mm) }
Polygon2<Explicit> Outline { Vertices: Vertices }
Profile OutlineProfile { Loop Outer { Outline |> TraceLoop } }
```

## Inspection, validation, and compatibility

`aetheris inspect --json` now reports `boundaries` with shape ID/type/variant, `ClosedBoundary2` capability, center, rotation, dimensions, analytic area/perimeter, generated guides, exact carrier, and source provenance. The legacy `polygons` projection remains for `Polygon2` consumers, including byte-stable `Polygon2<Rhombus>` behavior.

Canonical families validate positive dimensions and non-degenerate orientation. Slot length is overall length and must be at least width. Rounded radius is inclusive from zero through half the smaller dimension. Explicit polygons preserve Set order/names and reject fewer than three points, duplicates/zero edges, self-intersection, and zero area. Regular polygons reject counts outside `3..1024` and missing/nonpositive size forms.

The canonical fixture is `fixtures/Canonical/Boundary2/closed-boundary-family.firmament`; typed negative witnesses are under `fixtures/Invalid/Boundary2/`. Candidate LANG-BURN replacements are the manual rectangle loops in Feature/PMI/Integration fixtures and the record-array slot pattern; no burn-in rewrite was performed in X1.

## Validation

- Release solution build: passed.
- Full solution: 3,373 passed (the intentionally empty FrictionLab test assembly reports no discoverable tests).
- Full Firmament suite: 1,407 passed.
- Full CLI suite: 419 passed.
- Focused ClosedBoundary2/guide/TraceLoop/area/perimeter/invalid/analytic STEP tests: passed.
- Canonical family CLI inspection: 15 retained boundary declarations with discoverable guides.
- Canonical family production CLI build: passed; output under ignored `artifacts/local/boundary2/`.
- `Polygon2<Rhombus>` Set regression and CLI inspection: passed.
- Canonical qualification: 161 fixtures passed.
- Repository layout and documentation-link guard: passed.
- Deterministic repeat export and fresh published-CLI parity: byte-identical SHA-256 `8af59b8584e2ecd92826442180fb7f83b6821292eb2d045107422478d57dac07`.
- STEP reimport: valid enclosed manifold; focused ellipse export/reimport retained `Ellipse3` curves.
- `git diff --check`: passed.
