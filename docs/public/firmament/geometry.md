# Geometry and semantic features

Preview 3 supports bounded production routes, not an unrestricted general-purpose CAD kernel. Public native examples cover Box, Cylinder, Frustum, RoundedBox, line/arc profile extrusion and composition, semantic shaft/counterbore/countersink holes, slots, patterns, selected chamfer/fillet routes, hollow bodies, and a bounded cubic lattice. Consult the [support matrix](../reference/supported-features.md) before relying on a combination.

The V2 compiler also qualifies parser-backed analytic single-solid source using `model` / `solid`: `Sphere`, `Cone` (including a pointed zero-radius end), and `Torus`. Those forms round-trip as analytic AP242 surfaces, but they do not imply arbitrary boolean or profile-composition support. Use the qualified [sphere](../../../fixtures/FirmamentV2/Primitive/valid/a3-sphere-step-qualified.firmament), [pointed cone](../../../fixtures/FirmamentV2/Primitive/valid/a3-pointed-cone-step-qualified.firmament), and [torus](../../../fixtures/FirmamentV2/Primitive/valid/a3-torus-step-qualified.firmament) sources as the boundary examples.

Use named features and selectors, never raw internal B-rep IDs in native authoring. [`box-holes-pmi-chamfer.firmament`](../../../fixtures/FirmamentV2/Canonical/valid/box-holes-pmi-chamfer.firmament) demonstrates two holes, an outer-boundary chamfer, and PMI surviving the same AP242 build. [`profile-compose-l-bracket-counterbore-pmi.firmament`](../../../fixtures/FirmamentV2/Canonical/valid/profile-compose-l-bracket-counterbore-pmi.firmament) demonstrates a profile-composed part, pattern holes, a counterbore, a chamfer, and shaft-diameter PMI.

`HoleDiameter` on a `Hole<Counterbore>` targets the shaft `Diameter`; `CounterboreDiameter` remains a distinct feature field and has no separate public PMI record kind in Preview 3. Pattern generation supports geometry as a semantic whole; authored names for generated instances are not a stable public instance-selector family in Preview 3.

## Boss

`Boss` is the first-class finite profile feature for adding connected material. In Preview 3 it is declared inside the active `Compose` body, targets that body's top support (`On: Top`), consumes an already-admitted line/arc `Profile`, and extrudes outward along `+Z` by a positive `Height`:

```firmament
Compose Body {
    Base Stock { Profile: BaseProfile; From: 0mm; To: 10mm; Role: Stock }
    Boss MountBoss { On: Top; Profile: BossProfile; Height: 6mm }
}
```

The active Compose body is the semantic target; `On: Top` is its admitted support face. The boss footprint must overlap the host with a proper connected region. A disjoint or point-tangent footprint, missing profile, unsupported support, or non-positive height fails with a `firmament-boss-*` diagnostic. Boss retains a stable `boss:<host>.<name>` identity, then lowers to the existing section-stack `Add` operation. It never emits an arbitrary second solid and does not introduce public Boolean authoring.

### Complete Boss + through-hole source

The following is a complete parseable file, including the required wrapper and admitted low-level circle Profile. `Hole<Shaft>` belongs inside the same Compose and uses `End: ThroughAll` (not `Through: true`):

```firmament
Model BossPlate {
    Units: mm
    Concept Struct Layout On XY {
        Rect2 Stock { Center: [0mm, 0mm]; Size: [50mm, 30mm] }
        Point2 C { Position: [0mm, 0mm] }
        Point2 E { Position: [8mm, 0mm] }
        Point2 N { Position: [0mm, 8mm] }
        Point2 W { Position: [-8mm, 0mm] }
        Point2 S { Position: [0mm, -8mm] }
        Circle2 BossCircle { Center: C; Radius: 8mm }
    }
    Profile BaseProfile Using Layout { Loop Outer {
        Segment Bottom { Trace: Stock.Bottom; From: Stock.BottomLeft; To: Stock.BottomRight }
        Segment Right { Trace: Stock.Right; From: Stock.BottomRight; To: Stock.TopRight }
        Segment Top { Trace: Stock.Top; From: Stock.TopRight; To: Stock.TopLeft }
        Segment Left { Trace: Stock.Left; From: Stock.TopLeft; To: Stock.BottomLeft }
    } }
    Profile BossProfile Using Layout { Loop Outer {
        Segment Q1 { Trace: BossCircle; From: E; To: N; Sweep: CounterClockwise }
        Segment Q2 { Trace: BossCircle; From: N; To: W; Sweep: CounterClockwise }
        Segment Q3 { Trace: BossCircle; From: W; To: S; Sweep: CounterClockwise }
        Segment Q4 { Trace: BossCircle; From: S; To: E; Sweep: CounterClockwise }
    } }
    Struct Plate { Compose Body {
        Base Stock { Profile: BaseProfile; From: 0mm; To: 6mm; Role: Stock }
        Boss MountBoss { On: Top; Profile: BossProfile; Height: 8mm }
        Hole<Shaft> ThroughBoss { On: +Z; Center: [0mm, 0mm]; Diameter: 5mm; End: ThroughAll; Role: MountingHole }
    } }
}
```

