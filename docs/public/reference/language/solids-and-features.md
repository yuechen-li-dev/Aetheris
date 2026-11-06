# Solids and features

A mechanical Model has **one material body**. You make it from a primitive, an
extruded Profile or a `Compose`, and then apply features to it. A second body in
the same Model is rejected at its declaration, never silently discarded. Separate
objects belong in separate parts of an [assembly](assemblies-and-motion.md).

## Primitives

| Primitive | Fields | Placement |
| --- | --- | --- |
| `Box` | `size: [x, y, z]` | centred in X and Y, from Z = 0 up |
| `Cylinder` | `radius`, `height` | axis on Z, from Z = 0 up |
| `Cone`, `Frustum` | `bottomRadius`, `topRadius`, `height` | axis on Z, from Z = 0 up; `topRadius: 0mm` gives a point |
| `Sphere` | `radius` | centred on the origin |
| `Torus` | `majorRadius`, `minorRadius` | centred on the origin, axis on **Y** |
| `RoundedBox` | `size`, `cornerRadius` | centred on the origin in all three axes; vertical corners rounded |
| `Ellipsoid` | `radii: [x, y, z]` (semi-axes, not diameters) | centred on the origin |
| `Polyhedron<N>` | `edgeLength` | centred on the origin |

The placements are not uniform. In particular, `Torus` lies in the XZ plane, and
`RoundedBox` is centred where `Box` sits on the XY plane. Check bounds with
`aetheris measure` when it matters, and place parts in assemblies through frames
and mates rather than relying on primitive origins.

`Polyhedron<N>` is a regular convex solid with `N` faces: 4, 6, 8, 12 or 20.
`Polyhedron<6>` has axis-aligned faces and `Polyhedron<8>` axis-aligned
vertices. Other counts reject.

`Ellipsoid` is an exact quadric. It is exported to STEP as exact rational patches
and recovered as an ellipsoid on reimport. It is symmetric; an egg shape needs a
different construction.

## Modify

`Modify name { ... }` applies features to an existing body: holes, edge finishes,
patterns, [grooves and manual ribs](../../firmament/grooves-and-ribs.md).
`Modify Base {}` with no features is a valid no-op, as long as `Base`
exists.

On a single `Extrude`, Modify takes the same features as on a `Compose` body:
holes, slots, patterns and edge finishes. The Extrude builds as
`Compose name { Base name { ... } }`. `Perforation` is for Box and hollow
Cylinder stock; on Extrude and Compose bodies it rejects at its declaration
(use a `Pattern` of holes for a grid of openings).

Angular mold `Draft` is not currently a V2 `Modify` feature. Its bounded legacy
operation does not imply support on the modern authoring path. Manual
`Rib<Rectangular>` accepts its own explicit side `draft` while constructing the
rib; that is separate from drafting existing body faces.

## Compose

`Compose name { ... }` builds a body from a Profile-based stack of operations. It
does not reopen a separately declared Box.

| Intent | Operation | Key fields |
| --- | --- | --- |
| Starting stock | `Base` | `profile`, `from`, `to` |
| Raised material | `Boss` | `on: Top` (or `name.Top`, or a `Span<Plane>`), `profile`, `height` |
| Finite recess | `Pocket` | `on`, `profile`, `depth`, optional `minimumFloorThickness` |
| Through cut | `Pocket` with `end: ThroughAll`, or `Remove` with `role: ThroughCut` | `on`, `profile`; no `depth` |
| Capsule slot | `Slot<Capsule>` | `length`, `width`, `direction`, `extent`, `role` |
| Drilled hole | `Hole<Shaft>`, `Hole<Counterbore>`, `Hole<Countersink>` | see below |

`on: Top` resolves to the current top of the material under the feature's
footprint, after the operations before it. If two equally high supports qualify,
the feature is rejected as ambiguous; name one, for example `on: pad.Top`.

A Pocket always leaves a floor. Its required floor comes from
`minimumFloorThickness`, then from an active manufacturing policy, then from a
default, and the build fails when the floor would be thinner. Do not mix `depth`
or a floor requirement with `end: ThroughAll`.

### Coaxial primitive Compose

A coaxial `Compose` can join positive-radius `Cylinder` and `Frustum` spans without sketches or Boolean union. The first span starts at the local origin along +Z; each later span names the previous span's top support, and their joining radii must match. For example:

