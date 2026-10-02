# Point wire routes

`WireRoute` now lowers a finite `Start` → `Corner` → `End` path to the existing WireForm exact cylinder/torus sweep. This first slice removes the guitar's Python-computed lengths, angles, and placement bases. Functions remain ordinary compile-time Firmament Functions; no `Comptime` keyword is needed.

```firmament
schema WireForm
Model Lead {
 WireRoute Signal {
  Diameter: 1mm;
  MinimumBendRadius: 4mm;
  Path {
   Start { At: Point3(0mm,0mm,0mm); }
   Corner Bend { At: Point3(0mm,30mm,0mm); Radius: 4mm; }
   End {
    At: Point2(20mm,20mm);
    On {
     From: World;
     TranslateLocal: [0mm,30mm,0mm];
     RotateLocal: { Axis: X; Angle: 90deg }
    }
   }
  }
 }
}
```

`At` accepts a checked Point3, or Point2 with an explicit `On` frame. Point coordinates are lengths. `On` shares the existing FrameTransform parser and transform composition, including Normal/Up and RotateLocal; this slice requires `From: World`. Within a reusable definition, World means the definition's coordinate system; ordinary assembly Placement remains the sole occurrence placement authority. Keyed Concept point coordinates can be supplied through existing scalar template arguments.

The Corner is the virtual intersection of the two route legs. The fillet does **not** pass through that intersection. For turning angle θ and centerline radius R, each leg loses `R * tan(θ/2)` at the corner. Firmament computes the tangent frame, bend plane, circle center, arc, and remaining straight lengths. Radius must meet MinimumBendRadius and exceed the wire's radius. Both remaining straight lengths must be positive. Collinear/reversing corners, duplicate/unknown fields, missing units, malformed frames, and non-finite dimensions fail with named `wire-route-*` diagnostics. Straight-only wires retain WireForm Straight.

The existing WireForm materializer still owns sweep validity, self-clearance, STEP export/reimport and manifold checks. MinimumBendRadius is an authored geometric constraint, not a material strain qualification. Inspection reports the derived Lead, named Corner, and Tail operations through the existing WireForm engineering feature path.

The guitar's six routes now use Point3 Template inputs bound to final public
nut, tuner and tailpiece frames after fixed placement. The standalone low-E file
retains explicit local frames as an independent route witness. See
[source-owned guitar authoring](source-owned-guitar-authoring.md) for Bind ordering,
cache dependencies, public-port restrictions and the first Line3 Follow guide.

Implemented witnesses: fixtures/Canonical/WireForm/point-route.firmament,
line-guide.firmament, and the guitar strings-assembly.firmament. Python only runs
CLI reproduction and verifies that authored source was not changed.

Exact-through Via, multiple corners, circular/spline guides and obstacle routing
remain in the [routing proposal](wire-routing-and-guides-proposal.md).
