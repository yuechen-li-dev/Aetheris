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

WIRE-X1 adds semantic winding generators without weakening the exact Straight/Bend route. `AxisCoil` authors a cylindrical helix from `Radius`, `Turns`, either `Pitch` or `Height`, explicit `Handedness`, and an optional stable `StartPhase`. If both Pitch and Height are supplied they must agree with `Height = Turns × Pitch`. The compiler derives the missing value, exact helix length, terminal position/tangent/transported frame, turn clearance, volume, and mass.

```firmament
AxisCoil Winding {
    Radius: 12mm
    Turns: 8
    Pitch: 5mm
    Handedness: RightHanded
    StartPhase: 0deg
}
```

`SurfaceCoil` generates a wire centerline by winding around a known support surface while maintaining a requested side and clearance. The author specifies winding intent; Aetheris derives the changing 3D path. X1 admits authored analytic `Cylinder`, `Frustum`/`Cone`, and `Sphere` supports. Cylinder/frustum winding uses `AxialPitch`. Sphere winding uses honest linear-latitude progression between `StartLatitude` and `EndLatitude`; it does not claim equal geodesic spacing and excludes the exact poles.

```firmament
Frustum CupInnerFrustum { BottomRadius: 18mm; TopRadius: 30mm; Height: 60mm }
SurfaceCoil Winding {
    Surface: CupInnerFrustum
    Side: Inside
    Clearance: 1mm
    Turns: 6
    AxialPitch: 8mm
    Handedness: RightHanded
}
```

`Clearance` is support-to-wire clearance; the compiler uses `Diameter / 2 + Clearance` as centerline offset. `CenterlineOffset` is the mutually exclusive mathematical alternative. Collapsing inward offsets, pole singularities, turn overlap, and unsupported surfaces fail with typed diagnostics. CLI inspection reports the winding kind/law, handedness, resolved pitch/height, exact or deterministically integrated stock length, start/end terminals, self/support clearance, and polynomial realization evidence.

The semantic/evaluable winding law remains authoritative. Coil-containing forms lower through deterministic rotation-minimal frame transport to cubic non-rational B-spline tube patches and planar caps; STEP uses no rational product surfaces and no faceted fallback. Coil-free forms still use exact cylinders and tori. The current approximation uses 32 longitudinal cubic spans per turn and four cubic polynomial quarter patches around the circular section; the compiler measures maximum/RMS centerline error against the winding law and fails if its 0.01 mm bound is exceeded.

X1 supports one continuous, unbranched, open wire with constant circular section. It does not support arbitrary/freeform supports, variable pitch or section, intentional turn contact, spring mechanics, contact dynamics, knots, branching, or closed-wire authoring. Surface support identity is authored rather than imported-face-ID based. Direct `Concept Path` plus `Sweep` remains the lower-level choice for explicitly authored line/arc trajectories.

See the canonical [axis coil](../../../fixtures/Canonical/WireForm/axis-coil.firmament), [frustum SurfaceCoil](../../../fixtures/Canonical/WireForm/frustum-surface-coil.firmament), [sphere SurfaceCoil](../../../fixtures/Canonical/WireForm/sphere-surface-coil.firmament), [composed Straight/Coil/Straight](../../../fixtures/Canonical/WireForm/straight-coil-straight.firmament), [90-degree bend](../../../fixtures/Canonical/WireForm/single-bend-90.firmament), and [Paperclip](../../../fixtures/Canonical/WireForm/paperclip.firmament).

`aetheris inspect source.firmament --json` reports the semantic `wireForm` object, including `operations`, `totalStraightLength`, `totalBendLength`, and compiler-derived `totalWireLength`. A materializing `build --json` adds volume, mass, surface inventory, manifold/reimport status, rational/faceted counts, and the STEP hash.
