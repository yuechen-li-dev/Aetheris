# Concept-directed Fixed seating

Author the layout first, declare which published seating ports conform to its
plane, then realize each member at a complete frame on that plane. The product
tree remains containment; datum geometry is semantic design authority, not a
synthetic Part. No simultaneous constraint search is introduced.

```firmament
Concept Struct Layout {
 Plane Deck { Origin: [0mm,0mm,53mm]; Normal: [0,0,1]; Up: [0,1,0] }
 DatumFrame BridgeSeat { On: Deck; At: [0mm,-35mm]; X: [1,0] }
}
Interface<Fixed> DeckSeat {
 Datum: Layout.Deck
 Members: [Hardware.Bridge.BottomSeat]
}
Assembly Hardware {
 <Assembly Hardware><Part Bridge = Panel<L:82mm,W:12mm,H:10mm,R:5mm>></Part></Assembly>
 Anchor: Hardware;
 Mate BridgeOnDeck: DeckSeat {
  Member: Hardware.Bridge.BottomSeat
  At: Layout.BridgeSeat
  Orientation: SameDirection
 }
}
```

`BottomSeat` is definition-owned and publishes a `Frame`. The Interface names the
common Concept plane; the Mate names one member and its complete target frame.
The compiler validates membership and derives a placement through the existing
frame-authoring path. The occurrence cannot also declare a Placement or another
datum Mate. Inspection retains the datum contract, per-occurrence DatumSeat,
placement constraints and material-seat evidence.

The initial syntax admits top-level Concept Struct layouts with finite literal
Plane Origin/Normal/Up and DatumFrame On/At/X fields. Origins and At coordinates
require mm; basis vectors are dimensionless. At is a two-component coordinate
within the plane; X is a nonzero two-component direction. Orientation is
SameDirection or OpposedDirection. A bare plane is insufficient as an At target.
Each member must be realized once. Members are direct part ports within their
owning assembly; direct traversal through private subassembly children is not
admitted. Concept structures inside part definitions remain part-owned.

Planes can also derive from published frames with Offset/Clocking; see
[derived Concept frames and boundaries](concept-derivation.md) for supported
sources, dependency rules and the guitar's current shared-outline authoring.

Reusable subassemblies can share the same layout declaration, with scoped seating
contracts for their own parts. Frames are interpreted in the owning assembly's
coordinates, following the existing local-definition model. The guitar's owning
subassemblies have identity placement, so their shared deck is also one world
plane. Arbitrary datum targeting across independently moved subassemblies and
parameterized layout expressions are not qualified by this first implementation.

## Material evidence

The seating frame is checked against the transformed material body: an admitted
planar face must contain the seating point on the Concept plane. This rejects a
frame declared in empty space. `Support: true` marks a material support member;
other seats using the same named datum are also checked against its planar face
at their seating points in world coordinates.

Plane coincidence is analytic. Straight-edged face inclusion uses the kernel's
planar domain classifier. Curved trim inclusion uses existing bounded boundary
tessellation, so that inclusion check is approximate and retains the material
face identity. Bounding-box inclusion never proves seating. This is point-contact
evidence, not a manufacturing claim of full-footprint support, machining tolerance
or screw engagement. Existing mate residual and solid-interference checks remain.

## Guitar migration

[`hardware-layout.firmament`](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/hardware-layout.firmament)
owns HardwareDeck and the body, pickup and bridge target frames. HardwareDeck now
derives from CarvedMaple's published top-section frame, whose authored height is 53mm.
Body, pickup and bridge subassemblies reference that plane through scoped Fixed
contracts. Their seating ports are published by the part definitions.

The bridge previously began at 57mm, leaving a 4mm gap above the 53mm deck. It now
begins at 53mm and its simplified panel is 10mm tall, preserving its 63mm top and
the existing saddles/string layout. The carved body's final two sections broaden
the flat crown to provide the intended hardware support while retaining the
carved perimeter. All four material seats pass, including hardware contact with
the body support. No geometry is supplied by the renderer.

Regression coverage includes common-datum motion with cached part reuse, complete
frame requirements, membership, orientation/basis checks, competing placement
authorities, false material ports, support misses, and the guitar's bridge bounds.

## Qualification and artifacts

The Release solution build, fast core lane (1,004 tests), and full serial solution
lane (4,061 tests) passed. Fresh CLI inspection reports all four material seats
passing with zero plane residual. Moving only the Concept deck in the regression
reuses the two part geometry definitions and recomputes their assembly placement.

AP242 export and CLI reimport succeeded with 126 occurrences excluding the root.
Export reports 60 definitions (53 geometry and seven assembly definitions);
reimport reports 53 geometry definitions and 35 subassembly occurrences.
USD export and Blender Cycles rendering also succeeded. The refreshed hero and
close side image were visually inspected; the side image shows bridge contact
with the body. This change did not repeat browser camera-interaction qualification.

Generated evidence is under ignored `artifacts/local/guitar-datum-seating/`:
`inspection.json`, `guitar.step`, `step-export.json`, `step-reimport.json`,
`guitar.usda`, `guitar-studio.blend`, `hero.png`, and `deck-side.png`.
Build/test logs are `artifacts/local/pickup-features/datum-closeout-{build,fast,full}.log`.
The fresh inspection measured 8.879 seconds of definition materialization and
0.978 seconds of geometry execution/validation; this is a single uncached run,
not an incremental benchmark. The 1,800 by 2,200 hero took 206.7 seconds at
64 Cycles samples on this host.