```firmament
Model CountersunkFastener {
  units: mm
  Compose body {
    Cylinder shaft { radius: 2.85mm; height: 12.85mm }
    Frustum head { on: shaft.Top; bottomRadius: 2.85mm; topRadius: 6mm; height: 3.15mm }
  }
  Modify body {
    EdgeFinish<Chamfer> tip { on: shaft.Bottom; target: Boundary; distance: 0.5mm }
  }
}
```

This produces exact cylindrical, conical and planar supports. A chamfer on an exposed terminal Cylinder rim removes equal axial and radial distance, giving a 45° lead-in. Its distance must be smaller than both cylinder radius and height. One chamfer per exposed end is admitted. For a reusable Record/Template and assembly seat, see the [fastener library](../../../../fixtures/Regression/CodeNativeAssembly/fastener-library.firmament).

This bounded stack rejects arbitrary placements, mismatched joining radii, radial shoulders, overlapping or disjoint spans, mixed Profile/primitive operations, internal-rim finishes, Fillet, holes and part PMI on this Compose family. It also rejects typed tolerances that it cannot project. Other Compose families retain their own admitted operations.

## Holes

| Variant | Fields |
| --- | --- |
| `Hole<Shaft>` | `on`, `center`, `diameter`, `end` |
| `Hole<Counterbore>` | adds `counterboreDiameter`, `counterboreDepth` |
| `Hole<Countersink>` | adds `countersinkDiameter`, `countersinkAngle` |

`end` is `ThroughAll` or `Blind <depth>`, for example `end: Blind 10mm`. A field
from another variant, such as `counterboreDepth` on a Shaft hole, is an error.

A blind `Hole<Shaft>` is a drilled hole. It ends in a 118° drill point, and its
depth is the full-diameter depth, excluding the point, as drawings dimension it.
`termination` changes the end:

| `termination` | End of the hole |
| --- | --- |
| omitted, or `DrillPoint` | 118° drill point |
| `DrillPoint { pointAngle: 135deg }` | drill point with that included angle |
| `Flat` | flat floor (a flat-bottom drill or end mill) |

`termination` applies to blind holes only. Blind counterbores and countersinks
have flat floors; a drill point on them is rejected. On Box stock, the top-face
drill point is built in a plane derived from the box, never from a face
reference.

### Holes from a plane

`from` replaces `on` to drill from a plane instead of a host face. Name an
explicit `Construction Plane` (Box and Compose bodies), or, on a Compose body, a
straight step of a closed Path or a named straight edge of a traced Profile's
outer loop (for example one traced with `as Front`), normally the outline the
body is built from:

```firmament
Hole<Shaft> SideDrill { from: Outline.FrontMiddle; center: Point2(-30mm, -25mm); diameter: 20mm; end: Blind 45mm }
Mirrored Feature SideDrillMirror from SideDrill { across: CenterPlane }
```

`Outline.FrontMiddle` is shorthand for the plane that step sweeps when the Path is
extruded; `Plate.Front` is the same for a Profile edge. It is computed from the Path alone, so it moves when the Path changes
and never refers to a body face. Written out, it is
`Plane { origin: <foot of the sketch origin on the step's line>; normal: <into the material>; up: [0, 0, 1] }`.
`center` is in that plane's frame: v runs along the extrusion (+Z), the hole runs
into the material, and u completes a right-handed frame. For a counter-clockwise
outline, u therefore runs against the step's direction; on a front edge stepped
along +X, u is world −X. The
[CTC-01 reconstruction](../../../../fixtures/Regression/Decompile/ctc-01.firmament)
uses this form.

Rules:

- `end` is `Blind <depth>`, `ShaftDepth(<depth>)` or `TotalDepth(<depth>)`
  (depth including the point). The hole ends in a drill point; `termination:
  Flat` from a plane is rejected.
- The mouth must lie wholly inside one planar side face of the body, which must
  coincide with the plane. Coplanar faces separated by notches are told apart by
  their own boundaries.
- On Box stock the plane's axes must be signed world axes. On a Compose body the
  drill direction is ±X or ±Y. The full-radius tool
  envelope through the point must stay in material, and envelopes of different
  drills must not touch.
- A Path-step plane needs a straight `Line` step of a closed Path in the XY
  sketch; a Profile-edge plane needs a straight, named outer-loop edge of a
  Profile in the XY sketch.

In a Compose body, all three variants are supported on the `+Z` support with
`end: ThroughAll`:

