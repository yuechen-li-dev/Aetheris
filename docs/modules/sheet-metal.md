# Sheet Metal Module

`Aetheris.SheetMetal` includes a shared exact planar-contour contract, exact line/arc corner-relief removal on bounded authored cases, stable Sheet Metal Concept Paths, reusable templates, and an M8 bounded semantic-layout pass. Imported STEP recovery remains separate from authored construction.

## Construction architecture

```text
Firmament SheetMetal
  -> SheetMetalPartIr (region / bend / corner / relief graph)
  -> planar + analytic cylindrical formed topology -> closed BRep -> AP242

SheetMetalPartIr
  -> exact graph traversal and neutral-axis bend strips
  -> shared line/arc profile arrangement
  -> PlanarContour2 outer blank + exact cut/relief loops
  -> thickness-bearing flat AP242 / analytic SVG
```

`PlanarContour2` is shared with normal Firmament Profiles. It owns one ordered outer loop, ordered inner loops, a plane frame, stable IDs, and segment provenance. Supported native curves are line segments, bounded circular arcs, and full circles. Validation rejects open chains, endpoint mismatches, duplicate IDs, wrong winding, self-intersection, invalid inner-loop nesting, and zero-length/zero-area topology.

The bounded kernel publishes line/line, line/arc, and arc/arc intersection; normalized-parameter split/trim; explicit-side line/arc offset with miter joins; and known-topology arrangement/stitching. It does not expose a universal arbitrary Boolean or silently polygonize analytic curves.

## Authoring and Concept Paths

M8 permits a `Concept Struct` to declare named `Datum`, regular `Pattern`, and bounded outer-edge `Tab` intent before the `SheetMetal` body. The compiler resolves those declarations into a `SheetMetalSemanticLayout`, validates required/equal-size/equal-pitch/mirror claims, generates stable feature members, and only then lowers exact formed and flat profiles. Profile M1 routes the outer-edge `Tab` through the shared Semantic Profile MIR, so its stable path owns three generated contour descendants in both formed and flat states rather than relying on anonymous polygon indices. This is intentionally not a general sketch solver. `Span` and `SpanOffset` on a flange provide reusable partial-edge attachment. See [Semantic Profiles](../language/semantic-profiles.md).

The resolved layout is visible in `aetheris sheetmetal inspect --json`; semantic paths are visible through `aetheris sheetmetal paths`.

```firmament
SheetMetal Tray {
    Thickness: 1.2mm;
    KFactor: 0.42;
    Base Main { Profile: Rectangle { Width: 240mm; Height: 180mm; }; }
    Flange FrontWall { From: Main.Front; Height: 40mm; Angle: 90deg; Radius: 1.5mm; Corner: Miter; }
    Flange FrontLip { From: FrontWall.Outer; Height: 10mm; Angle: 90deg; Radius: 1.2mm; Direction: Down; }
    Cut Fan { On: Main; Profile: Circle { Diameter: 120mm; }; At: (120mm, 90mm); }
}
```

A rectangular authored base exposes `Front`, `Right`, `Rear`, `Left`, and `Center`. A flange exposes `Root`, `Outer`, `Left`, `Right`, `LeftCorner`, `RightCorner`, and `Bend`. `Outer` is `FlangeAttachable`; `Center` is `PointCapable`, so using `Main.Center` in `Flange.From` produces a capability diagnostic rather than a topology-ID failure.

Formed/flat correspondence uses the same semantic identity: `FrontWall.Bend` maps to `Flat.FrontWall.Bend`; `Main.Front` maps to `Flat.Main.Front`. Inspect the public surface with:

```text
aetheris sheetmetal paths part.firmament
aetheris sheetmetal paths part.firmament --json
```

Normal Firmament Profile paths continue to use the same resolved line/arc substrate (`Outline.South`, `Outline.East`, and so on); M4 contour operations accept ordinary `ResolvedProfile2D` values directly.

## Corners and reliefs

- `Open`: deterministic bend-end clearance; exact composed outer blank on admitted topology.
- `Mitered`: symmetric authored setback, shared by formed bend/flange width and flat blank composition.
- `RectangularRelief`: exact diagonal line-loop removal with authored/derived width and depth.
- `RoundRelief`: exact round-ended line/arc removal with retained analytic arcs.

Automatic relief uses width at least one thickness and depth `inside radius + thickness`. Derived values remain in relief evidence. Flat STEP and SVG consume the same exact removal contour. Formed construction consumes the same corner record to shorten bend/flange ends, but it does not yet materialize a curved round-relief wall through the formed skins; that is a documented remaining parity seam.

Multiple reliefs combined with nested flange chains can still expose an unresolved angular-order/dangling-fragment rejection in the shared arrangement. The exact-blank result is then absent and `sheetmetal-exact-blank-contour` is reported; compatibility region output remains review-only. Fabrication DFM fails when no validated exact blank is available.

## Templates

The module owns three bounded reusable generators:

- `LBracket(LBracketSpec)`
- `UChannel(UChannelSpec)`
- `FourWallTray(FourWallTraySpec)`

Their policy parameters include thickness, inside radius, K-factor, material, corner policy, and relief policy. Expansion emits ordinary Sheet Metal declarations, so the normal lowering, DFM, Concept Paths, correspondence, STEP, and SVG paths remain authoritative. Template specialization changes dimensions without changing path shape (`Base.Front`, `Front.Outer`, `Front.Bend`, and flat counterparts).

## DFM and manufacturing model

M4 validates exact blank/relief topology, relief width/depth, inside-radius ratio, minimum flange length, cut-to-bend distance, cut-to-edge distance, overlap, finite coordinates, loop closure, winding, zero-width regions, duplicate cuts, and bend-line containment. Deterministic findings include suggested numeric repairs but never auto-apply them.

Flat bend lines carry semantic bend ID, exact 2D endpoints, direction, angle, inside radius, thickness, K-factor, and bend allowance. SVG uses semantic IDs and separate groups for sheet regions, exact blank, cuts, corner reliefs, and bend lines. A future DXF writer can serialize cut, bend-up, bend-down, and mark layers from the flat IR without reconstructing geometry.

## Commands and fixtures

```text
aetheris build part.firmament --output part-formed.step
aetheris sheetmetal flatten part.firmament --step part-flat.step --svg part-flat.svg
aetheris sheetmetal inspect part.firmament --json
aetheris sheetmetal paths part.firmament
aetheris sheetmetal recover imported.step --out-dir recovery
aetheris sheetmetal compare imported.step reconstructed.firmament
```

Canonical M4 dogfood is [`m4-psu-enclosure.firmament`](../../fixtures/FirmamentV2/SheetMetal/m4-psu-enclosure.firmament). The [M4 evidence bundle](sheetmetal/artifacts/m4/README.md) records exact-kernel scope, template/DFM evidence, CTC-03 comparison, timings, hashes, and remaining limits.

The [M8 CTC-03 evidence bundle](sheetmetal/artifacts/m8/README.md) records full opening recovery, semantic-layout authoring, independent regeneration, comparison, PMI evidence, generalization, timings, and the remaining outer-contour blocker.

## Bounded capability verdict

M4 can generate production-like exact blanks for common line/arc brackets, channels, open/miter trays, nested lips, cuts, and individual rectangular/round relief cases without generic BRep Boolean. It is not yet commercial sheet-metal parity: the largest blocker is robust simultaneous multi-corner relief/seam resolution—including exact formed curved relief walls and hostile historical trim reconstruction—without arrangement ambiguity.
