# Geometry and semantic features

For exact regular solids, ellipsoids and the bounded whole-stock fillet, see
[regular polyhedra and ellipsoids in the syntax guide](syntax.md#regular-polyhedra-ellipsoids-and-whole-stock-rounding).
`target: AllEdges` currently applies only to untouched size-based Box and regular
Polyhedron stock. It does not admit rounding all edges of an arbitrary body.

Ellipsoid BRep export uses exact rational quadratic patches and reimports them as
native exact quadrics. STEP also defines an [`ELLIPSOID` CSG primitive](https://www.steptools.com/docs/stp_aim/html/t_ellipsoid.html);
that entity is outside the [BRep face-surface select](https://www.steptools.com/docs/stp_aim/html/t_advanced_face_geometry_select.html).
The current exporter retains the BRep artifact contract.

Preview 3 plus bounded X0/X1 additions support production routes, not an unrestricted general-purpose CAD kernel. Public native examples cover Box, Cylinder, Frustum, RoundedBox, line/arc profile extrusion and composition, bounded [Revolve](revolve.md), bounded [circular Sweep](sweep.md), ordered [Formed Wire](wire-form.md), semantic shaft/counterbore/countersink holes, slots, patterns, selected chamfer/fillet routes, hollow bodies, and a bounded cubic lattice. Consult the [support matrix](../reference/supported-features.md) before relying on a combination.

Analytic single solids use the same direct named declaration family: `Sphere Body { Radius: 7.5mm }`, `Cone Body { ... }`, and `Torus Body { ... }`. `Cone` includes a pointed zero-radius end. These forms round-trip as analytic AP242 surfaces, but they do not imply arbitrary Boolean or profile-composition support. The older `solid Body: Sphere { radius: ... }` spelling remains a compatibility input, not canonical authoring. Use the qualified [sphere](../../../fixtures/Canonical/Primitives/sphere.firmament), [pointed cone](../../../fixtures/Canonical/Primitives/pointed-cone.firmament), and [torus](../../../fixtures/Canonical/Primitives/torus.firmament) sources as the boundary examples.

Use named features and selectors, never raw internal B-rep IDs in native authoring. [`multiple-hole-dimensions-with-chamfer.firmament`](../../../fixtures/Canonical/PMI/multiple-hole-dimensions-with-chamfer.firmament) demonstrates two holes, an outer-boundary chamfer, and PMI surviving the same AP242 build. [`counterbore-shaft-diameter.firmament`](../../../fixtures/Canonical/PMI/counterbore-shaft-diameter.firmament) demonstrates a profile-composed part, pattern holes, a counterbore, a chamfer, and shaft-diameter PMI.

A face-local `Hole<Shaft>` or `Hole<Counterbore>` on a `Box` may cross the edge of its entry face: a through hole drilled near an edge leaves a notch, and it builds as one. Only exact tangency is rejected, because the wall there would have zero thickness along a line; the diagnostic gives the threshold in your own terms (for example, "Put Center X below 19mm to keep a wall on that side, or above it to cut through the +X face"). Countersunk and drill-point holes, whose entries are conical, are not yet supported when they break out, and say so.

`HoleDiameter` on `Hole<Counterbore>` and `Hole<Countersink>` targets the shaft `Diameter`; `CounterboreDiameter` remains a distinct feature field and has no separate public PMI record kind in Preview 3. Pattern generation supports geometry as a semantic whole; authored names for generated instances are not a stable public instance-selector family in Preview 3.

### Circular hole families on composed profiles (current source)

A traced L profile or other admitted line/arc composition can carry
`Hole<Shaft>`, `Hole<Counterbore>` and `Hole<Countersink>`. Through holes enter on
`+Z`, or a validated coplanar `Span<Plane>`. Counterbore steps remain cylindrical;
countersinks use exact conical walls and circular trims. Both require a positive
shaft interval and strict interior material around the entry. Entry height is
resolved at the authored center. Unrelated bosses cannot lift a counterbore or
countersink above its plate, and a pocket-floor entry must fit the exposed floor. The planner checks
actual composed material through the cavity depth, so a nearby pocket or slot
cannot silently clip the entry. Cavities must be disjoint; breakout, touching
cavities, unsupported blind ends and oriented support adapters reject explicitly.

`HoleDiameter` attaches to the shaft wall patches, excluding the larger recess
and cone. AP242 preserves every shaft patch association. The GLB presentation
adapter displays all patches when they form one connected analytic cylindrical
wall; different axes and disconnected walls still require a multi-target
presentation. This does not change the engineering body.

See the [local bracket rehearsal](../code-native-bracket.md) for a typed
Template, two-Rect2 traced L, countersink and PDF-only reconstruction witness.

## Boss

Direct Box stock and its semantic Hole host use the same bottom-anchored frame:
X/Y are centered and Z runs from 0 to height. Adding a direct Hole does not
recenter the stock. The earlier discrepancy in the
[LANG-BURN-X1 report](../../release/LANG-BURN-X1-FIRMAMENT-BURN-IN.md) is historical;
the [stock-frame regression](../../../Aetheris.Kernel.Firmament.Tests/FirmamentV2SemanticHoleTests.cs)
checks host and materialized bounds. Profile/Compose construction uses its
explicit `from`/`to` interval.

`Boss` is the first-class finite profile feature for adding connected material. In Preview 3 it is declared inside the active profile-based `Compose` body, targets that body's top support (`On: Top`), consumes an already-admitted line/arc `Profile`, and extrudes outward along `+Z` by a positive `Height`. A `Compose` does not reopen or augment a separately declared direct `Box`; use a rectangular Base Profile when the stock is box-shaped.

```firmament
Compose Body {
    Base Stock { Profile: BaseProfile; From: 0mm; To: 10mm; Role: Stock }
    Boss MountBoss { On: Top; Profile: BossProfile; Height: 6mm }
}
```

The active Compose body is the semantic target. Relative `On: Top` resolves against the current semantic body state under the authored footprint: a later Boss over an earlier Boss therefore starts at the earlier Boss top, while a feature elsewhere on an exposed base plateau still resolves that plateau. Equal highest semantic supports fail with `feature-support-ambiguous:Top`; topology order is never a tie-break. Named `<feature>.Top` retains explicit support identity, and `Base.Top` explicitly selects the original Base operation. The boss footprint must overlap the selected support with a proper connected region. A disjoint or point-tangent footprint, missing profile, unsupported support, or non-positive height fails typed. Boss retains a stable `boss:<host>.<name>` identity, then lowers to the existing section-stack `Add` operation. It never emits an arbitrary second solid and does not introduce public Boolean authoring.

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
        Stock.Bottom
        |> Stock.Right
        |> Stock.Top
        |> Stock.Left
        |> Close
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

`Pocket` removes a profile from the active Compose body's top support. Its `depth` form deliberately leaves a floor:

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

For a through cut, use `Pocket cable { on: Top; profile: cableProfile; end: ThroughAll }`.
This lowers to the existing `Remove` operation with `role: ThroughCut` across the
stock interval to the selected top support. Omit `depth` and
`minimumFloorThickness`; mixing termination policies is an error. A `depth`
that accidentally reaches through remains an error and never changes into a
through cut automatically.

Minimum-floor precedence is:

1. `MinimumFloorThickness` on the Pocket;
2. an active template `minimumFloorThickness` concept;
3. the existing template `minimumWallThickness` concept;
4. the documented Preview 3 bounded default of `1mm`.

Every supplied policy value must be positive and finite. Pocket retains a stable `pocket:<host>.<name>` identity and lowers through the existing section-stack `Remove` operation. A depth-based Pocket must retain its floor; explicit `end: ThroughAll` selects the admitted through-cut operation. This is not arbitrary solid subtraction.

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
        Stock.Bottom
        |> Stock.Right
        |> Stock.Top
        |> Stock.Left
        |> Close
    } }
    Profile PocketProfile Using Layout { Loop Outer {
        Recess.Bottom
        |> Recess.Right
        |> Recess.Top
        |> Recess.Left
        |> Close
    } }
    Struct Block { Compose Body {
        Base Stock { Profile: BaseProfile; From: 0mm; To: 10mm; Role: Stock }
        Pocket RecessPocket { On: Top; Profile: PocketProfile; Depth: 4mm; MinimumFloorThickness: 2mm }
    } }
}
```

To author a block containing both features, declare all three profiles in one `Concept Struct ... On XY`, then place both the documented `Boss` and `Pocket` declarations after the unique `Base` in the same Compose. `Base` cardinality is validated explicitly; zero or multiple Base operations produce `compose-role-cardinality:Base:expected=1:actual=<n>`, never a raw LINQ exception. Apply post-construction `EdgeFinish` in `Modify Body`; its semantic profile target is such as `BaseProfile.Outer`, not the primitive-only `Boundary` selector.

The canonical [Boss + Pocket block](../../../fixtures/Canonical/Features/Boss/boss-pocket-block.firmament) combines a cylindrical boss, through shaft hole, and shallow rectangular pocket using public Firmament only. The focused [rectangular Pocket](../../../fixtures/Canonical/Features/Pocket/rectangular-pocket.firmament) reinforces finite depth and minimum-floor intent. Existing lower-level `Compose Add` / `Remove` remains compatible for bounded blockout work.

For readable local geometry, start with the [line/arc Concept Path](../../../fixtures/Canonical/Profiles/concept-path-line-arc-profile.firmament). The [profile-delta recess](../../../fixtures/Canonical/Profiles/profile-delta-recess-extrusion.firmament) then shows that semantic `ProfileDelta` programming is not Sheet-Metal-specific: a Table row, `with` override, and typed Template modify an ordinary extruded profile.

For geometry that already exposes named spans, prefer the finite [Profile pipeline flagship](../../../fixtures/Canonical/Pipeline/profile-pipeline-flagship.firmament). `Stock.Bottom |> Stock.Right |> Stock.Top |> Stock.Left |> Close` inherits segment identities and derives endpoint continuity and orientation from the named source geometry. The [mixed line/arc witness](../../../fixtures/Canonical/Pipeline/line-arc-profile.firmament), [inner-loop witness](../../../fixtures/Canonical/Pipeline/profile-with-inner-loop.firmament), [open Concept Path](../../../fixtures/Canonical/Pipeline/open-path.firmament), and [whole-loop trace](../../../fixtures/Canonical/Pipeline/traceloop-profile.firmament) exercise the same resolved Profile/Path boundary. Pipelines also support named endpoint selection with `from`/`to` and arc traversal with `Reverse`; see [syntax](syntax.md#constructing-from-a-layout). Use manual `Segment` declarations when deliberate low-level control or debugging requires them.

Sheet Metal has its own `Hole Name` syntax for planar circular openings; countersink and counterbore forms are Model-domain features and deliberately fail in Sheet Metal rather than being ignored.

## Box top fillet with holes

```firmament
Model Mount {
    units: mm
    Box Body { size: [60mm, 40mm, 10mm] }
    Modify Body {
        Hole<Shaft> Bore { on: +Z; center: [0mm, 0mm]; diameter: 8mm; end: ThroughAll }
        EdgeFinish<Fillet> TopEdge { face: +Z; target: Boundary; radius: 1mm }
    }
}
```

This constructs the four top-edge cylindrical fillets and the through hole in
one enclosed body. The cylinders meet at exact elliptical corner seams, matching
the existing direct-intersection two-edge fillet construction. The vertical
corners stay sharp; this does not request a spherical whole-box corner blend.
The stock remains centered in XY with its bottom at Z=0.

The same finish works without holes. With holes, the bounded combination admits
one Box, one Modify, one complete `+Z` `Boundary` finish and top `ThroughAll`
shaft/counterbore holes. No Region operations participate. For finish size `r`,
the entry circle must satisfy `abs(x) + entryRadius < width/2 - r` and
`abs(y) + entryRadius < depth/2 - r`, with kernel tolerance applied. A counterbore
uses its larger entry radius. Hole entry circles must also be mutually disjoint.
Touching or overlapping requests fail; neither the hole nor the finish is dropped.
The radius must be positive, smaller than the height, and smaller than half the
width and depth. Other combined selectors, blind holes, countersinks, mixed
finishes and hole/fillet intersections remain outside this qualified combination.

The [shaft/counterbore witness](../../../fixtures/Regression/Fillet/top-boundary-with-counterbore.firmament)
uses the ordinary authoring and AP242 paths. Explicit supported PMI is retained.

## Intentional Boolean boundary

Sphere remains a supported standalone analytic solid. Pocket is a supported finite prismatic profile feature. Subtracting a Sphere from a block, a hemispherical arbitrary cavity, public `Union` / `Subtract` / `Intersect`, and general CSG trees are not Preview 3 Firmament features. A future `SphericalSeat`, `Dish`, or tool-profile feature would require an engineering contract; geometric possibility alone is not one.
