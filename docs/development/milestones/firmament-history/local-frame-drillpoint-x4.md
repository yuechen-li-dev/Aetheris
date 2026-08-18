# LOCAL-FRAME-DRILLPOINT-X4

Construction Plane shaft holes now admit exact blind drilling on the existing Box and signed-permutation-frame lane:

```firmament
Hole<Shaft> SideBlind {
    From: PositiveXWorkplane
    Center: Point2(10mm, 6mm)
    Diameter: 8mm
    End: ShaftDepth(30mm)
    Termination: DrillPoint
}
```

`End: ShaftDepth(value)` ends at the circular shaft-to-DrillPoint transition. `End: TotalDepth(value)` ends at the Tip. Both forms require `Termination: DrillPoint`; `ThroughAll` cannot be combined with a DrillPoint. The compiler-owned default included point angle is `118deg`; override it with `Termination: DrillPoint { PointAngle: 100deg }`.

A blind drilled Hole terminates in a conical DrillPoint, not a flat cylinder cap. For radius `r` and included angle `theta`, `TipLength = r / tan(theta / 2)`. Thus `TotalDepth = ShaftDepth + TipLength`.

The authoritative `LocalFrameHoleBRepPlan` owns the mouth loop, cylindrical shaft wall, transition loop, analytic `ConeSurface`, singular Tip vertex, DirectedEdgeUse loops, face senses, and semantic correspondence. The materializer only consumes those planned entities. The inner cylinder and cone faces use `SameSense = false`, so their support normals are oriented into the removed volume; the Plan's two-arc periodic boundary convention gives closed mouth and transition selections.

Published source roles are `MouthLoop`, `MouthEdges`, `ShaftWallFaces`, `ShaftToDrillPointLoop`, `ShaftToDrillPointEdges`, `DrillPointFaces`, and `TipVertex`. `aetheris inspect-selections <source> --json` shows declared and derived depth, point angle, local/world mouth, host interval, cone evidence, and those selections.

Current restrictions remain deliberate: one simple Box host, a proper signed-permutation Construction Plane, local `+Z` drilling into a single contiguous host interval, and one round shaft. A future composed-host extension replaces only the Box local-interval query; the Hole AIR, local frame, depth convention, and plan-owned shaft/cone topology remain unchanged.