- The entry is found from the material at the hole's centre, so a taller boss
  elsewhere does not lift it. A pocket floor can be an entry only if the whole
  mouth fits on that floor.
- The entry footprint must lie entirely in material, including around pockets and
  slots. Overlapping or tangent entries and breakouts through a wall reject.
- A counterbore or countersink must leave a positive length of plain shaft below
  it. A countersink is an exact cone.
- Blind holes on `+Z` of a Compose body, and holes on its other faces through
  `on`, are not supported; drill side holes with `from`.

## Edge finishes

`EdgeFinish<Chamfer>` takes `distance`; `EdgeFinish<Fillet>` takes `radius`.
`kind: Chamfer` and `kind: Fillet` are older spellings; if both a variant and
`kind` are given, they must agree. A bare `Fillet name { ... }` is not an edge
finish.

An edge finish works on the edges around one face. To round the vertical edges
of a plate or block, round the corners of its outline instead; the result is
exact and holes still apply:

```firmament
Model Plate {
  units: mm
  Profile Outline = RoundedRect2 { center: [0mm, 0mm]; size: [50mm, 40mm]; radius: 3mm }
  Extrude Plate { profile: Outline; from: 0mm; to: 8mm }
  Modify Plate {
    Hole<Shaft> Corner { on: +Z; center: Point2(19mm, 14mm); diameter: 5.5mm; end: ThroughAll }
  }
}
```

### Fillets where two operations meet

`where: A meets B` selects the edges where the faces of two Compose operations
meet, and rounds them with one ball:

```text
EdgeFinish<Fillet> HexFoot { where: Hex meets Stock; radius: 5mm }
```

[`hex-boss-foot.firmament`](../../../../fixtures/Regression/JunctionFillet/hex-boss-foot.firmament)
rounds a hexagonal boss's foot this way.

- `A` and `B` name Compose operations (`Base`, `Add`, `Boss`, `Pocket`), or
  `role Name` for every operation with that role (`where: role Pad meets Plate`).
- A face belongs to an operation when it lies on that operation's prism: a cap
  inside its profile at one of its heights, or a side on its outline. An edge is
  selected when one of its faces belongs to `A` but not `B` and the other to `B`
  but not `A`. A wall that two operations share, such as a pad flush with the
  plate's side, selects nothing.
- Straight edges become exact cylinders and circular ones tori. Where two rounded
  edges on one face turn a corner and the edge between their walls stands square
  to that face, as at a boss's foot, they meet in an exact quarter-ellipse mitre.
  A rounded edge may also end on a face standing square to it.
- Several `where` finishes on one body are built together, each with its own
  radius, after the body's other construction (holes and another finish
  included). Edges of different radii must not meet.
- Rejected: `where` on a chamfer; operations that are not on XY along +Z; names
  that are not operations or roles of the Compose body; a junction with no edges;
  a rounded edge that ends where its face's edge runs on tangentially into a
  curved wall, or at a corner of convex and concave edges other than a mitre; and
  a radius that does not fit, which is refused with the largest that does.

What you can select depends on the body:

| Body | Selection |
| --- | --- |
| An unmodified Box | `face: +X`, `-X`, `+Y`, `-Y`, `+Z` or `-Z` with `target: Boundary`: that face's rim |
| An extruded Profile | `target: section.Outer` or `Boundary` with `on: Top` or `on: Bottom`: the whole rim |
| A Compose body | `target: profile.Outer` with `on: Top` or `on: Bottom`, within the qualified material history below |
| An untouched Box or `Polyhedron<N>`, or any body of plane, cylinder and cone faces | `target: AllEdges`, fillet only: every edge |

The details that trip people up:

- **One finish per construction.** Several finish requests on one Profile or
  Compose body reject, rather than building only the first. Uncalled Feature and
  Template definitions do not count. `where` fillets are the exception: any number
  of them can sit alongside one other finish.
- **Box rim with holes.** One Box top-rim chamfer or fillet can sit with top
  `ThroughAll` holes in the same Modify, if the hole entries stay strictly inside
  the inset top face. Blind and side holes sharing a rim finish are not supported.
  The fillet keeps sharp vertical corners; it is not whole-box rounding.
