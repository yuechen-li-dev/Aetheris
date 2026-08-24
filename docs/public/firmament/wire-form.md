# Formed Wire

Use `WireForm` when the design intent is a formed length of constant-section wire or rod. Authors specify the forming sequence; Aetheris derives the exact tangent centerline and materializes it with the circular Sweep kernel.

```firmament
WireForm BentWire {
    Diameter: 1mm
    Material: Standard.Materials.StainlessSteel.304_Annealed
    StartFrame { Origin: [0mm, 0mm, 0mm]; Tangent: [1, 0, 0]; Up: [0, 0, 1] }
    Straight Lead { Length: 20mm }
    Bend Corner { Radius: 5mm; Angle: 90deg; Plane: Up }
    Straight Tail { Length: 30mm }
}
```

`Diameter` and `Material` define the stock. Direct WireForm authoring uses the catalog identity; a typed product policy may carry that identity through a `String` field. `StartFrame` supplies an origin, initial tangent, and perpendicular `Up` reference. A `Straight` advances along the current tangent and preserves the frame. A signed `Bend` rotates the tangent by `Angle` around either the current local `Up` or `Right` axis. Here `Plane` names the current local **plane normal/rotation axis**: `Plane: Up` bends in the tangent/right plane, while `Plane: Right` bends in the tangent/up plane. The frame is transported by the same rigid rotation, avoiding arbitrary spin. Start-frame transformations therefore carry the forming program without global-coordinate bend hacks.

`Radius` is always the **centerline bend radius**. X0 applies a geometric-only minimum-radius rule: `Radius > Diameter / 2`. It does not invent material forming tables. Positive angles follow the right-hand rule about the selected local plane normal; negative angles reverse that normal. X0 admits nonzero angles through ±180°.

Each operation consumes a `WireState` (position, tangent, local frame, and accumulated length) and produces the next state. Changing an operation intentionally moves every downstream operation. The compiler retains operation identity and exposes the operation-to-line/arc-to-cylinder/torus correspondence through structured inspection.

The ideal cut-stock length is derived exactly:

- Straight: `Length`
- Bend: `Radius × abs(AngleRadians)`
- Total: the sum of all operation lengths

Circular area times total length gives volume; density from the Material DB gives mass. These are idealized centerline quantities. X0 does not model springback, plastic stretch, forming allowance corrections, or manufacturing process simulation.

The centerline lowers to explicit line and circular-arc AIR, then to exact circular Sweep geometry. Straights become cylinders, bends become toroidal patches, and open terminals become planar caps. Canonical WireForms have no rational B-spline surfaces and no faceted fallback. Both terminals retain position, tangent/frame, and section diameter.

X0 supports one continuous, unbranched, open wire with constant circular section. Nonadjacent contact or overlap fails closed. Coils, springs, helices, arbitrary splines, mathematical or physical knots, branching, variable section, deliberate contact, and closed-wire authoring are deferred. Direct `Concept Path` plus `Sweep` remains the lower-level choice for explicitly authored trajectories.

See the canonical [90-degree bend](../../../fixtures/Canonical/WireForm/single-bend-90.firmament), [U-wire](../../../fixtures/Canonical/WireForm/u-wire.firmament), [two-plane 3D wire](../../../fixtures/Canonical/WireForm/three-dimensional-bends.firmament), and [Paperclip](../../../fixtures/Canonical/WireForm/paperclip.firmament).

`aetheris inspect source.firmament --json` reports the semantic `wireForm` object, including `operations`, `totalStraightLength`, `totalBendLength`, and compiler-derived `totalWireLength`. A materializing `build --json` adds volume, mass, surface inventory, manifold/reimport status, rational/faceted counts, and the STEP hash.
