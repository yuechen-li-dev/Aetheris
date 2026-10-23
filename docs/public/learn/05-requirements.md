# 5. Requirements and checks

A drawing says more than shape. It says which dimensions matter, how far they
may vary, and which faces everything is measured from. This lesson attaches that
information to the part and makes the build check its own output.

## The source

[`05-checked-plate.firmament`](../../../fixtures/Canonical/Learn/05-checked-plate.firmament):

```firmament
// Lesson 5: state the requirement, publish it as PMI, and check the result.
Model CheckedPlate {
  units: mm

  Static boreDiameter: Length = 8mm tol +0.10mm -0.05mm

  Box stock { size: [40mm, 30mm, 8mm] }

  Modify stock {
    Hole<Shaft> bore {
      on: +Z
      center: Point2(0mm, 0mm)
      diameter: boreDiameter
      end: ThroughAll
    }
  }

  // PMI travels with the STEP file as AP242 semantic annotation.
  Pmi {
    Datum A { target: face(+Z) }
    HoleDiameter boreSize { target: bore; dimension: boreDiameter }
  }

  // Assertions fail the build if the exported body is not what you meant.
  Assert Bounds stock {
    min: [-20mm, -15mm, 0mm]
    max: [20mm, 15mm, 8mm]
    tolerance: 0mm
  }
  Assert Topology stock { faces: 7; edges: 15; vertices: 12; shells: 1 }
}
```

Three separate ideas are at work here.

## 1. Tolerances are part of the value

`8mm tol +0.10mm -0.05mm` is a nominal 8 mm that may run 0.10 mm over or
0.05 mm under. `tol 0.1mm` alone means ±0.1 mm. Geometry is built at the
nominal size; the allowance travels with the value through Records, Template
parameters and Feature arguments.

Declaring a tolerance does not, by itself, put anything on a drawing, and it
never loosens the kernel's geometric precision. Arithmetic on a toleranced value
produces a plain nominal value and warns you, because the compiler cannot guess
what the combined tolerance should be.

## 2. PMI publishes requirements in STEP

The `Pmi` block chooses what to publish. `Datum A` marks the top face as a
reference. `HoleDiameter boreSize` attaches the bore's toleranced diameter to the
actual hole wall. Both are written as AP242 semantic PMI, so a downstream tool
reading the STEP sees a dimension tied to a face, not a picture of text.

`build --json` reports what was emitted under `pmiExportEvidence`: here one
datum and one diameter. A PMI request the exporter cannot represent is reported
as deferred rather than dropped. See [PMI](../firmament/pmi.md) for the
supported annotations.

## 3. Assertions check the result

`Assert` blocks describe the exported body: its bounds, its topology, or its
volume. `validate` and `build` both evaluate them, and a failed or unverifiable
assertion fails the build. Change `faces: 7` to `faces: 8` and build:

```text
fatal firmament-v2-assert-topology-failed: target=stock; Faces: expected=8, measured=7; ...
```

You rarely need to write assertions by hand. `aetheris measure` prints them for
the part as it is now; paste the ones that express your intent. Keep in mind
what each one protects:

| Assertion | Protects against | Note |
|---|---|---|
| `Assert Bounds` | the part moving or changing size | exact for supported stock shapes |
| `Assert Volume` | material quietly appearing or disappearing | uses an exact method or a guaranteed interval |
| `Assert Topology` | a feature being merged, split or skipped | counts faces, edges, vertices and shells |

Topology counts are strict, which is the point: if a chamfer stops being built,
the face count changes. Use them where the topology itself is the contract.

## Try it

- Run `aetheris build ... --json` and find `pmiExportEvidence` and
  `geometryAssertions` in the output.
- Change the bore to `tol 0.02mm` and rebuild. The geometry does not change; the
  published diameter does.
- Add `Assert Volume stock { expected: ...; tolerance: 0mm^3 }` using the value
  from `measure`.

More: [PMI](../firmament/pmi.md), [checks and measurement](../reference/language/checks.md).

Next: [6. Assemblies](06-assemblies.md).
