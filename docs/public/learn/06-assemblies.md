# 6. Assemblies

An assembly is a set of parts and the relationships that place them. In
Firmament, parts publish named reference frames, the "ports" you would point at
when explaining how two parts go together, and a Mate connects two ports. The
compiler solves the placement; you never type a transformation matrix.

## The source

[`06-post-on-plate.firmament`](../../../fixtures/Canonical/Learn/06-post-on-plate.firmament):

```firmament
// Lesson 6: two parts, each publishing a mounting frame, joined by a Fixed mate.
Template<thickness: Length> Struct BasePlate {
  Profile outline = Rect2 { center: [0mm, 0mm]; size: [60mm, 40mm] }
  Extrude body { profile: outline; from: 0mm; to: thickness }
  expose {
    // Where a post sits: the centre of the top face, Z pointing up.
    Semantic Socket { DatumFrame Frame = [0mm, 0mm, thickness] x [1,0,0] y [0,1,0] z [0,0,1]; }
  }
}

Template<height: Length> Struct Post {
  Profile outline = Circle2 { center: [0mm, 0mm]; radius: 5mm }
  Extrude body { profile: outline; from: 0mm; to: height }
  expose {
    // The bottom of the post.
    Semantic Foot { DatumFrame Frame = [0mm, 0mm, 0mm] x [1,0,0] y [0,1,0] z [0,0,1]; }
  }
}

Assembly PostOnPlate {
  <Assembly PostOnPlate>
    <Part Plate = BasePlate<thickness: 6mm>></Part>
    <Part Upright = Post<height: 30mm>></Part>
  </Assembly>

  // The plate stays where it is; everything else is placed relative to it.
  anchor: PostOnPlate.Plate;

  // Fixed: put the post's Foot frame exactly on the plate's Socket frame.
  Interface<Fixed> Install { a: PostOnPlate.Plate.Socket; b: PostOnPlate.Upright.Foot; }
}
```

## Parts

`BasePlate` and `Post` are part definitions: `Template ... Struct`, built the
same way as a `Model`. Each publishes a port inside `expose`:

- `Semantic Socket` is a named engineering interface on the plate.
- `DatumFrame Frame = origin x X y Y z Z` is a full coordinate frame: an origin
  and three axes. The plate's socket sits at the top face, `thickness` above
  the origin, so it follows the template parameter.

A port is a reference, not material. Publishing it adds no faces and does not
guess at faces of the body.

## The tree

`<Assembly>` and `<Part>` tags list the occurrences. `Plate` and `Upright` are
occurrence names; `BasePlate<thickness: 6mm>` says which definition each one
uses. Two occurrences of the same specialized definition share one exact body.

Nesting in the tree only describes the product structure. It does not place
anything.

## Placement

`anchor` fixes one occurrence in place. `Interface<Fixed> Install` then says
that port `b` coincides with port `a`: origins together, axes aligned. Solve it
and the post moves up by the plate's thickness:

```sh
aetheris asm inspect fixtures/Canonical/Learn/06-post-on-plate.firmament --json
```

In `assemblyIr.placements`, `PostOnPlate.Upright` has status `Resolved` with a
translation of 6 mm in Z, derived from the mate. Build the assembly to STEP:

```sh
aetheris build fixtures/Canonical/Learn/06-post-on-plate.firmament --output artifacts/local/post-on-plate.step
```

The STEP file is an AP242 product structure: one assembly, two parts, their
placements, and the shared definitions.

This lesson uses the inline form. For a relationship you will apply more than
once, declare the contract once and apply it with named mates:

```firmament
Interface<Fixed> Seated { }
// Inside the Assembly:
Mate Install: Seated {
  A: PostOnPlate.Plate.Socket;
  B: PostOnPlate.Upright.Foot;
}
```

Besides `Fixed`, the typed interface families include `Revolute` and
`Prismatic` joints. Assemblies can also check tolerance stack-ups, reuse
subassemblies and animate joints. See [assemblies](../firmament/assemblies.md)
and the [authoring foundation](../firmament/assembly-authoring-foundation.md).

## Try it

- Change the plate to `BasePlate<thickness: 10mm>` and check that the post
  follows.
- Move the socket origin to `[20mm, 10mm, thickness]`.
- Remove the `anchor` line and read the diagnostic.

## Where to go next

You now know the core of the language. From here:

- The [language reference](../reference/language/README.md) covers each
  construct, its fields and its limits.
- The [guides](../README.md#guides) cover domains: sheet metal, FEA, STEP import,
  drawings, piping, structural members and more.
- The [canonical cookbook](../../../fixtures/Canonical/README.md) has a working
  example for nearly every supported feature.