- **Compose with holes.** A top-rim chamfer can coexist with pockets and holes as
  long as its corridor does not cross a counterbore shoulder or countersink
  transition. One constant-stock Base with disjoint through round holes also
  supports either cap's fillet or chamfer, including rounded Profile corners.
  This cavity-retaining path currently requires native XY stock, a single outer
  loop and positive clearance between each entry footprint and the rim corridor.
  Blind holes, slots and additional material operations reject in that path.
- **Whole-body rounding.** `target: AllEdges` needs one fillet. On Box and
  Polyhedron stock the radius must be below the stock's inradius. On any other
  body, the body is built without the finish and every edge of the result is
  rounded: each straight edge becomes a cylinder strip, each circular edge (a
  cylinder's rim, a hole's rim, an arc in an extruded profile) a torus, and each
  corner a sphere. Faces may be planes, cylinders and cones. Convex edges are
  rounded off; concave edges (a shoulder, a counterbore's step, a blind hole's
  floor) are filled in with the same radius. Where flat and curved walls meet
  tangentially (a rounded-rectangle extrusion) the strips run on into each
  other. Where one concave edge meets convex ones (the inner corner of an
  L-shaped extrusion) the concave edge is filled first and then rounded over,
  as fillets before rounds. On convex bodies bounded by planes, corners where
  four or more faces meet unevenly open into short strips, as a rolled ball
  would leave them; on curved bodies each corner must join three edges. Not
  supported yet: spline or elliptical edges (common on imported parts and on
  planes cut at an angle through cylinders), corners where two concave edges
  meet a convex one (a boss's foot corner), and faces split in two along one
  plane (some compositions leave a pocketed plate's walls split at the pocket's
  depth); each is refused at the finish, naming the edges, corner or faces
  found. A radius that does not fit, for
  instance one whose ball would reach a nearby hole or a small hole's axis, is
  refused with the largest that does. Assertions measure the rounded body. An
  all-edge chamfer is refused.
- **Size.** A finish must leave a non-collapsing cap. Straight polygon corners
  need not be right angles; tangent line/arc rims and arcs closed by a chord also
  work. `endClearance` is for open chains and rejects on closed loops.

The [fillet and chamfer guide](../../firmament/fillet-profiles.md) has
executable examples and explains how the planner chooses a construction.

## Combinations

That a feature exists does not mean every combination with other features is
supported. The [geometry guide](../../firmament/geometry.md) and
[supported features](../supported-features.md) list what combines. An
unsupported combination always rejects with a diagnostic; remaining geometry that
looks plausible is never evidence that everything you asked for was built.

## Regions and imported geometry

`Region` is not a general declaration. It appears in three bounded places:

- **Older mechanical source** has a lowercase
  `region sideHole on face(+X) { cut Cylinder { ... } }` inside `modify`. Use
  `Hole<Shaft>` in new source.
- **Sheet metal** names planar regions, and **FEA** selects `Region:` fields.
  These belong to their domains; see [sheet metal](../../firmament/sheet-metal.md)
  and [FEA](../../firmament/fea.md).
- **Imported STEP revisions.** `Recognize` names a region of an imported body.
  The supported workflow moves or removes one proved, complete, round through-hole
  in an imported part while keeping the rest of the BRep:
  - pin the STEP file with `sha256` (exact bytes) or `textSha256` (UTF-8, LF
    normalized);
  - select the hole by inspected candidate, for example
    `candidate: "through-hole:step-764+765"`. `aetheris imported-features inspect`
    lists candidates, and `plan --as MountHole` writes a complete revision;
  - `Replace ... with Hole<Shaft>` moves it within explicit `within` bounds (one
    box, or `[sourceBounds, destinationBounds]`), and `Remove Source.Name { within: ... }`
    deletes it;
  - `Source.Name.diameter`, `.depth`, `.center`, `.entry`, `.exit` and their
    `.X`/`.Y`/`.Z` components can be used in the edit;
  - `geometryOnly: true` is required, and excluded source annotations are
    reported.

  Limits: one move or removal per Model, along X, Y or Z, same size, between
  parallel planar faces. Slots and countersinks cannot be edited. A partial wall
  is never promoted to a complete hole. `importTolerance` controls how imported
  geometry is qualified and has nothing to do with a manufacturing `tol`. See
  [imported through-hole revisions](../../firmament/imported-feature-recognition.md).

Manual rib junctions and their admitted combinations with holes and perimeter
finishes are described in [Grooves and ribs](../../firmament/grooves-and-ribs.md#rib-junctions-and-mixed-features).
