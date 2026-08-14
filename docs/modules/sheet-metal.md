# Sheet Metal Module

`Aetheris.SheetMetal` 0.4.0 adds bounded M3 source-independent construction while preserving the M1/M2 imported-recovery workflow. Clean authored or reconstructed Firmament is engineering authority; imported STEP participates only in recovery or post-generation comparison.

## Construction architecture

```text
Firmament SheetMetal
  -> SheetMetalPartIr (region / bend / corner / relief graph)
  -> explicit sheet topology lowering
  -> planar skins + analytic cylindrical skins + thickness/cut walls
  -> closed formed BRep -> AP242

SheetMetalPartIr
  -> exact graph traversal using the same bend policy
  -> planar regions + neutral-axis bend strips
  -> stitched outer blank + mapped cuts
  -> thickness-bearing flat AP242 / SVG
```

The formed path constructs known sheet topology directly. It does not union flange solids with generic `BrepBoolean`. Every authored region, bend, cut, corner, and relief has a stable formed/flat correspondence entry.

## Canonical syntax and semantics

```firmament
SheetMetal Tray {
    Thickness: 1.2mm;
    KFactor: 0.42;
    Base Main { Profile: Rectangle { Width: 240mm; Height: 180mm; }; }
    Flange Front { From: Main.Front; Height: 40mm; Angle: 90deg; Radius: 1.5mm; Direction: Up; Relief: Auto; }
    Flange Lip { From: Front.Outer; Length: 15mm; Angle: 45deg; Radius: 1.5mm; Direction: Down; }
    Cut Fan { On: Main; Profile: Circle { Diameter: 120mm; }; At: (120mm, 90mm); }
}
```

`Length` and `Height` are aliases for the canonical `TangentToEdge` dimension: distance on the resulting planar flange from the bend tangent line to its free edge. `InsideRadius`/`Radius` is the physical inside radius. `Up` rotates the parent outward direction toward its material-positive normal; `Down` rotates oppositely. Angles strictly between 0° and 180° are supported; ordinary bends retain exact plane and cylinder supports.

`KFactor` defines `neutral radius = inside radius + K * thickness` and `bend allowance = abs(angle) * neutral radius`. The formed and flat paths consume the same bend record and policy. The flat bend line is the neutral-axis centerline of the explicit bend strip.

The bounded graph supports flanges from rectangular-base `Front/Rear/Left/Right` edges and from a previously generated flange's `Outer` (`Top`) edge. Duplicate edge ownership, disconnected parents, invalid bends, and cuts reaching a region boundary/bend zone are typed rejections.

## Corners, reliefs, and cuts

Adjacent base flanges receive deterministic end trimming. `Corner: Open` is the robust baseline. `Corner: Miter` selects the smaller symmetric miter setback but remains an open, non-welded seam. `Relief: Auto`, `Rectangular`, and `Round` create typed relief intent/provenance; the bounded formed topology realizes this as bend-end/open-corner material clearance. Auto width is at least one thickness and auto depth is `inside radius + thickness`. Closed overlap/gap seams, welded corners, arbitrary corner intersections, and a freestanding shop relief library remain deferred.

Circular holes and axis-aligned rectangular/slot profile cuts are exact formed through-cuts and map to the same owning flat region. Cuts crossing bend zones are rejected rather than warped or silently split. SVG shows region material, cut contours, neutral-axis bend lines, direction, angle, and radius.

## Authored fixtures and CTC-03

- [`m3-l-bracket.firmament`](../../fixtures/FirmamentV2/SheetMetal/m3-l-bracket.firmament)
- [`m3-u-channel.firmament`](../../fixtures/FirmamentV2/SheetMetal/m3-u-channel.firmament)
- [`m3-electronics-tray.firmament`](../../fixtures/FirmamentV2/SheetMetal/m3-electronics-tray.firmament)
- [`ctc03-idiomatic.firmament`](sheetmetal/artifacts/m2/ctc03-idiomatic.firmament)

The CTC-03 source contains no `EvidenceSource`, `FromEvidence`, face IDs, or recovered polygons. It independently generates 15 semantic regions, seven exact bends, two cuts, a closed formed BRep, and a valid flat blank. Post-generation comparison classifies it `NeedsReview`: all seven bend axis/angle/radius/graph comparisons and both cuts pass, while historical local boundary trims and the accepted M2 flat outline are not yet reproduced closely enough. This is deliberately not presented as full source parity.

## Recovery remains separate

Imported STEP still follows `STEP -> RecoveredSheetMetalEvidence -> recovered draft -> engineer/LLM reconstruction`. Recovered models may retain source-edge geometry and `Partial` status. That forensic dialect is distinct from the M3 clean authored dialect and remains useful for verification.

## Commands

```text
aetheris build part.firmament --output part-formed.step
aetheris sheetmetal flatten part.firmament --step part-flat.step --svg part-flat.svg
aetheris sheetmetal recover imported.step --out-dir recovery
aetheris sheetmetal compare imported.step reconstructed.firmament
```

## Bounded capability

M3 proves common brackets, parallel channels, four-wall open-corner trays, parent-flange lips, circular/rectangular cuts, analytic cylindrical bends, deterministic correspondence, and physical formed/flat STEP. It does not claim hems, jogs, beads, lofted bends, stamping, springback, closed-corner overlap policy, universal brake sequencing, or commercial sheet-metal parity. The largest production blocker is a richer exact 2D contour/trim kernel for commercial corner seams, arbitrary profile/relief curves, and manufacturing-grade DXF/tooling workflows.

See the [M1 bundle](sheetmetal/artifacts/m1/README.md), [M2 recovery bundle](sheetmetal/artifacts/m2/README.md), and [M3 evidence](sheetmetal/artifacts/m3/README.md).
