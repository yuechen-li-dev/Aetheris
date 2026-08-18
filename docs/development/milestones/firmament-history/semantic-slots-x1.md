# SEMANTIC-SLOTS-X1

`Slot<Capsule>` is a compiler-owned construction contract, not an anonymous user Profile.  Templates describe how users generate geometry.  Generics describe what standardized mechanical feature the compiler owns.

```firmament
Slot<Capsule> Relief {
  Center: Point2(0mm, 0mm)
  Direction: Vector2(1, 0)
  Length: 80mm       // overall end-to-end length
  Width: 40mm
  Extent: ThroughAll // or Between(-5mm, 5mm)
}
```

The compiler normalizes Direction and derives `Radius = Width / 2`, `StraightSpan = Length - Width`, and two end centers.  It rejects non-positive dimensions, a negative straight span, zero direction, invalid/empty `Between`, and `Length == Width`; use `Hole<Shaft>` for that circular case.

`Slot<RoundedRectangle>` uses the same center, direction, length, width, and extent contract, plus an explicit `CornerRadius`. It lowers to four straight segments and four exact quarter-circle arcs. Its corner radius must be positive and no greater than half of either overall dimension.

The lowering is one exact closed XY Profile: `PositiveSide`, `EndCap`, `NegativeSide`, `StartCap` (two lines and two semicircular arcs), then the existing arrangement and section-stack subtraction route.  It is not a box-plus-cylinder Boolean construction. `ThroughAll` resolves over the composed host material interval; `Between` is clipped to its declared material overlap.

The authoritative plan publishes `SlotEntryLoop`, `SlotExitLoop`, `SlotWallFace`, `SlotStraightWallFace`, and `SlotEndWallFace`, allowing `Selection` through `Slot(Name)` without coordinate/radius rediscovery.  A custom shape remains a Template, for example `Template TeardropOpening(...) -> custom Profile -> Compose Remove`; it should not become a compiler generic merely because it is recurrent.
