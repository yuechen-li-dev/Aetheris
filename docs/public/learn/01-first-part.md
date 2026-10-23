# 1. A first part

You will build a 80 × 50 × 8 mm plate with one 8 mm hole through the middle, then
use the CLI to check it, build it and look at what came out.

## The source

[`01-plate.firmament`](../../../fixtures/Canonical/Learn/01-plate.firmament):

```firmament
// Lesson 1: a plate with one through hole.
Model Plate {
  units: mm

  Box stock { size: [80mm, 50mm, 8mm] }

  Modify stock {
    Hole<Shaft> mount {
      on: +Z
      center: Point2(0mm, 0mm)
      diameter: 8mm
      end: ThroughAll
    }
  }
}
```

Line by line:

- `Model Plate { ... }` declares one part named `Plate`. One file can hold one or
  more declarations; a standalone part is a `Model`.
- `units: mm` sets the part's length unit. You still write units on every value.
- `Box stock { size: [...] }` creates the material body and names it `stock`.
  A Box is centred on X and Y and sits on the XY plane, so this one runs from
  Z = 0 to Z = 8 mm.
- `Modify stock { ... }` applies features to that body.
- `Hole<Shaft> mount` is a plain drilled hole named `mount`. The `<Shaft>` part
  picks the hole variant; lesson 3 uses the others.
  - `on: +Z` drills from the top face downward.
  - `center` is a point on that face, in the face's own X/Y coordinates.
  - `end: ThroughAll` takes it all the way through.

Names matter. `stock` and `mount` show up in reports, in STEP and in later
references such as PMI. Pick names you would use when talking about the part.

## Validate

```sh
aetheris validate fixtures/Canonical/Learn/01-plate.firmament
```

```text
Firmament V2 validation: valid (0 fatal, 0 warning)
```

`validate` parses the file, checks every name, field and unit, and confirms the
compiler can admit each request. Add `--json` for a machine-readable report.

To see what a mistake looks like, misspell `diameter` as `diamter` and validate
again:

```text
plate.firmament(11,7): fatal firmament-v2-canonical-field-unknown:Hole:diamter: Unknown field 'diamter' in Hole. Check this construct's documented fields.
```

Each diagnostic gives a file, line and column, a stable code you can search for,
and a message. The real error is usually the most specific one; here, the
unknown field. Summary diagnostics such as `firmament-v2-parse-failed` only tell
you that compilation stopped. See [diagnostics](../reference/diagnostics.md).

## Build

```sh
aetheris build fixtures/Canonical/Learn/01-plate.firmament --output artifacts/local/plate.step
```

The output is an exact STEP AP242 file: six planar faces and one cylindrical
hole wall, with no triangles and no approximation. Any CAD tool that reads STEP
can open it. Add `--json` to get a build report listing the features that were
produced.

## Check what you built

```sh
aetheris measure fixtures/Canonical/Learn/01-plate.firmament
```

```text
Volume: 31597.876140340508 mm^3; ... method=ExactRectangularStockMinusThroughHoles; errorBound=0 mm^3
Topology: 7 faces, 15 edges, 12 vertices, 1 shells
Assert Bounds stock { min: [-40mm, -25mm, 0mm]; max: [40mm, 25mm, 8mm]; tolerance: 0mm; }
```

`measure` builds the part and reports what it measured: volume, bounds and
topology, plus ready-to-paste `Assert` lines. Lesson 5 turns those into checks
that run on every build. To inspect a STEP file directly, from any source, use
`aetheris analyze artifacts/local/plate.step --json`.

With the Windows bundle, `aetheris view fixtures/Canonical/Learn/01-plate.firmament`
opens the part in Cadmata.

## Try it

- Move the hole to `Point2(25mm, 10mm)` and rebuild.
- Change `end: ThroughAll` to `end: Blind 5mm` for a 5 mm deep blind hole.
  The [hole fixtures](../../../fixtures/Canonical/Features/Holes/) show every
  form.
- Make the hole 60 mm wide, wider than the plate, and read the diagnostic: the
  compiler refuses to cut the plate into two pieces rather than export something
  you did not ask for.

Next: [2. Profiles](02-profiles.md).