## Pocket

`Pocket` is the first-class finite profile feature for accessible prismatic removal from the active Compose body's top support. It deliberately leaves a floor:

```firmament
Compose Body {
    Base Stock { Profile: BaseProfile; From: 0mm; To: 10mm; Role: Stock }
    Pocket ElectronicsRecess {
        On: Top
        Profile: PocketProfile
        Depth: 4mm
        MinimumFloorThickness: 2mm
    }
}
```

Preview 3 admits only `On: Top` / `+Z`, an enclosed admitted line/arc profile, and a positive finite `Depth`. The compiler derives `hostThickness` from the unique Base interval and calculates `remainingFloor = hostThickness - Depth`. `Depth == hostThickness`, greater depth, and tolerance-close through depth fail as `firmament-pocket-through-depth`; a positive remaining floor below policy fails as `firmament-pocket-minimum-floor-thickness`. Diagnostics include feature, host, requested depth, host thickness, remaining floor, and required floor.

Minimum-floor precedence is:

1. `MinimumFloorThickness` on the Pocket;
2. an active template `minimumFloorThickness` concept;
3. the existing template `minimumWallThickness` concept;
4. the documented Preview 3 bounded default of `1mm`.

Every supplied policy value must be positive and finite. Pocket retains a stable `pocket:<host>.<name>` identity and lowers through the existing section-stack `Remove` operation. It is intentionally not arbitrary solid subtraction and never means through-all. Use semantic `Hole`, `Slot`, or another documented opening feature for through removal.

### Complete rectangular Pocket source

`Rect2` exposes the four named corners and sides used below. This complete file creates 10 mm stock, a 4 mm pocket, and a 6 mm floor against a 2 mm requirement:

```firmament
Model PocketBlock {
    Units: mm
    Concept Struct Layout On XY {
        Rect2 Stock { Center: [0mm, 0mm]; Size: [40mm, 30mm] }
        Rect2 Recess { Center: [0mm, 0mm]; Size: [20mm, 14mm] }
    }
    Profile BaseProfile Using Layout { Loop Outer {
        Segment Bottom { Trace: Stock.Bottom; From: Stock.BottomLeft; To: Stock.BottomRight }
        Segment Right { Trace: Stock.Right; From: Stock.BottomRight; To: Stock.TopRight }
        Segment Top { Trace: Stock.Top; From: Stock.TopRight; To: Stock.TopLeft }
        Segment Left { Trace: Stock.Left; From: Stock.TopLeft; To: Stock.BottomLeft }
    } }
    Profile PocketProfile Using Layout { Loop Outer {
        Segment Bottom { Trace: Recess.Bottom; From: Recess.BottomLeft; To: Recess.BottomRight }
        Segment Right { Trace: Recess.Right; From: Recess.BottomRight; To: Recess.TopRight }
        Segment Top { Trace: Recess.Top; From: Recess.TopRight; To: Recess.TopLeft }
        Segment Left { Trace: Recess.Left; From: Recess.TopLeft; To: Recess.BottomLeft }
    } }
    Struct Block { Compose Body {
        Base Stock { Profile: BaseProfile; From: 0mm; To: 10mm; Role: Stock }
        Pocket RecessPocket { On: Top; Profile: PocketProfile; Depth: 4mm; MinimumFloorThickness: 2mm }
    } }
}
```

To author a block containing both features, declare all three profiles in one `Concept Struct ... On XY`, then place both the documented `Boss` and `Pocket` declarations after the unique `Base` in the same Compose. Feature declaration order does not replace their explicit finite intervals.

The canonical [Boss + Pocket mounting block](../../../fixtures/FirmamentV2/Canonical/valid/boss-pocket-mounting-block.firmament) combines a cylindrical boss, through shaft hole, and shallow rectangular pocket using public Firmament only. Existing lower-level `Compose Add` / `Remove` remains compatible for bounded blockout work.

Sheet Metal has its own `Hole Name` syntax for planar circular openings; countersink and counterbore forms are Model-domain features and deliberately fail in Sheet Metal rather than being ignored.

## Intentional Boolean boundary

Sphere remains a supported standalone analytic solid. Pocket is a supported finite prismatic profile feature. Subtracting a Sphere from a block, a hemispherical arbitrary cavity, public `Union` / `Subtract` / `Intersect`, and general CSG trees are not Preview 3 Firmament features. A future `SphericalSeat`, `Dish`, or tool-profile feature would require an engineering contract; geometric possibility alone is not one.
