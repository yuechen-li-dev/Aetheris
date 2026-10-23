# 4. Reuse

Real products come in families: the same plate in three thicknesses, the same
bolt pattern on every bracket. Firmament handles this at compile time. You name
your data, write a recipe once, and specialize it. When the compiler finishes,
every value is fixed and every repetition is expanded. Nothing runs at build or
load time, and nothing in the STEP file depends on a script.

If you program, these analogies help:

| Firmament | Feels like | But |
|---|---|---|
| `Record` | a struct type | only data, no methods |
| `Static` | a `const` | evaluated once, at compile time |
| `with` | a copy with changes | the original never changes |
| `Feature` | a function | returns exactly one feature |
| `Set` + `Pattern` | a map over a list | always finite, every result keeps a name |
| `Template` | a generic type | specialized by the compiler, not at run time |
| `Concept` / `Require` | a static assertion | checked before any geometry is made |

There is no `for`, no `while`, no `if` statement and no mutable state.

## The source

[`04-plate-family.firmament`](../../../fixtures/Canonical/Learn/04-plate-family.firmament):

```firmament
// Lesson 4: name the numbers, reuse the recipe, then specialize a family.
Record PlateSpec {
  width: Length
  depth: Length
  thickness: Length
  boltDiameter: Length
}

Static standard: PlateSpec = PlateSpec { width: 80mm depth: 50mm thickness: 8mm boltDiameter: 6mm }
Static heavy: PlateSpec = standard with { thickness: 12mm boltDiameter: 8mm }

Concept Positive(value: Length) => value > 0mm

Template<spec: PlateSpec> Model MountingPlate {
  units: mm

  Require positiveThickness => Positive(spec.thickness)
  Require boltsFit => spec.boltDiameter < spec.depth / 4

  // A Feature is a named, typed recipe. Calling it makes one hole.
  Feature Bolt(center: Point2, diameter: Length) -> Hole<Shaft> {
    return Hole<Shaft> {
      on: +Z
      center: center
      diameter: diameter
      end: ThroughAll
    }
  }

  // A Set keeps every entry, by name and in order.
  Static corners: Set<Point2> {
    northEast => Point2(30mm, 15mm)
    northWest => Point2(-30mm, 15mm)
    southWest => Point2(-30mm, -15mm)
    southEast => Point2(30mm, -15mm)
  }

  Box stock { size: [spec.width, spec.depth, spec.thickness] }

  Modify stock {
    Pattern bolts over corners {
      corner => Bolt(center: corner, diameter: spec.boltDiameter)
    }
  }
}

Model StandardPlate = MountingPlate<spec: standard>
```

Reading from the top:

1. **`Record PlateSpec`** declares the shape of the data. Fields are typed:
   `Length` refuses an angle or a bare number.
2. **`Static standard`** is one value of that type. **`heavy`** derives a new
   value from it with `with`; `standard` itself is unchanged.
3. **`Concept Positive`** is a reusable yes/no check over compile-time values.
4. **`Template<spec: PlateSpec> Model MountingPlate`** is a part with a typed
   parameter. It is not built until something specializes it.
5. **`Require`** states conditions the parameters must meet. They are checked
   before any geometry exists.
6. **`Feature Bolt`** is the recipe for one hole.
7. **`Set<Point2>`** lists the bolt positions with names. `corners.northEast` is
   one of them.
8. **`Pattern bolts over corners`** applies the recipe to every entry, producing
   four holes named after their keys.
9. **`Model StandardPlate = MountingPlate<spec: standard>`** is the actual part.

Build it like any other part:

```sh
aetheris build fixtures/Canonical/Learn/04-plate-family.firmament --output artifacts/local/plate-family.step
```

## Change one line

Specialize with `heavy` instead:

```firmament
Model StandardPlate = MountingPlate<spec: heavy>
```

`aetheris measure` now reports a 12 mm thick plate with 8 mm holes. Then set
`boltDiameter: 20mm` in `heavy` and validate:

```text
fatal firmament-template-require-failed:StandardPlate.boltsFit:20mm < 50mm / 4:...
```

The requirement names itself and shows the comparison that failed. No geometry
was attempted.

## Why named entries matter

Because each Set entry has a name, the four holes are `bolts.northEast`,
`bolts.northWest` and so on. If you insert a fifth position or reorder the list,
the existing holes keep their names, and so do PMI or other references that
point at them. Plain arrays are also available when order alone is enough; their
results are numbered by position instead.

## Choosing one option

To pick one of several values, declare the options and `match` on them:

```firmament
Model ConfiguredPlate {
  units: mm
  Options SizeChoice { Compact Standard }
  Static size: SizeChoice = Compact
  Static width: Length = match size {
    Compact => 40mm
    Standard => 80mm
  }
  Box stock { size: [width, 20mm, 5mm] }
}
```

Every option must be handled exactly once. There is no default arm, so adding a
new option is an error until you decide what it means everywhere it is used.

## Try it

- Add a fifth entry, `center => Point2(0mm, 0mm)`, to `corners`.
- Give `Bolt` a default: `diameter: Length = 6mm`, and drop the argument.
- Specialize a second part, `Model HeavyPlate = MountingPlate<spec: heavy>`, in
  its own file.

More: [Templates](../firmament/templates.md), [Patterns](../firmament/patterns.md)
and [compile-time data](../reference/language/data-and-choices.md).

Next: [5. Requirements and checks](05-requirements.md).
