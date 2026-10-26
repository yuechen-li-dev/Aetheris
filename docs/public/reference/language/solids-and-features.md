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
patterns. `Modify Base {}` with no features is a valid no-op, as long as `Base`
exists.

Angular mold `Draft` is not currently a V2 `Modify` feature. Its bounded legacy
operation does not imply support on the modern authoring path.

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

In a Compose body, all three variants are supported on the `+Z` support with
`end: ThroughAll`:

- The entry is found from the material at the hole's centre, so a taller boss
  elsewhere does not lift it. A pocket floor can be an entry only if the whole
  mouth fits on that floor.
- The entry footprint must lie entirely in material, including around pockets and
  slots. Overlapping or tangent entries and breakouts through a wall reject.
- A counterbore or countersink must leave a positive length of plain shaft below
  it. A countersink is an exact cone.
- Holes on other supports of a Compose body, and blind counterbores or
  countersinks there, are not supported.

## Edge finishes

`EdgeFinish<Chamfer>` takes `distance`; `EdgeFinish<Fillet>` takes `radius`.
`kind: Chamfer` and `kind: Fillet` are older spellings; if both a variant and
`kind` are given, they must agree. A bare `Fillet name { ... }` is not an edge
finish.

What you can select depends on the body:

| Body | Selection |
| --- | --- |
| A Box | `face: +Z` with `target: Boundary`: the top rim |
| An extruded Profile | `target: section.Outer` with `on: Top` or `on: Bottom`: the whole rim |
| A Compose body | `target: profile.Outer` with `on: Top`: that Profile's rim, on its own support |
| An untouched Box or `Polyhedron<N>` | `target: AllEdges`, fillet only: every edge |

The details that trip people up:

- **One finish per construction.** Several finish requests on one Profile or
  Compose body reject, rather than building only the first. Uncalled Feature and
  Template definitions do not count.
- **Box rim with holes.** One Box top-rim chamfer or fillet can sit with top
  `ThroughAll` holes in the same Modify, if the hole entries stay strictly inside
  the inset top face. Blind and side holes sharing a rim finish are not supported.
  The fillet keeps sharp vertical corners; it is not whole-box rounding.
- **Compose with holes.** A top-rim chamfer can coexist with pockets and holes as
  long as its corridor does not cross a counterbore shoulder or countersink
  transition. Fillets on Compose bodies are rejected.
- **Whole-stock rounding.** `target: AllEdges` needs one fillet, a radius smaller
  than the stock's inradius, and no other edits. It rejects on every other body,
  including Ellipsoid, RoundedBox, imported, hollow and already-edited stock, and
  it rejects an all-edge chamfer.
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
